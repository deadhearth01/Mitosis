using System.Runtime.InteropServices;
using Mitosis.Windows.Platform.Startup;

namespace Mitosis.Windows.App;

/// <summary>Initializes the Windows App SDK runtime for this unpackaged app (dynamic
/// dependencies). Requires a machine-wide Windows App SDK 2.x runtime (2.5+); the
/// Stage 2 shell was verified against 2.5.1. Throws <see cref="PrerequisiteException"/>
/// with a path-free diagnostic message when missing; the entry point shows that
/// message instead of letting the CLR crash dialog leak a stack trace.</summary>
internal static class Bootstrap
{
    private const uint MajorMinor2_5 = 0x00020005;

    public static void Initialize()
    {
        var minVersion = new PackageVersion(Major: 2, Minor: 5);
        int hr = MddBootstrapInitialize(MajorMinor2_5, null, minVersion);
        if (hr != 0)
        {
            throw new PrerequisiteException(StartupDiagnostics.DescribeMissingAppSdk(hr), hr);
        }
    }

    public static void Shutdown()
    {
        try
        {
            MddBootstrapShutdown();
        }
        catch
        {
            // Best effort during shutdown.
        }
    }

    [DllImport("Microsoft.WindowsAppRuntime.Bootstrap.dll")]
    private static extern int MddBootstrapInitialize(uint majorMinorVersion, string? versionTag, PackageVersion minVersion);

    [DllImport("Microsoft.WindowsAppRuntime.Bootstrap.dll")]
    private static extern void MddBootstrapShutdown();

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PackageVersion
    {
        public PackageVersion(ushort Major, ushort Minor)
        {
            this.Revision = 0;
            this.Build = 0;
            this.Minor = Minor;
            this.Major = Major;
        }

        public readonly ushort Revision;
        public readonly ushort Build;
        public readonly ushort Minor;
        public readonly ushort Major;
    }
}
