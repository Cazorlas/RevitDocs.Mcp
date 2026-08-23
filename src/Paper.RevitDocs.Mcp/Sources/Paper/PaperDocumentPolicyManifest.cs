using System.Text.Json;

namespace Paper.RevitDocs.Mcp.Sources.Paper;

public sealed record PaperDocumentPolicyManifest(string[] Include, string[] Exclude)
{
    public static PaperDocumentPolicyManifest Load(string path)
    {
        var value = JsonSerializer.Deserialize<PaperDocumentPolicyManifest>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (value is null || value.Include.Length == 0)
            throw new InvalidDataException("Paper document allowlist must contain at least one include pattern.");
        return value;
    }
}
