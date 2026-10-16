using Mitosis.Windows.ViewModels;

namespace Mitosis.ViewModels.Tests;

[TestClass]
public sealed class MainViewModelTests
{
    private static MainViewModel Create() => PreviewData.CreateMain();

    [TestMethod]
    public void InitialState_SelectsNothingAndShowsBanner()
    {
        var model = Create();

        Assert.IsFalse(model.HasSelection);
        Assert.IsNull(model.SelectedClone);
        Assert.IsTrue(model.ShowUpdateBanner);
        Assert.AreEqual("0.3.0", model.BannerVersion);
        Assert.IsTrue(model.IsPreview, "Stage 2 runs on explicit preview data only.");
    }

    [TestMethod]
    public void Subtitle_CountsClones()
    {
        Assert.AreEqual("6 clones", Create().CloneCountSubtitle);
        Assert.AreEqual("1 clone", new MainViewModel(
            PreviewData.CreateGroups().Take(1),
            PreviewData.CreateClones().Take(1), isPreview: true).CloneCountSubtitle);
        Assert.AreEqual(string.Empty, new MainViewModel(
            Array.Empty<AppGroupItem>(), Array.Empty<CloneItem>(), isPreview: true).CloneCountSubtitle);
    }

    [TestMethod]
    public void SelectClone_ShowsDetailState()
    {
        var model = Create();
        var work = PreviewData.CreateClones().First(c => c.Name == "Signal (Work)");

        model.SelectedClone = work;

        Assert.IsTrue(model.HasSelection);
        Assert.AreEqual(work, model.SelectedClone);
        Assert.IsFalse(model.ShowUpdateNotice);
        Assert.IsFalse(model.ShowMissingNotice);
        Assert.IsTrue(model.ShowLinksCard, "Identity clone with schemes shows the links card.");
    }

    [TestMethod]
    public void ClearSelection_ShowsEmptyState()
    {
        var model = Create();
        model.SelectedClone = PreviewData.CreateClones().First();

        model.SelectedClone = null;

        Assert.IsFalse(model.HasSelection);
        Assert.IsFalse(model.ShowUpdateNotice);
        Assert.IsFalse(model.ShowMissingNotice);
        Assert.IsFalse(model.ShowLinksCard);
    }

    [TestMethod]
    public void SelectUpdateAvailable_ShowsUpdateNotice()
    {
        var model = Create();

        model.SelectedClone = PreviewData.CreateClones().First(c => c.Name == "Notion (Client A)");

        Assert.IsTrue(model.ShowUpdateNotice);
        Assert.IsFalse(model.ShowMissingNotice);
        Assert.IsFalse(model.ShowLinksCard, "Compatibility-mode clone hides the links card.");
    }

    [TestMethod]
    public void SelectMissing_ShowsMissingNotice()
    {
        var model = Create();

        model.SelectedClone = PreviewData.CreateClones().First(c => c.Name == "Obsidian (Journal)");

        Assert.IsTrue(model.ShowMissingNotice);
        Assert.AreEqual("Original missing", MainViewModel.StatusText(model.SelectedClone!));
    }

    [TestMethod]
    public void StatusText_MatchesMacCopy()
    {
        var clones = PreviewData.CreateClones().ToDictionary(c => c.Name);
        Assert.AreEqual("Running", MainViewModel.StatusText(clones["Signal (Work)"]));
        Assert.AreEqual("Not running", MainViewModel.StatusText(clones["Signal (Family)"]));
        Assert.AreEqual("Update available", MainViewModel.StatusText(clones["Notion (Client A)"]));
        Assert.AreEqual("Original missing", MainViewModel.StatusText(clones["Obsidian (Journal)"]));
    }

    [TestMethod]
    public void ModeDisplayName_MatchesMacCopy()
    {
        Assert.AreEqual("Full clone", MainViewModel.ModeDisplayName(CloneModeKind.Identity));
        Assert.AreEqual("Compatibility mode", MainViewModel.ModeDisplayName(CloneModeKind.Fallback));
    }

    [TestMethod]
    public void Search_FiltersByCloneAndAppName()
    {
        var model = Create();

        model.SearchText = "sig";

        var visible = model.VisibleGroups.SelectMany(g => g.Items).Select(c => c.Name).ToList();
        CollectionAssert.AreEquivalent(
            new[] { "Signal (Work)", "Signal (Family)" }, visible);
        Assert.IsFalse(model.HasNoResults);
    }

    [TestMethod]
    public void Search_NoMatch_ShowsNoResultsAndClearsSelection()
    {
        var model = Create();
        model.SelectedClone = PreviewData.CreateClones().First();

        model.SearchText = "zzz-no-such-app";

        Assert.IsTrue(model.HasNoResults);
        Assert.IsFalse(model.HasSelection, "Selected clone left the visible set.");
    }

    [TestMethod]
    public void Search_Clear_RestoresFullLibrary()
    {
        var model = Create();
        model.SearchText = "zzz-no-such-app";

        model.SearchText = string.Empty;

        Assert.IsFalse(model.HasNoResults);
        Assert.AreEqual(5, model.VisibleGroups.Count);
    }

    [TestMethod]
    public void BusyClone_SurfacesBusyText()
    {
        var busy = PreviewData.CreateClones().First(c => c.Name == "Cursor (Side Project)");
        Assert.AreEqual("Refreshing…", busy.BusyText);
    }

    [TestMethod]
    public void EngineCommands_RaisePlaceholderDialog()
    {
        var model = Create();
        var requests = new List<DialogRequest>();
        model.DialogRequested += (_, r) => requests.Add(r);
        model.SelectedClone = PreviewData.CreateClones().First();

        model.OpenCloneCommand.Execute(null);
        model.RefreshCommand.Execute(null);
        model.DeleteCommand.Execute(null);
        model.CleanCachesCommand.Execute(null);
        model.EditBadgeCommand.Execute(null);
        model.FindAppCommand.Execute(null);
        model.RevealCloneCommand.Execute(null);
        model.InstallUpdateCommand.Execute(null);
        model.NewCloneCommand.Execute(null);

        Assert.AreEqual(9, requests.Count);
        Assert.IsTrue(requests.All(r => r.Title.Contains("not available in this preview")),
            "Every engine action must be an honest placeholder, never a fake success.");
    }

    [TestMethod]
    public void SelectedDetailStrings_MatchMacCopy()
    {
        var model = Create();
        model.SelectedClone = PreviewData.CreateClones().First(c => c.Name == "Notion (Client A)");

        Assert.AreEqual("Clone of Notion 3.0", model.SelectedSubtitle);
        Assert.AreEqual("Notion 3.1 is installed. Refresh this clone to use it. Logins and data stay.", model.SelectedUpdateMessage);
        Assert.AreEqual("Compatibility mode", model.SelectedModeName);
        Assert.AreEqual("Own login and data; uses Notion's taskbar icon", model.SelectedModeNote);

        model.SelectedClone = PreviewData.CreateClones().First(c => c.Name == "Obsidian (Journal)");
        Assert.AreEqual("Obsidian isn't where it was. Show Mitosis where it is now to use this clone again.", model.SelectedMissingMessage);

        model.SelectedClone = PreviewData.CreateClones().First(c => c.Name == "Signal (Work)");
        Assert.AreEqual("Full clone", model.SelectedModeName);
        Assert.AreEqual("Own login, data, notifications and taskbar icon", model.SelectedModeNote);
    }

    [TestMethod]
    public void DismissBanner_HidesIt()
    {
        var model = Create();

        model.DismissBannerCommand.Execute(null);

        Assert.IsFalse(model.ShowUpdateBanner);
    }

    [TestMethod]
    public void Commands_RespectOriginalMissing()
    {
        var model = Create();
        model.SelectedClone = PreviewData.CreateClones().First(c => c.Name == "Obsidian (Journal)");

        Assert.IsFalse(model.RefreshCommand.CanExecute(null));
        Assert.IsFalse(model.EditBadgeCommand.CanExecute(null));
        Assert.IsFalse(model.RevealOriginalCommand.CanExecute(null));
        Assert.IsTrue(model.FindAppCommand.CanExecute(null));

        model.SelectedClone = PreviewData.CreateClones().First(c => c.Name == "Signal (Work)");

        Assert.IsTrue(model.RefreshCommand.CanExecute(null));
        Assert.IsTrue(model.EditBadgeCommand.CanExecute(null));
        Assert.IsFalse(model.FindAppCommand.CanExecute(null));
    }

    [TestMethod]
    public void EngineCommands_DisabledWithoutSelection()
    {
        var model = Create();

        Assert.IsFalse(model.OpenCloneCommand.CanExecute(null));
        Assert.IsFalse(model.DeleteCommand.CanExecute(null));
        Assert.IsTrue(model.NewCloneCommand.CanExecute(null));
    }
}
