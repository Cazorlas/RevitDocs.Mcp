using NUnit.Framework;
using Paper.RevitDocs.Mcp.Documents;
using Paper.RevitDocs.Mcp.Search;
using Paper.RevitDocs.Mcp.Sources;
using Paper.RevitDocs.Mcp.Tools;

namespace Paper.RevitDocs.Mcp.Tests.Tools;

[TestFixture]
public sealed class RevitDocsToolsTests
{
    [Test]
    public async Task Search_ContinuesWhenOneProviderFails()
    {
        var registry = new DocumentSourceRegistry([new ThrowingSource(), new FakeSource()]);
        var tools = new RevitDocsTools(new DocumentSearchService(registry), new DocumentReadService(registry), registry);

        var response = await tools.SearchAsync("Wall", 2024, null, "auto", 10, null, default);

        Assert.That(response.Results.Select(result => result.ResultId), Is.EqualTo(new[] { "fake:wall" }));
    }

    [Test]
    public async Task Search_AcceptsNewestSupportedVersion()
    {
        var registry = new DocumentSourceRegistry([new FakeSource()]);
        var tools = new RevitDocsTools(new DocumentSearchService(registry), new DocumentReadService(registry), registry);

        var response = await tools.SearchAsync("Wall", RevitVersions.Maximum, null, "auto", 10, null, default);

        Assert.That(response.Results, Is.Not.Empty);
    }

    [TestCase(RevitVersions.Minimum - 1)]
    [TestCase(RevitVersions.Maximum + 1)]
    public void Search_RejectsUnsupportedVersion(int version)
    {
        var registry = new DocumentSourceRegistry([new FakeSource()]);
        var tools = new RevitDocsTools(new DocumentSearchService(registry), new DocumentReadService(registry), registry);

        Assert.That(async () => await tools.SearchAsync("Wall", version, null, "auto", 10, null, default),
            Throws.TypeOf<ArgumentOutOfRangeException>()
                .With.Message.Contains($"from {RevitVersions.Minimum} to {RevitVersions.Maximum}"));
    }

    [Test]
    public async Task Sources_ReportsEveryProviderState()
    {
        var registry = new DocumentSourceRegistry([new ThrowingSource(), new FakeSource()]);
        var tools = new RevitDocsTools(new DocumentSearchService(registry), new DocumentReadService(registry), registry);

        var response = await tools.SourcesAsync(default);

        Assert.That(response.Sources, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task CodeSearch_DoesNotCallOrdinaryOnlineDocumentationSources()
    {
        var onlineDocs = new FakeSource();
        var registry = new DocumentSourceRegistry([onlineDocs]);
        var tools = new RevitDocsTools(new DocumentSearchService(registry), new DocumentReadService(registry), registry);

        var response = await tools.CodeSearchAsync("Wall", null, null, null, "auto", 10, null, default);

        Assert.Multiple(() =>
        {
            Assert.That(response.Results, Is.Empty);
            Assert.That(onlineDocs.SearchCallCount, Is.Zero);
        });
    }

    private sealed class FakeSource : IDocumentSearchSource, IDocumentContentSource
    {
        public int SearchCallCount { get; private set; }
        public string SourceId => "fake";
        public Task<IReadOnlyList<DocumentReference>> SearchAsync(DocumentSearchQuery query, CancellationToken token)
        {
            SearchCallCount++;
            return Task.FromResult<IReadOnlyList<DocumentReference>>([new("fake:wall", SourceId, "Wall", "Wall",
                "https://example.test/wall", 2024, "1", DocumentKind.Api, CacheState.Live, 1,
                [new SourceLink(SourceId, "https://example.test/wall", "1")])]);
        }
        public Task<DocumentContent?> ReadAsync(string resultId, int maxCharacters, string? cursor, CancellationToken token) =>
            Task.FromResult<DocumentContent?>(new(resultId, SourceId, "Wall", "Wall docs", "https://example.test/wall",
                2024, "1", CacheState.Live, DateTimeOffset.UtcNow));
        public Task<DocumentSourceStatus> GetStatusAsync(CancellationToken token) => Task.FromResult(
            new DocumentSourceStatus(SourceId, "Fake", true, true, "ready", "1", null, "test", "MIT", null));
    }

    private sealed class ThrowingSource : IDocumentSearchSource
    {
        public string SourceId => "broken";
        public Task<IReadOnlyList<DocumentReference>> SearchAsync(DocumentSearchQuery query, CancellationToken token) =>
            throw new HttpRequestException("offline");
        public Task<DocumentSourceStatus> GetStatusAsync(CancellationToken token) => Task.FromResult(
            new DocumentSourceStatus(SourceId, "Broken", true, false, "degraded", null, null, null, null, "offline"));
    }
}
