#Requires -Version 7.2
# Gate 3 R3-P03 Tidewater invalid-quest authoring smoke. This is a bounded
# public-UI diagnostic, not a Gate qualification verdict.
[CmdletBinding(DefaultParameterSetName = 'Run')]
param(
    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [Alias('CandidateExecutable')]
    [ValidateNotNullOrEmpty()]
    [string]$Candidate,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [Alias('FixtureSourceRoot')]
    [ValidateNotNullOrEmpty()]
    [string]$FreshIsolatedFixture,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [Alias('PreviousLedger', 'LedgerPath')]
    [ValidateNotNullOrEmpty()]
    [string]$AuthoringLedger,

    [Parameter(ParameterSetName = 'Run')]
    [Alias('SourceOriginalHash')]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$SourceOriginalHashPinned = '50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720',

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$EvidenceRoot,

    [Parameter(ParameterSetName = 'Run')]
    [guid]$ZoneUuid = 'e620dd19-ead9-5913-a6d6-0f93109148c0',

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(15, 180)]
    [int]$StartupTimeoutSeconds = 60,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(30, 300)]
    [int]$InteractionTimeoutSeconds = 120,

    [Parameter(ParameterSetName = 'Run')]
    [switch]$CurrentCandidateFixture,

    [Parameter(ParameterSetName = 'Run')]
    [string]$ExpectedCandidateSha256,

    [Parameter(ParameterSetName = 'Run')]
    [string]$ExpectedAuthoredArchiveHash,

    [Parameter(ParameterSetName = 'Run')]
    [string]$ExpectedAuthoredHeadHash,

    [Parameter(ParameterSetName = 'Run')]
    [string]$ExpectedAuthoringLedgerSha256,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The authoring and playtest scripts expose the governed CDP, archive and
# native-process helpers only through their SelfTest path. Preserve this
# script's bindings while dot-sourcing both helpers; restoring null Run-only
# values would re-trigger their parameter validation in PowerShell.
$p03RequestedSelfTest = [bool]$SelfTest
$p03RequestedCandidate = $Candidate
$p03RequestedFixture = $FreshIsolatedFixture
$p03RequestedLedger = $AuthoringLedger
$p03RequestedSourcePin = $SourceOriginalHashPinned
$p03RequestedEvidenceRoot = $EvidenceRoot
$p03RequestedZoneUuid = $ZoneUuid
$p03RequestedStartupTimeout = $StartupTimeoutSeconds
$p03RequestedInteractionTimeout = $InteractionTimeoutSeconds
$p03RequestedCurrentCandidateFixture = [bool]$CurrentCandidateFixture
$p03RequestedExpectedCandidate = $ExpectedCandidateSha256
$p03RequestedExpectedArchive = $ExpectedAuthoredArchiveHash
$p03RequestedExpectedHead = $ExpectedAuthoredHeadHash
$p03RequestedExpectedLedger = $ExpectedAuthoringLedgerSha256
. (Join-Path $PSScriptRoot 'Invoke-Gate3GameplayAuthoringSmoke.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Invoke-Gate3GameplayPlaytestSmoke.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Gate3ProofStorage.ps1')

# The restricted PowerShell account cannot always query Win32_Process or
# Get-NetTCPConnection for WebView2.  Use the read-only Windows process APIs
# for diagnostics so a startup failure can still identify the exact child and
# its remote-debugging arguments.  This is never used to control a process.
if ($null -eq ([System.Management.Automation.PSTypeName]'P03NativeProcessQuery').Type) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class P03NativeProcessQuery
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr process,
        int informationClass,
        IntPtr information,
        int informationLength,
        out int returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    public static string ReadCommandLine(int processId)
    {
        const uint QueryInformation = 0x0400;
        const uint QueryLimitedInformation = 0x1000;
        var process = OpenProcess(QueryInformation | QueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero)
        {
            return "OPEN_FAILED:" + Marshal.GetLastWin32Error();
        }

        try
        {
            int required = 0;
            NtQueryInformationProcess(process, 60, IntPtr.Zero, 0, out required);
            if (required <= 0)
            {
                return "QUERY_SIZE_FAILED:" + required;
            }

            var memory = Marshal.AllocHGlobal(required);
            try
            {
                int returned = 0;
                var status = NtQueryInformationProcess(process, 60, memory, required, out returned);
                if (status != 0)
                {
                    return "QUERY_FAILED:0x" + status.ToString("X");
                }

                var commandLine = Marshal.PtrToStructure<UnicodeString>(memory);
                if (commandLine.Buffer == IntPtr.Zero || commandLine.Length == 0)
                {
                    return "EMPTY";
                }

                return Marshal.PtrToStringUni(commandLine.Buffer, commandLine.Length / 2) ?? "NULL";
            }
            finally
            {
                Marshal.FreeHGlobal(memory);
            }
        }
        finally
        {
            CloseHandle(process);
        }
    }

    public static int ReadParentProcessId(int processId)
    {
        const uint QueryInformation = 0x0400;
        const uint QueryLimitedInformation = 0x1000;
        var process = OpenProcess(QueryInformation | QueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero)
        {
            return -Marshal.GetLastWin32Error();
        }

        try
        {
            var memory = Marshal.AllocHGlobal(Marshal.SizeOf<ProcessBasicInformation>());
            try
            {
                int returned = 0;
                var status = NtQueryInformationProcess(
                    process,
                    0,
                    memory,
                    Marshal.SizeOf<ProcessBasicInformation>(),
                    out returned);
                if (status != 0)
                {
                    return -status;
                }

                var info = Marshal.PtrToStructure<ProcessBasicInformation>(memory);
                return unchecked((int)info.InheritedFromUniqueProcessId.ToInt64());
            }
            finally
            {
                Marshal.FreeHGlobal(memory);
            }
        }
        finally
        {
            CloseHandle(process);
        }
    }
}
'@
}

function Get-P03NativeProcessMetadata {
    param([Parameter(Mandatory)][int]$ProcessId)
    $commandLine = $null
    $parentPid = $null
    try { $commandLine = [P03NativeProcessQuery]::ReadCommandLine($ProcessId) } catch { $commandLine = "NATIVE_QUERY_ERROR:$($_.Exception.Message)" }
    try { $parentPid = [P03NativeProcessQuery]::ReadParentProcessId($ProcessId) } catch { $parentPid = $null }
    return [ordered]@{
        pid = $ProcessId
        parent_pid = $parentPid
        command_line = $commandLine
    }
}

# The shared starter throws before returning its run object when the exact
# authoring target never appears. Keep an owned startup record in this P03
# script so the failure still contains the process identity and the public
# /json/list observations, while retaining the exact title/URL contract.
$script:P03StartupDiagnostics = [ordered]@{
    port = $null
    process_id = $null
    process_path = $null
    process_start_utc = $null
    process_exit_code = $null
    endpoint = $null
    endpoint_observed = $false
    environment = $null
    target_snapshots = @()
    last_targets = @()
    native_snapshots = @()
    last_native_snapshot = $null
    selected_target = $null
    selected_target_observed_utc = $null
    cdp_transport = [ordered]@{
        renderer_settle_delay_ms = $null
        connections = @()
        browser_probe = $null
        browser_commands = @()
        target_commands = @()
    }
    last_reason = $null
    endpoint_errors = @()
    cleanup_attempted = $false
    cleanup_forced = $false
    cleanup_process_closed = $false
    cleanup_identity_verified = $false
    cleanup_note = $null
    graceful_close_failure = $null
}

function Convert-P03CdpTargetSnapshot {
    param([Parameter(Mandatory)][object]$Target)
    return [ordered]@{
        id = [string]$Target.id
        type = [string]$Target.type
        title = [string]$Target.title
        url = [string]$Target.url
        webSocketDebuggerUrl = [string]$Target.webSocketDebuggerUrl
    }
}

function Get-P03NativeStartupSnapshot {
    param(
        [Parameter(Mandatory)][int]$Port,
        [Parameter(Mandatory)][System.Diagnostics.Process]$OwnedProcess
    )
    $snapshot = [ordered]@{
        observed_utc = [DateTime]::UtcNow.ToString('O')
        port = $Port
        owned_process = $null
        port_owners = @()
        netstat_port_owners = @()
        webview_processes = @()
        webview_cim = @()
        errors = @()
    }
    try {
        $OwnedProcess.Refresh()
        $ownedMetadata = Get-P03NativeProcessMetadata -ProcessId ([int]$OwnedProcess.Id)
        $snapshot.owned_process = [ordered]@{
            pid = [int]$OwnedProcess.Id
            parent_pid = $ownedMetadata.parent_pid
            path = [string]$OwnedProcess.Path
            command_line = $ownedMetadata.command_line
            has_exited = [bool]$OwnedProcess.HasExited
            exit_code = if ($OwnedProcess.HasExited) { $OwnedProcess.ExitCode } else { $null }
            main_window_handle = [string]$OwnedProcess.MainWindowHandle
            main_window_title = [string]$OwnedProcess.MainWindowTitle
            responding = [bool]$OwnedProcess.Responding
            cpu_seconds = [double]$OwnedProcess.TotalProcessorTime.TotalSeconds
        }
    }
    catch {
        $snapshot.errors += "owned_process: $($_.Exception.Message)"
    }
    try {
        $connections = @(Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort $Port -ErrorAction Stop)
        $snapshot.port_owners = @($connections | ForEach-Object {
            $owner = $null
            try { $owner = Get-Process -Id ([int]$_.OwningProcess) -ErrorAction Stop } catch { }
            [ordered]@{
                state = [string]$_.State
                local_address = [string]$_.LocalAddress
                local_port = [int]$_.LocalPort
                owning_pid = [int]$_.OwningProcess
                owning_name = if ($null -ne $owner) { [string]$owner.ProcessName } else { $null }
                owning_path = if ($null -ne $owner) { [string]$owner.Path } else { $null }
                owning_window_title = if ($null -ne $owner) { [string]$owner.MainWindowTitle } else { $null }
            }
        })
    }
    catch {
        $snapshot.errors += "port_owner: $($_.Exception.Message)"
    }
    try {
        $netstatPath = Join-Path $env:SystemRoot 'System32\netstat.exe'
        $netstatLines = @(& $netstatPath -ano 2>$null)
        $snapshot.netstat_port_owners = @($netstatLines | ForEach-Object {
            $parts = @(([string]$_ -split '\s+') | Where-Object { $_ })
            if ($parts.Count -lt 5) { return }
            $localEndpoint = [string]$parts[1]
            if (-not ($localEndpoint.EndsWith(":$Port", [StringComparison]::OrdinalIgnoreCase))) { return }
            $owningPid = 0
            if (-not [int]::TryParse([string]$parts[4], [ref]$owningPid)) { return }
            $owner = $null
            try { $owner = Get-Process -Id $owningPid -ErrorAction Stop } catch { }
            $metadata = Get-P03NativeProcessMetadata -ProcessId $owningPid
            [ordered]@{
                protocol = [string]$parts[0]
                local_endpoint = $localEndpoint
                foreign_endpoint = [string]$parts[2]
                state = [string]$parts[3]
                owning_pid = $owningPid
                owning_name = if ($null -ne $owner) { [string]$owner.ProcessName } else { $null }
                owning_path = if ($null -ne $owner) { [string]$owner.Path } else { $null }
                owning_window_title = if ($null -ne $owner) { [string]$owner.MainWindowTitle } else { $null }
                owning_parent_pid = $metadata.parent_pid
                owning_command_line = $metadata.command_line
            }
        })
    }
    catch {
        $snapshot.errors += "netstat_port_owner: $($_.Exception.Message)"
    }
    try {
        $snapshot.webview_processes = @(Get-Process -Name 'msedgewebview2' -ErrorAction Stop | ForEach-Object {
            try { $_.Refresh() } catch { }
            $startTime = $null
            try { $startTime = $_.StartTime.ToUniversalTime().ToString('O') } catch { }
            $cpuSeconds = $null
            try { $cpuSeconds = [double]$_.TotalProcessorTime.TotalSeconds } catch { }
            $metadata = Get-P03NativeProcessMetadata -ProcessId ([int]$_.Id)
            [ordered]@{
                pid = [int]$_.Id
                parent_pid = $metadata.parent_pid
                path = [string]$_.Path
                command_line = $metadata.command_line
                start_time = $startTime
                main_window_title = [string]$_.MainWindowTitle
                responding = [bool]$_.Responding
                cpu_seconds = $cpuSeconds
            }
        })
    }
    catch {
        $snapshot.errors += "webview_processes: $($_.Exception.Message)"
    }
    try {
        $snapshot.webview_cim = @(Get-CimInstance Win32_Process -Filter "Name='msedgewebview2.exe'" -ErrorAction Stop | ForEach-Object {
            [ordered]@{
                pid = [int]$_.ProcessId
                parent_pid = [int]$_.ParentProcessId
                executable_path = [string]$_.ExecutablePath
                command_line = [string]$_.CommandLine
            }
        })
    }
    catch {
        $snapshot.errors += "webview_cim: $($_.Exception.Message)"
    }
    return $snapshot
}

function Wait-CdpTarget {
    param(
        [Parameter(Mandatory)][int]$Port,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [Parameter(Mandatory)][System.Diagnostics.Process]$Process
    )
    $diagnostics = $script:P03StartupDiagnostics
    $diagnostics.port = $Port
    $diagnostics.endpoint = "http://127.0.0.1:$Port/json/list"
    $diagnostics.process_id = [int]$Process.Id
    try { $diagnostics.process_path = [string]$Process.Path } catch { $diagnostics.process_path = $null }
    try { $diagnostics.process_start_utc = $Process.StartTime.ToUniversalTime().ToString('O') } catch { $diagnostics.process_start_utc = $null }
    $diagnostics.environment = [ordered]@{
        FORGE_AUTHORING_ROOT = [Environment]::GetEnvironmentVariable('FORGE_AUTHORING_ROOT', 'Process')
        FORGE_AUTHOR_SUBJECT = [Environment]::GetEnvironmentVariable('FORGE_AUTHOR_SUBJECT', 'Process')
        WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = [Environment]::GetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', 'Process')
        WEBVIEW2_USER_DATA_FOLDER = [Environment]::GetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER', 'Process')
    }
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastReason = 'endpoint not available'
    $poll = 0
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $poll += 1
        try {
            $Process.Refresh()
            if ($poll -eq 1 -or $poll % 5 -eq 0) {
                $native = Get-P03NativeStartupSnapshot -Port $Port -OwnedProcess $Process
                $diagnostics.last_native_snapshot = $native
                if (@($diagnostics.native_snapshots).Count -lt 180) {
                    $diagnostics.native_snapshots += $native
                }
            }
            if ($Process.HasExited) {
                $diagnostics.process_exit_code = $Process.ExitCode
                $diagnostics.last_reason = "Studio exited before exposing CDP (exit code $($Process.ExitCode))."
                $script:P03StartupDiagnostics = $diagnostics
                throw $diagnostics.last_reason
            }
            $targets = @(Invoke-RestMethodCompat -Uri $diagnostics.endpoint -TimeoutSeconds 2)
            $diagnostics.endpoint_observed = $true
            $snapshot = @($targets | ForEach-Object { Convert-P03CdpTargetSnapshot -Target $_ })
            $diagnostics.last_targets = $snapshot
            if (@($diagnostics.target_snapshots).Count -lt 600) {
                $diagnostics.target_snapshots += [ordered]@{
                    observed_utc = [DateTime]::UtcNow.ToString('O')
                    targets = $snapshot
                }
            }
            $matching = @($targets | Where-Object {
                [string]$_.type -ceq 'page' -and
                [string]$_.title -ceq $script:ExpectedTargetTitle -and
                [string]$_.url -ceq $script:ExpectedTargetUrl
            })
            if ($matching.Count -gt 1) {
                throw 'More than one exact authoring page target is exposed on the reserved port.'
            }
            if ($matching.Count -eq 1) {
                Assert-CdpTarget -Target $matching[0] -ExpectedPort $Port
                $diagnostics.selected_target = Convert-P03CdpTargetSnapshot -Target $matching[0]
                $diagnostics.selected_target_observed_utc = [DateTime]::UtcNow.ToString('O')
                $diagnostics.last_reason = 'exact authoring target selected'
                $script:P03StartupDiagnostics = $diagnostics
                return $matching[0]
            }
            $lastReason = "exact page URL/title/type not present; public_target_count=$($snapshot.Count)"
        }
        catch {
            $lastReason = $_.Exception.Message
            if (@($diagnostics.endpoint_errors).Count -lt 120) {
                $diagnostics.endpoint_errors += [ordered]@{
                    observed_utc = [DateTime]::UtcNow.ToString('O')
                    message = $lastReason
                }
            }
        }
        Start-Sleep -Milliseconds 200
    }
    try {
        $Process.Refresh()
        $diagnostics.process_exit_code = if ($Process.HasExited) { $Process.ExitCode } else { $null }
    } catch { }
    $diagnostics.last_reason = $lastReason
    $script:P03StartupDiagnostics = $diagnostics
    throw "Exact authoring CDP target did not appear on loopback port $Port. Last state: $lastReason. Public target snapshots: $($diagnostics.last_targets | ConvertTo-Json -Compress -Depth 8)"
}

function Add-P03CdpTransportEntry {
    param(
        [Parameter(Mandatory)][ValidateSet('connections', 'browser_commands', 'target_commands')][string]$Bucket,
        [Parameter(Mandatory)]$Entry
    )
    $transport = $script:P03StartupDiagnostics.cdp_transport
    $transport[$Bucket] = @($transport[$Bucket]) + @($Entry)
}

function Connect-P03CdpSocket {
    param(
        [Parameter(Mandatory)][string]$WebSocketUrl,
        [Parameter(Mandatory)][ValidateRange(5, 180)][int]$TimeoutSeconds,
        [Parameter(Mandatory)][ValidateSet('browser', 'target')][string]$Kind
    )
    $started = [DateTimeOffset]::UtcNow
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $timeout = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($TimeoutSeconds))
    try {
        Wait-NonGenericTask -Task ($socket.ConnectAsync([Uri]$WebSocketUrl, $timeout.Token))
        $timer.Stop()
        Add-P03CdpTransportEntry -Bucket 'connections' -Entry ([ordered]@{
            kind = $Kind
            websocket_url = $WebSocketUrl
            started_utc = $started.ToString('o')
            completed_utc = [DateTimeOffset]::UtcNow.ToString('o')
            elapsed_ms = $timer.Elapsed.TotalMilliseconds
            timeout_seconds = $TimeoutSeconds
            success = $true
            socket_state = [string]$socket.State
        })
        return $socket
    }
    catch {
        $timer.Stop()
        Add-P03CdpTransportEntry -Bucket 'connections' -Entry ([ordered]@{
            kind = $Kind
            websocket_url = $WebSocketUrl
            started_utc = $started.ToString('o')
            completed_utc = [DateTimeOffset]::UtcNow.ToString('o')
            elapsed_ms = $timer.Elapsed.TotalMilliseconds
            timeout_seconds = $TimeoutSeconds
            success = $false
            socket_state = [string]$socket.State
            error = $_.Exception.Message
        })
        $socket.Dispose()
        throw
    }
    finally {
        $timeout.Dispose()
    }
}

function Invoke-P03MeasuredCdpCommand {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][ValidateSet('browser', 'target')][string]$Kind,
        [Parameter(Mandatory)][string]$Method,
        [System.Collections.IDictionary]$Parameters = @{},
        [Parameter(Mandatory)][ValidateRange(1, 180)][int]$TimeoutSeconds
    )
    $started = [DateTimeOffset]::UtcNow
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $bucket = if ($Kind -eq 'browser') { 'browser_commands' } else { 'target_commands' }
    try {
        $result = Invoke-CdpCommand -Socket $Socket -Method $Method -Parameters $Parameters -TimeoutSeconds $TimeoutSeconds
        $timer.Stop()
        Add-P03CdpTransportEntry -Bucket $bucket -Entry ([ordered]@{
            method = $Method
            started_utc = $started.ToString('o')
            completed_utc = [DateTimeOffset]::UtcNow.ToString('o')
            elapsed_ms = $timer.Elapsed.TotalMilliseconds
            timeout_seconds = $TimeoutSeconds
            success = $true
            socket_state = [string]$Socket.State
        })
        return $result
    }
    catch {
        $timer.Stop()
        Add-P03CdpTransportEntry -Bucket $bucket -Entry ([ordered]@{
            method = $Method
            started_utc = $started.ToString('o')
            completed_utc = [DateTimeOffset]::UtcNow.ToString('o')
            elapsed_ms = $timer.Elapsed.TotalMilliseconds
            timeout_seconds = $TimeoutSeconds
            success = $false
            socket_state = [string]$Socket.State
            error = $_.Exception.Message
        })
        throw
    }
}

function Invoke-P03BrowserProtocolProbe {
    param(
        [Parameter(Mandatory)][int]$Port,
        [Parameter(Mandatory)][object]$Target,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $probe = [ordered]@{
        status = 'started'
        version_endpoint = "http://127.0.0.1:$Port/json/version"
        version = $null
        browser_websocket_url = $null
        browser_get_version = $null
        target_get_targets = $null
        target_activate = $null
        target_id = [string]$Target.id
        error = $null
    }
    $browserSocket = $null
    try {
        $version = Invoke-RestMethodCompat -Uri $probe.version_endpoint -TimeoutSeconds ([Math]::Min(5, $TimeoutSeconds))
        $probe.version = [ordered]@{
            Browser = [string]$version.Browser
            'Protocol-Version' = [string]$version.'Protocol-Version'
            webSocketDebuggerUrl = [string]$version.webSocketDebuggerUrl
        }
        $probe.browser_websocket_url = [string]$version.webSocketDebuggerUrl
        if ([string]::IsNullOrWhiteSpace($probe.browser_websocket_url)) {
            throw 'CDP /json/version did not expose a browser websocket endpoint.'
        }
        $browserSocket = Connect-P03CdpSocket -WebSocketUrl $probe.browser_websocket_url -TimeoutSeconds ([Math]::Min(30, $TimeoutSeconds)) -Kind 'browser'
        $probe.browser_get_version = Invoke-P03MeasuredCdpCommand -Socket $browserSocket -Kind 'browser' -Method 'Browser.getVersion' -TimeoutSeconds ([Math]::Min(30, $TimeoutSeconds))
        $probe.target_get_targets = Invoke-P03MeasuredCdpCommand -Socket $browserSocket -Kind 'browser' -Method 'Target.getTargets' -TimeoutSeconds ([Math]::Min(30, $TimeoutSeconds))
        if ([string]::IsNullOrWhiteSpace($probe.target_id)) {
            throw 'Exact page target did not expose a CDP target id for activation.'
        }
        $probe.target_activate = Invoke-P03MeasuredCdpCommand -Socket $browserSocket -Kind 'browser' -Method 'Target.activateTarget' -Parameters @{ targetId = $probe.target_id } -TimeoutSeconds ([Math]::Min(30, $TimeoutSeconds))
        $probe.status = 'passed'
    }
    catch {
        $probe.status = 'failed'
        $probe.error = $_.Exception.Message
    }
    finally {
        if ($null -ne $browserSocket) {
            try { $browserSocket.Dispose() } catch { }
        }
    }
    $script:P03StartupDiagnostics.cdp_transport.browser_probe = $probe
    return $probe
}

function Start-P03InvalidQuestProcess {
    param(
        [Parameter(Mandatory)][string]$CandidatePath,
        [Parameter(Mandatory)][string]$CandidateHash,
        [Parameter(Mandatory)][string]$FixtureRoot,
        [Parameter(Mandatory)][string]$EvidencePath,
        [Parameter(Mandatory)][int]$RunNumber,
        [Parameter(Mandatory)][int]$StartupTimeout
    )
    $profile = Join-Path $EvidencePath "webview2-user-data-$RunNumber"
    [void](New-Item -ItemType Directory -Path $profile -Force)
    $reservation = New-LoopbackPortReservation
    $process = $null
    $socket = $null
    try {
        [Environment]::SetEnvironmentVariable('FORGE_AUTHORING_ROOT', $FixtureRoot, 'Process')
        [Environment]::SetEnvironmentVariable('FORGE_AUTHOR_SUBJECT', "tidewater-gate3-gameplay-invalid-quest-$RunNumber", 'Process')
        [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', "--remote-debugging-address=127.0.0.1 --remote-debugging-port=$($reservation.Port)", 'Process')
        [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER', $profile, 'Process')
        $reservation.Listener.Stop()
        $stdout = Join-Path $EvidencePath "candidate-run-$RunNumber.stdout.log"
        $stderr = Join-Path $EvidencePath "candidate-run-$RunNumber.stderr.log"
        $process = Start-Process -FilePath $CandidatePath -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        [void](Wait-LaunchedExecutableIdentity -Process $process -Candidate $CandidatePath -ExpectedHash $CandidateHash -TimeoutSeconds ([Math]::Min(15, $StartupTimeout)))
        $target = Wait-CdpTarget -Port $reservation.Port -TimeoutSeconds $StartupTimeout -Process $process
        [void](Invoke-P03BrowserProtocolProbe -Port $reservation.Port -Target $target -TimeoutSeconds $StartupTimeout)
        $settleTimer = [Diagnostics.Stopwatch]::StartNew()
        Start-Sleep -Seconds 5
        $settleTimer.Stop()
        $script:P03StartupDiagnostics.cdp_transport.renderer_settle_delay_ms = $settleTimer.Elapsed.TotalMilliseconds
        $socket = Connect-P03CdpSocket -WebSocketUrl ([string]$target.webSocketDebuggerUrl) -TimeoutSeconds ([Math]::Min(30, $StartupTimeout)) -Kind 'target'
        $commandTimeout = [Math]::Min(180, [Math]::Max(30, $StartupTimeout))
        [void](Invoke-P03MeasuredCdpCommand -Socket $socket -Kind 'target' -Method 'Runtime.enable' -TimeoutSeconds $commandTimeout)
        [void](Invoke-P03MeasuredCdpCommand -Socket $socket -Kind 'target' -Method 'Page.enable' -TimeoutSeconds $commandTimeout)
        [void](Invoke-P03MeasuredCdpCommand -Socket $socket -Kind 'target' -Method 'Page.bringToFront' -TimeoutSeconds $commandTimeout)
        [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $StartupTimeout -Description 'Studio invalid-quest shell' -Expression @'
(() => ({ready:document.readyState==='complete'&&!!document.querySelector('.authoring-shell'),terminal:false,reason:'authoring shell pending'}))()
'@)
        return [pscustomobject]@{
            Process = $process
            Socket = $socket
            Reservation = $reservation
            Port = $reservation.Port
            Profile = $profile
            RunNumber = $RunNumber
            Forced = $false
        }
    }
    catch {
        if ($null -ne $socket) { try { $socket.Dispose() } catch { } }
        if ($null -ne $process -and -not $process.HasExited) {
            try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch { }
        }
        if ($null -ne $reservation) { $reservation.Listener.Stop() }
        throw
    }
}

function Stop-P03StartupOwnedProcess {
    param([Parameter(Mandatory)][string]$CandidatePath)
    $diagnostics = $script:P03StartupDiagnostics
    $diagnostics.cleanup_attempted = $true
    if ($null -eq $diagnostics.process_id) {
        $diagnostics.cleanup_note = 'No startup process identity was captured.'
        $script:P03StartupDiagnostics = $diagnostics
        return
    }
    try {
        $process = Get-Process -Id ([int]$diagnostics.process_id) -ErrorAction Stop
        $path = $null
        try { $path = [IO.Path]::GetFullPath([string]$process.Path) } catch { }
        if ([string]::IsNullOrWhiteSpace($path) -or
            -not $path.Equals([IO.Path]::GetFullPath($CandidatePath), [StringComparison]::OrdinalIgnoreCase)) {
            $diagnostics.cleanup_note = "Refused cleanup for PID $($diagnostics.process_id): executable identity did not match the owned V2 candidate."
            $script:P03StartupDiagnostics = $diagnostics
            return
        }
        $diagnostics.cleanup_identity_verified = $true
        $process.Refresh()
        if (-not $process.HasExited) {
            $process.Kill()
            [void]$process.WaitForExit(10000)
            $diagnostics.cleanup_forced = $true
        }
        $process.Refresh()
        $diagnostics.cleanup_process_closed = [bool]$process.HasExited
        $diagnostics.cleanup_note = 'Startup failure cleanup used only the captured exact candidate PID.'
    }
    catch {
        $diagnostics.cleanup_note = "Startup cleanup could not re-open the captured exact candidate PID: $($_.Exception.Message)"
    }
    $script:P03StartupDiagnostics = $diagnostics
}

if (-not $p03RequestedSelfTest) {
    $Candidate = $p03RequestedCandidate
    $FreshIsolatedFixture = $p03RequestedFixture
    $AuthoringLedger = $p03RequestedLedger
    $SourceOriginalHashPinned = $p03RequestedSourcePin
    $EvidenceRoot = $p03RequestedEvidenceRoot
    $ZoneUuid = $p03RequestedZoneUuid
    $StartupTimeoutSeconds = $p03RequestedStartupTimeout
    $InteractionTimeoutSeconds = $p03RequestedInteractionTimeout
    $CurrentCandidateFixture = $p03RequestedCurrentCandidateFixture
    $ExpectedCandidateSha256 = $p03RequestedExpectedCandidate
    $ExpectedAuthoredArchiveHash = $p03RequestedExpectedArchive
    $ExpectedAuthoredHeadHash = $p03RequestedExpectedHead
    $ExpectedAuthoringLedgerSha256 = $p03RequestedExpectedLedger
}

$p03ZoneId = if ($null -ne $ZoneUuid) { $ZoneUuid.ToString('D').ToLowerInvariant() } else { 'e620dd19-ead9-5913-a6d6-0f93109148c0' }
$p03EditorSummary = 'Personnages, objets et objectifs'
$p03QuestName = 'Rapporter le Sceau'
$p03ItemName = 'Sceau'
$p03Base009ArchiveHash = '72598abab38bf2460ab6bebead8a02f364d19894fa1804a50dd6e4c92247445e'
$p03Base009HeadHash = '2be5aab2dd674d5af8f67cd52aea6d61ba4a5de32a99c35855cc4d8cc908b4d4'
$script:CdpP03MessageId = 0

function Get-P03CurrentFixturePins {
    param([switch]$Enabled, [string]$CandidatePin, [string]$ArchivePin, [string]$HeadPin, [string]$LedgerPin)
    $pins = [ordered]@{candidate_sha256=$CandidatePin;archive_hash=$ArchivePin;head_hash=$HeadPin;ledger_sha256=$LedgerPin}
    if (-not $Enabled) {
        if (@($pins.Values | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }).Count -gt 0) { throw 'Explicit current-fixture pins require CurrentCandidateFixture.' }
        return $null
    }
    foreach ($key in @($pins.Keys)) {
        if ([string]$pins[$key] -notmatch '^[0-9A-Fa-f]{64}$') { throw "CurrentCandidateFixture requires an explicit 64-digit $key pin." }
        $pins[$key] = Get-NormalizedSha256 -Value ([string]$pins[$key])
    }
    return $pins
}

function Get-P03GameplayFacts {
    param([Parameter(Mandatory)][string]$Archive)
    $document = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($Archive))
    $gameplay = $document.head.corrections.gameplay
    if ($null -eq $gameplay) { throw "Managed archive has no GameplayDocument: $Archive" }
    $quests = @($gameplay.quests | Where-Object { [string]$_.name -ceq $p03QuestName })
    $items = @($gameplay.items | Where-Object { [string]$_.name -ceq $p03ItemName })
    if ($quests.Count -ne 1 -or $items.Count -ne 1) {
        throw "Expected exactly one '$p03QuestName' quest and one '$p03ItemName' item in the authored managed object."
    }
    $quest = $quests[0]
    $item = $items[0]
    $requiredItemId = [string]$quest.required_item
    $pickupSources = @($gameplay.objects | Where-Object {
            [string]$_.kind.kind -ceq 'pickup' -and [string]$_.kind.item -ceq $requiredItemId
        })
    $namedPickups = @($pickupSources | Where-Object { [string]$_.name -ceq $p03ItemName })
    if ($namedPickups.Count -ne 1 -or $pickupSources.Count -ne 1) {
        throw "The required item must have one unique '$p03ItemName' pickup source for R3-P03."
    }
    $requiredQuantity = [int]$quest.required_quantity
    $pickupQuantity = [int]$namedPickups[0].kind.quantity
    if ($requiredQuantity -le 0 -or $pickupQuantity -lt $requiredQuantity) {
        throw 'The authored pickup does not satisfy the quest quantity needed for the guarded deletion diagnostic.'
    }
    return [pscustomobject]@{
        Quest = $quest
        Item = $item
        Pickup = $namedPickups[0]
        QuestId = [string]$quest.id
        RequiredItemId = $requiredItemId
        RequiredQuantity = $requiredQuantity
        PickupQuantity = $pickupQuantity
    }
}

function Get-P03ArchiveState {
    param(
        [Parameter(Mandatory)][string]$ZoneArchive,
        [Parameter(Mandatory)][string]$WorkspaceArchive,
        [Parameter(Mandatory)][string]$ManagedObjectArchive,
        [Parameter(Mandatory)][string]$ZoneId
    )
    $zoneDocument = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($ZoneArchive))
    $workspaceDocument = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($WorkspaceArchive))
    $managedDocument = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($ManagedObjectArchive))
    $reference = Get-Gate3WorkspaceReference -Archive $WorkspaceArchive -ZoneId $ZoneId
    $zoneReceiptIds = @(
        foreach ($receipt in @($zoneDocument.receipts)) {
            if ($null -ne $receipt -and $null -ne $receipt.request -and $null -ne $receipt.request.command_id) {
                [string]$receipt.request.command_id
            }
        }
    )
    $managedReceiptIds = @(
        foreach ($receipt in @($managedDocument.receipts)) {
            if ($null -ne $receipt -and $null -ne $receipt.request -and $null -ne $receipt.request.command_id) {
                [string]$receipt.request.command_id
            }
        }
    )
    $workspaceCommandIds = @(
        foreach ($command in @($workspaceDocument.commands)) {
            if ($null -ne $command -and $null -ne $command.request -and $null -ne $command.request.command_id) {
                [string]$command.request.command_id
            }
        }
    )
    return [pscustomobject]@{
        zone_snapshot = Get-ArchiveSnapshot -LiteralPath $ZoneArchive
        workspace_snapshot = Get-ArchiveSnapshot -LiteralPath $WorkspaceArchive
        managed_snapshot = Get-ArchiveSnapshot -LiteralPath $ManagedObjectArchive
        zone_head_revision = [int]$zoneDocument.head.revision
        workspace_head_revision = [int]$workspaceDocument.head.revision
        managed_head_revision = [int]$managedDocument.head.revision
        zone_receipt_ids = $zoneReceiptIds
        managed_receipt_ids = $managedReceiptIds
        workspace_command_ids = $workspaceCommandIds
        workspace_reference = $reference
    }
}

function Test-P03SequenceEqual {
    param(
        [AllowNull()][object[]]$Before,
        [AllowNull()][object[]]$After
    )
    return (@($Before | ForEach-Object { [string]$_ }) -join "`n") -ceq (@($After | ForEach-Object { [string]$_ }) -join "`n")
}

function Compare-P03ArchiveState {
    param(
        [Parameter(Mandatory)]$Before,
        [Parameter(Mandatory)]$After
    )
    $zoneArchiveUnchanged = Test-ArchiveSnapshotsEqual -Before $Before.zone_snapshot -After $After.zone_snapshot
    $workspaceUnchanged = Test-ArchiveSnapshotsEqual -Before $Before.workspace_snapshot -After $After.workspace_snapshot
    $managedObjectUnchanged = Test-ArchiveSnapshotsEqual -Before $Before.managed_snapshot -After $After.managed_snapshot
    $revisionUnchanged = [int]$Before.zone_head_revision -eq [int]$After.zone_head_revision -and
        [int]$Before.workspace_head_revision -eq [int]$After.workspace_head_revision -and
        [int]$Before.managed_head_revision -eq [int]$After.managed_head_revision
    $headHashUnchanged = [string]$Before.workspace_reference.head_hash -ceq [string]$After.workspace_reference.head_hash -and
        [string]$Before.workspace_reference.archive_hash -ceq [string]$After.workspace_reference.archive_hash
    $receiptsUnchanged = (Test-P03SequenceEqual -Before $Before.zone_receipt_ids -After $After.zone_receipt_ids) -and
        (Test-P03SequenceEqual -Before $Before.managed_receipt_ids -After $After.managed_receipt_ids) -and
        (Test-P03SequenceEqual -Before $Before.workspace_command_ids -After $After.workspace_command_ids)
    return [ordered]@{
        zone_archive_unchanged = $zoneArchiveUnchanged
        workspace_archive_unchanged = $workspaceUnchanged
        managed_object_unchanged = $managedObjectUnchanged
        revisions_unchanged = $revisionUnchanged
        workspace_reference_hashes_unchanged = $headHashUnchanged
        receipt_and_command_sequences_unchanged = $receiptsUnchanged
        no_new_receipt = $receiptsUnchanged
        all_unchanged = [bool]($zoneArchiveUnchanged -and $workspaceUnchanged -and $managedObjectUnchanged -and
            $revisionUnchanged -and $headHashUnchanged -and $receiptsUnchanged)
    }
}

function Wait-P03InvalidQuestDiagnostic {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$ExpectedObjectId,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $summaryLiteral = ConvertTo-CompactJson -Value $p03EditorSummary
    $objectLiteral = ConvertTo-CompactJson -Value $ExpectedObjectId
    return Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'R3-P03 guarded pickup deletion diagnostic' -Expression @"
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const expectedSummary=$summaryLiteral, expectedObject=$objectLiteral;
  const editor=document.querySelector('details.authoring-gameplay-editor');
  const summary=[...document.querySelectorAll('summary')].find(node=>norm(node.textContent)===expectedSummary);
  const scope=summary?.closest('details')??editor;
  const label=[...(scope?.querySelectorAll('label')??[])].filter(node=>norm(node.textContent).startsWith('Élément à retoucher'));
  const select=label.length===1?label[0].querySelector('select'):null;
  const status=norm(document.querySelector('.authoring-command-status')?.textContent);
  const busy=document.querySelector('.authoring-toolbar')?.getAttribute('aria-busy');
  const apply=document.querySelector('button.authoring-apply');
  const selected=String(select?.value??'');
  const selectedText=norm(select?.selectedOptions?.[0]?.textContent);
  const mentionsQuest=status.includes('Rapporter le Sceau');
  const mentionsItem=status.includes('Sceau');
  const mentionsQuantity=/quantité/i.test(status);
  const refusal=/Suppression refusée|source démontrée|utilisé par/i.test(status);
  const applyDisabled=!apply||!!apply.disabled;
  return {ready:!!editor&&label.length===1&&selected===expectedObject&&selectedText==='Sceau'&&busy==='false'&&refusal&&mentionsQuest&&mentionsItem&&mentionsQuantity&&applyDisabled,
    terminal:false,reason:'status='+status+'; busy='+busy+'; selected='+selectedText+'; apply_disabled='+applyDisabled,
    status,busy,selected,selected_text:selectedText,mentions_quest:mentionsQuest,mentions_item:mentionsItem,
    mentions_quantity:mentionsQuantity,refusal,apply_disabled:applyDisabled};
})()
"@
}

function Close-P03TidewaterZone {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    Close-Gate3PlaytestZone -Socket $Socket -TimeoutSeconds $TimeoutSeconds
    [void](Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'R3-P03 close Tidewater zone' -Expression @'
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const context=norm(document.querySelector('.authoring-context')?.textContent);
  const open=document.documentElement?.getAttribute('data-forge-zone-open');
  return {ready:context==='Aucune carte ouverte'&&open==='false',terminal:false,reason:'context='+context+'; open='+open};
})()
'@)
}

if ($p03RequestedSelfTest) {
    foreach ($name in @(
        'ConvertTo-CompactJson', 'ConvertFrom-ExactJson', 'Invoke-CdpCommand', 'Invoke-CdpExpression',
        'Wait-CdpReady', 'Wait-CdpTarget', 'Connect-CdpSocket', 'New-LoopbackPortReservation',
        'Wait-LaunchedExecutableIdentity', 'Send-CdpMouseClick', 'Invoke-UiClick',
        'Invoke-UiClickInDetailsBySummary', 'Open-DetailsBySummary', 'Copy-IsolatedFixture',
        'Get-ArchiveSnapshot', 'Test-ArchiveSnapshotsEqual', 'Get-FileSha256', 'Get-NormalizedSha256',
        'Assert-Gate3ProofPath', 'Assert-DriveEPath', 'Assert-NoExistingReparseAncestor', 'Assert-TreeHasNoReparsePoints',
        'Test-PathWithinDirectory', 'Test-IsWindowsPlatform', 'Open-Gate3TidewaterZone',
        'Wait-GameplayEditor', 'Wait-GameplayForm', 'Save-GameplayUiDiagnostic',
        'Get-GameplayOptionValue', 'Select-GameplayOptionByText', 'Get-UiControlByLabel',
        'Get-Gate3WorkspaceReference', 'Get-Gate3GameplayReport', 'Start-ForgeStudioPlaytestProcess',
        'Stop-ForgeStudioPlaytestProcess', 'Add-P03CdpTransportEntry', 'Connect-P03CdpSocket',
        'Invoke-P03MeasuredCdpCommand', 'Invoke-P03BrowserProtocolProbe', 'Start-P03InvalidQuestProcess'
    )) {
        [void](Get-Command $name -ErrorAction Stop)
    }
    foreach ($name in @('Get-P03GameplayFacts', 'Get-P03ArchiveState', 'Compare-P03ArchiveState',
        'Wait-P03InvalidQuestDiagnostic', 'Close-P03TidewaterZone')) {
        [void](Get-Command $name -ErrorAction Stop)
    }
    if ($p03ZoneId -cnotmatch '^[0-9a-f-]{36}$') { throw 'R3-P03 Tidewater zone UUID selector is malformed.' }
    $raw = [IO.File]::ReadAllText($PSCommandPath)
    foreach ($needle in @(
        'R3-P03', 'Examiner la suppression de cet élément', 'Élément à retoucher', 'Personnages, objets et objectifs',
        'Rapporter le Sceau', 'Sceau', 'required_quantity', 'mentions_quantity', 'apply_disabled',
        'source_original_hash_pinned', 'saved_reference', 'head_hash', 'no_new_receipt',
        'parent_candidate_sha256', 'candidate_differs_from_parent',
        '72598abab38bf2460ab6bebead8a02f364d19894fa1804a50dd6e4c92247445e',
        '2be5aab2dd674d5af8f67cd52aea6d61ba4a5de32a99c35855cc4d8cc908b4d4',
        'startup_diagnostics', '/json/list', 'Stop-P03StartupOwnedProcess',
        'Browser.getVersion', 'Target.getTargets', 'Target.activateTarget', 'renderer_settle_delay_ms', 'target_commands',
        'browser_commands', 'Connect-P03CdpSocket', 'Start-P03InvalidQuestProcess',
        'graceful_close_failure', 'cleanup_identity_verified',
        'Save-GameplayUiDiagnostic', 'Page.captureScreenshot', 'Close-Gate3PlaytestZone', 'gold-close-dialog',
        'gate_qualified', 'Input.dispatchMouseEvent', 'Input.dispatchKeyEvent', 'Stop-ForgeStudioPlaytestProcess'
    )) {
        if ($raw.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) {
            throw "R3-P03 selector or invariant missing: $needle"
        }
    }
    if ($raw -match 'Runtime\.evaluate[^\r\n]*window\.__') { throw 'SelfTest found a domain-state injection pattern.' }
    Write-Output 'TIDEWATER_GATE3_INVALID_QUEST_SELF_TEST_PASS'
    return
}

if (-not (Test-IsWindowsPlatform)) { throw 'Windows/WebView2 is required.' }
$candidate = [IO.Path]::GetFullPath($Candidate)
$source = [IO.Path]::GetFullPath($FreshIsolatedFixture)
$ledgerInput = [IO.Path]::GetFullPath($AuthoringLedger)
$output = [IO.Path]::GetFullPath($EvidenceRoot)
$sourcePin = Get-NormalizedSha256 -Value $SourceOriginalHashPinned
$currentFixturePins = Get-P03CurrentFixturePins -Enabled:$CurrentCandidateFixture -CandidatePin $ExpectedCandidateSha256 -ArchivePin $ExpectedAuthoredArchiveHash -HeadPin $ExpectedAuthoredHeadHash -LedgerPin $ExpectedAuthoringLedgerSha256
foreach ($entry in @(
        @($candidate, 'Studio candidate'),
        @($source, 'Fresh isolated authored fixture'),
        @($ledgerInput, 'Passed authoring ledger'),
        @($output, 'Evidence root')
    )) {
    Assert-Gate3ProofPath -LiteralPath $entry[0] -Description $entry[1]
}
if (Test-Path -LiteralPath $output) { throw "EvidenceRoot must be fresh: $output" }
if ((Test-PathWithinDirectory -LiteralPath $output -Directory $source) -or
    (Test-PathWithinDirectory -LiteralPath $source -Directory $output)) {
    throw 'EvidenceRoot and the input fixture must remain separate.'
}
if ((Test-PathWithinDirectory -LiteralPath $output -Directory (Split-Path -Parent $candidate)) -or
    (Test-PathWithinDirectory -LiteralPath (Split-Path -Parent $candidate) -Directory $output)) {
    throw 'EvidenceRoot and the candidate directory must remain separate.'
}
if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Studio candidate is absent: $candidate" }
if (-not (Test-Path -LiteralPath $ledgerInput -PathType Leaf)) { throw "Authoring ledger is absent: $ledgerInput" }
if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "Authored fixture is absent: $source" }
Assert-TreeHasNoReparsePoints -LiteralPath $source -Description 'Fresh authored Tidewater fixture'
$authoringLedgerHash = Get-FileSha256 -LiteralPath $ledgerInput
if ($CurrentCandidateFixture -and $authoringLedgerHash -cne $currentFixturePins.ledger_sha256) { throw 'Current authoring ledger differs from its explicit SHA-256 pin.' }

$authoring = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($ledgerInput))
if ([string]$authoring.schema -cne 'forge.tidewater.gate3-gameplay-authoring-smoke' -or
    [string]$authoring.status -cne 'passed') {
    throw 'Authoring ledger is not the passed Gate 3 gameplay authoring smoke.'
}
if ([bool]$authoring.gate_qualified -or -not [bool]$authoring.source_preserved) {
    throw 'Authoring ledger does not preserve the local-only, source-preserved boundary.'
}
if ([string]$authoring.zone_uuid -cne $p03ZoneId) { throw 'Authoring ledger zone UUID differs from Tidewater.' }
if (-not ($authoring.PSObject.Properties.Name -contains 'saved_reference') -or
    $null -eq $authoring.saved_reference) {
    throw 'Passed authoring ledger has no authored saved_reference checkpoint.'
}
if (-not ([IO.Path]::GetFullPath([string]$authoring.isolated_fixture)).Equals($source, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'FreshIsolatedFixture is not the authored fixture recorded by the ledger.'
}
if ($authoring.PSObject.Properties.Name -contains 'source_original_hash_pinned' -and
    (Get-NormalizedSha256 -Value ([string]$authoring.source_original_hash_pinned)) -cne $sourcePin) {
    throw 'SourceOriginalHashPinned differs from the passed authoring ledger.'
}
$managedHash = Get-NormalizedSha256 -Value ([string]$authoring.saved_reference.archive_hash)
$managedHeadHash = Get-NormalizedSha256 -Value ([string]$authoring.saved_reference.head_hash)
if ($managedHash -notmatch '^[0-9a-f]{64}$' -or $managedHeadHash -notmatch '^[0-9a-f]{64}$') {
    throw 'Passed authoring ledger has invalid authored object or head hashes.'
}
if ($CurrentCandidateFixture) {
    if ($managedHash -cne $currentFixturePins.archive_hash -or $managedHeadHash -cne $currentFixturePins.head_hash) { throw 'Current authoring ledger differs from the explicitly reviewed archive/head pins.' }
} elseif ($managedHash -cne $p03Base009ArchiveHash -or $managedHeadHash -cne $p03Base009HeadHash) {
    throw 'Passed authoring ledger does not reference the reviewed base009 authored archive/head.'
}

$candidateHash = Get-NormalizedSha256 -Value (Get-FileSha256 -LiteralPath $candidate)
$parentCandidateHash = Get-NormalizedSha256 -Value ([string]$authoring.candidate_sha256)
if ($parentCandidateHash -notmatch '^[0-9a-f]{64}$') {
    throw 'Passed authoring ledger has an invalid parent candidate SHA-256.'
}
if ($CurrentCandidateFixture) {
    if ($candidateHash -cne $currentFixturePins.candidate_sha256 -or $candidateHash -cne $parentCandidateHash) { throw 'Current candidate must match both its explicit pin and the authoring ledger candidate.' }
} elseif ($candidateHash -ceq $parentCandidateHash) {
    throw 'R3-P03 requires the V2 candidate SHA-256 to differ from the parent authoring candidate.'
}
$sourceArchive = Join-Path (Join-Path $source $p03ZoneId) 'authoring.json'
$sourceWorkspaceArchive = Join-Path $source 'workspace-v1\authoring.json'
$sourceObject = Join-Path (Join-Path $source 'workspace-v1\objects') "$sourcePin.json"
$managedObject = Join-Path (Join-Path $source 'workspace-v1\objects') "$managedHash.json"
$sourceBefore = Get-ArchiveSnapshot -LiteralPath $sourceArchive
$sourceWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $sourceWorkspaceArchive
$sourceObjectBefore = Get-ArchiveSnapshot -LiteralPath $sourceObject
$managedBefore = Get-ArchiveSnapshot -LiteralPath $managedObject
if ($sourceBefore.Sha256 -cne $sourcePin -or $sourceObjectBefore.Sha256 -cne $sourcePin) {
    throw 'Source archive/object does not match SourceOriginalHashPinned.'
}
$workspaceReferenceBefore = Get-Gate3WorkspaceReference -Archive $sourceWorkspaceArchive -ZoneId $p03ZoneId
if ((Get-NormalizedSha256 -Value ([string]$workspaceReferenceBefore.archive_hash)) -cne $managedHash -or
    (Get-NormalizedSha256 -Value ([string]$workspaceReferenceBefore.head_hash)) -cne $managedHeadHash) {
    throw 'Workspace does not reference the authored current object/head checkpoint from the passed ledger.'
}
if ($managedBefore.Sha256 -cne $managedHash) { throw 'Authored managed object hash does not match its workspace reference.' }
$sourcePlacementsBefore = Get-Gate3ArchivePlacementsSnapshot -Archive $sourceArchive
Assert-Gate3PlacementsPreserved -Before $sourcePlacementsBefore -After (Get-Gate3ArchivePlacementsSnapshot -Archive $sourceObject)
Assert-Gate3PlacementsPreserved -Before $sourcePlacementsBefore -After (Get-Gate3ArchivePlacementsSnapshot -Archive $managedObject)
Assert-Gate3PlacementsLedger -Ledger $authoring -SourcePin $sourcePin -Baseline $sourcePlacementsBefore
$facts = Get-P03GameplayFacts -Archive $managedObject
$initialState = Get-P03ArchiveState -ZoneArchive $sourceArchive -WorkspaceArchive $sourceWorkspaceArchive -ManagedObjectArchive $managedObject -ZoneId $p03ZoneId
$initialReceiptCount = @($initialState.managed_receipt_ids).Count

[void](New-Item -ItemType Directory -Path $output)
$outputCreated = $true
$fixture = Join-Path $output 'authoring-root'
$cloneArchive = Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $p03ZoneId
$cloneWorkspaceArchive = Join-Path $fixture 'workspace-v1\authoring.json'
$cloneSourceArchive = Join-Path (Join-Path $fixture $p03ZoneId) 'authoring.json'
$cloneSourceObject = Join-Path (Join-Path $fixture 'workspace-v1\objects') "$sourcePin.json"
$cloneManagedObject = Join-Path (Join-Path $fixture 'workspace-v1\objects') "$managedHash.json"
$cloneBeforeState = Get-P03ArchiveState -ZoneArchive $cloneSourceArchive -WorkspaceArchive $cloneWorkspaceArchive -ManagedObjectArchive $cloneManagedObject -ZoneId $p03ZoneId
if ($cloneBeforeState.zone_snapshot.Sha256 -cne $sourceBefore.Sha256 -or
    $cloneBeforeState.workspace_snapshot.Sha256 -cne $sourceWorkspaceBefore.Sha256 -or
    $cloneBeforeState.managed_snapshot.Sha256 -cne $managedBefore.Sha256 -or
    (Get-ArchiveSnapshot -LiteralPath $cloneSourceObject).Sha256 -cne $sourceObjectBefore.Sha256) {
    throw 'Isolated P03 fixture copy does not preserve the source and authored checkpoints.'
}

$savedEnvironment = @{}
foreach ($name in @('FORGE_AUTHORING_ROOT', 'FORGE_AUTHOR_SUBJECT', 'WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', 'WEBVIEW2_USER_DATA_FOLDER')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$run = $null
$socket = $null
$failure = $null
$forcedProcessStop = $false
$processClosed = $false
$nativeCloseConfirmed = $false
$gracefulCloseFailure = $null
$uiClosed = $false
$runCompleted = $false
$selectedObjectId = [string]$facts.Pickup.id
$selectedControl = $null
$diagnosticState = $null
$diagnosticIntegrity = $null
$finalState = $null
$finalIntegrity = $null
$finalPlacementsSnapshot = $null
$placementsPreserved = $false
$stopResult = $null

try {
    $run = Start-P03InvalidQuestProcess -CandidatePath $candidate -CandidateHash $candidateHash -FixtureRoot $fixture -EvidencePath $output -RunNumber 1 -StartupTimeout $StartupTimeoutSeconds
    $socket = $run.Socket
    Open-Gate3TidewaterZone -Socket $socket -ZoneId $p03ZoneId -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Invoke-UiClick -Socket $socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Wait-GameplayEditor -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Open-DetailsBySummary -Socket $socket -Summary $p03EditorSummary)
    [void](Wait-GameplayForm -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)

    $optionValue = Get-GameplayOptionValue -Socket $socket -Label 'Élément à retoucher' -Text $p03ItemName -TimeoutSeconds $InteractionTimeoutSeconds
    if ([string]$optionValue -cne $selectedObjectId) {
        throw "Public Sceau option value '$optionValue' differs from authored managed object id '$selectedObjectId'."
    }
    Select-GameplayOptionByText -Socket $socket -Label 'Élément à retoucher' -Text $p03ItemName -TimeoutSeconds $InteractionTimeoutSeconds
    $selectedControl = Get-UiControlByLabel -Socket $socket -Label 'Élément à retoucher' -DetailsSummary $p03EditorSummary
    if ([string]$selectedControl.value -cne $selectedObjectId -or [bool]$selectedControl.disabled) {
        throw 'Public gameplay selector did not retain the authored Sceau object selection.'
    }

    [void](Invoke-UiClickInDetailsBySummary -Socket $socket -Summary $p03EditorSummary -Text 'Examiner la suppression de cet élément')
    $diagnosticState = Wait-P03InvalidQuestDiagnostic -Socket $socket -ExpectedObjectId $selectedObjectId -TimeoutSeconds $InteractionTimeoutSeconds
    if (-not [bool]$diagnosticState.refusal -or -not [bool]$diagnosticState.mentions_quest -or
        -not [bool]$diagnosticState.mentions_item -or -not [bool]$diagnosticState.mentions_quantity -or
        -not [bool]$diagnosticState.apply_disabled) {
        throw "R3-P03 public diagnostic was incomplete: $($diagnosticState.reason)"
    }
    Save-GameplayUiDiagnostic -Socket $socket -OutputRoot $output -Name 'r3-p03-invalid-required-item-removal'
    $afterDiagnosticState = Get-P03ArchiveState -ZoneArchive $cloneSourceArchive -WorkspaceArchive $cloneWorkspaceArchive -ManagedObjectArchive $cloneManagedObject -ZoneId $p03ZoneId
    $diagnosticIntegrity = Compare-P03ArchiveState -Before $cloneBeforeState -After $afterDiagnosticState
    if (-not [bool]$diagnosticIntegrity.all_unchanged) {
        throw 'Guarded invalid quest deletion changed an archive, head revision/hash or receipt sequence.'
    }

    Close-P03TidewaterZone -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds
    $uiClosed = $true
    $afterCloseState = Get-P03ArchiveState -ZoneArchive $cloneSourceArchive -WorkspaceArchive $cloneWorkspaceArchive -ManagedObjectArchive $cloneManagedObject -ZoneId $p03ZoneId
    $closeIntegrity = Compare-P03ArchiveState -Before $cloneBeforeState -After $afterCloseState
    if (-not [bool]$closeIntegrity.all_unchanged) {
        throw 'Closing the refused-edit session changed an archive, head revision/hash or receipt sequence.'
    }
    $runCompleted = $true
}
catch {
    $failure = "R3-P03: $($_.Exception.Message)"
    if ($null -ne $socket -and $socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
        try { Save-GameplayUiDiagnostic -Socket $socket -OutputRoot $output -Name 'r3-p03-failure' }
        catch { Write-Verbose "R3-P03 failure UI capture: $($_.Exception.Message)" }
    }
    if ($null -eq $run) {
        $forcedProcessStop = $true
        $gracefulCloseFailure = 'Startup failed before a public native close could be requested.'
        Stop-P03StartupOwnedProcess -CandidatePath $candidate
    }
}
finally {
    if ($null -ne $run) {
        try {
            $stopResult = Stop-ForgeStudioPlaytestProcess -Run $run
            $forcedProcessStop = [bool]$stopResult.Forced
            $processClosed = [bool]$stopResult.Closed
            $gracefulCloseFailure = if ($stopResult.PSObject.Properties.Name -contains 'GracefulCloseFailure') {
                [string]$stopResult.GracefulCloseFailure
            } else {
                $null
            }
            $nativeCloseConfirmed = -not $forcedProcessStop -and $processClosed
        }
        catch {
            $failure = "R3-P03 native cleanup: $($_.Exception.Message); $failure"
            $forcedProcessStop = $true
            $processClosed = $false
            $nativeCloseConfirmed = $false
        }
    }
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
}

$sourceAfter = Get-ArchiveSnapshot -LiteralPath $sourceArchive
$sourceWorkspaceAfter = Get-ArchiveSnapshot -LiteralPath $sourceWorkspaceArchive
$sourceObjectAfter = Get-ArchiveSnapshot -LiteralPath $sourceObject
$sourcePreserved = (Test-ArchiveSnapshotsEqual -Before $sourceBefore -After $sourceAfter) -and
    (Test-ArchiveSnapshotsEqual -Before $sourceWorkspaceBefore -After $sourceWorkspaceAfter) -and
    (Test-ArchiveSnapshotsEqual -Before $sourceObjectBefore -After $sourceObjectAfter)
if (-not $sourcePreserved) { $failure = "Input fixture source changed; $failure" }
try {
    $finalState = Get-P03ArchiveState -ZoneArchive $cloneSourceArchive -WorkspaceArchive $cloneWorkspaceArchive -ManagedObjectArchive $cloneManagedObject -ZoneId $p03ZoneId
    $finalIntegrity = Compare-P03ArchiveState -Before $cloneBeforeState -After $finalState
    if (-not [bool]$finalIntegrity.all_unchanged) { $failure = "P03 final archive immutability failed; $failure" }
    $finalPlacementsSnapshot = Get-Gate3ArchivePlacementsSnapshot -Archive $cloneManagedObject
    Assert-Gate3PlacementsPreserved -Before $sourcePlacementsBefore -After $finalPlacementsSnapshot
    $placementsPreserved = $true
}
catch {
    $failure = "P03 final archive snapshot failed: $($_.Exception.Message); $failure"
}
if ($forcedProcessStop) { $failure = "Owned Studio process required a forced stop; $failure" }
if (-not $processClosed) { $failure = "Owned Studio process did not close cleanly; $failure" }
if (-not $nativeCloseConfirmed) { $failure = "Public native close confirmation was not proven without force; $failure" }
if (-not $uiClosed) { $failure = "Tidewater zone was not closed through the public UI; $failure" }
if (-not $runCompleted) { $failure = "R3-P03 scenario did not complete; $failure" }

$ledger = [ordered]@{
    schema = 'forge.tidewater.gate3-invalid-quest-smoke'
    version = 1
    status = if ([string]::IsNullOrWhiteSpace($failure)) { 'passed' } else { 'failed_closed' }
    failure = $failure
    gate_qualified = $false
    human_visual_review = $false
    candidate = $candidate
    candidate_sha256 = $candidateHash
    parent_candidate_sha256 = $parentCandidateHash
    harness = $PSCommandPath
    harness_sha256 = Get-FileSha256 -LiteralPath $PSCommandPath
    candidate_differs_from_parent = ($candidateHash -cne $parentCandidateHash)
    fixture_mode = if ($CurrentCandidateFixture) { 'explicit_current_candidate_fixture' } else { 'historical_base009_cross_candidate' }
    explicit_current_fixture_pins = $currentFixturePins
    source_fixture = $source
    source_original_hash_pinned = $sourcePin
    source_placements = [ordered]@{
        source_archive_sha256=$sourcePin
        representation='ConvertTo-Json -Depth 100 -Compress; complete ordered placement array; SHA-256 of UTF-8 without BOM'
        before=[ordered]@{count=$sourcePlacementsBefore.Count; sha256=$sourcePlacementsBefore.Sha256; canonical_json=$sourcePlacementsBefore.CanonicalJson}
        after=if ($null -ne $finalPlacementsSnapshot) { [ordered]@{count=$finalPlacementsSnapshot.Count; sha256=$finalPlacementsSnapshot.Sha256; canonical_json=$finalPlacementsSnapshot.CanonicalJson} } else { $null }
        identity_and_content_preserved=$placementsPreserved
    }
    passed_authoring_ledger = $ledgerInput
    passed_authoring_ledger_sha256 = $authoringLedgerHash
    authored_managed_object_hash = $managedHash
    authored_managed_head_hash = $managedHeadHash
    base009_saved_reference = if ($CurrentCandidateFixture) { $null } else { [ordered]@{
        archive_hash = $p03Base009ArchiveHash
        head_hash = $p03Base009HeadHash
    } }
    authored_quest_id = $facts.QuestId
    authored_required_item_id = $facts.RequiredItemId
    authored_required_quantity = $facts.RequiredQuantity
    authored_pickup_id = [string]$facts.Pickup.id
    authored_pickup_quantity = $facts.PickupQuantity
    ui_selected_object_id = $selectedObjectId
    ui_selected_control = $selectedControl
    diagnostic = $diagnosticState
    diagnostic_json = Join-Path $output 'r3-p03-invalid-required-item-removal-ui.json'
    diagnostic_screenshot = Join-Path $output 'r3-p03-invalid-required-item-removal.png'
    initial_archive_state = $cloneBeforeState
    diagnostic_integrity = $diagnosticIntegrity
    final_archive_state = $finalState
    final_integrity = $finalIntegrity
    source_archive_before = $sourceBefore
    source_archive_after = $sourceAfter
    source_workspace_before = $sourceWorkspaceBefore
    source_workspace_after = $sourceWorkspaceAfter
    source_object_before = $sourceObjectBefore
    source_object_after = $sourceObjectAfter
    source_preserved = $sourcePreserved
    ui_closed = $uiClosed
    native_close_confirmed = $nativeCloseConfirmed
    graceful_close_failure = $gracefulCloseFailure
    owned_process_closed = $processClosed
    forced_process_stop = $forcedProcessStop
    startup_diagnostics = $script:P03StartupDiagnostics
    initial_managed_receipt_count = $initialReceiptCount
    limitations = @(
        'This smoke exercises only the public Studio refusal diagnostic for deleting the unique pickup source of a required quest item.',
        'The source pin 50bfd... is the preserved legacy archive/object; the workspace is expected to reference the authored current managed object hash from the passed ledger.',
        'DOM and screenshot files are evidence outputs. No gameplay/map JSON is written and no Tauri command or hidden domain state is injected.',
        'R3-P03 local diagnostic evidence does not claim a complete playable route, visual acceptance or Gate qualification.'
    )
}

if ($outputCreated) {
    $ledgerPath = Join-Path $output 'gate3-invalid-quest-smoke-ledger.json'
    [IO.File]::WriteAllText($ledgerPath, ($ledger | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
    if ($ledger.status -cne 'passed') { throw "R3-P03 invalid quest smoke failed closed: $failure. Ledger: $ledgerPath" }
    Write-Output "TIDEWATER_GATE3_INVALID_QUEST_SMOKE_PASS ledger=$ledgerPath"
}
