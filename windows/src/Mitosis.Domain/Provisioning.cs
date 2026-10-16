namespace Mitosis.Domain;

/// <summary>Evidence required before any application may be considered for an independent
/// instance. Unknown stays unknown: a successful directory copy is not proof of isolation,
/// and no field here is ever inferred from names, publishers, or install paths.</summary>
public sealed record IsolationEvidence(
    bool InstallFilesReadable,
    bool CopyPermitted,
    bool LaunchFromPrivateLocation,
    bool WritableDataIsolated,
    bool RegistryStateIsolated,
    bool UpdatesPreserveInstance,
    bool RuntimesAvailable,
    bool LicensingPermits)
{
    public static IsolationEvidence None { get; } = new(
        false, false, false, false, false, false, false, false);

    public bool IsComplete =>
        InstallFilesReadable && CopyPermitted && LaunchFromPrivateLocation
        && WritableDataIsolated && RegistryStateIsolated && UpdatesPreserveInstance
        && RuntimesAvailable && LicensingPermits;

    public IReadOnlyList<string> Missing() => new (string Name, bool Present)[]
        {
            ("Install files can be read", InstallFilesReadable),
            ("Files may legally and technically be copied", CopyPermitted),
            ("The application launches from a private location", LaunchFromPrivateLocation),
            ("Writable application data is isolated", WritableDataIsolated),
            ("Registry and configuration state is isolated", RegistryStateIsolated),
            ("Updates do not overwrite the private instance", UpdatesPreserveInstance),
            ("Required runtimes and dependencies are available", RuntimesAvailable),
            ("Licensing and package restrictions permit the operation", LicensingPermits),
        }
        .Where(e => !e.Present)
        .Select(e => e.Name)
        .ToList();
}

/// <summary>Planned-operation vocabulary. Plans are data: describing an operation never
/// performs it, and this stage executes nothing.</summary>
public enum PlannedOperationKind
{
    CreateInstanceDirectory,
    CopyApplicationFiles,
    WriteInstanceManifest,
    WriteLaunchConfig,
}

/// <summary>One planned step. All paths are relative to the instance root; absolute
/// paths, parent traversal, and reparse-point escapes are rejected by validation.</summary>
public sealed record PlannedOperation(
    PlannedOperationKind Kind,
    string? SourceRelativePath,
    string DestinationRelativePath,
    string Description);

/// <summary>Immutable operation plan. <see cref="CanExecute"/> is true only when every
/// failure is absent, the strategy supports execution, evidence is complete, and all
/// prerequisites are satisfied. Planning never touches the system.</summary>
public sealed record OperationPlan(
    Guid PlanId,
    string StrategyId,
    string StrategyVersion,
    Guid ProfileId,
    string AppId,
    string InstanceRoot,
    string InstanceRelativePath,
    IReadOnlyList<string> RequiredPrerequisites,
    IReadOnlyList<string> SatisfiedPrerequisites,
    IReadOnlyList<PlannedOperation> Operations,
    IReadOnlyList<string> ExpectedOutputs,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Failures,
    bool CanExecute,
    DateTimeOffset CreatedAt);

/// <summary>Inputs to planning. The caller supplies capabilities and evidence; the
/// planner gathers nothing and launches nothing.</summary>
public sealed record ProvisioningContext(
    string InstanceRoot,
    IReadOnlyList<string> SatisfiedPrerequisites,
    IsolationEvidence Evidence,
    IReadOnlyList<string> ExistingRelativeDestinations,
    string RequiredStrategyVersion,
    DateTimeOffset Timestamp);

/// <summary>Lexical destination safety. No filesystem access: reparse-point and
/// existence checks belong to execution time (future stage), which revalidates.</summary>
public static class DestinationSafety
{
    public static bool IsValidRelativePath(string? relativePath, out string failure)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            failure = "Destination must not be empty.";
            return false;
        }

        if (Path.IsPathRooted(relativePath))
        {
            failure = $"Destination must be relative: '{relativePath}'.";
            return false;
        }

        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        var segments = relativePath.Split(separators, StringSplitOptions.None);
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || segment == "." || segment == "..")
            {
                failure = $"Destination escapes its root: '{relativePath}'.";
                return false;
            }

            if (segment.EndsWith(".", StringComparison.Ordinal)
                || segment.EndsWith(" ", StringComparison.Ordinal))
            {
                failure = $"Destination segment is not a valid Windows name: '{segment}'.";
                return false;
            }

            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                failure = $"Destination contains invalid characters: '{segment}'.";
                return false;
            }
        }

        if (relativePath.Length > 200)
        {
            failure = "Destination path is too long.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    public static string CombineChecked(string root, string relativePath)
    {
        if (!IsValidRelativePath(relativePath, out var failure))
        {
            throw new InvalidProfileException(failure);
        }

        return Path.Combine(root, relativePath);
    }
}
