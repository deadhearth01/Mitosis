namespace Mitosis.Windows.ViewModels;

/// <summary>Explicitly fake in-memory library mirroring the macOS snapshot fixtures
/// (Snapshots.swift SampleData: Signal Work/Family, Notion Client A, Claude Research,
/// Cursor Side Project, Obsidian Journal). Preview only — never presented as discovered
/// or cloned applications. The engine will replace this source in later stages.</summary>
public static class PreviewData
{
    public const string BannerVersion = "0.3.0";

    public static MainViewModel CreateMain() =>
        new(CreateGroups(), CreateClones(), isPreview: true, showUpdateBanner: true, bannerVersion: BannerVersion);

    public static IReadOnlyList<AppGroupItem> CreateGroups() => new[]
    {
        new AppGroupItem("claude", "Claude", "C", "#D6791F"),
        new AppGroupItem("cursor", "Cursor", "C", "#1A1A1A"),
        new AppGroupItem("notion", "Notion", "N", "#111111"),
        new AppGroupItem("obsidian", "Obsidian", "O", "#6A5ACD"),
        new AppGroupItem("signal", "Signal", "S", "#3A76F0"),
    };

    public static IReadOnlyList<CloneItem> CreateClones() => new[]
    {
        new CloneItem(
            Id: Guid.Parse("00000000-0000-4000-8000-000000000001"),
            GroupId: "signal", Name: "Signal (Work)", Label: "Work", AppName: "Signal",
            SourceVersion: "8.2.1", Status: CloneStatusKind.UpToDate, IsRunning: true, BusyText: null,
            Mode: CloneModeKind.Identity, BadgeText: "W", BadgeColorHex: "#0A84FF",
            SourcePath: @"C:\Program Files\Signal\Signal.exe",
            DataPath: @"%LOCALAPPDATA%\Mitosis\Data\00000001",
            CreatedText: "27 Sep 2026 at 1:15 PM", UpdatedText: "2 hours ago", AvailableVersion: null,
            ExtraDiskText: "1.3 MB", DiskCaption: "The rest is shared with Signal",
            DataText: "48 MB", MemoryText: "412 MB", MemoryCaption: "6 processes",
            CpuText: "3%", CpuCaption: "Right now", HasLinkSchemes: true),
        new CloneItem(
            Id: Guid.Parse("00000000-0000-4000-8000-000000000002"),
            GroupId: "signal", Name: "Signal (Family)", Label: "Family", AppName: "Signal",
            SourceVersion: "8.2.1", Status: CloneStatusKind.UpToDate, IsRunning: false, BusyText: null,
            Mode: CloneModeKind.Identity, BadgeText: "F", BadgeColorHex: "#30D158",
            SourcePath: @"C:\Program Files\Signal\Signal.exe",
            DataPath: @"%LOCALAPPDATA%\Mitosis\Data\00000002",
            CreatedText: "20 Sep 2026 at 9:02 AM", UpdatedText: "3 days ago", AvailableVersion: null,
            ExtraDiskText: "1.1 MB", DiskCaption: "The rest is shared with Signal",
            DataText: "31 MB", MemoryText: "—", MemoryCaption: "Not running",
            CpuText: "—", CpuCaption: "Not running", HasLinkSchemes: true),
        new CloneItem(
            Id: Guid.Parse("00000000-0000-4000-8000-000000000003"),
            GroupId: "notion", Name: "Notion (Client A)", Label: "Client A", AppName: "Notion",
            SourceVersion: "3.0", Status: CloneStatusKind.UpdateAvailable, IsRunning: false, BusyText: null,
            Mode: CloneModeKind.Fallback, BadgeText: "A", BadgeColorHex: "#FF9F0A",
            SourcePath: @"C:\Program Files\Notion\Notion.exe",
            DataPath: @"%LOCALAPPDATA%\Mitosis\Data\00000003",
            CreatedText: "15 Sep 2026 at 4:40 PM", UpdatedText: "5 days ago", AvailableVersion: "3.1",
            ExtraDiskText: "2.4 MB", DiskCaption: "A small shortcut that opens Notion",
            DataText: "112 MB", MemoryText: "—", MemoryCaption: "Not running",
            CpuText: "—", CpuCaption: "Not running", HasLinkSchemes: false),
        new CloneItem(
            Id: Guid.Parse("00000000-0000-4000-8000-000000000004"),
            GroupId: "claude", Name: "Claude (Research)", Label: "Research", AppName: "Claude",
            SourceVersion: "1.4.2", Status: CloneStatusKind.UpToDate, IsRunning: true, BusyText: null,
            Mode: CloneModeKind.Identity, BadgeText: "R", BadgeColorHex: "#BF5AF2",
            SourcePath: @"C:\Program Files\Claude\Claude.exe",
            DataPath: @"%LOCALAPPDATA%\Mitosis\Data\00000004",
            CreatedText: "10 Sep 2026 at 11:25 AM", UpdatedText: "yesterday", AvailableVersion: null,
            ExtraDiskText: "1.7 MB", DiskCaption: "The rest is shared with Claude",
            DataText: "230 MB", MemoryText: "388 MB", MemoryCaption: "6 processes",
            CpuText: "3%", CpuCaption: "Right now", HasLinkSchemes: true),
        new CloneItem(
            Id: Guid.Parse("00000000-0000-4000-8000-000000000005"),
            GroupId: "cursor", Name: "Cursor (Side Project)", Label: "Side Project", AppName: "Cursor",
            SourceVersion: "1.4.2", Status: CloneStatusKind.UpToDate, IsRunning: false,
            BusyText: "Refreshing…",
            Mode: CloneModeKind.Identity, BadgeText: "S", BadgeColorHex: "#FF375F",
            SourcePath: @"C:\Program Files\Cursor\Cursor.exe",
            DataPath: @"%LOCALAPPDATA%\Mitosis\Data\00000005",
            CreatedText: "2 Sep 2026 at 8:12 PM", UpdatedText: "2 hours ago", AvailableVersion: null,
            ExtraDiskText: "—", DiskCaption: "The rest is shared with Cursor",
            DataText: "—", MemoryText: "—", MemoryCaption: "Not running",
            CpuText: "—", CpuCaption: "Not running", HasLinkSchemes: false),
        new CloneItem(
            Id: Guid.Parse("00000000-0000-4000-8000-000000000006"),
            GroupId: "obsidian", Name: "Obsidian (Journal)", Label: "Journal", AppName: "Obsidian",
            SourceVersion: "1.4.2", Status: CloneStatusKind.OriginalMissing, IsRunning: false, BusyText: null,
            Mode: CloneModeKind.Identity, BadgeText: "J", BadgeColorHex: "#8E8E93",
            SourcePath: @"C:\Program Files\Obsidian\Obsidian.exe",
            DataPath: @"%LOCALAPPDATA%\Mitosis\Data\00000006",
            CreatedText: "28 Aug 2026 at 10:03 AM", UpdatedText: "2 weeks ago", AvailableVersion: null,
            ExtraDiskText: "1.9 MB", DiskCaption: "The rest is shared with Obsidian",
            DataText: "64 MB", MemoryText: "—", MemoryCaption: "Not running",
            CpuText: "—", CpuCaption: "Not running", HasLinkSchemes: false),
    };
}
