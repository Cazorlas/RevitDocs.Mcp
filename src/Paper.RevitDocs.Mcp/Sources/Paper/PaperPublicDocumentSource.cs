using System.Globalization;
using System.Text;
using Paper.RevitDocs.Mcp.Documents;
using Paper.RevitDocs.Mcp.Security;

namespace Paper.RevitDocs.Mcp.Sources.Paper;

public sealed class PaperPublicDocumentSource : IDocumentSearchSource, IDocumentContentSource
{
    public const string Id = "paper-public";
    private const long MaximumDocumentBytes = 2_000_000;

    private readonly string _root;
    private readonly DocumentAccessPolicy _accessPolicy;

    public PaperPublicDocumentSource(string root, DocumentAccessPolicy accessPolicy)
    {
        _root = Path.GetFullPath(root);
        _accessPolicy = accessPolicy;
    }

    public string SourceId => Id;

    public Task<DocumentSourceStatus> GetStatusAsync(CancellationToken cancellationToken) => Task.FromResult(
        new DocumentSourceStatus(Id, "Paper public documentation", true, Directory.Exists(_root),
            Directory.Exists(_root) ? "local" : "not_ready", null, null, "PaperEngineer", "Proprietary",
            Directory.Exists(_root) ? null : $"Public documentation folder was not found: {_root}"));

    public async Task<IReadOnlyList<DocumentReference>> SearchAsync(
        DocumentSearchQuery query,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_root) || string.IsNullOrWhiteSpace(query.Query))
            return Array.Empty<DocumentReference>();

        var results = new List<DocumentReference>();
        foreach (var path in SafeFileEnumerator.Enumerate(_root).Where(path =>
                     Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_accessPolicy.IsAllowed(path)) continue;
            if (new FileInfo(path).Length > MaximumDocumentBytes) continue;

            var content = await File.ReadAllTextAsync(path, cancellationToken);
            if (!PublicContentPolicy.IsSafe(content)) continue;
            var title = FindTitle(path, content);
            if (!title.Contains(query.Query, StringComparison.OrdinalIgnoreCase)
                && !content.Contains(query.Query, StringComparison.OrdinalIgnoreCase)) continue;

            var relative = Path.GetRelativePath(_root, path).Replace('\\', '/');
            var resultId = EncodeResultId(relative);
            var publicUri = "paper:///" + relative;
            results.Add(new DocumentReference(resultId, Id, title, null, publicUri, null, null,
                DocumentKind.Documentation, CacheState.Local, 100,
                new[] { new SourceLink(Id, publicUri, null) }, BuildSummary(content, query.Query)));
        }

        return results;
    }

    public async Task<DocumentContent?> ReadAsync(
        string resultId,
        int maxCharacters,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var relative = DecodeResultId(resultId);
        if (relative == null) return null;

        var path = Path.GetFullPath(Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!_accessPolicy.IsAllowed(path) || !File.Exists(path) || new FileInfo(path).Length > MaximumDocumentBytes) return null;

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        if (!PublicContentPolicy.IsSafe(text)) return null;
        var offset = DecodeCursor(cursor);
        if (offset >= text.Length) return null;

        var length = Math.Min(Math.Clamp(maxCharacters, 1, 50_000), text.Length - offset);
        var nextOffset = offset + length;
        return new DocumentContent(resultId, Id, FindTitle(path, text), text.Substring(offset, length), "paper:///" + relative,
            null, null, CacheState.Local, File.GetLastWriteTimeUtc(path),
            nextOffset < text.Length ? EncodeCursor(nextOffset) : null);
    }

    private static string FindTitle(string path, string content)
    {
        var heading = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal));
        return heading == null ? Path.GetFileNameWithoutExtension(path) : heading[2..].Trim();
    }

    private static string? BuildSummary(string content, string query)
    {
        var index = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;
        var start = Math.Max(0, index - 80);
        return content.Substring(start, Math.Min(240, content.Length - start)).ReplaceLineEndings(" ").Trim();
    }

    private static string EncodeResultId(string relativePath) =>
        "paper:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(relativePath));

    private static string? DecodeResultId(string resultId)
    {
        if (!resultId.StartsWith("paper:", StringComparison.Ordinal)) return null;
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(resultId[6..]));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static int DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return 0;
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
                ? Math.Max(0, offset)
                : 0;
        }
        catch (FormatException)
        {
            return 0;
        }
    }

    private static string EncodeCursor(int offset) => Convert.ToBase64String(
        Encoding.UTF8.GetBytes(offset.ToString(CultureInfo.InvariantCulture)));
}
