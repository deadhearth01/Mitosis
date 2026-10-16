namespace Mitosis.Domain;

/// <summary>Final research disposition for a strategy candidate. Only
/// <see cref="EligibleForImplementation"/> justifies engineering review, and even that
/// never enables production execution by itself.</summary>
public enum CandidateDisposition
{
    Rejected,
    ResearchRequired,
    NeedsReview,
    EligibleForImplementation,
}

/// <summary>How far legal/licensing review has gone. No candidate here has counsel review.</summary>
public enum LegalReviewStatus
{
    NotStarted,
    VendorDocsChecked,
    NeedsCounsel,
}

/// <summary>One consulted source. Dates are access dates (ISO); URLs are archived in the
/// research document alongside what was actually read.</summary>
public sealed record EvidenceSource(string Title, string Url, string Accessed);

/// <summary>Structured research record for one strategy candidate. Facts come from vendor
/// documentation; everything else is an outstanding question. Candidates are data, never
/// strategies: nothing here can be planned or executed.</summary>
public sealed record StrategyCandidate(
    string Id,
    string Version,
    string Category,
    string ApplicabilityLimits,
    IReadOnlyList<EvidenceSource> Sources,
    IReadOnlyList<string> Prerequisites,
    string IsolationModel,
    string RegistryImplications,
    string UpdateImplications,
    LegalReviewStatus LegalReview,
    IsolationEvidence Evidence,
    IReadOnlyList<string> OutstandingQuestions,
    CandidateDisposition Disposition);
