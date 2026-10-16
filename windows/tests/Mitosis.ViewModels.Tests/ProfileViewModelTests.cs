using Mitosis.Application.Profiles;
using Mitosis.Domain;
using Mitosis.Windows.ViewModels;

namespace Mitosis.ViewModels.Tests;

internal sealed class FakeProfileRepository : ICloneProfileRepository
{
    public List<CloneProfile> Store { get; } = new();

    public Exception? ListFailure { get; set; }

    public TaskCompletionSource<IReadOnlyList<CloneProfile>>? Gate { get; set; }

    public int ListCalls { get; private set; }

    public TaskCompletionSource<CloneProfile>? AddGate { get; set; }

    public int AddCalls { get; private set; }

    public async Task<IReadOnlyList<CloneProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        ListCalls++;
        if (Gate != null)
        {
            return await Gate.Task.WaitAsync(cancellationToken);
        }

        if (ListFailure != null)
        {
            throw ListFailure;
        }

        return Store.ToList();
    }

    public Task<CloneProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Store.FirstOrDefault(p => p.Id == id));

    public Task<CloneProfile> AddAsync(CloneProfile profile, CancellationToken cancellationToken = default)
    {
        AddCalls++;
        if (AddGate != null)
        {
            return AddGate.Task;
        }

        Store.Add(profile);
        return Task.FromResult(profile);
    }

    public Task<CloneProfile> UpdateAsync(CloneProfile profile, CancellationToken cancellationToken = default) =>
        Task.FromResult(profile);

    public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Store.RemoveAll(p => p.Id == id);
        return Task.CompletedTask;
    }
}

[TestClass]
public sealed class ProfileViewModelTests
{
    private static (FakeDiscoveryService discovery, FakeProfileRepository profiles, MainViewModel model) Create(
        IEnumerable<DiscoveredApp>? apps = null)
    {
        var discovery = new FakeDiscoveryService();
        discovery.Returns(apps ?? Array.Empty<DiscoveredApp>());
        var profiles = new FakeProfileRepository();
        return (discovery, profiles, new MainViewModel(discovery, new FakeAssessor(), profiles));
    }

    [TestMethod]
    public async Task ZeroProfiles_ShowsEmptyList()
    {
        var (_, _, model) = Create(new[] { DiscoveredApps.Make("Slack") });
        await model.LoadAsync(TestContext.CancellationToken);
        model.SelectedApp = model.VisibleAppGroups.SelectMany(g => g.Items).Single();

        Assert.AreEqual(0, model.ProfilesForSelectedApp.Count);
        Assert.IsFalse(model.IsLoadingProfiles);
        Assert.IsNull(model.ProfilesError);
    }

    [TestMethod]
    public async Task OnePlannedProfile_IsLabeledHonestly()
    {
        var (_, profiles, model) = Create(new[] { DiscoveredApps.Make("Slack") });
        var appId = AppIdentity.Create(DiscoverySource.CurrentUserUninstall, "Slack", "Publisher");
        profiles.Store.Add(CloneProfile.Create(appId, "Slack (Work)", "W", "blue"));
        await model.LoadAsync(TestContext.CancellationToken);
        model.SelectedApp = model.VisibleAppGroups.SelectMany(g => g.Items).Single();

        var shown = model.ProfilesForSelectedApp.Single();
        Assert.AreEqual(CloneProfileState.Planned, shown.State);
        Assert.AreNotEqual(CloneProfileState.Ready, shown.State);
        Assert.AreEqual("Planned", MainViewModel.ProfileStateText(shown.State));
        Assert.AreEqual("Planned — metadata only, not a working clone.",
            MainViewModel.ProfileSubtitleText(shown));
    }

    [TestMethod]
    public async Task MultipleProfiles_AllListedForTheirApp()
    {
        var (_, profiles, model) = Create(new[]
        {
            DiscoveredApps.Make("Slack"), DiscoveredApps.Make("Signal"),
        });
        var slackId = AppIdentity.Create(DiscoverySource.CurrentUserUninstall, "Slack", "Publisher");
        var signalId = AppIdentity.Create(DiscoverySource.CurrentUserUninstall, "Signal", "Publisher");
        profiles.Store.Add(CloneProfile.Create(slackId, "Slack (Work)"));
        profiles.Store.Add(CloneProfile.Create(slackId, "Slack (Home)"));
        profiles.Store.Add(CloneProfile.Create(signalId, "Signal (Work)"));
        await model.LoadAsync(TestContext.CancellationToken);

        model.SelectedApp = model.VisibleAppGroups.SelectMany(g => g.Items)
            .First(i => i.App.DisplayName == "Slack");
        Assert.AreEqual(2, model.ProfilesForSelectedApp.Count);

        model.SelectedApp = model.VisibleAppGroups.SelectMany(g => g.Items)
            .First(i => i.App.DisplayName == "Signal");
        Assert.AreEqual(1, model.ProfilesForSelectedApp.Count);
    }

    [TestMethod]
    public async Task OrphanedProfile_IsSurfacedAndPreserved()
    {
        var (_, profiles, model) = Create(new[] { DiscoveredApps.Make("Slack") });
        var orphan = CloneProfile.Create("app:gone", "Ghost (Work)");
        profiles.Store.Add(orphan);
        await model.LoadAsync(TestContext.CancellationToken);

        Assert.IsTrue(model.HasOrphans);
        Assert.AreEqual(orphan.Id, model.OrphanProfiles.Single().Profile.Id);

        model.SelectedOrphan = model.OrphanProfiles.Single();
        Assert.IsTrue(model.ShowOrphanDetail);
        Assert.IsTrue(model.HasSelection);

        Assert.AreEqual(1, profiles.Store.Count, "Orphans are surfaced, never deleted.");
    }

    [TestMethod]
    public async Task ProfilesLoading_StateIsVisible()
    {
        var (_, profiles, model) = Create(new[] { DiscoveredApps.Make("Slack") });
        var gate = new TaskCompletionSource<IReadOnlyList<CloneProfile>>();
        profiles.Gate = gate;

        var load = model.LoadAsync();
        Assert.IsTrue(model.IsLoadingProfiles);
        gate.SetResult(Array.Empty<CloneProfile>());
        await load;

        Assert.IsFalse(model.IsLoadingProfiles);
    }

    [TestMethod]
    public async Task ProfilesError_RetryRecovers()
    {
        var (_, profiles, model) = Create(new[] { DiscoveredApps.Make("Slack") });
        profiles.ListFailure = new UnauthorizedAccessException("denied");
        await model.LoadAsync(TestContext.CancellationToken);

        Assert.IsTrue(model.ShowProfilesError);
        Assert.IsNotNull(model.ProfilesError);

        profiles.ListFailure = null;
        model.RefreshAppsCommand.Execute(null);
        await Task.Delay(500, TestContext.CancellationToken);

        Assert.IsNull(model.ProfilesError);
    }

    [TestMethod]
    public async Task Selection_PreservedAcrossReload()
    {
        var (discovery, _, model) = Create(new[]
        {
            DiscoveredApps.Make("Slack"), DiscoveredApps.Make("Signal"),
        });
        await model.LoadAsync(TestContext.CancellationToken);
        model.SelectedApp = model.VisibleAppGroups.SelectMany(g => g.Items)
            .First(i => i.App.DisplayName == "Signal");

        await model.LoadAsync(TestContext.CancellationToken);

        Assert.AreEqual("Signal", model.SelectedApp?.App.DisplayName);
        Assert.IsTrue(model.ShowAppDetail);
    }

    [TestMethod]
    public async Task PreviewMode_HasNoProductionProfiles()
    {
        var (_, profiles, model) = Create(new[] { DiscoveredApps.Make("Slack") });
        var appId = AppIdentity.Create(DiscoverySource.CurrentUserUninstall, "Slack", "Publisher");
        profiles.Store.Add(CloneProfile.Create(appId, "Slack (Work)"));
        await model.LoadAsync(TestContext.CancellationToken);

        model.ShowPreviewCommand.Execute(null);

        Assert.IsTrue(model.IsPreview);
        Assert.IsFalse(model.HasOrphans);
        Assert.IsNull(model.SelectedOrphan);
        Assert.IsFalse(model.ShowOrphanDetail);
    }

    [TestMethod]
    public void ProfileTexts_AreHonest()
    {
        Assert.AreEqual("Ready", MainViewModel.ProfileStateText(CloneProfileState.Ready));
        Assert.AreEqual("Unavailable", MainViewModel.ProfileStateText(CloneProfileState.Unavailable));
        var planned = CloneProfile.Create("a", "App (X)");
        Assert.IsTrue(MainViewModel.ProfileSubtitleText(planned).Contains("metadata only"));
        Assert.IsTrue(MainViewModel.ProfileCreatedText(planned).StartsWith("Created "));
    }

    public TestContext TestContext { get; set; } = null!;
}
