using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.System;
using Mitosis.Windows.ViewModels;
using Mitosis.Windows.Platform.Windowing;

namespace Mitosis.Windows.App.Views;

/// <summary>Mitosis Help (macOS Help ▸ Mitosis Help, 820×580). Content comes from
/// <see cref="HelpViewModel"/>; the only side effects are opening the Windows
/// privacy settings page or the repository issue page when a page button is used.</summary>
public sealed partial class HelpWindow : Window
{
    public HelpWindow(HelpViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Title = "Mitosis Help";
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            // macOS floors the help window at 700×500 (HelpView.swift:18).
            presenter.PreferredMinimumWidth = WindowSizes.HelpMinimumWidth;
            presenter.PreferredMinimumHeight = WindowSizes.HelpMinimumHeight;
        }

        AppWindow.ResizeClient(new SizeInt32(
            WindowSizes.HelpDefaultWidth, WindowSizes.HelpDefaultHeight));
        RootGrid.DataContext = ViewModel;
        Activated += OnFirstActivated;
    }

    public HelpViewModel ViewModel { get; }

    /// <summary>Keyboard users start on the topic list (the window's primary working
    /// control) instead of nowhere; the wizard has used this pattern since Stage 12
    /// and NewCloneWindow.OnFirstActivated documents the rationale. AppKit gives an
    /// opened sheet a first responder by default; WinUI does not, so this is explicit.</summary>
    private void OnFirstActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        TopicList.Focus(FocusState.Programmatic);
    }

    private void OnOpenTopicUrl(object sender, RoutedEventArgs e)
    {
        if (ViewModel.PageButton is { } button)
        {
            _ = Launcher.LaunchUriAsync(new Uri(HelpViewModel.UriFor(button.Kind)));
        }
    }
}
