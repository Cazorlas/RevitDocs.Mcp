# ADR 0001: Keep RevitDocs.Mcp independent from PaperPlus

## Status

Accepted on 2026-08-23.

## Decision

Host the documentation MCP in its own repository and solution. Keep the existing
`Paper.RevitDocs.Mcp` assembly and MCP tool names for client compatibility, but remove all PaperPlus
solution, layer, Revit-year, and build-matrix coupling.

## Why

The process runs outside Revit, targets one runtime, serves users independently, and is useful to
Codex, Claude, and other MCP clients. Keeping it in PaperPlus made Visual Studio configuration and
architecture tests carry exceptions for a product with a different lifecycle.

## Consequences

- Releases, source-catalog review, and client setup are managed here.
- PaperPlus can evolve without building or packaging this executable.
- Public Paper documentation remains optional through `PAPER_REVIT_DOCS_ROOT`; this does not grant
  access to Paper source or internal agent documentation.
