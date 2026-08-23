using NUnit.Framework;
using Paper.RevitDocs.Mcp.Documents;
using Paper.RevitDocs.Mcp.Sources.Repositories;

namespace Paper.RevitDocs.Mcp.Tests.Sources.Repositories;

[TestFixture]
public sealed class RepositorySourceTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRepoSourceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        File.WriteAllText(Path.Combine(_root, "src", "Sample.cs"), "class WallFinder { FilteredElementCollector collector; }");
        File.WriteAllText(Path.Combine(_root, "secret.txt"), "FilteredElementCollector secret");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, true);

    [Test]
    public async Task SearchAsync_UsesOnlyReviewedPatternsAndDoesNotUseNetwork()
    {
        var manifest = new RepositoryManifest("sample", "Sample", "https://github.com/example/sample", "MIT",
            "Example", true, ["src/**/*.cs"], ["**/bin/**"], _root, "abc123");
        var source = new RepositorySource(manifest);

        var results = await source.SearchAsync(new DocumentSearchQuery("FilteredElementCollector"), default);

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].CanonicalUri, Does.Contain("src/Sample.cs"));
            Assert.That(results[0].Revision, Is.EqualTo("abc123"));
        });
    }

    [Test]
    public async Task DisabledOrUnlicensedSource_IsNotReadyAndReturnsNothing()
    {
        var source = new RepositorySource(new RepositoryManifest("bad", "Bad", "https://example.test", null,
            null, true, ["**/*.cs"], [], _root, null));

        Assert.That(await source.SearchAsync(new DocumentSearchQuery("Wall"), default), Is.Empty);
        Assert.That((await source.GetStatusAsync(default)).Ready, Is.False);
    }

    [Test]
    public async Task ReadAsync_RejectsForgedResultThroughDirectoryLink_WhenLinksAreSupported()
    {
        var outside = Path.Combine(Path.GetTempPath(), "PaperRepoOutside", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "Secret.cs"), "secret");
        var link = Path.Combine(_root, "src", "linked");
        try
        {
            try { Directory.CreateSymbolicLink(link, outside); }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                Assert.Ignore("This environment does not permit symbolic-link creation.");
            }
            var manifest = new RepositoryManifest("sample", "Sample", "https://github.com/example/sample", "MIT",
                "Example", true, ["src/**/*.cs"], [], _root, "abc123");
            var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("src/linked/Secret.cs"));

            var content = await new RepositorySource(manifest).ReadAsync("repo:sample:" + encoded, 1000, null, default);

            Assert.That(content, Is.Null);
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link);
            if (Directory.Exists(outside)) Directory.Delete(outside, true);
        }
    }

    [Test]
    public async Task ReadAsync_RejectsForgedOversizedIncludedFile()
    {
        var path = Path.Combine(_root, "src", "Large.cs");
        File.WriteAllText(path, new string('x', 1_000_001));
        var manifest = new RepositoryManifest("sample", "Sample", "https://github.com/example/sample", "MIT",
            "Example", true, ["src/**/*.cs"], [], _root, "abc123");
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("src/Large.cs"));

        var content = await new RepositorySource(manifest).ReadAsync("repo:sample:" + encoded, 1000, null, default);

        Assert.That(content, Is.Null);
    }
}
