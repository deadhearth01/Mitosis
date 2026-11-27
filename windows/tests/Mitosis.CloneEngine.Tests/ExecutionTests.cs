using System.Text.Json;
using Mitosis.CloneEngine.Provisioning;
using Mitosis.Domain;

namespace Mitosis.CloneEngine.Tests;

/// <summary>A disposable directory tree under the user's temp path. Deletion clears
/// read-only attributes first so a copied-then-read-only file cannot strand the tree.</summary>
internal sealed class TempTree : IDisposable
{
    private TempTree(string root)
    {
        Root = root;
        Directory.CreateDirectory(root);
    }

    public string Root { get; }

    public static TempTree Create() =>
        new(Path.Combine(Path.GetTempPath(), "mitosis-exec-" + Guid.NewGuid().ToString("N")));

    public string At(params string[] parts)
    {
        var path = parts.Aggregate(Root, Path.Combine);
        return path;
    }

    public string Dir(params string[] parts)
    {
        var path = At(parts);
        System.IO.Directory.CreateDirectory(path);
        return path;
    }

    public string WriteFile(string relativePath, string content)
    {
        var path = At(relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var parent = System.IO.Path.GetDirectoryName(path);
        if (parent != null)
        {
            System.IO.Directory.CreateDirectory(parent);
        }

        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        try
        {
            foreach (var file in new DirectoryInfo(Root).EnumerateFiles("*", new EnumerationOptions
                     {
                         RecurseSubdirectories = true,
                         AttributesToSkip = 0,
                         IgnoreInaccessible = true,
                     }))
            {
                file.Attributes = FileAttributes.Normal;
            }

            Directory.Delete(Root, recursive: true);
        }
        catch
        {
            // Test cleanup is best effort; the OS temp cleaner owns the remainder.
        }
    }
}

internal static class ExecutionFixtures
{
    public const string StrategyId = "probe";

    public static DiscoveredApp AppFor(string installLocation, string name = "Probe") => new(
        Id: AppIdentity.Create(DiscoverySource.CurrentUserUninstall, name, "Probe Publisher"),
        DisplayName: name,
        Publisher: "Probe Publisher",
        Version: "1.0",
        InstallLocation: installLocation,
        ExecutablePath: null,
        PrimarySource: DiscoverySource.CurrentUserUninstall,
        Sources: new[] { DiscoverySource.CurrentUserUninstall },
        Scope: InstallScope.CurrentUser,
        IsSystemComponent: false,
        IconReference: null,
        Warnings: Array.Empty<string>());

    public static (OperationPlan Plan, FakeStrategy Strategy) Plan(
        DiscoveredApp app,
        string instanceRoot,
        CloneProfile? profile = null)
    {
        var strategy = new FakeStrategy { Id = StrategyId };
        var plan = OperationPlanner.Plan(
            app,
            profile ?? CloneProfile.Create(app.Id, "Probe (Work)"),
            PlanningFixtures.SupportedAssessment(StrategyId),
            strategy,
            PlanningFixtures.Context(root: instanceRoot));
        return (plan, strategy);
    }

    public static string Destination(OperationPlan plan) =>
        Path.Combine(plan.InstanceRoot, plan.InstanceRelativePath);

    public static string ComSpec =>
        Environment.GetEnvironmentVariable("ComSpec")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
}

[TestClass]
public sealed class CopyProvisionExecutorTests
{
    [TestMethod]
    public async Task Execute_CopiesFilesAndWritesManifestAndLaunchConfig()
    {
        using var temp = TempTree.Create();
        var source = temp.Dir("app");
        temp.WriteFile(@"app\probe.txt", "hello");
        temp.WriteFile(@"app\sub\data.bin", "payload");
        var instanceRoot = temp.Dir("root");

        var app = ExecutionFixtures.AppFor(source);
        var (plan, _) = ExecutionFixtures.Plan(app, instanceRoot);
        Assert.IsTrue(plan.CanExecute, string.Join("; ", plan.Failures));

        var executor = new CopyProvisionExecutor(new LaunchSpecification("probe.txt", Array.Empty<string>()));
        var result = await executor.ExecuteAsync(plan, null);

        Assert.IsTrue(result.Admitted, string.Join("; ", result.Errors));
        Assert.IsNotNull(result.Outputs);
        var destination = ExecutionFixtures.Destination(plan);
        Assert.IsTrue(Directory.Exists(destination));
        Assert.AreEqual("hello", File.ReadAllText(Path.Combine(destination, "probe.txt")));
        Assert.AreEqual("payload", File.ReadAllText(Path.Combine(destination, "sub", "data.bin")));
        Assert.IsTrue(Directory.Exists(Path.Combine(destination, "data")));

        var manifest = JsonSerializer.Deserialize<InstanceManifest>(
            File.ReadAllText(Path.Combine(destination, CopyProvisionExecutor.ManifestFileName)),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.IsNotNull(manifest);
        Assert.AreEqual(plan.PlanId, manifest.PlanId);
        Assert.AreEqual(plan.ProfileId, manifest.ProfileId);
        Assert.AreEqual(plan.AppId, manifest.AppId);
        Assert.AreEqual(ExecutionFixtures.StrategyId, manifest.StrategyId);
        Assert.AreEqual(Path.GetFullPath(source), manifest.SourceRoot);

        var config = JsonSerializer.Deserialize<LaunchConfig>(
            File.ReadAllText(Path.Combine(destination, CopyProvisionExecutor.LaunchConfigFileName)),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.IsNotNull(config);
        Assert.AreEqual("probe.txt", config.Executable);
        Assert.AreEqual("data", config.DataDirectory);
    }

    [TestMethod]
    public async Task Execute_MakesCopiedFilesWritable()
    {
        using var temp = TempTree.Create();
        var source = temp.Dir("app");
        var readOnly = temp.WriteFile(@"app\locked.txt", "locked");
        File.SetAttributes(readOnly, FileAttributes.ReadOnly);
        var instanceRoot = temp.Dir("root");

        var app = ExecutionFixtures.AppFor(source);
        var (plan, _) = ExecutionFixtures.Plan(app, instanceRoot);
        var executor = new CopyProvisionExecutor(new LaunchSpecification("locked.txt", Array.Empty<string>()));
        var result = await executor.ExecuteAsync(plan, null);

        Assert.IsTrue(result.Admitted, string.Join("; ", result.Errors));
        var copied = Path.Combine(ExecutionFixtures.Destination(plan), "locked.txt");
        Assert.AreEqual(FileAttributes.Normal, File.GetAttributes(copied) & ~FileAttributes.Archive);
        Assert.AreEqual(FileAttributes.ReadOnly, File.GetAttributes(readOnly) & FileAttributes.ReadOnly);
    }

    [TestMethod]
    public async Task Execute_RefusesExistingDestination()
    {
        using var temp = TempTree.Create();
        var source = temp.Dir("app");
        temp.WriteFile(@"app\probe.txt", "hello");
        var instanceRoot = temp.Dir("root");

        var app = ExecutionFixtures.AppFor(source);
        var (plan, _) = ExecutionFixtures.Plan(app, instanceRoot);
        var destination = ExecutionFixtures.Destination(plan);
        Directory.CreateDirectory(destination);
        var sentinel = Path.Combine(destination, "keep.txt");
        File.WriteAllText(sentinel, "mine");

        var executor = new CopyProvisionExecutor(new LaunchSpecification("probe.txt", Array.Empty<string>()));
        var result = await executor.ExecuteAsync(plan, null);

        Assert.IsFalse(result.Admitted);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("already exists", StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual("mine", File.ReadAllText(sentinel));
        Assert.IsFalse(File.Exists(Path.Combine(destination, "probe.txt")));
    }

    [TestMethod]
    public async Task Execute_RefusesMissingInstallLocation()
    {
        using var temp = TempTree.Create();
        var instanceRoot = temp.Dir("root");
        var app = ExecutionFixtures.AppFor(installLocation: null!);
        var (plan, _) = ExecutionFixtures.Plan(app, instanceRoot);
        Assert.IsTrue(plan.CanExecute, string.Join("; ", plan.Failures));

        var executor = new CopyProvisionExecutor(new LaunchSpecification("probe.txt", Array.Empty<string>()));
        var result = await executor.ExecuteAsync(plan, null);

        Assert.IsFalse(result.Admitted);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("install location", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(Directory.Exists(ExecutionFixtures.Destination(plan)));
    }

    [TestMethod]
    public async Task Execute_RefusesReparsePointInsideSource()
    {
        using var temp = TempTree.Create();
        var source = temp.Dir("app");
        temp.WriteFile(@"app\probe.txt", "hello");
        var outside = temp.Dir("outside");
        var link = Path.Combine(source, "linked");
        var created = RunMklink(link, outside);
        if (!created)
        {
            Assert.Inconclusive("This environment cannot create a directory junction.");
        }

        var instanceRoot = temp.Dir("root");
        var app = ExecutionFixtures.AppFor(source);
        var (plan, _) = ExecutionFixtures.Plan(app, instanceRoot);
        var executor = new CopyProvisionExecutor(new LaunchSpecification("probe.txt", Array.Empty<string>()));
        var result = await executor.ExecuteAsync(plan, null);

        Assert.IsFalse(result.Admitted);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("reparse point", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(Directory.Exists(ExecutionFixtures.Destination(plan)));
    }

    [TestMethod]
    public async Task Execute_RefusesUnadmittedPlan()
    {
        using var temp = TempTree.Create();
        var source = temp.Dir("app");
        temp.WriteFile(@"app\probe.txt", "hello");
        var instanceRoot = temp.Dir("root");

        var app = ExecutionFixtures.AppFor(source);
        var strategy = new FakeStrategy { Id = ExecutionFixtures.StrategyId };
        var plan = OperationPlanner.Plan(
            app,
            CloneProfile.Create(app.Id, "Probe (Work)"),
            PlanningFixtures.SupportedAssessment(ExecutionFixtures.StrategyId),
            strategy,
            PlanningFixtures.Context(root: instanceRoot, evidence: IsolationEvidence.None));
        Assert.IsFalse(plan.CanExecute);

        var executor = new CopyProvisionExecutor(new LaunchSpecification("probe.txt", Array.Empty<string>()));
        var result = await executor.ExecuteAsync(plan, null);

        Assert.IsFalse(result.Admitted);
        Assert.IsTrue(result.Errors.Any(e => e.Contains("not admitted", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(Directory.Exists(ExecutionFixtures.Destination(plan)));
    }

    private static bool RunMklink(string link, string target)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                ArgumentList = { "/c", "mklink", "/J", link, target },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process == null)
            {
                return false;
            }

            process.WaitForExit(10_000);
            return process.ExitCode == 0 && Directory.Exists(link);
        }
        catch
        {
            return false;
        }
    }
}

[TestClass]
public sealed class InstanceLauncherTests
{
    [TestMethod]
    public async Task Launch_RunsExecutableFromTheInstanceAndSubstitutesPlaceholders()
    {
        using var temp = TempTree.Create();
        var source = temp.Dir("app");
        File.Copy(ExecutionFixtures.ComSpec, Path.Combine(source, "cmd.exe"));
        var instanceRoot = temp.Dir("root");

        var app = ExecutionFixtures.AppFor(source);
        var (plan, _) = ExecutionFixtures.Plan(app, instanceRoot);
        var executor = new CopyProvisionExecutor(new LaunchSpecification(
            "cmd.exe",
            new[] { "/c", "echo {dataDirectory}> data-path.txt" }));
        var result = await executor.ExecuteAsync(plan, null);
        Assert.IsTrue(result.Admitted, string.Join("; ", result.Errors));

        var destination = ExecutionFixtures.Destination(plan);
        var outcome = await InstanceLauncher.LaunchAsync(destination);

        Assert.IsTrue(outcome.Started, outcome.Error);
        Assert.AreEqual(0, outcome.ExitCode);
        var written = File.ReadAllText(Path.Combine(destination, "data-path.txt")).Trim();
        Assert.AreEqual(Path.Combine(destination, "data"), written);
    }

    [TestMethod]
    public async Task Launch_DrainsOutputBeyondTheCaptureCap()
    {
        using var temp = TempTree.Create();
        var source = temp.Dir("app");
        File.Copy(ExecutionFixtures.ComSpec, Path.Combine(source, "cmd.exe"));
        // 200 KiB of text: past the 64 KiB capture cap and past a pipe buffer, so a
        // reader that stops at the cap leaves the child blocked on a full pipe.
        temp.WriteFile("app\\big.txt", new string('x', 200 * 1024));
        var instanceRoot = temp.Dir("root");

        var app = ExecutionFixtures.AppFor(source);
        var (plan, _) = ExecutionFixtures.Plan(app, instanceRoot);
        var executor = new CopyProvisionExecutor(new LaunchSpecification(
            "cmd.exe",
            new[] { "/c", "type big.txt" }));
        var result = await executor.ExecuteAsync(plan, null);
        Assert.IsTrue(result.Admitted, string.Join("; ", result.Errors));

        var outcome = await InstanceLauncher.LaunchAsync(
            ExecutionFixtures.Destination(plan), timeout: TimeSpan.FromSeconds(20));

        Assert.IsFalse(outcome.TimedOut, "A chatty child must not stall on a full pipe.");
        Assert.AreEqual(0, outcome.ExitCode, outcome.StandardError);
        Assert.AreEqual(InstanceLauncher.MaxCapturedCharacters, outcome.StandardOutput.Length);
    }

    [TestMethod]
    public async Task Launch_RefusesWhenTheDataDirectoryNameIsAFile()
    {
        using var temp = TempTree.Create();
        var source = temp.Dir("app");
        File.Copy(ExecutionFixtures.ComSpec, Path.Combine(source, "cmd.exe"));
        var instanceRoot = temp.Dir("root");

        var app = ExecutionFixtures.AppFor(source);
        var (plan, _) = ExecutionFixtures.Plan(app, instanceRoot);
        var executor = new CopyProvisionExecutor(new LaunchSpecification(
            "cmd.exe",
            Array.Empty<string>()));
        var result = await executor.ExecuteAsync(plan, null);
        Assert.IsTrue(result.Admitted, string.Join("; ", result.Errors));

        // Replace the private data directory with a file of the same name: the launcher
        // must refuse with a message instead of throwing out of the call.
        var destination = ExecutionFixtures.Destination(plan);
        var dataDirectory = Path.Combine(destination, "data");
        Directory.Delete(dataDirectory, recursive: true);
        File.WriteAllText(dataDirectory, "not a directory");

        var outcome = await InstanceLauncher.LaunchAsync(destination);

        Assert.IsFalse(outcome.Started);
        Assert.IsNotNull(outcome.Error);
        Assert.IsTrue(
            outcome.Error.Contains("data directory", StringComparison.OrdinalIgnoreCase),
            outcome.Error);
    }

    [TestMethod]
    public async Task Launch_RefusesExecutableOutsideTheInstance()
    {
        using var temp = TempTree.Create();
        var instance = temp.Dir("instance");
        var outside = temp.WriteFile(@"outside.exe", "not an executable");
        var config = new LaunchConfig(1, Path.GetRelativePath(instance, outside), Array.Empty<string>(), "data", ".");
        File.WriteAllText(
            Path.Combine(instance, CopyProvisionExecutor.LaunchConfigFileName),
            JsonSerializer.Serialize(config));

        var outcome = await InstanceLauncher.LaunchAsync(instance);

        Assert.IsFalse(outcome.Started);
        Assert.IsNotNull(outcome.Error);
        Assert.IsTrue(
            outcome.Error.Contains("not inside", StringComparison.OrdinalIgnoreCase)
            || outcome.Error.Contains("escapes", StringComparison.OrdinalIgnoreCase),
            outcome.Error);
    }

    [TestMethod]
    public async Task Launch_RefusesMissingLaunchConfig()
    {
        using var temp = TempTree.Create();
        var instance = temp.Dir("instance");
        var outcome = await InstanceLauncher.LaunchAsync(instance);
        Assert.IsFalse(outcome.Started);
        Assert.IsTrue(outcome.Error!.Contains("no launch configuration", StringComparison.OrdinalIgnoreCase));
    }
}

[TestClass]
public sealed class ProvisioningIsolationProbeTests
{
    /// <summary>End-to-end probe: the same install directory is provisioned twice with
    /// different profiles, both copies launch from their own instance directory, and each
    /// writes only into its own private tree. The probe executable is the system command
    /// processor, not a third-party application, so no installed app is copied or run.</summary>
    [TestMethod]
    public async Task TwoProfiles_CopyLaunchAndKeepWritableDataSeparate()
    {
        using var temp = TempTree.Create();
        var source = temp.Dir("app");
        File.Copy(ExecutionFixtures.ComSpec, Path.Combine(source, "cmd.exe"));
        var instanceRoot = temp.Dir("root");

        var app = ExecutionFixtures.AppFor(source);
        var executor = new CopyProvisionExecutor(new LaunchSpecification(
            "cmd.exe",
            new[] { "/c", "echo %CD%> marker.txt" }));

        var outcomes = new List<(OperationPlan Plan, string Destination, string Marker)>();
        foreach (var label in new[] { "Probe (Work)", "Probe (Personal)" })
        {
            var profile = CloneProfile.Create(app.Id, label);
            var (plan, _) = ExecutionFixtures.Plan(app, instanceRoot, profile);
            var result = await executor.ExecuteAsync(plan, null);
            Assert.IsTrue(result.Admitted, string.Join("; ", result.Errors));

            var destination = ExecutionFixtures.Destination(plan);
            var launch = await InstanceLauncher.LaunchAsync(destination);
            Assert.IsTrue(launch.Started, launch.Error);
            Assert.AreEqual(0, launch.ExitCode, launch.StandardError);

            var marker = Path.Combine(destination, "marker.txt");
            Assert.IsTrue(File.Exists(marker), "The probe did not write into its instance directory.");
            outcomes.Add((plan, destination, File.ReadAllText(marker).Trim()));
        }

        Assert.AreEqual(2, outcomes.Count);
        Assert.AreNotEqual(outcomes[0].Destination, outcomes[1].Destination);
        Assert.AreEqual(outcomes[0].Destination, outcomes[0].Marker);
        Assert.AreEqual(outcomes[1].Destination, outcomes[1].Marker);
        Assert.AreNotEqual(outcomes[0].Marker, outcomes[1].Marker);
        Assert.AreNotEqual(outcomes[0].Plan.ProfileId, outcomes[1].Plan.ProfileId);
        Assert.IsFalse(File.Exists(Path.Combine(outcomes[0].Destination, "instances")));
    }
}
