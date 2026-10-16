using System.Globalization;

namespace Mitosis.Windows.ViewModels;

/// <summary>Main-window presentation state: sidebar groups, filtering, selection, notices.
/// Filtering, selection, and dialog routing are real logic and unit-tested; the underlying
/// entries are preview data until the engine lands (see <see cref="PreviewData"/>).</summary>
public sealed class MainViewModel : ObservableObject
{
    private static readonly CompareOptions MatchOptions =
        CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    private readonly List<CloneItem> clones;
    private readonly List<AppGroupItem> groups;

    private string searchText = string.Empty;
    private CloneItem? selectedClone;
    private List<CloneGroupView> visibleGroups = new();
    private bool linkRoutingOn;

    public MainViewModel(IEnumerable<AppGroupItem> groups, IEnumerable<CloneItem> clones, bool isPreview,
        bool showUpdateBanner = false, string bannerVersion = "")
    {
        this.groups = groups.ToList();
        this.clones = clones.ToList();
        IsPreview = isPreview;
        ShowUpdateBanner = showUpdateBanner;
        BannerVersion = bannerVersion;

        NewCloneCommand = new RelayCommand(() => RaisePlaceholder("New Clone",
            "Clone creation arrives with the engine in a later stage. This preview shows the shell only."));
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

        RefreshVisibleGroups();
    }

    /// <summary>True while entries come from <see cref="PreviewData"/> rather than discovery.</summary>
    public bool IsPreview { get; }

    public bool ShowUpdateBanner { get; private set; }

    public string BannerVersion { get; }

    public event EventHandler<DialogRequest>? DialogRequested;

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

    public bool HasSelection => SelectedClone != null;

    public bool HasNoResults => visibleGroups.Count == 0;

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

        RaisePropertyChanged(nameof(VisibleGroups));
        RaisePropertyChanged(nameof(HasNoResults));
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
