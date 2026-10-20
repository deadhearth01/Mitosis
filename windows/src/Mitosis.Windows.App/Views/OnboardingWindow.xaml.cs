using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Windowing;
using System.ComponentModel;
using Windows.Graphics;
using Mitosis.Windows.ViewModels;
using Mitosis.Windows.Platform.Windowing;

namespace Mitosis.Windows.App.Views;

/// <summary>First-launch welcome flow (macOS 580×460, three pages). Non-dismissable:
/// window closing is refused while <see cref="OnboardingViewModel.Finished"/> is false,
/// so Esc, Alt+F4, and the title-bar close cannot skip the flow — matching macOS
/// interactiveDismissDisabled. Finishing (Later or Choose an App) persists the
/// onboarded flag and closes the window; Choose an App additionally asks the main
/// window to open the existing New Clone wizard.</summary>
public sealed partial class OnboardingWindow : Window
{
    private readonly OnboardingViewModel viewModel;

    public OnboardingWindow(OnboardingViewModel viewModel)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        Title = "Welcome to Mitosis";
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            // The macOS onboarding sheet is a fixed 580×460 (spec §2); mirroring that
            // keeps the three-page flow's layout intent instead of allowing resize.
            presenter.PreferredMinimumWidth = WindowSizes.OnboardingWidth;
            presenter.PreferredMinimumHeight = WindowSizes.OnboardingHeight;
            presenter.PreferredMaximumWidth = WindowSizes.OnboardingWidth;
            presenter.PreferredMaximumHeight = WindowSizes.OnboardingHeight;
            presenter.IsMaximizable = false;
        }

        AppWindow.ResizeClient(new SizeInt32(
            WindowSizes.OnboardingWidth, WindowSizes.OnboardingHeight));
        RootGrid.DataContext = this.viewModel;
        AppWindow.Closing += OnClosing;
        this.viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) =>
        {
            AppWindow.Closing -= OnClosing;
            this.viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        };
        UpdatePage();
        Activated += OnFirstActivated;
    }

    public OnboardingViewModel ViewModel => viewModel;

    /// <summary>Keyboard users start on the flow's progression button (Next on early
    /// pages, the closing actions on the last) instead of nowhere; the wizard has
    /// used this pattern since Stage 12. AppKit gives an opened sheet a first
    /// responder by default; WinUI does not, so focus is set explicitly.</summary>
    private void OnFirstActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        if (NextButton.Visibility == Visibility.Visible)
        {
            NextButton.Focus(FocusState.Programmatic);
        }
        else if (ChooseAppButton.Visibility == Visibility.Visible)
        {
            ChooseAppButton.Focus(FocusState.Programmatic);
        }
        else
        {
            LaterButton.Focus(FocusState.Programmatic);
        }
    }

    /// <summary>Raised with true when the user chose Choose an App; the owner opens
    /// the New Clone wizard after the window closes.</summary>
    public event EventHandler<bool>? Completed;

    private void OnClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        // Deterministic non-dismissable rule: only a finished flow releases the guard.
        if (!viewModel.Finished)
        {
            args.Cancel = true;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(OnboardingViewModel.PageIndex):
                UpdatePage();
                break;
            case nameof(OnboardingViewModel.ErrorMessage):
                SaveErrorBar.IsOpen = viewModel.ErrorMessage != null;
                SaveErrorBar.Message = viewModel.ErrorMessage ?? string.Empty;
                break;
        }
    }

    private void UpdatePage()
    {
        PageCaption.Visibility = viewModel.PageShowCaption ? Visibility.Visible : Visibility.Collapsed;
        BackButton.Visibility = viewModel.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
        var onLast = viewModel.IsLast;
        NextButton.Visibility = onLast ? Visibility.Collapsed : Visibility.Visible;
        LaterButton.Visibility = onLast ? Visibility.Visible : Visibility.Collapsed;
        ChooseAppButton.Visibility = onLast ? Visibility.Visible : Visibility.Collapsed;

        // Visual dots + the macOS speech-only "Page N of 3" label (Onboarding.swift:61).
        for (var dot = 0; dot < Dots.Children.Count; dot++)
        {
            if (Dots.Children[dot] is Ellipse ellipse)
            {
                ellipse.Fill = dot == viewModel.PageIndex
                    ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["MitosisAccentBrush"]
                    : new SolidColorBrush(Colors.Gray) { Opacity = 0.35 };
            }
        }

        AutomationProperties.SetName(Dots, viewModel.PageLabel);
    }

    private void OnBack(object sender, RoutedEventArgs e) => viewModel.PreviousPageCommand.Execute(null);

    private void OnNext(object sender, RoutedEventArgs e) => viewModel.NextPageCommand.Execute(null);

    private async void OnLater(object sender, RoutedEventArgs e) =>
        await FinishAsync(startFirstClone: false);

    private async void OnChooseApp(object sender, RoutedEventArgs e) =>
        await FinishAsync(startFirstClone: true);

    private async Task FinishAsync(bool startFirstClone)
    {
        await viewModel.FinishAsync(startFirstClone, CancellationToken.None);
        if (viewModel.Finished)
        {
            Completed?.Invoke(this, startFirstClone);
            Close();
        }
    }
}
