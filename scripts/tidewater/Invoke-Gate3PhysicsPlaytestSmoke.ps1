#Requires -Version 7.2
# Gate 3 public physics playtest for the V3 physics authoring fixture.
# This is a bounded public-UI/save-evidence smoke; it never qualifies Gate 3.
[CmdletBinding(DefaultParameterSetName = 'Run')]
param(
    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$Candidate,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
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

    [Parameter(ParameterSetName = 'Run')]
    [guid]$ZoneUuid = 'e620dd19-ead9-5913-a6d6-0f93109148c0',

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(15, 300)]
    [int]$StartupTimeoutSeconds = 180,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(30, 360)]
    [int]$InteractionTimeoutSeconds = 120,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateNotNullOrEmpty()]
    [string]$BrowserExecutableFolder = 'C:\Program Files (x86)\Microsoft\EdgeWebView\Application\154.0.4258.37',

    [Parameter(ParameterSetName = 'Run')]
    [switch]$ShoreFireOnly,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$physicsPlaytestRequestedSelfTest = [bool]$SelfTest
$physicsPlaytestRequestedCandidate = $Candidate
$physicsPlaytestRequestedFixture = $FreshIsolatedFixture
$physicsPlaytestRequestedLedger = $AuthoringLedger
$physicsPlaytestRequestedSourcePin = $SourceOriginalHashPinned
$physicsPlaytestRequestedEvidence = $EvidenceRoot
$physicsPlaytestRequestedZone = $ZoneUuid
$physicsPlaytestRequestedStartup = $StartupTimeoutSeconds
$physicsPlaytestRequestedInteraction = $InteractionTimeoutSeconds
$physicsPlaytestRequestedBrowser = $BrowserExecutableFolder
$physicsPlaytestRequestedShoreFireOnly = [bool]$ShoreFireOnly

# Import the governed CDP/fixture/process helpers without starting their own
# run.  The helper's self-test returns before its Run parameter set executes.
. (Join-Path $PSScriptRoot 'Invoke-Gate3GameplayPlaytestSmoke.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Gate3ProofStorage.ps1')
if (-not $physicsPlaytestRequestedSelfTest) {
    $Candidate = $physicsPlaytestRequestedCandidate
    $FreshIsolatedFixture = $physicsPlaytestRequestedFixture
    $AuthoringLedger = $physicsPlaytestRequestedLedger
    $SourceOriginalHashPinned = $physicsPlaytestRequestedSourcePin
    $EvidenceRoot = $physicsPlaytestRequestedEvidence
    $ZoneUuid = $physicsPlaytestRequestedZone
    $StartupTimeoutSeconds = $physicsPlaytestRequestedStartup
    $InteractionTimeoutSeconds = $physicsPlaytestRequestedInteraction
    $BrowserExecutableFolder = $physicsPlaytestRequestedBrowser
    $ShoreFireOnly = $physicsPlaytestRequestedShoreFireOnly
}

$zoneId = $ZoneUuid.ToString('D').ToLowerInvariant()
$physicsNames = @(
    'Plateforme de lancer', 'Charge de lancer', 'Support piège',
    'Charge piège', 'Balise inflammable', 'Poste de danger',
    'Veilleur du danger'
)
if ($ShoreFireOnly) { $physicsNames += 'Feu de rive' }

function Get-PhysicsPlaytestEntity {
    param(
        [Parameter(Mandatory)]$Collection,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Kind
    )
    $matches = @($Collection | Where-Object { [string]$_.name -ceq $Name })
    if ($matches.Count -ne 1) { throw "Expected exactly one $Kind named '$Name', got $($matches.Count)." }
    return $matches[0]
}

function Get-PhysicsCollectionCount {
    param([AllowNull()][object]$Value)
    if ($null -eq $Value) { return 0 }
    return @($Value).Count
}

function Get-PhysicsPlaytestProperty {
    param(
        [AllowNull()][object]$Object,
        [Parameter(Mandatory)][string]$Name
    )
    if ($null -eq $Object) { return $null }
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name)) { return $Object[$Name] }
        return $null
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-PhysicsWaterSample {
    param(
        [Parameter(Mandatory)]$State,
        [Parameter(Mandatory)]$Observation
    )
    $locomotion = Get-PhysicsPlaytestProperty -Object $Observation -Name 'locomotion'
    $step = Get-PhysicsPlaytestProperty -Object $locomotion -Name 'water_query_step'
    $time = Get-PhysicsPlaytestProperty -Object $locomotion -Name 'water_query_time'
    $waterHeight = Get-PhysicsPlaytestProperty -Object $locomotion -Name 'water_height'
    $waterMean = Get-PhysicsPlaytestProperty -Object $locomotion -Name 'water_mean'
    $mode = [string](Get-PhysicsPlaytestProperty -Object $locomotion -Name 'mode')
    $anchor = @(Get-PhysicsPlaytestProperty -Object $locomotion -Name 'anchor')
    $query = @(Get-PhysicsPlaytestProperty -Object $locomotion -Name 'water_query_position')
    $anchorY = [double]$Observation.y
    if ($anchor.Count -ge 3 -and [double]::IsFinite([double]$anchor[1])) { $anchorY = [double]$anchor[1] }
    $queryX = $null
    $queryZ = $null
    if ($query.Count -ge 2 -and [double]::IsFinite([double]$query[0]) -and [double]::IsFinite([double]$query[1])) {
        $queryX = [double]$query[0]
        $queryZ = [double]$query[1]
    }
    $queryDistance = $null
    if ($null -ne $queryX -and $null -ne $queryZ -and $Observation.ok) {
        $queryDistance = [Math]::Sqrt(([double]$queryX - [double]$Observation.x) * ([double]$queryX - [double]$Observation.x) + ([double]$queryZ - [double]$Observation.z) * ([double]$queryZ - [double]$Observation.z))
    }
    $finiteWater = $null -ne $waterMean -and [double]::IsFinite([double]$waterMean) -and
        $null -ne $waterHeight -and [double]::IsFinite([double]$waterHeight)
    $finiteStep = $null -ne $step -and [int64]$step -gt 0
    $feetBelowSurface = $finiteWater -and ([double]$waterHeight -gt $anchorY + 0.02)
    $wetMode = $mode -ieq 'Swim'
    return [ordered]@{
        valid = [bool]($Observation.ok -and $finiteStep -and $finiteWater -and $null -ne $queryDistance -and $queryDistance -lt 1.0 -and ($feetBelowSurface -or $wetMode))
        step = if ($null -ne $step) { [int64]$step } else { $null }
        time = if ($null -ne $time -and [double]::IsFinite([double]$time)) { [double]$time } else { $null }
        query_x = $queryX
        query_z = $queryZ
        query_distance_m = $queryDistance
        water_height = if ($null -ne $waterHeight) { [double]$waterHeight } else { $null }
        water_mean = if ($null -ne $waterMean) { [double]$waterMean } else { $null }
        anchor_y = $anchorY
        mode = $mode
        observation = $Observation
        raw_locomotion = $locomotion
        sample_source = 'public data-tidewater-live-shadows.player_locomotion'
    }
}

function Wait-PhysicsFreshWaterSamples {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $samples = [System.Collections.Generic.List[object]]::new()
    $lastStep = -1L
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $state = Get-PlaytestPublicState -Socket $Socket
        $observation = Get-PlaytestObservation -State $state
        $sample = Get-PhysicsWaterSample -State $state -Observation $observation
        Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'public_water_sample' -Payload $sample
        if ([bool]$sample.valid -and [int64]$sample.step -gt $lastStep) {
            [void]$samples.Add($sample)
            $lastStep = [int64]$sample.step
            if ($samples.Count -ge 2) { return @($samples) }
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Public water contact did not expose two fresh concordant GPU samples within $TimeoutSeconds seconds during '$Phase'; collected=$($samples.Count), last_step=$lastStep."
}

function Activate-PhysicsOwnedWindow {
    param([Parameter(Mandatory)]$Run)
    if ($null -eq $Run.Process) { throw 'Owned Studio process handle is missing before public route activation.' }
    $Run.Process.Refresh()
    if ($Run.Process.HasExited) { return }
    Add-Type -AssemblyName Microsoft.VisualBasic
    # A WebView2-hosting process may have no native top-level window even while
    # its CDP transport and public document are alive.  Activation is a focus
    # aid only; an absent/hidden window must not invalidate a public UI run.
    $windowHandle = [int64]$Run.Process.MainWindowHandle
    if ($windowHandle -eq 0) { return }
    try { [void][Microsoft.VisualBasic.Interaction]::AppActivate([int]$Run.Process.Id) } catch { return }
    Start-Sleep -Milliseconds 300
}

function Get-PhysicsPlaytestSaveFile {
    param(
        [Parameter(Mandatory)][string]$FixtureRoot,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $root = Join-Path $FixtureRoot '.game-saves'
        $files = @(
            if (Test-Path -LiteralPath $root -PathType Container) {
                Get-ChildItem -LiteralPath $root -File -Filter 'authoring.json' -Recurse -Force
            }
        )
        $fileCount = Get-PhysicsCollectionCount -Value $files
        if ($fileCount -eq 1 -and $files[0].Length -gt 0) { return $files[0].FullName }
        Start-Sleep -Milliseconds 200
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    throw 'The public game-save file did not appear as one regular authoring.json file.'
}

function Get-PhysicsPlaytestSaveMetadata {
    param(
        [Parameter(Mandatory)][string]$FixtureRoot
    )
    $root = Join-Path $FixtureRoot '.game-saves'
    $files = @(
        if (Test-Path -LiteralPath $root -PathType Container) {
            Get-ChildItem -LiteralPath $root -File -Filter 'authoring.json' -Recurse -Force
        }
    )
    $fileCount = Get-PhysicsCollectionCount -Value $files
    if ($fileCount -gt 1) { throw "Expected at most one public authoring.json save, got $fileCount." }
    if ($fileCount -eq 0) {
        return [pscustomobject]@{
            exists = $false
            path = $null
            sha256 = $null
            length = $null
            last_write_utc = $null
            last_write_utc_ticks = $null
        }
    }
    $item = Get-Item -LiteralPath $files[0].FullName -Force
    if ($item.Length -le 0) {
        return [pscustomobject]@{
            exists = $true
            path = [IO.Path]::GetFullPath($item.FullName)
            sha256 = $null
            length = [int64]$item.Length
            last_write_utc = $item.LastWriteTimeUtc.ToString('o')
            last_write_utc_ticks = [int64]$item.LastWriteTimeUtc.Ticks
        }
    }
    return [pscustomobject]@{
        exists = $true
        path = [IO.Path]::GetFullPath($item.FullName)
        sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        length = [int64]$item.Length
        last_write_utc = $item.LastWriteTimeUtc.ToString('o')
        last_write_utc_ticks = [int64]$item.LastWriteTimeUtc.Ticks
    }
}

function Test-PhysicsPlaytestSaveMetadataChanged {
    param(
        [Parameter(Mandatory)]$Before,
        [Parameter(Mandatory)]$After
    )
    if (-not [bool]$After.exists) { return $false }
    if (-not [bool]$Before.exists) { return $true }
    return ([string]$Before.path -cne [string]$After.path) -or
        ([string]$Before.sha256 -cne [string]$After.sha256) -or
        ([int64]$Before.length -ne [int64]$After.length) -or
        ([int64]$Before.last_write_utc_ticks -ne [int64]$After.last_write_utc_ticks)
}

function Test-PhysicsSaveCommitSharingViolation {
    param([Parameter(Mandatory)][System.Exception]$Exception)
    for ($current = $Exception; $null -ne $current; $current = $current.InnerException) {
        # Windows ERROR_SHARING_VIOLATION only; no other I/O failure is deferred.
        if ($current -is [IO.IOException] -and $current.HResult -eq -2147024864) { return $true }
    }
    return $false
}

function Wait-PhysicsPlaytestSaveCommit {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$FixtureRoot,
        [Parameter(Mandatory)]$BeforeMetadata,
        [Parameter(Mandatory)][int]$BeforeAckCount,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [Parameter(Mandatory)][string]$Name
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $ackObserved = $false
    $ackCountAfter = $BeforeAckCount
    $buttonDisabledObserved = $false
    $lastState = $null
    $lastMetadata = $BeforeMetadata
    $sharingViolationReads = 0
    $firstSharingViolationUtc = $null
    $lastSharingViolationUtc = $null
    $lastSharingViolationStack = $null
    $lastCommitReadSharingViolation = $null
    do {
        $lastState = Get-PlaytestPublicState -Socket $Socket
        $outputs = @($lastState.outputs)
        $ackCountAfter = @($outputs | Where-Object { $_ -ceq 'Partie sauvegardée.' }).Count
        if ($ackCountAfter -ge 1) { $ackObserved = $true }
        if (@($lastState.allButtons | Where-Object { $_.text -ceq 'Sauvegarder la partie' -and $_.disabled }).Count -ge 1) {
            $buttonDisabledObserved = $true
        }
        try {
            $lastMetadata = Get-PhysicsPlaytestSaveMetadata -FixtureRoot $FixtureRoot
            $lastCommitReadSharingViolation = $null
        } catch {
            if (-not (Test-PhysicsSaveCommitSharingViolation -Exception $_.Exception)) { throw }
            $lastCommitReadSharingViolation = $_
            $sharingViolationReads++
            $lastSharingViolationUtc = [DateTimeOffset]::UtcNow.ToString('o')
            if ($null -eq $firstSharingViolationUtc) { $firstSharingViolationUtc = $lastSharingViolationUtc }
            $lastSharingViolationStack = $_.ScriptStackTrace
            # Observe the same requested transaction again, within its original
            # deadline. No new Save click or previous metadata can satisfy it.
            Start-Sleep -Milliseconds 200
            continue
        }
        $fileChanged = Test-PhysicsPlaytestSaveMetadataChanged -Before $BeforeMetadata -After $lastMetadata
        if ($ackObserved -and $fileChanged) {
            return [pscustomobject]@{
                name = $Name
                ack_observed = $ackObserved
                ack_count_before = $BeforeAckCount
                ack_count_after = $ackCountAfter
                ack_was_new_count = $ackCountAfter -gt $BeforeAckCount
                save_button_disabled_observed = $buttonDisabledObserved
                file_changed = $fileChanged
                pre_save = $BeforeMetadata
                post_save = $lastMetadata
                public_state = $lastState
                commit_read_sharing_violations = $sharingViolationReads
                first_commit_read_sharing_utc = $firstSharingViolationUtc
                last_commit_read_sharing_utc = $lastSharingViolationUtc
                last_commit_read_sharing_stack = $lastSharingViolationStack
            }
        }
        Start-Sleep -Milliseconds 200
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    # Preserve the read failure if the deadline ended while still locked.
    # The caller retries only its legacy uncommitted/settling error; it must
    # never issue a second Save to hide a persistent sharing violation.
    if ($null -ne $lastCommitReadSharingViolation) { throw $lastCommitReadSharingViolation }
    $changedText = Test-PhysicsPlaytestSaveMetadataChanged -Before $BeforeMetadata -After $lastMetadata
    throw "Public save $Name did not produce a new committed authoring.json transaction within $TimeoutSeconds seconds (ack_observed=$ackObserved; file_changed=$changedText; before_sha=$($BeforeMetadata.sha256); after_sha=$($lastMetadata.sha256); before_write=$($BeforeMetadata.last_write_utc); after_write=$($lastMetadata.last_write_utc); commit_read_sharing_violations=$sharingViolationReads; first_read_lock=$firstSharingViolationUtc; last_read_lock=$lastSharingViolationUtc; last_read_stack=$lastSharingViolationStack)."
}

function Set-PhysicsPlaytestLaunchFacing {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase,
        [double]$TargetYawDegrees = 90.0
    )
    # The public movement contract is W = (-sin(yaw), -cos(yaw)).  The
    # retouched guardian is west of the launch charge, so yaw 90 degrees is
    # the intended public look direction.  Turn only through pointer-locked
    # mouse movement and record the resulting public status yaw; never write
    # a camera/domain value.
    $canvas = Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'launch-facing canvas' -Expression @'
(() => {
  const node=document.getElementById('forge-authoring-canvas');
  if(!node) return {ready:false,terminal:false,reason:'canvas pending'};
  const rect=node.getBoundingClientRect();
  const status=document.querySelector('#authoring-playtest-status')?.textContent??'';
  const capture=document.querySelector('#studio-input-capture-status')?.textContent??'';
  return {ready:document.pointerLockElement?.id==='forge-authoring-canvas'&&/entrée active true/i.test(status)&&/Exploration active/i.test(capture),x:rect.left+rect.width/2,y:rect.top+rect.height/2,terminal:false,reason:status+' | '+capture};
})()
'@
    $observations = [System.Collections.Generic.List[object]]::new()
    $mouseX = [double]$canvas.x
    for ($attempt = 1; $attempt -le 8; $attempt++) {
        $state = Get-PlaytestPublicState -Socket $Socket
        $observation = Get-PlaytestObservation -State $state
        $observations.Add($observation)
        if (-not $observation.ok -or $null -eq $observation.yaw_degrees) { throw 'Public playtest status did not expose a finite camera yaw before launch.' }
        $delta = [Math]::IEEERemainder(($TargetYawDegrees - [double]$observation.yaw_degrees), 360.0)
        if ([Math]::Abs($delta) -le 8.0) { break }
        # WebView2 reports this CDP horizontal movement with the opposite
        # camera-yaw sign; the resulting yaw is still read back publicly after
        # every correction.
        $direction = if ($delta -gt 0.0) { -1.0 } else { 1.0 }
        $pixels = [Math]::Min(300.0, [Math]::Max(20.0, [Math]::Abs($delta) * 5.0))
        # Pointer lock reports movement relative to the previous injected
        # pointer position.  Accumulate X so successive corrections do not
        # alternate between two absolute positions and overshoot.
        $mouseX += $direction * $pixels
        Send-CdpMouseEvent -Socket $Socket -Type mouseMoved -X $mouseX -Y $canvas.y
        Start-Sleep -Milliseconds 250
    }
    $finalState = Get-PlaytestPublicState -Socket $Socket
    $finalObservation = Get-PlaytestObservation -State $finalState
    if (-not $finalObservation.ok -or $null -eq $finalObservation.yaw_degrees) { throw 'Public playtest status did not expose a finite camera yaw after launch-facing turn.' }
    $finalDelta = [Math]::Abs([Math]::IEEERemainder(($TargetYawDegrees - [double]$finalObservation.yaw_degrees), 360.0))
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'public_launch_facing_observation' -Payload ([ordered]@{
            target_yaw_degrees = $TargetYawDegrees
            observation = $finalObservation
            absolute_delta_degrees = $finalDelta
            attempts = $observations.Count
            movement = 'pointer_locked_public_mouse'
            direction_contract = 'W=(-sin(yaw),-cos(yaw))'
        })
    if ($finalDelta -gt 20.0) { throw "Public launch-facing turn did not reach the requested observed yaw; target=$TargetYawDegrees observed=$($finalObservation.yaw_degrees) delta=$finalDelta" }
    return $finalObservation
}

function Copy-PhysicsPlaytestSave {
    param(
        [Parameter(Mandatory)][string]$FixtureRoot,
        [Parameter(Mandatory)][string]$EvidencePath,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $sourcePath = Get-PhysicsPlaytestSaveFile -FixtureRoot $FixtureRoot -TimeoutSeconds $TimeoutSeconds
    $fixturePrefix = [IO.Path]::GetFullPath($FixtureRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $relative = $sourcePath.Substring($fixturePrefix.Length).TrimStart([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if ($relative -notmatch '(?i)^\.game-saves\\([^\\]+)\\([^\\]+)\\authoring\.json$') {
        throw "Public save path has unexpected relative layout: $relative"
    }
    $module = $Matches[1]
    $area = $Matches[2]
    $destination = Join-Path (Join-Path (Join-Path $EvidencePath $Name) '.game-saves') (Join-Path $module $area)
    [void](New-Item -ItemType Directory -Path $destination -Force)
    $destination = Join-Path $destination 'authoring.json'
    [IO.File]::Copy($sourcePath, $destination, $true)
    return [pscustomobject]@{
        path = [IO.Path]::GetFullPath($destination)
        source_path = [IO.Path]::GetFullPath($sourcePath)
        sha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
        module_id = $module
        area_id = $area
    }
}

function New-PhysicsExposureRetreatPlan {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$FixtureRoot,
        [Parameter(Mandatory)][string]$PlayerId,
        [Parameter(Mandatory)][string]$FireId,
        [Parameter(Mandatory)][string]$SourceHash,
        [Parameter(Mandatory)][double]$TargetX,
        [Parameter(Mandatory)][double]$TargetZ,
        [ValidateRange(100,1000)][int]$IgniteToFirstKeyBudgetMilliseconds=1000
    )
    # All file hashing and route planning happen while the fire is still unlit.
    $state=Get-PlaytestPublicState -Socket $Socket
    $observation=Get-PlaytestObservation -State $state
    if(-not $observation.ok -or -not $observation.simulation_ready -or -not $observation.grounded -or
        $null -eq $observation.tick -or $null -eq $observation.yaw_degrees){throw 'Exposure preflight needs a ready, grounded public pose, tick and camera yaw.'}
    $keys=@(Get-PlaytestRouteKeys -DeltaX ($TargetX-[double]$observation.x) -DeltaZ ($TargetZ-[double]$observation.z) -YawDegrees ([double]$observation.yaw_degrees))
    if($keys.Count -lt 1 -or -not [double]::IsFinite($TargetX) -or -not [double]::IsFinite($TargetZ)){throw 'Exposure retreat has no finite public keyboard direction.'}
    return [pscustomobject]@{
        schema='forge.gate3.public-exposure-retreat';version=1;name='fire-exposure';fixture_root=$FixtureRoot
        player_id=$PlayerId;fire_id=$FireId;source_hash=$SourceHash;target_x=$TargetX;target_z=$TargetZ
        prepared_observation=$observation;pre_metadata=(Get-PhysicsPlaytestSaveMetadata -FixtureRoot $FixtureRoot)
        keys=$keys;first_key_budget_ms=$IgniteToFirstKeyBudgetMilliseconds;timeline=[System.Collections.Generic.List[object]]::new()
        ignite_click_utc=$null;ignite_ack_state=$null;ignite_sequence=$null;ignite_clock_ticks=$null;ignite_clock_utc=$null
        pre_save_observation=$null;pre_save_state=$null;pre_ack_count=0;save_click_dispatched=$false;save_click_acked=$false;save_resolved=$false
        first_key_elapsed_ms=$null;first_key_within_budget=$false;exposure_observation=$null;retreat_observation=$null
        retreat_vitality=$null;save_commit_observation=$null;capture=$null;failures=[System.Collections.Generic.List[string]]::new()
    }
}

function Add-PhysicsExposureTimeline {
    param([Parameter(Mandatory)]$Plan,[Parameter(Mandatory)][string]$Event,[AllowNull()]$Payload=$null)
    $now=[DateTimeOffset]::UtcNow
    $elapsed=$null
    if($null -ne $Plan.ignite_clock_ticks){
        $elapsed=($Plan.ignite_clock_utc-[DateTimeOffset]::Parse($Plan.ignite_click_utc)).TotalMilliseconds+
            ([Diagnostics.Stopwatch]::GetTimestamp()-$Plan.ignite_clock_ticks)*1000.0/[Diagnostics.Stopwatch]::Frequency
    }
    if($Plan.timeline.Count -ge 64){throw 'Exposure chronology quota exceeded.'}
    [void]$Plan.timeline.Add([ordered]@{event=$Event;utc=$now.ToString('o');since_ignite_click_ms=$elapsed;payload=$Payload})
    return $elapsed
}

function Get-PhysicsExposureUiTarget {
    param([Parameter(Mandatory)]$Socket,[Parameter(Mandatory)][string]$Text,[string]$Selector='button')
    # Same exact-node/visibility/disabled/exposed-centre guards as Get-UiTarget,
    # with a one-second lookup bound for this distinct exposure flow.
    $selectorLiteral=ConvertTo-CompactJson -Value $Selector
    $textLiteral=ConvertTo-CompactJson -Value $Text
    $expression=@"
(async () => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const matches=[...document.querySelectorAll($selectorLiteral)].filter(node=>norm(node.textContent)===$textLiteral);
  if(matches.length===0) return {ready:false,terminal:false,reason:'exact target not rendered yet'};
  if(matches.length!==1) return {ready:false,terminal:true,reason:'expected one exact target, got '+matches.length};
  const node=matches[0];node.scrollIntoView({block:'center',inline:'center'});
  await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
  const rect=node.getBoundingClientRect(),style=getComputedStyle(node);
  const visible=rect.width>0&&rect.height>0&&style.visibility!=='hidden'&&style.display!=='none';
  const x=rect.left+rect.width/2,y=rect.top+rect.height/2,hit=document.elementFromPoint(x,y);
  const unobscured=hit===node||(!!hit&&node.contains(hit));
  return {ready:visible&&unobscured,terminal:!visible,reason:!visible?'target hidden':unobscured?'target ready':'target centre obscured',x,y,disabled:node.matches(':disabled'),text:norm(node.textContent),hit_tag:hit?.tagName??'',hit_id:hit?.id??''};
})()
"@
    $target=Wait-CdpReady -Socket $Socket -Expression $expression -Description "bounded exposure UI target '$Text'" -TimeoutSeconds 1
    if($target.disabled){throw "Public exposure UI target '$Text' is disabled."}
    return $target
}

function Invoke-PhysicsExposureIgnite {
    param([Parameter(Mandatory)]$Socket,[Parameter(Mandatory)]$Plan,[Parameter(Mandatory)][string]$Text,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal)
    $before=Get-PlaytestPublicState -Socket $Socket
    $matches=@($before.actions|Where-Object{[string]$_.text -ceq $Text})
    if($matches.Count -ne 1 -or $matches[0].disabled){throw 'Exposure Ignite needs one enabled public action.'}
    $beforeFeedback=[string]$before.feedback
    Add-PlaytestJournal -Journal $Journal -Phase 'ignite' -Event 'public_action_before_escape' -Payload @{text=$Text;observation=(Get-PlaytestObservation -State $before);feedback=$beforeFeedback}
    Release-PlaytestCapture -Socket $Socket -TimeoutSeconds 1
    $atClick=Get-PlaytestPublicState -Socket $Socket
    Add-PlaytestJournal -Journal $Journal -Phase 'ignite' -Event 'public_action_after_escape_before_click' -Payload @{text=$Text;observation=(Get-PlaytestObservation -State $atClick);feedback=$atClick.feedback}
    $target=Get-PhysicsExposureUiTarget -Socket $Socket -Text $Text -Selector '.gameplay-actions button'
    # Start before dispatch, not at the later click acknowledgement. All target
    # resolution and file/route planning have already happened without heat.
    $Plan.ignite_click_utc=[DateTimeOffset]::UtcNow.ToString('o')
    $Plan.ignite_clock_utc=[DateTimeOffset]::Parse($Plan.ignite_click_utc)
    $Plan.ignite_clock_ticks=[Diagnostics.Stopwatch]::GetTimestamp()
    [void](Add-PhysicsExposureTimeline -Plan $Plan -Event 'ignite_click_dispatch_started' -Payload $target)
    Send-CdpMouseClick -Socket $Socket -X $target.x -Y $target.y
    Add-PlaytestJournal -Journal $Journal -Phase 'ignite' -Event 'public_action_click' -Payload $Text
    [void](Add-PhysicsExposureTimeline -Plan $Plan -Event 'ignite_click_acked')
    $state=Wait-PlaytestCondition -Socket $Socket -TimeoutSeconds 1 -Description 'bounded public exposure Ignite settled' -Condition {
        param($current)
        $currentMatches=@($current.actions|Where-Object{[string]$_.text -ceq $Text})
        ([string]$current.feedback -cne $beforeFeedback) -and (($currentMatches.Count -eq 0) -or (-not [bool]$currentMatches[0].disabled))
    }
    if([string]$state.feedback -notmatch '^Action acceptée\s+·\s+étape\s+\d+$'){throw "Exposure Ignite has no positive runtime acknowledgement: $($state.feedback)"}
    $Plan.ignite_ack_state=$state
    Add-PlaytestJournal -Journal $Journal -Phase 'ignite' -Event 'public_action_settled' -Payload @{text=$Text;state=$state}
    return $state
}

function Start-PhysicsDeferredExposureSave {
    param([Parameter(Mandatory)]$Socket,[Parameter(Mandatory)]$Plan,[AllowNull()][Parameter(Mandatory)]$IgniteState,[AllowEmptyString()][Parameter(Mandatory)][string]$IgniteClickUtc)
    if($null -eq $IgniteState){throw 'Exposure Save needs the real positive Ignite acknowledgement.'}
    $ack=[regex]::Match([string]$IgniteState.feedback,'^Action acceptée\s+·\s+étape\s+(?<sequence>\d+)$')
    if(-not $ack.Success){throw 'Exposure Save needs the real positive Ignite acknowledgement.'}
    $Plan.ignite_click_utc=$IgniteClickUtc
    if($null -eq $Plan.ignite_clock_ticks){
        $Plan.ignite_clock_utc=[DateTimeOffset]::UtcNow
        $Plan.ignite_clock_ticks=[Diagnostics.Stopwatch]::GetTimestamp()
    }
    $Plan.ignite_ack_state=$IgniteState
    $Plan.ignite_sequence=[long]$ack.Groups['sequence'].Value
    $Plan.pre_save_state=$IgniteState
    $Plan.pre_save_observation=Get-PlaytestObservation -State $IgniteState
    if(-not $Plan.pre_save_observation.ok -or -not $Plan.pre_save_observation.simulation_ready -or $null -eq $Plan.pre_save_observation.tick -or
        [long]$Plan.pre_save_observation.tick -lt [long]$Plan.prepared_observation.tick){throw 'Exposure Save lost its fresh public pre-click pose/tick.'}
    $Plan.pre_ack_count=@($IgniteState.outputs|Where-Object{$_ -ceq 'Partie sauvegardée.'}).Count
    [void](Add-PhysicsExposureTimeline -Plan $Plan -Event 'ignite_ack' -Payload @{feedback=$IgniteState.feedback;observation=$Plan.pre_save_observation})
    # Keep the existing Escape/release guards. No commit wait, file read, journal
    # serialization or post-save observation may precede the first retreat key.
    Release-PlaytestCapture -Socket $Socket -TimeoutSeconds 1
    [void](Add-PhysicsExposureTimeline -Plan $Plan -Event 'save_capture_released')
    $target=Get-PhysicsExposureUiTarget -Socket $Socket -Text 'Sauvegarder la partie'
    $Plan.save_click_dispatched=$true
    [void](Add-PhysicsExposureTimeline -Plan $Plan -Event 'save_click_dispatch_started' -Payload $target)
    Send-CdpMouseClick -Socket $Socket -X $target.x -Y $target.y
    $Plan.save_click_acked=$true
    [void](Add-PhysicsExposureTimeline -Plan $Plan -Event 'save_click_acked' -Payload $target)
}

function Invoke-PhysicsImmediateExposureRetreat {
    param([Parameter(Mandatory)]$Socket,[Parameter(Mandatory)]$Plan,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal)
    # Focus retains document visibility/focus, exposed canvas point, pointer lock
    # and active-input acknowledgement, including its full browser-rejection retry.
    Focus-PlaytestCanvas -Socket $Socket -TimeoutSeconds 1 -PointerCapturePollMilliseconds 25
    [void](Add-PhysicsExposureTimeline -Plan $Plan -Event 'pointer_capture_guard_satisfied')
    $held=[System.Collections.Generic.List[object]]::new()
    try {
        foreach($key in $Plan.keys){
            # A dispatch may reach the browser even if its acknowledgement fails.
            # Register before sending so finally still attempts the release.
            [void]$held.Add($key)
            [void](Add-PhysicsExposureTimeline -Plan $Plan -Event 'retreat_keydown_dispatch_started' -Payload $key)
            Send-Gate3PlaytestKeyEvent -Socket $Socket -Type rawKeyDown -Key $key.key -Code $key.code -VirtualKeyCode $key.vk
            $elapsed=Add-PhysicsExposureTimeline -Plan $Plan -Event 'retreat_keydown_acked' -Payload $key
            if($null -eq $Plan.first_key_elapsed_ms){
                $Plan.first_key_elapsed_ms=$elapsed
                $Plan.first_key_within_budget=$null -ne $elapsed -and $elapsed -ge 0 -and $elapsed -le $Plan.first_key_budget_ms
            }
        }
        # The first movement is already held while this bounded read collects the
        # real HUD and NPC diagnostic. It cannot postpone the first retreat key.
        try {
            $Plan.exposure_observation=Invoke-CdpExpression -Socket $Socket -TimeoutSeconds 1 -Expression @'
(() => {const norm=s=>(s??'').replace(/\s+/g,' ').trim();return {captured_at_utc:new Date().toISOString(),status:norm(document.querySelector('#authoring-playtest-status')?.textContent),input:norm(document.querySelector('#studio-input-capture-status')?.textContent),pointer_lock:document.pointerLockElement?.id??null,outputs:[...document.querySelectorAll('section.authoring-gameplay-controls output')].map(n=>norm(n.textContent)),navigation_raw:document.querySelector("pre[data-forge-navigation-observation='v1']")?.textContent??null};})()
'@
            [void](Add-PhysicsExposureTimeline -Plan $Plan -Event 'exposure_public_observation_during_retreat' -Payload $Plan.exposure_observation)
        }catch{[void](Add-PhysicsExposureTimeline -Plan $Plan -Event 'exposure_public_observation_unavailable' -Payload $_.Exception.Message)}
        Start-Sleep -Milliseconds 100
    }finally{
        foreach($key in $held){Send-Gate3PlaytestKeyEvent -Socket $Socket -Type keyUp -Key $key.key -Code $key.code -VirtualKeyCode $key.vk}
    }
    $Plan.retreat_observation=Move-PlaytestWASDSegment -Socket $Socket -TargetX $Plan.target_x -TargetZ $Plan.target_z -Tolerance 0.15 -MaxSeconds 12 -Journal $Journal -Phase 'leave-heat-radial' -RouteId 'public-actual-saved-pose-heat-retreat'
    $outside=Get-PlaytestPublicState -Socket $Socket
    $vitality=@($outside.outputs|ForEach-Object{$m=[regex]::Match([string]$_,'^Vitalité\s+(?<v>\d+)\b');if($m.Success){[int]$m.Groups['v'].Value}})
    if($vitality.Count -ne 1){throw 'Retreat did not expose one public player vitality.'}
    $Plan.retreat_vitality=$vitality[0]
    [void](Add-PhysicsExposureTimeline -Plan $Plan -Event 'retreat_arrived' -Payload @{observation=$Plan.retreat_observation;vitality=$Plan.retreat_vitality})
    if($Plan.retreat_vitality -le 0){throw 'The player was defeated during the public heat retreat.'}
}

function Assert-PhysicsDeferredExposureSnapshot {
    param([Parameter(Mandatory)]$Plan,[Parameter(Mandatory)]$Saved,[ValidateRange(0.05,1.0)][double]$PositionToleranceMeters=0.25)
    if([string]$Saved.source_hash -cne $Plan.source_hash){throw 'Deferred exposure Save source differs.'}
    $frame=$Saved.state.last_frame.frame
    $pre=$Plan.pre_save_observation
    if($null -eq $frame.tick -or $null -eq $Saved.state.tick -or $null -eq $pre.tick -or
        [string]$frame.source_hash -cne $Plan.source_hash -or [long]$frame.tick -ne [long]$Saved.state.tick -or
        [long]$frame.tick -lt [long]$pre.tick -or
        ($null -ne $Plan.retreat_observation -and [long]$frame.tick -gt [long]$Plan.retreat_observation.tick)){throw 'Deferred exposure Save has a stale, mismatched or future physical frame.'}
    $actor=$Saved.state.actors.($Plan.player_id)
    $pose=$actor.position
    $frameActor=@($frame.actors|Where-Object{[string]$_.actor -ceq $Plan.player_id})
    if($frameActor.Count -ne 1){throw 'Deferred exposure frame has no unique player pose.'}
    $delta2=0.0
    foreach($axis in @('x','y','z')){
        if($null -eq $pose.$axis -or $null -eq $pre.$axis -or $null -eq $frameActor[0].position.$axis -or
            -not [double]::IsFinite([double]$pose.$axis) -or -not [double]::IsFinite([double]$pre.$axis) -or
            [double]$pose.$axis -ne [double]$frameActor[0].position.$axis){throw 'Deferred exposure Save player pose differs from its physical frame.'}
        $delta2+=[Math]::Pow([double]$pose.$axis-[double]$pre.$axis,2)
    }
    $delta=[Math]::Sqrt($delta2)
    if($delta -gt $PositionToleranceMeters){throw 'Deferred exposure Save differs from the ORIGINAL pre-click player pose; the retreat pose cannot substitute.'}
    $receipts=@($Saved.state.receipts|Where-Object{[long]$_.sequence -eq $Plan.ignite_sequence -and
        [string]$_.request.actor -ceq $Plan.player_id -and [string]$_.request.action.kind -ceq 'ignite' -and [string]$_.request.action.object -ceq $Plan.fire_id})
    if($receipts.Count -ne 1 -or $null -eq $receipts[0].tick -or [long]$receipts[0].tick -lt [long]$Plan.prepared_observation.tick -or [long]$receipts[0].tick -gt [long]$frame.tick -or
        @($receipts[0].events|Where-Object{[string]$_.kind -ceq 'burning' -and [string]$_.object -ceq $Plan.fire_id -and $_.active -eq $true}).Count -ne 1){throw 'Deferred exposure Save lacks the exact accepted Ignite receipt/burning event.'}
    if([int]$actor.vitality -le 0 -or @($frame.heat|Where-Object{[string]$_.actor -ceq $Plan.player_id -and [string]$_.object -ceq $Plan.fire_id -and $_.line_of_sight -eq $true}).Count -ne 1 -or
        @($Saved.state.last_frame.events|Where-Object{[string]$_.kind -ceq 'damage' -and [string]$_.target -ceq $Plan.player_id -and $_.contact -eq $false -and [int]$_.amount -gt 0}).Count -lt 1){throw 'Deferred exposure Save does not attest real heat damage to a living player.'}
    return [ordered]@{coherent=$true;comparison='original_public_pre_save_pose';pre_save_observation=$pre;saved_position=$pose;position_delta_m=$delta;tolerance_m=$PositionToleranceMeters;frame_tick=[long]$frame.tick;ignite_receipt=$receipts[0];retreat_pose_used_for_coherence=$false}
}

function Complete-PhysicsDeferredExposureSave {
    param([Parameter(Mandatory)]$Socket,[Parameter(Mandatory)]$Plan,[Parameter(Mandatory)][string]$EvidencePath,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal)
    if(-not $Plan.save_click_acked){throw 'No public exposure Save click was acknowledged.'}
    $commit=Wait-PhysicsPlaytestSaveCommit -Socket $Socket -FixtureRoot $Plan.fixture_root -BeforeMetadata $Plan.pre_metadata -BeforeAckCount $Plan.pre_ack_count -TimeoutSeconds 5 -Name $Plan.name
    $Plan.save_commit_observation=$commit
    [void](Add-PhysicsExposureTimeline -Plan $Plan -Event 'save_commit_observed_after_retreat' -Payload $commit)
    if(-not $commit.ack_observed -or -not $commit.file_changed){throw 'Deferred exposure Save has no real acknowledgement and new file transaction.'}
    # Copy BEFORE validating anything else and BEFORE any subsequent Save. Failed
    # coherence/budget remains reviewable without overwriting the exposure frame.
    $capture=Copy-PhysicsPlaytestSave -FixtureRoot $Plan.fixture_root -EvidencePath $EvidencePath -Name $Plan.name -TimeoutSeconds 5
    $Plan.capture=$capture
    $Plan.save_resolved=$true
    if((Get-FileSha256 -LiteralPath $capture.path) -cne [string]$commit.post_save.sha256){throw 'Deferred exposure Save changed between its observed commit and evidence copy.'}
    $saved=ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($capture.path))
    $coherence=Assert-PhysicsDeferredExposureSnapshot -Plan $Plan -Saved $saved
    foreach($entry in @{save_ack_observed=[bool]$commit.ack_observed;save_ack_count_before=$commit.ack_count_before;save_ack_count_after=$commit.ack_count_after;save_ack_was_new_count=[bool]$commit.ack_was_new_count;save_file_changed=[bool]$commit.file_changed;pre_save_public_observation=$Plan.pre_save_observation;save_state_coherence=$coherence;pre_save_sha256=$commit.pre_save.sha256;post_save_sha256=$commit.post_save.sha256}.GetEnumerator()){$capture|Add-Member -NotePropertyName $entry.Key -NotePropertyValue $entry.Value}
    [void](Add-PhysicsExposureTimeline -Plan $Plan -Event 'save_commit_validated_after_retreat' -Payload $coherence)
    Add-PlaytestJournal -Journal $Journal -Phase 'fire-exposure' -Event 'public_deferred_exposure_save_captured' -Payload $capture
    if(-not $Plan.first_key_within_budget){throw "Ignite-to-first-key exceeded the exposure startup budget ($($Plan.first_key_elapsed_ms) ms; budget=$($Plan.first_key_budget_ms) ms). Retreat was attempted before refusing qualification."}
    return $capture
}

function Invoke-PhysicsDeferredExposureRetreat {
    param([Parameter(Mandatory)]$Socket,[Parameter(Mandatory)]$Plan,[AllowNull()][Parameter(Mandatory)]$IgniteState,[AllowEmptyString()][Parameter(Mandatory)][string]$IgniteClickUtc,
        [Parameter(Mandatory)][string]$EvidencePath,[AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal)
    try {Start-PhysicsDeferredExposureSave -Socket $Socket -Plan $Plan -IgniteState $IgniteState -IgniteClickUtc $IgniteClickUtc}
    catch{[void]$Plan.failures.Add("save_trigger: $($_.Exception.Message)")}
    finally{
        # A missing/failed Save must never hold the player in heat while it retries.
        try {Invoke-PhysicsImmediateExposureRetreat -Socket $Socket -Plan $Plan -Journal $Journal}
        catch{[void]$Plan.failures.Add("retreat: $($_.Exception.Message)")}
    }
    $capture=$null
    if($Plan.save_click_acked){
        try{$capture=Complete-PhysicsDeferredExposureSave -Socket $Socket -Plan $Plan -EvidencePath $EvidencePath -Journal $Journal}
        catch{[void]$Plan.failures.Add("save_validation: $($_.Exception.Message)")}
    }
    Add-PlaytestJournal -Journal $Journal -Phase 'fire-exposure' -Event 'public_exposure_retreat_chronology' -Payload $Plan
    if($Plan.failures.Count -gt 0){throw ($Plan.failures -join '; ')}
    return $capture
}

function Test-PhysicsExposureSavePending {
    param([AllowNull()]$Plan)
    return $null -ne $Plan -and $Plan.save_click_dispatched -and -not $Plan.save_resolved
}

function Invoke-PhysicsPublicSave {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$FixtureRoot,
        [Parameter(Mandatory)][string]$EvidencePath,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)][string]$PlayerId,
        [ValidateRange(0.05, 1.0)][double]$PositionToleranceMeters = 0.25
    )
    Release-PlaytestCapture -Socket $Socket -TimeoutSeconds $TimeoutSeconds
    $preState = Get-PlaytestPublicState -Socket $Socket
    $preObservation = Get-PlaytestObservation -State $preState
    if (-not $preObservation.ok) { throw "Public position/HUD was not readable before save $Name." }
    $preMetadata = Get-PhysicsPlaytestSaveMetadata -FixtureRoot $FixtureRoot
    $preAckCount = @($preState.outputs | Where-Object { $_ -ceq 'Partie sauvegardée.' }).Count
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'public_save_pre_observation' -Payload ([ordered]@{
            name = $Name
            observation = $preObservation
            status = [string]$preState.status
            feedback = [string]$preState.feedback
            pre_save = $preMetadata
            save_ack_count_before = $preAckCount
        })
    [void](Invoke-UiClick -Socket $Socket -Text 'Sauvegarder la partie')
    $commit = Wait-PhysicsPlaytestSaveCommit -Socket $Socket -FixtureRoot $FixtureRoot -BeforeMetadata $preMetadata -BeforeAckCount $preAckCount -TimeoutSeconds $TimeoutSeconds -Name $Name
    $capture = Copy-PhysicsPlaytestSave -FixtureRoot $FixtureRoot -EvidencePath $EvidencePath -Name $Name -TimeoutSeconds $TimeoutSeconds
    $savedDocument = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText([string]$capture.path))
    $savedActorProperty = $savedDocument.state.actors.PSObject.Properties[$PlayerId]
    if ($null -eq $savedActorProperty) { throw "Public save $Name has no saved player actor '$PlayerId'." }
    $savedPosition = $savedActorProperty.Value.position
    foreach ($axis in @('x', 'y', 'z')) {
        if ($null -eq $savedPosition.$axis -or -not [double]::IsFinite([double]$savedPosition.$axis)) {
            throw "Public save $Name has a non-finite saved player position on $axis."
        }
    }
    $postState = Get-PlaytestPublicState -Socket $Socket
    $postObservation = Get-PlaytestObservation -State $postState
    if (-not $postObservation.ok) { throw "Public position/HUD was not readable after committed save $Name." }
    $dx = [double]$savedPosition.x - [double]$postObservation.x
    $dy = [double]$savedPosition.y - [double]$postObservation.y
    $dz = [double]$savedPosition.z - [double]$postObservation.z
    $positionDelta = [Math]::Sqrt($dx * $dx + $dy * $dy + $dz * $dz)
    $coherence = [ordered]@{
        coherent = $positionDelta -le $PositionToleranceMeters
        saved_position = $savedPosition
        public_observation = $postObservation
        position_delta_m = $positionDelta
        tolerance_m = $PositionToleranceMeters
        public_status = [string]$postState.status
        public_feedback = [string]$postState.feedback
    }
    if (-not [bool]$coherence.coherent) {
        throw "Public save $Name committed a player position $positionDelta m from the fresh public observation (tolerance $PositionToleranceMeters m)."
    }
    $capture | Add-Member -NotePropertyName save_ack_observed -NotePropertyValue ([bool]$commit.ack_observed)
    $capture | Add-Member -NotePropertyName save_ack_count_before -NotePropertyValue ([int]$commit.ack_count_before)
    $capture | Add-Member -NotePropertyName save_ack_count_after -NotePropertyValue ([int]$commit.ack_count_after)
    $capture | Add-Member -NotePropertyName save_ack_was_new_count -NotePropertyValue ([bool]$commit.ack_was_new_count)
    $capture | Add-Member -NotePropertyName save_button_disabled_observed -NotePropertyValue ([bool]$commit.save_button_disabled_observed)
    $capture | Add-Member -NotePropertyName save_file_changed -NotePropertyValue ([bool]$commit.file_changed)
    $capture | Add-Member -NotePropertyName pre_save_sha256 -NotePropertyValue ([string]$commit.pre_save.sha256)
    $capture | Add-Member -NotePropertyName post_save_sha256 -NotePropertyValue ([string]$commit.post_save.sha256)
    $capture | Add-Member -NotePropertyName pre_save_last_write_utc -NotePropertyValue ([string]$commit.pre_save.last_write_utc)
    $capture | Add-Member -NotePropertyName post_save_last_write_utc -NotePropertyValue ([string]$commit.post_save.last_write_utc)
    $capture | Add-Member -NotePropertyName pre_save_public_observation -NotePropertyValue $preObservation
    $capture | Add-Member -NotePropertyName public_save_state -NotePropertyValue $postState
    $capture | Add-Member -NotePropertyName save_state_coherence -NotePropertyValue $coherence
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'public_save_captured' -Payload $capture
    return $capture
}

if ($physicsPlaytestRequestedSelfTest) {
    foreach ($name in @(
        'Open-Gate3PlaytestZone', 'Get-PlaytestPublicState', 'Get-PlaytestObservation',
        'Focus-PlaytestCanvas', 'Release-PlaytestCapture', 'Move-PlaytestRoute',
        'Move-PlaytestWASDSegment', 'Wait-PlaytestReady', 'Wait-PlaytestSettled',
        'Wait-PlaytestCondition', 'Invoke-PlaytestGameplayAction',
        'Start-ForgeStudioPlaytestProcess', 'Stop-ForgeStudioPlaytestProcess',
        'Close-Gate3PlaytestZone', 'Add-PlaytestJournal', 'Copy-IsolatedFixture',
        'Get-ArchiveSnapshot', 'Test-ArchiveSnapshotsEqual', 'ConvertFrom-ExactJson',
        'Get-FileSha256', 'Get-NormalizedSha256', 'Assert-Gate3ProofPath',
        'Test-IsWindowsPlatform', 'Get-PhysicsCollectionCount', 'Send-CdpMouseEvent',
        'Set-PhysicsPlaytestLaunchFacing', 'Get-PhysicsPlaytestProperty',
        'Get-PhysicsWaterSample', 'Wait-PhysicsFreshWaterSamples', 'Activate-PhysicsOwnedWindow'
    )) { [void](Get-Command $name -ErrorAction Stop) }
    if ((Get-PhysicsCollectionCount -Value $null) -ne 0 -or
        (Get-PhysicsCollectionCount -Value ([pscustomobject]@{ value = 1 })) -ne 1 -or
        (Get-PhysicsCollectionCount -Value @([pscustomobject]@{ value = 1 }, [pscustomobject]@{ value = 2 })) -ne 2) {
        throw 'Physics collection count normalization failed for empty, singleton or multi-item input.'
    }
    $raw = [IO.File]::ReadAllText($PSCommandPath)
    foreach ($needle in @(
        'forge.tidewater.gate3-physics-authoring-smoke',
        'Lancer Charge de lancer', 'Allumer Balise inflammable',
        'Pousser', 'released', 'destroyed', 'burning', 'fleeing',
        'Partie sauvegardée.', 'Reprendre la partie', 'gate_qualified = $false',
        'public_save_captured', 'Copy-PhysicsPlaytestSave',
        'Wait-PhysicsPlaytestSaveCommit', 'Get-PhysicsPlaytestSaveMetadata',
        'Test-PhysicsPlaytestSaveMetadataChanged', 'save_file_changed',
        'public_save_pre_observation', 'save_state_coherence', 'PositionToleranceMeters',
        'source_preserved', 'Close-Gate3PlaytestZone', 'public_launch_facing_observation',
        'pointer_locked_public_mouse', 'W=(-sin(yaw),-cos(yaw))',
        'ShoreFireOnly', 'Feu de rive', 'water_query_step',
        'public_water_sample', 'two fresh concordant GPU samples', 'Activate-PhysicsOwnedWindow',
        'Éteindre Feu de rive', 'shore_fire_burning_after_save'
    )) {
        if ($raw.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "Physics playtest contract missing: $needle" }
    }
    Write-Output 'TIDEWATER_GATE3_PHYSICS_PLAYTEST_SMOKE_SELF_TEST_PASS'
    return
}

if (-not (Test-IsWindowsPlatform)) { throw 'Windows/WebView2 is required.' }
$candidate = [IO.Path]::GetFullPath($Candidate)
$source = [IO.Path]::GetFullPath($FreshIsolatedFixture)
$ledgerInput = [IO.Path]::GetFullPath($AuthoringLedger)
$output = [IO.Path]::GetFullPath($EvidenceRoot)
$browserExecutable = Join-Path ([IO.Path]::GetFullPath($BrowserExecutableFolder)) 'msedgewebview2.exe'
if (-not (Test-Path -LiteralPath $browserExecutable -PathType Leaf)) { throw "WebView2 executable absent: $browserExecutable" }
$browserVersion = [string](Get-Item -LiteralPath $browserExecutable -Force).VersionInfo.FileVersion
if ($browserVersion -cne '154.0.4258.37') { throw "WebView2 runtime version mismatch: observed=$browserVersion expected=154.0.4258.37" }
$sourcePin = Get-NormalizedSha256 -Value $SourceOriginalHashPinned
foreach ($entry in @(@($candidate, 'V3 candidate'), @($source, 'V3 physics fixture'), @($ledgerInput, 'V3 authoring ledger'), @($output, 'Physics playtest evidence root'))) {
    Assert-Gate3ProofPath -LiteralPath $entry[0] -Description $entry[1]
}
if (Test-Path -LiteralPath $output) { throw "EvidenceRoot must be fresh: $output" }
if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Candidate absent: $candidate" }
if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "Physics fixture absent: $source" }
if (-not (Test-Path -LiteralPath $ledgerInput -PathType Leaf)) { throw "Physics authoring ledger absent: $ledgerInput" }
Assert-TreeHasNoReparsePoints -LiteralPath $source -Description 'V3 physics fixture'

$candidateHash = Get-FileSha256 -LiteralPath $candidate
$authoring = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($ledgerInput))
if ([string]$authoring.schema -notin @('forge.tidewater.gate3-physics-authoring-smoke', 'forge.tidewater.gate3-physics-retouch-smoke') -or [string]$authoring.status -cne 'passed') { throw 'Physics authoring/retouch ledger is not a passed physics authoring ledger.' }
if ([bool]$authoring.gate_qualified) { throw 'V3 physics authoring ledger incorrectly claims Gate qualification.' }
$sourcePreservationField = if ($authoring.PSObject.Properties.Name -contains 'source_original_archive_preserved') { 'source_original_archive_preserved' } elseif ($authoring.PSObject.Properties.Name -contains 'source_preserved') { 'source_preserved' } else { $null }
if ($null -eq $sourcePreservationField -or -not [bool]$authoring.$sourcePreservationField) { throw 'Physics authoring ledger does not attest original source preservation.' }
if ([string]$authoring.source_original_hash_pinned -cne $sourcePin) { throw 'V3 authoring ledger source pin differs.' }
if ([string]$authoring.candidate_sha256 -cne $candidateHash) { throw 'V3 authoring ledger candidate hash differs.' }
if (-not ([IO.Path]::GetFullPath([string]$authoring.isolated_fixture)).Equals($source, [StringComparison]::OrdinalIgnoreCase)) { throw 'V3 fixture does not match the authoring ledger.' }

$zoneArchive = Join-Path (Join-Path $source $zoneId) 'authoring.json'
$workspaceArchive = Join-Path $source 'workspace-v1\authoring.json'
$sourceObject = Join-Path (Join-Path $source 'workspace-v1\objects') "$sourcePin.json"
foreach ($path in @($zoneArchive, $workspaceArchive, $sourceObject)) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "V3 source archive is absent: $path" } }
$sourceZoneBefore = Get-ArchiveSnapshot -LiteralPath $zoneArchive
$sourceWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $workspaceArchive
$sourceObjectBefore = Get-ArchiveSnapshot -LiteralPath $sourceObject
if ($sourceZoneBefore.Sha256 -cne $sourcePin -or $sourceObjectBefore.Sha256 -cne $sourcePin) { throw 'V3 fixture no longer preserves the pinned original Tidewater bytes.' }
$workspaceDocument = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($workspaceArchive))
$reference = @(
    foreach ($project in @($workspaceDocument.head.projects)) {
        foreach ($module in @($project.modules)) {
            foreach ($zone in @($module.zones)) { if ([string]$zone.area_id -ceq $zoneId) { $zone } }
        }
    }
)
if ($reference.Count -ne 1) { throw 'V3 workspace must contain exactly one Tidewater zone reference.' }
$authoredArchivePin = Get-NormalizedSha256 -Value ([string]$reference[0].archive_hash)
$authoredObject = Join-Path (Join-Path $source 'workspace-v1\objects') "$authoredArchivePin.json"
if (-not (Test-Path -LiteralPath $authoredObject -PathType Leaf)) { throw "V3 authored checkpoint is absent: $authoredObject" }
$authoredBefore = Get-ArchiveSnapshot -LiteralPath $authoredObject
if ($authoredBefore.Sha256 -cne $authoredArchivePin) { throw 'V3 authored checkpoint hash does not match workspace archive_hash.' }
$authoredDocument = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($authoredObject))
$gameplay = $authoredDocument.head.corrections.gameplay
if ($null -eq $gameplay) { throw 'V3 authored checkpoint has no GameplayDocument.' }
$authoringIncludeShoreFire = Get-PhysicsPlaytestProperty -Object $authoring -Name 'include_shore_fire'
if ($ShoreFireOnly -and -not [bool]$authoringIncludeShoreFire) { throw 'ShoreFireOnly requires a passed authoring ledger with include_shore_fire=true.' }
$player = Get-PhysicsPlaytestEntity -Collection $gameplay.actors -Name 'Joueur' -Kind 'player'
$danger = Get-PhysicsPlaytestEntity -Collection $gameplay.actors -Name 'Veilleur du danger' -Kind 'danger actor'
$launch = Get-PhysicsPlaytestEntity -Collection $gameplay.objects -Name 'Charge de lancer' -Kind 'launch object'
$trapSupport = Get-PhysicsPlaytestEntity -Collection $gameplay.objects -Name 'Support piège' -Kind 'trap support'
$trapCharge = Get-PhysicsPlaytestEntity -Collection $gameplay.objects -Name 'Charge piège' -Kind 'trap charge'
$fire = Get-PhysicsPlaytestEntity -Collection $gameplay.objects -Name 'Balise inflammable' -Kind 'fire object'
$shoreFire = if ($ShoreFireOnly) { Get-PhysicsPlaytestEntity -Collection $gameplay.objects -Name 'Feu de rive' -Kind 'shore fire object' } else { $null }
$pushable = @($gameplay.objects | Where-Object { [bool]$_.physics.pushable })
$launchActionText = "Lancer $([string]$launch.name)"
$igniteActionText = "Allumer $([string]$fire.name)"
$launchTargetX = [double]$launch.position.x
$launchTargetZ = [double]$launch.position.z
$fireTargetX = [double]$fire.position.x
$fireTargetZ = [double]$fire.position.z
$shoreFireTargetX = if ($null -ne $shoreFire) { [double]$shoreFire.position.x } else { $null }
$shoreFireTargetZ = if ($null -ne $shoreFire) { [double]$shoreFire.position.z } else { $null }

[void](New-Item -ItemType Directory -Path $output -Force)
$fixture = Join-Path $output 'physics-playtest-root'
$cloneArchive = Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $zoneId
$cloneZoneBefore = Get-ArchiveSnapshot -LiteralPath $cloneArchive
$cloneWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath (Join-Path $fixture 'workspace-v1\authoring.json')
$cloneOriginalBefore = Get-ArchiveSnapshot -LiteralPath (Join-Path (Join-Path $fixture 'workspace-v1\objects') "$sourcePin.json")
$cloneAuthoredBefore = Get-ArchiveSnapshot -LiteralPath (Join-Path (Join-Path $fixture 'workspace-v1\objects') "$authoredArchivePin.json")

$savedEnvironment = @{}
foreach ($name in @('FORGE_AUTHORING_ROOT', 'FORGE_AUTHOR_SUBJECT', 'WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', 'WEBVIEW2_USER_DATA_FOLDER', 'WEBVIEW2_BROWSER_EXECUTABLE_FOLDER')) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
[Environment]::SetEnvironmentVariable('WEBVIEW2_BROWSER_EXECUTABLE_FOLDER', [IO.Path]::GetFullPath($BrowserExecutableFolder), 'Process')
$journal = [System.Collections.Generic.List[object]]::new()
$run1 = $null
$run2 = $null
$failure = $null
$run1Completed = $false
$run2Completed = $false
$forcedProcessStop = $false
$run1Stopped = $true
$run2Stopped = $true
$beforeSave = $null
$afterSave = $null
$resumeSave = $null
$shoreBurningSave = $null
$launchAction = $null
$igniteAction = $null
$shoreIgniteAction = $null
$shoreExtinguishAction = $null
$shoreWaterSamples = @()
$pushAction = $null
$dangerBefore = $null
$dangerAfter = $null
$run1FailurePublicState = $null
$run2FailurePublicState = $null
$launchContactApproachFailure = $null

$route = @(
    [pscustomobject]@{ x = 38.5; z = -117.0 },
    [pscustomobject]@{ x = 38.5; z = -120.0 },
    # The public gameplay probe verified this westward clearance around the
    # village NPC capsule before rejoining the canonical terrain corridor.
    [pscustomobject]@{ x = 39.77840909090909; z = -120.95397727272727 },
    [pscustomobject]@{ x = 30.25; z = -247.975 },
    [pscustomobject]@{ x = 30.075; z = -248.325 },
    # The direct southern/eastern segment reached the authored launch area in
    # V4-007 at x≈19.48,z≈-269.54; the detour through x=25 to x=22.525 hit a
    # zero-resolution slope before reaching the same area.  Keep only the
    # reviewed public segment and rely on the measured north-east contact
    # target below for the final short approach.
    [pscustomobject]@{ x = $launchTargetX + 1.5; z = $launchTargetZ }
)
$routeMaxSeconds = [Math]::Min(420, [Math]::Max(150, $InteractionTimeoutSeconds * 3))
$routeTolerance = 1.5
$shoreRoute = @(
    [pscustomobject]@{ x = 38.5; z = -117.0 },
    [pscustomobject]@{ x = 38.5; z = -120.0 },
    # Keep the player on the west side of the village capsule before taking
    # the low-slope north corridor.  The prior diagonal to (40,-115) stopped
    # with seven contacts despite a valid Tidewater/ShoreSim runtime.
    [pscustomobject]@{ x = 35.0; z = -120.0 },
    [pscustomobject]@{ x = 30.0; z = -120.0 },
    [pscustomobject]@{ x = 30.0; z = -100.0 },
    [pscustomobject]@{ x = 30.0; z = -70.0 },
    [pscustomobject]@{ x = 30.0; z = -40.0 },
    [pscustomobject]@{ x = 30.0; z = -32.0 },
    [pscustomobject]@{ x = $shoreFireTargetX; z = $shoreFireTargetZ }
)

try {
    if ($pushable.Count -eq 1) {
        $pushName = [string]$pushable[0].name
    } elseif ($pushable.Count -eq 0) {
        Add-PlaytestJournal -Journal $journal -Phase 'preflight' -Event 'push_unavailable' -Payload ([ordered]@{ reason = 'V3 authored GameplayDocument contains no pushable object'; push_action_claim = $false })
    } else {
        throw "V3 authoring exposes $($pushable.Count) pushable objects; public push target is ambiguous."
    }

    $run1 = Start-ForgeStudioPlaytestProcess -CandidatePath $candidate -CandidateHash $candidateHash -FixtureRoot $fixture -EvidencePath $output -RunNumber 1 -StartupTimeout $StartupTimeoutSeconds
    $run1Stopped = $false
    Open-Gate3PlaytestZone -Socket $run1.Socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Invoke-UiClick -Socket $run1.Socket -Text 'Tester & suivi' -Selector '[data-authoring-tab-button]')
    [void](Invoke-UiClick -Socket $run1.Socket -Text 'Jouer' -Selector 'button.authoring-play')
    [void](Wait-PlaytestReady -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'V3 physics playtest start')
    Focus-PlaytestCanvas -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_start')
    $beforeSave = Invoke-PhysicsPublicSave -Socket $run1.Socket -FixtureRoot $fixture -EvidencePath $output -Name 'before' -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_before_physics' -PlayerId ([string]$player.id)
    $dangerBefore = $beforeSave
    Activate-PhysicsOwnedWindow -Run $run1

    if ($ShoreFireOnly) {
        [void](Move-PlaytestRoute -Socket $run1.Socket -Waypoints $shoreRoute -Tolerance 1.0 -MaxSeconds $routeMaxSeconds -Journal $journal -Phase 'run1_shore_approach' -RouteId 'v4-measured-shore-route')
    } else {
        [void](Move-PlaytestRoute -Socket $run1.Socket -Waypoints $route -Tolerance $routeTolerance -MaxSeconds $routeMaxSeconds -Journal $journal -Phase 'run1_approach' -RouteId 'v3-village-to-launch-and-fire')
    }

    if (-not $ShoreFireOnly) {
    if ($pushable.Count -eq 1) {
        [void](Move-PlaytestWASDSegment -Socket $run1.Socket -TargetX ([double]$pushable[0].position.x) -TargetZ ([double]$pushable[0].position.z) -Tolerance 0.72 -MaxSeconds 45 -Journal $journal -Phase 'run1_push_approach' -RouteId 'v3-push-approach')
        [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_push_approach')
        $pushLabel = "Pousser $pushName"
        [void](Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'push action public availability' -Condition { param($s) @($s.actions | Where-Object { $_.text -ceq $pushLabel -and -not $_.disabled }).Count -eq 1 })
        $pushAction = Invoke-PlaytestGameplayAction -Socket $run1.Socket -Text $pushLabel -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_push'
    }

    # The authored cube may have been moved by a public retouch.  Approach its
    # south/east flank from the public route; the domain's confirmed-contact
    # observation and positive action acknowledgement decide whether this
    # geometry is actually valid.
    # The route's broad 1.5 m tolerance can stop at (19.5,-269.56), about
    # 0.66 m from the cube, before the confirmed-contact cast is possible.
    # First reach the reviewed eastern flank (20.5,-270) with a tight public
    # movement tolerance, then use the measured V4-water-010 contact point.
    $launchPreApproachState = Get-PlaytestPublicState -Socket $run1.Socket
    $launchPreApproachAction = @($launchPreApproachState.actions | Where-Object { $_.text -ceq $launchActionText -and -not $_.disabled })
    if ($launchPreApproachAction.Count -ne 1) {
        [void](Move-PlaytestWASDSegment -Socket $run1.Socket -TargetX ($launchTargetX + 1.5) -TargetZ $launchTargetZ -Tolerance 0.25 -MaxSeconds 60 -Journal $journal -Phase 'run1_launch_flank_approach' -RouteId 'v4-launch-flank-approach')
    } else {
        Add-PlaytestJournal -Journal $journal -Phase 'run1_launch_flank_approach' -Event 'launch_action_preexposed' -Payload ([ordered]@{
                action = $launchActionText
                public_state = $launchPreApproachState
                method = 'public_action_observation_before_blocked_final_step'
            })
    }
    $launchContactTargetX = $launchTargetX + 0.58
    $launchContactTargetZ = $launchTargetZ - 0.05
    try {
        [void](Move-PlaytestWASDSegment -Socket $run1.Socket -TargetX $launchContactTargetX -TargetZ $launchContactTargetZ -Tolerance 0.10 -MaxSeconds 60 -Journal $journal -Phase 'run1_launch_contact_approach' -RouteId 'v4-launch-contact-approach')
    }
    catch {
        $launchContactApproachFailure = $_.Exception.Message
        Add-PlaytestJournal -Journal $journal -Phase 'run1_launch_contact_approach' -Event 'public_contact_approach_blocked' -Payload ([ordered]@{
                error = $launchContactApproachFailure
                method = 'public_movement_only_then_action_observation'
            })
    }
    [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_launch_contact')
    $launchContactState = Get-PlaytestPublicState -Socket $run1.Socket
    $launchContactObservation = Get-PlaytestObservation -State $launchContactState
    $launchContactDx = $launchContactTargetX - [double]$launchContactObservation.x
    $launchContactDz = $launchContactTargetZ - [double]$launchContactObservation.z
    $launchContactDistance = [Math]::Sqrt($launchContactDx * $launchContactDx + $launchContactDz * $launchContactDz)
    Add-PlaytestJournal -Journal $journal -Phase 'run1_launch_contact' -Event 'launch_contact_public_observation' -Payload ([ordered]@{
            target_x = $launchContactTargetX
            target_z = $launchContactTargetZ
            observation = $launchContactObservation
            horizontal_distance_m = $launchContactDistance
            contact_geometry_target_radius_m = 0.30 + 0.25
        })
    [void](Set-PhysicsPlaytestLaunchFacing -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_launch_facing' -TargetYawDegrees 90.0)
    [void](Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'launch action public availability' -Condition { param($s) @($s.actions | Where-Object { $_.text -ceq $launchActionText -and -not $_.disabled }).Count -eq 1 })
    $launchAction = Invoke-PlaytestGameplayAction -Socket $run1.Socket -Text $launchActionText -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_launch'
    Start-Sleep -Seconds 3
    [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_after_launch')

    [void](Move-PlaytestWASDSegment -Socket $run1.Socket -TargetX $fireTargetX -TargetZ $fireTargetZ -Tolerance 0.72 -MaxSeconds 60 -Journal $journal -Phase 'run1_fire_approach' -RouteId 'v4-fire-approach')
    [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_fire_approach')
    [void](Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'ignite action public availability' -Condition { param($s) @($s.actions | Where-Object { $_.text -ceq $igniteActionText -and -not $_.disabled }).Count -eq 1 })
    $igniteAction = Invoke-PlaytestGameplayAction -Socket $run1.Socket -Text $igniteActionText -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_ignite'
    Start-Sleep -Seconds 4
    [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_after_fire')
    $afterSave = Invoke-PhysicsPublicSave -Socket $run1.Socket -FixtureRoot $fixture -EvidencePath $output -Name 'after' -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_after_physics' -PlayerId ([string]$player.id)
    } else {
        # The shore-only branch exercises the complete-water candidate from a
        # measured low-slope raster point.  It deliberately skips the forest
        # fire/launch sequence so heat damage cannot invalidate the water proof.
        [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_shore_approach_settled')
        $shoreWaterSamples = @(Wait-PhysicsFreshWaterSamples -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_shore_water_before_ignite')
        $shoreIgniteActionText = "Allumer $([string]$shoreFire.name)"
        $shoreExtinguishActionText = "Éteindre $([string]$shoreFire.name)"
        [void](Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'shore ignite action public availability' -Condition { param($s) @($s.actions | Where-Object { $_.text -ceq $shoreIgniteActionText -and -not $_.disabled }).Count -eq 1 })
        $shoreIgniteAction = Invoke-PlaytestGameplayAction -Socket $run1.Socket -Text $shoreIgniteActionText -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_shore_ignite'
        $igniteAction = $shoreIgniteAction
        [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_shore_after_ignite')
        $shoreBurningSave = Invoke-PhysicsPublicSave -Socket $run1.Socket -FixtureRoot $fixture -EvidencePath $output -Name 'shore-burning' -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_shore_burning_save' -PlayerId ([string]$player.id)
        $shoreWaterSamples = @($shoreWaterSamples + @(Wait-PhysicsFreshWaterSamples -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_shore_water_before_extinguish'))
        [void](Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'shore extinguish action public availability' -Condition { param($s) @($s.actions | Where-Object { $_.text -ceq $shoreExtinguishActionText -and -not $_.disabled }).Count -eq 1 })
        $shoreExtinguishAction = Invoke-PlaytestGameplayAction -Socket $run1.Socket -Text $shoreExtinguishActionText -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_shore_extinguish'
        [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_shore_after_extinguish')
        $afterSave = Invoke-PhysicsPublicSave -Socket $run1.Socket -FixtureRoot $fixture -EvidencePath $output -Name 'after' -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1_shore_after_physics' -PlayerId ([string]$player.id)
    }
    $dangerAfter = $afterSave
    Close-Gate3PlaytestZone -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds
    $run1Completed = $true
}
catch {
    if ($null -ne $run1 -and $null -ne $run1.Socket) {
        try { $run1FailurePublicState = Get-PlaytestPublicState -Socket $run1.Socket } catch {}
    }
    $failure = "run1: $($_.Exception.Message) at $($_.InvocationInfo.ScriptName):$($_.InvocationInfo.ScriptLineNumber); position=$($_.InvocationInfo.PositionMessage); stack=$($_.ScriptStackTrace)"
}
finally {
    if ($null -ne $run1) {
        $stop = Stop-ForgeStudioPlaytestProcess -Run $run1
        $forcedProcessStop = $forcedProcessStop -or [bool]$stop.Forced
        $run1Stopped = [bool]$stop.Closed
    }
}

if ([string]::IsNullOrWhiteSpace($failure)) {
    try {
        $run2 = Start-ForgeStudioPlaytestProcess -CandidatePath $candidate -CandidateHash $candidateHash -FixtureRoot $fixture -EvidencePath $output -RunNumber 2 -StartupTimeout $StartupTimeoutSeconds
        $run2Stopped = $false
        Open-Gate3PlaytestZone -Socket $run2.Socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds
        [void](Invoke-UiClick -Socket $run2.Socket -Text 'Tester & suivi' -Selector '[data-authoring-tab-button]')
        [void](Wait-PlaytestCondition -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'V3 public resume button' -Condition { param($s) @($s.allButtons | Where-Object { $_.text -ceq 'Reprendre la partie' -and -not $_.disabled }).Count -eq 1 })
        [void](Invoke-UiClick -Socket $run2.Socket -Text 'Reprendre la partie')
        [void](Wait-PlaytestCondition -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'V3 public resume acknowledgement' -Condition { param($s) @($s.outputs | Where-Object { $_ -ceq 'Reprise préparée.' }).Count -ge 1 })
        [void](Wait-PlaytestReady -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'V3 resumed physics playtest')
        Focus-PlaytestCanvas -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds
        [void](Wait-PlaytestSettled -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run2_resume')
        $resumeSave = Invoke-PhysicsPublicSave -Socket $run2.Socket -FixtureRoot $fixture -EvidencePath $output -Name 'resume' -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run2_resume_save' -PlayerId ([string]$player.id)
        Close-Gate3PlaytestZone -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds
        $run2Completed = $true
    }
    catch {
        if ($null -ne $run2 -and $null -ne $run2.Socket) {
            try { $run2FailurePublicState = Get-PlaytestPublicState -Socket $run2.Socket } catch {}
        }
        $failure = "run2: $($_.Exception.Message) at $($_.InvocationInfo.ScriptName):$($_.InvocationInfo.ScriptLineNumber); position=$($_.InvocationInfo.PositionMessage); stack=$($_.ScriptStackTrace)"
    }
    finally {
        if ($null -ne $run2) {
            $stop = Stop-ForgeStudioPlaytestProcess -Run $run2
            $forcedProcessStop = $forcedProcessStop -or [bool]$stop.Forced
            $run2Stopped = [bool]$stop.Closed
        }
    }
}

foreach ($name in $savedEnvironment.Keys) {
    $value = if ($null -eq $savedEnvironment[$name]) { [NullString]::Value } else { $savedEnvironment[$name] }
    [Environment]::SetEnvironmentVariable($name, $value, 'Process')
}

$sourceZoneAfter = Get-ArchiveSnapshot -LiteralPath $zoneArchive
$sourceWorkspaceAfter = Get-ArchiveSnapshot -LiteralPath $workspaceArchive
$sourceObjectAfter = Get-ArchiveSnapshot -LiteralPath $sourceObject
$sourceAuthoredAfter = Get-ArchiveSnapshot -LiteralPath $authoredObject
$sourcePreserved = (Test-ArchiveSnapshotsEqual -Before $sourceZoneBefore -After $sourceZoneAfter) -and
    (Test-ArchiveSnapshotsEqual -Before $sourceWorkspaceBefore -After $sourceWorkspaceAfter) -and
    (Test-ArchiveSnapshotsEqual -Before $sourceObjectBefore -After $sourceObjectAfter) -and
    (Test-ArchiveSnapshotsEqual -Before $authoredBefore -After $sourceAuthoredAfter)
if (-not $sourcePreserved) { $failure = "V3 source fixture changed during public physics playtest; $failure" }
if ($forcedProcessStop) { $failure = "Owned Studio process required a forced stop; $failure" }
if (-not $run1Stopped -or -not $run2Stopped) { $failure = "Owned Studio process did not close cleanly; $failure" }

$stateSummaries = @{}
foreach ($entry in @($beforeSave, $shoreBurningSave, $afterSave, $resumeSave)) {
    if ($null -eq $entry) { continue }
    try {
        $doc = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText([string]$entry.path))
        $stateSummaries[$entry.path] = [ordered]@{
            state_hash = [string]$doc.state_hash
            source_hash = [string]$doc.source_hash
            player = $doc.state.actors.($player.id)
            danger = $doc.state.actors.($danger.id)
            launch = $doc.state.objects.($launch.id)
            trap_support = $doc.state.objects.($trapSupport.id)
            trap_charge = $doc.state.objects.($trapCharge.id)
            fire = $doc.state.objects.($fire.id)
            shore_fire = if ($null -ne $shoreFire) { $doc.state.objects.($shoreFire.id) } else { $null }
            receipts = @($doc.state.receipts)
            last_frame = $doc.state.last_frame
        }
    } catch { $failure = "save parse: $($_.Exception.Message); $failure" }
}

$beforeState = if ($null -ne $beforeSave -and $stateSummaries.ContainsKey($beforeSave.path)) { $stateSummaries[$beforeSave.path] } else { $null }
$afterState = if ($null -ne $afterSave -and $stateSummaries.ContainsKey($afterSave.path)) { $stateSummaries[$afterSave.path] } else { $null }
$resumeState = if ($null -ne $resumeSave -and $stateSummaries.ContainsKey($resumeSave.path)) { $stateSummaries[$resumeSave.path] } else { $null }
$launchReleased = $null -ne $afterState -and [bool]$afterState.launch.released
$fireBurning = $null -ne $afterState -and [bool]$afterState.fire.burning
$shoreFireAfterState = if ($null -ne $afterState) { $afterState.shore_fire } else { $null }
$shoreFireResumeState = if ($null -ne $resumeState) { $resumeState.shore_fire } else { $null }
$shoreFireBurningAfterSave = $null -ne $shoreFireAfterState -and [bool]$shoreFireAfterState.burning
$shoreFireBurningAfterResume = $null -ne $shoreFireResumeState -and [bool]$shoreFireResumeState.burning
$shoreSampleCount = if ($ShoreFireOnly) { @($shoreWaterSamples).Count } else { $null }
$shoreExtinguishAcknowledged = if ($ShoreFireOnly) { $null -ne $shoreExtinguishAction } else { $null }
$dangerFleeing = $null -ne $afterState -and [bool]$afterState.danger.fleeing
$trapChanged = $null -ne $afterState -and ([bool]$afterState.trap_support.destroyed -or [bool]$afterState.trap_support.released -or [bool]$afterState.trap_charge.in_motion -or [bool]$afterState.trap_charge.released)
$afterEvents = if ($null -ne $afterState) { @($afterState.receipts | ForEach-Object events | ForEach-Object { $_.PSObject.Properties.Name }) } else { @() }
$requiredPhysicsEvents = @('Impulse', 'Released', 'Impact', 'Destroyed', 'Burning', 'Fleeing')
$observedEvents = @($requiredPhysicsEvents | Where-Object { $afterEvents -contains $_ })

$checks = [ordered]@{
    authoring_v3_passed = $true
    run1_public_playtest_completed = $run1Completed
    run2_public_resume_completed = $run2Completed
    launch_action_acknowledged = $null -ne $launchAction
    launch_released_in_after_save = $launchReleased
    fire_action_acknowledged = $null -ne $igniteAction
    fire_burning_in_after_save = $fireBurning
    danger_fleeing_in_after_save = $dangerFleeing
    trap_or_charge_state_changed = $trapChanged
    required_event_names_observed = @('Impulse', 'Burning') | Where-Object { $afterEvents -contains $_ } | Measure-Object | Select-Object -ExpandProperty Count
    pushable_target_present = $pushable.Count -eq 1
    push_action_acknowledged = $null -ne $pushAction
    save_before_after_resume_captured = $null -ne $beforeSave -and $null -ne $afterSave -and $null -ne $resumeSave
    source_preserved = $sourcePreserved
    owned_processes_closed = $run1Stopped -and $run2Stopped
    shore_water_samples = $shoreSampleCount
    shore_extinguish_action_acknowledged = $shoreExtinguishAcknowledged
    shore_fire_burning_after_save = $shoreFireBurningAfterSave
    shore_fire_burning_after_resume = $shoreFireBurningAfterResume
}
$ignoredChecks = @('pushable_target_present', 'push_action_acknowledged', 'trap_or_charge_state_changed')
if ($ShoreFireOnly) {
    $ignoredChecks += @('launch_action_acknowledged', 'launch_released_in_after_save', 'fire_burning_in_after_save', 'danger_fleeing_in_after_save', 'required_event_names_observed')
}
$checksValues = @($checks.GetEnumerator() | Where-Object { $_.Key -notin $ignoredChecks } | ForEach-Object {
        if ($ShoreFireOnly -and $_.Key -eq 'shore_fire_burning_after_save') { return -not [bool]$_.Value }
        if ($ShoreFireOnly -and $_.Key -eq 'shore_fire_burning_after_resume') { return -not [bool]$_.Value }
        [bool]$_.Value
    })
$mechanicalCorePassed = [string]::IsNullOrWhiteSpace($failure) -and $checksValues.Count -gt 0 -and (@($checksValues | Where-Object { -not $_ }).Count -eq 0)
$ledgerRoute = if ($ShoreFireOnly) { $shoreRoute } else { $route }
$shoreTargetRecord = $null
if ($ShoreFireOnly) {
    $shoreTargetRecord = [ordered]@{ x = $shoreFireTargetX; z = $shoreFireTargetZ; y = [double]$shoreFire.position.y }
}
$waterLimitation = if ($ShoreFireOnly) {
    'This shore-only run uses the complete-water candidate and requires two strictly increasing public GPU water_query_step samples, concordant query position and surface-above-feet evidence before Extinguish; it does not qualify the whole Gate 3.'
} else {
    'The default V3 path tests ignition/fleeing only; extinction requires the water-enabled candidate and a fresh water-contact sample.'
}

$ledger = [ordered]@{
    schema = 'forge.tidewater.gate3-physics-playtest-smoke'
    version = 1
    status = if ($mechanicalCorePassed) { 'passed' } else { 'failed_closed' }
    failure = $failure
    gate_qualified = $false
    human_visual_review = $false
    authoring_ledger = $ledgerInput
    candidate = $candidate
    candidate_sha256 = $candidateHash
    browser_executable_folder = [IO.Path]::GetFullPath($BrowserExecutableFolder)
    browser_runtime_version = $browserVersion
    source_fixture = $source
    source_original_hash_pinned = $sourcePin
    shore_fire_only = [bool]$ShoreFireOnly
    isolated_fixture = $fixture
    source_preserved = $sourcePreserved
    authoring_phase = [ordered]@{
        status = [string]$authoring.status
        ledger = $ledgerInput
        recipe_names = $physicsNames
        source_original_hash_pinned = $sourcePin
        authored_checkpoint = $authoredArchivePin
    }
    public_playtest_phase = [ordered]@{
        run1_completed = $run1Completed
        run2_resume_completed = $run2Completed
        launch_action = $launchAction
        push_action = $pushAction
        ignite_action = $igniteAction
        shore_ignite_action = $shoreIgniteAction
        shore_extinguish_action = $shoreExtinguishAction
        shore_burning_save = $shoreBurningSave
        save_before = $beforeSave
        save_after = $afterSave
        save_resume = $resumeSave
        run1_failure_public_state = $run1FailurePublicState
        run2_failure_public_state = $run2FailurePublicState
        state_summaries = $stateSummaries
        observed_event_names = $observedEvents
        route_waypoints_xz = @($ledgerRoute | ForEach-Object { [ordered]@{ x = [double]$_.x; z = [double]$_.z } })
        route_tolerance_m = $routeTolerance
        launch_contact_approach_failure = $launchContactApproachFailure
        shore_water_samples = @($shoreWaterSamples)
        shore_fire_target_xz = $shoreTargetRecord
    }
    checks = $checks
    limitations = @(
        'All interactions are public Studio mouse/keyboard controls and public Save/Reprendre controls; no Runtime evaluation, Tauri command or JSON state injection is used.',
        'V3 authoring currently contains no pushable object, so the push action is recorded as unavailable and no push claim is made.',
        'The throw action is accepted only when the runtime reports confirmed physical contact; a positive UI action acknowledgement alone does not prove an impact.',
        'The trap support/charge shares the authored guardian position; the smoke records released/destroyed/impact state but does not infer causal contact from proximity.',
        $waterLimitation,
        'The saved JSON is evidence of serialized runtime state and events; it is not a human visual review, navigation proof or Gate 3 qualification.'
    )
}
[IO.File]::WriteAllText((Join-Path $output 'gate3-physics-playtest-smoke-ledger.json'), ($ledger | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
if ($ledger.status -cne 'passed') { throw "Gate 3 physics playtest smoke failed closed: $failure" }
Write-Output "TIDEWATER_GATE3_PHYSICS_PLAYTEST_SMOKE_PASS ledger=$(Join-Path $output 'gate3-physics-playtest-smoke-ledger.json')"
