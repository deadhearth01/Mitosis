using Mitosis.Application.Evidence;
using Mitosis.Domain;
using Mitosis.Infrastructure.Evidence;

namespace Mitosis.Infrastructure.Tests;

[TestClass]
public sealed class EvidenceStoreTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), "mitosis-evidence-" + Guid.NewGuid().ToString("N"));

    private JsonEvidenceStore Store(string? dir = null) =>
        new(dir ?? Path.Combine(root, "store"));

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

    private static EvidenceBundle Bundle(string candidate = "documented-profile-flags") => new(
        CandidateId: candidate,
        CandidateVersion: "1",
        AppId: "app:example",
        AppVersion: "1.0",
        RecordId: Guid.NewGuid(),
        SchemaVersion: EvidenceEvaluator.CurrentSchemaVersion,
        VerifiedAtUtc: new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero),
        Verifier: "lab-notebook-7",
        Dimensions: EvidenceEvaluator.RequiredDimensions
            .Select(d => new DimensionEvidence(d, EvidenceStatus.Unknown, null, null, null, null))
            .ToList(),
        Sources: new[] { new EvidenceSourceRef("Vendor docs", "https://example.com/docs", "2026-10-16") },
        TestMethod: "Read the vendor documentation.",
        ReproSteps: "1. Open the URL. 2. Record the claim.",
        PlatformAssumptions: "Windows 11.",
        OutstandingQuestions: new[] { "Mutex behavior." },
        Approval: new ApprovalInfo(ApprovalStatus.NotRequested, null, null),
        IntegrityHash: null);

    [TestMethod]
    public async Task MissingDirectory_YieldsEmptyWithoutCreatingAnything()
    {
        var store = Store();

        Assert.AreEqual(0, (await store.ListAsync(TestContext.CancellationToken)).Count);
        Assert.IsFalse(Directory.Exists(Path.Combine(root, "store")));
    }

    [TestMethod]
    public async Task SaveListGet_RoundTripWithIntegrityHash()
    {
        var store = Store();
        var bundle = Bundle();

        var saved = await store.SaveAsync(bundle, TestContext.CancellationToken);

        Assert.IsFalse(string.IsNullOrEmpty(saved.IntegrityHash));
        var loaded = await store.GetAsync(saved.RecordId, TestContext.CancellationToken);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(saved.RecordId, loaded.RecordId);
        Assert.AreEqual(saved.IntegrityHash, loaded.IntegrityHash);
        Assert.AreEqual(saved.CandidateId, loaded.CandidateId);
        CollectionAssert.AreEqual(saved.Dimensions.ToList(), loaded.Dimensions.ToList());
        Assert.AreEqual(saved.RecordId,
            (await store.ListAsync(TestContext.CancellationToken)).Single().RecordId);
    }

    [TestMethod]
    public async Task InvalidBundle_IsRejectedBeforeWrite()
    {
        var store = Store();
        var bad = Bundle() with { CandidateId = "  " };

        await Assert.ThrowsExactlyAsync<EvidenceStoreException>(
            () => store.SaveAsync(bad, TestContext.CancellationToken));
        Assert.AreEqual(0, (await store.ListAsync(TestContext.CancellationToken)).Count);
    }

    [TestMethod]
    public async Task MalformedFile_FailsSafelyWithoutOverwrite()
    {
        var dir = Path.Combine(root, "store");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".json");
        await File.WriteAllTextAsync(path, "{ broken", TestContext.CancellationToken);
        var before = await File.ReadAllBytesAsync(path, TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<EvidenceStoreException>(
            () => Store(dir).ListAsync(TestContext.CancellationToken));
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task UnsupportedSchema_FailsSafely()
    {
        var dir = Path.Combine(root, "store");
        Directory.CreateDirectory(dir);
        var bundle = Bundle() with { SchemaVersion = 99, IntegrityHash = "x" };
        var json = System.Text.Json.JsonSerializer.Serialize(bundle);
        var path = Path.Combine(dir, bundle.RecordId.ToString("N") + ".json");
        await File.WriteAllTextAsync(path, json, TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<UnsupportedEvidenceSchemaException>(
            () => Store(dir).ListAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task TamperedFile_FailsIntegrityCheck()
    {
        var dir = Path.Combine(root, "store");
        var store = Store(dir);
        var saved = await store.SaveAsync(Bundle(), TestContext.CancellationToken);
        var path = Path.Combine(dir, saved.RecordId.ToString("N") + ".json");
        var json = await File.ReadAllTextAsync(path, TestContext.CancellationToken);
        await File.WriteAllTextAsync(path, json.Replace("Unknown", "Verified"), TestContext.CancellationToken);

        var ex = await Assert.ThrowsExactlyAsync<EvidenceStoreException>(
            () => store.ListAsync(TestContext.CancellationToken));
        Assert.IsTrue(ex.Message.Contains("integrity"));
    }

    [TestMethod]
    public async Task FailedSave_PreservesExistingFiles()
    {
        var dir = Path.Combine(root, "store");
        var store = Store(dir);
        var saved = await store.SaveAsync(Bundle(), TestContext.CancellationToken);
        var path = Path.Combine(dir, saved.RecordId.ToString("N") + ".json");
        var before = await File.ReadAllBytesAsync(path, TestContext.CancellationToken);
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // Re-saving the same record targets the locked file, so the replace fails.
        await Assert.ThrowsExactlyAsync<EvidenceStoreException>(
            () => store.SaveAsync(saved, TestContext.CancellationToken));

        locked.Dispose();
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path, TestContext.CancellationToken));
        Assert.AreEqual(0, Directory.GetFiles(dir, "*.tmp").Length);
    }

    [TestMethod]
    public async Task Get_MissingRecord_ReturnsNull()
    {
        Assert.IsNull(await Store().GetAsync(Guid.NewGuid(), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Cancellation_IsHonored()
    {
        var store = Store();
        await store.SaveAsync(Bundle(), TestContext.CancellationToken);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.ListAsync(new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public async Task ConcurrentSaves_DoNotCorruptTheStore()
    {
        var dir = Path.Combine(root, "store");
        var store = Store(dir);
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            store.SaveAsync(Bundle(), TestContext.CancellationToken));

        var saved = await Task.WhenAll(tasks);

        Assert.AreEqual(10, saved.Select(b => b.RecordId).Distinct().Count());
        Assert.AreEqual(10, (await store.ListAsync(TestContext.CancellationToken)).Count);
        Assert.AreEqual(10, Directory.GetFiles(dir, "*.json").Length);
    }

    [TestMethod]
    public async Task OnlyJsonFiles_LiveInTheStoreDirectory()
    {
        var store = Store();
        await store.SaveAsync(Bundle(), TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { ".json" },
            Directory.GetFiles(Path.Combine(root, "store")).Select(Path.GetExtension).Distinct().ToList());
    }
}
