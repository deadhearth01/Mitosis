using Mitosis.Domain;

namespace Mitosis.Application.Evidence;

/// <summary>Read-only-plus-save evidence store. Bundles contain no sensitive data by
/// construction; see docs/WINDOWS-EVIDENCE-BUNDLES.md for privacy rules.</summary>
public interface IEvidenceStore
{
    Task<IReadOnlyList<EvidenceBundle>> ListAsync(CancellationToken cancellationToken = default);

    Task<EvidenceBundle?> GetAsync(Guid recordId, CancellationToken cancellationToken = default);

    Task<EvidenceBundle> SaveAsync(EvidenceBundle bundle, CancellationToken cancellationToken = default);
}

/// <summary>Evidence-store failures. Messages are user-safe: exception type names and
/// record IDs only, never paths or file contents.</summary>
public class EvidenceStoreException : Exception
{
    public EvidenceStoreException(string message)
        : base(message)
    {
    }

    public EvidenceStoreException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public sealed class UnsupportedEvidenceSchemaException : EvidenceStoreException
{
    public UnsupportedEvidenceSchemaException(string message)
        : base(message)
    {
    }
}
