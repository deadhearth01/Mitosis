namespace Mitosis.Windows.ViewModels;

using System.ComponentModel;
using Mitosis.Application.Diagnostics;
using Mitosis.Application.Preferences;

/// <summary>Settings window state. Only flags with a real, testable effect are exposed
/// (Stage 13 rules): unimplemented macOS settings — menu bar/tray, clone auto-update,
/// the command-line installer, the default-clone-mode picker — have deliberately no
/// controls, because no Windows component can honor them yet. Implemented content:
///   Advanced ▸ Show Tips Again: ShowTips=true + Onboarded=false (macOS
///     SettingsView.swift:156-166), persisted to the preference store; effect visible
///     on the next launch, reversible by completing the welcome flow again.
///   General ▸ Storage: the application's own data folder with a reveal action;
///     informational only (macOS Location row adapted — no clone folder exists).
///   General ▸ Diagnostics: the local log folder with a reveal action and a
///     copyable environment report. Local-only; no application data or credentials.
///   General ▸ Updates: a manual, read-only GitHub Releases check (Stage 25). No
///     automatic check, download, or install.
/// Data folder is supplied at composition and never guessed here.</summary>
public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IAppPreferencesStore preferences;
    private readonly DoctorFacts diagnosticsFacts;
    private readonly CancellationTokenSource lifetime = new();
    private bool tipsReset;
    private bool disposed;
    private string? saveError;

    public SettingsViewModel(
        IAppPreferencesStore preferences,
        string dataFolderPath,
        DoctorFacts? diagnostics = null,
        UpdatesViewModel? updates = null)
    {
        this.preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        Updates = updates;
        if (updates is not null)
        {
            updates.PropertyChanged += OnUpdatesPropertyChanged;
        }
        DataFolderPath = string.IsNullOrWhiteSpace(dataFolderPath)
            ? throw new ArgumentException("Data folder path must not be empty.", nameof(dataFolderPath))
            : dataFolderPath;

        diagnosticsFacts = diagnostics ?? new DoctorFacts(
            AppVersion: "unknown",
            RuntimeVersion: "unknown",
            OperatingSystem: "unknown",
            Architecture: "unknown",
            WindowsAppSdkVersion: "unknown",
            DataDirectory: DataFolderPath,
            DataDirectoryWritable: false,
            LogDirectory: Path.Combine(DataFolderPath, "Logs"));
        LogFolderPath = string.IsNullOrWhiteSpace(diagnosticsFacts.LogDirectory)
            ? Path.Combine(DataFolderPath, "Logs")
            : diagnosticsFacts.LogDirectory;

        ShowTipsAgainCommand = new RelayCommand(
            async _ => await ShowTipsAgainAsync(CancellationToken.None),
            _ => !TipsReset);
        CheckForUpdatesCommand = new RelayCommand(
            async _ => await CheckForUpdatesForThisWindowAsync().ConfigureAwait(true),
            _ => CanCheckForUpdates);
        RevealDataFolderCommand = new RelayCommand(() =>
            RevealDataFolderRequested?.Invoke(this, EventArgs.Empty));
        RevealLogFolderCommand = new RelayCommand(() =>
            RevealLogFolderRequested?.Invoke(this, EventArgs.Empty));
        CopyDiagnosticsCommand = new RelayCommand(() =>
            CopyDiagnosticsRequested?.Invoke(this, DiagnosticsReport));
    }

    /// <summary>Shared manual update-check state, or null when Settings was composed
    /// without a release feed (tests and design-only previews).</summary>
    public UpdatesViewModel? Updates { get; }

    public bool CanCheckForUpdates => Updates is not null && !Updates.IsChecking;

    /// <summary>The running version without build metadata, for the Updates row.</summary>
    public string CurrentVersionText => Updates?.CurrentVersion ?? "unknown";

    /// <summary>Plain sentence describing the last check (never a fabricated "up to
    /// date"); also suitable for a live region.</summary>
    public string UpdateStatusText =>
        Updates?.StatusText ?? "Update checking is not available in this build.";

    public bool IsCheckingForUpdates => Updates?.IsChecking ?? false;

    public string CheckForUpdatesLabel => IsCheckingForUpdates ? "Checking…" : "Check for Updates";

    /// <summary>True only when the last successful check found a newer release with a
    /// usable HTTP(S) page.</summary>
    public bool HasUpdateLink => UpdateReleaseUrl is not null;

    /// <summary>A validated HTTP(S) page for the release, or null when none is known.
    /// Typed as <see cref="Uri"/> so the XAML binding cannot pass a malformed value to the
    /// shell.</summary>
    public Uri? UpdateReleaseUrl =>
        Uri.TryCreate(Updates?.ReleaseUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri
            : null;

    /// <summary>Starts one manual check. Settings shows the status; the main window's
    /// banner updates through the shared view model.</summary>
    public Task CheckForUpdatesAsync(CancellationToken cancellationToken = default) =>
        Updates?.CheckAsync(cancellationToken) ?? Task.CompletedTask;

    /// <summary>Command path: cancels silently when the window closes mid-check, so a
    /// closing window can never surface an unhandled cancellation.</summary>
    private async Task CheckForUpdatesForThisWindowAsync()
    {
        try
        {
            await CheckForUpdatesAsync(lifetime.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The window closed; there is nobody left to show a status to.
        }
    }

    /// <summary>Application-owned storage folder shown as plain text (macOS shows the
    /// clones directory). Revealed via the App layer on user request only.</summary>
    public string DataFolderPath { get; }

    /// <summary>Local diagnostic log folder, shown as selectable text.</summary>
    public string LogFolderPath { get; }

    /// <summary>Plain-text environment report copied to the clipboard on request.
    /// Home paths are redacted; application data is never read.</summary>
    public string DiagnosticsReport => DoctorReport.Build(diagnosticsFacts);

    public bool TipsReset
    {
        get => tipsReset;
        private set
        {
            if (SetProperty(ref tipsReset, value))
            {
                ShowTipsAgainCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Inline error when persisting fails; empty means the last action succeeded.</summary>
    public string? SaveError
    {
        get => saveError;
        private set => SetProperty(ref saveError, value);
    }

    public RelayCommand ShowTipsAgainCommand { get; }

    public RelayCommand CheckForUpdatesCommand { get; }

    public RelayCommand RevealDataFolderCommand { get; }

    public RelayCommand RevealLogFolderCommand { get; }

    public RelayCommand CopyDiagnosticsCommand { get; }

    /// <summary>Raised when the user asks to reveal the data folder; the window opens
    /// File Explorer (creating the directory first, like macOS revealClones does).</summary>
    public event EventHandler? RevealDataFolderRequested;

    /// <summary>Raised when the user asks to reveal the local log folder.</summary>
    public event EventHandler? RevealLogFolderRequested;

    /// <summary>Carries the report text so the window can copy it to the clipboard.</summary>
    public event EventHandler<string>? CopyDiagnosticsRequested;

    /// <summary>Detaches from the shared update state. The window closes once; without
    /// this, every reopened Settings window would stay subscribed to the feed state.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
        if (Updates is not null)
        {
            Updates.PropertyChanged -= OnUpdatesPropertyChanged;
        }
    }

    private void OnUpdatesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(CanCheckForUpdates));
        RaisePropertyChanged(nameof(CurrentVersionText));
        RaisePropertyChanged(nameof(UpdateStatusText));
        RaisePropertyChanged(nameof(IsCheckingForUpdates));
        RaisePropertyChanged(nameof(CheckForUpdatesLabel));
        RaisePropertyChanged(nameof(HasUpdateLink));
        RaisePropertyChanged(nameof(UpdateReleaseUrl));
        if (e.PropertyName is nameof(UpdatesViewModel.IsChecking))
        {
            CheckForUpdatesCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Verifies the store can be read and surfaces damage honestly. macOS
    /// starts every Settings window with the button enabled (tipsReset is window-local
    /// state, SettingsView.swift:141), so stored flags never disable it here.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        SaveError = null;
        try
        {
            _ = await preferences.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PreferencesStoreException ex)
        {
            SaveError = ex.Message;
        }
    }

    /// <summary>macOS SettingsView.swift:156-166 verbatim semantics: turn the tips and
    /// the welcome flow back on, persist, then disable for the rest of the window's
    /// lifetime. The change takes effect on the next launch; users reverse it by
    /// finishing the welcome flow again (either action persists Onboarded=true).</summary>
    public async Task ShowTipsAgainAsync(CancellationToken cancellationToken)
    {
        SaveError = null;
        try
        {
            var current = await preferences.LoadAsync(cancellationToken).ConfigureAwait(false);
            await preferences.SaveAsync(
                current with { ShowTips = true, Onboarded = false }, cancellationToken).ConfigureAwait(false);
            TipsReset = true;
        }
        catch (PreferencesStoreException ex)
        {
            SaveError = ex.Message;
        }
    }
}
