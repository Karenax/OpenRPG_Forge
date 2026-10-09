param(
    [Parameter(Mandatory)][string]$ExpectedBinarySha256,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$DistributionRoot,
    [string]$BinaryPath,
    [switch]$WithPebbles,
    [switch]$WithSourceFish
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Assert-SourceFishProof.ps1')
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$storage = Split-Path $repository -Parent
$integration = Join-Path $storage '.tidewater-integration'
$binary = Join-Path $storage '.build/cargo-target/debug/examples/tidewater_plant_probe.exe'
if ($DistributionRoot -and $BinaryPath) { throw 'Choisir DistributionRoot ou BinaryPath, pas les deux.' }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Un dossier de preuve neuf est obligatoire.' }
$bundle = Join-Path $integration 'preview-scene-bundle-fish-stand-content-fish-011'
$procedural = Join-Path $integration 'debris-native-input-20261005-v1'
$scanned = Join-Path $integration 'debris-scanned-package-20261005-v1'
$program = Join-Path $integration 'debris-scan-instance-program-20261005-v1/scanned.wgsl'
$pebbleProgram = Join-Path $integration 'pebble-program-20261006-v1'
$gameFish = Join-Path $integration 'game-fish-package-20261004-v1'
$fishSurface = Join-Path $integration 'fish-props-package-20261004-v2'
if ($DistributionRoot) {
    $DistributionRoot = (Resolve-Path -LiteralPath $DistributionRoot).Path
    $binary = Join-Path $DistributionRoot 'runtime/tidewater.exe'
    $bundle = Join-Path $DistributionRoot 'data'
    $procedural = Join-Path $DistributionRoot 'content/debris/procedural'
    $scanned = Join-Path $DistributionRoot 'content/debris/scanned'
    $program = Join-Path $DistributionRoot 'content/debris/scanned.wgsl'
    $pebbleProgram = Join-Path $DistributionRoot 'content/debris/pebbles'
    $gameFish = Join-Path $DistributionRoot 'content/fish/game'
    $fishSurface = Join-Path $DistributionRoot 'content/fish/surface'
}
if ($BinaryPath) {
    $binary = (Resolve-Path -LiteralPath $BinaryPath).Path
    if (-not (Test-Path -LiteralPath $binary -PathType Leaf)) { throw 'Le binaire choisi doit être un fichier.' }
}
$actualBinary = (Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualBinary -ne $ExpectedBinarySha256.ToLowerInvariant()) { throw 'Le binaire sélectionné ne correspond pas au build choisi.' }
$pins = @{
    (Join-Path $procedural 'debris-native.json') = '63e9abf5736fa043765366c78e7eea3688b62ce641b43c7a9db3bed34f528216'
    (Join-Path $scanned 'package.json') = 'f7faf1c052d59edae78420f22372daa2f00369c05f2bb60846e965bcd86c4ad4'
    $program = '3d54ba12b6546b79d220ab9912ec871237cd1b9a25d6043cc375f5cb1c9503c3'
}
if ($WithPebbles) {
    $pins[(Join-Path $pebbleProgram 'program.json')] = '0f6ea25a3fdcc20371241db9729e2b58b39f7e8e29500f064da5ffc3a92b3201'
    $pins[(Join-Path $pebbleProgram 'pebbles.wgsl')] = '6c9e1f3659f384fe58d33405713ae185ae334db1d1ca7d3e620f8d690def557b'
}
if ($WithSourceFish) {
    $pins[(Join-Path $gameFish 'package.json')] = 'f26aceb2fdcee7f9e2261f90bd7e75fa7d66b672eb854dc66caa453c904c456d'
    $pins[(Join-Path $fishSurface 'package.json')] = 'e578b44e93d18d79ec06c875b76dae005f71c17ecf1d0ff09aa3ecddd651330a'
}
foreach ($path in $pins.Keys) {
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $pins[$path]) { throw "Révision d'entrée différente : $path" }
}
$variables = @('FORGE_TIDEWATER_DEBRIS_ROOT','FORGE_TIDEWATER_SCANNED_ROOT','FORGE_TIDEWATER_SCANNED_PROGRAM','FORGE_TIDEWATER_PEBBLE_PROGRAM_ROOT','FORGE_TIDEWATER_STATIC_POSTFX','FORGE_TIDEWATER_GAME_FISH_ROOT','FORGE_TIDEWATER_FISH_PROP_ROOT','RUST_LOG')
# A previous diagnostic must not replace this walk with a static/temporal proof
# or silently disable an effect. Restore every inherited setting in finally.
$variables = @($variables + @([Environment]::GetEnvironmentVariables('Process').Keys |
    Where-Object { $_ -like 'FORGE_TIDEWATER_*' }) | Sort-Object -Unique)
$saved = @{}
foreach ($name in $variables) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$image = Join-Path $OutputDirectory 'island.png'
$log = Join-Path $OutputDirectory 'runtime.log'
$receipt = Join-Path $OutputDirectory 'execution.json'
$code = $null
$locationChanged = $false
try {
    foreach ($name in $variables) {
        if ($name -like 'FORGE_TIDEWATER_*') {
            [Environment]::SetEnvironmentVariable($name, $null, 'Process')
        }
    }
    if ($DistributionRoot) {
        Push-Location -LiteralPath $DistributionRoot
        $locationChanged = $true
    }
    $env:FORGE_TIDEWATER_DEBRIS_ROOT = $procedural
    $env:FORGE_TIDEWATER_SCANNED_ROOT = $scanned
    $env:FORGE_TIDEWATER_SCANNED_PROGRAM = $program
    if ($WithSourceFish) {
        $env:FORGE_TIDEWATER_GAME_FISH_ROOT = $gameFish
        $env:FORGE_TIDEWATER_FISH_PROP_ROOT = $fishSurface
    }
    if ($WithPebbles) {
        $env:FORGE_TIDEWATER_PEBBLE_PROGRAM_ROOT = $pebbleProgram
    } else {
        Remove-Item Env:FORGE_TIDEWATER_PEBBLE_PROGRAM_ROOT -ErrorAction SilentlyContinue
    }
    Remove-Item Env:FORGE_TIDEWATER_STATIC_POSTFX -ErrorAction SilentlyContinue
    $env:RUST_LOG = 'info'
    $arguments = @((Join-Path $bundle 'scene'),$image,'--water-spray-clouds',
        (Join-Path $bundle 'ocean'),(Join-Path $bundle 'shore'),(Join-Path $bundle 'spray'),(Join-Path $bundle 'clouds'),
        '--workspace-vegetation',(Join-Path $bundle 'workspace'),'e620dd19-ead9-5913-a6d6-0f93109148c0',
        '--building-package',(Join-Path $bundle 'buildings'),'a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6','--walk-smoke')
    & $binary @arguments 2>&1 | Tee-Object -FilePath $log
    $code = $LASTEXITCODE
    $walkPath = [IO.Path]::ChangeExtension($image, 'walk.json')
    $walk = if (Test-Path -LiteralPath $walkPath) { Get-Content -LiteralPath $walkPath -Raw | ConvertFrom-Json } else { $null }
    $inputsUnchanged = $true
    foreach ($path in $pins.Keys) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $pins[$path]) {
            $inputsUnchanged = $false
        }
    }
    $binaryUnchanged = (Test-Path -LiteralPath $binary -PathType Leaf) -and
        (Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash.ToLowerInvariant() -eq $actualBinary
    $passed = $code -eq 0 -and $null -ne $walk -and $walk.pass -eq $true -and
        (Test-Path -LiteralPath $image -PathType Leaf) -and $inputsUnchanged -and $binaryUnchanged
    $pebbleStaged = Select-String -LiteralPath $log -SimpleMatch 'Native Pebble resources staged' -Quiet
    if ($WithPebbles -and -not $pebbleStaged) { $passed = $false }
    $sourceFishPrepared = $false
    $sourceFishError = $null
    if ($null -ne $walk -and $walk.source_fish_requested -eq $true) {
        try {
            Assert-TidewaterSourceFishProof -WalkProof $walk
            $sourceFishPrepared = $true
        } catch { $sourceFishError = $_.Exception.Message }
    }
    if ($WithSourceFish -and -not $sourceFishPrepared) { $passed = $false }
    [ordered]@{schema='forge.tidewater.native-debris-walk-execution';version=1;binary_sha256=$actualBinary;
        selected_binary_path=$binary;
        source_fish_requested=[bool]$WithSourceFish;source_fish_preparation_pass=$sourceFishPrepared;
        source_fish_preparation_error=$sourceFishError;
        input_pins=$pins;inputs_unchanged=$inputsUnchanged;binary_unchanged=$binaryUnchanged;
        cleared_inherited_tidewater_variables=@($variables | Where-Object { $_ -like 'FORGE_TIDEWATER_*' -and $null -ne $saved[$_] });
        distribution_root=$DistributionRoot;arguments=$arguments;exit_code=$code;walk_receipt_present=($null -ne $walk);
        walk_receipt_sha256=$(if ($null -ne $walk) { (Get-FileHash -LiteralPath $walkPath -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null });
        image_sha256=$(if (Test-Path -LiteralPath $image -PathType Leaf) { (Get-FileHash -LiteralPath $image -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null });
        pebbles_requested=[bool]$WithPebbles;pebble_resources_staged=[bool]$pebbleStaged;
        pass=$passed;limits=@('bounded controller smoke only','Pebble resource staging and readiness do not independently prove visible Pebble pixels','no full map coverage or source visual parity','no Studio editing qualification')
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $receipt -Encoding utf8
    if (-not $passed) { throw 'Parcours incomplet ou échoué ; les preuves sont conservées.' }
} finally {
    if ($locationChanged) { Pop-Location }
    foreach ($name in $variables) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
}
