using System.Runtime.InteropServices;

namespace Mitosis.Windows.Platform.Startup;

/// <summary>Lets the already-running Mitosis process raise its own window when a
/// second launch is redirected to it. Windows normally prevents a background process
/// from taking the foreground; the process the user just launched is allowed to grant
/// that permission for one activation. Best effort: if the call fails, the existing
/// window is still activated but may stay behind other windows.</summary>
public static class ForegroundPermission
{
    private const int AsfwAny = -1; // AllowSetForegroundWindow(ASFW_ANY)

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);

    public static void AllowForegroundActivation()
    {
        try
        {
            AllowSetForegroundWindow(AsfwAny);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Older or unusual user32 surfaces: focus falls back to normal rules.
        }
    }
}
