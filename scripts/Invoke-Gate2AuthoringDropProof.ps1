[CmdletBinding(DefaultParameterSetName = 'Proof')]
param(
    [Parameter(ParameterSetName = 'Proof', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$CandidateExecutable,

    [Parameter(ParameterSetName = 'Proof', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ExpectedSha256,

    [Parameter(ParameterSetName = 'Proof', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$AuthoringRoot,

    [Parameter(ParameterSetName = 'Proof', Mandatory)]
    [guid]$ZoneUuid,

    [Parameter(ParameterSetName = 'Proof', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory,

    [Parameter(ParameterSetName = 'Proof')]
    [string]$WebView2UserDataDirectory,

    [Parameter(ParameterSetName = 'Proof')]
    [ValidateRange(10, 120)]
    [int]$StartupTimeoutSeconds = 45,

    [Parameter(ParameterSetName = 'Proof')]
    [ValidateRange(10, 180)]
    [int]$InteractionTimeoutSeconds = 75,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:CdpMessageId = 0
$script:CdpEvents = [System.Collections.Generic.List[object]]::new()
$script:CdpPendingResponses = @{}
$script:CdpDroppedEventCount = 0
$expectedTargetUrl = 'http://tauri.localhost/index.html?authoring=1'
$expectedTargetTitle = 'OpenRPG Forge Studio'
$assetId = 'vv-rock-moss-a'
$authorSubject = 'gate2-r2-027-cdp-proof'
$ciStatus = 'NOT RUN — infrastructure billing'

function ConvertTo-CompactJson {
    param([Parameter(Mandatory)][object]$Value)
    return ($Value | ConvertTo-Json -Depth 100 -Compress)
}

function ConvertFrom-ExactJson {
    param([Parameter(Mandatory)][string]$Json)
    $command = Get-Command ConvertFrom-Json
    $arguments = @{}
    if ($command.Parameters.ContainsKey('Depth')) {
        $arguments.Depth = 100
    }
    if ($command.Parameters.ContainsKey('DateKind')) {
        $arguments.DateKind = 'String'
    }
    return ($Json | ConvertFrom-Json @arguments)
}

function Get-AuditedRequestKindFromIpcBody {
    param([Parameter(Mandatory)][string]$Body)
    $payload = ConvertFrom-ExactJson -Json $Body
    if (-not ($payload.PSObject.Properties.Name -contains 'command') -or $payload.command -isnot [string]) {
        return $null
    }
    $wire = ConvertFrom-ExactJson -Json ([string]$payload.command)
    if ([string]$wire.command -cne 'authoring' -or -not ($wire.PSObject.Properties.Name -contains 'request')) {
        return $null
    }
    return [string]$wire.request.action
}

function Wait-NonGenericTask {
    param([Parameter(Mandatory)][System.Threading.Tasks.Task]$Task)
    [void]$Task.GetAwaiter().GetResult()
}

function Get-NormalizedSha256 {
    param([Parameter(Mandatory)][string]$Value)
    $normalized = $Value.Trim().ToLowerInvariant()
    if ($normalized -cnotmatch '^[0-9a-f]{64}$') {
        throw 'ExpectedSha256 must contain exactly 64 hexadecimal characters.'
    }
    return $normalized
}

function Get-FileSha256 {
    param([Parameter(Mandatory)][string]$LiteralPath)
    return (Get-FileHash -LiteralPath $LiteralPath -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Test-IsDriveEPath {
    param([Parameter(Mandatory)][string]$LiteralPath)
    $fullPath = [System.IO.Path]::GetFullPath($LiteralPath)
    return [System.IO.Path]::GetPathRoot($fullPath).TrimEnd('\') -ieq 'E:'
}

function Assert-DriveEPath {
    param(
        [Parameter(Mandatory)][string]$LiteralPath,
        [Parameter(Mandatory)][string]$Description
    )
    if (-not (Test-IsDriveEPath -LiteralPath $LiteralPath)) {
        throw "$Description must be an absolute path on E:. Received '$LiteralPath'."
    }
}

function Test-PathWithinDirectory {
    param(
        [Parameter(Mandatory)][string]$LiteralPath,
        [Parameter(Mandatory)][string]$Directory
    )
    $candidate = [System.IO.Path]::GetFullPath($LiteralPath).TrimEnd('\')
    $root = [System.IO.Path]::GetFullPath($Directory).TrimEnd('\')
    return $candidate.Equals($root, [StringComparison]::OrdinalIgnoreCase) -or
        $candidate.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoExistingReparseAncestor {
    param(
        [Parameter(Mandatory)][string]$LiteralPath,
        [Parameter(Mandatory)][string]$Description
    )
    $current = [System.IO.Path]::GetFullPath($LiteralPath)
    while (-not (Test-Path -LiteralPath $current)) {
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) {
            throw "$Description has no existing parent directory: $LiteralPath"
        }
        $current = $parent
    }
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        $item = Get-Item -LiteralPath $current -Force
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Description traverses reparse point '$current'; physical E: storage is required."
        }
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) {
            break
        }
        $current = $parent
    }
}

function Get-ArchiveSnapshot {
    param([Parameter(Mandatory)][string]$LiteralPath)
    if (-not (Test-Path -LiteralPath $LiteralPath -PathType Leaf)) {
        throw "Source archive not found: $LiteralPath"
    }
    $item = Get-Item -LiteralPath $LiteralPath
    return [pscustomobject]@{
        Path = $item.FullName
        Sha256 = Get-FileSha256 -LiteralPath $item.FullName
        Bytes = [int64]$item.Length
        LastWriteUtc = ([DateTimeOffset]$item.LastWriteTimeUtc).ToString('o')
        LastWriteUtcTicks = [int64]$item.LastWriteTimeUtc.Ticks
    }
}

function Test-ArchiveSnapshotsEqual {
    param(
        [Parameter(Mandatory)][object]$Before,
        [Parameter(Mandatory)][object]$After
    )
    return (
        [string]$Before.Sha256 -ceq [string]$After.Sha256 -and
        [int64]$Before.Bytes -eq [int64]$After.Bytes -and
        [int64]$Before.LastWriteUtcTicks -eq [int64]$After.LastWriteUtcTicks
    )
}

function Test-PlacementPreviewDiff {
    param(
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][int]$AcceptedCount
    )
    $escaped = [regex]::Escape('Proposition placements')
    $pattern = "^$escaped · (\d+) → (\d+) objets · 1 ajout\(s\), 0 retrait\(s\), 0 référence\(s\) remplacée\(s\), 0 transformation\(s\), 0 nom\(s\), 0 verrou\(s\), (\d+) objet\(s\) inchangé\(s\) · non appliquée · "
    $match = [regex]::Match($Text, $pattern, [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    return (
        $match.Success -and
        [int]$match.Groups[1].Value -eq $AcceptedCount -and
        [int]$match.Groups[2].Value -eq ($AcceptedCount + 1) -and
        [int]$match.Groups[3].Value -eq $AcceptedCount
    )
}

function New-LoopbackPortReservation {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    return [pscustomobject]@{
        Listener = $listener
        Port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
}

function Assert-CdpTarget {
    param(
        [Parameter(Mandatory)][object]$Target,
        [Parameter(Mandatory)][int]$ExpectedPort
    )
    if ([string]$Target.type -cne 'page') {
        throw "CDP target type must be exactly 'page'."
    }
    if ([string]$Target.title -cne $expectedTargetTitle) {
        throw "CDP target title '$($Target.title)' differs from '$expectedTargetTitle'."
    }
    if ([string]$Target.url -cne $expectedTargetUrl) {
        throw "CDP target URL '$($Target.url)' differs from '$expectedTargetUrl'."
    }
    $webSocketUri = $null
    if (-not [Uri]::TryCreate([string]$Target.webSocketDebuggerUrl, [UriKind]::Absolute, [ref]$webSocketUri)) {
        throw 'CDP webSocketDebuggerUrl is not an absolute URI.'
    }
    if ($webSocketUri.Scheme -cne 'ws' -or $webSocketUri.Host -notin @('127.0.0.1', 'localhost')) {
        throw 'CDP websocket must use ws on loopback.'
    }
    if ($webSocketUri.Port -ne $ExpectedPort) {
        throw "CDP websocket port $($webSocketUri.Port) differs from reserved port $ExpectedPort."
    }
    if (-not $webSocketUri.AbsolutePath.StartsWith('/devtools/page/', [StringComparison]::Ordinal)) {
        throw 'CDP websocket is not a page endpoint.'
    }
    if ($webSocketUri.Query.Length -ne 0 -or $webSocketUri.Fragment.Length -ne 0 -or $webSocketUri.UserInfo.Length -ne 0) {
        throw 'CDP websocket contains unexpected query, fragment, or credentials.'
    }
}

function Wait-CdpTarget {
    param(
        [Parameter(Mandatory)][int]$Port,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [Parameter(Mandatory)][System.Diagnostics.Process]$Process
    )
    $endpoint = "http://127.0.0.1:$Port/json/list"
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastReason = 'endpoint not available'
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if ($Process.HasExited) {
            throw "Studio exited before exposing CDP (exit code $($Process.ExitCode))."
        }
        try {
            $targets = @(Invoke-RestMethod -Uri $endpoint -TimeoutSec 2 -NoProxy)
            $matching = @($targets | Where-Object {
                [string]$_.type -ceq 'page' -and
                [string]$_.title -ceq $expectedTargetTitle -and
                [string]$_.url -ceq $expectedTargetUrl
            })
            if ($matching.Count -gt 1) {
                throw 'More than one exact authoring page target is exposed on the reserved port.'
            }
            if ($matching.Count -eq 1) {
                Assert-CdpTarget -Target $matching[0] -ExpectedPort $Port
                return $matching[0]
            }
            $lastReason = 'exact page URL/title/type not present'
        }
        catch {
            $lastReason = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 200
    }
    throw "Exact authoring CDP target did not appear on loopback port $Port. Last state: $lastReason"
}

function Connect-CdpSocket {
    param([Parameter(Mandatory)][string]$WebSocketUrl)
    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $timeout = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(10))
    try {
        Wait-NonGenericTask -Task ($socket.ConnectAsync([Uri]$WebSocketUrl, $timeout.Token))
        return $socket
    }
    catch {
        $socket.Dispose()
        throw "Unable to connect to CDP websocket: $($_.Exception.Message)"
    }
    finally {
        $timeout.Dispose()
    }
}

function Receive-CdpEnvelope {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][System.Threading.CancellationToken]$CancellationToken
    )
    $stream = [System.IO.MemoryStream]::new()
    try {
        do {
            $buffer = [byte[]]::new(65536)
            $segment = [ArraySegment[byte]]::new($buffer)
            $received = $Socket.ReceiveAsync($segment, $CancellationToken).GetAwaiter().GetResult()
            if ($received.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
                throw 'CDP websocket closed while awaiting a response.'
            }
            if ($received.Count -gt 0) {
                $stream.Write($buffer, 0, $received.Count)
            }
        } while (-not $received.EndOfMessage)
        return ConvertFrom-ExactJson -Json ([System.Text.Encoding]::UTF8.GetString($stream.ToArray()))
    }
    finally {
        $stream.Dispose()
    }
}

function Invoke-CdpCommand {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Method,
        [hashtable]$Parameters = @{},
        [ValidateRange(1, 180)][int]$TimeoutSeconds = 15
    )
    $script:CdpMessageId += 1
    $messageId = $script:CdpMessageId
    $request = [ordered]@{ id = $messageId; method = $Method; params = $Parameters }
    $payload = [System.Text.Encoding]::UTF8.GetBytes((ConvertTo-CompactJson -Value $request))
    $timeout = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($TimeoutSeconds))
    try {
        Wait-NonGenericTask -Task ($Socket.SendAsync(
            [ArraySegment[byte]]::new($payload),
            [System.Net.WebSockets.WebSocketMessageType]::Text,
            $true,
            $timeout.Token
        ))
        $pendingKey = [string]$messageId
        while ($true) {
            if ($script:CdpPendingResponses.ContainsKey($pendingKey)) {
                $envelope = $script:CdpPendingResponses[$pendingKey]
                $script:CdpPendingResponses.Remove($pendingKey)
            }
            else {
                $envelope = Receive-CdpEnvelope -Socket $Socket -CancellationToken $timeout.Token
            }
            if ($envelope.PSObject.Properties.Name -contains 'id') {
                if ([int]$envelope.id -ne $messageId) {
                    $script:CdpPendingResponses[[string]$envelope.id] = $envelope
                    continue
                }
                if ($envelope.PSObject.Properties.Name -contains 'error') {
                    throw "CDP $Method failed: $($envelope.error.message)"
                }
                return $envelope.result
            }
            if ($envelope.PSObject.Properties.Name -contains 'method') {
                if ($script:CdpEvents.Count -lt 2048) {
                    $script:CdpEvents.Add([pscustomobject]@{
                        Method = [string]$envelope.method
                        ReceivedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
                    })
                }
                else {
                    $script:CdpDroppedEventCount += 1
                }
            }
        }
    }
    finally {
        $timeout.Dispose()
    }
}

function Invoke-CdpExpression {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Expression,
        [ValidateRange(1, 180)][int]$TimeoutSeconds = 15
    )
    $response = Invoke-CdpCommand -Socket $Socket -Method 'Runtime.evaluate' -Parameters @{
        expression = $Expression
        returnByValue = $true
        awaitPromise = $true
        userGesture = $true
    } -TimeoutSeconds $TimeoutSeconds
    if ($response.PSObject.Properties.Name -contains 'exceptionDetails') {
        $description = [string]$response.exceptionDetails.text
        if ($response.exceptionDetails.PSObject.Properties.Name -contains 'exception') {
            $description = [string]$response.exceptionDetails.exception.description
        }
        throw "CDP JavaScript exception: $description"
    }
    if ($response.result.PSObject.Properties.Name -contains 'value') {
        return $response.result.value
    }
    if ([string]$response.result.subtype -eq 'null') {
        return $null
    }
    throw "CDP expression returned no serializable value (type '$($response.result.type)')."
}

function Wait-CdpReady {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Expression,
        [Parameter(Mandatory)][string]$Description,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastReason = 'not evaluated'
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $state = Invoke-CdpExpression -Socket $Socket -Expression $Expression
        if ($null -ne $state -and $state.ready -eq $true) {
            return $state
        }
        if ($null -ne $state -and $state.PSObject.Properties.Name -contains 'terminal' -and $state.terminal -eq $true) {
            throw "$Description reached a terminal failure: $($state.reason)"
        }
        if ($null -ne $state -and $state.PSObject.Properties.Name -contains 'reason') {
            $lastReason = [string]$state.reason
        }
        Start-Sleep -Milliseconds 200
    }
    throw "$Description did not become ready within $TimeoutSeconds seconds. Last state: $lastReason"
}

function Send-CdpMouseEvent {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][ValidateSet('mouseMoved', 'mousePressed', 'mouseReleased')][string]$Type,
        [Parameter(Mandatory)][double]$X,
        [Parameter(Mandatory)][double]$Y,
        [int]$Buttons = 0
    )
    $parameters = @{ type = $Type; x = $X; y = $Y; modifiers = 0; buttons = $Buttons; pointerType = 'mouse' }
    if ($Type -ne 'mouseMoved') {
        $parameters.button = 'left'
        $parameters.clickCount = 1
    }
    [void](Invoke-CdpCommand -Socket $Socket -Method 'Input.dispatchMouseEvent' -Parameters $parameters)
}

function Send-CdpMouseGesture {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][double]$StartX,
        [Parameter(Mandatory)][double]$StartY,
        [Parameter(Mandatory)][double]$EndX,
        [Parameter(Mandatory)][double]$EndY,
        [ValidateRange(1, 20)][int]$Steps = 6,
        [switch]$HoldOpen
    )
    Send-CdpMouseEvent -Socket $Socket -Type mouseMoved -X $StartX -Y $StartY
    Send-CdpMouseEvent -Socket $Socket -Type mousePressed -X $StartX -Y $StartY -Buttons 1
    for ($index = 1; $index -le $Steps; $index += 1) {
        $ratio = $index / [double]$Steps
        Send-CdpMouseEvent -Socket $Socket -Type mouseMoved -X ($StartX + (($EndX - $StartX) * $ratio)) -Y ($StartY + (($EndY - $StartY) * $ratio)) -Buttons 1
    }
    if (-not $HoldOpen) {
        Send-CdpMouseEvent -Socket $Socket -Type mouseReleased -X $EndX -Y $EndY
    }
}

function Send-CdpEscape {
    param([Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket)
    $key = @{ key = 'Escape'; code = 'Escape'; windowsVirtualKeyCode = 27; nativeVirtualKeyCode = 27; modifiers = 0 }
    [void](Invoke-CdpCommand -Socket $Socket -Method 'Input.dispatchKeyEvent' -Parameters (@{ type = 'rawKeyDown' } + $key))
    [void](Invoke-CdpCommand -Socket $Socket -Method 'Input.dispatchKeyEvent' -Parameters (@{ type = 'keyUp' } + $key))
}

function Save-CdpScreenshot {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$LiteralPath
    )
    $result = Invoke-CdpCommand -Socket $Socket -Method 'Page.captureScreenshot' -Parameters @{
        format = 'png'
        fromSurface = $true
        captureBeyondViewport = $false
    } -TimeoutSeconds 30
    $bytes = [Convert]::FromBase64String([string]$result.data)
    if ($bytes.Length -lt 1024) {
        throw 'CDP screenshot payload is unexpectedly small.'
    }
    [System.IO.File]::WriteAllBytes($LiteralPath, $bytes)
    return [pscustomobject]@{
        Path = [System.IO.Path]::GetFullPath($LiteralPath)
        Bytes = [int64]$bytes.Length
        Sha256 = Get-FileSha256 -LiteralPath $LiteralPath
        Method = 'Page.captureScreenshot'
    }
}

function Test-CdpListenerClosed {
    param([Parameter(Mandatory)][int]$Port)
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $connect = $client.ConnectAsync('127.0.0.1', $Port)
        try {
            if ($connect.Wait(250) -and $client.Connected) {
                return $false
            }
        }
        catch {
            return $true
        }
        return $true
    }
    finally {
        $client.Dispose()
    }
}

function Wait-CdpListenerClosed {
    param(
        [Parameter(Mandatory)][int]$Port,
        [ValidateRange(1, 30)][int]$TimeoutSeconds = 10
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (Test-CdpListenerClosed -Port $Port) {
            return $true
        }
        Start-Sleep -Milliseconds 200
    }
    return $false
}

function Invoke-SelfTest {
    if ((Get-NormalizedSha256 -Value ('A' * 64)) -cne ('a' * 64)) {
        throw 'SHA-256 normalization self-test failed.'
    }
    if (-not (Test-IsDriveEPath -LiteralPath 'E:\proof\fresh') -or (Test-IsDriveEPath -LiteralPath 'C:\proof\fresh')) {
        throw 'E: path policy self-test failed.'
    }
    $diff = "Proposition placements · 2 → 3 objets · 1 ajout(s), 0 retrait(s), 0 référence(s) remplacée(s), 0 transformation(s), 0 nom(s), 0 verrou(s), 2 objet(s) inchangé(s) · non appliquée · chargement de l'aperçu 3D demandé ; consulter son état"
    if (-not (Test-PlacementPreviewDiff -Text $diff -AcceptedCount 2)) {
        throw 'Placement preview diff self-test failed.'
    }
    $before = [pscustomobject]@{ Sha256 = ('a' * 64); Bytes = 12; LastWriteUtcTicks = 34 }
    $after = [pscustomobject]@{ Sha256 = ('a' * 64); Bytes = 12; LastWriteUtcTicks = 34 }
    if (-not (Test-ArchiveSnapshotsEqual -Before $before -After $after)) {
        throw 'Archive snapshot comparison self-test failed.'
    }
    $ipcBody = '{"command":"{\"command\":\"authoring\",\"request\":{\"action\":\"preview_zone\",\"request\":{}}}"}'
    if ((Get-AuditedRequestKindFromIpcBody -Body $ipcBody) -cne 'preview_zone') {
        throw 'Tauri IPC request classification self-test failed.'
    }
    Write-Output 'GATE2_AUTHORING_DROP_PROOF_SELF_TEST_PASS'
}

if ($SelfTest) {
    Invoke-SelfTest
    return
}

if (-not $IsWindows) {
    throw 'This proof harness requires Windows, WebView2, and a real Tauri candidate.'
}

$resolvedCandidate = [System.IO.Path]::GetFullPath($CandidateExecutable)
if (-not (Test-Path -LiteralPath $resolvedCandidate -PathType Leaf)) {
    throw "Candidate executable not found: $resolvedCandidate"
}
$expectedHash = Get-NormalizedSha256 -Value $ExpectedSha256
$actualCandidateHash = Get-FileSha256 -LiteralPath $resolvedCandidate
if ($actualCandidateHash -cne $expectedHash) {
    throw "Candidate SHA-256 mismatch. Expected $expectedHash, got $actualCandidateHash."
}

$resolvedAuthoringRoot = [System.IO.Path]::GetFullPath($AuthoringRoot)
if (-not (Test-Path -LiteralPath $resolvedAuthoringRoot -PathType Container)) {
    throw "Authoring root not found: $resolvedAuthoringRoot"
}
$resolvedAuthoringRoot = (Get-Item -LiteralPath $resolvedAuthoringRoot).FullName
$zoneId = $ZoneUuid.ToString('D').ToLowerInvariant()
$archivePath = Join-Path (Join-Path $resolvedAuthoringRoot $zoneId) 'authoring.json'
$archiveBefore = Get-ArchiveSnapshot -LiteralPath $archivePath

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
Assert-DriveEPath -LiteralPath $resolvedOutput -Description 'OutputDirectory'
Assert-NoExistingReparseAncestor -LiteralPath $resolvedOutput -Description 'OutputDirectory'
if (Test-PathWithinDirectory -LiteralPath $resolvedOutput -Directory $resolvedAuthoringRoot) {
    throw 'OutputDirectory must be outside AuthoringRoot so proof artifacts cannot contaminate source data.'
}
if (Test-Path -LiteralPath $resolvedOutput) {
    throw "OutputDirectory must be fresh and absent before the run: $resolvedOutput"
}
[void](New-Item -ItemType Directory -Path $resolvedOutput)
$resolvedOutput = (Get-Item -LiteralPath $resolvedOutput).FullName

if ([string]::IsNullOrWhiteSpace($WebView2UserDataDirectory)) {
    $resolvedUserData = Join-Path $resolvedOutput 'webview2-user-data'
}
else {
    $resolvedUserData = [System.IO.Path]::GetFullPath($WebView2UserDataDirectory)
}
Assert-DriveEPath -LiteralPath $resolvedUserData -Description 'WebView2UserDataDirectory'
Assert-NoExistingReparseAncestor -LiteralPath $resolvedUserData -Description 'WebView2UserDataDirectory'
if (Test-PathWithinDirectory -LiteralPath $resolvedUserData -Directory $resolvedAuthoringRoot) {
    throw 'WebView2UserDataDirectory must be outside AuthoringRoot.'
}
if (Test-Path -LiteralPath $resolvedUserData) {
    throw "WebView2UserDataDirectory must be fresh and absent before the run: $resolvedUserData"
}
[void](New-Item -ItemType Directory -Path $resolvedUserData)
$resolvedUserData = (Get-Item -LiteralPath $resolvedUserData).FullName

$ledgerPath = Join-Path $resolvedOutput 'R2-027-CDP-ledger.json'
$reportPath = Join-Path $resolvedOutput 'R2-027-CDP-REPORT.md'
$positiveScreenshotPath = Join-Path $resolvedOutput 'r2-027-positive-preview.png'
$finalScreenshotPath = Join-Path $resolvedOutput 'r2-027-final-cancelled.png'

$process = $null
$socket = $null
$reservation = $null
$cdpTarget = $null
$devToolsPort = 0
$auditInstalled = $false
$auditRestored = $false
$processClosed = $false
$gracefulCloseRequested = $false
$gracefulCloseAccepted = $false
$forcedOwnPid = $false
$listenerClosed = $false
$environmentRestored = $false
$proofFailure = $null
$initialUi = $null
$auditProbeState = $null
$positiveState = $null
$cancelledState = $null
$auditSnapshot = $null
$positiveScreenshot = $null
$finalScreenshot = $null
$negativeResults = [System.Collections.Generic.List[object]]::new()
$startedAt = [DateTimeOffset]::UtcNow

$trackedEnvironment = @(
    'FORGE_AUTHORING_ROOT',
    'FORGE_AUTHOR_SUBJECT',
    'WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS',
    'WEBVIEW2_USER_DATA_FOLDER'
)
$previousEnvironment = @{}
foreach ($name in $trackedEnvironment) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

$zoneLiteral = ConvertTo-CompactJson -Value $zoneId
$assetLiteral = ConvertTo-CompactJson -Value $assetId

$openZoneExpression = @"
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const button = [...document.querySelectorAll('button')].find(node => norm(node.textContent) === 'Actualiser la liste');
  if (!button) return { ready: false, reason: 'refresh button absent' };
  button.click();
  return { ready: true, reason: 'refresh requested' };
})()
"@

$zoneListedExpression = @"
(() => {
  const zone = $zoneLiteral;
  const labels = [...document.querySelectorAll('label')];
  const label = labels.find(node => (node.textContent ?? '').replace(/\s+/g, ' ').trim().startsWith('Choisir une zone'));
  const select = label?.querySelector('select');
  if (!select) return { ready: false, reason: 'zone select absent' };
  const option = [...select.options].find(item => item.value.toLowerCase() === zone);
  if (!option) return { ready: false, reason: 'exact zone UUID not listed' };
  if (option.disabled) return { ready: false, terminal: true, reason: 'exact zone is listed as unavailable' };
  return { ready: true, reason: 'exact zone listed' };
})()
"@

$selectAndOpenExpression = @"
(async () => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const zone = $zoneLiteral;
  const label = [...document.querySelectorAll('label')].find(node => norm(node.textContent).startsWith('Choisir une zone'));
  const select = label?.querySelector('select');
  const open = [...document.querySelectorAll('button')].find(node => norm(node.textContent) === 'Ouvrir la zone choisie');
  if (!select || !open) return { ready: false, reason: 'zone controls absent' };
  select.value = zone;
  select.dispatchEvent(new Event('change', { bubbles: true }));
  if (select.value.toLowerCase() !== zone) return { ready: false, terminal: true, reason: 'exact UUID selection refused' };
  await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  if (open.disabled) return { ready: false, terminal: true, reason: 'open button stayed disabled after exact UUID selection' };
  open.click();
  return { ready: true, reason: 'open requested' };
})()
"@

$uiReadyExpression = @"
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const zone = $zoneLiteral;
  const canvas = document.getElementById('forge-authoring-canvas');
  const zoneTerm = [...document.querySelectorAll('.authoring-zone-information dt')].find(node => norm(node.textContent) === 'Zone');
  const shownZone = norm(zoneTerm?.nextElementSibling?.textContent).toLowerCase();
  const mode = [...document.querySelectorAll('output')].map(node => norm(node.textContent)).find(text => text.startsWith('Mode ')) ?? '';
  const placementLegend = [...document.querySelectorAll('legend')].find(node => norm(node.textContent) === 'Placement manuel dans la zone');
  const placementFieldset = placementLegend?.closest('fieldset');
  const diff = document.querySelector('.authoring-command-diff');
  const apply = document.querySelector('.authoring-apply');
  const width = canvas?.clientWidth ?? 0;
  const height = canvas?.clientHeight ?? 0;
  const ready = shownZone === zone && mode.startsWith('Mode édition') && width > 0 && height > 0 && placementFieldset && !placementFieldset.disabled && apply && apply.disabled && (!diff || diff.hidden || norm(diff.textContent) === '');
  return { ready: !!ready, reason: 'zone=' + shownZone + '; mode=' + mode + '; canvas=' + width + 'x' + height + '; placementDisabled=' + placementFieldset?.disabled + '; applyDisabled=' + apply?.disabled + '; diff=' + norm(diff?.textContent), shownZone, mode, width, height };
})()
"@

$loadCatalogExpression = @"
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const summary = [...document.querySelectorAll('summary')].find(node => norm(node.textContent) === 'Bibliothèque d’assets');
  const details = summary?.closest('details');
  if (!details) return { ready: false, reason: 'asset library absent' };
  details.open = true;
  const button = [...details.querySelectorAll('button')].find(node => norm(node.textContent) === 'Charger le catalogue local');
  if (!button || button.disabled) return { ready: false, reason: 'catalog load button unavailable' };
  button.click();
  return { ready: true, reason: 'catalog load requested' };
})()
"@

$catalogReadyExpression = @"
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const asset = $assetLiteral;
  const card = [...document.querySelectorAll('button')].find(node => norm(node.textContent).startsWith(asset + ' · '));
  const refusal = [...document.querySelectorAll('output')].map(node => norm(node.textContent)).find(text => text.startsWith('Catalogue indisponible ou refusé'));
  if (refusal) return { ready: false, terminal: true, reason: refusal };
  return { ready: !!card, reason: card ? 'stable asset present' : 'stable asset absent' };
})()
"@

$selectAssetExpression = @"
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const asset = $assetLiteral;
  const card = [...document.querySelectorAll('button')].find(node => norm(node.textContent).startsWith(asset + ' · '));
  if (!card) return { ready: false, reason: 'stable asset card absent' };
  card.click();
  const selected = [...document.querySelectorAll('section[aria-label="Fiche asset"] h3')].some(node => norm(node.textContent) === asset);
  return { ready: selected, reason: selected ? 'stable asset selected' : 'asset selection not reflected' };
})()
"@

$installAuditExpression = @"
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const core = window.__TAURI__?.core;
  if (!core || typeof core.invoke !== 'function') throw new Error('Tauri core.invoke unavailable');
  if (window.__forgeGate2DropAudit) throw new Error('Gate 2 invoke audit already installed');
  const placementLabel = [...document.querySelectorAll('label')].find(node => norm(node.textContent).startsWith('Objet placé à inspecter'));
  const placementSelect = placementLabel?.querySelector('select');
  if (!placementSelect) throw new Error('Accepted placement select absent');
  const acceptedOptions = [...placementSelect.options].slice(1).map(option => ({ value: option.value, text: norm(option.textContent) }));
  const coreInvokeDescriptor = Object.getOwnPropertyDescriptor(core, 'invoke');
  const fetchDescriptor = Object.getOwnPropertyDescriptor(window, 'fetch');
  const originalFetch = window.fetch;
  if (typeof originalFetch !== 'function') throw new Error('window.fetch unavailable for IPC audit');
  if (fetchDescriptor && !fetchDescriptor.writable && !fetchDescriptor.configurable) {
    throw new Error('window.fetch cannot be replaced and restored safely');
  }
  const audit = {
    mechanism: 'window.fetch exact Tauri IPC endpoint',
    originalFetch,
    fetchDescriptor,
    hadOwnFetch: !!fetchDescriptor,
    coreFrozen: Object.isFrozen(core),
    coreInvokeDescriptor: coreInvokeDescriptor ? {
      writable: !!coreInvokeDescriptor.writable,
      configurable: !!coreInvokeDescriptor.configurable,
      enumerable: !!coreInvokeDescriptor.enumerable
    } : null,
    entries: [],
    acceptedOptions
  };
  const wrapper = function(...args) {
    const input = args[0];
    const init = args[1] ?? {};
    const rawUrl = typeof input === 'string' || input instanceof URL ? String(input) : String(input?.url ?? '');
    let endpoint = false;
    try {
      const url = new URL(rawUrl, window.location.href);
      endpoint = url.pathname === '/execute_studio_command' && (url.hostname === 'ipc.localhost' || url.protocol === 'ipc:');
    } catch {}
    const command = endpoint ? 'execute_studio_command' : null;
    let wire = null;
    let requestKind = null;
    let parseError = null;
    if (endpoint && typeof init?.body === 'string') {
      try {
        const payload = JSON.parse(init.body);
        wire = typeof payload?.command === 'string' ? JSON.parse(payload.command) : null;
        requestKind = wire?.command === 'authoring' && typeof wire?.request?.action === 'string'
          ? wire.request.action
          : null;
      } catch (error) {
        parseError = String(error);
      }
    }
    const entry = endpoint ? {
      index: audit.entries.length,
      transport: 'fetch',
      url: rawUrl,
      method: String(init?.method ?? 'GET').toUpperCase(),
      command,
      request_kind: requestKind,
      wire,
      parse_error: parseError,
      started_at_ms: performance.now(),
      outcome: 'pending'
    } : null;
    if (entry) audit.entries.push(entry);
    try {
      const result = audit.originalFetch.apply(window, args);
      return Promise.resolve(result).then(
        value => { if (entry) { entry.outcome = 'resolved'; entry.completed_at_ms = performance.now(); entry.http_status = value?.status ?? null; } return value; },
        error => { if (entry) { entry.outcome = 'rejected'; entry.completed_at_ms = performance.now(); entry.error = String(error); } throw error; }
      );
    } catch (error) {
      if (entry) { entry.outcome = 'threw'; entry.completed_at_ms = performance.now(); entry.error = String(error); }
      throw error;
    }
  };
  audit.wrapper = wrapper;
  try {
    if (fetchDescriptor) {
      const replacementDescriptor = Object.prototype.hasOwnProperty.call(fetchDescriptor, 'value')
        ? { ...fetchDescriptor, value: wrapper }
        : { value: wrapper, writable: true, configurable: true, enumerable: !!fetchDescriptor.enumerable };
      Object.defineProperty(window, 'fetch', replacementDescriptor);
    } else {
      Object.defineProperty(window, 'fetch', { value: wrapper, writable: true, configurable: true, enumerable: true });
    }
    if (window.fetch !== wrapper) throw new Error('Tauri IPC fetch audit installation did not stick');
    window.__forgeGate2DropAudit = audit;
    if (window.__forgeGate2DropAudit !== audit) throw new Error('Tauri IPC audit state installation did not stick');
  } catch (error) {
    try {
      if (audit.hadOwnFetch) Object.defineProperty(window, 'fetch', audit.fetchDescriptor);
      else delete window.fetch;
    } catch (restoreError) {
      throw new Error('Tauri IPC audit installation failed and rollback failed: ' + error + ' / ' + restoreError);
    }
    throw error;
  }
  return {
    installed: true,
    mechanism: audit.mechanism,
    acceptedOptions,
    coreFrozen: audit.coreFrozen,
    coreInvokeDescriptor: audit.coreInvokeDescriptor,
    fetchDescriptor: fetchDescriptor ? {
      writable: !!fetchDescriptor.writable,
      configurable: !!fetchDescriptor.configurable,
      enumerable: !!fetchDescriptor.enumerable
    } : null
  };
})()
"@

$auditProbeExpression = @'
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const button = [...document.querySelectorAll('button')].find(node => norm(node.textContent) === 'Relire la session native');
  if (!button || button.disabled) return { ready: false, terminal: true, reason: 'read-only native session probe unavailable' };
  button.click();
  return { ready: true, reason: 'read-only native session probe requested' };
})()
'@

$auditProbeReadyExpression = @'
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const audit = window.__forgeGate2DropAudit;
  if (!audit) return { ready: false, terminal: true, reason: 'IPC audit absent' };
  const malformed = audit.entries.filter(entry => entry.command === 'execute_studio_command' && (entry.parse_error || !entry.wire?.command));
  const authoring = audit.entries.filter(entry => entry.command === 'execute_studio_command' && entry.wire?.command === 'authoring');
  const unclassified = authoring.filter(entry => typeof entry.request_kind !== 'string');
  if (malformed.length || unclassified.length) return { ready: false, terminal: true, reason: 'read-only audit probe was malformed or unclassified' };
  const inspect = authoring.filter(entry => entry.request_kind === 'inspect');
  if (authoring.some(entry => ['preview_zone', 'apply_zone', 'save'].includes(entry.request_kind))) return { ready: false, terminal: true, reason: 'forbidden mutation command observed during read-only audit probe' };
  const placementLabel = [...document.querySelectorAll('label')].find(node => norm(node.textContent).startsWith('Objet placé à inspecter'));
  const select = placementLabel?.querySelector('select');
  const accepted = select ? [...select.options].slice(1).map(option => ({ value: option.value, text: norm(option.textContent) })) : [];
  const acceptedSame = JSON.stringify(accepted) === JSON.stringify(audit.acceptedOptions);
  const diff = document.querySelector('.authoring-command-diff');
  const apply = document.querySelector('.authoring-apply');
  const diffText = norm(diff?.textContent);
  const cleanUi = (!diff || diff.hidden || diffText === '') && (!apply || apply.disabled) && acceptedSame;
  const ready = inspect.length === 1 && inspect[0].outcome === 'resolved' && cleanUi;
  return { ready, reason: `inspect=${inspect.length}; outcome=${inspect[0]?.outcome}; cleanUi=${cleanUi}`, inspect_count: inspect.length, inspect_outcome: inspect[0]?.outcome ?? null, accepted_unchanged: acceptedSame, audit_valid: true };
})()
'@

$proofStateExpression = @'
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const audit = window.__forgeGate2DropAudit;
  if (!audit) return { ready: false, terminal: true, reason: 'invoke audit absent' };
  const placementLabel = [...document.querySelectorAll('label')].find(node => norm(node.textContent).startsWith('Objet placé à inspecter'));
  const placementSelect = placementLabel?.querySelector('select');
  const acceptedOptions = placementSelect ? [...placementSelect.options].slice(1).map(option => ({ value: option.value, text: norm(option.textContent) })) : [];
  const malformed = audit.entries.filter(entry => entry.command === 'execute_studio_command' && (entry.parse_error || !entry.wire?.command));
  const authoring = audit.entries.filter(entry => entry.command === 'execute_studio_command' && entry.wire?.command === 'authoring');
  const unclassifiedAuthoring = authoring.filter(entry => typeof entry.request_kind !== 'string');
  if (malformed.length || unclassifiedAuthoring.length) return { ready: false, terminal: true, reason: 'malformed or unclassified Tauri IPC audit entry' };
  const count = kind => authoring.filter(entry => entry.request_kind === kind).length;
  const diff = document.querySelector('.authoring-command-diff');
  const diffText = norm(diff?.textContent);
  const apply = document.querySelector('.authoring-apply');
  return {
    ready: true,
    reason: 'state sampled',
    audit_entries: audit.entries.length,
    authoring_entries: authoring.length,
    preview_zone: count('preview_zone'),
    apply_zone: count('apply_zone'),
    save: count('save'),
    diff_text: diffText,
    diff_hidden: !diff || diff.hidden || diffText === '',
    apply_disabled: !apply || apply.disabled,
    accepted_options: acceptedOptions,
    accepted_unchanged: JSON.stringify(acceptedOptions) === JSON.stringify(audit.acceptedOptions),
    audit_valid: true
  };
})()
'@

$geometryExpression = @"
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const asset = $assetLiteral;
  const card = [...document.querySelectorAll('button')].find(node => norm(node.textContent).startsWith(asset + ' · '));
  const canvas = document.getElementById('forge-authoring-canvas');
  const topbar = document.querySelector('.authoring-shell > .topbar');
  if (!card || !canvas || !topbar) return { ready: false, reason: 'gesture geometry nodes absent' };
  card.scrollIntoView({ block: 'center', inline: 'nearest' });
  const source = card.getBoundingClientRect();
  const target = canvas.getBoundingClientRect();
  const outside = topbar.getBoundingClientRect();
  const valid = source.width > 10 && source.height > 10 && target.width > 0 && target.height > 0 && outside.width > 0 && outside.height > 0;
  return {
    ready: valid,
    reason: valid ? 'geometry ready' : 'non-positive gesture geometry',
    source: { x: source.left + source.width / 2, y: source.top + source.height / 2, width: source.width, height: source.height },
    canvas: { x: target.left + target.width * 0.5, y: target.top + target.height * 0.55, width: target.width, height: target.height },
    outside: { x: outside.left + outside.width * 0.5, y: outside.top + outside.height * 0.5 }
  };
})()
"@

$positiveReadyExpression = @'
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const audit = window.__forgeGate2DropAudit;
  if (!audit) return { ready: false, terminal: true, reason: 'invoke audit absent' };
  const malformed = audit.entries.filter(entry => entry.command === 'execute_studio_command' && (entry.parse_error || !entry.wire?.command));
  const authoring = audit.entries.filter(entry => entry.command === 'execute_studio_command' && entry.wire?.command === 'authoring');
  const unclassifiedAuthoring = authoring.filter(entry => typeof entry.request_kind !== 'string');
  if (malformed.length || unclassifiedAuthoring.length) return { ready: false, terminal: true, reason: 'malformed or unclassified Tauri IPC audit entry' };
  const count = kind => authoring.filter(entry => entry.request_kind === kind).length;
  const diff = document.querySelector('.authoring-command-diff');
  const text = norm(diff?.textContent);
  const apply = document.querySelector('.authoring-apply');
  const placementLabel = [...document.querySelectorAll('label')].find(node => norm(node.textContent).startsWith('Objet placé à inspecter'));
  const select = placementLabel?.querySelector('select');
  const accepted = select ? [...select.options].slice(1).map(option => ({ value: option.value, text: norm(option.textContent) })) : [];
  const same = JSON.stringify(accepted) === JSON.stringify(audit.acceptedOptions);
  const projection = [...document.querySelectorAll('output')].map(node => norm(node.textContent)).find(value => value.startsWith('Placements :') || value.startsWith('Placements non projetés') || value.includes(' placements LOD0 projetés')) ?? '';
  if (projection.startsWith('Placements non projetés')) return { ready: false, terminal: true, reason: projection };
  const ready = count('preview_zone') === 1 && count('apply_zone') === 0 && count('save') === 0 && !diff.hidden && text.startsWith('Proposition placements · ') && text.includes('1 ajout(s)') && text.includes('non appliquée') && apply && !apply.disabled && same;
  return { ready: !!ready, reason: `preview=${count('preview_zone')}; apply=${count('apply_zone')}; save=${count('save')}; diff=${text}; applyDisabled=${apply?.disabled}; acceptedSame=${same}; projection=${projection}`, preview_zone: count('preview_zone'), apply_zone: count('apply_zone'), save: count('save'), diff_text: text, apply_disabled: apply?.disabled ?? true, accepted_options: accepted, accepted_unchanged: same, projection_status: projection };
})()
'@

$cancelReadyExpression = @'
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const audit = window.__forgeGate2DropAudit;
  if (!audit) return { ready: false, terminal: true, reason: 'invoke audit absent' };
  const malformed = audit.entries.filter(entry => entry.command === 'execute_studio_command' && (entry.parse_error || !entry.wire?.command));
  const authoring = audit.entries.filter(entry => entry.command === 'execute_studio_command' && entry.wire?.command === 'authoring');
  const unclassifiedAuthoring = authoring.filter(entry => typeof entry.request_kind !== 'string');
  if (malformed.length || unclassifiedAuthoring.length) return { ready: false, terminal: true, reason: 'malformed or unclassified Tauri IPC audit entry' };
  const count = kind => authoring.filter(entry => entry.request_kind === kind).length;
  const diff = document.querySelector('.authoring-command-diff');
  const text = norm(diff?.textContent);
  const apply = document.querySelector('.authoring-apply');
  const placementLabel = [...document.querySelectorAll('label')].find(node => norm(node.textContent).startsWith('Objet placé à inspecter'));
  const select = placementLabel?.querySelector('select');
  const accepted = select ? [...select.options].slice(1).map(option => ({ value: option.value, text: norm(option.textContent) })) : [];
  const same = JSON.stringify(accepted) === JSON.stringify(audit.acceptedOptions);
  const hidden = !diff || diff.hidden || text === '';
  const projection = [...document.querySelectorAll('output')].map(node => norm(node.textContent)).find(value => value.startsWith('Aucun placement manuel.') || value.startsWith('Placements :') || value.startsWith('Placements non projetés') || value.includes(' placements LOD0 projetés')) ?? '';
  if (projection.startsWith('Placements non projetés')) return { ready: false, terminal: true, reason: projection };
  const ready = count('preview_zone') === 1 && count('apply_zone') === 0 && count('save') === 0 && hidden && (!apply || apply.disabled) && same;
  return { ready: !!ready, reason: `preview=${count('preview_zone')}; apply=${count('apply_zone')}; save=${count('save')}; diffHidden=${hidden}; applyDisabled=${apply?.disabled}; acceptedSame=${same}; projection=${projection}`, preview_zone: count('preview_zone'), apply_zone: count('apply_zone'), save: count('save'), diff_text: text, diff_hidden: hidden, apply_disabled: !apply || apply.disabled, accepted_options: accepted, accepted_unchanged: same, projection_status: projection };
})()
'@

$auditSnapshotExpression = @'
(() => {
  const audit = window.__forgeGate2DropAudit;
  if (!audit) throw new Error('invoke audit absent');
  return {
    mechanism: audit.mechanism,
    core_frozen: audit.coreFrozen,
    core_invoke_descriptor: audit.coreInvokeDescriptor,
    fetch_descriptor: audit.fetchDescriptor ? {
      writable: !!audit.fetchDescriptor.writable,
      configurable: !!audit.fetchDescriptor.configurable,
      enumerable: !!audit.fetchDescriptor.enumerable
    } : null,
    entries: audit.entries.map(entry => ({ ...entry })),
    accepted_options: audit.acceptedOptions.map(item => ({ ...item }))
  };
})()
'@

$restoreAuditExpression = @'
(() => {
  const audit = window.__forgeGate2DropAudit;
  if (!audit) throw new Error('invoke audit cannot be restored');
  if (audit.hadOwnFetch) {
    Object.defineProperty(window, 'fetch', audit.fetchDescriptor);
  } else {
    if (!delete window.fetch) throw new Error('temporary own window.fetch could not be deleted');
  }
  if (window.fetch !== audit.originalFetch) throw new Error('original window.fetch was not restored');
  delete window.__forgeGate2DropAudit;
  return { restored: true, mechanism: audit.mechanism };
})()
'@

function Assert-NegativeState {
    param(
        [Parameter(Mandatory)][object]$State,
        [Parameter(Mandatory)][string]$Name
    )
    if (-not $State.audit_valid) {
        throw "$Name produced an invalid or unclassified Tauri IPC audit entry."
    }
    if ([int]$State.preview_zone -ne 0 -or [int]$State.apply_zone -ne 0 -or [int]$State.save -ne 0) {
        throw "$Name issued a forbidden PreviewZone, ApplyZone, or Save command."
    }
    if (-not $State.diff_hidden -or -not $State.apply_disabled) {
        throw "$Name exposed a preview diff or enabled Apply."
    }
    if (-not $State.accepted_unchanged) {
        throw "$Name mutated the accepted placement options."
    }
}

try {
    $reservation = New-LoopbackPortReservation
    $devToolsPort = $reservation.Port
    $browserArguments = "--remote-debugging-address=127.0.0.1 --remote-debugging-port=$devToolsPort"
    [Environment]::SetEnvironmentVariable('FORGE_AUTHORING_ROOT', $resolvedAuthoringRoot, 'Process')
    [Environment]::SetEnvironmentVariable('FORGE_AUTHOR_SUBJECT', $authorSubject, 'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', $browserArguments, 'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER', $resolvedUserData, 'Process')
    try {
        $reservation.Listener.Stop()
        $process = Start-Process -FilePath $resolvedCandidate -PassThru -WindowStyle Hidden
    }
    finally {
        $reservation.Listener.Stop()
    }

    $process.Refresh()
    $launchedPath = [System.IO.Path]::GetFullPath($process.Path)
    if (-not $launchedPath.Equals($resolvedCandidate, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Launched path '$launchedPath' differs from candidate '$resolvedCandidate'."
    }
    if ((Get-FileSha256 -LiteralPath $launchedPath) -cne $expectedHash) {
        throw 'Launched executable no longer matches the exact expected SHA-256.'
    }

    $cdpTarget = Wait-CdpTarget -Port $devToolsPort -TimeoutSeconds $StartupTimeoutSeconds -Process $process
    $socket = Connect-CdpSocket -WebSocketUrl ([string]$cdpTarget.webSocketDebuggerUrl)
    [void](Invoke-CdpCommand -Socket $socket -Method 'Runtime.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.enable')
    [void](Wait-CdpReady -Socket $socket -Description 'Authoring document' -TimeoutSeconds $StartupTimeoutSeconds -Expression @'
(() => ({ ready: document.readyState === 'complete' && !!document.querySelector('.authoring-shell'), reason: `readyState=${document.readyState}; shell=${!!document.querySelector('.authoring-shell')}` }))()
'@)

    [void](Invoke-CdpExpression -Socket $socket -Expression $openZoneExpression)
    [void](Wait-CdpReady -Socket $socket -Expression $zoneListedExpression -Description 'Exact zone catalogue entry' -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Invoke-CdpExpression -Socket $socket -Expression $selectAndOpenExpression)
    $initialUi = Wait-CdpReady -Socket $socket -Expression $uiReadyExpression -Description 'Exact zone edit mode and canvas' -TimeoutSeconds $InteractionTimeoutSeconds

    [void](Invoke-CdpExpression -Socket $socket -Expression $loadCatalogExpression)
    [void](Wait-CdpReady -Socket $socket -Expression $catalogReadyExpression -Description 'Governed asset catalogue' -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Wait-CdpReady -Socket $socket -Expression $selectAssetExpression -Description 'Stable asset selection' -TimeoutSeconds 10)
    $auditInstallation = Invoke-CdpExpression -Socket $socket -Expression $installAuditExpression
    if (-not $auditInstallation.installed) {
        throw 'Temporary Tauri invoke audit was not installed.'
    }
    $auditInstalled = $true
    $acceptedPlacementCount = @($auditInstallation.acceptedOptions).Count
    [void](Invoke-CdpExpression -Socket $socket -Expression $auditProbeExpression)
    $auditProbeState = Wait-CdpReady -Socket $socket -Expression $auditProbeReadyExpression -Description 'Read-only Tauri IPC audit probe' -TimeoutSeconds $InteractionTimeoutSeconds

    $geometry = Wait-CdpReady -Socket $socket -Expression $geometryExpression -Description 'Drop gesture geometry' -TimeoutSeconds 10
    Send-CdpMouseGesture -Socket $socket -StartX $geometry.source.x -StartY $geometry.source.y -EndX ($geometry.source.x + 5) -EndY $geometry.source.y -Steps 1
    Start-Sleep -Milliseconds 800
    $state = Invoke-CdpExpression -Socket $socket -Expression $proofStateExpression
    Assert-NegativeState -State $state -Name 'Sub-six-pixel gesture'
    $negativeResults.Add([pscustomobject]@{ Name = 'sub_6px'; Passed = $true; State = $state })

    $geometry = Wait-CdpReady -Socket $socket -Expression $geometryExpression -Description 'Off-canvas gesture geometry' -TimeoutSeconds 10
    Send-CdpMouseGesture -Socket $socket -StartX $geometry.source.x -StartY $geometry.source.y -EndX $geometry.outside.x -EndY $geometry.outside.y -Steps 6
    Start-Sleep -Milliseconds 800
    $state = Invoke-CdpExpression -Socket $socket -Expression $proofStateExpression
    Assert-NegativeState -State $state -Name 'Off-canvas gesture'
    $negativeResults.Add([pscustomobject]@{ Name = 'off_canvas'; Passed = $true; State = $state })

    $geometry = Wait-CdpReady -Socket $socket -Expression $geometryExpression -Description 'Escape gesture geometry' -TimeoutSeconds 10
    Send-CdpMouseGesture -Socket $socket -StartX $geometry.source.x -StartY $geometry.source.y -EndX $geometry.canvas.x -EndY $geometry.canvas.y -Steps 3 -HoldOpen
    Send-CdpEscape -Socket $socket
    Send-CdpMouseEvent -Socket $socket -Type mouseReleased -X $geometry.canvas.x -Y $geometry.canvas.y
    Start-Sleep -Milliseconds 800
    $state = Invoke-CdpExpression -Socket $socket -Expression $proofStateExpression
    Assert-NegativeState -State $state -Name 'Escape mid-gesture'
    $negativeResults.Add([pscustomobject]@{ Name = 'escape_mid_gesture'; Passed = $true; State = $state })

    $geometry = Wait-CdpReady -Socket $socket -Expression $geometryExpression -Description 'Positive gesture geometry' -TimeoutSeconds 10
    Send-CdpMouseGesture -Socket $socket -StartX $geometry.source.x -StartY $geometry.source.y -EndX $geometry.canvas.x -EndY $geometry.canvas.y -Steps 8
    $positiveState = Wait-CdpReady -Socket $socket -Expression $positiveReadyExpression -Description 'Single placement preview' -TimeoutSeconds $InteractionTimeoutSeconds
    if (-not (Test-PlacementPreviewDiff -Text ([string]$positiveState.diff_text) -AcceptedCount $acceptedPlacementCount)) {
        throw "Placement preview diff is not the exact one-addition shape for $acceptedPlacementCount accepted placements: $($positiveState.diff_text)"
    }
    if ([int]$positiveState.preview_zone -ne 1 -or [int]$positiveState.apply_zone -ne 0 -or [int]$positiveState.save -ne 0) {
        throw 'Positive gesture did not produce exactly one PreviewZone and zero ApplyZone/Save commands.'
    }
    if (-not [bool]$positiveState.accepted_unchanged -or [bool]$positiveState.apply_disabled) {
        throw 'Positive gesture did not preserve accepted placements with Apply enabled.'
    }
    $positiveScreenshot = Save-CdpScreenshot -Socket $socket -LiteralPath $positiveScreenshotPath

    Send-CdpEscape -Socket $socket
    $cancelledState = Wait-CdpReady -Socket $socket -Expression $cancelReadyExpression -Description 'Escape preview cancellation' -TimeoutSeconds 15
    if ([int]$cancelledState.preview_zone -ne 1 -or [int]$cancelledState.apply_zone -ne 0 -or [int]$cancelledState.save -ne 0 -or
        -not [bool]$cancelledState.diff_hidden -or -not [bool]$cancelledState.apply_disabled -or -not [bool]$cancelledState.accepted_unchanged) {
        throw 'Escape cancellation did not remove the diff, disable Apply, preserve accepted placements, and keep ApplyZone/Save absent.'
    }
    $auditSnapshot = Invoke-CdpExpression -Socket $socket -Expression $auditSnapshotExpression
    $restoreResult = Invoke-CdpExpression -Socket $socket -Expression $restoreAuditExpression
    if (-not $restoreResult.restored) {
        throw 'Original window.fetch transport function was not restored.'
    }
    $auditRestored = $true
    $auditInstalled = $false
    $finalScreenshot = Save-CdpScreenshot -Socket $socket -LiteralPath $finalScreenshotPath
}
catch {
    $proofFailure = $_.Exception.Message
}
finally {
    if ($auditInstalled -and $null -ne $socket -and $socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        try {
            [void](Invoke-CdpExpression -Socket $socket -Expression $restoreAuditExpression -TimeoutSeconds 5)
            $auditRestored = $true
            $auditInstalled = $false
        }
        catch {
            if ($null -eq $proofFailure) { $proofFailure = $_.Exception.Message }
            else { $proofFailure += " | IPC audit restore failed: $($_.Exception.Message)" }
        }
    }
    if ($null -ne $socket) {
        try {
            if ($socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
                $closeTimeout = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(2))
                try {
                    Wait-NonGenericTask -Task ($socket.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'R2-027 proof complete', $closeTimeout.Token))
                }
                finally {
                    $closeTimeout.Dispose()
                }
            }
        }
        catch {
            Write-Verbose "CDP websocket close failed: $($_.Exception.Message)"
        }
        finally {
            $socket.Dispose()
        }
    }
    foreach ($name in $trackedEnvironment) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
    $environmentRestored = $true
    if ($null -ne $reservation) {
        try { $reservation.Listener.Stop() } catch { Write-Verbose $_.Exception.Message }
    }
    if ($null -ne $process -and -not $process.HasExited) {
        $gracefulCloseRequested = $true
        try { $gracefulCloseAccepted = $process.CloseMainWindow() } catch { Write-Verbose $_.Exception.Message }
        if (-not $process.WaitForExit(8000)) {
            try {
                Stop-Process -Id $process.Id -Force -ErrorAction Stop
                $forcedOwnPid = $true
            }
            catch {
                # A normal close may win the race immediately before Stop-Process.
                $process.Refresh()
                if (-not $process.HasExited) {
                    if ($null -eq $proofFailure) { $proofFailure = "Unable to stop owned PID $($process.Id): $($_.Exception.Message)" }
                    else { $proofFailure += " | unable to stop owned PID $($process.Id): $($_.Exception.Message)" }
                }
            }
            if (-not $process.HasExited) {
                [void]$process.WaitForExit(5000)
            }
        }
    }
    if ($null -ne $process) {
        $processClosed = $process.HasExited
        if (-not $processClosed) {
            if ($null -eq $proofFailure) { $proofFailure = "Owned Studio PID $($process.Id) remained alive after cleanup." }
            else { $proofFailure += " | owned Studio PID $($process.Id) remained alive after cleanup" }
        }
    }
    if ($devToolsPort -gt 0) {
        $listenerClosed = Wait-CdpListenerClosed -Port $devToolsPort -TimeoutSeconds 10
        if (-not $listenerClosed) {
            if ($null -eq $proofFailure) { $proofFailure = "CDP listener remained reachable on port $devToolsPort after closing the owned process." }
            else { $proofFailure += " | CDP listener remained reachable on port $devToolsPort" }
        }
    }
}

$archiveAfter = $null
$archivePreserved = $false
try {
    $archiveAfter = Get-ArchiveSnapshot -LiteralPath $archivePath
    $archivePreserved = Test-ArchiveSnapshotsEqual -Before $archiveBefore -After $archiveAfter
    if (-not $archivePreserved) {
        throw 'Source authoring.json hash, byte length, or last-write timestamp changed.'
    }
}
catch {
    if ($null -eq $proofFailure) { $proofFailure = $_.Exception.Message }
    else { $proofFailure += " | archive verification failed: $($_.Exception.Message)" }
}

$candidateHashAfter = Get-FileSha256 -LiteralPath $resolvedCandidate
if ($candidateHashAfter -cne $expectedHash) {
    if ($null -eq $proofFailure) { $proofFailure = 'Candidate executable hash changed during proof.' }
    else { $proofFailure += ' | candidate executable hash changed during proof' }
}

$completedAt = [DateTimeOffset]::UtcNow
$proofPassed = [string]::IsNullOrWhiteSpace($proofFailure)
$ledger = [ordered]@{
    schema = 'forge.gate2.r2-027.authoring-drop-proof'
    schema_version = 1
    recorded_at_utc = $completedAt.ToString('o')
    gate = 'Gate 2'
    criterion = 'R2-027'
    gate_qualified = $false
    proof = [ordered]@{
        passed = $proofPassed
        status = if ($proofPassed) { 'automated_cdp_acquired' } else { 'failed' }
        method = 'automated CDP; not human review'
        human_review_performed = $false
        human_review_required_for_gate_qualification = $true
        failure = $proofFailure
        started_at_utc = $startedAt.ToString('o')
        completed_at_utc = $completedAt.ToString('o')
    }
    ci = [ordered]@{
        status = $ciStatus
    }
    candidate = [ordered]@{
        path = $resolvedCandidate
        expected_sha256 = $expectedHash
        before_sha256 = $actualCandidateHash
        after_sha256 = $candidateHashAfter
        exact_hash_verified = $actualCandidateHash -ceq $expectedHash -and $candidateHashAfter -ceq $expectedHash
    }
    authoring = [ordered]@{
        root = $resolvedAuthoringRoot
        zone_uuid = $zoneId
        archive = [ordered]@{
            path = $archivePath
            before = $archiveBefore
            after = $archiveAfter
            hash_bytes_mtime_exact = $archivePreserved
        }
        subject = $authorSubject
        initial_accepted_placement_count = if ($null -ne $auditSnapshot) { @($auditSnapshot.accepted_options).Count } else { $null }
        initial_accepted_placement_options = if ($null -ne $auditSnapshot) { @($auditSnapshot.accepted_options) } else { @() }
    }
    webview2 = [ordered]@{
        user_data_directory = $resolvedUserData
        launch_style = 'hidden'
        remote_debugging_address = '127.0.0.1'
        remote_debugging_port = $devToolsPort
        exact_target = if ($null -ne $cdpTarget) { [ordered]@{ type = $cdpTarget.type; title = $cdpTarget.title; url = $cdpTarget.url; websocket_url = $cdpTarget.webSocketDebuggerUrl } } else { $null }
        listener_gone_after_close = $listenerClosed
    }
    ui = [ordered]@{
        initial = $initialUi
        asset_id = $assetId
        audit_probe = $auditProbeState
        negative_cases = @($negativeResults)
        positive_preview = $positiveState
        escape_after_preview = $cancelledState
        projection_status = [ordered]@{
            scope = 'optional observation outside R2-027; explicit Placements non projetés is fail-closed'
            positive = if ($null -ne $positiveState) { $positiveState.projection_status } else { $null }
            after_escape = if ($null -ne $cancelledState) { $cancelledState.projection_status } else { $null }
        }
        invoke_audit = $auditSnapshot
        transport_audit_restored_before_final_screenshot = $auditRestored
    }
    screenshots = [ordered]@{
        positive_preview = $positiveScreenshot
        final_cancelled = $finalScreenshot
        acquisition = 'Page.captureScreenshot'
    }
    cdp = [ordered]@{
        request_count = $script:CdpMessageId
        asynchronous_event_count = $script:CdpEvents.Count
        dropped_event_count = $script:CdpDroppedEventCount
        asynchronous_event_methods = @($script:CdpEvents | ForEach-Object { $_.Method } | Sort-Object -Unique)
    }
    cleanup = [ordered]@{
        owned_process_id = if ($null -ne $process) { $process.Id } else { $null }
        owned_process_closed = $processClosed
        graceful_close_requested = $gracefulCloseRequested
        graceful_close_accepted = $gracefulCloseAccepted
        forced_only_owned_pid = $forcedOwnPid
        environment_restored = $environmentRestored
        cdp_listener_gone = $listenerClosed
    }
    limitations = @(
        'This is an automated CDP proof, not a human visual review.',
        'No ApplyZone or Save command is permitted by this harness.',
        'Projection status is an optional R2-026 observation; an explicit Placements non projetés status still fails closed.',
        'gate_qualified remains false even when every automated assertion passes.',
        $ciStatus
    )
}

$ledgerJson = $ledger | ConvertTo-Json -Depth 100
[System.IO.File]::WriteAllText($ledgerPath, $ledgerJson, [System.Text.UTF8Encoding]::new($false))
$ledgerHash = Get-FileSha256 -LiteralPath $ledgerPath

$statusLabel = if ($proofPassed) { 'PASS automatisé local' } else { 'FAIL' }
$failureLine = if ($proofPassed) { 'Aucune erreur automatisée.' } else { $proofFailure }
$reportTemplate = @'
# R2-027 — preuve CDP du glisser-déposer auteur

- Verdict du harness : **{0}**
- `gate_qualified=false`
- Méthode : **CDP automatisé, pas une revue humaine**
- CI distante : **{1}**
- Candidat : `{2}`
- SHA-256 candidat : `{3}`
- Zone exacte : `{4}`
- Asset stable : `{5}`

## Contrôles

- URL CDP exacte : `{6}`
- Titre CDP exact : `{7}`
- Cas négatifs : mouvement inférieur à 6 px, relâchement hors canvas, Échap pendant le geste.
- Cas positif : une seule commande `PreviewZone`, diff placements avec `1 ajout(s)` et `non appliquée`, aucune commande Apply/Save.
- Échap après l’aperçu : diff masqué, Apply désactivé, placements acceptés inchangés.
- Statut de projection : observation facultative hors R2-027 ; `Placements non projetés` reste un refus explicite bloquant.
- Archive source : hash, octets et mtime exacts avant/après = **{8}**.
- Listener CDP disparu après fermeture = **{9}**.
- Audit IPC retiré et descripteur original de `window.fetch` restauré avant la capture finale = **{10}**.

## Artefacts

- Ledger JSON : `{11}` (SHA-256 `{12}`)
- Capture aperçu positif : `{13}`
- Capture finale après Échap : `{14}`

## Limites

Ce replay prouve le comportement automatisé observé dans le candidat exact. Il ne constitue pas une revue humaine et ne qualifie ni Gate 2 dans son ensemble ni Gate 3.

Erreur : {15}
'@
$report = $reportTemplate -f @(
    $statusLabel,
    $ciStatus,
    $resolvedCandidate,
    $candidateHashAfter,
    $zoneId,
    $assetId,
    $expectedTargetUrl,
    $expectedTargetTitle,
    $archivePreserved,
    $listenerClosed,
    $auditRestored,
    $ledgerPath,
    $ledgerHash,
    $positiveScreenshotPath,
    $finalScreenshotPath,
    $failureLine
)
[System.IO.File]::WriteAllText($reportPath, $report, [System.Text.UTF8Encoding]::new($false))

if (-not $proofPassed) {
    throw "R2-027 CDP proof failed. Ledger: $ledgerPath. Failure: $proofFailure"
}

Write-Output "R2_027_CDP_PROOF_ACQUIRED gate_qualified=false automated_cdp=true human_review=false ledger=$ledgerPath report=$reportPath"
