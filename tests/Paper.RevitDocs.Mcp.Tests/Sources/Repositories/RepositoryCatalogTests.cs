using System.Text.Json;
using NUnit.Framework;
using Paper.RevitDocs.Mcp.Sources.Repositories;

namespace Paper.RevitDocs.Mcp.Tests.Sources.Repositories;

[TestFixture]
public sealed class RepositoryCatalogTests
{
    [TestCase("the-building-coder-samples", "https://github.com/jeremytammik/the_building_coder_samples",
        "cf3748045978bc35fcae88417c3024209be44fbe", "Jeremy Tammik")]
    [TestCase("nice3point-revittoolkit", "https://github.com/Nice3point/RevitToolkit",
        "b5abec5ca7302e3adc4e821cd3a8a9d4f9032e7e", "Nice3point")]
    [TestCase("ricaun-revit-test", "https://github.com/ricaun-io/RevitTest",
        "950a26455a14b0608e239747f074a413a3cbfd79", "ricaun")]
    public void ReviewedSource_IsEnabledAtReviewedCommitWithAttribution(
        string sourceId, string repositoryUrl, string reviewedRevision, string attribution)
    {
        var manifest = ReadCatalog().Single(source => source.Id == sourceId);

        Assert.Multiple(() =>
        {
            Assert.That(manifest.Enabled, Is.True);
            Assert.That(manifest.RepositoryUrl, Is.EqualTo(repositoryUrl));
            Assert.That(manifest.Revision, Is.EqualTo(reviewedRevision));
            Assert.That(manifest.License, Is.EqualTo("MIT"));
            Assert.That(manifest.Attribution, Is.EqualTo(attribution));
            Assert.That(manifest.LocalPath, Is.Null);
        });
    }

    [Test]
    public void UnreviewedSdkSource_RemainsDisabledWithoutApprovedLicense()
    {
        var manifest = ReadCatalog().Single(source => source.Id == "autodesk-revit-samples");

        Assert.Multiple(() =>
        {
            Assert.That(manifest.Enabled, Is.False);
            Assert.That(manifest.License, Is.Null);
            Assert.That(manifest.LocalPath, Is.Null);
        });
    }

    private static IReadOnlyList<RepositoryManifest> ReadCatalog()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RevitDocs.Mcp.sln")))
            directory = directory.Parent;

        var root = directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate RevitDocs.Mcp.sln from test output.");
        var path = Path.Combine(root, "src", "Paper.RevitDocs.Mcp", "Configuration", "repository-sources.json");
        return JsonSerializer.Deserialize<RepositoryManifest[]>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("The shipped repository catalog is null.");
    }
}
