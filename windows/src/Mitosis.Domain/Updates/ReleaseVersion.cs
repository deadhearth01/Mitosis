using System.Globalization;

namespace Mitosis.Domain.Updates;

/// <summary>Semantic-version comparison for update checks, ported from the macOS
/// <c>Versioning.swift</c> rules: an optional leading <c>v</c>, one to four numeric core
/// components, and an optional dotted prerelease. Build metadata (<c>+&lt;sha&gt;</c>)
/// is ignored, which the Windows informational version always carries.
///
/// Deliberate parity quirks: alphanumeric prerelease identifiers sort above numeric
/// ones, and a shorter prerelease sorts below a longer one with the same prefix.</summary>
public readonly record struct ReleaseVersion(IReadOnlyList<int> Core, IReadOnlyList<string>? Prerelease)
{
    public static bool TryParse(string? raw, out ReleaseVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var text = raw.Trim();

        // Informational versions are "x.y.z+<git-sha>"; the suffix is not semver.
        var plus = text.IndexOf('+');
        if (plus >= 0)
        {
            text = text[..plus];
        }

        if (text.Length > 1 && (text[0] == 'v' || text[0] == 'V'))
        {
            text = text[1..];
        }

        var dash = text.IndexOf('-');
        var coreText = dash >= 0 ? text[..dash] : text;
        var prereleaseText = dash >= 0 ? text[(dash + 1)..] : null;

        var coreParts = coreText.Split('.');
        if (coreParts.Length is < 1 or > 4)
        {
            return false;
        }

        var core = new int[coreParts.Length];
        for (var i = 0; i < coreParts.Length; i++)
        {
            if (!int.TryParse(coreParts[i], NumberStyles.None, CultureInfo.InvariantCulture, out core[i]))
            {
                return false;
            }
        }

        string[]? prerelease = null;
        if (prereleaseText is not null)
        {
            if (prereleaseText.Length == 0)
            {
                return false;
            }

            prerelease = prereleaseText.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (prerelease.Length == 0)
            {
                return false;
            }
        }

        version = new ReleaseVersion(core, prerelease);
        return true;
    }

    /// <summary>True when <paramref name="candidate"/> is strictly newer than
    /// <paramref name="current"/>. Unparseable input is never "newer", so a malformed
    /// release tag cannot produce an update banner.</summary>
    public static bool IsNewer(string? candidate, string? current)
    {
        if (!TryParse(candidate, out var a) || !TryParse(current, out var b))
        {
            return false;
        }

        return IsNewer(a, b);
    }

    public static bool IsNewer(ReleaseVersion candidate, ReleaseVersion current)
    {
        var length = Math.Max(candidate.Core.Count, current.Core.Count);
        for (var i = 0; i < length; i++)
        {
            var x = i < candidate.Core.Count ? candidate.Core[i] : 0;
            var y = i < current.Core.Count ? current.Core[i] : 0;
            if (x != y)
            {
                return x > y;
            }
        }

        var pa = candidate.Prerelease;
        var pb = current.Prerelease;
        if (pa is null && pb is null)
        {
            return false;
        }

        if (pa is null)
        {
            return true;
        }

        if (pb is null)
        {
            return false;
        }

        for (var i = 0; i < Math.Min(pa.Count, pb.Count); i++)
        {
            var x = pa[i];
            var y = pb[i];
            if (string.Equals(x, y, StringComparison.Ordinal))
            {
                continue;
            }

            var xIsNumber = int.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out var nx);
            var yIsNumber = int.TryParse(y, NumberStyles.None, CultureInfo.InvariantCulture, out var ny);
            if (xIsNumber && yIsNumber)
            {
                return nx > ny;
            }

            if (!xIsNumber && yIsNumber)
            {
                return true;
            }

            if (xIsNumber)
            {
                return false;
            }

            return string.CompareOrdinal(x, y) > 0;
        }

        return pa.Count > pb.Count;
    }

    /// <summary>A version without the <c>v</c> prefix or <c>+build</c> metadata, for
    /// display and for prerelease detection. Unrecognized text is returned trimmed.</summary>
    public static string DisplayVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "unknown";
        }

        var text = raw.Trim();
        var plus = text.IndexOf('+');
        if (plus > 0)
        {
            text = text[..plus];
        }

        if (text.Length > 1 && (text[0] == 'v' || text[0] == 'V'))
        {
            text = text[1..];
        }

        return text.Length == 0 ? raw.Trim() : text;
    }
}
