namespace Paper.RevitDocs.Mcp.Documents;

public enum DocumentKind
{
    Api,
    Documentation,
    Code
}

public enum CacheState
{
    Live,
    CachedFresh,
    CachedStale,
    Local
}

public enum DocumentQueryMode
{
    Auto,
    Symbol,
    Text
}

public sealed record SourceLink(string SourceId, string Uri, string? Revision);

public sealed record DocumentReference(
    string ResultId,
    string SourceId,
    string Title,
    string? Symbol,
    string CanonicalUri,
    int? RevitVersion,
    string? Revision,
    DocumentKind Kind,
    CacheState CacheState,
    double Score,
    IReadOnlyList<SourceLink> SupportingSources,
    string? Summary = null);

public sealed record DocumentContent(
    string ResultId,
    string SourceId,
    string Title,
    string Content,
    string CanonicalUri,
    int? RevitVersion,
    string? Revision,
    CacheState CacheState,
    DateTimeOffset RetrievedAt,
    string? NextCursor = null);

public sealed record DocumentSearchQuery(
    string Query,
    int? RevitVersion = null,
    IReadOnlyList<string>? Sources = null,
    DocumentQueryMode QueryMode = DocumentQueryMode.Auto,
    int Limit = 10,
    string? Cursor = null,
    string? Language = null);

public sealed record DocumentSearchPage(
    IReadOnlyList<DocumentReference> Results,
    string? NextCursor);

public sealed record DocumentSourceStatus(
    string SourceId,
    string DisplayName,
    bool Enabled,
    bool Ready,
    string State,
    string? Revision,
    DateTimeOffset? LastSuccessfulUpdate,
    string? Attribution,
    string? License,
    string? Message);
