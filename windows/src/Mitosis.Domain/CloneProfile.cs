namespace Mitosis.Domain;

/// <summary>Lifecycle of a clone profile metadata record. A record is metadata only until
/// the engine provisions and verifies it — see <see cref="CloneProfileState"/> rules.</summary>
public enum CloneProfileState
{
    /// <summary>Metadata recorded; nothing provisioned. Never shown as a working clone.</summary>
    Planned,

    /// <summary>Provisioning in progress (future engine work).</summary>
    Provisioning,

    /// <summary>Provisioned and launch-verified by the engine (set only then).</summary>
    Ready,

    /// <summary>Provisioning or verification failed; safe to retry.</summary>
    Failed,

    /// <summary>Associated application is currently undiscovered; record is kept.</summary>
    Unavailable,
}

/// <summary>Explicit transition rules. Anything not listed is rejected.</summary>
public static class CloneProfileTransitions
{
    public static bool IsValid(CloneProfileState from, CloneProfileState to) => (from, to) switch
    {
        (CloneProfileState.Planned, CloneProfileState.Provisioning) => true,
        (CloneProfileState.Provisioning, CloneProfileState.Ready) => true,
        (CloneProfileState.Provisioning, CloneProfileState.Failed) => true,
        (CloneProfileState.Failed, CloneProfileState.Provisioning) => true,
        (CloneProfileState.Ready, CloneProfileState.Provisioning) => true,
        (CloneProfileState.Ready, CloneProfileState.Unavailable) => true,
        (CloneProfileState.Unavailable, CloneProfileState.Provisioning) => true,
        (CloneProfileState.Unavailable, CloneProfileState.Ready) => true,
        _ => false,
    };
}

/// <summary>Badge presets mirrored from the macOS New Clone form (8 colors). A badge color
/// is a preset name (case-insensitive) or #RRGGBB; anything else is rejected.</summary>
public static class BadgePreset
{
    public static readonly IReadOnlyList<(string Name, string Hex)> Presets = new[]
    {
        ("blue", "#0A84FF"), ("green", "#30D158"), ("orange", "#FF9F0A"), ("red", "#FF453A"),
        ("purple", "#BF5AF2"), ("pink", "#FF375F"), ("yellow", "#FFD60A"), ("gray", "#8E8E93"),
    };

    public static bool IsValidColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return false;
        }

        var text = color.Trim();
        if (Presets.Any(p => string.Equals(p.Name, text, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return text.Length == 7 && text[0] == '#'
            && text.Skip(1).All(c => Uri.IsHexDigit(c));
    }

    public static bool IsValidText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Trim().Length is >= 1 and <= 2;
    }
}

/// <summary>Independent clone profile: persistent metadata for one intended instance of a
/// discovered application. References the stable application identity; never duplicates
/// the discovery record and never stores executable paths as identifiers.</summary>
public sealed record CloneProfile(
    Guid Id,
    string AppId,
    string Name,
    string? BadgeText,
    string? BadgeColor,
    CloneProfileState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static CloneProfile Create(string appId, string name, string? badgeText = null, string? badgeColor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        var validatedName = CloneName.Validate(name);
        var now = DateTimeOffset.UtcNow;
        return new CloneProfile(
            Guid.NewGuid(), appId.Trim(), validatedName,
            NormalizeBadgeText(badgeText), NormalizeBadgeColor(badgeColor),
            CloneProfileState.Planned, now, now).Validate();
    }

    public CloneProfile Rename(string name) => this with
    {
        Name = CloneName.Validate(name),
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    public CloneProfile ChangeBadge(string? badgeText, string? badgeColor) => this with
    {
        BadgeText = NormalizeBadgeText(badgeText),
        BadgeColor = NormalizeBadgeColor(badgeColor),
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    public CloneProfile TransitionTo(CloneProfileState state, DateTimeOffset? timestamp = null)
    {
        if (!CloneProfileTransitions.IsValid(State, state))
        {
            throw new InvalidProfileException(
                $"Cannot move profile '{Name}' from {State} to {state}.");
        }

        return this with { State = state, UpdatedAt = timestamp ?? DateTimeOffset.UtcNow };
    }

    public CloneProfile Validate()
    {
        if (Id == Guid.Empty)
        {
            throw new InvalidProfileException("Profile ID must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(AppId))
        {
            throw new InvalidProfileException("Profile must reference an application ID.");
        }

        CloneName.Validate(Name);

        if (BadgeText != null && !BadgePreset.IsValidText(BadgeText))
        {
            throw new InvalidProfileException($"Badge text '{BadgeText}' must be 1–2 characters.");
        }

        if (BadgeColor != null && !BadgePreset.IsValidColor(BadgeColor))
        {
            throw new InvalidProfileException($"Badge color '{BadgeColor}' is not a preset or #RRGGBB.");
        }

        if (CreatedAt.Offset != TimeSpan.Zero || UpdatedAt.Offset != TimeSpan.Zero)
        {
            throw new InvalidProfileException("Profile timestamps must be UTC.");
        }

        if (UpdatedAt < CreatedAt)
        {
            throw new InvalidProfileException("Profile update time cannot precede creation time.");
        }

        return this;
    }

    private static string? NormalizeBadgeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim().ToUpperInvariant();
        if (!BadgePreset.IsValidText(trimmed))
        {
            throw new InvalidProfileException($"Badge text '{text}' must be 1–2 characters.");
        }

        return trimmed;
    }

    private static string? NormalizeBadgeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        var trimmed = color.Trim();
        if (!BadgePreset.IsValidColor(trimmed))
        {
            throw new InvalidProfileException($"Badge color '{color}' is not a preset or #RRGGBB.");
        }

        return trimmed;
    }
}

/// <summary>Thrown when profile data violates domain rules. Carries no local paths.</summary>
public sealed class InvalidProfileException : Exception
{
    public InvalidProfileException(string message)
        : base(message)
    {
    }
}
