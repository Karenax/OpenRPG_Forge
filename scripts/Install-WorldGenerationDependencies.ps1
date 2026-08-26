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
$projectPrefix = $projectRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar

if (-not (Test-Path (Join-Path $projectRoot 'project.godot'))) {
    throw "'$projectRoot' is not a Godot project: project.godot was not found."
}
if (-not (Test-Path $manifestPath)) {
    throw "Dependency manifest '$manifestPath' was not found."
}
if (-not (Test-Path $integrationSource -PathType Container)) {
    throw "OpenRPG integration addon '$integrationSource' was not found."
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$terrain = @($manifest.dependencies | Where-Object key -eq 'terrain3d')
$pcg = @($manifest.dependencies | Where-Object key -eq 'pcgodot')
if ($manifest.schemaVersion -ne 1 -or $terrain.Count -ne 1 -or $pcg.Count -ne 1) {
    throw 'The world-generation dependency manifest does not match schema version 1.'
}
$terrain = $terrain[0]
$pcg = $pcg[0]
if ([string]$terrain.archive -notmatch '^https://' -or
    [string]$terrain.sha256 -notmatch '^[0-9a-f]{64}$') {
    throw 'Terrain3D must use an HTTPS archive and a lowercase SHA-256 pin.'
}
if ([string]$pcg.repository -notmatch '^https://' -or
    [string]$pcg.revision -notmatch '^[0-9a-f]{40}$') {
    throw 'The PCG backend must use an HTTPS repository and an immutable lowercase Git commit.'
}

function Resolve-ProjectTarget {
    param([Parameter(Mandatory = $true)][string]$RelativePath)
    if ([System.IO.Path]::IsPathRooted($RelativePath) -or $RelativePath -match '(^|[\\/])\.\.([\\/]|$)') {
        throw "Project target '$RelativePath' is not a safe relative path."
    }
    $resolved = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $RelativePath))
    if (-not $resolved.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Project target '$resolved' escapes '$projectRoot'."
    }
    return $resolved
}

$terrainTarget = Resolve-ProjectTarget ([string]$terrain.godotTarget)
$pcgAddonTarget = Resolve-ProjectTarget ([string]$pcg.godotTargets[0])
$pcgBinaryTarget = Resolve-ProjectTarget ([string]$pcg.godotTargets[1])
$integrationTarget = Resolve-ProjectTarget ([string]$manifest.integrationAddon.godotTarget)

if ($WhatIfPreference) {
    if (-not $SkipTerrain3D) { $PSCmdlet.ShouldProcess($terrainTarget, "Install pinned Terrain3D $($terrain.version)") | Out-Null }
    if (-not $SkipPcgGodot) {
        $PSCmdlet.ShouldProcess($pcgAddonTarget, "Install pinned Godot Flow commit $($pcg.revision)") | Out-Null
        $PSCmdlet.ShouldProcess($pcgBinaryTarget, 'Merge pinned Godot Flow runtime binaries') | Out-Null
    }
    $PSCmdlet.ShouldProcess($integrationTarget, 'Install the OpenRPG world-generation bridge') | Out-Null
    return
}

$temporaryParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$temporaryPrefix = $temporaryParent.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
$tempRoot = Join-Path $temporaryParent ("openrpg-worldgen-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

function Copy-DirectoryChecked {
    param([string]$Source, [string]$Destination)
    if (-not (Test-Path $Source)) { throw "Source directory '$Source' does not exist." }
    $resolvedDestination = [System.IO.Path]::GetFullPath($Destination)
    if (-not $resolvedDestination.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Destination '$resolvedDestination' escapes '$projectRoot'."
    }
    if ((Test-Path $Destination) -and -not $Force) {
        throw "Destination '$Destination' already exists. Re-run with -Force to replace it."
    }
    if ($PSCmdlet.ShouldProcess($Destination, "Install from $Source")) {
        $parent = Split-Path -Parent $Destination
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
        $staging = Join-Path $parent ("." + [System.IO.Path]::GetFileName($Destination) + ".staging-" + [guid]::NewGuid().ToString('N'))
        $backup = Join-Path $parent ("." + [System.IO.Path]::GetFileName($Destination) + ".backup-" + [guid]::NewGuid().ToString('N'))
        try {
            Copy-Item -LiteralPath $Source -Destination $staging -Recurse
            if (Test-Path $Destination) { Move-Item -LiteralPath $Destination -Destination $backup }
            Move-Item -LiteralPath $staging -Destination $Destination
            if (Test-Path $backup) { Remove-Item -LiteralPath $backup -Recurse -Force }
        }
        catch {
            if (Test-Path $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
            if ((Test-Path $backup) -and -not (Test-Path $Destination)) {
                Move-Item -LiteralPath $backup -Destination $Destination
            }
            throw
        }
    }
}

function Copy-DirectoryContentsChecked {
    param([string]$Source, [string]$Destination)
    if (-not (Test-Path $Source -PathType Container)) { throw "Source directory '$Source' does not exist." }
    $resolvedDestination = [System.IO.Path]::GetFullPath($Destination)
    if (-not $resolvedDestination.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Destination '$resolvedDestination' escapes '$projectRoot'."
    }
    $files = @(Get-ChildItem -LiteralPath $Source -File -Recurse)
    $collisions = @($files | Where-Object {
        $relative = [System.IO.Path]::GetRelativePath($Source, $_.FullName)
        Test-Path (Join-Path $Destination $relative)
    })
    if ($collisions.Count -gt 0 -and -not $Force) {
        throw "Destination '$Destination' contains $($collisions.Count) conflicting PCG file(s). Re-run with -Force to replace only those files."
    }
    if (-not $PSCmdlet.ShouldProcess($Destination, "Merge files from $Source")) { return }

    $backupRoot = Join-Path $tempRoot ("pcg-bin-backup-" + [guid]::NewGuid().ToString('N'))
    $written = [System.Collections.Generic.List[string]]::new()
    try {
        foreach ($file in $files) {
            $relative = [System.IO.Path]::GetRelativePath($Source, $file.FullName)
            $target = [System.IO.Path]::GetFullPath((Join-Path $Destination $relative))
            if (-not $target.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "PCG binary target '$target' escapes '$projectRoot'."
            }
            if (Test-Path $target) {
                $backup = Join-Path $backupRoot $relative
                New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
                Copy-Item -LiteralPath $target -Destination $backup
            }
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            $written.Add($target)
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        }
    }
    catch {
        foreach ($target in $written) { if (Test-Path $target) { Remove-Item -LiteralPath $target -Force } }
        if (Test-Path $backupRoot) {
            foreach ($backup in Get-ChildItem -LiteralPath $backupRoot -File -Recurse) {
                $relative = [System.IO.Path]::GetRelativePath($backupRoot, $backup.FullName)
                $target = Join-Path $Destination $relative
                New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
                Copy-Item -LiteralPath $backup.FullName -Destination $target -Force
            }
        }
        throw
    }
}

try {
    if (-not $SkipTerrain3D) {
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
        Copy-DirectoryChecked -Source $plugin.Directory.FullName -Destination $terrainTarget
    }

    if (-not $SkipPcgGodot) {
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
        Copy-DirectoryChecked -Source (Join-Path $clone 'demo/addons/flow_nodes_editor') -Destination $pcgAddonTarget
        Copy-DirectoryContentsChecked -Source (Join-Path $clone 'demo/bin') -Destination $pcgBinaryTarget
    }

    Copy-DirectoryChecked -Source $integrationSource -Destination $integrationTarget

    Write-Host ''
    Write-Host 'World-generation dependencies installed.'
    Write-Host 'In Godot, enable these editor plugins:'
    if (-not $SkipTerrain3D) { Write-Host '  - Terrain3D' }
    if (-not $SkipPcgGodot) { Write-Host '  - Flow Nodes Editor' }
    Write-Host '  - OpenRPG World Generation Bridge'
    Write-Host 'Then create or assign the reusable graphs under res://world/generation/graphs/.'
}
finally {
    $resolvedTemporary = [System.IO.Path]::GetFullPath($tempRoot)
    if (-not $resolvedTemporary.StartsWith($temporaryPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        [System.IO.Path]::GetFileName($resolvedTemporary) -notmatch '^openrpg-worldgen-[0-9a-f]{32}$') {
        throw "Refusing to clean unexpected temporary path '$resolvedTemporary'."
    }
    if (Test-Path $resolvedTemporary) { Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force }
}
