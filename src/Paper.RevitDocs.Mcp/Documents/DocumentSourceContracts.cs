namespace Paper.RevitDocs.Mcp.Documents;

public interface IDocumentSource
{
    string SourceId { get; }
    Task<DocumentSourceStatus> GetStatusAsync(CancellationToken cancellationToken);
}

public interface IDocumentSearchSource : IDocumentSource
{
    Task<IReadOnlyList<DocumentReference>> SearchAsync(
        DocumentSearchQuery query,
        CancellationToken cancellationToken);
}

public interface IDocumentContentSource : IDocumentSource
{
    Task<DocumentContent?> ReadAsync(
        string resultId,
        int maxCharacters,
        string? cursor,
        CancellationToken cancellationToken);
}

public interface ICodeDocumentSource : IDocumentSearchSource
{
}
