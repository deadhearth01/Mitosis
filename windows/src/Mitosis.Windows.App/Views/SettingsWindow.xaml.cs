using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using System.IO;
using Windows.Graphics;
using Windows.System;
using Mitosis.Windows.ViewModels;

namespace Mitosis.Windows.App.Views;

/// <summary>Mitosis Settings (macOS 540-wide, General/Advanced tab window). Tab
/// heights follow the macOS content heights (General 470 px, Advanced 260 px) plus
/// the top switcher. General shows the application data folder with a real reveal
/// action; Advanced carries Show Tips Again. The view model performs the saves and
/// reports persistence errors; the window mirrors that state for visible feedback.</summary>
public sealed partial class SettingsWindow : Window
{
    private const int GeneralClientHeight = 510;
    private const int AdvancedClientHeight = 300;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Title = "Mitosis Settings";
        AppWindow.ResizeClient(new SizeInt32(540, GeneralClientHeight));

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.RevealDataFolderRequested += OnRevealDataFolderRequested;
        Closed += (_, _) =>
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            ViewModel.RevealDataFolderRequested -= OnRevealDataFolderRequested;
        };
        UpdateSavedState();
        _ = ViewModel.LoadAsync();
    }

    public SettingsViewModel ViewModel { get; }

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
        AppWindow.ResizeClient(new SizeInt32(540, general ? GeneralClientHeight : AdvancedClientHeight));
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
}
