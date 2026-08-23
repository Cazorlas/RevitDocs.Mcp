# Public Repository Setup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete RevitDocs.Mcp public repository metadata and synchronized AI guidance, then verify both the standalone repository and PaperPlus.

**Architecture:** Keep runtime code unchanged. Protect the repository contract with architecture tests, keep `AGENTS.md` as the single stable AI-rule source, and make public documentation link to focused contribution, security, architecture, and release documents.

**Tech Stack:** Markdown, Git, PowerShell, .NET 8, NUnit, Claude Code project hooks, Codex/Claude project skills.

## Global Constraints

- Keep Apache License 2.0 and add `NOTICE` with `Copyright 2026 Cazorlas`.
- Keep all four MCP tools read-only and keep disabled sources disabled.
- Do not add CI, issue templates, release automation, dependency bots, runtime dependencies, commits, tags, or pushes.
- Preserve unrelated changes in both repositories.
- Verify RevitDocs.Mcp and PaperPlus independently.

---

### Task 1: Protect public repository metadata

**Files:**
- Modify: `tests/Paper.RevitDocs.Mcp.Tests/Architecture/ProjectBoundaryTests.cs`

**Interfaces:**
- Consumes: repository root located through `RevitDocs.Mcp.sln`.
- Produces: `PublicRepositoryMetadata_IsCompleteAndPortable()` architecture test.

- [x] **Step 1: Add the failing architecture test**

Add a test that requires `.gitignore`, `LICENSE`, `NOTICE`, `README.md`, `CONTRIBUTING.md`,
`SECURITY.md`, and `CHANGELOG.md`; checks for Apache-2.0, `Copyright 2026 Cazorlas`, README links,
and rejects `D:\Repository` from public Markdown except design/plan history.

- [x] **Step 2: Prove the test fails before implementation**

Run:

```powershell
dotnet test tests/Paper.RevitDocs.Mcp.Tests/Paper.RevitDocs.Mcp.Tests.csproj -c Release --filter "FullyQualifiedName~PublicRepositoryMetadata_IsCompleteAndPortable"
```

Expected: failure because `NOTICE`, `CONTRIBUTING.md`, `SECURITY.md`, and `CHANGELOG.md` do not exist.

### Task 2: Complete public repository files

**Files:**
- Modify: `.gitignore`
- Modify: `README.md`
- Create: `NOTICE`
- Create: `CONTRIBUTING.md`
- Create: `SECURITY.md`
- Create: `CHANGELOG.md`

**Interfaces:**
- Consumes: existing build, publish, source-manifest, client-example, architecture, and license paths.
- Produces: complete public onboarding, contribution, vulnerability-reporting, and release-history surface.

- [x] **Step 1: Expand `.gitignore` only for actual local outputs**

Cover .NET/Visual Studio/Rider output, test and coverage output, release artifacts, local caches,
secrets, user settings, OS/editor temporary files, and Claude local settings. Keep `.agents/`,
`.claude/settings.json`, both skills, source manifests, client examples, docs, and tests trackable.

- [x] **Step 2: Add license notice and repository policy documents**

Use `Copyright 2026 Cazorlas` in `NOTICE`; direct contributors to `scripts/verify.ps1`; direct private
security reports to GitHub private vulnerability reporting without inventing an email address; keep
`CHANGELOG.md` at `Unreleased` until an intentional tag exists.

- [x] **Step 3: Rewrite README as the public entry point**

Include overview, read-only tools, trust boundary, prerequisites, quick start, verification/publish
commands, Codex and Claude client configuration, maintainer-only source CRUD, architecture,
contributing, security, changelog, and Apache-2.0 license links.

- [x] **Step 4: Run the focused test**

Run the Task 1 command. Expected: 1 passed, 0 failed.

### Task 3: Synchronize AI setup documentation

**Files:**
- Modify: `AGENTS.md`
- Modify: `CODEX.md`
- Modify: `CLAUDE.md`
- Modify: `INSTRUCTION.md`
- Modify: `.agents/skills/revit-docs-mcp/SKILL.md`
- Modify: `.claude/skills/revit-docs-mcp/SKILL.md`
- Inspect: `.claude/settings.json`
- Inspect: `.claude/hooks/session-context.ps1`
- Inspect: `.claude/hooks/verify-on-stop.ps1`

**Interfaces:**
- Consumes: public repository documents created by Task 2.
- Produces: one stable instruction chain and byte-identical Codex/Claude skills.

- [x] **Step 1: Route repository policy consistently**

Keep `AGENTS.md` authoritative; make `CODEX.md` and `CLAUDE.md` concise entry points; link contribution,
security, changelog, architecture, and repository verification where relevant.

- [x] **Step 2: Update both skills identically**

Add the repository metadata/documentation workflow and the requirement to run both standalone and
PaperPlus checks when a change crosses the separation boundary. Copy the exact same content to both
skill locations.

- [x] **Step 3: Validate skills and hooks**

Run `quick_validate.py` for both skill directories, compare SHA-256 hashes, parse
`.claude/settings.json`, and exercise SessionStart plus normal/re-entry Stop inputs. Expected: both
skills valid and identical; all hook commands exit 0; re-entry produces no output.

### Task 4: Verify both repositories and record evidence

**Files:**
- Modify: `INSTRUCTION.md`

**Interfaces:**
- Consumes: completed repository setup.
- Produces: fresh standalone and PaperPlus verification evidence.

- [x] **Step 1: Verify RevitDocs.Mcp**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
git diff --check
```

Expected: deterministic tests and Release build pass; dependency vulnerability scan reports no known
vulnerable package from configured sources; intentional environment-dependent skips are reported.

- [x] **Step 2: Verify PaperPlus independently**

Run from `D:\Repository\Cazorlas\RevitAPI-C\Revit API`:

```powershell
dotnet build PaperPlus.sln -c DB2024 --no-restore --nologo -v:q
```

Expected: 0 errors. Existing warnings are reported separately and no RevitDocs project remains in
the PaperPlus solution.

- [x] **Step 3: Record current evidence and inspect scope**

Update `INSTRUCTION.md` with the fresh results, then inspect `git status`, `git diff --stat`, public
Markdown links, ignored files, secrets, private paths, and stale PaperPlus references. Do not commit.
