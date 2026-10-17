using Mitosis.Application.Preferences;
using Mitosis.Windows.ViewModels;

namespace Mitosis.ViewModels.Tests;

/// <summary>Settings window state: Advanced ▸ Show Tips Again (macOS
/// SettingsView.swift:156-166) and the informational data folder.</summary>
[TestClass]
public sealed class SettingsViewModelTests
{
    private const string DataFolder = @"C:\Users\me\AppData\Local\Mitosis";

    private static SettingsViewModel Model(FakePreferencesStore? store = null) =>
        new(store ?? new FakePreferencesStore(), DataFolder);

    [TestMethod]
    public void Constructor_KeepsTheSuppliedDataFolder()
    {
        var model = Model();

        Assert.AreEqual(DataFolder, model.DataFolderPath, "The path is shown as supplied, never guessed.");
    }

    [TestMethod]
    public void Constructor_RejectsBrokenInputs()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new SettingsViewModel(null!, DataFolder));
        Assert.ThrowsExactly<ArgumentException>(
            () => new SettingsViewModel(new FakePreferencesStore(), "  "));
    }

    [TestMethod]
    public void FreshWindow_StartsWithTheButtonAvailable()
    {
        var store = new FakePreferencesStore(new AppPreferences { Onboarded = true, ShowTips = true });
        var model = Model(store);

        Assert.IsFalse(model.TipsReset,
            "macOS tipsReset is window-local state — every Settings window starts enabled.");
        Assert.IsTrue(model.ShowTipsAgainCommand.CanExecute(null));
        Assert.IsNull(model.SaveError);
    }

    [TestMethod]
    public async Task LoadAsync_SurfacesAnUnreadableStoreWithoutDisablingAnything()
    {
        var model = new SettingsViewModel(new ThrowingPreferencesStore(), DataFolder);

        await model.LoadAsync();

        Assert.IsNotNull(model.SaveError);
        StringAssert.Contains(model.SaveError, "preference");
        Assert.IsFalse(model.TipsReset, "Nothing was clicked yet, so the button stays available.");
        Assert.IsTrue(model.ShowTipsAgainCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task LoadAsync_RecoversFromAnEarlierFailedLoad()
    {
        var store = new ToggleablePreferencesStore();
        var model = new SettingsViewModel(store, DataFolder);

        store.Healthy = false;
        await model.LoadAsync();
        Assert.IsNotNull(model.SaveError);

        store.Healthy = true;
        await model.LoadAsync();
        Assert.IsNull(model.SaveError, "A successful load clears the error.");
    }

    [TestMethod]
    public async Task ShowTipsAgain_PersistsAndDisablesForTheWindowLifetime()
    {
        var store = new FakePreferencesStore(new AppPreferences { Onboarded = true, ShowTips = false });
        var model = Model(store);

        await model.ShowTipsAgainAsync(CancellationToken.None);

        Assert.IsTrue(model.TipsReset);
        Assert.IsNull(model.SaveError);
        Assert.IsFalse(model.ShowTipsAgainCommand.CanExecute(null),
            "One reset per window, macOS wording: the label flips to Tips Are On.");
        var saved = store.Saved.Single();
        Assert.IsTrue(saved.ShowTips);
        Assert.IsFalse(saved.Onboarded, "macOS semantics: the welcome flow turns back on too.");
    }

    [TestMethod]
    public async Task ShowTipsAgain_MergesExistingStoredFlags()
    {
        var store = new FakePreferencesStore(
            new AppPreferences { Onboarded = true, ShowTips = true });
        var model = Model(store);

        await model.ShowTipsAgainAsync(CancellationToken.None);

        var saved = store.Saved.Single();
        Assert.IsTrue(saved.ShowTips);
        Assert.IsFalse(saved.Onboarded);
        Assert.IsNull(model.SaveError);
    }

    [TestMethod]
    public async Task ShowTipsAgain_Failure_ShowsTheErrorAndStaysAvailable()
    {
        var store = new FakePreferencesStore(saveFailure: _ =>
            new PreferencesStoreException("Couldn't save preferences (IOException)."));
        var model = Model(store);

        await model.ShowTipsAgainAsync(CancellationToken.None);

        Assert.IsFalse(model.TipsReset, "The flag was never persisted, so the view stays recoverable.");
        Assert.IsNotNull(model.SaveError);
        StringAssert.Contains(model.SaveError, "IOException");
        Assert.IsTrue(model.ShowTipsAgainCommand.CanExecute(null), "The retry stays possible.");
        Assert.AreEqual(0, store.Saved.Count);
    }

    [TestMethod]
    public void RevealDataFolderCommand_RaisesTheRequest()
    {
        var model = Model();
        var raised = 0;
        model.RevealDataFolderRequested += (_, _) => raised++;

        model.RevealDataFolderCommand.Execute(null);
        model.RevealDataFolderCommand.Execute(null);

        Assert.AreEqual(2, raised, "Each click asks the window to reveal exactly once.");
    }

    private sealed class ToggleablePreferencesStore : IAppPreferencesStore
    {
        public bool Healthy { get; set; } = true;

        public Task<AppPreferences> LoadAsync(CancellationToken cancellationToken = default) =>
            Healthy
                ? Task.FromResult(AppPreferences.Default)
                : Task.FromException<AppPreferences>(
                    new PreferencesStoreException("The preference store is damaged."));

        public Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
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
