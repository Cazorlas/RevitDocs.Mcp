using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Paper.RevitDocs.Mcp.Documents;
using Paper.RevitDocs.Mcp.Storage;

namespace Paper.RevitDocs.Mcp.Tests.Storage;

[TestFixture]
public sealed class DocumentStoreTests
{
    private string _root = null!;
    private DocumentStore _store = null!;

    [SetUp]
    public async Task SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRevitDocsTests", Guid.NewGuid().ToString("N"));
        _store = new DocumentStore(new CachePaths(_root));
        await _store.InitializeAsync(CancellationToken.None);
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Test]
    public void InitializeAsync_CreatesDatabaseInsideCacheRoot()
    {
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(_store.DatabasePath), Is.True);
            Assert.That(_store.DatabasePath.StartsWith(Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase),
                Is.True);
        });
    }

    [Test]
    public async Task SearchAsync_RanksExactSymbolBeforeFullText()
    {
        await _store.UpsertAsync(Reference("text", "Other", 2024, 50),
            Content("text", "FilteredElementCollector appears in prose"), null, CancellationToken.None);
        await _store.UpsertAsync(Reference("exact", "FilteredElementCollector", 2024, 1),
            Content("exact", "API class"), null, CancellationToken.None);

        var page = await _store.SearchAsync(
            new DocumentSearchQuery("FilteredElementCollector", 2024), CancellationToken.None);

        Assert.That(page.Results.Select(result => result.ResultId), Is.EqualTo(new[] { "exact", "text" }));
    }

    [Test]
    public async Task ReadAsync_LabelsExpiredContentAsStale()
    {
        await _store.UpsertAsync(Reference("cached", "Wall", 2024, 1), Content("cached", "Wall docs"),
            DateTimeOffset.UtcNow.AddMinutes(-1), CancellationToken.None);

        var result = await _store.ReadAsync("cached", 100_000, null, CancellationToken.None);

        Assert.That(result?.CacheState, Is.EqualTo(CacheState.CachedStale));
    }

    [Test]
    public async Task SearchAsync_QuarantinesEntryWithCorruptSourceMetadata()
    {
        await using (var connection = new SqliteConnection($"Data Source={_store.DatabasePath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO documents
                (result_id, source_id, title, canonical_uri, kind, cache_state, score, supporting_sources, retrieved_at, quarantined)
                VALUES ('bad', 'source', 'Bad', 'https://example.test/bad', 0, 1, 1, '{bad json', $now, 0);
                """;
            command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        var page = await _store.SearchAsync(new DocumentSearchQuery("Bad"), CancellationToken.None);

        Assert.That(page.Results, Is.Empty);

        await using var check = new SqliteConnection($"Data Source={_store.DatabasePath}");
        await check.OpenAsync();
        var checkCommand = check.CreateCommand();
        checkCommand.CommandText = "SELECT quarantined FROM documents WHERE result_id = 'bad';";
        Assert.That(Convert.ToInt32(await checkCommand.ExecuteScalarAsync()), Is.EqualTo(1));
    }

    private static DocumentReference Reference(string id, string symbol, int? version, double score) => new(
        id, "source", symbol, symbol, $"https://example.test/{id}", version, "abc", DocumentKind.Api,
        CacheState.CachedFresh, score, new[] { new SourceLink("source", $"https://example.test/{id}", "abc") });

    private static DocumentContent Content(string id, string text) => new(
        id, "source", id, text, $"https://example.test/{id}", 2024, "abc", CacheState.CachedFresh,
        DateTimeOffset.UtcNow);
}
