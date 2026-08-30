[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AuditDirectory,

    [Parameter(Mandatory = $true)]
    [ValidateLength(1, 256)]
    [string]$ReviewProvenance,

    [Parameter(Mandatory = $true)]
    [ValidateLength(1, 256)]
    [string]$ReferenceBaseline,

    [Parameter(Mandatory = $true)]
    [switch]$ReferenceQualityNotRegressed,

    [Parameter(Mandatory = $true)]
    [switch]$TerrainTextureCredible,

    [Parameter(Mandatory = $true)]
    [switch]$VegetationCredible,

    [Parameter(Mandatory = $true)]
    [switch]$BuildingsCredibleAndGrounded,

    [Parameter(Mandatory = $true)]
    [switch]$EntrancesAlignedAndOperable,

    [Parameter(Mandatory = $true)]
    [switch]$CharactersCredibleAndGrounded,

    [Parameter(Mandatory = $true)]
    [switch]$CameraViewsReadable,

    [ValidateLength(0, 1000)]
    [string]$Notes = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$AuditDirectory = [System.IO.Path]::GetFullPath($AuditDirectory)
$reportPath = Join-Path $AuditDirectory 'map-quality-machine-audit.json'
if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
    throw "Machine audit report '$reportPath' is missing."
}
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($report.schemaVersion -ne 1 -or -not [bool]$report.machineResult.passed) {
    throw 'Visual review cannot accept a map that failed its runtime machine audit.'
}
$ReferenceBaseline = $ReferenceBaseline.Trim()
if ([string]::IsNullOrWhiteSpace($ReferenceBaseline)) {
    throw 'Visual review requires the explicit product reference used for comparison.'
}

$criteria = [ordered]@{
    referenceQualityNotRegressed = [bool]$ReferenceQualityNotRegressed
    terrainTextureCredible = [bool]$TerrainTextureCredible
    vegetationCredible = [bool]$VegetationCredible
    buildingsCredibleAndGrounded = [bool]$BuildingsCredibleAndGrounded
    entrancesAlignedAndOperable = [bool]$EntrancesAlignedAndOperable
    charactersCredibleAndGrounded = [bool]$CharactersCredibleAndGrounded
    cameraViewsReadable = [bool]$CameraViewsReadable
}
$failedCriteria = @($criteria.GetEnumerator() | Where-Object { -not $_.Value } | ForEach-Object Key)
if ($failedCriteria.Count -gt 0) {
    throw "Visual review rejected the map: $($failedCriteria -join ', '). Correct it and rerun the complete audit."
}

$captureEvidence = @()
foreach ($capture in @($report.captures)) {
    $capturePath = Join-Path $AuditDirectory ([string]$capture.fileName)
    if (-not (Test-Path -LiteralPath $capturePath -PathType Leaf)) {
        throw "Visual-review capture '$capturePath' is missing."
    }
    $sha256 = (Get-FileHash -LiteralPath $capturePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($sha256 -ne [string]$capture.sha256) {
        throw "Visual-review capture '$capturePath' changed after the machine audit."
    }
    $captureEvidence += [ordered]@{
        role = [string]$capture.role
        fileName = [string]$capture.fileName
        sha256 = $sha256
    }
}
$requiredRoles = @('overview', 'overhead', 'ground-level')
if ([int]$report.machineResult.input.requiredPatrolActorCount -gt 0) {
    $requiredRoles += 'patrol-character'
}
if ([int]$report.machineResult.input.requiredBuildingGroupCount -gt 0) {
    $requiredRoles += @('building-detail', 'door-closed', 'door-open')
}
$missingRoles = @($requiredRoles | Where-Object { $_ -notin @($captureEvidence.role) })
if ($captureEvidence.Count -lt [Math]::Max(3, [int]$report.machineResult.input.requiredCaptureCount) -or
    $missingRoles.Count -gt 0) {
    throw "Visual review evidence is incomplete. Missing roles: $($missingRoles -join ', ')."
}

$fingerprintSource = [string]::Join("`n", @(
    'openrpg-map-quality-visual-review-v2'
    [string]$report.areaId
    $ReviewProvenance.Trim()
    $ReferenceBaseline
    ($criteria.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value.ToString().ToLowerInvariant())" })
    ($captureEvidence | ForEach-Object { "$($_.role):$($_.fileName)=$($_.sha256)" })
))
$bytes = [System.Text.Encoding]::UTF8.GetBytes($fingerprintSource)
$fingerprint = [System.Convert]::ToHexString(
    [System.Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
$review = [ordered]@{
    schemaVersion = 2
    areaId = [string]$report.areaId
    areaSlug = [string]$report.areaSlug
    reviewedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    reviewProvenance = $ReviewProvenance.Trim()
    referenceBaseline = $ReferenceBaseline
    machineReport = 'map-quality-machine-audit.json'
    criteria = $criteria
    captures = $captureEvidence
    notes = $Notes.Trim()
    fingerprint = $fingerprint
    passed = $true
}
$reviewPath = Join-Path $AuditDirectory 'map-quality-visual-review.json'
$json = $review | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText(
    $reviewPath,
    $json + [System.Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
Write-Output "OPENRPG_FORGE_MAP_QUALITY_PASS area=$($report.areaSlug) captures=$($captureEvidence.Count) fingerprint=$fingerprint review=$reviewPath"
