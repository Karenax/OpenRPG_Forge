#Requires -Version 7.2
# Public Studio input and Save only. Source/game state is never injected.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Candidate,
    [Parameter(Mandatory)][string]$FreshIsolatedFixture,
    [Parameter(Mandatory)][string]$AuthoringLedger,
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [ValidateSet('EnvironmentQuest','EnvironmentFireQuest','ForestDanger','ForestThrow','ShorePush','ShoreWater')][string]$Scenario='EnvironmentQuest',
    [int]$InteractionTimeoutSeconds=90
)
$cfg=@{Candidate=$Candidate;Source=$FreshIsolatedFixture;Ledger=$AuthoringLedger;Output=$EvidenceRoot;Scenario=$Scenario;Timeout=$InteractionTimeoutSeconds}
$physicsScriptInputBytes=@(foreach($scriptPath in @($PSCommandPath,(Join-Path $PSScriptRoot 'Invoke-Gate3GameplayAuthoringSmoke.ps1'),(Join-Path $PSScriptRoot 'Invoke-Gate3PhysicsPlaytestSmoke.ps1'),(Join-Path $PSScriptRoot 'Invoke-Gate3GameplayPlaytestSmoke.ps1'),(Join-Path $PSScriptRoot 'Gate3ProofStorage.ps1'),(Join-Path $PSScriptRoot '../Invoke-Gate2WorldEditingProof.ps1'))){$bytes=[IO.File]::ReadAllBytes($scriptPath);[pscustomobject]@{path=[IO.Path]::GetFullPath($scriptPath);bytes=$bytes;sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant();captured_utc=[DateTimeOffset]::UtcNow.ToString('o')}})
. (Join-Path $PSScriptRoot 'Invoke-Gate3GameplayAuthoringSmoke.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Invoke-Gate3PhysicsPlaytestSmoke.ps1') -SelfTest | Out-Null
foreach($scriptInput in $physicsScriptInputBytes){if((Get-FileSha256 -LiteralPath $scriptInput.path) -cne $scriptInput.sha256){throw "A public helper changed while loading: $($scriptInput.path)."}}
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$InteractionTimeoutSeconds=[int]$cfg.Timeout
$zoneId='e620dd19-ead9-5913-a6d6-0f93109148c0'
$sourcePin='50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720'
$candidate=[IO.Path]::GetFullPath($cfg.Candidate);$source=[IO.Path]::GetFullPath($cfg.Source);$output=[IO.Path]::GetFullPath($cfg.Output)
$candidateHash=Get-FileSha256 -LiteralPath $candidate
$authoringLedgerHash=Get-FileSha256 -LiteralPath $cfg.Ledger
$prior=ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($cfg.Ledger))
if($prior.status -cne 'passed' -or $prior.candidate_sha256 -cne $candidateHash -or $prior.isolated_fixture -cne $source -or -not $prior.source_preserved){throw 'Passed source authoring identity differs.'}
foreach($p in @($candidate,$source,$output)){Assert-Gate3ProofPath -LiteralPath $p -Description 'complete public physics proof'}
if(Test-Path -LiteralPath $output){throw 'Evidence root must be fresh.'}
Assert-TreeHasNoReparsePoints -LiteralPath $source -Description 'complete physics source'
$sourceWorkspace=Join-Path $source 'workspace-v1/authoring.json'
$reference=Get-Gate3WorkspaceReference -Archive $sourceWorkspace -ZoneId $zoneId
$authoredHash=[string]$reference.archive_hash
if([string]$reference.archive_hash -cne [string]$prior.saved_reference.archive_hash -or [string]$reference.head_hash -cne [string]$prior.saved_reference.head_hash){throw 'Current source checkpoint differs from the public authoring ledger.'}
$sourcePaths=@((Join-Path $source "$zoneId/authoring.json"),$sourceWorkspace,(Join-Path $source "workspace-v1/objects/$sourcePin.json"),(Join-Path $source "workspace-v1/objects/$authoredHash.json"))
$sourceBefore=@($sourcePaths|ForEach-Object{Get-ArchiveSnapshot -LiteralPath $_})
if($sourceBefore[0].Sha256 -cne $sourcePin -or $sourceBefore[2].Sha256 -cne $sourcePin){throw 'Original Tidewater archive differs.'}
if($sourceBefore[3].Sha256 -cne $authoredHash){throw 'Authored source bytes differ from the checkpoint hash.'}
$doc=ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($sourcePaths[3]))
$gp=$doc.head.corrections.gameplay
$player=Get-PhysicsPlaytestEntity -Collection $gp.actors -Name 'Joueur' -Kind 'player'
$guard=Get-PhysicsPlaytestEntity -Collection $gp.actors -Name 'Gardien de la forêt' -Kind 'guardian'
$danger=Get-PhysicsPlaytestEntity -Collection $gp.actors -Name 'Veilleur du danger' -Kind 'danger actor'
$launch=Get-PhysicsPlaytestEntity -Collection $gp.objects -Name 'Charge de lancer' -Kind 'throw object'
$support=Get-PhysicsPlaytestEntity -Collection $gp.objects -Name 'Support piège' -Kind 'trap support'
$load=Get-PhysicsPlaytestEntity -Collection $gp.objects -Name 'Charge piège' -Kind 'trap load'
$fire=Get-PhysicsPlaytestEntity -Collection $gp.objects -Name 'Balise inflammable' -Kind 'forest fire'
$shore=Get-PhysicsPlaytestEntity -Collection $gp.objects -Name 'Feu de rive' -Kind 'shore fire'
$push=Get-PhysicsPlaytestEntity -Collection $gp.objects -Name 'Bloc poussable de rive' -Kind 'push object'
$counter=Get-PhysicsPlaytestEntity -Collection $gp.objects -Name 'Bloc de contrepoids' -Kind 'environment occluder'
$seal=Get-PhysicsPlaytestEntity -Collection $gp.objects -Name 'Sceau' -Kind 'quest pickup'
[void](New-Item -ItemType Directory -Path $output)
$scriptInputRoot=Join-Path $output 'loaded-script-sources';[void](New-Item -ItemType Directory -Path $scriptInputRoot)
$loadedScriptManifest=@(foreach($scriptInput in $physicsScriptInputBytes){$copy=Join-Path $scriptInputRoot ([IO.Path]::GetFileName($scriptInput.path));[IO.File]::WriteAllBytes($copy,$scriptInput.bytes);[ordered]@{source_path=$scriptInput.path;loaded_sha256=$scriptInput.sha256;captured_utc=$scriptInput.captured_utc;verified_unchanged_after_import_utc=[DateTimeOffset]::UtcNow.ToString('o');copy_path=$copy;length=$scriptInput.bytes.Length}})
[IO.File]::WriteAllText((Join-Path $output 'loaded-script-inputs.json'),(ConvertTo-Json -InputObject $loadedScriptManifest -Depth 10),[Text.UTF8Encoding]::new($false))
$publicInputTranscript=Join-Path $output 'public-input-transcript.log'
[void](Start-Transcript -Path $publicInputTranscript -NoClobber)
$fixture=Join-Path $output 'physics-playtest-root'
$cloneArchive=Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $zoneId
$clonePaths=@($cloneArchive,(Join-Path $fixture 'workspace-v1/authoring.json'),(Join-Path $fixture "workspace-v1/objects/$sourcePin.json"),(Join-Path $fixture "workspace-v1/objects/$authoredHash.json"))
$cloneBefore=@($clonePaths|ForEach-Object{Get-ArchiveSnapshot -LiteralPath $_})
$envBefore=@{}
foreach($n in @('FORGE_AUTHORING_ROOT','FORGE_AUTHOR_SUBJECT','WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS','WEBVIEW2_USER_DATA_FOLDER','WEBVIEW2_BROWSER_EXECUTABLE_FOLDER')){$envBefore[$n]=[Environment]::GetEnvironmentVariable($n,'Process')}
[Environment]::SetEnvironmentVariable('WEBVIEW2_BROWSER_EXECUTABLE_FOLDER','C:\Program Files (x86)\Microsoft\EdgeWebView\Application\154.0.4258.37','Process')
$journal=[System.Collections.Generic.List[object]]::new();$saves=[ordered]@{};$actions=[ordered]@{};$failure=$null;$run1=$null;$run2=$null;$closed1=$true;$closed2=$true;$forced=$false;$waterSamples=@();$completed1=$false;$completed2=$false;$fireExposurePlan=$null;$environmentBeforeCombat=$null
function Mark-PhysicsPhase([string]$Phase){Write-Host "GATE3_PUBLIC_PHYSICS_PHASE $Phase";[IO.File]::WriteAllText((Join-Path $output 'phase.txt'),$Phase,[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText((Join-Path $output 'journal-in-progress.json'),(@($journal)|ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))}
function Save-PhysicsCheckpoint([object]$Socket,[string]$Name,[int]$MaxWaitSeconds=60,[int]$AttemptTimeoutSeconds=5){
    Mark-PhysicsPhase "save-$Name"
    $deadline=[DateTimeOffset]::UtcNow.AddSeconds($MaxWaitSeconds)
    do {
        try {$capture=Invoke-PhysicsPublicSave -Socket $Socket -FixtureRoot $fixture -EvidencePath $output -Name $Name -TimeoutSeconds $AttemptTimeoutSeconds -Journal $journal -Phase $Name -PlayerId ([string]$player.id);$saves[$Name]=$capture;return (ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($capture.path)))}
        catch {if($_.Exception.Message -notmatch 'did not produce a new committed'){throw};Add-PlaytestJournal -Journal $journal -Phase $Name -Event 'public_save_retry' -Payload $_.Exception.Message}
    }while([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Public Save '$Name' never committed while physical objects settled."
}
function Do-PhysicsAction([object]$Socket,[string]$Text,[string]$Phase){Mark-PhysicsPhase $Phase;[void](Wait-PlaytestCondition -Socket $Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description $Text -Condition {param($s)@($s.actions|Where-Object{$_.text -ceq $Text -and -not $_.disabled}).Count -eq 1});$actions[$Phase]=Invoke-PlaytestGameplayAction -Socket $Socket -Text $Text -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase $Phase}
function Assert-PhysicsEnvironmentBeforeCombat {
    param([Parameter(Mandatory)][ValidateSet('impact','heat')][string]$Kind,
        [Parameter(Mandatory)][object]$Before,[Parameter(Mandatory)][object]$Witness,
        [Parameter(Mandatory)][object]$Neutralized,[Parameter(Mandatory)][object]$BeforeCapture,[Parameter(Mandatory)][object]$WitnessCapture,
        [Parameter(Mandatory)][object]$NeutralizedCapture,[Parameter(Mandatory)][string]$SourceHash,
        [Parameter(Mandatory)][string]$PlayerId,[Parameter(Mandatory)][string]$GuardId,
        [Parameter(Mandatory)][string[]]$ObjectIds,[Parameter(Mandatory)][string]$FireId,
        [Parameter(Mandatory)][string]$WitnessPhase,[Parameter(Mandatory)][string]$NeutralizedPhase)
    $requiredWitness=if($Kind -ceq 'impact'){'launch-after'}else{'fire-exposure'}
    $requiredNeutralized=if($Kind -ceq 'impact'){'launch-after'}else{'post-danger'}
    if($WitnessPhase -cne $requiredWitness -or $NeutralizedPhase -cne $requiredNeutralized){throw 'Environment evidence is not from the required pre-combat phases.'}
    foreach($document in @($Before,$Witness,$Neutralized)){
        if([string]$document.source_hash -cne $SourceHash -or [string]$document.state.last_frame.frame.source_hash -cne $SourceHash -or
            [long]$document.state.last_frame.frame.tick -ne [long]$document.state.tick){throw 'Environment evidence source or exact frame tick differs.'}
        if(@($document.state.receipts|Where-Object{[string]$_.request.action.kind -ceq 'attack' -and [string]$_.request.action.target -ceq $GuardId}).Count){throw 'A combat receipt already exists in the pre-combat evidence.'}
    }
    foreach($capture in @($BeforeCapture,$WitnessCapture,$NeutralizedCapture)){
        $digest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes([string]$capture.path))).ToLowerInvariant()
        if($digest -cne [string]$capture.post_save_sha256 -or -not $capture.save_ack_observed -or -not $capture.save_file_changed -or -not $capture.save_state_coherence.coherent){throw 'Pre-combat Save file or committed coherence differs.'}
    }
    if([long]$Witness.state.tick -le [long]$Before.state.tick -or [long]$Neutralized.state.tick -lt [long]$Witness.state.tick -or
        [long]$Witness.state.sequence -le [long]$Before.state.sequence -or [long]$Neutralized.state.sequence -ne [long]$Witness.state.sequence){throw 'Environment evidence tick/sequence does not precede combat.'}
    $kindName=if($Kind -ceq 'impact'){'throw'}else{'ignite'}
    $actionObject=if($Kind -ceq 'impact'){$ObjectIds[0]}else{$FireId}
    $causalAction=@($Witness.state.receipts|Where-Object{[string]$_.request.action.kind -ceq $kindName -and [string]$_.request.action.object -ceq $actionObject -and
        [string]$_.request.actor -ceq $PlayerId -and [long]$_.sequence -eq [long]$Witness.state.sequence -and [long]$_.tick -gt [long]$Before.state.tick -and [long]$_.tick -le [long]$Witness.state.tick})
    if($causalAction.Count -ne 1){throw 'Environment evidence lacks the exact new Player throw/ignite receipt.'}
    $causes=[System.Collections.Generic.List[object]]::new()
    if($Kind -ceq 'impact'){
        foreach($entry in $Witness.state.contacts.PSObject.Properties){
            $contact=$entry.Value;$o=$contact.observation
            if([string]$entry.Name -cne [string]$o.command_id -or [string]$o.source -cnotin $ObjectIds -or [string]$o.target -cne $GuardId -or [string]$o.initiator -cne $PlayerId -or
                [double]$o.normal_speed_m_s -le 0 -or [long]$contact.tick -lt [long]$causalAction[0].tick -or [long]$contact.tick -gt [long]$Witness.state.tick){continue}
            $impact=@($contact.events|Where-Object{[string]$_.kind -ceq 'impact' -and [string]$_.source -ceq [string]$o.source -and [string]$_.target -ceq $GuardId -and [string]$_.initiator -ceq $PlayerId -and [double]$_.energy_j -gt 0})
            $damage=@($contact.events|Where-Object{[string]$_.kind -ceq 'damage' -and [string]$_.target -ceq $GuardId -and $_.contact -eq $true -and [int]$_.amount -gt 0})
            if($impact.Count -and $damage.Count){[void]$causes.Add([ordered]@{command_id=[string]$entry.Name;tick=[long]$contact.tick;observation=$o;impact=$impact;damage=$damage})}
        }
    }else{
        $frame=$Witness.state.last_frame
        $heat=@($frame.frame.heat|Where-Object{[string]$_.object -ceq $FireId -and [string]$_.actor -ceq $GuardId -and $_.line_of_sight -eq $true})
        $damage=@($frame.events|Where-Object{[string]$_.kind -ceq 'damage' -and [string]$_.target -ceq $GuardId -and $_.contact -eq $false -and [int]$_.amount -gt 0})
        $burning=@($causalAction[0].events|Where-Object{[string]$_.kind -ceq 'burning' -and [string]$_.object -ceq $FireId -and $_.active -eq $true})
        if($heat.Count -and $damage.Count -and $burning.Count -and [long]$frame.frame.tick -ge [long]$causalAction[0].tick){[void]$causes.Add([ordered]@{tick=[long]$frame.frame.tick;heat=$heat;damage=$damage})}
    }
    if(-not $causes.Count){throw 'No exact environmental Impact/Damage or heat/LOS receipt exists before combat.'}
    if([int]$Before.state.actors.($GuardId).vitality -le 0 -or [int]$Neutralized.state.actors.($GuardId).vitality -ne 0){throw 'The environment did not neutralize the living guardian before combat; damage followed by punches cannot qualify.'}
    return [ordered]@{kind=$Kind;source_hash=$SourceHash;witness_phase=$WitnessPhase;neutralized_phase=$NeutralizedPhase;
        before_path=[string]$BeforeCapture.path;before_sha256=[string]$BeforeCapture.post_save_sha256;witness_path=[string]$WitnessCapture.path;witness_sha256=[string]$WitnessCapture.post_save_sha256;neutralized_path=[string]$NeutralizedCapture.path;neutralized_sha256=[string]$NeutralizedCapture.post_save_sha256;
        before_tick=[long]$Before.state.tick;before_sequence=[long]$Before.state.sequence;witness_tick=[long]$Witness.state.tick;witness_sequence=[long]$Witness.state.sequence;
        neutralized_tick=[long]$Neutralized.state.tick;neutralized_sequence=[long]$Neutralized.state.sequence;causal_action=$causalAction[0];causes=@($causes);
        no_combat_before_neutralization=$true;guardian_neutralized_before_combat=$true;individual_lethal_heat_event_claim=$false}
}
function Assert-PhysicsSavedFireRange([object]$Socket,[object]$Saved,[string]$TargetId,[bool]$OutsideHeat,[string]$Phase){
    $savedPlayer=$Saved.state.actors.($player.id).position
    $savedFire=$Saved.state.objects.($fire.id).position
    $savedTarget=$Saved.state.objects.($TargetId).position
    $range=[double]$gp.profile.interaction_range_m
    $fireDistance=[Math]::Sqrt([Math]::Pow([double]$savedPlayer.x-[double]$savedFire.x,2)+[Math]::Pow([double]$savedPlayer.y-[double]$savedFire.y,2)+[Math]::Pow([double]$savedPlayer.z-[double]$savedFire.z,2))
    $targetDistance=[Math]::Sqrt([Math]::Pow([double]$savedPlayer.x-[double]$savedTarget.x,2)+[Math]::Pow([double]$savedPlayer.y-[double]$savedTarget.y,2)+[Math]::Pow([double]$savedPlayer.z-[double]$savedTarget.z,2))
    $observation=Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $Socket)
    $drift=[Math]::Sqrt([Math]::Pow([double]$observation.x-[double]$savedPlayer.x,2)+[Math]::Pow([double]$observation.y-[double]$savedPlayer.y,2)+[Math]::Pow([double]$observation.z-[double]$savedPlayer.z,2))
    Add-PlaytestJournal -Journal $journal -Phase $Phase -Event 'public_saved_pose_range_preflight' -Payload ([ordered]@{saved_player=$savedPlayer;saved_fire=$savedFire;saved_target=$savedTarget;fire_distance_m=$fireDistance;target_distance_m=$targetDistance;profile_range_m=$range;outside_heat_required=$OutsideHeat;public_observation=$observation;save_to_public_drift_m=$drift;line_of_sight_preflight_available=$false;line_of_sight_claim='Only the unchanged public guarded action acknowledgement and real exposure observations can attest LOS.'})
    if(-not $observation.ok -or -not $observation.simulation_ready -or -not $observation.grounded -or $drift -gt 0.08){throw 'The real public pose did not remain stable after its coherent Save.'}
    if($targetDistance -gt $range){throw 'The actual saved player pose is outside the unchanged interaction range.'}
    if($OutsideHeat -and $fireDistance -le $range){throw 'The actual saved pickup pose remains inside fire heat range.'}
}
function Get-PhysicsPublicPulseXZ([object]$Observation){
    $x=[double]$Observation.x;$z=[double]$Observation.z;$source='rounded_public_hud'
    if($null -ne $Observation.locomotion){
        $property=$Observation.locomotion.PSObject.Properties['anchor']
        if($null -eq $property){throw 'Public locomotion is present without its anchor.'}
        $anchor=@($property.Value)
        if($anchor.Count -ne 3){throw 'Public locomotion anchor is not a three-component vector.'}
        foreach($component in $anchor){
            $numeric=$component -is [double] -or $component -is [single] -or $component -is [decimal] -or $component -is [int] -or $component -is [long]
            if(-not $numeric -or -not [double]::IsFinite([double]$component)){throw 'Public locomotion anchor contains a nonnumeric or nonfinite component.'}
        }
        $x=[double]$anchor[0];$z=[double]$anchor[2];$source='public_locomotion_anchor'
    }
    if(-not [double]::IsFinite($x) -or -not [double]::IsFinite($z)){throw 'Public pulse X/Z position is nonfinite.'}
    return [ordered]@{x=$x;z=$z;source=$source}
}
function Move-PhysicsPublicPulses([object]$Socket,[double]$X,[double]$Z,[double]$Tolerance,[string]$Phase){
    Focus-PlaytestCanvas -Socket $Socket -TimeoutSeconds $InteractionTimeoutSeconds
    $deadline=[DateTimeOffset]::UtcNow.AddSeconds(12)
    do{
        $state=Get-PlaytestPublicState -Socket $Socket
        $o=Get-PlaytestObservation -State $state
        if(-not $o.ok -or -not $o.simulation_ready){throw 'Public pulse approach lost ready position.'}
        if(@($state.outputs|Where-Object {$_ -match '^Vitalité 0\b'}).Count){throw 'Public pulse approach observed player defeated.'}
        if(-not $o.input_active){Focus-PlaytestCanvas -Socket $Socket -TimeoutSeconds $InteractionTimeoutSeconds;continue}
        if(-not [string]::IsNullOrWhiteSpace([string]$state.locomotion_error)){throw 'Public locomotion diagnostic is malformed.'}
        $pulsePosition=Get-PhysicsPublicPulseXZ $o
        $dx=$X-$pulsePosition.x;$dz=$Z-$pulsePosition.z;$d=[Math]::Sqrt($dx*$dx+$dz*$dz)
        Add-PlaytestJournal -Journal $journal -Phase $Phase -Event 'public_pulse_observation' -Payload ([ordered]@{target_x=$X;target_z=$Z;distance_m=$d;position_source=$pulsePosition.source;position_x=$pulsePosition.x;position_z=$pulsePosition.z;observation=$o})
        if($d -le $Tolerance){return $o}
        $keys=@(Get-PlaytestRouteKeys -DeltaX $dx -DeltaZ $dz -YawDegrees ([double]$o.yaw_degrees))
        $held=[System.Collections.Generic.List[object]]::new()
        try{
            foreach($key in $keys){Send-Gate3PlaytestKeyEvent -Socket $Socket -Type rawKeyDown -Key $key.key -Code $key.code -VirtualKeyCode $key.vk;$held.Add($key)}
            Start-Sleep -Milliseconds ([int][Math]::Clamp($d*100,15,55))
        }finally{foreach($key in $held){Send-Gate3PlaytestKeyEvent -Socket $Socket -Type keyUp -Key $key.key -Code $key.code -VirtualKeyCode $key.vk}}
        Start-Sleep -Milliseconds 80
    }while([DateTimeOffset]::UtcNow -lt $deadline)
    throw "Public pulse approach did not reach ($X,$Z); no continued pressure is allowed."
}
function Finish-PhysicsPublicQuest([object]$Socket){
    if($cfg.Scenario -ceq 'EnvironmentFireQuest'){
        $safePickupRoute=@([pscustomobject]@{x=18;z=-265.8},[pscustomobject]@{x=18;z=-266.9})
        [void](Move-PlaytestRoute -Socket $Socket -Waypoints $safePickupRoute -Tolerance 0.15 -MaxSeconds 120 -Journal $journal -Phase 'pickup-clear-heat' -RouteId 'public-north-pickup-outside-fire-range')
        [void](Wait-PlaytestLocomotionSettled -Socket $Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'pickup-safe-settle' -SampleMilliseconds 120)
        $pickupSafe=Save-PhysicsCheckpoint $Socket 'pickup-safe'
        Assert-PhysicsSavedFireRange $Socket $pickupSafe ([string]$seal.id) $true 'pickup-preflight'
    }else{Move-PlaytestToXZ -Socket $Socket -X 18 -Z -268 -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'pickup'}
    Do-PhysicsAction $Socket 'Ramasser Sceau' 'pickup'
    [void](Wait-PlaytestCondition -Socket $Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'exact seal inventory' -Condition {param($s)$s.items -contains 'Sceau × 1'})
    if($cfg.Scenario -cne 'EnvironmentFireQuest'){[void](Wait-PlaytestLocomotionSettled -Socket $Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'pickup-settle' -SampleMilliseconds 120)}
    $returnRoute=@([pscustomobject]@{x=18;z=-265},[pscustomobject]@{x=25;z=-265},[pscustomobject]@{x=30.075;z=-248.325},[pscustomobject]@{x=30.25;z=-247.975},[pscustomobject]@{x=39.77840909090909;z=-120.95397727272727},[pscustomobject]@{x=38.5;z=-120},[pscustomobject]@{x=38.5;z=-117})
    Mark-PhysicsPhase 'quest-return'
    [void](Move-PlaytestRoute -Socket $Socket -Waypoints $returnRoute -Tolerance 0.75 -MaxSeconds 420 -Journal $journal -Phase 'quest-return' -RouteId 'east-corridor-village-return')
    Do-PhysicsAction $Socket "Voici l’objet demandé." 'quest-complete'
    [void](Wait-PlaytestCondition -Socket $Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'completed quest without seal duplication' -Condition {param($s)@($s.items|Where-Object{$_ -match '^Rapporter le Sceau\s+·\s+Completed$'}).Count -eq 1 -and $s.items -notcontains 'Sceau × 1'})
}
$forestRoute=@([pscustomobject]@{x=38.5;z=-117},[pscustomobject]@{x=38.5;z=-120},[pscustomobject]@{x=39.77840909090909;z=-120.95397727272727},[pscustomobject]@{x=30.25;z=-247.975},[pscustomobject]@{x=30.075;z=-248.325},[pscustomobject]@{x=25;z=-270},[pscustomobject]@{x=25;z=-272},[pscustomobject]@{x=20.5;z=-272},[pscustomobject]@{x=20.5;z=-270})
$shoreRoute=@([pscustomobject]@{x=38.5;z=-117},[pscustomobject]@{x=38.5;z=-120},[pscustomobject]@{x=35;z=-120},[pscustomobject]@{x=30;z=-120},[pscustomobject]@{x=30;z=-100},[pscustomobject]@{x=30;z=-70},[pscustomobject]@{x=30;z=-40},[pscustomobject]@{x=30;z=-33.5},[pscustomobject]@{x=42;z=-33.5})
try {
    Mark-PhysicsPhase 'run1-start'
    $run1=Start-ForgeStudioPlaytestProcess -CandidatePath $candidate -CandidateHash $candidateHash -FixtureRoot $fixture -EvidencePath $output -RunNumber 1 -StartupTimeout 180;$closed1=$false
    Open-Gate3PlaytestZone -Socket $run1.Socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Invoke-UiClick -Socket $run1.Socket -Text 'Tester & suivi' -Selector '[data-authoring-tab-button]')
    [void](Invoke-UiClick -Socket $run1.Socket -Text 'Jouer' -Selector 'button.authoring-play')
    [void](Wait-PlaytestReady -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'complete public physics start')
    Activate-PhysicsOwnedWindow -Run $run1
    Focus-PlaytestCanvas -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'start')
    $before=Save-PhysicsCheckpoint $run1.Socket 'before'
    if($cfg.Scenario -in @('ShorePush','ShoreWater')){
        Mark-PhysicsPhase 'shore-route'
        [void](Move-PlaytestRoute -Socket $run1.Socket -Waypoints $shoreRoute -Tolerance 0.6 -MaxSeconds 360 -Journal $journal -Phase 'shore-approach' -RouteId 'measured-shore-route')
        if($cfg.Scenario -ceq 'ShorePush'){
        $pushBefore=Save-PhysicsCheckpoint $run1.Socket 'push-before'
        $pushSettled=$pushBefore.state.objects.($push.id)
        [void](Move-PhysicsPublicPulses $run1.Socket ([double]$pushSettled.position.x) ([double]$pushSettled.position.z-1.5) 0.04 'push-south-flank')
        # Nominal walk radius 0.30 + cube half-extent 0.25, with the
        # unchanged 0.05 m contact cast: aim at the middle of [0.55,0.60].
        # Actual pose/rotation and the guarded Push acknowledgement decide contact.
        [void](Move-PhysicsPublicPulses $run1.Socket ([double]$pushSettled.position.x) ([double]$pushSettled.position.z-0.575) 0.025 'push-contact')
        Do-PhysicsAction $run1.Socket 'Pousser Bloc poussable de rive' 'push'
        [void](Move-PlaytestWASDSegment -Socket $run1.Socket -TargetX ([double]$pushSettled.position.x) -TargetZ ([double]$pushSettled.position.z-1.5) -Tolerance 0.15 -MaxSeconds 35 -Journal $journal -Phase 'push-clearance' -RouteId 'public-player-clearance-after-push')
        $pushAfter=Save-PhysicsCheckpoint $run1.Socket 'push-after'
        }else{$shoreArrival=Save-PhysicsCheckpoint $run1.Socket 'shore-arrival'}
        [void](Move-PlaytestWASDSegment -Socket $run1.Socket -TargetX 39.47 -TargetZ -32.38 -Tolerance 0.13 -MaxSeconds 40 -Journal $journal -Phase 'shore-water-contact' -RouteId 'public-measured-shore-water')
        $waterSamples=@(Wait-PhysicsFreshWaterSamples -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'water-before-ignite')
        Do-PhysicsAction $run1.Socket 'Allumer Feu de rive' 'shore-ignite'
        $burning=Save-PhysicsCheckpoint $run1.Socket 'shore-burning'
        $waterSamples=@($waterSamples+@(Wait-PhysicsFreshWaterSamples -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'water-before-extinguish'))
        Do-PhysicsAction $run1.Socket 'Éteindre Feu de rive' 'shore-extinguish'
        $after=Save-PhysicsCheckpoint $run1.Socket 'after'
    }else{
        if($cfg.Scenario -in @('EnvironmentQuest','EnvironmentFireQuest')){
            Move-PlaytestToXZ -Socket $run1.Socket -X 40 -Z -118 -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'quest-village'
            Do-PhysicsAction $run1.Socket "J’accepte." 'quest-accept'
        }
        Mark-PhysicsPhase 'forest-route'
        [void](Move-PlaytestRoute -Socket $run1.Socket -Waypoints $forestRoute -Tolerance 0.75 -MaxSeconds 420 -Journal $journal -Phase 'forest-approach' -RouteId 'east-forest-corridor')
        if($cfg.Scenario -in @('ForestDanger','EnvironmentFireQuest')){
            $northwestRoute=@([pscustomobject]@{x=25;z=-270},[pscustomobject]@{x=25;z=-265},[pscustomobject]@{x=17.05;z=-265},[pscustomobject]@{x=17.05;z=-267.6})
            [void](Move-PlaytestRoute -Socket $run1.Socket -Waypoints $northwestRoute -Tolerance 0.10 -MaxSeconds 120 -Journal $journal -Phase 'forest-fire-northwest-flank' -RouteId 'public-northwest-fire-flank-around-seal')
            [void](Move-PhysicsPublicPulses -Socket $run1.Socket -X 17.05 -Z -267.95 -Tolerance 0.04 -Phase 'forest-fire-seal-flank-precise')
            [void](Wait-PlaytestLocomotionSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'ignite-unlit-settle' -SampleMilliseconds 120)
            $launchBefore=Save-PhysicsCheckpoint $run1.Socket 'launch-before'
            Assert-PhysicsSavedFireRange $run1.Socket $launchBefore ([string]$fire.id) $false 'ignite-preflight'
            $actualPlayer=$launchBefore.state.actors.($player.id).position
            $actualFire=$launchBefore.state.objects.($fire.id).position
            $awayX=[double]$actualPlayer.x-[double]$actualFire.x;$awayZ=[double]$actualPlayer.z-[double]$actualFire.z
            $awayLength=[Math]::Sqrt($awayX*$awayX+$awayZ*$awayZ)
            if($awayLength -lt 0.1){throw 'No safe public radial heat retreat can be derived from this saved pose.'}
            $retreatX=[double]$actualFire.x+3.8*$awayX/$awayLength
            $retreatZ=[double]$actualFire.z+3.8*$awayZ/$awayLength
            Add-PlaytestJournal -Journal $journal -Phase 'ignite-preflight' -Event 'public_saved_pose_retreat_plan' -Payload ([ordered]@{player=$actualPlayer;fire=$actualFire;retreat_x=$retreatX;retreat_z=$retreatZ;retreat_xz_radius_m=3.8;target_tolerance_m=0.15})
            $fireExposurePlan=New-PhysicsExposureRetreatPlan -Socket $run1.Socket -FixtureRoot $fixture -PlayerId ([string]$player.id) -FireId ([string]$fire.id) -SourceHash ([string]$reference.head_hash) -TargetX $retreatX -TargetZ $retreatZ
            Add-PlaytestJournal -Journal $journal -Phase 'ignite-preflight' -Event 'public_exposure_save_and_retreat_prepared_unlit' -Payload $fireExposurePlan.prepared_observation
            # Pay the browser Escape cooldown before the hazard starts. The
            # Focus helper still verifies public pointer/input readiness after
            # Ignite and counts the full 1.5 s from a real captured release.
            Mark-PhysicsPhase 'capture-cooldown-before-fire'
            Start-Sleep -Milliseconds 1500
            Mark-PhysicsPhase 'ignite'
            try{$actions['ignite']=Invoke-PhysicsExposureIgnite -Socket $run1.Socket -Plan $fireExposurePlan -Text 'Allumer Balise inflammable' -Journal $journal}
            catch{[void]$fireExposurePlan.failures.Add("ignite: $($_.Exception.Message)")}
            # Dispatch the public exposure Save, retain all capture guards, and
            # send the prepared retreat key before awaiting its commit. The
            # distinct helper validates/copies the exposure snapshot AFTER
            # retreat, against its original public pre-click pose and tick.
            # No full journal serialization or second Save is allowed between.
            $exposureCapture=Invoke-PhysicsDeferredExposureRetreat -Socket $run1.Socket -Plan $fireExposurePlan -IgniteState $fireExposurePlan.ignite_ack_state -IgniteClickUtc ([string]$fireExposurePlan.ignite_click_utc) -EvidencePath $output -Journal $journal
            $saves['fire-exposure']=$exposureCapture
            $exposure=ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($exposureCapture.path))
            Mark-PhysicsPhase 'fire-exposure-copied-after-retreat'
            [void](Move-PlaytestWASDSegment -Socket $run1.Socket -TargetX 18 -TargetZ -265.8 -Tolerance 0.15 -MaxSeconds 25 -Journal $journal -Phase 'leave-heat' -RouteId 'public-north-heat-clearance')
            $after=Save-PhysicsCheckpoint $run1.Socket 'after'
            if($cfg.Scenario -ceq 'EnvironmentFireQuest'){
                $postDanger=Save-PhysicsCheckpoint $run1.Socket 'post-danger'
                $environmentBeforeCombat=Assert-PhysicsEnvironmentBeforeCombat -Kind heat -Before $launchBefore -Witness $exposure -Neutralized $postDanger -BeforeCapture $saves['launch-before'] -WitnessCapture $saves['fire-exposure'] -NeutralizedCapture $saves['post-danger'] -SourceHash ([string]$reference.head_hash) -PlayerId ([string]$player.id) -GuardId ([string]$guard.id) -ObjectIds @([string]$launch.id,[string]$load.id,[string]$counter.id) -FireId ([string]$fire.id) -WitnessPhase 'fire-exposure' -NeutralizedPhase 'post-danger'
                Finish-PhysicsPublicQuest $run1.Socket
                $after=Save-PhysicsCheckpoint $run1.Socket 'after-quest'
                $saves['after']=$saves['after-quest']
            }
        }else{
        $launchBefore=Save-PhysicsCheckpoint $run1.Socket 'launch-before'
        # Approach on the actual object's radial east axis. Prior V6 drift is
        # historical evidence, not an offset to inherit after a support fix.
        $launchPose=$launchBefore.state.objects.($launch.id).position
        try{[void](Move-PhysicsPublicPulses $run1.Socket ([double]$launchPose.x+0.575) ([double]$launchPose.z) 0.025 'throw-contact')}catch{Add-PlaytestJournal -Journal $journal -Phase 'throw-contact' -Event 'approach_bound' -Payload $_.Exception.Message}
        Do-PhysicsAction $run1.Socket 'Lancer Charge de lancer' 'launch'
        $launchAfter=Save-PhysicsCheckpoint $run1.Socket 'launch-after'
        if($cfg.Scenario -ceq 'ForestThrow'){
            $after=$launchAfter;$saves['after']=$saves['launch-after']
        }else{
        $environmentBeforeCombat=Assert-PhysicsEnvironmentBeforeCombat -Kind impact -Before $launchBefore -Witness $launchAfter -Neutralized $launchAfter -BeforeCapture $saves['launch-before'] -WitnessCapture $saves['launch-after'] -NeutralizedCapture $saves['launch-after'] -SourceHash ([string]$reference.head_hash) -PlayerId ([string]$player.id) -GuardId ([string]$guard.id) -ObjectIds @([string]$launch.id,[string]$load.id,[string]$counter.id) -FireId ([string]$fire.id) -WitnessPhase 'launch-after' -NeutralizedPhase 'launch-after'
        Finish-PhysicsPublicQuest $run1.Socket
        $after=Save-PhysicsCheckpoint $run1.Socket 'after'
        }
        }
    }
    Close-Gate3PlaytestZone -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds;$completed1=$true
}catch{$failure="run1: $($_.Exception.Message); exception_type=$($_.Exception.GetType().FullName); hresult=$($_.Exception.HResult); stack=$($_.ScriptStackTrace)";if($null -ne $run1){try{$s=Get-PlaytestPublicState -Socket $run1.Socket;[IO.File]::WriteAllText((Join-Path $output 'failure-public-state.json'),($s|ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))}catch{};if(Test-PhysicsExposureSavePending -Plan $fireExposurePlan){Add-PlaytestJournal -Journal $journal -Phase 'failure-state' -Event 'public_failure_save_skipped_to_preserve_pending_exposure' -Payload 'No subsequent Save may overwrite the exposure transaction before its evidence copy, including a click whose acknowledgement was lost.'}else{try{$failureSave=Save-PhysicsCheckpoint $run1.Socket 'failure-state'}catch{Add-PlaytestJournal -Journal $journal -Phase 'failure-state' -Event 'public_failure_save_unavailable' -Payload $_.Exception.Message}}}}
finally{if($null -ne $run1){$stop=Stop-ForgeStudioPlaytestProcess -Run $run1;$closed1=[bool]$stop.Closed;$forced=$forced -or [bool]$stop.Forced}}
if([string]::IsNullOrWhiteSpace($failure)){
    try{
        Mark-PhysicsPhase 'run2-new-process-resume'
        $run2=Start-ForgeStudioPlaytestProcess -CandidatePath $candidate -CandidateHash $candidateHash -FixtureRoot $fixture -EvidencePath $output -RunNumber 2 -StartupTimeout 180;$closed2=$false
        Open-Gate3PlaytestZone -Socket $run2.Socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds
        [void](Invoke-UiClick -Socket $run2.Socket -Text 'Tester & suivi' -Selector '[data-authoring-tab-button]')
        [void](Invoke-UiClick -Socket $run2.Socket -Text 'Reprendre la partie')
        [void](Wait-PlaytestCondition -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'new process resume acknowledgement' -Condition {param($s)$s.outputs -contains 'Reprise préparée.'})
        [void](Wait-PlaytestReady -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'new process resumed physics')
        Activate-PhysicsOwnedWindow -Run $run2
        Focus-PlaytestCanvas -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds
        [void](Wait-PlaytestSettled -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'resume-settle')
        $resume=Save-PhysicsCheckpoint $run2.Socket 'resume'
        Close-Gate3PlaytestZone -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds;$completed2=$true
    }catch{$failure="run2: $($_.Exception.Message); exception_type=$($_.Exception.GetType().FullName); hresult=$($_.Exception.HResult); stack=$($_.ScriptStackTrace)"}
    finally{if($null -ne $run2){$stop=Stop-ForgeStudioPlaytestProcess -Run $run2;$closed2=[bool]$stop.Closed;$forced=$forced -or [bool]$stop.Forced}}
}
foreach($n in $envBefore.Keys){$v=if($null -eq $envBefore[$n]){[NullString]::Value}else{$envBefore[$n]};[Environment]::SetEnvironmentVariable($n,$v,'Process')}
$sourcePreserved=$true
for($i=0;$i -lt $sourcePaths.Count;$i++){if(-not(Test-ArchiveSnapshotsEqual -Before $sourceBefore[$i] -After (Get-ArchiveSnapshot -LiteralPath $sourcePaths[$i]))){$sourcePreserved=$false}}
for($i=0;$i -lt $clonePaths.Count;$i++){if(-not(Test-ArchiveSnapshotsEqual -Before $cloneBefore[$i] -After (Get-ArchiveSnapshot -LiteralPath $clonePaths[$i]))){$sourcePreserved=$false}}
if(-not $sourcePreserved){$failure="Source changed; $failure"};if(-not $closed1 -or -not $closed2 -or $forced){$failure="Owned process closure failed; $failure"}
$summaries=[ordered]@{}
if($null -ne $fireExposurePlan -and $null -ne $fireExposurePlan.capture){$saves['fire-exposure']=$fireExposurePlan.capture}
foreach($name in $saves.Keys){$d=ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($saves[$name].path));$summaries[$name]=[ordered]@{source_hash=$d.source_hash;state_hash=$d.state_hash;player=$d.state.actors.($player.id);guardian=$d.state.actors.($guard.id);danger=$d.state.actors.($danger.id);launch=$d.state.objects.($launch.id);support=$d.state.objects.($support.id);load=$d.state.objects.($load.id);fire=$d.state.objects.($fire.id);shore_fire=$d.state.objects.($shore.id);push=$d.state.objects.($push.id);quests=$d.state.quests;receipts=$d.state.receipts;last_frame=$d.state.last_frame}}
$ledger=[ordered]@{schema='forge.tidewater.gate3-physics-environment-quest-smoke';version=1;status=if([string]::IsNullOrWhiteSpace($failure)){'observed_complete'}else{'failed_closed'};failure=$failure;scenario=$cfg.Scenario;gate_qualified=$false;human_visual_review=$false;navigation_claim=$false;candidate=$candidate;candidate_sha256=$candidateHash;authoring_ledger=$cfg.Ledger;authoring_ledger_sha256=$authoringLedgerHash;source_fixture=$source;source_original_hash_pinned=$sourcePin;source_preserved=$sourcePreserved;isolated_fixture=$fixture;run1_completed=$completed1;run2_resume_completed=$completed2;owned_processes_closed=($closed1 -and $closed2);forced_process_stop=$forced;actions=$actions;saves=$saves;state_summaries=$summaries;water_samples=$waterSamples;journal=@($journal);limitations=@('observed_complete confirms the public flow only; each R3-B capability still requires explicit causal review of events and persisted states.','Positive launch/push action alone does not prove an impact or AI reaction.','No Runtime state mutation, Tauri invocation or save injection was used.','NPC danger motion is a bounded local adapter; ADR0009 navigation provider remains unqualified.')}
$ledgerPath=Join-Path $output 'gate3-physics-environment-quest-ledger.json'
$ledger['fire_exposure_retreat']=$fireExposurePlan
$ledger['environment_before_combat']=$environmentBeforeCombat
$ledger['loaded_script_sources']=@($loadedScriptManifest|ForEach-Object{$_['source_sha256_at_run_end']=Get-FileSha256 -LiteralPath $_.source_path;$_})
$ledger['loaded_script_inputs_path']=Join-Path $output 'loaded-script-inputs.json'
$ledger['loaded_script_inputs_sha256']=Get-FileSha256 -LiteralPath $ledger.loaded_script_inputs_path
[void](Stop-Transcript)
$ledger['public_input_transcript_path']=$publicInputTranscript
$ledger['public_input_transcript_sha256']=Get-FileSha256 -LiteralPath $publicInputTranscript
[IO.File]::WriteAllText($ledgerPath,($ledger|ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))
if(-not [string]::IsNullOrWhiteSpace($failure)){throw "Public physics probe failed: $failure; ledger=$ledgerPath"}
Write-Output "GATE3_PUBLIC_PHYSICS_FLOW_OBSERVED ledger=$ledgerPath"
