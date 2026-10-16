using Mitosis.Domain;

namespace Mitosis.CloneEngine.Provisioning;

/// <summary>Provisioning strategy contract. Assessment (can this app ever work?) stays in
/// <c>Mitosis.Compatibility</c>; a strategy answers the narrower question of how one
/// instance would be built and whether this build may execute anything yet.
/// Strategies default to unavailable: <see cref="SupportsExecution"/> is false unless a
/// validated implementation explicitly opts in.</summary>
public interface IProvisioningStrategy
{
    string Id { get; }

    string Version { get; }

    /// <summary>False means planning only — the planner will never permit execution.</summary>
    bool SupportsExecution { get; }

    bool IsEnabled { get; }

    IReadOnlyList<string> RequiredPrerequisites { get; }

    /// <summary>Deterministic applicability check over discovery metadata. No I/O, no launching.</summary>
    bool AppliesTo(DiscoveredApp app);
}

/// <summary>Structured progress for a future execution. No executor exists in Stage 6.</summary>
public sealed record ProvisionProgress(int OperationIndex, int OperationCount, string Message);

/// <summary>Outcome of an execution request. Stage 6 only ever produces <see cref="Refused"/>.</summary>
public sealed record ProvisionResult(
    bool Admitted,
    Guid PlanId,
    IReadOnlyList<string> Errors)
{
    public static ProvisionResult Refused(Guid planId, params string[] errors) =>
        new(false, planId, errors);

    public static ProvisionResult Refused(Guid planId, IReadOnlyList<string> errors) =>
        new(false, planId, errors);
}

/// <summary>Execution handle for a future stage. Takes the immutable plan plus a progress
/// sink; implementations must revalidate before touching anything.</summary>
public interface IProvisionExecutor
{
    Task<ProvisionResult> ExecuteAsync(
        OperationPlan plan,
        IProgress<ProvisionProgress>? progress,
        CancellationToken cancellationToken = default);
}
