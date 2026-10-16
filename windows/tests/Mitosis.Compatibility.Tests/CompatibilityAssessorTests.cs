using Mitosis.Application.Discovery;
using Mitosis.Domain;

namespace Mitosis.Compatibility.Tests;

[TestClass]
public sealed class CompatibilityAssessorTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static DiscoveredApp App(
        string name = "Example App",
        string? publisher = "Example",
        InstallScope scope = InstallScope.CurrentUser,
        bool system = false) => new(
            Id: AppIdentity.Create(DiscoverySource.CurrentUserUninstall, name, publisher),
            DisplayName: name,
            Publisher: publisher,
            Version: "1.0",
            InstallLocation: null,
            ExecutablePath: null,
            PrimarySource: DiscoverySource.CurrentUserUninstall,
            Sources: new[] { DiscoverySource.CurrentUserUninstall },
            Scope: scope,
            IsSystemComponent: system,
            IconReference: null,
            Warnings: Array.Empty<string>());

    [TestMethod]
    public void EmptyCatalog_YieldsUnknownWithPrerequisite()
    {
        var assessor = new CompatibilityAssessor(Array.Empty<CompatibilityStrategy>());

        var result = assessor.Assess(App(), FixedTime);

        Assert.AreEqual(CompatibilityStatus.Unknown, result.Status);
        Assert.IsNull(result.StrategyId);
        Assert.AreEqual(1, result.Reasons.Count);
        Assert.AreEqual(1, result.MissingPrerequisites.Count);
        Assert.AreEqual(FixedTime, result.AssessedAt);
    }

    [TestMethod]
    public void SystemComponent_YieldsUnsupported()
    {
        var assessor = new CompatibilityAssessor(Array.Empty<CompatibilityStrategy>());

        var result = assessor.Assess(App(system: true), FixedTime);

        Assert.AreEqual(CompatibilityStatus.Unsupported, result.Status);
        Assert.IsNull(result.StrategyId);
        Assert.AreEqual(0, result.MissingPrerequisites.Count);
    }

    [TestMethod]
    public void MatchingStrategyWithMetPrerequisites_YieldsSupported()
    {
        var assessor = new CompatibilityAssessor(new[]
        {
            new CompatibilityStrategy("per-user-profile", "Separate per-user data folder.", "Example App", InstallScope.CurrentUser),
        });

        var result = assessor.Assess(App(), FixedTime);

        Assert.AreEqual(CompatibilityStatus.Supported, result.Status);
        Assert.AreEqual("per-user-profile", result.StrategyId);
        Assert.AreEqual(0, result.MissingPrerequisites.Count);
    }

    [TestMethod]
    public void StrategyWithUnmetScope_YieldsNeedsReview()
    {
        var assessor = new CompatibilityAssessor(new[]
        {
            new CompatibilityStrategy("per-user-profile", "Separate per-user data folder.", "Example App", InstallScope.CurrentUser),
        });

        var result = assessor.Assess(App(scope: InstallScope.Machine), FixedTime);

        Assert.AreEqual(CompatibilityStatus.NeedsReview, result.Status);
        Assert.AreEqual("per-user-profile", result.StrategyId);
        CollectionAssert.Contains(result.MissingPrerequisites.ToList(), "Requires CurrentUser installation scope");
    }

    [TestMethod]
    public void StrategyWithUnknownScope_YieldsNeedsReview()
    {
        var assessor = new CompatibilityAssessor(new[]
        {
            new CompatibilityStrategy("per-user-profile", "Separate per-user data folder.", "Example App", InstallScope.CurrentUser),
        });

        var result = assessor.Assess(App(scope: InstallScope.Unknown), FixedTime);

        Assert.AreEqual(CompatibilityStatus.NeedsReview, result.Status);
        CollectionAssert.Contains(result.MissingPrerequisites.ToList(), "Confirm installation scope");
    }

    [TestMethod]
    public void NonMatchingStrategy_YieldsUnknown()
    {
        var assessor = new CompatibilityAssessor(new[]
        {
            new CompatibilityStrategy("other", "Something else.", "Unrelated", null),
        });

        var result = assessor.Assess(App(), FixedTime);

        Assert.AreEqual(CompatibilityStatus.Unknown, result.Status);
        Assert.IsNull(result.StrategyId);
    }

    [TestMethod]
    public void Assessment_IsDeterministicForIdenticalInputs()
    {
        var assessor = new CompatibilityAssessor(Array.Empty<CompatibilityStrategy>());
        var app = App();

        var first = assessor.Assess(app, FixedTime);
        var second = assessor.Assess(app, FixedTime);

        // Record equality is reference-based for the reason lists, so compare field-wise.
        Assert.AreEqual(first.Status, second.Status);
        Assert.AreEqual(first.StrategyId, second.StrategyId);
        CollectionAssert.AreEqual(first.Reasons.ToList(), second.Reasons.ToList());
        CollectionAssert.AreEqual(first.MissingPrerequisites.ToList(), second.MissingPrerequisites.ToList());
        Assert.AreEqual(first.AssessedAt, second.AssessedAt);
    }

    [TestMethod]
    public void Assessor_ImplementsApplicationPort()
    {
        Assert.IsInstanceOfType<ICompatibilityAssessor>(
            new CompatibilityAssessor(Array.Empty<CompatibilityStrategy>()));
    }
}
