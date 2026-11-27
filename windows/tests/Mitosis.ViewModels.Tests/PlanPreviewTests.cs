using Mitosis.CloneEngine.Provisioning;
using Mitosis.Domain;
using Mitosis.Windows.ViewModels;

namespace Mitosis.ViewModels.Tests;

internal sealed class PreviewStrategy : IProvisioningStrategy
{
    public string Id { get; set; } = "preview";

    public string Version { get; set; } = "1";

    public bool SupportsExecution { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

    public List<string> Prerequisites { get; } = new();

    IReadOnlyList<string> IProvisioningStrategy.RequiredPrerequisites => Prerequisites;

    public Func<DiscoveredApp, bool> Applies { get; set; } = _ => true;

    bool IProvisioningStrategy.AppliesTo(DiscoveredApp app) => Applies(app);
}

/// <summary>The plan dry run must show the planner's honest output without performing any
/// part of it. These tests pin the refusal path that production runs today, the rendering
/// path a registered strategy will use, and the guarantee that nothing is created.</summary>
[TestClass]
public sealed class PlanPreviewTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private string root = string.Empty;

    [TestInitialize]
    public void CreateIsolatedRoot()
    {
        root = Path.Combine(Path.GetTempPath(), "mitosis-plan-preview", Guid.NewGuid().ToString("N"));
    }

    [TestCleanup]
    public void RemoveIsolatedRoot()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private ProvisioningContext Context(IEnumerable<string>? satisfied = null, IsolationEvidence? evidence = null) =>
        new(
            InstanceRoot: Path.GetFullPath(root),
            SatisfiedPrerequisites: (satisfied ?? Array.Empty<string>()).ToList(),
            Evidence: evidence ?? IsolationEvidence.None,
            ExistingRelativeDestinations: Array.Empty<string>(),
            RequiredStrategyVersion: "1",
            Timestamp: FixedTime);

    private static DiscoveredApp App() => new(
        Id: AppIdentity.Create(DiscoverySource.CurrentUserUninstall, "Brave", "Brave Software"),
        DisplayName: "Brave",
        Publisher: "Brave Software",
        Version: "1.0",
        InstallLocation: @"C:\Program Files\BraveSoftware\Brave-Browser\Application",
        ExecutablePath: null,
        PrimarySource: DiscoverySource.CurrentUserUninstall,
        Sources: new[] { DiscoverySource.CurrentUserUninstall },
        Scope: InstallScope.CurrentUser,
        IsSystemComponent: false,
        IconReference: null,
        Warnings: Array.Empty<string>());

    private static CompatibilityAssessment Unsupported() => new(
        CompatibilityStatus.Unknown,
        new[] { "No validated independent-instance strategy for this application yet." },
        null,
        new[] { "Validated per-application strategy" },
        FixedTime);

    private static CompatibilityAssessment Supported(string strategyId) => new(
        CompatibilityStatus.Supported,
        new[] { "Validated." },
        strategyId,
        Array.Empty<string>(),
        FixedTime);

    private static IsolationEvidence Complete() => new(
        InstallFilesReadable: true,
        CopyPermitted: true,
        LaunchFromPrivateLocation: true,
        WritableDataIsolated: true,
        RegistryStateIsolated: true,
        UpdatesPreserveInstance: true,
        RuntimesAvailable: true,
        LicensingPermits: true);

    [TestMethod]
    public void ProductionPreview_IsRefusedAndStillCarriesTheBanner()
    {
        var app = App();
        var profile = CloneProfile.Create(app.Id, "Brave (Work)");

        var preview = PlanPreviewViewModel.Create(app, profile, Unsupported(), root);

        Assert.AreEqual(PlanPreviewViewModel.PreviewBanner, preview.Banner);
        Assert.IsFalse(preview.Plan.CanExecute);
        Assert.IsFalse(preview.HasSteps, "No strategy means no planned steps.");
        Assert.IsTrue(preview.HasBlockingIssues);
        Assert.IsTrue(preview.BlockingIssues.Any(issue =>
            issue.Contains("No provisioning strategy", StringComparison.Ordinal)));
        Assert.IsNull(preview.Strategy);
        Assert.AreEqual("Brave — Brave (Work)", preview.Heading);
        Assert.IsFalse(Directory.Exists(root), "Previewing must not create the instance root.");
    }

    [TestMethod]
    public void PlanningOnlyStrategy_NeverListsStepsAndSaysWhy()
    {
        var app = App();
        var profile = CloneProfile.Create(app.Id, "Brave (Work)");
        var strategy = new PreviewStrategy { SupportsExecution = false };
        strategy.Prerequisites.Add("Vendor documents the flag");

        var preview = new PlanPreviewViewModel(
            app, profile, Supported(strategy.Id), new[] { strategy },
            Context(satisfied: strategy.Prerequisites, evidence: Complete()));

        Assert.IsFalse(preview.Plan.CanExecute);
        Assert.IsFalse(preview.HasSteps);
        Assert.IsTrue(preview.BlockingIssues.Any(issue =>
            issue.Contains("supports planning only", StringComparison.Ordinal)));
        Assert.IsFalse(Directory.Exists(root));
    }

    [TestMethod]
    public void ExecutableStrategy_ListsEveryStepWithoutCreatingAnything()
    {
        var app = App();
        var profile = CloneProfile.Create(app.Id, "Brave (Work)");
        var strategy = new PreviewStrategy();

        var preview = new PlanPreviewViewModel(
            app, profile, Supported(strategy.Id), new[] { strategy },
            Context(evidence: Complete()));

        Assert.IsTrue(preview.Plan.CanExecute, string.Join("; ", preview.BlockingIssues));
        Assert.AreEqual(4, preview.Steps.Count);
        Assert.AreEqual(1, preview.Steps[0].Number);
        Assert.AreEqual("Create the private instance directory.", preview.Steps[0].Description);
        Assert.AreEqual(Path.Combine("instances", profile.Id.ToString("N")), preview.Steps[0].Destination);
        Assert.AreEqual(2, preview.Plan.ExpectedOutputs.Count);
        Assert.AreEqual(
            Path.Combine(Path.GetFullPath(root), "instances", profile.Id.ToString("N")),
            preview.TargetPath);
        Assert.IsFalse(preview.HasBlockingIssues);
        Assert.IsFalse(Directory.Exists(root), "A dry run must not create the instance root.");
        Assert.IsFalse(Directory.Exists(preview.TargetPath), "A dry run must not create the instance directory.");
    }

    [TestMethod]
    public void MissingEvidence_IsReportedAsNotProvenRatherThanHidden()
    {
        var app = App();
        var profile = CloneProfile.Create(app.Id, "Brave (Work)");
        var strategy = new PreviewStrategy();

        var preview = new PlanPreviewViewModel(
            app, profile, Supported(strategy.Id), new[] { strategy }, Context());

        Assert.IsFalse(preview.Plan.CanExecute);
        Assert.IsTrue(preview.HasNotProven);
        Assert.IsTrue(preview.NotProven.Any(warning =>
            warning.Contains("Unproven", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Preview_LeavesTheProfileRecordUntouched()
    {
        var app = App();
        var profile = CloneProfile.Create(app.Id, "Brave (Work)");
        var before = profile;

        _ = PlanPreviewViewModel.Create(app, profile, Unsupported(), root);

        Assert.AreEqual(CloneProfileState.Planned, profile.State);
        Assert.AreEqual(before, profile);
    }

    [TestMethod]
    public void MismatchedProfile_IsRefusedInsteadOfGuessedAt()
    {
        var app = App();
        var profile = CloneProfile.Create(AppIdentity.Create(
            DiscoverySource.CurrentUserUninstall, "Other", "Publisher"), "Other");
        var strategy = new PreviewStrategy();

        var preview = new PlanPreviewViewModel(
            app, profile, Supported(strategy.Id), new[] { strategy },
            Context(evidence: Complete()));

        Assert.IsFalse(preview.Plan.CanExecute);
        Assert.IsTrue(preview.BlockingIssues.Any(issue =>
            issue.Contains("different application", StringComparison.Ordinal)));
    }
}
