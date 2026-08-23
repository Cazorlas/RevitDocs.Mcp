param(
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$publishRoot = Join-Path $repositoryRoot "artifacts\release-$Runtime-$stamp"
$projectPath = Join-Path $repositoryRoot 'src\Paper.RevitDocs.Mcp\Paper.RevitDocs.Mcp.csproj'
$testProject = Join-Path $repositoryRoot 'tests\Paper.RevitDocs.Mcp.Tests\Paper.RevitDocs.Mcp.Tests.csproj'

& (Join-Path $PSScriptRoot 'verify.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& dotnet publish $projectPath -c Release -r $Runtime --self-contained true -o $publishRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$serverPath = Join-Path $publishRoot 'Paper.RevitDocs.Mcp.exe'
$env:PAPER_MCP_PUBLISH_DIR = $publishRoot
$env:PAPER_MCP_SERVER_PATH = $serverPath
try {
    & dotnet test $testProject -c Release --no-restore --filter 'FullyQualifiedName~Packaging|FullyQualifiedName~Protocol'
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Remove-Item Env:PAPER_MCP_PUBLISH_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:PAPER_MCP_SERVER_PATH -ErrorAction SilentlyContinue
}

$hash = (Get-FileHash -LiteralPath $serverPath -Algorithm SHA256).Hash
Write-Output "Release: $publishRoot"
Write-Output "SHA256: $hash"
