using System.Reflection;

namespace Mitosis.Application;

/// <summary>Single source for the version shown by the app, the CLI, and the update
/// checker. The informational version (which normally carries a <c>+&lt;git-sha&gt;</c>
/// suffix) wins over the assembly version, and nothing is fabricated when neither
/// exists.</summary>
public static class ProductVersion
{
    public static string Describe(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
