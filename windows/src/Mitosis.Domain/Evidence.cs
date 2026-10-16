namespace Mitosis.Domain;

/// <summary>Isolation dimensions every evidence bundle must cover. Each dimension keeps its
/// own status; unknowns are never inferred from feature flags or profile arguments.</summary>
public enum IsolationDimension
{
    WritableDataDirs,
    RegistryConfig,
    MutexIpc,
    ProtocolHandlers,
    BackgroundServices,
    Updates,
    SensitiveData,
    LaunchPrerequisites,
    Recovery,
    Licensing,
}

/// <summary>Per-dimension verification status.</summary>
public enum EvidenceStatus
{
    Unknown,
    NotVerified,
    Verified,
    Failed,
    NotApplicable,
}

/// <summary>Owner approval, tracked apart from technical evidence. Technical completeness
/// never implies approval.</summary>
public enum ApprovalStatus
{
    NotRequested,
    Pending,
    Approved,
    Rejected,
}

/// <summary>One consulted source. Title and access date are mandatory; a URL, when present,
/// must be absolute HTTP(S).</summary>
public sealed record EvidenceSourceRef(string Title, string? Url, string Accessed);

/// <summary>One dimension's record: what was tried, what was seen, what was expected.</summary>
public sealed record DimensionEvidence(
    IsolationDimension Dimension,
    EvidenceStatus Status,
    string? Method,
    string? ObservedResult,
    string? ExpectedResult,
    string? Discrepancy);

/// <summary>Approval decision, separate from technical evidence.</summary>
public sealed record ApprovalInfo(ApprovalStatus Status, string? ApprovedBy, string? Rationale);

/// <summary>Structured evidence bundle. Free-text fields must never contain passwords,
/// tokens, cookies, databases, user documents, or full environment dumps; validation
/// enforces shape, privacy review enforces content.</summary>
public sealed record EvidenceBundle(
    string CandidateId,
    string CandidateVersion,
    string? AppId,
    string? AppVersion,
    Guid RecordId,
    int SchemaVersion,
    DateTimeOffset VerifiedAtUtc,
    string Verifier,
    IReadOnlyList<DimensionEvidence> Dimensions,
    IReadOnlyList<EvidenceSourceRef> Sources,
    string TestMethod,
    string ReproSteps,
    string? PlatformAssumptions,
    IReadOnlyList<string> OutstandingQuestions,
    ApprovalInfo Approval,
    string? IntegrityHash);

/// <summary>Counts-only summary for UI display. Contains no free-text fields, so no
/// sensitive content can leak through it.</summary>
public sealed record EvidenceSummary(
    int TotalDimensions,
    int RecordedDimensions,
    int VerifiedDimensions,
    int FailedDimensions,
    int MissingDimensions,
    bool Complete,
    bool HasFailures);
