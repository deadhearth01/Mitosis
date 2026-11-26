using Mitosis.Domain.Updates;

namespace Mitosis.Domain.Tests;

/// <summary>Selection rules mirrored from the macOS update checker: drafts and
/// prereleases are skipped for stable builds, unusable pages are skipped, and only a
/// strictly newer tag can win.</summary>
[TestClass]
public sealed class ReleaseSelectionTests
{
    private const string Current = "0.2.2+6f9ee6b";

    private static ReleaseCandidate Release(
        string tag, bool draft = false, bool prerelease = false,
        string url = "https://github.com/deadhearth01/Mitosis/releases/tag/v0.2.3") =>
        new(tag, draft, prerelease, url);

    [TestMethod]
    public void PicksTheNewestNewerRelease()
    {
        var selected = ReleaseSelection.SelectNewestNewer(
            new[]
            {
                Release("v0.2.1", url: "https://example.test/old"),
                Release("v0.2.3"),
                Release("v0.2.4-beta", prerelease: true, url: "https://example.test/beta"),
                Release("v0.2.2", url: "https://example.test/current"),
            },
            Current);

        Assert.IsNotNull(selected);
        Assert.AreEqual("0.2.3", selected.Version);
        Assert.AreEqual("https://github.com/deadhearth01/Mitosis/releases/tag/v0.2.3", selected.Url);
    }

    [TestMethod]
    public void SkipsDrafts()
    {
        var selected = ReleaseSelection.SelectNewestNewer(
            new[] { Release("v0.3.0", draft: true) }, Current);

        Assert.IsNull(selected, "A draft is not published to anyone.");
    }

    [TestMethod]
    public void SkipsPrereleasesForAStableBuild()
    {
        var selected = ReleaseSelection.SelectNewestNewer(
            new[] { Release("v0.3.0-beta.1", prerelease: true) }, Current);

        Assert.IsNull(selected);
    }

    [TestMethod]
    public void AllowsPrereleasesWhenTheRunningBuildIsAlreadyAPrerelease()
    {
        var selected = ReleaseSelection.SelectNewestNewer(
            new[] { Release("v0.3.0-beta.2", prerelease: true) },
            "0.3.0-beta.1+abc");

        Assert.IsNotNull(selected);
        Assert.AreEqual("0.3.0-beta.2", selected.Version);
    }

    [TestMethod]
    public void SkipsReleasesWithoutAUsablePage()
    {
        var selected = ReleaseSelection.SelectNewestNewer(
            new[]
            {
                Release("v0.3.0", url: ""),
                Release("v0.3.1", url: "file:///C:/temp/release.html"),
                Release("v0.3.2", url: "not a url"),
            },
            Current);

        Assert.IsNull(selected, "A banner with a dead link is worse than no banner.");
    }

    [TestMethod]
    public void ReturnsNullWhenNothingIsNewer()
    {
        var selected = ReleaseSelection.SelectNewestNewer(
            new[]
            {
                Release("v0.2.2"),
                Release("v0.2.1"),
            },
            Current);

        Assert.IsNull(selected);
    }

    [TestMethod]
    public void IgnoresUnparseableTags()
    {
        var selected = ReleaseSelection.SelectNewestNewer(
            new[] { Release("nightly"), Release("v0.2.3") }, Current);

        Assert.IsNotNull(selected);
        Assert.AreEqual("0.2.3", selected.Version);
    }

    [TestMethod]
    public void EmptyFeed_IsNotAnUpdate()
    {
        Assert.IsNull(ReleaseSelection.SelectNewestNewer(Array.Empty<ReleaseCandidate>(), Current));
    }
}
