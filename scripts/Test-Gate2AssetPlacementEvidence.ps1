#requires -Version 7.5
[CmdletBinding()]
param()

# Read-only validation bound to one immutable source commit. It does not execute tests or builds,
# scan the mutable asset root, launch Studio, or infer UI, visual, Release, GPU, performance,
# PresentMon or remote-CI results.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$reportPath = Join-Path $repo 'docs/evidence/gate2/ASSET_PLACEMENT.md'
$evidencePath = Join-Path $repo 'docs/evidence/gate2/ASSET_PLACEMENT_ARTIFACTS.json'
$expectedSourceCommit = 'c5ccc8742be18513cd3f117215fa57e5c6b46553'
$expectedSourceTree = '479c5484de5c903a41f17f3350dd96fccba8e036'
$expectedBaseCommit = 'cc03751adfbbbfd79ffab58b2b38a5b2a621b2c3'

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

Require ($evidence.schema -ceq 'forge.gate2.asset-placement-evidence') 'Wrong evidence schema'
Require ($evidence.schema_version -eq 1) 'Wrong evidence schema version'
Require ($evidence.source_commit -ceq $expectedSourceCommit) 'Wrong source candidate'
Require ($evidence.source_tree -ceq $expectedSourceTree) 'Wrong source tree'
Require ($evidence.base_commit -ceq $expectedBaseCommit) 'Wrong source base'
Require ($evidence.routes.Count -eq 1 -and $evidence.routes[0].id -ceq 'R2-027' -and $evidence.routes[0].status -ceq 'PASS') 'R2-027 is not the sole PASS route'
Require ($evidence.next_route -ceq 'R2-028') 'Wrong next route'

$metadata = @(& git -C $repo show -s --format='%H%n%T%n%P' $expectedSourceCommit)
Require ($LASTEXITCODE -eq 0 -and $metadata.Count -eq 3) 'Cannot read source commit metadata'
Require ($metadata[0] -ceq $expectedSourceCommit) 'Source commit is missing or ambiguous'
Require ($metadata[1] -ceq $expectedSourceTree) 'Source tree mismatch'
Require ($metadata[2] -ceq $expectedBaseCommit) 'Source base mismatch'
$base = (& git -C $repo rev-parse "$expectedBaseCommit^{commit}").Trim()
Require ($LASTEXITCODE -eq 0 -and $base -ceq $expectedBaseCommit) 'Base commit is missing or ambiguous'

$expectedChangedWithStatus = @(
    "M`tapps/forge_studio/Cargo.toml",
    "M`tapps/forge_studio/src/area_preview.rs",
    "M`tapps/forge_studio/src/area_preview_cache_tests.rs",
    "M`tapps/forge_studio/src/asset_browser.rs",
    "M`tapps/forge_studio/src/asset_browser_service.rs",
    "M`tapps/forge_studio/src/asset_browser_service_tests.rs",
    "A`tapps/forge_studio/src/asset_drag.rs",
    "M`tapps/forge_studio/src/desktop_ui.rs",
    "M`tapps/forge_studio/src/lib.rs",
    "M`tapps/forge_studio/src/main.rs",
    "M`tapps/forge_studio/src/workspace_api.rs",
    "M`tapps/forge_studio/src/workspace_service.rs",
    "M`tapps/forge_studio/src/workspace_service_tests.rs",
    "M`tapps/forge_studio/src/world_ui.rs",
    "M`tcrates/forge_assets/src/browser.rs",
    "M`tcrates/forge_engine/src/editor_brush.rs",
    "A`tcrates/forge_engine/src/editor_placement.rs",
    "M`tcrates/forge_engine/src/editor_viewport.rs",
    "M`tcrates/forge_engine/src/lib.rs",
    "M`tcrates/forge_project/src/lib.rs",
    "A`tcrates/forge_project/src/placement.rs",
    "M`tcrates/forge_project/src/workspace.rs",
    "M`tcrates/forge_project/src/workspace_edit.rs",
    "M`tcrates/forge_project/src/workspace_edit_tests.rs",
    "M`tcrates/forge_project/src/workspace_legacy.rs",
    "M`tcrates/forge_world/src/lib.rs",
    "A`tcrates/forge_world/src/placement.rs",
    "A`tcrates/forge_world/src/placement_tests.rs",
    "M`tcrates/forge_world/src/terrain_brush_tests.rs"
)
$expectedChanged = @($expectedChangedWithStatus | ForEach-Object { ($_ -split "`t", 2)[1] })
$actualChangedWithStatus = @(& git -C $repo diff-tree --no-commit-id --name-status -r $expectedSourceCommit)
Require ($LASTEXITCODE -eq 0) 'Cannot enumerate source commit changes'
Require ($actualChangedWithStatus.Count -eq 29) 'Unexpected source file count'
Require (@(Compare-Object -CaseSensitive $expectedChangedWithStatus $actualChangedWithStatus).Count -eq 0) 'Unexpected source file/status inventory'
$declaredChanged = @($evidence.source_change_boundary.changed_files)
Require ($evidence.source_change_boundary.changed_file_count -eq 29 -and $declaredChanged.Count -eq 29) 'Evidence source file count mismatch'
Require (@(Compare-Object -CaseSensitive $expectedChanged $declaredChanged).Count -eq 0) 'Evidence source inventory mismatch'
Require (@($expectedChanged | Where-Object { $_ -match '(^|/)(assets|upstream/cadle)(/|$)' }).Count -eq 0) 'Source unexpectedly changes an asset or Cadle path'

$actualCargoFiles = @($expectedChanged | Where-Object { $_ -match '(^|/)Cargo\.(toml|lock)$' })
$declaredCargoFiles = @($evidence.source_change_boundary.cargo_metadata_files)
Require ($actualCargoFiles.Count -eq 1 -and $actualCargoFiles[0] -ceq 'apps/forge_studio/Cargo.toml') 'Unexpected Cargo metadata boundary'
Require ($evidence.source_change_boundary.cargo_metadata_files_changed -eq 1 -and $declaredCargoFiles.Count -eq 1 -and $declaredCargoFiles[0] -ceq 'apps/forge_studio/Cargo.toml') 'Evidence Cargo metadata inventory mismatch'
Require (-not $evidence.source_change_boundary.cargo_lock_changed -and $evidence.source_change_boundary.new_third_party_packages_or_versions -eq 0) 'Cargo lock/package claim mismatch'
$cargoDiff = @(& git -C $repo diff --unified=0 $expectedBaseCommit $expectedSourceCommit -- apps/forge_studio/Cargo.toml) -join "`n"
Require ($LASTEXITCODE -eq 0) 'Cannot inspect Studio Cargo diff'
$cargoAdds = @($cargoDiff -split "`n" | Where-Object { $_ -cmatch '^\+[^+]' -and $_ -cne '+' })
$cargoRemoves = @($cargoDiff -split "`n" | Where-Object { $_ -cmatch '^-[^-]' -and $_ -cne '-' })
Require ($cargoAdds.Count -eq 1 -and $cargoRemoves.Count -eq 1) 'Studio Cargo change is not one feature-line replacement'
Require ($cargoAdds[0] -match '"DragEvent"' -and $cargoRemoves[0] -notmatch '"DragEvent"') 'web-sys DragEvent feature addition is missing'
Require (($cargoAdds[0] -replace ', "DragEvent"', '') -ceq ('+' + $cargoRemoves[0].Substring(1))) 'Studio Cargo change contains more than DragEvent'

$projectPlacement = GitText $expectedSourceCommit 'crates/forge_project/src/placement.rs'
$projectWorkspace = GitText $expectedSourceCommit 'crates/forge_project/src/workspace.rs'
$projectEdit = GitText $expectedSourceCommit 'crates/forge_project/src/workspace_edit.rs'
$projectTests = GitText $expectedSourceCommit 'crates/forge_project/src/workspace_edit_tests.rs'
$worldPlacement = GitText $expectedSourceCommit 'crates/forge_world/src/placement.rs'
$worldTests = GitText $expectedSourceCommit 'crates/forge_world/src/placement_tests.rs'
$enginePlacement = GitText $expectedSourceCommit 'crates/forge_engine/src/editor_placement.rs'
$engineBrush = GitText $expectedSourceCommit 'crates/forge_engine/src/editor_brush.rs'
$studioDrag = GitText $expectedSourceCommit 'apps/forge_studio/src/asset_drag.rs'
$studioBrowser = GitText $expectedSourceCommit 'apps/forge_studio/src/asset_browser.rs'
$studioAssetService = GitText $expectedSourceCommit 'apps/forge_studio/src/asset_browser_service.rs'
$studioAssetTests = GitText $expectedSourceCommit 'apps/forge_studio/src/asset_browser_service_tests.rs'
$studioWorkspaceTests = GitText $expectedSourceCommit 'apps/forge_studio/src/workspace_service_tests.rs'
$studioCacheTests = GitText $expectedSourceCommit 'apps/forge_studio/src/area_preview_cache_tests.rs'
$studioWorld = GitText $expectedSourceCommit 'apps/forge_studio/src/world_ui.rs'

RequireText $projectWorkspace '(?m)^pub const WORKSPACE_SCHEMA_VERSION: u32 = 8;$' 'Workspace v8 is missing'
foreach ($contract in @(
    '(?m)^pub const PLACEMENT_DOCUMENT_SCHEMA: &str = "forge\.placement";$',
    '(?m)^pub const PLACEMENT_DOCUMENT_SCHEMA_VERSION: u32 = 1;$',
    '(?m)^pub const MAX_ASSET_PINS_PER_DOCUMENT: usize = 256;$',
    '(?m)^pub const MAX_AUTHORED_PLACEMENTS_PER_DOCUMENT: usize = 1_024;$',
    '(?m)^pub const MAX_PLACEMENT_DOCUMENT_JSON_BYTES: usize = 512 \* 1_024;$',
    'pub struct AssetPin',
    'pub struct AuthoredPlacement'
)) { RequireText $projectPlacement $contract 'Persistent placement contract mismatch' }
RequireText $projectEdit 'WorkspaceChange::PlaceAsset' 'Shared workspace placement edit is missing'
RequireText $worldPlacement 'pub struct PlaceAssetCommand' 'Pure PlaceAssetCommand is missing'
RequireText $worldPlacement 'pub fn preview_place_asset' 'Pure placement preview is missing'
RequireText $worldPlacement 'pub fn placement_entity_id' 'Deterministic placement identity is missing'
RequireText $worldPlacement 'surface\.height_m' 'Native effective-terrain Y resolution is missing'

foreach ($test in @(
    @{ text = $projectPlacement; name = 'canonical_round_trip_and_hash_are_stable' },
    @{ text = $projectPlacement; name = 'canonical_order_duplicates_and_missing_pins_are_rejected' },
    @{ text = $projectPlacement; name = 'schema_json_and_collection_limits_are_enforced' },
    @{ text = $projectTests; name = 'place_asset_is_sorted_revisioned_reopenable_and_replayable_from_inverse' },
    @{ text = $projectTests; name = 'place_asset_rejects_five_stale_sources_atomically' },
    @{ text = $worldTests; name = 'placement_is_deterministic_revisioned_and_uses_canonical_initial_transform' },
    @{ text = $worldTests; name = 'placement_height_samples_the_effective_post_river_terrain' },
    @{ text = $studioDrag; name = 'latest_exact_surface_hit_becomes_a_source_only_drop_intent' },
    @{ text = $studioDrag; name = 'stale_scope_missing_hit_and_non_finite_coordinates_never_place' },
    @{ text = $studioAssetTests; name = 'placement_resolution_is_bound_to_the_exact_cached_snapshot_and_native_provenance' },
    @{ text = $studioAssetTests; name = 'persistent_placement_reopens_without_opening_assets_and_rejects_a_repinned_source' },
    @{ text = $studioWorkspaceTests; name = 'governed_asset_drop_is_one_revision_and_history_entry_and_survives_reopen' },
    @{ text = $studioWorkspaceTests; name = 'asset_drop_rejects_stale_or_uncorrelated_input_without_changing_the_session' },
    @{ text = $studioCacheTests; name = 'placement_only_revision_reuses_all_cached_terrain_and_vegetation_projection' },
    @{ text = $enginePlacement; name = 'bridge_is_latest_wins_and_never_reopens_an_old_generation' },
    @{ text = $enginePlacement; name = 'clear_is_latest_wins_and_removes_active_staging_and_scope' },
    @{ text = $enginePlacement; name = 'cross_area_staging_never_leaves_the_previous_area_visible' },
    @{ text = $enginePlacement; name = 'failure_destroys_only_staging_and_keeps_previous_projection_bit_stable' },
    @{ text = $enginePlacement; name = 'atomic_swap_retains_unchanged_entity_removes_undo_and_adds_redo_without_mesh_uploads' }
)) {
    RequireText $test.text ("fn {0}\(" -f [regex]::Escape($test.name)) ("Required test missing: {0}" -f $test.name)
}

RequireText $engineBrush 'pub struct EditorSurfacePointer' 'Surface pointer input is missing'
RequireText $engineBrush 'pub struct EditorSurfaceHit' 'Surface hit bridge is missing'
RequireText $enginePlacement 'pub fn submit_document' 'Public placement projection submission is missing'
RequireText $enginePlacement 'pub fn clear\(&self, generation: u64\)' 'Latest-wins clear API is missing'
RequireText $enginePlacement 'GltfAssetLabel::Scene\(0\)' 'Scene(0) loading is missing'
RequireText $enginePlacement 'RecursiveDependencyLoadState::Loaded' 'Recursive dependency load gate is missing'
RequireText $enginePlacement 'scenes\.values\(\)' 'Unique scene byte accounting is missing'
RequireText $studioBrowser 'draggable="true"' 'Direct draggable Assets surface is missing'
RequireText $studioWorld 'on:drop=' 'World canvas drop target is missing'
RequireText $studioWorld 'resolve_drop' 'Exact Studio drop correlation is missing'
RequireText $studioAssetService 'resolve_placement_document' 'Reopen placement resolution is missing'
RequireText $studioAssetService 'current_pin != persisted_pin' 'Persistent repin rejection is missing'

$counts = $evidence.automated_validation.library_test_counts
Require ($evidence.automated_validation.locked_library_suite_tests_passed -eq 678) 'Wrong library test total'
Require ($counts.forge_assets -eq 21 -and $counts.forge_project -eq 161 -and $counts.forge_world -eq 139 -and $counts.forge_engine -eq 211 -and $counts.forge_studio -eq 146) 'Wrong per-library test counts'
Require (($counts.forge_assets + $counts.forge_project + $counts.forge_world + $counts.forge_engine + $counts.forge_studio) -eq 678) 'Library test counts do not sum to 678'
Require ($evidence.automated_validation.studio_binary_tests_passed_separately -eq 6) 'Wrong Studio binary test count'
$receipts = @($evidence.automated_validation.receipts)
Require ($receipts.Count -eq 10 -and @($receipts | Where-Object { $_.status -cne 'PASS' }).Count -eq 0) 'Automated receipt inventory is incomplete or not PASS'
$expectedClippyCommands = @(
    'cargo clippy -p forge_assets -p forge_project -p forge_world --all-targets --all-features --locked -- -D warnings',
    'cargo clippy -p forge_engine --lib --locked -- -D warnings',
    'cargo clippy -p forge_studio --all-targets --all-features --locked -- -D warnings'
)
$actualNativeClippyCommands = @($receipts | Where-Object { $_.id -in @('assets_project_world_clippy_native_locked', 'forge_engine_clippy_native_lib_locked', 'forge_studio_clippy_native_locked') } | ForEach-Object { $_.command })
Require ($actualNativeClippyCommands.Count -eq 3 -and @(Compare-Object -CaseSensitive $expectedClippyCommands $actualNativeClippyCommands).Count -eq 0) 'Native Clippy receipt commands mismatch'

Require (-not $evidence.asset_root_boundary.hostile_concurrent_mutation_boundary_claimed -and $evidence.asset_root_boundary.packaged_runtime_root_deferred_to_gate -eq 4) 'Gate 4 asset-root limitation mismatch'
Require ($evidence.native_ui_qualification.status -ceq 'NOT RUN' -and $evidence.visual_qualification.status -ceq 'NOT RUN' -and $evidence.gpu_qualification.status -ceq 'NOT RUN' -and $evidence.performance_qualification.status -ceq 'NOT RUN' -and $evidence.presentmon.status -ceq 'NOT RUN') 'A deferred qualification is overstated'
Require ($evidence.remote_ci.status -ceq 'NOT RUN — infrastructure billing') 'Remote CI status mismatch'

foreach ($document in @($report, $readme, $context, $roadmap)) {
    RequireText $document '26/43' 'Gate 2 count is not aligned to 26/43'
    RequireText $document 'R2-028' 'Next route R2-028 is missing'
}
RequireText $report ([regex]::Escape($expectedSourceCommit)) 'Report source commit is missing'
RequireText $report ([regex]::Escape($expectedSourceTree)) 'Report source tree is missing'
RequireText $report ([regex]::Escape($expectedBaseCommit)) 'Report source base is missing'
RequireText $roadmap '(?m)^- \[x\] `R2-027` drag/drop' 'Roadmap R2-027 checkbox is not complete'
RequireText $roadmap '(?m)^- \[ \] `R2-028` picking\.$' 'Roadmap R2-028 must remain open'

Write-Output 'PASS: R2-027 asset-placement evidence is internally consistent and bound to the immutable source commit.'
