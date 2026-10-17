using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Mitosis.Application.Discovery;
using Mitosis.Application.Preferences;
using Mitosis.Compatibility;
using Mitosis.Domain;
using Mitosis.Infrastructure.Evidence;
using Mitosis.Infrastructure.Preferences;
using Mitosis.Infrastructure.Profiles;
using Mitosis.Windows.Platform.Discovery;
using Mitosis.Windows.Platform.Profiles;
using Mitosis.Windows.ViewModels;

namespace Mitosis.Windows.App;

/// <summary>Application entry point. Composition root: wires read-only discovery, the
/// compatibility assessor, the profile/evidence stores, and the user-preference store
/// (welcome flow + tips) into the main window. No cloning of any kind.</summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var viewModel = ComposeMainViewModel();
        window = new MainWindow(viewModel, WindowsProfilePaths.DataDirectory());
        window.Closed += (_, _) => Bootstrap.Shutdown();
        window.Activate();
    }

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
