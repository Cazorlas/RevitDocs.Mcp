using Paper.RevitDocs.Mcp.Documents;

namespace Paper.RevitDocs.Mcp.Storage;

public sealed class CachedDocumentSource(
    IDocumentSearchSource searchSource,
    IDocumentContentSource contentSource,
    DocumentStore store,
    TimeSpan timeToLive) : IDocumentSearchSource, IDocumentContentSource
{
    private const int CachePageCharacters = 100_000;
    private const int MaximumCachedDocumentCharacters = 2_000_000;
    public string SourceId => searchSource.SourceId;
    private readonly object _statusLock = new();
    private DateTimeOffset? _lastSuccessfulUpdate;
    private string _runtimeState = "unknown";
    private string? _runtimeMessage;

    public async Task<IReadOnlyList<DocumentReference>> SearchAsync(DocumentSearchQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var results = await searchSource.SearchAsync(query, cancellationToken);
            foreach (var result in results)
                await store.UpsertAsync(result, null, DateTimeOffset.UtcNow.Add(timeToLive), cancellationToken);
            RecordSuccess();
            return results;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TimeoutException or TaskCanceledException or InvalidDataException)
        {
            var cached = await store.SearchAsync(query, cancellationToken);
            RecordFailure(exception.Message);
            return cached.Results.Where(result => result.SourceId.Equals(SourceId, StringComparison.OrdinalIgnoreCase))
                .Select(result => result with { CacheState = CacheState.CachedStale }).ToArray();
        }
    }

    public async Task<DocumentContent?> ReadAsync(string resultId, int maxCharacters, string? cursor, CancellationToken cancellationToken)
    {
        try
        {
            var full = await ReadBoundedDocumentAsync(resultId, cancellationToken);
            if (full is null) return await store.ReadAsync(resultId, maxCharacters, cursor, cancellationToken);
            var reference = new DocumentReference(full.ResultId, full.SourceId, full.Title, null,
                full.CanonicalUri, full.RevitVersion, full.Revision, DocumentKind.Api, full.CacheState, 0,
                [new SourceLink(full.SourceId, full.CanonicalUri, full.Revision)]);
            await store.UpsertAsync(reference, full, DateTimeOffset.UtcNow.Add(timeToLive), cancellationToken);
            RecordSuccess();
            return await store.ReadAsync(resultId, maxCharacters, cursor, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TimeoutException or TaskCanceledException or InvalidDataException)
        {
            var cached = await store.ReadAsync(resultId, maxCharacters, cursor, cancellationToken);
            RecordFailure(exception.Message);
            return cached is null ? null : cached with { CacheState = CacheState.CachedStale };
        }
    }

    public async Task<DocumentSourceStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var configured = await searchSource.GetStatusAsync(cancellationToken);
        lock (_statusLock)
        {
            return configured with
            {
                Ready = configured.Ready,
                State = _runtimeState == "unknown" ? configured.State : _runtimeState,
                LastSuccessfulUpdate = _lastSuccessfulUpdate,
                Message = _runtimeMessage ?? configured.Message
            };
        }
    }

    private async Task<DocumentContent?> ReadBoundedDocumentAsync(string resultId, CancellationToken cancellationToken)
    {
        DocumentContent? first = null;
        var content = new System.Text.StringBuilder();
        string? cursor = null;
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            var page = await contentSource.ReadAsync(resultId, CachePageCharacters, cursor, cancellationToken);
            if (page is null) return first is null ? null : first with { Content = content.ToString(), NextCursor = null };
            first ??= page;
            if (content.Length + page.Content.Length > MaximumCachedDocumentCharacters)
                throw new InvalidDataException($"Document exceeds the {MaximumCachedDocumentCharacters:N0}-character cache limit.");
            content.Append(page.Content);
            cursor = page.NextCursor;
            if (cursor is not null && !seenCursors.Add(cursor))
                throw new InvalidDataException("Provider returned a repeated document cursor.");
        } while (cursor is not null);

        return first! with { Content = content.ToString(), NextCursor = null };
    }

    private void RecordSuccess()
    {
        lock (_statusLock)
        {
            _lastSuccessfulUpdate = DateTimeOffset.UtcNow;
            _runtimeState = "online";
            _runtimeMessage = null;
        }
    }

    private void RecordFailure(string message)
    {
        lock (_statusLock)
        {
            _runtimeState = "degraded_cache";
            _runtimeMessage = "Live source failed; cached content is used when available. " + message;
        }
    }
}
