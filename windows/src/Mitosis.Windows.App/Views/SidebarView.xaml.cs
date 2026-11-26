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
    /// left to the platform. Selection keys stay with the focused list.
    /// PreviewKeyDown, not KeyDown: AutoSuggestBox handles Escape itself to close its
    /// suggestion popup and marks the event handled, so a bubbling handler never runs
    /// (found in Stage 27: Esc left the query in place).</summary>
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
    // handlers forward real user gestures, and the view model re-announces its selection
    // after every refresh so a hidden row regains its highlight when it reappears.
    //
    // SelectionChanged also fires when a refresh replaces ItemsSource: WinUI then
    // selects a row the user never touched (observed with a search query: typing moved
    // the detail page to another app; closing the wizard reset it to the first app).
    // A genuine click, tap, or arrow key leaves focus inside the list; a programmatic
    // reset does not, so only forward changes that arrive while the list has focus.
    private void OnClonesSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null && e.AddedItems.Count > 0 && HasFocusWithin(sender)
            && e.AddedItems[0] is CloneItem clone && !ReferenceEquals(clone, ViewModel.SelectedClone))
        {
            ViewModel.SelectedClone = clone;
        }
    }

    private void OnOrphansSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null && e.AddedItems.Count > 0 && HasFocusWithin(sender)
            && e.AddedItems[0] is OrphanProfileItem orphan
            && !ReferenceEquals(orphan.Profile, ViewModel.SelectedOrphan?.Profile))
        {
            ViewModel.SelectedOrphan = orphan;
        }
    }

    private void OnAppsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null && e.AddedItems.Count > 0 && HasFocusWithin(sender)
            && e.AddedItems[0] is DiscoveredAppItem app
            && !ReferenceEquals(app.App, ViewModel.SelectedApp?.App))
        {
            ViewModel.SelectedApp = app;
        }
    }

    /// <summary>True when the focused element is the list or a descendant of it. Used to
    /// tell a user-driven selection from a selection the framework applies while the
    /// ItemsSource is being replaced.</summary>
    private static bool HasFocusWithin(object? sender)
    {
        if (sender is not FrameworkElement list || list.XamlRoot is null)
        {
            return false;
        }

        var node = FocusManager.GetFocusedElement(list.XamlRoot) as DependencyObject;
        while (node != null)
        {
            if (ReferenceEquals(node, list))
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
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
