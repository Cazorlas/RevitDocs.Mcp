# RevitDocs.Mcp

RevitDocs.Mcp is a standalone, read-only STDIO MCP server for versioned Revit API documentation,
reviewed public sample code, and explicitly allowlisted Paper user documentation. It runs outside
`Revit.exe`, targets .NET 10, and does not require an AI-provider API key.
Any agent or client that supports MCP over local STDIO can connect; the server is independent of the AI provider.

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

The catalog enables the MIT-licensed sources recorded in [the source review](docs/source-review.md) at pinned commit revisions.
The Autodesk source remains disabled pending license and attribution review.
Synchronization remains an explicit command; an enabled source requires a reviewed local clone or synchronized snapshot before search can return its content.
Paper documents use an allowlist with fixed deny rules for source code, internal plans, agent instructions, build output, secrets, license payloads, personal data, and paths outside the configured root.

See [the architecture guide](docs/architecture.md) and
[standalone-repository decision](docs/decisions/0001-standalone-repository.md) for process and trust
boundaries.

## Prerequisites

- Windows 10 or later for the supplied self-contained `win-x64` publishing flow.
- .NET 10 SDK for source builds and tests.
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

Copy the published folder to a stable location and configure the command in any MCP client that supports STDIO.
The checked-in configurations are examples for two clients:

- [Codex configuration](src/Paper.RevitDocs.Mcp/ClientExamples/codex.config.toml)
- [Claude Desktop configuration](src/Paper.RevitDocs.Mcp/ClientExamples/claude_desktop_config.json)

Use the absolute path of `Paper.RevitDocs.Mcp.exe` in the client configuration. Restart the client,
call `revit_docs_sources`, and then search. The server communicates through STDIO; launching the
executable directly does not open an interactive window.

### ChatGPT, Claude, and public directories

The current release provides local STDIO only; it contains no HTTP endpoint, hosted service, or desktop-extension bundle.

| Client or distribution route | Current support | Required next step |
| --- | --- | --- |
| Codex and Claude Desktop local configuration | STDIO configuration examples are included; Claude Desktop visible-UI validation remains pending. | Configure the executable and verify sources, search, and read in the client. |
| ChatGPT custom MCP connection | The local executable cannot be entered as a remote MCP URL. | Provide a reachable HTTPS MCP endpoint and test the connection in ChatGPT. |
| Claude web custom connector | Requires a remote MCP server. | Provide a cloud-reachable MCP endpoint and add it in Connectors settings. |
| Public ChatGPT directory | Not submitted. | Complete the remote server, client tests, privacy documentation, and OpenAI submission review. |
| Claude desktop-extension directory | Not submitted. | Package a `.mcpb` extension, test it, and submit it for Anthropic review. |

Official setup and submission requirements are maintained in the [OpenAI MCP quickstart](https://developers.openai.com/plugins/build/app-quickstart), [OpenAI remote-server review requirements](https://developers.openai.com/plugins/deploy/app-review), [Claude local MCP guide](https://support.claude.com/en/articles/10949351-getting-started-with-local-mcp-servers-on-claude-desktop), and [Claude remote-connector guide](https://support.claude.com/en/articles/11175166-get-started-with-custom-connectors-using-remote-mcp).
Publishing this GitHub repository does not register it in either product directory.

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
- The project is licensed under the [MIT License](LICENSE); attribution is recorded in
  [NOTICE](NOTICE).
