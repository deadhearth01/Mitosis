// Mitosis for Windows — domain primitive.
//
// Mirrors the clone-naming rules of the macOS implementation
// (Sources/MitosisCore/Naming.swift: CloneName) so both platforms
// produce identical "App (Label)" display names and accept the same input.

namespace Mitosis.Domain;

/// <summary>Why <see cref="CloneName.Validate"/> rejected a name.</summary>
public enum CloneNameError
{
    Empty,
    TooLong,
    LeadingDot,
    InvalidCharacter,
}

/// <summary>
/// Thrown when a clone name fails validation.
/// <see cref="Exception.Message"/> carries the user-facing text shown in the UI.
/// </summary>
public sealed class CloneNameException : Exception
{
    public CloneNameError Error { get; }

    public char? InvalidCharacter { get; }

    public CloneNameException(CloneNameError error, char? invalidCharacter = null)
        : base(Describe(error, invalidCharacter))
    {
        Error = error;
        InvalidCharacter = invalidCharacter;
    }

    private static string Describe(CloneNameError error, char? invalidCharacter) => error switch
    {
        CloneNameError.Empty => "Give the clone a name.",
        CloneNameError.TooLong => $"Clone names can be at most {CloneName.MaxLength} characters.",
        CloneNameError.LeadingDot => "Clone names can't start with a dot.",
        CloneNameError.InvalidCharacter => $"Clone names can't contain \"{invalidCharacter}\".",
        _ => "That clone name isn't valid.",
    };
}

/// <summary>
/// Clone display-name rules: names look like "App (Label)", for example "Slack (Work)",
/// so the label never merges with the app name.
/// </summary>
public static class CloneName
{
    public const int MaxLength = 60;

    /// <summary>
    /// Validates a user-entered clone name and returns the trimmed form.
    /// Throws <see cref="CloneNameException"/> with a user-facing message on failure.
    /// </summary>
    public static string Validate(string? raw)
    {
        var name = (raw ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new CloneNameException(CloneNameError.Empty);
        }

        // NOTE: Length is UTF-16 code units here; Swift counts grapheme clusters.
        // Identical results for BMP text; revisit if non-BMP limits matter (Stage 3+).
        if (name.Length > MaxLength)
        {
            throw new CloneNameException(CloneNameError.TooLong);
        }

        if (name.StartsWith(".", StringComparison.Ordinal))
        {
            throw new CloneNameException(CloneNameError.LeadingDot);
        }

        foreach (var c in name)
        {
            if (c == '/' || c == ':' || c == '\0' || IsNewline(c))
            {
                throw new CloneNameException(CloneNameError.InvalidCharacter, c);
            }
        }

        return name;
    }

    /// <summary>"&lt;App&gt; (&lt;Label&gt;)", e.g. "Slack (Work)". An empty label returns the app name.</summary>
    public static string Compose(string? app, string? label)
    {
        var a = (app ?? string.Empty).Trim();
        var l = (label ?? string.Empty).Trim();
        return l.Length == 0 ? a : $"{a} ({l})";
    }

    // Mirrors Swift Character.isNewline: LF, CR, VT, FF, NEL, LS, PS.
    private static bool IsNewline(char c) =>
        c is '\r' or '\n' or '\v' or '\f' or '\u0085' or '\u2028' or '\u2029';
}
