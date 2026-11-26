using Mitosis.Application.Updates;
using Mitosis.Windows.ViewModels;

namespace Mitosis.ViewModels.Tests;

/// <summary>Manual, opt-in update state. Nothing is checked automatically, a dismissal
/// lasts for the session, and failure is shown as itself rather than as an update.</summary>
[TestClass]
public sealed class UpdatesViewModelTests
{
    private const string Current = "0.2.2+6f9ee6b";

    private static UpdatesViewModel Model(
        Func<CancellationToken, Task<UpdateCheckResult>> check) =>
        new(new StubChecker(check), Current);

    [TestMethod]
    public void FreshState_IsIdleAndHasNoBanner()
    {
        var model = Model(_ => Task.FromResult(UpdateCheckResult.UpToDate(Current)));

        Assert.AreEqual("0.2.2", model.CurrentVersion, "Build metadata is not shown.");
        Assert.IsFalse(model.IsChecking);
        Assert.IsFalse(model.HasUpdate);
        Assert.IsFalse(model.ShowsBanner);
        Assert.IsNull(model.ReleaseUrl);
        Assert.IsTrue(model.CheckCommand.CanExecute(null));
        StringAssert.Contains(model.StatusText, "Check GitHub Releases");
    }

    [TestMethod]
    public async Task Available_ShowsTheBannerAndTheReleasePage()
    {
        var model = Model(_ => Task.FromResult(
            UpdateCheckResult.Available(Current, "0.2.3", "https://example.test/v0.2.3")));

        var changed = new List<string>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        await model.CheckAsync();

        Assert.IsTrue(model.HasUpdate);
        Assert.IsTrue(model.ShowsBanner);
        Assert.AreEqual("0.2.3", model.LatestVersion);
        Assert.AreEqual("Mitosis 0.2.3 is available.", model.BannerTitle);
        Assert.AreEqual("https://example.test/v0.2.3", model.ReleaseUrl);
        StringAssert.Contains(model.StatusText, "0.2.3 is available");
        CollectionAssert.Contains(changed, nameof(UpdatesViewModel.ShowsBanner));
        CollectionAssert.Contains(changed, nameof(UpdatesViewModel.BannerTitle));
    }

    [TestMethod]
    public async Task Dismissal_LastsForTheSession()
    {
        var model = Model(_ => Task.FromResult(
            UpdateCheckResult.Available(Current, "0.2.3", "https://example.test/v0.2.3")));
        await model.CheckAsync();

        model.Dismiss();

        Assert.IsFalse(model.ShowsBanner);
        Assert.IsTrue(model.HasUpdate, "The update is still known; only the banner is hidden.");

        await model.CheckAsync();

        Assert.IsFalse(model.ShowsBanner, "macOS keeps a dismissed banner dismissed for this run.");
    }

    [TestMethod]
    public async Task UpToDate_IsStatedPlainlyWithNoBanner()
    {
        var model = Model(_ => Task.FromResult(UpdateCheckResult.UpToDate(Current)));

        await model.CheckAsync();

        Assert.IsFalse(model.ShowsBanner);
        Assert.AreEqual(string.Empty, model.LatestVersion);
        StringAssert.Contains(model.StatusText, "0.2.2 is up to date");
    }

    [TestMethod]
    public async Task Failure_IsShownWithoutInventingARelease()
    {
        var model = Model(_ => Task.FromResult(
            UpdateCheckResult.Failed("Couldn't reach GitHub. Check your internet connection.")));

        await model.CheckAsync();

        Assert.IsFalse(model.ShowsBanner);
        Assert.AreEqual(string.Empty, model.LatestVersion);
        Assert.AreEqual("Couldn't reach GitHub. Check your internet connection.", model.StatusText);
    }

    [TestMethod]
    public async Task Checking_DisablesTheCommandAndIgnoresASecondStart()
    {
        var calls = 0;
        var pending = new TaskCompletionSource<UpdateCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var model = Model(_ =>
        {
            calls++;
            return pending.Task;
        });

        var running = model.CheckAsync();
        Assert.IsTrue(model.IsChecking);
        Assert.IsFalse(model.CheckCommand.CanExecute(null));

        await model.CheckAsync();
        Assert.AreEqual(1, calls, "Pressing the button twice must not start two checks.");

        pending.SetResult(UpdateCheckResult.UpToDate(Current));
        await running;

        Assert.IsFalse(model.IsChecking);
        Assert.IsTrue(model.CheckCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task Cancellation_LeavesANeutralStatusInsteadOfCheckingForever()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = Model(async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("unreachable");
        });
        using var cancellation = new CancellationTokenSource();

        var running = model.CheckAsync(cancellation.Token);
        await started.Task;
        cancellation.Cancel();
        await running;

        Assert.IsFalse(model.IsChecking);
        Assert.IsFalse(model.ShowsBanner);
        Assert.IsFalse(model.HasUpdate);
        StringAssert.Contains(model.StatusText, "canceled");
    }

    [TestMethod]
    public void Constructor_RejectsMissingInputs()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new UpdatesViewModel(null!, Current));
        Assert.ThrowsExactly<ArgumentException>(
            () => new UpdatesViewModel(new StubChecker(_ => Task.FromResult(
                UpdateCheckResult.UpToDate(Current))), "  "));
    }

    private sealed class StubChecker(
        Func<CancellationToken, Task<UpdateCheckResult>> check) : IUpdateChecker
    {
        public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default) =>
            check(cancellationToken);
    }
}
