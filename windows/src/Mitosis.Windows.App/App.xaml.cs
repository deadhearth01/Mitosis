using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Mitosis.Application.Diagnostics;
using Mitosis.Application.Discovery;
using Mitosis.Application.Preferences;
using Mitosis.Compatibility;
using Mitosis.Domain;
using Mitosis.Infrastructure.Diagnostics;
using Mitosis.Infrastructure.Evidence;
using Mitosis.Infrastructure.Preferences;
using Mitosis.Infrastructure.Profiles;
using Mitosis.Windows.Platform.Discovery;
using Mitosis.Windows.Platform.Profiles;
using Mitosis.Windows.Platform.Startup;
using Mitosis.Windows.ViewModels;

namespace Mitosis.Windows.App;

/// <summary>Application entry point. Composition root: wires read-only discovery, the
/// compatibility assessor, the profile/evidence stores, the user-preference store
/// (welcome flow + tips), and local diagnostics into the main window. No cloning of
/// any kind.</summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? window;
    private IDiagnosticLog? diagnosticLog;
    private bool handlingCrash;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var facts = DescribeEnvironment();
        diagnosticLog = new FileDiagnosticLog(WindowsProfilePaths.LogDirectory());
        diagnosticLog.Write(
            DiagnosticLevel.Info,
            "app.start",
            $"{facts.AppVersion}; {facts.OperatingSystem}; {facts.RuntimeVersion}");

        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        UnhandledException += OnUiUnhandledException;

        var viewModel = ComposeMainViewModel();
        window = new MainWindow(viewModel, WindowsProfilePaths.DataDirectory(), facts);
        window.Closed += (_, _) =>
        {
            diagnosticLog?.Write(DiagnosticLevel.Info, "app.stop", "main window closed");
            Bootstrap.Shutdown();
        };
        window.Activate();
    }

    /// <summary>Version and path facts shown in Settings ▸ Diagnostics. No application
    /// data, credentials, or file contents are read.</summary>
    private static DoctorFacts DescribeEnvironment()
    {
        var assembly = typeof(App).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
        var dataDirectory = WindowsProfilePaths.DataDirectory();
        return new DoctorFacts(
            version,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            typeof(Microsoft.UI.Xaml.Application).Assembly.GetName().Version?.ToString() ?? "unknown",
            dataDirectory,
            ProbeWritable(dataDirectory),
            WindowsProfilePaths.LogDirectory());
    }

    /// <summary>Writes and removes one probe file under the Mitosis data folder. This
    /// only proves the folder is writable; it never reads or modifies user data.</summary>
    private static bool ProbeWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".write-probe-{Guid.NewGuid():N}");
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

    private void OnUiUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        diagnosticLog?.Write(DiagnosticLevel.Error, "app.unhandled", Describe(e.Exception));
        if (handlingCrash)
        {
            return;
        }

        handlingCrash = true;
        e.Handled = true;
        NativeMessageBox.ShowError(
            "Mitosis hit an unexpected error",
            "Mitosis hit an unexpected error and needs to close.\n\n"
            + "Details were written to the local log folder:\n"
            + (diagnosticLog?.DirectoryPath ?? WindowsProfilePaths.LogDirectory())
            + "\n\nNo application data was changed.");
        Exit();
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        diagnosticLog?.Write(
            DiagnosticLevel.Error,
            "app.domain-unhandled",
            e.ExceptionObject is Exception exception ? Describe(exception) : "non-exception failure");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        diagnosticLog?.Write(DiagnosticLevel.Error, "app.unobserved-task", Describe(e.Exception));
        e.SetObserved();
    }

    private static string Describe(Exception exception) =>
        $"{exception.GetType().FullName}: {exception.Message}";

    private static MainViewModel ComposeMainViewModel()
    {
        var reader = new RegistryKeyReader();
        var sources = new IInstalledAppSource[]
        {
            new UninstallRegistrySource(
                RegistryHive.CurrentUser, RegistryView.Default,
                DiscoverySource.CurrentUserUninstall, InstallScope.CurrentUser, reader),
            new UninstallRegistrySource(
                RegistryHive.LocalMachine, RegistryView.Registry64,
                DiscoverySource.LocalMachineUninstall64, InstallScope.Machine, reader),
            new UninstallRegistrySource(
                RegistryHive.LocalMachine, RegistryView.Registry32,
                DiscoverySource.LocalMachineUninstall32, InstallScope.Machine, reader),
            new AppxPackageSource(new PackageManagerEnumerator()),
        };
        return new MainViewModel(
            new WindowsAppDiscoveryService(sources),
            new CompatibilityAssessor(Array.Empty<CompatibilityStrategy>()),
            new JsonCloneProfileRepository(WindowsProfilePaths.ProfileDirectory()),
            new JsonEvidenceStore(WindowsProfilePaths.EvidenceDirectory()),
            new JsonAppPreferencesStore(WindowsProfilePaths.PreferencesDirectory()));
    }
}
