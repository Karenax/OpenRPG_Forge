[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$LauncherExe,
    [Parameter(Mandatory)][string]$RuntimeExe,
    [Parameter(Mandatory)][string]$BundleRoot,
    [Parameter(Mandatory)][string]$LauncherSource,
    [Parameter(Mandatory)][string]$ProceduralDebrisRoot,
    [Parameter(Mandatory)][string]$ScannedDebrisRoot,
    [Parameter(Mandatory)][string]$ScannedDebrisProgram,
    [Parameter(Mandatory)][string]$PebbleProgramRoot,
    [Parameter(Mandatory)][string]$NativePebbleProof,
    [Parameter(Mandatory)][string]$GameFishRoot,
    [Parameter(Mandatory)][string]$FishSurfaceRoot,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$PythonExecutable = 'python',
    [switch]$SkipZip
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$python = (Get-Command $PythonExecutable -CommandType Application -ErrorAction Stop).Source

$launcher = [IO.Path]::GetFullPath($LauncherExe)
$runtime = [IO.Path]::GetFullPath($RuntimeExe)
$bundle = [IO.Path]::GetFullPath($BundleRoot)
$launcherSource = [IO.Path]::GetFullPath($LauncherSource)
$proceduralDebris = [IO.Path]::GetFullPath($ProceduralDebrisRoot)
$scannedDebris = [IO.Path]::GetFullPath($ScannedDebrisRoot)
$scannedProgram = [IO.Path]::GetFullPath($ScannedDebrisProgram)
$pebbleProgram = [IO.Path]::GetFullPath($PebbleProgramRoot)
$pebbleProofPath = [IO.Path]::GetFullPath($NativePebbleProof)
$gameFish = [IO.Path]::GetFullPath($GameFishRoot)
$fishSurface = [IO.Path]::GetFullPath($FishSurfaceRoot)
$output = [IO.Path]::GetFullPath($OutputDirectory)
$partial = "$output.partial"
$zip = "$output.zip"
$partialZip = "$zip.partial"

foreach ($file in @($launcher, $runtime)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Executable absent : $file" }
}
$pebbleProof = Get-Content -LiteralPath $pebbleProofPath -Raw | ConvertFrom-Json
$runtimeHash = (Get-FileHash -LiteralPath $runtime -Algorithm SHA256).Hash.ToLowerInvariant()
foreach ($field in @('schema', 'version', 'binary_sha256', 'pass', 'exit_code', 'walk_receipt_present', 'pebbles_requested', 'pebble_resources_staged', 'inputs_unchanged', 'binary_unchanged', 'walk_receipt_sha256', 'image_sha256')) {
    if ($pebbleProof.PSObject.Properties.Name -notcontains $field) {
        throw "Preuve native des cailloux incomplète : $field"
    }
}
if ($pebbleProof.schema -ne 'forge.tidewater.native-debris-walk-execution' -or
    $pebbleProof.version -ne 1 -or $pebbleProof.binary_sha256 -ne $runtimeHash -or
    $pebbleProof.pass -ne $true -or $pebbleProof.exit_code -ne 0 -or
    $pebbleProof.walk_receipt_present -ne $true -or $pebbleProof.pebbles_requested -ne $true -or
    $pebbleProof.pebble_resources_staged -ne $true -or
    $pebbleProof.inputs_unchanged -ne $true -or $pebbleProof.binary_unchanged -ne $true) {
    throw 'Le runtime doit passer la promenade avec cailloux sur ce binaire exact avant packaging.'
}
$proofDirectory = Split-Path -Parent $pebbleProofPath
foreach ($artifact in @(
    @{name='island.walk.json'; hash=$pebbleProof.walk_receipt_sha256},
    @{name='island.png'; hash=$pebbleProof.image_sha256}
)) {
    $artifactPath = Join-Path $proofDirectory $artifact.name
    if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf) -or
        (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $artifact.hash) {
        throw "Artefact de la promenade absent ou modifié : $($artifact.name)"
    }
}
$walkProof = Get-Content -LiteralPath (Join-Path $proofDirectory 'island.walk.json') -Raw | ConvertFrom-Json
. (Join-Path $PSScriptRoot 'Assert-SourceFishProof.ps1')
Assert-TidewaterSourceFishProof -WalkProof $walkProof
$launcherCheck = & $launcher --verify-distributable
if ($LASTEXITCODE -ne 0 -or $launcherCheck -ne 'FORGE_LAUNCHER_AUBE_FRONTEND_READY') {
    throw "Le lanceur ne contient pas L'Aube des royaumes : reconstruire avec cargo tauri build --no-bundle --features production."
}
# Older launchers recognize the first flag and return their old marker instead
# of opening a GUI for an unknown standalone flag.
$debrisCheck = & $launcher --verify-distributable --debris-runtime
if ($LASTEXITCODE -ne 0 -or $debrisCheck -ne 'FORGE_LAUNCHER_DEBRIS_RUNTIME_V1') {
    throw 'Le lanceur ne raccorde pas les débris distribuables : reconstruire le lanceur actuel.'
}
$pebbleCheck = & $launcher --verify-distributable --pebble-runtime
if ($LASTEXITCODE -ne 0 -or $pebbleCheck -ne 'FORGE_LAUNCHER_PEBBLE_RUNTIME_V1') {
    throw 'Le lanceur ne raccorde pas les cailloux distribuables : reconstruire le lanceur actuel.'
}
$debrisPins = @{
    (Join-Path $gameFish 'package.json') = 'f26aceb2fdcee7f9e2261f90bd7e75fa7d66b672eb854dc66caa453c904c456d'
    (Join-Path $fishSurface 'package.json') = 'e578b44e93d18d79ec06c875b76dae005f71c17ecf1d0ff09aa3ecddd651330a'
    (Join-Path $proceduralDebris 'debris-native.json') = '63e9abf5736fa043765366c78e7eea3688b62ce641b43c7a9db3bed34f528216'
    (Join-Path $scannedDebris 'package.json') = 'f7faf1c052d59edae78420f22372daa2f00369c05f2bb60846e965bcd86c4ad4'
    $scannedProgram = '3d54ba12b6546b79d220ab9912ec871237cd1b9a25d6043cc375f5cb1c9503c3'
    (Join-Path $pebbleProgram 'program.json') = '0f6ea25a3fdcc20371241db9729e2b58b39f7e8e29500f064da5ffc3a92b3201'
    (Join-Path $pebbleProgram 'pebbles.wgsl') = '6c9e1f3659f384fe58d33405713ae185ae334db1d1ca7d3e620f8d690def557b'
}
foreach ($path in $debrisPins.Keys) {
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $debrisPins[$path]) {
        throw "Révision des débris différente : $path"
    }
}
$fishCheck = & $launcher --verify-distributable --source-fish-runtime
if ($LASTEXITCODE -ne 0 -or $fishCheck -ne 'FORGE_LAUNCHER_SOURCE_FISH_RUNTIME_V1') {
    throw 'Le lanceur ne raccorde pas les poissons source distribuables : reconstruire le lanceur actuel.'
}
if (-not (Test-Path -LiteralPath $bundle -PathType Container)) { throw "Données absentes : $bundle" }
if (-not (Test-Path -LiteralPath $launcherSource -PathType Container)) { throw "Source du lanceur absente : $launcherSource" }
foreach ($marker in @(
    'scene/fixture.json', 'ocean/ocean.json', 'shore/shore-sim.json',
    'spray/spray.json', 'clouds/environment.json',
    'workspace/e620dd19-ead9-5913-a6d6-0f93109148c0',
    'buildings', 'scene/LICENSE-Tidewater.txt', 'scene/CREDITS-Tidewater.md'
)) {
    if (-not (Test-Path -LiteralPath (Join-Path $bundle $marker))) { throw "Données incomplètes : $marker" }
}
$cloudEnvironmentPath = Join-Path $bundle 'clouds/environment.json'
$sceneFixturePath = Join-Path $bundle 'scene/fixture.json'
$cloudEnvironment = Get-Content -LiteralPath $cloudEnvironmentPath -Raw | ConvertFrom-Json
$sceneFixture = Get-Content -LiteralPath $sceneFixturePath -Raw | ConvertFrom-Json
$cloudHash = (Get-FileHash -LiteralPath $cloudEnvironmentPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($sceneFixture.cloud_acquisition_sha256 -ne $cloudHash -or
    (($cloudEnvironment.PSObject.Properties.Name -contains 'cloud_coverage') -and [double]$cloudEnvironment.cloud_coverage -gt 0.6)) {
    throw 'La météo du paquet ne correspond pas à la scène Tidewater de référence.'
}
foreach ($candidate in @($output, $partial, $zip, $partialZip)) {
    if (Test-Path -LiteralPath $candidate) { throw "Destination déjà présente : $candidate" }
}

$parent = Split-Path -Parent $output
[IO.Directory]::CreateDirectory($parent) | Out-Null
if (-not $partial.StartsWith($parent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'La destination temporaire sort du dossier prévu.'
}

[IO.Directory]::CreateDirectory($partial) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $partial 'runtime')) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $partial 'licenses')) | Out-Null
Copy-Item -LiteralPath $launcher -Destination (Join-Path $partial 'OpenRPG-Forge-Tidewater.exe')
Copy-Item -LiteralPath $runtime -Destination (Join-Path $partial 'runtime/tidewater.exe')
Copy-Item -LiteralPath $bundle -Destination (Join-Path $partial 'data') -Recurse -Force
$debrisDestination = Join-Path $partial 'content/debris'
[IO.Directory]::CreateDirectory($debrisDestination) | Out-Null
Copy-Item -LiteralPath $proceduralDebris -Destination (Join-Path $debrisDestination 'procedural') -Recurse
Copy-Item -LiteralPath $scannedDebris -Destination (Join-Path $debrisDestination 'scanned') -Recurse
Copy-Item -LiteralPath $scannedProgram -Destination (Join-Path $debrisDestination 'scanned.wgsl')
$fishDestination = Join-Path $partial 'content/fish'
[IO.Directory]::CreateDirectory($fishDestination) | Out-Null
foreach ($fishInput in @(@{root=$gameFish; folder='game'}, @{root=$fishSurface; folder='surface'})) {
    $sourceRoot = $fishInput.root
    $destinationRoot = Join-Path $fishDestination $fishInput.folder
    Copy-Item -LiteralPath $sourceRoot -Destination $destinationRoot -Recurse
    $sourcePayloads = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Force)
    $copiedPayloads = @(Get-ChildItem -LiteralPath $destinationRoot -Recurse -File -Force)
    if ($sourcePayloads.Count -ne $copiedPayloads.Count) { throw "Copie des poissons source incomplète : $($fishInput.folder)" }
    foreach ($payload in $sourcePayloads) {
        $relative = $payload.FullName.Substring($sourceRoot.Length).TrimStart('\', '/')
        $copy = Join-Path $destinationRoot $relative
        if ((Get-FileHash -LiteralPath $payload.FullName -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash) { throw "Copie des poissons source modifiée : $relative" }
    }
}
[IO.Directory]::CreateDirectory((Join-Path $debrisDestination 'pebbles')) | Out-Null
foreach ($name in @('program.json', 'pebbles.wgsl')) {
    $source = Join-Path $pebbleProgram $name
    $destination = Join-Path (Join-Path $debrisDestination 'pebbles') $name
    Copy-Item -LiteralPath $source -Destination $destination
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() -ne $debrisPins[$source]) {
        throw "Copie du programme des cailloux différente : $name"
    }
}
# Keep these inputs outside data/: the scene manifest attests only its own bundle.
# The final distribution manifest below includes every debris payload and notice.
foreach ($sourceRoot in @($proceduralDebris, $scannedDebris)) {
    $folder = if ($sourceRoot -eq $proceduralDebris) { 'procedural' } else { 'scanned' }
    $sourcePayloads = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Force)
    $copiedPayloads = @(Get-ChildItem -LiteralPath (Join-Path $debrisDestination $folder) -Recurse -File -Force)
    if ($sourcePayloads.Count -ne $copiedPayloads.Count) { throw "Copie des débris incomplète : $folder" }
    foreach ($payload in $sourcePayloads) {
        $relative = $payload.FullName.Substring($sourceRoot.Length).TrimStart('\', '/')
        $copy = Join-Path (Join-Path $debrisDestination $folder) $relative
        if ((Get-FileHash -LiteralPath $payload.FullName -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash) { throw "Copie des débris modifiée : $relative" }
    }
}
if ((Get-FileHash -LiteralPath (Join-Path $debrisDestination 'scanned.wgsl') -Algorithm SHA256).Hash -ne
    (Get-FileHash -LiteralPath $scannedProgram -Algorithm SHA256).Hash) { throw 'Copie du shader scanné modifiée.' }
Copy-Item -LiteralPath (Join-Path $bundle 'scene/LICENSE-Tidewater.txt') -Destination (Join-Path $partial 'licenses/Tidewater-MIT.txt')
Copy-Item -LiteralPath (Join-Path $bundle 'scene/CREDITS-Tidewater.md') -Destination (Join-Path $partial 'licenses/Tidewater-CREDITS.md')
$forgeManifest = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Cargo.toml'))
$launcherManifest = Join-Path $launcherSource 'Cargo.toml'
& $python (Join-Path $PSScriptRoot 'collect_rust_notices.py') $forgeManifest $launcherManifest (Join-Path $partial 'licenses/rust')
if ($LASTEXITCODE -ne 0) { throw 'La collecte des licences Rust a échoué.' }

$readme = @'
OpenRPG Forge — promenade de l'île Tidewater (Windows x64)

Extraire TOUTE l'archive ZIP dans un dossier, puis double-cliquer
OpenRPG-Forge-Tidewater.exe. Dans L’Aube des royaumes, ouvrir « Jouer »
puis cliquer « Lancer l’île Tidewater ».

Clic dans le jeu : capturer la souris. WASD : marcher/nager. Maj : accélérer.
Espace : sauter/monter. C ou Ctrl : plonger. V : changer de vue.
H ou F10 : réglages graphiques (adaptation automatique, occlusion ambiante,
exposition, FXAA, bloom, saturation, contraste, netteté, vignette, grain photo,
brume marine, lumière rebond, flare solaire, flou de mouvement et TAA
expérimental désactivé par défaut, rayons sous-marins, surfaces immergées,
caustiques, réfraction des ombres et capture de réfraction). Dans ce menu,
la molette parcourt
les réglages et R rétablit
les valeurs par défaut. Les réglages sont sauvegardés dans
%LOCALAPPDATA%\OpenRPG Forge\Tidewater\graphics.json.
Échap : libérer la souris. Fermer la fenêtre du jeu pour revenir au lanceur.

Ce paquet est une démonstration de promenade de l'île en cours d'intégration,
pas un RPG terminé. Nécessite Windows x64, WebView2 et un GPU compatible.
Le lanceur et le jeu n'exigent ni Cargo, ni Node, ni clone Tidewater.
Les captures et journaux sont écrits dans
%LOCALAPPDATA%\OpenRPG Forge\Tidewater\captures.

Licence et crédits de la référence Tidewater : dossier licenses/.
'@
[IO.File]::WriteAllText((Join-Path $partial 'LISEZ-MOI.txt'), $readme, [Text.UTF8Encoding]::new($false))

$sourceFiles = @(Get-ChildItem -LiteralPath $bundle -Recurse -File -Force)
$copiedFiles = @(Get-ChildItem -LiteralPath (Join-Path $partial 'data') -Recurse -File -Force)
if ($sourceFiles.Count -ne $copiedFiles.Count) {
    throw "Copie incomplète : $($copiedFiles.Count)/$($sourceFiles.Count) fichiers."
}
foreach ($source in $sourceFiles) {
    $relative = $source.FullName.Substring($bundle.Length).TrimStart('\', '/')
    $copy = Join-Path (Join-Path $partial 'data') $relative
    if (-not (Test-Path -LiteralPath $copy -PathType Leaf)) { throw "Fichier manquant : $relative" }
    if ($source.Length -ne (Get-Item -LiteralPath $copy).Length) { throw "Taille modifiée : $relative" }
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash) {
        throw "Hash modifié : $relative"
    }
}

# These three source-image locations are development provenance only. The game does
# not load them; keep their metadata keys while removing the author's local paths.
$fixturePath = Join-Path $partial 'data/scene/fixture.json'
$fixtureText = [IO.File]::ReadAllText($fixturePath)
foreach ($reference in @(
    'E:\\OpenRPG_Forge\\.tidewater-integration\\source-water-detail-001.png',
    'E:\\OpenRPG_Forge\\.tidewater-integration\\source-water-breakers-002.png',
    'E:\\OpenRPG_Forge\\.tidewater-integration\\source-water-spray-003.png'
)) {
    if (-not $fixtureText.Contains($reference)) { throw "Référence attendue absente du fixture : $reference" }
    $fixtureText = $fixtureText.Replace($reference, 'reference-capture-not-bundled')
}
$null = ConvertFrom-Json -InputObject $fixtureText -Depth 100
[IO.File]::WriteAllText($fixturePath, $fixtureText, [Text.UTF8Encoding]::new($false))

# The copied bundle is self-verifying. Redacting the development-only paths changes
# the fixture bytes, so refresh its entry before the Windows manifest is generated.
$previewScenePath = Join-Path $partial 'data/preview-scene.json'
$previewScene = Get-Content -LiteralPath $previewScenePath -Raw | ConvertFrom-Json -Depth 100
$fixtureEntry = $previewScene.files.PSObject.Properties['scene/fixture.json']
if ($null -eq $fixtureEntry) { throw 'Le manifeste interne ne référence pas scene/fixture.json.' }
$fixtureBytes = [int64](Get-Item -LiteralPath $fixturePath).Length
$fixtureSha256 = (Get-FileHash -LiteralPath $fixturePath -Algorithm SHA256).Hash.ToLowerInvariant()
$fixtureEntry.Value.bytes = $fixtureBytes
$fixtureEntry.Value.sha256 = $fixtureSha256
[IO.File]::WriteAllText(
    $previewScenePath,
    (ConvertTo-Json -InputObject $previewScene -Depth 100) + "`n",
    [Text.UTF8Encoding]::new($false)
)
$previewSceneSha256 = (Get-FileHash -LiteralPath $previewScenePath -Algorithm SHA256).Hash.ToLowerInvariant()
& $python (Join-Path $PSScriptRoot 'package-preview-scene.py') --verify (Join-Path $partial 'data') --expected-sha256 $previewSceneSha256
if ($LASTEXITCODE -ne 0) { throw 'Le manifeste interne du paquet Tidewater est incohérent.' }

$manifestFiles = @(Get-ChildItem -LiteralPath $partial -Recurse -File -Force | ForEach-Object {
    [ordered]@{
        path = $_.FullName.Substring($partial.Length + 1).Replace('\', '/')
        bytes = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
})
$manifest = [ordered]@{
    schema = 'forge.tidewater.windows-playtest.v1'
    platform = 'windows-x64'
    source_commit = '1438b1abfcaee3267092b75573014f4d9b4a983c'
    area_id = 'e620dd19-ead9-5913-a6d6-0f93109148c0'
    native_pebble_proof_sha256 = (Get-FileHash -LiteralPath $pebbleProofPath -Algorithm SHA256).Hash.ToLowerInvariant()
    runtime_sha256 = $runtimeHash
    game_fish_package_sha256 = $walkProof.source_fish.game_package_sha256
    fish_surface_package_sha256 = $walkProof.source_fish.surface_package_sha256
    source_fish_proof_scope = 'MainWorld material/LOD preparation; visual comparison and extracted-package execution remain separate'
    files = $manifestFiles
}
$json = ConvertTo-Json -InputObject $manifest -Depth 5
[IO.File]::WriteAllText((Join-Path $partial 'manifest.json'), $json + "`n", [Text.UTF8Encoding]::new($false))

[IO.Directory]::Move($partial, $output)
if (-not $SkipZip) {
    Add-Type -AssemblyName System.IO.Compression.ZipFile
    # Explorer already creates an extraction folder named after the ZIP. A
    # second long enclosing folder can exceed Win32 limits for source blobs.
    [IO.Compression.ZipFile]::CreateFromDirectory($output, $partialZip, [IO.Compression.CompressionLevel]::Optimal, $false)
    [IO.File]::Move($partialZip, $zip)
}

$manifestBytes = ($manifestFiles | ForEach-Object { [int64]$_['bytes'] } | Measure-Object -Sum).Sum
Write-Output "TIDEWATER_PLAYTEST_CANDIDATE_BUILT path=$output zip=$(-not $SkipZip) files=$($manifestFiles.Count) bytes=$manifestBytes"
