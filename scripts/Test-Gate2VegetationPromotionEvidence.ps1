#requires -Version 7.5
[CmdletBinding()]
param()

# Read-only package validation. It never launches Studio and cannot infer a visual, GPU, latency
# or performance verdict from functional and compilation receipts.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$bundle = 'artifacts/gate-2/vegetation-promotion-retouch-2026-09-05'
$manifestPath = Join-Path $repo 'docs/evidence/gate2/VEGETATION_PROMOTION_AND_RETOUCH_ARTIFACTS.json'

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
Require ($manifest.schema -ceq 'forge.gate2.vegetation-promotion-retouch-evidence') 'Wrong evidence schema'
Require ($manifest.schema_version -eq 1) 'Wrong evidence version'
Require ($manifest.source_commit -ceq '0e365796ca7782ee16b441bfd553c980a35fb060') 'Wrong source candidate'
Require ($manifest.source_tree -ceq 'd30e72ab9859c1c37fab0f3c2234f04f08636dc6') 'Wrong source tree'
Require ($manifest.base_commit -ceq '725057a2c081beed2b5c8150a2f467485475aeb9') 'Wrong source parent'
Require ($manifest.routes.Count -eq 1 -and $manifest.routes[0].id -ceq 'R2-020' -and $manifest.routes[0].status -ceq 'PASS') 'R2-020 is not the sole PASS route'
Require ($manifest.next_route -ceq 'R2-021') 'Wrong next route'
Require ($manifest.native_ui_qualification.status -ceq 'NOT RUN') 'Native UI must remain explicitly not run'
Require ($manifest.performance_qualification.status -ceq 'NOT RUN') 'Performance must remain explicitly not run'
Require ($manifest.remote_ci.status -ceq 'NOT RUN — infrastructure billing' -and $manifest.remote_ci.decision -ceq 'ADR-0006') 'Wrong remote CI exception'
Require ($manifest.presentmon.status -ceq 'NOT RUN') 'PresentMon must remain NOT RUN'

$commit = (& git -C $repo rev-parse "$($manifest.source_commit)^{commit}").Trim()
Require ($LASTEXITCODE -eq 0 -and $commit -ceq $manifest.source_commit) 'Source commit is missing or ambiguous'
$tree = (& git -C $repo show -s --format=%T $manifest.source_commit).Trim()
Require ($LASTEXITCODE -eq 0 -and $tree -ceq $manifest.source_tree) 'Source tree mismatch'
$parent = (& git -C $repo show -s --format=%P $manifest.source_commit).Trim()
Require ($LASTEXITCODE -eq 0 -and $parent -ceq $manifest.base_commit) 'Source parent mismatch'

$expectedChanged = @(
    'apps/forge_studio/src/brush_ui.rs',
    'apps/forge_studio/src/vegetation_ui.rs',
    'apps/forge_studio/src/workspace_api.rs',
    'apps/forge_studio/src/workspace_service.rs',
    'apps/forge_studio/src/workspace_service_tests.rs',
    'crates/forge_project/src/lib.rs',
    'crates/forge_project/src/vegetation.rs',
    'crates/forge_project/src/workspace.rs',
    'crates/forge_project/src/workspace_edit.rs',
    'crates/forge_project/src/workspace_edit_tests.rs',
    'crates/forge_project/src/workspace_legacy.rs',
    'crates/forge_world/src/lib.rs',
    'crates/forge_world/src/vegetation.rs',
    'crates/forge_world/src/vegetation_cache.rs',
    'crates/forge_world/src/vegetation_promotion.rs'
)
$changed = @(& git -C $repo diff-tree --no-commit-id --name-only -r $manifest.source_commit)
Require ($LASTEXITCODE -eq 0) 'Cannot enumerate source changes'
Require ($changed.Count -eq 15 -and $changed.Count -eq $manifest.source_change_boundary.changed_files) 'Unexpected source file count'
Require (@(Compare-Object -CaseSensitive $expectedChanged $changed).Count -eq 0) 'Unexpected source file inventory'
Require (@($changed | Where-Object { $_ -match '(^|/)(assets|upstream/cadle)(/|$)' }).Count -eq 0) 'Source unexpectedly changes an asset or Cadle path'
Require (@($changed | Where-Object { $_ -match '(^|/)Cargo\.(toml|lock)$' }).Count -eq 0) 'Source unexpectedly changes Cargo metadata'
$diff = @(& git -C $repo diff $manifest.base_commit $manifest.source_commit -- $expectedChanged) -join "`n"
Require ($LASTEXITCODE -eq 0 -and $diff -notmatch '(?i)cadle') 'Source diff unexpectedly contains Cadle content'
Require ($manifest.source_change_boundary.dependencies_changed -eq 0 -and $manifest.source_change_boundary.assets_changed -eq 0 -and $manifest.source_change_boundary.cadle_paths_or_content_changed -eq 0) 'Manifest expands the source boundary'

$workspace = GitText $manifest.source_commit 'crates/forge_project/src/workspace.rs'
$vegetation = GitText $manifest.source_commit 'crates/forge_project/src/vegetation.rs'
$workspaceEdit = GitText $manifest.source_commit 'crates/forge_project/src/workspace_edit.rs'
$world = GitText $manifest.source_commit 'crates/forge_world/src/vegetation.rs'
$promotion = GitText $manifest.source_commit 'crates/forge_world/src/vegetation_promotion.rs'
$cache = GitText $manifest.source_commit 'crates/forge_world/src/vegetation_cache.rs'
$service = GitText $manifest.source_commit 'apps/forge_studio/src/workspace_service.rs'
$api = GitText $manifest.source_commit 'apps/forge_studio/src/workspace_api.rs'
$browser = GitText $manifest.source_commit 'apps/forge_studio/src/brush_ui.rs'
$vegetationUi = GitText $manifest.source_commit 'apps/forge_studio/src/vegetation_ui.rs'

Require ($workspace -match '(?m)^pub const WORKSPACE_SCHEMA_VERSION: u32 = 3;$') 'Workspace schema changed'
Require ($vegetation -match '(?m)^pub const VEGETATION_DOCUMENT_SCHEMA_VERSION_V1: u32 = 1;$') 'Vegetation v1 compatibility marker is missing'
Require ($vegetation -match '(?m)^pub const VEGETATION_DOCUMENT_SCHEMA_VERSION: u32 = 2;$') 'Vegetation schema v2 is missing'
Require ($vegetation -match '(?m)^pub const MAX_PROMOTED_VEGETATION_INSTANCES: usize = 1_024;$') 'Promotion collection bound mismatch'
Require ($vegetation -match 'pub promoted_instances: Vec<PromotedVegetationInstance>') 'Promoted source collection is missing'
Require ($vegetation -match 'pub fn new_v1\(' -and $vegetation -match 'pub fn migrate_to_latest\(') 'Explicit v1 compatibility or migration is missing'
Require ($workspaceEdit -match 'SetVegetationPromotedInstances') 'Atomic promoted-source edit is missing'
Require ($promotion -match 'pub struct VegetationPromotionTarget' -and $promotion -match 'resolve_nearest_generated_vegetation_instance') 'Deterministic promotion resolver is missing'
Require ($promotion -match 'already_promoted' -and $promotion -match 'preview_retouch_promoted_vegetation_instance') 'Overlap guard or retouch path is missing'
Require ($world -match 'ExpandedGrassBladeBudgetExceeded' -and $world -match 'materialized_promotions') 'Orphan materialization or effective grass budget is missing'
Require ($cache -match 'promotion_and_retouch_reuse_only_the_exact_derived_chunk_handles') 'Promotion cache proof is missing'
Require ($api -match 'PromoteVegetationInstance' -and $api -match 'RetouchPromotedVegetationInstance') 'Typed Studio commands are missing'
Require ($service -match 'first_free_species_id' -and $service -match 'promoted_instances') 'Orphan layer reservation is missing'
Require ($browser -match 'VegetationPromotion' -and $vegetationUi -match 'Appliquer la retouche') 'Bounded Studio controls are missing'

Require ($manifest.functional_contract.workspace_schema_version -eq 3) 'Wrong workspace schema claim'
Require ($manifest.functional_contract.vegetation_schema_version_previous -eq 1 -and $manifest.functional_contract.vegetation_schema_version_current -eq 2) 'Wrong vegetation schema claim'
Require ($manifest.functional_contract.maximum_promoted_instances -eq 1024) 'Wrong promotion collection claim'
Require ($manifest.functional_contract.visual_valley_v1_hash -ceq '664df33ffe5ea42ae8e6dc9b30e496abe62f054583f483f8019f9abb6f8d00f7') 'Wrong legacy fixture hash'
Require ($manifest.functional_contract.initial_authored_transform_matches_generated_bits) 'Initial transform parity is not recorded'
Require ($manifest.functional_contract.accepted_history_entries_per_command -eq 1 -and $manifest.functional_contract.undo_redo_exact -and $manifest.functional_contract.save_reopen_exact) 'History or persistence contract mismatch'
Require ($manifest.functional_contract.orphan_promotions_materialized_once -and $manifest.functional_contract.orphan_layer_ids_reserved_by_studio) 'Orphan source contract mismatch'
Require ($manifest.functional_contract.overlapping_promoted_instance_returns_already_promoted) 'Overlap guard is not recorded'
Require ($manifest.functional_contract.grass_budget_includes_orphan_promotions -and $manifest.functional_contract.grass_budget_failure_keeps_cache_atomic) 'Grass budget contract mismatch'
Require ($manifest.functional_contract.cache_retained_chunks_initial_promotion -eq 64 -and $manifest.functional_contract.cache_retained_chunks_same_chunk_retouch -eq 63 -and $manifest.functional_contract.cache_retained_chunks_cross_chunk_retouch -eq 62) 'Chunk cache contract mismatch'
Require (-not $manifest.functional_contract.generic_picking -and -not $manifest.functional_contract.generic_gizmo -and -not $manifest.functional_contract.demotion_or_deletion -and -not $manifest.functional_contract.live_retouch_preview) 'Evidence expands R2-020 beyond its implementation'

Require ($manifest.artifacts.Count -eq 7) 'Unexpected artifact count'
$paths = @($manifest.artifacts | ForEach-Object path)
Require (($paths | Sort-Object -Unique).Count -eq $paths.Count) 'Duplicate artifact path'
foreach ($artifact in $manifest.artifacts) {
    $path = Join-Path $repo $artifact.path
    Require (Test-Path -LiteralPath $path -PathType Leaf) "Missing artifact: $($artifact.path)"
    Require ((Get-Item -LiteralPath $path).Length -eq $artifact.bytes) "Size mismatch: $($artifact.path)"
    Require ((Hash $artifact.path) -ceq $artifact.sha256) "Hash mismatch: $($artifact.path)"
}

$targeted = Log 'cargo-test-targeted.log'
$targetedResults = [regex]::Matches($targeted, 'test result: ok\. (\d+) passed; (\d+) failed; (\d+) ignored;')
Require ($targetedResults.Count -eq 4) 'Unexpected targeted test-target count'
$targetedCounts = @($targetedResults | ForEach-Object { [int]$_.Groups[1].Value })
Require (($targetedCounts -join ',') -ceq '103,103,6,93') 'Unexpected targeted suite counts'
Require (($targetedCounts | Measure-Object -Sum).Sum -eq 305 -and $manifest.automated_validation.targeted_tests_passed -eq 305) 'Expected 305 targeted tests'
foreach ($result in $targetedResults) {
    Require ($result.Groups[2].Value -eq '0' -and $result.Groups[3].Value -eq '0') 'Failed or ignored targeted tests'
}

$requiredTests = @(
    'version_one_round_trips_bit_exactly_and_migrates_to_empty_version_two',
    'promoted_instance_version_two_round_trip_and_hash_are_canonical',
    'set_promoted_vegetation_is_pure_revisioned_reopenable_and_replayable',
    'promoted_vegetation_retouch_changes_only_transform_and_survives_layer_removal',
    'promoted_vegetation_rejects_noop_stale_forged_or_invalid_sources_atomically',
    'promoted_vegetation_collection_limit_is_enforced_through_the_atomic_edit',
    'resolver_is_deterministic_and_initial_promotion_is_bit_identical',
    'resolver_prioritizes_an_overlapping_promotion_before_stable_id_order',
    'orphan_grass_promotion_over_budget_fails_atomically',
    'promotion_and_retouch_reuse_only_the_exact_derived_chunk_handles',
    'vegetation_promotion_and_retouch_are_one_revision_each_and_survive_history_and_reopen',
    'vegetation_promotion_and_retouch_rejections_are_atomic',
    'vegetation_catalog_reserves_the_layer_id_of_an_orphan_promotion'
)
foreach ($test in $requiredTests) {
    Require ($targeted -match "test [^\r\n]*$([regex]::Escape($test)) \.\.\. ok") "Required test result missing: $test"
}

$expanded = Log 'cargo-test-workspace-excluding-unchanged-runtime.log'
Require ($expanded -match 'Compiling forge_assets' -and $expanded -notmatch 'test result:') 'Expanded NOT COMPLETED receipt mismatch'
Require ($manifest.automated_validation.expanded_workspace_status -ceq 'NOT COMPLETED') 'Expanded workspace status must not be promoted to PASS'

$nativeClippy = Log 'cargo-clippy-relevant-native.log'
$wasmClippy = Log 'cargo-clippy-studio-wasm.log'
$wasmCheck = Log 'cargo-check-studio-wasm.log'
$trunkDebug = Log 'trunk-build-debug.log'
$formatCheck = Log 'format-and-diff-check.log'
Require ($nativeClippy -match 'Finished `dev` profile' -and $nativeClippy -notmatch '(?m)^error(?:\[|:)') 'Native relevant Clippy did not finish cleanly'
Require ($wasmClippy -match 'Finished `dev` profile' -and $wasmClippy -notmatch '(?m)^error(?:\[|:)') 'Studio WASM Clippy did not finish cleanly'
Require ($wasmCheck -match 'Finished `dev` profile' -and $wasmCheck -notmatch '(?m)^error(?:\[|:)') 'Studio WASM check did not finish cleanly'
Require ($trunkDebug -match 'Finished `dev` profile' -and $trunkDebug -notmatch '(?m)^error(?:\[|:)') 'Trunk debug build did not finish cleanly'
Require ($formatCheck -match 'PASS: cargo fmt') 'Format/diff receipt mismatch'
Require ($manifest.automated_validation.trunk_release_build -ceq 'NOT RUN' -and $manifest.automated_validation.native_release_build -ceq 'NOT RUN') 'Release build limitations are misclassified'

Write-Output 'PASS: R2-020 evidence is bound to commit 0e365796 and tree d30e72ab.'
Write-Output 'PASS: 7 artifact hashes; 305 targeted tests; strict native/WASM Clippy; WASM check; Trunk debug build.'
Write-Output 'NOT COMPLETED: expanded workspace run stopped before tests at the disk protection floor.'
Write-Output 'NOT RUN: native UI, Release builds, remote CI (infrastructure billing), PresentMon, GPU, latency and performance qualification.'
