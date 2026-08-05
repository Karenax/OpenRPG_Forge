Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ForgeEnginePath {
    $command = Get-Command godot -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $packageRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
    if (Test-Path -LiteralPath $packageRoot) {
        $executable = Get-ChildItem `
            -LiteralPath $packageRoot `
            -Filter 'Godot_v4.7.1-stable_mono_win64.exe' `
            -File `
            -Recurse `
            -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($executable) {
            return $executable.FullName
        }
    }

    throw 'Le moteur 3D de développement compatible est introuvable. Voir docs/ENVIRONMENT.md.'
}

function Get-ForgeBlenderPath {
    param([switch]$Optional)

    if (-not [string]::IsNullOrWhiteSpace($env:OPENRPG_BLENDER) -and
        (Test-Path -LiteralPath $env:OPENRPG_BLENDER -PathType Leaf)) {
        return [System.IO.Path]::GetFullPath($env:OPENRPG_BLENDER)
    }

    $command = Get-Command blender -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $installationRoot = Join-Path $env:ProgramFiles 'Blender Foundation'
    if (Test-Path -LiteralPath $installationRoot) {
        $executable = Get-ChildItem -LiteralPath $installationRoot -Directory -Filter 'Blender *' |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName 'blender.exe' } |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
            Select-Object -First 1
        if ($executable) {
            return $executable
        }
    }

    if ($Optional) {
        return $null
    }

    throw 'Blender est introuvable. Installer Blender LTS ou définir OPENRPG_BLENDER.'
}
