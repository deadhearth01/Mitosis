using Mitosis.Application.Preferences;
using Mitosis.Windows.ViewModels;

namespace Mitosis.ViewModels.Tests;

/// <summary>Main-window preference wiring (Stage 13): the onboarding request is read
/// from the shared store, unreadable stores never nag, and Help/Settings route to
/// the window layer.</summary>
[TestClass]
public sealed class MainViewModelPreferencesTests
{
    private static MainViewModel Create(FakeDiscoveryService? discovery = null, IAppPreferencesStore? preferences = null) =>
        new(discovery ?? new FakeDiscoveryService(), new FakeAssessor(), new FakeProfileRepository(),
            preferences: preferences);

    private static FakePreferencesStore Store(bool onboarded = false) =>
        new(new AppPreferences { Onboarded = onboarded, ShowTips = true });

    [TestMethod]
    public async Task LoadAsync_AsksForOnboardingOnlyBeforeItCompletes()
    {
        var store = Store(onboarded: false);
        var model = Create(preferences: store);

        await model.LoadAsync();

        Assert.IsTrue(model.NeedsOnboarding);

        model.ClearOnboardingRequest();
        Assert.IsFalse(model.NeedsOnboarding, "The presenting window consumes the request once.");
    }

    [TestMethod]
    public async Task LoadAsync_OnboardedStore_NeverAsksAgain()
    {
        var store = Store(onboarded: true);
        var model = Create(preferences: store);

        await model.LoadAsync();

        Assert.IsFalse(model.NeedsOnboarding);
        Assert.AreEqual(store, model.PreferencesStore, "The store is shared with Help/Settings windows.");
    }

    [TestMethod]
    public async Task LoadAsync_UnreadableStore_TreatsTheUserAsOnboarded()
    {
        var store = new ThrowingPreferencesStore();
        var model = Create(preferences: store);

        await model.LoadAsync();

        Assert.IsFalse(model.NeedsOnboarding,
            "A damaged store must not surface the welcome flow on every launch.");
    }

    [TestMethod]
    public async Task LoadAsync_WithoutAStore_LeavesOnboardingUntouched()
    {
        var model = Create();

        await model.LoadAsync();

        Assert.IsFalse(model.NeedsOnboarding, "Preview/tests without persistence show no flow.");
        Assert.IsNull(model.PreferencesStore);
    }

    [TestMethod]
    public void ShowHelpCommand_RaisesTheRequest()
    {
        var model = Create();
        var raised = 0;
        model.ShowHelpRequested += (_, _) => raised++;

        model.ShowHelpCommand.Execute(null);
        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public void ShowSettingsCommand_RaisesTheRequest()
    {
        var model = Create();
        var raised = 0;
        model.ShowSettingsRequested += (_, _) => raised++;

        model.ShowSettingsCommand.Execute(null);
        model.ShowSettingsCommand.Execute(null);

        Assert.AreEqual(2, raised, "Settings can be opened as often as requested.");
    }

    private sealed class ThrowingPreferencesStore : IAppPreferencesStore
    {
        public Task<AppPreferences> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<AppPreferences>(
                new PreferencesStoreException("The preference store is damaged."));

        public Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
