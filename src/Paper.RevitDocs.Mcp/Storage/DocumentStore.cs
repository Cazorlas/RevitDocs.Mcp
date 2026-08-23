using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Paper.RevitDocs.Mcp.Documents;
using Paper.RevitDocs.Mcp.Search;

namespace Paper.RevitDocs.Mcp.Storage;

public sealed class DocumentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CachePaths _paths;

    public DocumentStore(CachePaths paths) => _paths = paths;

    public string DatabasePath => _paths.Database;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _paths.EnsureCreated();
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = DocumentStoreSchema.Create;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertAsync(
        DocumentReference reference,
        DocumentContent? content,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO documents
            (result_id, source_id, title, symbol, canonical_uri, revit_version, revision, kind,
             cache_state, score, supporting_sources, summary, content, retrieved_at, expires_at, quarantined)
            VALUES
            ($id, $source, $title, $symbol, $uri, $version, $revision, $kind,
             $cache, $score, $links, $summary, $content, $retrieved, $expires, 0)
            ON CONFLICT(result_id) DO UPDATE SET
              source_id = CASE WHEN excluded.content IS NULL THEN excluded.source_id ELSE documents.source_id END,
              title = CASE WHEN excluded.content IS NULL THEN excluded.title ELSE documents.title END,
              symbol = CASE WHEN excluded.content IS NULL THEN excluded.symbol ELSE documents.symbol END,
              canonical_uri = CASE WHEN excluded.content IS NULL THEN excluded.canonical_uri ELSE documents.canonical_uri END,
              revit_version = CASE WHEN excluded.content IS NULL THEN excluded.revit_version ELSE documents.revit_version END,
              revision = CASE WHEN excluded.content IS NULL THEN excluded.revision ELSE documents.revision END,
              kind = CASE WHEN excluded.content IS NULL THEN excluded.kind ELSE documents.kind END,
              cache_state = excluded.cache_state,
              score = CASE WHEN excluded.content IS NULL THEN excluded.score ELSE documents.score END,
              supporting_sources = CASE WHEN excluded.content IS NULL THEN excluded.supporting_sources ELSE documents.supporting_sources END,
              summary = CASE WHEN excluded.content IS NULL THEN excluded.summary ELSE documents.summary END,
              content = COALESCE(excluded.content, documents.content),
              retrieved_at = CASE WHEN excluded.content IS NULL THEN documents.retrieved_at ELSE excluded.retrieved_at END,
              expires_at = CASE WHEN excluded.content IS NULL THEN documents.expires_at ELSE excluded.expires_at END,
              quarantined = 0;
            DELETE FROM documents_fts WHERE result_id = $id;
            INSERT INTO documents_fts (result_id, title, symbol, summary, content)
            VALUES ($id, $title, $symbol, $summary,
                    (SELECT content FROM documents WHERE result_id = $id));
            """;
        Add(command, "$id", reference.ResultId);
        Add(command, "$source", reference.SourceId);
        Add(command, "$title", reference.Title);
        Add(command, "$symbol", reference.Symbol);
        Add(command, "$uri", reference.CanonicalUri);
        Add(command, "$version", reference.RevitVersion);
        Add(command, "$revision", reference.Revision);
        Add(command, "$kind", (int)reference.Kind);
        Add(command, "$cache", (int)reference.CacheState);
        Add(command, "$score", reference.Score);
        Add(command, "$links", JsonSerializer.Serialize(reference.SupportingSources, JsonOptions));
        Add(command, "$summary", reference.Summary);
        Add(command, "$content", content?.Content);
        Add(command, "$retrieved", (content?.RetrievedAt ?? DateTimeOffset.UtcNow).ToString("O"));
        Add(command, "$expires", expiresAt?.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<DocumentSearchPage> SearchAsync(
        DocumentSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Query)) return new DocumentSearchPage([], null);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.result_id, d.source_id, d.title, d.symbol, d.canonical_uri, d.revit_version,
                   d.revision, d.kind, d.cache_state, d.score, d.supporting_sources, d.summary
            FROM documents d
            WHERE d.quarantined = 0
              AND (
                    lower(d.symbol) = lower($query)
                    OR lower(d.title) = lower($query)
                    OR d.result_id IN (
                        SELECT result_id FROM documents_fts WHERE documents_fts MATCH $fts
                    )
                  )
              AND ($version IS NULL OR d.revit_version = $version OR d.revit_version IS NULL);
            """;
        Add(command, "$query", query.Query.Trim());
        Add(command, "$fts", QuoteFts(query.Query));
        Add(command, "$version", query.RevitVersion);

        var results = new List<DocumentReference>();
        var corruptIds = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                try
                {
                    results.Add(ReadReference(reader));
                }
                catch (JsonException)
                {
                    corruptIds.Add(reader.GetString(0));
                }
            }
        }

        foreach (var id in corruptIds) await QuarantineAsync(connection, id, cancellationToken);
        return DocumentResultMerger.Merge(results, query);
    }

    public async Task<DocumentContent?> ReadAsync(
        string resultId,
        int maxCharacters,
        string? cursor,
        CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT source_id, title, content, canonical_uri, revit_version, revision, cache_state,
                   retrieved_at, expires_at
            FROM documents
            WHERE result_id = $id AND quarantined = 0 AND content IS NOT NULL;
            """;
        Add(command, "$id", resultId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var cacheState = (CacheState)reader.GetInt32(6);
        if (!reader.IsDBNull(8)
            && DateTimeOffset.TryParse(reader.GetString(8), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var expires)
            && expires < DateTimeOffset.UtcNow)
            cacheState = CacheState.CachedStale;

        var fullContent = reader.GetString(2);
        var offset = DecodeCursor(cursor);
        if (offset >= fullContent.Length) return null;
        var length = Math.Min(Math.Clamp(maxCharacters, 1, 100_000), fullContent.Length - offset);
        var nextCursor = offset + length < fullContent.Length
            ? Convert.ToBase64String(BitConverter.GetBytes(offset + length))
            : null;
        return new DocumentContent(
            resultId,
            reader.GetString(0),
            reader.GetString(1),
            fullContent.Substring(offset, length),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetInt32(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            cacheState,
            DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            nextCursor);
    }

    private SqliteConnection CreateConnection() => new(new SqliteConnectionStringBuilder
    {
        DataSource = _paths.Database,
        Pooling = false
    }.ToString());

    private static DocumentReference ReadReference(SqliteDataReader reader)
    {
        var links = JsonSerializer.Deserialize<SourceLink[]>(reader.GetString(10), JsonOptions)
                    ?? throw new JsonException("Supporting source metadata was empty.");
        return new DocumentReference(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            (DocumentKind)reader.GetInt32(7),
            (CacheState)reader.GetInt32(8),
            reader.GetDouble(9),
            links,
            reader.IsDBNull(11) ? null : reader.GetString(11));
    }

    private static async Task QuarantineAsync(
        SqliteConnection connection,
        string resultId,
        CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE documents SET quarantined = 1 WHERE result_id = $id;";
        Add(command, "$id", resultId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void Add(SqliteCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    private static string QuoteFts(string query) => $"\"{query.Trim().Replace("\"", "\"\"")}\"";

    private static int DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return 0;
        try { return Math.Max(0, BitConverter.ToInt32(Convert.FromBase64String(cursor))); }
        catch (Exception exception) when (exception is FormatException or ArgumentException) { return 0; }
    }
}
