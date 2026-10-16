using Microsoft.UI.Xaml;
using Mitosis.Windows.ViewModels;

namespace Mitosis.Windows.App;

/// <summary>Application entry point. Builds the preview view model (Stage 2 runs on
/// explicit preview data; the engine arrives in later stages) and opens the main window.</summary>
public partial class App : Application
{
    private Window? window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var viewModel = PreviewData.CreateMain();
        window = new MainWindow(viewModel);
        window.Closed += (_, _) => Bootstrap.Shutdown();
        window.Activate();
    }
}
