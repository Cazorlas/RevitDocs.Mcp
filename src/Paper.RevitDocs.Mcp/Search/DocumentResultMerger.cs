using System.Globalization;
using System.Text;
using Paper.RevitDocs.Mcp.Documents;

namespace Paper.RevitDocs.Mcp.Search;

public static class DocumentResultMerger
{
    public static DocumentSearchPage Merge(
        IEnumerable<DocumentReference> references,
        DocumentSearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit, 1, 50);
        var offset = DecodeCursor(query.Cursor);

        var merged = references
            .Where(reference => MatchesMode(reference, query))
            .GroupBy(Identity, StringComparer.OrdinalIgnoreCase)
            .Select(group => MergeGroup(group, query))
            .OrderByDescending(item => Rank(item, query))
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ResultId, StringComparer.Ordinal)
            .ToArray();

        var page = merged.Skip(offset).Take(limit).ToArray();
        var nextOffset = offset + page.Length;
        return new DocumentSearchPage(page, nextOffset < merged.Length ? EncodeCursor(nextOffset) : null);
    }

    private static string Identity(DocumentReference reference) =>
        string.IsNullOrWhiteSpace(reference.Symbol)
            ? reference.CanonicalUri
            : $"{reference.Symbol.Trim()}|{reference.RevitVersion?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}";

    private static DocumentReference MergeGroup(
        IGrouping<string, DocumentReference> group,
        DocumentSearchQuery query)
    {
        var best = group
            .OrderByDescending(item => Rank(item, query))
            .ThenBy(item => item.ResultId, StringComparer.Ordinal)
            .First();
        var links = group
            .SelectMany(item => item.SupportingSources)
            .DistinctBy(link => $"{link.SourceId}|{link.Uri}|{link.Revision}", StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return best with { SupportingSources = links };
    }

    private static double Rank(DocumentReference reference, DocumentSearchQuery query)
    {
        var rank = reference.Score;
        var exactSymbol = !string.IsNullOrWhiteSpace(reference.Symbol)
                          && string.Equals(reference.Symbol.Trim(), query.Query.Trim(),
                              StringComparison.OrdinalIgnoreCase);

        if (exactSymbol && query.QueryMode != DocumentQueryMode.Text) rank += 10_000;
        if (query.QueryMode == DocumentQueryMode.Symbol && !string.IsNullOrWhiteSpace(reference.Symbol)) rank += 2_000;
        if (query.RevitVersion.HasValue && reference.RevitVersion == query.RevitVersion) rank += 1_000;
        else if (exactSymbol && query.RevitVersion.HasValue && reference.RevitVersion.HasValue) rank += 500;

        return rank;
    }

    private static bool MatchesMode(DocumentReference reference, DocumentSearchQuery query) => query.QueryMode switch
    {
        DocumentQueryMode.Symbol => !string.IsNullOrWhiteSpace(reference.Symbol)
                                    && reference.Symbol.Contains(query.Query.Trim(), StringComparison.OrdinalIgnoreCase),
        DocumentQueryMode.Text => reference.Title.Contains(query.Query.Trim(), StringComparison.OrdinalIgnoreCase)
                                  || (reference.Summary?.Contains(query.Query.Trim(), StringComparison.OrdinalIgnoreCase) ?? false),
        _ => true
    };

    private static int DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return 0;

        try
        {
            var value = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
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
