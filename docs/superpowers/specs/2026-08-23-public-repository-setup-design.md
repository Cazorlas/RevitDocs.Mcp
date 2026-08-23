# RevitDocs.Mcp public repository setup design

## Goal

Finish the standalone repository metadata and contributor documentation without changing MCP runtime
behavior or enabling any currently disabled documentation source.

## Repository files

- Keep Apache License 2.0 as the project license.
- Add `NOTICE` with `Copyright 2026 Cazorlas`.
- Expand `.gitignore` from the repository's actual .NET, Visual Studio, Rider, test, publish, cache,
  secret, and local-agent outputs. Do not ignore tracked source manifests, client examples, tests,
  documentation, or repository-shared AI configuration.
- Expand `README.md` with the product boundary, supported tools, verified quick start, publish flow,
  Codex and Claude client setup, maintainer-only source CRUD, security boundary, architecture links,
  contribution links, and license.
- Add `CONTRIBUTING.md`, `SECURITY.md`, and `CHANGELOG.md`. Keep the changelog human-curated and use
  an Unreleased section until a versioned release is intentionally tagged.
- Do not add CI workflows, issue templates, release automation, or dependency bots in this change.

## AI guidance synchronization

- Keep `AGENTS.md` as the stable cross-agent source of truth.
- Update `CODEX.md` and `CLAUDE.md` only as client-specific entry points; do not duplicate the full
  rule set.
- Update `INSTRUCTION.md` with fresh verification evidence and the next real action.
- Keep `.agents/skills/revit-docs-mcp/SKILL.md` and `.claude/skills/revit-docs-mcp/SKILL.md`
  byte-identical and point them to the new public repository documents where relevant.
- Keep Claude hooks repository-relative and ensure SessionStart context names the current instruction
  chain. The Stop hook remains conditional and must not recurse.

## Verification

- Run `git diff --check` and inspect ignored/tracked files.
- Scan the changed public files for secrets, private machine paths, stale PaperPlus paths, and broken
  relative links.
- Validate both AI skills and confirm their hashes match.
- Exercise both Claude hooks, including `stop_hook_active` re-entry.
- Run `scripts/verify.ps1` and report deterministic tests, build result, dependency vulnerability
  result, and intentional skips separately.

## Git scope

Continue on `feature/standalone-setup`. Do not commit, tag, or push unless the user explicitly asks.
