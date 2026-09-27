# Reuses the single review candidate and shared cache; never creates build histories.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$review = Join-Path $repository 'output/studio-gold-20260924'
$candidate = Join-Path $review 'candidate'
$executable = Join-Path $candidate 'forge-studio-candidate.exe'
if (Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $executable }) {
    throw 'Review candidate is running. Close it normally before rebuilding.'
}
$config = Join-Path $candidate 'tauri-candidate.conf.json'
if (-not (Test-Path -LiteralPath $config -PathType Leaf)) { throw 'Existing review configuration missing.' }
$saved = @{}
foreach ($key in @('CARGO_TARGET_DIR','CARGO_INCREMENTAL','RUSTFLAGS','TEMP','TMP','CARGO_NET_OFFLINE')) {
    $saved[$key] = [Environment]::GetEnvironmentVariable($key,'Process')
}
try {
    $env:CARGO_TARGET_DIR = 'E:\OpenRPG_Forge\.build\cargo-target'
    $env:CARGO_INCREMENTAL = '0'
    $env:RUSTFLAGS = '-C debuginfo=0'
    $env:TEMP = 'E:\OpenRPG_Forge\.build\cargo-temp'
    $env:TMP = $env:TEMP
    $env:CARGO_NET_OFFLINE = 'true'
    $frontend = Join-Path $review 'web'
    $wasm = Join-Path $env:CARGO_TARGET_DIR 'wasm32-unknown-unknown/release/forge_studio_frontend.wasm'
    & cargo build -p forge_studio --lib --no-default-features --features web --target wasm32-unknown-unknown --release --locked --offline
    if ($LASTEXITCODE -ne 0) { throw 'Studio Web frontend rebuild failed' }
    & wasm-bindgen $wasm --target web --out-dir $frontend --out-name forge_studio
    if ($LASTEXITCODE -ne 0) { throw 'wasm-bindgen failed' }
    Copy-Item -LiteralPath (Join-Path $repository 'apps/forge_studio/styles.css') -Destination (Join-Path $frontend 'styles.css')
    foreach ($file in Get-ChildItem -LiteralPath $frontend -File) {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $candidate ('web/' + $file.Name))
    }
    # Mark previous evidence stale before starting the native rebuild.
    $proofPath = Join-Path $candidate 'build-proof.json'
    @{ status='REBUILD_IN_PROGRESS'; runtime_verified=$false; gate_qualified=$false } |
        ConvertTo-Json | Set-Content -LiteralPath $proofPath -Encoding utf8
    Push-Location (Join-Path $repository 'apps/forge_studio')
    try {
        & cargo tauri build --debug --no-bundle --config $config -- --locked
        if ($LASTEXITCODE -ne 0) { throw 'Tauri candidate rebuild failed' }
    } finally { Pop-Location }
    Copy-Item -LiteralPath (Join-Path $env:CARGO_TARGET_DIR 'debug/forge-studio.exe') -Destination $executable
    $web = Join-Path $candidate 'web'
    $webRoot = (Resolve-Path -LiteralPath $web).ProviderPath.TrimEnd('\')
    $pins = @(Get-ChildItem -LiteralPath $web -Recurse -File | Sort-Object FullName | ForEach-Object {
        @{ path=$_.FullName.Substring($webRoot.Length + 1); sha256=(Get-FileHash -LiteralPath $_.FullName).Hash }
    })
    @{ status='BUILD_ONLY'; recorded_at_utc=[DateTime]::UtcNow.ToString('o');
        executable_sha256=(Get-FileHash -LiteralPath $executable).Hash;
        frontend_pins=$pins; build_cache_directory=$env:CARGO_TARGET_DIR;
        runtime_verified=$false; gate_qualified=$false } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $proofPath -Encoding utf8
} finally {
    foreach ($key in $saved.Keys) { [Environment]::SetEnvironmentVariable($key,$saved[$key],'Process') }
}
