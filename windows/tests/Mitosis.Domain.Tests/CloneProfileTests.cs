using Mitosis.Domain;

namespace Mitosis.Domain.Tests;

[TestClass]
public sealed class CloneProfileTests
{
    [TestMethod]
    public void Create_InitializesPlannedMetadataRecord()
    {
        var before = DateTimeOffset.UtcNow;

        var profile = CloneProfile.Create("app:abc123", "Slack (Work)", "W", "blue");

        Assert.AreNotEqual(Guid.Empty, profile.Id);
        Assert.AreEqual("app:abc123", profile.AppId);
        Assert.AreEqual("Slack (Work)", profile.Name);
        Assert.AreEqual("W", profile.BadgeText);
        Assert.AreEqual("blue", profile.BadgeColor);
        Assert.AreEqual(CloneProfileState.Planned, profile.State,
            "A new metadata record must never be Ready.");
        Assert.AreEqual(TimeSpan.Zero, profile.CreatedAt.Offset);
        Assert.AreEqual(TimeSpan.Zero, profile.UpdatedAt.Offset);
        Assert.IsTrue(profile.CreatedAt >= before);
        Assert.AreEqual(profile.CreatedAt, profile.UpdatedAt);
    }

    [TestMethod]
    public void Create_GeneratesStableUniqueIds()
    {
        var first = CloneProfile.Create("app:abc123", "Slack (Work)");
        var second = CloneProfile.Create("app:abc123", "Slack (Work)");

        Assert.AreNotEqual(first.Id, second.Id);
    }

    [TestMethod]
    public void Create_RejectsInvalidInput()
    {
        // Name violations surface as the naming rule's own user-facing error.
        Assert.ThrowsExactly<CloneNameException>(() => CloneProfile.Create("app:1", "   "));
        Assert.ThrowsExactly<CloneNameException>(() => CloneProfile.Create("app:1", new string('x', 61)));
        Assert.ThrowsExactly<CloneNameException>(() => CloneProfile.Create("app:1", ".hidden"));
        Assert.ThrowsExactly<InvalidProfileException>(() => CloneProfile.Create("app:1", "Slack (Work)", "TOOLONG"));
        Assert.ThrowsExactly<InvalidProfileException>(() => CloneProfile.Create("app:1", "Slack (Work)", "W", "ultraviolet"));
        Assert.ThrowsExactly<ArgumentException>(() => CloneProfile.Create("  ", "Slack (Work)"));
    }

    [TestMethod]
    public void Badge_AcceptsPresetsAndHex()
    {
        Assert.AreEqual("#0A84FF", CloneProfile.Create("a", "App (X)", "X", "#0A84FF").BadgeColor);
        Assert.AreEqual("RED", CloneProfile.Create("a", "App (X)", "x", "RED").BadgeColor);
        Assert.AreEqual("X", CloneProfile.Create("a", "App (X)", " x ").BadgeText);
        Assert.IsNull(CloneProfile.Create("a", "App (X)").BadgeText);
    }

    [TestMethod]
    public void Transitions_FollowExplicitRules()
    {
        var planned = CloneProfile.Create("a", "App (X)");

        Assert.ThrowsExactly<InvalidProfileException>(() => planned.TransitionTo(CloneProfileState.Ready),
            "Planned must never jump straight to Ready.");
        Assert.ThrowsExactly<InvalidProfileException>(() => planned.TransitionTo(CloneProfileState.Planned));

        var provisioning = planned.TransitionTo(CloneProfileState.Provisioning);
        var ready = provisioning.TransitionTo(CloneProfileState.Ready);
        Assert.AreEqual(CloneProfileState.Ready, ready.State);
        Assert.IsTrue(ready.UpdatedAt >= provisioning.UpdatedAt);

        Assert.ThrowsExactly<InvalidProfileException>(() => ready.TransitionTo(CloneProfileState.Planned));
        Assert.ThrowsExactly<InvalidProfileException>(() => ready.TransitionTo(CloneProfileState.Failed));

        var failed = planned.TransitionTo(CloneProfileState.Provisioning)
            .TransitionTo(CloneProfileState.Failed);
        Assert.AreEqual(CloneProfileState.Provisioning, failed.TransitionTo(CloneProfileState.Provisioning).State);

        var unavailable = ready.TransitionTo(CloneProfileState.Unavailable);
        Assert.AreEqual(CloneProfileState.Unavailable, unavailable.State);
        Assert.AreEqual(CloneProfileState.Provisioning,
            unavailable.TransitionTo(CloneProfileState.Provisioning).State);
    }

    [TestMethod]
    public void Rename_RevalidatesAndBumpsTimestamp()
    {
        var profile = CloneProfile.Create("a", "App (Work)");

        var renamed = profile.Rename("App (Home)");

        Assert.AreEqual("App (Home)", renamed.Name);
        Assert.IsTrue(renamed.UpdatedAt >= profile.UpdatedAt);
        Assert.ThrowsExactly<CloneNameException>(() => profile.Rename("  "));
    }

    [TestMethod]
    public void Transition_TimestampOverrideIsRespected()
    {
        var profile = CloneProfile.Create("a", "App (Work)");
        var when = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

        var next = profile.TransitionTo(CloneProfileState.Provisioning, when);

        Assert.AreEqual(when, next.UpdatedAt);
    }

    [TestMethod]
    public void Validate_RejectsBrokenRecords()
    {
        var valid = CloneProfile.Create("a", "App (Work)");

        Assert.ThrowsExactly<InvalidProfileException>(() => (valid with { Id = Guid.Empty }).Validate());
        Assert.ThrowsExactly<CloneNameException>(() => (valid with { Name = "  " }).Validate());
        Assert.ThrowsExactly<InvalidProfileException>(() =>
            (valid with { CreatedAt = valid.CreatedAt.AddHours(1) }).Validate());
    }

    [TestMethod]
    public void Transitions_CoverEveryDocumentedEdge()
    {
        var allowed = new (CloneProfileState From, CloneProfileState To)[]
        {
            (CloneProfileState.Planned, CloneProfileState.Provisioning),
            (CloneProfileState.Provisioning, CloneProfileState.Ready),
            (CloneProfileState.Provisioning, CloneProfileState.Failed),
            (CloneProfileState.Failed, CloneProfileState.Provisioning),
            (CloneProfileState.Ready, CloneProfileState.Provisioning),
            (CloneProfileState.Ready, CloneProfileState.Unavailable),
            (CloneProfileState.Unavailable, CloneProfileState.Provisioning),
            (CloneProfileState.Unavailable, CloneProfileState.Ready),
        };

        foreach (var from in Enum.GetValues<CloneProfileState>())
        {
            foreach (var to in Enum.GetValues<CloneProfileState>())
            {
                var expected = allowed.Contains((from, to));
                Assert.AreEqual(expected, CloneProfileTransitions.IsValid(from, to),
                    $"Transition {from} -> {to}");
            }
        }
    }
}
