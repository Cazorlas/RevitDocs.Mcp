using ModelContextProtocol.Client;
using NUnit.Framework;
using System.Text.Json;
using System.Diagnostics;

namespace Paper.RevitDocs.Mcp.Tests.Protocol;

[TestFixture]
public sealed class StdioProtocolTests
{
    [Test]
    [CancelAfter(20_000)]
    public async Task RawTransport_UsesJsonOnlyStdoutAndStopsWhenStdinCloses()
    {
        var server = Environment.GetEnvironmentVariable("PAPER_MCP_SERVER_PATH")
                     ?? typeof(Paper.RevitDocs.Mcp.Tools.RevitDocsTools).Assembly.Location;
        var isExe = Path.GetExtension(server).Equals(".exe", StringComparison.OrdinalIgnoreCase);
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = isExe ? server : "dotnet",
            Arguments = isExe ? string.Empty : $"\"{server}\"",
            WorkingDirectory = Path.GetDirectoryName(server),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Could not start MCP server.");
        try
        {
            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"raw-test","version":"1.0"}}}""");
            var initialize = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(initialize, Is.Not.Null.And.Not.Empty);
            using (var json = JsonDocument.Parse(initialize!))
                Assert.That(json.RootElement.GetProperty("id").GetInt32(), Is.EqualTo(1));

            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
            var tools = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            using (var json = JsonDocument.Parse(tools!))
                Assert.That(json.RootElement.GetProperty("id").GetInt32(), Is.EqualTo(2));

            process.StandardInput.Close();
            Assert.That(process.WaitForExit(5000), Is.True, "Server did not stop after stdin closed.");
            Assert.That(process.ExitCode, Is.Zero, await process.StandardError.ReadToEndAsync());
        }
        finally
        {
            if (!process.HasExited) process.Kill(true);
        }
    }

    [Test]
    [CancelAfter(20_000)]
    public async Task Server_InitializesListsFourToolsAndCallsSources()
    {
        var publicRoot = Path.Combine(Path.GetTempPath(), "PaperMcpProtocol", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(publicRoot);
        await File.WriteAllTextAsync(Path.Combine(publicRoot, "README.md"), "# Protocol fixture\nProtocolFixtureUnique content");
        var repositoryRoot = Path.Combine(publicRoot, "repository");
        Directory.CreateDirectory(repositoryRoot);
        await File.WriteAllTextAsync(Path.Combine(repositoryRoot, "Fixture.cs"), "class ProtocolRepositoryUnique {}");
        var repositoryConfig = Path.Combine(publicRoot, "repositories.json");
        await File.WriteAllTextAsync(repositoryConfig, JsonSerializer.Serialize(new[]
        {
            new
            {
                id = "protocol-repository", displayName = "Protocol repository",
                repositoryUrl = "https://github.com/example/protocol", license = "MIT", attribution = "Test fixture",
                enabled = true, include = new[] { "**/*.cs" }, exclude = Array.Empty<string>(),
                localPath = repositoryRoot, revision = "test-revision"
            }
        }));
        var server = Environment.GetEnvironmentVariable("PAPER_MCP_SERVER_PATH")
                     ?? typeof(Paper.RevitDocs.Mcp.Tools.RevitDocsTools).Assembly.Location;
        var publishedExecutable = Path.GetExtension(server).Equals(".exe", StringComparison.OrdinalIgnoreCase);
        var errors = new List<string>();
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "Paper Revit Docs protocol test",
            Command = publishedExecutable ? server : "dotnet",
            Arguments = publishedExecutable ? [] : [server],
            WorkingDirectory = Path.GetDirectoryName(server),
            StandardErrorLines = errors.Add,
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["PAPER_REVIT_DOCS_ROOT"] = publicRoot,
                ["PAPER_REVIT_DOCS_REPOSITORY_CONFIG"] = repositoryConfig
            }
        });
        try
        {
            await using var client = await McpClient.CreateAsync(transport);

            var tools = await client.ListToolsAsync();
            var names = tools.Select(tool => tool.Name).Order().ToArray();
            var sourcesResult = await client.CallToolAsync("revit_docs_sources", new Dictionary<string, object?>());
            var searchResult = await client.CallToolAsync("revit_docs_search", new Dictionary<string, object?>
            {
                ["query"] = "ProtocolFixtureUnique", ["sources"] = new[] { "paper-public" }, ["limit"] = 3
            });
            using var searchJson = JsonDocument.Parse(JsonSerializer.Serialize(searchResult.StructuredContent));
            var resultId = searchJson.RootElement.GetProperty("results")[0].GetProperty("resultId").GetString()!;
            var readResult = await client.CallToolAsync("revit_docs_read", new Dictionary<string, object?>
            {
                ["resultId"] = resultId
            });
            var codeResult = await client.CallToolAsync("revit_code_search", new Dictionary<string, object?>
            {
                ["query"] = "ProtocolRepositoryUnique", ["limit"] = 3
            });
            var invalidResult = await client.CallToolAsync("revit_docs_search", new Dictionary<string, object?>
            {
                ["query"] = string.Empty
            });
            var unknownReadResult = await client.CallToolAsync("revit_docs_read", new Dictionary<string, object?>
            {
                ["resultId"] = "unknown:opaque"
            });

            Assert.Multiple(() =>
            {
                Assert.That(names, Is.EqualTo(new[]
                {
                    "revit_code_search", "revit_docs_read", "revit_docs_search", "revit_docs_sources"
                }));
                Assert.That(sourcesResult.IsError, Is.Not.True, string.Join(Environment.NewLine, errors));
                Assert.That(searchResult.IsError, Is.Not.True, string.Join(Environment.NewLine, errors));
                Assert.That(codeResult.IsError, Is.Not.True, string.Join(Environment.NewLine, errors));
                Assert.That(codeResult.StructuredContent?.ToString(), Does.Contain("Fixture.cs"));
                Assert.That(readResult.IsError, Is.Not.True, string.Join(Environment.NewLine, errors));
                Assert.That(invalidResult.IsError, Is.True, "Invalid input must be an MCP tool error, not a successful warning payload.");
                Assert.That(unknownReadResult.IsError, Is.True, "Unknown result IDs must be MCP tool errors.");
                Assert.That(sourcesResult.StructuredContent, Is.Not.Null);
                Assert.That(readResult.StructuredContent?.ToString(), Does.Contain("ProtocolFixtureUnique content"));
            });
        }
        finally
        {
            Directory.Delete(publicRoot, true);
        }
    }
}
