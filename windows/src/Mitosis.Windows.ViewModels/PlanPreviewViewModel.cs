using Mitosis.CloneEngine.Provisioning;
using Mitosis.Domain;

namespace Mitosis.Windows.ViewModels;

/// <summary>One planned step, formatted for display. Paths stay relative so the preview
/// never implies an absolute location that execution would use.</summary>
public sealed record PlanPreviewStep(int Number, string Description, string Destination);

/// <summary>Read-only preview of the operation plan a profile would produce.
///
/// Planning is a pure function: <see cref="OperationPlanner"/> touches no filesystem,
/// registry, network, or process, and this view model only formats its result. It never
/// creates the instance directory, never reads application files, never launches anything,
/// and never mutates the profile record. The banner is part of the model, not the view, so
/// every surface that shows a plan carries the same "nothing has been executed" statement.
///
/// With an empty <see cref="ProductionCatalog"/> the planner refuses every request, so the
/// production preview explains the refusal instead of drawing a fictional plan. The step
/// list renders as soon as a strategy is registered and its evidence is supplied by the
/// caller — the same path the Stage 28 executor will use.</summary>
public sealed class PlanPreviewViewModel
{
    public const string PreviewBanner = "Preview only — nothing has been executed.";

    public PlanPreviewViewModel(
        DiscoveredApp app,
        CloneProfile profile,
        CompatibilityAssessment assessment,
        IReadOnlyList<IProvisioningStrategy>? strategies,
        ProvisioningContext context)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(assessment);
        ArgumentNullException.ThrowIfNull(context);

        App = app;
        Profile = profile;
        Assessment = assessment;

        Strategy = SelectStrategy(app, assessment, strategies);

        // The preview answers "what would the installed strategy plan right now?", so the
        // required version is the selected strategy's own version when there is one.
        var prepared = Strategy is null
            ? context
            : context with { RequiredStrategyVersion = Strategy.Version };

        Plan = OperationPlanner.Plan(app, profile, assessment, Strategy, prepared);

        Steps = Plan.Operations
            .Select((operation, index) => new PlanPreviewStep(
                index + 1,
                operation.Description,
                operation.DestinationRelativePath))
            .ToList();
    }

    /// <summary>Production preview: the real catalog (empty by design) and the user's
    /// application data root, with no evidence claimed and no prerequisite marked met.</summary>
    public static PlanPreviewViewModel Create(
        DiscoveredApp app,
        CloneProfile profile,
        CompatibilityAssessment assessment,
        string? instanceRoot = null)
        => new(
            app,
            profile,
            assessment,
            ProductionCatalog.Strategies,
            new ProvisioningContext(
                InstanceRoot: instanceRoot ?? DefaultInstanceRoot(),
                SatisfiedPrerequisites: Array.Empty<string>(),
                Evidence: IsolationEvidence.None,
                ExistingRelativeDestinations: Array.Empty<string>(),
                RequiredStrategyVersion: "1",
                Timestamp: DateTimeOffset.UtcNow));

    public DiscoveredApp App { get; }

    public CloneProfile Profile { get; }

    public CompatibilityAssessment Assessment { get; }

    public IProvisioningStrategy? Strategy { get; }

    public OperationPlan Plan { get; }

    public string Banner => PreviewBanner;

    public string Heading => $"{App.DisplayName} — {Profile.Name}";

    public string Summary => Plan.CanExecute
        ? "The plan is complete. Raising it in a future stage would create the private instance described below."
        : "This plan is refused. No part of it can run in this build.";

    public IReadOnlyList<string> BlockingIssues => Plan.Failures;

    public IReadOnlyList<string> NotProven => Plan.Warnings;

    public IReadOnlyList<PlanPreviewStep> Steps { get; }

    public bool HasBlockingIssues => Plan.Failures.Count > 0;

    public bool HasNotProven => Plan.Warnings.Count > 0;

    public bool HasSteps => Steps.Count > 0;

    /// <summary>Where an execution would place the instance. Display only: nothing is
    /// created, and the path is not tested for existence.</summary>
    public string TargetPath => Path.Combine(Plan.InstanceRoot, Plan.InstanceRelativePath);

    public string EmptyStepsText =>
        "No steps can be planned. A provisioning strategy must be registered and its evidence supplied first.";

    /// <summary>Picks the strategy whose assessment names it, falling back to any strategy
    /// that applies to the application. Selection is metadata-only; applying is a pure
    /// predicate on the discovery record.</summary>
    private static IProvisioningStrategy? SelectStrategy(
        DiscoveredApp app,
        CompatibilityAssessment assessment,
        IReadOnlyList<IProvisioningStrategy>? strategies)
    {
        if (strategies is null || strategies.Count == 0)
        {
            return null;
        }

        var named = strategies.FirstOrDefault(s =>
            string.Equals(s.Id, assessment.StrategyId, StringComparison.Ordinal) && s.AppliesTo(app));
        return named ?? strategies.FirstOrDefault(s => s.AppliesTo(app));
    }

    /// <summary>The same root the instance executor uses: the application-owned data
    /// directory. The planner appends "instances/{profileId}" to it.</summary>
    private static string DefaultInstanceRoot() => Path.GetFullPath(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Mitosis"));
}
