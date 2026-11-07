<# Install the publisher-verified Linux build tool on E:, using existing Hyper-V.
No host reboot is issued; existing Docker installations are preserved.
https://docs.docker.com/desktop/setup/install/windows-install/
#>
[CmdletBinding()]
param([ValidateSet('Inspect','Install')][string]$Mode='Inspect')
$ErrorActionPreference='Stop'
$gateInstaller='E:\OpenRPG_Forge\.build\tools\docker-desktop-4.96.3\Docker Desktop Installer.exe'
$gateApplication='E:\OpenRPG_Forge\.build\tools\docker-desktop'
$gateData='E:\OpenRPG_Forge\.build\docker-desktop'
$gateOutput='E:\OpenRPG_Forge\.build\vm-qualification\docker-linux\installation-001.json'
$gateExisting=@('C:\Program Files\Docker\Docker\Docker Desktop.exe',
    (Join-Path $gateApplication 'Docker Desktop.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\DockerDesktop\Docker Desktop.exe')) |
    Where-Object { Test-Path -LiteralPath $_ }
if ($Mode -eq 'Inspect') {
    @{ existing_applications=@($gateExisting); installer_present=(Test-Path -LiteralPath $gateInstaller);
       intended_backend='hyper-v'; host_restart_requested=$false } | ConvertTo-Json
    return
}
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator installation required' }
if ($gateExisting -or (Test-Path -LiteralPath $gateApplication) -or (Test-Path -LiteralPath $gateData) -or (Test-Path -LiteralPath $gateOutput)) { throw 'Existing installation/data/receipt preserved; inspect before any retry' }
if ((Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All).State -ne 'Enabled') { throw 'Hyper-V must already be enabled; no feature activation or reboot is attempted here' }
if ((Get-FileHash -LiteralPath $gateInstaller -Algorithm SHA256).Hash.ToLowerInvariant() -ne '3d8adad4f6c86583e0b00ea19a76197c51ccf5c83437c9ed775b739756452507') { throw 'Docker publisher digest mismatch' }
$gateSignature=Get-AuthenticodeSignature -LiteralPath $gateInstaller
if ($gateSignature.Status -ne 'Valid' -or $gateSignature.SignerCertificate.Subject -notmatch 'CN=Docker Inc,') { throw 'Valid Docker publisher signature required' }
if ((Get-PSDrive E).Free -lt 50GB) { throw '50 GiB free on development drive required before creating Linux build caches' }
New-Item -ItemType Directory -Path (Split-Path $gateOutput) -Force | Out-Null
$gateResult=@{schema='forge.gate7.docker-linux-install.v1'; started_utc=[DateTime]::UtcNow.ToString('o');
    version='4.96.3'; backend='hyper-v'; application=$gateApplication; data=$gateData;
    installer_sha256='3d8adad4f6c86583e0b00ea19a76197c51ccf5c83437c9ed775b739756452507';
    publisher_signature_valid=$true; windows_containers_enabled=$false;
    host_restart_requested=$false; qualification_complete=$false; succeeded=$false}
$gateResult | ConvertTo-Json | Set-Content -LiteralPath $gateOutput -Encoding UTF8
try {
    $gateProcess=Start-Process -FilePath $gateInstaller -ArgumentList @('install','--quiet','--accept-license',
        '--backend=hyper-v','--no-windows-containers','--always-run-service',
        ('--installation-dir='+$gateApplication),('--hyper-v-default-data-root='+$gateData)) -WindowStyle Hidden -Wait -PassThru
    $gateResult.exit_code=$gateProcess.ExitCode
    $gateResult.restart_required=($gateProcess.ExitCode -eq 3010)
    $gateResult.succeeded=($gateProcess.ExitCode -in @(0,3010))
} catch { $gateResult.error=$_.Exception.Message; throw }
finally {
    $gateResult.finished_utc=[DateTime]::UtcNow.ToString('o')
    $gateResult | ConvertTo-Json | Set-Content -LiteralPath $gateOutput -Encoding UTF8
}
if (-not $gateResult.succeeded) { throw ('Docker installation failed: '+$gateResult.exit_code) }
