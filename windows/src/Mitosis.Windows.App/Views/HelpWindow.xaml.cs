using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using Windows.System;
using Mitosis.Windows.ViewModels;

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
        AppWindow.ResizeClient(new SizeInt32(820, 580));
        RootGrid.DataContext = ViewModel;
    }

    public HelpViewModel ViewModel { get; }

    private void OnOpenTopicUrl(object sender, RoutedEventArgs e)
    {
        if (ViewModel.PageButton is { } button)
        {
            _ = Launcher.LaunchUriAsync(new Uri(HelpViewModel.UriFor(button.Kind)));
        }
    }
}
