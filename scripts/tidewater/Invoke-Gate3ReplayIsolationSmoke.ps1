#Requires -Version 7.2
# Gate 3 R3-P04 Tidewater replay/isolation smoke.  This is bounded public-UI
# evidence only; it never claims Gate qualification.
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
    [ValidateNotNullOrEmpty()]
    [string]$AuthoringLedger,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$SourceOriginalHashPinned,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$EvidenceRoot,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ReplayFixtureRoot,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateNotNullOrEmpty()]
    [string]$RuntimeRoot,

    [Parameter(ParameterSetName = 'Run')]
    [guid]$ZoneUuid = 'e620dd19-ead9-5913-a6d6-0f93109148c0',

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(15, 180)]
    [int]$StartupTimeoutSeconds = 60,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(30, 300)]
    [int]$InteractionTimeoutSeconds = 120,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Reuse the existing bounded CDP/fixture kit without importing any application
# state.  Keep this script's arguments under unique names because the helper has
# its own strict parameter block and dot-sources the Gate 2 kit.
$p04SelfTest = [bool]$SelfTest
$p04Candidate = $Candidate
$p04Fixture = $FreshIsolatedFixture
$p04AuthoringLedger = $AuthoringLedger
$p04SourcePin = $SourceOriginalHashPinned
$p04EvidenceRoot = $EvidenceRoot
$p04ReplayFixture = $ReplayFixtureRoot
$p04RuntimeRoot = $RuntimeRoot
$p04ZoneUuid = $ZoneUuid
$p04StartupTimeout = $StartupTimeoutSeconds
$p04InteractionTimeout = $InteractionTimeoutSeconds
. (Join-Path $PSScriptRoot 'Invoke-Gate3GameplayPlaytestSmoke.ps1') -SelfTest | Out-Null
if (-not $p04SelfTest) {
    $Candidate = $p04Candidate
    $FreshIsolatedFixture = $p04Fixture
    $AuthoringLedger = $p04AuthoringLedger
    $SourceOriginalHashPinned = $p04SourcePin
    $EvidenceRoot = $p04EvidenceRoot
    $ReplayFixtureRoot = $p04ReplayFixture
    $RuntimeRoot = $p04RuntimeRoot
    $ZoneUuid = $p04ZoneUuid
    $StartupTimeoutSeconds = $p04StartupTimeout
    $InteractionTimeoutSeconds = $p04InteractionTimeout
}

$zoneId = $p04ZoneUuid.ToString()
$gameplaySectionSelector = 'section.authoring-gameplay-controls'
$script:P04CdpRun = 0

function Get-P04PublicIdentity {
    param([Parameter(Mandatory)]$Socket)
    return Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const root=document.querySelector('details.authoring-zone-information');
  if(!root) return {present:false,zone:'',revision:null};
  const rows=[...root.querySelectorAll('dt')].map(node=>({key:norm(node.textContent),value:norm(node.nextElementSibling?.textContent)}));
  return {
    present:true,
    zone:rows.find(row=>row.key==='Zone')?.value??'',
    revision:rows.find(row=>row.key==='Révision')?.value??null
  };
})()
'@
}

function Get-P04ChosenStart {
    param([Parameter(Mandatory)]$Socket)
    return Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const details=[...document.querySelectorAll('details')].find(node=>norm(node.querySelector(':scope > summary')?.textContent)==='Choisir le départ du playtest');
  if(!details) return {present:false,x:null,z:null,facing:null};
  const read=prefix=>{
    const label=[...details.querySelectorAll('label')].find(node=>norm(node.textContent).startsWith(prefix));
    return label?.querySelector('input')?.value??null;
  };
  return {present:true,x:read('Départ X (m)'),z:read('Départ Z (m)'),facing:read('Cap du départ enregistré (°)')};
})()
'@
}

function Convert-P04Number {
    param([Parameter(Mandatory)][AllowNull()][string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) { return $null }
    return [double]::Parse($Text.Replace(',', '.'), [Globalization.CultureInfo]::InvariantCulture)
}

function Get-P04GameSaveSnapshot {
    param([Parameter(Mandatory)][string]$Root)
    $directory = Join-Path $Root '.game-saves'
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        return [ordered]@{ present = $false; files = @() }
    }
    $files = @(
        Get-ChildItem -LiteralPath $directory -File -Recurse | Sort-Object FullName | ForEach-Object {
            [ordered]@{
                relative = [IO.Path]::GetRelativePath($Root, $_.FullName)
                length = [int64]$_.Length
                sha256 = Get-FileSha256 -LiteralPath $_.FullName
            }
        }
    )
    return [ordered]@{ present = $true; files = $files }
}

function Test-P04GameSaveSnapshotEqual {
    param([Parameter(Mandatory)]$Before, [Parameter(Mandatory)]$After)
    return (ConvertTo-CompactJson -Value $Before) -ceq (ConvertTo-CompactJson -Value $After)
}

function Add-P04Journal {
    param(
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)][string]$Event,
        [AllowNull()][object]$Payload
    )
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event $Event -Payload $Payload
}

function Get-P04UiState {
    param([Parameter(Mandatory)]$Socket)
    $public = Get-PlaytestPublicState -Socket $Socket
    $identity = $null
    try {
        Open-DetailsBySummary -Socket $Socket -Summary 'Informations de la zone' | Out-Null
        Open-DetailsBySummary -Socket $Socket -Summary 'Identités techniques' | Out-Null
        $identity = Get-P04PublicIdentity -Socket $Socket
    }
    catch {
        $identity = [ordered]@{ present = $false; zone = ''; revision = $null; error = $_.Exception.Message }
    }
    return [pscustomobject]@{
        gameplay = $public
        identity = $identity
    }
}

function Invoke-P04ChosenStart {
    param(
        [Parameter(Mandatory)]$Run,
        [Parameter(Mandatory)][int]$RunNumber,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Diagnostics,
        [Parameter(Mandatory)][string]$EvidencePath
    )
    $socket = $Run.Socket
    [void](Invoke-UiClick -Socket $socket -Text 'Tester & suivi' -Selector '[data-authoring-tab-button]')
    Open-DetailsBySummary -Socket $socket -Summary 'Choisir le départ du playtest' | Out-Null
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $TimeoutSeconds -Description 'public chosen-start controls' -Expression @'
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const details=[...document.querySelectorAll('details')].find(node=>norm(node.querySelector(':scope > summary')?.textContent)==='Choisir le départ du playtest');
  const button=[...(details?.querySelectorAll('button')??[])].find(node=>norm(node.textContent)==='Reprendre les valeurs du départ accepté');
  return {ready:!!details&&!!button&&!button.disabled,terminal:false,reason:details?'chosen-start controls pending':'chosen-start details pending'};
})()
'@)
    $publicIdentity = $null
    try {
        Open-DetailsBySummary -Socket $socket -Summary 'Informations de la zone' | Out-Null
        Open-DetailsBySummary -Socket $socket -Summary 'Identités techniques' | Out-Null
        $publicIdentity = Get-P04PublicIdentity -Socket $socket
    }
    catch {
        throw "Run $RunNumber could not read the public zone revision: $($_.Exception.Message)"
    }
    if (-not $publicIdentity.present -or [string]$publicIdentity.zone -cne $zoneId -or [string]::IsNullOrWhiteSpace([string]$publicIdentity.revision)) {
        throw "Run $RunNumber public zone identity did not expose Tidewater and a revision."
    }
    $beforeReset = Get-P04ChosenStart -Socket $socket
    [void](Invoke-UiClickInDetailsBySummary -Socket $socket -Summary 'Choisir le départ du playtest' -Text 'Reprendre les valeurs du départ accepté')
    $afterReset = Get-P04ChosenStart -Socket $socket
    if (-not $afterReset.present) { throw "Run $RunNumber did not expose the chosen-start draft after public reset." }
    $x = Convert-P04Number -Text ([string]$afterReset.x)
    $z = Convert-P04Number -Text ([string]$afterReset.z)
    if ($null -eq $x -or $null -eq $z -or -not [double]::IsFinite($x) -or -not [double]::IsFinite($z)) {
        throw "Run $RunNumber public chosen-start values were not finite after reset."
    }
    Add-P04Journal -Journal $Journal -Phase "run$RunNumber" -Event 'public_start_reset' -Payload ([ordered]@{
        before = $beforeReset
        after = $afterReset
        control = 'Reprendre les valeurs du départ accepté'
    })
    [void](Invoke-UiClickInDetailsBySummary -Socket $socket -Summary 'Choisir le départ du playtest' -Text 'Jouer à cet endroit')
    [void](Wait-PlaytestReady -Socket $socket -TimeoutSeconds $TimeoutSeconds -Description "run $RunNumber public chosen-start playtest")
    Focus-PlaytestCanvas -Socket $socket -TimeoutSeconds $TimeoutSeconds
    $state = Get-PlaytestPublicState -Socket $socket
    $observation = Get-PlaytestObservation -State $state
    if (-not $observation.ok -or -not $observation.simulation_ready -or -not $observation.grounded) {
        throw "Run $RunNumber chosen-start playtest did not expose a finite grounded simulation-ready public state."
    }
    $capture = Capture-PlaytestCheckpoint -Socket $socket -EvidencePath $EvidencePath -RunNumber $RunNumber -Name 'chosen_start_ready' -Phase "run$RunNumber" -Journal $Journal -Diagnostics $Diagnostics
    Add-P04Journal -Journal $Journal -Phase "run$RunNumber" -Event 'chosen_start_started' -Payload ([ordered]@{
        requested_x = $x
        requested_z = $z
        observation = $observation
        ui_checkpoint = $capture
    })
    return [pscustomobject]@{
        requested_x = $x
        requested_z = $z
        public_identity = $publicIdentity
        before_reset = $beforeReset
        after_reset = $afterReset
        state = $state
        observation = $observation
        capture = $capture
    }
}

function Get-P04SaveResponse {
    param([AllowEmptyCollection()][Parameter(Mandatory)][object[]]$Outputs)
    if (@($Outputs | Where-Object { [string]$_ -ceq 'Partie sauvegardée.' }).Count -gt 0) { return 'acknowledged' }
    if (@($Outputs | Where-Object { [string]$_ -ceq 'action refused: wait for physical objects to settle before saving' }).Count -gt 0) { return 'physical_objects_not_settled' }
    return 'unexpected'
}

function Invoke-P04PublicSaveWithSettleRetry {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase
    )
    # A refused Save is a completed transaction. Waiting for that transaction's
    # old acknowledgement cannot observe objects settling later. Only this
    # exact public refusal permits another public click within the same bound.
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $attempt = 0
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $remaining = [Math]::Max(1, [int][Math]::Ceiling(($deadline - [DateTimeOffset]::UtcNow).TotalSeconds))
        $before = Wait-PlaytestCondition -Socket $Socket -TimeoutSeconds ([Math]::Min(30, $remaining)) -Description 'P04 public Save button available' -Condition {
            param($s)
            @($s.allButtons | Where-Object { $_.text -ceq 'Sauvegarder la partie' -and -not $_.disabled }).Count -eq 1
        }
        $beforeObservation = Get-PlaytestObservation -State $before
        if (-not $beforeObservation.ok -or -not $beforeObservation.simulation_ready -or -not $beforeObservation.grounded) { throw 'P04 public Save retry lost its grounded simulation-ready player.' }
        if ([DateTimeOffset]::UtcNow -ge $deadline) { break }
        $attempt += 1
        [void](Invoke-UiClick -Socket $Socket -Text 'Sauvegarder la partie')
        $beforeTick = [int64]$beforeObservation.tick
        $remaining = [Math]::Max(1, [int][Math]::Ceiling(($deadline - [DateTimeOffset]::UtcNow).TotalSeconds))
        $response = Wait-PlaytestCondition -Socket $Socket -TimeoutSeconds ([Math]::Min(30, $remaining)) -Description 'P04 completed public Save transaction' -Condition {
            param($s)
            $observed = Get-PlaytestObservation -State $s
            $feedback = @($s.outputs | Where-Object { [string]$_ -notmatch '^Vitalité\s' })
            $observed.ok -and $observed.tick -gt $beforeTick -and
                @($s.allButtons | Where-Object { $_.text -ceq 'Sauvegarder la partie' -and -not $_.disabled }).Count -eq 1 -and $feedback.Count -gt 0
        }
        $classification = Get-P04SaveResponse -Outputs @($response.outputs)
        Add-P04Journal -Journal $Journal -Phase $Phase -Event 'public_save_transaction' -Payload ([ordered]@{
            attempt = $attempt
            before = $beforeObservation
            after = Get-PlaytestObservation -State $response
            outputs = @($response.outputs)
            classification = $classification
            retry_scope = 'only exact public physical-objects-not-settled refusal; original time bound unchanged'
        })
        if ($classification -ceq 'acknowledged') { return $response }
        if ($classification -cne 'physical_objects_not_settled') { throw "P04 public Save returned an unexpected response: $(@($response.outputs) -join ' | ')" }
        if ([DateTimeOffset]::UtcNow -ge $deadline) { break }
        Start-Sleep -Milliseconds ([Math]::Min(2000, [int][Math]::Max(1, ($deadline - [DateTimeOffset]::UtcNow).TotalMilliseconds)))
    }
    throw "P04 public Save remained refused while physical objects settled within $TimeoutSeconds seconds."
}

function Get-P04TransportObservation {
    param([Parameter(Mandatory)]$Run)
    $observation = [ordered]@{
        observed_at_utc = [DateTimeOffset]::UtcNow.ToString('o')
        pid = $null
        port = $null
        profile = $null
        process_has_exited = $null
        process_exit_code = $null
        socket_state = $null
        cdp_targets = @()
        cdp_endpoint_error = $null
    }
    try {
        if ($Run.PSObject.Properties.Name -contains 'Process' -and $null -ne $Run.Process) {
            $observation.pid = [int]$Run.Process.Id
            $observation.process_has_exited = [bool]$Run.Process.HasExited
            if ($observation.process_has_exited) { $observation.process_exit_code = $Run.Process.ExitCode }
        }
    }
    catch { $observation.process_exit_code = "unavailable: $($_.Exception.Message)" }
    if ($Run.PSObject.Properties.Name -contains 'Port') { $observation.port = [int]$Run.Port }
    if ($Run.PSObject.Properties.Name -contains 'Profile') { $observation.profile = [string]$Run.Profile }
    if ($Run.PSObject.Properties.Name -contains 'Socket' -and $null -ne $Run.Socket) {
        try { $observation.socket_state = [string]$Run.Socket.State } catch { $observation.socket_state = "unavailable: $($_.Exception.Message)" }
    }
    if ($null -ne $observation.port) {
        try {
            $targets = @(Invoke-RestMethod -Uri ("http://127.0.0.1:{0}/json/list" -f $observation.port) -TimeoutSec 5)
            $observation.cdp_targets = @($targets | ForEach-Object {
                [ordered]@{
                    type = [string]$_.type
                    title = [string]$_.title
                    url = [string]$_.url
                    web_socket_debugger_url_present = -not [string]::IsNullOrWhiteSpace([string]$_.webSocketDebuggerUrl)
                }
            })
        }
        catch { $observation.cdp_endpoint_error = $_.Exception.Message }
    }
    return [pscustomobject]$observation
}

$script:P04LastStartObservation = $null

function Start-P04ForgeStudioPlaytestProcess {
    param(
        [Parameter(Mandatory)][string]$CandidatePath,
        [Parameter(Mandatory)][string]$CandidateHash,
        [Parameter(Mandatory)][string]$FixtureRoot,
        [Parameter(Mandatory)][string]$EvidencePath,
        [Parameter(Mandatory)][string]$RuntimePath,
        [Parameter(Mandatory)][int]$RunNumber,
        [Parameter(Mandatory)][int]$StartupTimeout
    )
    $profile = Join-Path $RuntimePath "webview2-user-data-$RunNumber"
    $tempRoot = Join-Path $RuntimePath "process-temp-$RunNumber"
    [void](New-Item -ItemType Directory -Path $profile -Force)
    [void](New-Item -ItemType Directory -Path $tempRoot -Force)
    $reservation = New-LoopbackPortReservation
    $process = $null
    $socket = $null
    $observation = [ordered]@{
        run_number = $RunNumber
        candidate = $CandidatePath
        profile = $profile
        temp_root = $tempRoot
        port = [int]$reservation.Port
        pid = $null
        target_type = $null
        target_title = $null
        target_url = $null
        target_web_socket_present = $false
        socket_state_before_first_command = $null
        first_command_method = $null
        first_command_delay_ms = 750
        failure = $null
        process_has_exited = $null
        process_exit_code = $null
        cleanup_forced = $false
    }
    try {
        [Environment]::SetEnvironmentVariable('FORGE_AUTHORING_ROOT', $FixtureRoot, 'Process')
        [Environment]::SetEnvironmentVariable('FORGE_AUTHOR_SUBJECT', "tidewater-gate3-replay-isolation-$RunNumber", 'Process')
        [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', "--remote-debugging-address=127.0.0.1 --remote-debugging-port=$($reservation.Port)", 'Process')
        [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER', $profile, 'Process')
        $reservation.Listener.Stop()
        $stdout = Join-Path $EvidencePath "candidate-run-$RunNumber.stdout.log"
        $stderr = Join-Path $EvidencePath "candidate-run-$RunNumber.stderr.log"
        $savedTemp = [Environment]::GetEnvironmentVariable('TEMP', 'Process')
        $savedTmp = [Environment]::GetEnvironmentVariable('TMP', 'Process')
        try {
            [Environment]::SetEnvironmentVariable('TEMP', $tempRoot, 'Process')
            [Environment]::SetEnvironmentVariable('TMP', $tempRoot, 'Process')
            $process = Start-Process -FilePath $CandidatePath -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        }
        finally {
            [Environment]::SetEnvironmentVariable('TEMP', $savedTemp, 'Process')
            [Environment]::SetEnvironmentVariable('TMP', $savedTmp, 'Process')
        }
        $observation.pid = [int]$process.Id
        [void](Wait-LaunchedExecutableIdentity -Process $process -Candidate $CandidatePath -ExpectedHash $CandidateHash -TimeoutSeconds ([Math]::Min(15, $StartupTimeout)))
        $target = Wait-CdpTarget -Port $reservation.Port -TimeoutSeconds $StartupTimeout -Process $process
        $observation.target_type = [string]$target.type
        $observation.target_title = [string]$target.title
        $observation.target_url = [string]$target.url
        $observation.target_web_socket_present = -not [string]::IsNullOrWhiteSpace([string]$target.webSocketDebuggerUrl)
        $socket = Connect-CdpSocket -WebSocketUrl ([string]$target.webSocketDebuggerUrl)
        $observation.socket_state_before_first_command = [string]$socket.State
        $script:P04LastStartObservation = [pscustomobject]$observation
        Start-Sleep -Milliseconds ([int]$observation.first_command_delay_ms)
        foreach ($method in @('Runtime.enable', 'Page.enable', 'Page.bringToFront')) {
            $observation.first_command_method = $method
            $script:P04LastStartObservation = [pscustomobject]$observation
            [void](Invoke-CdpCommand -Socket $socket -Method $method)
        }
        [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $StartupTimeout -Description "Studio shell run $RunNumber" -Expression @'
(() => ({ready:document.readyState==='complete'&&!!document.querySelector('.authoring-shell'),terminal:false,reason:'authoring shell pending'}))()
'@)
        $script:P04LastStartObservation = [pscustomobject]$observation
        return [pscustomobject]@{ Process = $process; Socket = $socket; Reservation = $reservation; Port = $reservation.Port; Profile = $profile; RunNumber = $RunNumber; Forced = $false; P04StartObservation = [pscustomobject]$observation }
    }
    catch {
        $observation.failure = $_.Exception.Message
        try {
            if ($null -ne $process) {
                $process.Refresh()
                $observation.process_has_exited = [bool]$process.HasExited
                if ($observation.process_has_exited) { $observation.process_exit_code = $process.ExitCode }
            }
        }
        catch { $observation.process_exit_code = "unavailable: $($_.Exception.Message)" }
        try { if ($null -ne $socket) { $observation.socket_state_after_failure = [string]$socket.State } } catch {}
        $script:P04LastStartObservation = [pscustomobject]$observation
        if ($null -ne $socket) { try { $socket.Dispose() } catch {} }
        if ($null -ne $process -and -not $process.HasExited) {
            try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue; $observation.cleanup_forced = $true } catch {}
        }
        if ($null -ne $reservation) { $reservation.Listener.Stop() }
        throw
    }
}

function Invoke-P04Run {
    param(
        [Parameter(Mandatory)][string]$CandidatePath,
        [Parameter(Mandatory)][string]$CandidateHash,
        [Parameter(Mandatory)][string]$FixtureRoot,
        [Parameter(Mandatory)][string]$EvidencePath,
        [Parameter(Mandatory)][string]$RuntimePath,
        [Parameter(Mandatory)][int]$RunNumber,
        [Parameter(Mandatory)][int]$StartupTimeout,
        [Parameter(Mandatory)][int]$InteractionTimeout,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Diagnostics,
        [switch]$SaveGame
    )
    $run = $null
    $completed = $false
    $failure = $null
    $chosen = $null
    $saveAcknowledged = $false
    $saveState = $null
    $mutatedObservation = $null
    $forcedStop = $false
    $closed = $false
    $transportBeforeFirstCommand = $null
    $firstCommandDelayMilliseconds = 750
    try {
        $script:P04LastStartObservation = $null
        $run = Start-P04ForgeStudioPlaytestProcess -CandidatePath $CandidatePath -CandidateHash $CandidateHash -FixtureRoot $FixtureRoot -EvidencePath $EvidencePath -RuntimePath $RuntimePath -RunNumber $RunNumber -StartupTimeout $StartupTimeout
        $transportBeforeFirstCommand = if ($run.PSObject.Properties.Name -contains 'P04StartObservation') { $run.P04StartObservation } else { Get-P04TransportObservation -Run $run }
        Add-P04Journal -Journal $Journal -Phase "run$RunNumber" -Event 'cdp_transport_ready' -Payload $transportBeforeFirstCommand
        Start-Sleep -Milliseconds $firstCommandDelayMilliseconds
        Open-Gate3PlaytestZone -Socket $run.Socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeout
        $chosen = Invoke-P04ChosenStart -Run $run -RunNumber $RunNumber -TimeoutSeconds $InteractionTimeout -Journal $Journal -Diagnostics $Diagnostics -EvidencePath $EvidencePath
        if ($SaveGame) {
            # Create a small public runtime delta before saving.  This makes the
            # second direct start distinguishable from a hidden resume while
            # keeping the route and the authored document untouched.
            Send-CdpKeyHold -Socket $run.Socket -Key 'd' -Code 'KeyD' -VirtualKeyCode 68 -Milliseconds 450
            [void](Wait-PlaytestSettled -Socket $run.Socket -TimeoutSeconds $InteractionTimeout -Journal $Journal -Phase "run${RunNumber}_public_delta")
            $mutatedObservation = Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $run.Socket)
            if (-not $mutatedObservation.ok -or -not $mutatedObservation.simulation_ready) { throw "Run $RunNumber public runtime delta did not expose a finite state." }
            Add-P04Journal -Journal $Journal -Phase "run${RunNumber}_public_delta" -Event 'public_runtime_delta' -Payload $mutatedObservation
            [void](Wait-PlaytestSettled -Socket $run.Socket -TimeoutSeconds $InteractionTimeout -Journal $Journal -Phase "run${RunNumber}_save")
            Release-PlaytestCapture -Socket $run.Socket -TimeoutSeconds $InteractionTimeout
            [void](Invoke-P04PublicSaveWithSettleRetry -Socket $run.Socket -TimeoutSeconds $InteractionTimeout -Journal $Journal -Phase "run${RunNumber}_save")
            $saveAcknowledged = $true
            $saveState = Get-P04UiState -Socket $run.Socket
            $saveCapture = Capture-PlaytestCheckpoint -Socket $run.Socket -EvidencePath $EvidencePath -RunNumber $RunNumber -Name 'game_save_acknowledged' -Phase "run${RunNumber}_save" -Journal $Journal -Diagnostics $Diagnostics
            Add-P04Journal -Journal $Journal -Phase "run${RunNumber}_save" -Event 'game_save_acknowledged' -Payload ([ordered]@{ state = $saveState; ui_checkpoint = $saveCapture })
        }
        Close-Gate3PlaytestZone -Socket $run.Socket -TimeoutSeconds $InteractionTimeout
        $completed = $true
    }
    catch {
        $failure = $_.Exception.Message
        if ($null -eq $transportBeforeFirstCommand -and $null -ne $script:P04LastStartObservation) {
            $transportBeforeFirstCommand = $script:P04LastStartObservation
        }
        Add-P04Journal -Journal $Journal -Phase "run${RunNumber}_failure" -Event 'run_failed' -Payload ([ordered]@{
            error = $failure
            transport_before_first_command = $transportBeforeFirstCommand
        })
        if ($null -ne $run -and $null -ne $run.Socket) {
            try { [void](Capture-PlaytestCheckpoint -Socket $run.Socket -EvidencePath $EvidencePath -RunNumber $RunNumber -Name 'failure' -Phase "run${RunNumber}_failure" -Journal $Journal -Diagnostics $Diagnostics) } catch {}
        }
    }
    finally {
        if ($null -ne $run) {
            $stop = Stop-ForgeStudioPlaytestProcess -Run $run
            $forcedStop = [bool]$stop.Forced
            $closed = [bool]$stop.Closed
        }
    }
    return [pscustomobject]@{
        run_number = $RunNumber
        completed = $completed
        failure = $failure
        chosen = $chosen
        mutated_observation = $mutatedObservation
        save_acknowledged = $saveAcknowledged
        save_state = $saveState
        forced_stop = $forcedStop
        closed = $closed
        transport_before_first_command = $transportBeforeFirstCommand
        first_command_delay_ms = $firstCommandDelayMilliseconds
    }
}

if ($p04SelfTest) {
    if ((Get-P04SaveResponse -Outputs @('Vitalité 100 · Endurance 100', 'Partie sauvegardée.')) -cne 'acknowledged') { throw 'P04 public Save acknowledgement classification failed.' }
    if ((Get-P04SaveResponse -Outputs @('action refused: wait for physical objects to settle before saving')) -cne 'physical_objects_not_settled') { throw 'P04 exact public settle refusal classification failed.' }
    if ((Get-P04SaveResponse -Outputs @('action refused: wait for the current physics projection after compatible edits')) -cne 'unexpected') { throw 'P04 unexpectedly retried a different physical refusal.' }
    if ((Get-P04SaveResponse -Outputs @('Partie sauvegardée incomplet.')) -cne 'unexpected') { throw 'P04 accepted a partial public Save acknowledgement.' }
    foreach ($name in @(
        'ConvertTo-CompactJson', 'ConvertFrom-ExactJson', 'Get-FileSha256', 'Get-NormalizedSha256',
        'Get-ArchiveSnapshot', 'Test-ArchiveSnapshotsEqual', 'Copy-IsolatedFixture',
        'Assert-Gate3ProofPath', 'Assert-NoExistingReparseAncestor', 'Assert-TreeHasNoReparsePoints',
        'Test-PathWithinDirectory', 'Test-IsWindowsPlatform', 'Open-Gate3PlaytestZone',
        'Get-PlaytestPublicState', 'Get-PlaytestObservation', 'Capture-PlaytestCheckpoint',
        'Wait-PlaytestReady', 'Wait-PlaytestSettled', 'Wait-PlaytestCondition',
        'Invoke-UiClick', 'Invoke-UiClickInDetailsBySummary', 'Open-DetailsBySummary', 'Send-CdpKeyHold',
        'Start-ForgeStudioPlaytestProcess', 'Stop-ForgeStudioPlaytestProcess', 'Close-Gate3PlaytestZone',
        'Get-WorkspaceZoneReference'
    )) { [void](Get-Command $name -ErrorAction Stop) }
    foreach ($name in @('Get-P04PublicIdentity', 'Get-P04ChosenStart', 'Get-P04GameSaveSnapshot', 'Get-P04TransportObservation', 'Start-P04ForgeStudioPlaytestProcess', 'Invoke-P04ChosenStart', 'Invoke-P04Run')) {
        [void](Get-Command $name -ErrorAction Stop)
    }
    $raw = [IO.File]::ReadAllText($PSCommandPath)
    foreach ($needle in @(
        'R3-P04', 'Choisir le départ du playtest', 'Reprendre les valeurs du départ accepté',
        'Jouer à cet endroit', 'Sauvegarder la partie', 'Partie sauvegardée.',
        'source_preserved', 'source_archive_before', 'public_revision', 'game_save_before',
        'game_save_after', 'run1', 'run2', 'simulation_isolated', 'ReplayFixtureRoot',
        'gate_qualified = $false'
    )) {
        if ($raw.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "R3-P04 smoke selector/ledger limit missing: $needle" }
    }
    foreach ($forbidden in @(('window.' + '__'), ('Game' + 'Save' + 'Request'), ('Studio' + 'Command::' + 'GameSave'), ('AuthoringRequest::' + 'Apply'), ('AuthoringRequest::' + 'Preview'))) {
        if ($raw.IndexOf($forbidden, [StringComparison]::Ordinal) -ge 0) { throw "R3-P04 smoke contains forbidden state/source injection marker: $forbidden" }
    }
    $left = [ordered]@{ present = $false; files = @() }
    $right = [ordered]@{ present = $false; files = @() }
    if (-not (Test-P04GameSaveSnapshotEqual -Before $left -After $right)) { throw 'Game-save snapshot equality self-test failed.' }
    Write-Output 'TIDEWATER_GATE3_REPLAY_ISOLATION_SMOKE_SELF_TEST_PASS'
    return
}

if (-not (Test-IsWindowsPlatform)) { throw 'Windows/WebView2 is required.' }
$candidate = [IO.Path]::GetFullPath($p04Candidate)
$source = [IO.Path]::GetFullPath($p04Fixture)
$ledgerInput = [IO.Path]::GetFullPath($p04AuthoringLedger)
$output = [IO.Path]::GetFullPath($p04EvidenceRoot)
$replayFixture = [IO.Path]::GetFullPath($p04ReplayFixture)
$runtimeRoot = if ([string]::IsNullOrWhiteSpace($p04RuntimeRoot)) { $output } else { [IO.Path]::GetFullPath($p04RuntimeRoot) }
$sourcePin = Get-NormalizedSha256 -Value $p04SourcePin
foreach ($entry in @(@($candidate, 'Candidate'), @($source, 'Fresh authored fixture'), @($ledgerInput, 'Authoring ledger'), @($output, 'Evidence root'), @($replayFixture, 'Replay fixture root'), @($runtimeRoot, 'Runtime root'))) {
    Assert-Gate3ProofPath -LiteralPath $entry[0] -Description $entry[1]
}
if (Test-Path -LiteralPath $output) { throw "EvidenceRoot must be fresh: $output" }
if (Test-Path -LiteralPath $replayFixture) { throw "ReplayFixtureRoot must be fresh: $replayFixture" }
if ($runtimeRoot -cne $output -and (Test-Path -LiteralPath $runtimeRoot)) { throw "RuntimeRoot must be fresh: $runtimeRoot" }
if ((Test-PathWithinDirectory -LiteralPath $replayFixture -Directory $source) -or (Test-PathWithinDirectory -LiteralPath $source -Directory $replayFixture)) { throw 'Replay fixture and authored source fixture must remain separate.' }
if ((Test-PathWithinDirectory -LiteralPath $replayFixture -Directory $output) -or (Test-PathWithinDirectory -LiteralPath $output -Directory $replayFixture)) { throw 'Replay fixture and evidence root must remain separate.' }
if ((Test-PathWithinDirectory -LiteralPath $runtimeRoot -Directory $source) -or (Test-PathWithinDirectory -LiteralPath $source -Directory $runtimeRoot)) { throw 'Runtime root and authored source fixture must remain separate.' }
if ((Test-PathWithinDirectory -LiteralPath $runtimeRoot -Directory $replayFixture) -or (Test-PathWithinDirectory -LiteralPath $replayFixture -Directory $runtimeRoot)) { throw 'Runtime root and replay fixture must remain separate.' }
if ((Test-PathWithinDirectory -LiteralPath $runtimeRoot -Directory $output) -or (Test-PathWithinDirectory -LiteralPath $output -Directory $runtimeRoot)) { throw 'Runtime root and evidence root must remain separate.' }
if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Candidate is absent: $candidate" }
if (-not (Test-Path -LiteralPath $ledgerInput -PathType Leaf)) { throw "Authoring ledger is absent: $ledgerInput" }
if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "Fresh authored fixture is absent: $source" }
Assert-TreeHasNoReparsePoints -LiteralPath $source -Description 'Fresh authored Tidewater fixture'

$candidateHash = Get-FileSha256 -LiteralPath $candidate
$authoring = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($ledgerInput))
if ([string]$authoring.schema -cne 'forge.tidewater.gate3-gameplay-authoring-smoke' -or [string]$authoring.status -cne 'passed') { throw 'Authoring ledger is not the passed Gate 3 gameplay authoring smoke.' }
if ([bool]$authoring.gate_qualified) { throw 'Authoring ledger incorrectly claims Gate qualification.' }
if (-not [bool]$authoring.source_preserved) { throw 'Authoring ledger does not attest source preservation.' }
if ([string]$authoring.zone_uuid -cne $zoneId) { throw 'Authoring ledger zone UUID differs from Tidewater.' }
if (-not ([IO.Path]::GetFullPath([string]$authoring.isolated_fixture)).Equals($source, [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture differs from the authoring ledger.' }
if ($authoring.PSObject.Properties.Name -contains 'source_original_hash_pinned' -and [string]$authoring.source_original_hash_pinned -cne $sourcePin) { throw 'Source hash pin differs from the authoring ledger.' }
if ($authoring.PSObject.Properties.Name -contains 'candidate_sha256' -and [string]$authoring.candidate_sha256 -cne $candidateHash) { throw 'Candidate hash differs from the authoring ledger pin.' }

$sourceArchive = Join-Path (Join-Path $source $zoneId) 'authoring.json'
$sourceWorkspaceArchive = Join-Path $source 'workspace-v1\authoring.json'
$sourceObject = Join-Path (Join-Path $source 'workspace-v1\objects') "$sourcePin.json"
$sourceBefore = Get-ArchiveSnapshot -LiteralPath $sourceArchive
$sourceWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $sourceWorkspaceArchive
$sourceObjectBefore = Get-ArchiveSnapshot -LiteralPath $sourceObject
if ($sourceBefore.Sha256 -cne $sourcePin -or $sourceObjectBefore.Sha256 -cne $sourcePin) { throw 'Fixture does not preserve the pinned source object bytes.' }
$reference = Get-WorkspaceZoneReference -Archive $sourceWorkspaceArchive -ZoneId $zoneId
$authoredArchivePin = Get-NormalizedSha256 -Value ([string]$authoring.saved_reference.archive_hash)
if ([string]$reference.archive_hash -cne $authoredArchivePin -or [string]$reference.head_hash -cne [string]$authoring.saved_reference.head_hash) { throw 'Workspace-v1 does not reference the passed authoring checkpoint.' }
$sourceAuthoredObject = Join-Path (Join-Path $source 'workspace-v1\objects') "$authoredArchivePin.json"
$sourceAuthoredBefore = Get-ArchiveSnapshot -LiteralPath $sourceAuthoredObject
if ($sourceAuthoredBefore.Sha256 -cne $authoredArchivePin) { throw 'Authored checkpoint differs from the authoring ledger.' }
$authoredDocument = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($sourceAuthoredObject))
$authoredRevision = $null
if ($authoredDocument.head.PSObject.Properties.Name -contains 'revision') { $authoredRevision = [int64]$authoredDocument.head.revision }

[void](New-Item -ItemType Directory -Path $output)
$outputCreated = $true
$fixture = $replayFixture
$cloneArchive = Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $zoneId
$cloneWorkspace = Join-Path $fixture 'workspace-v1\authoring.json'
$cloneOriginal = Join-Path (Join-Path $fixture 'workspace-v1\objects') "$sourcePin.json"
$cloneAuthored = Join-Path (Join-Path $fixture 'workspace-v1\objects') "$authoredArchivePin.json"
$cloneBefore = Get-ArchiveSnapshot -LiteralPath $cloneArchive
$cloneWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $cloneWorkspace
$cloneOriginalBefore = Get-ArchiveSnapshot -LiteralPath $cloneOriginal
$cloneAuthoredBefore = Get-ArchiveSnapshot -LiteralPath $cloneAuthored
$cloneGameSaveBefore = Get-P04GameSaveSnapshot -Root $fixture
$sourceGameSaveBefore = Get-P04GameSaveSnapshot -Root $source

$savedEnvironment = @{}
foreach ($name in @('FORGE_AUTHORING_ROOT', 'FORGE_AUTHOR_SUBJECT', 'WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', 'WEBVIEW2_USER_DATA_FOLDER')) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$journal = [System.Collections.Generic.List[object]]::new()
$uiDiagnostics = [System.Collections.Generic.List[object]]::new()
$run1 = $null
$run2 = $null
$failure = $null
$run1Result = $null
$run2Result = $null
$run1Start = $null
$run2Start = $null
$forcedProcessStop = $false

try {
    $run1Result = Invoke-P04Run -CandidatePath $candidate -CandidateHash $candidateHash -FixtureRoot $fixture -EvidencePath $output -RuntimePath $runtimeRoot -RunNumber 1 -StartupTimeout $p04StartupTimeout -InteractionTimeout $p04InteractionTimeout -Journal $journal -Diagnostics $uiDiagnostics -SaveGame
    $run1Start = $run1Result.chosen
    if (-not $run1Result.completed) { throw "run1 failed: $($run1Result.failure)" }
    $run2Result = Invoke-P04Run -CandidatePath $candidate -CandidateHash $candidateHash -FixtureRoot $fixture -EvidencePath $output -RuntimePath $runtimeRoot -RunNumber 2 -StartupTimeout $p04StartupTimeout -InteractionTimeout $p04InteractionTimeout -Journal $journal -Diagnostics $uiDiagnostics
    $run2Start = $run2Result.chosen
    if (-not $run2Result.completed) { throw "run2 failed: $($run2Result.failure)" }
}
catch { $failure = $_.Exception.Message }
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
}

$sourceAfter = Get-ArchiveSnapshot -LiteralPath $sourceArchive
$sourceWorkspaceAfter = Get-ArchiveSnapshot -LiteralPath $sourceWorkspaceArchive
$sourceObjectAfter = Get-ArchiveSnapshot -LiteralPath $sourceObject
$sourceAuthoredAfter = Get-ArchiveSnapshot -LiteralPath $sourceAuthoredObject
$cloneAfter = Get-ArchiveSnapshot -LiteralPath $cloneArchive
$cloneWorkspaceAfter = Get-ArchiveSnapshot -LiteralPath $cloneWorkspace
$cloneOriginalAfter = Get-ArchiveSnapshot -LiteralPath $cloneOriginal
$cloneAuthoredAfter = Get-ArchiveSnapshot -LiteralPath $cloneAuthored
$cloneGameSaveAfter = Get-P04GameSaveSnapshot -Root $fixture
$sourceGameSaveAfter = Get-P04GameSaveSnapshot -Root $source
$sourcePreserved = (Test-ArchiveSnapshotsEqual -Before $sourceBefore -After $sourceAfter) -and (Test-ArchiveSnapshotsEqual -Before $sourceWorkspaceBefore -After $sourceWorkspaceAfter) -and (Test-ArchiveSnapshotsEqual -Before $sourceObjectBefore -After $sourceObjectAfter) -and (Test-ArchiveSnapshotsEqual -Before $sourceAuthoredBefore -After $sourceAuthoredAfter)
$cloneSourcePreserved = (Test-ArchiveSnapshotsEqual -Before $cloneBefore -After $cloneAfter) -and (Test-ArchiveSnapshotsEqual -Before $cloneWorkspaceBefore -After $cloneWorkspaceAfter) -and (Test-ArchiveSnapshotsEqual -Before $cloneOriginalBefore -After $cloneOriginalAfter) -and (Test-ArchiveSnapshotsEqual -Before $cloneAuthoredBefore -After $cloneAuthoredAfter)
$sourceGameSavePreserved = Test-P04GameSaveSnapshotEqual -Before $sourceGameSaveBefore -After $sourceGameSaveAfter
$sameChosenStart = $false
$samePublicRevision = $false
$run1Moved = $false
if ($null -ne $run1Start -and $null -ne $run2Start) {
    $sameChosenStart = [Math]::Abs([double]$run1Start.requested_x - [double]$run2Start.requested_x) -le 0.001 -and [Math]::Abs([double]$run1Start.requested_z - [double]$run2Start.requested_z) -le 0.001 -and [Math]::Abs([double]$run1Start.observation.x - [double]$run2Start.observation.x) -le 0.20 -and [Math]::Abs([double]$run1Start.observation.z - [double]$run2Start.observation.z) -le 0.20
    $r1 = [string]$run1Start.capture.status
    $r2 = [string]$run2Start.capture.status
    $publicRevision1 = [string]$run1Start.public_identity.revision
    $publicRevision2 = [string]$run2Start.public_identity.revision
    $samePublicRevision = $publicRevision1.Length -gt 0 -and $publicRevision1 -ceq $publicRevision2
    if ($null -ne $run1Result.mutated_observation -and $run1Result.mutated_observation.ok) {
        $run1Moved = [Math]::Abs([double]$run1Result.mutated_observation.x - [double]$run1Start.observation.x) -gt 0.05 -or [Math]::Abs([double]$run1Result.mutated_observation.z - [double]$run1Start.observation.z) -gt 0.05
    }
}
$saveObserved = $null -ne $run1Result -and [bool]$run1Result.save_acknowledged -and @($cloneGameSaveAfter.files).Count -ge 1
$simulationIsolated = $sameChosenStart -and $run1Moved -and $null -ne $run1Result -and $null -ne $run2Result -and [bool]$run1Result.completed -and [bool]$run2Result.completed
if (-not $sourcePreserved) { $failure = "source fixture changed during replay smoke; $failure" }
if (-not $cloneSourcePreserved) { $failure = "isolated authoring source changed during replay smoke; $failure" }
if (-not $sourceGameSavePreserved) { $failure = "source fixture game-save store changed during replay smoke; $failure" }
if (-not $sameChosenStart) { $failure = "two public reset/start runs did not reproduce the same chosen start; $failure" }
if (-not $samePublicRevision) { $failure = "two public runs did not expose the same accepted zone revision; $failure" }
if (-not $run1Moved) { $failure = "run1 did not produce the bounded public runtime delta used to test isolation; $failure" }
if (-not $saveObserved) { $failure = "public game save was not observed in the isolated game-save store; $failure" }
if (($null -ne $run1Result -and [bool]$run1Result.forced_stop) -or ($null -ne $run2Result -and [bool]$run2Result.forced_stop)) { $failure = "an owned Studio process required forced stop; $failure" }
if (@($uiDiagnostics | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.error) }).Count -gt 0) { $failure = "required public UI evidence was incomplete; $failure" }

$ledger = [ordered]@{
    schema = 'forge.tidewater.gate3-replay-isolation-smoke'
    version = 1
    criterion = 'R3-P04'
    status = if ([string]::IsNullOrWhiteSpace($failure) -and $simulationIsolated -and $samePublicRevision -and $sourcePreserved -and $cloneSourcePreserved -and $saveObserved) { 'passed' } else { 'failed_closed' }
    failure = $failure
    gate_qualified = $false
    human_visual_review = $false
    public_save_retry = 'bounded retry only after exact physical-objects-not-settled public refusal; no guard or source change'
    harness = $PSCommandPath
    harness_sha256 = Get-FileSha256 -LiteralPath $PSCommandPath
    authoring_ledger = $ledgerInput
    candidate = $candidate
    candidate_sha256 = $candidateHash
    source_fixture = $source
    source_original_hash_pinned = $sourcePin
    evidence_root = $output
    source_archive_before = $sourceBefore
    source_archive_after = $sourceAfter
    source_workspace_before = $sourceWorkspaceBefore
    source_workspace_after = $sourceWorkspaceAfter
    source_object_before = $sourceObjectBefore
    source_object_after = $sourceObjectAfter
    authored_checkpoint = $reference
    authored_revision_from_checkpoint = $authoredRevision
    authored_object_before = $sourceAuthoredBefore
    authored_object_after = $sourceAuthoredAfter
    source_preserved = $sourcePreserved
    isolated_fixture = $fixture
    isolated_archive_before = $cloneBefore
    isolated_archive_after = $cloneAfter
    isolated_workspace_before = $cloneWorkspaceBefore
    isolated_workspace_after = $cloneWorkspaceAfter
    isolated_original_object_before = $cloneOriginalBefore
    isolated_original_object_after = $cloneOriginalAfter
    isolated_authored_object_before = $cloneAuthoredBefore
    isolated_authored_object_after = $cloneAuthoredAfter
    clone_source_preserved = $cloneSourcePreserved
    source_game_save_before = $sourceGameSaveBefore
    source_game_save_after = $sourceGameSaveAfter
    source_game_save_preserved = $sourceGameSavePreserved
    game_save_before = $cloneGameSaveBefore
    game_save_after = $cloneGameSaveAfter
    game_save_observed = $saveObserved
    run1 = $run1Result
    run2 = $run2Result
    simulation_isolated = $simulationIsolated
    same_chosen_start = $sameChosenStart
    same_public_revision = $samePublicRevision
    run1_moved_before_save = $run1Moved
    public_revision_evidence = [ordered]@{
        source_hash = $sourcePin
        authored_checkpoint_hash = $authoredArchivePin
        authored_revision = $authoredRevision
        run1_public_zone = if ($null -ne $run1Start) { [string]$run1Start.public_identity.zone } else { $null }
        run1_public_revision = if ($null -ne $run1Start) { [string]$run1Start.public_identity.revision } else { $null }
        run2_public_zone = if ($null -ne $run2Start) { [string]$run2Start.public_identity.zone } else { $null }
        run2_public_revision = if ($null -ne $run2Start) { [string]$run2Start.public_identity.revision } else { $null }
        direct_domain_hash_read = $false
        public_ui_read = 'Informations de la zone → Identités techniques → Révision'
    }
    public_ui_diagnostics = @($uiDiagnostics)
    journal = @($journal)
    zone_uuid = $zoneId
    limitations = @(
        'All controls are public Studio buttons and public CDP mouse/keyboard primitives; no hidden ECS state, Tauri command, Runtime window variable or source JSON edit is injected.',
        'The source hash is proven by byte-identical pinned archive/object snapshots and the authoring ledger. Studio publicly exposes the revision, not the internal ContentHash; direct_domain_hash_read remains false.',
        'Run 1 writes only the isolated game-save store through the public Sauvegarder la partie button. The original fixture game-save store and all authored source snapshots must remain unchanged.',
        'After the exact public physical-objects-not-settled refusal, a new public Save transaction may be attempted within the original interaction bound. All attempts and responses remain in the journal; no settling state is injected.',
        'Run 2 starts Jouer à cet endroit after the same public reset; it deliberately does not click Reprendre la partie. Matching initial coordinates show simulation isolation, not bit-identical frames or deterministic physics.',
        'This smoke does not prove navigation, combat, impact, fire/water, room traversal, human visual acceptance or whole Gate 3 qualification.'
    )
}

$ledgerPath = Join-Path $output 'gate3-replay-isolation-smoke-ledger.json'
[IO.File]::WriteAllText($ledgerPath, ($ledger | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
if ($ledger.status -cne 'passed') { throw "Gate 3 R3-P04 replay isolation smoke failed closed: $failure. Ledger: $ledgerPath" }
Write-Output "TIDEWATER_GATE3_REPLAY_ISOLATION_SMOKE_PASS ledger=$ledgerPath"
