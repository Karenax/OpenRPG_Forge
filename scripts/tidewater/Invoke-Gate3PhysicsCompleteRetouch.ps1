#Requires -Version 7.2
# Isolated public Studio authoring for the complete Tidewater physics probes.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Candidate,
    [Parameter(Mandatory)][string]$FreshIsolatedFixture,
    [Parameter(Mandatory)][string]$PreviousLedger,
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [int]$InteractionTimeoutSeconds = 120
)
$cfg = @{ Candidate=$Candidate; Source=$FreshIsolatedFixture; Prior=$PreviousLedger; Output=$EvidenceRoot; Timeout=$InteractionTimeoutSeconds }
. (Join-Path $PSScriptRoot 'Invoke-Gate3PhysicsRetouchSmoke.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Invoke-Gate3PhysicsPlaytestSmoke.ps1') -SelfTest | Out-Null
$InteractionTimeoutSeconds = [int]$cfg.Timeout
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$zoneId='e620dd19-ead9-5913-a6d6-0f93109148c0'
$sourcePin='50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720'
$candidate=[IO.Path]::GetFullPath($cfg.Candidate)
$source=[IO.Path]::GetFullPath($cfg.Source)
$output=[IO.Path]::GetFullPath($cfg.Output)
$prior=ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($cfg.Prior))
$candidateHash=Get-FileSha256 -LiteralPath $candidate
if($prior.status -cne 'passed' -or $prior.candidate_sha256 -cne $candidateHash -or $prior.isolated_fixture -cne $source){throw 'Passed parent authoring fixture/candidate identity differs.'}
foreach($p in @($candidate,$source,$output)){Assert-Gate3ProofPath -LiteralPath $p -Description 'complete physics proof'}
if(Test-Path -LiteralPath $output){throw 'Evidence root must be fresh.'}
Assert-TreeHasNoReparsePoints -LiteralPath $source -Description 'complete physics source'
$sourceWorkspace=Join-Path $source 'workspace-v1/authoring.json'
$parentReference=Get-Gate3WorkspaceReference -Archive $sourceWorkspace -ZoneId $zoneId
$parentHash=[string]$parentReference.archive_hash
$sourcePaths=@((Join-Path $source "$zoneId/authoring.json"),$sourceWorkspace,(Join-Path $source "workspace-v1/objects/$sourcePin.json"),(Join-Path $source "workspace-v1/objects/$parentHash.json"))
$sourceBefore=@($sourcePaths | ForEach-Object {Get-ArchiveSnapshot -LiteralPath $_})
if($sourceBefore[0].Sha256 -cne $sourcePin -or $sourceBefore[2].Sha256 -cne $sourcePin){throw 'Original Tidewater bytes differ.'}
[void](New-Item -ItemType Directory -Path $output)
$fixture=Join-Path $output 'physics-retouch-root'
$cloneArchive=Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $zoneId
$clonePaths=@($cloneArchive,(Join-Path $fixture "workspace-v1/objects/$sourcePin.json"),(Join-Path $fixture "workspace-v1/objects/$parentHash.json"))
$cloneBefore=@($clonePaths | ForEach-Object {Get-ArchiveSnapshot -LiteralPath $_})
$envBefore=@{}
foreach($n in @('FORGE_AUTHORING_ROOT','FORGE_AUTHOR_SUBJECT','WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS','WEBVIEW2_USER_DATA_FOLDER','WEBVIEW2_BROWSER_EXECUTABLE_FOLDER')){$envBefore[$n]=[Environment]::GetEnvironmentVariable($n,'Process')}
[Environment]::SetEnvironmentVariable('WEBVIEW2_BROWSER_EXECUTABLE_FOLDER','C:\Program Files (x86)\Microsoft\EdgeWebView\Application\154.0.4258.37','Process')
$run=$null;$failure=$null;$closed=$false;$forced=$false;$steps=[System.Collections.Generic.List[object]]::new();$savedReference=$null;$savedObjectPath=$null;$savedObject=$null
try {
    Write-Output 'GATE3_COMPLETE_PHYSICS_AUTHORING_START'
    $run=Start-ForgeStudioPlaytestProcess -CandidatePath $candidate -CandidateHash $candidateHash -FixtureRoot $fixture -EvidencePath $output -RunNumber 1 -StartupTimeout 180
    Open-Gate3PlaytestZone -Socket $run.Socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Invoke-UiClick -Socket $run.Socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Wait-GameplayEditor -Socket $run.Socket -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Open-DetailsBySummary -Socket $run.Socket -Summary 'Personnages, objets et objectifs')
    [void](Wait-PhysicsGameplayIdle -Socket $run.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'complete physics editor idle')
    # Raise both supported launch centers by 0.12 m: the same generic throw
    # now overlaps the reviewed trap support height. This is authoring only.
    $steps.Add((Retouch-Prop -Socket $run.Socket -Name 'Plateforme de lancer' -X '19' -Y '58.99091056823731' -Z '-270' -Mass '0' -Width '0.05' -Height '0.05' -Depth '0.05' -Pushable $false -Throwable $false -BreakEnergy '' -AllowPositionNoop -AllowPhysicsNoop))
    $steps.Add((Retouch-Prop -Socket $run.Socket -Name 'Charge de lancer' -X '19' -Y '59.28591056823731' -Z '-270' -Mass '30' -Width '0.5' -Height '0.5' -Depth '0.5' -Pushable $false -Throwable $true -BreakEnergy '' -AllowPositionNoop -AllowPhysicsNoop))
    $steps.Add((Retouch-Prop -Socket $run.Socket -Name 'Balise inflammable' -X '18' -Y '58.32' -Z '-270' -Mass '0' -Width '0.5' -Height '0.5' -Depth '0.5' -Pushable $false -Throwable $false -BreakEnergy '' -AllowPositionNoop -AllowPhysicsNoop -Fire))
    Select-GameplayOptionByText -Socket $run.Socket -Label 'Élément à retoucher' -Text 'Veilleur du danger' -TimeoutSeconds $InteractionTimeoutSeconds
    Set-GameplayForm -Socket $run.Socket -Kind 'friendly' -Name 'Veilleur du danger' -X '18.5' -Z '-268.5' -CenterY '' -Reply 'Je surveille le danger.' -TimeoutSeconds $InteractionTimeoutSeconds
    $steps.Add((Invoke-GameplayPreviewApply -Socket $run.Socket -PreviewButton 'Examiner le nom, la position et la réplique' -Description 'danger friendly clear launch flank' -TimeoutSeconds $InteractionTimeoutSeconds))
    # Shore raster floor=-0.52 m was already independently measured. A .5 m
    # cube has 0.05 m initial clearance and no logical support to refuse Push.
    $steps.Add((Add-PhysicsRecipeProp -Socket $run.Socket -Recipe @{Name='Bloc poussable de rive';X=40;Z=-32;CenterY=-0.22;Mass='10';Width='0.5';Height='0.5';Depth='0.5';Pushable=$true;Throwable=$false} -TimeoutSeconds $InteractionTimeoutSeconds))
    Write-Output 'GATE3_COMPLETE_PHYSICS_AUTHORING_APPLIED'
    [void](Wait-PhysicsGameplayIdle -Socket $run.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'complete physics authoring before save')
    [void](Invoke-UiClick -Socket $run.Socket -Text 'Sauvegarder')
    [void](Wait-CdpReady -Socket $run.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'complete physics authoring save' -Expression @'
(() => {const norm=v=>(v??'').replace(/\s+/g,' ').trim();const status=norm(document.querySelector('.authoring-command-status')?.textContent);const busy=document.querySelector('.authoring-toolbar')?.getAttribute('aria-busy');return {ready:status==='Session synchronisée'&&busy==='false',terminal:/refusée|erreur|invalide|impossible/i.test(status),reason:status+' '+busy};})()
'@)
    $savedReference=Get-Gate3WorkspaceReference -Archive (Join-Path $fixture 'workspace-v1/authoring.json') -ZoneId $zoneId
    $savedObjectPath=Join-Path $fixture "workspace-v1/objects/$($savedReference.archive_hash).json"
    $savedObject=Get-ArchiveSnapshot -LiteralPath $savedObjectPath
    if($savedObject.Sha256 -cne [string]$savedReference.archive_hash){throw 'Saved checkpoint hash differs.'}
    $doc=ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($savedObjectPath))
    $gp=$doc.head.corrections.gameplay
    $push=@($gp.objects | Where-Object name -ceq 'Bloc poussable de rive')
    $charge=@($gp.objects | Where-Object name -ceq 'Charge de lancer')
    $danger=@($gp.actors | Where-Object name -ceq 'Veilleur du danger')
    if($push.Count -ne 1 -or -not $push[0].physics.pushable -or $push[0].physics.mass_kg -ne 10 -or [Math]::Abs($push[0].position.y+0.22) -gt 0.001){throw 'Public shore push authoring differs.'}
    if($charge.Count -ne 1 -or [Math]::Abs($charge[0].position.y-59.28591056823731) -gt 0.001){throw 'Public launch height differs.'}
    if($danger.Count -ne 1 -or [Math]::Abs($danger[0].position.x-18.5) -gt 0.001 -or -not $danger[0].flees_danger){throw 'Public danger actor differs.'}
    Write-Output 'GATE3_COMPLETE_PHYSICS_AUTHORING_SAVED'
    Close-Gate3PlaytestZone -Socket $run.Socket -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Wait-CdpReady -Socket $run.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'close authored physics zone' -Expression @'
(() => {const c=(document.querySelector('.authoring-context')?.textContent??'').replace(/\s+/g,' ').trim();return {ready:c==='Aucune carte ouverte'&&document.documentElement?.getAttribute('data-forge-zone-open')==='false',terminal:false,reason:c};})()
'@)
} catch {
    $failure=$_.Exception.Message
    if($null -ne $run -and $null -ne $run.Socket){try{Save-GameplayUiDiagnostic -Socket $run.Socket -OutputRoot $output -Name 'failure'}catch{}}
} finally {
    if($null -ne $run){$stop=Stop-ForgeStudioPlaytestProcess -Run $run;$closed=[bool]$stop.Closed;$forced=[bool]$stop.Forced}
    foreach($n in $envBefore.Keys){$v=if($null -eq $envBefore[$n]){[NullString]::Value}else{$envBefore[$n]};[Environment]::SetEnvironmentVariable($n,$v,'Process')}
}
$sourcePreserved=$true
for($i=0;$i -lt $sourcePaths.Count;$i++){if(-not(Test-ArchiveSnapshotsEqual -Before $sourceBefore[$i] -After (Get-ArchiveSnapshot -LiteralPath $sourcePaths[$i]))){$sourcePreserved=$false}}
for($i=0;$i -lt $clonePaths.Count;$i++){if(-not(Test-ArchiveSnapshotsEqual -Before $cloneBefore[$i] -After (Get-ArchiveSnapshot -LiteralPath $clonePaths[$i]))){$sourcePreserved=$false}}
if(-not $sourcePreserved){$failure="Source archive changed; $failure"}
if(-not $closed -or $forced){$failure="Owned process did not close cleanly; $failure"}
$ledger=[ordered]@{schema='forge.tidewater.gate3-physics-retouch-smoke';version=1;status=if([string]::IsNullOrWhiteSpace($failure)){'passed'}else{'failed_closed'};failure=$failure;gate_qualified=$false;human_visual_review=$false;evidence_kind='public_studio_authoring_only';candidate=$candidate;candidate_sha256=$candidateHash;previous_authoring_ledger=$cfg.Prior;source_fixture=$source;isolated_fixture=$fixture;source_original_hash_pinned=$sourcePin;source_preserved=$sourcePreserved;include_shore_fire=$true;saved_reference=$savedReference;saved_object=$savedObject;saved_object_path=$savedObjectPath;steps=@($steps);owned_process_closed=$closed;forced_process_stop=$forced;limitations=@('Public authoring only; no physics, AI navigation, gameplay or gate verdict follows from placement.')}
$ledgerPath=Join-Path $output 'gate3-physics-retouch-smoke-ledger.json'
[IO.File]::WriteAllText($ledgerPath,($ledger|ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))
if($ledger.status -cne 'passed'){throw "Complete physics retouch failed: $failure; ledger=$ledgerPath"}
Write-Output "GATE3_COMPLETE_PHYSICS_RETOUCH_PASS ledger=$ledgerPath"
