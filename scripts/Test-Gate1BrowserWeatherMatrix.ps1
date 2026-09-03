[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$driverPath = Join-Path $PSScriptRoot 'Invoke-Gate1BrowserWeatherMatrix.ps1'
$configPath = Join-Path $PSScriptRoot 'gate1-browser-weather-matrix.v1.json'
$protocolPath = Join-Path $repositoryRoot 'docs/evidence/gate1/R1_023_026_BROWSER_WEATHER_MATRIX_PROTOCOL.md'
$roadmapPath = Join-Path $repositoryRoot 'docs/planning/MIGRATION_ROADMAP.md'
$trunkPath = Join-Path $PSScriptRoot 'Invoke-StudioTrunk.ps1'
$studioSourcePath = Join-Path $repositoryRoot 'apps/forge_studio/src/lib.rs'
$matrixSourcePath = Join-Path $repositoryRoot 'apps/forge_studio/src/gate1_browser_weather_matrix.rs'
$matrixWebSourcePath = Join-Path $repositoryRoot 'apps/forge_studio/src/gate1_browser_weather_matrix_web.rs'
$legacyTemporalPaths = [ordered]@{
    'scripts/Invoke-Gate1BrowserTemporal.ps1' = 'a278c4b703d3710415cbed5839078f8eca967bd00bdac2f6007ebefa3d0e5cad'
    'scripts/gate1-browser-temporal.v1.json' = 'f27165c409a89a5dd3b2e4433ac0ce15e4f95b933775c4b9e79151d7fdc3623a'
    'scripts/Test-Gate1BrowserTemporal.ps1' = 'ec955d8af2b5d2099094d60db377c2d849306636b7f659af9a4335356f08c448'
}

function Assert-Condition {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

foreach ($path in @($driverPath, $configPath, $protocolPath, $roadmapPath, $trunkPath, $studioSourcePath, $matrixSourcePath, $matrixWebSourcePath)) {
    Assert-Condition -Condition (Test-Path -LiteralPath $path -PathType Leaf) -Message "Required Browser weather matrix file is missing: $path"
}

foreach ($scriptPath in @($driverPath, $PSCommandPath, $trunkPath)) {
    $tokens = $null
    $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$errors)
    $parserDetails = @($errors | ForEach-Object { $_.Message }) -join ' | '
    Assert-Condition -Condition ($errors.Count -eq 0) -Message "PowerShell parser rejected '$scriptPath': $parserDetails"
}

$validation = @(& pwsh -NoProfile -File $driverPath -ConfigPath $configPath -ValidateOnly 2>&1)
Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "Driver config validation failed: $($validation -join [Environment]::NewLine)"
$selfTest = @(& pwsh -NoProfile -File $driverPath -ConfigPath $configPath -SelfTest 2>&1)
Assert-Condition -Condition ($LASTEXITCODE -eq 0) -Message "Driver self-test failed: $($selfTest -join [Environment]::NewLine)"

$driver = Get-Content -LiteralPath $driverPath -Raw
foreach ($requiredToken in @(
    'Page.addScriptToEvaluateOnNewDocument',
    '--remote-allow-origins=*',
    '__forgeGate1BrowserWeatherMatrixInputGuard',
    'forge-gate1-browser-weather-proof-advance',
    'forge-gate1-browser-weather-proof-ledger',
    'Page.captureScreenshot',
    'Runtime.consoleAPICalled',
    'Network.responseReceived',
    'Get-ServedBundleEvidence',
    'Browser weather acquisition requires a completely clean Git worktree',
    'Browser weather evidence must be written outside the Git worktree',
    'blocked_input_events',
    "headed_requested = `$true",
    "headed_observed = `$false",
    'pass_pending_human_and_precise_gpu_evidence',
    "r1_023 = 'open'",
    "r1_025 = 'open'",
    "r1_026 = 'open'",
    "human_review_performed = `$false",
    "weather_shader_consumption_proven = `$false",
    "water_draw_attribution_proven = `$false",
    "exact_render_frame_identity_proven = `$false"
)) {
    Assert-Condition -Condition $driver.Contains($requiredToken, [StringComparison]::Ordinal) -Message "Driver is missing required acquisition token '$requiredToken'."
}
Assert-Condition -Condition (-not $driver.Contains("headed = `$true", [StringComparison]::Ordinal)) -Message 'Driver must not claim that headed mode was observed.'
Assert-Condition -Condition $driver.Contains('[double]$Snapshot.canvas.left -ge 0 -and [double]$Snapshot.canvas.top -ge 0', [StringComparison]::Ordinal) -Message 'The full-canvas assertion must reject negative left/top bounds.'
$routePreflight = $driver.LastIndexOf('[void](Get-ExactRoute -Root $BaseUrl -Config $config -QualityTier low)', [StringComparison]::Ordinal)
$bundleAcquisition = $driver.LastIndexOf('$servedBundles = Get-ServedBundleEvidence -Root $BaseUrl -Config $config', [StringComparison]::Ordinal)
Assert-Condition -Condition ($routePreflight -ge 0 -and $bundleAcquisition -gt $routePreflight) -Message 'Loopback route preflight must precede every served-bundle request.'
$lastCaptureRegistration = $driver.LastIndexOf('$capturedStages.Add([ordered]@{', [StringComparison]::Ordinal)
$finalSnapshotAcquisition = $driver.LastIndexOf('$finalSnapshot = Get-BrowserSnapshot -Socket $socket -Config $Config', [StringComparison]::Ordinal)
$finalBaseValidation = $driver.LastIndexOf('$finalLedgers = Assert-BaseSnapshot -Snapshot $finalSnapshot -Config $Config -ExpectedCommit $ExpectedCommit -QualityTier $QualityTier', [StringComparison]::Ordinal)
$finalProofValidation = $driver.LastIndexOf('Assert-ProofLedger -Ledger $finalLedgers.Proof -Config $Config -ExpectedCommit $ExpectedCommit -QualityTier $QualityTier -ExpectedCheckpointCount 3', [StringComparison]::Ordinal)
$finalStateBinding = $driver.LastIndexOf('$latest = [pscustomobject]@{', [StringComparison]::Ordinal)
$networkAccounting = $driver.LastIndexOf('$responses = @(', [StringComparison]::Ordinal)
$consoleAccounting = $driver.LastIndexOf('$consoleEntries = @(', [StringComparison]::Ordinal)
$acceptanceVerdict = $driver.LastIndexOf('Assert-Condition -Condition ($consoleErrors.Count', [StringComparison]::Ordinal)
Assert-Condition -Condition (
    $lastCaptureRegistration -ge 0 -and
    $finalSnapshotAcquisition -gt $lastCaptureRegistration -and
    $finalBaseValidation -gt $finalSnapshotAcquisition -and
    $finalProofValidation -gt $finalBaseValidation -and
    $finalStateBinding -gt $finalProofValidation -and
    $networkAccounting -gt $finalStateBinding -and
    $consoleAccounting -gt $finalStateBinding -and
    $acceptanceVerdict -gt $networkAccounting -and
    $acceptanceVerdict -gt $consoleAccounting
) -Message 'The final post-capture snapshot, zero-input check, three-checkpoint ledger validation, and final-state binding must precede console/network verdicts.'

$trunkDriver = Get-Content -LiteralPath $trunkPath -Raw
Assert-Condition -Condition $trunkDriver.Contains('RequireExactCommit', [StringComparison]::Ordinal) -Message 'Trunk driver must expose exact-commit mode.'
Assert-Condition -Condition $trunkDriver.Contains('FORGE_SOURCE_COMMIT', [StringComparison]::Ordinal) -Message 'Trunk driver must bind the embedded source commit.'
Assert-Condition -Condition $trunkDriver.Contains('--no-autoreload', [StringComparison]::Ordinal) -Message 'The proof server must disable Trunk autoreload.'

$studioSource = @(
    Get-Content -LiteralPath $studioSourcePath -Raw
    Get-Content -LiteralPath $matrixSourcePath -Raw
    Get-Content -LiteralPath $matrixWebSourcePath -Raw
) -join "`n"
foreach ($requiredToken in @(
    'forge_gate1_weather_proof',
    'NonCanonicalRoute',
    'r1-023-025-026-browser-weather-matrix-v1',
    'GATE1_BROWSER_WEATHER_MINIMUM_SETTLED_FRAMES: u32 = 8',
    'weather.request_state(target_weather)',
    'browser_weather_proof_camera_pose',
    'forge-gate1-browser-weather-proof-advance',
    'forge-gate1-browser-weather-proof-ledger',
    'browser_captures_performed_by_controller: false',
    'weather_shader_consumption_proven: false',
    'water_draw_attribution_proven: false',
    'exact_render_frame_identity_proven: false',
    'human_review_performed: false',
    'gate1_routes_qualified: false',
    'transition_timeout_ms: GATE1_BROWSER_WEATHER_TRANSITION_TIMEOUT_MS',
    'water_camera_anchor_x_m: GATE1_BROWSER_WATER_VIEW_ANCHOR_X_M',
    'stage_started_ms: Option<f64>',
    'StudioGpuPresentationStatus::NotObserved'
)) {
    Assert-Condition -Condition $studioSource.Contains($requiredToken, [StringComparison]::Ordinal) -Message "Studio controller is missing required token '$requiredToken'."
}

foreach ($entry in $legacyTemporalPaths.GetEnumerator()) {
    $legacyPath = Join-Path $repositoryRoot ($entry.Key -replace '/', [IO.Path]::DirectorySeparatorChar)
    Assert-Condition -Condition (Test-Path -LiteralPath $legacyPath -PathType Leaf) -Message "R1-014 file is missing: $($entry.Key)"
    $actualHash = (Get-FileHash -LiteralPath $legacyPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-Condition -Condition ($actualHash -ceq $entry.Value) -Message "R1-014 file changed: $($entry.Key)"
}

foreach ($forbiddenToken in @('npm install', 'npx ', 'playwright', 'upstream/cadle', 'assets/cadle')) {
    Assert-Condition -Condition (-not $driver.Contains($forbiddenToken, [StringComparison]::OrdinalIgnoreCase)) -Message "Driver unexpectedly depends on forbidden token '$forbiddenToken'."
    Assert-Condition -Condition (-not $studioSource.Contains($forbiddenToken, [StringComparison]::OrdinalIgnoreCase)) -Message "Studio proof unexpectedly depends on forbidden token '$forbiddenToken'."
}

$protocol = Get-Content -LiteralPath $protocolPath -Raw
Assert-Condition -Condition $protocol.Contains('ACQUISITION BROWSER NON LANCÉE', [StringComparison]::Ordinal) -Message 'Protocol must state that Browser acquisition was not run.'
Assert-Condition -Condition $protocol.Contains('83f02928da72774a49d3d62ee68be85335b0bf01', [StringComparison]::Ordinal) -Message 'Protocol must bind the detached implementation base.'
Assert-Condition -Condition $protocol.Contains('`R1-023`, `R1-025` et `R1-026` restent ouvertes', [StringComparison]::Ordinal) -Message 'Protocol must keep all three routes open.'

$roadmap = Get-Content -LiteralPath $roadmapPath -Raw
foreach ($route in @('R1-023', 'R1-025', 'R1-026')) {
    Assert-Condition -Condition ($roadmap -cmatch "(?m)^- \[ \] ``$route``") -Message "Roadmap must keep $route unchecked."
}
Assert-Condition -Condition ($roadmap -cmatch '(?m)^- \[ \] `R1-014`') -Message 'Roadmap must keep R1-014 unchecked.'

[pscustomobject]@{
    status = 'pass'
    powershell_scripts_parsed = 3
    config_mutations_rejected = 6
    boundary_cases_rejected = 3
    controller_checkpoints_per_tier = 3
    controller_settled_frames = 8
    tiers = @('low', 'high')
    expected_page_captures = 6
    expected_canvas_captures = 6
    r1_014_hashes_unchanged = 3
    browser_run = 'not_performed_by_static_test'
    cargo_build = 'not_performed'
    r1_023 = 'open'
    r1_025 = 'open'
    r1_026 = 'open'
}
