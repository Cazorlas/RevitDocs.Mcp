using Paper.RevitDocs.Mcp.Documents;

namespace Paper.RevitDocs.Mcp.Sources;

public sealed class DocumentSourceRegistry(IEnumerable<IDocumentSource> sources)
{
    public IReadOnlyList<IDocumentSource> Sources { get; } = sources.GroupBy(source => source.SourceId,
        StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToArray();

    public IReadOnlyList<IDocumentSearchSource> SearchSources(IReadOnlyList<string>? filter = null) => Sources
        .OfType<IDocumentSearchSource>()
        .Where(source => source is not ICodeDocumentSource)
        .Where(source => filter is null || filter.Count == 0 || filter.Contains(source.SourceId, StringComparer.OrdinalIgnoreCase))
        .ToArray();

    public IReadOnlyList<IDocumentContentSource> ContentSources => Sources.OfType<IDocumentContentSource>().ToArray();
    public IReadOnlyList<IDocumentSearchSource> CodeSearchSources(IReadOnlyList<string>? filter = null) => Sources
        .OfType<ICodeDocumentSource>()
        .Where(source => filter is null || filter.Count == 0 || filter.Contains(source.SourceId, StringComparer.OrdinalIgnoreCase))
        .ToArray();
}
