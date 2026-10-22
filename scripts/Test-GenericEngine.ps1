param(
    [string]$TargetDirectory = "E:\OpenRPG_Forge\.build\cargo-target",
    [string]$OutputDirectory = "",
    [switch]$PrepareBrowser
)
$ErrorActionPreference = "Stop"
$sourceRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $sourceRoot
$env:CARGO_TARGET_DIR = $TargetDirectory
$env:CARGO_INCREMENTAL = '0'
$env:CARGO_HOME = 'E:\OpenRPG_Forge\.build\cargo-home'
$env:TEMP = 'E:\OpenRPG_Forge\.build\tmp'
$env:TMP = $env:TEMP
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path 'F:/OpenRPG_Forge/Preuves-et-analyses/actives/generic-engine' ([DateTime]::UtcNow.ToString("yyyyMMdd-HHmmss"))
}
if (Test-Path -LiteralPath $OutputDirectory) { throw "Output already exists: $OutputDirectory" }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$results = [System.Collections.Generic.List[object]]::new()
function Invoke-Check([string]$Name, [string]$Program, [string[]]$Arguments) {
    $log = Join-Path $OutputDirectory ($Name + ".log")
    & $Program @Arguments *> $log
    $code = $LASTEXITCODE
    $results.Add(@{ name=$Name; program=$Program; arguments=$Arguments; exit_code=$code; log_sha256=(Get-FileHash -LiteralPath $log -Algorithm SHA256).Hash.ToLowerInvariant() })
    $results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory "checks.json") -Encoding utf8
    if ($code -ne 0) { Get-Content -LiteralPath $log -Tail 30; throw "$Name failed: $code" }
}
Invoke-Check "input-before" "python" @("scripts/Snapshot-WorkingTree.py", "--output", (Join-Path $OutputDirectory "inputs-before.json"))
Invoke-Check "format" "cargo" @("fmt", "--all", "--", "--check")
Invoke-Check "boundaries" "python" @("scripts/test_engine_boundaries.py")
Invoke-Check "engine-native-check" "cargo" @("check", "-p", "forge_engine", "--lib", "--locked", "--offline", "-j", "2")
Invoke-Check "engine-minimal-check" "cargo" @("clippy", "-p", "forge_engine", "--lib", "--no-default-features", "--locked", "--offline", "-j", "2", "--", "-D", "warnings")
Invoke-Check "engine-web-check" "cargo" @("check", "-p", "forge_engine", "--lib", "--target", "wasm32-unknown-unknown", "--no-default-features", "--features", "web", "--locked", "--offline", "-j", "2")
Invoke-Check "engine-dependency-tree" "cargo" @("tree", "-p", "forge_engine", "--edges", "normal", "--locked", "--offline")
Invoke-Check "native-clippy" "cargo" @("clippy", "--workspace", "--all-targets", "--locked", "--offline", "-j", "2", "--", "-D", "warnings")
Invoke-Check "studio-wasm-clippy" "cargo" @("clippy", "-p", "forge_studio", "--target", "wasm32-unknown-unknown", "--no-default-features", "--features", "web", "--locked", "--offline", "-j", "2", "--", "-D", "warnings")
Invoke-Check "workspace-tests" "cargo" @("test", "--workspace", "--lib", "--tests", "--locked", "--offline", "-j", "2", "--", "--test-threads=1")
Invoke-Check "native-optical-proof" "cargo" @("run", "-p", "forge_tidewater", "--example", "generic_scene_render_probe", "--features", "qualification", "--locked", "--offline", "-j", "2", "--", (Join-Path $OutputDirectory "native"))
if ($PrepareBrowser) {
    Invoke-Check "browser-optical-build" "cargo" @("build", "-p", "forge_tidewater", "--example", "generic_scene_webgpu_probe", "--target", "wasm32-unknown-unknown", "--no-default-features", "--features", "web,qualification", "--locked", "--offline", "-j", "2")
    $wasm = Join-Path $TargetDirectory "wasm32-unknown-unknown/debug/examples/generic_scene_webgpu_probe.wasm"
    Invoke-Check "browser-bindings" "wasm-bindgen" @("--target", "web", "--out-dir", (Join-Path $OutputDirectory "generic-optical-wasm"), $wasm)
    Copy-Item -LiteralPath "docs/evidence/architecture/generic-optical-webgpu.html" -Destination (Join-Path $OutputDirectory "index.html")
    Write-Host "Serve $OutputDirectory over localhost and open index.html. A browser build alone is not a WebGPU execution proof."
}
Invoke-Check "input-after" "python" @("scripts/Snapshot-WorkingTree.py", "--compare", (Join-Path $OutputDirectory "inputs-before.json"), "--output", (Join-Path $OutputDirectory "inputs-after.json"))
Write-Host "Results: $OutputDirectory. This validates the exercised capabilities, not global engine genericity or a release."
