using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Mitosis.Windows.ViewModels;

namespace Mitosis.Windows.App.Views;

public sealed partial class SidebarView : UserControl
{
    public SidebarView()
    {
        InitializeComponent();
    }

    public MainViewModel ViewModel
    {
        get => (MainViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel), typeof(SidebarView), new PropertyMetadata(null));

    private void OnListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel?.SelectedClone == null)
        {
            return;
        }

        if (e.Key == VirtualKey.Enter && ViewModel.OpenCloneCommand.CanExecute(null))
        {
            ViewModel.OpenCloneCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Delete && ViewModel.DeleteCommand.CanExecute(null))
        {
            ViewModel.DeleteCommand.Execute(null);
            e.Handled = true;
        }
    }

    public void FocusSearch() => SearchBox.Focus(FocusState.Programmatic);

    private void OnAppsKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter
            && ViewModel?.SelectedApp != null
            && ViewModel.CreateCloneFromAppCommand.CanExecute(null))
        {
            ViewModel.CreateCloneFromAppCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnListRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (ViewModel == null || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var item = FindParent<ListViewItem>(source);
        if (item?.Content is CloneItem clone)
        {
            ViewModel.SelectedClone = clone;
        }
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null && parent is not T)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }

        return parent as T;
    }
}
