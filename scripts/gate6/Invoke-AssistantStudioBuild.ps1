[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('frontend','desktop')][string]$Stage,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9][a-z0-9-]{0,79}$')][string]$LogName
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$workspace = Split-Path -Parent $repository
$buildRoot = Join-Path $workspace '.build'
if ((Get-Item -LiteralPath $buildRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Build cache must be physical on E:.' }
$frontend = Join-Path $buildRoot 'frontend/studio'
$evidence = 'F:/OpenRPG_Forge/Preuves-et-analyses/actives/gate6-ia-mcp'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$log = Join-Path $evidence ($LogName + '.log')
if (Test-Path -LiteralPath $log) { throw 'Existing evidence preserved: choose a new log name.' }
$keys = @('CARGO_TARGET_DIR','CARGO_HOME','CARGO_INCREMENTAL','TEMP','TMP','NO_COLOR','CARGO_NET_OFFLINE','FORGE_STUDIO_LOCAL_ASSETS','PATH')
$previous = @{}; foreach ($key in $keys) { $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
$started = [DateTime]::UtcNow.ToString('o'); $exitCode = $null
$wasmOpt = $null
try {
    $env:CARGO_TARGET_DIR = Join-Path $buildRoot 'cargo-target'; $env:CARGO_HOME = Join-Path $buildRoot 'cargo-home'
    $env:CARGO_INCREMENTAL = '0'; $env:TEMP = Join-Path $buildRoot 'tmp'; $env:TMP = $env:TEMP
    $env:NO_COLOR = 'true'; $env:CARGO_NET_OFFLINE = 'true'
    if ($Stage -eq 'frontend') {
        $wasmOptPath = Join-Path $buildRoot 'tools/binaryen-131-001/bin/wasm-opt.exe'
        $wasmOptHash = (Get-FileHash -LiteralPath $wasmOptPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($wasmOptHash -ne 'a3e857e565f18773e24cb3d81d00baa70cee9be24efe775e1960565fea5e65b3') {
            throw 'Shared wasm-opt differs from the inspected Binaryen 131 input.'
        }
        $wasmOpt = @{path=$wasmOptPath;sha256=$wasmOptHash;version=131}
        $env:PATH = (Split-Path -Parent $wasmOptPath) + [IO.Path]::PathSeparator + $env:PATH
    }
    # Use the same static-input hook configuration as the canonical Studio.
    # Isolate qualification documents, never the product/frontend per chat.
    Push-Location -LiteralPath (Join-Path $repository 'apps/forge_studio')
    try {
        if ($Stage -eq 'frontend') {
            & trunk build --release --config Trunk.toml --offline true --locked true --skip-version-check --dist $frontend *> $log
        } else {
            if (-not (Test-Path -LiteralPath (Join-Path $frontend 'forge_studio_bg.wasm'))) { throw 'Build the shared frontend first.' }
            $configPath = Join-Path (Get-Location).Path 'tauri.shared.conf.json'
            & cargo tauri build --debug --no-bundle --config $configPath -- --locked --offline *> $log
        }
        $exitCode = $LASTEXITCODE
    } finally { Pop-Location }
    @{schema='forge.gate6-studio-build.v1';stage=$Stage;started_utc=$started;finished_utc=[DateTime]::UtcNow.ToString('o');exit_code=$exitCode;repository=$repository;frontend=$frontend;target=$env:CARGO_TARGET_DIR;log=$log;wasm_opt=$wasmOpt;static_assets_policy='canonical hook configuration';private_assets_staged=$null;runtime_qualified=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath ($log+'.receipt.json') -Encoding utf8
    Get-Content -LiteralPath $log -Tail 12
} finally { foreach ($key in $keys) { [Environment]::SetEnvironmentVariable($key,$previous[$key],'Process') } }
exit $exitCode
