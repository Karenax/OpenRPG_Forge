[CmdletBinding()]
param(
    [string]$CreatorDistributionDirectory = 'artifacts/distribution/creator-windows-x64',
    [string]$OutputDirectory = 'artifacts/distribution/installer-windows-x64',
    [string]$MakeNsisPath,
    [string]$Version = '0.1.0-dev',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$distributionRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/distribution'))
foreach ($variableName in @('CreatorDistributionDirectory', 'OutputDirectory')) {
    $value = Get-Variable -Name $variableName -ValueOnly
    if (-not [System.IO.Path]::IsPathRooted($value)) { $value = Join-Path $repositoryRoot $value }
    Set-Variable -Name $variableName -Value ([System.IO.Path]::GetFullPath($value))
}
$allowedPrefix = $distributionRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $OutputDirectory.StartsWith($allowedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Installer output must be below $distributionRoot."
}
if (-not (Test-Path -LiteralPath $CreatorDistributionDirectory -PathType Container)) {
    throw "Creator distribution missing: $CreatorDistributionDirectory"
}
if (Test-Path -LiteralPath $OutputDirectory) {
    if (-not $Force) { throw "Installer output already exists. Pass -Force: $OutputDirectory" }
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

& (Join-Path $PSScriptRoot 'Verify-CreatorDistribution.ps1') `
    -DistributionDirectory $CreatorDistributionDirectory

if ([string]::IsNullOrWhiteSpace($MakeNsisPath)) {
    $command = Get-Command 'makensis.exe' -ErrorAction SilentlyContinue
    if ($null -ne $command) { $MakeNsisPath = $command.Source }
    elseif (Test-Path -LiteralPath 'C:\Program Files (x86)\NSIS\makensis.exe') {
        $MakeNsisPath = 'C:\Program Files (x86)\NSIS\makensis.exe'
    }
    elseif (Test-Path -LiteralPath 'C:\Program Files\NSIS\makensis.exe') {
        $MakeNsisPath = 'C:\Program Files\NSIS\makensis.exe'
    }
    else { throw 'NSIS 3.12 or newer is required to build the Windows installer.' }
}
$MakeNsisPath = [System.IO.Path]::GetFullPath($MakeNsisPath)
$nsisVersion = (& $MakeNsisPath '/VERSION').Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($nsisVersion)) {
    throw 'Unable to read the NSIS compiler version.'
}
$scriptPath = Join-Path $repositoryRoot 'release/windows/OpenRPGForge.nsi'
$iconPath = Join-Path $repositoryRoot 'apps/Forge.CreatorLauncher/openrpg_forge.ico'
$installerPath = Join-Path $OutputDirectory "OpenRPG_Forge_Setup_$($Version)_x64.exe"
$buildLog = Join-Path $OutputDirectory 'nsis-build.log'

& $MakeNsisPath '/V3' "/O$buildLog" "/DSOURCE_DIR=$CreatorDistributionDirectory" `
    "/DOUTPUT_FILE=$installerPath" "/DPRODUCT_VERSION=$Version" "/DICON_FILE=$iconPath" $scriptPath
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "NSIS failed to produce the installer. See $buildLog"
}

$sourceManifest = Join-Path $CreatorDistributionDirectory 'distribution-manifest.json'
$signature = Get-AuthenticodeSignature -LiteralPath $installerPath
$manifest = [ordered]@{
    schema_version = 1
    product = 'OpenRPG Forge Creator Installer'
    version = $Version
    platform = 'windows-x64'
    installer = [ordered]@{
        file = [System.IO.Path]::GetFileName($installerPath)
        bytes = (Get-Item -LiteralPath $installerPath).Length
        sha256 = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
        authenticode = [string]$signature.Status
    }
    creator_manifest_sha256 = (Get-FileHash -LiteralPath $sourceManifest -Algorithm SHA256).Hash.ToLowerInvariant()
    nsis_version = $nsisVersion
    source_revision = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    source_dirty = @(& git -C $repositoryRoot status --porcelain --untracked-files=normal).Count -gt 0
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'installer-manifest.json') -Encoding utf8

Write-Output "OPENRPG_FORGE_WINDOWS_INSTALLER_READY path=$installerPath bytes=$($manifest.installer.bytes) signature=$($manifest.installer.authenticode) nsis=$nsisVersion"
