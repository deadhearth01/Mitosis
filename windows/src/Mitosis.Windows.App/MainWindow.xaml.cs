using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Mitosis.Application.Diagnostics;
using Mitosis.Windows.ViewModels;
using Mitosis.Windows.App.Views;
using Mitosis.Windows.Platform.Input;
using Mitosis.Windows.Platform.Windowing;

namespace Mitosis.Windows.App;

public sealed partial class MainWindow : Window
{
    private readonly string? dataFolderPath;
    private readonly DoctorFacts? diagnostics;

    public MainWindow(MainViewModel viewModel, string? dataFolderPath = null, DoctorFacts? diagnostics = null)
    {
        ViewModel = viewModel;
        this.dataFolderPath = dataFolderPath;
        this.diagnostics = diagnostics;
        InitializeComponent();
        Title = "Mitosis";
        ApplyMainWindowLimits();
        ApplyModernChrome();
        AppWindow.Resize(new SizeInt32(WindowSizes.MainDefaultWidth, WindowSizes.MainDefaultHeight));
        ViewModel.DialogRequested += OnDialogRequested;
        ViewModel.NewCloneRequested += OnNewCloneRequested;
        ViewModel.ShowHelpRequested += OnShowHelpRequested;
        ViewModel.ShowSettingsRequested += OnShowSettingsRequested;
        ViewModel.OpenReleaseRequested += OnOpenReleaseRequested;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) =>
        {
            ViewModel.DialogRequested -= OnDialogRequested;
            ViewModel.NewCloneRequested -= OnNewCloneRequested;
            ViewModel.ShowHelpRequested -= OnShowHelpRequested;
            ViewModel.ShowSettingsRequested -= OnShowSettingsRequested;
            ViewModel.OpenReleaseRequested -= OnOpenReleaseRequested;
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        };
        UpdateDetailVisibility();
        if (Content is FrameworkElement root)
        {
            root.KeyDown += OnContentKeyDown;
        }

        Activated += OnFirstActivated;
    }

    /// <summary>macOS floors the main window at 820×520 (MitosisRootApp.swift:17);
    /// the overlapped presenter enforces the same bounds natively on Windows. Applied
    /// best effort: a null or non-overlapped presenter (custom shells) simply keeps
    /// the default behavior instead of crashing.</summary>
    private void ApplyMainWindowLimits()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = WindowSizes.MainMinimumWidth;
            presenter.PreferredMinimumHeight = WindowSizes.MainMinimumHeight;
        }
    }

    /// <summary>macOS-style chrome: the content supplies its own unified title bar and the
    /// window sits on a Mica material, so the sidebar keeps a hint of the desktop the way
    /// the AppKit vibrant sidebar does. Best effort only - a composition target that
    /// refuses the backdrop keeps the stock title bar instead of failing the launch.</summary>
    private void ApplyModernChrome()
    {
        try
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or NotSupportedException)
        {
            // No Mica on this build: the themed solid surfaces below still render.
        }

        try
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            var titleBar = AppWindow.TitleBar;
            titleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
            TitleBarRow.Height = new GridLength(titleBar.Height > 0 ? titleBar.Height : 32);
            UpdateTitleBarColors();
            if (Content is FrameworkElement root)
            {
                root.ActualThemeChanged += (_, _) => UpdateTitleBarColors();
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or NotSupportedException)
        {
            ExtendsContentIntoTitleBar = false;
        }
    }

    /// <summary>Caption glyphs are drawn by the system over our own title bar, so their
    /// colours must follow the element theme; otherwise a dark window keeps a light
    /// title bar and the glyphs lose contrast.</summary>
    private void UpdateTitleBarColors()
    {
        try
        {
            var dark = (Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark;
            var foreground = dark ? Colors.White : Color.FromArgb(0xFF, 0x1C, 0x1C, 0x1E);
            var titleBar = AppWindow.TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonInactiveForegroundColor = dark
                ? Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x99, 0, 0, 0);
            titleBar.ButtonHoverBackgroundColor = dark
                ? Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x12, 0, 0, 0);
            titleBar.ButtonHoverForegroundColor = foreground;
            titleBar.ButtonPressedBackgroundColor = dark
                ? Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x20, 0, 0, 0);
            titleBar.ButtonPressedForegroundColor = foreground;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            // The caption keeps its system colours; nothing else depends on this.
        }
    }

    /// <summary>Returns focus to the main window after a secondary window closes so
    /// keyboard input does not stay with a dying window handle. macOS closing a sheet
    /// or secondary window returns focus to the main window (AppKit default).</summary>
    private void RestoreMainFocus()
    {
        try
        {
            if (AppWindow is { IsVisible: true })
            {
                Activate();
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException
            or System.Runtime.InteropServices.COMException)
        {
            // The main window is already closing; focus has nowhere to go.
        }
    }

    /// <summary>Closes every secondary window on application shutdown. Called before
    /// <c>Application.Exit()</c> because that call was observed sometimes leaving a
    /// child window - and therefore the process - alive.</summary>
    internal void CloseSecondaryWindows()
    {
        wizardWindow?.Close();
        helpWindow?.Close();
        settingsWindow?.Close();
        onboardingWindow?.Close();
    }

    /// <summary>Surfaces this window when a second launch is redirected to the running
    /// process. A minimized window is restored and a hidden one shown before activation;
    /// focus can still be refused by the shell, which is why the secondary launch only
    /// waits for an acknowledgement that the request reached this process.</summary>
    public void ActivateFromSecondLaunch()
    {
        try
        {
            if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
            {
                presenter.Restore();
            }

            if (!AppWindow.IsVisible)
            {
                AppWindow.Show();
            }

            Activate();
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException
            or System.Runtime.InteropServices.COMException)
        {
            // The window was already closing when the queued activation ran. The
            // secondary launch has either been acknowledged or will time out; there
            // is nothing left to surface.
        }
    }

    /// <summary>The update banner's close button promises to hide the message; macOS
    /// dismisses for the session (`updates.dismissed = true`, MainWindow.swift:94-103)
    /// and the view model already carries the tested dismissal logic.</summary>
    private void OnUpdateBannerClosed(InfoBar sender, object args) =>
        ViewModel.DismissBannerCommand.Execute(null);

    private async void OnFirstActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        await ViewModel.LoadAsync();
        // Application.Exit() is processed asynchronously, so a close that lands
        // while the first load is in flight can be followed by this continuation.
        // Never open the welcome window (and with it, a new message loop) after
        // shutdown has begun.
        if (App.IsShuttingDown)
        {
            return;
        }
        if (ViewModel.NeedsOnboarding)
        {
            ViewModel.ClearOnboardingRequest();
            ShowOnboarding();
        }
    }

    private void OnContentKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(CoreVirtualKeyStates.Down);
        switch (MainWindowShortcuts.Route(ctrl, e.Key))
        {
            case MainWindowShortcut.Help:
                OnShowHelpRequested(this, EventArgs.Empty);
                e.Handled = true;
                break;
            case MainWindowShortcut.Settings:
                OnShowSettingsRequested(this, EventArgs.Empty);
                e.Handled = true;
                break;
            case MainWindowShortcut.FocusSearch:
                Sidebar.FocusSearch();
                e.Handled = true;
                break;
        }
    }

    public MainViewModel ViewModel { get; }

    /// <summary>The banner is the only place Mitosis offers a link out. A user click on
    /// "View Release" opens the release page in the default browser through the OS;
    /// a malformed or non-HTTP(S) URL is ignored rather than handed to the shell.</summary>
    private async void OnOpenReleaseRequested(object? sender, string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            {
                await Launcher.LaunchUriAsync(uri);
            }
        }
        catch
        {
            // Honest no-op: the banner still names the version, and the release URL is
            // available through the GitHub releases page like any other link.
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.MainViewModel.HasSelection))
        {
            UpdateDetailVisibility();
        }
    }

    private NewCloneWindow? wizardWindow;

    private void OnNewCloneRequested(object? sender, EventArgs e)
    {
        if (wizardWindow != null)
        {
            wizardWindow.Activate();
            return;
        }

        var window = new Views.NewCloneWindow(ViewModel.CreateWizard());
        wizardWindow = window;
        window.Closed += OnWizardClosed;
        window.ShowProfileRequested += OnShowProfileRequested;
        window.Activate();
    }

    private HelpWindow? helpWindow;

    private SettingsWindow? settingsWindow;

    private OnboardingWindow? onboardingWindow;

    private HelpWindow OpenHelp()
    {
        if (helpWindow == null)
        {
            helpWindow = new HelpWindow(new HelpViewModel());
            helpWindow.Closed += (_, _) =>
            {
                helpWindow = null;
                RestoreMainFocus();
            };
        }

        return helpWindow;
    }

    private void OnShowHelpRequested(object? sender, EventArgs e) => OpenHelp().Activate();

    private SettingsWindow OpenSettings()
    {
        if (settingsWindow == null)
        {
            var settingsViewModel = new SettingsViewModel(
                ViewModel.PreferencesStore
                    ?? throw new InvalidOperationException("Settings need the preference store."),
                dataFolderPath
                    ?? throw new InvalidOperationException("Settings were composed without a data folder."),
                diagnostics,
                ViewModel.Updates);
            settingsViewModel.PreferencesChanged += OnSettingsPreferencesChanged;
            settingsWindow = new SettingsWindow(settingsViewModel);
            settingsWindow.Closed += (_, _) =>
            {
                settingsViewModel.PreferencesChanged -= OnSettingsPreferencesChanged;
                settingsWindow = null;
                RestoreMainFocus();
            };
        }

        return settingsWindow;
    }

    /// <summary>Applies a Settings change (currently the system-component filter) to the
    /// main list immediately, without waiting for the next launch.</summary>
    private async void OnSettingsPreferencesChanged(object? sender, EventArgs e)
    {
        try
        {
            await ViewModel.ReloadPreferencesAsync();
        }
        catch (OperationCanceledException)
        {
            // Shutdown raced the reload; the list keeps its current filter.
        }
    }

    private void OnShowSettingsRequested(object? sender, EventArgs e) => OpenSettings().Activate();

    private void ShowOnboarding()
    {
        // Defense in depth for the same startup race handled in
        // OnFirstActivated: never create a window once shutdown has begun.
        if (App.IsShuttingDown)
        {
            return;
        }
        if (onboardingWindow != null)
        {
            onboardingWindow.Activate();
            return;
        }

        var store = ViewModel.PreferencesStore
            ?? throw new InvalidOperationException("The welcome flow needs the preference store.");
        var window = new OnboardingWindow(new OnboardingViewModel(store));
        onboardingWindow = window;
        window.Completed += OnOnboardingCompleted;
        window.Closed += (_, _) =>
        {
            onboardingWindow = null;
            RestoreMainFocus();
        };
        window.Activate();
    }

    private void OnOnboardingCompleted(object? sender, bool startFirstClone)
    {
        if (sender is Views.OnboardingWindow window)
        {
            window.Completed -= OnOnboardingCompleted;
        }

        if (startFirstClone)
        {
            OnNewCloneRequested(this, EventArgs.Empty);
        }
    }

    private async void OnWizardClosed(object sender, WindowEventArgs args)
    {
        if (sender is Views.NewCloneWindow window)
        {
            window.Closed -= OnWizardClosed;
            window.ShowProfileRequested -= OnShowProfileRequested;
            wizardWindow = null;
            RestoreMainFocus();
            if (App.IsShuttingDown)
            {
                return;
            }

            await ViewModel.LoadAsync();
        }
    }

    private void OnShowProfileRequested(object? sender, string appId)
    {
        ViewModel.SelectAppById(appId);
    }

    private void UpdateDetailVisibility()
    {
        DetailView.Visibility = ViewModel.ShowCloneDetail ? Visibility.Visible : Visibility.Collapsed;
        AppView.Visibility = ViewModel.ShowAppDetail ? Visibility.Visible : Visibility.Collapsed;
        OrphanView.Visibility = ViewModel.ShowOrphanDetail ? Visibility.Visible : Visibility.Collapsed;
        EmptyPanel.Visibility = ViewModel.HasSelection ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnDialogRequested(object? sender, DialogRequest request)
    {
        var dialog = new ContentDialog
        {
            Title = request.Title,
            Content = request.Message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
