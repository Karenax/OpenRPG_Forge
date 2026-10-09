param([string]$Root, [switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
$deliveryRoot = $PSScriptRoot
$index = Get-Content -LiteralPath (Join-Path $deliveryRoot 'delivery.json') -Raw | ConvertFrom-Json
if ($index.schema -ne 'forge.delivery.index.v1') { throw 'Index de livraison incompatible.' }
$hub = Join-Path $deliveryRoot 'ForgeSetup.exe'
if ((Get-FileHash -LiteralPath $hub -Algorithm SHA256).Hash.ToLowerInvariant() -ne $index.setup_sha256) { throw 'Launcher modifie : verification refusee.' }
$version = $null
foreach ($key in @('HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'HKCU:\Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}')) {
    $value = Get-ItemProperty -LiteralPath $key -Name pv -ErrorAction SilentlyContinue
    if ($value -and $value.pv -match '^\d+\.\d+\.\d+\.\d+$' -and [version]$value.pv -gt [version]'0.0.0.0') { $version = $value.pv }
}
if ($CheckOnly) { @{ webview2_installed = [bool]$version; webview2_version = $version; launcher_integrity = 'PASS' } | ConvertTo-Json; exit 0 }
if (-not $version) {
    $installer = Join-Path $deliveryRoot 'prerequisites\MicrosoftEdgeWebView2RuntimeInstallerX64.exe'
    if (-not (Test-Path -LiteralPath $installer)) { throw 'Prerequis WebView2 absent de la livraison.' }
    if ((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant() -ne $index.prerequisites.webview2.sha256) { throw 'Prerequis WebView2 modifie.' }
    $signature = Get-AuthenticodeSignature -LiteralPath $installer
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') { throw 'Signature Microsoft du prerequis non valide.' }
    Write-Host 'WebView2 est necessaire au launcher et au Studio. Son installeur Microsoft hors ligne est fourni.'
    if ((Read-Host 'Installer ce prerequis maintenant ? [oui/non]') -ne 'oui') { exit 0 }
    $prerequisite = Start-Process -FilePath $installer -ArgumentList '/silent','/install' -WindowStyle Hidden -Wait -PassThru
    if ($prerequisite.ExitCode -ne 0) { throw "Installation WebView2 en echec : $($prerequisite.ExitCode)" }
}
if ($Root) {
    if ($Root -notmatch '^([A-Za-z]:[\\/]|\\\\[^\\]+\\[^\\]+\\)') { throw 'Le dossier installation doit etre absolu.' }
    $env:FORGE_DELIVERY_INSTALL_ROOT = [IO.Path]::GetFullPath($Root)
}
# The launcher is the visible application explicitly requested by the person starting this script.
Start-Process -FilePath $hub
