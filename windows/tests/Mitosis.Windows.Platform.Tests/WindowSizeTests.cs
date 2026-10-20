using Mitosis.Windows.Platform.Windowing;

namespace Mitosis.Windows.Platform.Tests;

[TestClass]
public sealed class WindowSizeTests
{
    [TestMethod]
#pragma warning disable MSTEST0032 // Values are deliberately compile-time constants: this test pins
    // the macOS reference numbers so any change is a conscious, reviewed decision.
    public void Values_MatchTheMacReference()
    {
        Assert.AreEqual(1060, WindowSizes.MainDefaultWidth);
        Assert.AreEqual(720, WindowSizes.MainDefaultHeight);
        Assert.AreEqual(820, WindowSizes.MainMinimumWidth);
        Assert.AreEqual(520, WindowSizes.MainMinimumHeight);
        Assert.AreEqual(820, WindowSizes.HelpDefaultWidth);
        Assert.AreEqual(580, WindowSizes.HelpDefaultHeight);
        Assert.AreEqual(700, WindowSizes.HelpMinimumWidth);
        Assert.AreEqual(500, WindowSizes.HelpMinimumHeight);
        Assert.AreEqual(540, WindowSizes.SettingsWidth);
        Assert.AreEqual(580, WindowSizes.OnboardingWidth);
        Assert.AreEqual(460, WindowSizes.OnboardingHeight);
        Assert.AreEqual(700, WindowSizes.WizardPickWidth);
        Assert.AreEqual(680, WindowSizes.WizardPickHeight);
    }
#pragma warning restore MSTEST0032

    [TestMethod]
    public void Bounded_PassesSensibleSizesThrough()
    {
        Assert.AreEqual((900, 600), WindowSizes.Bounded(900, 600, 820, 520));
        Assert.AreEqual((820, 520), WindowSizes.Bounded(820, 520, 820, 520));
    }

    [TestMethod]
    public void Bounded_ClampsBelowMinimumToTheMinimum()
    {
        Assert.AreEqual((820, 520), WindowSizes.Bounded(100, 10, 820, 520));
        Assert.AreEqual((700, 500), WindowSizes.Bounded(50, 1, 700, 500));
    }

    [TestMethod]
    public void Bounded_RefusesZeroAndNegativeRequests()
    {
        Assert.AreEqual((820, 520), WindowSizes.Bounded(0, 0, 820, 520));
        Assert.AreEqual((820, 520), WindowSizes.Bounded(-50, -30, 820, 520));
        Assert.AreEqual((580, 460), WindowSizes.Bounded(int.MinValue, int.MinValue, 580, 460));
    }

    [TestMethod]
    public void Bounded_KeepsSizesOnTheDefensiveCeiling()
    {
        Assert.AreEqual(
            (WindowSizes.MaximumDimension, WindowSizes.MaximumDimension),
            WindowSizes.Bounded(int.MaxValue, int.MaxValue, 820, 520));
        Assert.AreEqual((10_000, 600), WindowSizes.Bounded(50_000, 600, 820, 520));
    }

    [TestMethod]
    public void Bounded_ClampsEachAxisIndependently()
    {
        Assert.AreEqual((820, 700), WindowSizes.Bounded(600, 700, 820, 520),
            "One axis below the floor must not clamp the other.");
    }
}
