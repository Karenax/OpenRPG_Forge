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
