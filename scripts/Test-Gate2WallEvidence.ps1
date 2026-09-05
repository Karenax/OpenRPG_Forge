#requires -Version 7.5
[CmdletBinding()]
param()

# Read-only validation bound to one immutable source commit. It never launches Studio or a build and
# cannot infer a native visual, Release, GPU, PresentMon, remote CI or performance verdict.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$reportPath = Join-Path $repo 'docs/evidence/gate2/PROCEDURAL_WALLS.md'
$manifestPath = Join-Path $repo 'docs/evidence/gate2/PROCEDURAL_WALLS_ARTIFACTS.json'
$expectedSourceCommit = 'e31f822faa5fbe612017ff1efcf206a76a86038b'
$expectedSourceTree = '2416fec8684dd08fb4bc86824cf75a219b08ff8b'
$expectedBaseCommit = 'c6024e5045d4faa846834454aa8c08eee8a20d29'

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
Require ($manifest.schema -ceq 'forge.gate2.procedural-walls-evidence') 'Wrong evidence schema'
Require ($manifest.schema_version -eq 1) 'Wrong evidence version'
Require ($manifest.source_commit -ceq $expectedSourceCommit) 'Wrong source candidate'
Require ($manifest.source_tree -ceq $expectedSourceTree) 'Wrong source tree'
Require ($manifest.base_commit -ceq $expectedBaseCommit) 'Wrong source parent'
Require ($manifest.routes.Count -eq 1 -and $manifest.routes[0].id -ceq 'R2-025' -and $manifest.routes[0].status -ceq 'PASS') 'R2-025 is not the sole PASS route'
Require ($manifest.next_route -ceq 'R2-026') 'Wrong next route'

$metadata = @(& git -C $repo show -s --format='%H%n%T%n%P' $expectedSourceCommit)
Require ($LASTEXITCODE -eq 0 -and $metadata.Count -eq 3) 'Cannot read source commit metadata'
Require ($metadata[0] -ceq $expectedSourceCommit) 'Source commit is missing or ambiguous'
Require ($metadata[1] -ceq $expectedSourceTree) 'Source tree mismatch'
Require ($metadata[2] -ceq $expectedBaseCommit) 'Source parent mismatch'
$base = (& git -C $repo rev-parse "$expectedBaseCommit^{commit}").Trim()
Require ($LASTEXITCODE -eq 0 -and $base -ceq $expectedBaseCommit) 'Base commit is missing or ambiguous'

$expectedChangedWithStatus = @(
    "M`tapps/forge_studio/src/area_preview.rs",
    "M`tapps/forge_studio/src/area_preview_cache_tests.rs",
    "M`tapps/forge_studio/src/main.rs",
    "M`tapps/forge_studio/src/path_ui.rs",
    "M`tapps/forge_studio/src/workspace_api.rs",
    "M`tapps/forge_studio/src/workspace_service.rs",
    "M`tapps/forge_studio/src/workspace_service_tests.rs",
    "M`tapps/forge_studio/src/world_ui.rs",
    "M`tcrates/forge_engine/src/editor_partial_tests.rs",
    "M`tcrates/forge_engine/src/editor_river_tests.rs",
    "M`tcrates/forge_engine/src/editor_viewport.rs",
    "A`tcrates/forge_engine/src/editor_wall.rs",
    "A`tcrates/forge_engine/src/editor_wall_tests.rs",
    "M`tcrates/forge_project/src/lib.rs",
    "A`tcrates/forge_project/src/wall.rs",
    "M`tcrates/forge_project/src/workspace.rs",
    "M`tcrates/forge_project/src/workspace_edit.rs",
    "M`tcrates/forge_project/src/workspace_edit_tests.rs",
    "M`tcrates/forge_project/src/workspace_legacy.rs",
    "M`tcrates/forge_world/src/lib.rs",
    "M`tcrates/forge_world/src/path.rs",
    "M`tcrates/forge_world/src/river.rs",
    "M`tcrates/forge_world/src/terrain_brush_tests.rs",
    "A`tcrates/forge_world/src/wall.rs",
    "A`tcrates/forge_world/src/wall_tests.rs"
)
$expectedChanged = @($expectedChangedWithStatus | ForEach-Object { ($_ -split "`t", 2)[1] })
$changedWithStatus = @(& git -C $repo diff-tree --no-commit-id --name-status -r $expectedSourceCommit)
Require ($LASTEXITCODE -eq 0) 'Cannot enumerate source commit changes'
Require ($changedWithStatus.Count -eq 25) 'Unexpected source file count'
Require (@(Compare-Object -CaseSensitive $expectedChangedWithStatus $changedWithStatus).Count -eq 0) 'Unexpected source file/status inventory'
$manifestChanged = @($manifest.source_change_boundary.changed_files)
Require ($manifestChanged.Count -eq 25 -and @(Compare-Object -CaseSensitive $expectedChanged $manifestChanged).Count -eq 0) 'Manifest source inventory mismatch'
Require ($manifest.source_change_boundary.changed_file_count -eq 25) 'Manifest source file count mismatch'
Require (@($expectedChanged | Where-Object { $_ -match '(^|/)(assets|upstream/cadle)(/|$)' }).Count -eq 0) 'Source unexpectedly changes an asset or Cadle path'
Require (@($expectedChanged | Where-Object { $_ -match '(^|/)Cargo\.(toml|lock)$' }).Count -eq 0) 'Source unexpectedly changes Cargo metadata'
Require ($manifest.source_change_boundary.cargo_manifests_or_lockfiles_changed -eq 0 -and $manifest.source_change_boundary.runtime_dependencies_changed -eq 0 -and $manifest.source_change_boundary.dev_dependencies_changed -eq 0 -and $manifest.source_change_boundary.new_third_party_packages_or_versions -eq 0) 'Dependency boundary claim mismatch'
Require ($manifest.source_change_boundary.assets_changed -eq 0 -and $manifest.source_change_boundary.cadle_paths_or_content_changed -eq 0) 'Asset or Cadle boundary claim mismatch'

$projectWall = SourceText 'crates/forge_project/src/wall.rs'
$workspace = SourceText 'crates/forge_project/src/workspace.rs'
$workspaceEdit = SourceText 'crates/forge_project/src/workspace_edit.rs'
$projectTests = SourceText 'crates/forge_project/src/workspace_edit_tests.rs'
$worldPath = SourceText 'crates/forge_world/src/path.rs'
$worldRiver = SourceText 'crates/forge_world/src/river.rs'
$worldWall = SourceText 'crates/forge_world/src/wall.rs'
$worldWallTests = SourceText 'crates/forge_world/src/wall_tests.rs'
$engineWall = SourceText 'crates/forge_engine/src/editor_wall.rs'
$engineWallTests = SourceText 'crates/forge_engine/src/editor_wall_tests.rs'
$engineViewport = SourceText 'crates/forge_engine/src/editor_viewport.rs'
$areaPreview = SourceText 'apps/forge_studio/src/area_preview.rs'
$areaPreviewTests = SourceText 'apps/forge_studio/src/area_preview_cache_tests.rs'
$studioService = SourceText 'apps/forge_studio/src/workspace_service.rs'
$studioServiceTests = SourceText 'apps/forge_studio/src/workspace_service_tests.rs'
$studioPathUi = SourceText 'apps/forge_studio/src/path_ui.rs'

Require ($workspace -match '(?m)^pub const WORKSPACE_SCHEMA_VERSION: u32 = 7;$') 'Workspace schema must be v7'
Require ($projectWall -match '(?m)^pub const WALL_DOCUMENT_SCHEMA: &str = "forge\.wall";$' -and $projectWall -match '(?m)^pub const WALL_DOCUMENT_SCHEMA_VERSION: u32 = 1;$') 'Wall document v1 is missing'
Require ($projectWall -match '(?m)^pub const MAX_WALLS_PER_DOCUMENT: usize = 64;$' -and $projectWall -match '(?m)^pub const MAX_WALL_DOCUMENT_JSON_BYTES: usize = 64 \* 1_024;$' -and $projectWall -match '(?m)^pub const MAX_TOTAL_WALL_LENGTH_M: f64 = 8_192\.0;$') 'Wall document budgets mismatch'
Require ($projectWall -match '(?m)^pub const MIN_WALL_HEIGHT_M: f32 = 0\.25;$' -and $projectWall -match '(?m)^pub const MAX_WALL_HEIGHT_M: f32 = 8\.0;$' -and $projectWall -match '(?m)^pub const MIN_WALL_THICKNESS_M: f32 = 0\.10;$' -and $projectWall -match '(?m)^pub const MAX_WALL_THICKNESS_M: f32 = 2\.0;$') 'Wall profile bounds mismatch'
Require ($projectWall -match 'pub struct WallDocument' -and $projectWall -match 'pub struct AuthoredWall' -and $projectWall -match 'TerrainDrapedSolidV1' -and $projectWall -match 'ForgeStoneV1') 'Wall source types are incomplete'
Require ($workspaceEdit -match 'UpsertWall \{' -and $workspaceEdit -match 'expected_wall_document_hash' -and $workspaceEdit -match 'WallRoadConflict' -and $workspaceEdit -match 'WallRiverConflict') 'Atomic wall edit or same-path conflicts are missing'

Require ($worldWall -match '(?m)^pub const WALL_GENERATION_VERSION: u32 = 1;$' -and $worldWall -match '(?m)^pub const WALL_BASE_EMBED_M: f32 = 0\.02;$') 'Wall generation version or embed mismatch'
Require ($worldWall -match '(?m)^pub const WALL_VERTICES_PER_ROW: usize = 8;$' -and $worldWall -match '(?m)^pub const WALL_CAP_VERTEX_COUNT: usize = 8;$' -and $worldWall -match '(?m)^pub const WALL_INDICES_PER_LONGITUDINAL_INTERVAL: usize = 24;$' -and $worldWall -match '(?m)^pub const WALL_CAP_INDEX_COUNT: usize = 12;$') 'Wall topology constants mismatch'
Require ($worldWall -match '(?m)^pub const MAX_GENERATED_WALL_LONGITUDINAL_SAMPLES: usize = 16_384;$' -and $worldWall -match '(?m)^pub const MAX_GENERATED_WALL_VERTICES: usize = 131_584;$' -and $worldWall -match '(?m)^pub const MAX_GENERATED_WALL_INDICES: usize = 393_204;$') 'Wall geometry budgets mismatch'
Require ($worldWall -match 'pub struct GeneratedWall' -and $worldWall -match 'pub struct GeneratedWalls' -and $worldWall -match 'pub struct WallGenerationPlan') 'Generated wall contract is missing'
foreach ($field in @('terrain_document_hash', 'path_document_hash', 'wall_document_hash')) {
    Require ($worldWall -match [regex]::Escape("pub $field")) "Generated wall hash missing: $field"
}
Require ($worldWall -match 'left_base, left_top, left_top, right_top, right_top, right_base, right_base, left_base' -and $worldWall -match 'surface\.height_m - WALL_BASE_EMBED_M') 'Terrain-draped row construction mismatch'
Require ($worldWall -match 'append_caps' -and $worldWall -match 'build_indices' -and $worldWall -match 'build_normals') 'Solid topology or normal generation is missing'
Require ($worldWall -match 'pub fn validate_generated_walls' -and $worldWall -match 'generated wall set is not exhaustive' -and $worldWall -match 'same_vec3_slice_bits') 'Exact exhaustive wall validator is missing'
Require ($worldWall -match 'pub struct WallUpsertCommand' -and $worldWall -match 'pub fn preview_upsert_wall') 'Wall command boundary is missing'
Require ($worldPath -match 'let river_projection = crate::river::validate_candidate_rivers' -and $worldPath -match 'validate_candidate_walls_with_river_projection') 'Path validation does not reuse its river projection for walls'
Require ($worldRiver -match 'let river_projection = validate_candidate_rivers' -and $worldRiver -match 'validate_candidate_walls_with_river_projection') 'River validation does not reuse its river projection for walls'
Require ($worldWall -match 'if area\.walls\.walls\.is_empty\(\)' -and $worldWall -match 'validate_candidate_walls_with_river_projection') 'Empty-wall fast path is missing'

Require ($engineWall -match 'validate_generated_walls_against_sources' -and $engineWall -match 'source_walls\.walls\.len\(\) == generated\.walls\.len\(\)' -and $engineWall -match 'validate_wall_indices' -and $engineWall -match 'validate_wall_normals') 'Engine exact wall validation is incomplete'
$meshBuilder = [regex]::Match(
    $engineWall,
    '(?s)pub\(crate\) fn build_editor_wall_mesh.*?pub\(crate\) fn procedural_editor_wall_material'
).Value
Require ($meshBuilder -match 'Mesh::ATTRIBUTE_POSITION' -and $meshBuilder -match 'Mesh::ATTRIBUTE_NORMAL' -and $meshBuilder -notmatch 'Mesh::ATTRIBUTE_UV_0') 'Wall mesh must be procedural and UV-free'
Require ($engineWall -match 'base_color: Color::srgb\(0\.34, 0\.32, 0\.29\)' -and $engineWall -match 'perceptual_roughness: 0\.94') 'ForgeStoneV1 procedural material is missing'
Require ($engineViewport -match 'pub struct EditorWallRequest' -and $engineViewport -match 'pub source_walls: WallDocument' -and $engineViewport -match 'wall_rebuilt_meshes' -and $engineViewport -match 'wall_retained_meshes') 'Viewport wall request or cache counters are missing'
Require ($areaPreview -match 'pub walls: GeneratedWalls' -and $areaPreview -match 'WallGenerationPlan::from_documents\(&effective_area\.terrain, &area\.paths, &area\.walls\)') 'AreaPreview effective-terrain wall projection is missing'
Require ($studioService -match 'WorkspaceCommand::UpsertWall' -and $studioService -match 'execute_wall_upsert' -and $studioService -match 'forge_world::preview_upsert_wall') 'Studio wall command is not wired to world validation'
Require ($studioPathUi -match 'WallProfileForm' -and $studioPathUi -match 'Mettre à jour le mur' -and $studioPathUi -match 'contacts entre objets portés par des chemins distincts sont autorisés') 'Studio wall form or contact boundary is missing'

$requiredTests = @(
    @{ source = $projectWall; name = 'canonical_round_trip_and_hash_are_stable' },
    @{ source = $projectTests; name = 'workspace_v6_migration_adds_only_empty_walls_and_preserves_embedded_hashes' },
    @{ source = $projectTests; name = 'upsert_wall_creates_edits_sorts_reopens_and_replays_from_its_inverse' },
    @{ source = $projectTests; name = 'wall_targets_reject_closed_paths_and_road_or_river_conflicts_in_both_orders' },
    @{ source = $projectTests; name = 'distinct_path_contacts_between_road_river_and_wall_are_allowed' },
    @{ source = $worldWallTests; name = 'wall_command_roundtrip_undo_redo_and_generation_are_deterministic' },
    @{ source = $worldWallTests; name = 'generated_validator_is_exact_exhaustive_and_canonical' },
    @{ source = $worldWallTests; name = 'river_effective_terrain_is_used_and_revalidated_without_persisting_it' },
    @{ source = $worldWallTests; name = 'empty_wall_validation_returns_before_effective_terrain_is_inspected' },
    @{ source = $worldWallTests; name = 'distinct_geometry_contacts_are_allowed_and_same_path_roles_are_rejected' },
    @{ source = $engineWall; name = 'valid_walls_build_an_opaque_procedural_mesh_without_uvs' },
    @{ source = $engineWall; name = 'exact_source_validation_rejects_coherent_translation_and_orientation_forgery' },
    @{ source = $engineWallTests; name = 'wall_only_update_is_atomic_and_reuses_unchanged_mesh_handles' },
    @{ source = $engineWallTests; name = 'coherently_translated_wall_payload_preserves_the_presented_scene' },
    @{ source = $studioServiceTests; name = 'wall_profile_is_one_revision_and_survives_redraw_history_and_reopen' },
    @{ source = $studioServiceTests; name = 'wall_profile_and_same_path_conflict_rejections_are_atomic' },
    @{ source = $studioServiceTests; name = 'wall_and_road_on_distinct_crossing_paths_are_accepted_without_auto_junction' },
    @{ source = $studioServiceTests; name = 'opening_v2_through_v6_adds_new_area_documents_without_rewriting_legacy_snapshots' },
    @{ source = $areaPreviewTests; name = 'wall_only_edit_reuses_every_non_wall_projection_and_zero_terrain_chunks' },
    @{ source = $areaPreviewTests; name = 'river_edit_rebuilds_wall_bases_against_the_new_effective_terrain' }
)
foreach ($test in $requiredTests) {
    Require ($test.source -match [regex]::Escape($test.name)) "Required source/test missing: $($test.name)"
}

$contract = $manifest.functional_contract
Require ($contract.workspace_schema_version_previous -eq 6 -and $contract.workspace_schema_version_current -eq 7 -and $contract.wall_schema -ceq 'forge.wall' -and $contract.wall_schema_version -eq 1) 'Persistent wall schema claim mismatch'
Require ($contract.maximum_walls_per_document -eq 64 -and $contract.maximum_wall_document_json_bytes -eq 65536 -and $contract.maximum_total_control_polyline_length_m -eq 8192 -and -not $contract.control_polyline_length_budget_is_arc_length) 'Wall source budgets claim mismatch'
Require ($contract.minimum_wall_height_m -eq 0.25 -and $contract.maximum_wall_height_m -eq 8 -and $contract.minimum_wall_thickness_m -eq 0.1 -and $contract.maximum_wall_thickness_m -eq 2) 'Wall profile claim mismatch'
Require ($contract.wall_profile_algorithm -ceq 'terrain_draped_solid_v1' -and $contract.wall_surface -ceq 'forge_stone_v1' -and $contract.wall_generation_version -eq 1) 'Wall recipe claim mismatch'
Require ($contract.requires_existing_open_path -and $contract.same_path_wall_road_conflict_rejected -and $contract.same_path_wall_river_conflict_rejected -and $contract.distinct_path_wall_road_river_contacts_allowed -and -not $contract.automatic_intersections_or_openings_created) 'Wall path/contact boundary claim mismatch'
Require ($contract.wall_base_embed_m -eq 0.02 -and $contract.vertices_per_path_sample -eq 8 -and $contract.cap_vertices_per_wall -eq 8 -and $contract.longitudinal_bands -eq 4 -and $contract.indices_per_longitudinal_interval -eq 24 -and $contract.cap_indices_per_wall -eq 12) 'Wall topology claim mismatch'
Require ($contract.maximum_generated_longitudinal_samples -eq 16384 -and $contract.maximum_generated_vertices -eq 131584 -and $contract.maximum_generated_indices -eq 393204) 'Wall generated budgets claim mismatch'
Require ($contract.generated_walls_sorted_and_exhaustive -and $contract.exact_source_replay_validation -and $contract.positions_and_normals_bit_exact -and $contract.bases_sample_effective_terrain) 'Exact generated wall claim mismatch'
Require ($contract.path_redraw_revalidates_walls -and $contract.river_upsert_revalidates_walls -and $contract.path_and_river_commands_reuse_river_generation_result -and $contract.empty_wall_validation_skips_wall_river_projection) 'Dependent validation or no-redundant-work claim mismatch'
Require ($contract.accepted_history_entries_per_command -eq 1 -and $contract.undo_redo_exact -and $contract.save_reopen_exact) 'History or persistence claim mismatch'
Require (-not $contract.legacy_v1_through_v6_snapshots_rewritten -and $contract.legacy_embedded_document_hashes_preserved -and $contract.migrated_workspace_hash_changes_intentionally) 'Legacy migration claim mismatch'
Require ($contract.wall_only_effective_terrain_chunks_sent -eq 0 -and $contract.wall_only_non_wall_projections_reused -and $contract.unchanged_wall_mesh_handles_reused -and $contract.river_edit_rebuilds_wall_bases_on_effective_terrain) 'Wall cache claim mismatch'
Require ($contract.viewport_validates_effective_terrain_and_canonical_path_wall_sources -and $contract.viewport_replays_exact_world_wall_validation -and $contract.stale_forged_or_conflicting_payload_preserves_presented_scene) 'Viewport validation claim mismatch'
Require ($contract.blocking_wall_command_offloaded_from_async_executor -and -not $contract.wall_material_uses_external_texture_or_uv -and -not $contract.uses_external_assets -and -not $contract.native_visual_review_performed) 'Worker, asset or visual boundary claim mismatch'

$expectedReceipts = @(
    @{ id = 'forge_project_tests'; command = 'cargo test -p forge_project --lib'; status = 'PASS'; passed = 149 },
    @{ id = 'forge_world_tests'; command = 'cargo test -p forge_world --lib'; status = 'PASS'; passed = 133 },
    @{ id = 'forge_engine_tests_lib'; command = 'cargo test -p forge_engine --lib'; status = 'PASS'; passed = 201 },
    @{ id = 'forge_studio_tests_lib'; command = 'cargo test -p forge_studio --lib'; status = 'PASS'; passed = 125 },
    @{ id = 'forge_studio_tests_bin'; command = 'cargo test -p forge_studio --bin forge-studio'; status = 'PASS'; passed = 6 },
    @{ id = 'forge_studio_check_wasm'; command = 'cargo check -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web'; status = 'PASS' },
    @{ id = 'forge_studio_clippy_native'; command = 'cargo clippy -p forge_studio --all-targets -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_studio_clippy_wasm'; command = 'cargo clippy -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_engine_clippy_native'; command = 'cargo clippy -p forge_engine --all-targets --all-features -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_engine_clippy_wasm_lib'; command = 'cargo clippy -p forge_engine --lib --target wasm32-unknown-unknown --no-default-features --features web -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_project_clippy_native'; command = 'cargo clippy -p forge_project --all-targets -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_world_clippy_native'; command = 'cargo clippy -p forge_world --all-targets -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_project_world_clippy_wasm_lib'; command = 'cargo clippy -p forge_project -p forge_world --lib --target wasm32-unknown-unknown -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_studio_trunk_debug'; command = 'cmd /d /c "set NO_COLOR=true&&trunk build --locked --offline"'; status = 'PASS' },
    @{ id = 'cargo_fmt'; command = 'cargo fmt --all -- --check'; status = 'PASS' },
    @{ id = 'git_diff_check'; command = 'git diff --check'; status = 'PASS' }
)
$receipts = @($manifest.automated_validation.receipts)
Require ($manifest.automated_validation.raw_log_bundle -ceq 'NOT CAPTURED' -and $manifest.artifacts.Count -eq 0) 'Evidence must not invent a raw log bundle'
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
Require ($manifest.automated_validation.full_library_suite_tests_passed -eq (149 + 133 + 201 + 125)) 'Full-library suite total mismatch'
Require ($manifest.automated_validation.studio_binary_tests_passed_separately -eq 6) 'Studio binary count mismatch'
Require (@($receipts | Where-Object id -CEQ 'forge_studio_tests_bin')[0].excluded_from_full_library_suite_total) 'Studio binary tests must be excluded from the 608 library total'
foreach ($id in @('forge_engine_clippy_wasm_lib', 'forge_project_world_clippy_wasm_lib')) {
    $scope = @($receipts | Where-Object id -CEQ $id)[0].scope_note
    Require ($scope -match '(library|libraries) only' -and $scope -match 'no all-targets WASM verdict') "WASM receipt scope is overstated: $id"
}
Require (@($receipts | Where-Object id -CEQ 'forge_project_clippy_native')[0].scope_note -match 'forge_project sources remained unchanged') 'Project native receipt does not disclose its carry-forward boundary'

$recovery = $manifest.automated_validation.trunk_debug_recovery
Require ($recovery.prebuild_rejections -eq 2 -and $recovery.prebuild_environment_value -ceq 'NO_COLOR=1' -and $recovery.prebuild_outcome -ceq 'REJECTED BEFORE BUILD') 'Trunk pre-build rejection history mismatch'
Require ($recovery.actual_build_attempts -eq 2 -and $recovery.first_build_attempt -ceq 'FAIL — no space left on device' -and $recovery.recovery_command -ceq 'cargo clean -p forge_studio --target wasm32-unknown-unknown' -and $recovery.removed_files -eq 1259 -and $recovery.reclaimed_gib -eq 3.4 -and $recovery.second_build_attempt -ceq 'PASS' -and $recovery.raw_log -ceq 'NOT CAPTURED') 'Trunk build recovery history mismatch'
$trunkReceipt = @($receipts | Where-Object id -CEQ 'forge_studio_trunk_debug')[0]
Require ($trunkReceipt.prebuild_rejections -eq 2 -and $trunkReceipt.actual_build_attempts -eq 2 -and $trunkReceipt.first_build_attempt_environment_failure -ceq 'no space left on device' -and $trunkReceipt.recovery_command -ceq $recovery.recovery_command -and $trunkReceipt.removed_files -eq 1259 -and $trunkReceipt.reclaimed_gib -eq 3.4) 'Trunk receipt hides recovered attempts'

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
Require ($report -match 'TerrainDrapedSolidV1' -and $report -match 'ForgeStoneV1' -and $report -match '0,02 m') 'Report hides the wall recipe or terrain embed'
Require ($report -match '16 384 rangées' -and $report -match '131 584 sommets' -and $report -match '393 204 indices') 'Report hides aggregate geometry budgets'
Require ($report -match 'Deux premières invocations Trunk' -and $report -match 'NO_COLOR=1' -and $report -match '1 259 fichiers' -and $report -match '3,4 Gio') 'Report hides Trunk environmental recovery history'
Require ($report -match 'Aucun `Cargo\.toml`, `Cargo\.lock`, asset ou chemin/contenu\s+Cadle') 'Report does not state the exact Cargo/asset/Cadle boundary'

Write-Output "PASS: R2-025 evidence is bound to commit $($expectedSourceCommit.Substring(0, 8)), tree $($expectedSourceTree.Substring(0, 8)) and its exact 25-file inventory."
Write-Output 'PASS: 608 library tests plus 6 separate Studio binary tests; strict declared native/WASM product checks; recovered Trunk debug build.'
Write-Output 'PASS: persistent bounded TerrainDrapedSolidV1 walls, exact effective-terrain geometry, atomic history and cache/viewport reuse.'
Write-Output 'NOT RUN: native UI, Release builds, GPU, performance, remote CI (infrastructure billing) and PresentMon.'
