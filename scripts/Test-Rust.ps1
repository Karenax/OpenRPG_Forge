[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$SkipWasm,
    [switch]$SkipSupplyChain
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-Cargo {
    param([Parameter(Mandatory)] [string[]]$Arguments)
    & cargo @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "cargo $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$root = [IO.Path]::GetFullPath($RepositoryRoot)
$gitRoot = [IO.Path]::GetFullPath((& git -C $root rev-parse --show-toplevel).Trim())
if ($LASTEXITCODE -ne 0 -or $gitRoot -ne $root) {
    throw "RepositoryRoot must be the Forge checkout root: $root"
}
if ((& git -C $root branch --show-current).Trim() -ne "Rust") {
    throw "Validation is allowed only on branch 'Rust'."
}

Push-Location $root
try {
    Invoke-Cargo -Arguments @("fmt", "--all", "--", "--check")
    Invoke-Cargo -Arguments @("clippy", "--workspace", "--all-targets", "--locked", "--", "-D", "warnings")
    Invoke-Cargo -Arguments @("test", "--workspace", "--all-targets", "--locked")
    if (-not $SkipWasm) {
        Invoke-Cargo -Arguments @("check", "-p", "forge_engine", "--target", "wasm32-unknown-unknown", "--no-default-features", "--features", "web", "--locked")
        Invoke-Cargo -Arguments @("clippy", "-p", "forge_studio", "--target", "wasm32-unknown-unknown", "--no-default-features", "--features", "web", "--locked", "--", "-D", "warnings")
        Push-Location (Join-Path $root "apps/forge_studio")
        try {
            $previousNoColor = $env:NO_COLOR
            $env:NO_COLOR = "true"
            & trunk build --release --config Trunk.toml
            if ($LASTEXITCODE -ne 0) {
                throw "trunk build failed."
            }
        } finally {
            $env:NO_COLOR = $previousNoColor
            Pop-Location
        }
    }
    if (-not $SkipSupplyChain) {
        & cargo deny check licenses bans sources advisories
        if ($LASTEXITCODE -ne 0) {
            throw "cargo deny check failed."
        }
        & (Join-Path $PSScriptRoot "Test-RustSec.ps1") -RepositoryRoot $root
    }
} finally {
    Pop-Location
}

Write-Output "FORGE_RUST_VALIDATION_PASS wasm=$(-not $SkipWasm) supply_chain=$(-not $SkipSupplyChain)"
