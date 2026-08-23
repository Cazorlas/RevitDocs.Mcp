using System.Text.RegularExpressions;

namespace Paper.RevitDocs.Mcp.Security;

public static partial class PublicContentPolicy
{
    public static bool IsSafe(string content)
    {
        if (string.IsNullOrEmpty(content)) return true;
        if (content.Contains("-----BEGIN PRIVATE KEY-----", StringComparison.OrdinalIgnoreCase)
            || content.Contains("-----BEGIN OPENSSH PRIVATE KEY-----", StringComparison.OrdinalIgnoreCase)) return false;
        if (DeveloperPath().IsMatch(content)) return false;
        if (CredentialAssignment().IsMatch(content)) return false;
        if (BearerToken().IsMatch(content) || JwtToken().IsMatch(content)
            || GitHubToken().IsMatch(content) || AwsAccessKey().IsMatch(content)) return false;
        return !EmailAddress().IsMatch(content);
    }

    [GeneratedRegex(@"(?i)\b[A-Z]:\\Users\\[^\\\s]+\\")]
    private static partial Regex DeveloperPath();

    [GeneratedRegex("""(?im)\b(api[_-]?key|access[_-]?token|password|client[_-]?secret|secret|license[_-]?(key|payload|token))\b\s*[:=]\s*['"]?[^\s'"]{8,}""")]
    private static partial Regex CredentialAssignment();

    [GeneratedRegex(@"(?i)\bBearer\s+[A-Za-z0-9._~+/-]{16,}=*")]
    private static partial Regex BearerToken();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\b")]
    private static partial Regex JwtToken();

    [GeneratedRegex(@"\bgh[pousr]_[A-Za-z0-9]{20,}\b")]
    private static partial Regex GitHubToken();

    [GeneratedRegex(@"\b(AKIA|ASIA)[A-Z0-9]{16}\b")]
    private static partial Regex AwsAccessKey();

    [GeneratedRegex(@"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b")]
    private static partial Regex EmailAddress();
}
