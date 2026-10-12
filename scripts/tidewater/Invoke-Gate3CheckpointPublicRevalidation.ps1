#Requires -Version 7.2
# Reopen an unchanged authored checkpoint on a new candidate. UI reads and
# Studio Save are the only authoring operations; no runtime PASS is inherited.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Candidate,
    [Parameter(Mandatory)][string]$FreshIsolatedFixture,
    [Parameter(Mandatory)][string]$PreviousLedger,
    [Parameter(Mandatory)][string]$EvidenceRoot,
    [Alias('SourceOriginalHash')][ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$SourceOriginalHashPinned='50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720',
    [int]$InteractionTimeoutSeconds=120,
    [Alias('BrowserExecutableFolder')][ValidateNotNullOrEmpty()]
    [string]$BrowserFolder='C:\Program Files (x86)\Microsoft\EdgeWebView\Application\154.0.4258.37',
    [Alias('ExpectedBrowserRuntimeVersion','ExpectedWebViewVersion')]
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$WebViewVersion='154.0.4258.37'
)
$revalidationConfig=@{candidate=$Candidate;source=$FreshIsolatedFixture;parent=$PreviousLedger;output=$EvidenceRoot;SourcePin=$SourceOriginalHashPinned;timeout=$InteractionTimeoutSeconds;Browser=$BrowserFolder;ExpectedBrowser=$WebViewVersion}
$revalidationScriptInputBytes=@(foreach($scriptPath in @($PSCommandPath,(Join-Path $PSScriptRoot 'Invoke-Gate3GameplayAuthoringSmoke.ps1'),(Join-Path $PSScriptRoot 'Invoke-Gate3PhysicsPlaytestSmoke.ps1'),(Join-Path $PSScriptRoot 'Invoke-Gate3GameplayPlaytestSmoke.ps1'),(Join-Path $PSScriptRoot 'Gate3ProofStorage.ps1'),(Join-Path $PSScriptRoot '../Invoke-Gate2WorldEditingProof.ps1'))){$bytes=[IO.File]::ReadAllBytes($scriptPath);[pscustomobject]@{path=[IO.Path]::GetFullPath($scriptPath);bytes=$bytes;sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant();captured_utc=[DateTimeOffset]::UtcNow.ToString('o')}})
. (Join-Path $PSScriptRoot 'Invoke-Gate3GameplayAuthoringSmoke.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Invoke-Gate3PhysicsPlaytestSmoke.ps1') -SelfTest | Out-Null
foreach($scriptInput in $revalidationScriptInputBytes){if((Get-FileSha256 -LiteralPath $scriptInput.path) -cne $scriptInput.sha256){throw "A public helper changed while loading: $($scriptInput.path)."}}
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$candidate=[IO.Path]::GetFullPath($revalidationConfig.candidate)
$source=[IO.Path]::GetFullPath($revalidationConfig.source)
$output=[IO.Path]::GetFullPath($revalidationConfig.output)
$parentPath=[IO.Path]::GetFullPath($revalidationConfig.parent)
$timeout=[int]$revalidationConfig.timeout
$browserExecutable=Join-Path ([IO.Path]::GetFullPath($revalidationConfig.Browser)) 'msedgewebview2.exe'
if(-not(Test-Path -LiteralPath $browserExecutable -PathType Leaf)){throw "WebView2 executable absent: $browserExecutable"}
$browserVersion=[string](Get-Item -LiteralPath $browserExecutable).VersionInfo.FileVersion
if($browserVersion -cne $revalidationConfig.ExpectedBrowser){throw "WebView2 runtime mismatch: observed=$browserVersion expected=$($revalidationConfig.ExpectedBrowser)"}
$browserReceipt=[ordered]@{folder=[IO.Path]::GetFullPath($revalidationConfig.Browser);version=$browserVersion;executable_sha256=Get-FileSha256 -LiteralPath $browserExecutable}
$zoneId='e620dd19-ead9-5913-a6d6-0f93109148c0'
$sourcePin=Get-NormalizedSha256 -Value $revalidationConfig.SourcePin
foreach($path in @($candidate,$source,$output,$parentPath)){Assert-Gate3ProofPath -LiteralPath $path -Description 'public checkpoint revalidation'}
if(Test-Path -LiteralPath $output){throw 'Revalidation evidence root must be fresh.'}
Assert-TreeHasNoReparsePoints -LiteralPath $source -Description 'checkpoint revalidation source'
$parentHash=Get-FileSha256 -LiteralPath $parentPath
$parent=ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($parentPath))
$candidateHash=Get-FileSha256 -LiteralPath $candidate
if($parent.status -cne 'passed' -or -not $parent.source_preserved -or $parent.isolated_fixture -cne $source){throw 'A preserved, passed authoring parent for this exact fixture is required.'}
if($candidateHash -ceq $parent.candidate_sha256){throw 'Revalidation is reserved for a different candidate; use the existing concordant ledger otherwise.'}
$sourceWorkspace=Join-Path $source 'workspace-v1/authoring.json'
$reference=Get-Gate3WorkspaceReference -Archive $sourceWorkspace -ZoneId $zoneId
if($reference.archive_hash -cne $parent.saved_reference.archive_hash -or $reference.head_hash -cne $parent.saved_reference.head_hash){throw 'The parent checkpoint pins do not match the current source.'}
$archiveHash=[string]$reference.archive_hash
$sourceObject=Join-Path $source "workspace-v1/objects/$archiveHash.json"
$sourcePaths=@((Join-Path $source "$zoneId/authoring.json"),$sourceWorkspace,(Join-Path $source "workspace-v1/objects/$sourcePin.json"),$sourceObject)
$sourceBefore=@($sourcePaths|ForEach-Object{Get-ArchiveSnapshot -LiteralPath $_})
if($sourceBefore[0].Sha256 -cne $sourcePin -or $sourceBefore[2].Sha256 -cne $sourcePin -or $sourceBefore[3].Sha256 -cne $archiveHash){throw 'Original or authored checkpoint bytes differ from their pins.'}
$expected=ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($sourceObject))
$gp=$expected.head.corrections.gameplay
[void](New-Item -ItemType Directory -Path $output)
$scriptInputRoot=Join-Path $output 'loaded-script-sources';[void](New-Item -ItemType Directory -Path $scriptInputRoot)
$loadedScriptManifest=@(foreach($scriptInput in $revalidationScriptInputBytes){$copy=Join-Path $scriptInputRoot ([IO.Path]::GetFileName($scriptInput.path));[IO.File]::WriteAllBytes($copy,$scriptInput.bytes);[ordered]@{source_path=$scriptInput.path;loaded_sha256=$scriptInput.sha256;captured_utc=$scriptInput.captured_utc;verified_unchanged_after_import_utc=[DateTimeOffset]::UtcNow.ToString('o');copy_path=$copy;length=$scriptInput.bytes.Length}})
[IO.File]::WriteAllText((Join-Path $output 'loaded-script-inputs.json'),(ConvertTo-Json -InputObject $loadedScriptManifest -Depth 10),[Text.UTF8Encoding]::new($false))
$publicInputTranscript=Join-Path $output 'public-input-transcript.log'
[void](Start-Transcript -Path $publicInputTranscript -NoClobber)
$fixture=Join-Path $output 'revalidated-authoring-root'
$cloneArchive=Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $zoneId
$cloneWorkspace=Join-Path $fixture 'workspace-v1/authoring.json'
$cloneOriginal=Join-Path $fixture "workspace-v1/objects/$sourcePin.json"
$cloneObject=Join-Path $fixture "workspace-v1/objects/$archiveHash.json"
$cloneBefore=@((Get-ArchiveSnapshot -LiteralPath $cloneArchive),(Get-ArchiveSnapshot -LiteralPath $cloneOriginal),(Get-ArchiveSnapshot -LiteralPath $cloneObject))
$envBefore=@{}
foreach($name in @('FORGE_AUTHORING_ROOT','FORGE_AUTHOR_SUBJECT','WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS','WEBVIEW2_USER_DATA_FOLDER','WEBVIEW2_BROWSER_EXECUTABLE_FOLDER')){$envBefore[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
[Environment]::SetEnvironmentVariable('WEBVIEW2_BROWSER_EXECUTABLE_FOLDER',[IO.Path]::GetFullPath($revalidationConfig.Browser),'Process')
$journal=[System.Collections.Generic.List[object]]::new()
$entityReads=[System.Collections.Generic.List[object]]::new()
$failure=$null;$run=$null;$closed=$true;$forced=$false;$ack=$false;$savedReference=$null;$saveObservation=$null
function Read-RevalidationPublicForm([object]$Socket){
    return Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
 const norm=v=>(v??'').replace(/\s+/g,' ').trim();
 const editor=document.querySelector('details.authoring-gameplay-editor');
 if(!editor) throw new Error('Public gameplay editor is absent');
 return {context:norm(document.querySelector('.authoring-context')?.textContent),
 status:norm(document.querySelector('.authoring-command-status')?.textContent),
 toolbar_busy:document.querySelector('.authoring-toolbar')?.getAttribute('aria-busy'),
 fields:[...editor.querySelectorAll('label')].map(label=>{const c=label.querySelector('input,select,textarea');return {label:norm(label.textContent),summary:norm(label.closest('details')?.querySelector(':scope > summary')?.textContent),value:c?.value??null,checked:c?.type==='checkbox'?c.checked:null,disabled:c?.disabled??null,options:c?.tagName==='SELECT'?[...c.options].map(o=>({value:o.value,text:norm(o.textContent)})):null};})};
})()
'@
}
function Get-RevalidationField([object]$Read,[string]$Label){
    $matches=@($Read.fields|Where-Object{$_.label.StartsWith($Label,[StringComparison]::Ordinal)})
    if($matches.Count -ne 1){throw "Expected one public field '$Label', found $($matches.Count)."}
    if($matches[0].disabled){throw "Public field '$Label' is disabled."}
    return $matches[0]
}
function Assert-RevalidationNumber([object]$Read,[string]$Label,[double]$Expected){
    $actual=[double](Get-RevalidationField $Read $Label).value
    if(-not [double]::IsFinite($actual) -or [Math]::Abs($actual-$Expected) -gt 0.0002){throw "Public field '$Label' differs: $actual vs $Expected."}
}
try{
    $run=Start-ForgeStudioPlaytestProcess -CandidatePath $candidate -CandidateHash $candidateHash -FixtureRoot $fixture -EvidencePath $output -RunNumber 1 -StartupTimeout 180;$closed=$false
    Open-Gate3TidewaterZone -Socket $run.Socket -ZoneId $zoneId -TimeoutSeconds $timeout
    [void](Invoke-UiClick -Socket $run.Socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Wait-GameplayEditor -Socket $run.Socket -TimeoutSeconds $timeout)
    [void](Open-DetailsBySummary -Socket $run.Socket -Summary 'Personnages, objets et objectifs')
    [void](Wait-GameplayForm -Socket $run.Socket -TimeoutSeconds $timeout)
    [void](Open-DetailsBySummary -Socket $run.Socket -Summary 'Propriétés physiques')
    [void](Open-DetailsBySummary -Socket $run.Socket -Summary 'Statistiques du personnage sélectionné')
    $initial=Read-RevalidationPublicForm $run.Socket
    $options=@((Get-RevalidationField $initial 'Élément à retoucher').options|Where-Object{$_.value -cne ''})
    $entities=@($gp.actors)+@($gp.objects)+@($gp.places)
    if($options.Count -ne $entities.Count){throw 'Public selected-element options do not match the full authored entity count.'}
    foreach($entry in $entities){if(@($options|Where-Object{$_.value -ceq [string]$entry.id -and $_.text -ceq [string]$entry.name}).Count -ne 1){throw "Public option is absent or ambiguous: $($entry.name)."}}
    foreach($collection in @('actors','objects','places')){
        foreach($entry in @($gp.$collection)){
            Select-GameplayOptionByText -Socket $run.Socket -Label 'Élément à retoucher' -Text ([string]$entry.name) -TimeoutSeconds $timeout
            $entryId=ConvertTo-CompactJson -Value ([string]$entry.id)
            $entryName=ConvertTo-CompactJson -Value ([string]$entry.name)
            [void](Wait-CdpReady -Socket $run.Socket -TimeoutSeconds $timeout -Description "public selected fields $($entry.name)" -Expression @"
(() => {const e=document.querySelector('details.authoring-gameplay-editor');const n=s=>[...e.querySelectorAll('label')].find(l=>(l.textContent??'').trim().startsWith(s))?.querySelector('input,select,textarea');return {ready:n('Élément à retoucher')?.value===$entryId&&n('Nom')?.value===$entryName,terminal:false,reason:'selected entity fields pending'};})()
"@)
            $read=Read-RevalidationPublicForm $run.Socket
            if((Get-RevalidationField $read 'Nom').value -cne $entry.name){throw 'Selected public name differs.'}
            Assert-RevalidationNumber $read 'X (m)' ([double]$entry.position.x)
            Assert-RevalidationNumber $read 'Z (m)' ([double]$entry.position.z)
            Assert-RevalidationNumber $read 'Hauteur du centre (m, vide = terrain)' ([double]$entry.position.y)
            if($collection -ceq 'actors'){
                Assert-RevalidationNumber $read 'Vitalité maximale' ([double]$entry.max_vitality)
                Assert-RevalidationNumber $read 'Protection' ([double]$entry.protection)
                Assert-RevalidationNumber $read "Capacité d’inventaire" ([double]$entry.inventory_capacity)
                if([bool](Get-RevalidationField $read 'Réagit au danger par la fuite').checked -ne [bool]$entry.flees_danger){throw 'Public flee flag differs.'}
                if($null -ne $entry.dialogue){$dialogue=@($gp.dialogues|Where-Object id -CEQ $entry.dialogue)[0];$node=@($dialogue.nodes|Where-Object id -CEQ $dialogue.start)[0];if((Get-RevalidationField $read 'Réplique').value -cne $node.text){throw 'Public starting dialogue reply differs.'}}
            }
            if($collection -ceq 'objects'){
                $p=$entry.physics
                foreach($pair in @(@('Masse (kg)',$p.mass_kg),@('Largeur (m)',(2*[double]$p.body_half_extents_m.x)),@('Hauteur (m)',(2*[double]$p.body_half_extents_m.y)),@('Profondeur (m)',(2*[double]$p.body_half_extents_m.z)),@('Dégâts de chaleur par tick (0 = aucun dégât)',$p.heat_damage_per_tick))){Assert-RevalidationNumber $read $pair[0] ([double]$pair[1])}
                foreach($pair in @(@('Poussable',$p.pushable),@('Lançable',$p.throwable),@('Inflammable',$p.flammable))){if([bool](Get-RevalidationField $read $pair[0]).checked -ne [bool]$pair[1]){throw "Public physics flag differs: $($pair[0])."}}
                $break=(Get-RevalidationField $read 'Énergie de rupture (J, vide = solide)').value
                if($null -eq $p.break_energy_j){if($break -cne ''){throw 'Public solid-object break energy differs.'}}else{Assert-RevalidationNumber $read 'Énergie de rupture (J, vide = solide)' ([double]$p.break_energy_j)}
            }
            $entityReads.Add([ordered]@{collection=$collection;id=[string]$entry.id;name=[string]$entry.name;public_fields=$read;asserted_equal=$true})
            Write-Host "GATE3_PUBLIC_REVALIDATION_ENTITY $($entry.name)"
        }
    }
    Save-GameplayUiDiagnostic -Socket $run.Socket -OutputRoot $output -Name 'revalidation-before-save'
    $preSave=Read-RevalidationPublicForm $run.Socket
    [void](Invoke-UiClick -Socket $run.Socket -Text 'Sauvegarder')
    [void](Wait-CdpReady -Socket $run.Socket -TimeoutSeconds $timeout -Description 'public unchanged checkpoint Save acknowledgement' -Expression @'
(() => {const norm=v=>(v??'').replace(/\s+/g,' ').trim();const status=norm(document.querySelector('.authoring-command-status')?.textContent),busy=document.querySelector('.authoring-toolbar')?.getAttribute('aria-busy');return {ready:status==='Session synchronisée'&&busy==='false'&&norm(document.querySelector('.authoring-command-bar .gold-save-state')?.textContent)==='',terminal:/refusée|erreur|invalide|impossible/i.test(status),reason:'status='+status+'; busy='+busy};})()
'@)
    $ack=$true;$saveObservation=Read-RevalidationPublicForm $run.Socket
    $savedReference=Get-Gate3WorkspaceReference -Archive $cloneWorkspace -ZoneId $zoneId
    if($savedReference.archive_hash -cne $reference.archive_hash -or $savedReference.head_hash -cne $reference.head_hash){throw 'Public Save changed the supposedly unchanged checkpoint; do not rebind its original pins.'}
    if((Get-FileSha256 -LiteralPath $cloneObject) -cne $archiveHash){throw 'Saved authored object bytes changed.'}
    $saved=ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($cloneObject))
    if((ConvertTo-Json -InputObject $saved.head -Depth 100 -Compress) -cne (ConvertTo-Json -InputObject $expected.head -Depth 100 -Compress)){throw 'Saved complete head differs from the parent checkpoint.'}
    Save-GameplayUiDiagnostic -Socket $run.Socket -OutputRoot $output -Name 'revalidation-after-save'
    Close-Gate3PlaytestZone -Socket $run.Socket -TimeoutSeconds $timeout
}catch{$failure=$_.Exception.Message;if($null -ne $run){try{Save-GameplayUiDiagnostic -Socket $run.Socket -OutputRoot $output -Name 'failure'}catch{}}}
finally{
    if($null -ne $run){$stop=Stop-ForgeStudioPlaytestProcess -Run $run;$closed=[bool]$stop.Closed;$forced=[bool]$stop.Forced}
    foreach($name in $envBefore.Keys){$value=if($null -eq $envBefore[$name]){[NullString]::Value}else{$envBefore[$name]};[Environment]::SetEnvironmentVariable($name,$value,'Process')}
}
$sourcePreserved=(Get-FileSha256 -LiteralPath $parentPath) -ceq $parentHash
for($i=0;$i -lt $sourcePaths.Count;$i++){if(-not(Test-ArchiveSnapshotsEqual -Before $sourceBefore[$i] -After (Get-ArchiveSnapshot -LiteralPath $sourcePaths[$i]))){$sourcePreserved=$false}}
$cloneFixedPaths=@($cloneArchive,$cloneOriginal,$cloneObject)
for($i=0;$i -lt $cloneFixedPaths.Count;$i++){if(-not(Test-ArchiveSnapshotsEqual -Before $cloneBefore[$i] -After (Get-ArchiveSnapshot -LiteralPath $cloneFixedPaths[$i]))){$sourcePreserved=$false}}
if(-not $sourcePreserved -or -not $closed -or $forced){$failure="Preservation or owned closure failed; $failure"}
$ledger=[ordered]@{schema='forge.tidewater.gate3-gameplay-authoring-smoke';version=1;status=if([string]::IsNullOrWhiteSpace($failure)){'passed'}else{'failed_closed'};failure=$failure;evidence_kind='public_studio_unchanged_checkpoint_revalidation';candidate=$candidate;candidate_sha256=$candidateHash;isolated_fixture=$fixture;source_fixture=$source;source_original_hash_pinned=$sourcePin;source_preserved=$sourcePreserved;zone_uuid=$zoneId;saved_reference=$savedReference;saved_object_path=$cloneObject;saved_object=Get-ArchiveSnapshot -LiteralPath $cloneObject;gameplay_report=Get-Gate3GameplayReport -Archive $cloneObject;parent_gameplay_authoring_ledger=$parentPath;parent_gameplay_authoring_ledger_sha256=$parentHash;parent_saved_reference=$reference;public_entity_reads=@($entityReads);public_save_ack_observed=$ack;public_save_observation=$saveObservation;complete_head_identical=[string]::IsNullOrWhiteSpace($failure);authoring_steps=@([ordered]@{action='public_read_all_authored_entities';count=$entityReads.Count},[ordered]@{action='public_studio_save_unchanged_checkpoint';ack_observed=$ack});room_applied=$parent.room_applied;room_result=$parent.room_result;measured_floor_y=$parent.measured_floor_y;room_origin_x=$parent.room_origin_x;room_attestation_provenance=[ordered]@{parent_gameplay_authoring_ledger=$parentPath;parent_sha256=$parentHash;complete_head_identical=[string]::IsNullOrWhiteSpace($failure);runtime_results_inherited=$false};owned_process_closed=$closed;forced_process_stop=$forced;runtime_results_inherited=$false;gate_qualified=$false;human_visual_review=$false;limitations=@('UI exposes existing actor/object/place fields; all remaining dialogue/quest/link/room data are preserved by exact complete saved-head and archive identity.','No playtest result is inherited from the previous candidate.')}
$ledgerPath=Join-Path $output 'gate3-revalidated-gameplay-authoring-ledger.json'
$ledger['loaded_script_sources']=@($loadedScriptManifest|ForEach-Object{$_['source_sha256_at_run_end']=Get-FileSha256 -LiteralPath $_.source_path;$_})
$ledger['loaded_script_inputs_path']=Join-Path $output 'loaded-script-inputs.json'
$ledger['loaded_script_inputs_sha256']=Get-FileSha256 -LiteralPath $ledger.loaded_script_inputs_path
[void](Stop-Transcript)
$ledger['public_input_transcript_path']=$publicInputTranscript
$ledger['public_input_transcript_sha256']=Get-FileSha256 -LiteralPath $publicInputTranscript
$ledger['browser_runtime']=$browserReceipt
[IO.File]::WriteAllText($ledgerPath,($ledger|ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))
if($ledger.status -cne 'passed'){throw "Public checkpoint revalidation failed: $failure; ledger=$ledgerPath"}
Write-Output "GATE3_PUBLIC_CHECKPOINT_REVALIDATED ledger=$ledgerPath"
