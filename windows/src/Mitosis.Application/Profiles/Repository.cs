using Mitosis.Domain;

namespace Mitosis.Application.Profiles;

/// <summary>Metadata-only profile store. Removal deletes the metadata record alone — never
/// an installation, clone directory, or user data. All operations are async and
/// cancellation-aware.</summary>
public interface ICloneProfileRepository
{
    Task<IReadOnlyList<CloneProfile>> ListAsync(CancellationToken cancellationToken = default);

    Task<CloneProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CloneProfile> AddAsync(CloneProfile profile, CancellationToken cancellationToken = default);

    Task<CloneProfile> UpdateAsync(CloneProfile profile, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Base for profile-store failures. Messages are user-safe: no local paths,
/// no account names, no record contents beyond an ID or name.</summary>
public class ProfileStoreException : Exception
{
    public ProfileStoreException(string message)
        : base(message)
    {
    }

    public ProfileStoreException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public sealed class DuplicateProfileException : ProfileStoreException
{
    public DuplicateProfileException(string message)
        : base(message)
    {
    }
}

public sealed class ProfileNotFoundException : ProfileStoreException
{
    public ProfileNotFoundException(string message)
        : base(message)
    {
    }
}

public sealed class UnsupportedProfileSchemaException : ProfileStoreException
{
    public UnsupportedProfileSchemaException(string message)
        : base(message)
    {
    }
}
