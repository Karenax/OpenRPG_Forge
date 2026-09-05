#requires -Version 7.5
[CmdletBinding()]
param()

# Read-only validation bound to one immutable source commit. It does not execute tests or builds,
# launch Studio, inspect the mutable asset root, or infer UI, visual, Release, GPU, performance,
# PresentMon or remote-CI results.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$reportPath = Join-Path $repo 'docs/evidence/gate2/ASSET_PICKING.md'
$evidencePath = Join-Path $repo 'docs/evidence/gate2/ASSET_PICKING_ARTIFACTS.json'
$expectedSourceCommit = '79ea370e87da7bbc3ec19d7abd07889d781264c4'
$expectedSourceTree = '16dd740f967fdf4500bb5b9a980d40b05a82c424'
$expectedBaseCommit = '83616dbdbe1ae6b6a215364a28c54b4955ec4904'

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function GitText([string]$Commit, [string]$Path) {
    $object = '{0}:{1}' -f $Commit, $Path
    $lines = @(& git -C $repo show $object)
    Require ($LASTEXITCODE -eq 0) "Cannot read Git object: $object"
    $lines -join "`n"
}

function RequireText([string]$Text, [string]$Pattern, [string]$Message) {
    Require ($Text -match $Pattern) $Message
}

$evidence = Get-Content -Raw -LiteralPath $evidencePath | ConvertFrom-Json
$report = Get-Content -Raw -LiteralPath $reportPath
$readme = Get-Content -Raw -LiteralPath (Join-Path $repo 'README.md')
$context = Get-Content -Raw -LiteralPath (Join-Path $repo 'PROJECT_CONTEXT.md')
$roadmap = Get-Content -Raw -LiteralPath (Join-Path $repo 'docs/planning/MIGRATION_ROADMAP.md')

Require ($evidence.schema -ceq 'forge.gate2.asset-picking-evidence') 'Wrong evidence schema'
Require ($evidence.schema_version -eq 1) 'Wrong evidence schema version'
Require ($evidence.source_commit -ceq $expectedSourceCommit) 'Wrong source candidate'
Require ($evidence.source_tree -ceq $expectedSourceTree) 'Wrong source tree'
Require ($evidence.base_commit -ceq $expectedBaseCommit) 'Wrong source base'
Require ($evidence.routes.Count -eq 1 -and $evidence.routes[0].id -ceq 'R2-028' -and $evidence.routes[0].status -ceq 'PASS') 'R2-028 is not the sole PASS route'
Require ($evidence.next_route -ceq 'R2-029') 'Wrong next route'
Require ($evidence.automated_validation.raw_log_bundle -ceq 'NOT CAPTURED' -and $evidence.artifacts.Count -eq 0) 'Evidence invents raw logs or artifacts'

$metadata = @(& git -C $repo show -s --format='%H%n%T%n%P' $expectedSourceCommit)
Require ($LASTEXITCODE -eq 0 -and $metadata.Count -eq 3) 'Cannot read source commit metadata'
Require ($metadata[0] -ceq $expectedSourceCommit) 'Source commit is missing or ambiguous'
Require ($metadata[1] -ceq $expectedSourceTree) 'Source tree mismatch'
Require ($metadata[2] -ceq $expectedBaseCommit) 'Source base mismatch'
$base = (& git -C $repo rev-parse "$expectedBaseCommit^{commit}").Trim()
Require ($LASTEXITCODE -eq 0 -and $base -ceq $expectedBaseCommit) 'Base commit is missing or ambiguous'

$expectedChangedWithStatus = @(
    "M`tapps/forge_studio/src/lib.rs",
    "A`tapps/forge_studio/src/world_selection.rs",
    "M`tapps/forge_studio/src/world_ui.rs",
    "M`tcrates/forge_engine/src/editor_placement.rs",
    "M`tcrates/forge_engine/src/lib.rs"
)
$expectedChanged = @($expectedChangedWithStatus | ForEach-Object { ($_ -split "`t", 2)[1] })
$actualChangedWithStatus = @(& git -C $repo diff-tree --no-commit-id --name-status -r $expectedSourceCommit)
Require ($LASTEXITCODE -eq 0) 'Cannot enumerate source commit changes'
Require ($actualChangedWithStatus.Count -eq 5) 'Unexpected source file count'
Require (@(Compare-Object -CaseSensitive $expectedChangedWithStatus $actualChangedWithStatus).Count -eq 0) 'Unexpected source file/status inventory'
$declaredChanged = @($evidence.source_change_boundary.changed_files)
Require ($evidence.source_change_boundary.changed_file_count -eq 5 -and $declaredChanged.Count -eq 5) 'Evidence source file count mismatch'
Require (@(Compare-Object -CaseSensitive $expectedChanged $declaredChanged).Count -eq 0) 'Evidence source inventory mismatch'
Require (@($expectedChanged | Where-Object { $_ -notmatch '\.rs$' }).Count -eq 0) 'The source commit is not Rust-only'
Require (@($expectedChanged | Where-Object { $_ -match '(^|/)(assets|upstream/cadle)(/|$)' }).Count -eq 0) 'Source unexpectedly changes an asset or Cadle path'
Require (@($expectedChanged | Where-Object { $_ -match '(^|/)Cargo\.(toml|lock)$' }).Count -eq 0) 'Source unexpectedly changes Cargo metadata'
Require ($evidence.source_change_boundary.cargo_manifests_changed -eq 0 -and -not $evidence.source_change_boundary.cargo_lock_changed -and $evidence.source_change_boundary.new_third_party_packages_or_versions -eq 0) 'Cargo/package boundary claim mismatch'
Require (-not $evidence.source_change_boundary.workspace_or_persistent_schema_changed -and $evidence.source_change_boundary.persistent_source_files_changed -eq 0) 'Schema/persistence boundary claim mismatch'
Require ($evidence.source_change_boundary.assets_changed -eq 0 -and $evidence.source_change_boundary.cadle_paths_or_content_changed -eq 0) 'Asset/Cadle boundary claim mismatch'

$studioLib = GitText $expectedSourceCommit 'apps/forge_studio/src/lib.rs'
$studioSelection = GitText $expectedSourceCommit 'apps/forge_studio/src/world_selection.rs'
$studioWorld = GitText $expectedSourceCommit 'apps/forge_studio/src/world_ui.rs'
$enginePlacement = GitText $expectedSourceCommit 'crates/forge_engine/src/editor_placement.rs'
$engineLib = GitText $expectedSourceCommit 'crates/forge_engine/src/lib.rs'

foreach ($contract in @(
    '(?m)^pub struct EditorPlacementPointer \{$',
    '(?m)^pub struct EditorPlacementSample \{$',
    '(?m)^pub enum EditorPlacementPickingError \{$',
    '(?m)^    pub fn request_pick\($',
    '(?m)^const MAX_EDITOR_PLACEMENT_ANCESTOR_DEPTH: usize = 64;$',
    'update_editor_placement_pick',
    'TransformSystems::Propagate',
    'VisibilitySystems::CheckVisibility',
    'RayCastVisibility::Visible',
    'fn active_placement_ancestor\(',
    'fn current_pick_scope\(',
    'fn invalidate_pick\('
)) { RequireText $enginePlacement $contract 'Engine picking contract mismatch' }
RequireText $engineLib 'EditorPlacementPickingError, EditorPlacementPointer' 'Engine picking request/error exports are missing'
RequireText $engineLib 'EditorPlacementProjectionStatus, EditorPlacementSample' 'Engine picking sample export is missing'
Require (-not ($engineLib -match 'EditorPlacementRoot')) 'Private placement roots must not be exported'
RequireText $enginePlacement '(?s)fn invalidate_pick\(.*?state\.pick_pointer = None;.*?state\.pick_sample = None;' 'Submit/clear pick invalidation is incomplete'
RequireText $enginePlacement '(?s)let Ok\(\(camera, camera_transform\)\) = self\.cameras\.single\(\) else \{\s*return;\s*\};.*?camera\.logical_viewport_rect\(\)' 'Camera/viewport pending boundary changed'

foreach ($contract in @(
    '(?m)^const CLICK_TRAVEL_THRESHOLD_PX: i32 = 4;$',
    '(?m)^pub\(crate\) struct WorldSelectionScope \{$',
    '(?m)^pub\(crate\) struct WorldSelectionState \{$',
    'fn placement_revision\(',
    'Some\(\[0, 0\]\)',
    'squared_travel >',
    'pointer_id != self\.pointer_id'
)) { RequireText $studioSelection $contract 'Studio transient-selection or gesture contract mismatch' }
RequireText $studioLib '#\[cfg\(any\(test, all\(target_arch = "wasm32", feature = "web"\)\)\)\]' 'world_selection cfg is not test/WASM bounded'
RequireText $studioWorld '(?s)fn selection_scope\(.*?\.placements\s*\.revision\s*\.get\(\)' 'Studio scope is not tied to PlacementDocument revision'
RequireText $studioWorld 'fn apply_placement_sample\(' 'Studio sample application is missing'
RequireText $studioWorld 'fn WorldSelectionPanel\(' 'Studio selection panel is missing'
RequireText $studioWorld 'fn request_placement_pick\(' 'Studio pick request plumbing is missing'
RequireText $studioWorld '(?s)on:pointerup=.*?gesture\.move_to\(.*?drag\.set\(None\).*?gesture\.finish' 'Pointerup does not classify final travel before picking'

$studioTests = @(
    'click_without_travel_is_distinct_from_orbit_pan_and_wrong_pointer',
    'camera_motion_is_buffered_until_drag_threshold_then_catches_up_exactly',
    'rapid_pick_is_latest_wins_and_stale_sample_cannot_clear_selection',
    'exact_miss_clears_but_unknown_or_stale_hit_never_replaces_selection',
    'scope_revision_and_generation_must_match_exactly',
    'unrelated_workspace_revision_does_not_invalidate_the_exact_placement_projection',
    'reconcile_retains_existing_id_then_clears_removed_or_closed_scope_without_redo_magic',
    'selection_is_transient_and_leaves_project_json_revision_and_hash_bit_exact',
    'invalid_scope_coordinates_and_serial_exhaustion_fail_without_pending_work'
)
$enginePickingTests = @(
    'picking_resolves_a_gltf_descendant_and_rejects_an_old_scope',
    'placement_ancestor_walk_accepts_64_edges_and_rejects_65',
    'every_visible_mesh_in_front_occludes_an_active_placement',
    'hidden_staging_is_ignored_and_a_ray_miss_stays_empty',
    'pointer_scope_validation_and_latest_serial_win_are_exact',
    'an_exact_latest_miss_is_published_as_an_empty_sample',
    'commit_refreshes_the_private_pick_scope_of_a_retained_root',
    'submit_and_clear_invalidate_pick_requests_samples_and_scope',
    'picking_does_not_rebuild_geometry_or_upload_meshes'
)
foreach ($test in $studioTests) {
    RequireText $studioSelection ("fn {0}\(" -f [regex]::Escape($test)) ("Required Studio test missing: {0}" -f $test)
}
foreach ($test in $enginePickingTests) {
    RequireText $enginePlacement ("fn {0}\(" -f [regex]::Escape($test)) ("Required Engine test missing: {0}" -f $test)
}
Require (([regex]::Matches($studioSelection, '(?m)^    #\[test\]$')).Count -eq 9) 'Studio world_selection test inventory is not exactly 9'
Require (([regex]::Matches($enginePlacement, '(?m)^    #\[test\]$')).Count -eq 18) 'Engine editor_placement test inventory is not exactly 18'

$counts = $evidence.automated_validation.library_test_counts
$targeted = $evidence.automated_validation.targeted_test_counts
Require ($evidence.automated_validation.locked_engine_studio_library_tests_passed -eq 375) 'Wrong Engine + Studio library test total'
Require ($counts.forge_engine -eq 220 -and $counts.forge_studio -eq 155 -and ($counts.forge_engine + $counts.forge_studio) -eq 375) 'Wrong per-library test counts'
Require ($targeted.forge_engine_editor_placement -eq 18 -and $targeted.forge_studio_world_selection -eq 9 -and $targeted.included_in_locked_library_total) 'Wrong targeted test receipts'

$expectedReceipts = @(
    @{ id = 'engine_studio_locked_library_tests'; command = 'cargo test -p forge_engine -p forge_studio --lib --locked -j1'; passed = 375 },
    @{ id = 'forge_engine_editor_placement_targeted_tests'; command = 'cargo test -p forge_engine editor_placement --locked'; passed = 18 },
    @{ id = 'forge_studio_world_selection_targeted_tests'; command = 'cargo test -p forge_studio world_selection::tests --lib --locked -j1'; passed = 9 },
    @{ id = 'forge_engine_clippy_native_locked'; command = 'cargo clippy -p forge_engine --all-targets --locked -- -D warnings' },
    @{ id = 'forge_studio_clippy_native_locked'; command = 'cargo clippy -p forge_studio --all-targets --all-features --locked -- -D warnings' },
    @{ id = 'forge_studio_check_wasm_web_locked'; command = 'cargo check -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web --locked' },
    @{ id = 'forge_studio_clippy_wasm_web_locked'; command = 'cargo clippy -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web --locked -- -D warnings' },
    @{ id = 'forge_studio_trunk_debug_locked_offline'; command = 'trunk build --locked --offline' },
    @{ id = 'cargo_fmt'; command = 'cargo fmt --all -- --check' },
    @{ id = 'git_diff_check'; command = 'git diff --check' }
)
$receipts = @($evidence.automated_validation.receipts)
Require ($receipts.Count -eq $expectedReceipts.Count -and @($receipts | Where-Object { $_.status -cne 'PASS' }).Count -eq 0) 'Automated receipt inventory is incomplete or not PASS'
foreach ($expected in $expectedReceipts) {
    $actual = @($receipts | Where-Object { $_.id -ceq $expected.id })
    Require ($actual.Count -eq 1) "Missing or duplicate receipt: $($expected.id)"
    Require ($actual[0].command -ceq $expected.command) "Receipt command mismatch: $($expected.id)"
    if ($expected.ContainsKey('passed')) {
        Require ($actual[0].passed -eq $expected.passed -and $actual[0].failed -eq 0) "Receipt test count mismatch: $($expected.id)"
    }
}
Require (@($receipts | Where-Object id -CEQ 'forge_engine_clippy_native_locked')[0].executor -ceq 'qualification agent') 'Engine Clippy agent provenance is missing'

$trunk = $evidence.automated_validation.trunk_debug_prebuild
Require ($trunk.prebuild_rejections -eq 1 -and $trunk.environment_value -ceq 'NO_COLOR=1' -and $trunk.outcome -ceq 'REJECTED BEFORE BUILD') 'Trunk pre-build rejection history mismatch'
Require ($trunk.correction -ceq 'NO_COLOR removed' -and $trunk.actual_build_attempts -eq 2 -and $trunk.actual_build_outcome -ceq 'PASS' -and $trunk.raw_log -ceq 'NOT CAPTURED') 'Trunk recovery/final outcome mismatch'
$trunkReceipt = @($receipts | Where-Object id -CEQ 'forge_studio_trunk_debug_locked_offline')[0]
Require ($trunkReceipt.working_directory -ceq 'apps/forge_studio' -and $trunkReceipt.profile -ceq 'debug' -and $trunkReceipt.prebuild_rejections -eq 1 -and $trunkReceipt.rejected_environment_value -ceq 'NO_COLOR=1' -and $trunkReceipt.correction -ceq 'NO_COLOR removed' -and $trunkReceipt.actual_build_attempts -eq 2) 'Trunk receipt hides the rejected pre-build or corrected runs'

$pick = $evidence.picking_contract
Require ($pick.projection_must_be_current -and $pick.exact_generation_required -and $pick.strictly_increasing_serial_required -and $pick.result_correlates_project_area_and_placement_revision) 'Exact picking correlation claim mismatch'
Require ($pick.studio_uses_placement_document_revision_not_workspace_revision -and -not $pick.unrelated_workspace_revision_invalidates_pick -and $pick.placement_revision_mismatch_is_stale) 'Placement revision domain claim mismatch'
Require (-not $pick.stale_or_superseded_result_can_clear_selection -and $pick.exact_miss_clears_selection -and -not $pick.unknown_placement_replaces_selection) 'Stale/miss selection claim mismatch'
Require ($pick.first_visible_mesh_occludes_hits_behind_it -and -not $pick.hidden_staging_is_pickable -and $pick.maximum_placement_ancestor_edges -eq 64) 'Visibility/ancestor picking claim mismatch'
Require ($pick.submit_and_clear_invalidate_pointer_and_sample -and $pick.picking_mesh_uploads -eq 0 -and $pick.picking_geometry_cache_rebuilds -eq 0) 'Read-only renderer claim mismatch'
$gesture = $evidence.gesture_contract
Require ($gesture.click_travel_threshold_px -eq 4 -and $gesture.subthreshold_camera_delta -eq 0 -and $gesture.first_drag_delta_catches_up_from_origin -and $gesture.later_drag_deltas_are_incremental) 'Click/drag threshold claim mismatch'
Require ($gesture.pointerup_position_is_applied_before_pick_decision -and -not $gesture.wrong_pointer_consumes_gesture) 'Pointerup/wrong-pointer claim mismatch'
$selection = $evidence.selection_contract
Require ($selection.state_is_transient -and -not $selection.workspace_command_emitted -and -not $selection.project_json_changed -and -not $selection.project_content_hash_changed -and -not $selection.workspace_revision_changed) 'Transient no-mutation claim mismatch'
Require ($selection.removed_id_cleared -and -not $selection.redo_or_reopen_reselects_automatically) 'Undo/redo/reopen claim mismatch'
Require ($evidence.review_corrections.Count -eq 2 -and @($evidence.review_corrections | Where-Object { $_.status -cne 'FIXED IN QUALIFIED SOURCE' }).Count -eq 0) 'Review corrections are missing or presented as open defects'
Require ($evidence.known_non_blocking_risks.Count -eq 1 -and $evidence.known_non_blocking_risks[0].id -ceq 'PENDING_WITHOUT_CAMERA_OR_VIEWPORT' -and -not $evidence.known_non_blocking_risks[0].timeout_or_dedicated_diagnostic) 'Pending camera/viewport risk is missing or overstated'

Require ($evidence.native_ui_qualification.status -ceq 'NOT RUN' -and $evidence.visual_qualification.status -ceq 'NOT RUN') 'Native/visual UI qualification is overstated'
Require ($evidence.release_qualification.standalone_native_build -ceq 'NOT RUN' -and $evidence.release_qualification.native_release_build -ceq 'NOT RUN' -and $evidence.release_qualification.trunk_release_build -ceq 'NOT RUN') 'Release qualification is overstated'
Require ($evidence.gpu_qualification.status -ceq 'NOT RUN' -and $evidence.performance_qualification.status -ceq 'NOT RUN' -and $evidence.presentmon.status -ceq 'NOT RUN') 'GPU/performance/PresentMon qualification is overstated'
Require ($evidence.remote_ci.status -ceq 'NOT RUN — infrastructure billing' -and $evidence.remote_ci.decision -ceq 'ADR-0006') 'Remote CI status mismatch'

foreach ($document in @($report, $readme, $context, $roadmap)) {
    RequireText $document '27/43' 'Gate 2 count is not aligned to 27/43'
    RequireText $document 'R2-029' 'Next route R2-029 is missing'
}
foreach ($document in @($readme, $context, $roadmap)) {
    RequireText $document 'ASSET_PICKING\.md' 'Asset-picking evidence link is missing from a current project document'
}
RequireText $report ([regex]::Escape($expectedSourceCommit)) 'Report source commit is missing'
RequireText $report ([regex]::Escape($expectedSourceTree)) 'Report source tree is missing'
RequireText $report ([regex]::Escape($expectedBaseCommit)) 'Report source base is missing'
RequireText $report 'NO_COLOR=1' 'Report hides the rejected Trunk pre-build'
RequireText $report 'requête reste pending' 'Report hides the camera/viewport pending risk'
RequireText $report 'Deux défauts.*corrigés' 'Report does not identify the two acquired review corrections'
RequireText $report 'Aucun `Cargo\.toml`,\s+`Cargo\.lock`.*schéma ou donnée persistante' 'Report does not state the Cargo/asset/Cadle/schema/persistence boundary'
RequireText $roadmap '(?m)^- \[x\] `R2-027` drag/drop' 'Roadmap R2-027 checkbox regressed'
RequireText $roadmap '(?m)^- \[x\] `R2-028` picking' 'Roadmap R2-028 checkbox is not complete'
RequireText $roadmap '(?m)^- \[ \] `R2-029` gizmo translation/rotation/échelle\.$' 'Roadmap R2-029 must remain open'

Write-Output "PASS: R2-028 asset-picking evidence is bound to commit $($expectedSourceCommit.Substring(0, 8)), tree $($expectedSourceTree.Substring(0, 8)) and its exact five-file inventory."
Write-Output 'PASS: 375 Engine + Studio library tests, including 18/18 and 9/9 targeted tests; strict native/WASM checks and corrected Trunk debug build.'
Write-Output 'PASS: exact visible-mesh picking, transient no-mutation selection, and both adversarial review defects fixed in the qualified source.'
Write-Output 'NOT RUN: native/visual UI, Release, GPU, performance, PresentMon and remote CI (infrastructure billing).'
