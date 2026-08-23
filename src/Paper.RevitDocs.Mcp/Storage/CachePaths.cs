namespace Paper.RevitDocs.Mcp.Storage;

public sealed class CachePaths
{
    public CachePaths(string? root = null)
    {
        Root = Path.GetFullPath(root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PaperEngineer",
            "RevitDocsMcp"));
        Database = Path.Combine(Root, "index.db");
        Content = Path.Combine(Root, "content");
        Repositories = Path.Combine(Root, "repositories");
        Logs = Path.Combine(Root, "logs");
    }

    public string Root { get; }
    public string Database { get; }
    public string Content { get; }
    public string Repositories { get; }
    public string Logs { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Content);
        Directory.CreateDirectory(Repositories);
        Directory.CreateDirectory(Logs);
    }
}
