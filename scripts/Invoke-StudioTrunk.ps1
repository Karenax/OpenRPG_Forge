[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet("build", "serve")]
    [string]$Action,

    [switch]$RequireExactCommit
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$studioRoot = Join-Path $repositoryRoot "apps/forge_studio"
$previousNoColor = $env:NO_COLOR
$previousForgeSourceCommit = $env:FORGE_SOURCE_COMMIT

try {
    # Trunk accepts only boolean strings for NO_COLOR. Normalize inherited
    # values such as the commonly used "1" so Tauri hooks stay reproducible.
    $env:NO_COLOR = "true"
    if ($RequireExactCommit) {
        $status = @(& git -C $repositoryRoot status --porcelain=v1 --untracked-files=all)
        if ($LASTEXITCODE -ne 0) {
            throw "Unable to inspect the Git worktree before the exact Studio build."
        }
        if ($status.Count -ne 0) {
            throw "The exact Studio build requires a completely clean Git worktree."
        }
        $commit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0 -or $commit -cnotmatch '^[0-9a-f]{40}$') {
            throw "Unable to resolve the exact Git commit for the Studio build."
        }
        $env:FORGE_SOURCE_COMMIT = $commit
    }
    Push-Location $studioRoot
    try {
        if ($Action -eq "build") {
            & trunk build --release --config Trunk.toml
        } else {
            & trunk serve --config Trunk.toml --no-autoreload
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
    if ($null -eq $previousForgeSourceCommit) {
        Remove-Item Env:FORGE_SOURCE_COMMIT -ErrorAction SilentlyContinue
    } else {
        $env:FORGE_SOURCE_COMMIT = $previousForgeSourceCommit
    }
}
