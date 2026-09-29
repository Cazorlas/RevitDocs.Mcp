param(
    [string]$Probe = 'FilteredElementCollector',
    # Overrides the supported maximum read from source; used to exercise the detection path.
    [int]$Supported = 0
)

# Reports whether a Revit release newer than RevitVersions.Maximum has appeared.
# Two independent signals are checked; neither is inferred from the other:
#   rvtdocs - the documentation provider already serves search results for that year.
#   nuget   - the Revit API reference assemblies for that year are published on NuGet.
# In GitHub Actions the results are also written to GITHUB_OUTPUT.

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$versionsPath = Join-Path $repositoryRoot 'src/Paper.RevitDocs.Mcp/Documents/RevitVersions.cs'

if ($Supported -eq 0) {
    $match = Select-String -LiteralPath $versionsPath -Pattern 'public const int Maximum = (\d{4});'
    if (-not $match) { throw "Could not read RevitVersions.Maximum from $versionsPath." }
    $Supported = [int]$match.Matches[0].Groups[1].Value
}
$candidate = $Supported + 1

$query = [Uri]::EscapeDataString($Probe)
$searchUri = "https://rvtdocs.com/search/v2/api/?q=$query&v=$candidate&fields=title&limit=5&source=paper-mcp"
$search = Invoke-RestMethod -Uri $searchUri -TimeoutSec 30
$results = @($search.results) + @($search.current_version_results) | Where-Object { $_ -and "$($_.year_version)" -eq "$candidate" }
$rvtdocs = @($results).Count -gt 0

$index = Invoke-RestMethod -Uri 'https://api.nuget.org/v3-flatcontainer/nice3point.revit.api.revitapi/index.json' -TimeoutSec 30
$nugetVersions = @($index.versions | Where-Object { $_ -match "^$candidate\." })
$nuget = $nugetVersions.Count -gt 0

$summary = [ordered]@{
    supported = $Supported
    candidate = $candidate
    rvtdocs   = $rvtdocs
    nuget     = $nuget
    nuget_versions = ($nugetVersions -join ', ')
}
$summary.GetEnumerator() | ForEach-Object { Write-Output ("{0}={1}" -f $_.Key, $_.Value) }

if ($env:GITHUB_OUTPUT) {
    $summary.GetEnumerator() | ForEach-Object {
        Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value ("{0}={1}" -f $_.Key, "$($_.Value)".ToLowerInvariant())
    }
}
