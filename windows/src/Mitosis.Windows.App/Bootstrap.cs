using System.Runtime.InteropServices;

namespace Mitosis.Windows.App;

/// <summary>Initializes the Windows App SDK runtime for this unpackaged app (dynamic
/// dependencies). Requires a machine-wide Windows App SDK 2.x runtime; the Stage 2
/// shell was verified against 2.5.1. Throws a descriptive error when missing.</summary>
internal static class Bootstrap
{
    private const uint MajorMinor2_5 = 0x00020005;

    public static void Initialize()
    {
        var minVersion = new PackageVersion(Major: 2, Minor: 5);
        int hr = MddBootstrapInitialize(MajorMinor2_5, null, minVersion);
        if (hr != 0)
        {
            throw new InvalidOperationException(
                $"The Windows App SDK runtime could not be initialized (HRESULT 0x{hr:X8}). " +
                "Install the Windows App SDK 2.x runtime, then relaunch Mitosis. See windows/README.md.");
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
