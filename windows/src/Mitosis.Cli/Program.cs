using Microsoft.Win32;
using Mitosis.Compatibility;
using Mitosis.Domain;
using Mitosis.Infrastructure.Profiles;
using Mitosis.Windows.Platform.Discovery;
using Mitosis.Windows.Platform.Profiles;

namespace Mitosis.Cli;

/// <summary>Entry point for the read-only <c>mitosis.exe</c>. Composition mirrors the
/// app's discovery stack so both see the same registrations; nothing here can mutate
/// a profile, preference, application, or application data.</summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var parsed = CommandLine.Parse(args);
        if (parsed.Error is not null || parsed.Invocation is null)
        {
            await Console.Error.WriteLineAsync(parsed.Error ?? CommandLine.HelpHint).ConfigureAwait(false);
            return CliExitCode.Usage;
        }

        if (parsed.Invocation.Command == CliCommand.Help)
        {
            await Console.Out.WriteAsync(HelpText.Value).ConfigureAwait(false);
            return CliExitCode.Success;
        }

        var reader = new RegistryKeyReader();
        var sources = new IInstalledAppSource[]
        {
            new UninstallRegistrySource(
                RegistryHive.CurrentUser, RegistryView.Default,
                DiscoverySource.CurrentUserUninstall, InstallScope.CurrentUser, reader),
            new UninstallRegistrySource(
                RegistryHive.LocalMachine, RegistryView.Registry64,
                DiscoverySource.LocalMachineUninstall64, InstallScope.Machine, reader),
            new UninstallRegistrySource(
                RegistryHive.LocalMachine, RegistryView.Registry32,
                DiscoverySource.LocalMachineUninstall32, InstallScope.Machine, reader),
            new AppxPackageSource(new PackageManagerEnumerator()),
        };

        var runner = new CliRunner(
            new WindowsAppDiscoveryService(sources),
            new JsonCloneProfileRepository(WindowsProfilePaths.ProfileDirectory()),
            new CompatibilityAssessor(Array.Empty<CompatibilityStrategy>()),
            CliEnvironment.Current());

        return await runner.RunAsync(parsed.Invocation, Console.Out, Console.Error).ConfigureAwait(false);
    }
}
