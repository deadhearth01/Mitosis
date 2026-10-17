namespace Mitosis.Windows.ViewModels;

using Mitosis.Application.Preferences;

/// <summary>One onboarding page (macOS Onboarding.swift pages). Copy matches macOS
/// modulo OS nouns (cf. matrix W58): Mac apps → apps, Dock icon → taskbar icon.
/// Page 3 states honestly that cloning is not implemented yet on Windows.</summary>
public sealed record OnboardingPageContent(string Pose, string Title, string Text, string? Caption = null)
{
    /// <summary>Content asset for the decorative mascot. Hidden from the accessibility
    /// tree per the one-mascot rule (matrix W60); the title carries the meaning.</summary>
    public string PoseImage => $"ms-appx:///Assets/pose-{Pose}.png";
}

/// <summary>First-launch welcome flow: three pages, dots, Back/Next, then Later or
/// Choose an App (macOS Onboarding.swift:11-85). Both finishing actions persist the
/// onboarded flag; nothing else is touched — no profiles are created, nothing is
/// launched or modified. Choose an App merely asks the window to open the existing
/// metadata-only New Clone wizard afterwards. Dismissal is deterministic: the
/// onboarding window closes only through <see cref="Finished"/>.</summary>
public sealed class OnboardingViewModel : ObservableObject
{
    private static readonly OnboardingPageContent[] MacParityPages =
    {
        new("wave", "Welcome to Mitosis",
            "Run separate copies of your apps. Each copy has its own login, data, and taskbar icon."),
        new("key", "Each clone asks for its own permissions",
            "Windows treats every clone as a new app, so it may ask again for notifications, files, or the camera.",
            "Only once clones can run — today Mitosis launches nothing."),
        new("split", "Make your first clone",
            "Pick an app, add a label like Work, and Mitosis records it for you.",
            "Cloning is not implemented yet — saving opens the New Clone wizard and records a planned profile."),
    };

    private readonly IAppPreferencesStore preferences;
    private OnboardingPageContent[] pages;
    private int pageIndex;
    private bool finished;
    private string? errorMessage;

    public OnboardingViewModel(
        IAppPreferencesStore preferences,
        IReadOnlyList<OnboardingPageContent>? pages = null)
    {
        this.preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        this.pages = pages is { Count: 0 } ? throw new ArgumentException("Onboarding needs at least one page.", nameof(pages))
            : pages?.ToList().ToArray() ?? MacParityPages;
        pageIndex = 0;

        NextPageCommand = new RelayCommand(GoNext, () => !IsLast);
        PreviousPageCommand = new RelayCommand(GoPrevious, () => PageIndex > 0);
        CompleteWithoutCloneCommand = new RelayCommand(async _ => await FinishAsync(startFirstClone: false, CancellationToken.None));
        CompleteWithCloneCommand = new RelayCommand(async _ => await FinishAsync(startFirstClone: true, CancellationToken.None));
    }

    /// <summary>Visible pages. Tests pass a disjoint list to pin page rendering.</summary>
    public IReadOnlyList<OnboardingPageContent> Pages => pages;

    public int PageIndex
    {
        get => pageIndex;
        private set
        {
            if (SetProperty(ref pageIndex, Math.Max(0, Math.Min(value, pages.Length - 1))))
            {
                RaisePropertyChanged(nameof(CurrentPage));
                RaisePropertyChanged(nameof(CanGoBack));
                RaisePropertyChanged(nameof(IsLast));
                RaisePropertyChanged(nameof(PageLabel));
                RaisePropertyChanged(nameof(PageTitle));
                RaisePropertyChanged(nameof(PageText));
                RaisePropertyChanged(nameof(PageImage));
                RaisePropertyChanged(nameof(PageCaption));
                RaisePropertyChanged(nameof(PageShowCaption));
                NextPageCommand.RaiseCanExecuteChanged();
                PreviousPageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public OnboardingPageContent CurrentPage => pages[pageIndex];

    public bool CanGoBack => PageIndex > 0;

    public bool IsLast => PageIndex == pages.Length - 1;

    /// <summary>Screen-reader text for the page dots (macOS accessibilityLabel, Onboarding.swift:61).</summary>
    public string PageLabel => $"Page {PageIndex + 1} of {pages.Length}";

    public string PageTitle => CurrentPage.Title;

    public string PageText => CurrentPage.Text;

    public string PageImage => CurrentPage.PoseImage;

    public string? PageCaption => CurrentPage.Caption;

    public bool PageShowCaption => CurrentPage.Caption != null;

    public RelayCommand NextPageCommand { get; }

    public RelayCommand PreviousPageCommand { get; }

    /// <summary>The "Later" action: finish now, skip the wizard.</summary>
    public RelayCommand CompleteWithoutCloneCommand { get; }

    /// <summary>The "Choose an App" action: finish now, then open the wizard.</summary>
    public RelayCommand CompleteWithCloneCommand { get; }

    /// <summary>True once an evaluation-free completion (Later or Choose an App) has
    /// been persisted. The onboarding window uses this release its closing guard.</summary>
    public bool Finished
    {
        get => finished;
        private set => SetProperty(ref finished, value);
    }

    /// <summary>True when the user chose Choose an App; the owning window then opens
    /// the existing New Clone wizard. Never launches or provisions anything itself.</summary>
    public bool StartFirstClone { get; private set; }

    /// <summary>Shown when the preference could not be saved: finishing stays possible
    /// by clicking again (retry), and the welcome reappears on next launch instead of
    /// silently pretending the dismissal was recorded.</summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    private void GoNext() => PageIndex++;

    private void GoPrevious() => PageIndex--;

    /// <summary>Persists the onboarded flag and reports completion. On persistence
    /// failure completion is not faked: Finished stays false, the inline error names
    /// the problem, and another completion action retries the save.</summary>
    public async Task FinishAsync(bool startFirstClone, CancellationToken cancellationToken)
    {
        if (Finished)
        {
            return;
        }

        ErrorMessage = null;
        try
        {
            var current = await preferences.LoadAsync(cancellationToken).ConfigureAwait(false);
            await preferences.SaveAsync(current with { Onboarded = true }, cancellationToken).ConfigureAwait(false);
            StartFirstClone = startFirstClone;
            Finished = true;
        }
        catch (PreferencesStoreException ex)
        {
            // Honest failure: without a persisted flag the welcome flow reappears on
            // the next launch instead of pretending the dismissal was recorded.
            ErrorMessage = ex.Message;
        }
    }
}
