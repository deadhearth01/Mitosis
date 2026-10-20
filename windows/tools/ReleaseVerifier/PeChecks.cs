namespace Mitosis.ReleaseVerifier;

using System.Buffers.Binary;

/// <summary>Read-only PE-header inspection of the published executable, enough to
/// verify the two properties the distribution must promise: the code is x64 and
/// an application icon is embedded (RT_GROUP_ICON/RT_ICON resources). Works on
/// in-memory or on-disk bytes; throws MissingWindowsHeader when the file is not
/// a PE at all, which the verifier turns into a finding.</summary>
public static class PeChecks
{
    /// <summary>COFF machine codes the verifier distinguishes.</summary>
    public const ushort MachineX64 = 0x8664;
    public const ushort MachineX86 = 0x014C;
    public const ushort MachineArm64 = 0xAA64;

    public static ushort ReadMachine(ReadOnlySpan<byte> file)
    {
        var peOffset = ReadPeHeaderOffset(file);
        // COFF header begins at peOffset+4; Machine is its second field.
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
        if (optionalOffset + 2 > file.Length)
        {
            throw new MissingWindowsHeader("optional header truncated");
        }
        var magic = BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(optionalOffset, 2));
        // Optional header: magic first. Data directories begin at +96 (PE32,
        // magic 0x10B) or +112 (PE32+, magic 0x20B); index 2 is the resource
        // directory, i.e. two 8-byte entries further.
        var directBase = magic == 0x20B ? optionalOffset + 112 : optionalOffset + 96;
        var resourceRva = BinaryPrimitives.ReadUInt32LittleEndian(file.Slice(directBase + 16, 4));
        if (resourceRva == 0)
        {
            return false;
        }
        var resourceOffset = SectionTable.From(file, peOffset).RvaToOffset(resourceRva);
        return ResourceDirectory.ContainsTypeId(file, resourceOffset, 3)
            || ResourceDirectory.ContainsTypeId(file, resourceOffset, 14);
    }

    public static int ReadPeHeaderOffset(ReadOnlySpan<byte> file)
    {
        if (file.Length < 0x40 || file[0] != (byte)'M' || file[1] != (byte)'Z')
        {
            throw new MissingWindowsHeader("no DOS header");
        }
        var peOffset = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(0x3C, 4));
        if (peOffset < 0 || peOffset + 6 > file.Length ||
            BinaryPrimitives.ReadUInt32LittleEndian(file.Slice(peOffset, 4)) != 0x00004550)
        {
            throw new MissingWindowsHeader("no PE signature");
        }
        return peOffset;
    }

    public sealed class MissingWindowsHeader(string message) : Exception(message);
}

/// <summary>The COFF section table with the one operation the resource walk needs:
/// translating an RVA to a raw file offset through the single containing section.</summary>
internal readonly record struct SectionTable
{
    private readonly SectionEntry[] entries;

    private SectionTable(SectionEntry[] entries) => this.entries = entries;

    public static SectionTable From(ReadOnlySpan<byte> file, int peOffset)
    {
        var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(peOffset + 6, 2));
        var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(peOffset + 20, 2));
        var tableOffset = peOffset + 24 + (long)optionalSize;
        if (tableOffset + 40L * sectionCount > file.Length)
        {
            throw new PeChecks.MissingWindowsHeader("section table truncated");
        }
        var entries = new SectionEntry[sectionCount];
        for (var i = 0; i < sectionCount; i++)
        {
            var offset = tableOffset + 40 * i;
            // Per entry: Name(8) VirtualSize(+8) VirtualAddress(+12)
            // SizeOfRawData(+16) PointerToRawData(+20).
            entries[i] = new SectionEntry(
                BinaryPrimitives.ReadUInt32LittleEndian(file.Slice((int)offset + 8, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(file.Slice((int)offset + 12, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(file.Slice((int)offset + 16, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(file.Slice((int)offset + 20, 4)));
        }
        return new SectionTable(entries);
    }

    public uint RvaToOffset(uint rva)
    {
        foreach (var entry in entries)
        {
            var spanBytes = entry.VirtualSize != 0 ? entry.VirtualSize : entry.RawSize;
            if (rva >= entry.VirtualAddress && rva < entry.VirtualAddress + spanBytes)
            {
                return rva - entry.VirtualAddress + entry.RawPointer;
            }
        }
        throw new PeChecks.MissingWindowsHeader($"RVA 0x{rva:X} is not inside any section");
    }
}

internal readonly record struct SectionEntry(
    uint VirtualSize,
    uint VirtualAddress,
    uint RawSize,
    uint RawPointer);

/// <summary>Reads IMAGE_RESOURCE_DIRECTORY entries; only the first level (the type
/// ids) is ever consulted — one 8-byte entry per resource type in sorted order.</summary>
internal static class ResourceDirectory
{
    public static bool ContainsTypeId(ReadOnlySpan<byte> file, uint directoryOffset, uint typeId)
    {
        if (directoryOffset + 16 > (uint)file.Length)
        {
            return false;
        }
        var nameEntries = BinaryPrimitives.ReadUInt16LittleEndian(file.Slice((int)directoryOffset + 12, 2));
        var idEntries = BinaryPrimitives.ReadUInt16LittleEndian(file.Slice((int)directoryOffset + 14, 2));
        var entryOffset = directoryOffset + 16;
        for (var i = 0; i < nameEntries + idEntries; i++)
        {
            if (entryOffset + 8 > (uint)file.Length)
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
