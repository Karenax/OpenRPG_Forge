#requires -Version 7.5
[CmdletBinding()]
param()

# Read-only package validation. It never launches Studio and cannot infer a visual, GPU, latency,
# cadence or performance verdict from the functional receipts.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$bundle = 'artifacts/gate-2/vegetation-density-live-preview-2026-09-05'
$manifestPath = Join-Path $repo 'docs/evidence/gate2/VEGETATION_DENSITY_REALTIME_PREVIEW_ARTIFACTS.json'

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
Require ($manifest.schema -ceq 'forge.gate2.vegetation-density-realtime-preview-evidence') 'Wrong evidence schema'
Require ($manifest.schema_version -eq 1) 'Wrong evidence version'
Require ($manifest.source_commit -ceq 'cb9a8b1e2d2b2e6b85792a508df54925b2026a59') 'Wrong source candidate'
Require ($manifest.source_tree -ceq 'ae61bc521e7a95f6b7e2ab23168da370c34cb40c') 'Wrong source tree'
Require ($manifest.base_commit -ceq '718eb9882319c71f5395877f785d1dcc110b3951') 'Wrong source parent'
Require ($manifest.routes.Count -eq 1 -and $manifest.routes[0].id -ceq 'R2-019' -and $manifest.routes[0].status -ceq 'PASS') 'R2-019 is not the sole PASS route'
Require (($manifest.open_routes -join ',') -ceq 'R2-020') 'Wrong remaining R2-C boundary'
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
    'apps/forge_studio/src/area_preview.rs',
    'apps/forge_studio/src/area_preview_cache_tests.rs',
    'apps/forge_studio/src/brush_state.rs',
    'apps/forge_studio/src/brush_ui.rs',
    'apps/forge_studio/src/lib.rs',
    'apps/forge_studio/src/main.rs',
    'apps/forge_studio/src/workspace_api.rs',
    'apps/forge_studio/src/workspace_service.rs',
    'apps/forge_studio/src/workspace_service_tests.rs',
    'apps/forge_studio/src/world_ui.rs'
)
$changed = @(& git -C $repo diff-tree --no-commit-id --name-only -r $manifest.source_commit)
Require ($LASTEXITCODE -eq 0) 'Cannot enumerate source changes'
Require ($changed.Count -eq 10 -and $changed.Count -eq $manifest.source_change_boundary.changed_files) 'Unexpected source file count'
Require (@(Compare-Object -CaseSensitive $expectedChanged $changed).Count -eq 0) 'Unexpected source file inventory'
Require (@($changed | Where-Object { $_ -match '(^|/)(assets|upstream/cadle)/' }).Count -eq 0) 'Source unexpectedly changes an asset or Cadle path'
Require (@($changed | Where-Object { $_ -match '(^|/)Cargo\.(toml|lock)$' }).Count -eq 0) 'Source unexpectedly changes Cargo metadata'
$diff = @(& git -C $repo diff $manifest.base_commit $manifest.source_commit -- $expectedChanged) -join "`n"
Require ($LASTEXITCODE -eq 0 -and $diff -notmatch '(?i)cadle') 'Source diff unexpectedly contains Cadle content'
Require ($manifest.source_change_boundary.dependencies_changed -eq 0 -and $manifest.source_change_boundary.assets_changed -eq 0 -and $manifest.source_change_boundary.cadle_paths_or_content_changed -eq 0) 'Manifest expands the source boundary'

$workspace = GitText $manifest.source_commit 'crates/forge_project/src/workspace.rs'
$vegetation = GitText $manifest.source_commit 'crates/forge_project/src/vegetation.rs'
Require ($workspace -match '(?m)^pub const WORKSPACE_SCHEMA_VERSION: u32 = 3;$') 'Workspace schema changed'
Require ($vegetation -match '(?m)^pub const VEGETATION_DOCUMENT_SCHEMA_VERSION: u32 = 1;$') 'Vegetation schema changed'
Require (-not $manifest.functional_contract.schema_bump) 'Manifest claims a schema bump'

$brushState = GitText $manifest.source_commit 'apps/forge_studio/src/brush_state.rs'
Require ($brushState -match '(?m)^pub\(crate\) const STROKE_PREVIEW_INTERVAL_MS: u64 = 125;$') 'Preview cadence constant mismatch'
Require ($brushState -match 'active: Option<ActiveStrokePreview>' -and $brushState -match 'pending: Option<QueuedStrokePreview<T>>') 'Bounded active/pending coordinator is missing'
Require ($brushState -match 'Pure latest-wins coordinator') 'Latest-wins contract is missing'
$service = GitText $manifest.source_commit 'apps/forge_studio/src/workspace_service.rs'
$api = GitText $manifest.source_commit 'apps/forge_studio/src/workspace_api.rs'
$browser = GitText $manifest.source_commit 'apps/forge_studio/src/brush_ui.rs'
$native = GitText $manifest.source_commit 'apps/forge_studio/src/main.rs'
Require ($api -match 'VegetationDensityStrokePreviewRequest' -and $api -match 'VegetationDensityStrokePreviewResponse') 'Typed preview envelopes are missing'
Require ($service -match 'vegetation_density_stroke_preview_input' -and $native -match 'prepare_vegetation_density_preview') 'Immutable native candidate path is missing'
Require ($browser -match 'density_preview_observed' -and $browser -match 'PaintVegetationDensity') 'Observed-before-commit path is missing'

Require ($manifest.functional_contract.preview_interval_ms -eq 125) 'Wrong preview interval'
Require ($manifest.functional_contract.logical_dispatch_opportunities_per_second -eq 8) 'Wrong logical dispatch rate'
Require ($manifest.functional_contract.maximum_in_flight_native_previews -eq 1 -and $manifest.functional_contract.maximum_pending_ui_trajectories -eq 1) 'Preview work is not recorded as bounded 1+1'
Require ($manifest.functional_contract.superseded_errors_are_nonfatal) 'Superseded-error policy missing'
Require (-not $manifest.functional_contract.candidate_session_mutation -and -not $manifest.functional_contract.candidate_history_mutation -and -not $manifest.functional_contract.candidate_dirty_state_mutation -and -not $manifest.functional_contract.candidate_disk_mutation) 'Candidate is incorrectly recorded as mutating canonical state'
Require ($manifest.functional_contract.accepted_commit_history_entries -eq 1) 'Final commit is not recorded as one history entry'
Require (($manifest.functional_contract.prerelease_preview_modes -join ',') -ceq 'vegetation_density') 'Evidence expands preview beyond vegetation density'
Require (-not $manifest.functional_contract.persistent_instance_promotion) 'R2-020 must remain open'

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
Require (($targetedCounts -join ',') -ceq '94,100,6,85') 'Unexpected targeted suite counts'
Require (($targetedCounts | Measure-Object -Sum).Sum -eq 285 -and $manifest.automated_validation.targeted_tests_passed -eq 285) 'Expected 285 targeted tests'
foreach ($result in $targetedResults) {
    Require ($result.Groups[2].Value -eq '0' -and $result.Groups[3].Value -eq '0') 'Failed or ignored targeted tests'
}

$expanded = Log 'cargo-test-workspace-excluding-unchanged-runtime.log'
$expandedResults = [regex]::Matches($expanded, 'test result: ok\. (\d+) passed; (\d+) failed; (\d+) ignored;')
Require ($expandedResults.Count -eq 10 -and $manifest.automated_validation.expanded_workspace_test_targets -eq 10) 'Unexpected expanded test-target count'
$expandedCounts = @($expandedResults | ForEach-Object { [int]$_.Groups[1].Value })
Require (($expandedCounts -join ',') -ceq '3,0,0,3,12,173,94,100,6,85') 'Unexpected expanded suite counts'
Require (($expandedCounts | Measure-Object -Sum).Sum -eq 476 -and $manifest.automated_validation.expanded_workspace_tests_passed -eq 476) 'Expected 476 expanded tests'
foreach ($result in $expandedResults) {
    Require ($result.Groups[2].Value -eq '0' -and $result.Groups[3].Value -eq '0') 'Failed or ignored expanded tests'
}

$requiredTests = @(
    'density_preview_envelopes_round_trip_strictly',
    'density_stroke_preview_candidate_matches_canonical_apply_and_leaves_session_bit_exact',
    'successive_density_previews_share_the_canonical_base_and_noop_is_explicit',
    'rejected_density_previews_never_touch_session_history_dirty_state_or_disk',
    'density_preview_response_binds_serial_candidate_hash_and_renderer_projection',
    'disposable_density_candidates_from_one_base_return_to_the_canonical_cache',
    'burst_keeps_one_active_request_and_only_the_latest_pending_trajectory',
    'settling_a_superseded_response_releases_but_never_presents_it',
    'finished_trajectory_has_priority_and_bypasses_the_cadence_after_settlement',
    'adapter_ignores_a_failed_superseded_response_and_dispatches_the_pending_finish',
    'active_finish_cannot_be_followed_by_a_late_movement_preview',
    'adapter_can_discard_a_released_ticket_before_sending_it',
    'cancel_invalidates_a_late_response_before_presentation',
    'generation_area_and_revision_mismatches_are_never_presented',
    'changing_target_invalidates_late_work_and_releases_only_the_new_context'
)
foreach ($test in $requiredTests) {
    Require ($targeted -match "test [^\r\n]*$([regex]::Escape($test)) \.\.\. ok") "Required test result missing: $test"
}

$nativeClippy = Log 'cargo-clippy-relevant-native.log'
$wasmClippy = Log 'cargo-clippy-studio-wasm.log'
$wasmCheck = Log 'cargo-check-studio-wasm.log'
$trunkDebug = Log 'trunk-build-debug.log'
$trunkRelease = Log 'trunk-build-release.log'
Require ($nativeClippy -match 'Finished `dev` profile' -and $nativeClippy -notmatch '(?m)^error(?:\[|:)') 'Native relevant Clippy did not finish cleanly'
Require ($wasmClippy -match 'Finished `dev` profile' -and $wasmClippy -notmatch '(?m)^error(?:\[|:)') 'Studio WASM Clippy did not finish cleanly'
Require ($wasmCheck -match 'Finished `dev` profile' -and $wasmCheck -notmatch '(?m)^error(?:\[|:)') 'Studio WASM check did not finish cleanly'
Require ($trunkDebug -match 'Finished `dev` profile' -and $trunkDebug -notmatch 'error from build pipeline') 'Trunk debug build did not finish cleanly'
Require ($trunkRelease -match 'Finished `release` profile' -and $trunkRelease -match "couldn't find application wasm-opt" -and $trunkRelease -match 'unable to download in offline mode') 'Trunk release NOT COMPLETED receipt mismatch'
Require ($manifest.automated_validation.trunk_release_build -ceq 'NOT COMPLETED' -and $manifest.automated_validation.native_release_build -ceq 'NOT RUN') 'Build limitations are misclassified'

Write-Output 'PASS: R2-019 evidence is bound to commit cb9a8b1e and tree ae61bc52.'
Write-Output 'PASS: 7 artifact hashes; 285 targeted tests; 476 expanded tests; strict native/WASM Clippy; WASM check; Trunk debug build.'
Write-Output 'NOT COMPLETED: Trunk Release asset pipeline lacked offline wasm-opt after Rust compilation.'
Write-Output 'NOT RUN: native UI, native Release build, remote CI (infrastructure billing), PresentMon, GPU, latency and performance qualification.'
