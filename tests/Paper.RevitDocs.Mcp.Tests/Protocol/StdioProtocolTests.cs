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

    /// <summary>
    /// A client that checks structured content against the tool's output schema (Claude Code 2.1 does) refused every
    /// search whose next cursor or revision was unknown: the schema requires the property, and a null one was left out.
    /// </summary>
    [Test]
    [CancelAfter(20_000)]
    public async Task StructuredContent_WithUnknownValues_StillHasEveryRequiredProperty()
    {
        var publicRoot = Path.Combine(Path.GetTempPath(), "PaperMcpProtocol", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(publicRoot);
        await File.WriteAllTextAsync(Path.Combine(publicRoot, "README.md"), "# Schema fixture\nSchemaFixtureUnique content");
        var server = Environment.GetEnvironmentVariable("PAPER_MCP_SERVER_PATH")
                     ?? typeof(Paper.RevitDocs.Mcp.Tools.RevitDocsTools).Assembly.Location;
        var publishedExecutable = Path.GetExtension(server).Equals(".exe", StringComparison.OrdinalIgnoreCase);
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "Paper Revit Docs schema test",
            Command = publishedExecutable ? server : "dotnet",
            Arguments = publishedExecutable ? [] : [server],
            WorkingDirectory = Path.GetDirectoryName(server),
            EnvironmentVariables = new Dictionary<string, string?> { ["PAPER_REVIT_DOCS_ROOT"] = publicRoot }
        });
        try
        {
            await using var client = await McpClient.CreateAsync(transport);
            var search = (await client.ListToolsAsync()).Single(tool => tool.Name == "revit_docs_search");

            var result = await client.CallToolAsync("revit_docs_search", new Dictionary<string, object?>
            {
                ["query"] = "SchemaFixtureUnique", ["sources"] = new[] { "paper-public" }, ["limit"] = 3
            });

            using var schema = JsonDocument.Parse(JsonSerializer.Serialize(search.ProtocolTool.OutputSchema));
            using var content = JsonDocument.Parse(JsonSerializer.Serialize(result.StructuredContent));
            Assert.That(content.RootElement.GetProperty("results").GetArrayLength(), Is.GreaterThan(0), "the fixture is found");
            Assert.That(MissingRequired(schema.RootElement, schema.RootElement, content.RootElement, "$"), Is.Empty);
        }
        finally
        {
            Directory.Delete(publicRoot, true);
        }
    }

    /// <summary>Every property the schema requires that the data leaves out, by path; $ref resolved against the root.</summary>
    private static List<string> MissingRequired(JsonElement root, JsonElement schema, JsonElement data, string path)
    {
        var missing = new List<string>();
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var target = root;
            foreach (var part in reference.GetString()!.TrimStart('#', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
                target = target.GetProperty(part);
            return MissingRequired(root, target, data, path);
        }

        if (data.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("required", out var required))
            {
                foreach (var name in required.EnumerateArray().Select(n => n.GetString()!))
                    if (!data.TryGetProperty(name, out _)) missing.Add(path + "." + name);
            }

            if (schema.TryGetProperty("properties", out var properties))
            {
                foreach (var property in properties.EnumerateObject())
                    if (data.TryGetProperty(property.Name, out var value))
                        missing.AddRange(MissingRequired(root, property.Value, value, path + "." + property.Name));
            }
        }
        else if (data.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var items))
        {
            var index = 0;
            foreach (var item in data.EnumerateArray())
                missing.AddRange(MissingRequired(root, items, item, path + "[" + index++ + "]"));
        }

        return missing;
    }
}
