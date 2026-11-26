using System.Globalization;
using System.Text;

namespace Mitosis.Application.Diagnostics;

/// <summary>Severity of a local diagnostic entry. Never sent anywhere.</summary>
public enum DiagnosticLevel
{
    Info,
    Warning,
    Error,
}

/// <summary>Append-only local diagnostic trail. Implementations must never throw:
/// logging is best effort and must not change application behavior.</summary>
public interface IDiagnosticLog
{
    /// <summary>Directory that holds the log files; shown to the user for support.</summary>
    string DirectoryPath { get; }

    void Write(DiagnosticLevel level, string eventName, string message);
}

/// <summary>Pure text hygiene shared by the file logger and the diagnostics report.
/// Redacts the current user's home directory so logs and copied reports can be shared
/// without exposing the account name in a path.</summary>
public static class DiagnosticRedaction
{
    public const string HomePlaceholder = "~";

    /// <summary>Replaces the supplied (or current) home directory with <c>~</c>,
    /// case-insensitively and in both slash styles.</summary>
    public static string RedactHome(string? text, string? homeDirectory = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var home = string.IsNullOrWhiteSpace(homeDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : homeDirectory;
        home = home?.TrimEnd('\\', '/');
        if (string.IsNullOrWhiteSpace(home))
        {
            return text;
        }

        var result = text.Replace(home, HomePlaceholder, StringComparison.OrdinalIgnoreCase);
        var forward = home.Replace('\\', '/');
        if (!string.Equals(forward, home, StringComparison.Ordinal))
        {
            result = result.Replace(forward, HomePlaceholder, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    /// <summary>Collapses line breaks and tabs so one log call is always one line, and
    /// caps the length so a pathological message cannot dominate the file.</summary>
    public static string SingleLine(string? text, int maxLength = 2000)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var chars = new char[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            chars[i] = c is '\r' or '\n' or '\t' ? ' ' : c;
        }

        var trimmed = new string(chars).Trim();
        var builder = new StringBuilder(trimmed.Length);
        var previousSpace = false;
        foreach (var c in trimmed)
        {
            if (c == ' ')
            {
                if (previousSpace)
                {
                    continue;
                }

                previousSpace = true;
            }
            else
            {
                previousSpace = false;
            }

            builder.Append(c);
        }

        var collapsed = builder.ToString();
        return collapsed.Length <= maxLength ? collapsed : collapsed[..maxLength] + "…";
    }
}

/// <summary>Facts collected at startup for the diagnostics report. Paths are redacted
/// when the report is built, not here, so the caller can keep using them.</summary>
public sealed record DoctorFacts(
    string AppVersion,
    string RuntimeVersion,
    string OperatingSystem,
    string Architecture,
    string WindowsAppSdkVersion,
    string DataDirectory,
    bool DataDirectoryWritable,
    string LogDirectory);

/// <summary>Plain-text, local-only diagnostics summary. Deliberately excludes
/// application data, credentials, file contents, and network activity; the caller
/// supplies only version and path facts.</summary>
public static class DoctorReport
{
    public static string Build(
        DoctorFacts facts,
        string? homeDirectory = null,
        DateTimeOffset? generatedAt = null)
    {
        var now = (generatedAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var builder = new StringBuilder();
        builder.AppendLine("Mitosis diagnostics");
        builder.Append("Generated: ")
            .AppendLine(now.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        builder.Append("Application version: ").AppendLine(Value(facts.AppVersion, homeDirectory));
        builder.Append("Runtime: ").AppendLine(Value(facts.RuntimeVersion, homeDirectory));
        builder.Append("Operating system: ").AppendLine(Value(facts.OperatingSystem, homeDirectory));
        builder.Append("Architecture: ").AppendLine(Value(facts.Architecture, homeDirectory));
        builder.Append("Windows App SDK: ").AppendLine(Value(facts.WindowsAppSdkVersion, homeDirectory));
        builder.Append("Data folder: ")
            .AppendLine(DiagnosticRedaction.RedactHome(facts.DataDirectory, homeDirectory));
        builder.Append("Data folder writable: ").AppendLine(facts.DataDirectoryWritable ? "yes" : "no");
        builder.Append("Log folder: ")
            .AppendLine(DiagnosticRedaction.RedactHome(facts.LogDirectory, homeDirectory));
        builder.AppendLine();
        builder.AppendLine("Recorded: app/runtime versions and Mitosis data paths only.");
        builder.AppendLine("Not recorded: application data, credentials, file contents, or network activity.");
        return builder.ToString();
    }

    private static string Value(string? value, string? homeDirectory) =>
        string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : DiagnosticRedaction.RedactHome(value, homeDirectory);
}
