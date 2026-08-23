$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$branch = (& git -C $repositoryRoot branch --show-current 2>$null)
$changes = (& git -C $repositoryRoot status --short 2>$null | Measure-Object).Count

Write-Output "RevitDocs.Mcp context: read AGENTS.md then INSTRUCTION.md. Branch=$branch; changed paths=$changes. Keep all MCP tools read-only, keep stdout JSON-RPC-only, and verify sourced Revit/version/revision/cache facts instead of guessing."
