namespace Mitosis.Windows.ViewModels;

using Mitosis.Application.Preferences;

/// <summary>Settings window state. Only flags with a real, testable effect are exposed
/// (Stage 13 rules): unimplemented macOS settings — menu bar/tray, update checking,
/// clone auto-update, the command-line installer, the default-clone-mode picker —
/// have deliberately no controls, because no Windows component can honor them yet.
/// Implemented content:
///   Advanced ▸ Show Tips Again: ShowTips=true + Onboarded=false (macOS
///     SettingsView.swift:156-166), persisted to the preference store; effect visible
///     on the next launch, reversible by completing the welcome flow again.
///   General ▸ Storage: the application's own data folder with a reveal action;
///     informational only (macOS Location row adapted — no clone folder exists).
/// Data folder is supplied at composition and never guessed here.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly IAppPreferencesStore preferences;
    private bool tipsReset;
    private string? saveError;

    public SettingsViewModel(IAppPreferencesStore preferences, string dataFolderPath)
    {
        this.preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        DataFolderPath = string.IsNullOrWhiteSpace(dataFolderPath)
            ? throw new ArgumentException("Data folder path must not be empty.", nameof(dataFolderPath))
            : dataFolderPath;

        ShowTipsAgainCommand = new RelayCommand(
            async _ => await ShowTipsAgainAsync(CancellationToken.None),
            _ => !TipsReset);
        RevealDataFolderCommand = new RelayCommand(() =>
            RevealDataFolderRequested?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Application-owned storage folder shown as plain text (macOS shows the
    /// clones directory). Revealed via the App layer on user request only.</summary>
    public string DataFolderPath { get; }

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

    public RelayCommand RevealDataFolderCommand { get; }

    /// <summary>Raised when the user asks to reveal the data folder; the window opens
    /// File Explorer (creating the directory first, like macOS revealClones does).</summary>
    public event EventHandler? RevealDataFolderRequested;

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
