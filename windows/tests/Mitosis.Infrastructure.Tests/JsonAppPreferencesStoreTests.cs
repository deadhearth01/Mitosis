using Mitosis.Application.Preferences;
using Mitosis.Infrastructure.Preferences;

namespace Mitosis.Infrastructure.Tests;

[TestClass]
public sealed class JsonAppPreferencesStoreTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), "mitosis-prefs-" + Guid.NewGuid().ToString("N"));

    private string Dir(params string[] parts) => Path.Combine(new[] { root }.Concat(parts).ToArray());

    private JsonAppPreferencesStore Store(string? dir = null) =>
        new(dir ?? Dir("prefs"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch
        {
            // Best effort cleanup of isolated test directories.
        }
    }

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task MissingFile_YieldsDefaultsWithoutCreatingAnything()
    {
        var store = Store();

        var loaded = await store.LoadAsync(TestContext.CancellationToken);

        Assert.IsFalse(loaded.Onboarded);
        Assert.IsTrue(loaded.ShowTips,
            "macOS registers showTipsKey: true (MitosisRootApp.swift:11).");
        Assert.IsFalse(Directory.Exists(Dir("prefs")), "Reads must not create directories.");
    }

    [TestMethod]
    public async Task SaveLoad_RoundTrip()
    {
        var dir = Dir("prefs");
        var store = Store(dir);
        await store.SaveAsync(
            new AppPreferences { Onboarded = true, ShowTips = false }, TestContext.CancellationToken);

        var loaded = await Store(dir).LoadAsync(TestContext.CancellationToken);

        Assert.AreEqual(new AppPreferences { Onboarded = true, ShowTips = false }, loaded);
    }

    [TestMethod]
    public async Task Save_SerializesTheVersionedDocument()
    {
        var dir = Dir("prefs");
        await Store(dir).SaveAsync(AppPreferences.Default, TestContext.CancellationToken);

        var json = await File.ReadAllTextAsync(
            Path.Combine(dir, "preferences.json"), TestContext.CancellationToken);
        StringAssert.Contains(json, "\"SchemaVersion\": 1");
        StringAssert.Contains(json, "\"Onboarded\": false");
        StringAssert.Contains(json, "\"ShowTips\": true");
    }

    [TestMethod]
    public async Task MissingSchema_IsRejected()
    {
        var dir = Dir("prefs");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(
            Path.Combine(dir, "preferences.json"), "{}", TestContext.CancellationToken);

        var ex = await Assert.ThrowsExactlyAsync<PreferencesStoreException>(
            () => Store(dir).LoadAsync(TestContext.CancellationToken));
        Assert.IsTrue(ex.Message.Contains("0"), ex.Message);
        Assert.IsTrue(ex.Message.Contains("1"), ex.Message);
    }

    [TestMethod]
    public async Task MissingOptionalFields_YieldDefaults()
    {
        var dir = Dir("prefs");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(
            Path.Combine(dir, "preferences.json"), "{ \"schemaVersion\": 1 }", TestContext.CancellationToken);

        var loaded = await Store(dir).LoadAsync(TestContext.CancellationToken);

        Assert.AreEqual(AppPreferences.Default, loaded);
    }

    [TestMethod]
    public async Task UnknownFields_AreTolerated()
    {
        var dir = Dir("prefs");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(
            Path.Combine(dir, "preferences.json"),
            "{ \"schemaVersion\": 1, \"future\": { \"x\": 1 } }",
            TestContext.CancellationToken);

        Assert.AreEqual(AppPreferences.Default, await Store(dir).LoadAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task MalformedJson_IsRejectedWithoutTouchingTheFile()
    {
        var dir = Dir("prefs");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "preferences.json");
        await File.WriteAllTextAsync(path, "{ not json", TestContext.CancellationToken);
        var before = await File.ReadAllBytesAsync(path, TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<PreferencesStoreException>(
            () => Store(dir).LoadAsync(TestContext.CancellationToken));

        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task WrongFieldTypes_AreRejectedWithoutTouchingTheFile()
    {
        var dir = Dir("prefs");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "preferences.json");
        await File.WriteAllTextAsync(
            path, "{ \"schemaVersion\": \"one\", \"onboarded\": false }", TestContext.CancellationToken);
        var before = await File.ReadAllBytesAsync(path, TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<PreferencesStoreException>(
            () => Store(dir).LoadAsync(TestContext.CancellationToken));

        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task UnsupportedSchema_IsRejectedWithoutTouchingTheFile()
    {
        var dir = Dir("prefs");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "preferences.json");
        await File.WriteAllTextAsync(
            path, "{ \"schemaVersion\": 2, \"onboarded\": true }", TestContext.CancellationToken);
        var before = await File.ReadAllBytesAsync(path, TestContext.CancellationToken);

        var ex = await Assert.ThrowsExactlyAsync<PreferencesStoreException>(
            () => Store(dir).LoadAsync(TestContext.CancellationToken));

        StringAssert.Contains(ex.Message, "not supported");
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task EmptyFile_YieldsDefaults()
    {
        var dir = Dir("prefs");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "preferences.json"), "  ", TestContext.CancellationToken);

        var loaded = await Store(dir).LoadAsync(TestContext.CancellationToken);
        Assert.IsFalse(loaded.Onboarded);
        Assert.IsTrue(loaded.ShowTips);
    }

    [TestMethod]
    public async Task ReadFailure_IsActionable()
    {
        var dir = Dir("prefs");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "preferences.json");
        await File.WriteAllTextAsync(
            path, "{ \"schemaVersion\": 1 }", TestContext.CancellationToken);
        using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        var ex = await Assert.ThrowsExactlyAsync<PreferencesStoreException>(
            () => Store(dir).LoadAsync(TestContext.CancellationToken));
        Assert.IsTrue(ex.Message.Contains("Couldn't read"), ex.Message);
    }

    [TestMethod]
    public async Task FailedSave_PreservesExistingDataAndLeavesNoTempFiles()
    {
        var dir = Dir("prefs");
        await Store(dir).SaveAsync(
            new AppPreferences { Onboarded = true, ShowTips = false }, TestContext.CancellationToken);
        var path = Path.Combine(dir, "preferences.json");
        var before = await File.ReadAllBytesAsync(path, TestContext.CancellationToken);
        var store = Store(dir);
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        await Assert.ThrowsExactlyAsync<PreferencesStoreException>(
            () => store.SaveAsync(AppPreferences.Default, TestContext.CancellationToken));

        locked.Dispose();
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path, TestContext.CancellationToken));
        Assert.AreEqual(0, Directory.GetFiles(dir, "*.tmp").Length, "No stray temp files.");
    }

    [TestMethod]
    public async Task SaveOverwritesPreviousDocumentAtomically()
    {
        var dir = Dir("prefs");
        var store = Store(dir);
        await store.SaveAsync(AppPreferences.Default, TestContext.CancellationToken);
        await store.SaveAsync(
            new AppPreferences { Onboarded = false, ShowTips = false }, TestContext.CancellationToken);

        var loaded = await Store(dir).LoadAsync(TestContext.CancellationToken);
        Assert.AreEqual(new AppPreferences { Onboarded = false, ShowTips = false }, loaded);
        CollectionAssert.AreEqual(
            new[] { "preferences.json" },
            Directory.GetFiles(dir).Select(Path.GetFileName).ToList(),
            "One file only: the replaced document, no temp leftovers.");
    }

    [TestMethod]
    public async Task WriteToUnusableLocation_IsActionable()
    {
        Directory.CreateDirectory(root);
        var fileAsDir = Path.Combine(root, "blocker");
        await File.WriteAllTextAsync(fileAsDir, "x", TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<PreferencesStoreException>(
            () => Store(fileAsDir).SaveAsync(AppPreferences.Default, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ConcurrentSaves_DoNotCorruptTheStore()
    {
        var dir = Dir("prefs");
        var store = Store(dir);
        var saves = Enumerable.Range(0, 20).Select(i => store.SaveAsync(
            new AppPreferences { Onboarded = i % 2 == 0, ShowTips = true },
            TestContext.CancellationToken));

        await Task.WhenAll(saves);

        var reloaded = await Store(dir).LoadAsync(TestContext.CancellationToken);
        Assert.IsTrue(reloaded.ShowTips, "Store parses cleanly after concurrent writes.");
        CollectionAssert.AreEqual(
            new[] { "preferences.json" },
            Directory.GetFiles(dir).Select(Path.GetFileName).ToList(),
            "One file only: no half-written or temp leftovers.");
    }

    [TestMethod]
    public async Task Cancellation_IsHonored()
    {
        var store = Store();
        var canceled = new CancellationToken(canceled: true);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.LoadAsync(canceled));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.SaveAsync(AppPreferences.Default, canceled));
    }

    [TestMethod]
    public void Constructor_RejectsEmptyDirectory()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new JsonAppPreferencesStore("  "));
    }
}
