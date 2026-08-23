using System.ComponentModel;
using ModelContextProtocol.Server;
using Paper.RevitDocs.Mcp.Documents;
using Paper.RevitDocs.Mcp.Search;
using Paper.RevitDocs.Mcp.Sources;

namespace Paper.RevitDocs.Mcp.Tools;

[McpServerToolType]
public sealed class RevitDocsTools(
    DocumentSearchService search,
    DocumentReadService read,
    DocumentSourceRegistry registry)
{
    [McpServerTool(Name = "revit_docs_search", Title = "Search Revit documentation", ReadOnly = true,
        Idempotent = true, Destructive = false, OpenWorld = true, UseStructuredContent = true)]
    [Description("Search versioned Revit API documentation and allowlisted Paper public documentation.")]
    public Task<SearchToolResponse> SearchAsync(
        [Description("API symbol or search text.")] string query,
        [Description("Requested Revit version year.")] int? revitVersion = null,
        [Description("Optional source IDs.")] string[]? sources = null,
        [Description("Search mode: auto, symbol, or text.")] string queryMode = "auto",
        [Description("Maximum results, from 1 to 50.")] int limit = 10,
        string? cursor = null, CancellationToken cancellationToken = default) =>
        SearchCoreAsync(query, revitVersion, null, sources, queryMode, limit, cursor, false, cancellationToken);

    [McpServerTool(Name = "revit_code_search", Title = "Search reviewed Revit code", ReadOnly = true,
        Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Search only explicitly enabled, reviewed repository sources. Search never performs implicit network synchronization.")]
    public Task<SearchToolResponse> CodeSearchAsync(string query, int? revitVersion = null, string? language = null,
        string[]? sources = null, string queryMode = "auto", int limit = 10, string? cursor = null,
        CancellationToken cancellationToken = default) =>
        SearchCoreAsync(query, revitVersion, language, sources, queryMode, limit, cursor, true, cancellationToken);

    [McpServerTool(Name = "revit_docs_read", Title = "Read a documentation result", ReadOnly = true,
        Idempotent = true, Destructive = false, OpenWorld = true, UseStructuredContent = true)]
    [Description("Read bounded content using an opaque result ID returned by a search tool.")]
    public async Task<ReadToolResponse> ReadAsync(string resultId, int maxCharacters = 20_000, string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resultId)) throw new ArgumentException("resultId is required.", nameof(resultId));
        if (maxCharacters is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(maxCharacters), "maxCharacters must be from 1 to 100000.");
        var content = await read.ReadAsync(resultId, maxCharacters, cursor, cancellationToken);
        if (content is null)
            throw new KeyNotFoundException("Result was not found or its source is unavailable. Run a search again and use its current resultId.");
        return new ReadToolResponse(content, null);
    }

    [McpServerTool(Name = "revit_docs_sources", Title = "List Revit documentation sources", ReadOnly = true,
        Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("List source readiness, revision, attribution, license and cache/sync guidance.")]
    public async Task<SourcesToolResponse> SourcesAsync(CancellationToken cancellationToken = default)
    {
        var statuses = new List<DocumentSourceStatus>();
        foreach (var source in registry.Sources) statuses.Add(await source.GetStatusAsync(cancellationToken));
        return new SourcesToolResponse(statuses);
    }

    private async Task<SearchToolResponse> SearchCoreAsync(string query, int? version, string? language, string[]? sources, string queryMode, int limit,
        string? cursor, bool codeOnly, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("query is required.", nameof(query));
        if (limit is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(limit), "limit must be from 1 to 50.");
        if (version.HasValue && version is < 2019 or > 2027)
            throw new ArgumentOutOfRangeException(nameof(version), "revitVersion must be from 2019 to 2027.");
        if (!Enum.TryParse<DocumentQueryMode>(queryMode, true, out var mode))
            throw new ArgumentException("queryMode must be auto, symbol, or text.", nameof(queryMode));
        var page = await search.SearchAsync(new DocumentSearchQuery(query.Trim(), version, sources,
            mode, limit, cursor, language), codeOnly, cancellationToken);
        return new SearchToolResponse(page.Results, page.NextCursor, []);
    }
}

public sealed record SearchToolResponse(IReadOnlyList<DocumentReference> Results, string? NextCursor, IReadOnlyList<string> Warnings);
public sealed record ReadToolResponse(DocumentContent? Document, string? Error);
public sealed record SourcesToolResponse(IReadOnlyList<DocumentSourceStatus> Sources);
