using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Mitosis.Windows.Platform.Icons;
using Mitosis.Windows.ViewModels;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Mitosis.Windows.App.Views;

public sealed partial class AppDetailView : UserControl
{
    private readonly AppIconLoader iconLoader;
    private string? headerIconPath;

    public AppDetailView()
    {
        InitializeComponent();
        iconLoader = new AppIconLoader(DispatcherQueue);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public MainViewModel ViewModel
    {
        get => (MainViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel), typeof(AppDetailView), new PropertyMetadata(null, OnViewModelChanged));

    private static void OnViewModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not AppDetailView view)
        {
            return;
        }

        if (args.OldValue is MainViewModel old)
        {
            old.PropertyChanged -= view.OnViewModelPropertyChanged;
        }

        if (args.NewValue is MainViewModel current)
        {
            current.PropertyChanged += view.OnViewModelPropertyChanged;
        }

        view.UpdateHeaderIcon();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => UpdateHeaderIcon();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.SelectedApp) or nameof(MainViewModel.SelectedAppPublisherLine))
        {
            UpdateHeaderIcon();
        }
    }

    /// <summary>The 92px header tile prefers the application's own icon (read-only
    /// thumbnail of the registry <c>DisplayIcon</c>) and falls back to the letter tile
    /// whenever the reference is missing or unreadable.</summary>
    private void UpdateHeaderIcon()
    {
        var path = AppIconReference.Parse(ViewModel?.SelectedApp?.App.IconReference);
        if (string.Equals(path, headerIconPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        headerIconPath = path;
        HeaderIcon.Source = null;
        HeaderIcon.Visibility = Visibility.Collapsed;
        HeaderBadge.Visibility = Visibility.Visible;
        if (path is null)
        {
            return;
        }

        _ = ShowHeaderIconAsync(path);
    }

    private async Task ShowHeaderIconAsync(string path)
    {
        var source = await iconLoader.LoadAsync(path, 128).ConfigureAwait(true);
        if (source is null || !string.Equals(path, headerIconPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        HeaderIcon.Source = source;
        HeaderIcon.Visibility = Visibility.Visible;
        HeaderBadge.Visibility = Visibility.Collapsed;
    }

    private async void OnPreviewPlanClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Guid id
            || ViewModel.FindProfile(id) is not { } profile
            || ViewModel.SelectedApp is not { } item)
        {
            return;
        }

        var preview = PlanPreviewViewModel.Create(item.App, profile, item.Assessment);
        await PlanDialog.ShowAsync(XamlRoot, preview);
    }

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
