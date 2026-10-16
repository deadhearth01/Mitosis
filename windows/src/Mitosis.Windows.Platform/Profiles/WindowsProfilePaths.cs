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
}
