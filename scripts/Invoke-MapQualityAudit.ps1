[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$WorkspaceRoot,

    [string]$SpawnId,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [ValidateRange(0, 256)]
    [int]$MinimumPatrolActors = 0,

    [ValidateRange(0, 256)]
    [int]$MinimumBuildingGroups = 0,

    [ValidateRange(-1, 3)]
    [int]$RequiredVegetationStrata = -1,

    [ValidateRange(-1, 20)]
    [double]$MinimumGroundCoverDensityPerSquareMeter = -1,

    [string]$GodotExecutable,

    [string]$RuntimeProjectPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$WorkspaceRoot = [System.IO.Path]::GetFullPath($WorkspaceRoot)
if (-not [System.IO.Path]::IsPathRooted($WorkspaceRoot) -or
    -not (Test-Path -LiteralPath (Join-Path $WorkspaceRoot 'project.json') -PathType Leaf)) {
    throw "Workspace '$WorkspaceRoot' is not an absolute OpenRPG workspace."
}
if (-not [string]::IsNullOrWhiteSpace($SpawnId)) {
    $parsedSpawnId = [guid]::Empty
    if (-not [guid]::TryParse($SpawnId, [ref]$parsedSpawnId)) {
        throw "Spawn ID '$SpawnId' is not a UUID."
    }
    $SpawnId = $parsedSpawnId.ToString('D')
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($RuntimeProjectPath)) {
    $RuntimeProjectPath = Join-Path $repositoryRoot 'apps/Forge.Runtime'
}
$RuntimeProjectPath = [System.IO.Path]::GetFullPath($RuntimeProjectPath)
if (-not (Test-Path -LiteralPath (Join-Path $RuntimeProjectPath 'project.godot') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $RuntimeProjectPath 'Forge.Runtime.csproj') -PathType Leaf)) {
    throw "Runtime project '$RuntimeProjectPath' is not a Forge.Runtime Godot project."
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
if (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    throw 'Map-quality output directory must be absolute.'
}
[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null

if ([string]::IsNullOrWhiteSpace($GodotExecutable)) {
    $engine = Get-Command 'Godot_v4.7.1-stable_mono_win64_console.exe' -ErrorAction SilentlyContinue
    if ($null -eq $engine) {
        throw 'Map-quality audit requires the Godot 4.7.1 .NET console executable on PATH.'
    }
    $GodotExecutable = $engine.Source
}
else {
    $GodotExecutable = [System.IO.Path]::GetFullPath($GodotExecutable)
}
if (-not (Test-Path -LiteralPath $GodotExecutable -PathType Leaf)) {
    throw "Godot executable '$GodotExecutable' was not found."
}
$versionOutput = @(& $GodotExecutable --version 2>&1 | ForEach-Object { [string]$_ })
if ($LASTEXITCODE -ne 0 -or ($versionOutput -join "`n") -notmatch '^4\.7\.1\.stable') {
    throw "Map-quality audit requires Godot 4.7.1 stable; received '$($versionOutput -join ' ')'."
}

$runtimeProjectFile = Join-Path $RuntimeProjectPath 'Forge.Runtime.csproj'
# A project launch reads the development assembly from Godot's Debug temp path.
# Building Release alone can therefore leave a stale Debug runtime active and silently skip new gates.
& dotnet build $runtimeProjectFile '--configuration' 'Debug' '--no-restore'
if ($LASTEXITCODE -ne 0) {
    throw 'Forge.Runtime did not compile before the map-quality audit.'
}

$runtimeLog = Join-Path $OutputDirectory 'runtime.log'
$launchLog = Join-Path $OutputDirectory 'audit-launch.log'
$engineArguments = @(
    '--rendering-method', 'forward_plus',
    '--rendering-driver', 'vulkan',
    '--fixed-fps', '60',
    '--path', $RuntimeProjectPath,
    '--log-file', $runtimeLog,
    '--',
    '--workspace', $WorkspaceRoot,
    '--map-quality-audit', $OutputDirectory,
    '--map-quality-min-patrols', $MinimumPatrolActors.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    '--map-quality-min-building-groups', $MinimumBuildingGroups.ToString([System.Globalization.CultureInfo]::InvariantCulture)
)
if (-not [string]::IsNullOrWhiteSpace($SpawnId)) {
    $engineArguments += @('--spawn-id', $SpawnId)
}
if ($RequiredVegetationStrata -ge 0) {
    $engineArguments += @(
        '--map-quality-required-vegetation-strata',
        $RequiredVegetationStrata.ToString([System.Globalization.CultureInfo]::InvariantCulture))
}
if ($MinimumGroundCoverDensityPerSquareMeter -ge 0) {
    $engineArguments += @(
        '--map-quality-min-ground-cover-density',
        $MinimumGroundCoverDensityPerSquareMeter.ToString('R', [System.Globalization.CultureInfo]::InvariantCulture))
}

$output = @(& $GodotExecutable @engineArguments 2>&1 | ForEach-Object { [string]$_ })
$exitCode = $LASTEXITCODE
[System.IO.File]::WriteAllLines($launchLog, $output)
if ($exitCode -ne 0) {
    throw "Runtime map-quality audit failed with exit code $exitCode. See '$runtimeLog' and '$launchLog'."
}

$reportPath = Join-Path $OutputDirectory 'map-quality-machine-audit.json'
if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
    throw "Runtime map-quality audit did not write '$reportPath'."
}
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($report.schemaVersion -ne 1 -or -not [bool]$report.machineResult.passed -or
    -not [bool]$report.visualReviewRequired) {
    throw "Runtime map-quality report '$reportPath' did not pass its machine gate."
}
$captures = @($report.captures)
$requiredCaptureCount = [Math]::Max(3, [int]$report.machineResult.input.requiredCaptureCount)
if ($captures.Count -lt $requiredCaptureCount) {
    throw "Runtime map-quality report contains only $($captures.Count) of $requiredCaptureCount required captures."
}
foreach ($capture in $captures) {
    $path = Join-Path $OutputDirectory ([string]$capture.fileName)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Runtime map-quality capture '$path' is missing."
    }
    $sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($sha256 -ne [string]$capture.sha256) {
        throw "Runtime map-quality capture '$path' no longer matches its report."
    }
}
if ((Get-Content -LiteralPath $runtimeLog -Raw) -notmatch
    'OPENRPG_FORGE_RUNTIME_MAP_QUALITY_MACHINE_PASS') {
    throw "Runtime log '$runtimeLog' does not contain the machine-pass marker."
}

Write-Output "OPENRPG_FORGE_MAP_QUALITY_MACHINE_PASS area=$($report.areaSlug) captures=$($captures.Count) visual_review_required=true report=$reportPath"
Write-Output "Inspect every PNG in '$OutputDirectory', then run Confirm-MapQualityVisualReview.ps1."
