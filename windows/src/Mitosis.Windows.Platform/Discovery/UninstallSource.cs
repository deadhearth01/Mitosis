using Microsoft.Win32;
using Mitosis.Domain;

namespace Mitosis.Windows.Platform.Discovery;

/// <summary>Raw per-source record. Paths are recorded strings only — never enumerated,
/// never executed, never followed for further scanning.</summary>
public sealed record RawAppRecord(
    string DisplayName,
    string? Publisher,
    string? Version,
    string? InstallLocation,
    DiscoverySource Source,
    InstallScope Scope,
    bool IsSystemComponent,
    string? IconReference,
    IReadOnlyList<string> Warnings);

/// <summary>One known registration location. CollectAsync must be read-only, must not
/// recurse beyond the location's direct entries, and must tolerate partial failure.</summary>
public interface IInstalledAppSource
{
    string Name { get; }

    Task<IReadOnlyList<RawAppRecord>> CollectAsync(CancellationToken cancellationToken);
}

/// <summary>Reads one uninstall registration view (one registry key, direct subkeys only).
/// Skips entries without a display name, system components, and child (patch) entries.</summary>
public sealed class UninstallRegistrySource : IInstalledAppSource
{
    public const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private readonly RegistryHive hive;
    private readonly RegistryView view;
    private readonly DiscoverySource source;
    private readonly InstallScope scope;
    private readonly IRegistryReader reader;

    public UninstallRegistrySource(
        RegistryHive hive, RegistryView view, DiscoverySource source, InstallScope scope, IRegistryReader reader)
    {
        this.hive = hive;
        this.view = view;
        this.source = source;
        this.scope = scope;
        this.reader = reader;
    }

    public string Name => source.ToString();

    public Task<IReadOnlyList<RawAppRecord>> CollectAsync(CancellationToken cancellationToken)
    {
        var records = new List<RawAppRecord>();
        foreach (var subkey in reader.GetSubKeyNames(hive, view, UninstallPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var values = reader.GetValues(hive, view, UninstallPath + "\\" + subkey);
                var record = ToRecord(values);
                if (record != null)
                {
                    records.Add(record);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // One unreadable entry must not sink the whole location.
            }
        }

        return Task.FromResult<IReadOnlyList<RawAppRecord>>(records);
    }

    private RawAppRecord? ToRecord(IReadOnlyDictionary<string, object?> values)
    {
        if (!values.TryGetValue("DisplayName", out var rawName) || rawName is not string name || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (values.TryGetValue("SystemComponent", out var rawSystem) && rawSystem is int system && system == 1)
        {
            return null;
        }

        if (values.TryGetValue("ParentKeyName", out var rawParent) && rawParent is string parent && parent.Length > 0)
        {
            return null;
        }

        var warnings = new List<string>();
        string? version = null;
        if (values.TryGetValue("DisplayVersion", out var rawVersion) && rawVersion is string versionText && versionText.Length > 0)
        {
            version = versionText;
            if (!Version.TryParse(versionText, out _))
            {
                warnings.Add($"Version '{versionText}' is not numeric; kept as reported.");
            }
        }

        return new RawAppRecord(
            DisplayName: name.Trim(),
            Publisher: StringOrNull(values, "Publisher"),
            Version: version,
            InstallLocation: StringOrNull(values, "InstallLocation"),
            Source: source,
            Scope: scope,
            IsSystemComponent: false,
            IconReference: StringOrNull(values, "DisplayIcon"),
            Warnings: warnings);
    }

    private static string? StringOrNull(IReadOnlyDictionary<string, object?> values, string name)
    {
        if (values.TryGetValue(name, out var raw) && raw is string text && !string.IsNullOrWhiteSpace(text))
        {
            return text.Trim();
        }

        return null;
    }
}
