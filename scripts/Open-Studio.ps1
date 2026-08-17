[CmdletBinding()]
param(
    [string]$Workspace,
    [string]$Page = 'zones',
    [string]$Capture,
    [ValidateSet(
        'gizmo', 'rotation', 'context', 'editor', 'add', 'delete', 'assets', 'asset-install', 'trigger',
        'ux0-a', 'ux0-a-filter', 'ux0-a-help', 'ux0-a-references',
        'ux0-b', 'ux0-b-tabs', 'ux0-b-shortcuts', 'ux0-b-conflict', 'ux0-b-profile',
        'ux0-c', 'ux0-c-modes', 'ux0-c-camera', 'ux0-c-selection', 'ux0-c-transform',
        'ux0-c-world-navigation', 'ux0-c-scale-gizmo', 'ux0-c-orthographic',
        'ux0-d', 'ux0-d-palette', 'ux0-d-placement', 'ux0-d-invalid', 'ux0-d-clipboard', 'ux0-d-brush',
        'ux0-e-wizard', 'ux0-e-summary', 'ux0-e-module', 'ux0-e-localization', 'ux0-e-player-start',
        'ux0-f', 'ux0-f-lifecycle', 'ux0-f-connections', 'ux0-f-inventory', 'ux0-f-use', 'ux0-f-life',
        'ux0-f-picker',
        'ux0-g', 'ux0-g-equipment', 'ux0-g-rest', 'ux0-g-heartbeat', 'ux0-g-user-event',
        'ux0-h', 'ux0-h-abort', 'ux0-h-time', 'ux0-h-progression',
        'ux0-i', 'ux0-i-variables', 'ux0-i-cache', 'ux0-i-guide',
        'ux0-j', 'ux0-j-rename', 'ux0-j-diagnostics',
        'ux1-a', 'ux1-a-wizard', 'ux1-a-summary',
        'ux1-b', 'ux1-b-resize', 'ux1-b-copy',
        'ux1-c', 'ux1-c-loading', 'ux1-c-reduced', 'ux1-c-fallback',
        'ux1-d', 'ux1-d-tree', 'ux1-d-locked', 'ux1-d-export',
        'ux1-e', 'ux1-e-blueprint', 'ux1-e-interaction', 'ux1-e-preview',
        'ux1-f', 'ux1-f-static', 'ux1-f-merge', 'ux1-f-collisionless', 'ux1-f-preview',
        'ux1-g', 'ux1-g-actions', 'ux1-g-focus', 'ux1-g-event',
        'ux1-h', 'ux1-h-capacity', 'ux1-h-access', 'ux1-h-references',
        'ux1-i', 'ux1-i-identity', 'ux1-i-localization', 'ux1-i-appearance',
        'ux1-j', 'ux1-j-transform', 'ux1-j-pivot', 'ux1-j-parent', 'ux1-j-animation',
        'ux1-k', 'ux1-k-initial', 'ux1-k-states', 'ux1-k-transition',
        'ux1-l', 'ux1-l-health', 'ux1-l-resistances', 'ux1-l-destruction',
        'ux1-m', 'ux1-m-actions', 'ux1-m-permissions', 'ux1-m-override',
        'ux1-n', 'ux1-n-memberships', 'ux1-n-policies', 'ux1-n-decision',
        'ux1-o', 'ux1-o-bindings', 'ux1-o-sources', 'ux1-o-preview', 'ux1-o-used',
        'ux1-p', 'ux1-p-mechanisms', 'ux1-p-lifecycle', 'ux1-p-preview', 'ux1-p-jam',
        'ux1-q', 'ux1-q-policy', 'ux1-q-charge', 'ux1-q-virtual', 'ux1-q-preview',
        'ux1-r', 'ux1-r-detection', 'ux1-r-disarm', 'ux1-r-friendly', 'ux1-r-trigger', 'ux1-r-one-shot',
        'ux1-s', 'ux1-s-table', 'ux1-s-open', 'ux1-s-reset', 'ux1-s-seed', 'ux1-s-preview',
        'ux1-t', 'ux1-t-open', 'ux1-t-close', 'ux1-t-payload', 'ux1-t-preview',
        'ux1-u', 'ux1-u-bindings', 'ux1-u-transition', 'ux1-u-veto', 'ux1-u-preview',
        'ux1-v', 'ux1-v-used', 'ux1-v-disturbed', 'ux1-v-damaged', 'ux1-v-death',
        'ux1-w', 'ux1-w-scale', 'ux1-w-dynamic', 'ux1-w-copy',
        'ux1-x', 'ux1-x-placement', 'ux1-x-states', 'ux1-x-interaction',
        'ux1-y', 'ux1-y-foundation',
        'ux1-z', 'ux1-z-physical', 'ux1-z-spell', 'ux1-z-heartbeat', 'ux1-z-physics',
        'ux1-aa', 'ux1-aa-assembly', 'ux1-aa-verbs', 'ux1-aa-validation',
        'ux1-ab', 'ux1-ab-damage', 'ux1-ab-loading', 'ux1-ab-events',
        'ux1-ac', 'ux1-ac-trigger-advanced', 'ux1-ac-filter', 'ux1-ac-volumes', 'ux1-ac-occupation',
        'ux1-ad', 'ux1-ad-reactions', 'ux1-ad-references', 'ux1-ad-assembly',
        'ux1-ae', 'ux1-ae-discovery-access',
        'ux1-af', 'ux1-af-assisted-area-plan', 'ux1-af-area-build',
        'ux1-ag', 'ux1-ag-lock', 'ux1-ag-refusal-trap', 'ux1-ag-events',
        'ux1-ah', 'ux1-ah-trigger-automation-director',
        'ux1-ai', 'ux1-ai-trigger-validation',
        'ux1-aj', 'ux1-aj-door-validation',
        'ux2-a', 'ux2-a-area-living-world',
        'ux2-lw-b', 'ux2-lw-b-area-lifecycle',
        'ux2-lw-c', 'ux2-lw-c-area-user-events',
        'ux2-lw-d', 'ux2-lw-d-area-combat-authority',
        'ux2-lw-e', 'ux2-lw-e-trigger-living-world',
        'ux2-lw-f', 'ux2-lw-f-encounter-foundation',
        'ux2-b', 'ux2-b-terrain-tiles',
        'ux2-c', 'ux2-c-terrain-surface',
        'ux2-env-b', 'ux2-env-b-area-environment',
        'ux2-env-c', 'ux2-env-c-climate-water-reverb',
        'ux2-env-d', 'ux2-env-d-rendering-sky',
        'ux2-env-e', 'ux2-env-e-area-audio',
        'ux2-env-f', 'ux2-env-f-audio-emitters',
        'ux2-env-g', 'ux2-env-g-audio-workbench',
        'ux2-env-h', 'ux2-env-h-audio-range-control',
        'ux2-env-i', 'ux2-env-i-audio-scatter-events',
        'ux2-nav-b', 'ux2-nav-c', 'ux2-nav-d', 'ux2-nav-e', 'ux2-nav-f', 'ux2-nav-g', 'ux2-nav-h',
        'ux4-a', 'ux4-a-faction-foundation',
        'ux5-a', 'ux5-a-dialogue-foundation')]
    [string]$Evidence,
    [string]$Resolution,
    [switch]$Headless,
    [switch]$Smoke,
    [switch]$Wait,
    [switch]$SkipBuild
)

. (Join-Path $PSScriptRoot 'Common.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$studioPath = Join-Path $repositoryRoot 'apps\Forge.Studio'
$studioProject = Join-Path $studioPath 'Forge.Studio.csproj'
$artifactRoot = Join-Path $repositoryRoot 'artifacts\verification\studio-shell'
$studioLog = Join-Path $artifactRoot 'studio-interactive.log'
$enginePath = Get-ForgeEnginePath

if ([string]::IsNullOrWhiteSpace($Workspace)) {
    $Workspace = Join-Path $repositoryRoot 'apps\Forge.Runtime\content\forgotten-forest'
}

$Workspace = [System.IO.Path]::GetFullPath($Workspace)
if (-not (Test-Path -LiteralPath (Join-Path $Workspace 'project.json') -PathType Leaf)) {
    throw "Le workspace '$Workspace' ne contient pas project.json."
}

if ($Headless -and -not [string]::IsNullOrWhiteSpace($Capture)) {
    throw 'Une capture visuelle exige un lancement rendu, sans -Headless.'
}

[System.IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
if (-not $SkipBuild) {
    & dotnet build $studioProject --configuration Debug --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "La compilation du Studio a échoué avec le code $LASTEXITCODE."
    }
}

$renderingMethod = if ($Headless) { 'gl_compatibility' } else { 'forward_plus' }
$studioArguments = @('--rendering-method', $renderingMethod)
if ($Headless) {
    $studioArguments += '--headless'
} elseif ([string]::IsNullOrWhiteSpace($Resolution)) {
    $studioArguments += '--maximized'
}
if (-not [string]::IsNullOrWhiteSpace($Resolution)) {
    if ($Resolution -notmatch '^\d{3,4}x\d{3,4}$') {
        throw "La résolution '$Resolution' doit respecter le format 1600x900."
    }
    $studioArguments += @('--resolution', $Resolution)
}
$studioArguments += @('--path', $studioPath, '--log-file', $studioLog, '--', '--workspace', $Workspace, '--page', $Page)
if ($Smoke) {
    $studioArguments += '--studio-smoke-test'
}
if (-not [string]::IsNullOrWhiteSpace($Capture)) {
    $Capture = [System.IO.Path]::GetFullPath($Capture)
    $studioArguments += @('--capture', $Capture)
    if (-not [string]::IsNullOrWhiteSpace($Resolution)) {
        $studioArguments += @('--capture-size', $Resolution)
    }
}
if (-not [string]::IsNullOrWhiteSpace($Evidence)) {
    $studioArguments += @('--evidence', $Evidence)
}

$launch = @{
    FilePath = $enginePath
    ArgumentList = $studioArguments
    WorkingDirectory = $studioPath
    PassThru = $true
}
if ($Headless) {
    $launch.WindowStyle = 'Hidden'
}
$process = Start-Process @launch
if ($Wait -or $Headless -or $Smoke -or -not [string]::IsNullOrWhiteSpace($Capture)) {
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "OpenRPG Forge Studio a quitté avec le code $($process.ExitCode). Journal : $studioLog"
    }
    if (-not [string]::IsNullOrWhiteSpace($Capture) -and (Test-Path -LiteralPath $studioLog -PathType Leaf)) {
        Copy-Item -LiteralPath $studioLog -Destination "$Capture.log" -Force
    }
}

Write-Output "OpenRPG Forge Studio lancé sans l'éditeur Godot. Journal : $studioLog"
