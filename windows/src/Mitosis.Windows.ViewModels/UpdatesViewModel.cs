using Mitosis.Application.Updates;
using Mitosis.Domain.Updates;

namespace Mitosis.Windows.ViewModels;

/// <summary>Manual update-availability state shared by Settings (which starts the
/// check) and the main window (which shows the banner). Nothing downloads, installs,
/// or relaunches; no check runs automatically or on a timer.</summary>
public sealed class UpdatesViewModel : ObservableObject
{
    private readonly IUpdateChecker checker;
    private bool isChecking;
    private bool hasUpdate;
    private bool dismissed;
    private string latestVersion = string.Empty;
    private string? releaseUrl;
    private string statusText = "Check GitHub Releases for a newer version of Mitosis.";

    public UpdatesViewModel(IUpdateChecker checker, string currentVersion)
    {
        this.checker = checker ?? throw new ArgumentNullException(nameof(checker));
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            throw new ArgumentException("A current version is required.", nameof(currentVersion));
        }

        CurrentVersion = ReleaseVersion.DisplayVersion(currentVersion);
        CheckCommand = new RelayCommand(
            async _ => await CheckAsync().ConfigureAwait(true),
            _ => !IsChecking);
    }

    /// <summary>The running version without build metadata, for display.</summary>
    public string CurrentVersion { get; }

    /// <summary>One sentence suitable for a Settings status line or a live region.</summary>
    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public bool IsChecking
    {
        get => isChecking;
        private set
        {
            if (SetProperty(ref isChecking, value))
            {
                CheckCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>True after a successful check found a newer release.</summary>
    public bool HasUpdate
    {
        get => hasUpdate;
        private set => SetProperty(ref hasUpdate, value);
    }

    public string LatestVersion
    {
        get => latestVersion;
        private set => SetProperty(ref latestVersion, value);
    }

    public string? ReleaseUrl
    {
        get => releaseUrl;
        private set => SetProperty(ref releaseUrl, value);
    }

    /// <summary>The banner stays hidden after a deliberate dismissal for this session,
    /// matching the macOS behavior; a later check does not force it back.</summary>
    public bool ShowsBanner => hasUpdate && !dismissed;

    public string BannerTitle => $"Mitosis {LatestVersion} is available.";

    public RelayCommand CheckCommand { get; }

    public void Dismiss()
    {
        if (dismissed)
        {
            return;
        }

        dismissed = true;
        RaisePropertyChanged(nameof(ShowsBanner));
    }

    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        if (IsChecking)
        {
            return;
        }

        IsChecking = true;
        StatusText = "Checking GitHub for updates…";
        try
        {
            var result = await checker.CheckAsync(cancellationToken).ConfigureAwait(true);
            Apply(result);
        }
        catch (OperationCanceledException)
        {
            // Cancellation means the owning window closed or the caller gave up. Say so
            // instead of leaving "Checking…" behind for the next viewer of this state.
            StatusText = "The update check was canceled.";
        }
        finally
        {
            IsChecking = false;
        }
    }

    private void Apply(UpdateCheckResult result)
    {
        switch (result.Outcome)
        {
            case UpdateCheckOutcome.UpdateAvailable:
                LatestVersion = result.LatestVersion ?? string.Empty;
                ReleaseUrl = result.ReleaseUrl;
                HasUpdate = true;
                StatusText = $"Mitosis {LatestVersion} is available. The banner has a link to the release page.";
                break;
            case UpdateCheckOutcome.UpToDate:
                HasUpdate = false;
                LatestVersion = string.Empty;
                ReleaseUrl = null;
                StatusText = $"Mitosis {CurrentVersion} is up to date.";
                break;
            default:
                HasUpdate = false;
                LatestVersion = string.Empty;
                ReleaseUrl = null;
                StatusText = result.Failure ?? "Couldn't check for updates.";
                break;
        }

        RaisePropertyChanged(nameof(ShowsBanner));
        RaisePropertyChanged(nameof(BannerTitle));
    }
}
