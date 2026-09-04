#requires -Version 7.5
[CmdletBinding()]
param([switch]$Record)

# Read-only validation, except explicit generation of the derived evidence manifest.
# This does not run Studio or turn screenshot hashes into a visual/GPU verdict.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$bundle = 'artifacts/gate-2/terrain-brushes-2026-09-04'
$projectId = '9d72c00d-02a6-50c2-87b3-411787233851'
$baseline = "artifacts/gate-2/modules-areas-2026-09-04/snapshots/$projectId-r7.forge.json"
$manifestPath = Join-Path $repo 'docs/evidence/gate2/BRUSH_VIEWPORT_ARTIFACTS.json'
function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Hash([string]$RelativePath) {
    (Get-FileHash -LiteralPath (Join-Path $repo $RelativePath) -Algorithm SHA256).Hash.ToLowerInvariant()
}
function SnapshotPath([int]$Revision) {
    if ($Revision -eq 7) { return $baseline }
    "$bundle/snapshots/$projectId-r$Revision.forge.json"
}
function Snapshot([int]$Revision) {
    Get-Content -Raw -LiteralPath (Join-Path $repo (SnapshotPath $Revision)) | ConvertFrom-Json
}
function Normalized([int]$Revision, [switch]$WithoutEditedHeights) {
    $document = Snapshot $Revision
    # History deliberately restamps the workspace and every area's terrain revision.
    $document.revision = 0
    foreach ($module in $document.project.modules) {
        foreach ($area in $module.areas) { $area.terrain.revision = 0 }
    }
    if ($WithoutEditedHeights) {
        $document.project.modules[0].areas[0].terrain.PSObject.Properties.Remove('height_deltas')
    }
    ConvertTo-Json -InputObject $document -Depth 64 -Compress
}

Require ((Hash $baseline) -ceq '297035827452c15186b9f17840784d575b98f660ddcfb8984102d10c8de61ae4') 'Baseline r7 changed'
Require ((Hash "$bundle/snapshots/$projectId-r25.forge.json") -ceq '467470990459c12c33a96093b2cad80d6dccd5368185a10d9276c52e6af469b3') 'Final r25 changed'
$counts = [ordered]@{ '7'=0; '8'=550; '9'=0; '10'=550; '11'=1886; '12'=2633; '13'=2633; '14'=4018; '15'=5609; '20'=550; '25'=5609 }
$baseMetadata = Normalized 7 -WithoutEditedHeights
foreach ($revision in $counts.Keys) {
    $document = Snapshot $revision
    Require ($document.schema -ceq 'forge.workspace' -and $document.schema_version -eq 2) "r$revision schema"
    Require ($document.revision -eq $revision -and $document.project.id -ceq $projectId) "r$revision identity"
    Require ($document.project.modules.Count -eq 2) "r$revision modules"
    Require ($document.project.modules[1].areas[0].terrain.seed.ToString() -ceq '18446744073709551615') "r$revision u64 seed"
    $deltas = $document.project.modules[0].areas[0].terrain.height_deltas
    $count = if ($null -eq $deltas) { 0 } else { $deltas.Count }
    Require ($count -eq $counts[$revision]) "r$revision height count"
    Require ((Normalized $revision -WithoutEditedHeights) -ceq $baseMetadata) "r$revision changed metadata or another area"
}
foreach ($pair in @(@(7,9), @(8,10), @(10,20), @(15,25))) {
    Require ((Normalized $pair[0]) -ceq (Normalized $pair[1])) "History mismatch r$($pair[0])/r$($pair[1])"
}
$testLog = Get-Content -Raw -LiteralPath (Join-Path $repo "$bundle/viewport-fix1-tests.log")
$results = [regex]::Matches($testLog, 'test result: ok\. (\d+) passed; (\d+) failed; (\d+) ignored;')
Require ($results.Count -eq 13) 'Expected 13 test-target results'
$passed = 0
foreach ($result in $results) {
    $passed += [int]$result.Groups[1].Value
    Require ($result.Groups[2].Value -eq '0' -and $result.Groups[3].Value -eq '0') 'Failed or ignored tests'
}
Require ($passed -eq 456) 'Expected 456 tests'
$logs = @('viewport-fix1-tests.log', 'viewport-fix1-clippy-native.log', 'viewport-fix1-clippy-wasm.log',
    'viewport-fix1-trunk-confirmed.log', 'viewport-fix1-native-confirmed.log')
foreach ($log in $logs) {
    $text = Get-Content -Raw -LiteralPath (Join-Path $repo "$bundle/$log")
    Require ($text -match 'Finished' -and $text -notmatch '(?m)^error(?:\[|:)') "Incomplete/failed log $log"
}
$diagnostics = Get-Content -Raw -LiteralPath (Join-Path $repo "$bundle/viewport-26-c7-reopened-diagnostics-r25.ax.txt")
$ledgerLine = @($diagnostics -split "`n" | Where-Object { $_ -match 'Texte \{"busy"' })[0]
$ledger = $ledgerLine.Substring($ledgerLine.IndexOf('{')) | ConvertFrom-Json
Require (-not $ledger.busy -and $null -eq $ledger.error -and $null -eq $ledger.projection.error) 'Renderer not ready'
Require ($ledger.renderer_starts -eq 1 -and $ledger.projection.chunks -eq 64 -and $ledger.projection.revision -eq 25) 'Reopened renderer mismatch'
Require ($ledger.projection.source_hash -ceq 'ca6415d25c2babe2750f4b75743e80c9da2c43a7be5ed295af249932687ca6ab') 'Reopened source hash mismatch'

if ($Record) {
    Require (-not (Test-Path -LiteralPath $manifestPath)) 'Refusing to overwrite existing manifest'
    $captures = @(Get-ChildItem -LiteralPath (Join-Path $repo $bundle) -Filter 'viewport-*.png' | Sort-Object Name)
    Require ($captures.Count -eq 27) 'Expected 27 acquired screenshots (including initial negative case)'
    $paths = @($baseline, 'target/release/forge-studio.exe', 'apps/forge_studio/dist/forge_studio_bg.wasm')
    $paths += $logs | ForEach-Object { "$bundle/$_" }
    $paths += $counts.Keys | Where-Object { $_ -ne 7 } | ForEach-Object { SnapshotPath $_ }
    foreach ($capture in $captures) {
        $paths += "$bundle/$($capture.Name)"
        $paths += "$bundle/$($capture.BaseName).ax.txt"
    }
    $artifacts = @($paths | ForEach-Object {
        [ordered]@{ path=$_; bytes=(Get-Item -LiteralPath (Join-Path $repo $_)).Length; sha256=(Hash $_) }
    })
    $manifest = [ordered]@{
        schema='forge.gate2.brush-viewport-evidence'; schema_version=1
        source_commit='c7fc510dd66a04fbe8d780ce641216957ef8f81f'
        source_tree='d042fcf6f6f7191b47e81fa4e6f4ca825529c013'
        scope='Native brush workflow, history and clean-session reopen; no full Gate 2 or GPU/performance qualification'
        workspace_tests_passed=$passed; workspace_tests_failed=0; workspace_tests_ignored=0
        snapshot_height_counts=$counts; artifacts=$artifacts
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
}
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
Require ($manifest.source_commit -ceq 'c7fc510dd66a04fbe8d780ce641216957ef8f81f') 'Wrong source candidate'
$tree = git -C $repo rev-parse "$($manifest.source_commit)^{tree}"
Require ($LASTEXITCODE -eq 0 -and $tree -ceq $manifest.source_tree) 'Source tree mismatch'
Require ($manifest.artifacts.Count -eq 72) 'Unexpected manifest inventory'
foreach ($artifact in $manifest.artifacts) {
    Require ((Hash $artifact.path) -ceq $artifact.sha256) "Hash mismatch: $($artifact.path)"
    Require ((Get-Item -LiteralPath (Join-Path $repo $artifact.path)).Length -eq $artifact.bytes) "Size mismatch: $($artifact.path)"
}
Write-Output 'PASS: 72 artifact hashes; 11 snapshots; 4 exact history pairs; metadata/other areas intact; 456 tests; reopened r25 renderer.'
Write-Output 'Scope: evidence integrity and source comparisons, not a fresh test/build/UI run or GPU/performance verdict.'
