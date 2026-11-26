using System.Threading;
using Microsoft.UI.Dispatching;
using Mitosis.Application.Diagnostics;
using Mitosis.Infrastructure.Diagnostics;
using Mitosis.Windows.Platform.Profiles;
using Mitosis.Windows.Platform.Startup;

namespace Mitosis.Windows.App;

/// <summary>Genuine entry point. For an unpackaged app the Windows App SDK runtime must be
/// initialized before any WinUI type is activated, so bootstrapping happens here — ahead of
/// the XAML-generated startup path (see DISABLE_XAML_GENERATED_MAIN in the project file).
///
/// Single-instancing is decided here, before the UI exists: the first process owns a
/// per-user named mutex and shows the window; later launches ask that process to surface
/// its window and then exit without creating a duplicate (Stage 23).</summary>
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

        var log = new FileDiagnosticLog(WindowsProfilePaths.LogDirectory());
        SingleInstanceCoordinator? coordinator = null;
        SingleInstanceAttempt attempt;
        try
        {
            coordinator = SingleInstanceCoordinator.ForCurrentUser();
            attempt = coordinator.TryBecomePrimary();
        }
        catch (Exception ex)
        {
            // A guard failure must never stop the app from starting; the trade-off
            // (possible duplicate windows) is logged instead of hidden.
            attempt = SingleInstanceAttempt.Failed(ex.GetType().Name);
        }

        if (attempt.Role == SingleInstanceRole.Secondary)
        {
            log.Write(
                DiagnosticLevel.Info,
                "app.instance",
                "secondary launch; asking the running instance to surface");
            ForegroundPermission.AllowForegroundActivation();
            var delivered = coordinator?.RequestPrimaryActivation() ?? false;
            log.Write(
                delivered ? DiagnosticLevel.Info : DiagnosticLevel.Warning,
                "app.secondary-exit",
                delivered
                    ? "existing window activation requested"
                    : "no acknowledgement from the running instance; exiting without a duplicate window");
            coordinator?.Dispose();
            Bootstrap.Shutdown();
            return 0;
        }

        log.Write(
            attempt.Role == SingleInstanceRole.Failed ? DiagnosticLevel.Error : DiagnosticLevel.Info,
            "app.instance",
            attempt switch
            {
                { Role: SingleInstanceRole.Failed } =>
                    $"single-instance guard failed ({attempt.FailureReason}); continuing without it",
                { RecoveredFromAbandonedMutex: true } => "primary (recovered an abandoned lock)",
                _ => "primary",
            });

        global::WinRT.ComWrappersSupport.InitializeComWrappers();
        global::Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App(log, coordinator);
        });
        return 0;
    }
}
