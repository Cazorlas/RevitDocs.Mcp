# RevitDocs.Mcp current state

## Scope

Standalone `.NET 10` STDIO MCP server for versioned Revit API documentation, reviewed public sample
code, and explicitly allowlisted Paper user documentation. User-facing tools are read-only.
Any MCP-compatible agent or client with local STDIO support can connect; client examples do not restrict the supported AI providers.

## Current status

- The product and test projects have been separated from PaperPlus into `RevitDocs.Mcp.sln`.
- The assembly and four MCP tool names remain unchanged for client compatibility.
- RvtDocs provides online Revit API search/read with SQLite cache fallback.
- The reviewed Building Coder, Nice3point Revit Toolkit, and ricaun RevitTest sources are enabled at immutable commits.
  License evidence and the disabled Autodesk SDK decision are recorded in `docs/source-review.md`; synchronization remains explicit and never occurs during search.
- AngleSharp is `1.8.4`; Microsoft.Data.Sqlite and Microsoft.Extensions.Hosting are `10.0.12`.
  Product and test projects target `net10.0`, and SDK workflow lanes use `10.0.x`.
- Codex and Claude project guidance, skills, and Claude Code hooks are repository-local.
- Public repository metadata now includes MIT licensing and attribution, contributor guidance, private
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

- PR #10 merged on 2026-10-09 at `100b5d55f2fc976fc3a3faf0e3b88872d552bfad`; the maintenance branch was removed locally and remotely, and the main checkout was clean and synchronized. Review follow-up adds deterministic full-SHA synchronization coverage (exact commits/archive URLs, snapshot path, and persisted state), with focused checks passing 6/6.
- Merged-main CI run `37934538315` passed `scripts/verify.ps1` (67 passed, 1 skipped; 0 build warnings/errors; no vulnerable product packages) and `scripts/publish.ps1` (5/5 protocol/package checks). The Windows artifact is available on that run; executable SHA-256: `16A9C32CD996B4CB1525526476AF8A20BF779D609D6A1AFF73B9BC305381CA3C`. CI can execute the two symlink checks skipped on the local host.
- Final local `scripts/verify.ps1` after review follow-up: 65 passed, 3 skipped, 0 failed; Release build 0 warnings/errors; no vulnerable product packages reported.
- The maintainer explicitly approved the project license change from Apache-2.0 to MIT on 2026-10-09.
  `LICENSE`, `NOTICE`, README, project guidance, changelog, and source-review references now agree on MIT; third-party license metadata is unchanged.
  The product project copies root `LICENSE` and `NOTICE` to build and publish output.
- License regression evidence: metadata and old-package checks failed 2/2 before implementation; the locked metadata check passed 1/1 after the change.
- MIT packaging verification: `scripts/publish.ps1` reran the full `scripts/verify.ps1` gate (64 passed, 3 skipped; clean Release build; no vulnerable product packages) and all 5 published-process protocol/package tests passed.
  Independent readback confirmed packaged `LICENSE` and `NOTICE` match the repository files.
- `scripts/publish.ps1` (2026-10-09) ran the full `scripts/verify.ps1` gate: 64 passed, 3 skipped, 0 failed; Release build 0 warnings and 0 errors; no vulnerable product packages reported.
- The self-contained .NET 10 `win-x64` package passed all 5 published-process protocol/package tests.
- Explicit live RvtDocs search/read smoke tests passed 2/2 for Revit 2024 and 2027 on .NET 10.
- Explicit published-executable synchronization succeeded for all three reviewed source commits; independent raw STDIO calls confirmed source readiness and search/read for each snapshot with matching revision (3/3).
- Test-first evidence: runtime boundary expected `net10.0` and failed against `net8.0`, then passed 1/1; catalog cases failed 3 and passed 1 against the original manifest, then passed 4/4 after enabling the reviewed pins. Locked tests were unchanged during implementation.
- Current local MIT release candidate: `artifacts/release-win-x64-20261009-193455`.
  Executable SHA-256: `18DEDE0A857A3E9EF6AEFA5FFE976EE1FB428AF713379A717A096A41ED27D2F3`.
  The earlier .NET 10 candidate `artifacts/release-win-x64-20261009-190321` predates the license/package-content change.
- Both updated project skills pass `quick_validate.py` and match byte-for-byte; `git diff --check` passed.
- `scripts/check-revit-release.ps1` reported no Revit 2028 on either RvtDocs or NuGet on 2026-10-09; the supported range remains 2019-2027.
- The maintenance batch includes the .NET 10 migration, reviewed source snapshots, MIT licensing, and client-independent MCP documentation.
- `AUTOMERGE_TOKEN` secret metadata confirms installation on 2026-10-09. Manual auto-merge verification for PR #9 reached the merge step but failed with HTTP 401 `Bad credentials` (run `37929187487`); independent PR readback confirms it remains open.
- After the maintainer replaced the secret, auto-merge run `37929870985` passed on 2026-10-09; independent GitHub readback confirms PR #9 merged at `2026-10-09T12:25:07Z`, commit `c5ea0f6ba2b8e91a21e27fc796c8ba147c1a52f7` (AngleSharp `1.8.2` to `1.8.3` on remote main).
  This verifies the real merge path; the separate local migration still targets AngleSharp `1.8.4`.

Earlier verification (before the .NET 10 migration):

- `scripts/verify.ps1` (2026-09-30, after centralizing Revit versions): 60 passed, 3 skipped, 0 failed;
  Release build 0 warnings and 0 errors; no vulnerable packages. Explicit live smoke tests passed 2/2
  for Revit 2024 and 2027.
- `scripts/check-revit-release.ps1` reported no Revit 2028 on rvtdocs.com or NuGet on 2026-09-30, and
  detected 2027 on both when run with `-Supported 2026`. All workflows pass `actionlint`.
- Earlier baseline `scripts/verify.ps1`: 55 passed, 3 skipped, 0 failed; Release build completed with 0 warnings
  and 0 errors; no vulnerable NuGet packages were reported by the configured sources.
- `scripts/publish.ps1`: self-contained `win-x64` package protocol lane passed 4/4 tests.
- Explicit live `rvtdocs.com` search/read smoke test passed 1/1 for Revit 2024.
- Earlier release candidate: `artifacts/release-win-x64-20260823-191438`.
- Earlier release executable SHA-256:
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

- Claude Desktop visible-UI interoperability remains unverified: the client opens, but Windows rejected keyboard control with `window_not_focused` even after a restore request.
  Both the normal roaming configuration and the Microsoft Store package's redirected roaming configuration were backed up, and `paper-revit-docs` was added pointing to the verified .NET 10 package; existing RevGen entries were preserved.
  The first user restart used the Store configuration before this correction; a further full client restart and foreground UI smoke test remain required.
- The Claude GitHub App and `CLAUDE_CODE_OAUTH_TOKEN` secret were installed on 2026-09-30. A manual
  `Scheduled maintenance` run passed (no Revit 2028; live smoke 2/2), but its issue/`fix` path and the
  `@claude` workflow has not yet run end to end on GitHub.
- Autodesk SDK source distribution rights remain unresolved; its catalog entry stays disabled with no approved license.
- Symlink-escape tests may skip on Windows hosts without symbolic-link privileges.
- `Paper.UnitTest` currently compiles under both `DB2024` and the documented `DB2027` test lane, but
  NUnit3TestAdapter 6.2 discovers zero tests in both outputs. The previously documented 379-test
  `DB2027` result could not be reproduced on the current checkout and .NET 10.0.400 SDK, so PaperPlus
  unit-test execution remains unverified even though the full solution build succeeds; this is not
  counted as a passing test run.

## Next action

Restart Claude Desktop and perform the remaining foreground UI sources/search/read smoke test.
Package optional client extensions or plugins for distribution while retaining the standard, client-independent MCP surface; remote directory submission requires a separately tested hosted transport.
