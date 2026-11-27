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
    /// <summary>Identity key. Publisher text is compared verbatim (case-insensitive), so an
    /// identity never changes when only the publisher's spelling convention changes.</summary>
    public static string NormalizeKey(string displayName, string? publisher)
    {
        var name = (displayName ?? string.Empty).Trim().ToLowerInvariant();
        var pub = (publisher ?? string.Empty).Trim().ToLowerInvariant();
        return name + "\0" + pub;
    }

    /// <summary>Merge key. Packaged apps report their publisher as an X.500 distinguished
    /// name (<c>CN=Microsoft Corporation, O=...</c>) while the install registry reports the
    /// friendly name (<c>Microsoft Corporation</c>); reducing both to the common name keeps
    /// one application from appearing twice in the list. Identity still uses
    /// <see cref="NormalizeKey"/> so existing profile references keep resolving.</summary>
    public static string MergeKey(string displayName, string? publisher)
    {
        var name = (displayName ?? string.Empty).Trim().ToLowerInvariant();
        return name + "\0" + NormalizePublisher(publisher);
    }

    /// <summary>True when the publisher is reported as an X.500 distinguished name.</summary>
    public static bool IsDistinguishedName(string? publisher) =>
        CommonName(publisher) is not null;

    /// <summary>Publisher reduced to its common name when it is a distinguished name,
    /// otherwise the trimmed value, lower-cased for comparison.</summary>
    public static string NormalizePublisher(string? publisher)
    {
        var common = CommonName(publisher);
        return common ?? (publisher ?? string.Empty).Trim().ToLowerInvariant();
    }

    /// <summary>Reads the first <c>CN=</c> component of a distinguished name, honoring
    /// backslash escapes. Returns null when the text is not a distinguished name.</summary>
    private static string? CommonName(string? publisher)
    {
        var text = (publisher ?? string.Empty).Trim();
        if (text.Length == 0 || text.IndexOf('=') < 0)
        {
            return null;
        }

        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\\' && i + 1 < text.Length)
            {
                current.Append(text[++i]);
                continue;
            }

            if (c == ',')
            {
                parts.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        parts.Add(current.ToString());

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed.Substring(3).Trim();
                return value.Length == 0 ? null : value.ToLowerInvariant();
            }
        }

        return null;
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
