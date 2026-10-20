namespace Mitosis.ReleaseVerifier;

using System.Text.RegularExpressions;

/// <summary>Pure rules for the release-candidate archive name (docs/WINDOWS-RELEASE.md):
/// "Mitosis-windows-&lt;version&gt;-x64-&lt;revision&gt;.zip" where version is MAJOR.MINOR.PATCH
/// and revision is a short git hash or the literal "nogit" (never a fabricated hash).</summary>
public static class ArchiveName
{
    public sealed record Parts(string Version, string Revision);

    public static bool TryParse(string fileName, out Parts? parts)
    {
        parts = null;
        var name = Path.GetFileName(fileName);
        var match = Regex.Match(name,
            @"^Mitosis-windows-(\d+\.\d+\.\d+)-x64-([0-9a-f]{7,40}|nogit)\.zip$");
        if (!match.Success)
        {
            return false;
        }
        parts = new Parts(match.Groups[1].Value, match.Groups[2].Value);
        return true;
    }
}

/// <summary>Pure coherence rules between the built metadata the verifier reads from
/// the packaging (FileVersion / ProductVersion of the exe) and the archive file
/// name. Returns human-readable findings; an empty list means agreement.</summary>
public static class VersionCoherence
{
    public static List<string> Check(
        string? exeFileVersion,
        string? exeProductVersion,
        string? archiveVersion,
        string archiveRevision)
    {
        var findings = new List<string>();
        if (exeFileVersion is null)
        {
            findings.Add("the executable carries no FileVersion metadata");
            return findings;
        }
        if (!Regex.IsMatch(exeFileVersion, @"^\d+\.\d+\.\d+\.\d+$"))
        {
            findings.Add($"FileVersion '{exeFileVersion}' is not four numeric components");
        }

        var shortVersion = exeFileVersion.Split('.').Take(3).ToArray();

        if (archiveVersion is null)
        {
            findings.Add("the archive name does not carry a version");
        }
        else if (!Regex.IsMatch(archiveVersion, @"^\d+\.\d+\.\d+$"))
        {
            findings.Add($"archive version '{archiveVersion}' is not MAJOR.MINOR.PATCH digits");
        }
        else if (!string.Join('.', shortVersion).Equals(archiveVersion, StringComparison.Ordinal))
        {
            findings.Add($"archive version {archiveVersion} does not equal the built version "
                + $"{string.Join('.', shortVersion)}");
        }

        if (exeProductVersion is null)
        {
            findings.Add("the executable carries no ProductVersion metadata");
        }
        else if (archiveRevision == "nogit")
        {
            // A nogit build cannot carry a hash it never read from git.
            if (!exeProductVersion.StartsWith(string.Join('.', shortVersion), StringComparison.Ordinal))
            {
                findings.Add($"ProductVersion '{exeProductVersion}' does not start with the "
                    + $"built file version {string.Join('.', shortVersion)}");
            }
            else if (exeProductVersion.Contains('+'))
            {
                findings.Add("the archive claims no git data ('nogit') but the executable "
                    + $"still embeds a hash: '{exeProductVersion}'");
            }
        }
        else if (!exeProductVersion.HasPrefix(revision: archiveRevision))
        {
            findings.Add($"revision '{archiveRevision}' in the archive name is not a prefix "
                + $"of the executable's git hash in '{exeProductVersion}'");
        }
        return findings;
    }
}

file static class StringExtensions
{
    /// <summary>Matches "Version 0.2.2+<full git commit SHA>" where the <paramref name="revision"/>
    /// argument is that commit's abbreviated form.</summary>
    public static bool HasPrefix(this string version, string revision) =>
        Regex.Match(version, @"\+([0-9a-f]+)$") is { Success: true } match
        && match.Groups[1].Value.StartsWith(revision, StringComparison.Ordinal);
}
