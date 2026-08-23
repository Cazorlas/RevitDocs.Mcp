param(
    [switch]$Quick
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'RevitDocs.Mcp.sln'

Push-Location $repositoryRoot
try {
    if (-not $Quick) {
        & dotnet restore $solutionPath
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    $testArguments = @('test', $solutionPath, '-c', 'Release')
    if ($Quick) { $testArguments += '--no-restore' }
    & dotnet @testArguments
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if (-not $Quick) {
        & dotnet build $solutionPath -c Release --no-restore
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        & dotnet list 'src\Paper.RevitDocs.Mcp\Paper.RevitDocs.Mcp.csproj' package --vulnerable --include-transitive
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    & git diff --check
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
