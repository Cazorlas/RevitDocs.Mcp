using System.IO.Compression;
using System.Net;
using System.Text.Json;
using NUnit.Framework;
using Paper.RevitDocs.Mcp.Sources.Repositories;
using Paper.RevitDocs.Mcp.Storage;

namespace Paper.RevitDocs.Mcp.Tests.Sources.Repositories;

[TestFixture]
public sealed class RepositorySynchronizerTests
{
    [Test]
    public void SyncAsync_RejectsArchiveTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "PaperRepoSync", Guid.NewGuid().ToString("N"));
        try
        {
            using var client = new HttpClient(new GitHubHandler(CreateArchive("../escape.txt", "secret")));
            var synchronizer = new RepositorySynchronizer(client, new CachePaths(root));

            Assert.That(async () => await synchronizer.SyncAsync(Manifest(), default),
                Throws.TypeOf<InvalidDataException>());
            Assert.That(File.Exists(Path.Combine(root, "repositories", "sample", "escape.txt")), Is.False);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [TestCase("main", GitHubHandler.Sha)]
    [TestCase("cf3748045978bc35fcae88417c3024209be44fbe", "cf3748045978bc35fcae88417c3024209be44fbe")]
    public async Task SyncAsync_RecordsResolvedCommitAndExtractedPath(string configuredRevision, string resolvedRevision)
    {
        var root = Path.Combine(Path.GetTempPath(), "PaperRepoSync", Guid.NewGuid().ToString("N"));
        try
        {
            var handler = new GitHubHandler(CreateArchive("sample-sha/src/Sample.cs", "class Sample {}"), resolvedRevision);
            using var client = new HttpClient(handler);
            var manifest = Manifest() with { Revision = configuredRevision };
            var synced = await new RepositorySynchronizer(client, new CachePaths(root)).SyncAsync(manifest, default);
            var expectedSnapshot = Path.Combine(root, "repositories", manifest.Id, resolvedRevision);
            var state = JsonSerializer.Deserialize<RepositorySyncState>(File.ReadAllText(
                Path.Combine(root, "repositories", manifest.Id, "source-state.json")))!;

            Assert.Multiple(() =>
            {
                Assert.That(handler.RequestedUris, Is.EqualTo(new[]
                {
                    $"https://api.github.com/repos/example/sample/commits/{configuredRevision}",
                    $"https://codeload.github.com/example/sample/zip/{resolvedRevision}"
                }));
                Assert.That(synced.Revision, Is.EqualTo(resolvedRevision));
                Assert.That(synced.LocalPath, Is.EqualTo(expectedSnapshot));
                Assert.That(File.Exists(Path.Combine(synced.LocalPath!, "src", "Sample.cs")), Is.True);
                Assert.That(state.Revision, Is.EqualTo(resolvedRevision));
                Assert.That(state.LocalPath, Is.EqualTo(expectedSnapshot));
            });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public void SyncAsync_RejectsSourceIdThatCanEscapeCacheRoot()
    {
        using var client = new HttpClient(new GitHubHandler(CreateArchive("sample-sha/src/Sample.cs", "class Sample {}")));
        var synchronizer = new RepositorySynchronizer(client, new CachePaths(Path.GetTempPath()));
        var unsafeManifest = Manifest() with { Id = "../outside" };

        Assert.That(async () => await synchronizer.SyncAsync(unsafeManifest, default),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void SyncAsync_RejectsNonHexRevisionReturnedByGitHub()
    {
        var root = Path.Combine(Path.GetTempPath(), "PaperRepoSync", Guid.NewGuid().ToString("N"));
        try
        {
            using var client = new HttpClient(new GitHubHandler(CreateArchive("sample-sha/src/Sample.cs", "class Sample {}"), "../../outside"));
            var synchronizer = new RepositorySynchronizer(client, new CachePaths(root));

            Assert.That(async () => await synchronizer.SyncAsync(Manifest(), default),
                Throws.TypeOf<InvalidDataException>());
            Assert.That(Directory.Exists(Path.Combine(root, "outside")), Is.False);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public void ApplySyncedState_DoesNotOverrideCurrentRevocationOrPatterns()
    {
        var root = Path.Combine(Path.GetTempPath(), "PaperRepoState", Guid.NewGuid().ToString("N"));
        try
        {
            var sourceRoot = Path.Combine(root, "repositories", "sample");
            var snapshot = Path.Combine(sourceRoot, "sha");
            Directory.CreateDirectory(snapshot);
            File.WriteAllText(Path.Combine(sourceRoot, "source-state.json"),
                System.Text.Json.JsonSerializer.Serialize(new RepositorySyncState("sha", snapshot, DateTimeOffset.UtcNow)));

            var disabled = Manifest() with { Enabled = false, Include = ["docs/**/*.md"] };
            var applied = RepositorySyncStateStore.Apply(root, disabled);

            Assert.Multiple(() =>
            {
                Assert.That(applied.Enabled, Is.False);
                Assert.That(applied.LocalPath, Is.Null);
                Assert.That(applied.Include, Is.EqualTo(new[] { "docs/**/*.md" }));
            });
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static RepositoryManifest Manifest() => new("sample", "Sample", "https://github.com/example/sample",
        "MIT", "Example", true, ["**/*.cs"], [], null, "main");

    private static byte[] CreateArchive(string path, string content)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        using (var writer = new StreamWriter(archive.CreateEntry(path).Open())) writer.Write(content);
        return stream.ToArray();
    }

    private sealed class GitHubHandler(byte[] archive, string? returnedSha = null) : HttpMessageHandler
    {
        public const string Sha = "0123456789abcdef0123456789abcdef01234567";
        public List<string> RequestedUris { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            RequestedUris.Add(request.RequestUri!.AbsoluteUri);
            if (request.RequestUri!.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent($"{{\"sha\":\"{returnedSha ?? Sha}\"}}") });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new ByteArrayContent(archive) });
        }
    }
}
