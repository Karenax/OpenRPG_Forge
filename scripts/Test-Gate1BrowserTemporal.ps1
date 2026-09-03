[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$driverPath = Join-Path $PSScriptRoot 'Invoke-Gate1BrowserTemporal.ps1'
$configPath = Join-Path $PSScriptRoot 'gate1-browser-temporal.v1.json'
$protocolPath = Join-Path $repositoryRoot 'docs/evidence/gate1/R1_014_BROWSER_TEMPORAL_PROTOCOL.md'
$roadmapPath = Join-Path $repositoryRoot 'docs/planning/MIGRATION_ROADMAP.md'
$trunkPath = Join-Path $PSScriptRoot 'Invoke-StudioTrunk.ps1'
$trunkConfigPath = Join-Path $repositoryRoot 'apps/forge_studio/Trunk.toml'
$studioSourcePath = Join-Path $repositoryRoot 'apps/forge_studio/src/lib.rs'
$studioStylesPath = Join-Path $repositoryRoot 'apps/forge_studio/styles.css'
$evidenceRoot = Join-Path $repositoryRoot 'docs/evidence/gate1/browser/4e476b31ee33533b6f4023804d8ecd1be02f9c55/temporal'
$evidenceManifestPath = Join-Path $evidenceRoot 'artifact-manifest.json'
$evidenceLedgerPath = Join-Path $evidenceRoot 'r1-014-browser-temporal-ledger.json'

function Assert-Condition {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

foreach ($path in @($driverPath, $configPath, $protocolPath, $roadmapPath, $trunkPath, $trunkConfigPath, $studioSourcePath, $studioStylesPath, $evidenceManifestPath, $evidenceLedgerPath)) {
    Assert-Condition -Condition (Test-Path -LiteralPath $path -PathType Leaf) -Message "Required R1-014 harness file is missing: $path"
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
    '__forgeGate1TemporalInputGuard',
    'gate0-telemetry-ledger',
    'forge-scene-telemetry-ledger',
    'Page.captureScreenshot',
    'requestAnimationFrame',
    'Runtime.consoleAPICalled',
    'Network.responseReceived',
    'Get-FileHash',
    'Get-ServedBundleEvidence',
    'build_source_commit',
    'minimum_stable_layout_observations',
    'require_no_page_scrollbars',
    'captureBeyondViewport = $false',
    'maximum_console_warnings',
    'human_temporal_inspection_required'
)) {
    Assert-Condition -Condition $driver.Contains($requiredToken, [StringComparison]::Ordinal) -Message "Driver is missing required acquisition token '$requiredToken'."
}

$trunkDriver = Get-Content -LiteralPath $trunkPath -Raw
Assert-Condition -Condition $trunkDriver.Contains('RequireExactCommit', [StringComparison]::Ordinal) -Message 'Trunk driver must expose the exact-commit build mode.'
Assert-Condition -Condition $trunkDriver.Contains('FORGE_SOURCE_COMMIT', [StringComparison]::Ordinal) -Message 'Trunk driver must bind the embedded source commit.'
Assert-Condition -Condition $trunkDriver.Contains('--no-autoreload', [StringComparison]::Ordinal) -Message 'The proof server must disable Trunk autoreload so served bytes remain identical to dist.'
$trunkConfig = Get-Content -LiteralPath $trunkConfigPath -Raw
Assert-Condition -Condition ($trunkConfig -cmatch '(?m)^no_sri\s*=\s*true\s*$') -Message 'The proof build must omit unsupported preload integrity attributes.'
$studioSource = Get-Content -LiteralPath $studioSourcePath -Raw
Assert-Condition -Condition $studioSource.Contains('option_env!("FORGE_SOURCE_COMMIT")', [StringComparison]::Ordinal) -Message 'Studio scene telemetry must embed the exact source commit.'
Assert-Condition -Condition $studioSource.Contains('schema_version: 8', [StringComparison]::Ordinal) -Message 'Studio scene telemetry v8 is required.'
Assert-Condition -Condition $studioSource.Contains('power_preference: bevy::render::settings::PowerPreference::None', [StringComparison]::Ordinal) -Message 'Studio WebGPU must leave adapter power preference unspecified on Windows.'
$studioStyles = Get-Content -LiteralPath $studioStylesPath -Raw
Assert-Condition -Condition $studioStyles.Contains('.panel { padding: 17px 14px; overflow: auto; }', [StringComparison]::Ordinal) -Message 'Studio side panels must scroll internally so the canvas remains inside the fixed viewport.'
foreach ($forbiddenToken in @('npm install', 'npx ', 'playwright', 'upstream/cadle', 'assets/cadle')) {
    Assert-Condition -Condition (-not $driver.Contains($forbiddenToken, [StringComparison]::OrdinalIgnoreCase)) -Message "Driver unexpectedly depends on forbidden token '$forbiddenToken'."
}

$protocol = Get-Content -LiteralPath $protocolPath -Raw
Assert-Condition -Condition $protocol.Contains('ACQUISITION AUTOMATISÉE PASS — INSPECTION HUMAINE EN ATTENTE', [StringComparison]::Ordinal) -Message 'Protocol must record the automated Browser pass without claiming human inspection.'
Assert-Condition -Condition $protocol.Contains('4e476b31ee33533b6f4023804d8ecd1be02f9c55', [StringComparison]::Ordinal) -Message 'Protocol must bind the qualifying temporal acquisition commit.'
Assert-Condition -Condition $protocol.Contains('c2b32eb15f7a05e67e3687fe3fdf7e128051dfb32460b6b6f4416875a581ff1b', [StringComparison]::Ordinal) -Message 'Protocol must bind the qualifying temporal ledger hash.'
Assert-Condition -Condition $protocol.Contains('R1-014` reste ouvert', [StringComparison]::Ordinal) -Message 'Protocol must keep R1-014 open.'
$evidenceManifestHash = (Get-FileHash -LiteralPath $evidenceManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
Assert-Condition -Condition ($evidenceManifestHash -ceq '3ec9f4372026cabc5d9b35ff67852c322d3213f354241bde49bbceadebf4f9bc') -Message 'Exact-candidate artifact manifest hash mismatch.'
$evidenceManifest = Get-Content -LiteralPath $evidenceManifestPath -Raw | ConvertFrom-Json -Depth 64
Assert-Condition -Condition ($evidenceManifest.schema -ceq 'forge.gate1-browser-temporal-artifact-manifest') -Message 'Unexpected exact-candidate artifact manifest schema.'
Assert-Condition -Condition ($evidenceManifest.git_commit -ceq '4e476b31ee33533b6f4023804d8ecd1be02f9c55') -Message 'Exact-candidate artifact manifest commit mismatch.'
Assert-Condition -Condition (@($evidenceManifest.artifacts).Count -eq 10) -Message 'Exact-candidate artifact manifest must bind exactly ten artifacts.'
foreach ($artifact in @($evidenceManifest.artifacts)) {
    $artifactPath = Join-Path $evidenceRoot $artifact.relative_path
    Assert-Condition -Condition (Test-Path -LiteralPath $artifactPath -PathType Leaf) -Message "Exact-candidate artifact is missing: $($artifact.relative_path)"
    Assert-Condition -Condition ((Get-Item -LiteralPath $artifactPath).Length -eq [long]$artifact.bytes) -Message "Exact-candidate artifact byte count mismatch: $($artifact.relative_path)"
    $artifactHash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-Condition -Condition ($artifactHash -ceq [string]$artifact.sha256) -Message "Exact-candidate artifact hash mismatch: $($artifact.relative_path)"
}
$evidenceLedgerHash = (Get-FileHash -LiteralPath $evidenceLedgerPath -Algorithm SHA256).Hash.ToLowerInvariant()
Assert-Condition -Condition ($evidenceLedgerHash -ceq 'c2b32eb15f7a05e67e3687fe3fdf7e128051dfb32460b6b6f4416875a581ff1b') -Message 'Exact-candidate temporal ledger hash mismatch.'
$evidenceLedger = Get-Content -LiteralPath $evidenceLedgerPath -Raw | ConvertFrom-Json -Depth 64
Assert-Condition -Condition ($evidenceLedger.git.commit -ceq '4e476b31ee33533b6f4023804d8ecd1be02f9c55') -Message 'Exact-candidate temporal ledger commit mismatch.'
Assert-Condition -Condition ($evidenceLedger.git.tree -ceq 'cc6efae0289b10dcf12f97e6bb4d97b3ead29f90') -Message 'Exact-candidate temporal ledger tree mismatch.'
Assert-Condition -Condition ($evidenceLedger.automated_result.status -ceq 'pass') -Message 'Exact-candidate temporal ledger must retain automated PASS.'
Assert-Condition -Condition ($evidenceLedger.automated_result.human_temporal_inspection -ceq 'required_not_performed_by_harness') -Message 'Exact-candidate temporal ledger must keep human inspection pending.'
$roadmap = Get-Content -LiteralPath $roadmapPath -Raw
Assert-Condition -Condition ($roadmap -cmatch '(?m)^- \[ \] `R1-014`') -Message 'Roadmap must keep R1-014 unchecked.'
Assert-Condition -Condition $roadmap.Contains('ACQUISITION AUTOMATISÉE PASS / INSPECTION HUMAINE EN ATTENTE', [StringComparison]::Ordinal) -Message 'Roadmap must record the automated pass and pending human inspection.'

[pscustomobject]@{
    status = 'pass'
    powershell_scripts_parsed = 3
    config_mutations_rejected = 3
    registered_vegetation_regions = 3
    registered_terrain_witnesses = 1
    served_bundles_required = 4
    minimum_request_animation_frames = 120
    stable_layout_observations = 8
    canvas_containment_contract = 'pass'
    browser_run = 'not_performed_by_static_test'
    qualifying_browser_evidence = 'pass_exact_candidate_versioned'
    human_temporal_inspection = 'pending'
    cargo_build = 'not_performed'
}
