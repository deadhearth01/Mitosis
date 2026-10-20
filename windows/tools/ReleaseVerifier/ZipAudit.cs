namespace Mitosis.ReleaseVerifier;

/// <summary>Archive-entry level rules, independent of any particular ZIP reader:
/// name normalization (both Compress-Archive's backslash entries and standard
/// slash entries), path traversal rejection, and the denylist mirroring the
/// publish recipe plus git metadata. Pure functions so the failure cases are
/// unit-testable without touching real archives.</summary>
public static class ZipAudit
{
    /// <summary>Normalizes entry names to forward slashes and, only when the
    /// name is prefixed by the benign "./" form, drops that prefix. Leading
    /// dots inside segments are preserved on purpose: they carry the traversal
    /// and git-metadata signals the other rules look for.</summary>
    public static string Normalize(string fullName)
    {
        var name = fullName.Replace('\\', '/');
        return name.StartsWith("./") ? name[2..] : name;
    }

    public static bool IsDirectoryEntry(string fullName) =>
        fullName.EndsWith('/') || string.IsNullOrEmpty(Normalize(fullName));

    /// <summary>True for entries a release archive must never accept: absolute
    /// paths, drive letters, or any parent-directory segment (the entries would
    /// otherwise describe files outside the extraction root).</summary>
    public static bool IsTraversal(string normalizedFullName)
    {
        var name = Normalize(normalizedFullName);
        if (name.StartsWith('/'))
        {
            return true;
        }
        if (name.Length >= 2 && name[1] == ':')
        {
            return true;
        }
        return name.Split('/').Any(segment => segment == "..");
    }

    /// <summary>Returns the reason a file must not ship, or null when allowed.
    /// Mirrors the publish recipe's denylist (source, project, Swift, editor
    /// settings, logs) and adds git metadata that can never belong to a
    /// published tree.</summary>
    public static string? DeniedReason(string normalizedFullName)
    {
        var name = Normalize(normalizedFullName);
        var segments = name.Split('/');
        if (segments.Any(segment => segment == ".git" || segment == ".github"))
        {
            return "git or CI metadata";
        }
        var file = segments[^1];
        if (file is ".gitignore" or ".gitattributes")
        {
            return "git metadata";
        }
        var extension = Path.GetExtension(file);
        return extension.ToLowerInvariant() switch
        {
            ".cs" => "source code",
            ".csproj" => "project file",
            ".swift" => "source code",
            ".sln" => "solution file",
            ".user" => "editor settings",
            ".log" => "log file",
            ".bak" => "backup file",
            _ => null,
        };
    }
}
