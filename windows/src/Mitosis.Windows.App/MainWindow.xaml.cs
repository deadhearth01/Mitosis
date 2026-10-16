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
    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Title = "Mitosis";
        AppWindow.Resize(new SizeInt32(1060, 720));
        ViewModel.DialogRequested += OnDialogRequested;
        ViewModel.NewCloneRequested += OnNewCloneRequested;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) =>
        {
            ViewModel.DialogRequested -= OnDialogRequested;
            ViewModel.NewCloneRequested -= OnNewCloneRequested;
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
    }

    private void OnContentKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(CoreVirtualKeyStates.Down);
        if (ctrl && e.Key == VirtualKey.F)
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
