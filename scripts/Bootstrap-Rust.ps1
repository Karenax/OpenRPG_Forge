[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$InstallTools
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = [IO.Path]::GetFullPath($RepositoryRoot)
$gitRoot = [IO.Path]::GetFullPath((& git -C $root rev-parse --show-toplevel).Trim())
if ($LASTEXITCODE -ne 0 -or $gitRoot -ne $root) {
    throw "RepositoryRoot must be the Forge checkout root: $root"
}
$branch = (& git -C $root branch --show-current).Trim()
if ($branch -ne "Rust") {
    throw "Rust bootstrap is allowed only on branch 'Rust'; current branch is '$branch'."
}

& rustup toolchain install 1.98.0 --profile minimal --component clippy --component rustfmt --component rust-src --target wasm32-unknown-unknown
if ($LASTEXITCODE -ne 0) {
    throw "Unable to install the pinned Rust toolchain."
}

if ($InstallTools) {
    $tools = @(
        @("cargo-deny", "0.20.2"),
        @("cargo-audit", "0.22.2"),
        @("trunk", "0.21.14"),
        @("tauri-cli", "2.11.4"),
        @("wasm-bindgen-cli", "0.2.127")
    )
    foreach ($tool in $tools) {
        & cargo install --locked $tool[0] --version $tool[1]
        if ($LASTEXITCODE -ne 0) {
            throw "Unable to install $($tool[0]) $($tool[1])."
        }
    }
}

Push-Location $root
try {
    & cargo fetch --locked
    if ($LASTEXITCODE -ne 0) {
        throw "cargo fetch --locked failed."
    }
} finally {
    Pop-Location
}

$rustc = (& rustc --version).Trim()
$cargo = (& cargo --version).Trim()
$targets = @(& rustup target list --installed)
if ($rustc -notmatch "^rustc 1\.98\.0 " -or "wasm32-unknown-unknown" -notin $targets) {
    throw "Pinned toolchain validation failed: $rustc; targets=$($targets -join ',')"
}
Write-Output "FORGE_RUST_BOOTSTRAP_PASS branch=$branch rustc='$rustc' cargo='$cargo' wasm_target=true"
