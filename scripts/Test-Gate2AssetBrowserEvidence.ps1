#requires -Version 7.5
[CmdletBinding()]
param()

# Read-only validation bound to one immutable source commit. It does not launch Studio, scan an
# installation root, execute tests or builds, or infer a native UI, Release, GPU, performance,
# PresentMon or remote-CI verdict.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$reportPath = Join-Path $repo 'docs/evidence/gate2/ASSET_BROWSER.md'
$evidencePath = Join-Path $repo 'docs/evidence/gate2/ASSET_BROWSER_ARTIFACTS.json'
$expectedSourceCommit = '7dafbe2dd13f2bc40516fab4e891a063b285aa88'
$expectedSourceTree = 'f201b89d20222f4e725a0f746fc99ce54ad79060'
$expectedBaseCommit = '1997d838a2b342ed08f15ce6a3018fc9cd80185f'
$expectedManifestHash = 'c86c4454b3b0ae4ca0bb546d22b29f3e79a0bfd8107ef63b8af1f0ebbffd6677'

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

function GitBytes([string]$Commit, [string]$Path) {
    $start = [System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName = 'git'
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('-C', $repo, 'cat-file', 'blob', ('{0}:{1}' -f $Commit, $Path))) {
        $null = $start.ArgumentList.Add($argument)
    }
    $process = [System.Diagnostics.Process]::Start($start)
    $memory = [System.IO.MemoryStream]::new()
    $process.StandardOutput.BaseStream.CopyTo($memory)
    $standardError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    $exitCode = $process.ExitCode
    $bytes = $memory.ToArray()
    $memory.Dispose()
    $process.Dispose()
    Require ($exitCode -eq 0) "Cannot read binary Git object $Commit`:$Path`: $standardError"
    return ,$bytes
}

function Sha256Hex([byte[]]$Bytes) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()
}

function HashBytePattern([string]$Hash) {
    $parts = for ($index = 0; $index -lt 32; $index++) {
        '0x{0}' -f $Hash.Substring($index * 2, 2)
    }
    '(?s)' + (($parts | ForEach-Object { [regex]::Escape($_) }) -join ',\s*')
}

$evidence = Get-Content -Raw -LiteralPath $evidencePath | ConvertFrom-Json
$report = Get-Content -Raw -LiteralPath $reportPath
Require ($evidence.schema -ceq 'forge.gate2.asset-browser-evidence') 'Wrong evidence schema'
Require ($evidence.schema_version -eq 1) 'Wrong evidence schema version'
Require ($evidence.source_commit -ceq $expectedSourceCommit) 'Wrong source candidate'
Require ($evidence.source_tree -ceq $expectedSourceTree) 'Wrong source tree'
Require ($evidence.base_commit -ceq $expectedBaseCommit) 'Wrong source parent'
Require ($evidence.routes.Count -eq 1 -and $evidence.routes[0].id -ceq 'R2-026' -and $evidence.routes[0].status -ceq 'PASS') 'R2-026 is not the sole PASS route'
Require ($evidence.next_route -ceq 'R2-027') 'Wrong next route'

$metadata = @(& git -C $repo show -s --format='%H%n%T%n%P' $expectedSourceCommit)
Require ($LASTEXITCODE -eq 0 -and $metadata.Count -eq 3) 'Cannot read source commit metadata'
Require ($metadata[0] -ceq $expectedSourceCommit) 'Source commit is missing or ambiguous'
Require ($metadata[1] -ceq $expectedSourceTree) 'Source tree mismatch'
Require ($metadata[2] -ceq $expectedBaseCommit) 'Source parent mismatch'
$base = (& git -C $repo rev-parse "$expectedBaseCommit^{commit}").Trim()
Require ($LASTEXITCODE -eq 0 -and $base -ceq $expectedBaseCommit) 'Base commit is missing or ambiguous'

$expectedChangedWithStatus = @(
    "M`tCargo.lock",
    "M`tapps/forge_studio/Cargo.toml",
    "M`tapps/forge_studio/desktop.css",
    "A`tapps/forge_studio/src/asset_browser.rs",
    "A`tapps/forge_studio/src/asset_browser_service.rs",
    "A`tapps/forge_studio/src/asset_browser_service_tests.rs",
    "M`tapps/forge_studio/src/desktop_state.rs",
    "M`tapps/forge_studio/src/desktop_ui.rs",
    "M`tapps/forge_studio/src/lib.rs",
    "M`tapps/forge_studio/src/main.rs",
    "M`tcrates/forge_assets/Cargo.toml",
    "M`tcrates/forge_assets/README.md",
    "A`tcrates/forge_assets/src/browser.rs",
    "M`tcrates/forge_assets/src/lib.rs"
)
$expectedChanged = @($expectedChangedWithStatus | ForEach-Object { ($_ -split "`t", 2)[1] })
$changedWithStatus = @(& git -C $repo diff-tree --no-commit-id --name-status -r $expectedSourceCommit)
Require ($LASTEXITCODE -eq 0) 'Cannot enumerate source commit changes'
Require ($changedWithStatus.Count -eq 14) 'Unexpected source file count'
Require (@(Compare-Object -CaseSensitive $expectedChangedWithStatus $changedWithStatus).Count -eq 0) 'Unexpected source file/status inventory'
$declaredChanged = @($evidence.source_change_boundary.changed_files)
Require ($evidence.source_change_boundary.changed_file_count -eq 14 -and $declaredChanged.Count -eq 14) 'Evidence source file count mismatch'
Require (@(Compare-Object -CaseSensitive $expectedChanged $declaredChanged).Count -eq 0) 'Evidence source inventory mismatch'
Require (@($expectedChanged | Where-Object { $_ -match '(^|/)(assets|upstream/cadle)(/|$)' }).Count -eq 0) 'Source unexpectedly changes an asset or Cadle path'

$expectedCargoFiles = @('Cargo.lock', 'apps/forge_studio/Cargo.toml', 'crates/forge_assets/Cargo.toml')
$actualCargoFiles = @($expectedChanged | Where-Object { $_ -match '(^|/)Cargo\.(toml|lock)$' })
$declaredCargoFiles = @($evidence.source_change_boundary.cargo_metadata_files)
Require ($actualCargoFiles.Count -eq 3 -and @(Compare-Object -CaseSensitive $expectedCargoFiles $actualCargoFiles).Count -eq 0) 'Unexpected Cargo metadata boundary'
Require ($evidence.source_change_boundary.cargo_metadata_files_changed -eq 3 -and @(Compare-Object -CaseSensitive $expectedCargoFiles $declaredCargoFiles).Count -eq 0) 'Evidence Cargo metadata inventory mismatch'
$cargoDiff = @(& git -C $repo diff --unified=0 $expectedBaseCommit $expectedSourceCommit -- @expectedCargoFiles) -join "`n"
Require ($LASTEXITCODE -eq 0) 'Cannot inspect Cargo metadata diff'
Require ($cargoDiff -match '(?m)^\+forge_assets\.workspace = true$') 'Studio -> forge_assets edge is missing'
Require ($cargoDiff -match '(?m)^\+serde_json\.workspace = true$' -and $cargoDiff -match '(?m)^\+sha2\.workspace = true$') 'Native forge_assets dependency edges are missing'
Require ($cargoDiff -match '(?m)^\+ "forge_assets",$' -and $cargoDiff -match '(?m)^\+ "serde_json",$' -and $cargoDiff -match '(?m)^\+ "sha2 0\.11\.0",$') 'Lockfile dependency edges mismatch'
Require ($cargoDiff -notmatch '(?m)^\+\[\[package\]\]$' -and $cargoDiff -notmatch '(?m)^\+version = ' -and $cargoDiff -notmatch '(?m)^\+checksum = ') 'A new package or package version was added unexpectedly'
Require ($evidence.source_change_boundary.workspace_dependency_edges_activated -eq 3 -and $evidence.source_change_boundary.new_third_party_packages_or_versions -eq 0) 'Dependency-edge claim mismatch'
Require ($evidence.source_change_boundary.assets_changed -eq 0 -and $evidence.source_change_boundary.cadle_paths_or_content_changed -eq 0 -and $evidence.source_change_boundary.persistent_project_schemas_changed -eq 0) 'Asset, Cadle or persistent-schema boundary mismatch'

$assetsLibraryDiff = @(& git -C $repo diff --unified=0 $expectedBaseCommit $expectedSourceCommit -- crates/forge_assets/src/lib.rs) -join "`n"
Require ($LASTEXITCODE -eq 0) 'Cannot inspect forge_assets library diff'
$meaningfulLibraryAdds = @($assetsLibraryDiff -split "`n" | Where-Object { $_ -cmatch '^\+[^+]' -and $_ -cne '+' })
$meaningfulLibraryRemovals = @($assetsLibraryDiff -split "`n" | Where-Object { $_ -cmatch '^-[^-]' -and $_ -cne '-' })
Require ($meaningfulLibraryAdds.Count -eq 1 -and $meaningfulLibraryAdds[0] -ceq '+pub mod browser;' -and $meaningfulLibraryRemovals.Count -eq 0) 'Gate 0 forge_assets contract changed beyond exporting browser'

$governed = $evidence.governed_subset
$manifestBytes = GitBytes $expectedSourceCommit $governed.manifest_path
$actualManifestHash = Sha256Hex $manifestBytes
Require ($actualManifestHash -ceq $expectedManifestHash -and $governed.manifest_sha256 -ceq $expectedManifestHash) 'Governed manifest SHA-256 mismatch'
$assetManifest = [Text.Encoding]::UTF8.GetString($manifestBytes) | ConvertFrom-Json
Require ($assetManifest.schema -ceq 'forge.asset-subset-manifest' -and $assetManifest.schema_version -eq 1) 'Governed subset manifest schema mismatch'
Require ($assetManifest.subset_id -ceq 'visual-valley-quaternius-stylized-nature-standard-1.0' -and $assetManifest.source.pack_id -ceq 'quaternius:stylized-nature-megakit-standard-1.0') 'Governed subset identity mismatch'
Require ($assetManifest.source.archive_sha256 -ceq '298f6732b872e4cf7b30e6e7abf9641c7f6dc6b326df37ac089533ed7e3d58c9') 'Archive pin mismatch'
Require ($assetManifest.source.normalized_metadata_sha256 -ceq 'e3286b81f3a6171eb844a83528eac55b2727b2ef8e3f96d1d4e29faddf5b458d') 'Normalized metadata pin mismatch'
Require ($assetManifest.license.spdx -ceq 'CC0-1.0' -and $assetManifest.license.verified -and -not $assetManifest.license.attribution_required) 'Verified licence claim mismatch'
Require ($assetManifest.license.evidence_path -ceq 'License_Standard.txt' -and $assetManifest.license.evidence_sha256 -ceq '120710e542c3ebaf83856c4eb55b4a1e680593b39302eba7fdc5d942dfe78c58') 'Licence evidence pin mismatch'
Require (@($assetManifest.models).Count -eq 9 -and @($assetManifest.files).Count -eq 27) 'Governed inventory count mismatch'
$manifestFilePaths = @($assetManifest.files.path)
Require (@($manifestFilePaths | Sort-Object -Unique).Count -eq 27) 'Governed manifest contains duplicate file paths'
$licenseFiles = @($assetManifest.files | Where-Object { $_.path -ceq $assetManifest.license.evidence_path })
Require ($licenseFiles.Count -eq 1 -and $licenseFiles[0].sha256 -ceq $assetManifest.license.evidence_sha256) 'Licence evidence is not exactly one bound declaration'
Require ($governed.manifest_count -eq 1 -and $governed.model_count -eq 9 -and $governed.content_file_count -eq 26 -and $governed.license_evidence_file_count -eq 1 -and $governed.declared_file_count -eq 27) 'Evidence inventory count claim mismatch'

$assetTree = @(& git -C $repo ls-tree -r --name-only $expectedSourceCommit -- assets)
Require ($LASTEXITCODE -eq 0) 'Cannot enumerate committed asset tree'
$subsetManifestPaths = @($assetTree | Where-Object { $_ -cmatch '(^|/)subset\.manifest\.json$' })
Require ($subsetManifestPaths.Count -eq 1 -and $subsetManifestPaths[0] -ceq $governed.manifest_path) 'Expected exactly one committed subset manifest'
$subsetDirectory = $governed.manifest_path.Substring(0, $governed.manifest_path.LastIndexOf('/'))
$expectedSubsetTree = @($manifestFilePaths | ForEach-Object { "$subsetDirectory/$_" }) + $governed.manifest_path
$actualSubsetTree = @($assetTree | Where-Object { $_.StartsWith("$subsetDirectory/", [StringComparison]::Ordinal) })
Require ($actualSubsetTree.Count -eq 28 -and @(Compare-Object -CaseSensitive $expectedSubsetTree $actualSubsetTree).Count -eq 0) 'Committed subset tree differs from the exact manifest inventory'

$assetsBrowser = SourceText 'crates/forge_assets/src/browser.rs'
$assetsLib = SourceText 'crates/forge_assets/src/lib.rs'
$assetsReadme = SourceText 'crates/forge_assets/README.md'
$studioBrowser = SourceText 'apps/forge_studio/src/asset_browser.rs'
$studioService = SourceText 'apps/forge_studio/src/asset_browser_service.rs'
$studioServiceTests = SourceText 'apps/forge_studio/src/asset_browser_service_tests.rs'
$studioDesktopState = SourceText 'apps/forge_studio/src/desktop_state.rs'
$studioDesktopUi = SourceText 'apps/forge_studio/src/desktop_ui.rs'
$studioMain = SourceText 'apps/forge_studio/src/main.rs'

Require ($assetsLib -match '(?m)^pub mod browser;$' -and $assetsLib -match 'pub fn gate0_procedural' -and $assetsLib -match 'pub fn validate_procedural_only') 'Browser export or Gate 0 procedural contract is missing'
Require ($assetsBrowser -match '(?m)^pub const ASSET_BROWSER_SNAPSHOT_SCHEMA: &str = "forge\.asset-browser-snapshot";$' -and $assetsBrowser -match '(?m)^pub const ASSET_BROWSER_SNAPSHOT_VERSION: u32 = 1;$') 'Asset-browser snapshot v1 contract is missing'
Require ($assetsBrowser -match '(?s)derive\(Clone, Debug, PartialEq, Serialize, Deserialize\).*?pub struct AssetBrowserSnapshot' -and $assetsBrowser -match '#\[cfg\(not\(target_arch = "wasm32"\)\)\]\s*mod native') 'Cross-platform DTO or native-only scanner boundary is missing'
Require ($assetsBrowser -match '(?m)^pub const MAX_ASSET_SCAN_DEPTH: usize = 6;$' -and $assetsBrowser -match '(?m)^pub const MAX_ASSET_SCAN_DIRECTORIES: usize = 256;$' -and $assetsBrowser -match '(?m)^pub const MAX_ASSET_MANIFESTS: usize = 32;$' -and $assetsBrowser -match '(?m)^pub const MAX_ASSET_MODELS: usize = 512;$' -and $assetsBrowser -match '(?m)^pub const MAX_ASSET_FILES: usize = 4_096;$') 'Scanner count budgets mismatch'
Require ($assetsBrowser -match 'MAX_ASSET_SCAN_DIRECTORIES \+ MAX_ASSET_FILES \+ MAX_ASSET_MANIFESTS' -and $assetsBrowser -match '(?m)^pub const MAX_ASSET_PATH_BYTES: usize = 256;$') 'Global entry or path budget mismatch'
Require ($assetsBrowser -match '(?m)^pub const MAX_ASSET_MANIFEST_BYTES: u64 = 256 \* 1024;$' -and $assetsBrowser -match '(?m)^pub const MAX_ASSET_MANIFEST_TOTAL_BYTES: u64 = 4 \* 1024 \* 1024;$' -and $assetsBrowser -match '(?m)^pub const MAX_ASSET_FILE_BYTES: u64 = 128 \* 1024 \* 1024;$' -and $assetsBrowser -match '(?m)^pub const MAX_ASSET_FILE_TOTAL_BYTES: u64 = 512 \* 1024 \* 1024;$') 'Manifest/file byte budgets mismatch'
Require ($assetsBrowser -match '(?m)^pub const MAX_GLTF_JSON_BYTES: u64 = 8 \* 1024 \* 1024;$' -and $assetsBrowser -match '(?m)^pub const MAX_GLTF_JSON_TOTAL_BYTES: u64 = 32 \* 1024 \* 1024;$' -and $assetsBrowser -match '(?m)^pub const MAX_GLTF_URIS_PER_SCENE: usize = 512;$' -and $assetsBrowser -match '(?m)^pub const MAX_GLTF_URIS_TOTAL: usize = 4_096;$') 'glTF budgets mismatch'

$pinBlock = [regex]::Match($assetsBrowser, '(?s)pub const GOVERNED_ASSET_SUBSET_PINS:.*?= &\[(.*?)\n\}\];').Value
Require ($pinBlock -match [regex]::Escape($assetManifest.subset_id) -and $pinBlock -match [regex]::Escape($assetManifest.source.pack_id) -and $pinBlock -match [regex]::Escape($assetManifest.license.spdx) -and $pinBlock -match [regex]::Escape($assetManifest.license.evidence_path)) 'Compiled governed tuple is incomplete'
foreach ($hash in @($expectedManifestHash, $assetManifest.source.archive_sha256, $assetManifest.source.normalized_metadata_sha256, $assetManifest.license.evidence_sha256)) {
    Require ($pinBlock -match (HashBytePattern $hash)) "Compiled governed hash pin is missing: $hash"
}

Require ($assetsBrowser -match 'for child in iterator \{\s*budget\.entries = checked_count' -and $assetsBrowser -match 'children\.sort_unstable_by') 'Directory collection is not bounded before sorting'
Require ($assetsBrowser -match 'return Err\(AssetBrowserError::NoSubsetManifests\)' -and $assetsBrowser -match 'UnpinnedSubset' -and $assetsBrowser -match 'GovernedPinMismatch') 'Missing, unpinned or forged manifests are not fail-closed'
Require ($assetsBrowser -match 'fn stable_asset_key' -and $assetsBrowser -match 'pack_id\.to_ascii_lowercase\(\)' -and $assetsBrowser -match 'model_id\.to_ascii_lowercase\(\)') 'Stable key is not lowercase pack/model governed'
Require ($assetsBrowser -match 'assets\.sort_unstable_by_key\(\|asset\| asset\.id\)' -and $assetsBrowser -match 'fn category_snapshots' -and $assetsBrowser -match 'BTreeMap') 'Snapshot ordering is not deterministic'
Require ($assetsBrowser -match 'reject_reparse' -and $assetsBrowser -match 'ensure_under_root' -and $assetsBrowser -match 'ForbiddenCadlePath' -and $assetsBrowser -match 'DuplicateFilePath') 'Filesystem rejection boundary is incomplete'
Require ($assetsBrowser -match 'fn normalize_gltf_uri' -and $assetsBrowser -match 'schemes, colons and control characters are forbidden' -and $assetsBrowser -match 'UndeclaredGltfUri' -and $assetsBrowser -match 'UnsupportedGlb') 'glTF URI or GLB fail-closed contract is incomplete'
Require ($assetsReadme -match 'fiable et\s+stable pendant l.appel' -and $assetsReadme -match 'mutation concurrente hostile' -and $assetsReadme -match 'Gate 4') 'Stable-checkout/TOCTOU/packaging limitation is not documented in forge_assets'

Require ($studioBrowser -match '(?m)^pub const BROWSE_ASSETS_REQUEST_SCHEMA_VERSION: u32 = 1;$' -and $studioBrowser -match '#\[serde\(deny_unknown_fields\)\]\s*pub struct BrowseAssetsRequest') 'Strict BrowseAssetsRequest v1 is missing'
Require ($studioBrowser -match 'if visible\.get\(\) && !started\.get_untracked\(\)' -and $studioBrowser -match 'Backend Assets absent') 'First-visible scan or browser fallback is missing'
Require ($studioDesktopState -match 'pub enum DesktopApp \{\s*Projects,\s*World,\s*Assets,\s*Diagnostics' -and $studioDesktopUi -match '<DockButton controls app=DesktopApp::Assets/>' -and $studioDesktopUi -match '"Assets"') 'Fourth Assets window/dock/menu integration is missing'
Require ($studioMain -match 'execute_browse_assets' -and $studioMain -match 'spawn_blocking\(scan_compiled_assets\)' -and $studioService -match 'compiled_asset_root') 'Typed native IPC or blocking scan offload is missing'
Require ($studioService -match 'if request\.request_serial == 0' -and $studioService -notmatch 'last_request_serial' -and $studioService -match 'scan_in_flight: Option<u64>' -and $studioService -match 'next_scan_ticket: u64') 'Client correlation or opaque server ticket contract mismatch'
Require ($studioService -match 'if self\.scan_in_flight\.is_some\(\)' -and $studioService -match 'self\.scan_in_flight != Some\(ticket\.server_ticket\)' -and $studioService -match 'self\.scan_in_flight = None') 'Single-flight exact-ticket release is missing'
Require ($studioService -match 'validate_snapshot' -and $studioService -match 'self\.cache = Some\(snapshot\.clone\(\)\)') 'Atomic validated cache replacement is missing'

$requiredTests = @(
    @{ source = $assetsBrowser; name = 'synthetic_snapshot_is_root_independent_sorted_and_hash_bound' },
    @{ source = $assetsBrowser; name = 'asset_identity_depends_on_source_pack_not_local_subset_id' },
    @{ source = $assetsBrowser; name = 'repository_subset_proves_one_manifest_nine_models_and_twenty_six_files' },
    @{ source = $assetsBrowser; name = 'unpinned_and_pin_tuple_changes_are_rejected' },
    @{ source = $assetsBrowser; name = 'gltf_buffer_and_image_uris_are_strictly_local_and_declared' },
    @{ source = $assetsBrowser; name = 'gltf_buffers_require_uris_and_glb_is_fail_closed' },
    @{ source = $assetsBrowser; name = 'massive_directory_is_stopped_during_streaming_before_sort' },
    @{ source = $assetsBrowser; name = 'empty_root_is_rejected_explicitly' },
    @{ source = $assetsBrowser; name = 'symbolic_link_is_rejected_when_platform_allows_fixture_creation' },
    @{ source = $studioBrowser; name = 'browse_request_is_strict_and_refresh_defaults_to_false' },
    @{ source = $studioBrowser; name = 'filter_covers_name_provenance_paths_category_empty_and_no_result' },
    @{ source = $studioBrowser; name = 'browse_assets_uses_its_own_strict_studio_ipc_envelope' },
    @{ source = $studioServiceTests; name = 'first_scan_hit_and_refresh_keep_one_valid_atomic_snapshot' },
    @{ source = $studioServiceTests; name = 'exactly_one_required_scan_can_be_in_flight_while_warm_hits_remain_available' },
    @{ source = $studioServiceTests; name = 'client_serial_is_only_correlation_and_survives_reload_or_high_values' },
    @{ source = $studioServiceTests; name = 'invalid_refresh_snapshot_cannot_replace_the_valid_cached_snapshot' },
    @{ source = $studioServiceTests; name = 'scan_failure_releases_only_its_exact_ticket_and_allows_retry' }
)
foreach ($test in $requiredTests) {
    Require ($test.source -match [regex]::Escape($test.name)) "Required source/test missing: $($test.name)"
}

$contract = $evidence.functional_contract
Require ($contract.snapshot_schema -ceq 'forge.asset-browser-snapshot' -and $contract.snapshot_schema_version -eq 1 -and $contract.snapshot_is_derived -and -not $contract.snapshot_is_persistent_project_state) 'Snapshot contract claim mismatch'
Require ($contract.dto_available_on_native_and_wasm -and $contract.filesystem_scanner_native_only -and -not $contract.network_access_performed -and $contract.accepted_manifest_filename -ceq 'subset.manifest.json' -and $contract.missing_manifest_is_error) 'Platform/offline manifest claim mismatch'
Require ($contract.maximum_scan_depth -eq 6 -and $contract.maximum_scan_directories -eq 256 -and $contract.maximum_manifests -eq 32 -and $contract.maximum_models -eq 512 -and $contract.maximum_files -eq 4096 -and $contract.maximum_global_scan_entries -eq (256 + 32 + 4096)) 'Scanner count claim mismatch'
Require ($contract.maximum_manifest_bytes -eq 262144 -and $contract.maximum_manifest_total_bytes -eq 4194304 -and $contract.maximum_file_bytes -eq 134217728 -and $contract.maximum_file_total_bytes -eq 536870912 -and $contract.maximum_portable_path_bytes -eq 256) 'Scanner byte/path claim mismatch'
Require ($contract.maximum_gltf_json_bytes -eq 8388608 -and $contract.maximum_gltf_json_total_bytes -eq 33554432 -and $contract.maximum_gltf_uris_per_scene -eq 512 -and $contract.maximum_gltf_uris_total -eq 4096) 'glTF budget claim mismatch'
Require ($contract.directory_collection_bounded_before_sort -and $contract.manifest_must_match_independent_forge_pin -and $contract.inventory_sizes_hashes_and_license_verified) 'Bounded collection or governed pin claim mismatch'
Require ($contract.stable_id_key -ceq 'lowercase(source.pack_id/model.id)' -and -not $contract.stable_id_depends_on_subset_id -and $contract.entries_sorted_by_asset_id) 'Stable identity claim mismatch'
Require ($contract.absolute_parent_dot_backslash_paths_rejected -and $contract.symlink_and_reparse_points_rejected -and $contract.cadle_components_rejected -and $contract.duplicate_or_undeclared_files_rejected) 'Filesystem rejection claim mismatch'
Require ($contract.gltf_buffers_and_images_must_resolve_to_local_declared_paths -and $contract.gltf_file_http_https_data_uri_schemes_rejected -and -not $contract.glb_supported) 'glTF claim mismatch'
Require ($contract.compiled_checkout_root_assumed_stable_during_scan -and -not $contract.hostile_concurrent_mutation_boundary_claimed -and $contract.packaged_runtime_root_deferred_to_gate -eq 4) 'TOCTOU/packaging boundary claim mismatch'
Require ($contract.assets_window_is_fourth_desktop_app -and $contract.scan_waits_for_first_visible_assets_window -and -not $contract.plain_browser_invents_catalog -and -not $contract.plain_browser_filesystem_access) 'Studio window/browser fallback claim mismatch'
Require ($contract.browse_request_schema_version -eq 1 -and -not $contract.browse_request_allows_user_root -and $contract.request_serial_must_be_positive -and -not $contract.request_serial_requires_server_monotonicity) 'Browse request claim mismatch'
Require ($contract.exactly_one_required_scan_in_flight -and $contract.scan_runs_via_spawn_blocking -and $contract.cache_survives_failed_refresh -and -not $contract.stale_server_ticket_can_release_or_replace_current_scan) 'Single-flight/cache claim mismatch'
Require (-not $contract.gate0_procedural_contract_semantically_changed -and -not $contract.placement_import_thumbnail_or_new_asset_delivered -and $contract.next_placement_route -ceq 'R2-027') 'Product scope claim mismatch'

$expectedReceipts = @(
    @{ id = 'locked_integrated_library_tests'; command = 'cargo test -p forge_assets -p forge_project -p forge_world -p forge_engine -p forge_studio --lib --locked'; status = 'PASS'; passed = 640 },
    @{ id = 'forge_studio_tests_bin'; command = 'cargo test -p forge_studio --bin forge-studio --locked'; status = 'PASS'; passed = 6 },
    @{ id = 'assets_studio_clippy_native_locked'; command = 'cargo clippy -p forge_assets -p forge_studio --all-targets --locked -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_studio_clippy_native_final'; command = 'cargo clippy -p forge_studio --all-targets --all-features -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_studio_check_wasm_web_locked'; command = 'cargo check -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web --locked'; status = 'PASS' },
    @{ id = 'forge_assets_clippy_wasm_locked'; command = 'cargo clippy -p forge_assets --target wasm32-unknown-unknown --locked -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_studio_clippy_wasm_web_locked'; command = 'cargo clippy -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web --locked -- -D warnings'; status = 'PASS' },
    @{ id = 'forge_studio_trunk_debug'; command = 'cmd /d /c "set NO_COLOR=true&&trunk build --locked --offline"'; status = 'PASS' },
    @{ id = 'cargo_fmt'; command = 'cargo fmt --all -- --check'; status = 'PASS' },
    @{ id = 'git_diff_check'; command = 'git diff --check'; status = 'PASS' }
)
$receipts = @($evidence.automated_validation.receipts)
Require ($evidence.automated_validation.raw_log_bundle -ceq 'NOT CAPTURED' -and $evidence.artifacts.Count -eq 0) 'Evidence must not invent a raw log bundle'
Require ($receipts.Count -eq $expectedReceipts.Count -and @($receipts.id | Sort-Object -Unique).Count -eq $receipts.Count) 'Unexpected or duplicate validation receipts'
foreach ($expected in $expectedReceipts) {
    $actual = @($receipts | Where-Object { $_.id -ceq $expected.id })
    Require ($actual.Count -eq 1) "Missing validation receipt: $($expected.id)"
    $actual = $actual[0]
    Require ($actual.command -ceq $expected.command -and $actual.status -ceq $expected.status) "Validation receipt mismatch: $($expected.id)"
    if ($expected.ContainsKey('passed')) {
        Require ($actual.passed -eq $expected.passed -and $actual.failed -eq 0) "Test count mismatch: $($expected.id)"
    }
}
$counts = $evidence.automated_validation.library_test_counts
Require ($counts.forge_assets -eq 21 -and $counts.forge_project -eq 149 -and $counts.forge_world -eq 133 -and $counts.forge_engine -eq 201 -and $counts.forge_studio -eq 136) 'Per-package library test counts mismatch'
Require ($evidence.automated_validation.locked_library_suite_tests_passed -eq (21 + 149 + 133 + 201 + 136)) 'Locked library total mismatch'
Require ($evidence.automated_validation.studio_binary_tests_passed_separately -eq 6 -and $evidence.automated_validation.asset_browser_unit_tests_passed_after_cross_platform_fixture -eq 3) 'Studio binary or targeted Assets test count mismatch'
Require (@($receipts | Where-Object id -CEQ 'forge_studio_tests_bin')[0].excluded_from_locked_library_total) 'Studio binary tests must remain outside the 640 library total'
Require (@($receipts | Where-Object id -CEQ 'assets_studio_clippy_native_locked')[0].scope_note -match 'before three later native-only Studio') 'Combined native Clippy timing boundary is hidden'
Require (@($receipts | Where-Object id -CEQ 'forge_studio_clippy_native_final')[0].scope_note -match 'final Studio native source') 'Final Studio native Clippy scope is hidden'

$recovery = $evidence.automated_validation.disk_space_recovery
Require ($recovery.initial_integrated_test_attempt -ceq 'FAIL — LNK1318: Erreur PDB inattendue; LIMIT (12)' -and $recovery.initial_integrated_test_free_space_mib -eq 33 -and -not $recovery.initial_integrated_test_disk_space_is_confirmed_cause -and $recovery.initial_trunk_build_attempt -ceq 'FAIL — no space left on device') 'Initial failure context is hidden or overstated'
Require ($recovery.forge_studio_clean_reclaimed_gib -eq 3.1 -and $recovery.workspace_package_cleans_reclaimed_gib -eq 16.9 -and $recovery.removed_only_regenerable_build_artifacts -and -not $recovery.source_or_asset_files_removed) 'Disk-space recovery claim mismatch'
Require ($recovery.final_integrated_test -ceq 'PASS' -and $recovery.final_trunk_debug_build -ceq 'PASS' -and $recovery.raw_log -ceq 'NOT CAPTURED') 'Final recovery outcome mismatch'
$wasmDiagnostic = $evidence.automated_validation.additional_wasm_tests_diagnostic
Require ($wasmDiagnostic.status -ceq 'BLOCKED BY PRE-EXISTING NATIVE IMPORT' -and $wasmDiagnostic.blocker -ceq 'area_preview imports native-only workspace_service' -and -not $wasmDiagnostic.route_failure -and -not $wasmDiagnostic.full_wasm_test_harness_pass_claimed -and $wasmDiagnostic.standard_wasm_product_check_rerun -ceq 'PASS') 'WASM test-harness diagnostic is overstated or hidden'

Require ($evidence.native_ui_qualification.status -ceq 'NOT RUN') 'Native UI must remain explicitly NOT RUN'
Require ($evidence.release_qualification.standalone_native_build -ceq 'NOT RUN' -and $evidence.release_qualification.native_release_build -ceq 'NOT RUN' -and $evidence.release_qualification.trunk_release_build -ceq 'NOT RUN') 'Native standalone/Release builds must remain explicitly NOT RUN'
Require ($evidence.gpu_qualification.status -ceq 'NOT RUN' -and $evidence.performance_qualification.status -ceq 'NOT RUN') 'GPU/performance must remain explicitly NOT RUN'
Require ($evidence.remote_ci.status -ceq 'NOT RUN — infrastructure billing' -and $evidence.remote_ci.decision -ceq 'ADR-0006') 'Wrong remote CI exception'
Require ($evidence.presentmon.status -ceq 'NOT RUN') 'PresentMon must remain explicitly NOT RUN'

$forbiddenPassClaims = @(
    '(?i)(native UI|UI native|inspection visuelle native)[^\r\n]*\bPASS\b',
    '(?i)(native release|build natif autonome|build natif Release|trunk release)[^\r\n]*\bPASS\b',
    '(?i)\bGPU\b[^\r\n]*\bPASS\b',
    '(?i)\bPresentMon\b[^\r\n]*\bPASS\b',
    '(?i)(remote CI|CI distante)[^\r\n]*\bPASS\b'
)
$evidenceLines = @(Get-Content -LiteralPath $reportPath) + @(Get-Content -LiteralPath $evidencePath)
foreach ($line in $evidenceLines) {
    foreach ($pattern in $forbiddenPassClaims) {
        Require ($line -notmatch $pattern) "Forbidden qualification claim: $line"
    }
}
Require ($report -match 'R2-026 est `PASS`' -and $report -match 'Gate 2 atteint 25/43' -and $report -match 'R2-027`, drag/drop') 'Report route state mismatch'
Require ($report -match [regex]::Escape($expectedManifestHash) -and $report -match '1 manifeste, 9 modèles et 27 fichiers déclarés' -and $report -match '26 fichiers de\s+contenu plus une preuve de licence') 'Report hides the governed manifest or inventory'
Require ($report -match 'exactement trois métadonnées Cargo' -and $report -match 'Aucun nouveau package\s+tiers ni aucune nouvelle version' -and $report -match 'Aucun asset existant n.est modifié') 'Report hides the Cargo/asset boundary'
Require ($report -match '3,1 Gio' -and $report -match '16,9 Gio' -and $report -match 'Aucun journal brut n.est revendiqué') 'Report hides disk recovery or raw-log boundary'
Require ($report -match 'mutation concurrente\s+hostile \(TOCTOU\)' -and $report -match 'racine d.installation packagée appartient à Gate 4') 'Report hides stable-root/TOCTOU/packaging boundary'
Require ($report -match 'R2-026 ne place, n.importe et ne génère aucun thumbnail' -and $report -match 'drag/drop et le placement commencent en R2-027') 'Report overstates placement/import scope'

Write-Output "PASS: R2-026 evidence is bound to commit $($expectedSourceCommit.Substring(0, 8)), tree $($expectedSourceTree.Substring(0, 8)) and its exact 14-file inventory."
Write-Output "PASS: governed manifest $($expectedManifestHash.Substring(0, 8)) proves 1 manifest, 9 models, 26 content files plus 1 licence file."
Write-Output 'PASS: 640 locked library tests plus 6 separate Studio binary tests; strict declared native/WASM checks and Trunk debug build.'
Write-Output 'NOT RUN: native UI, standalone native/Release builds, GPU, performance, remote CI (infrastructure billing) and PresentMon.'
