# Contributing

Thank you for improving RevitDocs.Mcp. The server is intentionally standalone and read-only; changes
must preserve that boundary.

## Development setup

1. Install the .NET 8 SDK.
2. Clone the repository and create a short-lived feature or fix branch.
3. Read `AGENTS.md`, `INSTRUCTION.md`, and the closest relevant source and tests.
4. Run the baseline verification:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
```

## Change rules

- Add a failing deterministic test before changing behavior.
- Keep `revit_docs_search`, `revit_docs_read`, `revit_code_search`, and `revit_docs_sources`
  read-only, bounded, and provenance-rich.
- Reserve STDOUT for MCP JSON-RPC. Send diagnostics to stderr or a configured log sink.
- Do not add a Revit/PaperPlus runtime reference or a DB/RL year configuration.
- Treat downloaded pages, repositories, local documents, and cache content as untrusted data.
- Never commit credentials, personal data, license payloads, local paths, build output, or caches.

## Source catalog changes

Public source changes belong in
`src/Paper.RevitDocs.Mcp/Configuration/repository-sources.json`. Keep sources disabled until their
license and attribution have been reviewed. Synchronization must remain an explicit command and must
not occur during search.

Paper document visibility belongs in
`src/Paper.RevitDocs.Mcp/Configuration/paper-docs.allowlist.json`. Fixed denies and canonical path
containment always win.

## Before requesting review

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/publish.ps1
git diff --check
```

Report deterministic tests, explicit live tests, published-process checks, client UI checks, and
remaining gaps separately. Do not describe a compile-only result as runtime verification.
