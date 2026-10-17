using System.Collections.ObjectModel;

namespace Mitosis.Windows.ViewModels;

/// <summary>Decorative mascot poses used by Help and onboarding (macOS Mascot poses).
/// The App layer maps each pose to the in-place content asset pose-*.png from the
/// original brand tree; the assets themselves are never modified.</summary>
public enum HelpPose
{
    Wave,
    Key,
    Split,
    Guide,
    Search,
    Point,
    Refresh,
    Stats,
    Goodbye,
    Oops,
    Thinking,
}

/// <summary>Provisional SF Symbol → Windows glyph mapping for help topic icons
/// (macOS HelpView.swift:40-52). Presented through the App layer; pending visual
/// verification like the rest of the Stage 2 font/icon mapping.</summary>
public enum HelpSymbol
{
    Sparkles,
    CheckmarkSeal,
    LockShield,
    Link,
    CyclingArrows,
    BarChart,
    Trash,
    WrenchScrewdriver,
    Terminal,
}

/// <summary>The one action button offered on a help page (macOS HelpView.swift:146-151).</summary>
public sealed record HelpTopicButton(string Label, HelpButtonKind Kind);

public enum HelpButtonKind
{
    PrivacySettings,
    ReportProblem,
}

public enum HelpTopicKind
{
    Start,
    Apps,
    Permissions,
    Links,
    Updates,
    Stats,
    Deleting,
    Trouble,
    Cli,
}

/// <summary>One help topic: macOS structure (HelpView.swift), Windows-honest content.
/// The nine macOS topics are kept with their titles and poses, but the paragraphs
/// describe only what the Windows implementation actually does today and say plainly
/// what is macOS-only, engine work not yet implemented, or future. Nothing claims a
/// Planned profile is a working clone.</summary>
public sealed class HelpTopic
{
    private static readonly HelpTopic[] WindowsTopics = CreateTopics();

    private HelpTopic(
        HelpTopicKind kind,
        string title,
        HelpSymbol symbol,
        HelpPose pose,
        string[] paragraphs,
        HelpTopicButton? button)
    {
        Kind = kind;
        Title = title;
        Symbol = symbol;
        Pose = pose;
        Paragraphs = paragraphs;
        Button = button;
    }

    public HelpTopicKind Kind { get; }

    public string Title { get; }

    public HelpSymbol Symbol { get; }

    public HelpPose Pose { get; }

    /// <summary>Content asset for the decorative mascot, hidden from the accessibility
    /// tree (one-mascot rule, matrix W60). Same pose mapping as macOS HelpView.</summary>
    public string PoseImage => $"ms-appx:///Assets/pose-{Pose}.png";

    public IReadOnlyList<string> Paragraphs { get; }

    public HelpTopicButton? Button { get; }

    public static IReadOnlyList<HelpTopic> All => WindowsTopics;

    private static HelpTopic[] CreateTopics() => new[]
    {
        new HelpTopic(
            HelpTopicKind.Start,
            "Getting started",
            HelpSymbol.Sparkles,
            HelpPose.Guide,
            new[]
            {
                "Mitosis lets you run separate copies of your apps. Each copy, called a clone, gets its own login, settings, and data, so you can use Slack (Work) and Slack (Personal) side by side. The original app is never changed.",
                "On Windows you can already pick an app (Ctrl+N), give it a label like Work, and save it. The copy itself is not created yet — saving records a planned profile, and its label says so: Planned — metadata only, not a working clone.",
                "Cloning arrives when validated provisioning lands in a later stage. This guide describes how the finished Windows app will work, and marks everywhere that differs from what is implemented today.",
            },
            null),
        new HelpTopic(
            HelpTopicKind.Apps,
            "Which apps work",
            HelpSymbol.CheckmarkSeal,
            HelpPose.Search,
            new[]
            {
                "Mitosis lists the apps it can find registered on Windows: current-user and machine uninstall registrations, and packaged apps.",
                "As on macOS, most apps built with Electron or Chromium work best: clones get their own login, data, notifications, and taskbar icon. Apps with developer-tied features clone in compatibility mode, with the original app's taskbar icon. Apple's own apps and iCloud-tied apps aren't supported.",
                "The app picker shows a compatibility status before you start: Supported, Needs review, Not supported, or Not yet assessed. Until the cloning engine lands, these statuses are advisory: no copying happens for any app.",
            },
            null),
        new HelpTopic(
            HelpTopicKind.Permissions,
            "Permissions",
            HelpSymbol.LockShield,
            HelpPose.Key,
            new[]
            {
                "When a clone runs on Windows, Windows decides what it may do, per app. The first time a clone needs notifications, your files, the camera, or the microphone, Windows asks again.",
                "That keeps each copy's access separate. You can review it in Windows Settings ▸ Privacy & security.",
                "This matters once clones can run. Today Mitosis records planned profiles only and launches nothing.",
            },
            new HelpTopicButton("Open Privacy & Security", HelpButtonKind.PrivacySettings)),
        new HelpTopic(
            HelpTopicKind.Links,
            "Sign-in links",
            HelpSymbol.Link,
            HelpPose.Point,
            new[]
            {
                "Many apps sign you in through your browser, and the browser hands a link back to whichever copy of the app it can find.",
                "The macOS app can take over those links per clone (Send sign-in links to the right copy). Windows shows the toggle's user interface, but the routing engine behind it is not implemented yet: the toggle holds its state only, and links are never intercepted.",
            },
            null),
        new HelpTopic(
            HelpTopicKind.Updates,
            "App updates",
            HelpSymbol.CyclingArrows,
            HelpPose.Refresh,
            new[]
            {
                "When an original app updates, the macOS app rebuilds its clones automatically in the background and restores the previous copy on a failed rebuild. That engine is not implemented on Windows yet.",
                "Once it lands, the settings controlling it (Update clones automatically, and Mitosis's own update checks) will be added to Settings — toggles are not shown before they have an effect.",
            },
            null),
        new HelpTopic(
            HelpTopicKind.Stats,
            "Stats and caches",
            HelpSymbol.BarChart,
            HelpPose.Stats,
            new[]
            {
                "The macOS app shows what each clone adds to disk and, while it runs, its memory and CPU use, and can delete cache files the app can download again. Logins and settings stay.",
                "The Windows detail page shows placeholder tiles until the engine lands: the live collector behind Extra disk, Data, Memory, and CPU is not implemented yet, and Clean Caches is disabled.",
            },
            null),
        new HelpTopic(
            HelpTopicKind.Deleting,
            "Deleting clones",
            HelpSymbol.Trash,
            HelpPose.Goodbye,
            new[]
            {
                "On macOS, deleting a clone moves it — and optionally its data folder — to the Trash, so nothing is erased right away.",
                "The Windows port does not delete anything yet: clone deletion needs the engine. Planned profiles can be recorded today, and their records are kept even when the app is no longer discovered.",
            },
            null),
        new HelpTopic(
            HelpTopicKind.Trouble,
            "Troubleshooting",
            HelpSymbol.WrenchScrewdriver,
            HelpPose.Oops,
            new[]
            {
                "If the app list is empty or wrong: apps come from Windows registrations — uninstall entries (current user, machine 64-bit and 32-bit) and packaged apps. Restarting Mitosis refreshes the list; use Retry when an error is shown.",
                "If profiles can't be saved or loaded: the profile store may be damaged. Mitosis leaves it untouched and says so. Restore it from backup or remove profiles.json, then retry.",
                "Still stuck? Choose Report a Problem… below. Error reports from Mitosis leave out your name and files.",
            },
            new HelpTopicButton("Report a Problem…", HelpButtonKind.ReportProblem)),
        new HelpTopic(
            HelpTopicKind.Cli,
            "Command line",
            HelpSymbol.Terminal,
            HelpPose.Thinking,
            new[]
            {
                "The macOS app ships a mitosis command (list, clone, refresh, delete, doctor, and more), installed from Settings.",
                "No Windows command-line tool exists yet, so the Settings command-line installer is absent here. It will appear when the Windows CLI does.",
            },
            null),
    };
}

/// <summary>Help window state: topic list plus the selected page. Mirrors the macOS
/// NavigationSplitView with .start selected by default (HelpView.swift:5-19). The
/// window opens external targets; the view model stays dependency-free.</summary>
public sealed class HelpViewModel : ObservableObject
{
    private static readonly string[] ButtonUris =
    {
        "ms-settings:privacy",
        "https://github.com/deadhearth01/Mitosis/issues/new/choose",
    };

    private HelpTopic? selectedTopic;

    public HelpViewModel()
    {
        Topics = new ReadOnlyCollection<HelpTopic>(HelpTopic.All.ToArray());
        selectedTopic = Topics.FirstOrDefault();
    }

    public IReadOnlyList<HelpTopic> Topics { get; }

    /// <summary>macOS opens with .start selected (HelpView.swift:5).</summary>
    public HelpTopic? SelectedTopic
    {
        get => selectedTopic;
        set
        {
            if (SetProperty(ref selectedTopic, value))
            {
                RaisePropertyChanged(nameof(PageTitle));
                RaisePropertyChanged(nameof(PageParagraphs));
                RaisePropertyChanged(nameof(PageButton));
                RaisePropertyChanged(nameof(PageShowButton));
            }
        }
    }

    public bool PageShowButton => SelectedTopic?.Button != null;

    public string PageTitle => SelectedTopic?.Title ?? string.Empty;

    public IReadOnlyList<string> PageParagraphs => SelectedTopic?.Paragraphs ?? Array.Empty<string>();

    public HelpTopicButton? PageButton => SelectedTopic?.Button;

    /// <summary>URI for a help button. Windows privacy page instead of the macOS
    /// System-Settings deep link; the repository URL matches macOS SystemLinks.</summary>
    public static string UriFor(HelpButtonKind kind) => kind switch
    {
        HelpButtonKind.PrivacySettings => ButtonUris[0],
        HelpButtonKind.ReportProblem => ButtonUris[1],
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown help action."),
    };
}
