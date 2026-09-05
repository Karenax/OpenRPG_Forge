#requires -Version 7.5
[CmdletBinding()]
param()

# Read-only validation bound to one immutable source commit. It never launches Studio and cannot
# infer a native visual, Release, GPU or performance verdict.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$manifestPath = Join-Path $repo 'docs/evidence/gate2/PATH_CREATION_AND_EDITING_ARTIFACTS.json'
$expectedSourceCommit = '91be10b84898fab1623ef7f7d67ef4fe2dd4ea17'
$expectedSourceTree = 'a44593d4affc3f6bea588f0d7570d40c10da9efd'
$expectedBaseCommit = '168e95da4a80d6ef7e9b2736980463fd1053d8cb'

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function GitText([string]$Commit, [string]$Path) {
    $object = '{0}:{1}' -f $Commit, $Path
    $lines = @(& git -C $repo show $object)
    Require ($LASTEXITCODE -eq 0) "Cannot read Git object: $object"
    $lines -join "`n"
}

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
Require ($manifest.schema -ceq 'forge.gate2.path-creation-editing-evidence') 'Wrong evidence schema'
Require ($manifest.schema_version -eq 1) 'Wrong evidence version'
Require ($manifest.source_commit -ceq $expectedSourceCommit) 'Wrong source candidate'
Require ($manifest.source_tree -ceq $expectedSourceTree) 'Wrong source tree'
Require ($manifest.base_commit -ceq $expectedBaseCommit) 'Wrong source parent'
Require ($manifest.routes.Count -eq 1 -and $manifest.routes[0].id -ceq 'R2-021' -and $manifest.routes[0].status -ceq 'PASS') 'R2-021 is not the sole PASS route'
Require ($manifest.next_route -ceq 'R2-022') 'Wrong next route'
Require ($manifest.native_ui_qualification.status -ceq 'NOT RUN') 'Native UI must remain explicitly NOT RUN'
Require ($manifest.release_qualification.native_release_build -ceq 'NOT RUN' -and $manifest.release_qualification.trunk_release_build -ceq 'NOT RUN') 'Release builds must remain explicitly NOT RUN'
Require ($manifest.performance_qualification.status -ceq 'NOT RUN') 'Performance must remain explicitly NOT RUN'
Require ($manifest.remote_ci.status -ceq 'NOT RUN — infrastructure billing' -and $manifest.remote_ci.decision -ceq 'ADR-0006') 'Wrong remote CI exception'
Require ($manifest.presentmon.status -ceq 'NOT RUN') 'PresentMon must remain explicitly NOT RUN'

$base = (& git -C $repo rev-parse "$expectedBaseCommit^{commit}").Trim()
Require ($LASTEXITCODE -eq 0 -and $base -ceq $expectedBaseCommit) 'Base commit is missing or ambiguous'
$commit = (& git -C $repo rev-parse "$expectedSourceCommit^{commit}").Trim()
Require ($LASTEXITCODE -eq 0 -and $commit -ceq $expectedSourceCommit) 'Source commit is missing or ambiguous'
$tree = (& git -C $repo show -s --format=%T $expectedSourceCommit).Trim()
Require ($LASTEXITCODE -eq 0 -and $tree -ceq $expectedSourceTree) 'Source tree mismatch'
$parent = (& git -C $repo show -s --format=%P $expectedSourceCommit).Trim()
Require ($LASTEXITCODE -eq 0 -and $parent -ceq $expectedBaseCommit) 'Source parent mismatch'

$expectedChanged = @(
    'apps/forge_studio/src/area_preview.rs',
    'apps/forge_studio/src/area_preview_cache_tests.rs',
    'apps/forge_studio/src/brush_state.rs',
    'apps/forge_studio/src/brush_ui.rs',
    'apps/forge_studio/src/main.rs',
    'apps/forge_studio/src/path_ui.rs',
    'apps/forge_studio/src/workspace_api.rs',
    'apps/forge_studio/src/workspace_service.rs',
    'apps/forge_studio/src/workspace_service_tests.rs',
    'apps/forge_studio/src/world_ui.rs',
    'crates/forge_engine/src/editor_partial_tests.rs',
    'crates/forge_engine/src/editor_path.rs',
    'crates/forge_engine/src/editor_viewport.rs',
    'crates/forge_project/README.md',
    'crates/forge_project/src/lib.rs',
    'crates/forge_project/src/path.rs',
    'crates/forge_project/src/workspace_edit.rs',
    'crates/forge_project/src/workspace_edit_tests.rs',
    'crates/forge_project/src/workspace_legacy.rs',
    'crates/forge_project/src/workspace.rs',
    'crates/forge_world/src/lib.rs',
    'crates/forge_world/src/path.rs',
    'crates/forge_world/src/terrain_brush_tests.rs'
)
$changed = @(& git -C $repo diff-tree --no-commit-id --name-only -r $expectedSourceCommit)
Require ($LASTEXITCODE -eq 0) 'Cannot enumerate source commit changes'
Require ($changed.Count -eq 23 -and $changed.Count -eq $manifest.source_change_boundary.changed_file_count) 'Unexpected source file count'
Require (@(Compare-Object -CaseSensitive $expectedChanged $changed).Count -eq 0) 'Unexpected source file inventory'
$manifestChanged = @($manifest.source_change_boundary.changed_files)
Require ($manifestChanged.Count -eq 23 -and @(Compare-Object -CaseSensitive $expectedChanged $manifestChanged).Count -eq 0) 'Manifest source inventory mismatch'
Require (@($changed | Where-Object { $_ -match '(^|/)(assets|upstream/cadle)(/|$)' }).Count -eq 0) 'Source unexpectedly changes an asset or Cadle path'
Require (@($changed | Where-Object { $_ -match '(^|/)Cargo\.(toml|lock)$' }).Count -eq 0) 'Source unexpectedly changes Cargo metadata'
Require ($manifest.source_change_boundary.cargo_manifests_or_lockfiles_changed -eq 0 -and $manifest.source_change_boundary.dependencies_changed -eq 0 -and $manifest.source_change_boundary.assets_changed -eq 0 -and $manifest.source_change_boundary.cadle_paths_or_content_changed -eq 0) 'Manifest expands the source boundary'

function SourceText([string]$Path) {
    GitText $expectedSourceCommit $Path
}

$allSource = @($expectedChanged | ForEach-Object { SourceText $_ }) -join "`n"
Require ($allSource -notmatch '(?i)cadle') 'Source files unexpectedly contain Cadle content'

$pathSource = SourceText 'crates/forge_project/src/path.rs'
$workspace = SourceText 'crates/forge_project/src/workspace.rs'
$workspaceEdit = SourceText 'crates/forge_project/src/workspace_edit.rs'
$workspaceLegacy = SourceText 'crates/forge_project/src/workspace_legacy.rs'
$worldPath = SourceText 'crates/forge_world/src/path.rs'
$areaPreview = SourceText 'apps/forge_studio/src/area_preview.rs'
$areaPreviewTests = SourceText 'apps/forge_studio/src/area_preview_cache_tests.rs'
$enginePath = SourceText 'crates/forge_engine/src/editor_path.rs'
$engineViewport = SourceText 'crates/forge_engine/src/editor_viewport.rs'
$studioState = SourceText 'apps/forge_studio/src/brush_state.rs'
$studioApi = SourceText 'apps/forge_studio/src/workspace_api.rs'
$studioService = SourceText 'apps/forge_studio/src/workspace_service.rs'
$studioServiceTests = SourceText 'apps/forge_studio/src/workspace_service_tests.rs'
$studioPathUi = SourceText 'apps/forge_studio/src/path_ui.rs'
$studioWorldUi = SourceText 'apps/forge_studio/src/world_ui.rs'

Require ($workspace -match '(?m)^pub const WORKSPACE_SCHEMA_VERSION: u32 = 4;$' -and $workspace -match 'pub paths: PathDocument') 'Workspace v4 path source is missing'
Require ($pathSource -match '(?m)^pub const PATH_DOCUMENT_SCHEMA: &str = "forge\.path";$' -and $pathSource -match '(?m)^pub const PATH_DOCUMENT_SCHEMA_VERSION: u32 = 1;$') 'Path schema v1 is missing'
Require ($pathSource -match '(?m)^pub const MAX_PATHS_PER_DOCUMENT: usize = 64;$' -and $pathSource -match '(?m)^pub const MAX_PATH_CONTROL_POINTS: usize = 64;$' -and $pathSource -match '(?m)^pub const MAX_TOTAL_PATH_CONTROL_POINTS: usize = 2_048;$') 'Path collection bounds mismatch'
Require ($pathSource -match '(?m)^pub const MAX_PATH_LENGTH_M: f64 = 8_192\.0;$' -and $pathSource -match '(?m)^pub const MAX_TOTAL_PATH_LENGTH_M: f64 = 32_768\.0;$' -and $pathSource -match '(?m)^pub const MAX_PATH_DOCUMENT_JSON_BYTES: usize = 512 \* 1_024;$') 'Path length or JSON bounds mismatch'
Require ($pathSource -match 'pub struct PathId' -and $pathSource -match 'pub struct AuthoredPath' -and $pathSource -match 'CentripetalCatmullRomV1') 'Persistent path identity or interpolation is missing'
Require ($workspaceEdit -match 'UpsertPath' -and $workspaceEdit -match 'expected_path_document_hash') 'Atomic path upsert is missing'
Require ($workspaceLegacy -match '(?m)^\s*1 => migrate_v1' -and $workspaceLegacy -match '(?m)^\s*2 => migrate_v2' -and $workspaceLegacy -match '(?m)^\s*3 => migrate_v3' -and $workspaceLegacy -match 'PathDocument::new') 'Workspace v1/v2/v3 path migration is missing'

Require ($worldPath -match '(?m)^pub const PATH_SAMPLE_SPACING_M: f32 = 2\.0;$' -and $worldPath -match '(?m)^pub const MIN_PATH_TRACE_POINTS: usize = 2;$' -and $worldPath -match '(?m)^pub const MAX_PATH_TRACE_POINTS: usize = 64;$' -and $worldPath -match '(?m)^pub const MIN_PATH_TRACE_POINT_DISTANCE_M: f32 = 0\.5;$') 'World path sampling or trace bounds mismatch'
Require ($worldPath -match 'pub struct GeneratedPaths' -and $worldPath -match 'pub terrain_document_hash: ContentHash' -and $worldPath -match 'pub path_document_hash: ContentHash') 'Generated path source hashes are missing'
Require ($worldPath -match 'pub fn preview_create_path' -and $worldPath -match 'pub fn preview_redraw_path' -and $worldPath -match 'WorkspaceChange::UpsertPath') 'Pure path commands are missing'
Require ($worldPath -match 'fn validate_candidate_paths' -and $worldPath -match 'PathGenerationPlan::from_documents\(&area\.terrain, &area\.paths\)\?\.generate\(\)\?') 'Whole candidate path document is not validated before edit exposure'
Require ($worldPath -match 'CurveSampleOutOfBounds' -and $worldPath -match 'centripetal_catmull_rom_plan_view') 'Curve overshoot rejection or Catmull-Rom generation is missing'

Require ($areaPreview -match 'pub paths: GeneratedPaths' -and $areaPreview -match 'PathGenerationPlan::from_documents') 'Complete paths are missing from AreaPreview'
Require ($studioWorldUi -match 'preview\.paths\.terrain_document_hash != terrain_hash' -and $studioWorldUi -match 'preview\.paths\.path_document_hash != path_hash') 'Studio path hash validation is missing'
Require ($areaPreviewTests -match 'path_only_edit_reuses_all_terrain_and_vegetation_chunks_and_replaces_paths') 'Path-only cache proof is missing'
Require ($engineViewport -match 'pub struct EditorPathRequest' -and $engineViewport -match 'path::validate_generated_paths' -and $engineViewport -match 'path_draft = None') 'Viewport path request validation or invalidation is missing'
Require ($enginePath -match '(?m)^const MAX_PRESENTED_PATH_SAMPLES: usize = 18_496;$' -and $enginePath -match 'fn validate_sample_sequence' -and $enginePath -match 'pub struct EditorPathDraft') 'Viewport path bounds or draft are missing'
Require ($studioApi -match 'CreatePath' -and $studioApi -match 'RedrawPath') 'Typed Studio path commands are missing'
Require ($studioService -match 'preview_create_path' -and $studioService -match 'preview_redraw_path' -and $studioService -match 'first_free_path_identity_and_name') 'Studio path service is missing'
Require ($studioState -match '(?m)^pub\(crate\) const PATH_TRACE_MIN_POINTS: usize = 2;$' -and $studioState -match '(?m)^pub\(crate\) const PATH_TRACE_MAX_POINTS: usize = 64;$' -and $studioState -match '(?m)^pub\(crate\) const PATH_TRACE_MIN_SPACING_M: f32 = 4\.0;$') 'Studio path trace bounds mismatch'
Require ($studioPathUi -match 'if draft\.finishing && sampled == Some\(draft\.last_serial\)' -and $studioPathUi -match 'workspace\.send\(command\)' -and $studioPathUi -match 'bridge\.path_draft = None') 'One release command or local draft invalidation is missing'

Require ($manifest.functional_contract.workspace_schema_version_previous -eq 3 -and $manifest.functional_contract.workspace_schema_version_current -eq 4) 'Wrong workspace migration claim'
Require ($manifest.functional_contract.path_schema -ceq 'forge.path' -and $manifest.functional_contract.path_schema_version -eq 1) 'Wrong path schema claim'
Require ($manifest.functional_contract.maximum_paths_per_document -eq 64 -and $manifest.functional_contract.maximum_control_points_per_path -eq 64 -and $manifest.functional_contract.maximum_total_control_points -eq 2048) 'Wrong path collection claim'
Require ($manifest.functional_contract.maximum_path_length_m -eq 8192 -and $manifest.functional_contract.maximum_total_path_length_m -eq 32768 -and $manifest.functional_contract.maximum_path_document_json_bytes -eq 524288) 'Wrong path source budget claim'
Require ($manifest.functional_contract.trace_minimum_points -eq 2 -and $manifest.functional_contract.trace_maximum_points -eq 64 -and $manifest.functional_contract.world_minimum_consecutive_point_distance_m -eq 0.5 -and $manifest.functional_contract.studio_minimum_trace_spacing_m -eq 4.0) 'Wrong trace bounds claim'
Require ($manifest.functional_contract.control_chord_subdivision_target_m -eq 2.0 -and -not $manifest.functional_contract.local_curve_arc_spacing_strictly_bounded -and $manifest.functional_contract.exact_endpoints -and $manifest.functional_contract.persistent_y_resolved_from_terrain) 'Wrong deterministic generation claim'
Require ($manifest.functional_contract.generated_output_hash_for_curved_fixture -ceq '2ed0df0fb5f050022c84bde5817962b7b71c87a1ed5d88659e672e68d1d9112a') 'Wrong curved fixture hash'
Require ($manifest.functional_contract.curve_out_of_bounds_is_rejected_without_clamp -and $manifest.functional_contract.entire_candidate_document_is_generated_before_edit_exposure) 'Candidate overshoot guard is not recorded'
Require ($manifest.functional_contract.redraw_preserves_id_name_closed_and_interpolation -and $manifest.functional_contract.accepted_history_entries_per_command -eq 1 -and $manifest.functional_contract.undo_redo_exact -and $manifest.functional_contract.save_reopen_exact) 'Persistence or redraw contract mismatch'
Require (-not $manifest.functional_contract.legacy_v1_v2_v3_snapshots_rewritten -and $manifest.functional_contract.legacy_embedded_terrain_vegetation_density_hashes_preserved -and $manifest.functional_contract.migrated_workspace_hash_changes_intentionally) 'Legacy compatibility claim mismatch'
Require (-not $manifest.functional_contract.separate_path_cache -and $manifest.functional_contract.path_only_terrain_chunks_rebuilt -eq 0 -and $manifest.functional_contract.path_only_vegetation_chunks_rebuilt -eq 0 -and $manifest.functional_contract.path_only_vegetation_chunks_retained -eq 64) 'Derived path/cache claim mismatch'
Require ($manifest.functional_contract.maximum_presented_path_samples -eq 18496 -and $manifest.functional_contract.viewport_replacement_atomic) 'Viewport budget or atomicity claim mismatch'
Require ($manifest.functional_contract.one_command_on_pointer_release -and -not $manifest.functional_contract.drag_draft_persisted -and $manifest.functional_contract.drag_draft_invalidated_on_cancel_area_generation_or_error -and -not $manifest.functional_contract.native_spline_preview_during_drag) 'Transient gesture contract mismatch'
Require (-not $manifest.functional_contract.rename_path -and -not $manifest.functional_contract.delete_path -and -not $manifest.functional_contract.generic_picking -and -not $manifest.functional_contract.generic_gizmo -and -not $manifest.functional_contract.road_profile_r2_022 -and -not $manifest.functional_contract.river_carving_r2_023 -and -not $manifest.functional_contract.intersections_r2_024) 'Evidence expands R2-021 beyond its implementation'

$expectedReceipts = @(
    @{ id = 'forge_project_tests'; command = 'cargo test -p forge_project'; status = 'PASS'; passed = 115 },
    @{ id = 'forge_project_clippy_native'; command = 'cargo clippy -p forge_project --all-targets -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_project_check_wasm'; command = 'cargo check -p forge_project --target wasm32-unknown-unknown'; status = 'PASS' },
    @{ id = 'forge_project_clippy_wasm'; command = 'cargo clippy -p forge_project --target wasm32-unknown-unknown --all-targets -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_world_tests'; command = 'cargo test -p forge_world'; status = 'PASS'; passed = 103 },
    @{ id = 'forge_world_clippy_native'; command = 'cargo clippy -p forge_world --all-targets --all-features -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_world_clippy_wasm'; command = 'cargo clippy -p forge_world --target wasm32-unknown-unknown --all-targets --all-features -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_engine_tests_full'; command = 'cargo test -p forge_engine --lib'; status = 'PASS'; passed = 176 },
    @{ id = 'forge_engine_tests_editor_viewport'; command = 'cargo test -p forge_engine editor_viewport --lib'; status = 'PASS'; passed = 18 },
    @{ id = 'forge_engine_clippy_native'; command = 'cargo clippy -p forge_engine --all-targets --all-features -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_engine_clippy_wasm_lib'; command = 'cargo clippy -p forge_engine --lib --target wasm32-unknown-unknown --no-default-features --features web -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_engine_clippy_wasm_all_targets'; command = 'cargo clippy -p forge_engine --target wasm32-unknown-unknown --no-default-features --features web --all-targets -- -D warnings'; status = 'FAIL — PRE-EXISTING TEST-ONLY' },
    @{ id = 'forge_studio_check_tests'; command = 'cargo check -p forge_studio --tests'; status = 'PASS' },
    @{ id = 'forge_studio_check_wasm'; command = 'cargo check -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web'; status = 'PASS' },
    @{ id = 'forge_studio_tests_lib'; command = 'cargo test -p forge_studio --lib'; status = 'PASS'; passed = 110 },
    @{ id = 'forge_studio_tests_bin'; command = 'cargo test -p forge_studio --bin forge-studio'; status = 'PASS'; passed = 6 },
    @{ id = 'forge_studio_tests_path_service'; command = 'cargo test -p forge_studio --lib path_create_and_redraw'; status = 'PASS'; passed = 2 },
    @{ id = 'forge_studio_tests_path_trace'; command = 'cargo test -p forge_studio --lib authored_path_trace'; status = 'PASS'; passed = 2 },
    @{ id = 'forge_studio_tests_path_cache'; command = 'cargo test -p forge_studio --lib path_only_edit'; status = 'PASS'; passed = 1 },
    @{ id = 'forge_studio_clippy_native'; command = 'cargo clippy -p forge_studio --all-targets -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_studio_clippy_wasm'; command = 'cargo clippy -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web -- -D warnings'; status = 'PASS' },
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
$fullSuiteSum = 115 + 103 + 176 + 110
Require ($fullSuiteSum -eq 504 -and $manifest.automated_validation.full_library_suite_tests_passed -eq $fullSuiteSum -and $manifest.automated_validation.targeted_subsuite_counts_are_not_additive) 'Full-suite total or subset classification mismatch'
$wasmAllTargets = @($receipts | Where-Object id -CEQ 'forge_engine_clippy_wasm_all_targets')[0]
Require (-not $wasmAllTargets.r2_021_regression -and $wasmAllTargets.diagnostic -match 'src/lib\.rs:3435' -and $wasmAllTargets.diagnostic -match 'std::time::Instant') 'Known Engine WASM all-targets limitation is not exact'
$trunk = @($receipts | Where-Object id -CEQ 'forge_studio_trunk_debug')[0]
Require ($trunk.working_directory -ceq 'apps/forge_studio' -and $trunk.preflight_issue -match 'NO_COLOR=1' -and $trunk.advisory -match 'proc-macro-error2 2\.0\.1') 'Trunk debug receipt or advisory mismatch'

$requiredTests = @(
    @{ source = $pathSource; name = 'versioned_json_round_trip_and_hash_are_canonical' },
    @{ source = $pathSource; name = 'collections_order_counts_and_lengths_are_bounded' },
    @{ source = $workspaceEdit; name = 'WorkspaceChange' },
    @{ source = SourceText 'crates/forge_project/src/workspace_edit_tests.rs'; name = 'workspace_v3_migration_adds_only_empty_paths_and_migrates_embedded_sources' },
    @{ source = SourceText 'crates/forge_project/src/workspace_edit_tests.rs'; name = 'workspace_v3_migration_preserves_current_embedded_hashes_but_has_a_new_workspace_hash' },
    @{ source = SourceText 'crates/forge_project/src/workspace_edit_tests.rs'; name = 'upsert_path_creates_edits_sorts_reopens_and_replays_from_its_inverse' },
    @{ source = SourceText 'crates/forge_project/src/workspace_edit_tests.rs'; name = 'upsert_path_rejects_noop_stale_missing_and_invalid_inputs_atomically' },
    @{ source = $worldPath; name = 'two_point_path_is_a_terrain_projected_line_with_exact_endpoints' },
    @{ source = $worldPath; name = 'curved_generation_replays_hashes_and_tracks_the_exact_terrain_surface' },
    @{ source = $worldPath; name = 'create_is_open_snapped_canonical_and_exactly_one_revision' },
    @{ source = $worldPath; name = 'redraw_preserves_identity_metadata_closed_state_and_interpolation' },
    @{ source = $worldPath; name = 'identical_redraw_is_a_fail_closed_noop' },
    @{ source = $worldPath; name = 'create_and_redraw_reject_curve_overshoot_without_exposing_an_edit' },
    @{ source = $engineViewport; name = 'path_only_projection_atomically_replaces_overlay_without_mesh_upload' },
    @{ source = $engineViewport; name = 'invalid_path_projection_leaves_presented_overlay_intact' },
    @{ source = $enginePath; name = 'draft_requires_current_generation_and_bounded_points' },
    @{ source = $studioState; name = 'authored_path_trace_keeps_3d_draft_and_emits_bounded_xz_only' },
    @{ source = $studioState; name = 'authored_path_trace_requires_two_points_and_refuses_the_sixty_fifth_atomically' },
    @{ source = $areaPreviewTests; name = 'path_only_edit_reuses_all_terrain_and_vegetation_chunks_and_replaces_paths' },
    @{ source = $studioServiceTests; name = 'path_create_and_redraw_are_one_revision_each_and_survive_history_and_reopen' },
    @{ source = $studioServiceTests; name = 'path_create_and_redraw_rejections_are_atomic' },
    @{ source = $studioServiceTests; name = 'path_identity_and_default_name_are_deterministic_after_undo' },
    @{ source = $studioServiceTests; name = 'opening_v2_and_v3_adds_empty_paths_in_memory_without_rewriting_legacy_snapshots' }
)
foreach ($test in $requiredTests) {
    Require ($test.source -match [regex]::Escape($test.name)) "Required source/test missing: $($test.name)"
}

Write-Output "PASS: R2-021 evidence is bound to commit $($expectedSourceCommit.Substring(0, 8)) and tree $($expectedSourceTree.Substring(0, 8))."
Write-Output 'PASS: 504 full-library tests declared; strict relevant native/WASM product checks; Trunk debug build.'
Write-Output 'KNOWN FAIL OUTSIDE R2-021: forge_engine WASM --all-targets pre-existing Instant mismatch in a test helper; product --lib is PASS.'
Write-Output 'NOT RUN: native UI, Release builds, remote CI (infrastructure billing), PresentMon, GPU, latency and performance qualification.'
