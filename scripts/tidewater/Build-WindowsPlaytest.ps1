[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$LauncherExe,
    [Parameter(Mandatory)][string]$RuntimeExe,
    [Parameter(Mandatory)][string]$BundleRoot,
    [Parameter(Mandatory)][string]$LauncherSource,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [switch]$SkipZip
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$launcher = [IO.Path]::GetFullPath($LauncherExe)
$runtime = [IO.Path]::GetFullPath($RuntimeExe)
$bundle = [IO.Path]::GetFullPath($BundleRoot)
$launcherSource = [IO.Path]::GetFullPath($LauncherSource)
$output = [IO.Path]::GetFullPath($OutputDirectory)
$partial = "$output.partial"
$zip = "$output.zip"
$partialZip = "$zip.partial"

foreach ($file in @($launcher, $runtime)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Executable absent : $file" }
}
$launcherCheck = & $launcher --verify-distributable
if ($LASTEXITCODE -ne 0 -or $launcherCheck -ne 'FORGE_LAUNCHER_AUBE_FRONTEND_READY') {
    throw "Le lanceur ne contient pas L'Aube des royaumes : reconstruire avec cargo tauri build --no-bundle --features production."
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
Copy-Item -LiteralPath (Join-Path $bundle 'scene/LICENSE-Tidewater.txt') -Destination (Join-Path $partial 'licenses/Tidewater-MIT.txt')
Copy-Item -LiteralPath (Join-Path $bundle 'scene/CREDITS-Tidewater.md') -Destination (Join-Path $partial 'licenses/Tidewater-CREDITS.md')
$forgeManifest = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Cargo.toml'))
$launcherManifest = Join-Path $launcherSource 'Cargo.toml'
& python (Join-Path $PSScriptRoot 'collect_rust_notices.py') $forgeManifest $launcherManifest (Join-Path $partial 'licenses/rust')
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
expérimental désactivé par défaut). Dans ce menu, la molette parcourt
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
& python (Join-Path $PSScriptRoot 'package-preview-scene.py') --verify (Join-Path $partial 'data') --expected-sha256 $previewSceneSha256
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
    files = $manifestFiles
}
$json = ConvertTo-Json -InputObject $manifest -Depth 5
[IO.File]::WriteAllText((Join-Path $partial 'manifest.json'), $json + "`n", [Text.UTF8Encoding]::new($false))

[IO.Directory]::Move($partial, $output)
if (-not $SkipZip) {
    Add-Type -AssemblyName System.IO.Compression.ZipFile
    [IO.Compression.ZipFile]::CreateFromDirectory($output, $partialZip, [IO.Compression.CompressionLevel]::Optimal, $true)
    [IO.File]::Move($partialZip, $zip)
}

$manifestBytes = ($manifestFiles | ForEach-Object { [int64]$_['bytes'] } | Measure-Object -Sum).Sum
Write-Output "TIDEWATER_PLAYTEST_CANDIDATE_BUILT path=$output zip=$(-not $SkipZip) files=$($manifestFiles.Count) bytes=$manifestBytes"
