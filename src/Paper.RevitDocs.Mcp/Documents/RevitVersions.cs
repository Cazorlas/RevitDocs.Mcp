namespace Paper.RevitDocs.Mcp.Documents;

/// <summary>
/// The single source of truth for the Revit version years this server searches and accepts.
/// Supporting a new Revit release is a change to <see cref="Maximum"/>; see docs/maintenance.md.
/// </summary>
public static class RevitVersions
{
    public const int Minimum = 2019;
    public const int Maximum = 2027;

    public static IReadOnlyList<int> NewestFirst { get; } =
        Enumerable.Range(Minimum, Maximum - Minimum + 1).Reverse().ToArray();

    public static bool IsSupported(int version) => version is >= Minimum and <= Maximum;
}
