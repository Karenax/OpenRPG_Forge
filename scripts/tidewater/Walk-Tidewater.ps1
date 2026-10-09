param([switch]$SkipBuild, [switch]$Smoke, [switch]$LongSmoke, [string]$CapturePath)
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$storage = Split-Path $repository -Parent
$integration = Join-Path $storage '.tidewater-integration'
$bundle = Join-Path $integration 'preview-scene-bundle-fish-stand-content-fish-011'
$procedural = Join-Path $integration 'debris-native-input-20261005-v1'
$scanned = Join-Path $integration 'debris-scanned-package-20261005-v1'
$program = Join-Path $integration 'debris-scan-instance-program-20261005-v1/scanned.wgsl'
$gameFish = Join-Path $integration 'game-fish-package-20261004-v1'
$fishSurface = Join-Path $integration 'fish-props-package-20261004-v2'
$pebbleProgram = Join-Path $integration 'pebble-program-20261006-v1'
$pins = @{
    (Join-Path $procedural 'debris-native.json') = '63e9abf5736fa043765366c78e7eea3688b62ce641b43c7a9db3bed34f528216'
    (Join-Path $scanned 'package.json') = 'f7faf1c052d59edae78420f22372daa2f00369c05f2bb60846e965bcd86c4ad4'
    $program = '3d54ba12b6546b79d220ab9912ec871237cd1b9a25d6043cc375f5cb1c9503c3'
    (Join-Path $gameFish 'package.json') = 'f26aceb2fdcee7f9e2261f90bd7e75fa7d66b672eb854dc66caa453c904c456d'
    (Join-Path $fishSurface 'package.json') = 'e578b44e93d18d79ec06c875b76dae005f71c17ecf1d0ff09aa3ecddd651330a'
    (Join-Path $pebbleProgram 'program.json') = '0f6ea25a3fdcc20371241db9729e2b58b39f7e8e29500f064da5ffc3a92b3201'
    (Join-Path $pebbleProgram 'pebbles.wgsl') = '6c9e1f3659f384fe58d33405713ae185ae334db1d1ca7d3e620f8d690def557b'
}
foreach ($path in $pins.Keys) {
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $pins[$path]) {
        throw "Les données Tidewater ne correspondent pas au paquet prévu : $path"
    }
}
$debrisVariables = @('FORGE_TIDEWATER_DEBRIS_ROOT','FORGE_TIDEWATER_SCANNED_ROOT','FORGE_TIDEWATER_SCANNED_PROGRAM','FORGE_TIDEWATER_PEBBLE_PROGRAM_ROOT','FORGE_TIDEWATER_STATIC_POSTFX','FORGE_TIDEWATER_GAME_FISH_ROOT','FORGE_TIDEWATER_FISH_PROP_ROOT')
$savedDebrisVariables = @{}
foreach ($name in $debrisVariables) {
    $savedDebrisVariables[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$env:CARGO_TARGET_DIR = Join-Path $storage '.build/cargo-target'
$env:CARGO_INCREMENTAL = '0'
$env:TEMP = Join-Path $storage '.build/cargo-temp'
$env:TMP = $env:TEMP
Remove-Item Env:RUSTFLAGS -ErrorAction SilentlyContinue
Push-Location $repository
try {
    $env:FORGE_TIDEWATER_DEBRIS_ROOT = $procedural
    $env:FORGE_TIDEWATER_SCANNED_ROOT = $scanned
    $env:FORGE_TIDEWATER_SCANNED_PROGRAM = $program
    $env:FORGE_TIDEWATER_GAME_FISH_ROOT = $gameFish
    $env:FORGE_TIDEWATER_FISH_PROP_ROOT = $fishSurface
    $env:FORGE_TIDEWATER_PEBBLE_PROGRAM_ROOT = $pebbleProgram
    Remove-Item Env:FORGE_TIDEWATER_STATIC_POSTFX -ErrorAction SilentlyContinue
    if (-not $SkipBuild) {
        & cargo +1.98.0 build -p forge_engine --example tidewater_plant_probe --locked --offline -j 1
        if ($LASTEXITCODE -ne 0) { throw 'La compilation Tidewater a échoué.' }
    }
    $executable = Join-Path $env:CARGO_TARGET_DIR 'debug/examples/tidewater_plant_probe.exe'
    $output = if ($CapturePath) { $CapturePath } else { Join-Path $integration ('walk-' + [Guid]::NewGuid().ToString('N') + '.png') }
    $walkMode = if ($LongSmoke) { '--walk-smoke-long' } elseif ($Smoke) { '--walk-smoke' } else { '--walk' }
    $launchArgs = @(
        (Join-Path $bundle 'scene'), $output,
        '--water-spray-clouds', (Join-Path $bundle 'ocean'),
        (Join-Path $bundle 'shore'), (Join-Path $bundle 'spray'), (Join-Path $bundle 'clouds'),
        '--workspace-vegetation', (Join-Path $bundle 'workspace'), 'e620dd19-ead9-5913-a6d6-0f93109148c0',
        '--building-package', (Join-Path $bundle 'buildings'),
        'a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6', $walkMode
    )
    & $executable @launchArgs
    if ($LASTEXITCODE -ne 0) { throw 'Le parcours Tidewater a échoué ; consulter les erreurs ci-dessus.' }
    if ($Smoke -or $LongSmoke) {
        $receiptPath = [IO.Path]::ChangeExtension($output, 'walk.json')
        if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
            throw 'Le test a été fermé avant son bilan : aucun résultat de parcours validé.'
        }
        $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        if ($receipt.pass -ne $true -or -not (Test-Path -LiteralPath $output -PathType Leaf)) {
            throw 'Le bilan ou la capture du parcours Tidewater est incomplet.'
        }
    }
} finally {
    foreach ($name in $debrisVariables) {
        [Environment]::SetEnvironmentVariable($name, $savedDebrisVariables[$name], 'Process')
    }
    Pop-Location
}
