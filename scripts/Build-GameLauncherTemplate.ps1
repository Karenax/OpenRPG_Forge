[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDirectory = 'artifacts/templates/game-windows-x64',
    [string]$EnginePath,
    [switch]$Force,
    [switch]$SkipRuntimeSmoke
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
if (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot $OutputDirectory
}

$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$allowedPrefix = $artifactsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $outputPath.StartsWith($allowedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The launcher template output must be a child of $artifactsRoot."
}
if (Test-Path -LiteralPath $outputPath) {
    if (-not $Force) {
        throw "The launcher template already exists. Pass -Force to replace it: $outputPath"
    }
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

$outputParent = Split-Path -Parent $outputPath
New-Item -ItemType Directory -Path $outputParent -Force | Out-Null
$temporaryPath = Join-Path $outputParent ".game-launcher-template-$([Guid]::NewGuid().ToString('N')).tmp"
$runtimeBuildPath = Join-Path $artifactsRoot "distribution/.game-launcher-runtime-$([Guid]::NewGuid().ToString('N'))"

function Invoke-Dotnet {
    param([Parameter(Mandatory)][string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

try {
    New-Item -ItemType Directory -Path $temporaryPath -Force | Out-Null
    $publishPath = Join-Path $temporaryPath '.build/launcher'
    $restoreLock = Join-Path $temporaryPath '.build/launcher.restore.lock.json'
    $launcherProject = Join-Path $repositoryRoot 'apps/Forge.GameLauncher/Forge.GameLauncher.csproj'
    Invoke-Dotnet @(
        'restore', $launcherProject, '--runtime', 'win-x64', '--force',
        "-p:NuGetLockFilePath=$restoreLock", '-p:RestorePackagesWithLockFile=false'
    )
    Invoke-Dotnet @(
        'publish', $launcherProject, '--configuration', $Configuration,
        '--runtime', 'win-x64', '--self-contained', 'true', '--no-restore',
        '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:DebugType=None', '--output', $publishPath
    )

    $launcher = Join-Path $publishPath 'OpenRPG_Forge_GameLauncher.exe'
    if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
        throw 'The game launcher publish did not produce OpenRPG_Forge_GameLauncher.exe.'
    }
    Copy-Item -LiteralPath $launcher -Destination (Join-Path $temporaryPath 'OpenRPG_Forge_GameLauncher.exe')

    $runtimeParameters = @{
        Configuration = $Configuration
        OutputDirectory = $runtimeBuildPath
        Force = $true
    }
    if (-not [string]::IsNullOrWhiteSpace($EnginePath)) {
        $runtimeParameters.EnginePath = $EnginePath
    }
    if ($SkipRuntimeSmoke) {
        $runtimeParameters.SkipSmoke = $true
    }
    & (Join-Path $PSScriptRoot 'Build-RuntimeDistribution.ps1') @runtimeParameters
    if ($LASTEXITCODE -ne 0) {
        throw "Runtime template export failed with exit code $LASTEXITCODE."
    }

    $runtimeTarget = Join-Path $temporaryPath 'runtime'
    $runtimePrefix = $runtimeBuildPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    foreach ($sourceFile in Get-ChildItem -LiteralPath $runtimeBuildPath -Recurse -File) {
        $relativePath = $sourceFile.FullName.Substring($runtimePrefix.Length)
        $segments = $relativePath -split '[\\/]'
        if ($segments -contains 'content' -or
            $sourceFile.Name -in @('distribution-manifest.json', 'export.log', 'runtime-smoke.log')) {
            continue
        }
        $targetFile = Join-Path $runtimeTarget $relativePath
        New-Item -ItemType Directory -Path (Split-Path -Parent $targetFile) -Force | Out-Null
        Copy-Item -LiteralPath $sourceFile.FullName -Destination $targetFile -Force
    }

    foreach ($requiredFile in @(
        (Join-Path $temporaryPath 'OpenRPG_Forge_GameLauncher.exe'),
        (Join-Path $runtimeTarget 'OpenRPG_Forge_Runtime.exe'),
        (Join-Path $runtimeTarget 'OpenRPG_Forge_Runtime.pck')
    )) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw "The assembled game launcher template is incomplete: $requiredFile"
        }
    }

    Remove-Item -LiteralPath (Join-Path $temporaryPath '.build') -Recurse -Force
    [System.IO.Directory]::Move($temporaryPath, $outputPath)
    $files = @(Get-ChildItem -LiteralPath $outputPath -Recurse -File)
    Write-Output "OPENRPG_FORGE_GAME_LAUNCHER_TEMPLATE_READY path=$outputPath files=$($files.Count) runtime_smoke=$(-not $SkipRuntimeSmoke)"
}
finally {
    if (Test-Path -LiteralPath $temporaryPath) {
        Remove-Item -LiteralPath $temporaryPath -Recurse -Force
    }
    if (Test-Path -LiteralPath $runtimeBuildPath) {
        Remove-Item -LiteralPath $runtimeBuildPath -Recurse -Force
    }
}
