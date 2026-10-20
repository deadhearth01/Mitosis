using System.Runtime.InteropServices;

namespace Mitosis.Windows.Platform.Startup;

/// <summary>Win32 MessageBox used only before any WinUI/app-model type may safely
/// activate — currently for the missing-runtime prerequisite error. Win32 is on the
/// machine runtime for every target this app supports, so this adds no dependency.
/// Error style plus foreground/topmost flags so the dialog is not hidden behind an
/// app window that did not finish launching.</summary>
public static class NativeMessageBox
{
    private const uint MbIconError = 0x00000010;
    private const uint MbSetForeground = 0x00010000;
    private const uint MbTopmost = 0x00040000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    public static void ShowError(string caption, string text)
    {
        MessageBoxW(IntPtr.Zero, text, caption, MbIconError | MbSetForeground | MbTopmost);
    }
}
