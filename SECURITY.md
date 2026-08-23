# Security policy

## Supported versions

Until the first versioned release is tagged, security fixes are applied to the default branch. After
versioned releases begin, this section will identify the supported release lines explicitly.

## Reporting a vulnerability

Do not open a public issue for a suspected vulnerability. Use GitHub's private vulnerability report:

https://github.com/Cazorlas/RevitDocs.Mcp/security/advisories/new

Include the affected tool or provider, reproduction steps, expected and observed behavior, potential
impact, and any suggested mitigation. Remove credentials, personal data, private document contents,
and machine-specific paths before submitting evidence.

## Security boundary

The user-facing MCP surface is read-only. Reports involving arbitrary file access, path traversal,
symlink escape, allowlist bypass, credential exposure, unbounded network or archive processing,
STDOUT protocol corruption, source spoofing, or execution through retrieved content are treated as
security-sensitive.

Third-party pages and repositories are untrusted content. The server must never execute them or follow
instruction-like text retrieved from them.
