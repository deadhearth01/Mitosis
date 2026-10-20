using System.IO.Compression;
using System.Text;
using Mitosis.ReleaseVerifier;
using Verifier = Mitosis.ReleaseVerifier.ReleaseVerifier;

namespace Mitosis.Windows.Platform.Tests;

/// <summary>Failure-case and agreement tests for the release-candidate verifier
/// (mitosis/tools/ReleaseVerifier, Stages 19–20). Every archive is a synthetic
/// fixture built inside the test run in a fresh temporary directory: the
/// executables are minimal hand-built PE images (DOS/COFF/optional headers plus
/// a one-entry resource directory), not real builds, so no repository state is
/// touched and nothing is published. The version metadata seam is faked through
/// ReleaseVerifier.ExeAuditProvider because a hand-built PE carries no version
/// resource; the real FileVersionInfo path is exercised by the actual
/// publish-and-verify run documented in docs/WINDOWS-RELEASE.md.
///
/// Stage 20 hardening is covered by adversarial fixtures: traversal in both
/// slash styles, absolute/drive paths, case-colliding and Windows-ambiguous
/// names, resource limits (entry count, archive size, sidecar size, mutated
/// decompression bomb), malformed sidecars, and malformed PE structures that
/// previously threw uncontrolled exceptions — each fixture must produce a
/// finding and fail closed.</summary>
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
        string? sidecarHash = null,
        string? sidecarName = null,
        bool sidecarSingleToken = false)
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
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(path))).ToLowerInvariant();
        var text = sidecarSingleToken
            ? hash
            : hash + "  " + (sidecarName ?? Path.GetFileName(path)) + "\n";
        File.WriteAllText(path + ".sha256", text);
        return path;
    }

    private static byte[] Text(string content) => Encoding.UTF8.GetBytes(content);

    private static (string EntryName, byte[] Bytes)[] RequiredEntries(
        byte[]? exeBytes = null,
        bool omitPose = false) =>
    [
        ("Mitosis.exe", exeBytes ?? PeFixture.Build()),
        ("Microsoft.WindowsAppRuntime.Bootstrap.dll", Text("stub")),
        ("Mitosis.deps.json", Text("{}")),
        ("Mitosis.runtimeconfig.json", Text("{}")),
        ..(omitPose
            ? Array.Empty<(string, byte[])>()
            : Enumerable.Range(0, 13)
                .Select(i => ($"Assets/pose-{i:D2}.png", new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }))),
    ];

    private static Mitosis.ReleaseVerifier.ReleaseVerifier.ExeAuditProvider AgreeingAudit =>
        _ => ("1.2.3.0", "1.2.3+" + FullHash);

    private static (string, VerificationResult) VerifyFixture(
        string path,
        VerificationLimits? limits = null,
        Mitosis.ReleaseVerifier.ReleaseVerifier.ExeAuditProvider? audit = null)
    {
        var result = Verifier.Verify(path, audit ?? AgreeingAudit, limits);
        return (Path.GetDirectoryName(path)!, result);
    }

    // ---- agreement (the legitimate artifact must keep passing) --------------

    [TestMethod]
    public void ConsistentArchivePassesVerification()
    {
        var path = WriteArchive(RequiredEntries());
        var (directory, result) = VerifyFixture(path);
        Assert.IsTrue(result.Passed, "findings: " + string.Join("; ", result.Findings));
        Assert.AreEqual(64, result.Sha256.Length);
        Directory.Delete(directory, true);
    }

    // ---- entry-name rules ---------------------------------------------------

    [TestMethod]
    public void MissingRequiredFileIsReported()
    {
        var entries = RequiredEntries().ToList();
        entries.RemoveAll(e => e.EntryName == "Mitosis.deps.json");
        var path = WriteArchive(entries.ToArray());
        var (_, result) = VerifyFixture(path);
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
        var (_, result) = VerifyFixture(path);
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
        var (_, result) = VerifyFixture(path);
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
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains(".git/config")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void BackslashTraversalEntryIsRejected()
    {
        var entries = RequiredEntries().ToList();
        entries.Add(("..\\..\\evil.txt", Text("outside")));
        var path = WriteArchive(entries.ToArray());
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("outside the archive root")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void ForwardSlashTraversalEntryIsRejected()
    {
        var entries = RequiredEntries().ToList();
        entries.Add(("../evil.txt", Text("outside")));
        var path = WriteArchive(entries.ToArray());
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("outside the archive root")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void AbsolutePathEntryIsRejected()
    {
        var entries = RequiredEntries().ToList();
        entries.Add(("/etc/evil.txt", Text("absolute")));
        var path = WriteArchive(entries.ToArray());
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("outside the archive root")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void DriveQualifiedEntryIsRejected()
    {
        var entries = RequiredEntries().ToList();
        entries.Add(("C:\\Windows\\evil.txt", Text("drive")));
        var path = WriteArchive(entries.ToArray());
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("outside the archive root")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void CaseCollidingEntriesAreRejected()
    {
        var entries = RequiredEntries().ToList();
        entries.Add(("mitosis.exe", Text("collision")));
        var path = WriteArchive(entries.ToArray());
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f =>
            f.Contains("mitosis.exe") && f.Contains("more than once")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void DotSegmentCollidingEntriesAreRejected()
    {
        var entries = RequiredEntries().ToList();
        entries.Add(("Assets/./pose-03.png", Text("same file by another spelling")));
        var path = WriteArchive(entries.ToArray());
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("pose-03.png") &&
            f.Contains("more than once")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void TrailingDotEntryIsRejected()
    {
        var entries = RequiredEntries().ToList();
        entries.Add(("Assets/evil.txt.", Text("windows strips the dot")));
        var path = WriteArchive(entries.ToArray());
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("different Windows file")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void TrailingSpaceEntryIsRejected()
    {
        var entries = RequiredEntries().ToList();
        entries.Add(("Assets/readme.txt ", Text("windows strips the space")));
        var path = WriteArchive(entries.ToArray());
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("different Windows file")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void AlternateDataStreamEntryIsRejected()
    {
        var entries = RequiredEntries().ToList();
        entries.Add(("Assets/Mitosis.exe:stream", Text("NTFS alternate stream")));
        var path = WriteArchive(entries.ToArray());
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("different Windows file")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    // ---- resource limits ----------------------------------------------------

    [TestMethod]
    public void EntryCountLimitIsEnforced()
    {
        var entries = RequiredEntries(omitPose: true).ToList();
        for (var i = 0; i < 5; i++)
        {
            entries.Add(($"Extra-{i}.dat", new byte[64]));
        }
        var path = WriteArchive(entries.ToArray());
        var (_, result) = VerifyFixture(path,
            new VerificationLimits { MaxEntries = 3 });
        Assert.IsTrue(result.Findings.Any(f => f.Contains("documented maximum of 3")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void ArchiveSizeLimitIsEnforced()
    {
        var path = WriteArchive(RequiredEntries());
        var (_, result) = VerifyFixture(path,
            new VerificationLimits { MaxArchiveBytes = 8 });
        Assert.IsTrue(result.Findings.Any(f => f.Contains("documented release limit")),
            "findings: " + string.Join("; ", result.Findings));
        Assert.AreEqual("", result.Sha256, "an over-limit archive must not be hashed");
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void ExtractionHoldsAtPerEntryDeclaredLimit()
    {
        // A real 1 MB entry under a 10 KB limit: the untrusted central-directory
        // size is rejected before extraction. (The current .NET reader also
        // truncates decompression at the declared size — pinned separately in
        // ReaderTruncatesAtDeclaredUncompressedSize — so this pre-check is the
        // primary bomb guard and the metered copy is defense in depth.)
        var entries = new (string, byte[])[] { ("Assets/quit-large.png", new byte[1_000_000]) };
        var path = WriteArchive(entries);
        var (_, result) = VerifyFixture(path,
            new VerificationLimits { MaxEntryUncompressedBytes = 10_000 });
        Assert.IsTrue(result.Findings.Any(f =>
            f.Contains("Assets/quit-large.png") && f.Contains("above the")
                && f.Contains("10,000-byte per-entry limit")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void ReaderTruncatesAtDeclaredUncompressedSize()
    {
        // Safety-assumption pin: the verifier's resource arguments rest on two
        // layers — the declared-size pre-check above, and the BCL reader ending
        // decompression at the declared uncompressed size. This fixture lies in
        // the central directory (declared 900) while the stream really holds
        // 1,000,000 bytes; if the reader ever stops truncating at the declared
        // size, the metered copy in ExtractEntries is what still bounds the
        // expansion — re-test both here. The verifier must simply fail the
        // archive (required files absent), never crash or over-extract.
        var (directory, path) = CreateFixtureDirectory();
        try
        {
            using (var stream = File.Create(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("Assets/bomb.png");
                using var bomb = entry.Open();
                bomb.Write(new byte[1_000_000]);
            }
            PatchCentralUncompressedSize(path, 900);
            using var check = ZipFile.OpenRead(path);
            var checkEntry = check.Entries[0];
            Assert.AreEqual(900L, checkEntry.Length, "patched declaration must be read");
            long delivered;
            using (var src = checkEntry.Open())
            {
                var buf = new byte[65536];
                int n;
                delivered = 0;
                while ((n = src.Read(buf, 0, buf.Length)) > 0) { delivered += n; }
            }
            Assert.AreEqual(900L, delivered, "the reader must truncate decompression "
                + "at the declared uncompressed size; if this changes the metered "
                + "extraction becomes the primary bomb guard");
            check.Dispose();
            File.WriteAllText(path + ".sha256",
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    File.ReadAllBytes(path))).ToLowerInvariant() + "  " +
                    Path.GetFileName(path) + "\n");
            var (_, result) = VerifyFixture(path,
                new VerificationLimits { MaxEntryUncompressedBytes = 10_000 });
            Assert.IsFalse(result.Passed, "a lying archive must fail verification");
            Assert.IsFalse(result.Findings.Any(f => f.Contains("cannot be opened")),
                "findings: " + string.Join("; ", result.Findings));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void ExtractionStopsWhenTotalLimitIsCrossed()
    {
        var entries = new (string, byte[])[]
        {
            ("file-1.dat", new byte[4096]),
            ("file-2.dat", new byte[4096]),
        };
        var path = WriteArchive(entries);
        var (_, result) = VerifyFixture(path,
            new VerificationLimits { MaxTotalUncompressedBytes = 6000 });
        Assert.IsTrue(result.Findings.Any(f =>
            f.Contains("documented 6,000-byte expanded-size limit")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    /// <summary>Central-directory lies: patches the entry's declared
    /// uncompressed size (the metadata AuditEntries trusts for its pre-check)
    /// without touching the compressed stream, so only the metered
    /// decompression can catch the bomb.</summary>
    private static void PatchCentralUncompressedSize(string zipPath, uint declaredSize)
    {
        var bytes = File.ReadAllBytes(zipPath);
        var signature = new byte[] { 0x50, 0x4B, 0x01, 0x02 }; // central directory header
        var offset = bytes.AsSpan().IndexOf(signature);
        Assert.IsGreaterThan(0, offset, "fixture zip has no central directory");
        BitConverter.GetBytes(declaredSize).CopyTo(bytes, offset + 24);
        File.WriteAllBytes(zipPath, bytes);
    }

    // ---- sidecar rules ------------------------------------------------------

    [TestMethod]
    public void SidecarHashMismatchIsReported()
    {
        var path = WriteArchive(RequiredEntries(), sidecarHash: new string('0', 64));
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("sidecar")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void SidecarMustNameTheArchive()
    {
        var path = WriteArchive(RequiredEntries(), sidecarName: "other-file.zip");
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("names 'other-file.zip'")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void SidecarSingleTokenIsRejected()
    {
        var path = WriteArchive(RequiredEntries(), sidecarSingleToken: true);
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f =>
            f.Contains("'<hash>  <archive name>'")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void SidecarSizeLimitIsEnforced()
    {
        var path = WriteArchive(RequiredEntries());
        var (_, result) = VerifyFixture(path,
            new VerificationLimits { MaxSidecarBytes = 16 });
        Assert.IsTrue(result.Findings.Any(f => f.Contains("one short line")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void MissingSidecarIsReported()
    {
        var path = WriteArchive(RequiredEntries());
        File.Delete(path + ".sha256");
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("sidecar")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    // ---- archive integrity --------------------------------------------------

    [TestMethod]
    public void CorruptArchiveIsReportedNotThrown()
    {
        var (directory, path) = CreateFixtureDirectory();
        File.WriteAllBytes(path, new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00 });
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            File.ReadAllBytes(path))).ToLowerInvariant();
        File.WriteAllText(path + ".sha256", hash + "  " + Path.GetFileName(path));
        var (_, result) = VerifyFixture(path);
        Assert.IsFalse(result.Passed, "a truncated ZIP must not pass");
        Assert.IsTrue(result.Findings.Any(f => f.Contains("cannot be opened or extracted")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(directory, true);
    }

    [TestMethod]
    public void UnrecognizedArchiveNameIsReported()
    {
        var (directory, _) = CreateFixtureDirectory();
        var path = Path.Combine(directory, "Artifact.zip");
        WriteArchive(RequiredEntries(), archivePath: path);
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("does not follow") &&
            f.Contains("Mitosis-windows-<version>")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(directory, true);
    }

    [TestMethod]
    public void ScratchDirectoryIsCleanedAfterFailure()
    {
        var (directory, path) = CreateFixtureDirectory();
        File.WriteAllBytes(path, new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 });
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            File.ReadAllBytes(path))).ToLowerInvariant();
        File.WriteAllText(path + ".sha256", hash + "  " + Path.GetFileName(path));
        // The result is deliberately ignored — the call itself is what the
        // cleanup assertion below checks.
        Verifier.Verify(path, AgreeingAudit);
        var leftovers = Directory
            .EnumerateDirectories(Path.GetTempPath(), "mitosis-release-verify-*")
            .ToList();
        Assert.AreEqual(0, leftovers.Count,
            "verifier scratch directories must be cleaned on every path: "
            + string.Join("; ", leftovers));
        Directory.Delete(directory, true);
    }

    // ---- version coherence --------------------------------------------------

    [TestMethod]
    public void VersionMismatchBetweenNameAndExeIsReported()
    {
        var path = WriteArchive(RequiredEntries());
        var (_, result) = VerifyFixture(path, audit: _ => ("2.0.0.1", "2.0.0+" + FullHash));
        Assert.IsTrue(result.Findings.Any(f =>
            f.Contains("archive version 1.2.3 does not equal the built version 2.0.0")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void RevisionMismatchBetweenNameAndExeIsReported()
    {
        var path = WriteArchive(RequiredEntries());
        var (_, result) = VerifyFixture(path,
            audit: _ => ("1.2.3.0", "1.2.3+ffffff0123456789abcdef0123456789abcdef01"));
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
        var (_, result) = VerifyFixture(nogitPath);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("still embeds a hash")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(nogitPath)!, true);
    }

    // ---- executable parsing (malformed PE must fail, not crash) -------------

    [TestMethod]
    public void WrongArchitectureIsReported()
    {
        var path = WriteArchive(RequiredEntries(
            PeFixture.Build(machine: PeChecks.MachineArm64)));
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("ARM64, not the documented x64")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void ExecutableWithoutIconIsReported()
    {
        var path = WriteArchive(RequiredEntries(
            PeFixture.Build(withIcon: false)));
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("embeds no application icon")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void NonExeExecutableIsReported()
    {
        var path = WriteArchive(RequiredEntries(Text("this is text, not a program")));
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("not a valid Windows executable")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void TruncatedOptionalHeaderIsReportedNotCrashing()
    {
        // Regression (Stage 20): this layout used to escape the parser's
        // exception type with ArgumentOutOfRangeException — the verifier
        // crashed on the fixture instead of reporting the archive as invalid.
        var path = WriteArchive(RequiredEntries(
            PeFixture.Build()[..0x5A]));
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("not a valid Windows executable")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void RvaOutsideSectionsIsReportedNotCrashing()
    {
        var path = WriteArchive(RequiredEntries(
            PeFixture.Build(resourceRva: 0x2000)));
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f =>
            f.Contains("does not resolve inside a section")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void HugeSectionCountIsReportedNotCrashing()
    {
        var exe = PeFixture.Build();
        BitConverter.GetBytes(ushort.MaxValue).CopyTo(exe, 0x46); // NumberOfSections
        var path = WriteArchive(RequiredEntries(exe));
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("not a valid Windows executable")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void BadOptionalMagicIsReportedNotCrashing()
    {
        var exe = PeFixture.Build();
        BitConverter.GetBytes((ushort)0x0222).CopyTo(exe, 0x58); // OptionalHeader Magic
        var path = WriteArchive(RequiredEntries(exe));
        var (_, result) = VerifyFixture(path);
        Assert.IsTrue(result.Findings.Any(f => f.Contains("unexpected optional header magic")),
            "findings: " + string.Join("; ", result.Findings));
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [TestMethod]
    public void NearMaxPeOffsetDoesNotWrap()
    {
        // Regression (Stage 20): peOffset + 6 could wrap negative in int
        // arithmetic for an attacker-chosen e_lfanew; the header check must
        // reject it in long arithmetic.
        var file = new byte[0x100];
        file[0] = (byte)'M';
        file[1] = (byte)'Z';
        file[0x3C] = 0xFF;
        file[0x3D] = 0xFF;
        file[0x3E] = 0xFF;
        file[0x3F] = 0x7F; // e_lfanew = 0x7FFFFFFF (keeps the int positive)
        Assert.ThrowsExactly<PeChecks.MissingWindowsHeader>(
            () => PeChecks.ReadPeHeaderOffset(file));
    }
}

/// <summary>Hand-built minimal PE images for fixture executables: DOS header,
/// PE signature, COFF header (chosen machine), a PE32+ optional header whose
/// resource data directory points at a one-entry resource directory (RT_ICON
/// when withIcon). Deliberately minimal: only what PeChecks reads; the
/// mutation knobs are for the malformed-input regression tests.</summary>
internal static class PeFixture
{
    public const int Size = 0x300;

    public static byte[] Build(
        ushort machine = 0x8664,
        bool withIcon = true,
        uint resourceRva = 0x1000)
    {
        var file = new byte[Size];
        file[0] = (byte)'M';
        file[1] = (byte)'Z';
        BitConverter.GetBytes(0x40).CopyTo(file, 0x3C);            // e_lfanew
        BitConverter.GetBytes(0x00004550u).CopyTo(file, 0x40);     // PE signature
        BitConverter.GetBytes(machine).CopyTo(file, 0x44);         // COFF Machine
        BitConverter.GetBytes((ushort)1).CopyTo(file, 0x46);       // NumberOfSections
        BitConverter.GetBytes((ushort)240).CopyTo(file, 0x54);     // SizeOfOptionalHeader
        BitConverter.GetBytes((ushort)0x20B).CopyTo(file, 0x58);   // PE32+ magic
        BitConverter.GetBytes(16u).CopyTo(file, 0x58 + 108);       // NumberOfRvaAndSizes
        BitConverter.GetBytes(resourceRva).CopyTo(file, 0x58 + 112 + 16);
        // One section: VirtualSize=0x20 VirtualAddress=0x1000 RawSize=0x80
        // RawPointer=0x200 at 0x58 + 240.
        BitConverter.GetBytes(0x20u).CopyTo(file, 0x58 + 240 + 8);
        BitConverter.GetBytes(0x1000u).CopyTo(file, 0x58 + 240 + 12);
        BitConverter.GetBytes(0x80u).CopyTo(file, 0x58 + 240 + 16);
        BitConverter.GetBytes(0x200u).CopyTo(file, 0x58 + 240 + 20);
        // Resource directory at RVA 0x1000 = file 0x200: the
        // Characteristics/TimeDateStamp fields stay zero at 0x200..0x20C, then
        // the entry counts at +12/+14 and the first entry from +16.
        BitConverter.GetBytes((ushort)0).CopyTo(file, 0x20C);
        BitConverter.GetBytes((ushort)(withIcon ? 1 : 0)).CopyTo(file, 0x20E);
        if (withIcon)
        {
            BitConverter.GetBytes(3u).CopyTo(file, 0x210);          // RT_ICON
            BitConverter.GetBytes(0x80000000u).CopyTo(file, 0x214); // subdirectory
        }
        return file;
    }
}
