using Mitosis.Domain;

namespace Mitosis.CloneEngine.Provisioning;

/// <summary>Execution boundary. Revalidates every request; a rejected plan, a missing
/// executor, or stale prerequisites all refuse safely without touching the system.
/// Profile records are never mutated here — <see cref="CloneProfileState.Ready"/> can
/// only be set by a future provisioner after genuine verified work.</summary>
public static class ProvisioningGate
{
    public static async Task<ProvisionResult> ExecuteAsync(
        OperationPlan plan,
        IProvisioningStrategy strategy,
        ProvisioningContext context,
        IProvisionExecutor? executor,
        IProgress<ProvisionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!plan.CanExecute || plan.Failures.Count > 0)
        {
            return ProvisionResult.Refused(plan.PlanId,
                new[] { "Plan was not admitted for execution." }.Concat(plan.Failures).ToList());
        }

        if (executor == null)
        {
            return ProvisionResult.Refused(plan.PlanId, "No provision executor is installed.");
        }

        if (!string.Equals(plan.StrategyId, strategy.Id, StringComparison.Ordinal)
            || !string.Equals(plan.StrategyVersion, strategy.Version, StringComparison.Ordinal))
        {
            return ProvisionResult.Refused(plan.PlanId, "Plan strategy no longer matches the installed strategy.");
        }

        if (!strategy.IsEnabled || !strategy.SupportsExecution)
        {
            return ProvisionResult.Refused(plan.PlanId, $"Strategy '{strategy.Id}' cannot execute.");
        }

        var missing = strategy.RequiredPrerequisites
            .Where(p => !context.SatisfiedPrerequisites.Contains(p, StringComparer.Ordinal))
            .ToList();
        if (missing.Count > 0)
        {
            return ProvisionResult.Refused(plan.PlanId,
                missing.Select(p => $"Prerequisite no longer satisfied: {p}.").ToList());
        }

        if (!context.Evidence.IsComplete)
        {
            return ProvisionResult.Refused(plan.PlanId, "Isolation evidence is incomplete at execution time.");
        }

        return await executor.ExecuteAsync(plan, progress, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Production strategy catalog. Empty by design in Stage 6: no Windows
/// application category has validated isolation evidence yet (see
/// docs/WINDOWS-PROVISIONING.md), so the planner refuses execution for everything.</summary>
public static class ProductionCatalog
{
    public static IReadOnlyList<IProvisioningStrategy> Strategies { get; } =
        Array.Empty<IProvisioningStrategy>();
}
