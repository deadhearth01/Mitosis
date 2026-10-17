using Mitosis.Application.Preferences;

namespace Mitosis.Infrastructure.Preferences;

/// <summary>Versioned JSON preference store. Format: { "schemaVersion": 1, ... }.
/// Loading never creates anything: a missing file returns <see cref="AppPreferences.Default"/>;
/// unreadable, malformed, or unsupported files raise <see cref="PreferencesStoreException"/>
/// so callers keep defaults and never replace data they could not read. Saves are atomic
/// (temporary file plus same-directory replace) and serialized against overlapping writes,
/// matching <see cref="Mitosis.Infrastructure.Profiles.JsonCloneProfileRepository"/>.</summary>
public sealed class JsonAppPreferencesStore : IAppPreferencesStore
{
    private const string FileName = "preferences.json";

    private readonly string directory;
    private readonly SemaphoreSlim gate = new(1, 1);

    public JsonAppPreferencesStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Preference directory must not be empty.", nameof(directory));
        }

        this.directory = directory;
    }

    public async Task<AppPreferences> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteAsync(preferences, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private string FilePath => Path.Combine(directory, FileName);

    private async Task<AppPreferences> ReadAsync(CancellationToken cancellationToken)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return AppPreferences.Default;
        }
        catch (DirectoryNotFoundException)
        {
            return AppPreferences.Default;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PreferencesStoreException(
                $"Couldn't read the preference store ({ex.GetType().Name}).", ex);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return AppPreferences.Default;
        }

        PreferenceDocument document;
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
        try
        {
            document = System.Text.Json.JsonSerializer.Deserialize<PreferenceDocument>(json, options)
                ?? throw new PreferencesStoreException("The preference store is empty.");
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new PreferencesStoreException(
                "The preference store is damaged. It was left untouched — remove preferences.json to reset.", ex);
        }

        if (document.SchemaVersion != AppPreferences.CurrentSchemaVersion)
        {
            throw new PreferencesStoreException(
                $"Preference store schema {document.SchemaVersion} is not supported " +
                $"(expected {AppPreferences.CurrentSchemaVersion}).");
        }

        return new AppPreferences
        {
            Onboarded = document.Onboarded ?? false,
            ShowTips = document.ShowTips ?? true,
        };
    }

    private async Task WriteAsync(AppPreferences preferences, CancellationToken cancellationToken)
    {
        var document = new PreferenceDocument
        {
            SchemaVersion = AppPreferences.CurrentSchemaVersion,
            Onboarded = preferences.Onboarded,
            ShowTips = preferences.ShowTips,
        };

        string json;
        try
        {
            json = System.Text.Json.JsonSerializer.Serialize(document,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            throw new PreferencesStoreException("Couldn't encode preferences. Nothing was written.", ex);
        }

        string tempPath = Path.Combine(directory, FileName + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, FilePath, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            DeleteQuietly(tempPath);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DeleteQuietly(tempPath);
            throw new PreferencesStoreException(
                $"Couldn't save preferences ({ex.GetType().Name}). The existing store was left untouched.", ex);
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best effort: a stray temp file never corrupts the real store.
        }
    }

    private sealed class PreferenceDocument
    {
        public int SchemaVersion { get; set; }

        public bool? Onboarded { get; set; }

        public bool? ShowTips { get; set; }
    }
}
