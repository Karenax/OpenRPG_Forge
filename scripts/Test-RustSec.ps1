[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$NoFetch
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-WarningKey {
    param([Parameter(Mandatory)] $Warning)
    return "$($Warning.id)|$($Warning.kind)|$($Warning.package)|$($Warning.version)"
}

$root = [IO.Path]::GetFullPath($RepositoryRoot)
$policyPath = Join-Path $root "docs/evidence/gate0/RUSTSEC_POLICY.json"
$policy = Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json
if ($policy.schema -ne "forge.rustsec-policy" -or $policy.schema_version -ne 1) {
    throw "Unsupported RustSec policy schema in $policyPath."
}

$auditArguments = @("audit")
if ($NoFetch) {
    $auditArguments += "--no-fetch"
}
$auditArguments += "--json"

Push-Location $root
try {
    $auditRaw = (& cargo @auditArguments | Out-String)
    if ($LASTEXITCODE -ne 0) {
        throw "cargo $($auditArguments -join ' ') failed with exit code $LASTEXITCODE."
    }
} finally {
    Pop-Location
}

$audit = $auditRaw | ConvertFrom-Json
if ($audit.vulnerabilities.count -ne 0 -or $audit.vulnerabilities.found) {
    throw "RustSec found $($audit.vulnerabilities.count) vulnerability entries."
}

$actual = @()
foreach ($warningGroup in $audit.warnings.PSObject.Properties) {
    foreach ($warning in @($warningGroup.Value)) {
        $actual += [pscustomobject]@{
            id      = [string]$warning.advisory.id
            kind    = [string]$warning.kind
            package = [string]$warning.package.name
            version = [string]$warning.package.version
        }
    }
}

$expectedKeys = @($policy.allowed_warnings | ForEach-Object { Get-WarningKey $_ } | Sort-Object)
$actualKeys = @($actual | ForEach-Object { Get-WarningKey $_ } | Sort-Object)
$differences = @(Compare-Object -ReferenceObject $expectedKeys -DifferenceObject $actualKeys)
if ($differences.Count -ne 0) {
    $rendered = $differences | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }
    throw "RustSec warning set differs from the exact Gate 0 policy:`n$($rendered -join [Environment]::NewLine)"
}

$distributionBlockers = @($policy.allowed_warnings | Where-Object distribution_blocking)
foreach ($blocker in $distributionBlockers) {
    Write-Warning "Distribution blocker retained: $(Get-WarningKey $blocker)"
}

Write-Output "FORGE_RUSTSEC_POLICY_PASS vulnerabilities=0 warnings=$($actual.Count) distribution_blockers=$($distributionBlockers.Count)"
