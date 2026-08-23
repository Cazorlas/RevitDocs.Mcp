using Paper.RevitDocs.Mcp.Documents;
using Paper.RevitDocs.Mcp.Sources;

namespace Paper.RevitDocs.Mcp.Search;

public sealed class DocumentSearchService(DocumentSourceRegistry registry)
{
    public async Task<DocumentSearchPage> SearchAsync(DocumentSearchQuery query, bool codeOnly, CancellationToken cancellationToken)
    {
        var results = new List<DocumentReference>();
        var sources = codeOnly ? registry.CodeSearchSources(query.Sources) : registry.SearchSources(query.Sources);
        foreach (var source in sources)
        {
            try
            {
                var values = await source.SearchAsync(query, cancellationToken);
                results.AddRange(codeOnly ? values.Where(value => value.Kind == DocumentKind.Code) : values);
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or TimeoutException or TaskCanceledException or InvalidDataException)
            {
                // A source outage is isolated; other providers and cached/local sources remain usable.
            }
        }
        return DocumentResultMerger.Merge(results, query);
    }
}
