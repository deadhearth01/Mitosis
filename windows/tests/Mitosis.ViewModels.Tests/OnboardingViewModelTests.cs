using Mitosis.Application.Preferences;
using Mitosis.Windows.ViewModels;

namespace Mitosis.ViewModels.Tests;

/// <summary>First-launch welcome flow (macOS Onboarding.swift): deterministic pages,
/// both finish actions persist only the onboarded flag, and failure is honest.</summary>
[TestClass]
public sealed class OnboardingViewModelTests
{
    private static OnboardingViewModel Model(FakePreferencesStore? store = null) =>
        new(store ?? new FakePreferencesStore());

    [TestMethod]
    public void DefaultPages_MatchTheMacThreePageFlow()
    {
        var model = Model();

        Assert.AreEqual(3, model.Pages.Count);
        Assert.AreEqual("wave", model.Pages[0].Pose);
        Assert.AreEqual("key", model.Pages[1].Pose);
        Assert.AreEqual("split", model.Pages[2].Pose);
        Assert.AreEqual("Welcome to Mitosis", model.Pages[0].Title);
        StringAssert.Contains(model.Pages[1].Title, "permissions");
        StringAssert.Contains(model.Pages[2].Title, "first clone");
        StringAssert.Contains(model.Pages[2].Caption!, "not implemented yet",
            "The last page states plainly what saving does today.");
    }

    [TestMethod]
    public void InitialState_ShowsFirstPageWithoutBack()
    {
        var model = Model();

        Assert.AreEqual(0, model.PageIndex);
        Assert.IsFalse(model.CanGoBack);
        Assert.IsFalse(model.IsLast);
        Assert.IsFalse(model.Finished);
        Assert.IsFalse(model.StartFirstClone);
        Assert.AreEqual("Page 1 of 3", model.PageLabel);
        Assert.AreEqual(model.Pages[0].Title, model.PageTitle);
        Assert.AreEqual(model.Pages[0].Text, model.PageText);
        Assert.IsFalse(model.PageShowCaption, "The first page has no caption.");
        Assert.IsFalse(model.PreviousPageCommand.CanExecute(null));
    }

    [TestMethod]
    public void NextAndBack_NavigateWithinBounds()
    {
        var model = Model();

        model.NextPageCommand.Execute(null);

        Assert.AreEqual(1, model.PageIndex);
        Assert.IsTrue(model.CanGoBack);
        Assert.IsTrue(model.PreviousPageCommand.CanExecute(null));
        Assert.IsFalse(model.IsLast);
        Assert.AreEqual("Page 2 of 3", model.PageLabel);
        Assert.AreEqual(model.Pages[1].Title, model.PageTitle);

        model.PreviousPageCommand.Execute(null);

        Assert.AreEqual(0, model.PageIndex);
        Assert.IsFalse(model.CanGoBack);
        Assert.AreEqual("Page 1 of 3", model.PageLabel);
    }

    [TestMethod]
    public void Next_CannotPassTheLastPage()
    {
        var model = Model();
        model.NextPageCommand.Execute(null);
        model.NextPageCommand.Execute(null);

        Assert.IsTrue(model.IsLast);
        Assert.IsFalse(model.NextPageCommand.CanExecute(null), "Forward stops at the last page.");

        model.NextPageCommand.Execute(null);
        Assert.AreEqual(2, model.PageIndex, "Extra Next clicks must not loop or crash.");
    }

    [TestMethod]
    public void Back_CannotMoveBeforeTheFirstPage()
    {
        var model = Model();

        Assert.IsFalse(model.PreviousPageCommand.CanExecute(null));

        model.PreviousPageCommand.Execute(null);
        Assert.AreEqual(0, model.PageIndex, "Back is a no-op on the first page.");
    }

    [TestMethod]
    public void CustomPages_AreUsedVerbatim()
    {
        var pages = new[]
        {
            new OnboardingPageContent("thinking", "Custom", "Body"),
            new OnboardingPageContent("stats", "Custom last", "Body", "Caption"),
        };

        var model = new OnboardingViewModel(new FakePreferencesStore(), pages);

        Assert.AreEqual(2, model.Pages.Count);
        Assert.AreEqual("Page 1 of 2", model.PageLabel);
        model.NextPageCommand.Execute(null);
        Assert.IsTrue(model.IsLast);
        Assert.AreEqual("Custom last", model.PageTitle);
        Assert.IsTrue(model.PageShowCaption);
        Assert.AreEqual("ms-appx:///Assets/pose-stats.png", model.PageImage);
    }

    [TestMethod]
    public void PoseImages_PointAtContentAssets()
    {
        var model = Model();

        StringAssert.StartsWith(model.PageImage, "ms-appx:///Assets/pose-");
        Assert.AreEqual("ms-appx:///Assets/pose-key.png", model.Pages[1].PoseImage);
    }

    [TestMethod]
    public async Task FinishLater_PersistsOnboardedOnly()
    {
        var store = new FakePreferencesStore();
        var model = Model(store);

        await model.FinishAsync(startFirstClone: false, CancellationToken.None);

        Assert.IsTrue(model.Finished);
        Assert.IsFalse(model.StartFirstClone, "Later never opens the wizard.");
        Assert.IsNull(model.ErrorMessage);
        Assert.AreEqual(1, store.Saved.Count);
        Assert.IsTrue(store.Saved.Single().Onboarded);
        Assert.IsTrue(store.Saved.Single().ShowTips,
            "The fresh-install tips default (on) is preserved, not reset.");
    }

    [TestMethod]
    public async Task FinishChooseAnApp_MarksTheWizardHandoff()
    {
        var store = new FakePreferencesStore();
        var model = Model(store);

        await model.FinishAsync(startFirstClone: true, CancellationToken.None);

        Assert.IsTrue(model.Finished);
        Assert.IsTrue(model.StartFirstClone, "Choose an App asks the window to open the wizard.");
        Assert.IsTrue(store.Saved.Single().Onboarded);
    }

    [TestMethod]
    public async Task Finish_MergesWithTheStoredFlags()
    {
        var store = new FakePreferencesStore(
            new AppPreferences { Onboarded = false, ShowTips = false });
        var model = Model(store);

        await model.FinishAsync(startFirstClone: false, CancellationToken.None);

        var saved = store.Saved.Single();
        Assert.IsTrue(saved.Onboarded);
        Assert.IsFalse(saved.ShowTips, "An existing tips preference is preserved, not reset.");
    }

    [TestMethod]
    public async Task PersistenceFailure_IsHonestAndTheRecheckRetries()
    {
        var store = new FakePreferencesStore(saveFailure: attempt =>
            attempt == 0 ? new PreferencesStoreException("Couldn't save preferences.") : null);
        var model = Model(store);

        await model.FinishAsync(startFirstClone: false, CancellationToken.None);

        Assert.IsFalse(model.Finished, "Completion is never faked without persistence.");
        Assert.IsFalse(model.StartFirstClone);
        Assert.IsNotNull(model.ErrorMessage);

        await model.FinishAsync(startFirstClone: true, CancellationToken.None);

        Assert.IsTrue(model.Finished, "A retry that persists completes the flow.");
        Assert.IsTrue(model.StartFirstClone);
        Assert.IsNull(model.ErrorMessage, "A success clears the earlier error.");
        Assert.IsTrue(store.Saved.Single().Onboarded);
    }

    [TestMethod]
    public async Task FinishTwice_IsIdempotent()
    {
        var store = new FakePreferencesStore();
        var model = Model(store);

        await model.FinishAsync(startFirstClone: true, CancellationToken.None);
        await model.FinishAsync(startFirstClone: false, CancellationToken.None);

        Assert.IsTrue(model.Finished);
        Assert.IsTrue(model.StartFirstClone, "The first choice wins; a second finish changes nothing.");
        Assert.AreEqual(1, store.Saved.Count);
    }

    [TestMethod]
    public void EmptyCustomPages_AreRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => new OnboardingViewModel(
                new FakePreferencesStore(), Array.Empty<OnboardingPageContent>()));
    }

    [TestMethod]
    public void Constructor_RejectsNullStore()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new OnboardingViewModel(null!));
    }

    [TestMethod]
    public void Captions_ShowOnlyWhereContentHasOne()
    {
        var model = Model();

        Assert.IsNull(model.Pages[0].Caption);
        Assert.IsNotNull(model.Pages[1].Caption);
        Assert.IsNotNull(model.Pages[2].Caption);
    }
}
