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
- GitHub Actions CI, Dependabot, a weekly watch for new Revit releases and rvtdocs.com API changes, and
  an `@claude` workflow; see `docs/maintenance.md`.

### Changed

- The project license changes from Apache-2.0 to MIT with maintainer approval; published packages include the project LICENSE and NOTICE.
- The standalone process targets .NET 10; AngleSharp is updated to `1.8.4`, and Microsoft.Extensions.Hosting and Microsoft.Data.Sqlite are updated to `10.0.12`.
- Reviewed MIT-licensed repository sources are enabled at pinned commit revisions; synchronization remains explicit, and the Autodesk source remains disabled pending review.
- The supported Revit years are defined once, in `RevitVersions`; the version-range error message and
  version-less search both follow it.

### Fixed

- Structured tool results keep every property their output schema requires, writing unknown values as
  `null`; clients that validate structured content (Claude Code 2.1) had refused results with no next cursor or
  revision.

### Security

- Deny-wins Paper document policy, canonical path containment, bounded remote content, and explicit
  source synchronization.
