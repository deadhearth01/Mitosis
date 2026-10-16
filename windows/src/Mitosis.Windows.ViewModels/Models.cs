using Mitosis.Domain;

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

/// <summary>One discovered application with its compatibility assessment.
/// Preview clone rows (<see cref="CloneItem"/>) and discovered apps are never mixed:
/// production shows apps, explicit preview mode shows clones.</summary>
public sealed record DiscoveredAppItem(DiscoveredApp App, CompatibilityAssessment Assessment)
{
    /// <summary>First letter or digit for the row tile; "#" when none is usable.</summary>
    public string Initial
    {
        get
        {
            var first = App.DisplayName.Trim().FirstOrDefault(char.IsLetterOrDigit);
            return first == default ? "#" : char.ToUpperInvariant(first).ToString();
        }
    }

    public string StatusText => MainViewModel.AssessmentStatusText(Assessment.Status);
}

/// <summary>A sidebar group of discovered applications, keyed by initial letter.</summary>
public sealed class AppGroupView
{
    public AppGroupView(AppGroupItem group, IReadOnlyList<DiscoveredAppItem> items)
    {
        Group = group;
        Items = items;
    }

    public AppGroupItem Group { get; }

    public IReadOnlyList<DiscoveredAppItem> Items { get; }
}

/// <summary>A persisted profile whose application is not currently discovered.
/// The record is always preserved; it is surfaced, never deleted.</summary>
public sealed record OrphanProfileItem(CloneProfile Profile)
{
    public string Initial
    {
        get
        {
            var first = Profile.Name.Trim().FirstOrDefault(char.IsLetterOrDigit);
            return first == default ? "#" : char.ToUpperInvariant(first).ToString();
        }
    }

    public string StateText => MainViewModel.ProfileStateText(Profile.State);
}

/// <summary>Request for the view to show an explanatory dialog (engine actions are
/// placeholders in Stage 2 and must never pretend to succeed).</summary>
public sealed record DialogRequest(string Title, string Message);
