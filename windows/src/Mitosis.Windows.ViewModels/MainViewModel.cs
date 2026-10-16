using System.Globalization;
using Mitosis.Application.Discovery;
using Mitosis.Application.Profiles;
using Mitosis.CloneEngine.Provisioning;
using Mitosis.Domain;

namespace Mitosis.Windows.ViewModels;

/// <summary>Main-window presentation state: sidebar groups, filtering, selection, notices.
/// Filtering, selection, and dialog routing are real logic and unit-tested; the underlying
/// entries are preview data until the engine lands (see <see cref="PreviewData"/>).</summary>
public sealed class MainViewModel : ObservableObject
{
    private static readonly CompareOptions MatchOptions =
        CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    private List<CloneItem> clones;
    private List<AppGroupItem> groups;
    private readonly IAppDiscoveryService? discovery;
    private readonly ICompatibilityAssessor? assessor;

    private string searchText = string.Empty;
    private CloneItem? selectedClone;
    private List<CloneGroupView> visibleGroups = new();
    private List<DiscoveredAppItem> appItems = new();
    private List<AppGroupView> visibleAppGroups = new();
    private DiscoveredAppItem? selectedApp;
    private readonly ICloneProfileRepository? profiles;
    private List<CloneProfile> profileRecords = new();
    private List<OrphanProfileItem> orphanProfiles = new();
    private OrphanProfileItem? selectedOrphan;
    private bool isLoadingProfiles;
    private string? profilesError;
    private bool linkRoutingOn;
    private bool isLoadingApps;
    private string? discoveryError;

    public MainViewModel(IEnumerable<AppGroupItem> groups, IEnumerable<CloneItem> clones, bool isPreview,
        bool showUpdateBanner = false, string bannerVersion = "")
    {
        this.groups = groups.ToList();
        this.clones = clones.ToList();
        IsPreview = isPreview;
        ShowUpdateBanner = showUpdateBanner;
        BannerVersion = bannerVersion;

        NewCloneCommand = new RelayCommand(() =>
        {
            if (IsPreview)
            {
                RaisePlaceholder("New Clone",
                    "Clone creation arrives with the engine in a later stage. This preview shows the shell only.");
            }
            else
            {
                NewCloneRequested?.Invoke(this, EventArgs.Empty);
            }
        });
        OpenCloneCommand = new RelayCommand(
            () => RaisePlaceholder("Open",
                SelectedClone == null ? "Select a clone first." : $"Opening {SelectedClone.Name} needs the engine, which is not implemented yet."),
            () => SelectedClone != null);
        EditBadgeCommand = new RelayCommand(
            () => RaisePlaceholder("Edit Badge", "Badge editing arrives with the engine in a later stage."),
            () => SelectedClone is { Status: not CloneStatusKind.OriginalMissing });
        RefreshCommand = new RelayCommand(
            () => RaisePlaceholder("Refresh", "Refreshing a clone needs the engine, which is not implemented yet."),
            () => SelectedClone is { Status: not CloneStatusKind.OriginalMissing });
        DeleteCommand = new RelayCommand(
            () => RaisePlaceholder("Delete",
                "Deleting a clone needs the engine, which is not implemented yet. Nothing was touched."),
            () => SelectedClone != null);
        CleanCachesCommand = new RelayCommand(
            () => RaisePlaceholder("Clean caches", "Cache cleaning needs the engine, which is not implemented yet."),
            () => SelectedClone != null);
        FindAppCommand = new RelayCommand(
            () => RaisePlaceholder("Find App", "Relinking needs the engine, which is not implemented yet."),
            () => SelectedClone?.Status == CloneStatusKind.OriginalMissing);
        RevealCloneCommand = new RelayCommand(
            () => RaisePlaceholder("Show in File Explorer", "Revealing file locations needs real clones, which are not implemented yet."),
            () => SelectedClone != null);
        RevealOriginalCommand = new RelayCommand(
            () => RaisePlaceholder("Show Original in File Explorer", "Revealing real apps needs the engine, which is not implemented yet."),
            () => SelectedClone is { Status: not CloneStatusKind.OriginalMissing });
        RevealDataCommand = new RelayCommand(
            () => RaisePlaceholder("Show Data Folder", "Data folders need real clones, which are not implemented yet."),
            () => SelectedClone != null);
        DismissBannerCommand = new RelayCommand(() => ShowUpdateBanner = false);
        InstallUpdateCommand = new RelayCommand(
            () => RaisePlaceholder("Update Mitosis", "Self-update arrives with packaging in a later stage. Nothing was changed."));
        RefreshAppsCommand = new RelayCommand(async _ => await LoadAsync());
        ShowPreviewCommand = new RelayCommand(ShowPreview);
        CreateCloneFromAppCommand = new RelayCommand(
            () => RaisePlaceholder("New Clone", "Creating clones from discovered apps arrives with the engine in a later stage."),
            () => SelectedApp != null);

        RefreshVisibleGroups();
    }

    /// <summary>Production constructor: discovery-backed. Preview data is never used here;
    /// see <see cref="ShowPreviewCommand"/> for the explicit opt-in fallback.</summary>
    public MainViewModel(
        IAppDiscoveryService discovery, ICompatibilityAssessor assessor, ICloneProfileRepository profiles)
        : this(Enumerable.Empty<AppGroupItem>(), Enumerable.Empty<CloneItem>(), isPreview: false)
    {
        this.discovery = discovery;
        this.assessor = assessor;
        this.profiles = profiles;
    }

    /// <summary>True while entries come from <see cref="PreviewData"/> rather than discovery.</summary>
    public bool IsPreview { get; private set; }

    public bool ShowUpdateBanner { get; private set; }

    public string BannerVersion { get; private set; }

    public event EventHandler<DialogRequest>? DialogRequested;

    /// <summary>Raised when the New Clone action should open the wizard (production only).</summary>
    public event EventHandler? NewCloneRequested;

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value ?? string.Empty))
            {
                RefreshVisibleGroups();
            }
        }
    }

    public IReadOnlyList<CloneGroupView> VisibleGroups => visibleGroups;

    public CloneItem? SelectedClone
    {
        get => selectedClone;
        set
        {
            if (SetProperty(ref selectedClone, value))
            {
                if (value != null)
                {
                    SelectedApp = null;
                    SelectedOrphan = null;
                }

                RaisePropertyChanged(nameof(HasSelection));
                RaisePropertyChanged(nameof(ShowUpdateNotice));
                RaisePropertyChanged(nameof(ShowMissingNotice));
                RaisePropertyChanged(nameof(ShowLinksCard));
                RaisePropertyChanged(nameof(SelectedSubtitle));
                RaisePropertyChanged(nameof(SelectedUpdateMessage));
                RaisePropertyChanged(nameof(SelectedMissingMessage));
                RaisePropertyChanged(nameof(SelectedModeName));
                RaisePropertyChanged(nameof(SelectedModeNote));
                RaiseCanExecute();
            }
        }
    }

    public bool HasSelection => SelectedClone != null || SelectedApp != null || SelectedOrphan != null;

    public bool HasNoResults => visibleGroups.Count == 0 && visibleAppGroups.Count == 0;

    public bool HasApps => visibleAppGroups.Count > 0;

    public bool HasClones => visibleGroups.Count > 0;

    public bool IsLoadingApps
    {
        get => isLoadingApps;
        private set
        {
            if (SetProperty(ref isLoadingApps, value))
            {
                RaisePropertyChanged(nameof(ShowAppsLoading));
            }
        }
    }

    public string? DiscoveryError
    {
        get => discoveryError;
        private set
        {
            if (SetProperty(ref discoveryError, value))
            {
                RaisePropertyChanged(nameof(ShowAppsError));
            }
        }
    }

    public bool ShowAppsLoading => IsLoadingApps;

    public bool ShowAppsError => !IsLoadingApps && DiscoveryError != null;

    public bool ShowAppsEmpty => !IsLoadingApps && DiscoveryError == null && !HasApps && !IsPreview;

    public IReadOnlyList<AppGroupView> VisibleAppGroups => visibleAppGroups;

    public DiscoveredAppItem? SelectedApp
    {
        get => selectedApp;
        set
        {
            if (SetProperty(ref selectedApp, value))
            {
                if (value != null)
                {
                    SelectedClone = null;
                    SelectedOrphan = null;
                }

                RaisePropertyChanged(nameof(HasSelection));
                RaisePropertyChanged(nameof(ShowAppDetail));
                RaisePropertyChanged(nameof(SelectedAppPublisherLine));
                RaisePropertyChanged(nameof(SelectedAppSourcesText));
                RaisePropertyChanged(nameof(SelectedAppPublisherOrUnknown));
                RaisePropertyChanged(nameof(SelectedAppVersionOrUnknown));
                RaisePropertyChanged(nameof(SelectedAppScopeText));
                RaisePropertyChanged(nameof(SelectedAppReasons));
                RaisePropertyChanged(nameof(SelectedAppNeeds));
                RaisePropertyChanged(nameof(ShowSelectedAppNeeds));
                RaisePropertyChanged(nameof(ProfilesForSelectedApp));
                RaisePropertyChanged(nameof(ShowProfilesEmpty));
                CreateCloneFromAppCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool ShowAppDetail => SelectedApp != null;

    public bool ShowCloneDetail => SelectedClone != null;

    public bool IsLoadingProfiles
    {
        get => isLoadingProfiles;
        private set
        {
            if (SetProperty(ref isLoadingProfiles, value))
            {
                RaisePropertyChanged(nameof(ShowProfilesLoading));
            }
        }
    }

    public string? ProfilesError
    {
        get => profilesError;
        private set
        {
            if (SetProperty(ref profilesError, value))
            {
                RaisePropertyChanged(nameof(ShowProfilesError));
            }
        }
    }

    public bool ShowProfilesLoading => IsLoadingProfiles;

    public bool ShowProfilesError => !IsLoadingProfiles && ProfilesError != null;

    public bool ShowProfilesEmpty =>
        !IsLoadingProfiles && ProfilesError == null && SelectedApp != null && ProfilesForSelectedApp.Count == 0;

    /// <summary>Persisted profiles for the selected application, if any.</summary>
    public IReadOnlyList<CloneProfile> ProfilesForSelectedApp => SelectedApp is { } selected
        ? profileRecords.Where(p => p.AppId == selected.App.Id).ToList()
        : Array.Empty<CloneProfile>();

    /// <summary>Profiles whose application is not currently discovered. Kept, never deleted.</summary>
    public IReadOnlyList<OrphanProfileItem> OrphanProfiles => orphanProfiles;

    public bool HasOrphans => orphanProfiles.Count > 0;

    public OrphanProfileItem? SelectedOrphan
    {
        get => selectedOrphan;
        set
        {
            if (SetProperty(ref selectedOrphan, value))
            {
                if (value != null)
                {
                    SelectedClone = null;
                    SelectedApp = null;
                }

                RaisePropertyChanged(nameof(HasSelection));
                RaisePropertyChanged(nameof(ShowOrphanDetail));
            }
        }
    }

    public bool ShowOrphanDetail => SelectedOrphan != null;

    public string SelectedAppPublisherLine => SelectedApp is { } item
        ? string.Join(" ·", new[] { item.App.Publisher, item.App.Version }.Where(s => !string.IsNullOrEmpty(s)))
        : string.Empty;

    public string SelectedAppSourcesText => SelectedApp is { } item
        ? string.Join(", ", item.App.Sources.Select(SourceText))
        : string.Empty;

    public string SelectedAppPublisherOrUnknown =>
        string.IsNullOrEmpty(SelectedApp?.App.Publisher) ? "Unknown" : SelectedApp.App.Publisher;

    public string SelectedAppVersionOrUnknown =>
        string.IsNullOrEmpty(SelectedApp?.App.Version) ? "Unknown" : SelectedApp.App.Version;

    public string SelectedAppScopeText =>
        SelectedApp is { } item ? ScopeText(item.App.Scope) : string.Empty;

    /// <summary>Missing prerequisites behind the current assessment, if any.</summary>
    public IReadOnlyList<string> SelectedAppNeeds =>
        SelectedApp is { } item ? item.Assessment.MissingPrerequisites : Array.Empty<string>();

    public bool ShowSelectedAppNeeds => SelectedAppNeeds.Count > 0;

    public IReadOnlyList<string> SelectedAppReasons =>
        SelectedApp is { } item ? item.Assessment.Reasons : Array.Empty<string>();

    public bool ShowUpdateNotice => SelectedClone?.Status == CloneStatusKind.UpdateAvailable;

    public bool ShowMissingNotice => SelectedClone?.Status == CloneStatusKind.OriginalMissing;

    /// <summary>macOS shows the links card for identity clones whose app declares URL schemes.</summary>
    public bool ShowLinksCard =>
        SelectedClone?.Mode == CloneModeKind.Identity && (SelectedClone?.HasLinkSchemes ?? false);

    public bool LinkRoutingOn
    {
        get => linkRoutingOn;
        set => SetProperty(ref linkRoutingOn, value);
    }

    public string SelectedSubtitle => SelectedClone is { } c
        ? $"Clone of {c.AppName} {c.SourceVersion}"
        : string.Empty;

    public string SelectedUpdateMessage => SelectedClone is { Status: CloneStatusKind.UpdateAvailable } c
        ? $"{c.AppName} {c.AvailableVersion} is installed. Refresh this clone to use it. Logins and data stay."
        : string.Empty;

    public string SelectedMissingMessage => SelectedClone is { Status: CloneStatusKind.OriginalMissing } c
        ? $"{c.AppName} isn't where it was. Show Mitosis where it is now to use this clone again."
        : string.Empty;

    public string SelectedModeName => SelectedClone is { } c ? ModeDisplayName(c.Mode) : string.Empty;

    public string SelectedModeNote => SelectedClone is { } c
        ? c.Mode == CloneModeKind.Identity
            ? "Own login, data, notifications and taskbar icon"
            : $"Own login and data; uses {c.AppName}'s taskbar icon"
        : string.Empty;

    /// <summary>All assessed applications (unfiltered) for wizard handoff.</summary>
    public IReadOnlyList<DiscoveredAppItem> AllAppItems => appItems.ToList();

    /// <summary>All persisted profiles (unfiltered) for wizard duplicate checks.</summary>
    public IReadOnlyList<CloneProfile> AllProfiles => profileRecords.ToList();

    /// <summary>Selects an application by stable ID; returns false when absent.</summary>
    public bool SelectAppById(string appId)
    {
        var item = appItems.FirstOrDefault(i => i.App.Id == appId);
        if (item == null)
        {
            return false;
        }

        SelectedApp = item;
        return true;
    }

    /// <summary>Builds a wizard over the current discovery snapshot. Requires the
    /// discovery-backed constructor; preview mode keeps its placeholder instead.</summary>
    public NewCloneWizardViewModel CreateWizard()
    {
        if (profiles == null)
        {
            throw new InvalidOperationException("The wizard needs a profile repository.");
        }

        return new NewCloneWizardViewModel(
            appItems.Select(i => new DiscoveredAppItem(i.App, i.Assessment)).ToList(),
            profileRecords.ToList(),
            profiles,
            SelectedApp,
            ProductionCatalog.Strategies);
    }

    public string CloneCountSubtitle => clones.Count switch
    {
        0 => string.Empty,
        1 => "1 clone",
        var n => $"{n} clones",
    };

    public RelayCommand NewCloneCommand { get; }

    public RelayCommand OpenCloneCommand { get; }

    public RelayCommand EditBadgeCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand DeleteCommand { get; }

    public RelayCommand CleanCachesCommand { get; }

    public RelayCommand FindAppCommand { get; }

    public RelayCommand RevealCloneCommand { get; }

    public RelayCommand RevealOriginalCommand { get; }

    public RelayCommand RevealDataCommand { get; }

    public RelayCommand DismissBannerCommand { get; }

    public RelayCommand InstallUpdateCommand { get; }

    public RelayCommand RefreshAppsCommand { get; }

    public RelayCommand ShowPreviewCommand { get; }

    public RelayCommand CreateCloneFromAppCommand { get; }

    public static string StatusText(CloneItem clone) => clone.Status switch
    {
        CloneStatusKind.OriginalMissing => "Original missing",
        CloneStatusKind.UpdateAvailable => "Update available",
        _ => clone.IsRunning ? "Running" : "Not running",
    };

    public static string ModeDisplayName(CloneModeKind mode) => mode switch
    {
        CloneModeKind.Identity => "Full clone",
        CloneModeKind.Fallback => "Compatibility mode",
        _ => mode.ToString(),
    };

    public static string AssessmentStatusText(CompatibilityStatus status) => status switch
    {
        CompatibilityStatus.Supported => "Supported",
        CompatibilityStatus.NeedsReview => "Needs review",
        CompatibilityStatus.Unsupported => "Not supported",
        _ => "Not yet assessed",
    };

    /// <summary>Runs discovery off the UI thread, assesses each result, loads persisted
    /// profiles, and publishes the grouped lists. Failures surface as actionable error
    /// states; cancellation is silent.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (discovery == null || assessor == null)
        {
            return;
        }

        IsLoadingApps = true;
        IsLoadingProfiles = profiles != null;
        DiscoveryError = null;
        ProfilesError = null;
        try
        {
            var found = await discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
            var keptAppId = SelectedApp?.App.Id;
            appItems = found.Select(a => new DiscoveredAppItem(a, assessor.Assess(a))).ToList();
            RefreshVisibleGroups();
            SelectedApp = appItems.FirstOrDefault(i => i.App.Id == keptAppId);

            if (profiles != null)
            {
                try
                {
                    var keptOrphanId = SelectedOrphan?.Profile.Id;
                    profileRecords = (await profiles.ListAsync(cancellationToken).ConfigureAwait(false)).ToList();
                    RefreshVisibleGroups();
                    SelectedOrphan = orphanProfiles.FirstOrDefault(o => o.Profile.Id == keptOrphanId);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    ProfilesError = $"Couldn't load profiles ({ex.GetType().Name}). Try again.";
                }
                finally
                {
                    IsLoadingProfiles = false;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            DiscoveryError = $"Couldn't list installed apps ({ex.GetType().Name}). Try again.";
        }
        finally
        {
            IsLoadingApps = false;
            IsLoadingProfiles = false;
        }
    }

    /// <summary>Explicit opt-in fallback: replaces live content with clearly marked preview
    /// data. Never invoked automatically; see <see cref="ShowPreviewCommand"/>.</summary>
    private void ShowPreview()
    {
        var preview = PreviewData.CreateMain();
        groups = PreviewData.CreateGroups().ToList();
        clones = PreviewData.CreateClones().ToList();
        appItems = new List<DiscoveredAppItem>();
        profileRecords = new List<CloneProfile>();
        selectedApp = null;
        selectedOrphan = null;
        IsPreview = true;
        ShowUpdateBanner = preview.ShowUpdateBanner;
        BannerVersion = preview.BannerVersion;
        DiscoveryError = null;
        ProfilesError = null;
        SearchText = string.Empty;
        RefreshVisibleGroups();
        RaisePropertyChanged(nameof(HasApps));
        RaisePropertyChanged(nameof(HasClones));
        RaisePropertyChanged(nameof(HasOrphans));
        RaisePropertyChanged(nameof(ShowAppsEmpty));
        RaisePropertyChanged(nameof(HasSelection));
        RaisePropertyChanged(nameof(ShowAppDetail));
        RaisePropertyChanged(nameof(ShowOrphanDetail));
    }

    public static string ProfileStateText(CloneProfileState state) => state switch
    {
        CloneProfileState.Planned => "Planned",
        CloneProfileState.Provisioning => "Provisioning",
        CloneProfileState.Ready => "Ready",
        CloneProfileState.Failed => "Failed",
        _ => "Unavailable",
    };

    public static string ProfileSubtitleText(CloneProfile profile) => profile.State switch
    {
        CloneProfileState.Planned => "Planned — metadata only, not a working clone.",
        _ => ProfileStateText(profile.State),
    };

    public static string ProfileCreatedText(CloneProfile profile) =>
        "Created " + profile.CreatedAt.ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    public static string ScopeText(InstallScope scope) => scope switch
    {
        InstallScope.CurrentUser => "Current user",
        InstallScope.Machine => "This machine",
        _ => "Unknown scope",
    };

    public static string SourceText(DiscoverySource source) => source switch
    {
        DiscoverySource.CurrentUserUninstall => "Current-user registration",
        DiscoverySource.LocalMachineUninstall32 => "Machine registration (32-bit)",
        DiscoverySource.LocalMachineUninstall64 => "Machine registration (64-bit)",
        DiscoverySource.AppxPackage => "Packaged app registration",
        _ => source.ToString(),
    };

    private void RaiseCanExecute()
    {
        OpenCloneCommand.RaiseCanExecuteChanged();
        EditBadgeCommand.RaiseCanExecuteChanged();
        RefreshCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
        CleanCachesCommand.RaiseCanExecuteChanged();
        FindAppCommand.RaiseCanExecuteChanged();
        RevealCloneCommand.RaiseCanExecuteChanged();
        RevealOriginalCommand.RaiseCanExecuteChanged();
        RevealDataCommand.RaiseCanExecuteChanged();
    }

    private void RaisePlaceholder(string action, string message) =>
        DialogRequested?.Invoke(this, new DialogRequest(
            $"{action} is not available in this preview",
            message + " Preview data only — nothing was changed."));

    private void RefreshVisibleGroups()
    {
        var query = searchText.Trim();
        var next = new List<CloneGroupView>();
        foreach (var group in groups.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
        {
            var items = clones
                .Where(c => c.GroupId == group.Id && Matches(c, group, query))
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (items.Count > 0)
            {
                next.Add(new CloneGroupView(group, items));
            }
        }

        visibleGroups = next;
        if (selectedClone != null && !next.SelectMany(g => g.Items).Any(c => c.Id == selectedClone.Id))
        {
            SelectedClone = null;
        }

        var appNext = new List<AppGroupView>();
        foreach (var key in appItems.Select(GroupKey).Distinct().OrderBy(k => k == "#" ? string.Empty : k, StringComparer.Ordinal))
        {
            var items = appItems
                .Where(i => GroupKey(i) == key && MatchesApp(i, query))
                .OrderBy(i => i.App.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.App.Id, StringComparer.Ordinal)
                .ToList();
            if (items.Count > 0)
            {
                appNext.Add(new AppGroupView(
                    new AppGroupItem(key, key == "#" ? "#" : key, key, "#0A84FF"), items));
            }
        }

        visibleAppGroups = appNext;
        if (selectedApp != null && !appNext.SelectMany(g => g.Items).Any(i => i.App.Id == selectedApp.App.Id))
        {
            SelectedApp = null;
        }

        var knownIds = new HashSet<string>(appItems.Select(i => i.App.Id), StringComparer.Ordinal);
        orphanProfiles = profileRecords
            .Where(p => !knownIds.Contains(p.AppId))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Id)
            .Select(p => new OrphanProfileItem(p))
            .ToList();
        if (selectedOrphan != null && !orphanProfiles.Any(o => o.Profile.Id == selectedOrphan.Profile.Id))
        {
            SelectedOrphan = null;
        }

        RaisePropertyChanged(nameof(VisibleGroups));
        RaisePropertyChanged(nameof(VisibleAppGroups));
        RaisePropertyChanged(nameof(HasApps));
        RaisePropertyChanged(nameof(HasClones));
        RaisePropertyChanged(nameof(HasOrphans));
        RaisePropertyChanged(nameof(HasNoResults));
        RaisePropertyChanged(nameof(ShowAppsEmpty));
        RaisePropertyChanged(nameof(ShowProfilesEmpty));
        RaisePropertyChanged(nameof(ProfilesForSelectedApp));
    }

    private static string GroupKey(DiscoveredAppItem item)
    {
        var first = item.App.DisplayName.Trim().FirstOrDefault(char.IsLetterOrDigit);
        if (first == default || !char.IsLetter(first))
        {
            return "#";
        }

        return char.ToUpperInvariant(first).ToString();
    }

    private static bool MatchesApp(DiscoveredAppItem item, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }

        var compare = CultureInfo.InvariantCulture.CompareInfo;
        return compare.IndexOf(item.App.DisplayName, query, MatchOptions) >= 0
            || (item.App.Publisher != null && compare.IndexOf(item.App.Publisher, query, MatchOptions) >= 0);
    }

    private static bool Matches(CloneItem clone, AppGroupItem group, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }

        var compare = CultureInfo.InvariantCulture.CompareInfo;
        return compare.IndexOf(clone.Name, query, MatchOptions) >= 0
            || compare.IndexOf(group.Name, query, MatchOptions) >= 0;
    }
}
