# RevitDocs.Mcp

RevitDocs.Mcp is a standalone, read-only STDIO MCP server for versioned Revit API documentation,
reviewed public sample code, and explicitly allowlisted Paper user documentation. It runs outside
`Revit.exe`, targets .NET 8, and does not require an AI-provider API key.

## MCP tools

| Tool | Purpose |
| --- | --- |
| `revit_docs_search` | Search versioned API and public documentation. |
| `revit_docs_read` | Read bounded content from a result returned by search. |
| `revit_code_search` | Search enabled, reviewed repository snapshots or local clones. |
| `revit_docs_sources` | Inspect source availability, attribution, revision, and cache state. |

All four tools are read-only. They cannot execute code, modify a Revit model, install software,
synchronize repositories during search, or read arbitrary local paths.

## Data and trust boundary

Online RvtDocs content is cached under `%LOCALAPPDATA%\PaperEngineer\RevitDocsMcp`. An outage may
return a matching entry explicitly labeled stale. The server does not invent content, silently switch
Revit versions, or treat retrieved text as agent instructions.

Repository sources are disabled until a maintainer reviews their license and attribution. Paper
documents use an allowlist with fixed deny rules for source code, internal plans, agent instructions,
build output, secrets, license payloads, personal data, and paths outside the configured root.

See [the architecture guide](docs/architecture.md) and
[standalone-repository decision](docs/decisions/0001-standalone-repository.md) for process and trust
boundaries.

## Prerequisites

- Windows 10 or later for the supplied self-contained `win-x64` publishing flow.
- .NET 8 SDK for source builds and tests.
- An MCP client that supports local STDIO servers, such as Codex or Claude Desktop.

The published Windows executable is self-contained and does not require a separate .NET runtime.

## Quick start from source

```powershell
git clone https://github.com/Cazorlas/RevitDocs.Mcp.git
cd RevitDocs.Mcp
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/publish.ps1
```

`publish.ps1` creates a timestamped self-contained folder under `artifacts/`, runs protocol and
package tests against the published executable, and prints its SHA-256.

## Configure an MCP client

Copy the published folder to a stable location and adapt one of the checked-in examples:

- [Codex configuration](src/Paper.RevitDocs.Mcp/ClientExamples/codex.config.toml)
- [Claude Desktop configuration](src/Paper.RevitDocs.Mcp/ClientExamples/claude_desktop_config.json)

Use the absolute path of `Paper.RevitDocs.Mcp.exe` in the client configuration. Restart the client,
call `revit_docs_sources`, and then search. The server communicates through STDIO; launching the
executable directly does not open an interactive window.

## Commands

```powershell
# Build and run deterministic tests
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1

# Build and verify a timestamped self-contained Windows package
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/publish.ps1

# Deliberately run the live rvtdocs.com smoke test
dotnet test tests/Paper.RevitDocs.Mcp.Tests/Paper.RevitDocs.Mcp.Tests.csproj `
  -c Release --filter "FullyQualifiedName~RevitApiOnlineSmokeTests"

# Check whether a Revit release newer than the supported range has appeared
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-revit-release.ps1
```

Build success proves compilation only. Client-visible behavior and live providers require their own
runtime evidence.

## Maintainer-only source CRUD

Users can only read. Maintainers add, edit, disable, or remove public code sources in
`src/Paper.RevitDocs.Mcp/Configuration/repository-sources.json`. Every entry must preserve a stable
ID, canonical repository URL, revision policy, include/exclude patterns, license, attribution, and
enabled state. Review the license before setting `enabled` to `true`.

Paper document visibility is managed separately in
`src/Paper.RevitDocs.Mcp/Configuration/paper-docs.allowlist.json`. A deny always wins, even when an
allow pattern is broad.

After changing a manifest:

```powershell
dotnet test RevitDocs.Mcp.sln -c Release
<published-folder>\Paper.RevitDocs.Mcp.exe sync <source-id>
```

For an administrator-owned manifest outside the installation directory, set
`PAPER_REVIT_DOCS_REPOSITORY_CONFIG`. To expose an installed set of public Paper documents, set
`PAPER_REVIT_DOCS_ROOT`. Never place credentials in either manifest.

## AI development setup

- [AGENTS.md](AGENTS.md) is the stable cross-agent rule source.
- [CODEX.md](CODEX.md) and [CLAUDE.md](CLAUDE.md) route each client to those rules and current state.
- `.claude/settings.json` provides SessionStart context and a conditional Stop verification hook.
- `.agents/skills/revit-docs-mcp` and `.claude/skills/revit-docs-mcp` contain the same project skill.
- [INSTRUCTION.md](INSTRUCTION.md) records verified current state, gaps, and the next action.
- [docs/maintenance.md](docs/maintenance.md) covers CI, the weekly Revit-release and provider watch,
  the `@claude` GitHub workflow, Dependabot, and the procedure for supporting a new Revit version.

## Project policy

- Read [CONTRIBUTING.md](CONTRIBUTING.md) before proposing a change.
- Report vulnerabilities using [SECURITY.md](SECURITY.md), not a public issue.
- User-visible changes are summarized in [CHANGELOG.md](CHANGELOG.md).
- The project is licensed under [Apache License 2.0](LICENSE); attribution is recorded in
  [NOTICE](NOTICE).
