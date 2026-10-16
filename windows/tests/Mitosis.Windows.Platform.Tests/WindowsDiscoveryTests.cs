using Microsoft.Win32;
using Mitosis.Domain;
using Mitosis.Windows.Platform.Discovery;

namespace Mitosis.Windows.Platform.Tests;

internal sealed class FakeRegistryReader : IRegistryReader
{
    private readonly Dictionary<string, Dictionary<string, object?>> keys = new(StringComparer.OrdinalIgnoreCase);

    public List<string> AccessedPaths { get; } = new();

    public void AddKey(string path, Dictionary<string, object?> values) => keys[path] = values;

    public IReadOnlyList<string> GetSubKeyNames(RegistryHive hive, RegistryView view, string path)
    {
        AccessedPaths.Add(path);
        var prefix = path + "\\";
        return keys.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && k.Substring(prefix.Length).IndexOf('\\') < 0)
            .Select(k => k.Substring(prefix.Length))
            .ToList();
    }

    public IReadOnlyDictionary<string, object?> GetValues(RegistryHive hive, RegistryView view, string path)
    {
        AccessedPaths.Add(path);
        return keys.TryGetValue(path, out var values) ? values : new Dictionary<string, object?>();
    }
}

internal sealed class FakeAppxEnumerator : IAppxEnumerator
{
    public List<AppxEntry> Entries { get; } = new();

    public IEnumerable<AppxEntry> Enumerate() => Entries;
}

internal sealed class FakeSource : IInstalledAppSource
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<RawAppRecord>>> collect;

    public FakeSource(string name, Func<CancellationToken, Task<IReadOnlyList<RawAppRecord>>> collect)
    {
        Name = name;
        this.collect = collect;
    }

    public string Name { get; }

    public int Calls { get; private set; }

    public Task<IReadOnlyList<RawAppRecord>> CollectAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return collect(cancellationToken);
    }

    public static FakeSource Returning(params RawAppRecord[] records) =>
        new("fake", _ => Task.FromResult<IReadOnlyList<RawAppRecord>>(records));

    public static FakeSource Throwing(Exception exception) =>
        new("failing", _ => Task.FromException<IReadOnlyList<RawAppRecord>>(exception));
}

internal static class Records
{
    public static RawAppRecord Make(
        string name,
        string? publisher = "Publisher",
        DiscoverySource source = DiscoverySource.CurrentUserUninstall,
        InstallScope scope = InstallScope.CurrentUser,
        string? version = "1.0") => new(
            DisplayName: name,
            Publisher: publisher,
            Version: version,
            InstallLocation: null,
            Source: source,
            Scope: scope,
            IsSystemComponent: false,
            IconReference: null,
            Warnings: Array.Empty<string>());
}

[TestClass]
public sealed class UninstallRegistrySourceTests
{
    private const string Base = UninstallRegistrySource.UninstallPath;

    private static UninstallRegistrySource Create(IRegistryReader reader) => new(
        RegistryHive.CurrentUser, RegistryView.Default,
        DiscoverySource.CurrentUserUninstall, InstallScope.CurrentUser, reader);

    [TestMethod]
    public async Task MissingBaseKey_YieldsEmpty()
    {
        var source = Create(new FakeRegistryReader());

        var records = await source.CollectAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, records.Count);
    }

    [TestMethod]
    public async Task SkipsNamelessSystemAndChildEntries()
    {
        var reader = new FakeRegistryReader();
        reader.AddKey(Base + "\\good", new() { ["DisplayName"] = "Good App", ["DisplayVersion"] = "2.0" });
        reader.AddKey(Base + "\\noname", new() { ["Publisher"] = "Nobody" });
        reader.AddKey(Base + "\\sys", new() { ["DisplayName"] = "Helper", ["SystemComponent"] = 1 });
        reader.AddKey(Base + "\\child", new() { ["DisplayName"] = "Patch", ["ParentKeyName"] = "good" });
        var source = Create(reader);

        var records = await source.CollectAsync(TestContext.CancellationToken);

        var names = records.Select(r => r.DisplayName).ToList();
        CollectionAssert.AreEqual(new[] { "Good App" }, names);
        Assert.AreEqual("2.0", records[0].Version);
    }

    [TestMethod]
    public async Task MalformedVersion_IsKeptWithWarning()
    {
        var reader = new FakeRegistryReader();
        reader.AddKey(Base + "\\weird", new() { ["DisplayName"] = "Weird", ["DisplayVersion"] = "beta-x" });
        var source = Create(reader);

        var records = await source.CollectAsync(TestContext.CancellationToken);

        Assert.AreEqual("beta-x", records[0].Version);
        Assert.AreEqual(1, records[0].Warnings.Count);
    }

    [TestMethod]
    public async Task OnlyTouchesBaseAndDirectChildren_NeverRecurses()
    {
        var reader = new FakeRegistryReader();
        reader.AddKey(Base + "\\app", new() { ["DisplayName"] = "App" });
        var source = Create(reader);

        await source.CollectAsync(TestContext.CancellationToken);

        foreach (var path in reader.AccessedPaths)
        {
            var rest = path.Substring(Base.Length).TrimStart('\\');
            Assert.IsFalse(rest.Contains('\\'),
                $"Discovery must not recurse below direct entries: {path}");
        }
    }

    [TestMethod]
    public async Task UnreadableEntry_IsSkippedWithoutSinkingTheSource()
    {
        var reader = new ExplodingReader();
        var source = Create(reader);

        var records = await source.CollectAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, records.Count);
    }

    [TestMethod]
    public async Task Cancellation_IsHonored()
    {
        var reader = new FakeRegistryReader();
        reader.AddKey(Base + "\\app", new() { ["DisplayName"] = "App" });
        var source = Create(reader);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => source.CollectAsync(new CancellationToken(canceled: true)));
    }

    public TestContext TestContext { get; set; } = null!;

    private sealed class ExplodingReader : IRegistryReader
    {
        public IReadOnlyList<string> GetSubKeyNames(RegistryHive hive, RegistryView view, string path) =>
            new[] { "broken" };

        public IReadOnlyDictionary<string, object?> GetValues(RegistryHive hive, RegistryView view, string path) =>
            throw new UnauthorizedAccessException("denied");
    }
}

[TestClass]
public sealed class AppxPackageSourceTests
{
    public TestContext TestContext { get; set; } = null!;

    private static AppxEntry Entry(
        string name, string display = "App", bool framework = false, bool resource = false, bool system = false) => new(
            Name: name,
            DisplayName: display,
            Publisher: "CN=Publisher",
            Version: "1.2.3.4",
            InstallLocation: @"C:\Program Files\WindowsApps\pkg",
            IsFramework: framework,
            IsResource: resource,
            IsSystem: system);

    [TestMethod]
    public async Task SkipsFrameworkAndResourcePackages()
    {
        var enumerator = new FakeAppxEnumerator();
        enumerator.Entries.Add(Entry("fw", framework: true));
        enumerator.Entries.Add(Entry("res", resource: true));
        enumerator.Entries.Add(Entry("app", "Real App"));
        var source = new AppxPackageSource(enumerator);

        var records = await source.CollectAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "Real App" }, records.Select(r => r.DisplayName).ToList());
    }

    [TestMethod]
    public async Task FlagsSystemPackagesAndWarnsOnResourceNames()
    {
        var enumerator = new FakeAppxEnumerator();
        enumerator.Entries.Add(Entry("sys", "System Thing", system: true));
        enumerator.Entries.Add(Entry("odd", "ms-resource:AppName"));
        var source = new AppxPackageSource(enumerator);

        var records = await source.CollectAsync(TestContext.CancellationToken);
        var byName = records.ToDictionary(r => r.DisplayName);

        Assert.IsTrue(byName["System Thing"].IsSystemComponent);
        Assert.AreEqual(DiscoverySource.AppxPackage, byName["System Thing"].Source);
        Assert.AreEqual(1, byName["ms-resource:AppName"].Warnings.Count);
    }

    [TestMethod]
    public async Task Cancellation_IsHonored()
    {
        var enumerator = new FakeAppxEnumerator();
        enumerator.Entries.Add(Entry("app"));
        var source = new AppxPackageSource(enumerator);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => source.CollectAsync(new CancellationToken(canceled: true)));
    }
}

[TestClass]
public sealed class WindowsAppDiscoveryServiceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task EmptySources_YieldEmpty()
    {
        var service = new WindowsAppDiscoveryService(new[] { FakeSource.Returning() });

        var apps = await service.DiscoverAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, apps.Count);
    }

    [TestMethod]
    public async Task Duplicates_MergeDeterministically()
    {
        var service = new WindowsAppDiscoveryService(new[]
        {
            FakeSource.Returning(
                Records.Make("Slack", "Slack Technologies", DiscoverySource.LocalMachineUninstall64, InstallScope.Machine)),
            FakeSource.Returning(
                Records.Make("  SLACK ", "slack technologies", DiscoverySource.CurrentUserUninstall, InstallScope.CurrentUser)),
        });

        var apps = await service.DiscoverAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, apps.Count);
        var app = apps[0];
        CollectionAssert.AreEquivalent(
            new[] { DiscoverySource.LocalMachineUninstall64, DiscoverySource.CurrentUserUninstall },
            app.Sources.ToList());
        Assert.AreEqual(InstallScope.Machine, app.Scope, "Machine scope wins the merge.");
        Assert.AreEqual("Slack", app.DisplayName, "First source in order wins display fields.");
    }

    [TestMethod]
    public async Task Ordering_IsDeterministic()
    {
        var service = new WindowsAppDiscoveryService(new[]
        {
            FakeSource.Returning(Records.Make("Zulu"), Records.Make("alpha"), Records.Make("Mike")),
        });

        var apps = await service.DiscoverAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[] { "alpha", "Mike", "Zulu" },
            apps.Select(a => a.DisplayName).ToList());
    }

    [TestMethod]
    public async Task ExecutablePath_IsNeverInferred()
    {
        var service = new WindowsAppDiscoveryService(new[] { FakeSource.Returning(Records.Make("Tool")) });

        var apps = await service.DiscoverAsync(TestContext.CancellationToken);

        Assert.IsNull(apps[0].ExecutablePath);
    }

    [TestMethod]
    public async Task PartialSourceFailure_DoesNotSinkDiscovery()
    {
        var good = FakeSource.Returning(Records.Make("Survivor"));
        var bad = FakeSource.Throwing(new InvalidOperationException("source down"));
        var service = new WindowsAppDiscoveryService(new IInstalledAppSource[] { bad, good });

        var apps = await service.DiscoverAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "Survivor" }, apps.Select(a => a.DisplayName).ToList());
    }

    [TestMethod]
    public async Task EachSource_IsCollectedExactlyOnce()
    {
        var first = FakeSource.Returning(Records.Make("One"));
        var second = FakeSource.Returning(Records.Make("Two"));
        var service = new WindowsAppDiscoveryService(new IInstalledAppSource[] { first, second });

        await service.DiscoverAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, first.Calls);
        Assert.AreEqual(1, second.Calls, "No repeated discovery per pass.");
    }

    [TestMethod]
    public async Task Cancellation_IsHonored()
    {
        var service = new WindowsAppDiscoveryService(new[] { FakeSource.Returning(Records.Make("One")) });

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => service.DiscoverAsync(new CancellationToken(canceled: true)));
    }
}
