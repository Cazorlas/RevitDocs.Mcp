namespace Paper.RevitDocs.Mcp.Security;

internal static class SafeFileEnumerator
{
    public static IEnumerable<string> Enumerate(string root, int maximumFiles = 50_000)
    {
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(root));
        var count = 0;
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            string[] entries;
            try { entries = Directory.GetFileSystemEntries(directory); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { continue; }

            foreach (var entry in entries)
            {
                FileAttributes attributes;
                try { attributes = File.GetAttributes(entry); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { continue; }
                if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                if ((attributes & FileAttributes.Directory) != 0) { pending.Push(entry); continue; }
                if (++count > maximumFiles) yield break;
                yield return entry;
            }
        }
    }
}
