using Mitosis.CloneEngine.Provisioning;
using Mitosis.Domain;

namespace Mitosis.CloneEngine.Tests;

[TestClass]
public sealed class StrategyResearchTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ProductionCatalog_RemainsEmpty()
    {
        Assert.AreEqual(0, ProductionCatalog.Strategies.Count);
    }

    [TestMethod]
    public void ResearchCandidates_AreCompleteRecords()
    {
        Assert.IsTrue(ResearchCatalog.Candidates.Count >= 4);

        foreach (var candidate in ResearchCatalog.Candidates)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(candidate.Id));
            Assert.IsFalse(string.IsNullOrWhiteSpace(candidate.Category));
            Assert.IsFalse(string.IsNullOrWhiteSpace(candidate.IsolationModel));
            Assert.IsNotNull(candidate.Evidence);
            Assert.IsNotNull(candidate.Sources);
            Assert.IsNotNull(candidate.Prerequisites);
            Assert.IsNotNull(candidate.OutstandingQuestions);
        }
    }

    [TestMethod]
    public void ResearchCandidates_CannotBecomeProductionStrategies()
    {
        var candidateIds = ResearchCatalog.Candidates.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);

        Assert.AreEqual(0, ProductionCatalog.Strategies.Count(s => candidateIds.Contains(s.Id)),
            "Research records are a different type and can never enter the planner.");
    }

    [TestMethod]
    public void EligibleCandidate_DoesNotImplyExecution()
    {
        var eligible = ResearchCatalog.Candidates
            .Where(c => c.Disposition == CandidateDisposition.EligibleForImplementation)
            .ToList();

        Assert.AreEqual(1, eligible.Count, "Exactly one candidate earned review eligibility.");
        Assert.AreEqual("documented-profile-flags", eligible[0].Id);
        Assert.IsFalse(eligible[0].Evidence.IsComplete,
            "Eligibility means 'worth reviewing', not 'proven'.");
        Assert.IsTrue(eligible[0].OutstandingQuestions.Count > 0);
        Assert.AreEqual(0, ProductionCatalog.Strategies.Count,
            "Eligibility never installs an executing strategy.");
    }

    [TestMethod]
    public void RejectedCandidates_NameTheirBlocker()
    {
        var rejected = ResearchCatalog.Candidates
            .Where(c => c.Disposition == CandidateDisposition.Rejected)
            .ToList();

        Assert.IsTrue(rejected.Count >= 3);
        Assert.IsTrue(rejected.Any(c => c.Id == "store-msix-copy"),
            "Protected packages are rejected with vendor evidence.");
        Assert.IsTrue(rejected.Any(c => c.Id == "generic-registry-copy"),
            "Copying arbitrary installs without evidence is rejected.");
    }

    [TestMethod]
    public void MissingEvidence_RemainsUnknown()
    {
        Assert.IsFalse(IsolationEvidence.None.IsComplete);
        Assert.AreEqual(8, IsolationEvidence.None.Missing().Count);
    }

    [TestMethod]
    public void DisabledStrategy_RefusedAtPlanAndGate()
    {
        var app = PlanningFixtures.App();
        var profile = PlanningFixtures.Profile(app.Id);
        var strategy = new FakeStrategy { IsEnabled = false };

        var plan = OperationPlanner.Plan(app, profile,
            PlanningFixtures.SupportedAssessment(), strategy, PlanningFixtures.Context());
        Assert.IsFalse(plan.CanExecute);

        var executor = new FakeExecutor();
        var result = ProvisioningGate.ExecuteAsync(
            plan, strategy, PlanningFixtures.Context(), executor).GetAwaiter().GetResult();

        Assert.IsFalse(result.Admitted);
        Assert.AreEqual(0, executor.Calls);
    }

    [TestMethod]
    public void CatalogedEvidence_DoesNotMutateInputs()
    {
        var app = PlanningFixtures.App();
        var profile = PlanningFixtures.Profile(app.Id);
        var before = (app, profile);

        OperationPlanner.Plan(app, profile,
            PlanningFixtures.SupportedAssessment(), null, PlanningFixtures.Context());

        Assert.AreEqual(before.app, app);
        Assert.AreEqual(before.profile, profile);
    }
}
