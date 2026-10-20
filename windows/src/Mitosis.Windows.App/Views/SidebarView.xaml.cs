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

    /// <summary>macOS .searchable (CloneList.swift:26) clears the query on Esc; the search
    /// box adopts that here when there is actually text to clear. An empty box's Esc is
    /// left to the platform. Selection keys stay with the focused list.</summary>
    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && ViewModel is { } viewModel && viewModel.ClearSearch())
        {
            e.Handled = true;
        }
    }

    // macOS clears a selection only when the entry leaves the model (AppModel.swift:112);
    // a search filter that hides a row must not clear the detail page. The list views
    // therefore keep their selection out of the view model (OneWay bindings): these
    // handlers forward real user clicks, and the view model re-announces its selection
    // after every refresh so a hidden row regains its highlight when it reappears.
    private void OnClonesSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null && e.AddedItems.Count > 0
            && e.AddedItems[0] is CloneItem clone && !ReferenceEquals(clone, ViewModel.SelectedClone))
        {
            ViewModel.SelectedClone = clone;
        }
    }

    private void OnOrphansSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null && e.AddedItems.Count > 0
            && e.AddedItems[0] is OrphanProfileItem orphan
            && !ReferenceEquals(orphan.Profile, ViewModel.SelectedOrphan?.Profile))
        {
            ViewModel.SelectedOrphan = orphan;
        }
    }

    private void OnAppsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null && e.AddedItems.Count > 0
            && e.AddedItems[0] is DiscoveredAppItem app
            && !ReferenceEquals(app.App, ViewModel.SelectedApp?.App))
        {
            ViewModel.SelectedApp = app;
        }
    }

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
