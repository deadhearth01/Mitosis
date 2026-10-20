namespace Mitosis.ReleaseVerifier;

/// <summary>Archive-entry level rules, independent of any particular ZIP reader:
/// name normalization (both Compress-Archive's backslash entries and standard
/// slash entries), traversal rejection, Windows-filesystem ambiguity checks, and
/// the denylist mirroring the publish recipe plus git metadata. Pure functions
/// so the failure cases are unit-testable without touching real archives.
///
/// Entry names are treated as hostile: a ZIP entry name is not a filesystem
/// path, and Windows filesystems add their own equivalence rules (trailing
/// dots and spaces are stripped, colons introduce alternate data streams), so a
/// name that would resolve to a *different* file than it names on disk must be
/// rejected before extraction touches anything. Structural signals (an
/// absolute/leading-slash or drive-qualified name) never survive
/// normalization — the traversal rule therefore inspects the raw entry name,
/// not a collapsed one.</summary>
public static class ZipAudit
{
    /// <summary>Normalizes an entry name to a canonical relative path: forward
    /// slashes, no empty segments (double slashes and slash-run noise), and no
    /// "." segments (they would otherwise hide duplicate names like
    /// "Assets/pose.png" vs "Assets/./pose.png" that extract to the same file).
    /// Absolute-path and drive-structure is intentionally NOT preserved here —
    /// extraction accepts only names whose normalized form is safe and whose
    /// raw form passed IsTraversal.</summary>
    public static string Normalize(string fullName) =>
        string.Join('/', fullName
            .Replace('\\', '/')
            .Split('/')
            .Where(segment => segment.Length > 0 && segment != "."));

    /// <summary>Directory entries are names that normalize to nothing or end
    /// with a slash — regardless of which separator style the producer used
    /// (Compress-Archive writes directory entries as "Assets\").</summary>
    public static bool IsDirectoryEntry(string fullName)
    {
        var name = Normalize(fullName);
        return name.Length == 0 || name.EndsWith('/');
    }

    /// <summary>True for entries that would extract outside the extraction
    /// root. Examines the raw entry name, because normalization may legitimately
    /// discard structural markers: an absolute name ("/etc/passwd" or
    /// "C:\Users\...") collapses to relative-looking segments that would then
    /// extract inside the root without these raw checks.</summary>
    public static bool IsTraversal(string fullName)
    {
        var raw = fullName.Replace('\\', '/');
        if (raw.StartsWith('/'))
        {
            return true;
        }
        var firstName = raw.Split('/')[0];
        if (firstName.Length >= 2 && firstName[1] == ':')
        {
            return true;
        }
        return raw.Split('/')
            .Where(segment => segment.Length > 0 && segment != ".")
            .Any(segment => segment == "..");
    }

    /// <summary>True when Windows filesystem rules would turn the entry name
    /// into a different file than the name says: a final segment ending in a
    /// dot or space is stored as the name with the trailing characters stripped
    /// (so "Mitosis.exe." and "Mitosis.exe" extract to the same file), and a
    /// colon anywhere opens a named file stream ("Mitosis.exe:stream") instead
    /// of a file. Both enable collisions and surprise writes, so they never
    /// extract.</summary>
    public static bool IsWindowsAmbiguous(string fullName)
    {
        var name = Normalize(fullName);
        if (name.Length == 0 || name.EndsWith('/'))
        {
            return false; // directory entries have no file ambiguity
        }
        var last = name.Split('/')[^1];
        return last.EndsWith('.') || last.EndsWith(' ') || last.Contains(':');
    }

    /// <summary>Returns the reason a file must not ship, or null when allowed.
    /// Mirrors the publish recipe's denylist (source, project, Swift, editor
    /// settings, logs) and adds git metadata that can never belong to a
    /// published tree.</summary>
    public static string? DeniedReason(string fullName)
    {
        var name = Normalize(fullName);
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
