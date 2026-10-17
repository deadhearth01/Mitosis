using Mitosis.Windows.ViewModels;

namespace Mitosis.ViewModels.Tests;

/// <summary>Help window state: nine macOS topics with Windows-honest content, the
/// Start topic selected first (HelpView.swift:5), and only honest action buttons.</summary>
[TestClass]
public sealed class HelpViewModelTests
{
    [TestMethod]
    public void Topics_MatchTheMacOrderAndCount()
    {
        var model = new HelpViewModel();

        CollectionAssert.AreEqual(
            new[]
            {
                HelpTopicKind.Start, HelpTopicKind.Apps, HelpTopicKind.Permissions,
                HelpTopicKind.Links, HelpTopicKind.Updates, HelpTopicKind.Stats,
                HelpTopicKind.Deleting, HelpTopicKind.Trouble, HelpTopicKind.Cli,
            },
            model.Topics.Select(t => t.Kind).ToList());
        Assert.AreEqual(9, model.Topics.Count);
    }

    [TestMethod]
    public void SelectedTopic_OpensOnStart()
    {
        var model = new HelpViewModel();

        Assert.AreEqual(model.Topics[0], model.SelectedTopic);
        Assert.AreEqual("Getting started", model.PageTitle);
        Assert.IsTrue(model.PageParagraphs.Count > 0);
        Assert.IsNull(model.PageButton, "Getting started has no action button.");
        Assert.IsFalse(model.PageShowButton);
    }

    [TestMethod]
    public void EveryTopic_HasHonestContentAndUniqueAsset()
    {
        var model = new HelpViewModel();

        foreach (var topic in model.Topics)
        {
            Assert.IsTrue(topic.Paragraphs.Count > 0, $"{topic.Kind} needs paragraphs.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(topic.Title));
            Assert.IsFalse(string.IsNullOrWhiteSpace(topic.PoseImage));
            StringAssert.StartsWith(topic.PoseImage, "ms-appx:///Assets/pose-");
        }

        Assert.AreEqual(
            model.Topics.Count,
            model.Topics.Select(t => t.Pose).Distinct().Count(),
            "One pose per topic keeps the mascot poses intentional.");
    }

    [TestMethod]
    public void HonestCopy_MarksUnimplementedFeatures()
    {
        var model = new HelpViewModel();
        var paragraphs = model.Topics.SelectMany(t => t.Paragraphs).ToList();
        var combined = string.Join(" ", paragraphs);

        StringAssert.Contains(combined, "not implemented yet",
            "Unimplemented features say so plainly.");
        StringAssert.Contains(combined, "Planned",
            "Planned profiles are never described as working clones.");
    }

    [TestMethod]
    public void OnlyTwoTopics_OfferAnActionButton()
    {
        var model = new HelpViewModel();

        var withButtons = model.Topics.Where(t => t.Button != null).ToList();
        Assert.AreEqual(2, withButtons.Count);

        var permissions = withButtons.Single(t => t.Button!.Kind == HelpButtonKind.PrivacySettings);
        Assert.AreEqual(HelpTopicKind.Permissions, permissions.Kind);
        var trouble = withButtons.Single(t => t.Button!.Kind == HelpButtonKind.ReportProblem);
        Assert.AreEqual(HelpTopicKind.Trouble, trouble.Kind);
    }

    [TestMethod]
    public void SelectingATopic_UpdatesThePage()
    {
        var model = new HelpViewModel();
        var trouble = model.Topics.Single(t => t.Kind == HelpTopicKind.Trouble);

        model.SelectedTopic = trouble;

        Assert.AreEqual("Troubleshooting", model.PageTitle);
        Assert.IsTrue(model.PageShowButton, "Troubleshooting offers Report a Problem.");
        Assert.AreEqual("Report a Problem…", model.PageButton!.Label);
        Assert.AreEqual(HelpButtonKind.ReportProblem, model.PageButton.Kind);

        var start = model.Topics.Single(t => t.Kind == HelpTopicKind.Start);
        model.SelectedTopic = start;
        Assert.IsFalse(model.PageShowButton);
        Assert.IsNull(model.PageButton);
    }

    [TestMethod]
    public void ClearingTheSelection_ShowsAnEmptyPage()
    {
        var model = new HelpViewModel();
        model.SelectedTopic = null;

        Assert.IsNull(model.SelectedTopic);
        Assert.AreEqual(string.Empty, model.PageTitle);
        Assert.AreEqual(0, model.PageParagraphs.Count);
        Assert.IsFalse(model.PageShowButton);
    }

    [TestMethod]
    public void UriFor_MapsWindowsHonestTargets()
    {
        Assert.AreEqual("ms-settings:privacy", HelpViewModel.UriFor(HelpButtonKind.PrivacySettings));
        Assert.AreEqual(
            "https://github.com/deadhearth01/Mitosis/issues/new/choose",
            HelpViewModel.UriFor(HelpButtonKind.ReportProblem));
    }

    [TestMethod]
    public void UriFor_RejectsUnknownKinds()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => HelpViewModel.UriFor((HelpButtonKind)999));
    }
}
