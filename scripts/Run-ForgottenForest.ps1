[CmdletBinding()]
param(
    [string]$VaultRoot = $env:OPENRPG_ASSET_VAULT,
    [switch]$SkipAuthoring
)

. (Join-Path $PSScriptRoot 'Common.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runtimePath = Join-Path $repositoryRoot 'apps\Forge.Runtime'
$workspacePath = Join-Path $runtimePath 'content\forgotten-forest'
$cliProject = Join-Path $repositoryRoot 'tools\Forge.Cli\Forge.Cli.csproj'
$runtimeProject = Join-Path $runtimePath 'Forge.Runtime.csproj'
$artifactRoot = Join-Path $repositoryRoot 'artifacts\verification'
$runtimeLog = Join-Path $artifactRoot 'forgotten-forest-interactive.log'
$enginePath = Get-ForgeEnginePath

if ([string]::IsNullOrWhiteSpace($VaultRoot) -and (Test-Path -LiteralPath 'E:\OpenRPG_AssetVault')) {
    $VaultRoot = 'E:\OpenRPG_AssetVault'
}
if ([string]::IsNullOrWhiteSpace($VaultRoot) -or !(Test-Path -LiteralPath $VaultRoot)) {
    throw 'Le Vault d’assets est introuvable. Définissez OPENRPG_ASSET_VAULT ou passez -VaultRoot.'
}

[System.IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
& dotnet build $cliProject --configuration Debug --nologo
if ($LASTEXITCODE -ne 0) { throw "La compilation de l’outil Forge a échoué avec le code $LASTEXITCODE." }

if (!$SkipAuthoring -or !(Test-Path -LiteralPath (Join-Path $workspacePath 'forge.project.json'))) {
    & dotnet run --project $cliProject --configuration Debug --no-build -- `
        author-forgotten-forest $workspacePath --vault $VaultRoot
    if ($LASTEXITCODE -ne 0) { throw "L’authoring de Forgotten Forest a échoué avec le code $LASTEXITCODE." }
}

& dotnet build $runtimeProject --configuration Debug --nologo
if ($LASTEXITCODE -ne 0) { throw "La compilation du runtime a échoué avec le code $LASTEXITCODE." }

Start-Process `
    -FilePath $enginePath `
    -ArgumentList @(
        '--rendering-method', 'forward_plus',
        '--path', $runtimePath,
        '--log-file', $runtimeLog,
        '--', '--workspace', $workspacePath) `
    -WorkingDirectory $runtimePath

Write-Output "Forgotten Forest lancé. ZQSD : déplacement ; clic droit/milieu : caméra ; molette : zoom."
Write-Output "Journal : $runtimeLog"
