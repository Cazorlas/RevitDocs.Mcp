using NUnit.Framework;
using Paper.RevitDocs.Mcp.Security;

namespace Paper.RevitDocs.Mcp.Tests.Packaging;

[TestFixture]
public sealed class ReleasePackageTests
{
    [Test]
    public void PublishFolder_ContainsRequiredPublicFilesAndNoDeveloperPathOrRuleFiles()
    {
        var root = Environment.GetEnvironmentVariable("PAPER_MCP_PUBLISH_DIR");
        if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Set PAPER_MCP_PUBLISH_DIR for release-package verification.");
        Assert.That(Directory.Exists(root), Is.True, root);

        var files = Directory.GetFiles(root!, "*", SearchOption.AllDirectories);
        Assert.Multiple(() =>
        {
            Assert.That(files.Any(path => path.EndsWith("Paper.RevitDocs.Mcp.exe", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(files.Any(path => path.EndsWith("repository-sources.json", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(files.Any(path => path.EndsWith("paper-docs.allowlist.json", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(files.Any(path => path.EndsWith("README.md", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(files.Any(path => path.EndsWith("THIRD-PARTY-NOTICES.md", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(File.Exists(Path.Combine(root!, "LICENSE")), Is.True, "Published LICENSE is missing.");
            Assert.That(File.Exists(Path.Combine(root!, "NOTICE")), Is.True, "Published NOTICE is missing.");
            Assert.That(files.Select(Path.GetFileName), Has.None.Matches<string>(name =>
                name is not null && (name.Equals("CLAUDE.md", StringComparison.OrdinalIgnoreCase)
                                     || name.Equals("CODEX.md", StringComparison.OrdinalIgnoreCase)
                                     || name.Equals("AGENTS.md", StringComparison.OrdinalIgnoreCase)
                                     || name.Equals("INSTRUCTION.md", StringComparison.OrdinalIgnoreCase))));
        });

        var repositoryDirectory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (repositoryDirectory != null && !File.Exists(Path.Combine(repositoryDirectory.FullName, "RevitDocs.Mcp.sln")))
            repositoryDirectory = repositoryDirectory.Parent;
        Assert.That(repositoryDirectory, Is.Not.Null, "Could not locate the repository license.");
        var publishedLicense = File.ReadAllText(Path.Combine(root!, "LICENSE"));
        Assert.Multiple(() =>
        {
            Assert.That(publishedLicense, Does.Contain("MIT License"));
            Assert.That(publishedLicense, Is.EqualTo(File.ReadAllText(Path.Combine(repositoryDirectory!.FullName, "LICENSE"))));
        });

        var publicText = string.Join('\n', files.Where(path => Path.GetExtension(path) is ".json" or ".toml" or ".md")
            .Select(File.ReadAllText));
        Assert.That(publicText, Does.Not.Contain("D:\\Repository\\Cazorlas").IgnoreCase);
        Assert.That(PublicContentPolicy.IsSafe(publicText), Is.True,
            "Published text contains a user-profile path, private key, or credential assignment.");
    }
}
