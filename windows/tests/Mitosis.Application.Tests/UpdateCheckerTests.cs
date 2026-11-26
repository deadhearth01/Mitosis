using Mitosis.Application.Updates;
using Mitosis.Domain.Updates;

namespace Mitosis.Application.Tests;

/// <summary>The manual update check never retries, never downloads, and turns every
/// failure into a display-ready outcome. These tests pin the timeout, cancellation, and
/// honest-failure behavior that keep a flaky network from breaking the app.</summary>
[TestClass]
public sealed class UpdateCheckerTests
{
    private const string Current = "0.2.2+6f9ee6b";

    private static UpdateChecker Checker(
        Func<CancellationToken, Task<IReadOnlyList<ReleaseCandidate>>> fetch,
        TimeSpan? timeout = null) =>
        new(new StubFeed(fetch), Current, timeout);

    private static ReleaseCandidate Release(string tag, string url = "https://example.test/release") =>
        new(tag, false, false, url);

    [TestMethod]
    public async Task NewerRelease_IsReportedWithItsPage()
    {
        var result = await Checker(_ => Task.FromResult<IReadOnlyList<ReleaseCandidate>>(
            new[] { Release("v0.2.3") })).CheckAsync();

        Assert.AreEqual(UpdateCheckOutcome.UpdateAvailable, result.Outcome);
        Assert.AreEqual(Current, result.CurrentVersion);
        Assert.AreEqual("0.2.3", result.LatestVersion);
        Assert.AreEqual("https://example.test/release", result.ReleaseUrl);
        Assert.IsNull(result.Failure);
    }

    [TestMethod]
    public async Task SameOrOlderRelease_IsUpToDate()
    {
        var result = await Checker(_ => Task.FromResult<IReadOnlyList<ReleaseCandidate>>(
            new[] { Release("v0.2.2"), Release("v0.2.1") })).CheckAsync();

        Assert.AreEqual(UpdateCheckOutcome.UpToDate, result.Outcome);
        Assert.IsNull(result.LatestVersion);
        Assert.IsNull(result.ReleaseUrl);
        Assert.IsNull(result.Failure);
    }

    [TestMethod]
    public async Task Timeout_IsAFailureNotAnUpdate()
    {
        var checker = Checker(
            async cancellationToken =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return Array.Empty<ReleaseCandidate>();
            },
            TimeSpan.FromMilliseconds(80));

        var result = await checker.CheckAsync();

        Assert.AreEqual(UpdateCheckOutcome.Failed, result.Outcome);
        StringAssert.Contains(result.Failure, "timed out");
        Assert.IsNull(result.LatestVersion);
    }

    [TestMethod]
    public async Task CallerCancellation_IsNotSwallowed()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var checker = Checker(
            cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyList<ReleaseCandidate>>(Array.Empty<ReleaseCandidate>());
            });

        try
        {
            await checker.CheckAsync(cancellation.Token);
            Assert.Fail("Caller cancellation must propagate instead of becoming an update state.");
        }
        catch (OperationCanceledException)
        {
            // Expected: closing the window cancels the check.
        }
    }

    [TestMethod]
    public async Task FeedProblem_IsShownVerbatim()
    {
        var result = await Checker(
            _ => throw new ReleaseFeedException("GitHub didn't answer normally. Try again later."))
            .CheckAsync();

        Assert.AreEqual(UpdateCheckOutcome.Failed, result.Outcome);
        Assert.AreEqual("GitHub didn't answer normally. Try again later.", result.Failure);
    }

    [TestMethod]
    public async Task NetworkFailure_HasAReadableMessage()
    {
        var result = await Checker(
            _ => throw new HttpRequestException("offline")).CheckAsync();

        Assert.AreEqual(UpdateCheckOutcome.Failed, result.Outcome);
        StringAssert.Contains(result.Failure, "internet connection");
    }

    [TestMethod]
    public async Task UnexpectedFailure_IsHonestAndStillSafe()
    {
        var result = await Checker(
            _ => throw new InvalidOperationException("boom")).CheckAsync();

        Assert.AreEqual(UpdateCheckOutcome.Failed, result.Outcome);
        StringAssert.Contains(result.Failure, "couldn't read");
        Assert.IsNull(result.LatestVersion, "A broken response must not fabricate a version.");
    }

    [TestMethod]
    public async Task UnparseableCurrentVersion_FailsWithoutCallingTheNetwork()
    {
        var called = false;
        var checker = new UpdateChecker(
            new StubFeed(_ =>
            {
                called = true;
                return Task.FromResult<IReadOnlyList<ReleaseCandidate>>(Array.Empty<ReleaseCandidate>());
            }),
            "nightly");

        var result = await checker.CheckAsync();

        Assert.AreEqual(UpdateCheckOutcome.Failed, result.Outcome);
        StringAssert.Contains(result.Failure, "version");
        Assert.IsFalse(called, "Nothing should be requested when this build cannot be compared.");
    }

    [TestMethod]
    public void Constructor_RejectsEmptyInputs()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new UpdateChecker(null!, Current));
        Assert.ThrowsExactly<ArgumentException>(
            () => new UpdateChecker(new StubFeed(_ => Task.FromResult<IReadOnlyList<ReleaseCandidate>>(
                Array.Empty<ReleaseCandidate>())), " "));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new UpdateChecker(new StubFeed(_ => Task.FromResult<IReadOnlyList<ReleaseCandidate>>(
                Array.Empty<ReleaseCandidate>())), Current, TimeSpan.Zero));
    }

    private sealed class StubFeed(
        Func<CancellationToken, Task<IReadOnlyList<ReleaseCandidate>>> fetch) : IReleaseFeed
    {
        public Task<IReadOnlyList<ReleaseCandidate>> FetchAsync(CancellationToken cancellationToken = default) =>
            fetch(cancellationToken);
    }
}
