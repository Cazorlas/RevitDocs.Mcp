using System.Text.Json;

namespace Paper.RevitDocs.Mcp.Sources.Repositories;

public static class RepositorySyncStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static RepositoryManifest Apply(string cacheRoot, RepositoryManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.LocalPath)) return manifest;
        if (!manifest.Enabled || string.IsNullOrWhiteSpace(manifest.License) || string.IsNullOrWhiteSpace(manifest.Attribution))
            return manifest;

        string statePath;
        try
        {
            statePath = Path.Combine(RepositoryManifestSecurity.GetSourceRoot(cacheRoot, manifest.Id), "source-state.json");
        }
        catch (InvalidOperationException) { return manifest; }
        if (!File.Exists(statePath)) return manifest;

        try
        {
            var state = JsonSerializer.Deserialize<RepositorySyncState>(File.ReadAllText(statePath), JsonOptions);
            if (state is null || !Directory.Exists(state.LocalPath)) return manifest;
            var expectedRoot = Path.GetDirectoryName(statePath)!;
            var localPath = Path.GetFullPath(state.LocalPath);
            if (!localPath.StartsWith(expectedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return manifest;
            return manifest with { LocalPath = localPath, Revision = state.Revision, SyncedAt = state.SyncedAt };
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        { return manifest; }
    }
}
