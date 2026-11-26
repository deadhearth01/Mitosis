using Mitosis.Domain.Updates;

namespace Mitosis.Application.Updates;

/// <summary>Read-only availability check against a release feed. Uses a 15-second
/// timeout, never retries automatically, downloads nothing, and performs no telemetry.
/// Failures are returned as display-ready outcomes so the caller cannot crash on a
/// flaky network.</summary>
public sealed class UpdateChecker : IUpdateChecker
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly IReleaseFeed feed;
    private readonly string currentVersion;

    public UpdateChecker(IReleaseFeed feed, string currentVersion, TimeSpan? timeout = null)
    {
        this.feed = feed ?? throw new ArgumentNullException(nameof(feed));
        this.currentVersion = string.IsNullOrWhiteSpace(currentVersion)
            ? throw new ArgumentException("A current version is required.", nameof(currentVersion))
            : currentVersion;
        Timeout = timeout ?? DefaultTimeout;
        if (Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "The timeout must be positive.");
        }
    }

    public TimeSpan Timeout { get; }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!ReleaseVersion.TryParse(currentVersion, out _))
        {
            return UpdateCheckResult.Failed(
                "This build's version couldn't be compared with the release list.");
        }

        using var timeout = new CancellationTokenSource(Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            var candidates = await feed.FetchAsync(linked.Token).ConfigureAwait(false);
            var newer = ReleaseSelection.SelectNewestNewer(candidates, currentVersion);
            return newer is { } selected
                ? UpdateCheckResult.Available(currentVersion, selected.Version, selected.Url)
                : UpdateCheckResult.UpToDate(currentVersion);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UpdateCheckResult.Failed("The update check timed out. Try again later.");
        }
        catch (ReleaseFeedException ex)
        {
            return UpdateCheckResult.Failed(ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            return UpdateCheckResult.Failed("Couldn't reach GitHub. Check your internet connection.");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return UpdateCheckResult.Failed("GitHub sent a response Mitosis couldn't read.");
        }
    }
}
