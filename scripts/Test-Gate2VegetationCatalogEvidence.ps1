#requires -Version 7.5
[CmdletBinding()]
param()

# Read-only package validation. It does not run tests, build or launch Studio, or infer a visual,
# GPU, cadence or performance verdict.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$bundle = 'artifacts/gate-2/vegetation-catalog-exclusions-2026-09-04'
$manifestPath = Join-Path $repo 'docs/evidence/gate2/VEGETATION_CATALOG_AND_EXCLUSIONS_ARTIFACTS.json'

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Hash([string]$RelativePath) {
    (Get-FileHash -LiteralPath (Join-Path $repo $RelativePath) -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Log([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $repo "$bundle/$Name")
}

function GitText([string]$Commit, [string]$Path) {
    $object = '{0}:{1}' -f $Commit, $Path
    $lines = @(& git -C $repo show $object)
    Require ($LASTEXITCODE -eq 0) "Cannot read Git object: $object"
    $lines -join "`n"
}

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
Require ($manifest.schema -ceq 'forge.gate2.vegetation-catalog-exclusions-evidence') 'Wrong evidence schema'
Require ($manifest.schema_version -eq 1) 'Wrong evidence version'
Require ($manifest.source_commit -ceq '394de87f13f1561781b19159369e01d95a246255') 'Wrong source candidate'
Require ($manifest.source_tree -ceq 'a818ff6aa553631a431a06d34cb4ba702f5a6004') 'Wrong source tree'
Require ($manifest.base_commit -ceq '92cf726022d7d9e692a3398f6c7077966b903fcc') 'Wrong source parent'
Require ($manifest.routes.Count -eq 2) 'Unexpected route count'
Require ($manifest.routes[0].id -ceq 'R2-016' -and $manifest.routes[0].status -ceq 'PASS') 'R2-016 is not PASS'
Require ($manifest.routes[1].id -ceq 'R2-017' -and $manifest.routes[1].status -ceq 'PASS') 'R2-017 is not PASS'
Require (($manifest.open_routes -join ',') -ceq 'R2-019,R2-020') 'Wrong remaining R2-C boundary'
Require ($manifest.native_ui_qualification.status -ceq 'NOT RUN') 'Native UI must remain explicitly not run'
Require ($manifest.remote_ci.status -ceq 'NOT RUN — infrastructure billing') 'Remote CI status is not the ADR-0006 exception'
Require ($manifest.remote_ci.decision -ceq 'ADR-0006') 'Wrong remote CI decision'
Require ($manifest.presentmon.status -ceq 'NOT RUN') 'PresentMon must remain NOT RUN'

$commit = (& git -C $repo rev-parse "$($manifest.source_commit)^{commit}").Trim()
Require ($LASTEXITCODE -eq 0 -and $commit -ceq $manifest.source_commit) 'Source commit is missing or ambiguous'
$tree = (& git -C $repo rev-parse "$($manifest.source_commit)^{tree}").Trim()
Require ($LASTEXITCODE -eq 0 -and $tree -ceq $manifest.source_tree) 'Source tree mismatch'
$parent = (& git -C $repo rev-parse "$($manifest.source_commit)^").Trim()
Require ($LASTEXITCODE -eq 0 -and $parent -ceq $manifest.base_commit) 'Source parent mismatch'

$expectedChanged = @(
    'apps/forge_studio/desktop.css',
    'apps/forge_studio/src/area_preview_cache_tests.rs',
    'apps/forge_studio/src/brush_ui.rs',
    'apps/forge_studio/src/vegetation_ui.rs',
    'apps/forge_studio/src/workspace_api.rs',
    'apps/forge_studio/src/workspace_service.rs',
    'apps/forge_studio/src/workspace_service_tests.rs',
    'apps/forge_studio/src/world_ui.rs',
    'crates/forge_project/src/lib.rs',
    'crates/forge_project/src/vegetation_catalog.rs',
    'crates/forge_project/src/workspace.rs',
    'crates/forge_project/src/workspace_edit.rs',
    'crates/forge_project/src/workspace_edit_tests.rs',
    'crates/forge_world/src/vegetation.rs',
    'crates/forge_world/src/vegetation_cache.rs'
)
$changed = @(& git -C $repo diff-tree --no-commit-id --name-only -r $manifest.source_commit)
Require ($LASTEXITCODE -eq 0) 'Cannot enumerate candidate changes'
Require ($changed.Count -eq 15 -and $changed.Count -eq $manifest.source_change_boundary.changed_files) 'Unexpected candidate file count'
Require (@(Compare-Object -CaseSensitive $expectedChanged $changed).Count -eq 0) 'Unexpected candidate file inventory'
Require (@($changed | Where-Object { $_ -match '(^|/)(assets|upstream/cadle)/' }).Count -eq 0) 'Candidate unexpectedly changes an asset or Cadle path'
Require (@($changed | Where-Object { $_ -match '(^|/)Cargo\.(toml|lock)$' }).Count -eq 0) 'Candidate unexpectedly changes dependencies'

$diff = @(& git -C $repo diff $manifest.base_commit $manifest.source_commit -- $expectedChanged) -join "`n"
Require ($LASTEXITCODE -eq 0 -and $diff -notmatch '(?i)cadle') 'Candidate diff unexpectedly contains Cadle content'
Require ($manifest.source_change_boundary.dependencies_changed -eq 0 -and $manifest.source_change_boundary.assets_changed -eq 0 -and $manifest.source_change_boundary.cadle_paths_or_content_changed -eq 0) 'Manifest expands the source boundary'

$baseWorkspace = GitText $manifest.base_commit 'crates/forge_project/src/workspace.rs'
$sourceWorkspace = GitText $manifest.source_commit 'crates/forge_project/src/workspace.rs'
$baseVegetation = GitText $manifest.base_commit 'crates/forge_project/src/vegetation.rs'
$sourceVegetation = GitText $manifest.source_commit 'crates/forge_project/src/vegetation.rs'
$workspacePattern = '(?m)^pub const WORKSPACE_SCHEMA_VERSION: u32 = 3;$'
$vegetationPattern = '(?m)^pub const VEGETATION_DOCUMENT_SCHEMA_VERSION: u32 = 1;$'
Require ($baseWorkspace -match $workspacePattern -and $sourceWorkspace -match $workspacePattern) 'Workspace schema changed'
Require ($baseVegetation -match $vegetationPattern -and $sourceVegetation -match $vegetationPattern) 'Vegetation schema changed'
Require (-not $manifest.functional_contract.schema_bump) 'Manifest claims a schema bump'

$catalog = GitText $manifest.source_commit 'crates/forge_project/src/vegetation_catalog.rs'
foreach ($preset in @('Tree', 'Shrub', 'Fern', 'Grass')) {
    Require ($catalog -match "Self::$preset => VegetationLayer::new") "Missing procedural preset: $preset"
}
$service = GitText $manifest.source_commit 'apps/forge_studio/src/workspace_service.rs'
foreach ($command in @('AddVegetationSpecies', 'RenameVegetationSpecies', 'RemoveVegetationSpecies', 'AddVegetationExclusion', 'RemoveVegetationExclusion')) {
    Require ($service -match $command) "Missing Studio command: $command"
}
$brush = GitText $manifest.source_commit 'apps/forge_studio/src/brush_ui.rs'
Require ($brush -match 'ExclusionBrush::Circle' -and $brush -match 'ExclusionBrush::Corridor') 'Circle or corridor brush missing'

Require ($manifest.artifacts.Count -eq 5) 'Unexpected artifact inventory'
$paths = @($manifest.artifacts | ForEach-Object path)
Require (($paths | Sort-Object -Unique).Count -eq $paths.Count) 'Duplicate artifact path'
foreach ($artifact in $manifest.artifacts) {
    $path = Join-Path $repo $artifact.path
    Require (Test-Path -LiteralPath $path -PathType Leaf) "Missing artifact: $($artifact.path)"
    Require ((Get-Item -LiteralPath $path).Length -eq $artifact.bytes) "Size mismatch: $($artifact.path)"
    Require ((Hash $artifact.path) -ceq $artifact.sha256) "Hash mismatch: $($artifact.path)"
}

$testLog = Log 'cargo-test-workspace-excluding-unchanged-runtime.log'
$results = [regex]::Matches($testLog, 'test result: ok\. (\d+) passed; (\d+) failed; (\d+) ignored;')
Require ($results.Count -eq 10 -and $results.Count -eq $manifest.automated_validation.expanded_workspace_test_targets) 'Unexpected expanded test-target count'
$passed = 0
$counts = @()
foreach ($result in $results) {
    $count = [int]$result.Groups[1].Value
    $passed += $count
    $counts += $count
    Require ($result.Groups[2].Value -eq '0' -and $result.Groups[3].Value -eq '0') 'Failed or ignored expanded workspace tests'
}
Require (($counts -join ',') -ceq '3,0,0,3,12,173,94,86,5,85') 'Unexpected per-target test counts'
Require ($passed -eq 461 -and $passed -eq $manifest.automated_validation.expanded_workspace_tests_passed) 'Expected 461 expanded workspace tests'
Require ($manifest.automated_validation.expanded_workspace_tests_failed -eq 0 -and $manifest.automated_validation.expanded_workspace_tests_ignored -eq 0) 'Manifest claims failed or ignored expanded tests'
Require ($manifest.automated_validation.targeted_suite_counts.forge_project -eq 94 -and $manifest.automated_validation.targeted_suite_counts.forge_studio_lib -eq 86 -and $manifest.automated_validation.targeted_suite_counts.forge_world -eq 85) 'Wrong targeted suite counts'
Require (($manifest.automated_validation.targeted_suite_counts.forge_project + $manifest.automated_validation.targeted_suite_counts.forge_studio_lib + $manifest.automated_validation.targeted_suite_counts.forge_world) -eq 265) 'Expected 265 targeted tests'

$requiredTests = @(
    'tree_preset_reproduces_the_forge_tree_layer',
    'shrub_preset_reproduces_the_forge_shrub_layer',
    'fern_preset_reproduces_the_forge_fern_layer',
    'grass_preset_reproduces_the_forge_grass_layer',
    'set_vegetation_catalogue_is_atomic_and_retains_density_by_stable_id',
    'catalogue_parameter_edit_preserves_the_density_document_exactly',
    'catalogue_rejects_stale_noop_and_invalid_replacements_without_mutation',
    'set_vegetation_exclusions_is_pure_revisioned_reopenable_and_replayable',
    'set_vegetation_exclusions_rejects_stale_hash_and_noop_without_mutation',
    'set_vegetation_exclusions_enforces_geometry_identity_and_collection_bound',
    'vegetation_catalog_crud_reuses_free_ids_and_survives_history_and_reopen',
    'vegetation_exclusion_circle_and_corridor_are_crud_and_reuse_free_ids',
    'invalid_stale_or_wrong_area_vegetation_edits_are_atomic_and_target_one_area',
    'authored_exclusion_shapes_remove_only_contained_instances_and_replay_exactly',
    'catalogue_rename_changes_the_recipe_hash_but_retains_every_chunk_handle',
    'new_zero_density_species_synchronises_without_changing_rendered_chunks',
    'local_circle_exclusion_replaces_only_affected_chunks_and_matches_full_generation',
    'catalogue_rename_and_zero_density_addition_retain_the_complete_preview_cache',
    'local_circle_exclusion_updates_only_the_touched_preview_chunks'
)
foreach ($test in $requiredTests) {
    Require ($testLog -match "test [^\r\n]*$([regex]::Escape($test)) \.\.\. ok") "Required test result missing: $test"
}

Require ($manifest.automated_validation.complete_workspace_status -ceq 'NOT COMPLETED' -and $manifest.automated_validation.complete_workspace_attempts -eq 2) 'Full workspace attempts are misclassified'
$attempt1 = Log 'cargo-test-workspace-attempt-1-linker-failure.log'
$attempt2 = Log 'cargo-test-workspace-attempt-2-linker-failure.log'
Require ($attempt1 -match 'LNK1318:.*LIMIT \(12\).*forge_runtime\.pdb' -and $attempt1 -match 'could not compile `forge_runtime`') 'First linker failure receipt mismatch'
Require ($attempt2 -match 'LNK1318:.*FILE_SYSTEM \(3\).*forge_runtime\.pdb' -and $attempt2 -match 'could not compile `forge_runtime`') 'Second linker failure receipt mismatch'

$nativeClippy = Log 'cargo-clippy-relevant-native.log'
$wasmClippy = Log 'cargo-clippy-studio-wasm.log'
Require ($nativeClippy -match 'Finished `dev` profile' -and $nativeClippy -notmatch '(?m)^error(?:\[|:)') 'Native relevant Clippy did not finish cleanly'
Require ($wasmClippy -match 'Finished `dev` profile' -and $wasmClippy -notmatch '(?m)^error(?:\[|:)') 'Studio WASM Clippy did not finish cleanly'
Require ($wasmClippy -match 'proc-macro-error2 v2\.0\.1') 'Expected transitive WASM advisory is missing'

Write-Output 'PASS: R2-016 and R2-017 evidence is bound to commit 394de87f and tree a818ff6a.'
Write-Output 'PASS: 5 artifact hashes; 265 targeted tests represented; 461 expanded tests; strict relevant native/WASM Clippy.'
Write-Output 'NOT COMPLETED: full workspace tests stopped twice at forge_runtime PDB linkage (LNK1318); no 519-test claim.'
Write-Output 'NOT RUN: native UI, remote CI (infrastructure billing), PresentMon and GPU/performance qualification.'
