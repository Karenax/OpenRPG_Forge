#Requires -Version 7.2
# Builds only the pure navigation worker; no Studio, GPU, launch or gate verdict.
[CmdletBinding()]
param(
    [string]$Output = $env:TRUNK_STAGING_DIR,
    [string]$CargoTargetDirectory = $env:CARGO_TARGET_DIR,
    [ValidateSet('debug','release')][string]$Configuration = 'release',
    [switch]$StageLocalAssets
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path -Parent $PSScriptRoot
if (-not $CargoTargetDirectory) { throw 'Supply CargoTargetDirectory or set CARGO_TARGET_DIR explicitly.' }
if (-not $Output) { throw 'Supply Output or run as a Trunk hook with TRUNK_STAGING_DIR.' }
$targetRoot = [IO.Path]::GetFullPath($CargoTargetDirectory)
$outputRoot = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
function Get-NavigationSourceInputs {
    $sourceInputs = @('Cargo.toml','Cargo.lock','crates/forge_navigation/Cargo.toml','crates/forge_navigation/build.rs',
        'third_party/rerecast-0.4.0/Cargo.toml','apps/forge_studio/forge-navigation-worker.js')
    foreach ($sourceDirectory in @('crates/forge_navigation/src','third_party/rerecast-0.4.0/src')) {
        $sourceInputs += @(Get-ChildItem -LiteralPath (Join-Path $repository $sourceDirectory) -Recurse -File | ForEach-Object {
            [IO.Path]::GetRelativePath($repository,$_.FullName).Replace('\','/')
        })
    }
    return @($sourceInputs | Sort-Object -Unique)
}
$inputs = @(Get-NavigationSourceInputs)
$pins = @($inputs | ForEach-Object {
    @{ path=$_; sha256=(Get-FileHash -LiteralPath (Join-Path $repository $_) -Algorithm SHA256).Hash.ToLowerInvariant() }
})
$previousTarget = $env:CARGO_TARGET_DIR
try {
    $env:CARGO_TARGET_DIR = $targetRoot
    Push-Location $repository
    try {
        $arguments = @('build','-p','forge_navigation','--lib','--no-default-features','--features','web',
            '--target','wasm32-unknown-unknown','--locked','--offline','-j1')
        if ($Configuration -eq 'release') { $arguments += '--release' }
        & cargo @arguments
        if ($LASTEXITCODE -ne 0) { throw 'Navigation worker compilation failed.' }
        $wasm = Join-Path $targetRoot "wasm32-unknown-unknown/$Configuration/forge_navigation.wasm"
        & wasm-bindgen $wasm --target web --out-dir $outputRoot --out-name forge_navigation
        if ($LASTEXITCODE -ne 0) { throw 'Navigation worker bindings failed.' }
    } finally { Pop-Location }
    Copy-Item -LiteralPath (Join-Path $repository 'apps/forge_studio/forge-navigation-worker.js') -Destination $outputRoot -Force
    if (@(Compare-Object -CaseSensitive -ReferenceObject $inputs -DifferenceObject @(Get-NavigationSourceInputs)).Count -ne 0) {
        throw 'Navigation worker source inventory changed during compilation.'
    }
    foreach ($pin in $pins) {
        if ((Get-FileHash -LiteralPath (Join-Path $repository $pin.path) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $pin.sha256) {
            throw "Navigation worker source changed during compilation: $($pin.path)"
        }
    }
    $outputs = @('forge_navigation.js','forge_navigation_bg.wasm','forge-navigation-worker.js')
    @{ schema='forge.navigation-worker-build'; version=1; build_only=$true; runtime_verified=$false; source_unchanged_during_build=$true;
        configuration=$Configuration; recorded_at_utc=[DateTime]::UtcNow.ToString('o'); source_pins=$pins;
        artifacts=@($outputs | ForEach-Object {
            @{ path=$_; sha256=(Get-FileHash -LiteralPath (Join-Path $outputRoot $_) -Algorithm SHA256).Hash.ToLowerInvariant() }
        }) } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputRoot 'forge-navigation-worker-manifest.json') -Encoding utf8
} finally { $env:CARGO_TARGET_DIR = $previousTarget }
if ($StageLocalAssets) {
    & (Join-Path $PSScriptRoot 'Stage-StudioLocalAssets.ps1') -Output $outputRoot
}
