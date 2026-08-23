using NUnit.Framework;
using Paper.RevitDocs.Mcp.Sources.Paper;

namespace Paper.RevitDocs.Mcp.Tests.Sources.Paper;

[TestFixture]
public sealed class DocumentAccessPolicyTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "PaperRevitDocsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "help"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "progress"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [TestCase("README.md", true)]
    [TestCase("docs/help/getting-started.md", true)]
    [TestCase("docs/help/private/customer.md", false)]
    [TestCase("CLAUDE.md", false)]
    [TestCase("docs/help/INSTRUCTION.md", false)]
    [TestCase("docs/progress/build.md", false)]
    [TestCase("src/Tool.cs", false)]
    public void IsAllowed_AppliesAllowlistAndInternalDenies(string relativePath, bool expected)
    {
        var policy = CreatePolicy();

        Assert.That(policy.IsAllowed(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar))),
            Is.EqualTo(expected));
    }

    [Test]
    public void IsAllowed_RejectsTraversalOutsideRoot()
    {
        var policy = CreatePolicy();
        var outside = Path.GetFullPath(Path.Combine(_root, "..", "secret.md"));

        Assert.That(policy.IsAllowed(outside), Is.False);
    }

    [Test]
    public void IsAllowed_RejectsSymbolicLinkEscape_WhenLinksAreSupported()
    {
        var outside = Path.Combine(Path.GetTempPath(), "PaperOutside", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "secret.md"), "secret");
        var link = Path.Combine(_root, "docs", "help", "external");
        try
        {
            Directory.CreateSymbolicLink(link, outside);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Assert.Ignore("Symbolic links are unavailable in this environment.");
        }

        try
        {
            Assert.That(CreatePolicy().IsAllowed(Path.Combine(link, "secret.md")), Is.False);
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link);
            Directory.Delete(outside, true);
        }
    }

    private DocumentAccessPolicy CreatePolicy() => new(
        _root,
        new[] { "README.md", "docs/help/**/*.md" },
        new[] { "docs/help/private/**" });
}
