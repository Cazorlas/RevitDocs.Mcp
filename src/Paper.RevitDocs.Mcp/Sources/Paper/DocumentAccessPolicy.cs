using System.IO.Enumeration;

namespace Paper.RevitDocs.Mcp.Sources.Paper;

public sealed class DocumentAccessPolicy
{
    private static readonly HashSet<string> DeniedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CLAUDE.md",
        "CODEX.md",
        "AGENTS.md",
        "INSTRUCTION.md"
    };

    private readonly string _root;
    private readonly string[] _includePatterns;
    private readonly string[] _excludePatterns;

    public DocumentAccessPolicy(string root, IEnumerable<string> includePatterns, IEnumerable<string>? excludePatterns = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(includePatterns);

        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        _includePatterns = includePatterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(Normalize)
            .ToArray();
        _excludePatterns = (excludePatterns ?? [])
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(Normalize)
            .ToArray();
    }

    public bool IsAllowed(string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath)) return false;

        var fullPath = Path.GetFullPath(candidatePath);
        var rootPrefix = _root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) return false;

        var relative = Normalize(Path.GetRelativePath(_root, fullPath));
        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment == "..")) return false;
        if (!ResolvedPathStaysInsideRoot(segments)) return false;
        if (DeniedFileNames.Contains(segments[^1])) return false;
        if (ContainsDeniedFolder(segments)) return false;

        return _includePatterns.Any(pattern => GlobMatches(pattern, relative))
               && !_excludePatterns.Any(pattern => GlobMatches(pattern, relative));
    }

    private bool ResolvedPathStaysInsideRoot(IReadOnlyList<string> segments)
    {
        var current = _root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            FileSystemInfo? info = Directory.Exists(current) ? new DirectoryInfo(current)
                : File.Exists(current) ? new FileInfo(current) : null;
            if (info?.LinkTarget is null) continue;
            var resolved = info.ResolveLinkTarget(true)?.FullName;
            if (resolved is null) return false;
            current = Path.GetFullPath(resolved);
            if (!current.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private static bool ContainsDeniedFolder(IReadOnlyList<string> segments)
    {
        for (var index = 0; index + 1 < segments.Count; index++)
        {
            if (!segments[index].Equals("docs", StringComparison.OrdinalIgnoreCase)) continue;
            if (segments[index + 1].Equals("progress", StringComparison.OrdinalIgnoreCase)
                || segments[index + 1].Equals("superpowers", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return segments.Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                                       || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)
                                       || segment.Equals(".git", StringComparison.OrdinalIgnoreCase));
    }

    private static bool GlobMatches(string pattern, string relativePath)
    {
        var patternSegments = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSegments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return MatchSegments(patternSegments, 0, pathSegments, 0);
    }

    private static bool MatchSegments(
        IReadOnlyList<string> pattern,
        int patternIndex,
        IReadOnlyList<string> path,
        int pathIndex)
    {
        if (patternIndex == pattern.Count) return pathIndex == path.Count;
        if (pattern[patternIndex] == "**")
        {
            if (MatchSegments(pattern, patternIndex + 1, path, pathIndex)) return true;
            return pathIndex < path.Count && MatchSegments(pattern, patternIndex, path, pathIndex + 1);
        }

        if (pathIndex == path.Count) return false;
        return FileSystemName.MatchesSimpleExpression(pattern[patternIndex], path[pathIndex], true)
               && MatchSegments(pattern, patternIndex + 1, path, pathIndex + 1);
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
}
