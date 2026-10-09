# Paper Revit Docs MCP

Local, read-only STDIO MCP server for Revit API documentation, reviewed public code sources, and explicitly allowlisted Paper user documentation. It runs as a separate `.NET 10` process and never loads into `Revit.exe`.
Any agent or client that supports MCP over local STDIO can connect; the server is independent of the AI provider.

## Tools

- `revit_docs_search`: searches versioned API docs and enabled public documentation sources.
- `revit_docs_read`: reads bounded content using an opaque search result ID.
- `revit_code_search`: searches enabled reviewed local repository snapshots; it never downloads during a search.
- `revit_docs_sources`: reports readiness, revision, attribution, license, and setup guidance.

Online RvtDocs results are cached under `%LOCALAPPDATA%\PaperEngineer\RevitDocsMcp`. If the endpoint is unavailable, matching cached results are returned as `CachedStale`; the server never invents replacement content.

## Client setup

Publish first:

```powershell
dotnet publish Paper.RevitDocs.Mcp.csproj -c Release -r win-x64 --self-contained true
```

Copy the published folder to a stable location, then adapt the examples in `ClientExamples`. MCP uses STDIO: do not run the executable in a terminal and expect an interactive UI.

For Paper documentation, set `PAPER_REVIT_DOCS_ROOT` to the repository root. `Configuration/paper-docs.allowlist.json` is the authoritative include/exclude policy. Agent rules, progress/spec files, build outputs, symlink escapes, paths outside that root, and content containing credential/private-key or developer-profile patterns remain denied even if a result ID is forged.

`Configuration/repository-sources.json` enables reviewed MIT-licensed sources at pinned commit revisions; the Autodesk source remains disabled pending license and attribution review.
Enabled sources require a reviewed clone configured through `localPath` or an explicit `Paper.RevitDocs.Mcp.exe sync <source-id>` command before search can return content.
Sync resolves the configured revision to a commit SHA, downloads a bounded GitHub archive, rejects traversal entries, and records the resolved revision.
The MCP client requires a restart after synchronization.
Search never downloads or executes code.

For an administrator-managed manifest outside the install folder, set `PAPER_REVIT_DOCS_REPOSITORY_CONFIG` to its full path. Persisted sync state stores only the resolved snapshot path, commit, and time; the current manifest always retains authority over enabled state, license, attribution, and file patterns.

## Security boundary

The server is read-only. It does not use API keys, execute sample code, modify Revit, load Revit assemblies, or expose arbitrary local files. Diagnostics go to stderr so stdout remains valid MCP JSON-RPC.
