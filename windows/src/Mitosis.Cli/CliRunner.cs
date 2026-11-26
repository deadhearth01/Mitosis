using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mitosis.Application.Diagnostics;
using Mitosis.Application.Discovery;
using Mitosis.Application.Profiles;
using Mitosis.Domain;

namespace Mitosis.Cli;

/// <summary>Executes the read-only commands against injected services. Nothing here
/// writes to the profile store, preferences, application data, or the registry; only
/// <c>doctor</c> creates and immediately deletes one zero-byte writability probe.</summary>
public sealed class CliRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly IAppDiscoveryService discovery;
    private readonly ICloneProfileRepository profiles;
    private readonly ICompatibilityAssessor assessor;
    private readonly CliEnvironment environment;
    private readonly Func<DateTimeOffset> clock;

    public CliRunner(
        IAppDiscoveryService discovery,
        ICloneProfileRepository profiles,
        ICompatibilityAssessor assessor,
        CliEnvironment environment,
        Func<DateTimeOffset>? clock = null)
    {
        this.discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.assessor = assessor ?? throw new ArgumentNullException(nameof(assessor));
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<int> RunAsync(
        CliInvocation invocation,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        switch (invocation.Command)
        {
            case CliCommand.Help:
                output.Write(HelpText.Value);
                return CliExitCode.Success;
            case CliCommand.Version:
                WriteVersion(invocation.Json, output);
                return CliExitCode.Success;
            case CliCommand.List:
                return await WriteListAsync(invocation.Json, output, error, cancellationToken)
                    .ConfigureAwait(false);
            case CliCommand.Profiles:
                return await WriteProfilesAsync(invocation.Json, output, error, cancellationToken)
                    .ConfigureAwait(false);
            case CliCommand.Doctor:
                return await WriteDoctorAsync(invocation.Json, output, cancellationToken)
                    .ConfigureAwait(false);
            default:
                error.WriteLine($"mitosis: unsupported command. {CommandLine.HelpHint}");
                return CliExitCode.Usage;
        }
    }

    private void WriteVersion(bool json, TextWriter output)
    {
        if (json)
        {
            output.WriteLine(JsonSerializer.Serialize(
                new
                {
                    application = "Mitosis",
                    version = environment.AppVersion,
                    runtime = environment.RuntimeVersion,
                    operatingSystem = environment.OperatingSystem,
                    architecture = environment.Architecture,
                },
                JsonOptions));
            return;
        }

        output.WriteLine($"Mitosis {environment.AppVersion}");
        output.WriteLine($"{environment.RuntimeVersion} on {environment.OperatingSystem} ({environment.Architecture})");
    }

    private async Task<int> WriteListAsync(
        bool json,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DiscoveredApp> apps;
        try
        {
            apps = await discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            error.WriteLine(
                $"mitosis: couldn't list installed applications ({ex.GetType().Name}). Run 'mitosis doctor' for local state.");
            return CliExitCode.Failure;
        }

        var assessedAt = clock();
        var rows = apps
            .Select(app => (App: app, Assessment: assessor.Assess(app, assessedAt)))
            .OrderBy(row => row.App.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.App.Publisher, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (json)
        {
            output.WriteLine(JsonSerializer.Serialize(
                new
                {
                    count = rows.Count,
                    applications = rows.Select(row => new
                    {
                        row.App.Id,
                        name = row.App.DisplayName,
                        row.App.Publisher,
                        row.App.Version,
                        scope = row.App.Scope,
                        sources = row.App.Sources.ToArray(),
                        support = row.Assessment.Status,
                        row.App.IsSystemComponent,
                    }).ToArray(),
                },
                JsonOptions));
            return CliExitCode.Success;
        }

        if (rows.Count == 0)
        {
            output.WriteLine("No applications discovered.");
            output.WriteLine("Discovery reads Windows uninstall registrations and packaged-app metadata only.");
            return CliExitCode.Success;
        }

        WriteTable(
            output,
            ("NAME", 40),
            ("PUBLISHER", 28),
            ("VERSION", 16),
            ("SCOPE", 8),
            ("SOURCE", 24),
            ("SUPPORT", 12));
        foreach (var (app, assessment) in rows)
        {
            WriteTable(
                output,
                (app.DisplayName, 40),
                (app.Publisher, 28),
                (app.Version, 16),
                (ScopeLabel(app.Scope), 8),
                (SourceLabel(app.PrimarySource), 24),
                (SupportLabel(assessment.Status), 12));
        }

        output.WriteLine();
        output.WriteLine(rows.Count == 1 ? "1 application." : $"{rows.Count} applications.");
        output.WriteLine("Read-only: Windows uninstall registrations and packaged-app metadata.");
        return CliExitCode.Success;
    }

    private async Task<int> WriteProfilesAsync(
        bool json,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CloneProfile> records;
        try
        {
            records = await profiles.ListAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ProfileStoreException or IOException
            or UnauthorizedAccessException or InvalidProfileException
            or System.Text.Json.JsonException)
        {
            error.WriteLine(
                $"mitosis: couldn't read the profile store ({ex.GetType().Name}). The store was not changed.");
            return CliExitCode.Failure;
        }

        if (json)
        {
            output.WriteLine(JsonSerializer.Serialize(
                new
                {
                    count = records.Count,
                    profiles = records.Select(profile => new
                    {
                        profile.Id,
                        profile.AppId,
                        profile.Name,
                        state = profile.State.ToString().ToLowerInvariant(),
                        profile.BadgeText,
                        profile.BadgeColor,
                        createdAt = profile.CreatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                        updatedAt = profile.UpdatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                    }).ToArray(),
                    note = "Metadata records only; nothing is provisioned by a profile.",
                },
                JsonOptions));
            return CliExitCode.Success;
        }

        if (records.Count == 0)
        {
            output.WriteLine("No clone profiles yet.");
            output.WriteLine("Profiles are metadata only and provision nothing.");
            return CliExitCode.Success;
        }

        WriteTable(
            output,
            ("NAME", 28),
            ("APP", 40),
            ("STATE", 12),
            ("BADGE", 16),
            ("UPDATED (UTC)", 16));
        foreach (var profile in records)
        {
            WriteTable(
                output,
                (profile.Name, 28),
                (profile.AppId, 40),
                (profile.State.ToString().ToLowerInvariant(), 12),
                (BadgeLabel(profile), 16),
                (profile.UpdatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), 16));
        }

        output.WriteLine();
        output.WriteLine(records.Count == 1 ? "1 profile." : $"{records.Count} profiles.");
        output.WriteLine("Metadata records only; none of these is a provisioned or working clone.");
        return CliExitCode.Success;
    }

    private async Task<int> WriteDoctorAsync(
        bool json,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var facts = environment.ToFacts();
        var state = await CollectLocalStateAsync(facts, cancellationToken).ConfigureAwait(false);
        var generatedAt = clock();

        if (json)
        {
            output.WriteLine(JsonSerializer.Serialize(
                new
                {
                    generatedAt = generatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                    application = "Mitosis",
                    applicationVersion = facts.AppVersion,
                    runtime = facts.RuntimeVersion,
                    operatingSystem = facts.OperatingSystem,
                    architecture = facts.Architecture,
                    windowsAppSdk = facts.WindowsAppSdkVersion,
                    dataFolder = DiagnosticRedaction.RedactHome(facts.DataDirectory),
                    dataFolderWritable = facts.DataDirectoryWritable,
                    logFolder = DiagnosticRedaction.RedactHome(facts.LogDirectory),
                    profiles = new
                    {
                        readable = state.ProfileStoreReadable,
                        count = state.ProfileCount,
                        problem = state.ProfileStoreProblem,
                    },
                    logs = new
                    {
                        fileCount = state.LogFileCount,
                        bytes = state.LogBytes,
                        latestEntryUtc = state.LatestLogEntryUtc?.UtcDateTime
                            .ToString("O", CultureInfo.InvariantCulture),
                        problem = state.LogProblem,
                    },
                    discovery = new
                    {
                        count = state.DiscoveredApplicationCount,
                        problem = state.DiscoveryProblem,
                    },
                },
                JsonOptions));
            return CliExitCode.Success;
        }

        output.Write(DoctorReport.Build(facts, homeDirectory: null, generatedAt, state));
        return CliExitCode.Success;
    }

    private async Task<DoctorLocalState> CollectLocalStateAsync(
        DoctorFacts facts,
        CancellationToken cancellationToken)
    {
        var profileStoreReadable = true;
        var profileCount = 0;
        string? profileProblem = null;
        try
        {
            var records = await profiles.ListAsync(cancellationToken).ConfigureAwait(false);
            profileCount = records.Count;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ProfileStoreException or IOException
            or UnauthorizedAccessException or InvalidProfileException
            or System.Text.Json.JsonException)
        {
            profileStoreReadable = false;
            profileProblem = ex.GetType().Name;
        }

        var (logFileCount, logBytes, latestLogEntry, logProblem) = InspectLogs(facts.LogDirectory);

        int? discoveredCount = null;
        string? discoveryProblem = null;
        try
        {
            var apps = await discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
            discoveredCount = apps.Count;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            discoveryProblem = ex.GetType().Name;
        }

        return new DoctorLocalState(
            profileStoreReadable,
            profileCount,
            profileProblem,
            logFileCount,
            logBytes,
            latestLogEntry,
            discoveredCount,
            discoveryProblem,
            logProblem);
    }

    private static (int Count, long Bytes, DateTimeOffset? Latest, string? Problem) InspectLogs(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return (0, 0, null, null);
        }

        try
        {
            var files = new DirectoryInfo(directory)
                .EnumerateFiles("mitosis*.log", SearchOption.TopDirectoryOnly)
                .ToList();
            long bytes = 0;
            DateTimeOffset? latest = null;
            foreach (var file in files)
            {
                bytes += file.Length;
                var written = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);
                if (latest is null || written > latest)
                {
                    latest = written;
                }
            }

            return (files.Count, bytes, latest, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return (0, 0, null, ex.GetType().Name);
        }
    }

    private static void WriteTable(TextWriter output, params (string? Value, int Width)[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            var (value, width) = cells[i];
            var text = OneLine(value);
            if (text.Length > width)
            {
                text = text[..(width - 3)] + "...";
            }

            output.Write(i == cells.Length - 1 ? text : text.PadRight(width + 2));
        }

        output.WriteLine();
    }

    private static string OneLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var text = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
        return text.Length == 0 ? "-" : text;
    }

    private static string ScopeLabel(InstallScope scope) => scope switch
    {
        InstallScope.CurrentUser => "user",
        InstallScope.Machine => "machine",
        _ => "unknown",
    };

    private static string SourceLabel(DiscoverySource source) => source switch
    {
        DiscoverySource.CurrentUserUninstall => "user uninstall",
        DiscoverySource.LocalMachineUninstall32 => "machine uninstall (32)",
        DiscoverySource.LocalMachineUninstall64 => "machine uninstall (64)",
        DiscoverySource.AppxPackage => "packaged app",
        _ => source.ToString(),
    };

    private static string SupportLabel(CompatibilityStatus status) => status switch
    {
        CompatibilityStatus.NeedsReview => "needs review",
        CompatibilityStatus.Unsupported => "unsupported",
        CompatibilityStatus.Supported => "supported",
        _ => "unknown",
    };

    private static string BadgeLabel(CloneProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.BadgeText)
            && !string.IsNullOrWhiteSpace(profile.BadgeColor))
        {
            return $"{profile.BadgeText} {profile.BadgeColor}";
        }

        if (!string.IsNullOrWhiteSpace(profile.BadgeText))
        {
            return profile.BadgeText;
        }

        return string.IsNullOrWhiteSpace(profile.BadgeColor) ? "-" : profile.BadgeColor;
    }
}
