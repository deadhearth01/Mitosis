using Mitosis.Application.Profiles;
using Mitosis.Domain;
using Mitosis.Infrastructure.Profiles;

namespace Mitosis.Infrastructure.Tests;

[TestClass]
public sealed class JsonProfileRepositoryTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), "mitosis-profiles-" + Guid.NewGuid().ToString("N"));

    private string Dir(params string[] parts) => Path.Combine(new[] { root }.Concat(parts).ToArray());

    private JsonCloneProfileRepository Repo(string? dir = null) =>
        new(dir ?? Dir("store"));

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
    public async Task MissingFile_YieldsEmptyWithoutCreatingAnything()
    {
        var repo = Repo();

        var profiles = await repo.ListAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, profiles.Count);
        Assert.IsFalse(Directory.Exists(Dir("store")), "Reads must not create directories.");
    }

    [TestMethod]
    public async Task AddGetList_RoundTrip()
    {
        var repo = Repo();
        var created = CloneProfile.Create("app:1", "Slack (Work)", "W", "blue");

        var added = await repo.AddAsync(created, TestContext.CancellationToken);

        Assert.AreEqual(created.Id, added.Id);
        Assert.AreEqual(created, await repo.GetByIdAsync(created.Id, TestContext.CancellationToken));
        CollectionAssert.AreEqual(new[] { created.Id },
            (await repo.ListAsync(TestContext.CancellationToken)).Select(p => p.Id).ToList());
    }

    [TestMethod]
    public async Task Records_PersistAcrossInstancesDeterministically()
    {
        var dir = Dir("store");
        await Repo(dir).AddAsync(CloneProfile.Create("app:1", "Zulu (Work)"), TestContext.CancellationToken);
        await Repo(dir).AddAsync(CloneProfile.Create("app:1", "Alpha (Work)"), TestContext.CancellationToken);

        var reloaded = await Repo(dir).ListAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "Alpha (Work)", "Zulu (Work)" },
            reloaded.Select(p => p.Name).ToList());
        var json = await File.ReadAllTextAsync(Path.Combine(dir, "profiles.json"), TestContext.CancellationToken);
        Assert.IsTrue(json.Contains("\"SchemaVersion\": 1"));
    }

    [TestMethod]
    public async Task DuplicateId_IsRejected()
    {
        var repo = Repo();
        var profile = CloneProfile.Create("app:1", "Slack (Work)");
        await repo.AddAsync(profile, TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<DuplicateProfileException>(
            () => repo.AddAsync(profile, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task DuplicateName_IsRejectedCaseInsensitively()
    {
        var repo = Repo();
        await repo.AddAsync(CloneProfile.Create("app:1", "Slack (Work)"), TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<DuplicateProfileException>(
            () => repo.AddAsync(CloneProfile.Create("app:2", "slack (work)"), TestContext.CancellationToken));

        var existing = (await repo.ListAsync(TestContext.CancellationToken)).Single();
        await repo.AddAsync(CloneProfile.Create("app:2", "Other (Work)"), TestContext.CancellationToken);
        var other = (await repo.ListAsync(TestContext.CancellationToken)).First(p => p.Name == "Other (Work)");
        await Assert.ThrowsExactlyAsync<DuplicateProfileException>(
            () => repo.UpdateAsync(other.Rename("SLACK (WORK)"), TestContext.CancellationToken));

        // Renaming a record to its own name (even with different casing) is allowed.
        await repo.UpdateAsync(existing.Rename("SLACK (WORK)"), TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task Update_PersistsChangesAndRejectsUnknown()
    {
        var repo = Repo();
        var profile = await repo.AddAsync(CloneProfile.Create("app:1", "Slack (Work)"), TestContext.CancellationToken);

        var updated = await repo.UpdateAsync(
            profile.Rename("Slack (Home)").ChangeBadge("H", "#FF9F0A"), TestContext.CancellationToken);

        Assert.AreEqual("Slack (Home)", updated.Name);
        Assert.AreEqual("H", updated.BadgeText);
        Assert.AreEqual(
            "Slack (Home)",
            (await repo.GetByIdAsync(profile.Id, TestContext.CancellationToken))?.Name);

        var ghost = CloneProfile.Create("app:9", "Ghost (Work)");
        await Assert.ThrowsExactlyAsync<ProfileNotFoundException>(
            () => repo.UpdateAsync(ghost, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Remove_DeletesMetadataOnly()
    {
        var repo = Repo();
        var keep = await repo.AddAsync(CloneProfile.Create("app:1", "Keep (Work)"), TestContext.CancellationToken);
        var drop = await repo.AddAsync(CloneProfile.Create("app:1", "Drop (Work)"), TestContext.CancellationToken);

        await repo.RemoveAsync(drop.Id, TestContext.CancellationToken);

        Assert.IsNull(await repo.GetByIdAsync(drop.Id, TestContext.CancellationToken));
        Assert.AreEqual(keep.Id, (await repo.ListAsync(TestContext.CancellationToken)).Single().Id);
        await Assert.ThrowsExactlyAsync<ProfileNotFoundException>(
            () => repo.RemoveAsync(drop.Id, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task InvalidProfile_IsRejectedBeforeWrite()
    {
        var repo = Repo();

        await Assert.ThrowsExactlyAsync<CloneNameException>(
            () => repo.AddAsync(CloneProfile.Create("app:1", "Slack (Work)") with { Name = "  " },
                TestContext.CancellationToken));
        Assert.AreEqual(0, (await repo.ListAsync(TestContext.CancellationToken)).Count);
    }

    [TestMethod]
    public async Task MalformedJson_IsRejectedWithoutTouchingTheFile()
    {
        var dir = Dir("store");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "profiles.json");
        await File.WriteAllTextAsync(path, "{ not json", TestContext.CancellationToken);
        var before = await File.ReadAllBytesAsync(path, TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<ProfileStoreException>(
            () => Repo(dir).ListAsync(TestContext.CancellationToken));

        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task UnsupportedSchema_IsRejectedWithoutTouchingTheFile()
    {
        var dir = Dir("store");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "profiles.json");
        await File.WriteAllTextAsync(path, "{ \"schemaVersion\": 99, \"profiles\": [] }", TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<UnsupportedProfileSchemaException>(
            () => Repo(dir).ListAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task InvalidOrDuplicateRecords_AreRejectedWithoutTouchingTheFile()
    {
        var dir = Dir("store");
        Directory.CreateDirectory(dir);
        var id = Guid.NewGuid();
        var record = (object)new
        {
            id,
            appId = "app:1",
            name = "Slack (Work)",
            badgeText = (string?)null,
            badgeColor = (string?)null,
            state = "Planned",
            createdAt = DateTimeOffset.UtcNow,
            updatedAt = DateTimeOffset.UtcNow,
        };
        var dupe = System.Text.Json.JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            profiles = new[] { record, record },
        });
        var path = Path.Combine(dir, "profiles.json");
        await File.WriteAllTextAsync(path, dupe, TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<ProfileStoreException>(
            () => Repo(dir).ListAsync(TestContext.CancellationToken));

        var badState = System.Text.Json.JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            profiles = new[] { new { id = Guid.NewGuid(), appId = "app:1", name = "X (Y)", state = "Launched" } },
        });
        await File.WriteAllTextAsync(path, badState, TestContext.CancellationToken);
        await Assert.ThrowsExactlyAsync<ProfileStoreException>(
            () => Repo(dir).ListAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task UnknownFields_AreTolerated()
    {
        var dir = Dir("store");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(
            Path.Combine(dir, "profiles.json"),
            "{ \"schemaVersion\": 1, \"profiles\": [], \"future\": { \"x\": 1 } }",
            TestContext.CancellationToken);

        Assert.AreEqual(0, (await Repo(dir).ListAsync(TestContext.CancellationToken)).Count);
    }

    [TestMethod]
    public async Task ReadFailure_IsActionable()
    {
        var dir = Dir("store");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "profiles.json");
        await File.WriteAllTextAsync(path, "{ \"schemaVersion\": 1, \"profiles\": [] }", TestContext.CancellationToken);
        using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        var ex = await Assert.ThrowsExactlyAsync<ProfileStoreException>(
            () => Repo(dir).ListAsync(TestContext.CancellationToken));
        Assert.IsTrue(ex.Message.Contains("Couldn't read"), ex.Message);
    }

    [TestMethod]
    public async Task FailedSave_PreservesExistingData()
    {
        var dir = Dir("store");
        var repo = Repo(dir);
        var kept = await repo.AddAsync(CloneProfile.Create("app:1", "Keep (Work)"), TestContext.CancellationToken);
        var path = Path.Combine(dir, "profiles.json");
        var before = await File.ReadAllBytesAsync(path, TestContext.CancellationToken);
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        await Assert.ThrowsExactlyAsync<ProfileStoreException>(
            () => repo.AddAsync(CloneProfile.Create("app:1", "New (Work)"), TestContext.CancellationToken));

        locked.Dispose();
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(path, TestContext.CancellationToken));
        Assert.AreEqual(kept.Id, (await repo.ListAsync(TestContext.CancellationToken)).Single().Id);
        Assert.AreEqual(0, Directory.GetFiles(dir, "*.tmp").Length, "No stray temp files.");
    }

    [TestMethod]
    public async Task WriteToUnusableLocation_IsActionable()
    {
        Directory.CreateDirectory(root);
        var fileAsDir = Path.Combine(root, "blocker");
        await File.WriteAllTextAsync(fileAsDir, "x", TestContext.CancellationToken);

        await Assert.ThrowsExactlyAsync<ProfileStoreException>(
            () => Repo(fileAsDir).AddAsync(CloneProfile.Create("app:1", "New (Work)"), TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Cancellation_IsHonored()
    {
        var repo = Repo();
        await repo.AddAsync(CloneProfile.Create("app:1", "Keep (Work)"), TestContext.CancellationToken);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => repo.ListAsync(new CancellationToken(canceled: true)));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => repo.AddAsync(CloneProfile.Create("app:1", "New (Work)"), new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public async Task ConcurrentAdds_DoNotCorruptTheStore()
    {
        var repo = Repo();
        var tasks = Enumerable.Range(0, 20).Select(i =>
            repo.AddAsync(CloneProfile.Create("app:1", $"App ({i:D2})"), TestContext.CancellationToken));

        var added = await Task.WhenAll(tasks);

        Assert.AreEqual(20, added.Select(p => p.Id).Distinct().Count());
        Assert.AreEqual(20, (await repo.ListAsync(TestContext.CancellationToken)).Count);
        var reparsed = await Repo(Dir("store")).ListAsync(TestContext.CancellationToken);
        Assert.AreEqual(20, reparsed.Count, "Store parses cleanly after concurrent writes.");
    }

    [TestMethod]
    public void Constructor_RejectsEmptyDirectory()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new JsonCloneProfileRepository("  "));
    }

    [TestMethod]
    public async Task NoExtraFiles_AreCreated()
    {
        var repo = Repo();
        await repo.AddAsync(CloneProfile.Create("app:1", "Keep (Work)"), TestContext.CancellationToken);
        await repo.RemoveAsync((await repo.ListAsync(TestContext.CancellationToken)).Single().Id,
            TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "profiles.json" },
            Directory.GetFiles(Dir("store")).Select(Path.GetFileName).ToList());
    }
}
