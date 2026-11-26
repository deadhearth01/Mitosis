using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using System.ComponentModel;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.System;
using Mitosis.Windows.ViewModels;
using Mitosis.Windows.Platform.Windowing;

namespace Mitosis.Windows.App.Views;

/// <summary>Mitosis Settings (macOS 540-wide, General/Advanced tab window). Tab
/// heights follow the macOS content heights (General 470 px, Advanced 260 px) plus
/// the top switcher; the Windows-only Diagnostics section adds to General. General
/// shows the application data folder and local log folder with real reveal actions;
/// Advanced carries Show Tips Again. The view model performs the saves and reports
/// persistence errors; the window mirrors that state for visible feedback.</summary>
public sealed partial class SettingsWindow : Window
{
    private const int GeneralClientHeight = 740;
    private const int AdvancedClientHeight = 300;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Title = "Mitosis Settings";
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            // macOS fixes the Settings width at 540 (SettingsView.swift:16 .frame(width:));
            // locking preferred min/max to the same value reproduces that on Windows while
            // height stays resizable to follow tab content.
            presenter.PreferredMinimumWidth = WindowSizes.SettingsWidth;
            presenter.PreferredMaximumWidth = WindowSizes.SettingsWidth;
        }

        AppWindow.ResizeClient(new SizeInt32(WindowSizes.SettingsWidth, GeneralClientHeight));

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.RevealDataFolderRequested += OnRevealDataFolderRequested;
        ViewModel.RevealLogFolderRequested += OnRevealLogFolderRequested;
        ViewModel.CopyDiagnosticsRequested += OnCopyDiagnosticsRequested;
        Closed += (_, _) =>
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            ViewModel.RevealDataFolderRequested -= OnRevealDataFolderRequested;
            ViewModel.RevealLogFolderRequested -= OnRevealLogFolderRequested;
            ViewModel.CopyDiagnosticsRequested -= OnCopyDiagnosticsRequested;
            ViewModel.Dispose();
        };
        UpdateSavedState();
        _ = ViewModel.LoadAsync();
        Activated += OnFirstActivated;
    }

    public SettingsViewModel ViewModel { get; }

    /// <summary>Keyboard users start on the General tab item (the already-selected
    /// tab) instead of nowhere; NewCloneWindow.OnFirstActivated established the
    /// pattern (AppKit gives an opened sheet a first responder; WinUI does not).</summary>
    private void OnFirstActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        // Activated can run before the window's content has finished loading; a
        // Focus() call at that moment silently does nothing (found in Stage 27:
        // the Settings tab never received keyboard focus). Low priority runs after
        // the first layout and activation work, so the tab really gets focus.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () => GeneralTab.Focus(FocusState.Programmatic));
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.TipsReset)
            || e.PropertyName == nameof(SettingsViewModel.SaveError))
        {
            UpdateSavedState();
        }
    }

    private void UpdateSavedState()
    {
        TipsButton.IsEnabled = !ViewModel.TipsReset;
        TipsButton.Content = ViewModel.TipsReset ? "Tips Are On" : "Show Tips Again";
        SaveErrorBar.IsOpen = ViewModel.SaveError != null;
        SaveErrorBar.Message = ViewModel.SaveError ?? string.Empty;
    }

    private void OnTabSelected(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var isGeneral = args.SelectedItem is NavigationViewItem { Tag: "General" or null };
        ShowTab(isGeneral);
    }

    private void ShowTab(bool general)
    {
        GeneralPanel.Visibility = general ? Visibility.Visible : Visibility.Collapsed;
        AdvancedPanel.Visibility = general ? Visibility.Collapsed : Visibility.Visible;
        AppWindow.ResizeClient(new SizeInt32(
            WindowSizes.SettingsWidth, general ? GeneralClientHeight : AdvancedClientHeight));
    }

    private async void OnRevealDataFolderRequested(object? sender, EventArgs e)
    {
        try
        {
            Directory.CreateDirectory(ViewModel.DataFolderPath);
            await Launcher.LaunchFolderPathAsync(ViewModel.DataFolderPath);
        }
        catch
        {
            // Honest no-op: a failed reveal never changes state; the folder path is
            // selectable text above the button for manual navigation.
        }
    }

    private async void OnRevealLogFolderRequested(object? sender, EventArgs e)
    {
        try
        {
            Directory.CreateDirectory(ViewModel.LogFolderPath);
            await Launcher.LaunchFolderPathAsync(ViewModel.LogFolderPath);
        }
        catch
        {
            // Same honest no-op: the log folder path stays selectable above.
        }
    }

    private void OnCopyDiagnosticsRequested(object? sender, string report)
    {
        var package = new DataPackage();
        package.SetText(report);
        Clipboard.SetContent(package);
        CopyConfirmation.Visibility = Visibility.Visible;
    }
}
