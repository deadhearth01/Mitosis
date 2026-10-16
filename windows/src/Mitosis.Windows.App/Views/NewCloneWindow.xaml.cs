using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;
using Mitosis.Windows.ViewModels;

namespace Mitosis.Windows.App.Views;

public sealed partial class NewCloneWindow : Window
{
    public NewCloneWindow(NewCloneWizardViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Title = "New Clone";
        AppWindow.Resize(new SizeInt32(700, 680));
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) => ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        UpdateStepVisibility();
        UpdateDynamicVisibility();
        Activated += OnFirstActivated;
        if (Content is FrameworkElement root)
        {
            root.KeyDown += OnContentKeyDown;
        }
    }

    private void OnFirstActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        PickSearch.Focus(FocusState.Programmatic);
    }

    public NewCloneWizardViewModel ViewModel { get; }

    /// <summary>Raised when the wizard saved a profile and the user asked to see it.</summary>
    public event EventHandler<string>? ShowProfileRequested;

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NewCloneWizardViewModel.Step))
        {
            UpdateStepVisibility();
            if (ViewModel.ShowPick)
            {
                PickSearch.Focus(FocusState.Programmatic);
            }
            else if (ViewModel.ShowDetails)
            {
                NameField.Focus(FocusState.Programmatic);
            }
        }
        else if (e.PropertyName == nameof(NewCloneWizardViewModel.CanContinueFromPick)
            || e.PropertyName == nameof(NewCloneWizardViewModel.HasValidationMessage)
            || e.PropertyName == nameof(NewCloneWizardViewModel.NeedsAcknowledgement)
            || e.PropertyName == nameof(NewCloneWizardViewModel.SelectedColorHex))
        {
            UpdateDynamicVisibility();
        }
    }

    private static readonly string[] SwatchHexes =
    {
        "#0A84FF", "#30D158", "#FF9F0A", "#FF453A",
        "#BF5AF2", "#FF375F", "#FFD60A", "#8E8E93",
    };

    private void UpdateDynamicVisibility()
    {
        UnsupportedReasonText.Visibility =
            !ViewModel.CanContinueFromPick && ViewModel.SelectedApp != null
                ? Visibility.Visible : Visibility.Collapsed;
        NameHintText.Visibility = ViewModel.HasValidationMessage ? Visibility.Collapsed : Visibility.Visible;
        NameErrorText.Visibility = ViewModel.HasValidationMessage ? Visibility.Visible : Visibility.Collapsed;
        AckCheck.Visibility = ViewModel.NeedsAcknowledgement ? Visibility.Visible : Visibility.Collapsed;
        for (var i = 0; i < SwatchHexes.Length; i++)
        {
            if (Content is FrameworkElement root && root.FindName("Ring" + i) is FrameworkElement ring)
            {
                ring.Visibility = string.Equals(SwatchHexes[i], ViewModel.SelectedColorHex,
                    StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private void UpdateStepVisibility()
    {
        StepPick.Visibility = ViewModel.ShowPick ? Visibility.Visible : Visibility.Collapsed;
        StepDetails.Visibility = ViewModel.ShowDetails ? Visibility.Visible : Visibility.Collapsed;
        StepReview.Visibility = ViewModel.ShowReview ? Visibility.Visible : Visibility.Collapsed;
        StepSaving.Visibility = ViewModel.ShowSaving ? Visibility.Visible : Visibility.Collapsed;
        StepDone.Visibility = ViewModel.ShowDone ? Visibility.Visible : Visibility.Collapsed;
        StepFailed.Visibility = ViewModel.ShowFailed ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnContentKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && !ViewModel.IsSaving)
        {
            ViewModel.Cancel();
            Close();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter)
        {
            if (ViewModel.ShowPick && ViewModel.GoToDetailsCommand.CanExecute(null))
            {
                ViewModel.GoToDetailsCommand.Execute(null);
                e.Handled = true;
            }
            else if (ViewModel.ShowDetails && ViewModel.GoToReviewCommand.CanExecute(null))
            {
                ViewModel.GoToReviewCommand.Execute(null);
                e.Handled = true;
            }
            else if (ViewModel.ShowReview && ViewModel.SaveCommand.CanExecute(null))
            {
                ViewModel.SaveCommand.Execute(null);
                e.Handled = true;
            }
            else if (ViewModel.ShowDone)
            {
                Close();
                e.Handled = true;
            }
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Cancel();
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnShowProfileClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CreatedProfile is { } created)
        {
            ShowProfileRequested?.Invoke(this, created.AppId);
        }

        Close();
    }
}
