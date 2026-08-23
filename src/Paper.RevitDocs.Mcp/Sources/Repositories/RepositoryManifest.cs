namespace Paper.RevitDocs.Mcp.Sources.Repositories;

public sealed record RepositoryManifest(
    string Id,
    string DisplayName,
    string RepositoryUrl,
    string? License,
    string? Attribution,
    bool Enabled,
    IReadOnlyList<string> Include,
    IReadOnlyList<string> Exclude,
    string? LocalPath = null,
    string? Revision = null,
    DateTimeOffset? SyncedAt = null);

public sealed record RepositorySyncState(
    string Revision,
    string LocalPath,
    DateTimeOffset SyncedAt);

public static class RepositoryManifestSecurity
{
    public static bool IsValidId(string id) =>
        !string.IsNullOrWhiteSpace(id)
        && id.Length <= 64
        && char.IsAsciiLetterOrDigit(id[0])
        && id.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');

    public static string GetSourceRoot(string cacheRoot, string id)
    {
        if (!IsValidId(id)) throw new InvalidOperationException("Repository source ID must be a lowercase alphanumeric slug with optional hyphens.");
        if (id.Any(char.IsUpper)) throw new InvalidOperationException("Repository source ID must be lowercase.");

        var repositoriesRoot = Path.GetFullPath(Path.Combine(cacheRoot, "repositories"));
        var sourceRoot = Path.GetFullPath(Path.Combine(repositoriesRoot, id));
        if (!sourceRoot.StartsWith(repositoriesRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Repository source directory escaped the cache root.");
        return sourceRoot;
    }
}
