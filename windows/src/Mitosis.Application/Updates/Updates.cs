using Mitosis.Domain.Updates;

namespace Mitosis.Application.Updates;

/// <summary>The only remote source Mitosis reads. Implementations fetch public release
/// metadata and never touch application data or user accounts.</summary>
public interface IReleaseFeed
{
    Task<IReadOnlyList<ReleaseCandidate>> FetchAsync(CancellationToken cancellationToken = default);
}

/// <summary>A feed problem already phrased for a person. The message never includes a
/// URL with credentials, a response body, or an exception stack.</summary>
public sealed class ReleaseFeedException : Exception
{
    public ReleaseFeedException(string message)
        : base(message)
    {
    }

    public ReleaseFeedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public enum UpdateCheckOutcome
{
    UpToDate,
    UpdateAvailable,
    Failed,
}

/// <summary>Result of one manual check. A failure is a first-class outcome, not an
/// exception: the caller shows it and changes nothing.</summary>
public sealed record UpdateCheckResult(
    UpdateCheckOutcome Outcome,
    string? CurrentVersion,
    string? LatestVersion,
    string? ReleaseUrl,
    string? Failure)
{
    public static UpdateCheckResult UpToDate(string currentVersion) =>
        new(UpdateCheckOutcome.UpToDate, currentVersion, null, null, null);

    public static UpdateCheckResult Available(string currentVersion, string latestVersion, string releaseUrl) =>
        new(UpdateCheckOutcome.UpdateAvailable, currentVersion, latestVersion, releaseUrl, null);

    public static UpdateCheckResult Failed(string failure) =>
        new(UpdateCheckOutcome.Failed, null, null, null, failure);
}

public interface IUpdateChecker
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default);
}
