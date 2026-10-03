#Requires -Version 7.2
# Extend a pinned checkpoint through public Studio controls. This does not run
# navigation or inherit any earlier runtime, native-test or human verdict.
[CmdletBinding(DefaultParameterSetName='Run')]
param(
    [Parameter(ParameterSetName='Run',Mandatory)][string]$Candidate,
    [Parameter(ParameterSetName='Run',Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$CandidateSha256Pinned,
    [Parameter(ParameterSetName='Run',Mandatory)][string]$BaseAuthoringFixture,
    [Parameter(ParameterSetName='Run',Mandatory)][string]$PreviousGameplayAuthoringLedger,
    [Parameter(ParameterSetName='Run',Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$PreviousLedgerSha256Pinned,
    [Parameter(ParameterSetName='Run',Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$SourceOriginalHashPinned,
    [Parameter(ParameterSetName='Run',Mandatory)][string]$EvidenceRoot,
    [Parameter(ParameterSetName='Run',Mandatory)][double]$OutdoorX,
    [Parameter(ParameterSetName='Run',Mandatory)][double]$OutdoorZ,
    [Parameter(ParameterSetName='Run',Mandatory)][string]$OutdoorPatrolPoints,
    [Parameter(ParameterSetName='Run',Mandatory)][double]$IndoorX,
    [Parameter(ParameterSetName='Run',Mandatory)][double]$IndoorZ,
    [Parameter(ParameterSetName='Run',Mandatory)][double]$IndoorCenterY,
    [Parameter(ParameterSetName='Run',Mandatory)][string]$IndoorPatrolPoints,
    [Parameter(ParameterSetName='Run')][guid]$ZoneUuid='e620dd19-ead9-5913-a6d6-0f93109148c0',
    [Parameter(ParameterSetName='Run')][ValidateRange(30,180)][int]$InteractionTimeoutSeconds=120,
    [Parameter(ParameterSetName='Run')][ValidateRange(30,120)][int]$StartupTimeoutSeconds=60,
    [Parameter(ParameterSetName='SelfTest',Mandatory)][switch]$SelfTest
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$navAuthoringSelfTest=[bool]$SelfTest
$navAuthoringRequested=@{}
foreach($name in @('Candidate','CandidateSha256Pinned','BaseAuthoringFixture','PreviousGameplayAuthoringLedger','PreviousLedgerSha256Pinned','SourceOriginalHashPinned','EvidenceRoot','OutdoorX','OutdoorZ','OutdoorPatrolPoints','IndoorX','IndoorZ','IndoorCenterY','IndoorPatrolPoints','ZoneUuid','InteractionTimeoutSeconds','StartupTimeoutSeconds')) {
    $navAuthoringRequested[$name]=(Get-Variable -Name $name -ValueOnly)
}
$navAuthoringExecutedHash=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
$navAuthoringDependencies=@{}
foreach($relative in @('Invoke-Gate3PhysicsAuthoringSmoke.ps1','Invoke-Gate3GameplayAuthoringSmoke.ps1','Gate3ProofStorage.ps1','Gate3NavigationEvidence.ps1','..\Invoke-Gate2WorldEditingProof.ps1')){
    $dependency=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot $relative))
    $navAuthoringDependencies[$dependency]=(Get-FileHash -LiteralPath $dependency -Algorithm SHA256).Hash.ToLowerInvariant()
}
. (Join-Path $PSScriptRoot 'Invoke-Gate3PhysicsAuthoringSmoke.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Gate3NavigationEvidence.ps1')
foreach($dependency in $navAuthoringDependencies.Keys){if((Get-FileHash -LiteralPath $dependency -Algorithm SHA256).Hash.ToLowerInvariant() -cne $navAuthoringDependencies[$dependency]){throw 'A navigation authoring dependency changed while it was being loaded.'}}
if(-not $navAuthoringSelfTest){foreach($name in $navAuthoringRequested.Keys){Set-Variable -Name $name -Value $navAuthoringRequested[$name]}}
$navOutdoorName='Patrouilleur du village'
$navIndoorName='Messager de la salle'

function Assert-Gate3PatrolUiText {
    param([Parameter(Mandatory)][string]$Text,[Parameter(Mandatory)][int]$ExpectedCount)
    $lines=@($Text -split '\r?\n' | Where-Object { $_.Trim().Length -gt 0 })
    if($lines.Count -ne $ExpectedCount){throw "Patrol UI text requires exactly $ExpectedCount points."}
    foreach($line in $lines){
        $parts=@($line.Trim() -split '\s+')
        if($parts.Count -notin @(2,3)){throw 'Patrol UI line must be X Z [center Y].'}
        foreach($part in $parts){
            $number=0.0
            if(-not [double]::TryParse($part,[Globalization.NumberStyles]::Float,[Globalization.CultureInfo]::InvariantCulture,[ref]$number) -or -not [double]::IsFinite($number)){throw 'Patrol UI coordinate is not finite invariant numeric text.'}
        }
    }
}

function Set-Gate3PublicPatrolText {
    param([Parameter(Mandatory)]$Socket,[Parameter(Mandatory)][string]$Text)
    [void](Open-DetailsBySummary -Socket $Socket -Summary 'Patrouille du personnage sélectionné')
    $prepared=Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const nodes=[...document.querySelectorAll('textarea[aria-label="Points de patrouille"]')];
  if(nodes.length!==1) throw new Error('Expected one public patrol textarea, got '+nodes.length);
  const node=nodes[0]; if(node.disabled||node.closest('fieldset:disabled')) throw new Error('Public patrol textarea disabled');
  node.scrollIntoView({block:'center',inline:'center'});node.focus();
  return {focused:document.activeElement===node,before:node.value};
})()
'@
    if(-not $prepared.focused){throw 'Public patrol textarea did not acquire focus.'}
    Send-CdpKeyStroke -Socket $Socket -Key 'a' -Code 'KeyA' -VirtualKeyCode 65 -Modifiers 2
    [void](Invoke-CdpCommand -Socket $Socket -Method 'Input.insertText' -Parameters @{text=$Text})
    Send-CdpKeyStroke -Socket $Socket -Key 'Tab' -Code 'Tab' -VirtualKeyCode 9
    $literal=ConvertTo-CompactJson -Value $Text
    [void](Wait-CdpReady -Socket $Socket -TimeoutSeconds 10 -Description 'public patrol textarea retained keyboard input' -Expression @"
(() => {const nodes=[...document.querySelectorAll('textarea[aria-label="Points de patrouille"]')];const n=nodes[0];return {ready:nodes.length===1&&n.value===$literal&&document.activeElement!==n,terminal:nodes.length!==1,reason:'public patrol input verification'};})()
"@)
}

function Add-Gate3DedicatedPatrolActor {
    param([Parameter(Mandatory)]$Socket,[Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][double]$X,[Parameter(Mandatory)][double]$Z,
        [AllowEmptyString()][string]$CenterY='',
        [Parameter(Mandatory)][string]$Points,[Parameter(Mandatory)][int]$TimeoutSeconds)
    $culture=[Globalization.CultureInfo]::InvariantCulture
    [void](Wait-GameplayForm -Socket $Socket -TimeoutSeconds $TimeoutSeconds)
    Set-GameplayForm -Socket $Socket -Kind 'friendly' -Name $Name -X $X.ToString('R',$culture) -Z $Z.ToString('R',$culture) -CenterY $CenterY -Reply 'Je suis le trajet préparé dans Studio.' -TimeoutSeconds $TimeoutSeconds
    $add=Invoke-GameplayPreviewApply -Socket $Socket -PreviewButton "Examiner l’ajout" -Description "add dedicated $Name" -TimeoutSeconds $TimeoutSeconds
    Select-GameplayOptionByText -Socket $Socket -Label 'Élément à retoucher' -Text $Name -TimeoutSeconds $TimeoutSeconds
    $publicId=Get-GameplayOptionValue -Socket $Socket -Label 'Élément à retoucher' -Text $Name -TimeoutSeconds $TimeoutSeconds
    Set-Gate3PublicPatrolText -Socket $Socket -Text $Points
    $patrol=Invoke-GameplayPreviewApply -Socket $Socket -PreviewButton 'Examiner la patrouille' -Description "author dedicated patrol $Name" -TimeoutSeconds $TimeoutSeconds
    $flee=Set-PhysicsActorFleeFlag -Socket $Socket -Name $Name -FleesDanger $false -TimeoutSeconds $TimeoutSeconds
    return [ordered]@{action='add_dedicated_public_patrol_actor';name=$Name;actor=$publicId;spawn_x=$X;spawn_z=$Z;spawn_center_y=$CenterY;patrol_ui_text=$Points;add=$add;patrol=$patrol;flee=$flee;runtime_claim='none'}
}

function Assert-Gate3NavigationExtensionPreserved {
    param([Parameter(Mandatory)]$Before,[Parameter(Mandatory)]$After,[Parameter(Mandatory)][object[]]$AddedActors)
    Assert-PhysicsParentGameplayPreserved -Before $Before.head.corrections.gameplay -After $After.head.corrections.gameplay -GuardianFleeRetouched $false
    foreach($field in $Before.head.corrections.PSObject.Properties){
        if($field.Name -ceq 'gameplay'){continue}
        if((ConvertTo-Json -InputObject $field.Value -Depth 100 -Compress) -cne (ConvertTo-Json -InputObject $After.head.corrections.PSObject.Properties[$field.Name].Value -Depth 100 -Compress)){throw "Navigation authoring changed non-gameplay correction $($field.Name)."}
    }
    $oldGameplay=$Before.head.corrections.gameplay;$newGameplay=$After.head.corrections.gameplay
    foreach($field in @('objects','places','items','quests')){
        if(@($newGameplay.$field).Count -ne @($oldGameplay.$field).Count){throw "Navigation extension added unintended $field entries."}
    }
    if(@($AddedActors).Count -ne 2 -or @($newGameplay.actors).Count -ne @($oldGameplay.actors).Count+2 -or @($newGameplay.dialogues).Count -ne @($oldGameplay.dialogues).Count+2){throw 'Dedicated actors/dialogues count differs from the public two-actor extension.'}
    foreach($added in $AddedActors){
        $actor=@($newGameplay.actors | Where-Object { [string]$_.id -ceq [string]$added.actor -and [string]$_.name -ceq [string]$added.name })
        if($actor.Count -ne 1 -or $actor[0].role -cne 'friendly' -or $actor[0].flees_danger -ne $false){throw 'Dedicated patrol actor identity/role/flee flag differs from public authoring.'}
        if([Math]::Abs([double]$actor[0].position.x-[double]$added.spawn_x) -gt 0.0001 -or [Math]::Abs([double]$actor[0].position.z-[double]$added.spawn_z) -gt 0.0001 -or -not [double]::IsFinite([double]$actor[0].position.y)){throw 'Dedicated actor spawn differs from public X/Z or has a nonfinite height.'}
        if([string]$added.spawn_center_y -ne '' -and [Math]::Abs([double]$actor[0].position.y-[double]::Parse([string]$added.spawn_center_y,[Globalization.CultureInfo]::InvariantCulture)) -gt 0.0001){throw 'Dedicated indoor spawn differs from explicit public center height.'}
        $lines=@($added.patrol_ui_text -split '\r?\n' | Where-Object { $_.Trim().Length -gt 0 })
        if(@($actor[0].patrol).Count -ne $lines.Count){throw 'Saved patrol point count differs from public textarea.'}
        for($i=0;$i -lt $lines.Count;$i++){
            $parts=@($lines[$i].Trim() -split '\s+')
            $point=$actor[0].patrol[$i]
            if([Math]::Abs([double]$point.x-[double]::Parse($parts[0],[Globalization.CultureInfo]::InvariantCulture)) -gt 0.0001 -or [Math]::Abs([double]$point.z-[double]::Parse($parts[1],[Globalization.CultureInfo]::InvariantCulture)) -gt 0.0001 -or -not [double]::IsFinite([double]$point.y)){throw 'Saved patrol point differs from public X/Z or has nonfinite canonical height.'}
            if($parts.Count -eq 3 -and [Math]::Abs([double]$point.y-[double]::Parse($parts[2],[Globalization.CultureInfo]::InvariantCulture)) -gt 0.0001){throw 'Saved explicit patrol height differs from public textarea.'}
        }
    }
    if(@($After.receipts).Count -lt @($Before.receipts).Count+6){throw 'Public actor/patrol/statistics commands did not add six receipts.'}
    for($i=0;$i -lt @($Before.receipts).Count;$i++){
        if((ConvertTo-Json -InputObject $Before.receipts[$i] -Depth 100 -Compress) -cne (ConvertTo-Json -InputObject $After.receipts[$i] -Depth 100 -Compress)){throw 'Navigation extension altered the parent command receipt prefix.'}
    }
}

if($navAuthoringSelfTest){
    foreach($name in @('Set-Gate3PublicPatrolText','Add-Gate3DedicatedPatrolActor','Assert-Gate3NavigationExtensionPreserved','Copy-IsolatedFixture','Get-PhysicsLastReceiptHash','Set-PhysicsActorFleeFlag')){[void](Get-Command $name -ErrorAction Stop)}
    Assert-Gate3PatrolUiText -Text "50 -117`n50 -135.5" -ExpectedCount 2
    Assert-Gate3PatrolUiText -Text '49 -150.5' -ExpectedCount 1
    $rejected=$false;try{Assert-Gate3PatrolUiText -Text 'NaN -150.5' -ExpectedCount 1}catch{$rejected=$true};if(-not $rejected){throw 'Nonfinite patrol coordinate accepted.'}
    # Synthetic preservation contracts only, never submitted as a zone/gameplay
    # source. Exercise the safeguards that protect the common final checkpoint.
    $beforeContract=[pscustomobject]@{head=[pscustomobject]@{corrections=[pscustomobject]@{buildings=@([pscustomobject]@{id='room';origin_y=6.65});placements=@([pscustomobject]@{id='placement';asset_hash='pinned'});gameplay=[pscustomobject]@{actors=@([pscustomobject]@{id='parent-guard';name='Gardien';flees_danger=$false});objects=@([pscustomobject]@{id='parent-door';name='Door';open=$false});places=@();items=@();quests=@();dialogues=@();player='parent-player'}}};receipts=@([pscustomobject]@{command_id='parent-command'})}
    $afterContract=$beforeContract | ConvertTo-Json -Depth 100 | ConvertFrom-Json
    $addedContract=@([pscustomobject]@{actor='outdoor';name='Outdoor';spawn_x=50;spawn_z=-117;spawn_center_y='';patrol_ui_text="50 -117`n50 -135.5"},[pscustomobject]@{actor='indoor';name='Indoor';spawn_x=48;spawn_z=-145;spawn_center_y='7.62';patrol_ui_text='49 -150.5'})
    $afterContract.head.corrections.gameplay.actors+=@([pscustomobject]@{id='outdoor';name='Outdoor';role='friendly';flees_danger=$false;position=[pscustomobject]@{x=50;y=5;z=-117};patrol=@([pscustomobject]@{x=50;y=5;z=-117},[pscustomobject]@{x=50;y=5.2;z=-135.5})},[pscustomobject]@{id='indoor';name='Indoor';role='friendly';flees_danger=$false;position=[pscustomobject]@{x=48;y=7.62;z=-145};patrol=@([pscustomobject]@{x=49;y=8;z=-150.5})})
    $afterContract.head.corrections.gameplay.dialogues=@([pscustomobject]@{id='outdoor-dialogue'},[pscustomobject]@{id='indoor-dialogue'})
    1..6 | ForEach-Object {$afterContract.receipts+=([pscustomobject]@{command_id="new-public-command-$_"})}
    Assert-Gate3NavigationExtensionPreserved -Before $beforeContract -After $afterContract -AddedActors $addedContract
    foreach($mutation in @('guard_flag','room_floor','parent_receipt','extra_object','patrol_coordinate','spawn_height')){
        $bad=$afterContract | ConvertTo-Json -Depth 100 | ConvertFrom-Json
        switch($mutation){
            guard_flag {$bad.head.corrections.gameplay.actors[0].flees_danger=$true}
            room_floor {$bad.head.corrections.buildings[0].origin_y=7}
            parent_receipt {$bad.receipts[0].command_id='changed'}
            extra_object {$bad.head.corrections.gameplay.objects+=([pscustomobject]@{id='unintended';name='Unexpected';open=$false})}
            patrol_coordinate {$bad.head.corrections.gameplay.actors[1].patrol[1].x=51}
            spawn_height {$bad.head.corrections.gameplay.actors[2].position.y=8}
        }
        $rejected=$false;try{Assert-Gate3NavigationExtensionPreserved -Before $beforeContract -After $bad -AddedActors $addedContract}catch{$rejected=$true}
        if(-not $rejected){throw "Navigation preservation guard accepted $mutation."}
        Write-Output "NAV_AUTHORING_PRESERVATION_NEGATIVE_PASS $mutation"
    }
    Write-Output 'GATE3_NAVIGATION_AUTHORING_SELFTEST_PASS public helper/parser contract only; no authoring/runtime result'
    return
}

Assert-Gate3PatrolUiText -Text $OutdoorPatrolPoints -ExpectedCount 2
Assert-Gate3PatrolUiText -Text $IndoorPatrolPoints -ExpectedCount 1
foreach($value in @($OutdoorX,$OutdoorZ,$IndoorX,$IndoorZ,$IndoorCenterY)){if(-not [double]::IsFinite($value)){throw 'Actor spawn coordinate is nonfinite.'}}
$candidate=[IO.Path]::GetFullPath($Candidate);$source=[IO.Path]::GetFullPath($BaseAuthoringFixture)
$ledgerInput=[IO.Path]::GetFullPath($PreviousGameplayAuthoringLedger);$output=[IO.Path]::GetFullPath($EvidenceRoot)
$zoneId=$ZoneUuid.ToString('D').ToLowerInvariant();$sourcePin=$SourceOriginalHashPinned
foreach($path in @($candidate,$source,$ledgerInput,$output)){Assert-Gate3ProofPath -LiteralPath $path -Description 'Navigation proof path'}
if(Test-Path -LiteralPath $output){throw 'Navigation EvidenceRoot must be fresh.'}
if((Test-PathWithinDirectory -LiteralPath $output -Directory $source) -or (Test-PathWithinDirectory -LiteralPath $source -Directory $output) -or (Test-PathWithinDirectory -LiteralPath $output -Directory (Split-Path -Parent $candidate))){throw 'Navigation source/output/candidate paths overlap.'}
Assert-TreeHasNoReparsePoints -LiteralPath $source -Description 'Navigation base fixture'
if((Get-FileSha256 -LiteralPath $candidate) -cne $CandidateSha256Pinned){throw 'Candidate differs from explicitly pinned executable.'}
if((Get-FileSha256 -LiteralPath $ledgerInput) -cne $PreviousLedgerSha256Pinned){throw 'Parent authoring ledger differs from explicit pin.'}
$candidateKitAndImports=Assert-Gate3CandidateInputs -CandidateExecutable $candidate
$previous=ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($ledgerInput))
if($previous.schema -cne 'forge.tidewater.gate3-gameplay-authoring-smoke' -or $previous.status -cne 'passed' -or $previous.gate_qualified -or -not $previous.source_preserved){throw 'Parent is not a passed source-preserving authoring checkpoint.'}
if([string]$previous.source_original_hash_pinned -cne $sourcePin){throw 'Parent original source pin differs.'}
$matchesFixture=$false
foreach($field in @('isolated_fixture','source_fixture')){$path=[string](Get-PhysicsProperty $previous $field);if($path -ne '' -and [IO.Path]::GetFullPath($path).Equals($source,[StringComparison]::OrdinalIgnoreCase)){$matchesFixture=$true}}
if(-not $matchesFixture){throw 'Base fixture is not recorded in the parent ledger.'}
$sourceWorkspace=Join-Path $source 'workspace-v1\authoring.json'
$sourceReference=Get-Gate3WorkspaceReference -Archive $sourceWorkspace -ZoneId $zoneId
if([string]$sourceReference.archive_hash -cne [string]$previous.saved_reference.archive_hash -or [string]$sourceReference.head_hash -cne [string]$previous.saved_reference.head_hash){throw 'Base archive/head differ from parent ledger.'}
$currentHash=[string]$sourceReference.archive_hash
$sourcePaths=@((Join-Path $source "$zoneId\authoring.json"),$sourceWorkspace,(Join-Path $source "workspace-v1\objects\$sourcePin.json"),(Join-Path $source "workspace-v1\objects\$currentHash.json"))
$sourceBefore=@($sourcePaths | ForEach-Object {Get-ArchiveSnapshot -LiteralPath $_})
if($sourceBefore[0].Sha256 -cne $sourcePin -or $sourceBefore[2].Sha256 -cne $sourcePin -or $sourceBefore[3].Sha256 -cne $currentHash){throw 'Pinned original/current objects differ.'}
$before=ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($sourcePaths[3]))
if((Get-PhysicsLastReceiptHash $before) -cne [string]$sourceReference.head_hash){throw 'Parent head receipt differs.'}
foreach($name in @($navOutdoorName,$navIndoorName)){if(@($before.head.corrections.gameplay.actors | Where-Object {$_.name -ceq $name}).Count -gt 0){throw "Dedicated actor already present: $name"}}
[void](New-Item -ItemType Directory -Path $output)
[IO.File]::Copy($PSCommandPath,(Join-Path $output 'navigation-authoring-harness-source.ps1'))
if((Get-FileHash -LiteralPath (Join-Path $output 'navigation-authoring-harness-source.ps1') -Algorithm SHA256).Hash.ToLowerInvariant() -cne $navAuthoringExecutedHash){throw 'Navigation authoring harness changed since startup.'}
$dependencyRoot=Join-Path $output 'executed-helper-sources';[void](New-Item -ItemType Directory -Path $dependencyRoot)
foreach($dependency in $navAuthoringDependencies.Keys){
    $copied=Join-Path $dependencyRoot ([IO.Path]::GetFileName($dependency));[IO.File]::Copy($dependency,$copied)
    if((Get-FileHash -LiteralPath $copied -Algorithm SHA256).Hash.ToLowerInvariant() -cne $navAuthoringDependencies[$dependency]){throw 'Navigation authoring dependency changed after load; source-copy provenance refused.'}
}
$fixture=Join-Path $output 'navigation-authoring-root'
$cloneArchive=Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $zoneId
$cloneWorkspace=Join-Path $fixture 'workspace-v1\authoring.json'
$clonePaths=@($cloneArchive,(Join-Path $fixture "workspace-v1\objects\$sourcePin.json"),(Join-Path $fixture "workspace-v1\objects\$currentHash.json"))
$cloneBefore=@($clonePaths | ForEach-Object {Get-ArchiveSnapshot -LiteralPath $_})
$profile=Join-Path $output 'webview2-user-data';[void](New-Item -ItemType Directory -Path $profile)
$savedEnvironment=@{};foreach($name in @('FORGE_AUTHORING_ROOT','FORGE_AUTHOR_SUBJECT','WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS','WEBVIEW2_USER_DATA_FOLDER')){$savedEnvironment[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
$process=$null;$socket=$null;$reservation=$null;$failure=$null;$forced=$false;$nativeClose=$false
$savedReference=$null;$savedObjectPath=$null;$savedObject=$null;$gameplayReport=$null
$steps=[System.Collections.Generic.List[object]]::new()
try{
    $reservation=New-LoopbackPortReservation
    [Environment]::SetEnvironmentVariable('FORGE_AUTHORING_ROOT',$fixture,'Process')
    [Environment]::SetEnvironmentVariable('FORGE_AUTHOR_SUBJECT','gate3-public-navigation-authoring','Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS',"--remote-debugging-address=127.0.0.1 --remote-debugging-port=$($reservation.Port)",'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER',$profile,'Process')
    $reservation.Listener.Stop()
    $process=Start-Process -FilePath $candidate -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $output 'candidate.stdout.log') -RedirectStandardError (Join-Path $output 'candidate.stderr.log')
    [void](Wait-LaunchedExecutableIdentity -Process $process -Candidate $candidate -ExpectedHash $CandidateSha256Pinned)
    $target=Wait-CdpTarget -Port $reservation.Port -TimeoutSeconds $StartupTimeoutSeconds -Process $process
    $socket=Connect-CdpSocket -WebSocketUrl ([string]$target.webSocketDebuggerUrl)
    [void](Invoke-CdpCommand -Socket $socket -Method 'Runtime.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.bringToFront')
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'Studio authoring shell' -Expression "(() => ({ready:document.readyState==='complete'&&!!document.querySelector('.authoring-shell'),reason:'authoring shell pending'}))()")
    Open-Gate3TidewaterZone -Socket $socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Invoke-UiClick -Socket $socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Wait-GameplayEditor -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Open-DetailsBySummary -Socket $socket -Summary $editorSummary)
    [void](Wait-PhysicsGameplayIdle -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'navigation authoring idle')
    $steps.Add((Add-Gate3DedicatedPatrolActor -Socket $socket -Name $navOutdoorName -X $OutdoorX -Z $OutdoorZ -Points $OutdoorPatrolPoints -TimeoutSeconds $InteractionTimeoutSeconds))
    $steps.Add((Add-Gate3DedicatedPatrolActor -Socket $socket -Name $navIndoorName -X $IndoorX -Z $IndoorZ -CenterY $IndoorCenterY.ToString('R',[Globalization.CultureInfo]::InvariantCulture) -Points $IndoorPatrolPoints -TimeoutSeconds $InteractionTimeoutSeconds))
    Save-GameplayUiDiagnostic -Socket $socket -OutputRoot $output -Name 'public-patrols-before-save'
    $workspaceBefore=Get-ArchiveSnapshot -LiteralPath $cloneWorkspace
    [void](Invoke-UiClick -Socket $socket -Text 'Sauvegarder')
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'navigation authoring public save' -Expression @'
(() => {const norm=v=>(v??'').replace(/\s+/g,' ').trim();const status=norm(document.querySelector('.authoring-command-status')?.textContent);const busy=document.querySelector('.authoring-toolbar')?.getAttribute('aria-busy');return {ready:status==='Session synchronisée'&&busy==='false'&&norm(document.querySelector('.authoring-command-bar .gold-save-state')?.textContent)==='',terminal:/refusée|erreur|invalide|impossible/i.test(status),reason:status+'; busy='+busy};})()
'@)
    if(Test-ArchiveSnapshotsEqual -Before $workspaceBefore -After (Get-ArchiveSnapshot -LiteralPath $cloneWorkspace)){throw 'Public authoring Save did not change the workspace.'}
    $savedReference=Get-Gate3WorkspaceReference -Archive $cloneWorkspace -ZoneId $zoneId
    if([string]$savedReference.archive_hash -ceq $currentHash -or [string]$savedReference.head_hash -ceq [string]$sourceReference.head_hash){throw 'Patrol Save did not advance archive/head.'}
    $savedObjectPath=Join-Path $fixture "workspace-v1\objects\$($savedReference.archive_hash).json"
    $savedObject=Get-ArchiveSnapshot -LiteralPath $savedObjectPath
    if($savedObject.Sha256 -cne [string]$savedReference.archive_hash){throw 'Saved patrol archive hash differs from workspace receipt.'}
    $after=ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($savedObjectPath))
    Assert-Gate3NavigationExtensionPreserved -Before $before -After $after -AddedActors @($steps)
    if((Get-PhysicsLastReceiptHash $after) -cne [string]$savedReference.head_hash){throw 'Saved patrol head differs from last authoring receipt.'}
    $gameplayReport=Get-Gate3GameplayReport -Archive $savedObjectPath
    Save-GameplayUiDiagnostic -Socket $socket -OutputRoot $output -Name 'public-patrols-saved'
}catch{
    $failure=$_.Exception.Message
    if($null -ne $socket -and $socket.State -eq [Net.WebSockets.WebSocketState]::Open){try{Save-GameplayUiDiagnostic -Socket $socket -OutputRoot $output -Name 'failure'}catch{Write-Verbose $_.Exception.Message}}
}finally{
    if($null -ne $process -and -not $process.HasExited -and $null -ne $socket -and $socket.State -eq [Net.WebSockets.WebSocketState]::Open){
        try{
            $process.Refresh();if(-not $process.CloseMainWindow()){throw 'Owned window refused public close.'}
            [void](Wait-CdpReady -Socket $socket -TimeoutSeconds 10 -Description 'owned native close confirmation' -Expression "(() => ({ready:!!document.querySelector('dialog.gold-close-dialog[open]'),reason:'close confirmation pending'}))()")
            [void](Invoke-UiClick -Socket $socket -Text 'Quitter sans enregistrer' -ScopeSelector 'dialog.gold-close-dialog[open]')
            $nativeClose=$process.WaitForExit(10000)
        }catch{Write-Verbose $_.Exception.Message}
    }
    if($null -ne $socket){$socket.Dispose()}
    if($null -ne $process -and -not $process.HasExited){try{[void]$process.CloseMainWindow()}catch{Write-Verbose $_.Exception.Message};if(-not $process.WaitForExit(5000)){Stop-Process -Id $process.Id -Force;[void]$process.WaitForExit(5000);$forced=$true}}
    if($null -ne $reservation){$reservation.Listener.Stop()}
    foreach($name in $savedEnvironment.Keys){[Environment]::SetEnvironmentVariable($name,$savedEnvironment[$name],'Process')}
}
$sourcePreserved=$true;for($i=0;$i -lt $sourcePaths.Count;$i++){if(-not(Test-ArchiveSnapshotsEqual -Before $sourceBefore[$i] -After (Get-ArchiveSnapshot -LiteralPath $sourcePaths[$i]))){$sourcePreserved=$false}}
$clonePreserved=$true;for($i=0;$i -lt $clonePaths.Count;$i++){if(-not(Test-ArchiveSnapshotsEqual -Before $cloneBefore[$i] -After (Get-ArchiveSnapshot -LiteralPath $clonePaths[$i]))){$clonePreserved=$false}}
$closed=$null -eq $process -or $process.HasExited
if(-not $sourcePreserved -or -not $clonePreserved -or -not $closed -or $forced){$failure="Source preservation or clean owned closure failed. $failure"}
$status=if([string]::IsNullOrWhiteSpace($failure)){'passed'}else{'failed_closed'}
$extension=[ordered]@{schema='forge.tidewater.gate3-navigation-authoring-smoke';version=1;status=$status;failure=$failure;candidate=$candidate;candidate_sha256=$CandidateSha256Pinned;executed_harness_sha256=$navAuthoringExecutedHash;executed_helper_sha256=$navAuthoringDependencies;candidate_kit_and_imports=$candidateKitAndImports;parent_gameplay_authoring_ledger=$ledgerInput;parent_gameplay_authoring_ledger_sha256=$PreviousLedgerSha256Pinned;parent_candidate_sha256=$previous.candidate_sha256;parent_saved_reference=$sourceReference;source_fixture=$source;source_original_hash_pinned=$sourcePin;source_preserved=$sourcePreserved;isolated_fixture=$fixture;isolated_parent_objects_preserved=$clonePreserved;saved_reference=$savedReference;saved_object_path=$savedObjectPath;saved_object=$savedObject;steps=@($steps);parent_gameplay_entries_preserved=($status -ceq 'passed');parent_command_receipts_preserved=($status -ceq 'passed');non_gameplay_head_corrections_identical=($status -ceq 'passed');owned_process_closed=$closed;forced_process_stop=$forced;native_close_confirmed=$nativeClose;gate_qualified=$false;human_visual_review=$false;navigation_runtime_claim=$false;runtime_results_inherited=$false}
$extensionPath=Join-Path $output 'gate3-navigation-authoring-smoke-ledger.json'
[IO.File]::WriteAllText($extensionPath,($extension | ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))
if($status -cne 'passed'){throw "Public navigation authoring failed closed: $failure. Ledger: $extensionPath"}
$final=[ordered]@{schema='forge.tidewater.gate3-gameplay-authoring-smoke';version=1;status='passed';evidence_kind='public_studio_gameplay_navigation_extension';gate_qualified=$false;human_visual_review=$false;source_preserved=$sourcePreserved;zone_uuid=$zoneId;candidate=$candidate;candidate_sha256=$CandidateSha256Pinned;isolated_fixture=$fixture;source_fixture=$source;source_original_hash_pinned=$sourcePin;saved_reference=$savedReference;saved_object_path=$savedObjectPath;saved_object=$savedObject;gameplay_report=$gameplayReport;parent_gameplay_authoring_ledger=$ledgerInput;parent_gameplay_authoring_ledger_sha256=$PreviousLedgerSha256Pinned;parent_saved_reference=$sourceReference;navigation_extension_ledger=$extensionPath;navigation_extension_ledger_sha256=Get-FileSha256 -LiteralPath $extensionPath;navigation_actors=@($steps);authoring_steps=@($steps);runtime_results_inherited=$false;owned_process_closed=$closed;forced_process_stop=$forced;parent_gameplay_entries_preserved=$true;parent_command_receipts_preserved=$true;room_applied=Get-PhysicsProperty $previous 'room_applied';room_result=Get-PhysicsProperty $previous 'room_result';measured_floor_y=Get-PhysicsProperty $previous 'measured_floor_y';room_origin_x=Get-PhysicsProperty $previous 'room_origin_x';room_attestation_provenance=[ordered]@{parent_gameplay_authoring_ledger=$ledgerInput;parent_gameplay_authoring_ledger_sha256=$PreviousLedgerSha256Pinned;parent_attestation=Get-PhysicsProperty $previous 'room_attestation_provenance';non_gameplay_head_corrections_identical=$true;existing_gameplay_entries_preserved=$true;runtime_results_inherited=$false}}
$finalPath=Join-Path $output 'gate3-final-gameplay-authoring-ledger.json'
[IO.File]::WriteAllText($finalPath,($final | ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))
Write-Output "GATE3_NAVIGATION_PUBLIC_AUTHORING_PASS ledger=$extensionPath final=$finalPath runtime_claim=false"
