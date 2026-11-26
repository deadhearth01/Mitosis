using System.Reflection;
using System.Runtime.InteropServices;
using Mitosis.Application;
using Mitosis.Application.Diagnostics;
using Mitosis.Windows.Platform.Profiles;

namespace Mitosis.Cli;

/// <summary>Machine facts and the two Mitosis-owned folders used by the read-only
/// commands. Everything is injected so tests never touch the real profile store.</summary>
public sealed record CliEnvironment(
    string AppVersion,
    string RuntimeVersion,
    string OperatingSystem,
    string Architecture,
    string DataDirectory,
    string LogDirectory)
{
    /// <summary>The CLI does not load WinUI, so it cannot read the Windows App SDK
    /// version the UI runs against. It says so instead of guessing.</summary>
    public const string WindowsAppSdkVersion = "not loaded by the CLI";

    public static CliEnvironment Current() => new(
        ProductVersion.Describe(Assembly.GetExecutingAssembly()),
        RuntimeInformation.FrameworkDescription,
        RuntimeInformation.OSDescription,
        RuntimeInformation.OSArchitecture.ToString(),
        WindowsProfilePaths.DataDirectory(),
        WindowsProfilePaths.LogDirectory());

    public DoctorFacts ToFacts() => new(
        AppVersion,
        RuntimeVersion,
        OperatingSystem,
        Architecture,
        WindowsAppSdkVersion,
        DataDirectory,
        ProbeWritable(DataDirectory),
        LogDirectory);

    /// <summary>Proves the data folder can be written by creating and deleting one
    /// zero-byte probe file. <c>doctor</c> is the only command that does this; no
    /// profile, preference, or application data is created or changed. Unlike the
    /// GUI, the CLI does not create the data folder: when it is absent the probe
    /// runs in the nearest existing parent, which is exactly the folder that would
    /// have to accept it.</summary>
    internal static bool ProbeWritable(string directory)
    {
        var probeDirectory = ExistingAncestor(directory);
        if (probeDirectory is null)
        {
            return false;
        }

        try
        {
            var probe = Path.Combine(probeDirectory, $".cli-probe-{Guid.NewGuid():N}");
            using var stream = new FileStream(
                probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            stream.WriteByte(0);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    private static string? ExistingAncestor(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        for (var current = directory; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            if (Directory.Exists(current))
            {
                return current;
            }
        }

        return null;
    }
}
