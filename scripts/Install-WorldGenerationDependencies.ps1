[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$GodotProjectPath,

    [switch]$Force,
    [switch]$SkipTerrain3D,
    [switch]$SkipPcgGodot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repositoryRoot 'third_party/world-generation.dependencies.json'
$integrationSource = Join-Path $repositoryRoot 'integrations/godot/addons/openrpg_world_generation'
$projectRoot = [System.IO.Path]::GetFullPath($GodotProjectPath)

if (-not (Test-Path (Join-Path $projectRoot 'project.godot'))) {
    throw "'$projectRoot' is not a Godot project: project.godot was not found."
}
if (-not (Test-Path $manifestPath)) {
    throw "Dependency manifest '$manifestPath' was not found."
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("openrpg-worldgen-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

function Copy-DirectoryChecked {
    param([string]$Source, [string]$Destination)
    if (-not (Test-Path $Source)) { throw "Source directory '$Source' does not exist." }
    if ((Test-Path $Destination) -and -not $Force) {
        throw "Destination '$Destination' already exists. Re-run with -Force to replace it."
    }
    if ($PSCmdlet.ShouldProcess($Destination, "Install from $Source")) {
        if (Test-Path $Destination) { Remove-Item $Destination -Recurse -Force }
        $parent = Split-Path -Parent $Destination
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
        Copy-Item $Source $Destination -Recurse -Force
    }
}

try {
    if (-not $SkipTerrain3D) {
        $terrain = $manifest.dependencies | Where-Object key -eq 'terrain3d'
        $archive = Join-Path $tempRoot 'terrain3d.zip'
        Invoke-WebRequest -Uri $terrain.archive -OutFile $archive
        $actualHash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne [string]$terrain.sha256) {
            throw "Terrain3D SHA-256 mismatch. Expected $($terrain.sha256), received $actualHash."
        }
        $expanded = Join-Path $tempRoot 'terrain3d'
        Expand-Archive -Path $archive -DestinationPath $expanded
        $plugin = Get-ChildItem $expanded -Recurse -Filter plugin.cfg |
            Where-Object FullName -Match '[\\/]addons[\\/]terrain_3d[\\/]plugin\.cfg$' |
            Select-Object -First 1
        if ($null -eq $plugin) { throw 'Terrain3D addon directory was not found in the verified archive.' }
        Copy-DirectoryChecked -Source $plugin.Directory.FullName -Destination (Join-Path $projectRoot 'addons/terrain_3d')
    }

    if (-not $SkipPcgGodot) {
        $pcg = $manifest.dependencies | Where-Object key -eq 'pcgodot'
        $git = Get-Command git -ErrorAction SilentlyContinue
        if ($null -eq $git) { throw 'Git is required to install the pinned PCG backend and its submodules.' }
        $clone = Join-Path $tempRoot 'godot_flow'
        & $git.Source clone --filter=blob:none --no-checkout ([string]$pcg.repository) $clone
        if ($LASTEXITCODE -ne 0) { throw 'Unable to clone the PCG backend repository.' }
        & $git.Source -C $clone checkout --detach ([string]$pcg.revision)
        if ($LASTEXITCODE -ne 0) { throw "Unable to checkout PCG revision $($pcg.revision)." }
        & $git.Source -C $clone submodule update --init --recursive
        if ($LASTEXITCODE -ne 0) { throw 'Unable to initialize PCG backend submodules.' }
        $resolved = (& $git.Source -C $clone rev-parse HEAD).Trim()
        if ($resolved -ne [string]$pcg.revision) { throw "PCG revision mismatch: $resolved." }
        Copy-DirectoryChecked -Source (Join-Path $clone 'demo/addons/flow_nodes_editor') -Destination (Join-Path $projectRoot 'addons/flow_nodes_editor')
        Copy-DirectoryChecked -Source (Join-Path $clone 'demo/bin') -Destination (Join-Path $projectRoot 'bin')
    }

    Copy-DirectoryChecked -Source $integrationSource -Destination (Join-Path $projectRoot 'addons/openrpg_world_generation')

    Write-Host ''
    Write-Host 'World-generation dependencies installed.'
    Write-Host 'In Godot, enable these editor plugins:'
    Write-Host '  - Terrain3D'
    Write-Host '  - Flow Nodes Editor'
    Write-Host '  - OpenRPG World Generation Bridge'
    Write-Host 'Then create or assign the reusable graphs under res://world/generation/graphs/.'
}
finally {
    if (Test-Path $tempRoot) { Remove-Item $tempRoot -Recurse -Force }
}
