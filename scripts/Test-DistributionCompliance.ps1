[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DistributionDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not [System.IO.Path]::IsPathRooted($DistributionDirectory)) {
    $DistributionDirectory = Join-Path $repositoryRoot $DistributionDirectory
}
$distributionPath = [System.IO.Path]::GetFullPath($DistributionDirectory)
$requiredFiles = @(
    'compliance/PROJECT-LICENSE.txt',
    'compliance/sbom.cdx.json',
    'compliance/licenses.csv',
    'compliance/THIRD-PARTY-NOTICES.txt',
    'compliance/ASSET-CREDITS.md',
    'compliance/FORGESCRIPT-NOTICES.md',
    'compliance/SIGNING-STATUS.txt'
)
foreach ($relativePath in $requiredFiles) {
    $filePath = Join-Path $distributionPath $relativePath.Replace('/', '\')
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        throw "Distribution compliance file missing: $relativePath"
    }
}

$projectLicensePath = Join-Path $repositoryRoot 'LICENSE.txt'
$distributedLicensePath = Join-Path $distributionPath 'compliance/PROJECT-LICENSE.txt'
if (-not (Test-Path -LiteralPath $projectLicensePath -PathType Leaf)) {
    throw "Project license missing: $projectLicensePath"
}
$projectLicenseHash = (Get-FileHash -LiteralPath $projectLicensePath -Algorithm SHA256).Hash
$distributedLicenseHash = (Get-FileHash -LiteralPath $distributedLicensePath -Algorithm SHA256).Hash
if ($projectLicenseHash -ne $distributedLicenseHash) {
    throw 'The distributed project license differs from the repository license.'
}
$projectLicense = Get-Content -LiteralPath $projectLicensePath -Raw
if ($projectLicense -notmatch '(?m)^# PolyForm Noncommercial License 1\.0\.0\r?$' -or
    $projectLicense -notmatch '(?m)^Required Notice: Copyright 2026 Karenax and OpenRPG Forge contributors\r?$' -or
    $projectLicense -match 'OpenNever Forge') {
    throw 'The project license name, version or required notice is invalid.'
}

$bom = Get-Content -LiteralPath (Join-Path $distributionPath 'compliance/sbom.cdx.json') -Raw | ConvertFrom-Json
if ($bom.bomFormat -ne 'CycloneDX' -or $bom.specVersion -ne '1.6' -or $bom.components.Count -lt 3) {
    throw 'The CycloneDX SBOM has an invalid header or no useful components.'
}
$projectLicenseId = [string]$bom.metadata.component.licenses[0].license.id
if ($projectLicenseId -ne 'PolyForm-Noncommercial-1.0.0') {
    throw "The SBOM project license is invalid: $projectLicenseId"
}
$references = New-Object System.Collections.Generic.HashSet[string]
foreach ($component in $bom.components) {
    if ([string]::IsNullOrWhiteSpace([string]$component.'bom-ref') -or
        -not $references.Add([string]$component.'bom-ref')) {
        throw "Duplicate or empty SBOM component reference: $($component.'bom-ref')"
    }
    if ($null -eq $component.licenses -or $component.licenses.Count -eq 0 -or
        [string]::IsNullOrWhiteSpace([string]$component.licenses[0].license.id)) {
        throw "SBOM component has no SPDX license: $($component.name)"
    }
}
$licenses = @(Import-Csv -LiteralPath (Join-Path $distributionPath 'compliance/licenses.csv'))
if ($licenses.Count -ne $bom.components.Count) {
    throw "License registry count differs from the SBOM: licenses=$($licenses.Count) components=$($bom.components.Count)"
}
if (@($licenses | Where-Object { [string]::IsNullOrWhiteSpace($_.License) }).Count -gt 0) {
    throw 'The license registry contains an empty license.'
}

Write-Output "OPENRPG_FORGE_DISTRIBUTION_COMPLIANCE_PASS components=$($bom.components.Count) licenses=$($licenses.Count) project_license=PolyForm-Noncommercial-1.0.0 spec=1.6"
