using Mitosis.Application;

namespace Mitosis.Cli;

/// <summary>Read-only command line surface. Parsing is pure: it never touches the
/// registry, the filesystem, or the network.</summary>
public enum CliCommand
{
    Help,
    Version,
    List,
    Profiles,
    Doctor,
}

/// <summary>One parsed invocation. <see cref="Json"/> is accepted by every command so
/// scripts can switch format without a second parser.</summary>
public sealed record CliInvocation(CliCommand Command, bool Json);

/// <summary>Result of parsing: either an invocation or a one-line usage error. No
/// exception is thrown for malformed input, so the process can report it cleanly.</summary>
public sealed record CliParseResult(CliInvocation? Invocation, string? Error)
{
    public static CliParseResult Success(CliInvocation invocation) => new(invocation, null);

    public static CliParseResult Failure(string error) => new(null, error);
}

public static class CommandLine
{
    public const string HelpHint = "Run 'mitosis --help' for usage.";

    public static CliParseResult Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 0)
        {
            return CliParseResult.Success(new CliInvocation(CliCommand.Help, Json: false));
        }

        var first = args[0].Trim().ToLowerInvariant();
        switch (first)
        {
            case "help":
            case "-h":
            case "--help":
                if (args.Count > 1)
                {
                    return CliParseResult.Failure(
                        $"Help takes no arguments (got '{args[1]}'). {HelpHint}");
                }

                return CliParseResult.Success(new CliInvocation(CliCommand.Help, Json: false));
            case "version":
            case "-v":
            case "--version":
                return ParseCommand(args, 1, CliCommand.Version);
            case "list":
                return ParseCommand(args, 1, CliCommand.List);
            case "profiles":
                return ParseCommand(args, 1, CliCommand.Profiles);
            case "doctor":
                return ParseCommand(args, 1, CliCommand.Doctor);
            default:
                return CliParseResult.Failure(
                    $"Unknown command '{args[0]}'. {HelpHint}");
        }
    }

    private static CliParseResult ParseCommand(IReadOnlyList<string> args, int start, CliCommand command)
    {
        var json = false;
        for (var i = start; i < args.Count; i++)
        {
            if (IsJson(args[i]))
            {
                if (json)
                {
                    return CliParseResult.Failure($"--json was given more than once. {HelpHint}");
                }

                json = true;
                continue;
            }

            return CliParseResult.Failure(
                $"Unexpected argument '{args[i]}' for '{args[0]}'. {HelpHint}");
        }

        return CliParseResult.Success(new CliInvocation(command, json));
    }

    private static bool IsJson(string value) =>
        string.Equals(value, "--json", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Process exit codes. 0 success, 1 bad usage, 2 an operation failed (a
/// read-only command could not complete, for example a damaged profile store).</summary>
public static class CliExitCode
{
    public const int Success = 0;
    public const int Usage = 1;
    public const int Failure = 2;
}

public static class HelpText
{
    public const string Value = """
        mitosis - read-only command line for Mitosis on Windows

        Usage:
          mitosis version [--json]
          mitosis list [--json]
          mitosis profiles [--json]
          mitosis doctor [--json]
          mitosis help

        Commands:
          version    Print the Mitosis version.
          list       List applications discovered on this PC.
          profiles   List clone profile metadata records.
          doctor     Print a local diagnostics report and store health.
          help       Show this help.

        Options:
          --json     Machine-readable output (all commands).
          -h, --help Show this help.
          -v, --version
                     Same as 'mitosis version'.

        This CLI is read-only. It never clones, launches, modifies, or deletes an
        application or its data. Cloning and other engine commands are not
        implemented on Windows yet, so they are not accepted at all.
        """;
}
