[CmdletBinding()]
param(
    [string]$SolutionPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($SolutionPath)) {
    $SolutionPath = Join-Path $repositoryRoot 'OpenRPGForge.sln'
}
elseif (-not [System.IO.Path]::IsPathRooted($SolutionPath)) {
    $SolutionPath = Join-Path $repositoryRoot $SolutionPath
}
$SolutionPath = [System.IO.Path]::GetFullPath($SolutionPath)

$output = & dotnet package list --project $SolutionPath --vulnerable --include-transitive --no-restore --format json 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "NuGet vulnerability audit failed with exit code $LASTEXITCODE.`n$($output -join "`n")"
}

$document = ($output -join "`n") | ConvertFrom-Json
$vulnerable = @($document.projects | ForEach-Object {
    $frameworksProperty = $_.PSObject.Properties['frameworks']
    if ($null -ne $frameworksProperty) { $frameworksProperty.Value }
} | ForEach-Object {
    $packages = @()
    $topLevelProperty = $_.PSObject.Properties['topLevelPackages']
    $transitiveProperty = $_.PSObject.Properties['transitivePackages']
    if ($null -ne $topLevelProperty) { $packages += @($topLevelProperty.Value) }
    if ($null -ne $transitiveProperty) { $packages += @($transitiveProperty.Value) }
    $packages
} | Where-Object {
    $vulnerabilitiesProperty = $_.PSObject.Properties['vulnerabilities']
    $null -ne $vulnerabilitiesProperty -and @($vulnerabilitiesProperty.Value).Count -gt 0
})

if ($vulnerable.Count -gt 0) {
    throw "NuGet vulnerability audit found $($vulnerable.Count) vulnerable package entries."
}

Write-Output "OPENRPG_FORGE_NUGET_AUDIT_PASS projects=$(@($document.projects).Count) vulnerable=0"
