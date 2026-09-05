#requires -Version 7.5
[CmdletBinding()]
param()

# Read-only validation bound to one immutable source commit. It never launches Studio and cannot
# infer a native visual, Release, GPU, PresentMon, remote CI or performance verdict.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$reportPath = Join-Path $repo 'docs/evidence/gate2/ROAD_PROFILE_AUTHORING.md'
$manifestPath = Join-Path $repo 'docs/evidence/gate2/ROAD_PROFILE_AUTHORING_ARTIFACTS.json'
$expectedSourceCommit = 'e117d834b6ecdb6a283238e931631181b88f8ee6'
$expectedSourceTree = '1df4af8c05881cb59993385b9f275b0fcb71a2b3'
$expectedBaseCommit = '7995ab1a9d82d1462d98aecc0abbf2a2aebb60ff'

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
Require ($manifest.schema -ceq 'forge.gate2.road-profile-authoring-evidence') 'Wrong evidence schema'
Require ($manifest.schema_version -eq 1) 'Wrong evidence version'
Require ($manifest.source_commit -ceq $expectedSourceCommit) 'Wrong source candidate'
Require ($manifest.source_tree -ceq $expectedSourceTree) 'Wrong source tree'
Require ($manifest.base_commit -ceq $expectedBaseCommit) 'Wrong source parent'
Require ($manifest.routes.Count -eq 1 -and $manifest.routes[0].id -ceq 'R2-022' -and $manifest.routes[0].status -ceq 'PASS') 'R2-022 is not the sole PASS route'
Require ($manifest.next_route -ceq 'R2-023') 'Wrong next route'

$base = (& git -C $repo rev-parse "$expectedBaseCommit^{commit}").Trim()
Require ($LASTEXITCODE -eq 0 -and $base -ceq $expectedBaseCommit) 'Base commit is missing or ambiguous'
$commit = (& git -C $repo rev-parse "$expectedSourceCommit^{commit}").Trim()
Require ($LASTEXITCODE -eq 0 -and $commit -ceq $expectedSourceCommit) 'Source commit is missing or ambiguous'
$tree = (& git -C $repo show -s --format=%T $expectedSourceCommit).Trim()
Require ($LASTEXITCODE -eq 0 -and $tree -ceq $expectedSourceTree) 'Source tree mismatch'
$parent = (& git -C $repo show -s --format=%P $expectedSourceCommit).Trim()
Require ($LASTEXITCODE -eq 0 -and $parent -ceq $expectedBaseCommit) 'Source parent mismatch'

$expectedChangedWithStatus = @(
    "M`tCargo.lock",
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
    "A`tcrates/forge_engine/src/editor_road.rs",
    "M`tcrates/forge_engine/src/editor_viewport.rs",
    "M`tcrates/forge_project/README.md",
    "M`tcrates/forge_project/src/lib.rs",
    "A`tcrates/forge_project/src/road.rs",
    "M`tcrates/forge_project/src/workspace.rs",
    "M`tcrates/forge_project/src/workspace_edit.rs",
    "M`tcrates/forge_project/src/workspace_edit_tests.rs",
    "M`tcrates/forge_project/src/workspace_legacy.rs",
    "M`tcrates/forge_world/Cargo.toml",
    "M`tcrates/forge_world/src/lib.rs",
    "M`tcrates/forge_world/src/path.rs",
    "A`tcrates/forge_world/src/road.rs",
    "A`tcrates/forge_world/src/road_tests.rs",
    "M`tcrates/forge_world/src/terrain_brush_tests.rs"
)
$expectedChanged = @($expectedChangedWithStatus | ForEach-Object { ($_ -split "`t", 2)[1] })
$changedWithStatus = @(& git -C $repo diff-tree --no-commit-id --name-status -r $expectedSourceCommit)
Require ($LASTEXITCODE -eq 0) 'Cannot enumerate source commit changes'
Require ($changedWithStatus.Count -eq 26) 'Unexpected source file count'
Require (@(Compare-Object -CaseSensitive $expectedChangedWithStatus $changedWithStatus).Count -eq 0) 'Unexpected source file/status inventory'
$manifestChanged = @($manifest.source_change_boundary.changed_files)
Require ($manifestChanged.Count -eq 26 -and @(Compare-Object -CaseSensitive $expectedChanged $manifestChanged).Count -eq 0) 'Manifest source inventory mismatch'
Require ($manifest.source_change_boundary.changed_file_count -eq 26) 'Manifest source file count mismatch'
Require (@($expectedChanged | Where-Object { $_ -match '(^|/)(assets|upstream/cadle)(/|$)' }).Count -eq 0) 'Source unexpectedly changes an asset or Cadle path'

$cargoMetadata = @($expectedChanged | Where-Object { $_ -match '(^|/)Cargo\.(toml|lock)$' })
Require ($cargoMetadata.Count -eq 2 -and $cargoMetadata -ccontains 'Cargo.lock' -and $cargoMetadata -ccontains 'crates/forge_world/Cargo.toml') 'Unexpected Cargo metadata boundary'
Require ($manifest.source_change_boundary.cargo_manifests_or_lockfiles_changed -eq 2 -and $manifest.source_change_boundary.runtime_dependencies_changed -eq 0 -and $manifest.source_change_boundary.dev_dependencies_changed -eq 1 -and $manifest.source_change_boundary.new_third_party_packages_or_versions -eq 0) 'Cargo dependency boundary claim mismatch'
Require ($manifest.source_change_boundary.assets_changed -eq 0 -and $manifest.source_change_boundary.cadle_paths_or_content_changed -eq 0) 'Asset or Cadle boundary claim mismatch'
$cargoDiff = @(& git -C $repo diff --unified=0 $expectedBaseCommit $expectedSourceCommit -- Cargo.lock crates/forge_world/Cargo.toml) -join "`n"
Require ($LASTEXITCODE -eq 0) 'Cannot inspect Cargo metadata diff'
Require ($cargoDiff -match '(?m)^\+\[dev-dependencies\]$' -and $cargoDiff -match '(?m)^\+serde_json\.workspace = true$') 'serde_json is not recorded as a forge_world dev dependency'
Require ($cargoDiff -match '(?m)^\+ "serde_json",$' -and $cargoDiff -notmatch '(?m)^\+name = ') 'Cargo.lock change is not limited to the existing serde_json package reference'

$workspace = SourceText 'crates/forge_project/src/workspace.rs'
$workspaceEdit = SourceText 'crates/forge_project/src/workspace_edit.rs'
$workspaceLegacy = SourceText 'crates/forge_project/src/workspace_legacy.rs'
$projectRoad = SourceText 'crates/forge_project/src/road.rs'
$projectTests = SourceText 'crates/forge_project/src/workspace_edit_tests.rs'
$worldRoad = SourceText 'crates/forge_world/src/road.rs'
$worldRoadTests = SourceText 'crates/forge_world/src/road_tests.rs'
$worldPath = SourceText 'crates/forge_world/src/path.rs'
$engineRoad = SourceText 'crates/forge_engine/src/editor_road.rs'
$engineViewport = SourceText 'crates/forge_engine/src/editor_viewport.rs'
$studioApi = SourceText 'apps/forge_studio/src/workspace_api.rs'
$studioService = SourceText 'apps/forge_studio/src/workspace_service.rs'
$studioServiceTests = SourceText 'apps/forge_studio/src/workspace_service_tests.rs'
$studioPathUi = SourceText 'apps/forge_studio/src/path_ui.rs'
$areaPreview = SourceText 'apps/forge_studio/src/area_preview.rs'
$areaPreviewTests = SourceText 'apps/forge_studio/src/area_preview_cache_tests.rs'
$studioWorldUi = SourceText 'apps/forge_studio/src/world_ui.rs'

Require ($workspace -match '(?m)^pub const WORKSPACE_SCHEMA_VERSION: u32 = 5;$' -and $workspace -match 'pub roads: RoadDocument') 'Workspace v5 road source is missing'
Require ($projectRoad -match '(?m)^pub const ROAD_DOCUMENT_SCHEMA: &str = "forge\.road";$' -and $projectRoad -match '(?m)^pub const ROAD_DOCUMENT_SCHEMA_VERSION: u32 = 1;$') 'Road schema v1 is missing'
Require ($projectRoad -match '(?m)^pub const MAX_ROADS_PER_DOCUMENT: usize = 64;$' -and $projectRoad -match '(?m)^pub const MAX_ROAD_DOCUMENT_JSON_BYTES: usize = 64 \* 1_024;$' -and $projectRoad -match '(?m)^pub const MAX_TOTAL_ROAD_LENGTH_M: f64 = 8_192\.0;$') 'Road collection, JSON or control-polyline budget mismatch'
Require ($projectRoad -match '(?m)^pub const MIN_ROAD_WIDTH_M: f32 = 1\.0;$' -and $projectRoad -match '(?m)^pub const MAX_ROAD_WIDTH_M: f32 = 16\.0;$' -and $projectRoad -match '(?m)^pub const MAX_ROAD_CROWN_HEIGHT_M: f32 = 0\.30;$' -and $projectRoad -match '(?m)^pub const MAX_ROAD_CROWN_TO_WIDTH_RATIO: f32 = 0\.05;$') 'Road profile bounds mismatch'
Require ($projectRoad -match 'TerrainDrapedCrownV1' -and $projectRoad -match 'CompactedEarthV1' -and $workspaceEdit -match 'UpsertRoad' -and $workspaceEdit -match 'expected_path_document_hash' -and $workspaceEdit -match 'expected_road_document_hash') 'Persistent road upsert contract is missing'
Require ($workspaceLegacy -match '(?m)^\s*4 => migrate_v4' -and $workspaceLegacy -match 'RoadDocument::new') 'Workspace v4 road migration is missing'

Require ($worldRoad -match '(?m)^pub const ROAD_GENERATION_VERSION: u32 = 1;$' -and $worldRoad -match '(?m)^pub const ROAD_MESH_CLEARANCE_M: f32 = 0\.04;$' -and $worldRoad -match '(?m)^pub const ROAD_MESH_MAX_LONGITUDINAL_CHORD_M: f32 = 2\.0;$') 'Road generation version, clearance or horizontal chord bound mismatch'
Require ($worldRoad -match '(?m)^pub const ROAD_CROSS_SECTION_VERTEX_COUNT: usize = 3;$' -and $worldRoad -match '(?m)^pub const MAX_GENERATED_ROAD_LONGITUDINAL_SAMPLES: usize = 16_384;$' -and $worldRoad -match 'MAX_GENERATED_ROAD_VERTICES' -and $worldRoad -match 'MAX_GENERATED_ROAD_INDICES') 'Road mesh budgets mismatch'
Require ($worldRoad -match 'pub struct GeneratedRoads' -and $worldRoad -match 'pub terrain_document_hash: ContentHash' -and $worldRoad -match 'pub path_document_hash: ContentHash' -and $worldRoad -match 'pub road_document_hash: ContentHash') 'Generated road source hashes are missing'
Require ($worldRoad -match 'pub fn preview_upsert_road' -and $worldRoad -match 'WorkspaceChange::UpsertRoad' -and $worldRoad -match 'FootprintOutOfBounds') 'Pure road command or footprint rejection is missing'
Require ($worldPath -match 'validate_candidate_roads') 'Path redraw does not revalidate road footprints'

Require ($engineViewport -match 'pub struct EditorRoadRequest' -and $engineViewport -match 'road::validate_generated_roads' -and $engineViewport -match 'road_mesh_uploads') 'Viewport road request, validation or counters are missing'
Require ($engineRoad -match '(?m)^const EARTH_MATERIAL_WEIGHTS: \[f32; 4\] = \[0\.0, 1\.0, 0\.0, 1\.0\];$' -and $engineRoad -match 'validate_road_geometry' -and $engineRoad -match 'RoadSurface::CompactedEarthV1') 'Procedural road material or geometry validation is missing'
Require ($studioApi -match 'UpsertRoad' -and $studioService -match 'execute_road_upsert' -and $studioService -match 'preview_upsert_road') 'Typed Studio road command or service is missing'
Require ($studioPathUi -match 'fn RoadProfileForm' -and $studioPathUi -match 'RoadProfileAlgorithm::TerrainDrapedCrownV1' -and $studioPathUi -match 'RoadSurface::CompactedEarthV1') 'Studio road profile form is missing'
Require ($areaPreview -match 'pub roads: GeneratedRoads' -and $studioWorldUi -match 'preview\.roads\.road_document_hash != road_hash') 'AreaPreview road projection or hash validation is missing'

$requiredTests = @(
    @{ source = $projectRoad; name = 'canonical_round_trip_and_hash_are_stable' },
    @{ source = $projectRoad; name = 'strict_json_rejects_unknown_schema_and_oversized_input' },
    @{ source = $projectRoad; name = 'identity_revision_name_and_schema_are_validated' },
    @{ source = $projectRoad; name = 'roads_are_unique_sorted_non_nil_and_bounded' },
    @{ source = $projectRoad; name = 'profile_numeric_limits_and_relation_are_enforced' },
    @{ source = $projectTests; name = 'workspace_v4_migration_adds_only_empty_roads_and_preserves_embedded_hashes' },
    @{ source = $projectTests; name = 'upsert_road_creates_edits_sorts_reopens_and_replays_from_its_inverse' },
    @{ source = $projectTests; name = 'upsert_road_rejects_noop_stale_hashes_missing_closed_and_invalid_atomically' },
    @{ source = $projectTests; name = 'upsert_road_rejects_missing_or_closed_targets_without_mutation' },
    @{ source = $projectTests; name = 'road_assignments_respect_collection_and_total_length_limits' },
    @{ source = $worldRoadTests; name = 'straight_road_is_three_points_four_triangles_and_exactly_terrain_draped' },
    @{ source = $worldRoadTests; name = 'generated_contract_is_strict_and_roads_are_canonical' },
    @{ source = $worldRoadTests; name = 'adaptive_mesh_resampling_bounds_chords_without_changing_the_path' },
    @{ source = $worldRoadTests; name = 'upsert_update_noop_and_context_rejections_are_atomic' },
    @{ source = $worldRoadTests; name = 'footprint_failure_and_path_redraw_revalidation_are_atomic' },
    @{ source = $worldRoadTests; name = 'generation_rejects_cross_document_identity_missing_and_closed_paths' },
    @{ source = $worldRoadTests; name = 'checked_budgets_and_invalid_triangles_fail_closed' },
    @{ source = $engineRoad; name = 'valid_roads_build_the_existing_terrain_material_vertex_contract' },
    @{ source = $engineRoad; name = 'validation_rejects_wrong_sources_order_profile_and_mesh_shape' },
    @{ source = $engineRoad; name = 'aggregate_vertex_and_index_budgets_fail_before_mesh_adaptation' },
    @{ source = $engineRoad; name = 'validation_rejects_reversed_cross_sections_and_degenerate_triangles' },
    @{ source = $engineViewport; name = 'road_only_update_is_atomic_and_reuses_unchanged_mesh_handles' },
    @{ source = $engineViewport; name = 'road_staging_keeps_old_projection_visible_until_complete_swap' },
    @{ source = $engineViewport; name = 'stale_or_invalid_road_update_preserves_presented_projection' },
    @{ source = $engineViewport; name = 'road_removal_occurs_only_at_swap_and_releases_its_mesh' },
    @{ source = $areaPreviewTests; name = 'road_only_edit_reuses_terrain_vegetation_and_path_projection' },
    @{ source = $studioServiceTests; name = 'road_profile_is_one_revision_and_survives_redraw_history_and_reopen' },
    @{ source = $studioServiceTests; name = 'road_profile_rejections_are_atomic' },
    @{ source = $studioServiceTests; name = 'opening_v2_v3_and_v4_adds_new_area_documents_without_rewriting_legacy_snapshots' }
)
foreach ($test in $requiredTests) {
    Require ($test.source -match [regex]::Escape($test.name)) "Required source/test missing: $($test.name)"
}

Require ($manifest.functional_contract.workspace_schema_version_previous -eq 4 -and $manifest.functional_contract.workspace_schema_version_current -eq 5) 'Wrong workspace migration claim'
Require ($manifest.functional_contract.road_schema -ceq 'forge.road' -and $manifest.functional_contract.road_schema_version -eq 1) 'Wrong road schema claim'
Require ($manifest.functional_contract.maximum_roads_per_document -eq 64 -and $manifest.functional_contract.maximum_road_document_json_bytes -eq 65536) 'Wrong road document budget claim'
Require ($manifest.functional_contract.minimum_road_width_m -eq 1 -and $manifest.functional_contract.maximum_road_width_m -eq 16 -and $manifest.functional_contract.maximum_road_crown_height_m -eq 0.30 -and $manifest.functional_contract.maximum_road_crown_to_width_ratio -eq 0.05) 'Wrong road profile bounds claim'
Require ($manifest.functional_contract.maximum_total_control_polyline_length_m -eq 8192 -and -not $manifest.functional_contract.control_polyline_length_budget_is_arc_length) 'Control-polyline length is incorrectly claimed as arc length'
Require ($manifest.functional_contract.road_generation_version -eq 1 -and $manifest.functional_contract.road_mesh_clearance_m -eq 0.04 -and $manifest.functional_contract.maximum_horizontal_chord_m -eq 2 -and -not $manifest.functional_contract.horizontal_chord_bound_is_arc_length) 'Generated road spacing claim mismatch'
Require ($manifest.functional_contract.cross_section_vertices_per_row -eq 3 -and $manifest.functional_contract.triangles_per_longitudinal_interval -eq 4 -and $manifest.functional_contract.maximum_generated_longitudinal_samples -eq 16384 -and $manifest.functional_contract.maximum_generated_vertices -eq 49152 -and $manifest.functional_contract.maximum_generated_indices -eq 196608) 'Generated road budget claim mismatch'
Require ($manifest.functional_contract.candidate_document_generated_before_edit_exposure -and $manifest.functional_contract.path_redraw_revalidates_road_footprints -and $manifest.functional_contract.out_of_bounds_is_rejected_without_clamp) 'Candidate validation contract mismatch'
Require ($manifest.functional_contract.accepted_history_entries_per_command -eq 1 -and $manifest.functional_contract.undo_redo_exact -and $manifest.functional_contract.save_reopen_exact) 'Persistence contract mismatch'
Require (-not $manifest.functional_contract.legacy_v1_v2_v3_v4_snapshots_rewritten -and $manifest.functional_contract.legacy_embedded_terrain_vegetation_density_path_hashes_preserved -and $manifest.functional_contract.migrated_workspace_hash_changes_intentionally) 'Legacy compatibility claim mismatch'
Require ($manifest.functional_contract.road_only_reuses_terrain_vegetation_and_paths -and $manifest.functional_contract.road_only_terrain_mesh_uploads -eq 0 -and $manifest.functional_contract.unchanged_road_mesh_handles_reused -and $manifest.functional_contract.old_projection_visible_during_staging -and $manifest.functional_contract.road_removal_occurs_only_at_atomic_swap -and $manifest.functional_contract.stale_or_invalid_request_preserves_presented_projection) 'Road cache or atomic staging claim mismatch'
Require ($manifest.functional_contract.uses_existing_procedural_terrain_material -and -not $manifest.functional_contract.native_visual_review_performed -and -not $manifest.functional_contract.river_carving_r2_023 -and -not $manifest.functional_contract.intersections_r2_024) 'Evidence expands R2-022 beyond its implementation'

$expectedReceipts = @(
    @{ id = 'forge_project_tests'; command = 'cargo test -p forge_project'; status = 'PASS'; passed = 126 },
    @{ id = 'forge_world_tests'; command = 'cargo test -p forge_world'; status = 'PASS'; passed = 110 },
    @{ id = 'forge_engine_tests_lib'; command = 'cargo test -p forge_engine --lib'; status = 'PASS'; passed = 184 },
    @{ id = 'forge_studio_tests_lib'; command = 'cargo test -p forge_studio --lib'; status = 'PASS'; passed = 113 },
    @{ id = 'forge_studio_tests_bin'; command = 'cargo test -p forge_studio --bin forge-studio'; status = 'PASS'; passed = 6 },
    @{ id = 'forge_studio_check_wasm'; command = 'cargo check -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web'; status = 'PASS' },
    @{ id = 'forge_studio_clippy_native'; command = 'cargo clippy -p forge_studio --all-targets -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_studio_clippy_wasm'; command = 'cargo clippy -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_engine_clippy_native'; command = 'cargo clippy -p forge_engine --all-targets --all-features -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_engine_clippy_wasm_lib'; command = 'cargo clippy -p forge_engine --lib --target wasm32-unknown-unknown --no-default-features --features web -- -D warnings'; status = 'PASS' },
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
$fullSuiteSum = 126 + 110 + 184 + 113
Require ($fullSuiteSum -eq 533 -and $manifest.automated_validation.full_library_suite_tests_passed -eq $fullSuiteSum) 'Full-library suite total mismatch'
Require ($manifest.automated_validation.studio_binary_tests_passed_separately -eq 6) 'Studio binary count is not kept separate'
$studioBin = @($receipts | Where-Object id -CEQ 'forge_studio_tests_bin')[0]
Require ($studioBin.excluded_from_full_library_suite_total) 'Studio binary tests must be excluded from the 533 library total'
$engineWasm = @($receipts | Where-Object id -CEQ 'forge_engine_clippy_wasm_lib')[0]
Require ($engineWasm.scope_note -match 'library only' -and $engineWasm.scope_note -match 'no all-targets WASM verdict') 'Engine WASM receipt scope is overstated'

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
Require ($report -match 'corde horizontale' -and $report -match "ce plafond ne constitue pas une borne de longueur d’arc") 'Report does not distinguish horizontal chord from arc length'
Require ($report -match 'serde_json\.workspace = true' -and $report -match 'dépendance de développement') 'Report hides the dev-only serde_json change'
Require ($report -match 'Aucun asset, contenu Cadle ou dépendance runtime') 'Report does not state the exact asset/runtime boundary'

Write-Output "PASS: R2-022 evidence is bound to commit $($expectedSourceCommit.Substring(0, 8)), tree $($expectedSourceTree.Substring(0, 8)) and its exact 26-file inventory."
Write-Output 'PASS: 533 library tests plus 6 separate Studio binary tests; strict declared native/WASM product checks; Trunk debug build.'
Write-Output 'PASS: workspace v5 / forge.road v1, bounded control polylines and horizontal chords, atomic persistence and viewport swap.'
Write-Output 'NOT RUN: native UI, Release builds, GPU, performance, remote CI (infrastructure billing) and PresentMon.'
