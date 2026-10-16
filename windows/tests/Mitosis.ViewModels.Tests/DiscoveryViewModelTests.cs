using Mitosis.Application.Discovery;
using Mitosis.Domain;
using Mitosis.Windows.ViewModels;

namespace Mitosis.ViewModels.Tests;

internal sealed class FakeDiscoveryService : IAppDiscoveryService
{
    private Func<CancellationToken, Task<IReadOnlyList<DiscoveredApp>>> next =
        _ => Task.FromResult<IReadOnlyList<DiscoveredApp>>(Array.Empty<DiscoveredApp>());

    public int Calls { get; private set; }

    public void Returns(IEnumerable<DiscoveredApp> apps)
    {
        var list = apps.ToList();
        next = _ => Task.FromResult<IReadOnlyList<DiscoveredApp>>(list);
    }

    public void Throws(Exception exception) =>
        next = _ => Task.FromException<IReadOnlyList<DiscoveredApp>>(exception);

    public void Blocks(TaskCompletionSource<IReadOnlyList<DiscoveredApp>> gate) =>
        next = _ => gate.Task;

    public Task<IReadOnlyList<DiscoveredApp>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        Calls++;
        return next(cancellationToken);
    }
}

internal sealed class FakeAssessor : ICompatibilityAssessor
{
    public Func<DiscoveredApp, CompatibilityAssessment>? AssessFunc { get; set; }

    public CompatibilityAssessment Assess(DiscoveredApp app, DateTimeOffset? assessedAt = null) =>
        AssessFunc?.Invoke(app) ?? new CompatibilityAssessment(
            CompatibilityStatus.Unknown,
            new[] { "No validated independent-instance strategy for this application yet." },
            null,
            new[] { "Validated per-application strategy" },
            assessedAt ?? DateTimeOffset.UtcNow);
}

internal static class DiscoveredApps
{
    public static DiscoveredApp Make(
        string name,
        string? publisher = "Publisher",
        DiscoverySource source = DiscoverySource.CurrentUserUninstall) => new(
            Id: AppIdentity.Create(source, name, publisher),
            DisplayName: name,
            Publisher: publisher,
            Version: "1.0",
            InstallLocation: null,
            ExecutablePath: null,
            PrimarySource: source,
            Sources: new[] { source },
            Scope: InstallScope.CurrentUser,
            IsSystemComponent: false,
            IconReference: null,
            Warnings: Array.Empty<string>());
}

[TestClass]
public sealed class DiscoveryViewModelTests
{
    private static MainViewModel Create(FakeDiscoveryService discovery, FakeAssessor? assessor = null) =>
        new(discovery, assessor ?? new FakeAssessor(), new FakeProfileRepository());

    [TestMethod]
    public void Production_StartsEmptyAndNeverPreview()
    {
        var model = Create(new FakeDiscoveryService());

        Assert.IsFalse(model.IsPreview, "Production must never enable preview implicitly.");
        Assert.IsFalse(model.HasApps);
        Assert.IsFalse(model.HasSelection);
    }

    [TestMethod]
    public async Task LoadAsync_ShowsLoadingState()
    {
        var discovery = new FakeDiscoveryService();
        var gate = new TaskCompletionSource<IReadOnlyList<DiscoveredApp>>();
        discovery.Blocks(gate);
        var model = Create(discovery);

        var load = model.LoadAsync();
        Assert.IsTrue(model.IsLoadingApps);
        gate.SetResult(Array.Empty<DiscoveredApp>());
        await load;

        Assert.IsFalse(model.IsLoadingApps);
    }

    [TestMethod]
    public async Task LoadAsync_SuccessGroupsByInitial()
    {
        var discovery = new FakeDiscoveryService();
        discovery.Returns(new[]
        {
            DiscoveredApps.Make("Slack"), DiscoveredApps.Make("Signal"), DiscoveredApps.Make("7-Zip"),
        });
        var model = Create(discovery);

        await model.LoadAsync(TestContext.CancellationToken);

        Assert.IsTrue(model.HasApps);
        Assert.IsFalse(model.ShowAppsEmpty);
        Assert.IsNull(model.DiscoveryError);
        var keys = model.VisibleAppGroups.Select(g => g.Group.Name).ToList();
        CollectionAssert.AreEqual(new[] { "#", "S" }, keys);
        Assert.AreEqual(2, model.VisibleAppGroups.First(g => g.Group.Name == "S").Items.Count);
    }

    [TestMethod]
    public async Task LoadAsync_EmptyShowsEmptyStateWithoutPreviewMixing()
    {
        var model = Create(new FakeDiscoveryService());

        await model.LoadAsync(TestContext.CancellationToken);

        Assert.IsTrue(model.ShowAppsEmpty);
        Assert.IsFalse(model.HasApps);
        Assert.IsFalse(model.IsPreview);
        Assert.AreEqual(0, model.VisibleGroups.Count, "No preview clones leak into production.");
    }

    [TestMethod]
    public async Task LoadAsync_FailureShowsErrorAndRetryWorks()
    {
        var discovery = new FakeDiscoveryService();
        discovery.Throws(new UnauthorizedAccessException("denied"));
        var model = Create(discovery);

        await model.LoadAsync(TestContext.CancellationToken);

        Assert.IsTrue(model.ShowAppsError);
        Assert.IsNotNull(model.DiscoveryError);
        Assert.IsFalse(model.HasApps, "Failed discovery never falls back to fake entries silently.");

        discovery.Returns(new[] { DiscoveredApps.Make("Slack") });
        model.RefreshAppsCommand.Execute(null);
        for (var i = 0; i < 100 && !model.HasApps; i++)
        {
            await Task.Delay(50, TestContext.CancellationToken);
        }

        Assert.AreEqual(2, discovery.Calls);
        Assert.IsTrue(model.HasApps);
        Assert.IsNull(model.DiscoveryError);
    }

    [TestMethod]
    public async Task Selection_IsPreservedAcrossRefreshWhenPossible()
    {
        var discovery = new FakeDiscoveryService();
        discovery.Returns(new[] { DiscoveredApps.Make("Slack"), DiscoveredApps.Make("Signal") });
        var model = Create(discovery);
        await model.LoadAsync(TestContext.CancellationToken);
        model.SelectedApp = model.VisibleAppGroups.SelectMany(g => g.Items)
            .First(i => i.App.DisplayName == "Slack");

        discovery.Returns(new[] { DiscoveredApps.Make("Slack"), DiscoveredApps.Make("Discord") });
        await model.LoadAsync(TestContext.CancellationToken);

        Assert.AreEqual("Slack", model.SelectedApp?.App.DisplayName);

        discovery.Returns(new[] { DiscoveredApps.Make("Discord") });
        await model.LoadAsync(TestContext.CancellationToken);

        Assert.IsNull(model.SelectedApp, "Selection clears when the app disappears.");
        Assert.IsFalse(model.HasSelection);
    }

    [TestMethod]
    public async Task Search_FiltersByNameAndPublisher()
    {
        var discovery = new FakeDiscoveryService();
        discovery.Returns(new[]
        {
            DiscoveredApps.Make("Slack", "Slack Technologies"),
            DiscoveredApps.Make("Signal", "Signal Foundation"),
        });
        var model = Create(discovery);
        await model.LoadAsync(TestContext.CancellationToken);

        model.SearchText = "signal foundation";

        var visible = model.VisibleAppGroups.SelectMany(g => g.Items).ToList();
        Assert.AreEqual(1, visible.Count);
        Assert.AreEqual("Signal", visible[0].App.DisplayName);
    }

    [TestMethod]
    public async Task Assessment_IsAttachedToEachItem()
    {
        var discovery = new FakeDiscoveryService();
        discovery.Returns(new[] { DiscoveredApps.Make("Slack") });
        var assessor = new FakeAssessor
        {
            AssessFunc = app => new CompatibilityAssessment(
                CompatibilityStatus.NeedsReview, new[] { "Candidate." }, "x", new[] { "Proof" },
                DateTimeOffset.UnixEpoch),
        };
        var model = Create(discovery, assessor);

        await model.LoadAsync(TestContext.CancellationToken);

        var item = model.VisibleAppGroups.SelectMany(g => g.Items).Single();
        Assert.AreEqual(CompatibilityStatus.NeedsReview, item.Assessment.Status);
        Assert.AreEqual("x", item.Assessment.StrategyId);
    }

    [TestMethod]
    public async Task ShowPreview_IsExplicitAndIsolated()
    {
        var discovery = new FakeDiscoveryService();
        discovery.Throws(new InvalidOperationException("down"));
        var model = Create(discovery);
        await model.LoadAsync(TestContext.CancellationToken);
        Assert.IsFalse(model.IsPreview);

        model.ShowPreviewCommand.Execute(null);

        Assert.IsTrue(model.IsPreview);
        Assert.IsFalse(model.HasApps, "Preview shows clones, never discovered apps.");
        Assert.IsTrue(model.HasClones);
        Assert.AreEqual(1, discovery.Calls, "Preview does not re-query discovery.");
    }

    [TestMethod]
    public async Task CreateCloneFromApp_IsHonestPlaceholder()
    {
        var discovery = new FakeDiscoveryService();
        discovery.Returns(new[] { DiscoveredApps.Make("Slack") });
        var model = Create(discovery);
        await model.LoadAsync(TestContext.CancellationToken);

        Assert.IsFalse(model.CreateCloneFromAppCommand.CanExecute(null));
        model.SelectedApp = model.VisibleAppGroups.SelectMany(g => g.Items).Single();

        DialogRequest? request = null;
        model.DialogRequested += (_, r) => request = r;
        model.CreateCloneFromAppCommand.Execute(null);

        Assert.IsNotNull(request);
        Assert.IsTrue(request.Title.Contains("not available in this preview"));
    }

    [TestMethod]
    public async Task AssessmentPrerequisites_AreExposedForExplanation()
    {
        var discovery = new FakeDiscoveryService();
        discovery.Returns(new[] { DiscoveredApps.Make("Slack") });
        var assessor = new FakeAssessor
        {
            AssessFunc = app => new CompatibilityAssessment(
                CompatibilityStatus.NeedsReview, new[] { "Candidate." }, "x",
                new[] { "Proof of isolation", "License confirmation" }, DateTimeOffset.UnixEpoch),
        };
        var model = new MainViewModel(discovery, assessor, new FakeProfileRepository());
        await model.LoadAsync(TestContext.CancellationToken);
        model.SelectedApp = model.VisibleAppGroups.SelectMany(g => g.Items).Single();

        CollectionAssert.AreEqual(
            new[] { "Proof of isolation", "License confirmation" },
            model.SelectedAppNeeds.ToList());
        Assert.IsTrue(model.ShowSelectedAppNeeds);
    }

    [TestMethod]
    public void DisplayTexts_MatchConventions()
    {
        Assert.AreEqual("Supported", MainViewModel.AssessmentStatusText(CompatibilityStatus.Supported));
        Assert.AreEqual("Needs review", MainViewModel.AssessmentStatusText(CompatibilityStatus.NeedsReview));
        Assert.AreEqual("Not supported", MainViewModel.AssessmentStatusText(CompatibilityStatus.Unsupported));
        Assert.AreEqual("Not yet assessed", MainViewModel.AssessmentStatusText(CompatibilityStatus.Unknown));
        Assert.AreEqual("Current user", MainViewModel.ScopeText(InstallScope.CurrentUser));
        Assert.AreEqual("This machine", MainViewModel.ScopeText(InstallScope.Machine));
        Assert.AreEqual("Packaged app registration", MainViewModel.SourceText(DiscoverySource.AppxPackage));
    }

    public TestContext TestContext { get; set; } = null!;
}
