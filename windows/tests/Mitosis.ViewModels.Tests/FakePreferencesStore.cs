using Mitosis.Application.Preferences;

namespace Mitosis.ViewModels.Tests;

/// <summary>In-memory preference store: deterministic loads, recorded saves, and
/// optional counted failures so the honest error paths are exercised.</summary>
internal sealed class FakePreferencesStore : IAppPreferencesStore
{
    private readonly Func<int, Exception?> saveFailure;
    private int saveAttempts;

    public FakePreferencesStore(
        AppPreferences? initial = null,
        Func<int, Exception?>? saveFailure = null)
    {
        Next = initial ?? AppPreferences.Default;
        this.saveFailure = saveFailure ?? (_ => null);
    }

    public AppPreferences Next { get; set; }

    public int Loads { get; private set; }

    public int Saves { get; private set; }

    public List<AppPreferences> Saved { get; } = new();

    public Task<AppPreferences> LoadAsync(CancellationToken cancellationToken = default)
    {
        Loads++;
        return Task.FromResult(Next);
    }

    public Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default)
    {
        var attempt = saveAttempts++;
        if (saveFailure(attempt) is { } failure)
        {
            return Task.FromException(failure);
        }

        Saves++;
        Saved.Add(preferences);
        return Task.CompletedTask;
    }
}
