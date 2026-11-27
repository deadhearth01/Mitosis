using Mitosis.Application.Diagnostics;
using Mitosis.Application.Preferences;
using Mitosis.Application.Updates;
using Mitosis.Windows.ViewModels;

namespace Mitosis.ViewModels.Tests;

/// <summary>Settings window state: Advanced ▸ Show Tips Again (macOS
/// SettingsView.swift:156-166) and the informational data folder.</summary>
[TestClass]
public sealed class SettingsViewModelTests
{
    private const string DataFolder = @"C:\Users\me\AppData\Local\Mitosis";

    private static SettingsViewModel Model(FakePreferencesStore? store = null, DoctorFacts? facts = null) =>
        new(store ?? new FakePreferencesStore(), DataFolder, facts);

    private static DoctorFacts Facts() => new(
        "0.2.2+test", ".NET 10.0.4", "Windows 11", "X64", "2.5.1",
        DataFolder, true, Path.Combine(DataFolder, "Logs"));

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

    [TestMethod]
    public void Diagnostics_ExposeTheLogFolderAndReport()
    {
        var model = Model(facts: Facts());

        Assert.AreEqual(Path.Combine(DataFolder, "Logs"), model.LogFolderPath);
        var report = model.DiagnosticsReport;
        StringAssert.Contains(report, "Application version: 0.2.2+test");
        StringAssert.Contains(report, "Runtime: .NET 10.0.4");
        StringAssert.Contains(report, @"Log folder: " + Path.Combine(DataFolder, "Logs"));
        StringAssert.Contains(report, "Not recorded: application data, credentials, file contents, or network activity.");
    }

    [TestMethod]
    public void Diagnostics_DefaultFactsStayInsideTheDataFolder()
    {
        var model = Model();

        Assert.AreEqual(Path.Combine(DataFolder, "Logs"), model.LogFolderPath);
        StringAssert.Contains(model.DiagnosticsReport, "Application version: unknown");
        StringAssert.Contains(model.DiagnosticsReport, "Data folder writable: no");
    }

    [TestMethod]
    public void DiagnosticsCommands_RaiseTheirRequests()
    {
        var model = Model(facts: Facts());
        var reveals = 0;
        string? copied = null;
        model.RevealLogFolderRequested += (_, _) => reveals++;
        model.CopyDiagnosticsRequested += (_, text) => copied = text;

        model.RevealLogFolderCommand.Execute(null);
        model.CopyDiagnosticsCommand.Execute(null);

        Assert.AreEqual(1, reveals);
        Assert.IsNotNull(copied);
        StringAssert.Contains(copied, "Mitosis diagnostics");
        StringAssert.Contains(copied, "Application version: 0.2.2+test");
    }

    [TestMethod]
    public async Task Updates_ForwardTheSharedCheckState()
    {
        var updates = new UpdatesViewModel(
            new StubUpdateChecker(_ => Task.FromResult(
                UpdateCheckResult.Available("0.2.2", "0.2.3", "https://example.test/v0.2.3"))),
            "0.2.2+6f9ee6b");
        var model = new SettingsViewModel(
            new FakePreferencesStore(), DataFolder, Facts(), updates);

        Assert.IsTrue(model.CanCheckForUpdates);
        Assert.AreEqual("0.2.2", model.CurrentVersionText);
        Assert.IsFalse(model.HasUpdateLink);
        StringAssert.Contains(model.UpdateStatusText, "Check GitHub Releases");

        await model.CheckForUpdatesAsync();

        Assert.IsTrue(model.HasUpdateLink);
        Assert.AreEqual(new Uri("https://example.test/v0.2.3"), model.UpdateReleaseUrl);
        StringAssert.Contains(model.UpdateStatusText, "0.2.3 is available");
        Assert.AreEqual("Check for Updates", model.CheckForUpdatesLabel);
    }

    [TestMethod]
    public async Task Dispose_StopsForwardingTheSharedState()
    {
        var updates = new UpdatesViewModel(
            new StubUpdateChecker(_ => Task.FromResult(UpdateCheckResult.UpToDate("0.2.2"))),
            "0.2.2");
        var model = new SettingsViewModel(
            new FakePreferencesStore(), DataFolder, Facts(), updates);
        model.Dispose();

        var forwarded = false;
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.UpdateStatusText))
            {
                forwarded = true;
            }
        };

        await updates.CheckAsync();

        Assert.IsFalse(forwarded, "A closed Settings window must not keep listening.");
    }

    [TestMethod]
    public async Task Checking_DisablesTheSettingsButtonUntilTheCheckEnds()
    {
        var pending = new TaskCompletionSource<UpdateCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var updates = new UpdatesViewModel(
            new StubUpdateChecker(_ => pending.Task), "0.2.2");
        var model = new SettingsViewModel(
            new FakePreferencesStore(), DataFolder, Facts(), updates);

        var running = model.CheckForUpdatesAsync();

        Assert.IsFalse(model.CanCheckForUpdates);
        Assert.IsFalse(model.CheckForUpdatesCommand.CanExecute(null));
        Assert.AreEqual("Checking…", model.CheckForUpdatesLabel);

        pending.SetResult(UpdateCheckResult.UpToDate("0.2.2"));
        await running;

        Assert.IsTrue(model.CanCheckForUpdates);
        Assert.IsTrue(model.CheckForUpdatesCommand.CanExecute(null));
    }

    [TestMethod]
    public void WithoutAFeed_SettingsSaysSoInsteadOfFakingACheck()
    {
        var model = Model();

        Assert.IsFalse(model.CanCheckForUpdates);
        Assert.IsFalse(model.CheckForUpdatesCommand.CanExecute(null));
        StringAssert.Contains(model.UpdateStatusText, "not available");
        Assert.IsFalse(model.HasUpdateLink);
    }

    [TestMethod]
    public async Task ShowSystemComponents_LoadsTheStoredValue()
    {
        var store = new FakePreferencesStore(new AppPreferences { ShowSystemComponents = true });
        var model = Model(store);

        await model.LoadAsync();

        Assert.IsTrue(model.ShowSystemComponents);
        Assert.AreEqual(0, store.Saves, "Loading must not rewrite the store.");
    }

    [TestMethod]
    public async Task ShowSystemComponents_PersistsAndRaisesTheChange()
    {
        var store = new FakePreferencesStore(new AppPreferences { Onboarded = true, ShowTips = true });
        var model = Model(store);
        await model.LoadAsync();
        var changes = 0;
        model.PreferencesChanged += (_, _) => changes++;

        model.ShowSystemComponents = true;
        for (var i = 0; i < 100 && store.Saves == 0; i++)
        {
            await Task.Delay(10);
        }

        Assert.IsTrue(model.ShowSystemComponents);
        Assert.AreEqual(1, store.Saves);
        Assert.IsTrue(store.Saved.Single().ShowSystemComponents);
        Assert.AreEqual(1, changes, "The main window is told to re-filter immediately.");
    }

    [TestMethod]
    public async Task ShowSystemComponents_FailedSave_RestoresTheToggle()
    {
        var store = new FakePreferencesStore(
            new AppPreferences { Onboarded = true },
            _ => new PreferencesStoreException("disk full"));
        var model = Model(store);

        model.ShowSystemComponents = true;
        for (var i = 0; i < 100 && model.SaveError == null; i++)
        {
            await Task.Delay(10);
        }

        Assert.IsFalse(model.ShowSystemComponents,
            "The toggle must not claim a value the store rejected.");
        Assert.IsNotNull(model.SaveError);
    }

    private sealed class StubUpdateChecker(
        Func<CancellationToken, Task<UpdateCheckResult>> check) : IUpdateChecker
    {
        public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default) =>
            check(cancellationToken);
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
