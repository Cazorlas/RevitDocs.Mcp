using NUnit.Framework;
using Paper.RevitDocs.Mcp.Documents;
using Paper.RevitDocs.Mcp.Search;

namespace Paper.RevitDocs.Mcp.Tests.Search;

[TestFixture]
public sealed class DocumentResultMergerTests
{
    [Test]
    public void ExactSymbolAtRequestedVersion_RanksFirst()
    {
        var query = new DocumentSearchQuery("FilteredElementCollector.WherePasses", 2024);
        var results = new[]
        {
            Reference("text", "Other", 2024, DocumentKind.Api, 900),
            Reference("neighbor", query.Query, 2025, DocumentKind.Api, 100),
            Reference("exact", query.Query, 2024, DocumentKind.Api, 1)
        };

        var page = DocumentResultMerger.Merge(results, query);

        Assert.That(page.Results.Select(result => result.ResultId),
            Is.EqualTo(new[] { "exact", "neighbor", "text" }));
    }

    [Test]
    public void DuplicateSymbolAndVersion_PreservesEverySupportingSource()
    {
        var query = new DocumentSearchQuery("Autodesk.Revit.DB.Wall", 2024);
        var results = new[]
        {
            Reference("rvt", query.Query, 2024, DocumentKind.Api, 5, "rvtdocs"),
            Reference("api", query.Query, 2024, DocumentKind.Api, 10, "revitapidocs")
        };

        var page = DocumentResultMerger.Merge(results, query);

        Assert.Multiple(() =>
        {
            Assert.That(page.Results, Has.Count.EqualTo(1));
            Assert.That(page.Results[0].SupportingSources.Select(source => source.SourceId),
                Is.EquivalentTo(new[] { "rvtdocs", "revitapidocs" }));
        });
    }

    [Test]
    public void UnknownVersion_RemainsUnknown()
    {
        var result = Reference("unknown", "Wall", null, DocumentKind.Api, 1);

        var page = DocumentResultMerger.Merge(new[] { result }, new DocumentSearchQuery("Wall", 2024));

        Assert.That(page.Results.Single().RevitVersion, Is.Null);
    }

    [Test]
    public void LimitAndCursor_ReturnStablePages()
    {
        var results = Enumerable.Range(0, 5)
            .Select(index => Reference(index.ToString(), $"Symbol{index}", 2024, DocumentKind.Api, 5 - index))
            .ToArray();

        var first = DocumentResultMerger.Merge(results, new DocumentSearchQuery("Symbol", 2024, Limit: 2));
        var second = DocumentResultMerger.Merge(results,
            new DocumentSearchQuery("Symbol", 2024, Limit: 2, Cursor: first.NextCursor));

        Assert.Multiple(() =>
        {
            Assert.That(first.Results.Select(result => result.ResultId), Is.EqualTo(new[] { "0", "1" }));
            Assert.That(first.NextCursor, Is.Not.Null);
            Assert.That(second.Results.Select(result => result.ResultId), Is.EqualTo(new[] { "2", "3" }));
        });
    }

    [Test]
    public void QueryMode_FiltersSymbolAndTextResultsDifferently()
    {
        var symbol = Reference("symbol", "Wall", 2024, DocumentKind.Api, 1);
        var text = Reference("text", "Other", 2024, DocumentKind.Api, 1) with { Summary = "Wall workflow" };

        var symbolPage = DocumentResultMerger.Merge([symbol, text],
            new DocumentSearchQuery("Wall", QueryMode: DocumentQueryMode.Symbol));
        var textPage = DocumentResultMerger.Merge([symbol, text],
            new DocumentSearchQuery("workflow", QueryMode: DocumentQueryMode.Text));

        Assert.That(symbolPage.Results.Select(value => value.ResultId), Is.EqualTo(new[] { "symbol" }));
        Assert.That(textPage.Results.Select(value => value.ResultId), Is.EqualTo(new[] { "text" }));
    }

    private static DocumentReference Reference(
        string id,
        string symbol,
        int? version,
        DocumentKind kind,
        double score,
        string source = "source") => new(
        id,
        source,
        symbol,
        symbol,
        $"https://example.test/{id}",
        version,
        null,
        kind,
        CacheState.Live,
        score,
        new[] { new SourceLink(source, $"https://example.test/{id}", null) });
}
