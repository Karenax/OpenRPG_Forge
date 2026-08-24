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

function Invoke-ForgeProjectImport {
    param(
        [Parameter(Mandatory)][string]$EnginePath,
        [Parameter(Mandatory)][string]$ProjectPath,
        [Parameter(Mandatory)][string]$LogPath,
        [int]$TimeoutSeconds = 180
    )

    $resolvedEngine = [System.IO.Path]::GetFullPath($EnginePath)
    $resolvedProject = [System.IO.Path]::GetFullPath($ProjectPath)
    $resolvedLog = [System.IO.Path]::GetFullPath($LogPath)
    if (-not (Test-Path -LiteralPath $resolvedEngine -PathType Leaf)) {
        throw "Godot import engine is missing: $resolvedEngine"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $resolvedProject 'project.godot') -PathType Leaf)) {
        throw "Godot import project is invalid: $resolvedProject"
    }
    [System.IO.Directory]::CreateDirectory((Split-Path -Parent $resolvedLog)) | Out-Null

    $process = Start-Process -FilePath $resolvedEngine -ArgumentList @(
        '--headless', '--import', '--path', "`"$resolvedProject`"", '--log-file', "`"$resolvedLog`""
    ) -WorkingDirectory $resolvedProject -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        throw "Godot import exceeded $TimeoutSeconds seconds. Log: $resolvedLog"
    }
    if ($process.ExitCode -ne 0) {
        throw "Godot import failed with exit code $($process.ExitCode). Log: $resolvedLog"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $resolvedProject '.godot') -PathType Container)) {
        throw "Godot import did not create its project cache. Log: $resolvedLog"
    }

    Write-Host "OPENRPG_FORGE_GODOT_IMPORT_PASS project=$resolvedProject"
}
