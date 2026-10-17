namespace Mitosis.Application.Preferences;

/// <summary>User flags for implemented Windows UI features only. Every member must have a
/// real, testable effect (Stage 13 rules): unimplemented macOS settings (menu bar/tray,
/// update checking, clone auto-update, default clone mode) are deliberately absent.
/// Defaults mirror the macOS registration in <c>MitosisRootApp.swift:11</c> minus the
/// not-yet-implemented keys: the welcome flow shows once, tips are on.</summary>
public sealed record AppPreferences
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>The 3-page welcome flow (macOS Onboarding.swift) has completed or been
    /// dismissed. Persisted by every finish action so it is shown only on first launch.</summary>
    public bool Onboarded { get; init; }

    /// <summary>Wake tip / welcome state behind Settings ▸ Advanced ▸ Show Tips Again.</summary>
    public bool ShowTips { get; init; }

    /// <summary>Defaults for a missing preference store: not onboarded yet, tips on
    /// (macOS registers <c>showTipsKey: true</c>, MitosisRootApp.swift:11).</summary>
    public static AppPreferences Default { get; } = new() { ShowTips = true };
}

/// <summary>Persistence behind <see cref="AppPreferences"/>. Loading a missing store yields
/// defaults and never creates directories or files; corrupt or unsupported stores raise
/// <see cref="PreferencesStoreException"/> (callers fall back to defaults, never overwrite),
/// keeping the never-silently-replace rule of the profile store.</summary>
public interface IAppPreferencesStore
{
    Task<AppPreferences> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default);
}

/// <summary>Base for preference-store failures. Messages are user-safe: no storage paths
/// (macOS-parity redaction happens at the presentation layer if needed).</summary>
public class PreferencesStoreException : Exception
{
    public PreferencesStoreException(string message)
        : base(message)
    {
    }

    public PreferencesStoreException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
