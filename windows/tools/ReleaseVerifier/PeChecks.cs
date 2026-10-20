namespace Mitosis.ReleaseVerifier;

using System.Buffers.Binary;

/// <summary>Read-only PE-header inspection of the published executable, enough to
/// verify the two properties the distribution must promise: the code is x64 and
/// an application icon is embedded (RT_GROUP_ICON/RT_ICON resources). Works on
/// in-memory or on-disk bytes.
///
/// The parser treats the bytes as hostile: every field read is bounds-checked in
/// long arithmetic (no unchecked casts, no uint wrap-around), every malformed
/// structure — truncated header, bad magic, section table beyond the file,
/// resource directory pointing outside the file — surfaces as
/// MissingWindowsHeader, which the verifier reports as a finding rather than an
/// uncontrolled crash.</summary>
public static class PeChecks
{
    /// <summary>COFF machine codes the verifier distinguishes.</summary>
    public const ushort MachineX64 = 0x8664;
    public const ushort MachineX86 = 0x014C;
    public const ushort MachineArm64 = 0xAA64;

    public static ushort ReadMachine(ReadOnlySpan<byte> file)
    {
        var peOffset = ReadPeHeaderOffset(file);
        // COFF header begins at peOffset+4; Machine is its second field. The
        // header-offset check already guarantees peOffset+6 bytes exist.
        return BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(peOffset + 4, 2));
    }

    /// <summary>True when the executable embeds icon resources (type 3 = RT_ICON,
    /// type 14 = RT_GROUP_ICON entries in the root resource directory). Absence
    /// is a release finding — Explorer, the taskbar, and shortcuts would show
    /// the generic exe glyph.</summary>
    public static bool HasIconGroup(ReadOnlySpan<byte> file)
    {
        var peOffset = ReadPeHeaderOffset(file);
        var optionalOffset = peOffset + 24;
        if (!InBounds(file, optionalOffset, 2))
        {
            throw new MissingWindowsHeader("optional header is beyond the file");
        }
        var magic = BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(optionalOffset, 2));
        // Optional header: magic first. Data directories begin at +96 (PE32,
        // magic 0x10B) or +112 (PE32+, magic 0x20B); index 2, the resource
        // directory, sits two 8-byte entries in. Anything else is not a layout
        // this parser can honestly interpret — say so instead of guessing.
        var directoryBase = magic switch
        {
            0x10B => optionalOffset + 96,
            0x20B => optionalOffset + 112,
            _ => throw new MissingWindowsHeader(
                $"unexpected optional header magic 0x{magic:X4}"),
        };
        if (!InBounds(file, directoryBase, 16 + 4))
        {
            throw new MissingWindowsHeader("resource data directory is beyond the file");
        }
        var resourceRva = BinaryPrimitives.ReadUInt32LittleEndian(
            file.Slice(directoryBase + 16, 4));
        if (resourceRva == 0)
        {
            return false;
        }
        var resourceDirectory = SectionTable.From(file, peOffset)
            .RvaToOffsetOrThrow(resourceRva);
        return ResourceDirectory.ContainsTypeId(file, resourceDirectory, 3)
            || ResourceDirectory.ContainsTypeId(file, resourceDirectory, 14);
    }

    /// <summary>The PE signature offset from the DOS header, validated: DOS
    /// header present, stored offset fits the file (in long arithmetic — a
    /// near-int-max value must not wrap the comparison), and the PE signature
    /// really is there.</summary>
    public static int ReadPeHeaderOffset(ReadOnlySpan<byte> file)
    {
        if (file.Length < 0x40 || file[0] != (byte)'M' || file[1] != (byte)'Z')
        {
            throw new MissingWindowsHeader("no DOS header");
        }
        var peOffset = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(0x3C, 4));
        // peOffset + 6 could wrap negative for a near-max attacker value, so
        // the bound is written in long arithmetic and without overflow.
        if (peOffset < 0 || peOffset + 6L > file.Length ||
            BinaryPrimitives.ReadUInt32LittleEndian(file.Slice(peOffset, 4)) != 0x00004550)
        {
            throw new MissingWindowsHeader("no PE signature");
        }
        return peOffset;
    }

    /// <summary>Offset/length bound check in long arithmetic; the tails of
    /// malformed or adversarial files must fail here, never in Slice.</summary>
    internal static bool InBounds(ReadOnlySpan<byte> file, long offset, long count) =>
        offset >= 0 && count >= 0 && offset <= file.Length - count;

    public sealed class MissingWindowsHeader(string message) : Exception(message);
}

/// <summary>The COFF section table with the one operation the resource walk
/// needs: translating an RVA to a raw file offset through the single containing
/// section. Every read is bounds-checked; malformed tables throw.</summary>
internal readonly record struct SectionTable
{
    private readonly (uint VirtualAddress, uint VirtualSize, uint RawSize, uint RawPointer)[] entries;

    private SectionTable((uint, uint, uint, uint)[] entries) => this.entries = entries;

    /// <summary>Reads the section table chosen by <paramref name="peOffset"/>.
    /// Requires the caller to have validated the DOS/PE header (bounds and
    /// signature) and guarantees peOffset+22 bytes exist before reading the
    /// COFF fields it depends on.</summary>
    public static SectionTable From(ReadOnlySpan<byte> file, int peOffset)
    {
        if (!PeChecks.InBounds(file, peOffset, 24))
        {
            throw new PeChecks.MissingWindowsHeader("COFF header truncated");
        }
        var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(peOffset + 6, 2));
        var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(peOffset + 20, 2));
        long tableOffset = peOffset + 24 + (long)optionalSize;
        // Multiplied size in long first: a section count of 65535 with an
        // oversized optional header must fail a comparison, not wrap.
        if (tableOffset + 40L * sectionCount > file.Length)
        {
            throw new PeChecks.MissingWindowsHeader("section table truncated");
        }
        var entries = new (uint, uint, uint, uint)[sectionCount];
        for (var i = 0; i < sectionCount; i++)
        {
            var offset = tableOffset + 40 * i;
            // Per section entry: Name(8) VirtualSize(+8) VirtualAddress(+12)
            // SizeOfRawData(+16) PointerToRawData(+20). The tuple below writes
            // reads positionally into the declared element names, so the reads
            // are ordered to match those names, not the PE field layout.
            entries[i] = (
                BinaryPrimitives.ReadUInt32LittleEndian(file.Slice((int)offset + 12, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(file.Slice((int)offset + 8, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(file.Slice((int)offset + 16, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(file.Slice((int)offset + 20, 4)));
        }
        return new SectionTable(entries);
    }

    /// <summary>The translated file offset of a resource RVA, or null when the
    /// RVA does not resolve inside a mapped section range.</summary>
    public uint? RvaToOffset(uint rva)
    {
        foreach (var entry in entries)
        {
            var size = entry.VirtualSize != 0 ? entry.VirtualSize : entry.RawSize;
            // Long arithmetic: a VA near uint.MaxValue plus a large size must
            // not wrap into a spuriously matching range.
            if (rva >= entry.VirtualAddress && rva < entry.VirtualAddress + size)
            {
                return rva - entry.VirtualAddress + entry.RawPointer;
            }
        }
        return null;
    }

    /// <summary>Throws the verifier's malformed-file exception when the RVA
    /// cannot be resolved to real bytes in the file; the bounds check that
    /// decides pass/fail lives in ResourceDirectory, this only certifies the
    /// translation happened.</summary>
    public long RvaToOffsetOrThrow(uint rva) =>
        RvaToOffset(rva) is { } offset
            ? offset
            : throw new PeChecks.MissingWindowsHeader(
                $"resource directory RVA 0x{rva:X} does not resolve inside a section");
}

/// <summary>Reads IMAGE_RESOURCE_DIRECTORY entries; only the first level (the
/// type ids) is ever consulted. The directory may legitimately point near the
/// end of the file — every read is long-arithmetic bounds-checked so a pointing
/// past-the-end directory reports "absent resources" rather than crashing.</summary>
internal static class ResourceDirectory
{
    public static bool ContainsTypeId(ReadOnlySpan<byte> file, long directoryOffset, uint typeId)
    {
        // A 16-byte directory header plus full 8-byte entries must all fit.
        if (!PeChecks.InBounds(file, directoryOffset, 16))
        {
            return false;
        }
        var nameEntries = BinaryPrimitives.ReadUInt16LittleEndian(
            file.Slice((int)directoryOffset + 12, 2));
        var idEntries = BinaryPrimitives.ReadUInt16LittleEndian(
            file.Slice((int)directoryOffset + 14, 2));
        var entryCount = nameEntries + idEntries;
        long entryOffset = directoryOffset + 16;
        // Entry count is at most 65535, so the loop is bounded even on
        // fully synthetic tables; the per-entry bounds check never lets a
        // read escape the buffer.
        for (var i = 0; i < entryCount; i++)
        {
            if (!PeChecks.InBounds(file, entryOffset, 8))
            {
                return false;
            }
            var id = BinaryPrimitives.ReadUInt32LittleEndian(file.Slice((int)entryOffset, 4));
            if (id == typeId)
            {
                return true;
            }
            entryOffset += 8;
        }
        return false;
    }
}
