using Mitosis.Domain;

namespace Mitosis.Application.Discovery;

/// <summary>Read-only discovery of installed Windows applications. Implementations must
/// never write, execute, download, or recursively scan; see docs/WINDOWS-DISCOVERY.md.</summary>
public interface IAppDiscoveryService
{
    Task<IReadOnlyList<DiscoveredApp>> DiscoverAsync(CancellationToken cancellationToken = default);
}

/// <summary>Pure, deterministic compatibility assessment. Must not launch applications,
/// touch the system, or perform I/O.</summary>
public interface ICompatibilityAssessor
{
    CompatibilityAssessment Assess(DiscoveredApp app, DateTimeOffset? assessedAt = null);
}
