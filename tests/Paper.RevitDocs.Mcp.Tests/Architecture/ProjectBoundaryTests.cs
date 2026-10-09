using System.Text.Json;
using System.Xml.Linq;
using NUnit.Framework;

namespace Paper.RevitDocs.Mcp.Tests.Architecture;

[TestFixture]
public sealed class ProjectBoundaryTests
{
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "RevitDocs.Mcp.sln")))
                directory = directory.Parent;

            return directory?.FullName
                   ?? throw new DirectoryNotFoundException("Could not locate RevitDocs.Mcp.sln from test output.");
        }
    }

    private static string ProductProject => Path.Combine(
        RepositoryRoot, "src", "Paper.RevitDocs.Mcp", "Paper.RevitDocs.Mcp.csproj");

    [Test]
    public void ProductProject_IsIndependentNet10Executable()
    {
        Assert.That(File.Exists(ProductProject), Is.True, $"Missing {ProductProject}");

        var document = XDocument.Load(ProductProject);
        var properties = document.Descendants("PropertyGroup").Elements().ToArray();
        var references = document.Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(properties.Single(element => element.Name.LocalName == "TargetFramework").Value,
                Is.EqualTo("net10.0"));
            Assert.That(properties.Single(element => element.Name.LocalName == "OutputType").Value,
                Is.EqualTo("Exe"));
            Assert.That(references, Is.Empty);
        });
    }

    [Test]
    public void Solution_ContainsOnlyStandaloneProductAndTests()
    {
        var solution = File.ReadAllText(Path.Combine(RepositoryRoot, "RevitDocs.Mcp.sln"));

        Assert.Multiple(() =>
        {
            Assert.That(solution, Does.Contain("src\\Paper.RevitDocs.Mcp\\Paper.RevitDocs.Mcp.csproj"));
            Assert.That(solution, Does.Contain("tests\\Paper.RevitDocs.Mcp.Tests\\Paper.RevitDocs.Mcp.Tests.csproj"));
            Assert.That(solution, Does.Contain("Debug|Any CPU"));
            Assert.That(solution, Does.Contain("Release|Any CPU"));
            Assert.That(solution, Does.Not.Contain("PaperPlus.sln"));
            Assert.That(solution, Does.Not.Contain("DB2024"));
            Assert.That(solution, Does.Not.Contain("RL2024"));
        });
    }

    [Test]
    public void RepositoryGuidance_IsDiscoverableAndStatesProtocolBoundary()
    {
        var agents = File.ReadAllText(Path.Combine(RepositoryRoot, "AGENTS.md"));
        var claude = File.ReadAllText(Path.Combine(RepositoryRoot, "CLAUDE.md"));
        var codex = File.ReadAllText(Path.Combine(RepositoryRoot, "CODEX.md"));
        var instruction = File.ReadAllText(Path.Combine(RepositoryRoot, "INSTRUCTION.md"));

        Assert.Multiple(() =>
        {
            Assert.That(agents, Does.Contain("STDOUT"));
            Assert.That(agents, Does.Contain("read-only"));
            Assert.That(agents, Does.Contain("scripts/verify.ps1"));
            Assert.That(claude, Does.Contain("AGENTS.md"));
            Assert.That(codex, Does.Contain("AGENTS.md"));
            Assert.That(instruction, Does.Contain("Current status"));
        });
    }

    [Test]
    public void ClaudeHooks_AreProjectScopedAndPortable()
    {
        var settingsPath = Path.Combine(RepositoryRoot, ".claude", "settings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var hooks = document.RootElement.GetProperty("hooks");
        var serialized = hooks.GetRawText();
        var stopHook = File.ReadAllText(Path.Combine(
            RepositoryRoot, ".claude", "hooks", "verify-on-stop.ps1"));

        Assert.Multiple(() =>
        {
            Assert.That(hooks.TryGetProperty("SessionStart", out _), Is.True);
            Assert.That(hooks.TryGetProperty("Stop", out _), Is.True);
            Assert.That(serialized, Does.Contain("${CLAUDE_PROJECT_DIR}"));
            Assert.That(serialized, Does.Not.Contain("D:\\Repository"));
            Assert.That(File.Exists(Path.Combine(RepositoryRoot, ".claude", "hooks", "session-context.ps1")), Is.True);
            Assert.That(File.Exists(Path.Combine(RepositoryRoot, ".claude", "hooks", "verify-on-stop.ps1")), Is.True);
            Assert.That(stopHook, Does.Contain("README\\.md"));
            Assert.That(stopHook, Does.Contain("NOTICE"));
        });
    }

    [Test]
    public void RevitDocsSkill_IsAvailableToCodexAndClaude()
    {
        var codexSkill = Path.Combine(RepositoryRoot, ".agents", "skills", "revit-docs-mcp", "SKILL.md");
        var claudeSkill = Path.Combine(RepositoryRoot, ".claude", "skills", "revit-docs-mcp", "SKILL.md");

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(codexSkill), Is.True);
            Assert.That(File.Exists(claudeSkill), Is.True);
            Assert.That(File.ReadAllText(codexSkill), Is.EqualTo(File.ReadAllText(claudeSkill)));
            Assert.That(File.ReadAllText(codexSkill), Does.Contain("name: revit-docs-mcp"));
            Assert.That(File.ReadAllText(codexSkill), Does.Contain("revit_docs_sources"));
        });
    }

    [Test]
    public void PublicRepositoryMetadata_IsCompleteAndPortable()
    {
        string[] requiredFiles =
        {
            ".gitignore",
            "LICENSE",
            "NOTICE",
            "README.md",
            "CONTRIBUTING.md",
            "SECURITY.md",
            "CHANGELOG.md"
        };
        var missing = requiredFiles
            .Where(path => !File.Exists(Path.Combine(RepositoryRoot, path)))
            .ToArray();

        Assert.That(missing, Is.Empty, $"Missing public repository files: {string.Join(", ", missing)}");

        var license = File.ReadAllText(Path.Combine(RepositoryRoot, "LICENSE"));
        var notice = File.ReadAllText(Path.Combine(RepositoryRoot, "NOTICE"));
        var readme = File.ReadAllText(Path.Combine(RepositoryRoot, "README.md"));
        var publicMarkdown = new[] { "README.md", "CONTRIBUTING.md", "SECURITY.md", "CHANGELOG.md" }
            .Select(path => File.ReadAllText(Path.Combine(RepositoryRoot, path)))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(license, Does.Contain("MIT License"));
            Assert.That(license, Does.Contain("Copyright (c) 2026 Cazorlas"));
            Assert.That(license, Does.Contain("Permission is hereby granted, free of charge"));
            Assert.That(license, Does.Not.Contain("Apache License"));
            Assert.That(notice, Does.Contain("Copyright 2026 Cazorlas"));
            Assert.That(readme, Does.Contain("CONTRIBUTING.md"));
            Assert.That(readme, Does.Contain("SECURITY.md"));
            Assert.That(readme, Does.Contain("CHANGELOG.md"));
            Assert.That(readme, Does.Contain("docs/architecture.md"));
            Assert.That(publicMarkdown, Has.None.Contains("D:\\Repository"));
        });
    }
}
