using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Mitosis.Windows.ViewModels;

namespace Mitosis.Windows.App.Views;

public sealed partial class AppDetailView : UserControl
{
    public AppDetailView()
    {
        InitializeComponent();
    }

    public MainViewModel ViewModel
    {
        get => (MainViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel), typeof(AppDetailView), new PropertyMetadata(null));

    private async void OnEditProfileClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is Guid id && ViewModel.FindProfile(id) is { } profile)
        {
            await ProfileDialogs.EditAsync(XamlRoot, ViewModel, profile);
        }
    }

    private async void OnRemoveProfileClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is Guid id && ViewModel.FindProfile(id) is { } profile)
        {
            await ProfileDialogs.RemoveAsync(XamlRoot, ViewModel, profile);
        }
    }
}
