using Mitosis.Application.Profiles;
using Mitosis.CloneEngine.Provisioning;
using Mitosis.Domain;

namespace Mitosis.Windows.ViewModels;

/// <summary>New Clone wizard steps. Mirrors the macOS sheet flow (pick → details →
/// working → done / failed) with an added review step before persistence and honest
/// metadata-only completion copy. Saving is the only side effect: one profile record.</summary>
public enum WizardStep
{
    Pick,
    Details,
    Review,
    Saving,
    Done,
    Failed,
}

/// <summary>Metadata-only New Clone workflow. Validates against live compatibility
/// assessments and persists a <see cref="CloneProfileState.Planned"/> record.
/// Never provisions, launches, copies, or deletes anything.</summary>
public sealed class NewCloneWizardViewModel : ObservableObject
{
    private readonly List<DiscoveredAppItem> apps;
    private readonly List<CloneProfile> existingProfiles;
    private readonly ICloneProfileRepository profiles;
    private readonly IReadOnlyList<IProvisioningStrategy> catalog;

    private string searchText = string.Empty;
    private DiscoveredAppItem? selectedApp;
    private WizardStep step = WizardStep.Pick;
    private string profileName = string.Empty;
    private string badgeText = string.Empty;
    private string selectedColorHex = "#0A84FF";
    private bool acknowledged;
    private bool isSaving;
    private string? errorMessage;
    private CloneProfile? createdProfile;
    private bool cancelled;

    public NewCloneWizardViewModel(
        IReadOnlyList<DiscoveredAppItem> apps,
        IReadOnlyList<CloneProfile> existingProfiles,
        ICloneProfileRepository profiles,
        DiscoveredAppItem? preselected = null,
        IEnumerable<IProvisioningStrategy>? catalog = null)
    {
        this.apps = apps.ToList();
        this.existingProfiles = existingProfiles.ToList();
        this.profiles = profiles;
        this.catalog = (catalog ?? Enumerable.Empty<IProvisioningStrategy>()).ToList();
        selectedApp = preselected != null && this.apps.Any(i => i.App.Id == preselected.App.Id)
            ? preselected
            : null;

        GoToDetailsCommand = new RelayCommand(
            () => GoToDetails(), () => CanContinueFromPick);
        GoToReviewCommand = new RelayCommand(
            () => GoToReview(), () => CanContinueFromDetails);
        GoBackCommand = new RelayCommand(() => GoBack(), () => Step is WizardStep.Details or WizardStep.Review);
        SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => Step == WizardStep.Review && !IsSaving);
        RetryCommand = new RelayCommand(() => Retry());
        SelectColorCommand = new RelayCommand(parameter =>
        {
            if (parameter is string hex)
            {
                SelectedColorHex = hex;
            }
        });
    }

    public WizardStep Step
    {
        get => step;
        private set
        {
            if (SetProperty(ref step, value))
            {
                RaisePropertyChanged(nameof(ShowPick));
                RaisePropertyChanged(nameof(ShowDetails));
                RaisePropertyChanged(nameof(ShowReview));
                RaisePropertyChanged(nameof(ShowSaving));
                RaisePropertyChanged(nameof(ShowDone));
                RaisePropertyChanged(nameof(ShowFailed));
                RaiseCanExecute();
            }
        }
    }

    public bool ShowPick => Step == WizardStep.Pick;

    public bool ShowDetails => Step == WizardStep.Details;

    public bool ShowReview => Step == WizardStep.Review;

    public bool ShowSaving => Step == WizardStep.Saving;

    public bool ShowDone => Step == WizardStep.Done;

    public bool ShowFailed => Step == WizardStep.Failed;

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value ?? string.Empty))
            {
                RaisePropertyChanged(nameof(VisibleApps));
            }
        }
    }

    public IReadOnlyList<DiscoveredAppItem> VisibleApps
    {
        get
        {
            var query = searchText.Trim();
            return apps
                .Where(i => Matches(i, query))
                .OrderBy(i => i.App.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public DiscoveredAppItem? SelectedApp
    {
        get => selectedApp;
        set
        {
            if (SetProperty(ref selectedApp, value))
            {
                RaisePropertyChanged(nameof(CanContinueFromPick));
                RaisePropertyChanged(nameof(CompatibilityNote));
                RaisePropertyChanged(nameof(DetailsTitle));
                RaisePropertyChanged(nameof(UnsupportedReason));
                RaisePropertyChanged(nameof(ReviewCompatibilityText));
                RaisePropertyChanged(nameof(NeedsAcknowledgement));
                RaisePropertyChanged(nameof(CanContinueFromDetails));
                RaiseCanExecute();
            }
        }
    }

    public bool CanContinueFromPick =>
        SelectedApp != null && SelectedApp.Assessment.Status != CompatibilityStatus.Unsupported;

    public string UnsupportedReason => SelectedApp is { } selected
        && selected.Assessment.Status == CompatibilityStatus.Unsupported
        ? string.Join(" ", selected.Assessment.Reasons)
        : string.Empty;

    public string DetailsTitle => SelectedApp is { } current
        ? $"New Clone of {current.App.DisplayName}"
        : "New Clone";

    public bool HasValidationMessage => ValidationMessage != null;

    public string SelectedColorName =>
        BadgePreset.Presets.FirstOrDefault(p =>
            string.Equals(p.Hex, SelectedColorHex, StringComparison.OrdinalIgnoreCase)).Name
        ?? SelectedColorHex;

    public string ReviewBadgeText => $"{PreviewBadgeText} · {SelectedColorName}";

    public string ReviewCompatibilityText => SelectedApp is { } current
        ? MainViewModel.AssessmentStatusText(current.Assessment.Status)
            + (current.Assessment.Reasons.Count > 0 ? " — " + current.Assessment.Reasons[0] : string.Empty)
        : string.Empty;

    public string ProvisioningNote
    {
        get
        {
            var enabled = catalog.Where(s => s.IsEnabled).ToList();
            if (enabled.Count == 0)
            {
                return "No validated provisioning strategy exists yet — this saves a planned profile only.";
            }

            return "Available planning " + (enabled.Count == 1 ? "strategy" : "strategies") + ": "
                + string.Join(", ", enabled.Select(s => $"'{s.Id} {s.Version}'"))
                + ". Execution arrives in a later stage.";
        }
    }

    public string CompatibilityNote => SelectedApp is { } selected ? selected.Assessment.Status switch
    {
        CompatibilityStatus.Supported =>
            $"Validated strategy '{selected.Assessment.StrategyId}': independent copies are supported for this application.",
        CompatibilityStatus.NeedsReview =>
            string.Join(" ", selected.Assessment.Reasons)
            + " Acknowledgement is required to record a planned profile.",
        CompatibilityStatus.Unsupported => UnsupportedReason,
        _ => "Independent-instance support has not been established for this application. "
            + "You can record a planned profile; it stays unverified until support is validated.",
    } : string.Empty;

    public bool NeedsAcknowledgement =>
        SelectedApp?.Assessment.Status == CompatibilityStatus.NeedsReview;

    public bool Acknowledged
    {
        get => acknowledged;
        set
        {
            if (SetProperty(ref acknowledged, value))
            {
                RaisePropertyChanged(nameof(CanContinueFromDetails));
                RaiseCanExecute();
            }
        }
    }

    public string ProfileName
    {
        get => profileName;
        set
        {
            if (SetProperty(ref profileName, value ?? string.Empty))
            {
                RaisePropertyChanged(nameof(ValidationMessage));
                RaisePropertyChanged(nameof(HasValidationMessage));
                RaisePropertyChanged(nameof(CanContinueFromDetails));
                RaiseCanExecute();
            }
        }
    }

    public string BadgeText
    {
        get => badgeText;
        set
        {
            var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized.Length > 2)
            {
                normalized = normalized.Substring(0, 2);
            }

            if (SetProperty(ref badgeText, normalized))
            {
                RaisePropertyChanged(nameof(PreviewBadgeText));
                RaisePropertyChanged(nameof(ReviewBadgeText));
                RaisePropertyChanged(nameof(ValidationMessage));
                RaisePropertyChanged(nameof(HasValidationMessage));
                RaisePropertyChanged(nameof(CanContinueFromDetails));
                RaiseCanExecute();
            }
        }
    }

    public string PreviewBadgeText => string.IsNullOrEmpty(BadgeText)
        ? InitialOf(SelectedApp?.App.DisplayName ?? "?")
        : BadgeText;

    public string SelectedColorHex
    {
        get => selectedColorHex;
        set
        {
            if (SetProperty(ref selectedColorHex, value ?? "#0A84FF"))
            {
                RaisePropertyChanged(nameof(ReviewBadgeText));
                RaisePropertyChanged(nameof(ValidationMessage));
                RaisePropertyChanged(nameof(HasValidationMessage));
                RaiseCanExecute();
            }
        }
    }

    /// <summary>Inline validation, macOS copy where applicable. Null means valid.</summary>
    public string? ValidationMessage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
            {
                return "Add a name, like Slack (Work).";
            }

            string name;
            try
            {
                name = CloneName.Validate(ProfileName);
            }
            catch (CloneNameException ex)
            {
                return ex.Message;
            }

            if (existingProfiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return $"You already have {name}.";
            }

            if (!string.IsNullOrEmpty(BadgeText) && !BadgePreset.IsValidText(BadgeText))
            {
                return "Badge text must be 1–2 characters.";
            }

            if (!BadgePreset.IsValidColor(SelectedColorHex))
            {
                return "Choose a badge color.";
            }

            return null;
        }
    }

    public bool CanContinueFromDetails =>
        SelectedApp != null
        && SelectedApp.Assessment.Status != CompatibilityStatus.Unsupported
        && ValidationMessage == null
        && (!NeedsAcknowledgement || Acknowledged);

    public bool IsSaving
    {
        get => isSaving;
        private set
        {
            if (SetProperty(ref isSaving, value))
            {
                RaiseCanExecute();
            }
        }
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public CloneProfile? CreatedProfile
    {
        get => createdProfile;
        private set => SetProperty(ref createdProfile, value);
    }

    public bool Cancelled
    {
        get => cancelled;
        private set => SetProperty(ref cancelled, value);
    }

    public string DoneTitle => CreatedProfile is { } created ? $"{created.Name} planned." : string.Empty;

    public const string DoneMessage =
        "The profile was saved. The application has not been copied, launched, or modified — provisioning arrives in a later stage.";

    public string DoneMessageText => DoneMessage;

    public string FailedTitle => $"Couldn't save {(string.IsNullOrWhiteSpace(ProfileName) ? "the profile" : ProfileName.Trim())}";

    public RelayCommand GoToDetailsCommand { get; }

    public RelayCommand GoToReviewCommand { get; }

    public RelayCommand GoBackCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand RetryCommand { get; }

    public RelayCommand SelectColorCommand { get; }

    public bool GoToDetails()
    {
        if (!CanContinueFromPick || SelectedApp == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            ProfileName = SuggestName(SelectedApp.App.DisplayName);
        }

        if (string.IsNullOrWhiteSpace(BadgeText))
        {
            BadgeText = InitialOf(SelectedApp.App.DisplayName);
        }

        SelectedColorHex = FirstUnusedColor(SelectedApp.App.Id);
        Acknowledged = false;
        RaisePropertyChanged(nameof(PreviewBadgeText));
        Step = WizardStep.Details;
        return true;
    }

    public bool GoToReview()
    {
        if (!CanContinueFromDetails)
        {
            return false;
        }

        Step = WizardStep.Review;
        return true;
    }

    public void GoBack()
    {
        if (Step == WizardStep.Review)
        {
            Step = WizardStep.Details;
        }
        else if (Step == WizardStep.Details)
        {
            Step = WizardStep.Pick;
        }
    }

    public void Retry()
    {
        ErrorMessage = null;
        Step = WizardStep.Review;
    }

    public void Cancel()
    {
        if (IsSaving)
        {
            return;
        }

        Cancelled = true;
    }

    /// <summary>Persists one Planned record. Reentrant calls while saving are ignored;
    /// failures land on the Failed step with the wizard state intact.</summary>
    public async Task<CloneProfile?> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (IsSaving || SelectedApp == null || !CanContinueFromDetails)
        {
            return null;
        }

        IsSaving = true;
        Step = WizardStep.Saving;
        try
        {
            var profile = CloneProfile.Create(
                SelectedApp.App.Id, ProfileName, BadgeText, SelectedColorHex);
            var saved = await profiles.AddAsync(profile, cancellationToken).ConfigureAwait(false);
            if (saved.State != CloneProfileState.Planned)
            {
                throw new InvalidProfileException("A new profile must stay Planned.");
            }

            CreatedProfile = saved;
            Step = WizardStep.Done;
            return saved;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is DuplicateProfileException
            or InvalidProfileException
            or CloneNameException
            or ProfileStoreException)
        {
            ErrorMessage = ex.Message;
            Step = WizardStep.Failed;
            return null;
        }
        catch (Exception)
        {
            ErrorMessage = "Couldn't save the profile. Try again.";
            Step = WizardStep.Failed;
            return null;
        }
        finally
        {
            IsSaving = false;
        }
    }

    public string SuggestName(string appName)
    {
        var taken = new HashSet<string>(existingProfiles.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        var candidates = new[] { "Work", "Personal" }.Concat(Enumerable.Range(2, 98).Select(n => n.ToString()));
        foreach (var label in candidates)
        {
            var name = CloneName.Compose(appName, label);
            if (!taken.Contains(name))
            {
                return name;
            }
        }

        return CloneName.Compose(appName, "New");
    }

    private string FirstUnusedColor(string appId)
    {
        var used = new HashSet<string>(
            existingProfiles.Where(p => p.AppId == appId).Select(p => NormalizeColor(p.BadgeColor)),
            StringComparer.OrdinalIgnoreCase);
        return BadgePreset.Presets.Select(p => p.Hex).FirstOrDefault(hex => !used.Contains(hex))
            ?? BadgePreset.Presets[0].Hex;
    }

    private static string NormalizeColor(string? color)
    {
        var preset = BadgePreset.Presets.FirstOrDefault(p =>
            string.Equals(p.Name, color, StringComparison.OrdinalIgnoreCase));
        return preset != default ? preset.Hex : (color ?? string.Empty);
    }

    private static string InitialOf(string text)
    {
        var first = text.Trim().FirstOrDefault(char.IsLetterOrDigit);
        return first == default ? string.Empty : char.ToUpperInvariant(first).ToString();
    }

    private static bool Matches(DiscoveredAppItem item, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }

        var compare = System.Globalization.CultureInfo.InvariantCulture.CompareInfo;
        const System.Globalization.CompareOptions options =
            System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace;
        return compare.IndexOf(item.App.DisplayName, query, options) >= 0
            || (item.App.Publisher != null && compare.IndexOf(item.App.Publisher, query, options) >= 0);
    }

    private void RaiseCanExecute()
    {
        GoToDetailsCommand.RaiseCanExecuteChanged();
        GoToReviewCommand.RaiseCanExecuteChanged();
        GoBackCommand.RaiseCanExecuteChanged();
        SaveCommand.RaiseCanExecuteChanged();
    }
}
