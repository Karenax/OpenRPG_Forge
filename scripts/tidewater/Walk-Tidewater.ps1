param([switch]$SkipBuild, [switch]$Smoke, [switch]$LongSmoke, [string]$CapturePath)
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$storage = Split-Path $repository -Parent
$integration = Join-Path $storage '.tidewater-integration'
$bundle = Join-Path $integration 'preview-scene-bundle-clear-004'
$env:CARGO_TARGET_DIR = Join-Path $storage '.build/cargo-target'
$env:CARGO_INCREMENTAL = '0'
$env:TEMP = Join-Path $storage '.build/cargo-temp'
$env:TMP = $env:TEMP
Remove-Item Env:RUSTFLAGS -ErrorAction SilentlyContinue
Push-Location $repository
try {
    if (-not $SkipBuild) {
        & cargo build -p forge_engine --example tidewater_plant_probe --locked --offline -j 1
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
    Pop-Location
}
