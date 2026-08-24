[CmdletBinding()]
param(
    [string]$DistributionDirectory = 'artifacts/distribution/studio-windows-x64',
    [switch]$RunSmoke
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not [System.IO.Path]::IsPathRooted($DistributionDirectory)) {
    $DistributionDirectory = Join-Path $repositoryRoot $DistributionDirectory
}
$distributionPath = [System.IO.Path]::GetFullPath($DistributionDirectory)
$manifestPath = Join-Path $distributionPath 'distribution-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Distribution manifest missing: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schema_version -ne 1 -or $manifest.product -ne 'OpenRPG Forge Studio') {
    throw 'Unsupported Studio distribution manifest.'
}
if ($manifest.platform -notin @('windows-x64', 'linux-x64')) {
    throw "Unsupported Studio distribution platform: $($manifest.platform)"
}

$distributionPrefix = $distributionPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
$pathComparison = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows)) {
    [System.StringComparison]::OrdinalIgnoreCase
}
else {
    [System.StringComparison]::Ordinal
}
function Resolve-DistributionRelativePath {
    param([Parameter(Mandatory)][string]$RelativePath)

    $normalized = $RelativePath.Replace('\', '/')
    if ([string]::IsNullOrWhiteSpace($normalized) -or
        [System.IO.Path]::IsPathRooted($normalized)) {
        throw "Invalid distribution-relative path: $RelativePath"
    }
    $resolved = [System.IO.Path]::GetFullPath((Join-Path $distributionPath $normalized))
    if (-not $resolved.StartsWith($distributionPrefix, $pathComparison)) {
        throw "Distribution path escapes the verified package: $RelativePath"
    }
    return $resolved
}

foreach ($file in $manifest.files) {
    $filePath = Resolve-DistributionRelativePath -RelativePath ([string]$file.path)
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        throw "Distribution file missing: $($file.path)"
    }
    if ((Get-Item -LiteralPath $filePath).Length -ne [long]$file.bytes) {
        throw "Distribution size mismatch: $($file.path)"
    }
    $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne [string]$file.sha256) {
        throw "Distribution hash mismatch: $($file.path)"
    }
}

$executableName = if ($manifest.platform -eq 'windows-x64') {
    'OpenRPG_Forge_Studio.exe'
}
else {
    'OpenRPG_Forge_Studio.x86_64'
}
foreach ($relativePath in @(
        $executableName,
        'OpenRPG_Forge_Studio.pck',
        'workspaces/forgotten-forest/project.json')) {
    $candidate = Resolve-DistributionRelativePath -RelativePath $relativePath
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Required Studio distribution file missing: $relativePath"
    }
}
$executablePath = Resolve-DistributionRelativePath -RelativePath $executableName
$header = New-Object byte[] 4
$stream = [System.IO.File]::OpenRead($executablePath)
try {
    if ($stream.Read($header, 0, $header.Length) -ne $header.Length) {
        throw "Studio executable header is incomplete: $executableName"
    }
}
finally {
    $stream.Dispose()
}
if ($manifest.platform -eq 'windows-x64' -and
    ($header[0] -ne 0x4d -or $header[1] -ne 0x5a)) {
    throw 'The Windows Studio executable is not a PE image.'
}
if ($manifest.platform -eq 'linux-x64' -and
    ($header[0] -ne 0x7f -or $header[1] -ne 0x45 -or $header[2] -ne 0x4c -or
        $header[3] -ne 0x46)) {
    throw 'The Linux Studio executable is not an ELF image.'
}
if ($null -eq (Get-ChildItem -LiteralPath $distributionPath -Recurse -Filter 'Forge.Studio.dll' -File |
        Select-Object -First 1)) {
    throw 'Forge.Studio.dll is missing from the Studio distribution.'
}
$nativeLibraryName = if ($manifest.platform -eq 'windows-x64') {
    'forge_script_native.dll'
}
else {
    'libforge_script_native.so'
}
$nativeLibrary = Get-ChildItem -LiteralPath $distributionPath -Recurse -Filter $nativeLibraryName -File |
    Select-Object -First 1
if ($null -eq $nativeLibrary) {
    throw "$nativeLibraryName is missing from the Studio distribution."
}
$nativeHeader = New-Object byte[] 4
$nativeStream = [System.IO.File]::OpenRead($nativeLibrary.FullName)
try {
    if ($nativeStream.Read($nativeHeader, 0, $nativeHeader.Length) -ne $nativeHeader.Length) {
        throw "ForgeScript native library header is incomplete: $nativeLibraryName"
    }
}
finally {
    $nativeStream.Dispose()
}
if ($manifest.platform -eq 'windows-x64' -and
    ($nativeHeader[0] -ne 0x4d -or $nativeHeader[1] -ne 0x5a)) {
    throw 'The Windows ForgeScript native library is not a PE image.'
}
if ($manifest.platform -eq 'linux-x64' -and
    ($nativeHeader[0] -ne 0x7f -or $nativeHeader[1] -ne 0x45 -or $nativeHeader[2] -ne 0x4c -or
        $nativeHeader[3] -ne 0x46)) {
    throw 'The Linux ForgeScript native library is not an ELF image.'
}

& (Join-Path $PSScriptRoot 'Test-DistributionCompliance.ps1') -DistributionDirectory $distributionPath

if ($RunSmoke) {
    $isWindowsHost = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows)
    $isLinuxHost = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Linux)
    if (($manifest.platform -eq 'windows-x64' -and -not $isWindowsHost) -or
        ($manifest.platform -eq 'linux-x64' -and -not $isLinuxHost)) {
        throw "A $($manifest.platform) smoke test requires a matching host."
    }

    $executable = Join-Path $distributionPath $executableName
    if ($manifest.platform -eq 'windows-x64') {
        $consoleExecutable = Join-Path $distributionPath 'OpenRPG_Forge_Studio.console.exe'
        if (Test-Path -LiteralPath $consoleExecutable -PathType Leaf) { $executable = $consoleExecutable }
    }
    elseif ($isLinuxHost) {
        & chmod +x $executable
        if ($LASTEXITCODE -ne 0) { throw 'Unable to mark the Linux Studio executable as runnable.' }
    }

    $workspace = Join-Path $distributionPath 'workspaces/forgotten-forest'
    $smokeLog = Join-Path $distributionPath 'studio-verification-smoke.log'
    $arguments = @(
        '--headless', '--log-file', "`"$smokeLog`"", '--',
        '--workspace', "`"$workspace`"", '--page', 'placement',
        '--studio-distribution-smoke-test'
    )
    $launch = @{
        FilePath = $executable
        ArgumentList = $arguments
        WorkingDirectory = $distributionPath
        PassThru = $true
    }
    if ($isWindowsHost) { $launch.WindowStyle = 'Hidden' }
    $process = Start-Process @launch
    if (-not $process.WaitForExit(900000)) {
        Stop-Process -Id $process.Id -Force
        throw "Studio distribution smoke exceeded fifteen minutes. See $smokeLog"
    }
    if ($process.ExitCode -ne 0) {
        throw "Studio distribution smoke failed with exit code $($process.ExitCode). See $smokeLog"
    }
    foreach ($marker in @(
            'OPENRPG_FORGE_STUDIO_EDIT_PASS',
            'OPENRPG_FORGE_STUDIO_UX5A_PASS',
            'OPENRPG_FORGE_STUDIO_UX5B_PASS',
            'OPENRPG_FORGE_STUDIO_DISTRIBUTION_SMOKE_PASS')) {
        if (-not (Select-String -LiteralPath $smokeLog -SimpleMatch $marker -Quiet)) {
            throw "Studio distribution smoke did not emit '$marker'. See $smokeLog"
        }
    }
}

Write-Output "OPENRPG_FORGE_STUDIO_DISTRIBUTION_VERIFY_PASS platform=$($manifest.platform) files=$($manifest.files.Count) smoke=$RunSmoke source=$($manifest.source_revision)"
