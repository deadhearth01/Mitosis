using Microsoft.Win32;
using Mitosis.Domain;

namespace Mitosis.Windows.Platform.Discovery;

/// <summary>Unhandled registry access behind a seam so discovery is unit-testable without
/// touching the real registry. Production implementation opens keys read-only.</summary>
public interface IRegistryReader
{
    IReadOnlyList<string> GetSubKeyNames(RegistryHive hive, RegistryView view, string path);

    IReadOnlyDictionary<string, object?> GetValues(RegistryHive hive, RegistryView view, string path);
}

/// <summary>Production registry reader. Read-only: keys are opened without write access,
/// values are returned as-is, missing or inaccessible keys yield empty results.</summary>
public sealed class RegistryKeyReader : IRegistryReader
{
    public IReadOnlyList<string> GetSubKeyNames(RegistryHive hive, RegistryView view, string path)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(path, writable: false);
            return key?.GetSubKeyNames() ?? Array.Empty<string>();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            return Array.Empty<string>();
        }
    }

    public IReadOnlyDictionary<string, object?> GetValues(RegistryHive hive, RegistryView view, string path)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(path, writable: false);
            if (key == null)
            {
                return new Dictionary<string, object?>();
            }

            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in key.GetValueNames())
            {
                result[name] = key.GetValue(name);
            }

            return result;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            return new Dictionary<string, object?>();
        }
    }
}
