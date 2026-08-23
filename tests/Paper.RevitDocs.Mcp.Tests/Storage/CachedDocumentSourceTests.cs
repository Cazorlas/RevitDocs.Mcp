using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Paper.RevitDocs.Mcp.Documents;
using Paper.RevitDocs.Mcp.Storage;

namespace Paper.RevitDocs.Mcp.Tests.Storage;

[TestFixture]
public sealed class CachedDocumentSourceTests
{
    [Test]
    public async Task OnlineFailure_ReturnsExplicitlyStaleCachedSearchAndContent()
    {
        var root = Path.Combine(Path.GetTempPath(), "PaperCachedSourceTests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DocumentStore(new CachePaths(root));
            await store.InitializeAsync(default);
            var live = new SwitchableSource();
            var cached = new CachedDocumentSource(live, live, store, TimeSpan.FromHours(1));

            var firstSearch = await cached.SearchAsync(new DocumentSearchQuery("Wall"), default);
            var firstRead = await cached.ReadAsync(firstSearch[0].ResultId, 1000, null, default);
            live.Offline = true;
            var fallbackSearch = await cached.SearchAsync(new DocumentSearchQuery("Wall"), default);
            var fallbackRead = await cached.ReadAsync(firstSearch[0].ResultId, 1000, null, default);

            Assert.Multiple(() =>
            {
                Assert.That(firstRead?.Content, Is.EqualTo("Wall documentation"));
                Assert.That(fallbackSearch.Single().CacheState, Is.EqualTo(CacheState.CachedStale));
                Assert.That(fallbackRead?.Content, Is.EqualTo("Wall documentation"));
                Assert.That(fallbackSearch.Single().Title, Is.EqualTo("Wall"));
                Assert.That(fallbackSearch.Single().Symbol, Is.EqualTo("Wall"));
                Assert.That(fallbackSearch.Single().Summary, Is.EqualTo("Wall API"));
            });
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task OfflineRead_AppliesRequestedPageToCachedFullContent()
    {
        var root = Path.Combine(Path.GetTempPath(), "PaperCachedSourceTests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DocumentStore(new CachePaths(root));
            await store.InitializeAsync(default);
            var live = new SwitchableSource();
            var cached = new CachedDocumentSource(live, live, store, TimeSpan.FromHours(1));

            var first = await cached.ReadAsync("live:wall", 4, null, default);
            live.Offline = true;
            var second = await cached.ReadAsync("live:wall", 4, first!.NextCursor, default);

            Assert.Multiple(() =>
            {
                Assert.That(first.Content, Is.EqualTo("Wall"));
                Assert.That(second?.Content, Is.EqualTo(" doc"));
                Assert.That(second?.CacheState, Is.EqualTo(CacheState.CachedStale));
            });
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task Read_CachesProviderPagesBeyondOneHundredThousandCharacters()
    {
        var root = Path.Combine(Path.GetTempPath(), "PaperCachedSourceTests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DocumentStore(new CachePaths(root));
            await store.InitializeAsync(default);
            var live = new SwitchableSource { Content = new string('a', 100_000) + "SECOND-PAGE" };
            var cached = new CachedDocumentSource(live, live, store, TimeSpan.FromHours(1));

            var first = await cached.ReadAsync("live:wall", 100_000, null, default);
            live.Offline = true;
            var second = await cached.ReadAsync("live:wall", 100, first!.NextCursor, default);

            Assert.That(second?.Content, Is.EqualTo("SECOND-PAGE"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class SwitchableSource : IDocumentSearchSource, IDocumentContentSource
    {
        public bool Offline { get; set; }
        public string Content { get; set; } = "Wall documentation";
        public string SourceId => "live";
        public Task<IReadOnlyList<DocumentReference>> SearchAsync(DocumentSearchQuery query, CancellationToken token)
        {
            if (Offline) throw new HttpRequestException("offline");
            return Task.FromResult<IReadOnlyList<DocumentReference>>([new("live:wall", SourceId, "Wall", "Wall",
                "https://example.test/wall", 2024, "1", DocumentKind.Api, CacheState.Live, 1,
                [new SourceLink(SourceId, "https://example.test/wall", "1")], "Wall API")]);
        }
        public Task<DocumentContent?> ReadAsync(string id, int max, string? cursor, CancellationToken token)
        {
            if (Offline) throw new HttpRequestException("offline");
            var offset = string.IsNullOrWhiteSpace(cursor) ? 0 : BitConverter.ToInt32(Convert.FromBase64String(cursor));
            if (offset >= Content.Length) return Task.FromResult<DocumentContent?>(null);
            var length = Math.Min(max, Content.Length - offset);
            var next = offset + length < Content.Length ? Convert.ToBase64String(BitConverter.GetBytes(offset + length)) : null;
            return Task.FromResult<DocumentContent?>(new(id, SourceId, "Wall", Content.Substring(offset, length),
                "https://example.test/wall", 2024, "1", CacheState.Live, DateTimeOffset.UtcNow, next));
        }
        public Task<DocumentSourceStatus> GetStatusAsync(CancellationToken token) => throw new NotSupportedException();
    }
}
