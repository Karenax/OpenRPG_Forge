[CmdletBinding()]
param(
    [string]$PackagePath,
    [string]$OutputDirectory,
    [string]$GodotExecutable = 'Godot_v4.7.1-stable_mono_win64_console.exe'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $repositoryRoot 'artifacts\world-generation\solaren-vertical-slice\solaren-world-generation.orpmod'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\network\secure-runtime'
}
$PackagePath = [IO.Path]::GetFullPath($PackagePath)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
    throw "Secure runtime smoke package is missing: $PackagePath"
}
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null

$engine = (Get-Command $GodotExecutable -ErrorAction Stop).Source
$runtimePath = Join-Path $repositoryRoot 'apps\Forge.Runtime'
$certificatePath = Join-Path $OutputDirectory 'internet-host-certificate.pfx'
$hostSettings = Join-Path $OutputDirectory 'host-settings.json'
$joinSettings = Join-Path $OutputDirectory 'join-settings.json'
$hostLog = Join-Path $OutputDirectory 'host.log'
$joinLog = Join-Path $OutputDirectory 'join.log'

$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()

function New-Base64UrlSecret {
    $bytes = [byte[]]::new(32)
    [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    try {
        return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    }
    finally {
        [Security.Cryptography.CryptographicOperations]::ZeroMemory($bytes)
    }
}

$token = New-Base64UrlSecret
$certificatePassword = New-Base64UrlSecret
$rsa = [Security.Cryptography.RSA]::Create(2048)
$request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
    'CN=openrpg-secure-runtime-smoke',
    $rsa,
    [Security.Cryptography.HashAlgorithmName]::SHA256,
    [Security.Cryptography.RSASignaturePadding]::Pkcs1)
$certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddDays(7))
try {
    $certificateBytes = $certificate.Export(
        [Security.Cryptography.X509Certificates.X509ContentType]::Pfx,
        $certificatePassword)
    try {
        [IO.File]::WriteAllBytes($certificatePath, $certificateBytes)
    }
    finally {
        [Security.Cryptography.CryptographicOperations]::ZeroMemory($certificateBytes)
    }
    $certificateSha256 = [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData($certificate.RawData)).ToLowerInvariant()
}
finally {
    $certificate.Dispose()
    $rsa.Dispose()
}

function Write-LauncherSettings([string]$Path, [string]$Mode, [string]$Player, [Guid]$AccountId, [bool]$IsHostCertificate) {
    $settings = [ordered]@{
        schemaVersion = 2
        resolutionWidth = 1600
        resolutionHeight = 900
        windowMode = 'fullscreen'
        graphicsQuality = 'high'
        vSync = $true
        masterVolume = 0
        musicVolume = 0
        effectsVolume = 0
        reduceMotion = $true
        sessionMode = $Mode
        playerName = $Player
        serverAddress = '127.0.0.1'
        serverPort = $port
        networkAccountId = $AccountId
        networkJoinToken = $token
        serverCertificateSha256 = $certificateSha256
        hostCertificatePath = $(if ($IsHostCertificate) { $certificatePath } else { '' })
        hostCertificatePassword = $(if ($IsHostCertificate) { $certificatePassword } else { '' })
    }
    [IO.File]::WriteAllText(
        $Path,
        ($settings | ConvertTo-Json -Depth 4) + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))
}

Write-LauncherSettings $hostSettings 'localHost' 'Hote' ([Guid]::NewGuid()) $true
Write-LauncherSettings $joinSettings 'directConnectPreview' 'Invite' ([Guid]::NewGuid()) $false

function Start-Runtime([string]$SettingsPath, [string]$Mode, [string]$Player, [string]$LogPath) {
    $arguments = @(
        '--headless',
        '--rendering-method', 'forward_plus',
        '--path', $runtimePath,
        '--log-file', $LogPath,
        '--',
        '--module-package', $PackagePath,
        '--launcher-settings', $SettingsPath,
        '--launcher-mode', $Mode,
        '--player-name', $Player,
        '--server-port', $port.ToString([Globalization.CultureInfo]::InvariantCulture),
        '--secure-internet-runtime-smoke-test'
    )
    if ($Mode -eq 'internet-join') {
        $arguments += @('--server-address', '127.0.0.1')
    }
    return Start-Process -FilePath $engine -ArgumentList $arguments -PassThru -WindowStyle Hidden
}

function Read-SharedText([string]$Path) {
    $stream = [IO.FileStream]::new(
        $Path,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
    try {
        $reader = [IO.StreamReader]::new($stream, [Text.Encoding]::UTF8, $true, 4096, $true)
        try { return $reader.ReadToEnd() }
        finally { $reader.Dispose() }
    }
    finally { $stream.Dispose() }
}

function Wait-ForLogMarker([Diagnostics.Process]$Process, [string]$Path, [string]$Marker, [int]$TimeoutSeconds) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path -PathType Leaf) {
            $content = Read-SharedText $Path
            if ($content.Contains($Marker, [StringComparison]::Ordinal)) { return }
        }
        if ($Process.HasExited) {
            throw "Runtime exited with code $($Process.ExitCode) before marker '$Marker'."
        }
        Start-Sleep -Milliseconds 200
    }
    throw "Timed out waiting for runtime marker '$Marker'."
}

function Wait-ForExit([Diagnostics.Process]$Process, [int]$TimeoutSeconds) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    while (-not $Process.HasExited -and [DateTimeOffset]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 200
    }
    if (-not $Process.HasExited) { throw "Runtime process $($Process.Id) did not finish in time." }
    if ($Process.ExitCode -ne 0) { throw "Runtime process $($Process.Id) failed with code $($Process.ExitCode)." }
}

$hostProcess = $null
$joinProcess = $null
try {
    $hostProcess = Start-Runtime $hostSettings 'internet-host' 'Hote' $hostLog
    Wait-ForLogMarker $hostProcess $hostLog 'OPENRPG_FORGE_RUNTIME_INTERNET_HOST_PASS' 60
    $joinProcess = Start-Runtime $joinSettings 'internet-join' 'Invite' $joinLog
    Wait-ForExit $hostProcess 90
    Wait-ForExit $joinProcess 30

    $hostContent = Read-SharedText $hostLog
    $joinContent = Read-SharedText $joinLog
    if (-not $hostContent.Contains('OPENRPG_FORGE_RUNTIME_SECURE_INTERNET_HOST_PASS', [StringComparison]::Ordinal) -or
        -not $joinContent.Contains('OPENRPG_FORGE_RUNTIME_SECURE_INTERNET_JOIN_PASS', [StringComparison]::Ordinal)) {
        throw 'Secure runtime smoke markers are incomplete.'
    }
}
finally {
    foreach ($process in @($joinProcess, $hostProcess)) {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
        if ($null -ne $process) { $process.Dispose() }
    }
}

Write-Output (
    "OPENRPG_FORGE_SECURE_INTERNET_RUNTIME_PASS transport=tls-tcp authentication=pinned-token " +
    "players=2 combat=server inventory=server quest=server save=server output=$OutputDirectory")
