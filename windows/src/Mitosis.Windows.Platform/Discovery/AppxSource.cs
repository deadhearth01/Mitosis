using Mitosis.Domain;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace Mitosis.Windows.Platform.Discovery;

/// <summary>Enumerated packaged app, without executing anything. The production
/// implementation reads package metadata only.</summary>
public sealed record AppxEntry(
    string Name,
    string DisplayName,
    string Publisher,
    string Version,
    string InstallLocation,
    bool IsFramework,
    bool IsResource,
    bool IsSystem);

/// <summary>Seam over PackageManager so packaged-app discovery is unit-testable.</summary>
public interface IAppxEnumerator
{
    IEnumerable<AppxEntry> Enumerate();
}

/// <summary>Production enumerator: current user's packages, metadata only, no execution.</summary>
public sealed class PackageManagerEnumerator : IAppxEnumerator
{
    public IEnumerable<AppxEntry> Enumerate()
    {
        var manager = new PackageManager();
        foreach (var package in manager.FindPackagesForUser(string.Empty))
        {
            var id = package.Id;
            yield return new AppxEntry(
                Name: id.Name,
                DisplayName: package.DisplayName,
                Publisher: id.Publisher,
                Version: $"{id.Version.Major}.{id.Version.Minor}.{id.Version.Build}.{id.Version.Revision}",
                InstallLocation: package.InstalledPath,
                IsFramework: package.IsFramework,
                IsResource: package.IsResourcePackage,
                IsSystem: package.SignatureKind == PackageSignatureKind.System);
        }
    }
}

/// <summary>Collects packaged apps. Framework and resource packages are skipped (they are
/// not applications); system-signed packages are kept and flagged for assessment.</summary>
public sealed class AppxPackageSource : IInstalledAppSource
{
    private readonly IAppxEnumerator enumerator;

    public AppxPackageSource(IAppxEnumerator enumerator)
    {
        this.enumerator = enumerator;
    }

    public string Name => nameof(AppxPackageSource);

    public Task<IReadOnlyList<RawAppRecord>> CollectAsync(CancellationToken cancellationToken)
    {
        var records = new List<RawAppRecord>();
        foreach (var entry in enumerator.Enumerate())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.IsFramework || entry.IsResource)
            {
                continue;
            }

            var warnings = new List<string>();
            var displayName = entry.DisplayName;
            if (displayName.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add("Display name is an unresolved resource reference; kept as reported.");
            }

            records.Add(new RawAppRecord(
                DisplayName: displayName,
                Publisher: entry.Publisher,
                Version: entry.Version,
                InstallLocation: entry.InstallLocation,
                Source: DiscoverySource.AppxPackage,
                Scope: InstallScope.CurrentUser,
                IsSystemComponent: entry.IsSystem,
                IconReference: null,
                Warnings: warnings));
        }

        return Task.FromResult<IReadOnlyList<RawAppRecord>>(records);
    }
}
