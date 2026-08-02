[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
}

$snapshotRoot = Join-Path $RepositoryRoot 'Divers\OpenRPG_Forge_Aurora_Feature_Dossier'
$catalogPath = Join-Path $snapshotRoot 'feature_catalog.csv'
$sourceTrackerPath = Join-Path $snapshotRoot 'tracking\AURORA_PARITY_COVERAGE.csv'
$auditPath = Join-Path $RepositoryRoot 'tracking\project\AURORA_CORE_P0_AUDIT.csv'
$outputRoot = Join-Path $RepositoryRoot 'tracking\project'
$projectTrackerPath = Join-Path $outputRoot 'AURORA_PARITY_COVERAGE.csv'
$gateMatrixPath = Join-Path $outputRoot 'AURORA_GATE_MATRIX.csv'

foreach ($requiredPath in @($catalogPath, $sourceTrackerPath, $auditPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required Aurora parity input is missing: $requiredPath"
    }
}

$catalog = @(Import-Csv -LiteralPath $catalogPath -Delimiter ';' -Encoding UTF8)
$sourceTracker = @(Import-Csv -LiteralPath $sourceTrackerPath -Delimiter ';' -Encoding UTF8)
$audit = @(Import-Csv -LiteralPath $auditPath -Delimiter ';' -Encoding UTF8)

if ($catalog.Count -ne 717 -or $sourceTracker.Count -ne 717) {
    throw "Aurora parity snapshot must contain exactly 717 catalog and tracker rows."
}

if ($audit.Count -ne 76) {
    throw "Core P0 audit must contain exactly 76 rows."
}

$catalogIds = @($catalog | ForEach-Object id)
if (@($catalogIds | Sort-Object -Unique).Count -ne $catalog.Count) {
    throw 'Aurora parity catalog contains duplicate feature IDs.'
}

$sourceById = @{}
foreach ($row in $sourceTracker) {
    if (-not $catalogIds.Contains($row.feature_id)) {
        throw "Source tracker contains unknown feature ID '$($row.feature_id)'."
    }

    $sourceById[$row.feature_id] = $row
}

$auditById = @{}
foreach ($row in $audit) {
    if (-not $catalogIds.Contains($row.feature_id)) {
        throw "Core P0 audit contains unknown feature ID '$($row.feature_id)'."
    }

    if ($auditById.ContainsKey($row.feature_id)) {
        throw "Core P0 audit contains duplicate feature ID '$($row.feature_id)'."
    }

    $feature = $catalog | Where-Object id -eq $row.feature_id | Select-Object -First 1
    if ($feature.priority -ne 'P0' -or $feature.domain -notin @('SHELL', 'PROJECT', 'AREA', 'PALETTE', 'BUILD', 'QA')) {
        throw "Core audit feature '$($row.feature_id)' is not one of the 76 required Core P0 features."
    }

    if ($row.status -notin @('NotStarted', 'Designed', 'Implemented', 'Tested')) {
        throw "Core audit feature '$($row.feature_id)' has unsupported status '$($row.status)'."
    }

    if ($row.status -eq 'Tested' -and [string]::IsNullOrWhiteSpace($row.test_evidence)) {
        throw "Tested feature '$($row.feature_id)' must provide test evidence."
    }

    $auditById[$row.feature_id] = $row
}

function Get-TargetGate {
    param([Parameter(Mandatory)]$Feature)

    $priority = $Feature.priority
    $id = $Feature.id
    switch ($Feature.domain) {
        'SHELL' { return $(if ($priority -in @('P0', 'P1')) { 'G7' } else { 'G13' }) }
        'PROJECT' { return $(if ($priority -in @('P0', 'P1')) { 'G7' } elseif ($priority -eq 'P2') { 'G11' } else { 'G13' }) }
        'AREA' { return $(if ($priority -in @('P0', 'P1')) { 'G7' } elseif ($priority -eq 'P2') { 'G11' } else { 'G13' }) }
        'PALETTE' { return $(if ($priority -in @('P0', 'P1')) { 'G7' } elseif ($priority -eq 'P2') { 'G11' } else { 'G13' }) }
        { $_ -in @('CREATURE', 'FACTION', 'ITEM', 'PLACEABLE', 'DOOR', 'TRIGGER', 'ENCOUNTER', 'MERCHANT', 'COMPANION') } {
            return $(if ($priority -in @('P0', 'P1')) { 'G8' } else { 'G13' })
        }
        { $_ -in @('DIALOGUE', 'QUEST') } {
            return $(if ($priority -eq 'P0') { 'G1' } elseif ($priority -eq 'P1') { 'G8' } else { 'G13' })
        }
        { $_ -in @('WAYPOINT', 'AUDIO') } {
            return $(if ($priority -eq 'P0') { 'G1' } elseif ($priority -eq 'P1') { 'G7' } elseif ($priority -eq 'P2') { 'G11' } else { 'G13' })
        }
        'SCRIPT' { return $(if ($priority -eq 'P0') { 'G3' } elseif ($priority -eq 'P1') { 'G8' } else { 'G13' }) }
        'GAMEPLAY' {
            if ($id -eq 'ORF-GAME-020') { return 'G4' }
            return $(if ($priority -eq 'P0') { 'G2' } elseif ($priority -eq 'P1') { 'G7' } elseif ($priority -eq 'P2') { 'G11' } else { 'G13' })
        }
        'DM' { return $(if ($priority -in @('P1', 'P2')) { 'G10' } else { 'G13' }) }
        'MULTI' { return $(if ($priority -in @('P1', 'P2')) { 'G9' } else { 'G13' }) }
        'CONTENT' {
            if ($id -in @(
                    'ORF-CONT-001', 'ORF-CONT-002', 'ORF-CONT-003', 'ORF-CONT-006', 'ORF-CONT-007',
                    'ORF-CONT-019', 'ORF-CONT-023', 'ORF-CONT-024', 'ORF-CONT-030', 'ORF-CONT-031')) {
                return 'G4'
            }
            if ($id -in @('ORF-CONT-011', 'ORF-CONT-020')) { return 'G7' }
            return $(if ($priority -in @('P0', 'P1', 'P2')) { 'G11' } else { 'G13' })
        }
        'EE' {
            if ($id -in @('ORF-EE-002', 'ORF-EE-028')) { return 'G7' }
            return $(if ($priority -in @('P0', 'P1')) { 'G11' } else { 'G13' })
        }
        'AI' { return $(if ($priority -eq 'P0') { 'G5' } elseif ($priority -in @('P1', 'P2')) { 'G12' } else { 'G13' }) }
        'BUILD' {
            if ($id -in @('ORF-BUILD-001', 'ORF-BUILD-013', 'ORF-BUILD-014', 'ORF-BUILD-019')) { return 'G0' }
            if ($id -in @('ORF-BUILD-022', 'ORF-BUILD-023')) { return 'G11' }
            return $(if ($priority -eq 'P0') { 'G7' } elseif ($priority -in @('P1', 'P2')) { 'G11' } else { 'G13' })
        }
        'QA' {
            if ($id -in @('ORF-QA-029', 'ORF-QA-030')) { return 'G0' }
            if ($id -in @('ORF-QA-015', 'ORF-QA-026')) { return 'G4' }
            if ($id -eq 'ORF-QA-007') { return 'G11' }
            return $(if ($priority -eq 'P0') { 'G7' } elseif ($priority -eq 'P1') { 'G11' } else { 'G13' })
        }
        default { throw "No target gate rule exists for domain '$($Feature.domain)'." }
    }
}

function Get-Milestone {
    param([Parameter(Mandatory)][string]$Gate)

    switch ($Gate) {
        'G0' { 'Governance baseline' }
        'G1' { 'Narrative foundation' }
        'G2' { 'Data-driven gameplay' }
        'G3' { 'Safe ForgeScript' }
        'G4' { 'Saves and content security' }
        'G5' { 'Transactional agent foundation' }
        'G7' { 'Creator Alpha' }
        'G8' { 'RPG Creator Beta' }
        'G9' { 'Network Beta' }
        'G10' { 'Live Director Preview' }
        'G11' { 'OpenRPG Forge 1.0' }
        'G12' { 'Advanced creation assistant' }
        'G13' { 'Post-1.0 advanced capabilities' }
        default { throw "Unknown gate '$Gate'." }
    }
}

function Get-DesignEvidence {
    param([Parameter(Mandatory)]$SourceRow)

    if ([string]::IsNullOrWhiteSpace($SourceRow.design_evidence)) {
        return 'Divers/OpenRPG_Forge_Aurora_Feature_Dossier/FEATURE_CATALOG.md'
    }

    return "Divers/OpenRPG_Forge_Aurora_Feature_Dossier/$($SourceRow.design_evidence)"
}

function ConvertTo-DeterministicCsv {
    param(
        [Parameter(Mandatory)][object[]]$Rows
    )

    $lines = @($Rows | ConvertTo-Csv -Delimiter ';' -NoTypeInformation)
    return [string]::Join("`n", $lines) + "`n"
}

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Content
    )

    [System.IO.File]::WriteAllText(
        $Path,
        $Content,
        [System.Text.UTF8Encoding]::new($false))
}

$trackerRows = foreach ($feature in $catalog) {
    $source = $sourceById[$feature.id]
    $gate = Get-TargetGate -Feature $feature
    $audited = $auditById[$feature.id]
    $status = if ($null -ne $audited) { $audited.status } else { 'Researched' }
    $implementationEvidence = if ($null -ne $audited) { $audited.implementation_evidence } else { '' }
    $testEvidence = if ($null -ne $audited) { $audited.test_evidence } else { '' }
    $notes = if ($null -ne $audited) {
        $audited.notes
    }
    else {
        "Research baseline only. Audit implementation evidence before changing this status. Target gate: $gate."
    }

    [pscustomobject][ordered]@{
        feature_id = $feature.id
        domain = $feature.domain
        title = $feature.title
        priority = $feature.priority
        target_layer = $feature.layer
        status = $status
        design_evidence = Get-DesignEvidence -SourceRow $source
        implementation_evidence = $implementationEvidence
        test_evidence = $testEvidence
        owner = ''
        target_epic = $gate
        notes = $notes
    }
}

$gateRows = foreach ($feature in $catalog) {
    $gate = Get-TargetGate -Feature $feature
    [pscustomobject][ordered]@{
        feature_id = $feature.id
        domain = $feature.domain
        priority = $feature.priority
        target_gate = $gate
        target_milestone = Get-Milestone -Gate $gate
    }
}

[System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$trackerContent = ConvertTo-DeterministicCsv -Rows @($trackerRows)
$gateContent = ConvertTo-DeterministicCsv -Rows @($gateRows)

if ($Check) {
    foreach ($expected in @(
            [pscustomobject]@{ Path = $projectTrackerPath; Content = $trackerContent },
            [pscustomobject]@{ Path = $gateMatrixPath; Content = $gateContent })) {
        if (-not (Test-Path -LiteralPath $expected.Path)) {
            throw "Generated Aurora parity artifact is missing: $($expected.Path)"
        }

        $actual = [System.IO.File]::ReadAllText($expected.Path)
        if (-not [string]::Equals($actual, $expected.Content, [StringComparison]::Ordinal)) {
            throw "Generated Aurora parity artifact is stale: $($expected.Path)"
        }
    }
}
else {
    Write-Utf8NoBom -Path $projectTrackerPath -Content $trackerContent
    Write-Utf8NoBom -Path $gateMatrixPath -Content $gateContent
}

$statusSummary = $trackerRows | Group-Object status | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Count)" }
$gateSummary = $gateRows | Group-Object target_gate | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Count)" }
Write-Output "OPENRPG_FORGE_AURORA_TRACKER_UPDATED features=$($trackerRows.Count) $($statusSummary -join ' ')"
Write-Output "OPENRPG_FORGE_AURORA_GATES_ASSIGNED $($gateSummary -join ' ')"
if ($Check) {
    Write-Output 'OPENRPG_FORGE_AURORA_TRACKER_FRESH=PASS'
}
