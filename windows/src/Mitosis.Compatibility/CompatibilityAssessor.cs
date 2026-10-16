using Mitosis.Application.Discovery;
using Mitosis.Domain;

namespace Mitosis.Compatibility;

/// <summary>Deterministic rule-based assessor over an explicit strategy catalog.
/// Production catalog is empty in Stage 3, so generic applications assess as
/// <see cref="CompatibilityStatus.Unknown"/> and system components as
/// <see cref="CompatibilityStatus.Unsupported"/>. Pure: no I/O, no launching.</summary>
public sealed class CompatibilityAssessor : ICompatibilityAssessor
{
    private readonly IReadOnlyList<CompatibilityStrategy> strategies;

    public CompatibilityAssessor(IEnumerable<CompatibilityStrategy> strategies)
    {
        this.strategies = strategies.ToList();
    }

    public CompatibilityAssessment Assess(DiscoveredApp app, DateTimeOffset? assessedAt = null)
    {
        var when = assessedAt ?? DateTimeOffset.UtcNow;

        foreach (var strategy in strategies)
        {
            if (!Matches(strategy, app))
            {
                continue;
            }

            var missing = MissingPrerequisites(strategy, app);
            if (missing.Count == 0)
            {
                return new CompatibilityAssessment(
                    CompatibilityStatus.Supported,
                    new[] { $"Validated strategy '{strategy.Id}': {strategy.Description}" },
                    strategy.Id,
                    Array.Empty<string>(),
                    when);
            }

            return new CompatibilityAssessment(
                CompatibilityStatus.NeedsReview,
                new[] { $"Candidate strategy '{strategy.Id}' needs validation: {strategy.Description}" },
                strategy.Id,
                missing,
                when);
        }

        if (app.IsSystemComponent)
        {
            return new CompatibilityAssessment(
                CompatibilityStatus.Unsupported,
                new[] { "This component is part of Windows and can't be given independent copies." },
                null,
                Array.Empty<string>(),
                when);
        }

        return new CompatibilityAssessment(
            CompatibilityStatus.Unknown,
            new[] { "No validated independent-instance strategy for this application yet." },
            null,
            new[] { "Validated per-application strategy" },
            when);
    }

    private static bool Matches(CompatibilityStrategy strategy, DiscoveredApp app)
    {
        if (strategy.NameContains != null
            && !app.DisplayName.Contains(strategy.NameContains, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static IReadOnlyList<string> MissingPrerequisites(CompatibilityStrategy strategy, DiscoveredApp app)
    {
        if (strategy.RequiresScope != null)
        {
            if (app.Scope == InstallScope.Unknown)
            {
                return new[] { "Confirm installation scope" };
            }

            if (app.Scope != strategy.RequiresScope)
            {
                return new[] { $"Requires {strategy.RequiresScope} installation scope" };
            }
        }

        return Array.Empty<string>();
    }
}
