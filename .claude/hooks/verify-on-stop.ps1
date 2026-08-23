$ErrorActionPreference = 'Stop'
$inputJson = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($inputJson)) { exit 0 }

$hookInput = $inputJson | ConvertFrom-Json
if ($hookInput.stop_hook_active -eq $true) { exit 0 }

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$changed = & git -C $repositoryRoot status --porcelain --untracked-files=all 2>$null
if ($LASTEXITCODE -ne 0) {
    @{
        decision = 'block'
        reason = 'Unable to inspect repository state before stopping.'
    } | ConvertTo-Json -Compress
    exit 0
}
$relevant = $changed | Where-Object {
    $_ -match '(src/|tests/|scripts/|docs/|\.claude/|\.agents/|\.sln$|\.csproj$|\.gitignore$|AGENTS\.md$|CLAUDE\.md$|CODEX\.md$|INSTRUCTION\.md$|README\.md$|CONTRIBUTING\.md$|SECURITY\.md$|CHANGELOG\.md$|LICENSE$|NOTICE$)'
}
if (-not $relevant) { exit 0 }

$verifyScript = Join-Path $repositoryRoot 'scripts\verify.ps1'
$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
$output = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $verifyScript -Quick 2>&1 | Out-String
$verifyExitCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorActionPreference
if ($verifyExitCode -eq 0) { exit 0 }

$tail = ($output -split "`r?`n" | Select-Object -Last 30) -join "`n"
@{
    decision = 'block'
    reason = "Repository verification failed. Fix the failure before stopping:`n$tail"
} | ConvertTo-Json -Compress
