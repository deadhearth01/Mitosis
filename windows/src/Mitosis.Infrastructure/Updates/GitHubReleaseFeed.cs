using System.Net;
using System.Text.Json;
using Mitosis.Application.Updates;
using Mitosis.Domain.Updates;

namespace Mitosis.Infrastructure.Updates;

/// <summary>The one network request Mitosis makes: a minimal read of public GitHub
/// release metadata. No token, no user identifier, no telemetry; GitHub sees the
/// caller's IP exactly as any HTTPS request would reveal it. The endpoint is fixed and
/// the response is bounded.</summary>
public sealed class GitHubReleaseFeed : IReleaseFeed
{
    public static readonly Uri DefaultEndpoint =
        new("https://api.github.com/repos/deadhearth01/Mitosis/releases?per_page=10");

    private const long MaxResponseBytes = 512 * 1024;

    private readonly HttpClient client;
    private readonly Uri endpoint;
    private readonly string userAgent;

    public GitHubReleaseFeed(string currentVersion, HttpClient? client = null, Uri? endpoint = null)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            throw new ArgumentException("A current version is required.", nameof(currentVersion));
        }

        this.client = client ?? new HttpClient();
        this.endpoint = endpoint ?? DefaultEndpoint;
        userAgent = $"Mitosis/{ReleaseVersion.DisplayVersion(currentVersion)}";
    }

    public async Task<IReadOnlyList<ReleaseCandidate>> FetchAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd(userAgent);

        using var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new ReleaseFeedException("GitHub didn't answer normally. Try again later.");
        }

        if (response.Content.Headers.ContentLength is > MaxResponseBytes)
        {
            throw new ReleaseFeedException("GitHub sent a response Mitosis couldn't read.");
        }

        using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        // Content-Length is a hint, not a guarantee (chunked responses omit it), so the
        // body itself is bounded while reading. The cap is inclusive: exactly 512 KB is
        // readable, one byte more is a refusal.
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await stream
                .ReadAsync(chunk.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaxResponseBytes)
            {
                throw new ReleaseFeedException("GitHub sent a response Mitosis couldn't read.");
            }

            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        JsonDocument document;
        try
        {
            document = await JsonDocument
                .ParseAsync(buffer, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            throw new ReleaseFeedException("GitHub sent a response Mitosis couldn't read.");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new ReleaseFeedException("GitHub sent a response Mitosis couldn't read.");
            }

            var releases = new List<ReleaseCandidate>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object
                    || !element.TryGetProperty("tag_name", out var tag)
                    || tag.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(tag.GetString())
                    || !element.TryGetProperty("html_url", out var url)
                    || url.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(url.GetString()))
                {
                    throw new ReleaseFeedException("GitHub sent a response Mitosis couldn't read.");
                }

                releases.Add(new ReleaseCandidate(
                    tag.GetString()!,
                    ReadFlag(element, "draft"),
                    ReadFlag(element, "prerelease"),
                    url.GetString()!));
            }

            return releases;
        }
    }

    private static bool ReadFlag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property)
        && property.ValueKind == JsonValueKind.True;
}
