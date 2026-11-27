using System.Text.RegularExpressions;

namespace Mitosis.Windows.Platform.Discovery;

/// <summary>Name-based classification for installed entries that are not applications a
/// person opens: runtimes, redistributables, media extensions, drivers, language packs,
/// and background installers or servers. Windows flags some of these itself
/// (<c>SystemComponent=1</c>, dropped before merging), but many installer-authored
/// entries carry no flag, so the sidebar needs a conservative, documented list.
///
/// This is a display filter only. The entry stays discovered, keeps its id, and is one
/// toggle away in Settings; nothing is read, executed, or removed to classify it.
///
/// Rules are deliberately narrow — word boundaries, a versioned "Driver", exact names —
/// so a real application that merely contains a word like "Driver" or "Runtime"
/// ("Driver Booster", "Runtime Commander") stays visible.</summary>
public static class SystemComponentClassifier
{
    private static readonly Regex[] Rules =
    {
        // Runtimes and redistributables: "Microsoft .NET Runtime - 8.0.33 (x64)",
        // "Microsoft Visual C++ 2013 Redistributable (x64)".
        new(@"^microsoft \.net runtime\b", Options),
        new(@"\bredistributable\b", Options),

        // Media and codec packages: "AV1 Video Extension", "HEVC Video Extensions from
        // Device Manufacturer".
        new(@"\bextensions?\b", Options),

        // OS content packs and databases: "English (United Kingdom) Local Experience
        // Pack", "Microsoft Windows Application Compatibility Fix Database".
        new(@"\blocal experience pack\b", Options),
        new(@"\bcompatibility fix database\b", Options),

        // Background installers: "GlideX Service Installer".
        new(@"\bservice installer\b", Options),

        // Driver packages carry a dotted version: "NVIDIA Graphics Driver 582.99",
        // "NVIDIA HD Audio Driver 1.4.5.7". "Driver Booster" has no version and stays.
        new(@"\bdriver\s+\d+(\.\d+)+\b", Options),

        // Windows optional components: "Microsoft Ink Handwriting Recognition
        // (en-IN)", "Mobile devices", "Game Speech Window".
        new(@"\bink handwriting recognition\b", Options),
        new(@"^mobile devices$", Options),
        new(@"^game speech window$", Options),

        // Shell-hosted system apps named repeatedly as noise: "App Installer".
        new(@"^app installer$", Options),

        // Office side-by-side content and services: "Microsoft 365 - en-us",
        // "Microsoft.Office.ActionsServer", "OfficePushNotificationsUtility",
        // "Local AI Manager for Microsoft 365".
        new(@"^microsoft 365 - ", Options),
        new(@"^microsoft\.office\.", Options),
        new(@"^officepushnotificationsutility$", Options),
        new(@"^local ai manager for microsoft 365$", Options),
    };

    private const RegexOptions Options =
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    /// <summary>True when the display name is a component rather than an application a
    /// person opens. Parasitic by design: a wrong "false" only leaves noise in the list,
    /// while a wrong "true" hides a real app (recoverable with the Settings toggle).</summary>
    public static bool IsComponentName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        var name = displayName.Trim();
        foreach (var rule in Rules)
        {
            if (rule.IsMatch(name))
            {
                return true;
            }
        }

        return false;
    }
}
