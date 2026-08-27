[CmdletBinding()]
param(
    [string]$DistributionDirectory,
    [string]$Workspace,
    [string]$TemplateDirectory,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($DistributionDirectory)) {
    $DistributionDirectory = Join-Path $repositoryRoot 'exe'
}
if ([string]::IsNullOrWhiteSpace($Workspace)) {
    $Workspace = Join-Path $repositoryRoot 'apps\Forge.Runtime\content\crypt-of-ashes'
}
if ([string]::IsNullOrWhiteSpace($TemplateDirectory)) {
    $TemplateDirectory = Join-Path $repositoryRoot 'artifacts\templates\game-windows-x64'
}

$distribution = [System.IO.Path]::GetFullPath($DistributionDirectory)
$workspacePath = [System.IO.Path]::GetFullPath($Workspace)
$template = [System.IO.Path]::GetFullPath($TemplateDirectory)
$launcherSource = Join-Path $template 'OpenRPG_Forge_GameLauncher.exe'
$runtimeExecutable = Join-Path $distribution 'runtime\OpenRPG_Forge_Runtime.exe'
$runtimePackage = Join-Path $distribution 'runtime\OpenRPG_Forge_Runtime.pck'
$launcher = Join-Path $distribution 'Cendres.exe'
$module = Join-Path $distribution 'game\crypt-of-ashes-current.orpmod'
$launcherManifest = Join-Path $distribution 'Cendres.game-launcher.json'
$distributionManifest = Join-Path $distribution 'Cendres.game-distribution-manifest.json'
$eAcute = [char]0x00E9
$emDash = [char]0x2014
$regressionLabel = "r${eAcute}gression"

foreach ($required in @($distribution, $workspacePath, $template)) {
    if (-not (Test-Path -LiteralPath $required -PathType Container)) {
        throw "Required directory is missing: $required"
    }
}
foreach ($required in @($launcherSource, $runtimeExecutable, $runtimePackage)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required distribution file is missing: $required"
    }
}

$outputs = @($launcher, $module, $launcherManifest, $distributionManifest)
if (-not $Force -and $outputs.Where({ Test-Path -LiteralPath $_ }).Count -gt 0) {
    throw 'Crypte regression launcher outputs already exist. Use -Force to replace only those four files.'
}

New-Item -ItemType Directory -Path (Split-Path -Parent $module) -Force | Out-Null
$packageTemporary = Join-Path ([System.IO.Path]::GetTempPath()) (
    "openrpg-crypt-regression-" + [Guid]::NewGuid().ToString('N') + '.orpmod')
try {
    & dotnet run --project (Join-Path $repositoryRoot 'tools\Forge.Cli') --configuration Release --no-restore -- `
        package export $workspacePath $packageTemporary --target editor
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $packageTemporary -PathType Leaf)) {
        throw "Crypte regression package export failed with exit code $LASTEXITCODE."
    }

    Copy-Item -LiteralPath $launcherSource -Destination $launcher -Force
    Copy-Item -LiteralPath $packageTemporary -Destination $module -Force

    function Get-Sha256([string]$Path) {
        return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    }

    $launcherDocument = [pscustomobject][ordered]@{
        schemaVersion = 1
        gameId = '30000000-0000-4000-8000-000000000001'
        slug = 'crypt-of-ashes'
        displayName = 'La Crypte des Cendres'
        tagline = "Module de $regressionLabel jouable : les braises vous attendent."
        version = '0.8.0'
        accentColor = '#C77A45'
        executableFile = 'Cendres.exe'
        runtimeExecutable = 'runtime/OpenRPG_Forge_Runtime.exe'
        modulePackage = 'game/crypt-of-ashes-current.orpmod'
        launcherSha256 = Get-Sha256 $launcher
        runtimeSha256 = Get-Sha256 $runtimeExecutable
        moduleSha256 = Get-Sha256 $module
        backgroundImage = $null
        newsUrl = $null
        supportUrl = $null
        settingsEnabled = $true
        multiplayer = 'localHost'
        defaultServerPort = 5121
        music = $null
    }
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText(
        $launcherManifest,
        ($launcherDocument | ConvertTo-Json -Depth 8) + [Environment]::NewLine,
        $utf8)

    $files = [System.Collections.Generic.List[object]]::new()
    $paths = @('Cendres.exe', 'game/crypt-of-ashes-current.orpmod', 'Cendres.game-launcher.json')
    $distributionPrefix = $distribution.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    $paths += Get-ChildItem -LiteralPath (Join-Path $distribution 'runtime') -Recurse -File |
        ForEach-Object { $_.FullName.Substring($distributionPrefix.Length).Replace('\', '/') }
    foreach ($relative in $paths | Sort-Object -Unique) {
        $absolute = Join-Path $distribution ($relative.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        $files.Add([pscustomobject][ordered]@{
            path = $relative
            bytes = (Get-Item -LiteralPath $absolute).Length
            sha256 = Get-Sha256 $absolute
        })
    }
    $distributionDocument = [pscustomobject][ordered]@{
        schemaVersion = 1
        gameId = '30000000-0000-4000-8000-000000000001'
        product = "La Crypte des Cendres $emDash module de $regressionLabel"
        version = '0.8.0'
        platform = 'windows-x64'
        files = $files
    }
    [System.IO.File]::WriteAllText(
        $distributionManifest,
        ($distributionDocument | ConvertTo-Json -Depth 8) + [Environment]::NewLine,
        $utf8)

    & dotnet run --project (Join-Path $repositoryRoot 'tools\Forge.Cli') --configuration Release --no-restore -- `
        launcher verify $distribution --executable Cendres.exe
    if ($LASTEXITCODE -ne 0) {
        throw "Crypte regression launcher verification failed with exit code $LASTEXITCODE."
    }
}
finally {
    if (Test-Path -LiteralPath $packageTemporary -PathType Leaf) {
        Remove-Item -LiteralPath $packageTemporary
    }
}

Write-Output "OPENRPG_FORGE_CRYPT_REGRESSION_LAUNCHER_READY path=$launcher package=$module verified=true"
