using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Mitosis.ReleaseVerifier;

/// <summary>The outcome of one verification run. Findings are human-readable,
/// phrased as "what is wrong with the archive", so they can be pasted into a
/// report without further tooling; an empty list means the archive passed.</summary>
public sealed record VerificationResult(string Archive, string Sha256, List<string> Findings)
{
    public bool Passed => Findings.Count == 0;
}

/// <summary>Orchestrates the release-candidate verification for one ZIP: computes
/// the SHA-256, compares the .sha256 sidecar, opens and fully enumerates the
/// archive (rejecting traversal and denied content), extracts it into a fresh
/// temporary directory, and re-checks the extracted output against the same
/// rules the publish recipe enforces (required files, 13 pose assets, x64
/// executable with an embedded icon, version metadata coherent with the archive
/// name). It deliberately does not read the publish tree itself — an artifact
/// judge must look at the artifact, not at its inputs.</summary>
public static class ReleaseVerifier
{
    /// <summary>The runtime pieces a clean machine cannot work without. Mirrors
    /// windows/scripts/publish-release.ps1; keep both lists identical.</summary>
    public static readonly string[] RequiredRootFiles =
    {
        "Mitosis.exe",
        "Microsoft.WindowsAppRuntime.Bootstrap.dll",
        "Mitosis.deps.json",
        "Mitosis.runtimeconfig.json",
    };

    public const int RequiredPoseCount = 13;

    public const ushort ExpectedMachine = PeChecks.MachineX64;

    /// <summary>Test seam: when provided, replaces reading FileVersionInfo from
    /// the extracted executable (synthetic fixture executables carry no version
    /// resource). Null in production.</summary>
    public delegate (string? FileVersion, string? ProductVersion)? ExeAuditProvider(string exePath);

    public static VerificationResult Verify(string archivePath, ExeAuditProvider? exeAuditOverride = null)
    {
        var archive = Path.GetFullPath(archivePath);
        var findings = new List<string>();
        var sha = ComputeSha256(archive, findings);

        var archiveVersion = (string?)null;
        var archiveRevision = "nogit";
        if (ArchiveName.TryParse(Path.GetFileName(archive), out var nameParts)
            && nameParts is not null)
        {
            archiveVersion = nameParts.Version;
            archiveRevision = nameParts.Revision;
        }
        else
        {
            findings.Add($"archive name '{Path.GetFileName(archive)}' does not follow "
                + "Mitosis-windows-<version>-x64-<revision>.zip");
        }
        CheckSha256Sidecar(archive, sha, findings);

        if (!File.Exists(archive))
        {
            findings.Add("the archive file does not exist");
            return new VerificationResult(archive, sha, findings);
        }

        var extractedDirectory = Path.Combine(Path.GetTempPath(),
            "mitosis-release-verify-" + Guid.NewGuid().ToString("N")[..12]);
        try
        {
            using var zip = ZipFile.OpenRead(archive);
            var extractable = AuditEntries(zip, findings);
            ExtractEntries(zip, extractable, extractedDirectory);
            AuditExtractedTree(extractedDirectory, archiveVersion, archiveRevision,
                findings, exeAuditOverride);
        }
        catch (Exception failure) when (failure is InvalidDataException or IOException
            or UnauthorizedAccessException or NotSupportedException)
        {
            findings.Add($"archive cannot be opened or extracted: {failure.Message}");
        }
        finally
        {
            TryCleanup(extractedDirectory);
        }
        return new VerificationResult(archive, sha, findings);
    }

    /// <summary>SHA-256 by streaming read; an unreadable archive surfaces as a
    /// finding and an empty hash rather than an unhandled exception.</summary>
    private static string ComputeSha256(string archivePath, List<string> findings)
    {
        try
        {
            using var stream = File.OpenRead(archivePath);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            findings.Add($"archive cannot be read for hashing: {failure.Message}");
            return "";
        }
    }

    /// <summary>The publish step writes "<archive>.sha256"; the verifier
    /// recomputes the hash and requires the sidecar to agree, so the reported
    /// value cannot drift from the bytes.</summary>
    private static void CheckSha256Sidecar(string archivePath, string sha, List<string> findings)
    {
        var sidecarPath = archivePath + ".sha256";
        if (!File.Exists(sidecarPath))
        {
            findings.Add("there is no '<archive>.sha256' sidecar next to the archive");
            return;
        }
        if (sha.Length != 64)
        {
            return; // hashing already failed; a sidecar comparison is impossible
        }
        var sidecar = File.ReadAllText(sidecarPath, Encoding.UTF8).Trim();
        if (Regex.Match(sidecar, @"^[0-9a-fA-F]{64}") is not { Success: true } match)
        {
            findings.Add("the .sha256 sidecar does not start with a SHA-256 hash");
        }
        else if (!string.Equals(match.Value, sha, StringComparison.OrdinalIgnoreCase))
        {
            findings.Add($"the .sha256 sidecar ({match.Value[..12]}...) does not match "
                + $"the archive ({sha[..12]}...)");
        }
    }

    /// <summary>One pass over every entry: normalization, traversal rejection,
    /// denylist, duplicate detection. Returns the entries safe to extract.</summary>
    private static List<ZipArchiveEntry> AuditEntries(ZipArchive zip, List<string> findings)
    {
        var extractable = new List<ZipArchiveEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            var normalized = ZipAudit.Normalize(entry.FullName);
            if (ZipAudit.IsDirectoryEntry(entry.FullName))
            {
                continue;
            }
            if (ZipAudit.IsTraversal(normalized))
            {
                findings.Add($"entry '{normalized}' would extract outside the archive "
                    + "root and was rejected");
                continue;
            }
            if (ZipAudit.DeniedReason(normalized) is { } reason)
            {
                findings.Add($"entry '{normalized}' must not ship: {reason}");
                continue;
            }
            if (!seen.Add(normalized))
            {
                findings.Add($"entry '{normalized}' appears more than once in the archive");
                continue;
            }
            extractable.Add(entry);
        }
        return extractable;
    }

    private static void ExtractEntries(
        ZipArchive zip, List<ZipArchiveEntry> entries, string extractedDirectory)
    {
        foreach (var entry in entries)
        {
            var relative = ZipAudit.Normalize(entry.FullName)
                .Replace('/', Path.DirectorySeparatorChar);
            var destination = Path.Combine(extractedDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination);
        }
    }

    /// <summary>The extracted tree must satisfy the same rules as the publish
    /// output: required runtime files, the 13 pose assets, an x64 executable
    /// with an embedded icon, and version metadata agreeing with the name.</summary>
    private static void AuditExtractedTree(
        string extractedDirectory,
        string? archiveVersion,
        string archiveRevision,
        List<string> findings,
        ExeAuditProvider? exeAuditOverride)
    {
        foreach (var required in RequiredRootFiles)
        {
            if (!File.Exists(Path.Combine(extractedDirectory, required)))
            {
                findings.Add($"the archive is missing required file {required}");
            }
        }
        var assetsDirectory = Path.Combine(extractedDirectory, "Assets");
        var poses = Directory.Exists(assetsDirectory)
            ? Directory.GetFiles(assetsDirectory, "pose-*.png")
            : Array.Empty<string>();
        switch (poses.Length)
        {
            case RequiredPoseCount:
                break;
            case 0 when !Directory.Exists(assetsDirectory):
                findings.Add("the archive is missing the Assets directory with the brand pose assets");
                break;
            default:
                findings.Add($"expected {RequiredPoseCount} pose-*.png assets, found {poses.Length}");
                break;
        }

        var exePath = Path.Combine(extractedDirectory, "Mitosis.exe");
        if (!File.Exists(exePath))
        {
            return; // already reported as missing; nothing more to inspect
        }
        AuditExecutable(exePath, archiveVersion, archiveRevision, findings, exeAuditOverride);
    }

    private static void AuditExecutable(
        string exePath,
        string? archiveVersion,
        string archiveRevision,
        List<string> findings,
        ExeAuditProvider? exeAuditOverride)
    {
        ushort machine;
        bool hasIcon;
        try
        {
            var bytes = File.ReadAllBytes(exePath);
            machine = PeChecks.ReadMachine(bytes);
            hasIcon = PeChecks.HasIconGroup(bytes);
        }
        catch (PeChecks.MissingWindowsHeader failure)
        {
            findings.Add($"Mitosis.exe is not a valid Windows executable: {failure.Message}");
            return;
        }
        if (machine != ExpectedMachine)
        {
            var known = machine == PeChecks.MachineArm64
                ? "ARM64"
                : machine == PeChecks.MachineX86 ? "x86" : $"unknown (0x{machine:X4})";
            findings.Add($"Mitosis.exe is {known}, not the documented x64 build");
        }
        if (!hasIcon)
        {
            findings.Add("Mitosis.exe embeds no application icon resources");
        }

        string? fileVersion;
        string? productVersion;
        if (exeAuditOverride is { } audit)
        {
            (fileVersion, productVersion) = audit(exePath) ?? (null, null);
        }
        else
        {
            (fileVersion, productVersion) = ReadVersionInfo(exePath);
        }
        foreach (var finding in VersionCoherence.Check(
            fileVersion, productVersion, archiveVersion, archiveRevision))
        {
            findings.Add(finding);
        }
    }

    /// <summary>FileVersionInfo on Windows reads the version resource through the
    /// OS: exactly what Explorer and the taskbar see for end users.</summary>
    private static (string?, string?) ReadVersionInfo(string exePath)
    {
        var info = FileVersionInfo.GetVersionInfo(exePath);
        return (string.IsNullOrEmpty(info.FileVersion) ? null : info.FileVersion,
            string.IsNullOrEmpty(info.ProductVersion) ? null : info.ProductVersion);
    }

    /// <summary>Best-effort removal of the extraction scratch directory. A
    /// cleanup that fails is noise, not a release problem, so it never adds
    /// findings.</summary>
    private static void TryCleanup(string extractedDirectory)
    {
        try
        {
            Directory.Delete(extractedDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
