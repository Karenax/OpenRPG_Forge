# Packaging-input fixtures only; no GLB, Rust build or graphical qualification.
param([Parameter(Mandatory)][string]$Output)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (Test-Path -LiteralPath $Output) { throw 'Fresh fixture output required' }
$root = [IO.Path]::GetFullPath($Output)
if ([IO.Path]::GetPathRoot($root) -ne 'E:\') { throw 'Fixtures must stay on E:' }
$frontend = Join-Path $root 'frontend'
$kit = Join-Path $root 'kit'
$null = New-Item -ItemType Directory -Path $frontend, (Join-Path $kit 'assets') -Force
'fixture' | Set-Content (Join-Path $frontend 'index.html')
'not a compiled wasm' | Set-Content (Join-Path $frontend 'forge_studio_bg.wasm')
'not a real glb' | Set-Content (Join-Path $kit 'assets/fixture.glb')
$wasmHash = (Get-FileHash (Join-Path $frontend 'forge_studio_bg.wasm')).Hash
$build = @{ schema = 'forge.blender-asset-kit-build'; asset_kit_id = 'fixture'; asset_kit_revision = 1;
    qualification = @{ technical_export_validated = $true };
    assets = @(@{ glb = @{ path = 'assets/fixture.glb'; sha256 = (Get-FileHash (Join-Path $kit 'assets/fixture.glb')).Hash } }) }
function Save-Build { $build | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $kit 'build.json') }
Save-Build
foreach ($name in @('preview.json', 'prototype-catalog.json')) {
    @{ asset_kit_id = 'fixture'; asset_kit_revision = 1 } | ConvertTo-Json | Set-Content (Join-Path $kit $name)
}
$parameters = @{ Frontend = $frontend; ExpectedWasmSha256 = $wasmHash; Output = (Join-Path $root 'candidate'); ValidateOnly = $true }
$builder = Join-Path $PSScriptRoot 'Build-StudioCandidate.ps1'
$plain = & $builder @parameters | ConvertFrom-Json
if ($null -ne $plain.window_x -or $null -ne $plain.window_y) { throw 'Default window position changed' }
if ($plain.window_width -ne 1920 -or $plain.window_height -ne 1080) { throw 'Default window size changed' }
$sized = & $builder @parameters -WindowWidth 1600 -WindowHeight 900 | ConvertFrom-Json
if ($sized.window_width -ne 1600 -or $sized.window_height -ne 900 -or $sized.build_executed) { throw 'Explicit window size lost' }
$positioned = & $builder @parameters -WindowX -1920 -WindowY 52 | ConvertFrom-Json
if ($positioned.window_x -ne -1920 -or $positioned.window_y -ne 52) { throw 'Explicit window coordinates lost' }
try {
    & $builder @parameters -WindowX -1920 | Out-Null
    throw 'Incomplete position accepted'
} catch {
    if ($_.Exception.Message -notlike '*WindowX and WindowY must be supplied together*') { throw }
}
if ($plain.files -ne 2 -or $plain.build_executed) { throw 'Plain validation failed' }
$parameters.GovernedKit = $kit
$withKit = & $builder @parameters | ConvertFrom-Json
if ($withKit.files -ne 6 -or $withKit.build_executed -or (Test-Path $parameters.Output)) { throw 'Kit validation mutated output or wrong pins' }
if ($withKit.bridge_probe -or $withKit.entry_url -ne 'index.html?forge_scene=world-sketch-visual-valley&forge_kit=governed&forge_forest=batches') { throw 'Default scene changed' }
$probe = & $builder @parameters -BridgeProbe | ConvertFrom-Json
if (-not $probe.bridge_probe -or $probe.entry_url -ne ($withKit.entry_url + '&forge_probe=bridge') -or
    $probe.files -ne $withKit.files -or $probe.build_executed -or (Test-Path $parameters.Output)) { throw 'Bridge probe validation failed' }
function Assert-Rejected([string]$Message) {
    try { & $builder @parameters | Out-Null } catch {
        if ($_.Exception.Message -like "*$Message*") { return }
        throw
    }
    throw "Expected rejection: $Message"
}
$authoring = & $builder @parameters -Authoring | ConvertFrom-Json
if (-not $authoring.authoring -or $authoring.entry_url -ne 'index.html?authoring=1' -or
    $authoring.files -ne $withKit.files -or $authoring.build_executed -or (Test-Path $parameters.Output)) {
    throw 'Authoring route omitted kit pins or mutated output'
}
$parameters.Authoring = $true
foreach ($option in @('BridgeProbe', 'TemporalLodProbe', 'QualityMotionProbe', 'PrewarmedQualityMotionProbe',
    'CandidateView', 'WaterDepthPreview', 'WaterMediumPreview', 'WaterReflectionPreview', 'CloudPreview', 'FixedView')) {
    $parameters[$option] = if ($option -eq 'FixedView') { 'human_bridge' } else { $true }
    Assert-Rejected 'Authoring excludes'
    $parameters.Remove($option)
}
$parameters.Remove('GovernedKit')
Assert-Rejected 'Authoring requires an explicit GovernedKit'
$parameters.GovernedKit = $kit
$parameters.Remove('Authoring')
$motion = & $builder @parameters -BridgeProbe -QualityMotionProbe | ConvertFrom-Json
if ($motion.entry_url -ne ($probe.entry_url + '&forge_quality_motion=1') -or $motion.build_executed) {
    throw 'Quality motion route differs'
}
$parameters.QualityMotionProbe = $true
Assert-Rejected 'QualityMotionProbe requires BridgeProbe'
$parameters.Remove('QualityMotionProbe')
$cloud = & $builder @parameters -WaterReflectionPreview -CloudPreview | ConvertFrom-Json
if ($cloud.entry_url -ne ($withKit.entry_url + '&forge_water=clear-reflections-preview&forge_clouds=preview') -or
    $cloud.files -ne $withKit.files -or $cloud.build_executed -or (Test-Path $parameters.Output)) {
    throw 'Integrated cloud route changed inputs or omitted effects'
}
$fixed = & $builder @parameters -FixedView human_bridge | ConvertFrom-Json
if ($fixed.entry_url -ne ($withKit.entry_url + '&forge_view=human_bridge')) { throw 'Fixed view URL differs' }
$optical = & $builder @parameters -FixedView human_bridge -WaterDepthPreview | ConvertFrom-Json
if ($optical.entry_url -ne ($fixed.entry_url + '&forge_water=depth-preview') -or
    $optical.files -ne $withKit.files -or $optical.build_executed -or (Test-Path $parameters.Output)) { throw 'Optical candidate validation failed' }
$parameters.BridgeProbe = $true
foreach ($option in @('WaterMediumPreview', 'WaterReflectionPreview')) {
    $waterOptions = @{ $option = $true }
    $result = & $builder @parameters @waterOptions | ConvertFrom-Json
    $mode = if ($option -eq 'WaterMediumPreview') { 'clear-medium-preview' } else { 'clear-reflections-preview' }
    if ($result.entry_url -ne ($probe.entry_url + '&forge_water=' + $mode) -or
        $result.files -ne $withKit.files -or $result.build_executed -or (Test-Path $parameters.Output)) {
        throw 'Water route or input immutability differs'
    }
}
foreach ($pair in @(
    @('WaterDepthPreview', 'WaterMediumPreview'),
    @('WaterDepthPreview', 'WaterReflectionPreview'),
    @('WaterMediumPreview', 'WaterReflectionPreview'),
    @('WaterDepthPreview', 'WaterMediumPreview', 'WaterReflectionPreview')
)) {
    foreach ($option in $pair) { $parameters[$option] = $true }
    Assert-Rejected 'Choose one water preview'
    foreach ($option in $pair) { $parameters.Remove($option) }
}
$parameters.FixedView = 'human_bridge'
Assert-Rejected 'mutually exclusive'
$parameters.Remove('BridgeProbe')
$parameters.Remove('FixedView')
$parameters.CandidateView = $true
Assert-Rejected 'requires FixedView'
$parameters.FixedView = 'human_bridge'
Assert-Rejected 'document missing'
$parameters.Remove('CandidateView')
$parameters.Remove('FixedView')
$build.assets[0].glb.sha256 = '0' * 64
Save-Build
Assert-Rejected 'hash mismatch'
$build.assets[0].glb.path = 'assets/../outside.glb'
Save-Build
Assert-Rejected 'Unsafe kit asset path'
$build.asset_kit_revision = 2
Save-Build
Assert-Rejected 'identity/revision mismatch'
$null = New-Item -ItemType Directory (Join-Path $frontend 'forge-kit')
Assert-Rejected 'already contains a kit'
'STUDIO_KIT_INPUT_TESTS_PASS: including authoring kit and integrated cloud routes; no build or output mutation'
