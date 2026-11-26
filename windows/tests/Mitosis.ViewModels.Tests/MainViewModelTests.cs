using Mitosis.Application.Updates;
using Mitosis.Domain;
using Mitosis.Windows.ViewModels;

namespace Mitosis.ViewModels.Tests;

[TestClass]
public sealed class MainViewModelTests
{
    private static MainViewModel Create() => PreviewData.CreateMain();

    public TestContext TestContext { get; set; } = null!;

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
    public void Search_NoMatch_ShowsNoResultsButKeepsSelection()
    {
        var model = Create();
        var selected = PreviewData.CreateClones().First();
        model.SelectedClone = selected;

        model.SearchText = "zzz-no-such-app";

        Assert.IsTrue(model.HasNoResults);
        Assert.AreSame(selected, model.SelectedClone,
            "macOS keeps a selection whose row the search merely hides (AppModel.swift:112).");
        Assert.IsTrue(model.ShowCloneDetail, "The detail page stays with the hidden selection.");
        Assert.AreEqual("No results for \"zzz-no-such-app\"", model.NoResultsText);
    }

    [TestMethod]
    public void Search_Clear_RestoresSelectionHighlight()
    {
        var model = Create();
        var signal = PreviewData.CreateClones().First(c => c.Name == "Signal (Work)");
        model.SelectedClone = signal;

        model.SearchText = "zzz-no-such-app";
        Assert.IsFalse(model.VisibleGroups.SelectMany(g => g.Items).Any(c => c.Id == signal.Id));

        model.SearchText = string.Empty;

        Assert.AreSame(signal, model.SelectedClone);
        CollectionAssert.Contains(
            model.VisibleGroups.SelectMany(g => g.Items).ToList(), signal,
            "The row the query had hidden is selected again once the query clears.");
    }

    [TestMethod]
    public void NoResults_RequiresAnActiveQuery()
    {
        var model = new MainViewModel(
            Array.Empty<AppGroupItem>(), Array.Empty<CloneItem>(), isPreview: true);

        Assert.IsFalse(model.HasActiveQuery);
        Assert.IsFalse(model.HasNoResults,
            "An empty library is an empty state, not a search miss — macOS only shows "
            + "ContentUnavailableView.search for a typed query (MainWindow.swift:80).");

        model.SearchText = "   ";
        Assert.IsFalse(model.HasActiveQuery, "Whitespace-only text is not a query.");
        Assert.IsFalse(model.HasNoResults);

        model.SearchText = "anything";
        Assert.IsTrue(model.HasActiveQuery);
        Assert.IsTrue(model.HasNoResults);
        Assert.AreEqual("No results for \"anything\"", model.NoResultsText);
    }

    [TestMethod]
    public void ClearSearch_ClearsQueryAndReportsWhenThereIsNothingToClear()
    {
        var model = Create();
        Assert.IsFalse(model.ClearSearch(), "Nothing typed — an empty box's Esc must pass through.");

        model.SearchText = "sig";
        Assert.IsTrue(model.ClearSearch());
        Assert.AreEqual(string.Empty, model.SearchText);
        Assert.AreEqual(5, model.VisibleGroups.Count);
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
        model.UpdateBannerCommand.Execute(null);
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
    public async Task RealModel_ShowsTheBannerOnlyAfterANewerReleaseIsFound()
    {
        var updates = new UpdatesViewModel(
            new StubUpdateChecker(_ => Task.FromResult(
                UpdateCheckResult.Available("0.2.2", "0.2.3", "https://example.test/v0.2.3"))),
            "0.2.2+6f9ee6b");
        var model = new MainViewModel(
            Array.Empty<AppGroupItem>(), Array.Empty<CloneItem>(), isPreview: false, updates: updates);

        Assert.IsFalse(model.ShowUpdateBanner, "A real run starts with no fabricated banner.");

        var opened = new List<string>();
        model.OpenReleaseRequested += (_, url) => opened.Add(url);

        await updates.CheckAsync();

        Assert.IsTrue(model.ShowUpdateBanner);
        Assert.AreEqual("Mitosis 0.2.3 is available.", model.BannerTitle);
        Assert.AreEqual("View Release", model.UpdateBannerActionText);

        model.UpdateBannerCommand.Execute(null);

        CollectionAssert.AreEqual(new[] { "https://example.test/v0.2.3" }, opened);
    }

    [TestMethod]
    public void PreviewBanner_KeepsTheMacWordingAndOpensNothing()
    {
        var model = Create();

        Assert.IsTrue(model.ShowUpdateBanner);
        Assert.AreEqual("Update Now", model.UpdateBannerActionText);
        Assert.IsNull(model.UpdateReleaseUrl, "Preview never carries a real release link.");
    }

    private sealed class StubUpdateChecker(
        Func<CancellationToken, Task<UpdateCheckResult>> check) : IUpdateChecker
    {
        public Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default) =>
            check(cancellationToken);
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

    /// <summary>WinUI's ListViewItem falls back to the data item's ToString() for its
    /// accessible name; a record dump would put paths and internal state in the
    /// accessibility tree (found in Stage 27). Each row type names itself the way it reads.</summary>
    [TestMethod]
    public void RowToString_IsTheAccessibleName()
    {
        var clone = PreviewData.CreateClones().First(c => c.Name == "Signal (Work)");
        Assert.AreEqual("Work, Running", clone.ToString());

        var app = new DiscoveredAppItem(DiscoveredApps.Make("Android Studio"), new FakeAssessor().Assess(DiscoveredApps.Make("Android Studio")));
        Assert.AreEqual("Android Studio, Not yet assessed", app.ToString());

        var orphan = new OrphanProfileItem(CloneProfile.Create("currentuseruninstall:missing", "Slack (Old)"));
        Assert.AreEqual($"Slack (Old), {orphan.StateText}", orphan.ToString());
    }

    [TestMethod]
    public async Task PublisherLine_SeparatesPublisherAndVersion()
    {
        var discovery = new FakeDiscoveryService();
        discovery.Returns(new[] { DiscoveredApps.Make("Slack", "Slack Technologies") });
        var model = new MainViewModel(discovery, new FakeAssessor(), new FakeProfileRepository());
        await model.LoadAsync(TestContext.CancellationToken);

        model.SelectedApp = model.VisibleAppGroups.SelectMany(g => g.Items).Single();

        Assert.AreEqual("Slack Technologies · 1.0", model.SelectedAppPublisherLine);
    }

    /// <summary>Found in Stage 27: the error text was set while the loading flag was
    /// still true, so the retry panel's visibility never re-evaluated when loading
    /// finished and the panel never appeared.</summary>
    [TestMethod]
    public async Task ProfilesFailure_AnnouncesTheErrorStateWhenLoadingFinishes()
    {
        var discovery = new FakeDiscoveryService();
        discovery.Returns(new[] { DiscoveredApps.Make("Slack") });
        var profiles = new FakeProfileRepository { ListFailure = new IOException("boom") };
        var model = new MainViewModel(discovery, new FakeAssessor(), profiles);
        var announced = false;
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.ShowProfilesError) && model.ShowProfilesError)
            {
                announced = true;
            }
        };

        await model.LoadAsync(TestContext.CancellationToken);

        Assert.IsTrue(model.ShowProfilesError);
        Assert.IsTrue(announced, "The retry panel must be announced after the loading flag clears.");
    }
}
