using Paper.RevitDocs.Mcp.Documents;
using Paper.RevitDocs.Mcp.Sources;

namespace Paper.RevitDocs.Mcp.Search;

public sealed class DocumentReadService(DocumentSourceRegistry registry)
{
    public async Task<DocumentContent?> ReadAsync(string resultId, int maxCharacters, string? cursor, CancellationToken cancellationToken)
    {
        foreach (var source in registry.ContentSources)
        {
            try
            {
                var content = await source.ReadAsync(resultId, Math.Clamp(maxCharacters, 1, 100_000), cursor, cancellationToken);
                if (content is not null) return content;
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or TimeoutException or TaskCanceledException)
            {
                // Continue to cached/local providers if a live provider is unavailable.
            }
        }
        return null;
    }
}
