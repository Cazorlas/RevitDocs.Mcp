using AngleSharp.Html.Parser;

namespace Paper.RevitDocs.Mcp.Sources.RevitApi;

public static class HtmlDocumentExtractor
{
    public static async Task<string> ExtractAsync(string html, CancellationToken cancellationToken)
    {
        var document = await new HtmlParser().ParseDocumentAsync(html, cancellationToken);
        var root = document.QuerySelector("main, article, [role=main], .main-content") ?? document.Body;
        if (root is null) return string.Empty;

        foreach (var noise in root.QuerySelectorAll("script, style, nav, header, footer, noscript")) noise.Remove();
        return string.Join('\n', root.TextContent.Split('\n')
            .Select(line => string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Where(line => line.Length > 0));
    }
}
