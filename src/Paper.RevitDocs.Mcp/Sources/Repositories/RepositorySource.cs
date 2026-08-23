using System.IO.Enumeration;
using System.Diagnostics;
using System.Text;
using Paper.RevitDocs.Mcp.Documents;
using Paper.RevitDocs.Mcp.Security;

namespace Paper.RevitDocs.Mcp.Sources.Repositories;

public sealed class RepositorySource(RepositoryManifest manifest) : ICodeDocumentSource, IDocumentContentSource
{
    public string SourceId => manifest.Id;
    private readonly string? _effectiveRevision = ResolveLocalRevision(manifest.LocalPath) ?? manifest.Revision;
    private bool Ready => manifest.Enabled && !string.IsNullOrWhiteSpace(manifest.License)
                          && !string.IsNullOrWhiteSpace(manifest.Attribution)
                          && !string.IsNullOrWhiteSpace(manifest.LocalPath) && Directory.Exists(manifest.LocalPath);

    public async Task<IReadOnlyList<DocumentReference>> SearchAsync(DocumentSearchQuery query, CancellationToken cancellationToken)
    {
        if (!Ready || string.IsNullOrWhiteSpace(query.Query)) return [];
        var root = Path.GetFullPath(manifest.LocalPath!);
        var results = new List<DocumentReference>();
        foreach (var path in SafeFileEnumerator.Enumerate(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (!IsIncluded(relative) || !MatchesLanguage(relative, query.Language) || new FileInfo(path).Length > 1_000_000) continue;
            string text;
            try { text = await File.ReadAllTextAsync(path, cancellationToken); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { continue; }
            var index = text.IndexOf(query.Query, StringComparison.OrdinalIgnoreCase);
            if (index < 0) continue;
            var uri = BuildUri(relative);
            results.Add(new DocumentReference(Encode(relative), SourceId, relative, null, uri, null,
                _effectiveRevision, DocumentKind.Code, CacheState.Local, 50,
                [new SourceLink(SourceId, uri, _effectiveRevision)], Snippet(text, index)));
            if (results.Count >= Math.Clamp(query.Limit, 1, 50)) break;
        }
        return results;
    }

    public async Task<DocumentContent?> ReadAsync(string resultId, int maxCharacters, string? cursor, CancellationToken cancellationToken)
    {
        if (!Ready || !TryDecode(resultId, out var relative)) return null;
        var root = Path.GetFullPath(manifest.LocalPath!);
        var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !IsIncluded(relative) || !File.Exists(path) || new FileInfo(path).Length > 1_000_000
            || !ResolvedPathStaysInsideRoot(root, path)) return null;
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        var offset = DecodeCursor(cursor);
        if (offset >= text.Length) return null;
        var length = Math.Min(text.Length - offset, Math.Clamp(maxCharacters, 1, 100_000));
        var next = offset + length < text.Length ? Convert.ToBase64String(BitConverter.GetBytes(offset + length)) : null;
        return new DocumentContent(resultId, SourceId, relative, text.Substring(offset, length), BuildUri(relative), null,
            _effectiveRevision, CacheState.Local, File.GetLastWriteTimeUtc(path), next);
    }

    public Task<DocumentSourceStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var message = !manifest.Enabled ? "Disabled until explicitly enabled."
            : string.IsNullOrWhiteSpace(manifest.License) || string.IsNullOrWhiteSpace(manifest.Attribution)
                ? "License and attribution must be reviewed before enabling."
                : string.IsNullOrWhiteSpace(manifest.LocalPath) || !Directory.Exists(manifest.LocalPath)
                    ? "Run the explicit sync command or configure a local clone." : null;
        return Task.FromResult(new DocumentSourceStatus(SourceId, manifest.DisplayName, manifest.Enabled, Ready,
            Ready ? "local" : "not_ready", _effectiveRevision, manifest.SyncedAt, manifest.Attribution, manifest.License, message));
    }

    private bool IsIncluded(string relative) => manifest.Include.Any(pattern => Match(pattern, relative))
                                                && !manifest.Exclude.Any(pattern => Match(pattern, relative));
    private static bool Match(string pattern, string path) => MatchParts(pattern.Replace('\\', '/').Split('/'), 0, path.Split('/'), 0);
    private static bool MatchesLanguage(string path, string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return true;
        var extension = Path.GetExtension(path);
        return language.Trim().ToLowerInvariant() switch
        {
            "csharp" or "c#" or "cs" => extension.Equals(".cs", StringComparison.OrdinalIgnoreCase),
            "fsharp" or "f#" or "fs" => extension.Equals(".fs", StringComparison.OrdinalIgnoreCase)
                                              || extension.Equals(".fsx", StringComparison.OrdinalIgnoreCase),
            "python" or "py" => extension.Equals(".py", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
    private static bool MatchParts(string[] pattern, int pi, string[] path, int si)
    {
        if (pi == pattern.Length) return si == path.Length;
        if (pattern[pi] == "**") return MatchParts(pattern, pi + 1, path, si)
                                    || si < path.Length && MatchParts(pattern, pi, path, si + 1);
        return si < path.Length && FileSystemName.MatchesSimpleExpression(pattern[pi], path[si], true)
                                && MatchParts(pattern, pi + 1, path, si + 1);
    }
    private string BuildUri(string relative) => $"{manifest.RepositoryUrl.TrimEnd('/')}/blob/{_effectiveRevision ?? "HEAD"}/{relative}";
    private string Encode(string relative) => $"repo:{SourceId}:{Convert.ToBase64String(Encoding.UTF8.GetBytes(relative))}";
    private bool TryDecode(string id, out string relative)
    {
        relative = string.Empty;
        var prefix = $"repo:{SourceId}:";
        if (!id.StartsWith(prefix, StringComparison.Ordinal)) return false;
        try { relative = Encoding.UTF8.GetString(Convert.FromBase64String(id[prefix.Length..])).Replace('\\', '/'); return !relative.Contains("..", StringComparison.Ordinal); }
        catch (FormatException) { return false; }
    }
    private static string Snippet(string text, int index)
    {
        var start = Math.Max(0, index - 100);
        return text.Substring(start, Math.Min(300, text.Length - start)).ReplaceLineEndings(" ");
    }

    private static bool ResolvedPathStaysInsideRoot(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        var current = root;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (string.IsNullOrWhiteSpace(segment) || segment == "..") return false;
            current = Path.Combine(current, segment);
            FileSystemInfo? info = Directory.Exists(current) ? new DirectoryInfo(current)
                : File.Exists(current) ? new FileInfo(current) : null;
            if (info?.LinkTarget is null) continue;
            var resolved = info.ResolveLinkTarget(true)?.FullName;
            if (resolved is null) return false;
            current = Path.GetFullPath(resolved);
            if (!current.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private static string? ResolveLocalRevision(string? localPath)
    {
        if (string.IsNullOrWhiteSpace(localPath) || !Directory.Exists(Path.Combine(localPath, ".git"))) return null;
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = localPath,
                Arguments = "rev-parse HEAD",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null || !process.WaitForExit(3000) || process.ExitCode != 0) return null;
            var revision = process.StandardOutput.ReadToEnd().Trim();
            return revision.Length is >= 7 and <= 64 && revision.All(Uri.IsHexDigit) ? revision : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        { return null; }
    }

    private static int DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return 0;
        try { return Math.Max(0, BitConverter.ToInt32(Convert.FromBase64String(cursor))); }
        catch (Exception exception) when (exception is FormatException or ArgumentException) { return 0; }
    }
}
