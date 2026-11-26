namespace Mitosis.Domain.Updates;

/// <summary>One release as reported by the feed, before any version policy is applied.
/// <see cref="TagName"/> is raw (<c>v0.2.3</c>); display and comparison normalize it.</summary>
public sealed record ReleaseCandidate(
    string TagName,
    bool IsDraft,
    bool IsPrerelease,
    string HtmlUrl);

/// <summary>A release that is newer than the running build and has a usable page.</summary>
public sealed record SelectedRelease(string Version, string Url);

/// <summary>Mirrors the macOS <c>UpdateChecker.pick</c> rule: ignore drafts, ignore
/// prereleases unless this build is already a prerelease, require a newer parseable
/// version, and take the newest. Candidates without an absolute HTTP(S) page are
/// unusable and skipped rather than shown as a dead banner.</summary>
public static class ReleaseSelection
{
    public static SelectedRelease? SelectNewestNewer(
        IEnumerable<ReleaseCandidate> candidates,
        string currentVersion)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var onPrerelease = ReleaseVersion.DisplayVersion(currentVersion).Contains('-');
        SelectedRelease? best = null;

        foreach (var candidate in candidates)
        {
            if (candidate.IsDraft || (candidate.IsPrerelease && !onPrerelease))
            {
                continue;
            }

            if (!IsUsableUrl(candidate.HtmlUrl))
            {
                continue;
            }

            var version = ReleaseVersion.DisplayVersion(candidate.TagName);
            if (!ReleaseVersion.IsNewer(version, currentVersion))
            {
                continue;
            }

            if (best is null || ReleaseVersion.IsNewer(version, best.Version))
            {
                best = new SelectedRelease(version, candidate.HtmlUrl);
            }
        }

        return best;
    }

    private static bool IsUsableUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
