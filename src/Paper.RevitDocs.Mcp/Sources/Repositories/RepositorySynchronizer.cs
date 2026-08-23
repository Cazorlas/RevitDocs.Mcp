using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using Paper.RevitDocs.Mcp.Storage;

namespace Paper.RevitDocs.Mcp.Sources.Repositories;

public sealed class RepositorySynchronizer(HttpClient http, CachePaths paths)
{
    private const long MaximumArchiveBytes = 100 * 1024 * 1024;
    private const long MaximumExtractedBytes = 500 * 1024 * 1024;
    private const int MaximumArchiveEntries = 100_000;

    public async Task<RepositoryManifest> SyncAsync(RepositoryManifest manifest, CancellationToken cancellationToken)
    {
        Validate(manifest);
        var repository = new Uri(manifest.RepositoryUrl);
        var parts = repository.AbsolutePath.Trim('/').Split('/');
        var revision = await ResolveRevisionAsync(parts[0], parts[1], manifest.Revision!, cancellationToken);
        var repositoryRoot = RepositoryManifestSecurity.GetSourceRoot(paths.Root, manifest.Id);
        var destination = Path.GetFullPath(Path.Combine(repositoryRoot, revision));
        if (!destination.StartsWith(repositoryRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Resolved revision escaped the repository cache root.");
        Directory.CreateDirectory(repositoryRoot);

        if (!Directory.Exists(destination))
        {
            var tempRoot = Path.Combine(repositoryRoot, ".sync-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var archive = await DownloadAsync(parts[0], parts[1], revision, cancellationToken);
                ExtractSafely(archive, tempRoot);
                Directory.Move(tempRoot, destination);
            }
            catch
            {
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
                throw;
            }
        }

        var state = new RepositorySyncState(revision, destination, DateTimeOffset.UtcNow);
        await File.WriteAllTextAsync(Path.Combine(repositoryRoot, "source-state.json"),
            JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        return manifest with { LocalPath = destination, Revision = revision, SyncedAt = state.SyncedAt };
    }

    private async Task<string> ResolveRevisionAsync(string owner, string name, string revision, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/commits/{Uri.EscapeDataString(revision)}");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Paper-RevitDocs-MCP", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var sha = json.RootElement.GetProperty("sha").GetString();
        return !string.IsNullOrWhiteSpace(sha) && sha.Length is >= 7 and <= 64 && sha.All(Uri.IsHexDigit)
            ? sha.ToLowerInvariant()
            : throw new InvalidDataException("GitHub did not return a valid hexadecimal commit SHA.");
    }

    private async Task<MemoryStream> DownloadAsync(string owner, string name, string revision, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"https://codeload.github.com/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/zip/{revision}",
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaximumArchiveBytes)
            throw new InvalidDataException("Repository archive exceeds the 100 MB limit.");
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (output.Length + read > MaximumArchiveBytes) throw new InvalidDataException("Repository archive exceeds the 100 MB limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        output.Position = 0;
        return output;
    }

    private static void ExtractSafely(Stream archiveStream, string destination)
    {
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);
        if (archive.Entries.Count > MaximumArchiveEntries)
            throw new InvalidDataException("Repository archive contains too many entries.");
        long extractedBytes = 0;
        string? archiveRoot = null;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length > MaximumExtractedBytes - extractedBytes)
                throw new InvalidDataException("Repository archive exceeds the 500 MB extracted-size limit.");
            extractedBytes += entry.Length;
            var normalized = entry.FullName.Replace('\\', '/').Trim('/');
            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) continue;
            if (segments.Any(segment => segment is "." or ".."))
                throw new InvalidDataException("Repository archive contains a traversal entry.");
            archiveRoot ??= segments[0];
            if (!segments[0].Equals(archiveRoot, StringComparison.Ordinal))
                throw new InvalidDataException("Repository archive must have one common top-level directory.");
            if (segments.Length == 1) continue;
            var relative = Path.Combine(segments.Skip(1).ToArray());
            var target = Path.GetFullPath(Path.Combine(destination, relative));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Repository archive contains a traversal entry.");
            if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, false);
        }
    }

    private static void Validate(RepositoryManifest manifest)
    {
        _ = RepositoryManifestSecurity.GetSourceRoot(Path.GetTempPath(), manifest.Id);
        if (!manifest.Enabled) throw new InvalidOperationException("Enable the reviewed source in repository-sources.json first.");
        if (string.IsNullOrWhiteSpace(manifest.License) || string.IsNullOrWhiteSpace(manifest.Attribution))
            throw new InvalidOperationException("License and attribution must be reviewed before sync.");
        if (string.IsNullOrWhiteSpace(manifest.Revision)) throw new InvalidOperationException("A branch, tag, or commit revision is required.");
        if (!Uri.TryCreate(manifest.RepositoryUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Trim('/').Split('/').Length != 2)
            throw new InvalidOperationException("Only reviewed github.com owner/repository URLs are supported.");
    }
}
