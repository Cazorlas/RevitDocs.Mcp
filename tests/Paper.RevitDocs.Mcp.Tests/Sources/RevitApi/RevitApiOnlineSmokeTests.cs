using NUnit.Framework;
using Paper.RevitDocs.Mcp.Documents;
using Paper.RevitDocs.Mcp.Http;
using Paper.RevitDocs.Mcp.Sources.RevitApi;

namespace Paper.RevitDocs.Mcp.Tests.Sources.RevitApi;

[TestFixture]
[Explicit("Uses the live rvtdocs.com endpoint; run deliberately during release verification.")]
public sealed class RevitApiOnlineSmokeTests
{
    [TestCase(2024)]
    [TestCase(RevitVersions.Maximum)]
    public async Task RvtDocs_SearchReturnsNormalizedResult(int version)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var source = new RvtDocsSource(new BoundedHttpClient(client));

        var results = await source.SearchAsync(new DocumentSearchQuery("FilteredElementCollector", version), default);
        var content = await source.ReadAsync(results[0].ResultId, 20_000, null, default);

        Assert.That(results, Is.Not.Empty);
        Assert.That(results.All(result => result.RevitVersion == version && result.CanonicalUri.StartsWith("https://rvtdocs.com/")), Is.True);
        Assert.That(content?.Content, Does.Contain("FilteredElementCollector"));
    }
}
