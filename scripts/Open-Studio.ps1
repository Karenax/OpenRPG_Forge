[CmdletBinding()]
param(
    [string]$Workspace,
    [string]$Page = 'zones',
    [string]$Capture,
    [string]$CapturePageMatrix,
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
        'ux2-lw-g', 'ux2-lw-g-encounter-selection-activation',
        'ux2-lw-h', 'ux2-lw-h-encounter-scenario',
        'ux2-lw-i', 'ux2-lw-i-encounter-runtime-policies', 'ux2-encounter-008-010-014',
        'ux2-lw-j', 'ux2-lw-j-encounter-tactical', 'ux2-encounter-016-017-019',
        'ux2-lw-k', 'ux2-lw-k-encounter-statistics', 'ux2-encounter-018',
        'ux2-lw-l', 'ux2-lw-l-encounter-validation', 'ux2-encounter-020',
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
        'ux2-env-j', 'ux2-env-j-map-fow-lod', 'ux2-area-034-lod',
        'ux2-env-k', 'ux2-env-k-water-graphics',
        'ux2-nav-b', 'ux2-nav-c', 'ux2-nav-d', 'ux2-nav-e', 'ux2-nav-f', 'ux2-nav-g', 'ux2-nav-h', 'ux2-nav-i',
        'ux2-nav-j', 'ux2-nav-j-visibility-occlusion', 'ux2-area-033-visibility',
        'ux2-nav-k', 'ux2-wpt-010',
        'ux2-nav-l', 'ux2-wpt-012',
        'ux2-nav-m', 'ux2-wpt-013',
        'ux3-a', 'ux3-a-blueprint-foundation', 'ux3-a-instance', 'ux3-a-palettes',
        'ux3-b', 'ux3-b-blueprint-library', 'ux3-b-prefabs',
        'ux3-c', 'ux3-c-blueprint-discovery', 'ux3-c-drag-drop',
        'ux3-d', 'ux3-d-blueprint-governance', 'ux3-d-dependencies',
        'ux3-e', 'ux3-e-asset-quality', 'ux3-e-budget',
        'ux3-f', 'ux3-f-content-browser', 'ux3-f-import-profile', 'ux3-f-blender',
        'ux3-g', 'ux3-g-asset-manifest', 'ux3-g-collision-navigation', 'ux3-g-animation', 'ux3-g-vfx-lod',
        'ux3-h', 'ux3-h-content-data', 'ux3-h-batch-import', 'ux3-h-blueprint-export', 'ux3-h-text-sources', 'ux3-h-data-tables',
        'ux3-i', 'ux3-i-governed-assets', 'ux3-i-provenance', 'ux3-i-quarantine', 'ux3-i-media-generation',
        'ux4-a', 'ux4-a-faction-foundation',
        'ux4-b', 'ux4-b-faction-runtime',
        'ux4-c', 'ux4-c-faction-matrix',
        'ux4-d', 'ux4-d-creature-foundation', 'ux4-d-creature-onspawn', 'ux4-d-wizard', 'ux4-d-species', 'ux4-d-preview',
        'ux4-e', 'ux4-e-creature-presentation', 'ux4-e-portraits', 'ux4-e-appearance',
        'ux4-e-colors', 'ux4-e-preview',
        'ux4-f', 'ux4-f-creature-animation-rules', 'ux4-f-animation', 'ux4-f-initial-resources',
        'ux4-f-attributes', 'ux4-f-defenses', 'ux4-f-preview',
        'ux4-g', 'ux4-g-creature-progression', 'ux4-g-classes', 'ux4-g-alignment', 'ux4-g-skills',
        'ux4-g-talents', 'ux4-g-powers', 'ux4-g-preview',
        'ux4-h', 'ux4-h-creature-capabilities-inventory', 'ux4-h-abilities', 'ux4-h-inventory',
        'ux4-h-preview',
        'ux4-i', 'ux4-i-creature-loot', 'ux4-i-drops', 'ux4-i-pickpocket', 'ux4-i-corpse',
        'ux4-i-preview',
        'ux4-j', 'ux4-j-creature-interaction', 'ux4-j-factions', 'ux4-j-perception',
        'ux4-j-dialogues', 'ux4-j-onconversation', 'ux4-j-protection', 'ux4-j-preview',
        'ux4-k', 'ux4-k-creature-movement',
        'ux4-l', 'ux4-l-creature-voice',
        'ux4-m', 'ux4-m-creature-role-behavior', 'ux4-m-ambient', 'ux4-m-auto-level',
        'ux4-n', 'ux4-n-creature-events', 'ux4-n-heartbeat', 'ux4-n-custom',
        'ux4-o', 'ux4-o-creature-damage-validation', 'ux4-o-validation',
        'ux4-p', 'ux4-p-creature-event-ai', 'ux4-p-controller', 'ux4-p-qualification',
        'ux4-q', 'ux4-q-area-voice', 'ux4-q-localized-voice',
        'ux4-r', 'ux4-r-assisted-boss', 'ux4-r-boss-creation',
        'ux4-s', 'ux4-s-items', 'ux4-s-item-authoring',
        'ux4-t', 'ux4-t-merchants', 'ux4-t-merchant-authoring',
        'ux4-u', 'ux4-u-companions', 'ux4-u-companion-authoring',
        'ux5-a', 'ux5-a-dialogue-foundation',
        'ux5-b', 'ux5-b-dialogue-production',
        'ux5-c', 'ux5-c-quest-authoring',
        'ux5-d', 'ux5-d-localization',
        'ux5-e', 'ux5-e-lore',
        'ux5-f', 'ux5-f-cinematics',
        'ux6-a', 'ux6-a-rules',
        'ux6-b', 'ux6-b-gameplay-foundation',
        'ux6-c', 'ux6-c-gameplay-capabilities',
        'ux6-d', 'ux6-d-gameplay-simulation',
        'ux6-e', 'ux6-e-gameplay-production',
        'ux6-f', 'ux6-f-script-foundation',
        'ux6-g', 'ux6-g-script-authoring-gameplay',
        'ux6-h', 'ux6-h-script-world-apis',
        'ux6-i', 'ux6-i-world-movement-utilities',
        'ux6-j', 'ux6-j-script-safety-policy',
        'ux6-k', 'ux6-k-script-module-permissions', 'ux6-k-kits', 'ux6-k-bindings',
        'ux6-l', 'ux6-l-script-runtime-tools', 'ux6-l-time', 'ux6-l-events',
        'ux6-m', 'ux6-m-script-automation', 'ux6-m-camera', 'ux6-m-tags',
        'ux6-m-module-switches', 'ux6-m-ai-switches',
        'ux6-n', 'ux6-n-hot-reload', 'ux6-n-debugger', 'ux6-n-hot-reload-debugger',
        'ux6-o', 'ux6-o-script-quality', 'ux6-o-profile', 'ux6-o-documentation',
        'ux6-o-analysis', 'ux6-o-state',
        'ux6-p', 'ux6-p-script-interop', 'ux6-p-binding', 'ux6-p-transactions',
        'ux6-q', 'ux6-q-compiler-assistant', 'ux6-q-compiler', 'ux6-q-assistant',
        'ux6-r', 'ux6-r-runtime-data-ui', 'ux6-r-rules', 'ux6-r-ui', 'ux6-r-json', 'ux6-r-snapshot',
        'ux6-s', 'ux6-s-creator-technical', 'ux6-s-devices', 'ux6-s-resources', 'ux6-s-party', 'ux6-s-progression',
        'ux6-t', 'ux6-t-aoe', 'ux6-t-nui', 'ux6-t-modern-runtime',
        'ux6-u', 'ux6-u-blackboard', 'ux6-u-variables',
        'ux6-v', 'ux6-v-pathfinding', 'ux6-v-advanced-pathfinding',
        'ux6-w', 'ux6-w-runtime-transforms', 'ux6-w-tweens', 'ux6-w-runtime-preview',
        'ux6-x', 'ux6-x-runtime-ui', 'ux6-x-hud-shell', 'ux6-x-type-scale',
        'ux6-y', 'ux6-y-runtime-operations', 'ux6-y-runtime-weather', 'ux6-y-area-loading',
        'ux6-z', 'ux6-z-assisted-ability', 'ux6-z-governance', 'ux6-z-tests',
        'ux7-a', 'ux7-a-canonical-validation', 'ux7-a-schema', 'ux7-a-references',
        'ux7-a-graphs', 'ux7-a-geometry', 'ux7-a-navigation', 'ux7-a-localization',
        'ux7-a-license', 'ux7-a-area', 'ux7-a-audio', 'ux7-a-clickable',
        'ux7-b', 'ux7-b-quality-lab', 'ux7-b-domain', 'ux7-b-headless',
        'ux7-b-saves', 'ux7-b-logs',
        'ux7-c', 'ux7-c-quality-suites',
        'ux7-d', 'ux7-d-performance-lab',
        'ux7-e', 'ux7-e-governance-lab',
        'ux7-f', 'ux7-f-history-recovery',
        'ux7-g', 'ux7-g-playtest-lab',
        'ux7-h', 'ux7-h-visual-regression',
        'ux7-i', 'ux7-i-observability',
        'ux8-a', 'ux8-a-build-foundation',
        'ux8-b', 'ux8-b-build-collaboration',
        'ux8-c', 'ux8-c-build-governance',
        'ux8-d', 'ux8-d-repository-readiness',
        'ux8-e', 'ux8-e-production-evidence',
        'ux8-f', 'ux8-f-supply-chain-evidence',
        'ux8-g', 'ux8-g-content-packages',
        'ux8-h', 'ux8-h-distribution-release',
        'ux8-i', 'ux8-i-compatibility-migrations',
        'ux8-j', 'ux8-j-security-workers',
        'ux8-k', 'ux8-k-bulk-transfer',
        'ux8-l', 'ux8-l-repository-sync',
        'ux8-m', 'ux8-m-network-player-experience', 'ux8-m-connection', 'ux8-m-lobby',
        'ux8-m-character-builder', 'ux8-m-character-authority',
        'ux8-n', 'ux8-n-package-lifecycle', 'ux8-n-curated-packs',
        'ux8-n-resource-namespaces', 'ux8-n-package-migration', 'ux8-n-safe-uninstall',
        'ux9-a', 'ux9-a-network-foundation',
        'ux9-b', 'ux9-b-network-operations', 'ux9-b-content-sync', 'ux9-b-version-lock',
        'ux9-b-roles', 'ux9-b-chat', 'ux9-b-event-bus',
        'ux9-c', 'ux9-c-network-world-administration', 'ux9-c-instances', 'ux9-c-dynamic-areas',
        'ux9-c-authentication', 'ux9-c-administration', 'ux9-c-moderation',
        'ux9-d', 'ux9-d-network-persistence-observability', 'ux9-d-persistence', 'ux9-d-sql',
        'ux9-d-observability', 'ux9-d-metrics', 'ux9-d-profiler', 'ux9-d-reconnection',
        'ux9-e', 'ux9-e-network-resilience-capacity', 'ux9-e-backup', 'ux9-e-rollback',
        'ux9-e-capacity', 'ux9-e-queue',
        'ux9-f', 'ux9-f-live-director-workspace', 'ux9-f-client', 'ux9-f-creator',
        'ux9-f-spawn', 'ux9-f-chooser', 'ux9-f-focus',
        'ux9-g', 'ux9-g-live-director-actions', 'ux9-g-relocation', 'ux9-g-destruction',
        'ux9-g-healing', 'ux9-g-limbo', 'ux9-g-possession', 'ux9-g-return',
        'ux9-h', 'ux9-h-live-director-authority', 'ux9-h-impersonation', 'ux9-h-protection',
        'ux9-h-currency', 'ux9-h-experience', 'ux9-h-door', 'ux9-h-portal',
        'ux9-i', 'ux9-i-live-director-group', 'ux9-i-trigger', 'ux9-i-selection',
        'ux9-i-movement', 'ux9-i-attack', 'ux9-i-travel', 'ux9-i-player',
        'ux9-j', 'ux9-j-live-director-operations', 'ux9-j-search', 'ux9-j-script',
        'ux9-j-variable', 'ux9-j-scene', 'ux9-j-visibility', 'ux9-j-fog',
        'ux9-k', 'ux9-k-live-director-narrative', 'ux9-k-quest',
        'ux9-k-encounter', 'ux9-k-rencontre',
        'ux9-l', 'ux9-l-live-director-access', 'ux9-l-authentication',
        'ux9-l-inspection', 'ux9-l-players', 'ux9-l-audit', 'ux9-l-chat',
        'ux9-m', 'ux9-m-live-director-production', 'ux9-m-rest', 'ux9-m-level',
        'ux9-m-appearance', 'ux9-m-group', 'ux9-m-recording',
        'ux9-n', 'ux9-n-network-production', 'ux9-n-content', 'ux9-n-voice',
        'ux9-n-shard', 'ux9-n-hot-reload', 'ux9-n-extensions', 'ux9-n-protocol',
        'ux9-o', 'ux9-o-network-closure', 'ux9-o-dedicated', 'ux9-o-combat',
        'ux9-o-logs', 'ux9-o-anti-cheat',
        'ux10-a', 'ux10-a-context',
        'ux10-b', 'ux10-b-plans',
        'ux10-c', 'ux10-c-safety',
        'ux10-d', 'ux10-d-creative')]
    [string]$Evidence,
    [string]$Resolution,
    [double]$UiScale,
    [switch]$Headless,
    [ValidateSet('Route', 'Wave', 'Full')]
    [string]$SmokeScope = 'Full',
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
    $Workspace = Join-Path $repositoryRoot 'apps\Forge.Runtime\content\crypt-of-ashes'
}

$Workspace = [System.IO.Path]::GetFullPath($Workspace)
if (-not (Test-Path -LiteralPath (Join-Path $Workspace 'project.json') -PathType Leaf)) {
    throw "Le workspace '$Workspace' ne contient pas project.json."
}

if ($Headless -and -not [string]::IsNullOrWhiteSpace($Capture)) {
    throw 'Une capture visuelle exige un lancement rendu, sans -Headless.'
}
if ($Headless -and -not [string]::IsNullOrWhiteSpace($CapturePageMatrix)) {
    throw 'Une matrice visuelle exige un lancement rendu, sans -Headless.'
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
    $studioArguments += '--fullscreen'
}
if (-not [string]::IsNullOrWhiteSpace($Resolution)) {
    if ($Resolution -notmatch '^\d{3,4}x\d{3,4}$') {
        throw "La résolution '$Resolution' doit respecter le format 1600x900."
    }
    $studioArguments += @('--windowed', '--resolution', $Resolution)
}
$studioArguments += @('--path', $studioPath, '--log-file', $studioLog, '--', '--workspace', $Workspace, '--page', $Page)
if ($UiScale -ne 0) {
    if ($UiScale -lt 0.75 -or $UiScale -gt 2.0) {
        throw "L'échelle UI '$UiScale' doit être comprise entre 0.75 et 2.0."
    }
    $studioArguments += @('--ui-scale', $UiScale.ToString([System.Globalization.CultureInfo]::InvariantCulture))
}
if ($Smoke) {
    $studioArguments += @('--studio-smoke-test', '--smoke-scope', $SmokeScope)
}
if (-not [string]::IsNullOrWhiteSpace($Capture)) {
    $Capture = [System.IO.Path]::GetFullPath($Capture)
    $studioArguments += @('--capture', $Capture)
    if (-not [string]::IsNullOrWhiteSpace($Resolution)) {
        $studioArguments += @('--capture-size', $Resolution)
    }
}
if (-not [string]::IsNullOrWhiteSpace($CapturePageMatrix)) {
    $CapturePageMatrix = [System.IO.Path]::GetFullPath($CapturePageMatrix)
    $studioArguments += @('--capture-page-matrix', $CapturePageMatrix)
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
if ($Wait -or $Headless -or $Smoke -or -not [string]::IsNullOrWhiteSpace($Capture) -or
    -not [string]::IsNullOrWhiteSpace($CapturePageMatrix)) {
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "OpenRPG Forge Studio a quitté avec le code $($process.ExitCode). Journal : $studioLog"
    }
    if (-not [string]::IsNullOrWhiteSpace($Capture) -and (Test-Path -LiteralPath $studioLog -PathType Leaf)) {
        Copy-Item -LiteralPath $studioLog -Destination "$Capture.log" -Force
    }
    if (-not [string]::IsNullOrWhiteSpace($CapturePageMatrix) -and (Test-Path -LiteralPath $studioLog -PathType Leaf)) {
        Copy-Item -LiteralPath $studioLog -Destination (Join-Path $CapturePageMatrix 'matrix.log') -Force
    }
}

Write-Output "OpenRPG Forge Studio lancé sans l'éditeur Godot. Journal : $studioLog"
