[CmdletBinding()]
param(
    [string]$Vault = $(if ($env:OPENRPG_ASSET_VAULT) { $env:OPENRPG_ASSET_VAULT } else { 'E:\OpenRPG_AssetVault' }),
    [int]$Port = 8765
)

$ErrorActionPreference = 'Stop'
$toolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$serverPath = Join-Path $toolRoot 'server.py'
$pythonCommand = Get-Command python -ErrorAction SilentlyContinue

if (-not $pythonCommand) {
    throw 'Python est introuvable. Installez Python 3.11+ ou ajoutez-le au PATH.'
}

if (-not (Test-Path -LiteralPath $Vault -PathType Container)) {
    throw "Asset Vault introuvable : $Vault"
}

Write-Host 'Ouverture de OpenRPG Asset Atlas...'
Write-Host "Vault : $Vault"
Write-Host 'Fermez cette fenêtre ou appuyez sur Ctrl+C pour arrêter le logiciel.'

& $pythonCommand.Source $serverPath --vault $Vault --port $Port --open
exit $LASTEXITCODE
