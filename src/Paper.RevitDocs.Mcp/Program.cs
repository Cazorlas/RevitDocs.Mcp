using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Paper.RevitDocs.Mcp.Documents;
using Paper.RevitDocs.Mcp.Http;
using Paper.RevitDocs.Mcp.Search;
using Paper.RevitDocs.Mcp.Sources;
using Paper.RevitDocs.Mcp.Sources.Paper;
using Paper.RevitDocs.Mcp.Sources.RevitApi;
using Paper.RevitDocs.Mcp.Sources.Repositories;
using Paper.RevitDocs.Mcp.Storage;
using Paper.RevitDocs.Mcp.Tools;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(15) });
builder.Services.AddSingleton(serviceProvider => new BoundedHttpClient(serviceProvider.GetRequiredService<HttpClient>()));
builder.Services.AddSingleton(new CachePaths(Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaperEngineer", "RevitDocsMcp")));
builder.Services.AddSingleton<DocumentStore>();
builder.Services.AddSingleton<RepositorySynchronizer>();
builder.Services.AddSingleton<DocumentSourceRegistry>(serviceProvider =>
{
    var boundedHttp = serviceProvider.GetRequiredService<BoundedHttpClient>();
    var store = serviceProvider.GetRequiredService<DocumentStore>();
    var rvtDocs = new RvtDocsSource(boundedHttp);
    var sources = new List<IDocumentSource>
    {
        new CachedDocumentSource(rvtDocs, rvtDocs, store, TimeSpan.FromDays(7))
    };

    var paperRoot = Environment.GetEnvironmentVariable("PAPER_REVIT_DOCS_ROOT");
    if (!string.IsNullOrWhiteSpace(paperRoot) && Directory.Exists(paperRoot))
    {
        var policyPath = Path.Combine(AppContext.BaseDirectory, "Configuration", "paper-docs.allowlist.json");
        try
        {
            var manifest = PaperDocumentPolicyManifest.Load(policyPath);
            var policy = new DocumentAccessPolicy(paperRoot, manifest.Include, manifest.Exclude);
            sources.Add(new PaperPublicDocumentSource(paperRoot, policy));
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Console.Error.WriteLine($"Paper public documentation is disabled: {exception.Message}");
        }
    }
    sources.AddRange(LoadRepositorySources());
    return new DocumentSourceRegistry(sources);
});
builder.Services.AddSingleton<DocumentSearchService>();
builder.Services.AddSingleton<DocumentReadService>();
// Unknown values are written as null, never left out: the output schema requires every property, and a client that
// checks structured content against it (Claude Code 2.1) refused each result whose cursor or revision was unknown.
var toolJson = new JsonSerializerOptions(ModelContextProtocol.McpJsonUtilities.DefaultOptions)
{
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
};
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<RevitDocsTools>(toolJson);

var host = builder.Build();
await host.Services.GetRequiredService<DocumentStore>().InitializeAsync(CancellationToken.None);
if (args.Length > 0 && args[0].Equals("sync", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length != 2)
    {
        Console.Error.WriteLine("Usage: Paper.RevitDocs.Mcp sync <source-id>");
        Environment.ExitCode = 2;
        return;
    }
    RepositoryManifest? manifest;
    try
    {
        manifest = LoadRepositoryManifests().FirstOrDefault(value => value.Id.Equals(args[1], StringComparison.OrdinalIgnoreCase));
    }
    catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
    {
        Console.Error.WriteLine($"Repository configuration is invalid: {exception.Message}");
        Environment.ExitCode = 2;
        return;
    }
    if (manifest is null)
    {
        Console.Error.WriteLine($"Unknown repository source: {args[1]}");
        Environment.ExitCode = 2;
        return;
    }
    try
    {
        var synced = await host.Services.GetRequiredService<RepositorySynchronizer>().SyncAsync(manifest, CancellationToken.None);
        Console.Error.WriteLine($"Synced {synced.Id} at {synced.Revision}.");
    }
    catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or IOException or JsonException)
    {
        Console.Error.WriteLine($"Sync failed: {exception.Message}");
        Environment.ExitCode = 1;
    }
    return;
}
await host.RunAsync();

static IEnumerable<IDocumentSource> LoadRepositorySources()
{
    var path = RepositoryConfigurationPath();
    if (!File.Exists(path)) return [];
    try
    {
        return LoadRepositoryManifests().Select(manifest => (IDocumentSource)new RepositorySource(ApplySyncedState(manifest))).ToArray();
    }
    catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
    {
        return [];
    }
}

static RepositoryManifest[] LoadRepositoryManifests()
{
    var path = RepositoryConfigurationPath();
    if (!File.Exists(path)) return [];
    var manifests = JsonSerializer.Deserialize<RepositoryManifest[]>(File.ReadAllText(path),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    return manifests.Select(manifest =>
    {
        if (string.IsNullOrWhiteSpace(manifest.LocalPath)) return manifest;
        var expanded = Environment.ExpandEnvironmentVariables(manifest.LocalPath);
        var resolved = Path.IsPathRooted(expanded) ? Path.GetFullPath(expanded)
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, expanded));
        return manifest with { LocalPath = resolved };
    }).ToArray();
}

static string RepositoryConfigurationPath()
{
    var configured = Environment.GetEnvironmentVariable("PAPER_REVIT_DOCS_REPOSITORY_CONFIG");
    return string.IsNullOrWhiteSpace(configured)
        ? Path.Combine(AppContext.BaseDirectory, "Configuration", "repository-sources.json")
        : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured));
}

static RepositoryManifest ApplySyncedState(RepositoryManifest manifest)
{
    var cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PaperEngineer", "RevitDocsMcp");
    return RepositorySyncStateStore.Apply(cacheRoot, manifest);
}
