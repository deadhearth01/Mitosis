namespace Mitosis.Domain;

/// <summary>Compatibility verdict for future independent-instance support. A status
/// describes the evidence available — never mere installability.</summary>
public enum CompatibilityStatus
{
    /// <summary>No validated strategy and no concrete blocker known.</summary>
    Unknown,

    /// <summary>A candidate strategy exists but needs validation or user review.</summary>
    NeedsReview,

    /// <summary>A concrete technical limitation prevents independent copies.</summary>
    Unsupported,

    /// <summary>A documented strategy with all prerequisites established.</summary>
    Supported,
}

/// <summary>Deterministic assessment for one application and one strategy catalog.
/// <see cref="AssessedAt"/> is caller-supplied so identical inputs give identical results.</summary>
public sealed record CompatibilityAssessment(
    CompatibilityStatus Status,
    IReadOnlyList<string> Reasons,
    string? StrategyId,
    IReadOnlyList<string> MissingPrerequisites,
    DateTimeOffset AssessedAt);

/// <summary>A validated independent-instance strategy candidate. Empty in production
/// during Stage 3: macOS launch profiles are not Windows evidence, so no generic
/// entry may claim <see cref="CompatibilityStatus.Supported"/>.</summary>
public sealed record CompatibilityStrategy(
    string Id,
    string Description,
    string? NameContains,
    InstallScope? RequiresScope);
