namespace Mitosis.Windows.ViewModels;

/// <summary>Presentation mirror of macOS CloneStatus (MitosisCore/CloneMaintenance.swift).
/// Strings shown in the UI reuse the macOS copy verbatim.</summary>
public enum CloneStatusKind
{
    UpToDate,
    UpdateAvailable,
    OriginalMissing,
}

/// <summary>Presentation mirror of macOS SupportLevel (MitosisCore/Models.swift).</summary>
public enum SupportKind
{
    Full,
    Limited,
    Unsupported,
}

/// <summary>Presentation mirror of macOS CloneMode display names (ErrorReport.swift).</summary>
public enum CloneModeKind
{
    Identity,
    Fallback,
}

/// <summary>One source app in the sidebar's By-App section.</summary>
public sealed record AppGroupItem(string Id, string Name, string Initial, string TintHex);

/// <summary>One clone row / detail page. Display strings are precomputed; in Stage 2 they
/// come from preview data only (see PreviewData). The engine will supply live values later.</summary>
public sealed record CloneItem(
    Guid Id,
    string GroupId,
    string Name,
    string Label,
    string AppName,
    string SourceVersion,
    CloneStatusKind Status,
    bool IsRunning,
    string? BusyText,
    CloneModeKind Mode,
    string BadgeText,
    string BadgeColorHex,
    string SourcePath,
    string DataPath,
    string CreatedText,
    string UpdatedText,
    string? AvailableVersion,
    string ExtraDiskText,
    string DiskCaption,
    string DataText,
    string MemoryText,
    string MemoryCaption,
    string CpuText,
    string CpuCaption,
    bool HasLinkSchemes);

/// <summary>A sidebar group with its currently visible clones.</summary>
public sealed class CloneGroupView
{
    public CloneGroupView(AppGroupItem group, IReadOnlyList<CloneItem> items)
    {
        Group = group;
        Items = items;
    }

    public AppGroupItem Group { get; }

    public IReadOnlyList<CloneItem> Items { get; }
}

/// <summary>Request for the view to show an explanatory dialog (engine actions are
/// placeholders in Stage 2 and must never pretend to succeed).</summary>
public sealed record DialogRequest(string Title, string Message);
