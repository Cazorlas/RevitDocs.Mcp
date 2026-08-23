using System.Text;
using NUnit.Framework;
using Paper.RevitDocs.Mcp.Documents;
using Paper.RevitDocs.Mcp.Sources.Paper;

namespace Paper.RevitDocs.Mcp.Tests.Sources.Paper;

[TestFixture]
public sealed class PaperPublicDocumentSourceTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRevitDocsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "help"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "progress"));
        File.WriteAllText(Path.Combine(_root, "README.md"), "# Paper\nPublic routing help");
        File.WriteAllText(Path.Combine(_root, "docs", "help", "mep.md"), "# MEP\nConnector routing guide");
        File.WriteAllText(Path.Combine(_root, "docs", "progress", "secret.md"), "Connector secret");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Test]
    public async Task SearchAsync_ReturnsOnlyAllowlistedDocuments()
    {
        var source = CreateSource();

        var results = await source.SearchAsync(new DocumentSearchQuery("Connector"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].Title, Is.EqualTo("MEP"));
            Assert.That(results[0].CacheState, Is.EqualTo(CacheState.Local));
        });
    }

    [Test]
    public async Task ReadAsync_RejectsForgedResultForDeniedPath()
    {
        var source = CreateSource();
        var deniedRelativePath = "docs/progress/secret.md";
        var forged = "paper:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(deniedRelativePath));

        var content = await source.ReadAsync(forged, 1_000, null, CancellationToken.None);

        Assert.That(content, Is.Null);
    }

    [TestCase("api_key = abcdefghijklmnop")]
    [TestCase("Local example: C:\\Users\\developer\\source\\private.md")]
    [TestCase("-----BEGIN PRIVATE KEY-----")]
    [TestCase("Authorization: Bearer abcdefghijklmnopqrstuvwxyz")]
    [TestCase("token eyJabcdefghijk.abcdefghijk.abcdefghijk")]
    [TestCase("github ghp_abcdefghijklmnopqrstuvwxyz123456")]
    [TestCase("aws AKIAABCDEFGHIJKLMNOP")]
    [TestCase("license_payload = abcdefghijklmnop")]
    [TestCase("customer: person@example.com")]
    public async Task SearchAndRead_RejectContentThatLooksConfidential(string unsafeContent)
    {
        var path = Path.Combine(_root, "docs", "help", "unsafe.md");
        File.WriteAllText(path, "# Unsafe\nConnector\n" + unsafeContent);
        var source = CreateSource();
        var id = "paper:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("docs/help/unsafe.md"));

        Assert.That(await source.SearchAsync(new DocumentSearchQuery("Connector"), default),
            Has.None.Matches<DocumentReference>(result => result.ResultId == id));
        Assert.That(await source.ReadAsync(id, 1000, null, default), Is.Null);
    }

    [Test]
    public async Task SearchAndRead_RejectOversizedAllowlistedFile()
    {
        var path = Path.Combine(_root, "docs", "help", "large.md");
        File.WriteAllText(path, new string('x', 2_000_001));
        var source = CreateSource();
        var id = "paper:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("docs/help/large.md"));

        Assert.That(await source.SearchAsync(new DocumentSearchQuery("xxx"), default),
            Has.None.Matches<DocumentReference>(result => result.ResultId == id));
        Assert.That(await source.ReadAsync(id, 1000, null, default), Is.Null);
    }

    private PaperPublicDocumentSource CreateSource()
    {
        var policy = new DocumentAccessPolicy(_root, new[] { "README.md", "docs/help/**/*.md" });
        return new PaperPublicDocumentSource(_root, policy);
    }
}
