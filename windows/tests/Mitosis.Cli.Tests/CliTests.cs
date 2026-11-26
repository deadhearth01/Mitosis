using System.Text;
using System.Text.Json;
using Mitosis.Application.Discovery;
using Mitosis.Application.Profiles;
using Mitosis.Cli;
using Mitosis.Domain;

namespace Mitosis.Cli.Tests;

[TestClass]
public sealed class CommandLineTests
{
    [TestMethod]
    public void NoArguments_ShowsHelp()
    {
        var parsed = CommandLine.Parse(Array.Empty<string>());

        Assert.IsNull(parsed.Error);
        Assert.AreEqual(CliCommand.Help, parsed.Invocation!.Command);
        Assert.IsFalse(parsed.Invocation.Json);
    }

    [TestMethod]
    [DataRow("help", CliCommand.Help)]
    [DataRow("--help", CliCommand.Help)]
    [DataRow("-h", CliCommand.Help)]
    [DataRow("version", CliCommand.Version)]
    [DataRow("--version", CliCommand.Version)]
    [DataRow("-v", CliCommand.Version)]
    [DataRow("list", CliCommand.List)]
    [DataRow("profiles", CliCommand.Profiles)]
    [DataRow("doctor", CliCommand.Doctor)]
    public void Commands_Parse(string arg, CliCommand expected)
    {
        var parsed = CommandLine.Parse(new[] { arg });

        Assert.IsNull(parsed.Error);
        Assert.AreEqual(expected, parsed.Invocation!.Command);
    }

    [TestMethod]
    public void JsonFlag_IsAcceptedForDataCommands()
    {
        foreach (var command in new[] { "version", "list", "profiles", "doctor" })
        {
            var parsed = CommandLine.Parse(new[] { command, "--json" });

            Assert.IsNull(parsed.Error, command);
            Assert.IsTrue(parsed.Invocation!.Json, command);
        }
    }

    [TestMethod]
    public void JsonFlag_IsCaseInsensitive()
    {
        var parsed = CommandLine.Parse(new[] { "list", "--JSON" });

        Assert.IsNull(parsed.Error);
        Assert.IsTrue(parsed.Invocation!.Json);
    }

    [TestMethod]
    public void UnknownCommand_IsAUsageError()
    {
        var parsed = CommandLine.Parse(new[] { "clone", "Slack" });

        Assert.IsNull(parsed.Invocation);
        StringAssert.Contains(parsed.Error, "Unknown command 'clone'");
        StringAssert.Contains(parsed.Error, "mitosis --help");
    }

    [TestMethod]
    public void UnexpectedArgument_IsAUsageError()
    {
        var parsed = CommandLine.Parse(new[] { "list", "Slack" });

        Assert.IsNull(parsed.Invocation);
        StringAssert.Contains(parsed.Error, "Unexpected argument 'Slack'");
    }

    [TestMethod]
    public void DuplicateJsonFlag_IsAUsageError()
    {
        var parsed = CommandLine.Parse(new[] { "doctor", "--json", "--json" });

        Assert.IsNull(parsed.Invocation);
        StringAssert.Contains(parsed.Error, "more than once");
    }

    [TestMethod]
    public void HelpRejectsArguments()
    {
        var parsed = CommandLine.Parse(new[] { "help", "--json" });

        Assert.IsNull(parsed.Invocation);
        StringAssert.Contains(parsed.Error, "Help takes no arguments");
    }
}

[TestClass]
public sealed class CliRunnerTests
{
    [TestMethod]
    public async Task Version_PrintsEnvironmentVersion()
    {
        var (runner, output, error) = CreateRunner(version: "9.9.9-test");

        var exit = await runner.RunAsync(new CliInvocation(CliCommand.Version, Json: false), output, error);

        Assert.AreEqual(CliExitCode.Success, exit);
        StringAssert.Contains(output.ToString(), "Mitosis 9.9.9-test");
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task Version_Json_HasExpectedShape()
    {
        var (runner, output, error) = CreateRunner(version: "9.9.9-test");

        var exit = await runner.RunAsync(new CliInvocation(CliCommand.Version, Json: true), output, error);

        Assert.AreEqual(CliExitCode.Success, exit);
        using var document = JsonDocument.Parse(output.ToString());
        Assert.AreEqual("Mitosis", document.RootElement.GetProperty("application").GetString());
        Assert.AreEqual("9.9.9-test", document.RootElement.GetProperty("version").GetString());
    }

    [TestMethod]
    public async Task List_RendersDiscoveredApplications()
    {
        var (runner, output, error) = CreateRunner(apps: new[]
        {
            App("Slack", "Slack Technologies", "4.40.0", DiscoverySource.CurrentUserUninstall),
            App("Notepad", "Microsoft Corporation", "11.0", DiscoverySource.AppxPackage),
        });

        var exit = await runner.RunAsync(new CliInvocation(CliCommand.List, Json: false), output, error);

        var text = output.ToString();
        Assert.AreEqual(CliExitCode.Success, exit);
        StringAssert.Contains(text, "NAME");
        StringAssert.Contains(text, "Slack");
        StringAssert.Contains(text, "Notepad");
        StringAssert.Contains(text, "2 applications.");
        StringAssert.Contains(text, "Read-only");
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task List_Json_HasExpectedShape()
    {
        var (runner, output, error) = CreateRunner(apps: new[]
        {
            App("Slack", "Slack Technologies", "4.40.0", DiscoverySource.CurrentUserUninstall),
        });

        var exit = await runner.RunAsync(new CliInvocation(CliCommand.List, Json: true), output, error);

        Assert.AreEqual(CliExitCode.Success, exit);
        using var document = JsonDocument.Parse(output.ToString());
        Assert.AreEqual(1, document.RootElement.GetProperty("count").GetInt32());
        var app = document.RootElement.GetProperty("applications")[0];
        Assert.AreEqual("Slack", app.GetProperty("name").GetString());
        Assert.AreEqual("currentUser", app.GetProperty("scope").GetString());
        Assert.AreEqual("unknown", app.GetProperty("support").GetString());
        Assert.AreEqual("currentUserUninstall", app.GetProperty("sources")[0].GetString());
    }

    [TestMethod]
    public async Task List_Empty_IsHonest()
    {
        var (runner, output, error) = CreateRunner(apps: Array.Empty<DiscoveredApp>());

        var exit = await runner.RunAsync(new CliInvocation(CliCommand.List, Json: false), output, error);

        Assert.AreEqual(CliExitCode.Success, exit);
        StringAssert.Contains(output.ToString(), "No applications discovered.");
    }

    [TestMethod]
    public async Task List_DiscoveryFailure_ReturnsFailureWithoutPaths()
    {
        var (runner, output, error) = CreateRunner(discoveryFailure: new UnauthorizedAccessException(@"C:\secret\path"));

        var exit = await runner.RunAsync(new CliInvocation(CliCommand.List, Json: false), output, error);

        Assert.AreEqual(CliExitCode.Failure, exit);
        StringAssert.Contains(error.ToString(), "UnauthorizedAccessException");
        Assert.IsFalse(error.ToString().Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(string.Empty, output.ToString());
    }

    [TestMethod]
    public async Task Profiles_RendersMetadataOnlyNotice()
    {
        var profile = CloneProfile.Create("appx:1234", "Slack Work", "W", "blue");
        var (runner, output, error) = CreateRunner(profiles: new[] { profile });

        var exit = await runner.RunAsync(new CliInvocation(CliCommand.Profiles, Json: false), output, error);

        var text = output.ToString();
        Assert.AreEqual(CliExitCode.Success, exit);
        StringAssert.Contains(text, "Slack Work");
        StringAssert.Contains(text, "appx:1234");
        StringAssert.Contains(text, "planned");
        StringAssert.Contains(text, "1 profile.");
        StringAssert.Contains(text, "none of these is a provisioned or working clone");
    }

    [TestMethod]
    public async Task Profiles_Empty_IsHonest()
    {
        var (runner, output, error) = CreateRunner(profiles: Array.Empty<CloneProfile>());

        var exit = await runner.RunAsync(new CliInvocation(CliCommand.Profiles, Json: false), output, error);

        Assert.AreEqual(CliExitCode.Success, exit);
        StringAssert.Contains(output.ToString(), "No clone profiles yet.");
    }

    [TestMethod]
    public async Task Profiles_StoreFailure_ReturnsFailure()
    {
        var (runner, output, error) = CreateRunner(
            profileFailure: new ProfileStoreException("The profile store is damaged."));

        var exit = await runner.RunAsync(new CliInvocation(CliCommand.Profiles, Json: false), output, error);

        Assert.AreEqual(CliExitCode.Failure, exit);
        StringAssert.Contains(error.ToString(), "ProfileStoreException");
        StringAssert.Contains(error.ToString(), "was not changed");
        Assert.AreEqual(string.Empty, output.ToString());
    }

    [TestMethod]
    public async Task Doctor_ReportsLocalState()
    {
        using var temp = new TempTree();
        File.WriteAllText(Path.Combine(temp.Logs, "mitosis.log"), "one entry in a log");

        var (runner, output, error) = CreateRunner(
            apps: new[]
            {
                App("Slack", "Slack Technologies", "4.40.0", DiscoverySource.CurrentUserUninstall),
                App("Notepad", "Microsoft Corporation", "11.0", DiscoverySource.AppxPackage),
                App("Paint", "Microsoft Corporation", "11.0", DiscoverySource.AppxPackage),
            },
            profiles: new[] { CloneProfile.Create("appx:1234", "Slack Work") },
            environment: temp.Environment);

        var exit = await runner.RunAsync(new CliInvocation(CliCommand.Doctor, Json: false), output, error);

        var text = output.ToString();
        Assert.AreEqual(CliExitCode.Success, exit);
        StringAssert.Contains(text, "Mitosis diagnostics");
        StringAssert.Contains(text, "Profiles: 1 (store readable)");
        StringAssert.Contains(text, "Logs: 1 file");
        StringAssert.Contains(text, "Discovered applications: 3");
        StringAssert.Contains(text, "Data folder writable: yes");
        StringAssert.Contains(text, "Not recorded: application data, credentials, file contents, or network activity.");
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task Doctor_UnreadableStoreAndDiscovery_AreReportedWithoutFailing()
    {
        var (runner, output, error) = CreateRunner(
            profileFailure: new ProfileStoreException("damaged"),
            discoveryFailure: new UnauthorizedAccessException());

        var exit = await runner.RunAsync(new CliInvocation(CliCommand.Doctor, Json: false), output, error);

        var text = output.ToString();
        Assert.AreEqual(CliExitCode.Success, exit);
        StringAssert.Contains(text, "Profiles: unreadable (ProfileStoreException)");
        StringAssert.Contains(text, "Discovered applications: unavailable (UnauthorizedAccessException)");
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task Doctor_Json_HasExpectedShape()
    {
        using var temp = new TempTree();
        var (runner, output, error) = CreateRunner(
            apps: new[] { App("Slack", "Slack Technologies", "4.40.0", DiscoverySource.CurrentUserUninstall) },
            profiles: new[] { CloneProfile.Create("appx:1234", "Slack Work") },
            environment: temp.Environment);

        var exit = await runner.RunAsync(new CliInvocation(CliCommand.Doctor, Json: true), output, error);

        Assert.AreEqual(CliExitCode.Success, exit);
        using var document = JsonDocument.Parse(output.ToString());
        var root = document.RootElement;
        Assert.AreEqual(1, root.GetProperty("profiles").GetProperty("count").GetInt32());
        Assert.IsTrue(root.GetProperty("profiles").GetProperty("readable").GetBoolean());
        Assert.AreEqual(1, root.GetProperty("discovery").GetProperty("count").GetInt32());
        Assert.AreEqual(0, root.GetProperty("logs").GetProperty("fileCount").GetInt32());
        Assert.IsFalse(string.IsNullOrWhiteSpace(root.GetProperty("generatedAt").GetString()));
    }

    [TestMethod]
    public async Task Doctor_DoesNotCreateOrModifyTheProfileStore()
    {
        using var temp = new TempTree();
        var store = new Mitosis.Infrastructure.Profiles.JsonCloneProfileRepository(temp.Profiles);
        var runner = new CliRunner(
            new FakeDiscovery(Array.Empty<DiscoveredApp>(), null),
            store,
            new Mitosis.Compatibility.CompatibilityAssessor(Array.Empty<CompatibilityStrategy>()),
            temp.Environment);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await runner.RunAsync(new CliInvocation(CliCommand.Doctor, Json: false), output, error);

        Assert.AreEqual(CliExitCode.Success, exit);
        Assert.IsFalse(File.Exists(Path.Combine(temp.Profiles, "profiles.json")),
            "A read-only command must not create the profile store.");
        StringAssert.Contains(output.ToString(), "Profiles: 0 (store readable)");
    }

    [TestMethod]
    public async Task Doctor_DoesNotCreateTheDataFolderWhenItIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "mitosis-cli-tests", Guid.NewGuid().ToString("N"));
        var data = Path.Combine(root, "Mitosis");
        try
        {
            var environment = new CliEnvironment(
                "0.2.2", ".NET 10", "Windows", "X64", data, Path.Combine(data, "Logs"));
            var runner = new CliRunner(
                new FakeDiscovery(Array.Empty<DiscoveredApp>(), null),
                new FakeProfiles(Array.Empty<CloneProfile>(), null),
                new Mitosis.Compatibility.CompatibilityAssessor(Array.Empty<CompatibilityStrategy>()),
                environment);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exit = await runner.RunAsync(new CliInvocation(CliCommand.Doctor, Json: false), output, error);

            Assert.AreEqual(CliExitCode.Success, exit);
            StringAssert.Contains(output.ToString(), "Data folder writable: yes");
            Assert.IsFalse(Directory.Exists(data),
                "A read-only command must not create the Mitosis data folder.");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task Doctor_UnusableDataFolder_IsReportedWithoutCrashing()
    {
        var environment = new CliEnvironment(
            "0.2.2", ".NET 10", "Windows", "X64",
            "\0invalid\0",
            "\0invalid\0");
        var runner = new CliRunner(
            new FakeDiscovery(Array.Empty<DiscoveredApp>(), null),
            new FakeProfiles(Array.Empty<CloneProfile>(), null),
            new Mitosis.Compatibility.CompatibilityAssessor(Array.Empty<CompatibilityStrategy>()),
            environment);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await runner.RunAsync(new CliInvocation(CliCommand.Doctor, Json: false), output, error);

        Assert.AreEqual(CliExitCode.Success, exit);
        StringAssert.Contains(output.ToString(), "Data folder writable: no");
        StringAssert.Contains(output.ToString(), "Logs: none yet");
    }

    private static (CliRunner Runner, StringWriter Output, StringWriter Error) CreateRunner(
        string version = "0.2.2",
        IReadOnlyList<DiscoveredApp>? apps = null,
        IReadOnlyList<CloneProfile>? profiles = null,
        Exception? discoveryFailure = null,
        Exception? profileFailure = null,
        CliEnvironment? environment = null)
    {
        environment ??= new CliEnvironment(
            version, ".NET 10", "Windows", "X64",
            Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "MitosisLogs"));

        var runner = new CliRunner(
            new FakeDiscovery(apps ?? Array.Empty<DiscoveredApp>(), discoveryFailure),
            new FakeProfiles(profiles ?? Array.Empty<CloneProfile>(), profileFailure),
            new Mitosis.Compatibility.CompatibilityAssessor(Array.Empty<CompatibilityStrategy>()),
            environment,
            () => new DateTimeOffset(2026, 11, 26, 10, 0, 0, TimeSpan.Zero));

        return (runner, new StringWriter(), new StringWriter());
    }

    private static DiscoveredApp App(
        string name,
        string publisher,
        string version,
        DiscoverySource source) => new(
        Id: AppIdentity.Create(source, name, publisher),
        DisplayName: name,
        Publisher: publisher,
        Version: version,
        InstallLocation: null,
        ExecutablePath: null,
        PrimarySource: source,
        Sources: new[] { source },
        Scope: InstallScope.CurrentUser,
        IsSystemComponent: false,
        IconReference: null,
        Warnings: Array.Empty<string>());

    private sealed class FakeDiscovery : IAppDiscoveryService
    {
        private readonly IReadOnlyList<DiscoveredApp> apps;
        private readonly Exception? failure;

        public FakeDiscovery(IReadOnlyList<DiscoveredApp> apps, Exception? failure)
        {
            this.apps = apps;
            this.failure = failure;
        }

        public Task<IReadOnlyList<DiscoveredApp>> DiscoverAsync(CancellationToken cancellationToken = default) =>
            failure is null
                ? Task.FromResult(apps)
                : Task.FromException<IReadOnlyList<DiscoveredApp>>(failure);
    }

    private sealed class FakeProfiles : ICloneProfileRepository
    {
        private readonly IReadOnlyList<CloneProfile> records;
        private readonly Exception? failure;

        public FakeProfiles(IReadOnlyList<CloneProfile> records, Exception? failure)
        {
            this.records = records;
            this.failure = failure;
        }

        public Task<IReadOnlyList<CloneProfile>> ListAsync(CancellationToken cancellationToken = default) =>
            failure is null
                ? Task.FromResult(records)
                : Task.FromException<IReadOnlyList<CloneProfile>>(failure);

        public Task<CloneProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The read-only CLI never reads a single profile.");

        public Task<CloneProfile> AddAsync(CloneProfile profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The read-only CLI never writes profiles.");

        public Task<CloneProfile> UpdateAsync(CloneProfile profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The read-only CLI never writes profiles.");

        public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The read-only CLI never removes profiles.");
    }

    private sealed class TempTree : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(), "mitosis-cli-tests", Guid.NewGuid().ToString("N"));

        public TempTree()
        {
            Data = Path.Combine(root, "Mitosis");
            Profiles = Path.Combine(Data, "Profiles");
            Logs = Path.Combine(Data, "Logs");
            Directory.CreateDirectory(Logs);
            Environment = new CliEnvironment("0.2.2", ".NET 10", "Windows", "X64", Data, Logs);
        }

        public string Data { get; }

        public string Profiles { get; }

        public string Logs { get; }

        public CliEnvironment Environment { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
