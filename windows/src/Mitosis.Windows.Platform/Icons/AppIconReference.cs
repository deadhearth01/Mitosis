using System.IO;

namespace Mitosis.Windows.Platform.Icons;

/// <summary>Parses a registry <c>DisplayIcon</c> value into a readable icon source.
/// The value is untrusted metadata: only a fully qualified local path with a known
/// icon-bearing extension is accepted, never a network path, and nothing is ever
/// executed or written.</summary>
public static class AppIconReference
{
    private static readonly string[] AllowedExtensions = [".exe", ".ico", ".dll"];

    /// <summary>Returns the icon file path, or null when the value is absent, remote,
    /// relative, or of an unsupported kind.</summary>
    public static string? Parse(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
        {
            return null;
        }

        var text = displayIcon.Trim();

        if (text.StartsWith('"'))
        {
            var close = text.IndexOf('"', 1);
            if (close <= 1)
            {
                return null;
            }

            text = text.Substring(1, close - 1);
        }
        else
        {
            // "C:\Apps\Thing.exe,0" / "C:\Apps\Thing.exe,-12" — the index follows the
            // last comma. A comma inside a directory name is rare but possible, so only
            // strip a suffix that is purely an integer.
            var comma = text.LastIndexOf(',');
            if (comma > 0 && IsIndex(text.AsSpan(comma + 1)))
            {
                text = text.Substring(0, comma);
            }
        }

        text = text.Trim().Trim('"').Trim();
        if (text.Length == 0)
        {
            return null;
        }

        string expanded;
        try
        {
            expanded = Environment.ExpandEnvironmentVariables(text);
        }
        catch
        {
            return null;
        }

        if (!Path.IsPathFullyQualified(expanded)
            || expanded.StartsWith(@"\\", StringComparison.Ordinal)
            || expanded.StartsWith(@"\\?\", StringComparison.Ordinal)
            || expanded.StartsWith(@"\\.\", StringComparison.Ordinal)
            || expanded.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        var extension = Path.GetExtension(expanded);
        if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return expanded;
    }

    private static bool IsIndex(ReadOnlySpan<char> span)
    {
        if (span.Length == 0)
        {
            return false;
        }

        var start = span[0] == '-' ? 1 : 0;
        if (start == span.Length)
        {
            return false;
        }

        for (var i = start; i < span.Length; i++)
        {
            if (!char.IsAsciiDigit(span[i]))
            {
                return false;
            }
        }

        return true;
    }
}
