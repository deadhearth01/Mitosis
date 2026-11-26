using System.Net;
using System.Text;
using Mitosis.Application.Updates;
using Mitosis.Infrastructure.Updates;

namespace Mitosis.Infrastructure.Tests;

/// <summary>The GitHub feed is a bounded, read-only JSON read: fixed endpoint, no token,
/// no telemetry, and no partial trust. Anything unexpected is a readable failure, never
/// a guessed release.</summary>
[TestClass]
public sealed class GitHubReleaseFeedTests
{
    private const string ReleasesJson = """
        [
          {
            "tag_name": "v0.2.3",
            "draft": false,
            "prerelease": false,
            "html_url": "https://github.com/deadhearth01/Mitosis/releases/tag/v0.2.3"
          },
          {
            "tag_name": "v0.3.0-beta.1",
            "draft": true,
            "prerelease": true,
            "html_url": "https://github.com/deadhearth01/Mitosis/releases/tag/v0.3.0-beta.1"
          }
        ]
        """;

    private static GitHubReleaseFeed Feed(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
        return new GitHubReleaseFeed("0.2.2+6f9ee6b", new HttpClient(handler));
    }

    [TestMethod]
    public async Task ParsesEveryFieldTheSelectionRulesNeed()
    {
        var releases = await Feed(ReleasesJson).FetchAsync();

        Assert.AreEqual(2, releases.Count);
        Assert.AreEqual("v0.2.3", releases[0].TagName);
        Assert.IsFalse(releases[0].IsDraft);
        Assert.IsFalse(releases[0].IsPrerelease);
        Assert.AreEqual(
            "https://github.com/deadhearth01/Mitosis/releases/tag/v0.2.3",
            releases[0].HtmlUrl);
        Assert.IsTrue(releases[1].IsDraft);
        Assert.IsTrue(releases[1].IsPrerelease);
    }

    [TestMethod]
    public async Task SendsTheMinimalAnonymousRequest()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json"),
        });
        var feed = new GitHubReleaseFeed("0.2.2+6f9ee6b", new HttpClient(handler));

        await feed.FetchAsync();

        Assert.IsNotNull(handler.LastRequest);
        Assert.AreEqual(HttpMethod.Get, handler.LastRequest.Method);
        Assert.AreEqual(GitHubReleaseFeed.DefaultEndpoint, handler.LastRequest.RequestUri);
        Assert.AreEqual("Mitosis/0.2.2", handler.LastRequest.Headers.UserAgent.ToString());
        StringAssert.Contains(
            string.Join(" ", handler.LastRequest.Headers.Accept),
            "application/vnd.github+json");
        Assert.IsNull(handler.LastRequest.Headers.Authorization,
            "The public release list needs no credential, so none is sent.");
    }

    [TestMethod]
    public async Task EmptyReleaseList_IsAnEmptyResult()
    {
        var releases = await Feed("[]").FetchAsync();

        Assert.AreEqual(0, releases.Count);
    }

    [TestMethod]
    public async Task NonSuccessStatus_IsAReadableFailure()
    {
        var ex = await Assert.ThrowsExactlyAsync<ReleaseFeedException>(
            () => Feed("[]", HttpStatusCode.Forbidden).FetchAsync());

        StringAssert.Contains(ex.Message, "didn't answer normally");
    }

    [TestMethod]
    public async Task InvalidJson_IsAReadableFailure()
    {
        var ex = await Assert.ThrowsExactlyAsync<ReleaseFeedException>(
            () => Feed("{not json").FetchAsync());

        StringAssert.Contains(ex.Message, "couldn't read");
    }

    [TestMethod]
    public async Task NonArrayRoot_IsAReadableFailure()
    {
        var ex = await Assert.ThrowsExactlyAsync<ReleaseFeedException>(
            () => Feed("""{"message":"Not Found"}""").FetchAsync());

        StringAssert.Contains(ex.Message, "couldn't read");
    }

    [TestMethod]
    public async Task ReleaseWithoutATag_IsAReadableFailure()
    {
        var ex = await Assert.ThrowsExactlyAsync<ReleaseFeedException>(
            () => Feed("""[{"html_url":"https://example.test/r"}]""").FetchAsync());

        StringAssert.Contains(ex.Message, "couldn't read");
    }

    [TestMethod]
    public async Task OversizedResponse_IsRejectedBeforeDownloadingIt()
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Array.Empty<byte>()),
            };
            response.Content.Headers.ContentLength = 600 * 1024;
            return response;
        });
        var feed = new GitHubReleaseFeed("0.2.2", new HttpClient(handler));

        var ex = await Assert.ThrowsExactlyAsync<ReleaseFeedException>(() => feed.FetchAsync());

        StringAssert.Contains(ex.Message, "couldn't read");
    }

    [TestMethod]
    public async Task OversizedResponseWithoutAContentLength_IsRejectedWhileReading()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnboundedContent(600 * 1024),
        });
        var feed = new GitHubReleaseFeed("0.2.2", new HttpClient(handler));

        var ex = await Assert.ThrowsExactlyAsync<ReleaseFeedException>(() => feed.FetchAsync());

        StringAssert.Contains(ex.Message, "couldn't read",
            "A chunked body must hit the same 512 KB cap as a declared one.");
    }

    [TestMethod]
    public void Constructor_RequiresAVersionForTheUserAgent()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new GitHubReleaseFeed(" "));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }

    /// <summary>A response body of known size whose length cannot be declared up front,
    /// so only the read loop can enforce the cap.</summary>
    private sealed class UnboundedContent(int length) : HttpContent
    {
        protected override bool TryComputeLength(out long computedLength)
        {
            computedLength = 0;
            return false;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var block = new byte[16 * 1024];
            for (var written = 0; written < length; written += block.Length)
            {
                await stream.WriteAsync(block.AsMemory(0, Math.Min(block.Length, length - written)));
            }
        }
    }
}
