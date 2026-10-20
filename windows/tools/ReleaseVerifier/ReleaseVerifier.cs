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

/// <summary>Orchestrates the release-candidate verification for one ZIP: bounds
/// the archive by size before reading it, computes the SHA-256, requires the
/// matching .sha256 sidecar, opens and fully enumerates the archive (rejecting
/// traversal, ambiguous Windows names, denied content, and entries beyond the
/// resource limits), extracts with metered decompression into a fresh temporary
/// directory, and re-checks the extracted output against the same rules the
/// publish recipe enforces (required files, 13 pose assets, x64 executable with
/// an embedded icon, version metadata coherent with the archive name). It
/// deliberately does not read the publish tree itself — an artifact judge must
/// look at the artifact, not at its inputs — and treats every input byte as
/// hostile: malformed structures surface as findings, never as crashes, and
/// cleanup of the scratch directory runs on every path.</summary>
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

    public static VerificationResult Verify(
        string archivePath,
        ExeAuditProvider? exeAuditOverride = null,
        VerificationLimits? limits = null)
    {
        var checkedLimits = limits ?? VerificationLimits.Default;
        var archive = Path.GetFullPath(archivePath);
        var findings = new List<string>();

        if (!CheckArchivePresenceAndSize(archive, checkedLimits, findings))
        {
            return new VerificationResult(archive, "", findings);
        }
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
        CheckSha256Sidecar(archive, sha, checkedLimits, findings);

        var extractedDirectory = Path.Combine(Path.GetTempPath(),
            "mitosis-release-verify-" + Guid.NewGuid().ToString("N")[..12]);
        try
        {
            using var zip = ZipFile.OpenRead(archive);
            var extractable = AuditEntries(zip, checkedLimits, findings);
            var extractionCompleted = ExtractEntries(
                zip, extractable, extractedDirectory, checkedLimits, findings);
            if (extractionCompleted)
            {
                AuditExtractedTree(extractedDirectory, archiveVersion, archiveRevision,
                    findings, exeAuditOverride);
            }
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

    /// <summary>The archive must exist and fit within the documented size limit
    /// before anything reads it — a multi-gigabyte attacker file must be
    /// rejected by one stat call, not by hashing it for hours.</summary>
    private static bool CheckArchivePresenceAndSize(
        string archivePath, VerificationLimits limits, List<string> findings)
    {
        if (!File.Exists(archivePath))
        {
            findings.Add("the archive file does not exist");
            return false;
        }
        long length;
        try
        {
            length = new FileInfo(archivePath).Length;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            findings.Add($"archive cannot be examined: {failure.Message}");
            return false;
        }
        if (length > limits.MaxArchiveBytes)
        {
            findings.Add($"the archive is {length:N0} bytes, above the "
                + $"{limits.MaxArchiveBytes:N0}-byte documented release limit");
            return false;
        }
        return true;
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

    /// <summary>The publish step writes "<archive>.sha256" as "<hash>  <name>"
    /// (sha256sum-style: two spaces, then the archive's file name). The
    /// verifier recomputes the hash and requires both to agree, so a sidecar
    /// cannot be quietly associated with a different archive and the reported
    /// value cannot drift from the bytes. Reads are bounded: a huge sidecar is
    /// a finding, not a memory event, and an unreadable one is a finding
    /// instead of an unhandled exception.</summary>
    private static void CheckSha256Sidecar(
        string archivePath, string sha, VerificationLimits limits, List<string> findings)
    {
        var sidecarPath = archivePath + ".sha256";
        if (!File.Exists(sidecarPath))
        {
            findings.Add("there is no '<archive>.sha256' sidecar next to the archive");
            return;
        }
        long sidecarLength;
        try
        {
            sidecarLength = new FileInfo(sidecarPath).Length;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            findings.Add($"the .sha256 sidecar cannot be examined: {failure.Message}");
            return;
        }
        if (sidecarLength > limits.MaxSidecarBytes)
        {
            findings.Add($"the .sha256 sidecar is {sidecarLength:N0} bytes; "
                + $"a legitimate sidecar is one short line");
            return;
        }
        if (sha.Length != 64)
        {
            return; // hashing already failed; a sidecar comparison is impossible
        }
        string sidecar;
        try
        {
            sidecar = File.ReadAllText(sidecarPath, Encoding.UTF8).Trim();
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            findings.Add($"the .sha256 sidecar cannot be read: {failure.Message}");
            return;
        }
        // Two tokens: "<hash>  <archive file name>" (the second token names the
        // very archive next to this sidecar).
        var parts = sidecar.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            findings.Add("the .sha256 sidecar must be '<hash>  <archive name>', "
                + $"found {parts.Length} token(s)");
            return;
        }
        if (!Regex.IsMatch(parts[0], "^[0-9a-fA-F]{64}$"))
        {
            findings.Add("the .sha256 sidecar does not start with a SHA-256 hash");
            return;
        }
        if (!string.Equals(parts[0], sha, StringComparison.OrdinalIgnoreCase))
        {
            findings.Add($"the .sha256 sidecar ({parts[0][..12]}...) does not match "
                + $"the archive ({sha[..12]}...)");
            return;
        }
        if (!string.Equals(parts[1], Path.GetFileName(archivePath), StringComparison.OrdinalIgnoreCase))
        {
            findings.Add($"the .sha256 sidecar names '{parts[1]}', not this archive "
                + $"('{Path.GetFileName(archivePath)}')");
        }
    }

    /// <summary>One pass over every entry: structural checks (count, traversal,
    /// ambiguity, denylist, duplicates) decide which entries are safe to
    /// extract at all. Declared entry sizes are validated here too, but they
    /// are untrusted metadata — the real meter runs during extraction.</summary>
    private static List<ZipArchiveEntry> AuditEntries(
        ZipArchive zip, VerificationLimits limits, List<string> findings)
    {
        var extractable = new List<ZipArchiveEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalEntries = 0;
        foreach (var entry in zip.Entries)
        {
            if (ZipAudit.IsDirectoryEntry(entry.FullName))
            {
                continue;
            }
            if (++totalEntries > limits.MaxEntries)
            {
                findings.Add($"the archive holds more than the documented maximum of "
                    + $"{limits.MaxEntries} file entries; refusing to process further");
                break;
            }
            if (ZipAudit.IsTraversal(entry.FullName))
            {
                findings.Add($"entry '{ZipAudit.Normalize(entry.FullName)}' would extract "
                    + "outside the archive root and was rejected");
                continue;
            }
            if (ZipAudit.IsWindowsAmbiguous(entry.FullName))
            {
                findings.Add($"entry '{ZipAudit.Normalize(entry.FullName)}' resolves to a "
                    + "different Windows file than it names (trailing dots/space or a "
                    + "colon) and was rejected");
                continue;
            }
            if (entry.Length > limits.MaxEntryUncompressedBytes)
            {
                findings.Add($"entry '{ZipAudit.Normalize(entry.FullName)}' declares "
                    + $"{entry.Length:N0} compressed-to-expanded bytes, above the "
                    + $"{limits.MaxEntryUncompressedBytes:N0}-byte per-entry limit");
                continue;
            }
            if (ZipAudit.DeniedReason(entry.FullName) is { } reason)
            {
                findings.Add($"entry '{ZipAudit.Normalize(entry.FullName)}' must not "
                    + $"ship: {reason}");
                continue;
            }
            var normalized = ZipAudit.Normalize(entry.FullName);
            if (!seen.Add(normalized))
            {
                findings.Add($"entry '{normalized}' appears more than once in the archive");
                continue;
            }
            extractable.Add(entry);
        }
        return extractable;
    }

    /// <summary>Extraction with metered decompression into the scratch
    /// directory. The archive's declared sizes are metadata — an entry that
    /// decompresses beyond the per-entry limit, or one where the running total
    /// crosses the overall limit, aborts the extraction (declared limit lies
    /// therefore cannot become disk exhaustion). Each file is created
    /// exclusively (no overwriting, no reliance on the archive's own file
    /// attributes; there are no symlinks in the produced stream regardless of
    /// what attributes hostile entries claim). Returns false when the meter
    /// stopped the extraction early.</summary>
    private static bool ExtractEntries(
        ZipArchive zip,
        List<ZipArchiveEntry> entries,
        string extractedDirectory,
        VerificationLimits limits,
        List<string> findings)
    {
        long totalWritten = 0;
        foreach (var entry in entries)
        {
            var relative = ZipAudit.Normalize(entry.FullName)
                .Replace('/', Path.DirectorySeparatorChar);
            var destination = Path.Combine(extractedDirectory, relative);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                long entryWritten = 0;
                using var source = entry.Open();
                using var target = new FileStream(
                    destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                var buffer = new byte[81920];
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    entryWritten += read;
                    totalWritten += read;
                    if (entryWritten > limits.MaxEntryUncompressedBytes)
                    {
                        findings.Add($"entry '{ZipAudit.Normalize(entry.FullName)}' expanded "
                            + $"beyond the {limits.MaxEntryUncompressedBytes:N0}-byte "
                            + "per-entry limit during extraction; extraction stopped");
                        return false;
                    }
                    if (totalWritten > limits.MaxTotalUncompressedBytes)
                    {
                        findings.Add($"extraction exceeded the documented "
                            + $"{limits.MaxTotalUncompressedBytes:N0}-byte expanded-size "
                            + "limit; extraction stopped");
                        return false;
                    }
                    target.Write(buffer, 0, read);
                }
            }
            catch (Exception failure) when (failure is InvalidDataException or IOException
                or UnauthorizedAccessException or NotSupportedException)
            {
                findings.Add($"entry '{ZipAudit.Normalize(entry.FullName)}' cannot be "
                    + $"extracted: {failure.Message}");
                return false;
            }
        }
        return true;
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
        catch (Exception unexpected) when (PeSafeFailure(unexpected))
        {
            // The PE parser guarantees all malformed input reaches the parser's
            // own exception type; this guard exists so a latent parser bug can
            // never crash verification — an artifact suspected buggy fails
            // closed instead.
            findings.Add($"Mitosis.exe could not be audited: {unexpected.GetType().Name}");
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

    private static bool PeSafeFailure(Exception unexpected) =>
        unexpected is not PeChecks.MissingWindowsHeader
        && unexpected is not IOException && unexpected is not OutOfMemoryException;

    /// <summary>FileVersionInfo on Windows reads the version resource through the
    /// OS: exactly what Explorer and the taskbar see for end users.</summary>
    private static (string?, string?) ReadVersionInfo(string exePath)
    {
        var info = FileVersionInfo.GetVersionInfo(exePath);
        return (string.IsNullOrEmpty(info.FileVersion) ? null : info.FileVersion,
            string.IsNullOrEmpty(info.ProductVersion) ? null : info.ProductVersion);
    }

    /// <summary>Best-effort removal of the extraction scratch directory. Fails
    /// quietly: a locked leftover directory is a stray temp folder, not a
    /// verification opinion, and never masks the actual findings. The directory
    /// is unknown to callers until the call returns, so a leftover is a new,
    /// uniquely named sibling whose absence cannot confuse anything.</summary>
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
