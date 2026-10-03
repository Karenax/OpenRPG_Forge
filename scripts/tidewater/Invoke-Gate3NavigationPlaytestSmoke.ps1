#Requires -Version 7.2
# Bounded public candidate replay; source authoring, fixture and executable must
# all match the common final ledger. No native/LAB or previous runtime inherits.
[CmdletBinding(DefaultParameterSetName='Run')]
param(
    [Parameter(ParameterSetName='Run',Mandatory)][string]$Candidate,
    [Parameter(ParameterSetName='Run',Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$CandidateSha256Pinned,
    [Parameter(ParameterSetName='Run',Mandatory)][string]$FreshIsolatedFixture,
    [Parameter(ParameterSetName='Run',Mandatory)][string]$AuthoringLedger,
    [Parameter(ParameterSetName='Run',Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$AuthoringLedgerSha256Pinned,
    [Parameter(ParameterSetName='Run',Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$SourceOriginalHashPinned,
    [Parameter(ParameterSetName='Run',Mandatory)][string]$EvidenceRoot,
    [Parameter(ParameterSetName='Run')][guid]$ZoneUuid='e620dd19-ead9-5913-a6d6-0f93109148c0',
    [Parameter(ParameterSetName='Run')][ValidateRange(30,180)][int]$InteractionTimeoutSeconds=120,
    [Parameter(ParameterSetName='Run')][ValidateRange(15,120)][int]$StartupTimeoutSeconds=60,
    [Parameter(ParameterSetName='Run')][ValidateRange(15,120)][int]$NavigationWindowSeconds=60,
    [Parameter(ParameterSetName='SelfTest',Mandatory)][switch]$SelfTest
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$navigationReplaySelfTest=[bool]$SelfTest
$navigationReplayRequested=@{}
foreach($name in @('Candidate','CandidateSha256Pinned','FreshIsolatedFixture','AuthoringLedger','AuthoringLedgerSha256Pinned','SourceOriginalHashPinned','EvidenceRoot','ZoneUuid','InteractionTimeoutSeconds','StartupTimeoutSeconds','NavigationWindowSeconds')){$navigationReplayRequested[$name]=(Get-Variable -Name $name -ValueOnly)}
$navigationReplayHash=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
$navigationReplayDependencies=@{}
foreach($relative in @('Invoke-Gate3GameplayPlaytestSmoke.ps1','Gate3ProofStorage.ps1','Gate3NavigationEvidence.ps1','..\Invoke-Gate2WorldEditingProof.ps1')){
    $dependency=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot $relative));$navigationReplayDependencies[$dependency]=(Get-FileHash -LiteralPath $dependency -Algorithm SHA256).Hash.ToLowerInvariant()
}
. (Join-Path $PSScriptRoot 'Invoke-Gate3GameplayPlaytestSmoke.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Gate3NavigationEvidence.ps1')
foreach($dependency in $navigationReplayDependencies.Keys){if((Get-FileHash -LiteralPath $dependency -Algorithm SHA256).Hash.ToLowerInvariant() -cne $navigationReplayDependencies[$dependency]){throw 'A navigation replay dependency changed while it was being loaded.'}}
if(-not $navigationReplaySelfTest){foreach($name in $navigationReplayRequested.Keys){Set-Variable -Name $name -Value $navigationReplayRequested[$name]}}

function Capture-Gate3NavigationSample {
    param([Parameter(Mandatory)]$Socket,[Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)][string]$HeadHash,[Parameter(Mandatory)][string]$OutputRoot,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Samples,
        [AllowNull()]$Previous)
    $public=Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $Socket)
    if(-not $public.ok -or -not $public.simulation_ready){throw 'Public gameplay readiness was lost during navigation capture.'}
    $capture=Get-Gate3PublicNavigationSnapshot -Socket $Socket
    $observation=$capture.observation
    $path=Join-Path $OutputRoot ('navigation-sample-{0:D4}.json' -f $Samples.Count)
    # Retain the public raw diagnostic even if it fails freshness or provenance.
    # Failure captures must not erase the very observation that rejected a run.
    try{
    # A duplicate DOM sample is retained but cannot satisfy a freshness check.
    if($null -ne $Previous -and [long]$observation.tick -eq [long]$Previous.tick){
        [void](Assert-Gate3NavigationObservation $observation $HeadHash $null)
        $fresh=$false
    }else{[void](Assert-Gate3NavigationObservation $observation $HeadHash $Previous);$fresh=$true}
    }catch{
        $capture | Add-Member -NotePropertyName validation_error -NotePropertyValue $_.Exception.Message
        [IO.File]::WriteAllText($path,($capture | ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))
        $Samples.Add([ordered]@{index=$Samples.Count;phase=$Phase;path=$path;sha256=Get-FileSha256 -LiteralPath $path;tick=Get-Gate3NavigationProperty $observation 'tick';fresh=$false;validation_error=$_.Exception.Message;observation=$observation})
        throw
    }
    $capture | Add-Member -NotePropertyName phase -NotePropertyValue $Phase
    $capture | Add-Member -NotePropertyName fresh -NotePropertyValue $fresh
    $capture | Add-Member -NotePropertyName player_public_observation -NotePropertyValue $public
    $capture | Add-Member -NotePropertyName index -NotePropertyValue $Samples.Count
    [IO.File]::WriteAllText($path,($capture | ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))
    $Samples.Add([ordered]@{index=$Samples.Count;phase=$Phase;path=$path;sha256=Get-FileSha256 -LiteralPath $path;tick=$observation.tick;sequence=$observation.sequence;fresh=$fresh;observation=$observation})
    if($fresh){return $observation}
    return $null
}

function Wait-Gate3NavigationEvidenceWindow {
    param([Parameter(Mandatory)]$Socket,[Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)][string]$HeadHash,[Parameter(Mandatory)][string]$OutputRoot,
        [Parameter(Mandatory)][guid]$IndoorActor,[Parameter(Mandatory)][guid]$OutdoorActor,
        [Parameter(Mandatory)][ValidateSet('closed_no_path','opened_exit')][string]$Goal,
        [AllowNull()]$Room,[AllowNull()]$Door,[AllowNull()]$Before,
        [Parameter(Mandatory)][int]$MaxSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Samples)
    $deadline=[DateTimeOffset]::UtcNow.AddSeconds($MaxSeconds)
    $fresh=[System.Collections.Generic.List[object]]::new();$previous=$Before;$lastFresh=[DateTimeOffset]::UtcNow
    $noPath=$null;$outdoor=$null;$seam=$null;$exit=$null;$reached=$null;$revision=$null;$pendingReason='first sample pending'
    while([DateTimeOffset]::UtcNow -lt $deadline){
        $observation=Capture-Gate3NavigationSample -Socket $Socket -Phase $Phase -HeadHash $HeadHash -OutputRoot $OutputRoot -Samples $Samples -Previous $previous
        if($null -eq $observation){
            if(([DateTimeOffset]::UtcNow-$lastFresh).TotalSeconds -gt 5){throw 'Public navigation tick froze for five seconds while gameplay claimed Ready.'}
            Start-Sleep -Milliseconds 250;continue
        }
        $fresh.Add($observation);$previous=$observation;$lastFresh=[DateTimeOffset]::UtcNow
        foreach($id in @($IndoorActor,$OutdoorActor)){
            $actor=Get-Gate3NavigationActor $observation $id
            if($actor.status -in @('outside_mesh','empty_source','invalid_geometry','budget_exceeded','disabled')){throw "Dedicated actor $id reported terminal navigation status $($actor.status)."}
        }
        if($Goal -ceq 'closed_no_path'){
            $noPath=$null
            try{$noPath=Get-Gate3NavigationStationaryReport -Observations @($fresh | Select-Object -Last 3) -ActorId $IndoorActor -ExpectedStatus no_path -ExpectedGameplaySourceHash $HeadHash}catch{$pendingReason=$_.Exception.Message}
            try{$outdoor=Get-Gate3NavigationOutdoorMovementReport -Observations @($fresh) -ActorId $OutdoorActor -ExpectedGameplaySourceHash $HeadHash}catch{$pendingReason=$_.Exception.Message}
            # These keys/boundary describe the reviewed authored Tidewater case,
            # not a runtime route or a new provider/profile contract.
            try{$seam=Get-Gate3NavigationTileSeamReport -Observations @($fresh) -ActorId $OutdoorActor -BoundaryZ -134.4 -ExpectedTiles @([pscustomobject]@{x=1;z=-4},[pscustomobject]@{x=1;z=-5}) -ExpectedGameplaySourceHash $HeadHash}catch{$pendingReason=$_.Exception.Message}
            if($null -ne $noPath -and $null -ne $outdoor -and $null -ne $seam){return [ordered]@{no_path=$noPath;outdoor=$outdoor;tile_seam=$seam;latest=$observation;samples=@($fresh)}}
        }else{
            $reached=$null
            try{$exit=Get-Gate3NavigationIndoorExitReport -Observations @($fresh) -ActorId $IndoorActor -Room $Room -Door $Door -ExpectedGameplaySourceHash $HeadHash}catch{$pendingReason=$_.Exception.Message}
            try{$reached=Get-Gate3NavigationStationaryReport -Observations @($fresh | Select-Object -Last 3) -ActorId $IndoorActor -ExpectedStatus reached -ExpectedGameplaySourceHash $HeadHash}catch{$pendingReason=$_.Exception.Message}
            try{$revision=Get-Gate3NavigationTileRevisionReport -Before $Before -After $observation -ExpectedGameplaySourceHash $HeadHash}catch{$pendingReason=$_.Exception.Message}
            if($null -ne $exit -and $null -ne $reached -and $null -ne $revision){return [ordered]@{exit=$exit;reached=$reached;door_revision=$revision;latest=$observation;samples=@($fresh)}}
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Public navigation window $Goal did not qualify within ${MaxSeconds}s: $pendingReason"
}

function Get-Gate3NavigationSavedDoorReceipts {
    param([Parameter(Mandatory)]$Save,[Parameter(Mandatory)][string]$HeadHash,[Parameter(Mandatory)][string]$AreaId,
        [Parameter(Mandatory)][string]$PlayerId,[Parameter(Mandatory)][string]$DoorId,
        [Parameter(Mandatory)][long]$OpenSequence,[Parameter(Mandatory)][long]$CloseSequence)
    if($Save.schema -cne 'forge.game-save' -or $Save.version -ne 1 -or [string]$Save.source_hash -cne $HeadHash -or [string]$Save.area_id -cne $AreaId -or $Save.state.requires_physics_revalidation){throw 'Committed navigation Save schema/source/area/revalidation differs.'}
    if([long]$Save.state.last_frame.frame.tick -ne [long]$Save.state.tick -or [string]$Save.state.last_frame.frame.source_hash -cne $HeadHash){throw 'Committed navigation Save has no coherent physics frame.'}
    $receipts=@($Save.state.receipts | Where-Object {$_.request.action.kind -ceq 'set_door' -and [string]$_.request.action.object -ceq $DoorId})
    if($receipts.Count -ne 2){throw 'Committed navigation Save does not contain exactly two door commands.'}
    if($OpenSequence -lt 1 -or $CloseSequence -le $OpenSequence -or @($receipts | ForEach-Object {[string]$_.request.command_id} | Select-Object -Unique).Count -ne 2){throw 'Committed door command sequences/identities are not distinct and ordered.'}
    foreach($expected in @([ordered]@{sequence=$OpenSequence;open=$true},[ordered]@{sequence=$CloseSequence;open=$false})){
        $matching=@($receipts | Where-Object {[long]$_.sequence -eq $expected.sequence -and [bool]$_.request.action.open -eq $expected.open -and [string]$_.request.actor -ceq $PlayerId -and [long]$_.sequence -eq [long]$_.request.expected_sequence+1 -and [long]$_.tick -le [long]$Save.state.tick -and @($_.events | Where-Object {$_.kind -ceq 'door_changed' -and [string]$_.object -ceq $DoorId -and [bool]$_.open -eq $expected.open}).Count -eq 1})
        if($matching.Count -ne 1){throw 'Public door acknowledgement does not match a unique committed event receipt.'}
    }
    if((Get-Gate3NavigationProperty $Save.state.objects $DoorId).open){throw 'Final door state is not closed in the committed Save.'}
    return [ordered]@{tick=$Save.state.tick;source_hash=$HeadHash;door=$DoorId;open_sequence=$OpenSequence;close_sequence=$CloseSequence;receipts=$receipts;physics_frame_coherent=$true;door_finally_closed=$true;state_hash_recomputed=$false}
}

function Invoke-Gate3NavigationPublicSave {
    param([Parameter(Mandatory)]$Socket,[Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal)
    $deadline=[DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds);$attempt=0
    while([DateTimeOffset]::UtcNow -lt $deadline){
        $remaining=[Math]::Max(1,[int][Math]::Ceiling(($deadline-[DateTimeOffset]::UtcNow).TotalSeconds))
        $before=Wait-PlaytestCondition -Socket $Socket -TimeoutSeconds ([Math]::Min(30,$remaining)) -Description 'navigation public Save button available' -Condition {param($s) @($s.allButtons | Where-Object {$_.text -ceq 'Sauvegarder la partie' -and -not $_.disabled}).Count -eq 1}
        $observed=Get-PlaytestObservation -State $before
        if(-not $observed.ok -or -not $observed.simulation_ready -or -not $observed.grounded){throw 'Navigation public Save lost its grounded ready player.'}
        $beforeTick=[long]$observed.tick;$attempt++
        [void](Invoke-UiClick -Socket $Socket -Text 'Sauvegarder la partie')
        $remaining=[Math]::Max(1,[int][Math]::Ceiling(($deadline-[DateTimeOffset]::UtcNow).TotalSeconds))
        $response=Wait-PlaytestCondition -Socket $Socket -TimeoutSeconds ([Math]::Min(30,$remaining)) -Description 'navigation completed public Save transaction' -Condition {
            param($s)
            $after=Get-PlaytestObservation -State $s
            $feedback=@($s.outputs | Where-Object {[string]$_ -notmatch '^Vitalité\s'})
            $after.ok -and $after.tick -gt $beforeTick -and @($s.allButtons | Where-Object {$_.text -ceq 'Sauvegarder la partie' -and -not $_.disabled}).Count -eq 1 -and $feedback.Count -gt 0
        }
        $accepted=@($response.outputs | Where-Object {$_ -ceq 'Partie sauvegardée.'}).Count -gt 0
        $settleRefusal=@($response.outputs | Where-Object {$_ -ceq 'action refused: wait for physical objects to settle before saving'}).Count -gt 0
        Add-PlaytestJournal -Journal $Journal -Phase 'navigation-save' -Event 'public_save_transaction' -Payload ([ordered]@{attempt=$attempt;before=$observed;after=Get-PlaytestObservation -State $response;outputs=$response.outputs;acknowledged=$accepted;exact_settle_refusal=$settleRefusal})
        if($accepted){return $response}
        if(-not $settleRefusal){throw "Navigation public Save returned an unexpected response: $($response.outputs -join ' | ')"}
        Start-Sleep -Milliseconds ([Math]::Min(2000,[int][Math]::Max(1,($deadline-[DateTimeOffset]::UtcNow).TotalMilliseconds)))
    }
    throw 'Public navigation Save never committed after bounded new transactions for the exact moving-object refusal.'
}

if($navigationReplaySelfTest){
    foreach($name in @('Capture-Gate3NavigationSample','Wait-Gate3NavigationEvidenceWindow','Get-Gate3NavigationSavedDoorReceipts','Start-ForgeStudioPlaytestProcess','Stop-ForgeStudioPlaytestProcess','Get-PlaytestInteriorScenario','Move-PlaytestWASDSegment')){[void](Get-Command $name -ErrorAction Stop)}
    & (Join-Path $PSScriptRoot 'Test-Gate3NavigationEvidence.ps1')
    $testDoor='door-id';$testPlayer='player-id';$testHead='f'*64
    $testSave=[pscustomobject]@{schema='forge.game-save';version=1;source_hash=$testHead;area_id='area-id';state=[pscustomobject]@{tick=120;requires_physics_revalidation=$false;last_frame=[pscustomobject]@{frame=[pscustomobject]@{tick=120;source_hash=$testHead}};objects=[pscustomobject]@{'door-id'=[pscustomobject]@{open=$false}};receipts=@(
        [pscustomobject]@{request=[pscustomobject]@{command_id='command-open';actor=$testPlayer;expected_sequence=0;action=[pscustomobject]@{kind='set_door';object=$testDoor;open=$true}};sequence=1;tick=100;events=@([pscustomobject]@{kind='door_changed';object=$testDoor;open=$true})},
        [pscustomobject]@{request=[pscustomobject]@{command_id='command-close';actor=$testPlayer;expected_sequence=1;action=[pscustomobject]@{kind='set_door';object=$testDoor;open=$false}};sequence=2;tick=110;events=@([pscustomobject]@{kind='door_changed';object=$testDoor;open=$false})})}}
    [void](Get-Gate3NavigationSavedDoorReceipts -Save $testSave -HeadHash $testHead -AreaId 'area-id' -PlayerId $testPlayer -DoorId $testDoor -OpenSequence 1 -CloseSequence 2)
    foreach($mutation in @('wrong_ack','duplicate_command','missing_event','old_frame','door_still_open')){
        $bad=$testSave | ConvertTo-Json -Depth 100 | ConvertFrom-Json
        switch($mutation){wrong_ack {$bad.state.receipts[0].sequence=3};duplicate_command {$bad.state.receipts[1].request.command_id='command-open'};missing_event {$bad.state.receipts[0].events=@()};old_frame {$bad.state.last_frame.frame.tick=119};door_still_open {$bad.state.objects.'door-id'.open=$true}}
        $rejected=$false;try{[void](Get-Gate3NavigationSavedDoorReceipts -Save $bad -HeadHash $testHead -AreaId 'area-id' -PlayerId $testPlayer -DoorId $testDoor -OpenSequence 1 -CloseSequence 2)}catch{$rejected=$true}
        if(-not $rejected){throw "Navigation door receipt contract accepted $mutation."};Write-Output "NAV_SAVE_RECEIPT_NEGATIVE_PASS $mutation"
    }
    Write-Output 'GATE3_NAVIGATION_PLAYTEST_SELFTEST_PASS no public replay executed'
    return
}

$candidate=[IO.Path]::GetFullPath($Candidate);$source=[IO.Path]::GetFullPath($FreshIsolatedFixture);$authoringPath=[IO.Path]::GetFullPath($AuthoringLedger);$output=[IO.Path]::GetFullPath($EvidenceRoot)
$zoneId=$ZoneUuid.ToString('D').ToLowerInvariant();$sourcePin=$SourceOriginalHashPinned
foreach($path in @($candidate,$source,$authoringPath,$output)){Assert-Gate3ProofPath -LiteralPath $path -Description 'Navigation replay proof path'}
if(Test-Path -LiteralPath $output){throw 'Navigation replay EvidenceRoot must be fresh.'}
if((Test-PathWithinDirectory -LiteralPath $output -Directory $source) -or (Test-PathWithinDirectory -LiteralPath $source -Directory $output) -or (Test-PathWithinDirectory -LiteralPath $output -Directory (Split-Path -Parent $candidate))){throw 'Navigation replay source/output/candidate paths overlap.'}
Assert-TreeHasNoReparsePoints -LiteralPath $source -Description 'Navigation replay source'
if((Get-FileSha256 -LiteralPath $candidate) -cne $CandidateSha256Pinned -or (Get-FileSha256 -LiteralPath $authoringPath) -cne $AuthoringLedgerSha256Pinned){throw 'Navigation replay candidate or final authoring ledger pin differs.'}
$authoring=ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($authoringPath))
if($authoring.schema -cne 'forge.tidewater.gate3-gameplay-authoring-smoke' -or $authoring.status -cne 'passed' -or -not $authoring.source_preserved -or $authoring.gate_qualified -or $authoring.runtime_results_inherited -or [string]$authoring.candidate_sha256 -cne $CandidateSha256Pinned -or [string]$authoring.source_original_hash_pinned -cne $sourcePin -or -not [IO.Path]::GetFullPath([string]$authoring.isolated_fixture).Equals($source,[StringComparison]::OrdinalIgnoreCase)){throw 'Navigation replay is not authorized by this candidate common final authoring ledger.'}
$outdoor=@($authoring.navigation_actors | Where-Object {$_.name -ceq 'Patrouilleur du village'})
$indoor=@($authoring.navigation_actors | Where-Object {$_.name -ceq 'Messager de la salle'})
if($outdoor.Count -ne 1 -or $indoor.Count -ne 1){throw 'Final authoring ledger has no exact dedicated actor pair.'}
$outdoorId=[guid]$outdoor[0].actor;$indoorId=[guid]$indoor[0].actor
$reference=Get-WorkspaceZoneReference -Archive (Join-Path $source 'workspace-v1\authoring.json') -ZoneId $zoneId
if([string]$reference.archive_hash -cne [string]$authoring.saved_reference.archive_hash -or [string]$reference.head_hash -cne [string]$authoring.saved_reference.head_hash){throw 'Final navigation workspace archive/head differ.'}
$archiveHash=[string]$reference.archive_hash;$headHash=[string]$reference.head_hash
$sourcePaths=@((Join-Path $source "$zoneId\authoring.json"),(Join-Path $source 'workspace-v1\authoring.json'),(Join-Path $source "workspace-v1\objects\$sourcePin.json"),(Join-Path $source "workspace-v1\objects\$archiveHash.json"))
$sourceBefore=@($sourcePaths | ForEach-Object {Get-ArchiveSnapshot -LiteralPath $_})
if($sourceBefore[0].Sha256 -cne $sourcePin -or $sourceBefore[2].Sha256 -cne $sourcePin -or $sourceBefore[3].Sha256 -cne $archiveHash){throw 'Navigation replay source archive pins differ.'}
$authored=ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($sourcePaths[3]))
$navInteriorScenario=Get-PlaytestInteriorScenario -Archive $sourcePaths[3]
$room=@($authored.head.corrections.buildings)[0]
$door=@($authored.head.corrections.gameplay.objects | Where-Object {[string]$_.id -ceq [string]$navInteriorScenario.door_id})[0]
$playerId=[string]$authored.head.corrections.gameplay.player
[void](New-Item -ItemType Directory -Path $output)
[IO.File]::Copy($PSCommandPath,(Join-Path $output 'navigation-playtest-harness-source.ps1'))
if((Get-FileHash -LiteralPath (Join-Path $output 'navigation-playtest-harness-source.ps1') -Algorithm SHA256).Hash.ToLowerInvariant() -cne $navigationReplayHash){throw 'Navigation replay harness changed since startup.'}
$dependencyRoot=Join-Path $output 'executed-helper-sources';[void](New-Item -ItemType Directory -Path $dependencyRoot)
foreach($dependency in $navigationReplayDependencies.Keys){$copied=Join-Path $dependencyRoot ([IO.Path]::GetFileName($dependency));[IO.File]::Copy($dependency,$copied);if((Get-FileHash -LiteralPath $copied -Algorithm SHA256).Hash.ToLowerInvariant() -cne $navigationReplayDependencies[$dependency]){throw 'Navigation replay dependency changed after load; source-copy provenance refused.'}}
$fixture=Join-Path $output 'navigation-playtest-root'
[void](Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $zoneId)
$clonePaths=@((Join-Path $fixture "$zoneId\authoring.json"),(Join-Path $fixture 'workspace-v1\authoring.json'),(Join-Path $fixture "workspace-v1\objects\$sourcePin.json"),(Join-Path $fixture "workspace-v1\objects\$archiveHash.json"))
$cloneBefore=@($clonePaths | ForEach-Object {Get-ArchiveSnapshot -LiteralPath $_})
$gameSaveHashesBefore=@{}
if(Test-Path -LiteralPath (Join-Path $fixture '.game-saves')){Get-ChildItem -LiteralPath (Join-Path $fixture '.game-saves') -Filter authoring.json -File -Recurse | ForEach-Object {$gameSaveHashesBefore[$_.FullName]=Get-FileSha256 -LiteralPath $_.FullName}}
$environmentBefore=@{};foreach($name in @('FORGE_AUTHORING_ROOT','FORGE_AUTHOR_SUBJECT','WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS','WEBVIEW2_USER_DATA_FOLDER')){$environmentBefore[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
$run=$null;$stop=$null;$failure=$null;$closedEvidence=$null;$openedEvidence=$null;$closedAgainRevision=$null;$saveReceiptReport=$null;$savedPath=$null;$savedHash=$null
$samples=[System.Collections.Generic.List[object]]::new();$journal=[System.Collections.Generic.List[object]]::new();$diagnostics=[System.Collections.Generic.List[object]]::new()
try{
    $run=Start-ForgeStudioPlaytestProcess -CandidatePath $candidate -CandidateHash $CandidateSha256Pinned -FixtureRoot $fixture -EvidencePath $output -RunNumber 1 -StartupTimeout $StartupTimeoutSeconds
    Open-Gate3PlaytestZone -Socket $run.Socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Invoke-UiClick -Socket $run.Socket -Text 'Tester & suivi' -Selector '[data-authoring-tab-button]')
    [void](Invoke-UiClick -Socket $run.Socket -Text 'Jouer' -Selector 'button.authoring-play')
    [void](Wait-PlaytestReady -Socket $run.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'public navigation candidate readiness')
    Focus-PlaytestCanvas -Socket $run.Socket -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Capture-PlaytestCheckpoint -Socket $run.Socket -EvidencePath $output -RunNumber 1 -Name 'navigation_ready' -Phase 'navigation' -Journal $journal -Diagnostics $diagnostics)
    $closedEvidence=Wait-Gate3NavigationEvidenceWindow -Socket $run.Socket -Phase 'closed_door_and_outdoor' -HeadHash $headHash -OutputRoot $output -IndoorActor $indoorId -OutdoorActor $outdoorId -Goal closed_no_path -MaxSeconds $NavigationWindowSeconds -Samples $samples
    foreach($point in @(@{x=$navInteriorScenario.west_clear_x;z=$navInteriorScenario.village_z;t=0.75},@{x=$navInteriorScenario.west_clear_x;z=$navInteriorScenario.south_clear_z;t=0.75},@{x=$navInteriorScenario.door_x;z=$navInteriorScenario.south_clear_z;t=0.75},@{x=$navInteriorScenario.door_x;z=$navInteriorScenario.exterior_z;t=0.35})){
        Focus-PlaytestCanvas -Socket $run.Socket -TimeoutSeconds $InteractionTimeoutSeconds
        Observe-PlaytestFacing -Socket $run.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'navigation-door-approach'
        [void](Move-PlaytestWASDSegment -Socket $run.Socket -TargetX $point.x -TargetZ $point.z -Tolerance $point.t -MaxSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'navigation-door-approach' -RouteId 'canonical-room-public-approach')
    }
    [void](Wait-PlaytestSettled -Socket $run.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'navigation-door-approach')
    $beforeOpen=Capture-Gate3NavigationSample -Socket $run.Socket -Phase 'immediately_before_open' -HeadHash $headHash -OutputRoot $output -Samples $samples -Previous $closedEvidence.latest
    if($null -eq $beforeOpen){throw 'No fresh navigation observation immediately before public door Open.'}
    $openAck=Invoke-PlaytestGameplayAction -Socket $run.Socket -Text 'Ouvrir Porte intérieure' -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'navigation-open'
    $openSequence=Get-PlaytestPublicActionSequence -Feedback ([string]$openAck.feedback)
    $openedEvidence=Wait-Gate3NavigationEvidenceWindow -Socket $run.Socket -Phase 'door_open_actor_exit' -HeadHash $headHash -OutputRoot $output -IndoorActor $indoorId -OutdoorActor $outdoorId -Goal opened_exit -Room $room -Door $door -Before $beforeOpen -MaxSeconds $NavigationWindowSeconds -Samples $samples
    [void](Capture-PlaytestCheckpoint -Socket $run.Socket -EvidencePath $output -RunNumber 1 -Name 'navigation_actor_reached' -Phase 'navigation' -Journal $journal -Diagnostics $diagnostics)
    $closeAck=Invoke-PlaytestGameplayAction -Socket $run.Socket -Text 'Fermer Porte intérieure' -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'navigation-close'
    $closeSequence=Get-PlaytestPublicActionSequence -Feedback ([string]$closeAck.feedback)
    $closeDeadline=[DateTimeOffset]::UtcNow.AddSeconds([Math]::Min(30,$NavigationWindowSeconds));$closePrevious=$openedEvidence.latest
    while([DateTimeOffset]::UtcNow -lt $closeDeadline){
        $closedSample=Capture-Gate3NavigationSample -Socket $run.Socket -Phase 'door_closed_again' -HeadHash $headHash -OutputRoot $output -Samples $samples -Previous $closePrevious
        if($null -ne $closedSample){
            $closePrevious=$closedSample
            try{$closedAgainRevision=Get-Gate3NavigationTileRevisionReport -Before $openedEvidence.latest -After $closedSample -ExpectedGameplaySourceHash $headHash}catch{Write-Verbose $_.Exception.Message}
            if($null -ne $closedAgainRevision){break}
        }
        Start-Sleep -Milliseconds 250
    }
    if($null -eq $closedAgainRevision){throw 'Final public Close did not produce a settled changed navigation tile receipt.'}
    Release-PlaytestCapture -Socket $run.Socket -TimeoutSeconds 10
    [void](Invoke-Gate3NavigationPublicSave -Socket $run.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal)
    $saves=@(Get-ChildItem -LiteralPath (Join-Path $fixture '.game-saves') -Filter authoring.json -File -Recurse | Where-Object {$_.Directory.Name -ceq $zoneId})
    if($saves.Count -ne 1){throw 'Navigation replay did not produce one owned zone Save.'}
    $savedPath=$saves[0].FullName;$savedHash=Get-FileSha256 -LiteralPath $savedPath
    if($gameSaveHashesBefore.ContainsKey($savedPath) -and [string]$gameSaveHashesBefore[$savedPath] -ceq $savedHash){throw 'Public navigation Save did not create or change the owned saved file.'}
    $save=ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($savedPath))
    $saveReceiptReport=Get-Gate3NavigationSavedDoorReceipts -Save $save -HeadHash $headHash -AreaId $zoneId -PlayerId $playerId -DoorId $navInteriorScenario.door_id -OpenSequence $openSequence -CloseSequence $closeSequence
    [void](Capture-PlaytestCheckpoint -Socket $run.Socket -EvidencePath $output -RunNumber 1 -Name 'navigation_saved_closed' -Phase 'navigation' -Journal $journal -Diagnostics $diagnostics)
    Close-Gate3PlaytestZone -Socket $run.Socket -TimeoutSeconds $InteractionTimeoutSeconds
}catch{
    $failure=$_.Exception.Message
    if($null -ne $run -and $run.Socket.State -eq [Net.WebSockets.WebSocketState]::Open){try{[void](Capture-PlaytestCheckpoint -Socket $run.Socket -EvidencePath $output -RunNumber 1 -Name 'navigation_failure' -Phase 'navigation-failure' -Journal $journal -Diagnostics $diagnostics)}catch{Write-Verbose $_.Exception.Message}}
}finally{
    $stop=Stop-ForgeStudioPlaytestProcess -Run $run
    foreach($name in $environmentBefore.Keys){[Environment]::SetEnvironmentVariable($name,$environmentBefore[$name],'Process')}
}
$sourcePreserved=$true;$clonePreserved=$true
for($i=0;$i -lt $sourcePaths.Count;$i++){if(-not(Test-ArchiveSnapshotsEqual -Before $sourceBefore[$i] -After (Get-ArchiveSnapshot -LiteralPath $sourcePaths[$i]))){$sourcePreserved=$false};if(-not(Test-ArchiveSnapshotsEqual -Before $cloneBefore[$i] -After (Get-ArchiveSnapshot -LiteralPath $clonePaths[$i]))){$clonePreserved=$false}}
if(-not $sourcePreserved -or -not $clonePreserved -or -not $stop.Closed -or $stop.Forced){$failure="Navigation replay source preservation/clean closure failed. $failure"}
$status=if([string]::IsNullOrWhiteSpace($failure)){'observed_complete'}else{'failed_closed'}
$ledger=[ordered]@{schema='forge.tidewater.gate3-navigation-public-playtest';version=1;status=$status;failure=$failure;candidate=$candidate;candidate_sha256=$CandidateSha256Pinned;authoring_ledger=$authoringPath;authoring_ledger_sha256=$AuthoringLedgerSha256Pinned;executed_harness_sha256=$navigationReplayHash;source_fixture=$source;source_original_hash_pinned=$sourcePin;checkpoint=$reference;isolated_fixture=$fixture;zone_uuid=$zoneId;source_preserved=$sourcePreserved;clone_authoring_preserved=$clonePreserved;closed_door_outdoor_evidence=$closedEvidence;opened_door_exit_evidence=$openedEvidence;closed_again_tile_revision=$closedAgainRevision;committed_save_door_receipts=$saveReceiptReport;committed_save_path=$savedPath;committed_save_sha256=$savedHash;samples=@($samples);journal=@($journal);ui_diagnostics=@($diagnostics);owned_processes_closed=$stop.Closed;forced_process_stop=$stop.Forced;gate_qualified=$false;human_visual_review=$false;native_results_inherited=$false;previous_runtime_results_inherited=$false;scope='bounded authored Tidewater outdoor patrol plus canonical room closed NoPath/open exit/revision/Reached only';limitations=@('No whole-map path or all NPC behavior qualification is inferred.','Provider/profile/policy receipts are recorded from public live candidate diagnostics; native tests remain separate.','Observed support, doorway traversal and tile receipts are required; authored coordinates alone cannot satisfy this replay.')}
$ledgerPath=Join-Path $output 'gate3-navigation-public-playtest-ledger.json'
$ledger['executed_helper_sha256']=$navigationReplayDependencies
[IO.File]::WriteAllText($ledgerPath,($ledger | ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))
if($status -cne 'observed_complete'){throw "Public navigation replay failed closed: $failure. Ledger: $ledgerPath"}
Write-Output "GATE3_NAVIGATION_PUBLIC_REPLAY_OBSERVED_COMPLETE ledger=$ledgerPath gate_qualified=false"
