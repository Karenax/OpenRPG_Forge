[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet("build", "serve")]
    [string]$Action
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$studioRoot = Join-Path $repositoryRoot "apps/forge_studio"
$previousNoColor = $env:NO_COLOR

try {
    # Trunk accepts only boolean strings for NO_COLOR. Normalize inherited
    # values such as the commonly used "1" so Tauri hooks stay reproducible.
    $env:NO_COLOR = "true"
    Push-Location $studioRoot
    try {
        if ($Action -eq "build") {
            & trunk build --release --config Trunk.toml
        } else {
            & trunk serve --config Trunk.toml
        }

        if ($LASTEXITCODE -ne 0) {
            throw "trunk $Action failed with exit code $LASTEXITCODE."
        }
    } finally {
        Pop-Location
    }
} finally {
    if ($null -eq $previousNoColor) {
        Remove-Item Env:NO_COLOR -ErrorAction SilentlyContinue
    } else {
        $env:NO_COLOR = $previousNoColor
    }
}
