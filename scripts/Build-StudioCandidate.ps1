#Requires -Version 7.2
# Local candidate evidence only: no publication, launch, or qualification verdict.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Frontend,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$ExpectedWasmSha256,
    [Parameter(Mandatory)][string]$Output,
    [string]$BuildCacheDirectory = 'E:/OpenRPG_Forge/.gate1-build-cache/terrain-water-20260906',
    [string]$TemporaryDirectory = 'E:/OpenRPG_Forge/.gate1-build-cache/terrain-water-temp-20260906',
    [string]$GovernedKit,
    [ValidateSet('debug','release')][string]$ExpectedNavigationWorkerConfiguration = 'release',
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$BinaryName = 'forge-studio',
    [Nullable[int]]$WindowX,
    [Nullable[int]]$WindowY,
    [ValidateRange(1100,7680)][int]$WindowWidth = 1920,
    [ValidateRange(700,4320)][int]$WindowHeight = 1080,
    [switch]$Authoring,
    [switch]$BridgeProbe,
    [switch]$TemporalLodProbe,
    [switch]$QualityMotionProbe,
    [switch]$PrewarmedQualityMotionProbe,
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$FixedView,
    [switch]$CandidateView,
    [switch]$WaterDepthPreview,
    [switch]$WaterMediumPreview,
    [switch]$WaterReflectionPreview,
    [switch]$CloudPreview,
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (($null -eq $WindowX) -ne ($null -eq $WindowY)) {
    throw 'WindowX and WindowY must be supplied together'
}
$repository = Split-Path -Parent $PSScriptRoot
$studio = Join-Path $repository 'apps/forge_studio'
# `cargo tauri build` compiles the manifest's [[bin]] target. Accepting another
# filename can silently copy an older executable left in the shared cache.
if ($BinaryName -ne 'forge-studio') {
    throw 'BinaryName must match apps/forge_studio/Cargo.toml [[bin]]: forge-studio'
}
$frontendRoot = (Resolve-Path -LiteralPath $Frontend).Path
if ($Authoring) {
    if (-not $GovernedKit) { throw 'Authoring requires an explicit GovernedKit source' }
    if ($BridgeProbe -or $TemporalLodProbe -or $QualityMotionProbe -or $PrewarmedQualityMotionProbe -or
        $FixedView -or $CandidateView -or $WaterDepthPreview -or $WaterMediumPreview -or
        $WaterReflectionPreview -or $CloudPreview) {
        throw 'Authoring excludes Gate 1 scene and probe options'
    }
}
if ($BridgeProbe -and $FixedView) { throw 'BridgeProbe and FixedView are mutually exclusive' }
if ($QualityMotionProbe -and -not $BridgeProbe) { throw 'QualityMotionProbe requires BridgeProbe' }
if ($TemporalLodProbe -and (-not $BridgeProbe -or $QualityMotionProbe -or $PrewarmedQualityMotionProbe)) {
    throw 'TemporalLodProbe requires BridgeProbe without a quality-motion campaign'
}
if ($PrewarmedQualityMotionProbe -and (-not $BridgeProbe -or $QualityMotionProbe)) {
    throw 'PrewarmedQualityMotionProbe requires BridgeProbe and excludes the cold QualityMotionProbe'
}
if (([int][bool]$WaterDepthPreview + [int][bool]$WaterMediumPreview + [int][bool]$WaterReflectionPreview) -gt 1) {
    throw 'Choose one water preview'
}
if ($CandidateView -and -not $FixedView) { throw 'CandidateView requires FixedView' }
if ($CandidateView -and -not (Test-Path -LiteralPath (Join-Path $frontendRoot 'forge-views/world-sketch.json') -PathType Leaf)) {
    throw 'Candidate view document missing'
}
$outputRoot = [IO.Path]::GetFullPath($Output)
if ([IO.Path]::GetPathRoot($outputRoot) -ne 'E:\') { throw 'Candidate output must stay on E:' }
if (Test-Path -LiteralPath $outputRoot) { throw 'Output already exists; refusing overwrite' }
$branch = & git -C $repository branch --show-current
if ($LASTEXITCODE -ne 0 -or $branch -ne 'Rust') { throw 'Requires the existing Rust branch' }
if (-not (Test-Path -LiteralPath (Join-Path $frontendRoot 'index.html') -PathType Leaf)) {
    throw 'Frontend index.html missing'
}
if ($Authoring) {
    $entry = [IO.File]::ReadAllText((Join-Path $frontendRoot 'index.html'))
    if ($entry -match 'data-trunk\s+rel="rust"') {
        throw 'Authoring frontend still has the unbuilt Trunk source index.html'
    }
    if (Test-Path -LiteralPath (Join-Path $frontendRoot 'forge-imports/forge-imports')) {
        throw 'Authoring imports are nested one level too deep'
    }
    $frontendModule = Join-Path $frontendRoot 'forge_studio.js'
    if (-not (Test-Path -LiteralPath $frontendModule -PathType Leaf)) {
        throw 'Authoring frontend JavaScript module missing'
    }
    $moduleSource = [IO.File]::ReadAllText($frontendModule)
    if ($moduleSource -notmatch 'wasm\.start\(\)' -or
        $moduleSource -notmatch 'wasm\.__wbindgen_start\(\)') {
        throw 'Authoring requires the web frontend startup; build WASM with --no-default-features --features web'
    }
}
$wasm = Join-Path $frontendRoot 'forge_studio_bg.wasm'
if ((Get-FileHash -LiteralPath $wasm).Hash -ne $ExpectedWasmSha256) { throw 'WASM hash mismatch' }
foreach ($workerArtifact in @('forge_navigation.js','forge_navigation_bg.wasm','forge-navigation-worker.js','forge-navigation-worker-manifest.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $frontendRoot $workerArtifact) -PathType Leaf)) {
        throw "Navigation worker artifact missing: $workerArtifact. Run Build-NavigationWorker.ps1 for this frontend."
    }
}
$workerManifest = Get-Content -LiteralPath (Join-Path $frontendRoot 'forge-navigation-worker-manifest.json') -Raw | ConvertFrom-Json
if ($workerManifest.schema -ne 'forge.navigation-worker-build' -or $workerManifest.version -ne 1 -or
    $workerManifest.build_only -ne $true -or $workerManifest.runtime_verified -ne $false -or
    $workerManifest.source_unchanged_during_build -ne $true -or
    $workerManifest.configuration -ne $ExpectedNavigationWorkerConfiguration) { throw 'Invalid navigation worker manifest/configuration.' }
$expectedWorkerArtifacts = @('forge_navigation.js','forge_navigation_bg.wasm','forge-navigation-worker.js')
$expectedWorkerInputs = @('Cargo.toml','Cargo.lock','crates/forge_navigation/Cargo.toml','crates/forge_navigation/build.rs',
    'third_party/rerecast-0.4.0/Cargo.toml','apps/forge_studio/forge-navigation-worker.js')
foreach ($workerSourceDirectory in @('crates/forge_navigation/src','third_party/rerecast-0.4.0/src')) {
    $expectedWorkerInputs += @(Get-ChildItem -LiteralPath (Join-Path $repository $workerSourceDirectory) -Recurse -File | ForEach-Object {
        [IO.Path]::GetRelativePath($repository,$_.FullName).Replace('\','/')
    })
}
$expectedWorkerInputs = @($expectedWorkerInputs | Sort-Object -Unique)
foreach ($workerSet in @(
    @{ actual=@($workerManifest.artifacts.path); expected=$expectedWorkerArtifacts },
    @{ actual=@($workerManifest.source_pins.path); expected=$expectedWorkerInputs }
)) {
    if ($workerSet.actual.Count -ne $workerSet.expected.Count -or
        @($workerSet.actual | Sort-Object -Unique).Count -ne $workerSet.actual.Count -or
        @(Compare-Object -CaseSensitive -ReferenceObject $workerSet.expected -DifferenceObject $workerSet.actual).Count -ne 0) {
        throw 'Navigation worker manifest has incomplete or duplicate pins.'
    }
}
foreach ($workerArtifact in $workerManifest.artifacts) {
    if ($workerArtifact.path -notin @('forge_navigation.js','forge_navigation_bg.wasm','forge-navigation-worker.js')) { throw 'Unexpected navigation worker artifact path.' }
    if ((Get-FileHash -LiteralPath (Join-Path $frontendRoot $workerArtifact.path)).Hash.ToLowerInvariant() -ne $workerArtifact.sha256) { throw 'Navigation worker artifact hash mismatch.' }
}
foreach ($workerInput in $workerManifest.source_pins) {
    if ($workerInput.path -match '(^|[/\\])\.\.([/\\]|$)' -or [IO.Path]::IsPathRooted($workerInput.path)) { throw 'Invalid navigation worker source path.' }
    if ((Get-FileHash -LiteralPath (Join-Path $repository $workerInput.path)).Hash.ToLowerInvariant() -ne $workerInput.sha256) { throw 'Navigation worker source is stale.' }
}
if ((Get-Item -LiteralPath $frontendRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'Frontend reparse points are not supported'
}
$entries = @(Get-ChildItem -LiteralPath $frontendRoot -Recurse -Force)
if ($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
    throw 'Frontend reparse points are not supported'
}
$pins = @($entries | Where-Object { -not $_.PSIsContainer } | Sort-Object FullName | ForEach-Object {
    [ordered]@{ path = [IO.Path]::GetRelativePath($frontendRoot, $_.FullName); sha256 = (Get-FileHash -LiteralPath $_.FullName).Hash }
})
$inputPaths = @{}
foreach ($pin in $pins) { $inputPaths[$pin.path] = Join-Path $frontendRoot $pin.path }
if ($GovernedKit) {
    if (Test-Path -LiteralPath (Join-Path $frontendRoot 'forge-kit')) {
        throw 'Frontend already contains a kit; refusing an ambiguous replacement'
    }
    $kitRoot = (Resolve-Path -LiteralPath $GovernedKit).Path
    if ((Get-Item -LiteralPath $kitRoot).Attributes -band [IO.FileAttributes]::ReparsePoint -or
        (Get-ChildItem -LiteralPath $kitRoot -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })) {
        throw 'Kit reparse points are not supported'
    }
    $kitBuild = Get-Content -LiteralPath (Join-Path $kitRoot 'build.json') -Raw | ConvertFrom-Json
    if ($kitBuild.schema -ne 'forge.blender-asset-kit-build' -or -not $kitBuild.qualification.technical_export_validated) {
        throw 'Kit requires a technically validated build report'
    }
    foreach ($name in @('preview.json', 'prototype-catalog.json')) {
        $document = Get-Content -LiteralPath (Join-Path $kitRoot $name) -Raw | ConvertFrom-Json
        if ($document.asset_kit_id -ne $kitBuild.asset_kit_id -or $document.asset_kit_revision -ne $kitBuild.asset_kit_revision) {
            throw 'Kit document identity/revision mismatch'
        }
    }
    $kitFiles = @('build.json', 'preview.json', 'prototype-catalog.json')
    foreach ($asset in $kitBuild.assets) {
        $relative = [string]$asset.glb.path
        if ($relative -notmatch '^assets/[A-Za-z0-9_./-]+\.glb$' -or $relative.Split('/') -contains '..') {
            throw 'Unsafe kit asset path'
        }
        $assetPath = [IO.Path]::GetFullPath((Join-Path $kitRoot $relative))
        if (-not $assetPath.StartsWith($kitRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Kit asset escapes source directory'
        }
        if ((Get-FileHash -LiteralPath $assetPath).Hash -ne $asset.glb.sha256) { throw 'Kit asset hash mismatch' }
        $kitFiles += $relative
    }
    foreach ($name in $kitFiles | Select-Object -Unique) {
        $relative = 'forge-kit/' + $name
        $inputPaths[$relative] = Join-Path $kitRoot $name
        $pins += [ordered]@{ path = $relative; sha256 = (Get-FileHash -LiteralPath $inputPaths[$relative]).Hash }
    }
}
$entryUrl = 'index.html?forge_scene=world-sketch-visual-valley&forge_kit=governed&forge_forest=batches'
if ($Authoring) { $entryUrl = 'index.html?authoring=1' }
if ($BridgeProbe) { $entryUrl += '&forge_probe=bridge' }
if ($TemporalLodProbe) { $entryUrl += '&forge_temporal_lod=1' }
if ($QualityMotionProbe) { $entryUrl += '&forge_quality_motion=1' }
if ($PrewarmedQualityMotionProbe) { $entryUrl += '&forge_quality_motion=prewarmed' }
if ($FixedView) { $entryUrl += "&forge_view=$FixedView" }
if ($CandidateView) { $entryUrl += '&forge_view_input=candidate' }
if ($WaterDepthPreview) { $entryUrl += '&forge_water=depth-preview' }
if ($WaterMediumPreview) { $entryUrl += '&forge_water=clear-medium-preview' }
if ($WaterReflectionPreview) { $entryUrl += '&forge_water=clear-reflections-preview' }
if ($CloudPreview) { $entryUrl += '&forge_clouds=preview' }
if ($ValidateOnly) {
    [ordered]@{ status = 'INPUTS_VALID'; files = $pins.Count; build_executed = $false; binary_name = $BinaryName; window_x = $WindowX; window_y = $WindowY;
        window_width = $WindowWidth; window_height = $WindowHeight;
        entry_url = $entryUrl; authoring = [bool]$Authoring; bridge_probe = [bool]$BridgeProbe } | ConvertTo-Json
    return
}
# Keep the copied frontend separate from its input; generated output is never a source.
$null = New-Item -ItemType Directory -Path $outputRoot
$bundle = Join-Path $outputRoot 'web'
$null = New-Item -ItemType Directory -Path $bundle
foreach ($pin in $pins) {
    $destination = Join-Path $bundle $pin.path
    $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination)
    Copy-Item -LiteralPath $inputPaths[$pin.path] -Destination $destination
    if ((Get-FileHash -LiteralPath $destination).Hash -ne $pin.sha256) { throw 'Frontend changed during copy' }
}
# A Windows absolute forward-slash path is parsed by Tauri as a URL (scheme e:).
$relativeBundle = [IO.Path]::GetRelativePath($studio, $bundle).Replace('\', '/')
if ([IO.Path]::IsPathRooted($relativeBundle) -or $relativeBundle -match '^[A-Za-z][A-Za-z0-9+.-]*:') {
    throw 'frontendDist must be a relative directory, never a drive URL'
}
$windowTitle = if ($Authoring) { 'OpenRPG Forge Studio · Édition locale' } else { 'OpenRPG Forge Studio · Gate 1 candidat local' }
$config = [ordered]@{
    build = @{ beforeBuildCommand = $null; frontendDist = $relativeBundle }
    app = @{ windows = @(@{ label = 'main'; title = $windowTitle;
        url = $entryUrl;
        width = $WindowWidth; height = $WindowHeight; minWidth = 1100; minHeight = 700; resizable = $true;
        dragDropEnabled = -not [bool]$Authoring }) }
    bundle = @{ active = $false }
}
if ($null -ne $WindowX) {
    $config.app.windows[0].x = $WindowX
    $config.app.windows[0].y = $WindowY
}
$configPath = Join-Path $outputRoot 'tauri-candidate.conf.json'
$config | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $configPath -Encoding utf8
$savedEnvironment = @{}
foreach ($name in @('CARGO_TARGET_DIR', 'CARGO_INCREMENTAL', 'RUSTFLAGS', 'TEMP', 'TMP', 'NO_COLOR', 'CARGO_NET_OFFLINE')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    $env:CARGO_TARGET_DIR = [IO.Path]::GetFullPath($BuildCacheDirectory)
    $env:CARGO_INCREMENTAL = '0'
    $env:RUSTFLAGS = '-C debuginfo=0'
    $env:TEMP = [IO.Path]::GetFullPath($TemporaryDirectory)
    $env:TMP = $env:TEMP
    $null = New-Item -ItemType Directory -Force -Path $env:CARGO_TARGET_DIR, $env:TEMP
    $env:NO_COLOR = 'true'
    $env:CARGO_NET_OFFLINE = 'true'
    Push-Location $studio
    try {
        & cargo tauri build --debug --no-bundle --config $configPath -- --locked *> (Join-Path $outputRoot 'build.log')
        if ($LASTEXITCODE -ne 0) { throw "Tauri build failed: $LASTEXITCODE; log retained" }
    } finally { Pop-Location }
    # This name comes from [[bin]], not the CLI's package-name display.
    $executable = Join-Path $outputRoot 'forge-studio-candidate.exe'
    Copy-Item -LiteralPath (Join-Path $env:CARGO_TARGET_DIR "debug/$BinaryName.exe") -Destination $executable
    foreach ($pin in $pins) {
        foreach ($inputFile in @($inputPaths[$pin.path], (Join-Path $bundle $pin.path))) {
            if ((Get-FileHash -LiteralPath $inputFile).Hash -ne $pin.sha256) {
                throw 'Frontend pin changed during build; candidate not verified'
            }
        }
    }
    [ordered]@{ status = 'BUILD_ONLY'; recorded_at_utc = [DateTime]::UtcNow.ToString('o');
        branch = $branch; binary_name = $BinaryName; executable_sha256 = (Get-FileHash -LiteralPath $executable).Hash;
        build_cache_directory = $env:CARGO_TARGET_DIR; temporary_directory = $env:TEMP;
        entry_url = $entryUrl; authoring = [bool]$Authoring; bridge_probe = [bool]$BridgeProbe;
        frontend_pins = $pins; runtime_verified = $false; gate_qualified = $false
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputRoot 'build-proof.json') -Encoding utf8
    Write-Output "BUILD_ONLY: $executable (runtime and gate not qualified)"
} finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
}
