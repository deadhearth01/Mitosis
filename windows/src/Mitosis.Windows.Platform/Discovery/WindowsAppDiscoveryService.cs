using Mitosis.Application.Discovery;
using Mitosis.Domain;

namespace Mitosis.Windows.Platform.Discovery;

/// <summary>Read-only orchestration over known registration locations. Merges duplicate
/// registrations deterministically (normalized name + publisher, case-insensitive),
/// tolerates per-source failure, honors cancellation. Never writes, executes,
/// downloads, or recursively scans.</summary>
public sealed class WindowsAppDiscoveryService : IAppDiscoveryService
{
    private readonly IReadOnlyList<IInstalledAppSource> sources;

    public WindowsAppDiscoveryService(IEnumerable<IInstalledAppSource> sources)
    {
        this.sources = sources.ToList();
    }

    public async Task<IReadOnlyList<DiscoveredApp>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var records = new List<RawAppRecord>();
        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                records.AddRange(await source.CollectAsync(cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // One unreliable source must not sink the whole discovery pass.
            }
        }

        return Merge(records);
    }

    internal static IReadOnlyList<DiscoveredApp> Merge(IReadOnlyList<RawAppRecord> records)
    {
        return records
            .GroupBy(r => AppIdentity.NormalizeKey(r.DisplayName, r.Publisher), StringComparer.Ordinal)
            .Select(g =>
            {
                // First occurrence wins display fields: sources are enumerated in composition
                // order (user scope before machine), so input order is the priority order.
                var ordered = g.ToList();
                var first = ordered[0];
                var scope = ordered.Any(r => r.Scope == InstallScope.Machine)
                    ? InstallScope.Machine
                    : ordered.Any(r => r.Scope == InstallScope.CurrentUser)
                        ? InstallScope.CurrentUser
                        : InstallScope.Unknown;
                return new DiscoveredApp(
                    Id: AppIdentity.Create(first.Source, first.DisplayName, first.Publisher),
                    DisplayName: first.DisplayName.Trim(),
                    Publisher: ordered.Select(r => r.Publisher).FirstOrDefault(p => !string.IsNullOrEmpty(p)),
                    Version: ordered.Select(r => r.Version).FirstOrDefault(v => !string.IsNullOrEmpty(v)),
                    InstallLocation: ordered.Select(r => r.InstallLocation).FirstOrDefault(p => !string.IsNullOrEmpty(p)),
                    ExecutablePath: null,
                    PrimarySource: first.Source,
                    Sources: ordered.Select(r => r.Source).Distinct().ToList(),
                    Scope: scope,
                    IsSystemComponent: ordered.Any(r => r.IsSystemComponent),
                    IconReference: ordered.Select(r => r.IconReference).FirstOrDefault(p => !string.IsNullOrEmpty(p)),
                    Warnings: ordered.SelectMany(r => r.Warnings).Distinct().ToList());
            })
            .OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Id, StringComparer.Ordinal)
            .ToList();
    }
}
