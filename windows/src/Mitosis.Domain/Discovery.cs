using System.Security.Cryptography;
using System.Text;

namespace Mitosis.Domain;

/// <summary>Where a Windows application registration was found. Only known registration
/// locations are ever enumerated; drives and profile directories are never crawled.</summary>
public enum DiscoverySource
{
    CurrentUserUninstall,
    LocalMachineUninstall32,
    LocalMachineUninstall64,
    AppxPackage,
}

/// <summary>Installation scope, where determinable from the discovery source.</summary>
public enum InstallScope
{
    Unknown,
    CurrentUser,
    Machine,
}

/// <summary>Stable identity for a discovered application. The identity derives from the
/// discovery source plus a normalized (name, publisher) key — never from a bare executable
/// path — so reinstalls to a new folder keep their identity while renames do not collide.</summary>
public static class AppIdentity
{
    public static string NormalizeKey(string displayName, string? publisher)
    {
        var name = (displayName ?? string.Empty).Trim().ToLowerInvariant();
        var pub = (publisher ?? string.Empty).Trim().ToLowerInvariant();
        return name + "\0" + pub;
    }

    public static string Create(DiscoverySource source, string displayName, string? publisher)
    {
        var input = source + "\0" + NormalizeKey(displayName, publisher);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return source.ToString().ToLowerInvariant() + ":" + Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }
}

/// <summary>One discovered Windows application. Missing metadata stays missing — it is
/// never fabricated. Paths are attributes for display/diagnostics, never scan roots and
/// never executed.</summary>
public sealed record DiscoveredApp(
    string Id,
    string DisplayName,
    string? Publisher,
    string? Version,
    string? InstallLocation,
    string? ExecutablePath,
    DiscoverySource PrimarySource,
    IReadOnlyList<DiscoverySource> Sources,
    InstallScope Scope,
    bool IsSystemComponent,
    string? IconReference,
    IReadOnlyList<string> Warnings);
