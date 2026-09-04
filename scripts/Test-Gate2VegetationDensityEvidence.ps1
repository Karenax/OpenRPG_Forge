#requires -Version 7.5
[CmdletBinding()]
param()

# Read-only package validation. It does not launch Studio, build binaries, or infer a visual,
# GPU, cadence or performance verdict.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$bundle = 'artifacts/gate-2/vegetation-density-seed-2026-09-04'
$manifestPath = Join-Path $repo 'docs/evidence/gate2/VEGETATION_DENSITY_AND_STABLE_SEED_ARTIFACTS.json'

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
Require ($manifest.schema -ceq 'forge.gate2.vegetation-density-stable-seed-evidence') 'Wrong evidence schema'
Require ($manifest.schema_version -eq 1) 'Wrong evidence version'
Require ($manifest.source_commit -ceq 'd8dd10c4f9ef2c2e270955ff23fc08a8d90ff24e') 'Wrong source candidate'
Require ($manifest.source_tree -ceq 'a12c29477efb9ab775ee7d709a7469da159fcf7a') 'Wrong source tree'
Require ($manifest.base_commit -ceq '4a69a43f9641bbf89007b56266c39a36f56f8fc7') 'Wrong source parent'
Require ($manifest.routes.Count -eq 2) 'Unexpected route count'
Require ($manifest.routes[0].id -ceq 'R2-015' -and $manifest.routes[0].status -ceq 'PASS') 'R2-015 is not PASS'
Require ($manifest.routes[1].id -ceq 'R2-018' -and $manifest.routes[1].status -ceq 'PASS') 'R2-018 is not PASS'
Require ($manifest.native_ui_qualification.status -ceq 'NOT RUN') 'Native UI must remain explicitly deferred'
Require ($manifest.native_launch.status -ceq 'PASS' -and $manifest.native_launch.process_started -and $manifest.native_launch.process_responding -and $manifest.native_launch.process_stopped_after_check) 'Native launch receipt mismatch'
Require ($manifest.remote_ci.status -ceq 'NOT RUN — infrastructure billing') 'Remote CI status is not the ADR-0006 exception'
Require ($manifest.presentmon.status -ceq 'NOT RUN') 'PresentMon must remain NOT RUN'
Require (($manifest.open_routes -join ',') -ceq 'R2-016,R2-017,R2-019,R2-020') 'Wrong remaining vegetation route boundary'

$commit = git -C $repo rev-parse "$($manifest.source_commit)^{commit}"
Require ($LASTEXITCODE -eq 0 -and $commit -ceq $manifest.source_commit) 'Source commit is missing or ambiguous'
$tree = git -C $repo rev-parse "$($manifest.source_commit)^{tree}"
Require ($LASTEXITCODE -eq 0 -and $tree -ceq $manifest.source_tree) 'Source tree mismatch'
$parent = git -C $repo rev-parse "$($manifest.source_commit)^"
Require ($LASTEXITCODE -eq 0 -and $parent -ceq $manifest.base_commit) 'Source parent mismatch'

$changed = @(git -C $repo diff-tree --no-commit-id --name-only -r $manifest.source_commit)
Require ($LASTEXITCODE -eq 0 -and $changed.Count -eq 23) 'Unexpected candidate file inventory'
Require (@($changed | Where-Object { $_ -match '(^|/)(assets|upstream/cadle)/' }).Count -eq 0) 'Candidate unexpectedly changes an asset or Cadle path'
Require (@($changed | Where-Object { $_ -match '(^|/)Cargo\.(toml|lock)$' }).Count -eq 0) 'Candidate unexpectedly changes dependencies'
foreach ($requiredSource in @(
    'crates/forge_project/src/vegetation_density.rs',
    'crates/forge_world/src/vegetation_density_brush.rs',
    'crates/forge_world/src/vegetation_cache.rs',
    'apps/forge_studio/src/brush_ui.rs',
    'crates/forge_engine/src/editor_viewport.rs'
)) {
    Require ($changed -ccontains $requiredSource) "Required source change missing: $requiredSource"
}

Require ($manifest.artifacts.Count -eq 8) 'Unexpected artifact inventory'
$paths = @($manifest.artifacts | ForEach-Object path)
Require (($paths | Sort-Object -Unique).Count -eq $paths.Count) 'Duplicate artifact path'
Require ($paths -ccontains $manifest.native_launch.executable) 'Qualified native executable is missing from the inventory'
foreach ($artifact in $manifest.artifacts) {
    $path = Join-Path $repo $artifact.path
    Require (Test-Path -LiteralPath $path -PathType Leaf) "Missing artifact: $($artifact.path)"
    Require ((Get-Item -LiteralPath $path).Length -eq $artifact.bytes) "Size mismatch: $($artifact.path)"
    Require ((Hash $artifact.path) -ceq $artifact.sha256) "Hash mismatch: $($artifact.path)"
}

$testLog = Log 'workspace-tests.log'
$results = [regex]::Matches($testLog, 'test result: ok\. (\d+) passed; (\d+) failed; (\d+) ignored;')
Require ($results.Count -eq 13 -and $results.Count -eq $manifest.automated_validation.workspace_test_targets) 'Unexpected workspace test-target count'
$passed = 0
foreach ($result in $results) {
    $passed += [int]$result.Groups[1].Value
    Require ($result.Groups[2].Value -eq '0' -and $result.Groups[3].Value -eq '0') 'Failed or ignored workspace tests'
}
Require ($passed -eq 519 -and $passed -eq $manifest.automated_validation.workspace_tests_passed) 'Expected 519 workspace tests'
Require ($manifest.automated_validation.workspace_tests_failed -eq 0 -and $manifest.automated_validation.workspace_tests_ignored -eq 0) 'Manifest claims failed or ignored tests'

$requiredTests = @(
    'standalone_json_budget_rejects_an_excessive_sparse_mask',
    'legacy_migration_binds_the_default_recipe_to_the_preserved_grass_material_id',
    'set_vegetation_density_is_pure_revisioned_reopenable_and_replayable_after_undo',
    'density_stamp_is_pure_revisioned_radial_and_canonical',
    'density_stroke_is_one_revision_and_bounded',
    'resolved_density_patch_is_bound_to_the_exact_source_hash',
    'density_brush_rejects_noop_invalid_wrong_project_and_missing_layer',
    'density_layers_must_match_the_recipe_exactly_and_in_order',
    'density_mask_replay_is_identical_and_another_seed_diverges',
    'local_density_selects_cells_and_preserves_survivor_order',
    'replay_is_bit_identical_and_seed_changes_ids_and_output',
    'authored_terrain_edit_preserves_surviving_attempt_ids',
    'local_mask_replaces_one_chunk_and_retains_the_other_sixty_three_handles',
    'replay_save_equivalent_undo_and_redo_are_exact',
    'vegetation_density_dry_run_matches_the_canonical_apply',
    'vegetation_density_history_and_reopen_preserve_exact_sources',
    'vegetation_density_ipc_invalid_noop_and_stale_requests_do_not_touch_history',
    'local_density_paint_changes_only_its_vegetation_chunk_and_keeps_terrain_partial',
    'workspace_adapter_reports_the_exact_density_source_hash',
    'editor_vegetation_is_chunk_batched_and_published_visible',
    'unchanged_editor_vegetation_reuses_every_chunk_without_upload'
)
foreach ($test in $requiredTests) {
    Require ($testLog -match "test [^\r\n]*$([regex]::Escape($test)) \.\.\. ok") "Required test result missing: $test"
}

foreach ($name in @('clippy-native.log', 'clippy-wasm.log', 'trunk-release.log', 'tauri-release.log')) {
    $text = Log $name
    Require ($text -match 'Finished' -and $text -notmatch '(?m)^error(?:\[|:)') "Incomplete or failed final log: $name"
}
Require ((Log 'clippy-native.log') -match 'Checking forge_studio' -and (Log 'clippy-native.log') -match 'Checking forge_runtime') 'Native Clippy did not cover the workspace applications'
Require ((Log 'clippy-wasm.log') -match 'Checking forge_engine' -and (Log 'clippy-wasm.log') -match 'forge_studio') 'WASM Clippy did not include engine and Studio'
Require ((Log 'trunk-release.log') -match 'Finished `release` profile') 'Trunk Release completion missing'
Require ((Log 'tauri-release.log') -match 'Built application at:') 'Tauri Release application receipt missing'

$launch = Get-Content -Raw -LiteralPath (Join-Path $repo "$bundle/native-launch.json") | ConvertFrom-Json
Require ($launch.schema -ceq 'forge.gate2.native-launch-observation' -and $launch.schema_version -eq 1) 'Wrong native launch receipt schema'
Require ($launch.source_commit -ceq $manifest.source_commit -and $launch.executable -ceq $manifest.native_launch.executable) 'Native launch receipt source mismatch'
Require ($launch.process_started -and $launch.process_responding -and $launch.process_stopped_after_check) 'Native process lifecycle was not completed'
Require ($launch.ui_interaction.status -ceq 'NOT RUN') 'Native launch receipt invents UI interaction'

$wasmPath = Join-Path $repo "$bundle/bin/forge_studio-d8dd10c.wasm"
$wasm = [IO.File]::ReadAllBytes($wasmPath)
Require ($wasm.Length -ge 8 -and $wasm[0] -eq 0 -and $wasm[1] -eq 97 -and $wasm[2] -eq 115 -and $wasm[3] -eq 109) 'Invalid WebAssembly header'
$wasmText = [Text.Encoding]::ASCII.GetString($wasm)
Require ($wasmText.Contains($manifest.source_commit)) 'WASM binary does not embed the complete candidate commit'

$exePath = Join-Path $repo "$bundle/bin/forge-studio-d8dd10c.exe"
$exe = [IO.File]::ReadAllBytes($exePath)
Require ($exe.Length -ge 256 -and $exe[0] -eq 77 -and $exe[1] -eq 90) 'Invalid Windows MZ header'
$peOffset = [BitConverter]::ToInt32($exe, 0x3c)
Require ($peOffset -ge 0x40 -and $peOffset + 4 -le $exe.Length) 'Invalid Windows PE offset'
Require ($exe[$peOffset] -eq 80 -and $exe[$peOffset + 1] -eq 69 -and $exe[$peOffset + 2] -eq 0 -and $exe[$peOffset + 3] -eq 0) 'Invalid Windows PE signature'

Write-Output 'PASS: R2-015 and R2-018 automated package is bound to commit d8dd10c4 and tree a12c2947.'
Write-Output 'PASS: 8 artifact hashes, 519 tests, strict native/WASM Clippy and Release WASM/PE builds verified.'
Write-Output 'PASS: no dependency, asset or Cadle path changed; local density changes 1 chunk and retains 63.'
Write-Output 'NOT RUN: native UI interaction, remote CI (infrastructure billing), PresentMon and GPU/performance qualification.'
