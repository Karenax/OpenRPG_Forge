#requires -Version 7.5
[CmdletBinding()]
param()

# Read-only evidence validation. This does not launch Studio or infer a GPU/performance verdict.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$bundle = 'artifacts/gate-2/partial-terrain-2026-09-04'
$projectId = '9d72c00d-02a6-50c2-87b3-411787233851'
$areaId = '5426f526-6ac5-508b-98b3-da80d3df7116'
$manifestPath = Join-Path $repo 'docs/evidence/gate2/PARTIAL_TERRAIN_REFRESH_ARTIFACTS.json'

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Hash([string]$RelativePath) {
    (Get-FileHash -LiteralPath (Join-Path $repo $RelativePath) -Algorithm SHA256).Hash.ToLowerInvariant()
}
function SnapshotPath([int]$Revision) {
    if ($Revision -eq 25) {
        return "artifacts/gate-2/terrain-brushes-2026-09-04/snapshots/$projectId-r25.forge.json"
    }
    "$bundle/snapshots/$projectId-r$Revision.forge.json"
}
function Snapshot([int]$Revision) {
    Get-Content -Raw -LiteralPath (Join-Path $repo (SnapshotPath $Revision)) | ConvertFrom-Json
}
function Normalized([int]$Revision, [switch]$WithoutEditedHeights) {
    $document = Snapshot $Revision
    $document.revision = 0
    foreach ($module in $document.project.modules) {
        foreach ($area in $module.areas) {
            $area.terrain.revision = 0
            if ($WithoutEditedHeights -and $area.id -ceq $areaId) {
                $area.terrain.PSObject.Properties.Remove('height_deltas')
            }
        }
    }
    ConvertTo-Json -InputObject $document -Depth 100 -Compress
}

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
Require ($manifest.schema -ceq 'forge.gate2.partial-terrain-refresh-evidence') 'Wrong evidence schema'
Require ($manifest.schema_version -eq 1 -and $manifest.route -ceq 'R2-014') 'Wrong evidence version/route'
Require ($manifest.source_commit -ceq 'b290e7f2744a7872cfb6af6ed8ddb8d3d5deed1f') 'Wrong source candidate'
$tree = git -C $repo rev-parse "$($manifest.source_commit)^{tree}"
Require ($LASTEXITCODE -eq 0 -and $tree -ceq $manifest.source_tree) 'Source tree mismatch'
Require ($manifest.artifacts.Count -eq 21) 'Unexpected artifact inventory'
foreach ($artifact in $manifest.artifacts) {
    $path = Join-Path $repo $artifact.path
    Require (Test-Path -LiteralPath $path -PathType Leaf) "Missing artifact: $($artifact.path)"
    Require ((Get-Item -LiteralPath $path).Length -eq $artifact.bytes) "Size mismatch: $($artifact.path)"
    Require ((Hash $artifact.path) -ceq $artifact.sha256) "Hash mismatch: $($artifact.path)"
}

$testLog = Get-Content -Raw -LiteralPath (Join-Path $repo "$bundle/workspace-tests.log")
$results = [regex]::Matches($testLog, 'test result: ok\. (\d+) passed; (\d+) failed; (\d+) ignored;')
Require ($results.Count -eq 13) 'Expected 13 test-target results'
$passed = 0
foreach ($result in $results) {
    $passed += [int]$result.Groups[1].Value
    Require ($result.Groups[2].Value -eq '0' -and $result.Groups[3].Value -eq '0') 'Failed or ignored workspace tests'
}
Require ($passed -eq 462 -and $passed -eq $manifest.workspace_tests_passed) 'Expected 462 workspace tests'

$cacheLog = Get-Content -Raw -LiteralPath (Join-Path $repo "$bundle/cache-parity.log")
Require ($cacheLog -match 'test result: ok\. 4 passed; 0 failed; 0 ignored;') 'Cache parity tests missing'
Require ([regex]::Matches($cacheLog, '(?m)^revision \d+: 3 chunks,').Count -eq 4) 'Expected four sparse cache transitions'
Require ($cacheLog -match '(?m)^revision 2: 0 chunks, 521 bytes') 'Metadata-only cache transition missing'

foreach ($log in @('clippy-native.log', 'clippy-wasm-final.log', 'trunk-release.log', 'native-release.log')) {
    $text = Get-Content -Raw -LiteralPath (Join-Path $repo "$bundle/$log")
    Require ($text -match 'Finished' -and $text -notmatch '(?m)^error(?:\[|:)') "Incomplete/failed final log $log"
}
$initialWasm = Get-Content -Raw -LiteralPath (Join-Path $repo "$bundle/clippy-wasm.log")
Require ($initialWasm -match 'too many lines \(108/100\)' -and $initialWasm -match 'could not compile') 'Initial WASM Clippy failure not preserved'

$wasmText = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes((Join-Path $repo "$bundle/bin/forge_studio-b290e7f.wasm")))
Require ($wasmText.Contains($manifest.source_commit)) 'WASM bundle does not embed the candidate commit'

$expected = [ordered]@{
    '25' = [ordered]@{ rebuilt=64; retained=0; deltas=5609 }
    '26' = [ordered]@{ rebuilt=3; retained=61; deltas=6120 }
    '27' = [ordered]@{ rebuilt=3; retained=61; deltas=5609 }
    '28' = [ordered]@{ rebuilt=3; retained=61; deltas=6120 }
}
foreach ($revision in $expected.Keys) {
    $numericRevision = [int]$revision
    $document = Snapshot $numericRevision
    Require ($document.schema -ceq 'forge.workspace' -and $document.schema_version -eq 2) "r$revision schema"
    Require ($document.revision -eq $numericRevision -and $document.project.id -ceq $projectId) "r$revision identity"
    $area = @($document.project.modules.areas | Where-Object { $_.id -ceq $areaId })
    $deltaCount = if ($null -eq $area[0].terrain.height_deltas) {
        0
    } else {
        @($area[0].terrain.height_deltas).Count
    }
    Require ($area.Count -eq 1 -and $deltaCount -eq $expected[$revision].deltas) "r$revision height count"
    Require ($document.project.modules[1].areas[0].terrain.seed.ToString() -ceq '18446744073709551615') "r$revision u64 seed"

    $prefix = if ($revision -eq 25) { '00-b290-r25-full-before' }
        elseif ($revision -eq 26) { '01-b290-r26-partial-3-of-64' }
        elseif ($revision -eq 27) { '02-b290-r27-undo-partial-3-of-64' }
        else { '03-b290-r28-redo-partial-3-of-64' }
    $ax = Get-Content -Raw -LiteralPath (Join-Path $repo "$bundle/snapshots/$prefix.ax.txt")
    $counter = "Terrain du projet · r$revision · 64 chunks · $($expected[$revision].rebuilt) recalculés / $($expected[$revision].retained) conservés"
    Require ($ax.Contains($counter)) "r$revision native counter missing"
}

Require ((Normalized 26) -cne (Normalized 25)) 'Stroke did not change target terrain'
Require ((Normalized 27) -ceq (Normalized 25)) 'Undo did not restore r25 exactly'
Require ((Normalized 28) -ceq (Normalized 26)) 'Redo did not restore r26 exactly'
$baseWithoutTarget = Normalized 25 -WithoutEditedHeights
foreach ($revision in @(26, 27, 28)) {
    Require ((Normalized $revision -WithoutEditedHeights) -ceq $baseWithoutTarget) "r$revision changed metadata or another area"
}

Write-Output 'PASS: R2-014 candidate/tree; 21 artifact hashes; 462 tests; native 64/0 then 3/61 stroke, undo and redo.'
Write-Output 'PASS: r27 restores r25; r28 restores r26; metadata/other areas and u64::MAX seed intact.'
Write-Output 'Scope: evidence integrity and source comparisons, not a fresh test/build/UI run or GPU/performance verdict.'
