using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mitosis.Application.Evidence;
using Mitosis.Domain;

namespace Mitosis.Infrastructure.Evidence;

/// <summary>File-per-bundle JSON evidence store. One file per record ID under the given
/// directory; atomic temp-plus-replace writes; malformed or tampered files fail safely
/// without overwriting the original. The SHA-256 integrity hash detects accidental
/// alteration of the stored bytes — it is not tamper-proofing and implies nothing
/// about confidentiality.</summary>
public sealed class JsonEvidenceStore : IEvidenceStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string directory;
    private readonly SemaphoreSlim gate = new(1, 1);

    public JsonEvidenceStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Evidence directory must not be empty.", nameof(directory));
        }

        this.directory = directory;
    }

    public async Task<IReadOnlyList<EvidenceBundle>> ListAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var bundles = new List<EvidenceBundle>();
            string[] files;
            try
            {
                files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json") : Array.Empty<string>();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new EvidenceStoreException(
                    $"Couldn't list the evidence store ({ex.GetType().Name}).", ex);
            }

            foreach (var file in files.OrderBy(f => f, StringComparer.Ordinal))
            {
                bundles.Add(await ReadFileAsync(file, cancellationToken).ConfigureAwait(false));
            }

            return bundles;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<EvidenceBundle?> GetAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = Path.Combine(directory, recordId.ToString("N") + ".json");
            if (!File.Exists(path))
            {
                return null;
            }

            var bundle = await ReadFileAsync(path, cancellationToken).ConfigureAwait(false);
            return bundle.RecordId == recordId ? bundle : null;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<EvidenceBundle> SaveAsync(EvidenceBundle bundle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var errors = EvidenceEvaluator.Validate(bundle);
            if (errors.Count > 0)
            {
                throw new EvidenceStoreException(
                    "Evidence bundle is invalid: " + string.Join(" ", errors.Take(3)));
            }

            var stamped = bundle with { IntegrityHash = null };
            var hash = ComputeHash(CanonicalJson(stamped));
            stamped = stamped with { IntegrityHash = hash };

            var json = JsonSerializer.Serialize(stamped, Options);
            var path = Path.Combine(directory, stamped.RecordId.ToString("N") + ".json");
            var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
                File.Move(tempPath, path, overwrite: true);
            }
            catch (OperationCanceledException)
            {
                DeleteQuietly(tempPath);
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                DeleteQuietly(tempPath);
                throw new EvidenceStoreException(
                    $"Couldn't save evidence ({ex.GetType().Name}). The existing store was left untouched.", ex);
            }

            return stamped;
        }
        finally
        {
            gate.Release();
        }
    }

    internal static string ComputeHash(string canonicalJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string CanonicalJson(EvidenceBundle bundle) =>
        JsonSerializer.Serialize(bundle, Options);

    private static async Task<EvidenceBundle> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new EvidenceStoreException(
                $"Couldn't read the evidence store ({ex.GetType().Name}).", ex);
        }

        EvidenceBundle bundle;
        try
        {
            bundle = JsonSerializer.Deserialize<EvidenceBundle>(json, Options)
                ?? throw new EvidenceStoreException("An evidence file is empty and was left untouched.");
        }
        catch (JsonException ex)
        {
            throw new EvidenceStoreException("An evidence file is malformed and was left untouched.", ex);
        }

        if (bundle.SchemaVersion != EvidenceEvaluator.CurrentSchemaVersion)
        {
            throw new UnsupportedEvidenceSchemaException(
                $"Evidence schema {bundle.SchemaVersion} is not supported.");
        }

        var errors = EvidenceEvaluator.Validate(bundle);
        if (errors.Count > 0)
        {
            throw new EvidenceStoreException(
                "An evidence file is invalid and was left untouched: " + string.Join(" ", errors.Take(3)));
        }

        var expected = ComputeHash(CanonicalJson(bundle with { IntegrityHash = null }));
        if (!string.Equals(expected, bundle.IntegrityHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new EvidenceStoreException(
                "An evidence file failed its integrity check and was left untouched.");
        }

        return bundle;
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
}
