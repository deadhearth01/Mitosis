using Mitosis.Application.Profiles;
using Mitosis.Application.Profiles;
using Mitosis.Domain;
using Mitosis.Windows.ViewModels;

namespace Mitosis.ViewModels.Tests;

[TestClass]
public sealed class NewCloneWizardTests
{
    private static DiscoveredAppItem Item(
        string name,
        CompatibilityStatus status = CompatibilityStatus.Unknown,
        string? strategyId = null) => new(
            DiscoveredApps.Make(name),
            new CompatibilityAssessment(
                status,
                new[] { $"{status} reason." },
                strategyId,
                status == CompatibilityStatus.Supported
                    ? Array.Empty<string>()
                    : new[] { "Validated per-application strategy" },
                DateTimeOffset.UnixEpoch));

    private static (FakeProfileRepository profiles, NewCloneWizardViewModel wizard) Create(
        IEnumerable<DiscoveredAppItem>? apps = null,
        IEnumerable<CloneProfile>? existing = null,
        DiscoveredAppItem? preselected = null)
    {
        var profiles = new FakeProfileRepository();
        return (profiles, new NewCloneWizardViewModel(
            (apps ?? new[] { Item("Slack") }).ToList(),
            (existing ?? Array.Empty<CloneProfile>()).ToList(),
            profiles,
            preselected));
    }

    private static (FakeProfileRepository profiles, NewCloneWizardViewModel wizard) CreateWith(
        FakeProfileRepository profiles,
        IEnumerable<DiscoveredAppItem>? apps = null,
        IEnumerable<CloneProfile>? existing = null) =>
        (profiles, new NewCloneWizardViewModel(
            (apps ?? new[] { Item("Slack") }).ToList(),
            (existing ?? Array.Empty<CloneProfile>()).ToList(),
            profiles));

    [TestMethod]
    public void InitialState_IsPickWithoutSelection()
    {
        var (_, wizard) = Create();

        Assert.AreEqual(WizardStep.Pick, wizard.Step);
        Assert.IsTrue(wizard.ShowPick);
        Assert.IsNull(wizard.SelectedApp);
        Assert.IsFalse(wizard.CanContinueFromPick);
        Assert.IsFalse(wizard.GoToDetailsCommand.CanExecute(null));
    }

    [TestMethod]
    public void PreselectedApp_StartsPicked()
    {
        var app = Item("Slack");
        var (_, wizard) = Create(new[] { app }, preselected: app);

        Assert.AreEqual(app, wizard.SelectedApp);
        Assert.AreEqual(WizardStep.Pick, wizard.Step);
    }

    [TestMethod]
    public void UnknownApp_AllowsWithExplanation()
    {
        var (_, wizard) = Create();
        wizard.SelectedApp = wizard.VisibleApps.Single();

        Assert.IsTrue(wizard.GoToDetails());
        Assert.AreEqual(WizardStep.Details, wizard.Step);
        Assert.IsTrue(wizard.CompatibilityNote.Contains("not been established"));
        Assert.IsFalse(wizard.NeedsAcknowledgement);
        Assert.IsTrue(wizard.CanContinueFromDetails, "Prefilled name/badge/color are valid.");
    }

    [TestMethod]
    public void UnsupportedApp_BlocksProgress()
    {
        var (_, wizard) = Create(new[] { Item("Sys", CompatibilityStatus.Unsupported) });
        wizard.SelectedApp = wizard.VisibleApps.Single();

        Assert.IsFalse(wizard.CanContinueFromPick);
        Assert.IsFalse(wizard.GoToDetails());
        Assert.AreEqual(WizardStep.Pick, wizard.Step);
        Assert.IsFalse(string.IsNullOrEmpty(wizard.UnsupportedReason));
    }

    [TestMethod]
    public void NeedsReview_RequiresAcknowledgement()
    {
        var (_, wizard) = Create(new[] { Item("App", CompatibilityStatus.NeedsReview) });
        wizard.SelectedApp = wizard.VisibleApps.Single();
        Assert.IsTrue(wizard.GoToDetails());

        Assert.IsTrue(wizard.NeedsAcknowledgement);
        Assert.IsFalse(wizard.CanContinueFromDetails);

        wizard.Acknowledged = true;
        Assert.IsTrue(wizard.CanContinueFromDetails);
        Assert.IsTrue(wizard.GoToReview());
    }

    [TestMethod]
    public void Supported_AllowsWithoutAcknowledgement()
    {
        var (_, wizard) = Create(new[] { Item("App", CompatibilityStatus.Supported, "s1") });
        wizard.SelectedApp = wizard.VisibleApps.Single();
        Assert.IsTrue(wizard.GoToDetails());

        Assert.IsFalse(wizard.NeedsAcknowledgement);
        Assert.IsTrue(wizard.CompatibilityNote.Contains("s1"));
        Assert.IsTrue(wizard.GoToReview());
    }

    [TestMethod]
    public void BackNavigation_PreservesState()
    {
        var (_, wizard) = Create();
        wizard.SelectedApp = wizard.VisibleApps.Single();
        wizard.GoToDetails();
        wizard.ProfileName = "Slack (Home)";
        wizard.GoToReview();

        wizard.GoBack();
        Assert.AreEqual(WizardStep.Details, wizard.Step);
        Assert.AreEqual("Slack (Home)", wizard.ProfileName);

        wizard.GoBack();
        Assert.AreEqual(WizardStep.Pick, wizard.Step);
        Assert.AreEqual("Slack", wizard.SelectedApp?.App.DisplayName, "Selection preserved.");
        Assert.AreEqual("Slack (Home)", wizard.ProfileName, "Fields preserved.");
    }

    [TestMethod]
    public void GoToDetails_WithoutApp_StaysPut()
    {
        var (_, wizard) = Create();

        Assert.IsFalse(wizard.GoToDetails());
        Assert.IsFalse(wizard.GoToReview());
        Assert.AreEqual(WizardStep.Pick, wizard.Step);
    }

    [TestMethod]
    public void NameValidation_MatchesDomainRules()
    {
        var (_, wizard) = Create();
        wizard.SelectedApp = wizard.VisibleApps.Single();
        wizard.GoToDetails();

        wizard.ProfileName = "   ";
        Assert.AreEqual("Add a name, like Slack (Work).", wizard.ValidationMessage);

        wizard.ProfileName = "a/b";
        Assert.IsTrue(wizard.ValidationMessage?.Contains("can't contain") ?? false);

        wizard.ProfileName = new string('x', 61);
        Assert.IsTrue(wizard.ValidationMessage?.Contains("at most 60") ?? false);

        wizard.ProfileName = "Slack (Work)";
        Assert.IsNull(wizard.ValidationMessage);
    }

    [TestMethod]
    public void DuplicateName_IsRejected()
    {
        var appId = AppIdentity.Create(DiscoverySource.CurrentUserUninstall, "Slack", "Publisher");
        var (_, wizard) = Create(existing: new[] { CloneProfile.Create(appId, "Slack (Work)") });
        wizard.SelectedApp = wizard.VisibleApps.Single();
        wizard.GoToDetails();

        // Prefill avoids the taken suggestion; an explicit duplicate still fails.
        wizard.ProfileName = "slack (work)";
        Assert.AreEqual("You already have slack (work).", wizard.ValidationMessage);
        Assert.IsFalse(wizard.GoToReview());
    }

    [TestMethod]
    public void Prefill_SuggestsFirstUnusedNameColorAndBadge()
    {
        var appId = AppIdentity.Create(DiscoverySource.CurrentUserUninstall, "Slack", "Publisher");
        var (_, wizard) = Create(existing: new[]
        {
            CloneProfile.Create(appId, "Slack (Work)", "W", "blue"),
        });
        wizard.SelectedApp = wizard.VisibleApps.Single();
        wizard.GoToDetails();

        Assert.AreEqual("Slack (Personal)", wizard.ProfileName);
        Assert.AreEqual("S", wizard.BadgeText, "Badge follows the app initial.");
        Assert.AreEqual("#30D158", wizard.SelectedColorHex, "First unused color wins.");
    }

    [TestMethod]
    public void BadgeText_IsClampedToTwoUppercaseCharacters()
    {
        var (_, wizard) = Create();
        wizard.SelectedApp = wizard.VisibleApps.Single();
        wizard.GoToDetails();

        wizard.BadgeText = "toolong";
        Assert.AreEqual("TO", wizard.BadgeText);
    }

    [TestMethod]
    public void InvalidColor_IsRejected()
    {
        var (_, wizard) = Create();
        wizard.SelectedApp = wizard.VisibleApps.Single();
        wizard.GoToDetails();

        wizard.SelectedColorHex = "ultraviolet";
        Assert.AreEqual("Choose a badge color.", wizard.ValidationMessage);
    }

    [TestMethod]
    public async Task Save_PersistsPlannedRecordHonestly()
    {
        var (profiles, wizard) = Create();
        wizard.SelectedApp = wizard.VisibleApps.Single();
        wizard.GoToDetails();
        wizard.GoToReview();

        var saved = await wizard.SaveAsync(TestContext.CancellationToken);

        Assert.IsNotNull(saved);
        Assert.AreEqual(WizardStep.Done, wizard.Step);
        Assert.AreEqual(saved, wizard.CreatedProfile);
        Assert.AreEqual(CloneProfileState.Planned, saved.State, "Metadata save must never mark Ready.");
        Assert.AreEqual($"{saved.Name} planned.", wizard.DoneTitle);
        Assert.IsTrue(NewCloneWizardViewModel.DoneMessage.Contains("has not been copied"));
        Assert.AreEqual(1, profiles.AddCalls, "Exactly one repository call; no engine involved.");
        var stored = profiles.Store.Single();
        Assert.AreEqual(saved.Id, stored.Id);
        Assert.AreEqual(CloneProfileState.Planned, stored.State);
    }

    [TestMethod]
    public async Task DoubleSubmit_ProducesOneRecord()
    {
        var (profiles, wizard) = Create();
        var gate = new TaskCompletionSource<CloneProfile>();
        profiles.AddGate = gate;
        wizard.SelectedApp = wizard.VisibleApps.Single();
        wizard.GoToDetails();
        wizard.GoToReview();

        var first = wizard.SaveAsync(TestContext.CancellationToken);
        Assert.IsTrue(wizard.IsSaving);
        var second = await wizard.SaveAsync(TestContext.CancellationToken);

        Assert.IsNull(second, "Reentrant save is ignored.");
        var profile = CloneProfile.Create("app:1", "Slack (Work)");
        gate.SetResult(profile);
        Assert.AreEqual(profile, await first);
        Assert.AreEqual(1, profiles.AddCalls);
        Assert.AreEqual(WizardStep.Done, wizard.Step);
    }

    [TestMethod]
    public async Task DuplicateRace_FailsWithStateIntact()
    {
        // Simulate a lost race: the snapshot is clean but the store takes the name first.
        var racing = new RacingRepository();
        var racy = new NewCloneWizardViewModel(
            new[] { Item("Slack") }, Array.Empty<CloneProfile>(), racing);
        racy.SelectedApp = racy.VisibleApps.Single();
        racy.GoToDetails();
        racy.ProfileName = "Slack (Work)";
        Assert.IsTrue(racy.GoToReview());

        var saved = await racy.SaveAsync(TestContext.CancellationToken);

        Assert.IsNull(saved);
        Assert.AreEqual(WizardStep.Failed, racy.Step);
        Assert.IsTrue(racy.ErrorMessage?.Contains("already have") ?? false);
        Assert.AreEqual("Slack (Work)", racy.ProfileName, "Wizard state intact for retry.");
        Assert.IsFalse(racy.IsSaving);

        racy.ProfileName = "Slack (Home)";
        racy.Retry();
        Assert.AreEqual(WizardStep.Review, racy.Step);
        Assert.IsNull(racy.ErrorMessage);
    }

    [TestMethod]
    public async Task RepositoryFailure_ShowsActionableError()
    {
        var (profiles, wizard) = Create();
        profiles.Store.Add(CloneProfile.Create("app:1", "Slack (Work)"));
        wizard.SelectedApp = wizard.VisibleApps.Single();
        wizard.GoToDetails();
        wizard.ProfileName = "Slack (Home)";
        // Force a store failure through a broken repository.
        var broken = new BrokenRepository();
        var failing = new NewCloneWizardViewModel(
            new[] { Item("Slack") }, Array.Empty<CloneProfile>(), broken);
        failing.SelectedApp = failing.VisibleApps.Single();
        failing.GoToDetails();
        failing.ProfileName = "Slack (Home)";
        failing.GoToReview();

        var saved = await failing.SaveAsync(TestContext.CancellationToken);

        Assert.IsNull(saved);
        Assert.AreEqual(WizardStep.Failed, failing.Step);
        Assert.IsFalse(string.IsNullOrEmpty(failing.ErrorMessage));
        Assert.IsTrue(failing.FailedTitle.Contains("Slack (Home)"));
    }

    [TestMethod]
    public void Cancel_BeforeSave_LeavesNoRecord()
    {
        var (profiles, wizard) = Create();
        wizard.SelectedApp = wizard.VisibleApps.Single();
        wizard.GoToDetails();
        wizard.GoToReview();

        wizard.Cancel();

        Assert.IsTrue(wizard.Cancelled);
        Assert.AreEqual(0, profiles.AddCalls);
        Assert.AreEqual(0, profiles.Store.Count);
    }

    [TestMethod]
    public async Task Cancel_DuringSave_IsIgnored()
    {
        var (profiles, wizard) = Create();
        var gate = new TaskCompletionSource<CloneProfile>();
        profiles.AddGate = gate;
        wizard.SelectedApp = wizard.VisibleApps.Single();
        wizard.GoToDetails();
        wizard.GoToReview();

        var save = wizard.SaveAsync(TestContext.CancellationToken);
        wizard.Cancel();

        Assert.IsFalse(wizard.Cancelled, "In-flight save cannot be cancelled.");
        gate.SetResult(CloneProfile.Create("app:1", "Slack (Work)"));
        await save;
        Assert.AreEqual(WizardStep.Done, wizard.Step);
    }

    [TestMethod]
    public void Search_FiltersPickList()
    {
        var (_, wizard) = Create(new[] { Item("Slack"), Item("Signal") });

        wizard.SearchText = "sig";

        Assert.AreEqual("Signal", wizard.VisibleApps.Single().App.DisplayName);
    }

    [TestMethod]
    public void SelectColor_UpdatesHex()
    {
        var (_, wizard) = Create();
        wizard.SelectedApp = wizard.VisibleApps.Single();
        wizard.GoToDetails();

        wizard.SelectColorCommand.Execute("#FF453A");

        Assert.AreEqual("#FF453A", wizard.SelectedColorHex);
    }

    private sealed class RacingRepository : ICloneProfileRepository
    {
        public Task<IReadOnlyList<CloneProfile>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CloneProfile>>(Array.Empty<CloneProfile>());

        public Task<CloneProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<CloneProfile?>(null);

        public Task<CloneProfile> AddAsync(CloneProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromException<CloneProfile>(new DuplicateProfileException($"You already have {profile.Name}."));

        public Task<CloneProfile> UpdateAsync(CloneProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(profile);

        public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class BrokenRepository : ICloneProfileRepository
    {
        public Task<IReadOnlyList<CloneProfile>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CloneProfile>>(Array.Empty<CloneProfile>());

        public Task<CloneProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<CloneProfile?>(null);

        public Task<CloneProfile> AddAsync(CloneProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromException<CloneProfile>(new ProfileStoreException("Couldn't save profiles (IOException)."));

        public Task<CloneProfile> UpdateAsync(CloneProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(profile);

        public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public TestContext TestContext { get; set; } = null!;
}

[TestClass]
public sealed class MainViewModelNewCloneTests
{
    [TestMethod]
    public void ProductionNewClone_RaisesWizardEvent()
    {
        var discovery = new FakeDiscoveryService();
        var model = new MainViewModel(discovery, new FakeAssessor(), new FakeProfileRepository());

        var raised = 0;
        DialogRequest? dialog = null;
        model.NewCloneRequested += (_, _) => raised++;
        model.DialogRequested += (_, r) => dialog = r;
        model.NewCloneCommand.Execute(null);

        Assert.AreEqual(1, raised);
        Assert.IsNull(dialog, "Production opens the wizard, not a placeholder.");
    }

    [TestMethod]
    public void PreviewNewClone_ShowsPlaceholder()
    {
        var model = PreviewData.CreateMain();

        var raised = 0;
        DialogRequest? dialog = null;
        model.NewCloneRequested += (_, _) => raised++;
        model.DialogRequested += (_, r) => dialog = r;
        model.NewCloneCommand.Execute(null);

        Assert.AreEqual(0, raised);
        Assert.IsNotNull(dialog);
    }

    [TestMethod]
    public async Task CreateWizard_HandsOverSnapshot()
    {
        var discovery = new FakeDiscoveryService();
        discovery.Returns(new[] { DiscoveredApps.Make("Slack") });
        var model = new MainViewModel(discovery, new FakeAssessor(), new FakeProfileRepository());
        await model.LoadAsync(TestContext.CancellationToken);

        var wizard = model.CreateWizard();

        Assert.AreEqual("Slack", wizard.VisibleApps.Single().App.DisplayName);
        Assert.AreEqual(WizardStep.Pick, wizard.Step);
    }

    [TestMethod]
    public void PreviewCreateWizard_Throws()
    {
        var model = PreviewData.CreateMain();

        Assert.ThrowsExactly<InvalidOperationException>(() => model.CreateWizard());
    }

    [TestMethod]
    public async Task SelectAppById_SelectsKnownApp()
    {
        var discovery = new FakeDiscoveryService();
        discovery.Returns(new[] { DiscoveredApps.Make("Slack") });
        var model = new MainViewModel(discovery, new FakeAssessor(), new FakeProfileRepository());
        await model.LoadAsync(TestContext.CancellationToken);

        Assert.IsFalse(model.SelectAppById("app:missing"));
        Assert.IsFalse(model.HasSelection);

        var id = model.VisibleAppGroups.SelectMany(g => g.Items).Single().App.Id;
        Assert.IsTrue(model.SelectAppById(id));
        Assert.AreEqual("Slack", model.SelectedApp?.App.DisplayName);
    }

    public TestContext TestContext { get; set; } = null!;
}
