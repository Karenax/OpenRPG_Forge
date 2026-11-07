# Inert worker fixtures: shadows Cargo/wasm-bindgen functions; never compiles.
param([Parameter(Mandatory)][string]$ScratchRoot)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (Test-Path -LiteralPath $ScratchRoot) { throw 'Fresh scratch root required' }
$fixtureRoot = [IO.Path]::GetFullPath($ScratchRoot)
if (-not $fixtureRoot.StartsWith([IO.Path]::GetFullPath('E:/OpenRPG_Forge/.build/tmp').TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Worker fixture must stay under shared temporary storage'
}
# Check every existing ancestor before creating a fixture; never traverse a junction.
$ancestor = Split-Path -Parent $fixtureRoot
while ($ancestor) {
    $item = Get-Item -LiteralPath $ancestor -Force -ErrorAction Stop
    if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Worker fixture ancestor is not a physical directory'
    }
    $parent = Split-Path -Parent $ancestor
    if (-not $parent -or $parent -eq $ancestor) { break }
    $ancestor = $parent
}
$repo = Join-Path $fixtureRoot 'repo'
$paths = @('scripts','crates/forge_navigation/src','third_party/rerecast-0.4.0/src','apps/forge_studio','.cargo')
foreach ($name in $paths) { $null = New-Item -ItemType Directory -Path (Join-Path $repo $name) -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Build-NavigationWorker.ps1') -Destination (Join-Path $repo 'scripts/Build-NavigationWorker.ps1')
foreach ($name in @('Cargo.toml','Cargo.lock','crates/forge_navigation/Cargo.toml','crates/forge_navigation/build.rs',
        'crates/forge_navigation/src/lib.rs','third_party/rerecast-0.4.0/Cargo.toml','third_party/rerecast-0.4.0/src/lib.rs',
        'apps/forge_studio/forge-navigation-worker.js','.cargo/config.toml')) {
    'inert fixture' | Set-Content -LiteralPath (Join-Path $repo $name) -Encoding utf8
}
$global:MockWorkerMutate = $false
$global:MockWorkerSource = Join-Path $repo 'crates/forge_navigation/src/lib.rs'
function global:cargo {
    $global:MockCargoArguments = @($args)
    $profileDir = if ($args -contains '--profile') { 'forge-navigation-worker' } else { 'debug' }
    $dir = Join-Path $env:CARGO_TARGET_DIR "wasm32-unknown-unknown/$profileDir"
    $null = New-Item -ItemType Directory -Path $dir -Force
    'inert wasm' | Set-Content -LiteralPath (Join-Path $dir 'forge_navigation.wasm') -Encoding utf8
    if ($global:MockWorkerMutate) { 'changed' | Set-Content -LiteralPath $global:MockWorkerSource -Encoding utf8 }
    $global:LASTEXITCODE = 0
}
function global:wasm-bindgen {
    $global:MockBindgenArguments = @($args)
    $output = $args[[array]::IndexOf($args,'--out-dir') + 1]
    'inert js' | Set-Content -LiteralPath (Join-Path $output 'forge_navigation.js') -Encoding utf8
    'inert wasm bindings' | Set-Content -LiteralPath (Join-Path $output 'forge_navigation_bg.wasm') -Encoding utf8
    $global:LASTEXITCODE = 0
}
$worker = Join-Path $repo 'scripts/Build-NavigationWorker.ps1'
$target = Join-Path $fixtureRoot 'target'
$release = Join-Path $fixtureRoot 'release-out'
& $worker -Output $release -CargoTargetDirectory $target
$receipt = Get-Content -LiteralPath (Join-Path $release 'forge-navigation-worker-manifest.json') -Raw | ConvertFrom-Json
if ($receipt.version -ne 2 -or $receipt.cargo_profile -cne 'forge-navigation-worker' -or
    $receipt.profile_inherits -cne 'release' -or $receipt.effective_profile_overrides_verified -ne $false -or
    @($receipt.source_pins.path) -cnotcontains '.cargo/config.toml' -or
    $global:MockBindgenArguments[0] -cne (Join-Path $target 'wasm32-unknown-unknown/forge-navigation-worker/forge_navigation.wasm')) {
    throw 'Profiled worker fixture differs'
}
$expected = @('--config','profile.forge-navigation-worker.inherits="release"','build','-p','forge_navigation','--lib',
    '--no-default-features','--features','web','--target','wasm32-unknown-unknown','--locked','--offline','-j1',
    '--profile','forge-navigation-worker')
if (($global:MockCargoArguments -join "`n") -cne ($expected -join "`n")) { throw 'Wrong Cargo invocation' }
# Execute the actual reader function isolated from the rest of the developer tool.
$tokens = $null; $parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Build-StudioCandidate.ps1'), [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Studio candidate parser errors' }
$functionAst = $ast.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Test-NavigationWorkerHeader' }, $true)
if (-not $functionAst) { throw 'Studio candidate worker reader missing' }
. ([scriptblock]::Create($functionAst.Extent.Text))
if (-not (Test-NavigationWorkerHeader $receipt 'release')) { throw 'Real v2 reader rejected receipt' }
$legacy = [pscustomobject]@{schema='forge.navigation-worker-build';version=1;build_only=$true;runtime_verified=$false;source_unchanged_during_build=$true;configuration='release'}
if (-not (Test-NavigationWorkerHeader $legacy 'release')) { throw 'Real v2 reader rejected historical v1' }
$receipt.profile_inherits = 'dev'
if (Test-NavigationWorkerHeader $receipt 'release') { throw 'Real reader accepted altered inheritance' }
$debug = Join-Path $fixtureRoot 'debug-out'
& $worker -Output $debug -CargoTargetDirectory $target -Configuration debug
$debugReceipt = Get-Content -LiteralPath (Join-Path $debug 'forge-navigation-worker-manifest.json') -Raw | ConvertFrom-Json
if ($debugReceipt.version -ne 1 -or $debugReceipt.configuration -cne 'debug' -or $global:MockCargoArguments -contains '--profile') {
    throw 'Legacy debug worker behavior changed'
}
$global:MockWorkerMutate = $true
try {
    & $worker -Output (Join-Path $fixtureRoot 'tampered-out') -CargoTargetDirectory $target
    throw 'Mutated worker source was accepted'
} catch {
    if ($_.Exception.Message -notlike '*Navigation worker source changed during compilation*') { throw }
}
if (Test-Path -LiteralPath (Join-Path $fixtureRoot 'tampered-out/forge-navigation-worker-manifest.json')) { throw 'Receipt published after source change' }
# Run the real candidate-input fixture in this inert repository, with a mocked Git branch.
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Build-StudioCandidate.ps1') -Destination (Join-Path $repo 'scripts/Build-StudioCandidate.ps1')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Test-StudioCandidateKit.ps1') -Destination (Join-Path $repo 'scripts/Test-StudioCandidateKit.ps1')
function global:git { $global:LASTEXITCODE = 0; 'Rust' }
& (Join-Path $repo 'scripts/Test-StudioCandidateKit.ps1') -Output (Join-Path $fixtureRoot 'candidate-inputs')
'NAVIGATION_WORKER_PROFILE_FIXTURES_PASS: mocked commands only; no build'
