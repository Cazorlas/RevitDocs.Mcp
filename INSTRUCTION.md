# RevitDocs.Mcp current state

## Scope

Standalone `.NET 8` STDIO MCP server for versioned Revit API documentation, reviewed public sample
code, and explicitly allowlisted Paper user documentation. User-facing tools are read-only.

## Current status

- The product and test projects have been separated from PaperPlus into `RevitDocs.Mcp.sln`.
- The assembly and four MCP tool names remain unchanged for client compatibility.
- RvtDocs provides online Revit API search/read with SQLite cache fallback.
- Repository manifests ship disabled; synchronization is explicit and never occurs during search.
- Codex and Claude project guidance, skills, and Claude Code hooks are repository-local.
- Public repository metadata now includes Apache-2.0 attribution, contributor guidance, private
  vulnerability reporting, and a curated changelog; README is the public entry point.
- Supported Revit years (2019-2027) are defined once in `Documents/RevitVersions.cs`.
- GitHub automation is checked in: Windows CI (`ci.yml`), Dependabot, a weekly watch for new Revit
  releases and rvtdocs.com API changes (`maintenance.yml`), and an `@claude` workflow (`claude.yml`).
  Procedures are in `docs/maintenance.md`.
- `main` is protected by two rulesets: `main: PR + CI required` (no bypass) and `main: only admin
  merges`. A direct push to `main` was rejected with GH013. Claude reviews same-repository,
  non-Dependabot pull requests, and `dependabot-automerge.yml` merges minor/patch Dependabot updates
  after CI.

## Validation

- `scripts/verify.ps1` (2026-09-30, after centralizing Revit versions): 60 passed, 3 skipped, 0 failed;
  Release build 0 warnings and 0 errors; no vulnerable packages. Explicit live smoke tests passed 2/2
  for Revit 2024 and 2027.
- `scripts/check-revit-release.ps1` reported no Revit 2028 on rvtdocs.com or NuGet on 2026-09-30, and
  detected 2027 on both when run with `-Supported 2026`. All workflows pass `actionlint`.
- Earlier baseline `scripts/verify.ps1`: 55 passed, 3 skipped, 0 failed; Release build completed with 0 warnings
  and 0 errors; no vulnerable NuGet packages were reported by the configured sources.
- `scripts/publish.ps1`: self-contained `win-x64` package protocol lane passed 4/4 tests.
- Explicit live `rvtdocs.com` search/read smoke test passed 1/1 for Revit 2024.
- Release candidate: `artifacts/release-win-x64-20260823-191438`.
- Release executable SHA-256:
  `E3EC4B3F6EE6B46C82E43E3E786025FF7590E1978B2E1A00BCE54A9CD0915FC3`.
- Both repository skills pass `quick_validate.py`; both Claude hooks were parsed and exercised.
- The source and test projects, their solution mappings, and the superseded feature documents were
  removed from PaperPlus after the standalone release was verified.
- Public Markdown relative links, private-path absence, ignore boundaries, and `git diff --check`
  passed. The two project skills validate and match byte-for-byte; SessionStart and both Stop-hook
  paths exit successfully.
- The cleaned PaperPlus solution builds with `DB2024`: 0 errors and 160 pre-existing warnings.

NUnit is intentionally pinned to `4.3.2`. NUnit `4.6.1` adds `Action` overloads alongside
`TestDelegate`, which makes existing `Assert.Multiple(...)` lambdas ambiguous on a clean compile.
Do not update it without changing and freshly compiling the affected tests.

## Known gaps

- Claude Desktop visible-UI interoperability has not been verified.
- The Claude GitHub App and `CLAUDE_CODE_OAUTH_TOKEN` secret were installed on 2026-09-30. A manual
  `Scheduled maintenance` run passed (no Revit 2028; live smoke 2/2), but its issue/`fix` path and the
  `@claude` workflow has not yet run end to end on GitHub.
- Dependabot auto-merge needs the `AUTOMERGE_TOKEN` secret (admin fine-grained token, this repository
  only), because a user-owned repository cannot grant ruleset bypass to GitHub Actions (API 422).
  Without it, qualifying pull requests are only reported. Its decision step was dry-run locally
  against real pull requests; the merge step has not yet run on GitHub.
- Public sample sources remain disabled until their license and attribution are reviewed.
- Symlink-escape tests may skip on Windows hosts without symbolic-link privileges.
- `Paper.UnitTest` currently compiles under both `DB2024` and the documented `DB2027` test lane, but
  NUnit3TestAdapter 6.2 discovers zero tests in both outputs. The previously documented 379-test
  `DB2027` result could not be reproduced on the current checkout and .NET 10.0.400 SDK, so PaperPlus
  unit-test execution remains unverified even though the full solution build succeeds; this is not
  counted as a passing test run.

## Next action

Review the disabled public-source
manifests and their attribution before enabling any additional source, then perform the remaining
Claude Desktop visible-UI smoke test.
