#requires -Version 7.5
[CmdletBinding()]
param()

# Read-only validation bound to one immutable source commit. It never launches Studio and cannot
# infer a native visual, Release, GPU, PresentMon, remote CI or performance verdict.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$reportPath = Join-Path $repo 'docs/evidence/gate2/ROAD_RIVER_INTERSECTIONS.md'
$manifestPath = Join-Path $repo 'docs/evidence/gate2/ROAD_RIVER_INTERSECTIONS_ARTIFACTS.json'
$expectedSourceCommit = '5b05553e374ec59afd115f30335cb8039ef19452'
$expectedSourceTree = '6e733362bb3ed888b08f45409be7522f0a4744c7'
$expectedBaseCommit = '2717f0c1a5997e3662c9a6d5063e4bcbbdd342f0'

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function GitText([string]$Commit, [string]$Path) {
    $object = '{0}:{1}' -f $Commit, $Path
    $lines = @(& git -C $repo show $object)
    Require ($LASTEXITCODE -eq 0) "Cannot read Git object: $object"
    $lines -join "`n"
}

function SourceText([string]$Path) {
    GitText $expectedSourceCommit $Path
}

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$report = Get-Content -Raw -LiteralPath $reportPath
Require ($manifest.schema -ceq 'forge.gate2.road-river-intersections-evidence') 'Wrong evidence schema'
Require ($manifest.schema_version -eq 1) 'Wrong evidence version'
Require ($manifest.source_commit -ceq $expectedSourceCommit) 'Wrong source candidate'
Require ($manifest.source_tree -ceq $expectedSourceTree) 'Wrong source tree'
Require ($manifest.base_commit -ceq $expectedBaseCommit) 'Wrong source parent'
Require ($manifest.routes.Count -eq 1 -and $manifest.routes[0].id -ceq 'R2-024' -and $manifest.routes[0].status -ceq 'PASS') 'R2-024 is not the sole PASS route'
Require ($manifest.next_route -ceq 'R2-025') 'Wrong next route'

$base = (& git -C $repo rev-parse "$expectedBaseCommit^{commit}").Trim()
Require ($LASTEXITCODE -eq 0 -and $base -ceq $expectedBaseCommit) 'Base commit is missing or ambiguous'
$commit = (& git -C $repo rev-parse "$expectedSourceCommit^{commit}").Trim()
Require ($LASTEXITCODE -eq 0 -and $commit -ceq $expectedSourceCommit) 'Source commit is missing or ambiguous'
$tree = (& git -C $repo show -s --format=%T $expectedSourceCommit).Trim()
Require ($LASTEXITCODE -eq 0 -and $tree -ceq $expectedSourceTree) 'Source tree mismatch'
$parent = (& git -C $repo show -s --format=%P $expectedSourceCommit).Trim()
Require ($LASTEXITCODE -eq 0 -and $parent -ceq $expectedBaseCommit) 'Source parent mismatch'

$expectedChangedWithStatus = @(
    "M`tapps/forge_studio/src/area_preview.rs",
    "M`tapps/forge_studio/src/area_preview_cache_tests.rs",
    "M`tapps/forge_studio/src/workspace_service_tests.rs",
    "M`tapps/forge_studio/src/world_ui.rs",
    "M`tcrates/forge_engine/src/editor_path.rs",
    "M`tcrates/forge_engine/src/editor_river.rs",
    "M`tcrates/forge_engine/src/editor_river_tests.rs",
    "M`tcrates/forge_engine/src/editor_road.rs",
    "M`tcrates/forge_engine/src/editor_viewport.rs",
    "M`tcrates/forge_world/src/lib.rs",
    "M`tcrates/forge_world/src/path.rs",
    "M`tcrates/forge_world/src/river.rs",
    "M`tcrates/forge_world/src/river_tests.rs",
    "M`tcrates/forge_world/src/road.rs"
)
$expectedChanged = @($expectedChangedWithStatus | ForEach-Object { ($_ -split "`t", 2)[1] })
$changedWithStatus = @(& git -C $repo diff-tree --no-commit-id --name-status -r $expectedSourceCommit)
Require ($LASTEXITCODE -eq 0) 'Cannot enumerate source commit changes'
Require ($changedWithStatus.Count -eq 14) 'Unexpected source file count'
Require (@(Compare-Object -CaseSensitive $expectedChangedWithStatus $changedWithStatus).Count -eq 0) 'Unexpected source file/status inventory'
$manifestChanged = @($manifest.source_change_boundary.changed_files)
Require ($manifestChanged.Count -eq 14 -and @(Compare-Object -CaseSensitive $expectedChanged $manifestChanged).Count -eq 0) 'Manifest source inventory mismatch'
Require ($manifest.source_change_boundary.changed_file_count -eq 14) 'Manifest source file count mismatch'
Require (@($expectedChanged | Where-Object { $_ -match '(^|/)(assets|upstream/cadle)(/|$)' }).Count -eq 0) 'Source unexpectedly changes an asset or Cadle path'
Require (@($expectedChanged | Where-Object { $_ -match '(^|/)Cargo\.(toml|lock)$' }).Count -eq 0) 'Source unexpectedly changes Cargo metadata'
Require (@($expectedChanged | Where-Object { $_ -match '^crates/forge_project/' }).Count -eq 0) 'Source unexpectedly changes the persistent project schema'
Require ($manifest.source_change_boundary.cargo_manifests_or_lockfiles_changed -eq 0 -and $manifest.source_change_boundary.runtime_dependencies_changed -eq 0 -and $manifest.source_change_boundary.dev_dependencies_changed -eq 0 -and $manifest.source_change_boundary.new_third_party_packages_or_versions -eq 0) 'Dependency boundary claim mismatch'
Require ($manifest.source_change_boundary.assets_changed -eq 0 -and $manifest.source_change_boundary.cadle_paths_or_content_changed -eq 0 -and $manifest.source_change_boundary.persistent_schema_files_changed -eq 0) 'Asset, Cadle or persistent-schema boundary claim mismatch'

$workspace = SourceText 'crates/forge_project/src/workspace.rs'
$worldPath = SourceText 'crates/forge_world/src/path.rs'
$worldRoad = SourceText 'crates/forge_world/src/road.rs'
$worldRiver = SourceText 'crates/forge_world/src/river.rs'
$worldRiverTests = SourceText 'crates/forge_world/src/river_tests.rs'
$enginePath = SourceText 'crates/forge_engine/src/editor_path.rs'
$engineRoad = SourceText 'crates/forge_engine/src/editor_road.rs'
$engineRiver = SourceText 'crates/forge_engine/src/editor_river.rs'
$engineRiverTests = SourceText 'crates/forge_engine/src/editor_river_tests.rs'
$engineViewport = SourceText 'crates/forge_engine/src/editor_viewport.rs'
$areaPreview = SourceText 'apps/forge_studio/src/area_preview.rs'
$areaPreviewTests = SourceText 'apps/forge_studio/src/area_preview_cache_tests.rs'
$studioServiceTests = SourceText 'apps/forge_studio/src/workspace_service_tests.rs'
$studioWorldUi = SourceText 'apps/forge_studio/src/world_ui.rs'

Require ($workspace -match '(?m)^pub const WORKSPACE_SCHEMA_VERSION: u32 = 6;$') 'Workspace schema must remain v6'
Require ($worldRiver -match '(?m)^pub const RIVER_GENERATION_VERSION: u32 = 2;$') 'River generation v2 is missing'
Require ($worldRiver -match '(?m)^pub const MAX_GENERATED_ROAD_RIVER_INTERSECTIONS: usize = 64;$') 'Ford count bound mismatch'
Require ($worldRiver -match '(?m)^pub const MIN_TERRAIN_FORD_CROSSING_ANGLE_DEGREES: f32 = 30\.0;$' -and $worldRiver -match '(?m)^pub const MAX_TERRAIN_FORD_CROSSING_ANGLE_DEGREES: f32 = 90\.0;$') 'Ford crossing angle bounds mismatch'
Require ($worldRiver -match '(?m)^pub const MIN_TERRAIN_FORD_WATER_DEPTH_M: f32 = 0\.05;$') 'Ford residual water-depth bound mismatch'
Require ($worldRiver -match 'pub enum RoadRiverIntersectionKind' -and $worldRiver -match 'TerrainFordV1' -and $worldRiver -match 'pub struct GeneratedRoadRiverIntersection') 'TerrainFordV1 metadata type is missing'
foreach ($field in @('river_path_id', 'road_path_id', 'kind', 'position_xz_m', 'crossing_angle_degrees', 'road_surface_height_m', 'water_surface_height_m')) {
    Require ($worldRiver -match [regex]::Escape("pub $field")) "Ford metadata field missing: $field"
}
Require ($worldRiver -match 'pub road_river_intersections: Vec<GeneratedRoadRiverIntersection>' -and $worldRiver -match 'pub fn validate_generated_road_river_intersections') 'Generated ford list or exhaustive validator is missing'
Require ($worldRiver -match 'metadata are not strictly sorted and unique' -and $worldRiver -match 'metadata are not exhaustive') 'Sorted, unique or exhaustive ford validation is missing'
Require ($worldRiver -match 'validate_generated_river_centrelines' -and $worldRiver -match 'validate_generated_road_centrelines' -and $worldRiver -match 'to_bits\(\)') 'Generated centreline binding is missing'
Require ($worldRiver -match 'prepare_terrain_ford' -and $worldRiver -match 'MultipleRiverRoadIntersections' -and $worldRiver -match 'TangentialRiverRoadIntersection' -and $worldRiver -match 'ShallowTerrainFordAngle' -and $worldRiver -match 'TerrainFordNearEndpoint' -and $worldRiver -match 'ExtendedRiverRoadOverlap' -and $worldRiver -match 'InsufficientTerrainFordWaterDepth') 'Bounded ford classifier or rejection taxonomy is incomplete'
Require ($worldRiver -match 'actual\.kind != RoadRiverIntersectionKind::TerrainFordV1' -and $worldRiver -match 'metadata values differ from visible generated surfaces') 'Ford kind or visible-surface equality check is missing'
Require ($worldRiver -match '(?m)^pub const MAX_GENERATED_RIVER_LONGITUDINAL_SAMPLES: usize = 16_384;$' -and $worldRoad -match '(?m)^pub const MAX_GENERATED_ROAD_LONGITUDINAL_SAMPLES: usize = 16_384;$') 'Aggregate centreline budgets mismatch'
Require ($worldPath -match 'pub fn validate_generated_path_centrelines' -and $worldPath -match 'TerrainGenerationPlan::from_document' -and $worldPath -match 'same_vec3_bits') 'Canonical path/terrain validation is missing'

Require ($enginePath -match 'validate_generated_path_centrelines' -and $enginePath -match 'source_terrain: &TerrainDocument' -and $enginePath -match 'source_paths: &PathDocument') 'Engine path canonical-source validation is missing'
Require ($engineRoad -match 'source_roads: &RoadDocument' -and $engineRoad -match 'source_roads\.roads\.len\(\) == generated\.roads\.len\(\)' -and $engineRoad -match 'source\.profile == derived\.profile') 'Engine road completeness validation is missing'
Require ($engineRiver -match 'source_terrain: &TerrainDocument' -and $engineRiver -match 'source_rivers: &RiverDocument' -and $engineRiver -match 'source_rivers\.rivers\.len\(\) == generated\.rivers\.len\(\)' -and $engineRiver -match 'validate_generated_road_river_intersections') 'Engine river completeness or ford validation is missing'
Require ($engineViewport -match 'pub struct EditorPathRequest' -and $engineViewport -match 'pub source_terrain: TerrainDocument' -and $engineViewport -match 'pub source_paths: PathDocument') 'Viewport canonical path request is missing'
Require ($engineViewport -match 'pub struct EditorRoadRequest' -and $engineViewport -match 'pub source_roads: RoadDocument' -and $engineViewport -match 'pub struct EditorRiverRequest' -and $engineViewport -match 'pub source_rivers: RiverDocument') 'Viewport canonical road/river request is missing'
Require ($engineViewport -match 'validate_request_payload' -and $engineViewport -match 'path::validate_generated_paths' -and $engineViewport -match 'road::validate_generated_roads' -and $engineViewport -match 'river::validate_generated_rivers') 'Viewport fail-closed validation chain is missing'
Require ($engineViewport -notmatch 'RiverGenerationPlan::from_documents') 'Viewport unexpectedly replays the complete RiverGenerationPlan'

Require ($areaPreview -match 'pub effective_terrain: TerrainDocument' -and $areaPreview -match 'RiverGenerationPlan::from_documents') 'AreaPreview effective-terrain recipe is missing'
Require ($studioWorldUi -match 'fn current_projection_sources' -and $studioWorldUi -match 'Cible modifiée pendant la génération' -and $studioWorldUi -match 'Sources terrain/chemin/route/rivière modifiées') 'Studio post-response source reread is missing'
foreach ($sourceField in @('source_terrain', 'source_paths', 'source_roads', 'source_rivers')) {
    Require ($studioWorldUi -match [regex]::Escape($sourceField)) "Studio canonical request field missing: $sourceField"
}

$requiredTests = @(
    @{ source = $worldPath; name = 'generated_path_centrelines_are_bound_to_the_exact_canonical_source' },
    @{ source = $worldRiverTests; name = 'terrain_ford_is_deterministic_visible_and_created_in_both_orders' },
    @{ source = $worldRiverTests; name = 'generated_river_centreline_cannot_move_with_its_ford_metadata' },
    @{ source = $worldRiverTests; name = 'generated_road_centreline_cannot_move_away_from_its_path' },
    @{ source = $worldRiverTests; name = 'unsupported_road_river_contacts_are_rejected_atomically' },
    @{ source = $worldRiverTests; name = 'ford_with_insufficient_residual_water_depth_is_atomic' },
    @{ source = $engineRiver; name = 'validation_rejects_forged_or_noncanonical_ford_metadata' },
    @{ source = $engineRiverTests; name = 'forged_canonical_terrain_hash_is_rejected_with_the_presented_scene_intact' },
    @{ source = $engineRiverTests; name = 'forged_omitted_or_stale_ford_payload_preserves_the_presented_scene' },
    @{ source = $studioServiceTests; name = 'terrain_ford_creation_is_order_independent_and_reopens_with_the_same_derived_hash' },
    @{ source = $studioServiceTests; name = 'terrain_ford_redraw_from_separate_to_crossed_is_one_revision_and_undo_redo_exact' },
    @{ source = $areaPreviewTests; name = 'road_only_ford_creation_and_retouch_reuse_effective_terrain_vegetation_paths_and_water' }
)
foreach ($test in $requiredTests) {
    Require ($test.source -match [regex]::Escape($test.name)) "Required source/test missing: $($test.name)"
}

$contract = $manifest.functional_contract
Require ($contract.workspace_schema_version -eq 6 -and -not $contract.persistent_schema_changed -and -not $contract.new_authoring_command_added -and -not $contract.ford_metadata_persisted) 'Persistent contract boundary mismatch'
Require ($contract.river_generation_version -eq 2 -and $contract.intersection_kind -ceq 'terrain_ford_v1') 'Ford version or kind claim mismatch'
Require ($contract.maximum_generated_road_river_intersections -eq 64 -and $contract.minimum_crossing_angle_degrees -eq 30 -and $contract.maximum_crossing_angle_degrees -eq 90 -and $contract.minimum_residual_water_depth_m -eq 0.05) 'Ford bounds claim mismatch'
Require ($contract.requires_distinct_road_and_river_paths -and $contract.requires_exactly_one_isolated_transversal_crossing -and $contract.requires_endpoint_clearance -and $contract.requires_local_footprint_overlap) 'Supported crossing claim mismatch'
Require ($contract.river_river_overlaps_remain_rejected -and $contract.same_path_road_river_conflict_remains_rejected -and $contract.unsupported_road_river_contacts_rejected_atomically) 'Unsupported overlap claim mismatch'
Require ($contract.intersection_metadata_sorted_unique_and_exhaustive -and $contract.intersection_values_match_visible_surfaces_bit_exactly -and $contract.generated_source_hashes_and_version_correlated) 'Ford metadata validation claim mismatch'
Require ($contract.generated_river_centrelines_match_path_resampling_bit_exactly -and $contract.generated_road_centrelines_match_path_resampling_bit_exactly -and $contract.maximum_generated_river_longitudinal_samples -eq 16384 -and $contract.maximum_generated_road_longitudinal_samples -eq 16384) 'Centreline binding or budget claim mismatch'
Require ($contract.canonical_terrain_persisted_unchanged -and $contract.effective_terrain_is_derived_and_not_persisted) 'Effective-terrain boundary claim mismatch'
Require ($contract.creation_is_deterministic_in_both_authoring_orders -and $contract.accepted_history_entries_per_command -eq 1 -and $contract.undo_redo_exact -and $contract.save_reopen_derived_hash_exact) 'Studio persistence claim mismatch'
Require ($contract.road_only_effective_terrain_chunks_sent -eq 0 -and -not $contract.road_only_terrain_mesh_rebuilt -and $contract.road_only_vegetation_paths_and_water_reused -and $contract.road_only_road_mesh_rebuilt) 'Road-only cache claim mismatch'
Require ($contract.studio_rereads_current_sources_before_pending -and $contract.density_preview_uses_validated_current_area_sources -and $contract.viewport_validates_canonical_terrain_path_road_river_sources) 'Canonical Studio/viewport source claim mismatch'
Require (-not $contract.viewport_replays_complete_river_generation_plan -and $contract.viewport_internal_native_worker_trust_boundary -and $contract.stale_forged_or_omitted_payload_preserves_presented_scene) 'Viewport trust or replay boundary claim mismatch'
Require (-not $contract.uses_external_assets -and -not $contract.native_visual_review_performed) 'Asset or visual boundary claim mismatch'

$expectedReceipts = @(
    @{ id = 'forge_project_tests'; command = 'cargo test -p forge_project --lib'; status = 'PASS'; passed = 137 },
    @{ id = 'forge_world_tests'; command = 'cargo test -p forge_world --lib'; status = 'PASS'; passed = 126 },
    @{ id = 'forge_engine_tests_lib'; command = 'cargo test -p forge_engine --lib'; status = 'PASS'; passed = 193 },
    @{ id = 'forge_studio_tests_lib'; command = 'cargo test -p forge_studio --lib'; status = 'PASS'; passed = 120 },
    @{ id = 'forge_studio_tests_bin'; command = 'cargo test -p forge_studio --bin forge-studio'; status = 'PASS'; passed = 6 },
    @{ id = 'forge_studio_check_wasm'; command = 'cargo check -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web'; status = 'PASS' },
    @{ id = 'forge_studio_clippy_native'; command = 'cargo clippy -p forge_studio --all-targets -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_studio_clippy_wasm'; command = 'cargo clippy -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_engine_clippy_native'; command = 'cargo clippy -p forge_engine --all-targets --all-features -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_engine_clippy_wasm_lib'; command = 'cargo clippy -p forge_engine --lib --target wasm32-unknown-unknown --no-default-features --features web -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_project_world_clippy_native'; command = 'cargo clippy -p forge_project -p forge_world --all-targets -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_project_world_clippy_wasm_lib'; command = 'cargo clippy -p forge_project -p forge_world --lib --target wasm32-unknown-unknown -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_studio_trunk_debug'; command = '$env:NO_COLOR=''true''; trunk build --locked --offline'; status = 'PASS' },
    @{ id = 'cargo_fmt'; command = 'cargo fmt --all -- --check'; status = 'PASS' },
    @{ id = 'git_diff_check'; command = 'git diff --check'; status = 'PASS' }
)
$receipts = @($manifest.automated_validation.receipts)
Require ($manifest.automated_validation.raw_log_bundle -ceq 'NOT CAPTURED' -and $manifest.artifacts.Count -eq 0) 'This evidence must not invent a raw log bundle'
Require ($receipts.Count -eq $expectedReceipts.Count) 'Unexpected validation receipt count'
Require ((@($receipts.id | Sort-Object -Unique)).Count -eq $receipts.Count) 'Duplicate validation receipt id'
foreach ($expected in $expectedReceipts) {
    $actual = @($receipts | Where-Object { $_.id -ceq $expected.id })
    Require ($actual.Count -eq 1) "Missing validation receipt: $($expected.id)"
    $actual = $actual[0]
    Require ($actual.command -ceq $expected.command -and $actual.status -ceq $expected.status) "Validation receipt mismatch: $($expected.id)"
    if ($expected.ContainsKey('passed')) {
        Require ($actual.passed -eq $expected.passed -and $actual.failed -eq 0) "Test count mismatch: $($expected.id)"
    }
}
Require ($manifest.automated_validation.full_library_suite_tests_passed -eq (137 + 126 + 193 + 120)) 'Full-library suite total mismatch'
Require ($manifest.automated_validation.studio_binary_tests_passed_separately -eq 6) 'Studio binary count mismatch'
Require (@($receipts | Where-Object id -CEQ 'forge_studio_tests_bin')[0].excluded_from_full_library_suite_total) 'Studio binary tests must be excluded from the 576 library total'
foreach ($id in @('forge_engine_clippy_wasm_lib', 'forge_project_world_clippy_wasm_lib')) {
    Require (@($receipts | Where-Object id -CEQ $id)[0].scope_note -match '(library|libraries) only' -and @($receipts | Where-Object id -CEQ $id)[0].scope_note -match 'no all-targets WASM verdict') "WASM receipt scope is overstated: $id"
}
$recovery = $manifest.automated_validation.trunk_debug_recovery
Require ($recovery.first_attempt -ceq 'FAIL — no space left on device' -and $recovery.recovery_command -ceq 'cargo clean -p forge_studio --target wasm32-unknown-unknown' -and $recovery.reclaimed_gib -eq 4.5 -and $recovery.second_attempt -ceq 'PASS' -and $recovery.raw_log -ceq 'NOT CAPTURED') 'Trunk recovery history mismatch'
$trunkReceipt = @($receipts | Where-Object id -CEQ 'forge_studio_trunk_debug')[0]
Require ($trunkReceipt.attempts -eq 2 -and $trunkReceipt.first_attempt_environment_failure -ceq 'no space left on device' -and $trunkReceipt.recovery_command -ceq $recovery.recovery_command -and $trunkReceipt.reclaimed_gib -eq 4.5) 'Trunk receipt hides its recovered first attempt'

Require ($manifest.native_ui_qualification.status -ceq 'NOT RUN') 'Native UI must remain explicitly NOT RUN'
Require ($manifest.release_qualification.native_release_build -ceq 'NOT RUN' -and $manifest.release_qualification.trunk_release_build -ceq 'NOT RUN') 'Release builds must remain explicitly NOT RUN'
Require ($manifest.gpu_qualification.status -ceq 'NOT RUN' -and $manifest.performance_qualification.status -ceq 'NOT RUN') 'GPU/performance must remain explicitly NOT RUN'
Require ($manifest.remote_ci.status -ceq 'NOT RUN — infrastructure billing' -and $manifest.remote_ci.decision -ceq 'ADR-0006') 'Wrong remote CI exception'
Require ($manifest.presentmon.status -ceq 'NOT RUN') 'PresentMon must remain explicitly NOT RUN'

$forbiddenPassClaims = @(
    '(?i)(native UI|UI native|inspection visuelle native).*\bPASS\b',
    '(?i)(native release|build natif Release|trunk release).*\bPASS\b',
    '(?i)\bGPU\b.*\bPASS\b',
    '(?i)\bPresentMon\b.*\bPASS\b',
    '(?i)(remote CI|CI distante).*\bPASS\b'
)
$evidenceLines = @(Get-Content -LiteralPath $reportPath) + @(Get-Content -LiteralPath $manifestPath)
foreach ($line in $evidenceLines) {
    foreach ($pattern in $forbiddenPassClaims) {
        Require ($line -notmatch $pattern) "Forbidden qualification claim: $line"
    }
}
Require ($report -match 'TerrainFordV1' -and $report -match '30 à 90 degrés' -and $report -match '0,05 m') 'Report hides the bounded ford kind, angle or depth'
Require ($report -match 'strictement triée et unique' -and $report -match 'exhaustive') 'Report omits sorted, unique or exhaustive metadata'
Require ($report -match 'worker natif interne' -and $report -match 'ne rejoue pas intégralement `RiverGenerationPlan`') 'Report hides the viewport trust/replay boundary'
Require ($report -match 'première tentative Trunk a échoué' -and $report -match 'cargo clean -p forge_studio --target wasm32-unknown-unknown' -and $report -match '4,5 Gio') 'Report hides the recovered Trunk attempt'
Require ($report -match 'Aucun asset,\s+contenu Cadle, manifeste Cargo, lockfile ou dépendance') 'Report does not state the exact source/dependency boundary'

Write-Output "PASS: R2-024 evidence is bound to commit $($expectedSourceCommit.Substring(0, 8)), tree $($expectedSourceTree.Substring(0, 8)) and its exact 14-file inventory."
Write-Output 'PASS: 576 library tests plus 6 separate Studio binary tests; strict declared native/WASM product checks; recovered Trunk debug build.'
Write-Output 'PASS: bounded TerrainFordV1 metadata, canonical source binding, deterministic persistence and road-only cache reuse.'
Write-Output 'NOT RUN: native UI, Release builds, GPU, performance, remote CI (infrastructure billing) and PresentMon.'
