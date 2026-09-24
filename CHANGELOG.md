# Changelog

All notable user-visible changes to this project are documented here. The format follows Keep a
Changelog, and versioned releases will follow Semantic Versioning once release tags begin.

## Unreleased

### Added

- Standalone .NET 8 STDIO MCP server with four bounded read-only documentation tools.
- Versioned RvtDocs search/read with fresh and stale SQLite cache states.
- Reviewed repository-source catalog and allowlisted Paper documentation provider.
- Self-contained Windows publishing and published-process protocol verification.
- Repository-local guidance, skills, and hooks for Codex and Claude Code.

### Fixed

- Structured tool results keep every property their output schema requires, writing unknown values as
  `null`; clients that validate structured content (Claude Code 2.1) had refused results with no next cursor or
  revision.

### Security

- Deny-wins Paper document policy, canonical path containment, bounded remote content, and explicit
  source synchronization.
