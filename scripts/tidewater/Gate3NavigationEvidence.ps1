#Requires -Version 7.2
# Public DOM observation helpers. No application state, authored route or runtime
# command is supplied through this reader. Profile values come from the candidate.
Set-StrictMode -Version Latest

function Get-Gate3NavigationProperty {
    param([AllowNull()]$Value, [Parameter(Mandatory)][string]$Name)
    if ($null -eq $Value) { return $null }
    if ($Value -is [System.Collections.IDictionary]) { return $Value[$Name] }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -ne $property) { return $property.Value }
    return $null
}

function Assert-Gate3NavigationVector {
    param([Parameter(Mandatory)]$Value, [Parameter(Mandatory)][string]$Name)
    if (@($Value).Count -ne 3) { throw "$Name is not a three component vector." }
    foreach ($component in @($Value)) {
        if (-not [double]::IsFinite([double]$component)) { throw "$Name contains a nonfinite value." }
    }
}

function Assert-Gate3NavigationObservation {
    param([Parameter(Mandatory)]$Observation,
        [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedGameplaySourceHash,
        [AllowNull()]$Previous)
    if ($Observation.schema -cne 'forge.navigation-runtime-observation' -or [int]$Observation.version -ne 1) {
        throw 'Public navigation observation schema/version differs.'
    }
    if ([string]$Observation.gameplay_source_hash -cne $ExpectedGameplaySourceHash) { throw 'Navigation observation belongs to another authored source.' }
    foreach($field in @('simulation_ready','simulation_error','feedback_phase','policy_kind')){
        if($Observation.PSObject.Properties.Name -notcontains $field){throw "Public navigation diagnostic lacks $field."}
    }
    if($Observation.simulation_ready -ne $true -or $null -ne $Observation.simulation_error -or $Observation.feedback_phase -cne 'post_writeback'){
        throw 'Navigation diagnostic is not a ready, error-free post-Writeback observation.'
    }
    foreach ($field in @('run','sequence','tick','active_jobs','pending_jobs','completed_builds','rejected_stale_results','policy_revision','policy_area_count')) {
        $value = Get-Gate3NavigationProperty $Observation $field
        if ($null -eq $value -or [double]$value -lt 0 -or [double]$value -ne [Math]::Floor([double]$value)) { throw "Invalid public navigation counter $field." }
    }
    if ([string]$Observation.provider_source_hash -cnotmatch '^[0-9a-f]{64}$') { throw 'Provider source receipt is absent.' }
    # An empty policy source is the existing unconfigured policy, not a canonical
    # policy receipt. Preserve that distinction instead of inventing a hash.
    $policyHash = [string]$Observation.policy_source_hash
    if ($policyHash -ne '' -and $policyHash -cnotmatch '^[0-9a-f]{64}$') { throw 'Policy source receipt is malformed.' }
    if(($Observation.policy_kind -ceq 'empty' -and $policyHash -ne '') -or
        ($Observation.policy_kind -ceq 'canonical' -and $policyHash -cnotmatch '^[0-9a-f]{64}$') -or
        $Observation.policy_kind -cnotin @('empty','canonical')){throw 'Public navigation policy kind/source receipt differs.'}
    foreach ($field in @('radius_m','height_m','speed_mps','step_m','snap_m','max_slope_radians')) {
        $value = Get-Gate3NavigationProperty $Observation.profile $field
        if ($null -eq $value -or -not [double]::IsFinite([double]$value)) { throw "Profile field $field is absent/nonfinite." }
    }
    $statuses = @('waiting_for_source','queued','generating','ready','moving','reached','no_path','outside_mesh','empty_source','invalid_geometry','stale','budget_exceeded','disabled')
    $actorIds = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($actor in @($Observation.actors)) {
        $parsedId = [guid]::Empty
        if (-not [guid]::TryParse([string]$actor.actor,[ref]$parsedId) -or -not $actorIds.Add([string]$actor.actor)) { throw 'Actor identity is malformed/duplicated.' }
        if ([string]$actor.status -cnotin $statuses) { throw 'Actor navigation status is unknown.' }
        foreach ($field in @('feet','actual_velocity','desired_velocity')) { Assert-Gate3NavigationVector -Value $actor.$field -Name $field }
        if ($null -ne $actor.target) { Assert-Gate3NavigationVector -Value $actor.target -Name 'target' }
        if($actor.PSObject.Properties.Name -notcontains 'feedback_tick' -or $actor.PSObject.Properties.Name -notcontains 'requested_translation'){throw 'Actor diagnostic has no same-step KCC feedback/request fields.'}
        if($null -eq $actor.requested_translation){
            if($null -ne $actor.feedback_tick){throw 'Actor reports a feedback tick without a KCC request.'}
        }else{
            Assert-Gate3NavigationVector -Value $actor.requested_translation -Name 'requested_translation'
            if($null -eq $actor.feedback_tick -or [long]$actor.feedback_tick -ne [long]$Observation.tick){throw 'Actor physical feedback is not from the current gameplay simulation step.'}
        }
    }
    $tileKeys = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($tile in @($Observation.tiles)) {
        $key = "$($tile.key.x),$($tile.key.z)"
        if (-not $tileKeys.Add($key) -or [string]$tile.status -cnotin $statuses) { throw 'Tile identity/status is invalid.' }
        foreach ($field in @('revision','token','source_triangles','polygons','retained_bytes','build_ms')) {
            $value = Get-Gate3NavigationProperty $tile $field
            if ($null -eq $value -or -not [double]::IsFinite([double]$value) -or [double]$value -lt 0) { throw "Tile $key $field is invalid." }
        }
        if ($tile.status -ceq 'ready' -and ([string]$tile.source_hash -cnotmatch '^[0-9a-f]{64}$' -or [int]$tile.polygons -le 0 -or [int]$tile.source_triangles -le 0 -or $null -ne $tile.error)) { throw "Ready tile $key has no valid receipt/geometry." }
    }
    if ($null -ne $Previous) {
        if ([long]$Observation.run -ne [long]$Previous.run) { throw 'Navigation observation changed runtime run.' }
        if ([long]$Observation.tick -le [long]$Previous.tick) { throw 'Navigation sample is not a fresh simulation tick.' }
        if ([long]$Observation.sequence -lt [long]$Previous.sequence) { throw 'Navigation action sequence regressed.' }
        if ([string]$Observation.provider_source_hash -cne [string]$Previous.provider_source_hash -or
            (ConvertTo-Json -InputObject $Observation.profile -Compress) -cne (ConvertTo-Json -InputObject $Previous.profile -Compress)) { throw 'Navigation provider/profile changed within the run.' }
    }
    return $Observation
}

function Get-Gate3PublicNavigationSnapshot {
    param([Parameter(Mandatory)]$Socket)
    $capture = Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const nodes=[...document.querySelectorAll('pre[data-forge-navigation-observation="v1"]')];
  if(nodes.length!==1) throw new Error('Expected one public navigation diagnostic, got '+nodes.length);
  const raw=nodes[0].textContent??'';
  return {captured_at:new Date().toISOString(),diagnostic_selector:'pre[data-forge-navigation-observation="v1"]',
    document_focused:document.hasFocus(),visibility:document.visibilityState,raw,observation:JSON.parse(raw)};
})()
'@
    if ($null -eq $capture) { throw 'Public navigation DOM capture is absent.' }
    return $capture
}

function Get-Gate3NavigationActor {
    param([Parameter(Mandatory)]$Observation, [Parameter(Mandatory)][guid]$ActorId)
    $matches = @($Observation.actors | Where-Object { [string]$_.actor -ceq $ActorId.ToString('D') })
    if ($matches.Count -ne 1) { throw "Dedicated actor $ActorId is absent/duplicated in public navigation observation." }
    return $matches[0]
}

function Test-Gate3NavigationActorTilesSettled {
    param([Parameter(Mandatory)]$Observation, [Parameter(Mandatory)]$Actor)
    if ([long]$Observation.active_jobs -ne 0 -or [long]$Observation.pending_jobs -ne 0 -or @($Actor.required_tiles).Count -eq 0) { return $false }
    foreach ($key in @($Actor.required_tiles)) {
        $tiles = @($Observation.tiles | Where-Object { $_.key.x -eq $key.x -and $_.key.z -eq $key.z })
        if ($tiles.Count -ne 1 -or $tiles[0].status -cne 'ready') { return $false }
    }
    return $true
}

function Test-Gate3NavigationCurrentPhysicalFeedback {
    param([Parameter(Mandatory)]$Observation,[Parameter(Mandatory)]$Actor)
    return $null -ne $Actor.requested_translation -and $null -ne $Actor.feedback_tick -and [long]$Actor.feedback_tick -eq [long]$Observation.tick
}

function Get-Gate3NavigationStationaryReport {
    param([Parameter(Mandatory)][object[]]$Observations, [Parameter(Mandatory)][guid]$ActorId,
        [Parameter(Mandatory)][ValidateSet('no_path','reached')][string]$ExpectedStatus,
        [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedGameplaySourceHash,
        [ValidateRange(0.000001,0.1)][double]$ObservedFeetToleranceM=0.01)
    if ($Observations.Count -lt 3) { throw 'Stationarity requires at least three fresh public simulation samples.' }
    $previous = $null; $firstFeet = $null; $ticks = [System.Collections.Generic.List[long]]::new()
    foreach ($observation in $Observations) {
        [void](Assert-Gate3NavigationObservation $observation $ExpectedGameplaySourceHash $previous)
        $actor = Get-Gate3NavigationActor $observation $ActorId
        if ($actor.status -cne $ExpectedStatus -or -not $actor.grounded -or -not (Test-Gate3NavigationActorTilesSettled $observation $actor) -or -not (Test-Gate3NavigationCurrentPhysicalFeedback $observation $actor)) { throw 'Stationary actor status/same-step KCC support/tile receipts are not settled.' }
        # Measurement epsilon only; this does not change the product's actuator.
        foreach ($index in @(0,2)) {
            if ([Math]::Abs([double]$actor.desired_velocity[$index]) -gt 0.000001 -or [Math]::Abs([double]$actor.actual_velocity[$index]) -gt 0.000001 -or [Math]::Abs([double]$actor.requested_translation[$index]) -gt 0.000001) { throw 'Stationary actor still has a horizontal navigation command/request or actual velocity.' }
        }
        if ($null -eq $firstFeet) { $firstFeet = @($actor.feet) }
        $distance = [Math]::Sqrt([Math]::Pow([double]$actor.feet[0]-[double]$firstFeet[0],2)+[Math]::Pow([double]$actor.feet[2]-[double]$firstFeet[2],2))
        if ($distance -gt $ObservedFeetToleranceM) { throw 'Stationary actor moved beyond the declared observation tolerance.' }
        $ticks.Add([long]$observation.tick); $previous = $observation
    }
    return [ordered]@{actor=$ActorId.ToString('D');status=$ExpectedStatus;fresh_ticks=@($ticks);same_step_feedback=$true;feet=@($firstFeet);horizontal_command_zero=$true;horizontal_request_zero=$true;horizontal_actual_zero=$true;grounded=$true;required_tiles_ready=$true;observed_feet_tolerance_m=$ObservedFeetToleranceM}
}

function Get-Gate3NavigationTileRevisionReport {
    param([Parameter(Mandatory)]$Before, [Parameter(Mandatory)]$After,
        [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedGameplaySourceHash)
    [void](Assert-Gate3NavigationObservation $Before $ExpectedGameplaySourceHash $null)
    [void](Assert-Gate3NavigationObservation $After $ExpectedGameplaySourceHash $Before)
    if ([long]$After.active_jobs -ne 0 -or [long]$After.pending_jobs -ne 0) { throw 'Door revision observation still has pending navigation work.' }
    $changed = [System.Collections.Generic.List[object]]::new(); $unchanged = [System.Collections.Generic.List[object]]::new()
    foreach ($tile in @($After.tiles)) {
        $old = @($Before.tiles | Where-Object { $_.key.x -eq $tile.key.x -and $_.key.z -eq $tile.key.z })
        if ($old.Count -ne 1 -or $tile.status -cne 'ready' -or $old[0].status -cne 'ready') { continue }
        $receipt = [ordered]@{key=$tile.key;before=$old[0];after=$tile}
        if ([long]$tile.revision -gt [long]$old[0].revision -and [long]$tile.token -gt [long]$old[0].token -and [string]$tile.source_hash -cne [string]$old[0].source_hash) { $changed.Add($receipt) }
        elseif ([long]$tile.revision -eq [long]$old[0].revision -and [long]$tile.token -eq [long]$old[0].token -and [string]$tile.source_hash -ceq [string]$old[0].source_hash) { $unchanged.Add($receipt) }
    }
    if ($changed.Count -eq 0) { throw 'Public door action did not produce a settled changed tile receipt.' }
    return [ordered]@{before_tick=$Before.tick;after_tick=$After.tick;changed_tiles=@($changed);unchanged_tiles=@($unchanged);completed_builds_before=$Before.completed_builds;completed_builds_after=$After.completed_builds;door_action_receipt_required_separately=$true}
}

function Get-Gate3NavigationOutdoorMovementReport {
    param([Parameter(Mandatory)][object[]]$Observations,[Parameter(Mandatory)][guid]$ActorId,
        [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedGameplaySourceHash,
        [ValidateRange(0.01,100)][double]$MinimumObservedDisplacementM=0.5)
    $previous=$null; $first=$null; $maximum=0.0; $supported=0; $movingVelocity=$false; $targets=[System.Collections.Generic.HashSet[string]]::new(); $ticks=[System.Collections.Generic.List[long]]::new()
    foreach($observation in $Observations) {
        [void](Assert-Gate3NavigationObservation $observation $ExpectedGameplaySourceHash $previous)
        $actor=Get-Gate3NavigationActor $observation $ActorId
        if($actor.status -in @('moving','reached') -and $actor.grounded -and (Test-Gate3NavigationActorTilesSettled $observation $actor) -and (Test-Gate3NavigationCurrentPhysicalFeedback $observation $actor)) {
            if($null -eq $first){$first=@($actor.feet)}
            $maximum=[Math]::Max($maximum,[Math]::Sqrt([Math]::Pow([double]$actor.feet[0]-[double]$first[0],2)+[Math]::Pow([double]$actor.feet[2]-[double]$first[2],2)))
            if($null -ne $actor.target){[void]$targets.Add((ConvertTo-Json -InputObject $actor.target -Compress))}
            if([Math]::Abs([double]$actor.actual_velocity[0])+[Math]::Abs([double]$actor.actual_velocity[2]) -gt 0.000001){$movingVelocity=$true}
            $supported++; $ticks.Add([long]$observation.tick)
        }
        $previous=$observation
    }
    if($supported -lt 3 -or $maximum -lt $MinimumObservedDisplacementM -or $targets.Count -lt 2 -or -not $movingVelocity){throw 'Outdoor patrol has no fresh supported displacement/actual velocity and two observed targets.'}
    return [ordered]@{actor=$ActorId.ToString('D');fresh_supported_ticks=@($ticks);same_step_feedback=$true;actual_horizontal_velocity_observed=$movingVelocity;maximum_horizontal_displacement_m=$maximum;minimum_observed_displacement_m=$MinimumObservedDisplacementM;distinct_observed_targets=@($targets);required_tiles_ready=$true;terrain_route_extent_claim='bounded observed route only';native_results_inherited=$false}
}

function Get-Gate3NavigationTileSeamReport {
    param([Parameter(Mandatory)][object[]]$Observations,[Parameter(Mandatory)][guid]$ActorId,
        [Parameter(Mandatory)][double]$BoundaryZ,[Parameter(Mandatory)][object[]]$ExpectedTiles,
        [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedGameplaySourceHash)
    if(-not [double]::IsFinite($BoundaryZ) -or $ExpectedTiles.Count -ne 2 -or
        $ExpectedTiles[0].x -ne $ExpectedTiles[1].x -or [Math]::Abs($ExpectedTiles[0].z-$ExpectedTiles[1].z) -ne 1){throw 'Seam evidence needs one finite boundary and two adjacent provider tile keys.'}
    $previous=$null;$north=[System.Collections.Generic.List[object]]::new();$south=[System.Collections.Generic.List[object]]::new();$near=[System.Collections.Generic.List[object]]::new()
    foreach($observation in $Observations){
        [void](Assert-Gate3NavigationObservation $observation $ExpectedGameplaySourceHash $previous)
        $previous=$observation;$actor=Get-Gate3NavigationActor $observation $ActorId
        if($actor.status -cnotin @('moving','reached') -or -not $actor.grounded -or
            -not (Test-Gate3NavigationCurrentPhysicalFeedback $observation $actor) -or
            -not (Test-Gate3NavigationActorTilesSettled $observation $actor)){continue}
        $radius=[double]$observation.profile.radius_m
        if($radius -le 0){throw 'Observed NPC profile has no positive capsule radius.'}
        $witness=[ordered]@{tick=$observation.tick;feedback_tick=$actor.feedback_tick;feet=@($actor.feet);requested_translation=$actor.requested_translation;actual_velocity=$actor.actual_velocity;grounded=$actor.grounded;collision_count=$actor.collision_count;profile=$observation.profile}
        $z=[double]$actor.feet[2]
        if($z -gt $BoundaryZ+$radius){$north.Add($witness)}
        if($z -lt $BoundaryZ-$radius){$south.Add($witness)}
        if([Math]::Abs($z-$BoundaryZ) -le $radius+[double]$observation.profile.step_m -and
            [Math]::Abs([double]$actor.actual_velocity[2]) -gt 0.000001){
            $receipts=[System.Collections.Generic.List[object]]::new()
            foreach($key in $ExpectedTiles){
                $required=@($actor.required_tiles|Where-Object{$_.x -eq $key.x -and $_.z -eq $key.z})
                $ready=@($observation.tiles|Where-Object{$_.key.x -eq $key.x -and $_.key.z -eq $key.z -and $_.status -ceq 'ready'})
                if($required.Count -ne 1 -or $ready.Count -ne 1){break}
                $receipts.Add($ready[0])
            }
            if($receipts.Count -eq 2){$witness['tile_receipts']=@($receipts);$near.Add($witness)}
        }
    }
    foreach($crossing in $near){
        $n=@($north|Where-Object{[long]$_.tick -lt [long]$crossing.tick}|Select-Object -First 1)
        $s=@($south|Where-Object{[long]$_.tick -gt [long]$crossing.tick}|Select-Object -First 1)
        if($n.Count -eq 0 -or $s.Count -eq 0){
            $s=@($south|Where-Object{[long]$_.tick -lt [long]$crossing.tick}|Select-Object -First 1)
            $n=@($north|Where-Object{[long]$_.tick -gt [long]$crossing.tick}|Select-Object -First 1)
        }
        if($n.Count -eq 1 -and $s.Count -eq 1){return [ordered]@{actor=$ActorId.ToString('D');boundary_z=$BoundaryZ;expected_provider_tiles=$ExpectedTiles;north_witness=$n[0];seam_witness=$crossing;south_witness=$s[0];same_step_supported_crossing=$true;provider_source_hash=$Observations[0].provider_source_hash;gameplay_source_hash=$ExpectedGameplaySourceHash;scope='observed authored Tidewater tile raccord only';runtime_results_inherited=$false}}
    }
    throw 'Outdoor patrol has no ordered current KCC poses on both sides of the tile raccord, supported movement at the seam and both ready required tile receipts.'
}

function Get-Gate3NavigationIndoorExitReport {
    param([Parameter(Mandatory)][object[]]$Observations,[Parameter(Mandatory)][guid]$ActorId,
        [Parameter(Mandatory)]$Room,[Parameter(Mandatory)]$Door,
        [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedGameplaySourceHash)
    if([Math]::Abs([double]$Room.yaw_degrees) -gt 0.000001){throw 'Indoor exit reader currently requires the actual unrotated canonical room.'}
    $previous=$null;$inside=$false;$outside=$false;$doorwaySamples=[System.Collections.Generic.List[object]]::new()
    $front=[double]$Room.origin_m.z-[double]$Room.dimensions.depth_m/2.0
    $doorX=[double]$Door.position.x;$opening=[double]$Room.dimensions.door_width_m
    foreach($observation in $Observations){
        [void](Assert-Gate3NavigationObservation $observation $ExpectedGameplaySourceHash $previous)
        $actor=Get-Gate3NavigationActor $observation $ActorId
        $radius=[double]$observation.profile.radius_m
        if($actor.grounded -and $actor.status -in @('moving','reached') -and (Test-Gate3NavigationActorTilesSettled $observation $actor) -and (Test-Gate3NavigationCurrentPhysicalFeedback $observation $actor)){
            $x=[double]$actor.feet[0];$y=[double]$actor.feet[1];$z=[double]$actor.feet[2]
            if($z -gt $front+[double]$Room.dimensions.wall_m+$radius -and $z -lt [double]$Room.origin_m.z+[double]$Room.dimensions.depth_m/2.0-[double]$Room.dimensions.wall_m-$radius -and [Math]::Abs($x-[double]$Room.origin_m.x) -lt [double]$Room.dimensions.width_m/2.0-[double]$Room.dimensions.wall_m-$radius){$inside=$true}
            if($inside -and $z -lt $front-[double]$Room.dimensions.wall_m-$radius){$outside=$true}
            # The longitudinal envelope is the actual capsule radius plus wall
            # thickness; transverse clearance uses the observed candidate profile.
            if($inside -and [Math]::Abs($z-$front) -le $radius+[double]$Room.dimensions.wall_m -and [Math]::Abs($x-$doorX) -le $opening/2.0-$radius -and [Math]::Abs($y-[double]$Room.origin_m.y) -le [double]$observation.profile.step_m){
                $doorwaySamples.Add([ordered]@{tick=$observation.tick;feet=$actor.feet;grounded=$actor.grounded;collisions=$actor.collision_count;radius_m=$radius;required_tiles=$actor.required_tiles})
            }
        }
        $previous=$observation
    }
    if(-not $inside -or -not $outside -or $doorwaySamples.Count -eq 0){throw 'Indoor actor has no fresh grounded traversal from canonical interior through the actual doorway to exterior.'}
    return [ordered]@{actor=$ActorId.ToString('D');canonical_room=$Room;door=$Door;inside_then_outside=$true;same_step_feedback=$true;doorway_grounded_samples=@($doorwaySamples);support_height_tolerance_contract='candidate observed step_m';native_results_inherited=$false}
}
