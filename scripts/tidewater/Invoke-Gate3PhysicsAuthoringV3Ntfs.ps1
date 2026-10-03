#Requires -Version 7.2
# Gate 3 V3 NTFS launcher for the public physics/fire/flee authoring smoke.
# The default action is preparation only. Use -Run after the UI focus is
# explicitly released; the delegated smoke creates its own fresh fixture and
# ledger and never rewrites the parent authoring evidence.
[CmdletBinding(DefaultParameterSetName = 'Prepare')]
param(
    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [ValidateNotNullOrEmpty()]
    [string]$Candidate = 'E:\OpenRPG_Forge\.gate3-evidence\20261002\tidewater-authoring-003-candidate-ntfs\forge-studio-candidate.exe',

    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [ValidateNotNullOrEmpty()]
    [string]$FreshIsolatedFixture = 'E:\OpenRPG_Forge\.gate3-evidence\20261002\tidewater-gameplay-authoring-runtime-009\authoring-root',

    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [ValidateNotNullOrEmpty()]
    [string]$PreviousLedger = 'E:\OpenRPG_Forge\.gate3-evidence\20261002\tidewater-gameplay-authoring-runtime-009\gate3-gameplay-authoring-smoke-ledger.json',

    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [ValidateNotNullOrEmpty()]
    [string]$EvidenceRoot = 'E:\OpenRPG_Forge\.gate3-evidence\20261002\tidewater-physics-authoring-runtime-v3-ntfs-001',

    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$SourceOriginalHashPinned = '50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720',

    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$ExpectedCandidateSha256 = 'b5679b1fcf0fd1157c03dd767fa51a1bc1f778ab1d200af0b1021ecc553b134b',

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [switch]$Run,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$targetScript = Join-Path $PSScriptRoot 'Invoke-Gate3PhysicsAuthoringSmoke.ps1'

function Assert-V3NtfsPath {
    param(
        [Parameter(Mandatory)][string]$LiteralPath,
        [Parameter(Mandatory)][string]$Description
    )
    $full = [IO.Path]::GetFullPath($LiteralPath)
    if (-not $full.StartsWith('E:\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must stay on E: for this NTFS campaign: $full"
    }
    if (Test-Path -LiteralPath $full) {
        $attributes = (Get-Item -LiteralPath $full -Force).Attributes
        if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Description is a reparse point: $full"
        }
    }
    return $full
}

if ($SelfTest) {
    if (-not (Test-Path -LiteralPath $targetScript -PathType Leaf)) {
        throw "Physics smoke target is absent: $targetScript"
    }
    if ($ExpectedCandidateSha256 -notmatch '^[0-9a-f]{64}$') {
        throw 'Expected candidate SHA-256 is malformed.'
    }
    $raw = [IO.File]::ReadAllText($PSCommandPath)
    foreach ($needle in @(
            'Invoke-Gate3PhysicsAuthoringSmoke.ps1',
            'tidewater-authoring-003-candidate-ntfs',
            'tidewater-physics-authoring-runtime-v3-ntfs-001',
            'Assert-V3NtfsPath',
            '-Run'
        )) {
        if ($raw.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) {
            throw "V3 NTFS wrapper self-test selector missing: $needle"
        }
    }
    Write-Output 'TIDEWATER_GATE3_PHYSICS_AUTHORING_V3_NTFS_SELF_TEST_PASS'
    return
}

$candidatePath = Assert-V3NtfsPath -LiteralPath $Candidate -Description 'V3 candidate'
$fixturePath = Assert-V3NtfsPath -LiteralPath $FreshIsolatedFixture -Description 'Base authoring fixture'
$ledgerPath = Assert-V3NtfsPath -LiteralPath $PreviousLedger -Description 'Previous authoring ledger'
$evidencePath = Assert-V3NtfsPath -LiteralPath $EvidenceRoot -Description 'Fresh evidence root'

if (-not (Test-Path -LiteralPath $candidatePath -PathType Leaf)) { throw "V3 candidate absent: $candidatePath" }
if (-not (Test-Path -LiteralPath $fixturePath -PathType Container)) { throw "Base authoring fixture absent: $fixturePath" }
if (-not (Test-Path -LiteralPath $ledgerPath -PathType Leaf)) { throw "Previous authoring ledger absent: $ledgerPath" }
if (Test-Path -LiteralPath $evidencePath) { throw "Evidence root must be fresh: $evidencePath" }

$candidateSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $candidatePath).Hash.ToLowerInvariant()
$expectedSha256 = $ExpectedCandidateSha256.ToLowerInvariant()
if ($candidateSha256 -cne $expectedSha256) {
    throw "V3 candidate SHA-256 mismatch: observed=$candidateSha256 expected=$expectedSha256"
}

$previous = Get-Content -LiteralPath $ledgerPath -Raw | ConvertFrom-Json
if ([string]$previous.schema -cne 'forge.tidewater.gate3-gameplay-authoring-smoke' -or
    [string]$previous.status -cne 'passed' -or
    [bool]$previous.gate_qualified -or
    -not [bool]$previous.source_preserved) {
    throw 'Previous authoring ledger is not a passed, non-qualified, source-preserving ledger.'
}
$previousSourcePin = ([string]$previous.source_original_hash_pinned).ToLowerInvariant()
if ($previousSourcePin -cne $SourceOriginalHashPinned.ToLowerInvariant()) {
    throw 'Previous authoring ledger source pin differs from SourceOriginalHashPinned.'
}

if ($Run) {
    & $targetScript `
        -Candidate $candidatePath `
        -FreshIsolatedFixture $fixturePath `
        -PreviousLedger $ledgerPath `
        -SourceOriginalHashPinned $SourceOriginalHashPinned `
        -EvidenceRoot $evidencePath `
        -StartupTimeoutSeconds 180 `
        -InteractionTimeoutSeconds 120
    exit $LASTEXITCODE
}

[ordered]@{
    schema = 'forge.tidewater.gate3-physics-authoring-v3-ntfs-ready'
    status = 'ready_no_ui_started'
    candidate = $candidatePath
    candidate_sha256 = $candidateSha256
    fresh_isolated_fixture = $fixturePath
    previous_ledger = $ledgerPath
    evidence_root = $evidencePath
    source_original_hash_pinned = $SourceOriginalHashPinned.ToLowerInvariant()
    startup_timeout_seconds = 180
    interaction_timeout_seconds = 120
    runtime_claim = 'none; preparation only'
    run_requires = 'explicit -Run after gameplay_map releases UI focus'
} | ConvertTo-Json -Depth 5
