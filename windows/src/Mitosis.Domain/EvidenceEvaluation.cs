namespace Mitosis.Domain;

/// <summary>Per-dimension evaluation outcome. The input status is preserved verbatim:
/// Unknown stays unknown, Failed stays failed.</summary>
public sealed record DimensionResult(
    IsolationDimension Dimension,
    EvidenceStatus Status,
    bool HasMethod,
    bool HasDiscrepancy);

/// <summary>Technical evaluation of one bundle. <see cref="AwaitingApproval"/> is true
/// unless the separate approval record says <see cref="ApprovalStatus.Approved"/> —
/// technical completeness never implies legal clearance or owner approval.</summary>
public sealed record EvidenceEvaluation(
    string CandidateId,
    Guid RecordId,
    IReadOnlyList<DimensionResult> Dimensions,
    int VerifiedCount,
    int FailedCount,
    int UnknownCount,
    int NotVerifiedCount,
    IReadOnlyList<string> MissingEvidence,
    IReadOnlyList<string> UnresolvedQuestions,
    bool Complete,
    bool HasFailures,
    ApprovalInfo Approval,
    bool AwaitingApproval);

/// <summary>Pure, deterministic evidence validation and evaluation. Takes bundles only:
/// candidate research dispositions cannot influence the outcome, so eligibility can
/// never upgrade a dimension. Never executes, launches, or probes anything.</summary>
public static class EvidenceEvaluator
{
    public const int CurrentSchemaVersion = 1;

    public static readonly IReadOnlyList<IsolationDimension> RequiredDimensions =
        Enum.GetValues<IsolationDimension>();

    /// <summary>Structural validation. Returns error strings; empty means structurally valid.
    /// An empty bundle (no dimensions) is invalid, never a success.</summary>
    public static IReadOnlyList<string> Validate(EvidenceBundle bundle)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(bundle.CandidateId))
        {
            errors.Add("Candidate ID is required.");
        }

        if (string.IsNullOrWhiteSpace(bundle.CandidateVersion))
        {
            errors.Add("Candidate version is required.");
        }

        if (bundle.RecordId == Guid.Empty)
        {
            errors.Add("Record ID is required.");
        }

        if (bundle.SchemaVersion != CurrentSchemaVersion)
        {
            errors.Add($"Schema version {bundle.SchemaVersion} is not supported (expected {CurrentSchemaVersion}).");
        }

        if (bundle.VerifiedAtUtc == default || bundle.VerifiedAtUtc.Offset != TimeSpan.Zero)
        {
            errors.Add("Verification date must be a UTC timestamp.");
        }

        if (string.IsNullOrWhiteSpace(bundle.Verifier) || bundle.Verifier.Trim().Length > 80
            || bundle.Verifier.IndexOfAny(new[] { '@', '\\', '/' }) >= 0)
        {
            errors.Add("Verifier must be a short non-sensitive label (no emails or paths).");
        }

        var dimensions = bundle.Dimensions ?? new List<DimensionEvidence>();
        if (dimensions.Count == 0)
        {
            errors.Add("At least the required isolation dimensions must be recorded.");
        }

        var seen = new HashSet<IsolationDimension>();
        foreach (var dimension in dimensions)
        {
            if (!Enum.IsDefined(dimension.Dimension))
            {
                errors.Add("A dimension has an invalid value.");
            }
            else if (!seen.Add(dimension.Dimension))
            {
                errors.Add($"Dimension {dimension.Dimension} is recorded twice.");
            }

            if (!Enum.IsDefined(dimension.Status))
            {
                errors.Add($"Dimension {dimension.Dimension} has an invalid status.");
            }
        }

        foreach (var required in RequiredDimensions)
        {
            if (!seen.Contains(required))
            {
                errors.Add($"Required dimension is missing: {required}.");
            }
        }

        var sources = bundle.Sources ?? new List<EvidenceSourceRef>();
        if (sources.Count == 0)
        {
            errors.Add("At least one source reference is required.");
        }

        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source.Title))
            {
                errors.Add("A source is missing its title.");
            }

            if (string.IsNullOrWhiteSpace(source.Accessed)
                || !DateOnly.TryParse(source.Accessed.Trim(), out _))
            {
                errors.Add($"Source '{source.Title}' has an invalid access date.");
            }

            if (!string.IsNullOrWhiteSpace(source.Url)
                && (!Uri.TryCreate(source.Url.Trim(), UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            {
                errors.Add($"Source '{source.Title}' has a malformed URL.");
            }
        }

        if (string.IsNullOrWhiteSpace(bundle.TestMethod))
        {
            errors.Add("Test method is required.");
        }

        if (string.IsNullOrWhiteSpace(bundle.ReproSteps))
        {
            errors.Add("Reproducible steps are required.");
        }

        if (bundle.Approval == null)
        {
            errors.Add("Approval record is required (may be NotRequested).");
        }

        return errors;
    }

    /// <summary>Evaluates a structurally valid bundle. Unknown and unverified dimensions
    /// pass through untouched; Failed dimensions fail the bundle without promotion.</summary>
    public static EvidenceEvaluation Evaluate(EvidenceBundle bundle)
    {
        var results = bundle.Dimensions.Select(d => new DimensionResult(
            d.Dimension,
            d.Status,
            !string.IsNullOrWhiteSpace(d.Method),
            !string.IsNullOrWhiteSpace(d.Discrepancy))).ToList();

        var verified = results.Count(r => r.Status == EvidenceStatus.Verified);
        var failed = results.Count(r => r.Status == EvidenceStatus.Failed);
        var unknown = results.Count(r => r.Status == EvidenceStatus.Unknown);
        var notVerified = results.Count(r => r.Status == EvidenceStatus.NotVerified);

        var missing = results
            .Where(r => r.Status is EvidenceStatus.Unknown or EvidenceStatus.NotVerified)
            .Select(r => r.Dimension.ToString())
            .ToList();

        var hasFailures = failed > 0;
        var complete = !hasFailures && unknown == 0 && notVerified == 0;

        return new EvidenceEvaluation(
            bundle.CandidateId,
            bundle.RecordId,
            results,
            verified,
            failed,
            unknown,
            notVerified,
            missing,
            (bundle.OutstandingQuestions ?? new List<string>()).ToList(),
            complete,
            hasFailures,
            bundle.Approval,
            bundle.Approval.Status != ApprovalStatus.Approved);
    }

    /// <summary>Counts-only summary with no free text, safe for UI display and logs.</summary>
    public static EvidenceSummary Summarize(EvidenceBundle bundle)
    {
        var recorded = bundle.Dimensions?.Count ?? 0;
        var verified = bundle.Dimensions?.Count(d => d.Status == EvidenceStatus.Verified) ?? 0;
        var failed = bundle.Dimensions?.Count(d => d.Status == EvidenceStatus.Failed) ?? 0;
        var missing = RequiredDimensions.Count
            - (bundle.Dimensions?.Select(d => d.Dimension).Distinct().Count() ?? 0);
        return new EvidenceSummary(
            RequiredDimensions.Count,
            recorded,
            verified,
            failed,
            Math.Max(0, missing),
            recorded > 0 && missing == 0 && failed == 0
                && (bundle.Dimensions?.All(d => d.Status is EvidenceStatus.Verified or EvidenceStatus.NotApplicable) ?? false),
            failed > 0);
    }
}
