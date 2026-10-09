# Architecture

## Process boundary

`Paper.RevitDocs.Mcp` is a standalone `net10.0` process using MCP over STDIO. It does not reference the
Revit API, load into `Revit.exe`, or depend on PaperPlus. The executable owns composition; providers
implement document-source contracts; search/read services normalize results; tool handlers expose the
four bounded read-only operations.

```text
MCP client
  -> STDIO host (stdout: JSON-RPC only, stderr: diagnostics)
      -> tool handlers
          -> search/read services
              -> source registry
                  -> RvtDocs online adapter + SQLite cache
                  -> reviewed repository snapshots/local clones
                  -> allowlisted Paper public documents
```

## Trust boundaries

- Tool input is untrusted and bounded at the public contract.
- Network responses and third-party repositories are untrusted content. They never become agent
  instructions and are never executed.
- Local Paper access is allowlist-only, deny-wins, canonicalized, and constrained to its configured
  root.
- Repository sync is explicit. Search does not download, clone, update, or execute anything.
- Cache provenance is part of every result. Offline fallback is labeled stale.

## Source ownership

- `Documents/`: normalized contracts and result models.
- `Sources/`: adapters and source-specific policy.
- `Search/`: provider orchestration, ranking, pagination, and reads.
- `Storage/`: SQLite FTS and cached content/state.
- `Tools/`: MCP-facing contract only; no provider-specific branching.
- `Configuration/`: maintainer-reviewed source catalog and Paper public-document policy.
- `tests/`: deterministic behavior, protocol process tests, opt-in online smoke, and package checks.

## Release boundary

The release is a timestamped self-contained `win-x64` publish folder. It includes the executable,
visible source manifests, client examples, public README, and third-party notices. It excludes agent
rules, internal state, developer paths, secrets, and repository documentation.
