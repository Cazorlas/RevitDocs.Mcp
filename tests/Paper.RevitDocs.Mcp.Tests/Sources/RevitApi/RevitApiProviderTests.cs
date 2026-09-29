using System.Net;
using NUnit.Framework;
using Paper.RevitDocs.Mcp.Documents;
using Paper.RevitDocs.Mcp.Http;
using Paper.RevitDocs.Mcp.Sources.RevitApi;

namespace Paper.RevitDocs.Mcp.Tests.Sources.RevitApi;

[TestFixture]
public sealed class RevitApiProviderTests
{
    [Test]
    public async Task SearchAsync_NormalizesVersionedRvtDocsResult()
    {
        var json = await ReadFixtureAsync("rvtdocs-search.json");
        using var http = Client(_ => Response(HttpStatusCode.OK, json, "application/json"));
        var source = new RvtDocsSource(new BoundedHttpClient(http, 64_000));

        var results = await source.SearchAsync(new DocumentSearchQuery("FilteredElementCollector", 2024), default);

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Symbol, Is.EqualTo("FilteredElementCollector"));
            Assert.That(results[0].RevitVersion, Is.EqualTo(2024));
            Assert.That(results[0].Summary, Does.Not.StartWith("Description:"));
        });
    }

    [Test]
    public async Task SearchAsync_WithoutVersionQueriesEverySupportedVersionNewestFirst()
    {
        var requested = new List<int>();
        using var http = Client(request =>
        {
            var version = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["v"];
            lock (requested) requested.Add(int.Parse(version!));
            return Response(HttpStatusCode.OK, "{\"results\": []}", "application/json");
        });
        var source = new RvtDocsSource(new BoundedHttpClient(http, 1024));

        await source.SearchAsync(new DocumentSearchQuery("Wall", null), default);

        Assert.That(requested.Order(), Is.EqualTo(Enumerable.Range(RevitVersions.Minimum,
            RevitVersions.Maximum - RevitVersions.Minimum + 1)));
        Assert.That(RevitVersions.NewestFirst.First(), Is.EqualTo(RevitVersions.Maximum));
    }

    [Test]
    public async Task ExtractAsync_UsesMainContentAndOmitsNavigation()
    {
        var html = await ReadFixtureAsync("revit-api-page.html");
        var text = await HtmlDocumentExtractor.ExtractAsync(html, default);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("FilteredElementCollector Class"));
            Assert.That(text, Does.Contain("new FilteredElementCollector"));
            Assert.That(text, Does.Not.Contain("Navigation noise"));
        });
    }

    [Test]
    public void BoundedClient_RejectsOversizedResponses()
    {
        using var http = Client(_ => Response(HttpStatusCode.OK, new string('x', 33), "text/plain"));
        var bounded = new BoundedHttpClient(http, 32);

        Assert.That(async () => await bounded.GetStringAsync(new Uri("https://example.test"), default),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void SearchAsync_MalformedResponseRaisesDegradedProviderFailure()
    {
        using var http = Client(_ => Response(HttpStatusCode.OK, "not-json", "application/json"));
        var source = new RvtDocsSource(new BoundedHttpClient(http, 1024));

        Assert.That(async () => await source.SearchAsync(new DocumentSearchQuery("Wall", 2024), default),
            Throws.TypeOf<InvalidDataException>());
    }

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
        new(new DelegateHandler(handler)) { Timeout = TimeSpan.FromSeconds(2) };

    private static HttpResponseMessage Response(HttpStatusCode status, string body, string mediaType) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType) };

    private static Task<string> ReadFixtureAsync(string name) => File.ReadAllTextAsync(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", name));

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(callback(request));
    }
}
