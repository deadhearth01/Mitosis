namespace Mitosis.Windows.Platform.Profiles;

/// <summary>Application-owned local data locations. The profile store lives under the
/// current user's local app data; the directory is created lazily on first save, never
/// on read. Tests always substitute isolated temporary directories instead.</summary>
public static class WindowsProfilePaths
{
    public static string ProfileDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mitosis",
        "Profiles");

    /// <summary>Evidence bundles live apart from clone profiles and application data.</summary>
    public static string EvidenceDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mitosis",
        "Evidence");

    /// <summary>Application root shown by Settings ▸ Storage; profiles and evidence
    /// live beneath it. The file is created on first save like every other store.</summary>
    public static string DataDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mitosis");

    /// <summary>Preference store path (onboarded flag, tips flag).</summary>
    public static string PreferencesDirectory() => DataDirectory();
}
