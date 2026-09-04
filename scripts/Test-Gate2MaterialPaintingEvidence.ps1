#requires -Version 7.5
[CmdletBinding()]
param()

# Read-only package validation. It does not launch Studio, build binaries, or infer a GPU/performance verdict.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$bundle = 'artifacts/gate-2/terrain-materials-2026-09-04'
$manifestPath = Join-Path $repo 'docs/evidence/gate2/TERRAIN_MATERIAL_PAINTING_ARTIFACTS.json'

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Hash([string]$RelativePath) {
    (Get-FileHash -LiteralPath (Join-Path $repo $RelativePath) -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Log([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $repo "$bundle/$Name")
}

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
Require ($manifest.schema -ceq 'forge.gate2.terrain-material-painting-evidence') 'Wrong evidence schema'
Require ($manifest.schema_version -eq 1 -and $manifest.route -ceq 'R2-012') 'Wrong evidence version/route'
Require ($manifest.source_commit -ceq '1b70be6d2927b06a693e169cfc00d345be61bf70') 'Wrong source candidate'
Require ($manifest.source_tree -ceq 'c021d2a9411f814626505de6fd8b05fa1be3688d') 'Wrong source tree'
Require ($manifest.base_commit -ceq 'cf30e12b54a860813e67c09440b2118996b446b2') 'Wrong source parent'
Require ($manifest.status -ceq 'PASS') 'R2-012 evidence status is not PASS'
Require ($manifest.automated_validation.status -ceq 'PASS') 'Automated candidate status is not PASS'
Require ($manifest.native_qualification.status -ceq 'PASS' -and $manifest.native_qualification.performed) 'Native qualification status is not PASS'
Require ($manifest.native_qualification.executable.source_commit -ceq $manifest.source_commit) 'Native executable is not bound to the candidate commit'
Require ($manifest.native_qualification.executable.kind -ceq 'standalone Tauri Release') 'Native executable is not the standalone Tauri package'
Require ($manifest.native_qualification.executable.path -ceq "$bundle/bin/forge-studio-1b70be6.exe") 'Unexpected native executable path'
Require ($manifest.native_qualification.project_id -ceq '98271e45-19a8-562c-a19e-4100f270da33') 'Unexpected native qualification project'
Require ($manifest.native_qualification.fresh_project.revision -eq 1 -and $manifest.native_qualification.fresh_project.chunks_recomputed -eq 64 -and $manifest.native_qualification.fresh_project.chunks_preserved -eq 0) 'Fresh-project native counters mismatch'
Require ($manifest.native_qualification.paint_earth.tool -ceq 'Peindre - Terre' -and $manifest.native_qualification.paint_earth.stroke_points -eq 1409 -and $manifest.native_qualification.paint_earth.revision -eq 2) 'Native earth-paint receipt mismatch'
Require ($manifest.native_qualification.paint_earth.chunks_recomputed -eq 8 -and $manifest.native_qualification.paint_earth.chunks_preserved -eq 56 -and $manifest.native_qualification.paint_earth.earth_band_visible) 'Native earth-paint result mismatch'
Require ($manifest.native_qualification.undo.revision -eq 3 -and $manifest.native_qualification.undo.chunks_recomputed -eq 8 -and $manifest.native_qualification.undo.chunks_preserved -eq 56 -and -not $manifest.native_qualification.undo.earth_band_visible) 'Native undo result mismatch'
Require ($manifest.native_qualification.redo.revision -eq 4 -and $manifest.native_qualification.redo.chunks_recomputed -eq 8 -and $manifest.native_qualification.redo.chunks_preserved -eq 56 -and $manifest.native_qualification.redo.earth_band_visible) 'Native redo result mismatch'
$reopen = $manifest.native_qualification.save_close_reopen
Require ($reopen.closed_cleanly -and $reopen.reopened_revision -eq 4 -and $reopen.status_message -ceq 'Projet rouvert depuis le snapshot vérifié') 'Native close/reopen receipt mismatch'
Require ($reopen.earth_band_visible -and $reopen.chunks_recomputed_on_load -eq 64 -and $reopen.chunks_preserved_on_load -eq 0 -and $reopen.exact_r4_snapshot) 'Native reopened state mismatch'
Require ($manifest.native_qualification.visual_capture.status -ceq 'OBSERVED_NOT_VERSIONED' -and $manifest.native_qualification.visual_capture.method -ceq 'CUA') 'Visual observation provenance mismatch'
Require ($manifest.remote_ci.status -ceq 'NOT RUN — infrastructure billing') 'Remote CI status is not the ADR-0006 exception'
Require ($manifest.presentmon.status -ceq 'NOT RUN') 'PresentMon must remain NOT RUN for this route'

$commit = git -C $repo rev-parse "$($manifest.source_commit)^{commit}"
Require ($LASTEXITCODE -eq 0 -and $commit -ceq $manifest.source_commit) 'Source commit is missing or ambiguous'
$tree = git -C $repo rev-parse "$($manifest.source_commit)^{tree}"
Require ($LASTEXITCODE -eq 0 -and $tree -ceq $manifest.source_tree) 'Source tree mismatch'
$parent = git -C $repo rev-parse "$($manifest.source_commit)^"
Require ($LASTEXITCODE -eq 0 -and $parent -ceq $manifest.base_commit) 'Source parent mismatch'

$changed = @(git -C $repo diff-tree --no-commit-id --name-only -r $manifest.source_commit)
Require ($LASTEXITCODE -eq 0 -and $changed.Count -eq 20) 'Unexpected candidate file inventory'
Require (@($changed | Where-Object { $_ -match '(^|/)(assets|upstream/cadle)/' }).Count -eq 0) 'Candidate unexpectedly changes an asset or Cadle path'
Require (@($changed | Where-Object { $_ -match '(^|/)Cargo\.(toml|lock)$' }).Count -eq 0) 'Candidate unexpectedly changes dependencies'

Require ($manifest.artifacts.Count -eq 8) 'Unexpected artifact inventory'
$paths = @($manifest.artifacts | ForEach-Object path)
Require (($paths | Sort-Object -Unique).Count -eq $paths.Count) 'Duplicate artifact path'
Require (@($paths | Where-Object { $_ -ceq $manifest.native_qualification.executable.path }).Count -eq 1) 'Qualified native executable is not fixed exactly once in the artifact inventory'
Require (@($paths | Where-Object { $_ -ceq $manifest.native_qualification.snapshots.r4 }).Count -eq 1) 'Qualified revision-4 snapshot is not fixed exactly once in the artifact inventory'
foreach ($artifact in $manifest.artifacts) {
    $path = Join-Path $repo $artifact.path
    Require (Test-Path -LiteralPath $path -PathType Leaf) "Missing artifact: $($artifact.path)"
    Require ((Get-Item -LiteralPath $path).Length -eq $artifact.bytes) "Size mismatch: $($artifact.path)"
    Require ((Hash $artifact.path) -ceq $artifact.sha256) "Hash mismatch: $($artifact.path)"
}
Require (@($paths | Where-Object { $_ -ceq "$bundle/native-release.log" }).Count -eq 0) 'Rejected Cargo devUrl build log must not be qualified as standalone evidence'

$testLog = Log 'workspace-tests.log'
$results = [regex]::Matches($testLog, 'test result: ok\. (\d+) passed; (\d+) failed; (\d+) ignored;')
Require ($results.Count -eq $manifest.automated_validation.workspace_test_targets) 'Unexpected workspace test-target count'
$passed = 0
foreach ($result in $results) {
    $passed += [int]$result.Groups[1].Value
    Require ($result.Groups[2].Value -eq '0' -and $result.Groups[3].Value -eq '0') 'Failed or ignored workspace tests'
}
Require ($passed -eq 481 -and $passed -eq $manifest.automated_validation.workspace_tests_passed) 'Expected 481 workspace tests'
Require ($manifest.automated_validation.workspace_tests_failed -eq 0 -and $manifest.automated_validation.workspace_tests_ignored -eq 0) 'Manifest claims failed or ignored tests'

$requiredTests = @(
    'versioned_material_weights_are_sparse_canonical_normalized_and_bounded',
    'set_terrain_material_weights_is_pure_revisioned_and_reversible',
    'material_brush_persists_normalized_weights_with_falloff_and_inverse',
    'material_brush_is_typed_bounded_and_repeated_paint_converges',
    'material_stroke_is_one_revision_and_partial_refresh_never_rebuilds_colliders',
    'removing_material_overrides_restores_the_exact_procedural_projection',
    'skipped_revision_refresh_combines_height_and_material_changes_exactly',
    'material_only_patch_updates_one_mesh_and_preserves_every_collider',
    'material_only_patch_on_chunk_seam_updates_four_meshes_without_touching_rapier',
    'material_brush_dry_run_apply_history_and_reopen_preserve_exact_sources',
    'material_brush_ipc_round_trip_invalid_and_stale_requests_do_not_touch_history'
)
foreach ($test in $requiredTests) {
    Require ($testLog -match "test [^\r\n]*$([regex]::Escape($test)) \.\.\. ok") "Required test result missing: $test"
}

foreach ($name in @('clippy-native.log', 'clippy-wasm.log', 'trunk-release.log', 'tauri-release.log')) {
    $text = Log $name
    Require ($text -match 'Finished' -and $text -notmatch '(?m)^error(?:\[|:)') "Incomplete or failed final log: $name"
}
Require ((Log 'clippy-native.log') -match 'Checking forge_studio') 'Native Clippy did not include Forge Studio'
Require ((Log 'clippy-wasm.log') -match 'Checking forge_engine' -and (Log 'clippy-wasm.log') -match 'Checking forge_studio') 'WASM Clippy did not include engine and Studio'
Require ((Log 'trunk-release.log') -match 'Finished `release` profile') 'Trunk Release completion missing'
Require ((Log 'tauri-release.log') -match 'Built application at:') 'Tauri Release application receipt missing'

$wasmPath = Join-Path $repo "$bundle/bin/forge_studio-1b70be6.wasm"
$wasm = [IO.File]::ReadAllBytes($wasmPath)
Require ($wasm.Length -ge 8 -and $wasm[0] -eq 0 -and $wasm[1] -eq 97 -and $wasm[2] -eq 115 -and $wasm[3] -eq 109) 'Invalid WebAssembly header'
$wasmText = [Text.Encoding]::ASCII.GetString($wasm)
Require ($wasmText.Contains($manifest.source_commit)) 'WASM binary does not embed the complete candidate commit'

$exePath = Join-Path $repo "$bundle/bin/forge-studio-1b70be6.exe"
$exe = [IO.File]::ReadAllBytes($exePath)
Require ($exe.Length -ge 256 -and $exe[0] -eq 77 -and $exe[1] -eq 90) 'Invalid Windows MZ header'
$peOffset = [BitConverter]::ToInt32($exe, 0x3c)
Require ($peOffset -ge 0x40 -and $peOffset + 4 -le $exe.Length) 'Invalid Windows PE offset'
Require ($exe[$peOffset] -eq 80 -and $exe[$peOffset + 1] -eq 69 -and $exe[$peOffset + 2] -eq 0 -and $exe[$peOffset + 3] -eq 0) 'Invalid Windows PE signature'

$r2Path = Join-Path $repo $manifest.native_qualification.snapshots.r2
$r4Path = Join-Path $repo $manifest.native_qualification.snapshots.r4
Require ($manifest.native_qualification.snapshots.non_overwritten) 'Native snapshot sequence is not marked non-overwritten'
Require (Test-Path -LiteralPath $r2Path -PathType Leaf) 'Native revision-2 snapshot is missing'
Require (Test-Path -LiteralPath $r4Path -PathType Leaf) 'Native revision-4 snapshot is missing'
$r2 = Get-Content -Raw -LiteralPath $r2Path | ConvertFrom-Json -Depth 100
$r4 = Get-Content -Raw -LiteralPath $r4Path | ConvertFrom-Json -Depth 100
Require ($r2.schema -ceq 'forge.workspace' -and $r2.schema_version -eq 2 -and $r2.revision -eq 2) 'Invalid native revision-2 workspace snapshot'
Require ($r4.schema -ceq 'forge.workspace' -and $r4.schema_version -eq 2 -and $r4.revision -eq 4) 'Invalid native revision-4 workspace snapshot'
Require ($r2.project.modules.Count -eq 1 -and $r2.project.modules[0].areas.Count -eq 1) 'Unexpected revision-2 project shape'
Require ($r4.project.modules.Count -eq 1 -and $r4.project.modules[0].areas.Count -eq 1) 'Unexpected revision-4 project shape'
Require ($r2.project.modules[0].areas[0].terrain.revision -eq 2 -and $r4.project.modules[0].areas[0].terrain.revision -eq 4) 'Native terrain revisions do not match the workspace revisions'
$r2.revision = 0
$r4.revision = 0
$r2.project.modules[0].areas[0].terrain.revision = 0
$r4.project.modules[0].areas[0].terrain.revision = 0
Require (($r2 | ConvertTo-Json -Depth 100 -Compress) -ceq ($r4 | ConvertTo-Json -Depth 100 -Compress)) 'Redo revision-4 snapshot does not exactly restore revision 2 outside expected revision fields'
$terrain = $r4.project.modules[0].areas[0].terrain
Require ($r4.project.id -ceq $manifest.native_qualification.project_id -and $terrain.project_id -ceq $manifest.native_qualification.project_id) 'Native revision-4 snapshot project identity mismatch'
Require ($terrain.material_weight_overrides.Count -eq 1409) 'Native revision-4 snapshot does not contain the 1409 painted material points'

Write-Output 'PASS: R2-012 automated package is bound to commit 1b70be6d and tree c021d2a; 8 artifact hashes and 481 tests verified.'
Write-Output 'PASS: standalone native Studio paint/undo/redo/save/reopen qualification and exact normalized r2/r4 restoration verified.'
Write-Output 'PASS: WASM/PE formats are valid; no dependency, asset or Cadle path changed; rejected Cargo devUrl build is excluded.'
Write-Output 'NOT RUN: remote CI — infrastructure billing. PresentMon was not launched and no GPU/performance verdict is claimed.'
