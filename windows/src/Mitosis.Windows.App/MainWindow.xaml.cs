using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.ComponentModel;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Core;
using Mitosis.Windows.ViewModels;
using Mitosis.Windows.App.Views;

namespace Mitosis.Windows.App;

public sealed partial class MainWindow : Window
{
    private readonly string? dataFolderPath;

    public MainWindow(MainViewModel viewModel, string? dataFolderPath = null)
    {
        ViewModel = viewModel;
        this.dataFolderPath = dataFolderPath;
        InitializeComponent();
        Title = "Mitosis";
        AppWindow.Resize(new SizeInt32(1060, 720));
        ViewModel.DialogRequested += OnDialogRequested;
        ViewModel.NewCloneRequested += OnNewCloneRequested;
        ViewModel.ShowHelpRequested += OnShowHelpRequested;
        ViewModel.ShowSettingsRequested += OnShowSettingsRequested;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) =>
        {
            ViewModel.DialogRequested -= OnDialogRequested;
            ViewModel.NewCloneRequested -= OnNewCloneRequested;
            ViewModel.ShowHelpRequested -= OnShowHelpRequested;
            ViewModel.ShowSettingsRequested -= OnShowSettingsRequested;
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        };
        UpdateDetailVisibility();
        if (Content is FrameworkElement root)
        {
            root.KeyDown += OnContentKeyDown;
        }

        Activated += OnFirstActivated;
    }

    private async void OnFirstActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        await ViewModel.LoadAsync();
        if (ViewModel.NeedsOnboarding)
        {
            ViewModel.ClearOnboardingRequest();
            ShowOnboarding();
        }
    }

    private void OnContentKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(CoreVirtualKeyStates.Down);
        if (e.Key == VirtualKey.F1)
        {
            OnShowHelpRequested(this, EventArgs.Empty);
            e.Handled = true;
        }
        // WinUI's VirtualKey enum lacks an OEM comma member; 188 is its fixed value.
        else if (ctrl && e.Key == (VirtualKey)188)
        {
            OnShowSettingsRequested(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (ctrl && e.Key == VirtualKey.F)
        {
            Sidebar.FocusSearch();
            e.Handled = true;
        }
    }

    public MainViewModel ViewModel { get; }

    public string BannerTitle => $"Mitosis {ViewModel.BannerVersion} is available.";

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.MainViewModel.HasSelection))
        {
            UpdateDetailVisibility();
        }
    }

    private NewCloneWindow? wizardWindow;

    private void OnNewCloneRequested(object? sender, EventArgs e)
    {
        if (wizardWindow != null)
        {
            wizardWindow.Activate();
            return;
        }

        var window = new Views.NewCloneWindow(ViewModel.CreateWizard());
        wizardWindow = window;
        window.Closed += OnWizardClosed;
        window.ShowProfileRequested += OnShowProfileRequested;
        window.Activate();
    }

    private HelpWindow? helpWindow;

    private SettingsWindow? settingsWindow;

    private OnboardingWindow? onboardingWindow;

    private HelpWindow OpenHelp()
    {
        if (helpWindow == null)
        {
            helpWindow = new HelpWindow(new HelpViewModel());
            helpWindow.Closed += (_, _) => helpWindow = null;
        }

        return helpWindow;
    }

    private void OnShowHelpRequested(object? sender, EventArgs e) => OpenHelp().Activate();

    private SettingsWindow OpenSettings()
    {
        if (settingsWindow == null)
        {
            settingsWindow = new SettingsWindow(
                new SettingsViewModel(
                    ViewModel.PreferencesStore
                        ?? throw new InvalidOperationException("Settings need the preference store."),
                    dataFolderPath
                        ?? throw new InvalidOperationException("Settings were composed without a data folder.")));
            settingsWindow.Closed += (_, _) => settingsWindow = null;
        }

        return settingsWindow;
    }

    private void OnShowSettingsRequested(object? sender, EventArgs e) => OpenSettings().Activate();

    private void ShowOnboarding()
    {
        if (onboardingWindow != null)
        {
            onboardingWindow.Activate();
            return;
        }

        var store = ViewModel.PreferencesStore
            ?? throw new InvalidOperationException("The welcome flow needs the preference store.");
        var window = new OnboardingWindow(new OnboardingViewModel(store));
        onboardingWindow = window;
        window.Completed += OnOnboardingCompleted;
        window.Closed += (_, _) =>
        {
            onboardingWindow = null;
        };
        window.Activate();
    }

    private void OnOnboardingCompleted(object? sender, bool startFirstClone)
    {
        if (sender is Views.OnboardingWindow window)
        {
            window.Completed -= OnOnboardingCompleted;
        }

        if (startFirstClone)
        {
            OnNewCloneRequested(this, EventArgs.Empty);
        }
    }

    private async void OnWizardClosed(object sender, WindowEventArgs args)
    {
        if (sender is Views.NewCloneWindow window)
        {
            window.Closed -= OnWizardClosed;
            window.ShowProfileRequested -= OnShowProfileRequested;
            wizardWindow = null;
            await ViewModel.LoadAsync();
        }
    }

    private void OnShowProfileRequested(object? sender, string appId)
    {
        ViewModel.SelectAppById(appId);
    }

    private void UpdateDetailVisibility()
    {
        DetailView.Visibility = ViewModel.ShowCloneDetail ? Visibility.Visible : Visibility.Collapsed;
        AppView.Visibility = ViewModel.ShowAppDetail ? Visibility.Visible : Visibility.Collapsed;
        OrphanView.Visibility = ViewModel.ShowOrphanDetail ? Visibility.Visible : Visibility.Collapsed;
        EmptyPanel.Visibility = ViewModel.HasSelection ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnDialogRequested(object? sender, DialogRequest request)
    {
        var dialog = new ContentDialog
        {
            Title = request.Title,
            Content = request.Message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
