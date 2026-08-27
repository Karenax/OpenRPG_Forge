[CmdletBinding()]
param(
    [ValidateSet('godot-flow', 'gf-pcgodot')]
    [string]$GraphProvider = 'godot-flow',

    [ValidateSet('addon', 'reference-2km')]
    [string]$QualificationProfile = 'addon',

    [string]$GodotExecutable,
    [string]$RunId,
    [string]$OutputRoot,

    [switch]$BuildPcgFromSource,
    [switch]$SkipCapture
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$fixtureDirectory = if ($QualificationProfile -eq 'reference-2km') {
    'tests/Godot/WorldGenerationReferenceQualification'
}
else {
    'tests/Godot/WorldGenerationAddonQualification'
}
$fixtureSource = Join-Path $repositoryRoot $fixtureDirectory
$installer = Join-Path $PSScriptRoot 'Install-WorldGenerationDependencies.ps1'
$dependencyManifestPath = Join-Path $repositoryRoot 'third_party/world-generation.dependencies.json'
$artifactDirectory = if ($QualificationProfile -eq 'reference-2km') {
    'artifacts/world-generation/reference-2km'
}
else {
    'artifacts/world-generation/qualification'
}
$qualificationRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $artifactDirectory))
$qualificationPrefix = $qualificationRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (-not (Test-Path -LiteralPath $fixtureSource -PathType Container) -or
    -not (Test-Path -LiteralPath $installer -PathType Leaf) -or
    -not (Test-Path -LiteralPath $dependencyManifestPath -PathType Leaf)) {
    throw 'World-generation qualification sources are incomplete.'
}

$dependencyManifest = Get-Content -LiteralPath $dependencyManifestPath -Raw | ConvertFrom-Json
$selectedProvider = @($dependencyManifest.dependencies | Where-Object providerKey -eq $GraphProvider)
if ($dependencyManifest.schemaVersion -ne 2 -or $selectedProvider.Count -ne 1) {
    throw "Provider '$GraphProvider' is not uniquely pinned in dependency manifest schema 2."
}
$selectedProvider = $selectedProvider[0]
$providerRevision = if ($null -ne $selectedProvider.PSObject.Properties['revision']) {
    [string]$selectedProvider.revision
}
else {
    [string]$selectedProvider.version
}
$providerVersionDirectory = if ($providerRevision -match '^[0-9a-f]{40}$') {
    'v' + $providerRevision.Substring(0, 8)
}
else {
    ($providerRevision -replace '[^A-Za-z0-9._-]', '-')
}

if ([string]::IsNullOrWhiteSpace($RunId)) {
    $RunId = [DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmssZ')
}
if ($RunId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$') {
    throw "RunId '$RunId' is not a safe artifact directory name."
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $runRoot = [System.IO.Path]::GetFullPath(
        (Join-Path $qualificationRoot "$GraphProvider/$providerVersionDirectory/$RunId"))
}
else {
    $runRoot = [System.IO.Path]::GetFullPath($OutputRoot)
}
if (-not $runRoot.StartsWith($qualificationPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Qualification output '$runRoot' escapes '$qualificationRoot'."
}
if (Test-Path -LiteralPath $runRoot) {
    throw "Qualification output '$runRoot' already exists; use a new RunId."
}

if ([string]::IsNullOrWhiteSpace($GodotExecutable)) {
    $engineCommand = Get-Command 'Godot_v4.7.1-stable_mono_win64_console.exe' -ErrorAction SilentlyContinue
    if ($null -eq $engineCommand) {
        $engineCommand = Get-Command 'godot' -ErrorAction SilentlyContinue
    }
    if ($null -eq $engineCommand) {
        $engineCommand = Get-Command 'godot4' -ErrorAction SilentlyContinue
    }
    if ($null -eq $engineCommand) {
        throw 'Godot 4.7.1 executable was not found. Pass -GodotExecutable explicitly.'
    }
    $GodotExecutable = $engineCommand.Source
}
else {
    $GodotExecutable = [System.IO.Path]::GetFullPath($GodotExecutable)
    if (-not (Test-Path -LiteralPath $GodotExecutable -PathType Leaf)) {
        throw "Godot executable '$GodotExecutable' was not found."
    }
}

$godotVersionOutput = @(& $GodotExecutable --version 2>&1 | ForEach-Object { [string]$_ })
if ($LASTEXITCODE -ne 0 -or ($godotVersionOutput -join "`n") -notmatch '^4\.7\.1\.stable') {
    throw "Qualification requires Godot 4.7.1 stable; received '$($godotVersionOutput -join ' ')'."
}
$godotVersion = ($godotVersionOutput | Select-Object -First 1).Trim()

function Copy-Tree {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )
    [System.IO.Directory]::CreateDirectory($Destination) | Out-Null
    foreach ($entry in Get-ChildItem -LiteralPath $Source -Force) {
        Copy-Item -LiteralPath $entry.FullName -Destination $Destination -Recurse -Force
    }
}

function Write-JsonArtifact {
    param(
        [Parameter(Mandatory = $true)]$Value,
        [Parameter(Mandatory = $true)][string]$Path
    )
    $Value | ConvertTo-Json -Depth 24 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}

function Remove-AnsiEscape {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Value)
    return [regex]::Replace($Value, "`e\[[0-?]*[ -/]*[@-~]", '')
}

function Assert-GodotDiagnostics {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][string[]]$Output,
        [switch]$AllowEditorShutdownLeaks
    )
    foreach ($rawLine in $Output) {
        $line = Remove-AnsiEscape $rawLine
        if ($line -match '^SCRIPT ERROR:') {
            throw "Godot emitted a script error: $line"
        }
        if ($line -notmatch '^ERROR:') {
            continue
        }
        $knownShutdownLeak = $line -match '^ERROR: \d+ RID allocations? .* leaked at exit\.$' -or
            $line -match '^ERROR: \d+ ObjectDB instances were leaked at exit' -or
            $line -match '^ERROR: \d+ resources still in use at exit' -or
            $line -match '^ERROR: (Texture|Buffer) with GL ID of \d+: leaked \d+ bytes\.$' -or
            $line -match '^ERROR: \d+ shaders of type [A-Za-z0-9_]+ were never freed$' -or
            $line -eq 'ERROR: Parameter "current_window" is null.'
        if (-not ($AllowEditorShutdownLeaks -and $knownShutdownLeak)) {
            throw "Godot emitted an unexpected error: $line"
        }
    }
}

function Invoke-GodotStep {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string[]]$RequiredPatterns = @(),
        [switch]$AllowEditorShutdownLeaks
    )
    $logPath = Join-Path $logsRoot "$Name.log"
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $output = @(& $GodotExecutable @Arguments 2>&1 | ForEach-Object { [string]$_ })
    $exitCode = $LASTEXITCODE
    $stopwatch.Stop()
    [System.IO.File]::WriteAllLines($logPath, $output)
    if ($exitCode -ne 0) {
        throw "Godot step '$Name' failed with exit code $exitCode. See '$logPath'."
    }
    Assert-GodotDiagnostics -Output $output -AllowEditorShutdownLeaks:$AllowEditorShutdownLeaks
    $normalizedOutput = ($output | ForEach-Object { Remove-AnsiEscape $_ }) -join "`n"
    foreach ($pattern in $RequiredPatterns) {
        if ($normalizedOutput -notmatch $pattern) {
            throw "Godot step '$Name' did not emit required evidence '$pattern'. See '$logPath'."
        }
    }
    return [pscustomobject]@{
        Name = $Name
        DurationMilliseconds = $stopwatch.ElapsedMilliseconds
        LogPath = $logPath
        Output = $output
        NormalizedOutput = $normalizedOutput
    }
}

function Get-MarkerData {
    param(
        [Parameter(Mandatory = $true)][string]$Output,
        [Parameter(Mandatory = $true)][string]$Marker
    )
    $line = @($Output -split "`r?`n" | Where-Object { $_ -match [regex]::Escape($Marker) }) |
        Select-Object -Last 1
    if ([string]::IsNullOrWhiteSpace($line)) {
        throw "Evidence marker '$Marker' was not found."
    }
    $values = [ordered]@{}
    foreach ($match in [regex]::Matches($line, '([a-zA-Z0-9_]+)=([^\s]+)')) {
        $key = $match.Groups[1].Value
        $rawValue = $match.Groups[2].Value
        $numeric = 0L
        if ([long]::TryParse($rawValue, [ref]$numeric)) {
            $values[$key] = $numeric
        }
        else {
            $values[$key] = $rawValue
        }
    }
    return $values
}

[System.IO.Directory]::CreateDirectory($runRoot) | Out-Null
$logsRoot = Join-Path $runRoot 'logs'
$workRoot = Join-Path $runRoot 'work'
$fixtureRoot = Join-Path $workRoot 'fixture'
$neutralRoot = Join-Path $workRoot 'provider-free-runtime'
$derivedRoot = Join-Path $runRoot 'derived'
$capturesRoot = Join-Path $runRoot 'captures'
[System.IO.Directory]::CreateDirectory($logsRoot) | Out-Null
[System.IO.Directory]::CreateDirectory($workRoot) | Out-Null

try {
    Copy-Tree -Source $fixtureSource -Destination $fixtureRoot

    $installerStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $installerArguments = @{
        GodotProjectPath = $fixtureRoot
        GraphProvider = $GraphProvider
        BuildPcgFromSource = $BuildPcgFromSource
    }
    $installerOutput = @(& $installer @installerArguments 2>&1 | ForEach-Object { [string]$_ })
    $installerSucceeded = $?
    $installerStopwatch.Stop()
    [System.IO.File]::WriteAllLines((Join-Path $logsRoot 'installer.log'), $installerOutput)
    if (-not $installerSucceeded) {
        throw "Pinned provider installation failed. See '$(Join-Path $logsRoot 'installer.log')'."
    }

    $import = Invoke-GodotStep `
        -Name 'editor-import' `
        -Arguments @('--headless', '--editor', '--path', $fixtureRoot, '--quit-after', '120') `
        -RequiredPatterns @('Data Flow plugin enabled') `
        -AllowEditorShutdownLeaks
    $providerLoad = Invoke-GodotStep `
        -Name 'provider-load' `
        -Arguments @('--headless', '--path', $fixtureRoot, '--script', 'res://provider_load_smoke.gd') `
        -RequiredPatterns @('WORLD_GENERATION_PROVIDER_LOAD_PASS')
    $live = Invoke-GodotStep `
        -Name 'live-generation' `
        -Arguments @('--editor', '--path', $fixtureRoot, '--script', 'res://world_generation_live_smoke.gd') `
        -RequiredPatterns @('WORLD_GENERATION_LIVE_SMOKE_PASS') `
        -AllowEditorShutdownLeaks

    $generatedScene = Join-Path $fixtureRoot 'qualification-output/generated_world.tscn'
    if (-not (Test-Path -LiteralPath $generatedScene -PathType Leaf)) {
        throw 'Live generation emitted a pass marker without a saved derived scene.'
    }
    [System.IO.Directory]::CreateDirectory($neutralRoot) | Out-Null
    [System.IO.Directory]::CreateDirectory((Join-Path $neutralRoot 'qualification-output')) | Out-Null
    Copy-Item -LiteralPath (Join-Path $fixtureSource 'runtime.project.godot') `
        -Destination (Join-Path $neutralRoot 'project.godot')
    Copy-Item -LiteralPath (Join-Path $fixtureSource 'world_generation_reopen_smoke.gd') `
        -Destination $neutralRoot
    Copy-Item -LiteralPath (Join-Path $fixtureSource 'world_generation_capture.gd') `
        -Destination $neutralRoot
    Copy-Item -LiteralPath $generatedScene `
        -Destination (Join-Path $neutralRoot 'qualification-output/generated_world.tscn')

    $reopen = Invoke-GodotStep `
        -Name 'provider-free-reopen' `
        -Arguments @('--headless', '--path', $neutralRoot, '--script', 'res://world_generation_reopen_smoke.gd') `
        -RequiredPatterns @('WORLD_GENERATION_REOPEN_PASS')

    $capture = $null
    if (-not $SkipCapture) {
        $capture = Invoke-GodotStep `
            -Name 'normalized-captures' `
            -Arguments @('--path', $neutralRoot, '--script', 'res://world_generation_capture.gd') `
            -RequiredPatterns @('WORLD_GENERATION_CAPTURE_PASS')
        $neutralCaptures = Join-Path $neutralRoot 'captures'
        if (-not (Test-Path -LiteralPath $neutralCaptures -PathType Container)) {
            throw 'Capture smoke emitted a pass marker without PNG artifacts.'
        }
        Copy-Tree -Source $neutralCaptures -Destination $capturesRoot
    }

    [System.IO.Directory]::CreateDirectory($derivedRoot) | Out-Null
    Copy-Item -LiteralPath (Join-Path $neutralRoot 'qualification-output/generated_world.tscn') `
        -Destination (Join-Path $derivedRoot 'generated_world.tscn')
    Copy-Item -LiteralPath (Join-Path $fixtureSource 'runtime.project.godot') `
        -Destination (Join-Path $derivedRoot 'provider-free-project.godot')
    Copy-Item -LiteralPath (Join-Path $fixtureSource 'qualification-recipe.json') `
        -Destination (Join-Path $runRoot 'recipe.json')
    Copy-Item -LiteralPath (Join-Path $fixtureSource 'qualification-execution-plan.json') `
        -Destination (Join-Path $runRoot 'execution-plan.json')

    $liveMarker = Get-MarkerData -Output $live.NormalizedOutput -Marker 'WORLD_GENERATION_LIVE_SMOKE_PASS'
    $executionFixture = Get-Content -LiteralPath (
        Join-Path $fixtureSource 'qualification-execution-plan.json') -Raw | ConvertFrom-Json
    $promotionCandidateReport = $null
    $promotionCandidateHash = $null
    $promotionCandidateScene = $null
    $promotionCandidateProviderReferences = @()
    $promotionProviderKeys = @()
    if ($QualificationProfile -eq 'reference-2km') {
        $compactPlanId = ([string]$executionFixture.planId).Replace('-', '').ToLowerInvariant()
        if ($compactPlanId -notmatch '^[0-9a-f]{32}$') {
            throw 'Qualification execution plan has no stable promotion-candidate ID.'
        }
        $promotionCandidateSource = Join-Path $fixtureRoot (
            "world/generation/artifacts/$compactPlanId")
        $promotionCandidateRoot = Join-Path $runRoot 'promotion-candidate'
        $promotionCandidateScene = Join-Path $promotionCandidateSource 'derived-world.tscn'
        $promotionCandidateReportPath = Join-Path $promotionCandidateSource 'validation-report.json'
        if (-not (Test-Path -LiteralPath $promotionCandidateScene -PathType Leaf) -or
            -not (Test-Path -LiteralPath $promotionCandidateReportPath -PathType Leaf)) {
            throw 'Live generation did not persist a canonical-promotion candidate and validation report.'
        }
        Copy-Tree -Source $promotionCandidateSource -Destination $promotionCandidateRoot
        $promotionCandidateReport = Get-Content -LiteralPath $promotionCandidateReportPath -Raw |
            ConvertFrom-Json
        $promotionCandidateHash = (
            Get-FileHash -LiteralPath $promotionCandidateScene -Algorithm SHA256).Hash.ToLowerInvariant()
        $promotionCandidateText = [System.IO.File]::ReadAllText($promotionCandidateScene)
        $promotionCandidateProviderReferences = @(@(
                'addons/terrain_3d',
                'addons/flow_nodes_editor',
                'FlowGraphNode3D',
                'Terrain3D'
            ) | Where-Object { $promotionCandidateText.Contains($_) })
        $promotionProviderKeys = @($promotionCandidateReport.providers | ForEach-Object {
            [string]$_.providerKey
        })
        $requiredPromotionStages = @(
            'terrain', 'hydrology', 'biomes', 'roads', 'settlements', 'scatter',
            'gameplay', 'navigation', 'validation')
        $promotionStages = @($promotionCandidateReport.providers | ForEach-Object {
            [string]$_.stage
        } | Sort-Object -Unique)
        $executionStages = @($executionFixture.steps | ForEach-Object {
            [string]$_.stage
        } | Sort-Object -Unique)
        if ([string]$promotionCandidateReport.planId -ne [string]$executionFixture.planId -or
            [string]$promotionCandidateReport.derivedSceneSha256 -ne $promotionCandidateHash -or
            [string]$promotionCandidateReport.contentFingerprint -ne [string]$liveMarker.content_fingerprint -or
            -not [bool]$promotionCandidateReport.validationSucceeded -or
            -not [bool]$promotionCandidateReport.collisionValidated -or
            -not [bool]$promotionCandidateReport.navigationValidated -or
            -not [bool]$promotionCandidateReport.manualEditsPreserved -or
            $promotionCandidateProviderReferences.Count -ne 0 -or
            $promotionStages.Count -ne $requiredPromotionStages.Count -or
            $executionStages.Count -ne $requiredPromotionStages.Count -or
            @($requiredPromotionStages | Where-Object { $promotionStages -notcontains $_ }).Count -ne 0 -or
            @($requiredPromotionStages | Where-Object { $executionStages -notcontains $_ }).Count -ne 0 -or
            $promotionProviderKeys -notcontains 'terrain3d' -or
            $promotionProviderKeys -notcontains $GraphProvider) {
            throw 'Canonical-promotion candidate report does not match the complete live qualified result.'
        }
    }

    $receiptPath = Join-Path $fixtureRoot '.openrpg/world-generation/provider-installation.json'
    if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
        throw 'Pinned provider receipt was not produced.'
    }
    Copy-Item -LiteralPath $receiptPath -Destination (Join-Path $runRoot 'provider-installation-receipt.json')
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    $providers = foreach ($provider in @($receipt.providers)) {
        $manifestEntry = @($dependencyManifest.dependencies | Where-Object providerKey -eq $provider.providerKey)
        if ($manifestEntry.Count -ne 1) {
            throw "Installed provider '$($provider.providerKey)' has no unique dependency manifest entry."
        }
        [ordered]@{
            backend = [string]$provider.backend
            providerKey = [string]$provider.providerKey
            displayName = [string]$manifestEntry[0].displayName
            versionOrRevision = [string]$provider.versionOrRevision
            repository = [string]$provider.repository
            license = [string]$manifestEntry[0].license
            treeRevision = if ($null -ne $provider.PSObject.Properties['treeRevision']) { [string]$provider.treeRevision } else { $null }
            submodules = if ($null -ne $provider.PSObject.Properties['submodules']) { @($provider.submodules) } else { @() }
            sourceBuild = if ($null -ne $provider.PSObject.Properties['sourceBuild']) { $provider.sourceBuild } else { $null }
            evidence = @($provider.evidence)
            receiptVerified = $true
        }
    }
    Write-JsonArtifact -Value ([ordered]@{
        schemaVersion = 1
        qualificationProfile = $QualificationProfile
        installer = [string]$receipt.installer
        generatedAtUtc = [string]$receipt.generatedAtUtc
        providers = @($providers)
    }) -Path (Join-Path $runRoot 'providers.json')

    $reopenMarker = Get-MarkerData -Output $reopen.NormalizedOutput -Marker 'WORLD_GENERATION_REOPEN_PASS'
    $captureMarker = if ($null -ne $capture) {
        Get-MarkerData -Output $capture.NormalizedOutput -Marker 'WORLD_GENERATION_CAPTURE_PASS'
    }
    else {
        $null
    }
    $scenePath = Join-Path $derivedRoot 'generated_world.tscn'
    $sceneText = [System.IO.File]::ReadAllText($scenePath)
    $forbiddenDependencies = @(
        'addons/terrain_3d',
        'addons/flow_nodes_editor',
        'FlowGraphNode3D',
        'Terrain3D'
    )
    $providerReferences = @($forbiddenDependencies | Where-Object { $sceneText.Contains($_) })
    if ($providerReferences.Count -ne 0) {
        throw "Derived scene retained provider references: $($providerReferences -join ', ')."
    }
    $sceneHash = (Get-FileHash -LiteralPath $scenePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ([string]$reopenMarker.scene_sha256 -ne $sceneHash -or
        [string]$liveMarker.baked_sha256 -ne $sceneHash) {
        throw 'Saved, reopened and reported scene fingerprints do not match.'
    }

    $captureFiles = @(if (Test-Path -LiteralPath $capturesRoot -PathType Container) {
        Get-ChildItem -LiteralPath $capturesRoot -Filter '*.png' -File | Sort-Object Name
    })
    $sceneManifest = [ordered]@{
        schemaVersion = 1
        relativePath = 'derived/generated_world.tscn'
        sha256 = $sceneHash
        bytes = (Get-Item -LiteralPath $scenePath).Length
        nodeCount = [regex]::Matches($sceneText, '(?m)^\[node ').Count
        providerReferenceCount = $providerReferences.Count
        terrainMeshCount = [long]$reopenMarker.terrain_mesh
        collisionHit = [long]$reopenMarker.collision_hit
        navigationPolygonCount = [long]$reopenMarker.navigation_polygons
        navigationPathPointCount = [long]$reopenMarker.navigation_path
        generatedInstanceCount = [long]$reopenMarker.pcg_instances
        promotionCandidate = if ($null -ne $promotionCandidateReport) {
            [ordered]@{
                sceneRelativePath = 'promotion-candidate/derived-world.tscn'
                reportRelativePath = 'promotion-candidate/validation-report.json'
                sha256 = $promotionCandidateHash
                bytes = (Get-Item -LiteralPath $promotionCandidateScene).Length
                contentFingerprint = [string]$promotionCandidateReport.contentFingerprint
                providerCount = $promotionProviderKeys.Count
                providerReferenceCount = $promotionCandidateProviderReferences.Count
            }
        }
        else {
            $null
        }
        captures = @($captureFiles | ForEach-Object {
            [ordered]@{
                relativePath = "captures/$($_.Name)"
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                bytes = $_.Length
            }
        })
    }
    Write-JsonArtifact -Value $sceneManifest -Path (Join-Path $runRoot 'scene-manifest.json')

    $allStepOutput = @($import.Output + $providerLoad.Output + $live.Output + $reopen.Output)
    if ($null -ne $capture) {
        $allStepOutput += $capture.Output
    }
    $normalizedLines = @($allStepOutput | ForEach-Object { Remove-AnsiEscape $_ })
    $warnings = @($normalizedLines | Where-Object { $_ -match '^WARNING:' } | Sort-Object -Unique)
    $knownEditorShutdownErrors = @($normalizedLines | Where-Object {
        $_ -match '^ERROR: \d+ RID allocations? .* leaked at exit\.$' -or
        $_ -match '^ERROR: \d+ ObjectDB instances were leaked at exit' -or
        $_ -match '^ERROR: \d+ resources still in use at exit' -or
        $_ -match '^ERROR: (Texture|Buffer) with GL ID of \d+: leaked \d+ bytes\.$' -or
        $_ -match '^ERROR: \d+ shaders of type [A-Za-z0-9_]+ were never freed$' -or
        $_ -eq 'ERROR: Parameter "current_window" is null.'
    } | Sort-Object -Unique)
    Write-JsonArtifact -Value ([ordered]@{
        schemaVersion = 1
        violationCount = 0
        violations = @()
        warningCount = $warnings.Count
        warnings = $warnings
        knownEditorScriptShutdownDiagnosticCount = $knownEditorShutdownErrors.Count
        knownEditorScriptShutdownDiagnostics = $knownEditorShutdownErrors
    }) -Path (Join-Path $runRoot 'violations.json')

    $metrics = [ordered]@{
        schemaVersion = 1
        qualificationProfile = $QualificationProfile
        repeatCount = [long]$liveMarker.runs
        terrainRegionCount = [long]$liveMarker.terrain3d_regions
        terrainWidthMeters = [double]$liveMarker.terrain_width
        terrainDepthMeters = [double]$liveMarker.terrain_depth
        generatedInstanceCount = [long]$liveMarker.pcg_instances
        fallbackCount = [long]$liveMarker.fallback
        collisionShapeCount = [long]$liveMarker.collision
        navigationPolygonCount = [long]$liveMarker.navigation_polygons
        navigationPathPointCount = [long]$reopenMarker.navigation_path
        terrainFingerprint = [string]$liveMarker.terrain_fingerprint
        graphFingerprint = [string]$liveMarker.graph_fingerprint
        sceneContentFingerprint = [string]$liveMarker.content_fingerprint
        sceneFingerprint = $sceneHash
        sceneBytes = (Get-Item -LiteralPath $scenePath).Length
        promotionCandidateContentFingerprint = if ($null -ne $promotionCandidateReport) {
            [string]$promotionCandidateReport.contentFingerprint
        }
        else {
            $null
        }
        promotionCandidateFingerprint = $promotionCandidateHash
        captureCount = $captureFiles.Count
        captureFingerprint = if ($null -ne $captureMarker) { [string]$captureMarker.fingerprint } else { $null }
        durationsMilliseconds = [ordered]@{
            install = $installerStopwatch.ElapsedMilliseconds
            editorImport = $import.DurationMilliseconds
            providerLoad = $providerLoad.DurationMilliseconds
            liveGenerationBake = $live.DurationMilliseconds
            providerFreeReopen = $reopen.DurationMilliseconds
            captures = if ($null -ne $capture) { $capture.DurationMilliseconds } else { $null }
        }
    }
    Write-JsonArtifact -Value $metrics -Path (Join-Path $runRoot 'metrics.json')

    $gpu = if ($null -ne $capture -and $capture.NormalizedOutput -match 'Using Device:\s*(.+)') {
        $Matches[1].Trim()
    }
    else {
        'not-observed'
    }
    $cpu = if (-not [string]::IsNullOrWhiteSpace($env:PROCESSOR_IDENTIFIER)) {
        $env:PROCESSOR_IDENTIFIER
    }
    elseif (Test-Path -LiteralPath '/proc/cpuinfo') {
        $cpuLine = Get-Content -LiteralPath '/proc/cpuinfo' | Where-Object { $_ -match '^model name\s*:' } | Select-Object -First 1
        if ($null -ne $cpuLine) { ($cpuLine -split ':', 2)[1].Trim() } else { 'unknown' }
    }
    else {
        'unknown'
    }
    Write-JsonArtifact -Value ([ordered]@{
        schemaVersion = 1
        qualificationProfile = $QualificationProfile
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        osDescription = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        osArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        processArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
        cpu = $cpu
        logicalProcessorCount = [Environment]::ProcessorCount
        availableMemoryBytes = [System.GC]::GetGCMemoryInfo().TotalAvailableMemoryBytes
        gpu = $gpu
        godotVersion = $godotVersion
        dotnetVersion = (& dotnet --version).Trim()
        powershellVersion = $PSVersionTable.PSVersion.ToString()
    }) -Path (Join-Path $runRoot 'environment.json')

    Write-JsonArtifact -Value ([ordered]@{
        schemaVersion = 1
        status = 'pending-human-review'
        required = $true
        capturesAvailable = ($captureFiles.Count -gt 0)
        claimsVisualImprovement = $false
        qualificationProfile = $QualificationProfile
        notes = if ($QualificationProfile -eq 'reference-2km') {
            @(
                'These normalized captures expose the complete technical 2 km reference proxy for human review.',
                'Artistic improvement remains unclaimed until blind scoring and measured manual correction are completed.'
            )
        }
        else {
            @(
                'These captures prove deterministic rendering viewpoints for the minimal addon qualification only.',
                'Blind artistic scoring is deferred to the complete 2 km reference-world benchmark.'
            )
        }
    }) -Path (Join-Path $runRoot 'manual-review.json')

    $currentPlatform = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
            [System.Runtime.InteropServices.OSPlatform]::Windows)) { 'windows' }
        elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
            [System.Runtime.InteropServices.OSPlatform]::Linux)) { 'linux' }
        else { 'macos' }
    $remainingPlatforms = @('windows', 'linux') | Where-Object { $_ -ne $currentPlatform }
    $qualificationGates = [ordered]@{
        exactGodotVersion = $true
        pinnedReceipt = $true
        realTerrain3DExecution = ([long]$liveMarker.terrain3d_regions -gt 0)
        realGraphExecution = ([long]$liveMarker.pcg_instances -gt 0)
        nativeBackendWithoutFallback = ([long]$liveMarker.fallback -eq 0)
        deterministicThreeRuns = (
            [long]$liveMarker.runs -eq 3 -and
            [string]$liveMarker.content_fingerprint -match '^[0-9a-f]{64}$')
        collision = ([long]$reopenMarker.collision_hit -eq 1)
        navigationPath = ([long]$reopenMarker.navigation_path -ge 2)
        savedAndReopened = $true
        providerFreeRuntime = ($providerReferences.Count -eq 0)
        normalizedCaptures = ($captureFiles.Count -eq 5)
    }
    if ($QualificationProfile -eq 'reference-2km') {
        $qualificationGates.canonicalPromotionCandidate = (
            [string]$promotionCandidateReport.contentFingerprint -eq
                [string]$liveMarker.content_fingerprint -and
            [string]$promotionCandidateReport.derivedSceneSha256 -eq
                $promotionCandidateHash -and
            $promotionCandidateProviderReferences.Count -eq 0)
    }
    $qualificationReport = [ordered]@{
        schemaVersion = 1
        outcome = 'passed-platform-smoke'
        qualificationProfile = $QualificationProfile
        providerKey = $GraphProvider
        providerVersion = $providerRevision
        platform = $currentPlatform
        decision = 'experimental'
        gates = $qualificationGates
        remainingGates = if ($QualificationProfile -eq 'reference-2km') {
            @(
                "Repeat the same 2 km reference qualification on: $($remainingPlatforms -join ', ')",
                'Qualify provider-free Windows and Linux exports.',
                'Complete blind visual scoring and measured manual-correction effort.'
            )
        }
        else {
            @(
                "Repeat the same addon smoke on: $($remainingPlatforms -join ', ')",
                'Qualify provider-free Windows and Linux exports.',
                'Run the complete 2 km reference-world benchmark.',
                'Complete blind visual scoring and measured manual-correction effort.'
            )
        }
    }
    Write-JsonArtifact -Value $qualificationReport -Path (Join-Path $runRoot 'qualification-report.json')

    $hashLines = foreach ($file in Get-ChildItem -LiteralPath $runRoot -File -Recurse | Sort-Object FullName) {
        if ($file.FullName.StartsWith(
                $workRoot + [System.IO.Path]::DirectorySeparatorChar,
                [System.StringComparison]::OrdinalIgnoreCase) -or
            $file.Name -eq 'hashes.sha256') {
            continue
        }
        $relativePath = [System.IO.Path]::GetRelativePath($runRoot, $file.FullName).Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $relativePath"
    }
    Set-Content -LiteralPath (Join-Path $runRoot 'hashes.sha256') -Value $hashLines -Encoding utf8NoBOM

    Write-Output (
        "WORLD_GENERATION_ADDON_QUALIFICATION_PASS profile=$QualificationProfile provider=$GraphProvider platform=$currentPlatform " +
        "runs=$($liveMarker.runs) fallback=$($liveMarker.fallback) collisions=$($reopenMarker.collision_hit) " +
        "navigation_path=$($reopenMarker.navigation_path) captures=$($captureFiles.Count) " +
        "promotion_candidate=$([int]($null -ne $promotionCandidateReport)) " +
        "scene_sha256=$sceneHash artifact=$runRoot")
}
catch {
    Write-JsonArtifact -Value ([ordered]@{
        schemaVersion = 1
        outcome = 'failed'
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        providerKey = $GraphProvider
        message = $_.Exception.Message
    }) -Path (Join-Path $runRoot 'failure.json')
    throw
}
