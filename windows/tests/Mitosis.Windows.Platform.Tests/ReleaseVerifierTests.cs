using System.IO.Compression;
using System.Text;
using Mitosis.ReleaseVerifier;

namespace Mitosis.Windows.Platform.Tests;

/// <summary>Failure-case and agreement tests for the release-candidate verifier
/// (mitosis/tools/ReleaseVerifier, Stage 19). Every archive is a synthetic
/// fixture built inside the test run in a fresh temporary directory: the
/// executables are minimal hand-built PE images (DOS/COFF/optional headers plus
/// a one-entry resource directory), not real builds, so no repository state is
/// touched and nothing is published. The version metadata seam is faked through
/// ReleaseVerifier.ExeAuditProvider because a hand-built PE carries no version
/// resource; the real FileVersionInfo path is exercised by the actual
/// publish-and-verify run documented in docs/WINDOWS-RELEASE.md.</summary>
[TestClass]
public sealed class ReleaseVerifierTests
{
    private const string Revision = "abcdef1";
    private const string FullHash = "abcdef1234567890abcdef1234567890abcdef12";

    private static (string Directory, string ArchivePath) CreateFixtureDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(),
            "mitosis-verifier-tests-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(directory);
        return (directory, Path.Combine(directory,
            $"Mitosis-windows-1.2.3-x64-{Revision}.zip"));
    }

    private static string WriteArchive(
        (string EntryName, byte[] Bytes)[] entries,
        string? archivePath = null,
        string? sidecarHash = null)
    {
        var (directory, defaultPath) = CreateFixtureDirectory();
        var path = archivePath ?? defaultPath;
        using (var stream = File.Create(path))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            foreach (var entry in entries)
            {
                var zipEntry = archive.CreateEntry(entry.EntryName);
                using var writer = zipEntry.Open();
                writer.Write(entry.Bytes);
            }
        }
        var hash = sidecarHash ??
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))
                .ToLowerInvariant();
        File.WriteAllText(path + ".sha256", hash + "  " + Path.GetFileName(path) + "\n");
        return path;
    }

    private static byte[] Text(string content) => Encoding.UTF8.GetBytes(content);

    /// <summary>The required-file set of a well-formed archive, with the
    /// executable built by the fixture PE builder and audit metadata matching
    /// the fixture name.</summary>
    private static (string EntryName, byte[] Bytes)[] RequiredEntries(byte[]? exeBytes = null) =>
    [
        ("Mitosis.exe", exeBytes ?? PeFixture.Build(machine: PeChecks.MachineX64, withIcon: true)),
        ("Microsoft.WindowsAppRuntime.Bootstrap.dll", Text("stub")),
        ("Mitosis.deps.json", Text("{}")),
        ("Mitosis.runtimeconfig.json", Text("{}")),
        ..Enumerable.Range(0, 13)
            .Select(i => ($"Assets/pose-{i:D2}.png", new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' })),
    ];

    private static Mitosis.ReleaseVerifier.ReleaseVerifier.ExeAuditProvider AgreeingAudit =>
        _ => ("1.2.3.0", "1.2.3+" + FullHash);

    [TestMethod]
    public void ConsistentArchivePassesVerification()
    {
        var path = WriteArchive(RequiredEntries());
        var result = Mitosis.ReleaseVerifier.ReleaseVerifier.Verify(path, AgreeingAudit);
        Assert.IsTrue(result.Passed, "findings: " + string.Join("; ", result.Findings));
        Assert.AreEqual(64, result.Sha256.Length);
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void MissingRequiredFileIsReported()
    {
        var entries = RequiredEntries().ToList();
        entries.RemoveAll(e => e.EntryName == "Mitosis.deps.json");
        var path = WriteArchive(entries.ToArray());
        var result = Mitosis.ReleaseVerifier.ReleaseVerifier.Verify(path, AgreeingAudit);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("Mitosis.deps.json")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void IncompletePoseSetIsReported()
    {
        var entries = RequiredEntries().ToList();
        entries.RemoveAll(e => e.EntryName.EndsWith("pose-05.png"));
        var path = WriteArchive(entries.ToArray());
        var result = Mitosis.ReleaseVerifier.ReleaseVerifier.Verify(path, AgreeingAudit);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("expected 13 pose-*.png assets, found 12")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void SourceFileEntryIsReported()
    {
        var entries = RequiredEntries().ToList();
        entries.Add(("Program.cs", Text("// should never ship")));
        var path = WriteArchive(entries.ToArray());
        var result = Mitosis.ReleaseVerifier.ReleaseVerifier.Verify(path, AgreeingAudit);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("Program.cs")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void GitMetadataEntryIsReported()
    {
        var entries = RequiredEntries().ToList();
        entries.Add((".git/config", Text("stale metadata")));
        var path = WriteArchive(entries.ToArray());
        var result = Mitosis.ReleaseVerifier.ReleaseVerifier.Verify(path, AgreeingAudit);
        Assert.IsTrue(result.Findings.Any(f => f.Contains(".git/config")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void TraversalEntryIsRejected()
    {
        var entries = RequiredEntries().ToList();
        entries.Add(("..\\..\\evil.txt", Text("outside")));
        var path = WriteArchive(entries.ToArray());
        var result = Mitosis.ReleaseVerifier.ReleaseVerifier.Verify(path, AgreeingAudit);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("outside the archive root")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void CorruptArchiveIsReportedNotThrown()
    {
        var (directory, path) = CreateFixtureDirectory();
        File.WriteAllBytes(path, new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00 });
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))
            .ToLowerInvariant();
        File.WriteAllText(path + ".sha256", hash + "  bogusname.zip");
        var result = Mitosis.ReleaseVerifier.ReleaseVerifier.Verify(path, AgreeingAudit);
        Assert.IsFalse(result.Passed, "a truncated ZIP must not pass");
        Assert.IsTrue(result.Findings.Any(f => f.Contains("cannot be opened or extracted")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(directory, true);
    }

    [TestMethod]
    public void SidecarHashMismatchIsReported()
    {
        var path = WriteArchive(RequiredEntries(), sidecarHash: new string('0', 64));
        var result = Mitosis.ReleaseVerifier.ReleaseVerifier.Verify(path, AgreeingAudit);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("sidecar")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void MissingSidecarIsReported()
    {
        var path = WriteArchive(RequiredEntries());
        File.Delete(path + ".sha256");
        var result = Mitosis.ReleaseVerifier.ReleaseVerifier.Verify(path, AgreeingAudit);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("sidecar")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void UnrecognizedArchiveNameIsReported()
    {
        var (directory, _) = CreateFixtureDirectory();
        var path = Path.Combine(directory, "Artifact.zip");
        WriteArchive(RequiredEntries(), archivePath: path);
        try
        {
            var result = Mitosis.ReleaseVerifier.ReleaseVerifier.Verify(path, AgreeingAudit);
            Assert.IsTrue(result.Findings.Any(f => f.Contains("does not follow") &&
                f.Contains("Mitosis-windows-<version>")),
                "findings: " + string.Join("; ", result.Findings));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void VersionMismatchBetweenNameAndExeIsReported()
    {
        var path = WriteArchive(RequiredEntries());
        var result = Mitosis.ReleaseVerifier.ReleaseVerifier.Verify(path,
            _ => ("2.0.0.1", "2.0.0+" + FullHash));
        Assert.IsTrue(result.Findings.Any(f =>
                f.Contains("archive version 1.2.3 does not equal the built version 2.0.0")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void RevisionMismatchBetweenNameAndExeIsReported()
    {
        var path = WriteArchive(RequiredEntries());
        var result = Mitosis.ReleaseVerifier.ReleaseVerifier.Verify(path,
            _ => ("1.2.3.0", "1.2.3+ffffff0123456789abcdef0123456789abcdef01"));
        Assert.IsTrue(result.Findings.Any(f => f.Contains("revision 'abcdef1' in the archive name")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void NogitArchiveMustNotCarryEmbeddedHash()
    {
        var path = WriteArchive(RequiredEntries());
        var nogitPath = Path.Combine(Path.GetDirectoryName(path)!,
            "Mitosis-windows-1.2.3-x64-nogit.zip");
        File.Move(path, nogitPath);
        File.Move(path + ".sha256", nogitPath + ".sha256");
        var result = Mitosis.ReleaseVerifier.ReleaseVerifier.Verify(nogitPath, AgreeingAudit);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("still embeds a hash")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(nogitPath)!, true);
    }

    [TestMethod]
    public void WrongArchitectureIsReported()
    {
        var path = WriteArchive(RequiredEntries(
            PeFixture.Build(machine: PeChecks.MachineArm64, withIcon: true)));
        var result = Mitosis.ReleaseVerifier.ReleaseVerifier.Verify(path, AgreeingAudit);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("ARM64, not the documented x64")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void ExecutableWithoutIconIsReported()
    {
        var path = WriteArchive(RequiredEntries(
            PeFixture.Build(machine: PeChecks.MachineX64, withIcon: false)));
        var result = Mitosis.ReleaseVerifier.ReleaseVerifier.Verify(path, AgreeingAudit);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("embeds no application icon")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void NonExeExecutableIsReported()
    {
        var path = WriteArchive(RequiredEntries(Text("this is text, not a program")));
        var result = Mitosis.ReleaseVerifier.ReleaseVerifier.Verify(path, AgreeingAudit);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("not a valid Windows executable")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }
}

/// <summary>Hand-built minimal PE images for fixture executables: DOS header,
/// PE signature, COFF header (chosen machine), a PE32+ optional header whose
/// resource data directory points at a one-entry resource directory (RT_ICON
/// when withIcon). Deliberately minimal: only what PeChecks reads.</summary>
internal static class PeFixture
{
    public static byte[] Build(ushort machine = 0x8664, bool withIcon = true)
    {
        var file = new byte[0x300];
        file[0] = (byte)'M';
        file[1] = (byte)'Z';
        // e_lfanew = 0x40
        BitConverter.GetBytes(0x40).CopyTo(file, 0x3C);
        // PE signature
        BitConverter.GetBytes(0x00004550u).CopyTo(file, 0x40);
        // COFF header at 0x44: Machine, NumberOfSections=1, then zero fields,
        // SizeOfOptionalHeader=240 (PE32+ full optional header), Characteristics.
        BitConverter.GetBytes(machine).CopyTo(file, 0x44);
        BitConverter.GetBytes((ushort)1).CopyTo(file, 0x46);
        BitConverter.GetBytes((ushort)240).CopyTo(file, 0x54);
        // Optional header at 0x58: PE32+ magic; NumberOfRvaAndSizes=16 at +108;
        // Resource directory = data directory 2 at +112+16.
        BitConverter.GetBytes((ushort)0x20B).CopyTo(file, 0x58);
        BitConverter.GetBytes(16u).CopyTo(file, 0x58 + 108);
        BitConverter.GetBytes(0x1000u).CopyTo(file, 0x58 + 112 + 16);
        // One section: VirtualSize=0x20 VirtualAddress=0x1000 RawSize=0x80
        // RawPointer=0x200. Section table follows the optional header: 0x58+240.
        BitConverter.GetBytes(0x20u).CopyTo(file, 0x58 + 240 + 8);
        BitConverter.GetBytes(0x1000u).CopyTo(file, 0x58 + 240 + 12);
        BitConverter.GetBytes(0x80u).CopyTo(file, 0x58 + 240 + 16);
        BitConverter.GetBytes(0x200u).CopyTo(file, 0x58 + 240 + 20);
        // Resource directory at RVA 0x1000 = file 0x200: IMAGE_RESOURCE_DIRECTORY
        // header (Characteristics/TimeDateStamp/Major/Minor stay zero at 0x200..0x20C)
        // then the entry counts at +12/+14 and entries from +16.
        BitConverter.GetBytes((ushort)0).CopyTo(file, 0x20C);               // named entries
        BitConverter.GetBytes((ushort)(withIcon ? 1 : 0)).CopyTo(file, 0x20E); // id entries
        if (withIcon)
        {
            BitConverter.GetBytes(3u).CopyTo(file, 0x210);   // type id = RT_ICON
            BitConverter.GetBytes(0x80000000u).CopyTo(file, 0x214); // subdirectory
        }
        return file;
    }
}
