using System.Text;
using System.Text.Json;
using Paper.RevitDocs.Mcp.Documents;
using Paper.RevitDocs.Mcp.Http;

namespace Paper.RevitDocs.Mcp.Sources.RevitApi;

public sealed class RvtDocsSource(BoundedHttpClient http) : IDocumentSearchSource, IDocumentContentSource
{
    private const string SearchEndpoint = "https://rvtdocs.com/search/v2/api/";
    public string SourceId => "rvtdocs";

    public async Task<IReadOnlyList<DocumentReference>> SearchAsync(DocumentSearchQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Query)) return [];
        try
        {
            IReadOnlyList<int> versions = query.RevitVersion.HasValue ? [query.RevitVersion.Value] : RevitVersions.NewestFirst;
            var searches = versions.Select(version => SearchVersionAsync(query, version, cancellationToken));
            return (await Task.WhenAll(searches)).SelectMany(result => result)
                .DistinctBy(result => result.ResultId, StringComparer.Ordinal)
                .ToArray();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("RvtDocs returned malformed JSON.", exception);
        }
    }

    private async Task<IReadOnlyList<DocumentReference>> SearchVersionAsync(
        DocumentSearchQuery query, int version, CancellationToken cancellationToken)
    {
        var endpoint = new Uri($"{SearchEndpoint}?q={Uri.EscapeDataString(query.Query.Trim())}"
                               + $"&v={version}&fields=title,description&limit={Math.Clamp(query.Limit, 1, 50)}&source=paper-mcp");
        var json = await http.GetStringAsync(endpoint, cancellationToken);
        using var document = JsonDocument.Parse(json);
        if ((!document.RootElement.TryGetProperty("results", out var values)
             && !document.RootElement.TryGetProperty("current_version_results", out values))
            || values.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("RvtDocs returned an unrecognized search response.");
        return values.EnumerateArray().Take(Math.Clamp(query.Limit, 1, 50))
            .Select(value => Normalize(value, version)).Where(value => value is not null).Cast<DocumentReference>().ToArray();
    }

    public async Task<DocumentContent?> ReadAsync(string resultId, int maxCharacters, string? cursor, CancellationToken cancellationToken)
    {
        if (!TryDecode(resultId, out var uri, out var version)) return null;
        var html = await http.GetStringAsync(uri, cancellationToken);
        var text = await HtmlDocumentExtractor.ExtractAsync(html, cancellationToken);
        var offset = DecodeCursor(cursor);
        if (offset >= text.Length) return null;
        var length = Math.Min(Math.Clamp(maxCharacters, 1, 100_000), text.Length - offset);
        var next = offset + length < text.Length ? Convert.ToBase64String(BitConverter.GetBytes(offset + length)) : null;
        return new DocumentContent(resultId, SourceId, uri.Segments.LastOrDefault()?.Trim('/') ?? uri.Host,
            text.Substring(offset, length), uri.AbsoluteUri, version, null, CacheState.Live, DateTimeOffset.UtcNow, next);
    }

    public Task<DocumentSourceStatus> GetStatusAsync(CancellationToken cancellationToken) => Task.FromResult(
        new DocumentSourceStatus(SourceId, "RvtDocs", true, true, "online", null, null,
            "rvtdocs.com", null, "Online search with local cache fallback."));

    private DocumentReference? Normalize(JsonElement value, int version)
    {
        var title = Text(value, "title");
        var url = Text(value, "url");
        if (url.StartsWith("/", StringComparison.Ordinal)) url = "https://rvtdocs.com" + url;
        if (string.IsNullOrWhiteSpace(title) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals("rvtdocs.com", StringComparison.OrdinalIgnoreCase)) return null;
        var type = Text(value, "type");
        var headline = Text(value, "headline_main");
        var symbol = !string.IsNullOrWhiteSpace(headline) ? headline
            : title.EndsWith($" {type}", StringComparison.OrdinalIgnoreCase)
                ? title[..^(type.Length + 1)] : title.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var summary = Text(value, "description").Replace("Description:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        if (int.TryParse(Text(value, "year_version"), out var resultVersion)) version = resultVersion;
        var id = Encode(uri, version);
        return new DocumentReference(id, SourceId, title, symbol, uri.AbsoluteUri, version, null,
            DocumentKind.Api, CacheState.Live, 0, [new SourceLink(SourceId, uri.AbsoluteUri, null)], summary);
    }

    private string Encode(Uri uri, int version) => $"{SourceId}:{Convert.ToBase64String(Encoding.UTF8.GetBytes($"{version}|{uri.AbsoluteUri}"))}";
    private bool TryDecode(string id, out Uri uri, out int version)
    {
        uri = null!; version = 0;
        if (!id.StartsWith(SourceId + ":", StringComparison.Ordinal)) return false;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(id[(SourceId.Length + 1)..])).Split('|', 2);
            return parts.Length == 2 && int.TryParse(parts[0], out version) && Uri.TryCreate(parts[1], UriKind.Absolute, out uri!)
                   && uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals("rvtdocs.com", StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException) { return false; }
    }

    private static string Text(JsonElement value, string name) =>
        value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : string.Empty;
    private static int DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return 0;
        try { return Math.Max(0, BitConverter.ToInt32(Convert.FromBase64String(cursor))); }
        catch (Exception exception) when (exception is FormatException or ArgumentException) { return 0; }
    }
}
