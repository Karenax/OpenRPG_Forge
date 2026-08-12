[CmdletBinding()]
param(
    [string]$InstallerDirectory = 'artifacts/distribution/installer-windows-x64',
    [switch]$RunInstallCycle,
    [int]$SmokeTimeoutSeconds = 240
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not [System.IO.Path]::IsPathRooted($InstallerDirectory)) {
    $InstallerDirectory = Join-Path $repositoryRoot $InstallerDirectory
}
$installerPathRoot = [System.IO.Path]::GetFullPath($InstallerDirectory)
$manifestPath = Join-Path $installerPathRoot 'installer-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Installer manifest missing: $manifestPath" }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schema_version -ne 1 -or $manifest.product -ne 'OpenRPG Forge Creator Installer') {
    throw 'Unsupported installer manifest.'
}
$installer = Join-Path $installerPathRoot ([string]$manifest.installer.file)
if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) { throw "Installer missing: $installer" }
if ((Get-Item -LiteralPath $installer).Length -ne [long]$manifest.installer.bytes) {
    throw 'Installer size differs from its manifest.'
}
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
if ($hash -ne [string]$manifest.installer.sha256) { throw 'Installer hash differs from its manifest.' }

if ($RunInstallCycle) {
    # Keep the test root comparable to the default LocalAppData destination. Some governed asset
    # paths are intentionally descriptive and a deeply nested verification root would add an
    # artificial MAX_PATH failure that users do not encounter at the supported destination.
    $verificationRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/i'))
    $testRoot = Join-Path $verificationRoot ([Guid]::NewGuid().ToString('N').Substring(0, 8))
    $installPath = Join-Path $testRoot 'OpenRPG Forge'
    $profileRoot = Join-Path $testRoot 'p'
    $appDataPath = Join-Path $profileRoot 'r'
    $localAppDataPath = Join-Path $profileRoot 'l'
    $tempPath = Join-Path $profileRoot 't'
    foreach ($path in @($appDataPath, $localAppDataPath, $tempPath)) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
    }
    if (@(Get-ChildItem -LiteralPath $profileRoot -Recurse -File).Count -ne 0) {
        throw 'Clean-profile fixture unexpectedly contains files before installation.'
    }

    $previousEnvironment = @{}
    foreach ($name in @('APPDATA', 'LOCALAPPDATA', 'TEMP', 'TMP')) {
        $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }
    try {
        [Environment]::SetEnvironmentVariable('APPDATA', $appDataPath, 'Process')
        [Environment]::SetEnvironmentVariable('LOCALAPPDATA', $localAppDataPath, 'Process')
        [Environment]::SetEnvironmentVariable('TEMP', $tempPath, 'Process')
        [Environment]::SetEnvironmentVariable('TMP', $tempPath, 'Process')

        $installProcess = Start-Process -FilePath $installer -ArgumentList @('/S', "/D=$installPath") `
            -WorkingDirectory $installerPathRoot -PassThru -Wait -WindowStyle Hidden
        if ($installProcess.ExitCode -ne 0) { throw "Silent installation failed: $($installProcess.ExitCode)" }
        if (-not (Test-Path -LiteralPath (Join-Path $installPath '.openrpg-forge-install') -PathType Leaf)) {
            throw 'Installed product marker is missing.'
        }
        & (Join-Path $PSScriptRoot 'Verify-CreatorDistribution.ps1') -DistributionDirectory $installPath `
            -RunSmoke -SmokeTimeoutSeconds $SmokeTimeoutSeconds

        # A second installation exercises the update-in-place path and both product-independent
        # installation files and external user data must survive it and the later uninstall.
        $userDataPath = Join-Path $appDataPath 'OpenRPG Forge UserData'
        New-Item -ItemType Directory -Path $userDataPath -Force | Out-Null
        'must-survive-uninstall' | Set-Content -LiteralPath (Join-Path $userDataPath 'preserved.txt') -Encoding ascii
        'update-preserved' | Set-Content -LiteralPath (Join-Path $installPath 'update-sentinel.txt') -Encoding ascii
        $updateProcess = Start-Process -FilePath $installer -ArgumentList @('/S', "/D=$installPath") `
            -WorkingDirectory $installerPathRoot -PassThru -Wait -WindowStyle Hidden
        if ($updateProcess.ExitCode -ne 0 -or
            -not (Test-Path -LiteralPath (Join-Path $installPath 'update-sentinel.txt') -PathType Leaf)) {
            throw 'Update-in-place failed or discarded an unrelated file.'
        }
        & (Join-Path $PSScriptRoot 'Verify-CreatorDistribution.ps1') -DistributionDirectory $installPath

        $uninstaller = Join-Path $installPath 'Uninstall OpenRPG Forge.exe'
        if (-not (Test-Path -LiteralPath $uninstaller -PathType Leaf)) { throw 'Uninstaller missing after installation.' }
        $uninstallProcess = Start-Process -FilePath $uninstaller -ArgumentList '/S' -WorkingDirectory $installPath `
            -PassThru -Wait -WindowStyle Hidden
        if ($uninstallProcess.ExitCode -ne 0) { throw "Silent uninstall failed: $($uninstallProcess.ExitCode)" }
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        while ((Test-Path -LiteralPath $installPath) -and [DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 250
        }
        if (Test-Path -LiteralPath $installPath) { throw "Uninstall left the installation directory behind: $installPath" }
        if (-not (Test-Path -LiteralPath (Join-Path $userDataPath 'preserved.txt') -PathType Leaf)) {
            throw 'Uninstall removed external user data.'
        }
    }
    finally {
        foreach ($name in @('APPDATA', 'LOCALAPPDATA', 'TEMP', 'TMP')) {
            [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
        }
    }
}

Write-Output "OPENRPG_FORGE_WINDOWS_INSTALLER_VERIFY_PASS hash=$hash cycle=$RunInstallCycle signature=$($manifest.installer.authenticode)"
