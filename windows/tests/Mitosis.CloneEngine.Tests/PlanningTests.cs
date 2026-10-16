using Mitosis.CloneEngine.Provisioning;
using Mitosis.Domain;

namespace Mitosis.CloneEngine.Tests;

internal sealed class FakeStrategy : IProvisioningStrategy
{
    public string Id { get; set; } = "fake";

    public string Version { get; set; } = "1.0";

    public bool SupportsExecution { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

    public List<string> Prerequisites { get; } = new();

    IReadOnlyList<string> IProvisioningStrategy.RequiredPrerequisites => Prerequisites;

    public Func<DiscoveredApp, bool> Applies { get; set; } = _ => true;

    bool IProvisioningStrategy.AppliesTo(DiscoveredApp app) => Applies(app);
}

internal sealed class FakeExecutor : IProvisionExecutor
{
    public int Calls { get; private set; }

    public List<ProvisionProgress> Reported { get; } = new();

    public Task<ProvisionResult> ExecuteAsync(
        OperationPlan plan, IProgress<ProvisionProgress>? progress, CancellationToken cancellationToken = default)
    {
        Calls++;
        progress?.Report(new ProvisionProgress(0, 1, "working"));
        progress?.Report(new ProvisionProgress(1, 1, "done"));
        return Task.FromResult(new ProvisionResult(true, plan.PlanId, Array.Empty<string>()));
    }
}

internal static class PlanningFixtures
{
    public static readonly DateTimeOffset FixedTime = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    public const string Root = @"C:\MitosisTest\InstancesRoot";

    public static DiscoveredApp App(string name = "Example", string? location = @"C:\Apps\Example") => new(
        Id: AppIdentity.Create(DiscoverySource.CurrentUserUninstall, name, "Publisher"),
        DisplayName: name,
        Publisher: "Publisher",
        Version: "1.0",
        InstallLocation: location,
        ExecutablePath: null,
        PrimarySource: DiscoverySource.CurrentUserUninstall,
        Sources: new[] { DiscoverySource.CurrentUserUninstall },
        Scope: InstallScope.CurrentUser,
        IsSystemComponent: false,
        IconReference: null,
        Warnings: Array.Empty<string>());

    public static CloneProfile Profile(string appId) =>
        CloneProfile.Create(appId, "Example (Work)");

    public static CompatibilityAssessment SupportedAssessment(string strategyId = "fake") => new(
        CompatibilityStatus.Supported,
        new[] { "Validated." },
        strategyId,
        Array.Empty<string>(),
        FixedTime);

    public static IsolationEvidence CompleteEvidence() => new(true, true, true, true, true, true, true, true);

    public static ProvisioningContext Context(
        IsolationEvidence? evidence = null,
        IEnumerable<string>? satisfied = null,
        IEnumerable<string>? existing = null,
        string root = Root,
        string version = "1.0") => new(
            InstanceRoot: root,
            SatisfiedPrerequisites: (satisfied ?? Array.Empty<string>()).ToList(),
            Evidence: evidence ?? CompleteEvidence(),
            ExistingRelativeDestinations: (existing ?? Array.Empty<string>()).ToList(),
            RequiredStrategyVersion: version,
            Timestamp: FixedTime);
}

[TestClass]
public sealed class StrategySelectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ProductionCatalog_IsEmpty()
    {
        Assert.AreEqual(0, ProductionCatalog.Strategies.Count);
    }

    [TestMethod]
    public void NullStrategy_IsRefused()
    {
        var app = PlanningFixtures.App();
        var profile = PlanningFixtures.Profile(app.Id);

        var plan = OperationPlanner.Plan(app, profile,
            PlanningFixtures.SupportedAssessment(), null, PlanningFixtures.Context());

        Assert.IsFalse(plan.CanExecute);
        Assert.AreEqual(0, plan.Operations.Count);
        Assert.IsTrue(plan.Failures.Any(f => f.Contains("No provisioning strategy")));
    }

    [TestMethod]
    public void UnknownApplicationType_IsRefused()
    {
        var strategy = new FakeStrategy { Applies = _ => false };

        var plan = OperationPlanner.Plan(PlanningFixtures.App(),
            PlanningFixtures.Profile(PlanningFixtures.App().Id),
            PlanningFixtures.SupportedAssessment(), strategy, PlanningFixtures.Context());

        Assert.IsFalse(plan.CanExecute);
        Assert.IsTrue(plan.Failures.Any(f => f.Contains("does not apply")));
    }

    [TestMethod]
    public void MissingPrerequisites_AreRefusedByName()
    {
        var strategy = new FakeStrategy();
        strategy.Prerequisites.Add("elevation-free-copy");

        var plan = OperationPlanner.Plan(PlanningFixtures.App(),
            PlanningFixtures.Profile(PlanningFixtures.App().Id),
            PlanningFixtures.SupportedAssessment(), strategy, PlanningFixtures.Context());

        Assert.IsFalse(plan.CanExecute);
        Assert.IsTrue(plan.Failures.Any(f => f.Contains("elevation-free-copy")));
    }

    [TestMethod]
    public void DisabledStrategy_IsRefused()
    {
        var strategy = new FakeStrategy { IsEnabled = false };

        var plan = OperationPlanner.Plan(PlanningFixtures.App(),
            PlanningFixtures.Profile(PlanningFixtures.App().Id),
            PlanningFixtures.SupportedAssessment(), strategy, PlanningFixtures.Context());

        Assert.IsFalse(plan.CanExecute);
        Assert.IsTrue(plan.Failures.Any(f => f.Contains("disabled")));
    }

    [TestMethod]
    public void StrategyVersionMismatch_IsRefused()
    {
        var strategy = new FakeStrategy { Version = "1.0" };

        var plan = OperationPlanner.Plan(PlanningFixtures.App(),
            PlanningFixtures.Profile(PlanningFixtures.App().Id),
            PlanningFixtures.SupportedAssessment(), strategy, PlanningFixtures.Context(version: "2.0"));

        Assert.IsFalse(plan.CanExecute);
        Assert.IsTrue(plan.Failures.Any(f => f.Contains("2.0")));
    }

    [TestMethod]
    public void InsufficientEvidence_IsRefusedWithUnprovenItems()
    {
        var plan = OperationPlanner.Plan(PlanningFixtures.App(),
            PlanningFixtures.Profile(PlanningFixtures.App().Id),
            PlanningFixtures.SupportedAssessment(), new FakeStrategy(),
            PlanningFixtures.Context(evidence: IsolationEvidence.None));

        Assert.IsFalse(plan.CanExecute);
        Assert.IsTrue(plan.Failures.Any(f => f.Contains("Isolation evidence is incomplete")));
        Assert.AreEqual(8, plan.Warnings.Count(w => w.StartsWith("Unproven:")));
    }

    [TestMethod]
    public void PlanningOnlyStrategy_NeverAdmitsExecution()
    {
        var strategy = new FakeStrategy { SupportsExecution = false };

        var plan = OperationPlanner.Plan(PlanningFixtures.App(),
            PlanningFixtures.Profile(PlanningFixtures.App().Id),
            PlanningFixtures.SupportedAssessment(), strategy, PlanningFixtures.Context());

        Assert.IsFalse(plan.CanExecute);
        Assert.IsTrue(plan.Failures.Any(f => f.Contains("planning only")));
    }

    [TestMethod]
    public void NonSupportedAssessment_IsRefused()
    {
        var assessment = new CompatibilityAssessment(CompatibilityStatus.Unknown,
            new[] { "No strategy." }, null, new[] { "Strategy" }, PlanningFixtures.FixedTime);

        var plan = OperationPlanner.Plan(PlanningFixtures.App(),
            PlanningFixtures.Profile(PlanningFixtures.App().Id),
            assessment, new FakeStrategy(), PlanningFixtures.Context());

        Assert.IsFalse(plan.CanExecute);
        Assert.IsTrue(plan.Failures.Any(f => f.Contains("does not permit provisioning")));
    }
}

[TestClass]
public sealed class OperationPlanningTests
{
    public TestContext TestContext { get; set; } = null!;

    private static (DiscoveredApp app, CloneProfile profile) Pair()
    {
        var app = PlanningFixtures.App();
        return (app, PlanningFixtures.Profile(app.Id));
    }

    private static OperationPlan Admitted()
    {
        var (app, profile) = Pair();
        return OperationPlanner.Plan(app, profile,
            PlanningFixtures.SupportedAssessment(), new FakeStrategy(), PlanningFixtures.Context());
    }

    [TestMethod]
    public void ValidPlan_HasExpectedStructure()
    {
        var (app, profile) = Pair();
        var before = profile;

        var plan = OperationPlanner.Plan(app, profile,
            PlanningFixtures.SupportedAssessment(), new FakeStrategy(), PlanningFixtures.Context());

        Assert.IsTrue(plan.CanExecute);
        Assert.AreEqual("fake", plan.StrategyId);
        Assert.AreEqual("1.0", plan.StrategyVersion);
        Assert.AreEqual(profile.Id, plan.ProfileId);
        Assert.AreEqual(app.Id, plan.AppId);
        Assert.AreEqual(PlanningFixtures.Root, plan.InstanceRoot);
        Assert.AreEqual(Path.Combine("instances", profile.Id.ToString("N")), plan.InstanceRelativePath);
        Assert.IsFalse(plan.InstanceRelativePath.Contains(profile.Name),
            "Destinations never embed profile names (traversal-proof by construction).");
        Assert.AreEqual(4, plan.Operations.Count);
        Assert.AreEqual(2, plan.ExpectedOutputs.Count);
        Assert.AreEqual(0, plan.Failures.Count);
        Assert.AreEqual(before, profile, "Planning never mutates the profile.");
        foreach (var operation in plan.Operations.Concat(
            plan.ExpectedOutputs.Select(o => new PlannedOperation(PlannedOperationKind.WriteInstanceManifest, null, o, ""))))
        {
            Assert.IsTrue(DestinationSafety.IsValidRelativePath(operation.DestinationRelativePath, out _));
        }
    }

    [TestMethod]
    public void NonPlannedProfile_IsRefused()
    {
        var app = PlanningFixtures.App();
        var ready = PlanningFixtures.Profile(app.Id)
            .TransitionTo(CloneProfileState.Provisioning)
            .TransitionTo(CloneProfileState.Ready);

        var plan = OperationPlanner.Plan(app, ready,
            PlanningFixtures.SupportedAssessment(), new FakeStrategy(), PlanningFixtures.Context());

        Assert.IsFalse(plan.CanExecute);
        Assert.IsTrue(plan.Failures.Any(f => f.Contains("Only Planned")));
    }

    [TestMethod]
    public void WrongAssociation_IsRefused()
    {
        var app = PlanningFixtures.App();
        var profile = PlanningFixtures.Profile("app:someone-else");

        var plan = OperationPlanner.Plan(app, profile,
            PlanningFixtures.SupportedAssessment(), new FakeStrategy(), PlanningFixtures.Context());

        Assert.IsFalse(plan.CanExecute);
        Assert.IsTrue(plan.Failures.Any(f => f.Contains("different application")));
    }

    [TestMethod]
    public void HostileProfileNames_CannotEscapeTheRoot()
    {
        // All cases pass CloneName validation (no slashes or colons) yet look dangerous.
        foreach (var name in new[] { "..\\..\\evil", "..", "evil.", " trailing " })
        {
            var app = PlanningFixtures.App();
            var profile = PlanningFixtures.Profile(app.Id).Rename($"App ({name})");
            var plan = OperationPlanner.Plan(app, profile,
                PlanningFixtures.SupportedAssessment(), new FakeStrategy(), PlanningFixtures.Context());

            Assert.IsFalse(plan.InstanceRelativePath.Contains(name.Trim()),
                $"Profile name leaked into destination: {name}");
            Assert.IsTrue(plan.InstanceRelativePath.StartsWith("instances" + Path.DirectorySeparatorChar));
        }
    }

    [TestMethod]
    public void DestinationSafety_RejectsEscapes()
    {
        Assert.IsFalse(DestinationSafety.IsValidRelativePath("../x", out _));
        Assert.IsFalse(DestinationSafety.IsValidRelativePath(@"a\..\..\x", out _));
        Assert.IsFalse(DestinationSafety.IsValidRelativePath(@"C:\Windows\x", out _));
        Assert.IsFalse(DestinationSafety.IsValidRelativePath("", out _));
        Assert.IsFalse(DestinationSafety.IsValidRelativePath("evil.", out _));
        Assert.IsFalse(DestinationSafety.IsValidRelativePath("trailing ", out _));
        Assert.IsFalse(DestinationSafety.IsValidRelativePath(new string('x', 201), out _));
        Assert.IsFalse(DestinationSafety.IsValidRelativePath("bad|name", out _));
        Assert.IsTrue(DestinationSafety.IsValidRelativePath(@"instances\abc123", out _));
        Assert.IsTrue(DestinationSafety.IsValidRelativePath("a/b", out _),
            "Forward slashes are valid Windows separators, not traversal.");
    }

    [TestMethod]
    public void NonAbsoluteRoot_IsRefused()
    {
        var (app, profile) = Pair();

        var plan = OperationPlanner.Plan(app, profile,
            PlanningFixtures.SupportedAssessment(), new FakeStrategy(),
            PlanningFixtures.Context(root: "instances"));

        Assert.IsFalse(plan.CanExecute);
        Assert.IsTrue(plan.Failures.Any(f => f.Contains("absolute")));
    }

    [TestMethod]
    public void NonNormalizedRoot_IsRefused()
    {
        var (app, profile) = Pair();

        var plan = OperationPlanner.Plan(app, profile,
            PlanningFixtures.SupportedAssessment(), new FakeStrategy(),
            PlanningFixtures.Context(root: @"C:\Base\..\Other"));

        Assert.IsFalse(plan.CanExecute);
    }

    [TestMethod]
    public void ExistingDestination_IsRefused()
    {
        var (app, profile) = Pair();
        var existing = Path.Combine("instances", profile.Id.ToString("N")).ToUpperInvariant();

        var plan = OperationPlanner.Plan(app, profile,
            PlanningFixtures.SupportedAssessment(), new FakeStrategy(),
            PlanningFixtures.Context(existing: new[] { existing }));

        Assert.IsFalse(plan.CanExecute);
        Assert.IsTrue(plan.Failures.Any(f => f.Contains("already exists")));
    }

    [TestMethod]
    public void Planning_CreatesNoDirectories()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mitosis-plan-" + Guid.NewGuid().ToString("N"));
        var (app, profile) = Pair();

        OperationPlanner.Plan(app, profile,
            PlanningFixtures.SupportedAssessment(), new FakeStrategy(),
            PlanningFixtures.Context(root: Path.Combine(dir, "root")));

        Assert.IsFalse(Directory.Exists(dir), "Planning performs no I/O.");
    }

    [TestMethod]
    public void DeterministicInputs_ProduceEqualPlans()
    {
        var (app, profile) = Pair();
        Func<OperationPlan> plan = () => OperationPlanner.Plan(app, profile,
            PlanningFixtures.SupportedAssessment(), new FakeStrategy(), PlanningFixtures.Context());

        var first = plan();
        var second = plan();

        // Lists compare by reference, so compare field-wise.
        Assert.AreEqual(first.StrategyId, second.StrategyId);
        Assert.AreEqual(first.StrategyVersion, second.StrategyVersion);
        Assert.AreEqual(first.ProfileId, second.ProfileId);
        Assert.AreEqual(first.AppId, second.AppId);
        Assert.AreEqual(first.InstanceRoot, second.InstanceRoot);
        Assert.AreEqual(first.InstanceRelativePath, second.InstanceRelativePath);
        CollectionAssert.AreEqual(first.Operations.ToList(), second.Operations.ToList());
        CollectionAssert.AreEqual(first.ExpectedOutputs.ToList(), second.ExpectedOutputs.ToList());
        CollectionAssert.AreEqual(first.Warnings.ToList(), second.Warnings.ToList());
        CollectionAssert.AreEqual(first.Failures.ToList(), second.Failures.ToList());
        Assert.AreEqual(first.CanExecute, second.CanExecute);
        Assert.AreEqual(first.CreatedAt, second.CreatedAt);
    }
}

[TestClass]
public sealed class ExecutionBoundaryTests
{
    public TestContext TestContext { get; set; } = null!;

    private static (OperationPlan plan, FakeStrategy strategy, ProvisioningContext context) Admitted()
    {
        var app = PlanningFixtures.App();
        var profile = PlanningFixtures.Profile(app.Id);
        var strategy = new FakeStrategy();
        var context = PlanningFixtures.Context();
        var plan = OperationPlanner.Plan(app, profile,
            PlanningFixtures.SupportedAssessment(), strategy, context);
        Assert.IsTrue(plan.CanExecute, string.Join("; ", plan.Failures));
        return (plan, strategy, context);
    }

    [TestMethod]
    public async Task RejectedPlan_NeverReachesExecutor()
    {
        var executor = new FakeExecutor();
        var app = PlanningFixtures.App();
        var profile = PlanningFixtures.Profile(app.Id);
        var refused = OperationPlanner.Plan(app, profile,
            new CompatibilityAssessment(CompatibilityStatus.Unknown, new[] { "No." }, null,
                Array.Empty<string>(), PlanningFixtures.FixedTime),
            new FakeStrategy(), PlanningFixtures.Context());

        var result = await ProvisioningGate.ExecuteAsync(
            refused, new FakeStrategy(), PlanningFixtures.Context(), executor, null, TestContext.CancellationToken);

        Assert.IsFalse(result.Admitted);
        Assert.AreEqual(0, executor.Calls);
    }

    [TestMethod]
    public async Task MissingExecutor_FailsSafely()
    {
        var (plan, strategy, context) = Admitted();

        var result = await ProvisioningGate.ExecuteAsync(
            plan, strategy, context, null, null, TestContext.CancellationToken);

        Assert.IsFalse(result.Admitted);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("No provision executor")));
    }

    [TestMethod]
    public async Task RevokedPrerequisite_BlocksExecution()
    {
        var app = PlanningFixtures.App();
        var profile = PlanningFixtures.Profile(app.Id);
        var strategy = new FakeStrategy();
        strategy.Prerequisites.Add("elevation-free-copy");
        var executor = new FakeExecutor();

        var admitted = OperationPlanner.Plan(app, profile,
            PlanningFixtures.SupportedAssessment(), strategy,
            PlanningFixtures.Context(satisfied: new[] { "elevation-free-copy" }));
        Assert.IsTrue(admitted.CanExecute);

        var stale = PlanningFixtures.Context(satisfied: new[] { "other" });
        var result = await ProvisioningGate.ExecuteAsync(
            admitted, strategy, stale, executor, null, TestContext.CancellationToken);

        Assert.IsFalse(result.Admitted);
        Assert.AreEqual(0, executor.Calls);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("elevation-free-copy")));
    }

    [TestMethod]
    public async Task StaleEvidence_BlocksExecution()
    {
        var (plan, strategy, _) = Admitted();
        var executor = new FakeExecutor();

        var result = await ProvisioningGate.ExecuteAsync(
            plan, strategy, PlanningFixtures.Context(evidence: IsolationEvidence.None),
            executor, null, TestContext.CancellationToken);

        Assert.IsFalse(result.Admitted);
        Assert.AreEqual(0, executor.Calls);
    }

    [TestMethod]
    public async Task SwappedStrategyVersion_BlocksExecution()
    {
        var (plan, _, context) = Admitted();
        var executor = new FakeExecutor();

        var result = await ProvisioningGate.ExecuteAsync(
            plan, new FakeStrategy { Version = "2.0" }, context,
            executor, null, TestContext.CancellationToken);

        Assert.IsFalse(result.Admitted);
        Assert.AreEqual(0, executor.Calls);
    }

    [TestMethod]
    public async Task DuplicatePlanningRequest_CollidesSafely()
    {
        var app = PlanningFixtures.App();
        var profile = PlanningFixtures.Profile(app.Id);
        var strategy = new FakeStrategy();
        var first = OperationPlanner.Plan(app, profile,
            PlanningFixtures.SupportedAssessment(), strategy, PlanningFixtures.Context());
        Assert.IsTrue(first.CanExecute);

        var second = OperationPlanner.Plan(app, profile,
            PlanningFixtures.SupportedAssessment(), strategy,
            PlanningFixtures.Context(existing: new[] { first.InstanceRelativePath }));

        Assert.IsFalse(second.CanExecute);
        Assert.IsTrue(second.Failures.Any(f => f.Contains("already exists")));
    }

    [TestMethod]
    public async Task ProgressSink_IsForwarded()
    {
        var (plan, strategy, context) = Admitted();
        var executor = new FakeExecutor();
        var reported = new List<ProvisionProgress>();
        var progress = new SyncProgress(reported);

        var result = await ProvisioningGate.ExecuteAsync(
            plan, strategy, context, executor, progress, TestContext.CancellationToken);

        Assert.IsTrue(result.Admitted);
        Assert.AreEqual(1, executor.Calls);
        Assert.AreEqual(2, reported.Count);
    }

    private sealed class SyncProgress : IProgress<ProvisionProgress>
    {
        private readonly List<ProvisionProgress> reported;

        public SyncProgress(List<ProvisionProgress> reported)
        {
            this.reported = reported;
        }

        public void Report(ProvisionProgress value) => reported.Add(value);
    }
}


