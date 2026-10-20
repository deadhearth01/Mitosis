namespace Mitosis.Windows.Platform.Windowing;

/// <summary>Window sizing limits shared by the shell windows, quoted from the macOS
/// reference (docs/WINDOWS-DESIGN-SPEC.md §2): main window defaults to 1060×720 with
/// an 820×520 floor (MitosisRootApp.swift:17,24), Help defaults to 820×580 with a
/// 700×500 floor (HelpView.swift:18,31), Settings is a fixed 540-wide window
/// (SettingsView.swift:16), and the onboarding sheet is a fixed 580×460 (Onboarding.swift).
/// Pure values and validation only — presenter application happens in the window layer
/// so this file stays testable without a desktop.</summary>
public static class WindowSizes
{
    public const int MainDefaultWidth = 1060;

    public const int MainDefaultHeight = 720;

    /// <summary>macOS main-window floor (MitosisRootApp.swift:17). Below it the sidebar
    /// and detail panel clip; values under this are refused, not honored.</summary>
    public const int MainMinimumWidth = 820;

    public const int MainMinimumHeight = 520;

    public const int HelpDefaultWidth = 820;

    public const int HelpDefaultHeight = 580;

    public const int HelpMinimumWidth = 700;

    public const int HelpMinimumHeight = 500;

    /// <summary>macOS Settings window width (SettingsView.swift:16 is a fixed frame).</summary>
    public const int SettingsWidth = 540;

    public const int OnboardingWidth = 580;

    public const int OnboardingHeight = 460;

    public const int WizardPickWidth = 700;

    public const int WizardPickHeight = 680;

    /// <summary>Defensive ceiling for any window dimension so a bug can never request
    /// an absurd size.</summary>
    public const int MaximumDimension = 10_000;

    /// <summary>Bounds a requested size between the given floor and ceiling. Values at or
    /// below zero (or below the floor) fall back to the floor, never to the caller's raw
    /// input, so a miscomputed size degrades to a usable one instead of a broken window.</summary>
    public static (int Width, int Height) Bounded(int width, int height, int minimumWidth, int minimumHeight)
    {
        var boundedWidth = Math.Clamp(Math.Max(width, 0), minimumWidth, MaximumDimension);
        var boundedHeight = Math.Clamp(Math.Max(height, 0), minimumHeight, MaximumDimension);
        return (boundedWidth, boundedHeight);
    }
}
