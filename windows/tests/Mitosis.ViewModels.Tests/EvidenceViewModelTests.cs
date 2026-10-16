using Mitosis.Application.Evidence;
using Mitosis.Domain;
using Mitosis.Windows.ViewModels;

namespace Mitosis.ViewModels.Tests;

internal sealed class FakeEvidenceStore : IEvidenceStore
{
    public List<EvidenceBundle> Bundles { get; } = new();

    public Exception? ListFailure { get; set; }

    public Task<IReadOnlyList<EvidenceBundle>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (ListFailure != null)
        {
            throw ListFailure;
        }

        return Task.FromResult<IReadOnlyList<EvidenceBundle>>(Bundles.ToList());
    }

    public Task<EvidenceBundle?> GetAsync(Guid recordId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Bundles.FirstOrDefault(b => b.RecordId == recordId));

    public Task<EvidenceBundle> SaveAsync(EvidenceBundle bundle, CancellationToken cancellationToken = default)
    {
        Bundles.Add(bundle);
        return Task.FromResult(bundle);
    }
}

[TestClass]
public sealed class EvidenceViewModelTests
{
    private static EvidenceBundle Bundle(string appId, EvidenceStatus status = EvidenceStatus.Unknown) => new(
        CandidateId: "documented-profile-flags",
        CandidateVersion: "1",
        AppId: appId,
        AppVersion: "1.0",
        RecordId: Guid.NewGuid(),
        SchemaVersion: EvidenceEvaluator.CurrentSchemaVersion,
        VerifiedAtUtc: new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero),
        Verifier: "lab-notebook-7",
        Dimensions: EvidenceEvaluator.RequiredDimensions
            .Select(d => new DimensionEvidence(d, status, "Read vendor docs", "Flag documented", "Flag documented", null))
            .ToList(),
        Sources: new[] { new EvidenceSourceRef("Vendor docs", "https://example.com/docs", "2026-10-16") },
        TestMethod: "Read the vendor documentation.",
        ReproSteps: "1. Open the URL. 2. Record the claim.",
        PlatformAssumptions: null,
        OutstandingQuestions: Array.Empty<string>(),
        Approval: new ApprovalInfo(ApprovalStatus.NotRequested, null, null),
        IntegrityHash: "hash");

    private static (FakeDiscoveryService discovery, FakeProfileRepository profiles, FakeEvidenceStore evidence, MainViewModel model)
        Create(FakeEvidenceStore? evidence = null)
    {
        var discovery = new FakeDiscoveryService();
        discovery.Returns(new[] { DiscoveredApps.Make("Slack") });
        var profiles = new FakeProfileRepository();
        var store = evidence ?? new FakeEvidenceStore();
        return (discovery, profiles, store, new MainViewModel(discovery, new FakeAssessor(), profiles, store));
    }

    private static void SelectSlack(MainViewModel model) =>
        model.SelectedApp = model.VisibleAppGroups.SelectMany(g => g.Items).Single();

    [TestMethod]
    public async Task NoBundles_ShowsNoVerificationRecorded()
    {
        var (_, _, _, model) = Create();
        await model.LoadAsync(TestContext.CancellationToken);
        SelectSlack(model);

        Assert.AreEqual("No verification recorded for this application.", model.SelectedAppEvidenceSummary);
    }

    [TestMethod]
    public async Task MatchingBundle_ShowsCountsNotFreeText()
    {
        var (_, _, evidence, model) = Create();
        await model.LoadAsync(TestContext.CancellationToken);
        var appId = model.VisibleAppGroups.SelectMany(g => g.Items).Single().App.Id;
        evidence.Bundles.Add(Bundle(appId));
        await model.LoadAsync(TestContext.CancellationToken);
        SelectSlack(model);

        Assert.AreEqual("10 of 10 dimensions recorded · 0 verified · provisioning unavailable.",
            model.SelectedAppEvidenceSummary);
    }

    [TestMethod]
    public async Task OtherAppBundle_DoesNotLeakAcrossApps()
    {
        var (_, _, evidence, model) = Create();
        evidence.Bundles.Add(Bundle("app:someone-else"));
        await model.LoadAsync(TestContext.CancellationToken);
        SelectSlack(model);

        Assert.AreEqual("No verification recorded for this application.", model.SelectedAppEvidenceSummary);
    }

    [TestMethod]
    public async Task FailingStore_FallsBackGracefully()
    {
        var evidence = new FakeEvidenceStore { ListFailure = new UnauthorizedAccessException("denied") };
        var (_, _, _, model) = Create(evidence);
        await model.LoadAsync(TestContext.CancellationToken);
        SelectSlack(model);

        Assert.AreEqual("No verification recorded for this application.", model.SelectedAppEvidenceSummary);
        Assert.IsTrue(model.HasApps, "Evidence failure never breaks discovery.");
    }

    [TestMethod]
    public async Task NoStore_WorksWithoutEvidence()
    {
        var discovery = new FakeDiscoveryService();
        discovery.Returns(new[] { DiscoveredApps.Make("Slack") });
        var model = new MainViewModel(discovery, new FakeAssessor(), new FakeProfileRepository());
        await model.LoadAsync(TestContext.CancellationToken);
        model.SelectedApp = model.VisibleAppGroups.SelectMany(g => g.Items).Single();

        Assert.AreEqual("No verification recorded for this application.", model.SelectedAppEvidenceSummary);
    }

    public TestContext TestContext { get; set; } = null!;
}
