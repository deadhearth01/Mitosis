using Mitosis.Application.Profiles;
using Mitosis.Domain;

namespace Mitosis.Infrastructure.Profiles;

/// <summary>Versioned JSON profile store. Format: { "schemaVersion": 1, "profiles": [...] }.
/// Missing file means an empty repository; malformed or unsupported files raise instead
/// of being replaced. Saves are atomic (temporary file plus same-directory replace) and
/// serialized against overlapping writes. Only this directory is ever touched.</summary>
public sealed class JsonCloneProfileRepository : ICloneProfileRepository
{
    public const int CurrentSchemaVersion = 1;

    private const string FileName = "profiles.json";

    private readonly string directory;
    private readonly SemaphoreSlim gate = new(1, 1);

    public JsonCloneProfileRepository(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Profile directory must not be empty.", nameof(directory));
        }

        this.directory = directory;
    }

    public async Task<IReadOnlyList<CloneProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadAllAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<CloneProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
            return records.FirstOrDefault(p => p.Id == id);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<CloneProfile> AddAsync(CloneProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
            var validated = profile.Validate();
            if (records.ContainsId(validated.Id))
            {
                throw new DuplicateProfileException($"A profile with this ID already exists ({validated.Id}).");
            }

            if (records.ContainsName(validated.Name))
            {
                throw new DuplicateProfileException($"You already have {validated.Name}.");
            }

            records.Add(validated);
            await WriteAllAsync(records, cancellationToken).ConfigureAwait(false);
            return validated;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<CloneProfile> UpdateAsync(CloneProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
            var validated = profile.Validate();
            var index = records.IndexOfId(validated.Id);
            if (index < 0)
            {
                throw new ProfileNotFoundException($"No profile with ID {validated.Id} exists.");
            }

            if (records.ContainsName(validated.Name, exceptId: validated.Id))
            {
                throw new DuplicateProfileException($"You already have {validated.Name}.");
            }

            records[index] = validated;
            await WriteAllAsync(records, cancellationToken).ConfigureAwait(false);
            return validated;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
            var index = records.IndexOfId(id);
            if (index < 0)
            {
                throw new ProfileNotFoundException($"No profile with ID {id} exists.");
            }

            records.RemoveAt(index);
            await WriteAllAsync(records, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private string FilePath => Path.Combine(directory, FileName);

    private static string Sanitize(string text)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            text = text.Replace(home, "~", StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }

    private async Task<List<CloneProfile>> ReadAllAsync(CancellationToken cancellationToken)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return new List<CloneProfile>();
        }
        catch (DirectoryNotFoundException)
        {
            return new List<CloneProfile>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ProfileStoreException(
                $"Couldn't read the profile store ({ex.GetType().Name}). Check permissions and try again.", ex);
        }

        ProfileDocument document;
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
        try
        {
            document = System.Text.Json.JsonSerializer.Deserialize<ProfileDocument>(json, options)
                ?? throw new InvalidProfileException("Profile store is empty.");
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new ProfileStoreException(
                "The profile store is damaged. It was left untouched — restore or remove profiles.json to continue.", ex);
        }

        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            throw new UnsupportedProfileSchemaException(
                $"Profile store schema {document.SchemaVersion} is not supported (expected {CurrentSchemaVersion}).");
        }

        var records = new List<CloneProfile>();
        foreach (var dto in document.Profiles ?? Enumerable.Empty<ProfileDto>())
        {
            try
            {
                records.Add(dto.ToProfile().Validate());
            }
            catch (Exception ex) when (ex is InvalidProfileException or CloneNameException)
            {
                throw new ProfileStoreException(
                    $"A stored profile record is invalid and was left untouched: {Sanitize(ex.Message)}", ex);
            }
        }

        var duplicateIds = records.GroupBy(p => p.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateIds.Count > 0)
        {
            throw new ProfileStoreException(
                $"The profile store contains a duplicate ID ({duplicateIds[0]}) and was left untouched.");
        }

        var duplicateNames = records.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateNames.Count > 0)
        {
            throw new ProfileStoreException(
                $"The profile store contains a duplicate name and was left untouched: {duplicateNames[0]}.");
        }

        return records.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Id)
            .ToList();
    }

    private async Task WriteAllAsync(List<CloneProfile> records, CancellationToken cancellationToken)
    {
        var document = new ProfileDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            Profiles = records
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Id)
                .Select(ProfileDto.FromProfile)
                .ToList(),
        };

        string json;
        try
        {
            json = System.Text.Json.JsonSerializer.Serialize(document,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
        {
            throw new ProfileStoreException("Couldn't encode profiles. Nothing was written.", ex);
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
            throw new ProfileStoreException(
                $"Couldn't save profiles ({ex.GetType().Name}). The existing store was left untouched.", ex);
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

    private sealed class ProfileDocument
    {
        public int SchemaVersion { get; set; }

        public List<ProfileDto>? Profiles { get; set; }
    }

    private sealed class ProfileDto
    {
        public Guid Id { get; set; }

        public string? AppId { get; set; }

        public string? Name { get; set; }

        public string? BadgeText { get; set; }

        public string? BadgeColor { get; set; }

        public string? State { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public static ProfileDto FromProfile(CloneProfile profile) => new()
        {
            Id = profile.Id,
            AppId = profile.AppId,
            Name = profile.Name,
            BadgeText = profile.BadgeText,
            BadgeColor = profile.BadgeColor,
            State = profile.State.ToString(),
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
        };

        public CloneProfile ToProfile()
        {
            if (!Enum.TryParse<CloneProfileState>(State, out var state))
            {
                throw new InvalidProfileException($"Unknown lifecycle state '{State}'.");
            }

            if (string.IsNullOrWhiteSpace(AppId) || string.IsNullOrWhiteSpace(Name))
            {
                throw new InvalidProfileException("Stored profile is missing its app reference or name.");
            }

            return new CloneProfile(Id, AppId, Name, BadgeText, BadgeColor, state, CreatedAt, UpdatedAt);
        }
    }
}

internal static class ProfileRecordList
{
    public static bool ContainsId(this List<CloneProfile> records, Guid id) =>
        records.Any(p => p.Id == id);

    public static bool ContainsName(this List<CloneProfile> records, string name, Guid? exceptId = null) =>
        records.Any(p => p.Id != exceptId
            && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    public static int IndexOfId(this List<CloneProfile> records, Guid id) =>
        records.FindIndex(p => p.Id == id);
}
