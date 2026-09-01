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
$studioSourcePath = Join-Path $repositoryRoot 'apps/forge_studio/src/lib.rs'

function Assert-Condition {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

foreach ($path in @($driverPath, $configPath, $protocolPath, $roadmapPath, $trunkPath, $studioSourcePath)) {
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
    'maximum_console_warnings',
    'human_temporal_inspection_required'
)) {
    Assert-Condition -Condition $driver.Contains($requiredToken, [StringComparison]::Ordinal) -Message "Driver is missing required acquisition token '$requiredToken'."
}

$trunkDriver = Get-Content -LiteralPath $trunkPath -Raw
Assert-Condition -Condition $trunkDriver.Contains('RequireExactCommit', [StringComparison]::Ordinal) -Message 'Trunk driver must expose the exact-commit build mode.'
Assert-Condition -Condition $trunkDriver.Contains('FORGE_SOURCE_COMMIT', [StringComparison]::Ordinal) -Message 'Trunk driver must bind the embedded source commit.'
$studioSource = Get-Content -LiteralPath $studioSourcePath -Raw
Assert-Condition -Condition $studioSource.Contains('option_env!("FORGE_SOURCE_COMMIT")', [StringComparison]::Ordinal) -Message 'Studio scene telemetry must embed the exact source commit.'
Assert-Condition -Condition $studioSource.Contains('schema_version: 8', [StringComparison]::Ordinal) -Message 'Studio scene telemetry v8 is required.'
foreach ($forbiddenToken in @('npm install', 'npx ', 'playwright', 'upstream/cadle', 'assets/cadle')) {
    Assert-Condition -Condition (-not $driver.Contains($forbiddenToken, [StringComparison]::OrdinalIgnoreCase)) -Message "Driver unexpectedly depends on forbidden token '$forbiddenToken'."
}

$protocol = Get-Content -LiteralPath $protocolPath -Raw
Assert-Condition -Condition $protocol.Contains('HARNESS READY — ACQUISITION NOT RUN', [StringComparison]::Ordinal) -Message 'Protocol must state that Browser acquisition has not run.'
Assert-Condition -Condition $protocol.Contains('R1-014` reste ouvert', [StringComparison]::Ordinal) -Message 'Protocol must keep R1-014 open.'
$roadmap = Get-Content -LiteralPath $roadmapPath -Raw
Assert-Condition -Condition ($roadmap -cmatch '(?m)^- \[ \] `R1-014`') -Message 'Roadmap must keep R1-014 unchecked.'
Assert-Condition -Condition $roadmap.Contains('HARNESS READY / ACQUISITION NOT RUN', [StringComparison]::Ordinal) -Message 'Roadmap must record the exact harness-only status.'

[pscustomobject]@{
    status = 'pass'
    powershell_scripts_parsed = 3
    config_mutations_rejected = 3
    registered_vegetation_regions = 3
    registered_terrain_witnesses = 1
    served_bundles_required = 4
    minimum_request_animation_frames = 120
    stable_layout_observations = 8
    browser_run = 'not_performed'
    cargo_build = 'not_performed'
}
