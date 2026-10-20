using System.Threading;
using Microsoft.UI.Dispatching;
using Mitosis.Windows.Platform.Startup;

namespace Mitosis.Windows.App;

/// <summary>Genuine entry point. For an unpackaged app the Windows App SDK runtime must be
/// initialized before any WinUI type is activated, so bootstrapping happens here — ahead of
/// the XAML-generated startup path (see DISABLE_XAML_GENERATED_MAIN in the project file).</summary>
internal static class Program
{
    // Returns the process exit code: 101 for the missing-prerequisite path.
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            Bootstrap.Initialize();
        }
        catch (PrerequisiteException ex)
        {
            // A machine-runtime failure happens before any WinUI surface exists, so
            // the fallback must not be a .NET crash dialog (its stack trace carries
            // local install paths). Win32 MessageBox works here and adds no
            // dependency; the message is generated without filesystem or network
            // access and names the prerequisite and its HRESULT.
            NativeMessageBox.ShowError(StartupDiagnostics.PrerequisiteDialogTitle, ex.Message);
            return 101;
        }
        global::WinRT.ComWrappersSupport.InitializeComWrappers();
        global::Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
        return 0;
    }
}
