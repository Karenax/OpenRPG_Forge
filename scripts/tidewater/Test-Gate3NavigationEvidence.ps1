#Requires -Version 7.2
# Contract fixtures only. These values are never submitted to Studio or engine.
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Gate3NavigationEvidence.ps1')
$hash='a'*64;$provider='b'*64;$actorId=[guid]'00111111-1111-4111-8111-111111111111'
function New-ReaderContractSample {
    param([long]$Tick,[string]$Status='no_path',[double]$X=48,[double]$Z=-145,[double]$TargetX=49)
    return [pscustomobject]@{schema='forge.navigation-runtime-observation';version=1;run=7;gameplay_source_hash=$hash;tick=$Tick;sequence=0;provider_source_hash=$provider;simulation_ready=$true;simulation_error=$null;feedback_phase='post_writeback';policy_kind='empty';
        profile=[pscustomobject]@{radius_m=0.4;height_m=1.8;speed_mps=1.6;step_m=0.3;snap_m=0.1;max_slope_radians=0.7};policy_source_hash='';policy_revision=0;policy_area_count=0;active_jobs=0;pending_jobs=0;completed_builds=1;rejected_stale_results=0;
        actors=@([pscustomobject]@{actor=$actorId.ToString('D');status=$Status;target=@($TargetX,6.65,-150.5);feet=@($X,6.65,$Z);actual_velocity=@(0,0,0);desired_velocity=@(0,0,0);requested_translation=@(0,-0.01,0);feedback_tick=$Tick;required_tiles=@([pscustomobject]@{x=1;z=-4});grounded=$true;collision_count=1});
        tiles=@([pscustomobject]@{key=[pscustomobject]@{x=1;z=-4};revision=1;token=1;source_hash=('c'*64);status='ready';source_triangles=12;polygons=4;retained_bytes=800;build_ms=2.4;error=$null})}
}
function Assert-ReaderRejects {
    param([string]$Name,[scriptblock]$Action)
    $rejected=$false;try{& $Action | Out-Null}catch{$rejected=$true}
    if(-not $rejected){throw "Reader accepted negative contract fixture $Name."}
    Write-Output "NAV_READER_NEGATIVE_PASS $Name"
}
$samples=@((New-ReaderContractSample 100),(New-ReaderContractSample 101),(New-ReaderContractSample 102))
[void](Get-Gate3NavigationStationaryReport -Observations $samples -ActorId $actorId -ExpectedStatus no_path -ExpectedGameplaySourceHash $hash)
Assert-ReaderRejects 'unchanged tick despite fresh DOM read' {Get-Gate3NavigationStationaryReport -Observations @($samples[0],$samples[0],$samples[2]) -ActorId $actorId -ExpectedStatus no_path -ExpectedGameplaySourceHash $hash}
Assert-ReaderRejects 'wrong authored head' {Assert-Gate3NavigationObservation $samples[0] ('d'*64) $null}
foreach($fault in @('not_ready','simulation_error','wrong_phase','missing_phase','wrong_policy_kind')){
    $bad=New-ReaderContractSample 102
    switch($fault){
        not_ready{$bad.simulation_ready=$false}
        simulation_error{$bad.simulation_error='test diagnostic refused'}
        wrong_phase{$bad.feedback_phase='pre_writeback'}
        missing_phase{$bad.PSObject.Properties.Remove('feedback_phase')}
        wrong_policy_kind{$bad.policy_kind='canonical'}
    }
    Assert-ReaderRejects $fault {Assert-Gate3NavigationObservation $bad $hash $samples[1]}
}
$bad=New-ReaderContractSample 102;$bad.actors[0].desired_velocity[0]=0.2
Assert-ReaderRejects 'NoPath horizontal fallback command' {Get-Gate3NavigationStationaryReport -Observations @($samples[0],$samples[1],$bad) -ActorId $actorId -ExpectedStatus no_path -ExpectedGameplaySourceHash $hash}
$bad=New-ReaderContractSample 102;$bad.pending_jobs=1
Assert-ReaderRejects 'unfinished navigation job' {Get-Gate3NavigationStationaryReport -Observations @($samples[0],$samples[1],$bad) -ActorId $actorId -ExpectedStatus no_path -ExpectedGameplaySourceHash $hash}
$bad=New-ReaderContractSample 102;$bad.tiles=@()
Assert-ReaderRejects 'missing actor required tile receipt' {Get-Gate3NavigationStationaryReport -Observations @($samples[0],$samples[1],$bad) -ActorId $actorId -ExpectedStatus no_path -ExpectedGameplaySourceHash $hash}
$bad=New-ReaderContractSample 102;$bad.profile.radius_m=0.5
Assert-ReaderRejects 'profile changed during the same run' {Assert-Gate3NavigationObservation $bad $hash $samples[1]}
$bad=New-ReaderContractSample 102;$bad.actors[0].feedback_tick=101
Assert-ReaderRejects 'old KCC pose attributed to current gameplay tick' {Assert-Gate3NavigationObservation $bad $hash $samples[1]}
$noRequest=New-ReaderContractSample 102;$noRequest.actors[0].requested_translation=$null;$noRequest.actors[0].feedback_tick=$null
[void](Assert-Gate3NavigationObservation $noRequest $hash $samples[1])
Assert-ReaderRejects 'explicit no-request state promoted to fresh physical support' {Get-Gate3NavigationStationaryReport -Observations @($samples[0],$samples[1],$noRequest) -ActorId $actorId -ExpectedStatus no_path -ExpectedGameplaySourceHash $hash}
$after=New-ReaderContractSample 120;$after.tiles[0].revision=2;$after.tiles[0].token=2;$after.tiles[0].source_hash='e'*64;$after.completed_builds=2
[void](Get-Gate3NavigationTileRevisionReport -Before $samples[0] -After $after -ExpectedGameplaySourceHash $hash)
Assert-ReaderRejects 'door action without changed tile receipt' {Get-Gate3NavigationTileRevisionReport -Before $samples[0] -After $samples[2] -ExpectedGameplaySourceHash $hash}
$moving=@((New-ReaderContractSample 200 moving 50 -117 50),(New-ReaderContractSample 201 moving 50 -120 50),(New-ReaderContractSample 202 moving 50 -122 51))
foreach($sample in $moving){$sample.actors[0].actual_velocity=@(0,0,-1.6);$sample.actors[0].desired_velocity=@(0,0,-1.6);$sample.actors[0].requested_translation=@(0,-0.01,-0.02)}
[void](Get-Gate3NavigationOutdoorMovementReport -Observations $moving -ActorId $actorId -ExpectedGameplaySourceHash $hash)
Assert-ReaderRejects 'targets change without physical movement' {Get-Gate3NavigationOutdoorMovementReport -Observations @((New-ReaderContractSample 200 moving 50 -117 50),(New-ReaderContractSample 201 moving 50 -117 50),(New-ReaderContractSample 202 moving 50 -117 51)) -ActorId $actorId -ExpectedGameplaySourceHash $hash}
$seamTiles=@([pscustomobject]@{x=1;z=-4},[pscustomobject]@{x=1;z=-5})
$crossing=@((New-ReaderContractSample 400 moving 50 -133.8),(New-ReaderContractSample 401 moving 50 -134.4),(New-ReaderContractSample 402 moving 50 -135.3))
foreach($sample in $crossing){
    $sample.actors[0].required_tiles=$seamTiles
    $sample.actors[0].actual_velocity=@(0,0,-1.6);$sample.actors[0].requested_translation=@(0,-0.01,-0.02)
    $sample.tiles+=([pscustomobject]@{key=[pscustomobject]@{x=1;z=-5};revision=1;token=2;source_hash=('f'*64);status='ready';source_triangles=14;polygons=5;retained_bytes=850;build_ms=2.5;error=$null})
}
$seamReport=Get-Gate3NavigationTileSeamReport -Observations $crossing -ActorId $actorId -BoundaryZ -134.4 -ExpectedTiles $seamTiles -ExpectedGameplaySourceHash $hash
if($seamReport.seam_witness.feedback_tick -ne 401 -or $seamReport.seam_witness.tile_receipts.Count -ne 2){throw 'Current seam support or both receipts were omitted.'}
Assert-ReaderRejects 'same tile loop is not a seam crossing' {Get-Gate3NavigationTileSeamReport -Observations $moving -ActorId $actorId -BoundaryZ -134.4 -ExpectedTiles $seamTiles -ExpectedGameplaySourceHash $hash}
foreach($fault in @('missing_seam_pose','missing_second_receipt','stale_seam_feedback','no_actual_seam_velocity','not_grounded_at_seam','tiles_not_required')){
    $badCrossing=@($crossing|ConvertTo-Json -Depth 20|ConvertFrom-Json -Depth 20)
    switch($fault){
        missing_seam_pose{$badCrossing=@($badCrossing[0],$badCrossing[2])}
        missing_second_receipt{$badCrossing[1].tiles=@($badCrossing[1].tiles[0])}
        stale_seam_feedback{$badCrossing[1].actors[0].feedback_tick=400}
        no_actual_seam_velocity{$badCrossing[1].actors[0].actual_velocity=@(0,0,0)}
        not_grounded_at_seam{$badCrossing[1].actors[0].grounded=$false}
        tiles_not_required{$badCrossing[1].actors[0].required_tiles=@($seamTiles[0])}
    }
    Assert-ReaderRejects $fault {Get-Gate3NavigationTileSeamReport -Observations $badCrossing -ActorId $actorId -BoundaryZ -134.4 -ExpectedTiles $seamTiles -ExpectedGameplaySourceHash $hash}
}
$room=[pscustomobject]@{origin_m=[pscustomobject]@{x=46;y=6.65;z=-145};yaw_degrees=0;dimensions=[pscustomobject]@{width_m=8;depth_m=6;wall_m=0.25;door_width_m=1.4}}
$door=[pscustomobject]@{position=[pscustomobject]@{x=46;y=7.75;z=-148}}
$exit=@((New-ReaderContractSample 300 moving 48 -145),(New-ReaderContractSample 301 moving 46 -147.9),(New-ReaderContractSample 302 reached 49 -150.5))
[void](Get-Gate3NavigationIndoorExitReport -Observations $exit -ActorId $actorId -Room $room -Door $door -ExpectedGameplaySourceHash $hash)
Assert-ReaderRejects 'outside coordinate without doorway KCC traversal' {Get-Gate3NavigationIndoorExitReport -Observations @($exit[0],$exit[2]) -ActorId $actorId -Room $room -Door $door -ExpectedGameplaySourceHash $hash}
Write-Output 'GATE3_NAVIGATION_EVIDENCE_SELFTEST_PASS synthetic reader contracts only; no runtime/navigation/terrain result'
