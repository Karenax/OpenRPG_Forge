#requires -Version 7.5
[CmdletBinding()]
param()

# Read-only validation bound to one immutable source commit. It never launches Studio and cannot
# infer a native visual, Release, GPU, PresentMon, remote CI or performance verdict.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$reportPath = Join-Path $repo 'docs/evidence/gate2/RIVER_CARVING.md'
$manifestPath = Join-Path $repo 'docs/evidence/gate2/RIVER_CARVING_ARTIFACTS.json'
$expectedSourceCommit = '7316e552d25db173af515f572a3a8ccc24c2f90f'
$expectedSourceTree = '805da88d55d4327adf4d64ffd733a4f3f0d54765'
$expectedBaseCommit = '07d03acd42d4d8519b01c541d7a2df7fdc7b2441'

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
Require ($manifest.schema -ceq 'forge.gate2.river-carving-evidence') 'Wrong evidence schema'
Require ($manifest.schema_version -eq 1) 'Wrong evidence version'
Require ($manifest.source_commit -ceq $expectedSourceCommit) 'Wrong source candidate'
Require ($manifest.source_tree -ceq $expectedSourceTree) 'Wrong source tree'
Require ($manifest.base_commit -ceq $expectedBaseCommit) 'Wrong source parent'
Require ($manifest.routes.Count -eq 1 -and $manifest.routes[0].id -ceq 'R2-023' -and $manifest.routes[0].status -ceq 'PASS') 'R2-023 is not the sole PASS route'
Require ($manifest.next_route -ceq 'R2-024') 'Wrong next route'

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
    "M`tapps/forge_studio/src/brush_ui.rs",
    "M`tapps/forge_studio/src/main.rs",
    "M`tapps/forge_studio/src/path_ui.rs",
    "M`tapps/forge_studio/src/workspace_api.rs",
    "M`tapps/forge_studio/src/workspace_service.rs",
    "M`tapps/forge_studio/src/workspace_service_tests.rs",
    "M`tapps/forge_studio/src/world_ui.rs",
    "M`tcrates/forge_engine/src/editor_partial_tests.rs",
    "A`tcrates/forge_engine/src/editor_river.rs",
    "A`tcrates/forge_engine/src/editor_river_tests.rs",
    "M`tcrates/forge_engine/src/editor_viewport.rs",
    "M`tcrates/forge_project/README.md",
    "M`tcrates/forge_project/src/lib.rs",
    "A`tcrates/forge_project/src/river.rs",
    "M`tcrates/forge_project/src/workspace.rs",
    "M`tcrates/forge_project/src/workspace_edit.rs",
    "M`tcrates/forge_project/src/workspace_edit_tests.rs",
    "M`tcrates/forge_project/src/workspace_legacy.rs",
    "M`tcrates/forge_world/src/lib.rs",
    "M`tcrates/forge_world/src/path.rs",
    "A`tcrates/forge_world/src/river.rs",
    "A`tcrates/forge_world/src/river_tests.rs",
    "M`tcrates/forge_world/src/road.rs",
    "M`tcrates/forge_world/src/terrain_brush_tests.rs",
    "M`tcrates/forge_world/src/vegetation_promotion.rs"
)
$expectedChanged = @($expectedChangedWithStatus | ForEach-Object { ($_ -split "`t", 2)[1] })
$changedWithStatus = @(& git -C $repo diff-tree --no-commit-id --name-status -r $expectedSourceCommit)
Require ($LASTEXITCODE -eq 0) 'Cannot enumerate source commit changes'
Require ($changedWithStatus.Count -eq 27) 'Unexpected source file count'
Require (@(Compare-Object -CaseSensitive $expectedChangedWithStatus $changedWithStatus).Count -eq 0) 'Unexpected source file/status inventory'
$manifestChanged = @($manifest.source_change_boundary.changed_files)
Require ($manifestChanged.Count -eq 27 -and @(Compare-Object -CaseSensitive $expectedChanged $manifestChanged).Count -eq 0) 'Manifest source inventory mismatch'
Require ($manifest.source_change_boundary.changed_file_count -eq 27) 'Manifest source file count mismatch'
Require (@($expectedChanged | Where-Object { $_ -match '(^|/)(assets|upstream/cadle)(/|$)' }).Count -eq 0) 'Source unexpectedly changes an asset or Cadle path'
Require (@($expectedChanged | Where-Object { $_ -match '(^|/)Cargo\.(toml|lock)$' }).Count -eq 0) 'Source unexpectedly changes Cargo metadata'
Require ($manifest.source_change_boundary.cargo_manifests_or_lockfiles_changed -eq 0 -and $manifest.source_change_boundary.runtime_dependencies_changed -eq 0 -and $manifest.source_change_boundary.dev_dependencies_changed -eq 0 -and $manifest.source_change_boundary.new_third_party_packages_or_versions -eq 0) 'Dependency boundary claim mismatch'
Require ($manifest.source_change_boundary.assets_changed -eq 0 -and $manifest.source_change_boundary.cadle_paths_or_content_changed -eq 0) 'Asset or Cadle boundary claim mismatch'

$workspace = SourceText 'crates/forge_project/src/workspace.rs'
$workspaceEdit = SourceText 'crates/forge_project/src/workspace_edit.rs'
$workspaceLegacy = SourceText 'crates/forge_project/src/workspace_legacy.rs'
$projectRiver = SourceText 'crates/forge_project/src/river.rs'
$projectTests = SourceText 'crates/forge_project/src/workspace_edit_tests.rs'
$worldRiver = SourceText 'crates/forge_world/src/river.rs'
$worldRiverTests = SourceText 'crates/forge_world/src/river_tests.rs'
$worldPath = SourceText 'crates/forge_world/src/path.rs'
$worldRoad = SourceText 'crates/forge_world/src/road.rs'
$vegetationPromotion = SourceText 'crates/forge_world/src/vegetation_promotion.rs'
$engineRiver = SourceText 'crates/forge_engine/src/editor_river.rs'
$engineRiverTests = SourceText 'crates/forge_engine/src/editor_river_tests.rs'
$engineViewport = SourceText 'crates/forge_engine/src/editor_viewport.rs'
$studioApi = SourceText 'apps/forge_studio/src/workspace_api.rs'
$studioService = SourceText 'apps/forge_studio/src/workspace_service.rs'
$studioServiceTests = SourceText 'apps/forge_studio/src/workspace_service_tests.rs'
$studioPathUi = SourceText 'apps/forge_studio/src/path_ui.rs'
$studioMain = SourceText 'apps/forge_studio/src/main.rs'
$areaPreview = SourceText 'apps/forge_studio/src/area_preview.rs'
$areaPreviewTests = SourceText 'apps/forge_studio/src/area_preview_cache_tests.rs'
$studioWorldUi = SourceText 'apps/forge_studio/src/world_ui.rs'

Require ($workspace -match '(?m)^pub const WORKSPACE_SCHEMA_VERSION: u32 = 6;$' -and $workspace -match 'pub rivers: RiverDocument') 'Workspace v6 river source is missing'
Require ($projectRiver -match '(?m)^pub const RIVER_DOCUMENT_SCHEMA: &str = "forge\.river";$' -and $projectRiver -match '(?m)^pub const RIVER_DOCUMENT_SCHEMA_VERSION: u32 = 1;$') 'River schema v1 is missing'
Require ($projectRiver -match '(?m)^pub const MAX_RIVERS_PER_DOCUMENT: usize = 16;$' -and $projectRiver -match '(?m)^pub const MAX_RIVER_DOCUMENT_JSON_BYTES: usize = 64 \* 1_024;$' -and $projectRiver -match '(?m)^pub const MAX_TOTAL_RIVER_LENGTH_M: f64 = 8_192\.0;$') 'River document or length budget mismatch'
Require ($projectRiver -match '(?m)^pub const MIN_RIVER_SURFACE_WIDTH_M: f32 = 2\.0;$' -and $projectRiver -match '(?m)^pub const MAX_RIVER_SURFACE_WIDTH_M: f32 = 32\.0;$' -and $projectRiver -match '(?m)^pub const MIN_RIVER_BED_DEPTH_M: f32 = 0\.15;$' -and $projectRiver -match '(?m)^pub const MAX_RIVER_BED_DEPTH_M: f32 = 8\.0;$') 'River width or depth bounds mismatch'
Require ($projectRiver -match '(?m)^pub const MIN_RIVER_BANK_BLEND_WIDTH_M: f32 = 1\.0;$' -and $projectRiver -match '(?m)^pub const MAX_RIVER_BANK_BLEND_WIDTH_M: f32 = 16\.0;$') 'River bank blend bounds mismatch'
Require ($projectRiver -match 'TerrainCarvedChannelV1' -and $projectRiver -match 'ForgeWaterV1' -and $workspaceEdit -match 'UpsertRiver' -and $workspaceEdit -match 'expected_river_document_hash') 'Persistent river upsert contract is missing'
Require ($workspaceLegacy -match '(?m)^\s*5 => migrate_v5' -and $workspaceLegacy -match 'RiverDocument::new') 'Workspace v5 river migration is missing'

Require ($worldRiver -match '(?m)^pub const RIVER_GENERATION_VERSION: u32 = 1;$' -and $worldRiver -match '(?m)^pub const RIVER_SURFACE_CLEARANCE_M: f32 = 0\.04;$' -and $worldRiver -match '(?m)^pub const RIVER_MESH_MAX_LONGITUDINAL_CHORD_M: f64 = 2\.0;$') 'River generation version, clearance or chord bound mismatch'
Require ($worldRiver -match '(?m)^pub const RIVER_CROSS_SECTION_VERTEX_COUNT: usize = 3;$' -and $worldRiver -match '(?m)^pub const MAX_GENERATED_RIVER_LONGITUDINAL_SAMPLES: usize = 16_384;$' -and $worldRiver -match '(?m)^pub const MAX_EFFECTIVE_RIVER_TERRAIN_SAMPLES: usize = 65_536;$' -and $worldRiver -match '(?m)^pub const MAX_RIVER_OVERLAP_COMPARISONS: usize = 4_000_000;$') 'River mesh, terrain or overlap budgets mismatch'
Require ($worldRiver -match 'pub struct RiverGenerationResult' -and $worldRiver -match 'pub effective_terrain: TerrainDocument' -and $worldRiver -match 'pub effective_terrain_document_hash: ContentHash') 'Effective terrain contract is missing'
Require ($worldRiver -match 'target_m\.min\(f64::from\(canonical_height_m\)\)' -and $worldRiver -match 'fn smoothstep' -and $worldRiver -match 'blend_to_earth' -and $worldRiver -match 'downstream_surface_m\.min\(raw_surface_m\)') 'Never-raised carving, earth blend or monotone water contract is missing'
Require ($worldRiver -match 'pub fn preview_upsert_river' -and $worldRiver -match 'validate_candidate_rivers' -and $worldRiver -match 'RiverOverlap' -and $worldRiver -match 'RiverRoadOverlap' -and $worldRiver -match 'FootprintOutOfBounds') 'Pure river command or fail-closed projection is missing'
Require ($worldPath -match 'validate_candidate_rivers' -and $worldRoad -match 'validate_candidate_rivers') 'Path or road edits do not revalidate river projection'

Require ($vegetationPromotion -match 'RiverGenerationPlan::from_documents' -and $vegetationPromotion -match '&river_projection\.effective_terrain' -and $vegetationPromotion -match 'VegetationPromotionError::River') 'Vegetation promotion does not resolve from effective terrain'
Require ($engineRiver -match 'validate_generated_rivers' -and $engineRiver -match 'procedural_editor_river_material' -and $engineRiver -match 'base_color_texture\.is_none') 'Bounded external-asset-free river adapter is missing'
Require ($engineViewport -match 'pub struct EditorRiverRequest' -and $engineViewport -match 'river_rebuilt_meshes' -and $engineViewport -match 'effective_terrain_source_hash') 'Viewport river request or counters are missing'
Require ($studioApi -match 'UpsertRiver' -and $studioService -match 'execute_river_upsert' -and $studioService -match 'preview_upsert_river') 'Typed Studio river command or service is missing'
Require ($studioPathUi -match 'fn RiverProfileForm' -and $studioPathUi -match 'RiverChannelProfileAlgorithm::TerrainCarvedChannelV1' -and $studioPathUi -match 'RiverSurface::ForgeWaterV1') 'Studio river profile form is missing'
Require ($areaPreview -match 'pub rivers: GeneratedRivers' -and $areaPreview -match 'RiverGenerationPlan::from_documents' -and $areaPreview -match 'effective_area\.terrain = river_projection\.effective_terrain') 'AreaPreview effective terrain pipeline is missing'
Require ($studioWorldUi -match 'EditorRiverRequest' -and $studioWorldUi -match 'effective_terrain_document_hash') 'Studio viewport river projection or hash validation is missing'
Require ($studioMain -match 'WorkspaceCommand::PromoteVegetationInstance' -and $studioMain -match 'WorkspaceCommand::UpsertRiver' -and $studioMain -match 'tauri::async_runtime::spawn_blocking') 'Blocking projection commands are not offloaded by Tauri'

$requiredTests = @(
    @{ source = $projectRiver; name = 'canonical_round_trip_and_hash_are_stable' },
    @{ source = $projectRiver; name = 'strict_json_rejects_unknown_schema_and_oversized_input' },
    @{ source = $projectRiver; name = 'rivers_are_unique_sorted_non_nil_and_bounded' },
    @{ source = $projectRiver; name = 'channel_numeric_limits_are_enforced' },
    @{ source = $projectTests; name = 'workspace_v5_migration_adds_only_empty_rivers_and_preserves_embedded_hashes' },
    @{ source = $projectTests; name = 'upsert_river_creates_edits_sorts_reopens_and_replays_from_its_inverse' },
    @{ source = $projectTests; name = 'upsert_river_rejects_noop_stale_hashes_missing_and_invalid_atomically' },
    @{ source = $projectTests; name = 'river_targets_reject_closed_paths_and_road_conflicts_without_mutation' },
    @{ source = $projectTests; name = 'river_assignments_respect_collection_and_total_control_polyline_length_limits' },
    @{ source = $worldRiverTests; name = 'generation_is_deterministic_hash_bound_sorted_and_strict' },
    @{ source = $worldRiverTests; name = 'carving_is_rounded_blended_earth_and_never_raises_canonical_terrain' },
    @{ source = $worldRiverTests; name = 'empty_river_document_preserves_the_canonical_terrain_exactly' },
    @{ source = $worldRiverTests; name = 'upsert_noop_context_invalid_and_missing_rejections_are_atomic' },
    @{ source = $worldRiverTests; name = 'river_river_and_river_road_overlaps_fail_atomically_in_both_edit_directions' },
    @{ source = $worldRiverTests; name = 'footprint_and_path_redraw_are_revalidated_before_a_revision_exists' },
    @{ source = $worldRiverTests; name = 'budgets_invalid_geometry_and_overlap_work_are_checked' },
    @{ source = $vegetationPromotion; name = 'river_visible_instance_resolution_and_promotion_are_bit_identical' },
    @{ source = $vegetationPromotion; name = 'resolver_propagates_an_invalid_river_projection_without_mutation' },
    @{ source = $engineRiver; name = 'valid_river_builds_a_bounded_external_asset_free_water_mesh' },
    @{ source = $engineRiver; name = 'validation_rejects_wrong_sources_order_and_malformed_geometry' },
    @{ source = $engineRiverTests; name = 'river_update_stages_derived_terrain_and_water_then_swaps_them_together' },
    @{ source = $engineRiverTests; name = 'river_removal_and_rejection_release_only_owned_objects_without_leaks' },
    @{ source = $engineRiverTests; name = 'superseded_river_staging_is_discarded_and_latest_request_wins' },
    @{ source = $areaPreviewTests; name = 'river_only_edit_partially_refreshes_the_effective_terrain_and_dependents' },
    @{ source = $studioServiceTests; name = 'river_profile_is_one_revision_and_survives_redraw_history_and_reopen' },
    @{ source = $studioServiceTests; name = 'vegetation_promotion_with_a_river_uses_the_visible_effective_projection' },
    @{ source = $studioServiceTests; name = 'river_profile_rejections_are_atomic' },
    @{ source = $studioServiceTests; name = 'opening_v2_v3_v4_and_v5_adds_new_area_documents_without_rewriting_legacy_snapshots' }
)
foreach ($test in $requiredTests) {
    Require ($test.source -match [regex]::Escape($test.name)) "Required source/test missing: $($test.name)"
}

$contract = $manifest.functional_contract
Require ($contract.workspace_schema_version_previous -eq 5 -and $contract.workspace_schema_version_current -eq 6) 'Wrong workspace migration claim'
Require ($contract.river_schema -ceq 'forge.river' -and $contract.river_schema_version -eq 1) 'Wrong river schema claim'
Require ($contract.maximum_rivers_per_document -eq 16 -and $contract.maximum_river_document_json_bytes -eq 65536 -and $contract.maximum_total_control_polyline_length_m -eq 8192 -and -not $contract.control_polyline_length_budget_is_arc_length) 'River document or control-polyline budget claim mismatch'
Require ($contract.minimum_surface_width_m -eq 2 -and $contract.maximum_surface_width_m -eq 32 -and $contract.minimum_bed_depth_m -eq 0.15 -and $contract.maximum_bed_depth_m -eq 8 -and $contract.minimum_bank_blend_width_m -eq 1 -and $contract.maximum_bank_blend_width_m -eq 16) 'River profile bounds claim mismatch'
Require ($contract.river_generation_version -eq 1 -and $contract.river_surface_clearance_m -eq 0.04 -and $contract.maximum_horizontal_chord_m -eq 2 -and -not $contract.horizontal_chord_bound_is_arc_length) 'Generated river spacing claim mismatch'
Require ($contract.cross_section_vertices_per_row -eq 3 -and $contract.triangles_per_longitudinal_interval -eq 4 -and $contract.maximum_generated_longitudinal_samples -eq 16384 -and $contract.maximum_generated_vertices -eq 49152 -and $contract.maximum_generated_indices -eq 196608) 'Generated river mesh budget claim mismatch'
Require ($contract.maximum_effective_terrain_samples -eq 65536 -and $contract.maximum_overlap_comparisons -eq 4000000) 'Carving or overlap budget claim mismatch'
Require ($contract.canonical_terrain_persisted_unchanged -and $contract.effective_terrain_is_derived_and_not_persisted -and $contract.empty_rivers_preserve_canonical_terrain_exactly -and $contract.carving_never_raises_a_terrain_sample -and $contract.bed_and_banks_blend_to_procedural_earth -and $contract.water_surface_monotone_non_increasing_along_path) 'Effective terrain or water claim mismatch'
Require ($contract.candidate_projection_generated_before_edit_exposure -and $contract.path_redraw_revalidates_river_projection -and $contract.same_path_road_river_conflict_rejected -and $contract.geometric_overlaps_rejected_pending_r2_024 -and -not $contract.intersections_r2_024 -and $contract.out_of_bounds_is_rejected_without_clamp) 'Fail-closed candidate claim mismatch'
Require ($contract.accepted_history_entries_per_command -eq 1 -and $contract.undo_redo_exact -and $contract.save_reopen_exact) 'Persistence claim mismatch'
Require (-not $contract.legacy_v1_v2_v3_v4_v5_snapshots_rewritten -and $contract.legacy_embedded_terrain_vegetation_density_path_road_hashes_preserved -and $contract.migrated_workspace_hash_changes_intentionally) 'Legacy compatibility claim mismatch'
Require ($contract.river_only_effective_terrain_refresh_is_partial -and $contract.dependent_terrain_vegetation_path_road_projections_use_effective_terrain -and $contract.vegetation_promotion_resolves_visible_effective_terrain_instance) 'Effective terrain integration claim mismatch'
$expectedOffloads = @('promote_vegetation_instance', 'create_path', 'redraw_path', 'upsert_road', 'upsert_river')
Require (@($contract.blocking_generation_commands_offloaded_from_async_executor).Count -eq 5 -and @(Compare-Object -CaseSensitive $expectedOffloads @($contract.blocking_generation_commands_offloaded_from_async_executor)).Count -eq 0) 'Async blocking-command boundary claim mismatch'
Require ($contract.old_projection_visible_during_staging -and $contract.terrain_and_water_swap_together -and $contract.unchanged_river_mesh_handles_reused -and $contract.river_removal_occurs_only_at_atomic_swap -and $contract.superseded_staging_is_discarded_without_leak -and $contract.stale_or_invalid_request_preserves_presented_projection -and -not $contract.uses_external_water_assets -and -not $contract.native_visual_review_performed) 'Viewport or visual boundary claim mismatch'

$expectedReceipts = @(
    @{ id = 'forge_project_tests'; command = 'cargo test -p forge_project --lib'; status = 'PASS'; passed = 137 },
    @{ id = 'forge_world_tests'; command = 'cargo test -p forge_world --lib'; status = 'PASS'; passed = 120 },
    @{ id = 'forge_engine_tests_lib'; command = 'cargo test -p forge_engine --lib'; status = 'PASS'; passed = 190 },
    @{ id = 'forge_studio_tests_lib'; command = 'cargo test -p forge_studio --lib'; status = 'PASS'; passed = 117 },
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
Require ($manifest.automated_validation.full_library_suite_tests_passed -eq (137 + 120 + 190 + 117)) 'Full-library suite total mismatch'
Require ($manifest.automated_validation.studio_binary_tests_passed_separately -eq 6) 'Studio binary count mismatch'
Require (@($receipts | Where-Object id -CEQ 'forge_studio_tests_bin')[0].excluded_from_full_library_suite_total) 'Studio binary tests must be excluded from the 564 library total'
foreach ($id in @('forge_engine_clippy_wasm_lib', 'forge_project_world_clippy_wasm_lib')) {
    Require (@($receipts | Where-Object id -CEQ $id)[0].scope_note -match '(library|libraries) only' -and @($receipts | Where-Object id -CEQ $id)[0].scope_note -match 'no all-targets WASM verdict') "WASM receipt scope is overstated: $id"
}

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
Require ($report -match 'polylignes de contrôle' -and $report -match "Ce n’est pas une borne de longueur d’arc") 'Report does not distinguish control-polyline length from spline arc length'
Require ($report -match 'corde horizontale' -and $report -match "ce plafond ne constitue pas une borne de\s+longueur d’arc") 'Report does not distinguish horizontal chord from arc length'
Require ($report -match 'terrain effectif' -and $report -match 'jamais persisté' -and $report -match "aucun échantillon n’est relevé") 'Report hides the non-destructive effective-terrain boundary'
Require ($report -match 'résolveur de promotion' -and $report -match 'worker bloquant') 'Report omits effective promotion or executor integration'
Require ($report -match 'Aucun asset, contenu Cadle, manifeste Cargo, lockfile ou dépendance') 'Report does not state the exact source/dependency boundary'

Write-Output "PASS: R2-023 evidence is bound to commit $($expectedSourceCommit.Substring(0, 8)), tree $($expectedSourceTree.Substring(0, 8)) and its exact 27-file inventory."
Write-Output 'PASS: 564 library tests plus 6 separate Studio binary tests; strict declared native/WASM product checks; Trunk debug build.'
Write-Output 'PASS: workspace v6 / forge.river v1, non-destructive effective terrain, visible promotion resolution and atomic terrain-water swap.'
Write-Output 'NOT RUN: native UI, Release builds, GPU, performance, remote CI (infrastructure billing) and PresentMon.'
