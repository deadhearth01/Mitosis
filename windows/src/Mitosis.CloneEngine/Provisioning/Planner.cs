using Mitosis.Domain;

namespace Mitosis.CloneEngine.Provisioning;

/// <summary>Deterministic operation planner. Pure function of its inputs: no filesystem,
/// registry, network, or process access. Never creates the destination directory.</summary>
public static class OperationPlanner
{
    public static OperationPlan Plan(
        DiscoveredApp app,
        CloneProfile profile,
        CompatibilityAssessment assessment,
        IProvisioningStrategy? strategy,
        ProvisioningContext context)
    {
        var failures = new List<string>();
        var warnings = new List<string>();

        if (strategy == null)
        {
            failures.Add("No provisioning strategy is available.");
            return Refused(app, profile, assessment, context, failures, warnings);
        }

        if (!strategy.IsEnabled)
        {
            failures.Add($"Strategy '{strategy.Id}' is disabled.");
        }

        if (!string.Equals(strategy.Version, context.RequiredStrategyVersion, StringComparison.Ordinal))
        {
            failures.Add(
                $"Strategy version {strategy.Version} does not satisfy required {context.RequiredStrategyVersion}.");
        }

        if (assessment.Status != CompatibilityStatus.Supported
            || !string.Equals(assessment.StrategyId, strategy.Id, StringComparison.Ordinal))
        {
            failures.Add(
                $"Assessment does not permit provisioning (status {assessment.Status}, " +
                $"strategy '{assessment.StrategyId ?? "none"}').");
        }

        if (!strategy.AppliesTo(app))
        {
            failures.Add($"Strategy '{strategy.Id}' does not apply to '{app.DisplayName}'.");
        }

        if (profile.State != CloneProfileState.Planned)
        {
            failures.Add($"Only Planned profiles can be provisioned (profile is {profile.State}).");
        }

        if (!string.Equals(profile.AppId, app.Id, StringComparison.Ordinal))
        {
            failures.Add("Profile is associated with a different application.");
        }

        if (string.IsNullOrWhiteSpace(context.InstanceRoot) || !Path.IsPathRooted(context.InstanceRoot))
        {
            failures.Add("Instance root must be an absolute application-owned directory.");
        }
        else if (!string.Equals(
            Path.GetFullPath(context.InstanceRoot).TrimEnd(Path.DirectorySeparatorChar),
            context.InstanceRoot.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Instance root must be a normalized absolute path.");
        }

        var relative = Path.Combine("instances", profile.Id.ToString("N"));
        if (!DestinationSafety.IsValidRelativePath(relative, out _))
        {
            failures.Add("Planned destination failed safety validation.");
        }
        else if (context.ExistingRelativeDestinations.Any(d =>
            string.Equals(Normalize(d), Normalize(relative), StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add("Destination already exists; refusing to overwrite.");
        }

        var missingPrerequisites = strategy.RequiredPrerequisites
            .Where(p => !context.SatisfiedPrerequisites.Contains(p, StringComparer.Ordinal))
            .ToList();
        failures.AddRange(missingPrerequisites.Select(p => $"Missing prerequisite: {p}."));

        var missingEvidence = context.Evidence.Missing();
        if (missingEvidence.Count > 0)
        {
            failures.Add("Isolation evidence is incomplete.");
            warnings.AddRange(missingEvidence.Select(m => "Unproven: " + m + "."));
        }

        if (app.InstallLocation != null)
        {
            warnings.Add("The install location comes from registration data and must be revalidated at execution.");
        }
        else
        {
            warnings.Add("No install location was discovered; file operations cannot be planned precisely.");
        }

        if (failures.Count > 0 || !strategy.SupportsExecution)
        {
            if (failures.Count == 0)
            {
                failures.Add($"Strategy '{strategy.Id}' supports planning only.");
            }

            return Refused(app, profile, assessment, context, failures, warnings);
        }

        var operations = new List<PlannedOperation>
        {
            new(PlannedOperationKind.CreateInstanceDirectory, null, relative,
                "Create the private instance directory."),
            new(PlannedOperationKind.CopyApplicationFiles, ".", relative,
                "Copy readable application files into the private directory."),
            new(PlannedOperationKind.WriteInstanceManifest, null,
                Path.Combine(relative, "mitosis-instance.json"),
                "Write the instance manifest."),
            new(PlannedOperationKind.WriteLaunchConfig, null,
                Path.Combine(relative, "mitosis-launch.json"),
                "Write the instance launch configuration."),
        };

        foreach (var operation in operations)
        {
            if (operation.DestinationRelativePath != null
                && !DestinationSafety.IsValidRelativePath(operation.DestinationRelativePath, out _))
            {
                failures.Add("Planned operation failed safety validation.");
                return Refused(app, profile, assessment, context, failures, warnings);
            }
        }

        return new OperationPlan(
            PlanId: Guid.NewGuid(),
            StrategyId: strategy.Id,
            StrategyVersion: strategy.Version,
            ProfileId: profile.Id,
            AppId: app.Id,
            InstanceRoot: context.InstanceRoot,
            InstanceRelativePath: relative,
            RequiredPrerequisites: strategy.RequiredPrerequisites.ToList(),
            SatisfiedPrerequisites: context.SatisfiedPrerequisites.ToList(),
            Operations: operations,
            ExpectedOutputs: new[]
            {
                Path.Combine(relative, "mitosis-instance.json"),
                Path.Combine(relative, "mitosis-launch.json"),
            },
            Warnings: warnings,
            Failures: Array.Empty<string>(),
            CanExecute: true,
            CreatedAt: context.Timestamp);
    }

    private static OperationPlan Refused(
        DiscoveredApp app,
        CloneProfile profile,
        CompatibilityAssessment assessment,
        ProvisioningContext context,
        List<string> failures,
        List<string> warnings) => new(
            PlanId: Guid.NewGuid(),
            StrategyId: assessment.StrategyId ?? "none",
            StrategyVersion: context.RequiredStrategyVersion,
            ProfileId: profile.Id,
            AppId: app.Id,
            InstanceRoot: context.InstanceRoot,
            InstanceRelativePath: Path.Combine("instances", profile.Id.ToString("N")),
            RequiredPrerequisites: Array.Empty<string>(),
            SatisfiedPrerequisites: context.SatisfiedPrerequisites.ToList(),
            Operations: Array.Empty<PlannedOperation>(),
            ExpectedOutputs: Array.Empty<string>(),
            Warnings: warnings,
            Failures: failures,
            CanExecute: false,
            CreatedAt: context.Timestamp);

    private static string Normalize(string relative) =>
        relative.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar);
}
