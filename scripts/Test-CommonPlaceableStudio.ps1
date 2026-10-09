#Requires -Version 7.2
# Bounded common-inspector smoke on an isolated candidate and fixture.
# UI commands, audit receipts and saved documents are evidence; screenshots are not human acceptance.
[CmdletBinding(DefaultParameterSetName = 'Run')]
param(
    [Parameter(ParameterSetName = 'Run', Mandatory)][string]$IsolationRoot,
    [Parameter(ParameterSetName = 'Run', Mandatory)][string]$CandidateExecutable,
    [Parameter(ParameterSetName = 'Run', Mandatory)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$ExpectedSha256,
    [Parameter(ParameterSetName = 'Run')][string]$ExpectedWasmSha256 = '',
    [Parameter(ParameterSetName = 'Run', Mandatory)][string]$FixtureSourceRoot,
    [Parameter(ParameterSetName = 'Run', Mandatory)][string]$OutputDirectory,
    [Parameter(ParameterSetName = 'Run', Mandatory)][guid]$ZoneUuid,
    # Match displayed source names, discovered from the common dropdown; never inject entity IDs.
    [Parameter(ParameterSetName = 'Run', Mandatory)][ValidateCount(1, 8)][string[]]$SourceNamePattern,
    [Parameter(ParameterSetName = 'Run')][string]$ManualNamePattern = '.',
    # Optional actual GPU plant pick. Two normalized canvas coordinates, supplied after inspection.
    [Parameter(ParameterSetName = 'Run')][double[]]$PlantPoint = @(),
    [Parameter(ParameterSetName = 'Run')][switch]$RequirePlant,
    [Parameter(ParameterSetName = 'Run')][switch]$OverviewCamera,
    [Parameter(ParameterSetName = 'Run')][ValidateRange(-200, 200)][int]$PlantCameraOrbitPixels = 0,
    [Parameter(ParameterSetName = 'Run')][ValidateRange(30, 180)][int]$InteractionTimeoutSeconds = 120,
    [Parameter(ParameterSetName = 'SelfTest', Mandatory)][switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$commonInputs = @{
    IsolationRoot = $IsolationRoot; CandidateExecutable = $CandidateExecutable; ExpectedSha256 = $ExpectedSha256
    ExpectedWasmSha256 = $ExpectedWasmSha256
    FixtureSourceRoot = $FixtureSourceRoot; OutputDirectory = $OutputDirectory; ZoneUuid = $ZoneUuid
    SourceNamePattern = $SourceNamePattern; ManualNamePattern = $ManualNamePattern; PlantPoint = $PlantPoint
    RequirePlant = [bool]$RequirePlant; OverviewCamera = [bool]$OverviewCamera
    PlantCameraOrbitPixels = [int]$PlantCameraOrbitPixels
    InteractionTimeoutSeconds = $InteractionTimeoutSeconds; SelfTest = [bool]$SelfTest
}
# Load the established CDP, exact-JSON and isolated-fixture helpers without launching its scenario.
. (Join-Path $PSScriptRoot 'tidewater\Invoke-FishStandStudioSmoke.ps1') -SelfTest | Out-Null
foreach ($name in $commonInputs.Keys) {
    # Run-only validated parameters are intentionally unbound during helper SelfTest.
    if ($null -ne $commonInputs[$name]) { Set-Variable -Name $name -Value $commonInputs[$name] }
}

# Keep each CDP evaluation inside the configured interaction budget. Initial
# imported-scene shader work may occupy the WebView thread longer than the
# shared helper's 15-second default; do not cancel its socket prematurely.
$script:commonCdpExpression = ${function:Invoke-CdpExpression}
function Invoke-CdpExpression {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Expression,
        [ValidateRange(1, 180)][int]$TimeoutSeconds = $InteractionTimeoutSeconds
    )
    & $script:commonCdpExpression -Socket $Socket -Expression $Expression -TimeoutSeconds $TimeoutSeconds
}

# Keep the shared CDP actions, but resolve only controls exposed by the current Studio mode.
# The shared helpers also serve other scenarios and remain unchanged by this bounded smoke.
$commonVisibleUiJavaScript = @'
const isVisible=node=>{
  if(!node||node.getClientRects().length===0) return false;
  for(let current=node;current;current=current.parentElement){
    const style=getComputedStyle(current);
    if(current.hidden||style.display==='none'||style.visibility==='hidden'||style.visibility==='collapse'||Number(style.opacity)===0) return false;
  }
  const rect=node.getBoundingClientRect(); return rect.width>0&&rect.height>0;
};
'@

function Get-UiTarget {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Selector,
        [Parameter(Mandatory)][string]$Text,
        [string]$ScopeSelector = 'document'
    )
    $selectorLiteral = ConvertTo-CompactJson -Value $Selector
    $textLiteral = ConvertTo-CompactJson -Value $Text
    $scopeLiteral = ConvertTo-CompactJson -Value $ScopeSelector
    $expression = @"
(async () => {
  $commonVisibleUiJavaScript
  const norm=v=>(v??'').replace(/\s+/g,' ').trim(), expected=$textLiteral, scopeSelector=$scopeLiteral;
  const scopes=scopeSelector==='document'?[document]:[...document.querySelectorAll(scopeSelector)].filter(isVisible);
  if(scopes.length!==1) return {ready:false,terminal:scopes.length>1,reason:'visible scopes='+scopes.length};
  const matches=[...scopes[0].querySelectorAll($selectorLiteral)].filter(node=>isVisible(node)&&norm(node.textContent)===expected);
  if(matches.length!==1) return {ready:false,terminal:matches.length>1,reason:'visible exact targets='+matches.length+': '+expected};
  const node=matches[0]; node.scrollIntoView({block:'center',inline:'center'});
  await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
  if(!isVisible(node)) return {ready:false,terminal:false,reason:'target became hidden'};
  const rect=node.getBoundingClientRect(), x=rect.left+rect.width/2, y=rect.top+rect.height/2, hit=document.elementFromPoint(x,y);
  const exposed=hit===node||!!hit&&node.contains(hit);
  return {ready:exposed,terminal:false,reason:exposed?'visible target ready':'target centre obscured',x,y,disabled:node.matches(':disabled'),text:norm(node.textContent)};
})()
"@
    return Wait-CdpReady -Socket $Socket -Expression $expression -Description "Visible UI target '$Text'" -TimeoutSeconds 10
}

function Open-DetailsBySummary {
    param([Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket, [Parameter(Mandatory)][string]$Summary)
    $summaryLiteral = ConvertTo-CompactJson -Value $Summary
    return Wait-CdpReady -Socket $Socket -Description "Visible details '$Summary'" -TimeoutSeconds 10 -Expression @"
(() => {
  $commonVisibleUiJavaScript
  const norm=v=>(v??'').replace(/\s+/g,' ').trim(), expected=$summaryLiteral;
  const matches=[...document.querySelectorAll('summary')].filter(node=>isVisible(node)&&norm(node.textContent)===expected);
  if(matches.length!==1) return {ready:false,terminal:matches.length>1,reason:'visible summaries='+matches.length+': '+expected};
  const details=matches[0].closest('details');
  if(!details) return {ready:false,terminal:true,reason:'summary has no details parent'};
  details.open=true; return {ready:true,open:details.open};
})()
"@
}

function Invoke-UiClickInDetailsBySummary {
    param([Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket, [Parameter(Mandatory)][string]$Summary, [Parameter(Mandatory)][string]$Text)
    $summaryLiteral = ConvertTo-CompactJson -Value $Summary
    $textLiteral = ConvertTo-CompactJson -Value $Text
    $target = Wait-CdpReady -Socket $Socket -Description "Visible UI target '$Text' in '$Summary'" -TimeoutSeconds 10 -Expression @"
(async () => {
  $commonVisibleUiJavaScript
  const norm=v=>(v??'').replace(/\s+/g,' ').trim(), summaryText=$summaryLiteral, expected=$textLiteral;
  const summaries=[...document.querySelectorAll('summary')].filter(node=>isVisible(node)&&norm(node.textContent)===summaryText);
  if(summaries.length!==1) return {ready:false,terminal:summaries.length>1,reason:'visible summaries='+summaries.length};
  const details=summaries[0].closest('details'); if(!details) return {ready:false,terminal:true,reason:'details parent absent'};
  details.open=true;
  const matches=[...details.querySelectorAll('button')].filter(node=>isVisible(node)&&norm(node.textContent)===expected);
  if(matches.length!==1) return {ready:false,terminal:matches.length>1,reason:'visible exact buttons='+matches.length};
  const node=matches[0]; node.scrollIntoView({block:'center',inline:'nearest'});
  await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
  if(!isVisible(node)||node.matches(':disabled')) return {ready:false,terminal:false,reason:'visible exact button unavailable'};
  const rect=node.getBoundingClientRect(), fractions=[.5,.25,.75,.15,.85];
  for(const fy of fractions) for(const fx of fractions){
    const x=rect.left+rect.width*fx,y=rect.top+rect.height*fy,hit=document.elementFromPoint(x,y);
    if(hit===node||!!hit&&node.contains(hit)) return {ready:true,reason:'exposed physical point',x,y,fx,fy,text:expected,summary:summaryText};
  }
  return {ready:false,terminal:false,reason:'all exact button interior points obscured'};
})()
"@
    Send-CdpMouseClick -Socket $Socket -X $target.x -Y $target.y
    return $target
}

function Get-UiControlByLabel {
    param([Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket, [Parameter(Mandatory)][string]$Label, [string]$DetailsSummary = '')
    $labelLiteral = ConvertTo-CompactJson -Value $Label
    $summaryLiteral = ConvertTo-CompactJson -Value $DetailsSummary
    return Invoke-CdpExpression -Socket $Socket -Expression @"
(() => {
  $commonVisibleUiJavaScript
  const norm=v=>(v??'').replace(/\s+/g,' ').trim(), expected=$labelLiteral, summaryText=$summaryLiteral;
  let scope=document;
  if(summaryText){
    const summaries=[...document.querySelectorAll('summary')].filter(node=>isVisible(node)&&norm(node.textContent)===summaryText);
    if(summaries.length!==1) throw new Error('Expected one visible details summary '+summaryText+', got '+summaries.length);
    scope=summaries[0].closest('details'); if(!scope) throw new Error('Details parent absent');
  }
  const controls=[...scope.querySelectorAll('label')].filter(label=>norm(label.textContent).startsWith(expected)).map(label=>label.querySelector('input,select,textarea')).filter(isVisible);
  if(controls.length!==1) throw new Error('Expected one visible control '+expected+', got '+controls.length);
  const control=controls[0];
  return {value:String(control.value??''),checked:!!control.checked,disabled:control.matches(':disabled'),tag:control.tagName,type:control.type??null};
})()
"@
}

function Set-UiControlByLabel {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Label, [Parameter(Mandatory)][object]$Value,
        [ValidateSet('value', 'checked')][string]$Kind = 'value', [string]$DetailsSummary = '', [string]$FieldsetLegend = ''
    )
    $labelLiteral = ConvertTo-CompactJson -Value $Label
    $valueLiteral = ConvertTo-CompactJson -Value $Value
    $kindLiteral = ConvertTo-CompactJson -Value $Kind
    $summaryLiteral = ConvertTo-CompactJson -Value $DetailsSummary
    $legendLiteral = ConvertTo-CompactJson -Value $FieldsetLegend
    return Wait-CdpReady -Socket $Socket -Description "Visible writable UI control '$Label'" -TimeoutSeconds 10 -Expression @"
(() => {
  $commonVisibleUiJavaScript
  const norm=v=>(v??'').replace(/\s+/g,' ').trim(), expected=$labelLiteral, summaryText=$summaryLiteral, legendText=$legendLiteral;
  let scope=document;
  if(summaryText){
    const summaries=[...document.querySelectorAll('summary')].filter(node=>isVisible(node)&&norm(node.textContent)===summaryText);
    if(summaries.length!==1) return {ready:false,terminal:summaries.length>1,reason:'visible summaries='+summaries.length};
    scope=summaries[0].closest('details'); if(!scope) return {ready:false,terminal:true,reason:'details parent absent'};
  }
  if(legendText){
    const fieldsets=[...scope.querySelectorAll('fieldset')].filter(node=>isVisible(node)&&norm(node.querySelector(':scope > legend')?.textContent)===legendText);
    if(fieldsets.length!==1) return {ready:false,terminal:fieldsets.length>1,reason:'visible fieldsets='+fieldsets.length};
    scope=fieldsets[0];
  }
  const controls=[...scope.querySelectorAll('label')].filter(label=>norm(label.textContent).startsWith(expected)).map(label=>label.querySelector('input,select,textarea')).filter(isVisible);
  if(controls.length!==1) return {ready:false,terminal:controls.length>1,reason:'visible controls='+controls.length+': '+expected};
  const control=controls[0]; if(control.matches(':disabled')) return {ready:false,terminal:false,reason:'control disabled'};
  const wanted=$kindLiteral==='checked'?!!${valueLiteral}:String($valueLiteral);
  if($kindLiteral==='checked') control.checked=wanted; else control.value=wanted;
  control.dispatchEvent(new Event('input',{bubbles:true})); control.dispatchEvent(new Event('change',{bubbles:true}));
  const observed=$kindLiteral==='checked'?!!control.checked:String(control.value), exact=observed===wanted;
  return {ready:exact,terminal:!exact,reason:exact?'control value accepted':'control value mismatch',label:expected,kind:$kindLiteral,wanted,observed,disabled:!!control.disabled};
})()
"@
}

function Select-UiOptionByLabel {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket, [Parameter(Mandatory)][string]$Label,
        [AllowEmptyString()][Parameter(Mandatory)][string]$Value, [ValidateRange(1, 180)][int]$TimeoutSeconds = 10
    )
    $labelLiteral = ConvertTo-CompactJson -Value $Label
    $valueLiteral = ConvertTo-CompactJson -Value $Value
    return Wait-CdpReady -Socket $Socket -Description "Visible exact option '$Value' under '$Label'" -TimeoutSeconds $TimeoutSeconds -Expression @"
(() => {
  $commonVisibleUiJavaScript
  const norm=v=>(v??'').replace(/\s+/g,' ').trim(), expected=$labelLiteral, value=$valueLiteral;
  const selects=[...document.querySelectorAll('label')].filter(label=>norm(label.textContent).startsWith(expected)).map(label=>label.querySelector('select')).filter(isVisible);
  if(selects.length!==1) return {ready:false,terminal:selects.length>1,reason:'visible selects='+selects.length+': '+expected};
  const select=selects[0]; if(select.matches(':disabled')) return {ready:false,terminal:false,reason:'select temporarily disabled'};
  const option=[...select.options].find(item=>item.value.toLowerCase()===value.toLowerCase());
  if(!option||option.disabled) return {ready:false,terminal:false,reason:'exact option unavailable: '+value};
  select.value=option.value; select.dispatchEvent(new Event('change',{bubbles:true}));
  const ready=select.value.toLowerCase()===value.toLowerCase();
  return {ready,terminal:false,reason:ready?'exact option selected':'selection did not stick',value:select.value,text:norm(option.textContent),option_count:select.options.length-1};
})()
"@
}

function Get-CommonOptionalProperty {
    param([AllowNull()]$Object, [Parameter(Mandatory)][string]$Name)
    if ($null -eq $Object) { return }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -ne $property) { return $property.Value }
    return
}

function Get-CommonCorrectionObject {
    param([Parameter(Mandatory)]$Corrections, [Parameter(Mandatory)][string]$Id)
    foreach ($placement in @(Get-CommonOptionalProperty $Corrections 'placements')) {
        if ([string]$placement.id -ceq $Id) { return $placement }
    }
    foreach ($record in @(Get-CommonOptionalProperty $Corrections 'source_object_edits')) {
        if ([string]$record.source.id -ceq $Id) {
            switch ([string]$record.state.kind) {
                'inherited' { return $record.source }
                'edited' { return $record.state.placement }
                'removed' { return $null }
                default { throw "Unknown source-object state for $Id" }
            }
        }
        foreach ($copy in @($record.copies)) {
            if ([string]$copy.id -ceq $Id) { return $copy }
        }
    }
    return $null
}

function Get-CommonSourceRecord {
    param([Parameter(Mandatory)]$Corrections, [Parameter(Mandatory)][string]$Id)
    $records = @(Get-CommonOptionalProperty $Corrections 'source_object_edits' | Where-Object { [string]$_.source.id -ceq $Id })
    if ($records.Count -gt 1) { throw 'Duplicate source records in the accepted corrections.' }
    if ($records.Count -eq 1) { return $records[0] }
    return $null
}

function Assert-CommonRemovalRecords {
    param([Parameter(Mandatory)]$Corrections, [Parameter(Mandatory)][object[]]$Cases)
    foreach ($case in $Cases | Where-Object kind -ne 'manual') {
        $record = Get-CommonSourceRecord -Corrections $Corrections -Id $case.id
        if ($null -eq $record -or [string]$record.state.kind -cne 'removed' -or @($record.copies).Count -ne 0) { throw 'Removed source requires an explicit tombstone and zero surviving copies.' }
    }
}

function Assert-CommonLockedControls {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)][bool]$Locked, [Parameter(Mandatory)][int]$TimeoutSeconds)
    $lockedLiteral = ConvertTo-CompactJson -Value $Locked
    [void](Wait-CdpReady -Socket $Socket -Description 'Common locked edit controls' -TimeoutSeconds $TimeoutSeconds -Expression @"
(() => { $commonVisibleUiJavaScript
  const norm=v=>(v??'').replace(/\s+/g,' ').trim(); const scope=[...document.querySelectorAll('.atelier-placement-tools .gold-advanced-view')].find(isVisible);
  const guarded=['Prévisualiser le déplacement','Prévisualiser la rotation','Prévisualiser l’échelle','Prévisualiser la suppression'];
  const buttons=[...(scope?.querySelectorAll('button')??[])].filter(isVisible); const states=guarded.map(text=>{const found=buttons.filter(b=>norm(b.textContent)===text);return {text,count:found.length,disabled:found.length===1&&found[0].matches(':disabled')};});
  const copy=buttons.filter(b=>norm(b.textContent)==='Prévisualiser une copie à ces coordonnées');
  return {ready:states.every(s=>s.count===1&&s.disabled===$lockedLiteral)&&copy.length===1&&!copy[0].matches(':disabled'),reason:JSON.stringify(states),controls:states}; })()
"@)
}

function Wait-CommonPublicCamera {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)][int]$TimeoutSeconds)
    return Wait-CdpReady -Socket $Socket -Description 'Fresh stable public imported viewport camera signature' -TimeoutSeconds $TimeoutSeconds -Expression @'
(async () => { const norm=v=>(v??'').replace(/\s+/g,' ').trim(); const canvas=document.getElementById('forge-authoring-canvas');
  const read=()=>{
    const raw=canvas?.getAttribute('data-tidewater-live-shadows')??'null'; let diagnostic=null; try { diagnostic=JSON.parse(raw); } catch {}
    const camera=diagnostic?.camera_signature, matrix=camera?.world_from_view, projection=camera?.projection;
    const viewport={width:canvas?.width??0,height:canvas?.height??0};
    const valid=Array.isArray(matrix)&&matrix.length===16&&matrix.every(Number.isFinite)&&Array.isArray(projection)&&projection.length===4&&projection.every(Number.isFinite)&&viewport.width>0&&viewport.height>0;
    return {raw,camera,viewport,valid};
  };
  // Public shadow diagnostics are published at most every 250 ms. A 120 ms pair can read one stale publication.
  const first=read(); await new Promise(resolve=>setTimeout(resolve,350)); const second=read();
  const stable=JSON.stringify(first.camera)===JSON.stringify(second.camera)&&JSON.stringify(first.viewport)===JSON.stringify(second.viewport), fresh=first.raw!==second.raw;
  const details=[...document.querySelectorAll('summary')].find(s=>norm(s.textContent)==="Plantes importées")?.closest('details');
  const gpu_status=[...(details?.querySelectorAll('output')??[])].map(n=>norm(n.textContent)).find(Boolean)??'';
  return {ready:first.valid&&second.valid&&stable&&fresh,reason:!first.valid||!second.valid?'camera diagnostics pending':(!stable?'camera or canvas still changing':(!fresh?'public diagnostic publication pending':'fresh stable public camera')),camera_signature:second.camera??null,viewport_size:second.viewport,publication_changed:fresh,sample_interval_ms:350,gpu_status,compass:norm(document.querySelector('.forge-compass-readout')?.textContent)}; })()
'@
}

function Compare-CommonPublicCamera {
    param([Parameter(Mandatory)]$Before, [Parameter(Mandatory)]$After)
    $comparison = [ordered]@{ equivalent=$false; reason='Invalid public camera samples'; matrix_deltas=@(); matrix_within_tolerance=$false; projection_equal=$false; fixed_tick_equal=$false; aspect_changed=$false; aspect_change_allowed=$false; viewport_changed=$false; viewport_aspect_coherent=$false; before_viewport_aspect=$null; after_viewport_aspect=$null; matrix_basis_tolerance=0.000001; translation_tolerance_m=0.0001; aspect_rounding_tolerance=0.000002 }
    $beforeMatrix = @($Before.camera_signature.world_from_view)
    $afterMatrix = @($After.camera_signature.world_from_view)
    $beforeProjection = @($Before.camera_signature.projection)
    $afterProjection = @($After.camera_signature.projection)
    if ($beforeMatrix.Count -ne 16 -or $afterMatrix.Count -ne 16 -or $beforeProjection.Count -ne 4 -or $afterProjection.Count -ne 4) { return [pscustomobject]$comparison }
    foreach ($value in @($beforeMatrix) + @($afterMatrix) + @($beforeProjection) + @($afterProjection)) {
        if (-not [double]::IsFinite([double]$value)) { return [pscustomobject]$comparison }
    }
    foreach ($sample in @($Before, $After)) {
        if (-not [double]::IsFinite([double]$sample.viewport_size.width) -or -not [double]::IsFinite([double]$sample.viewport_size.height) -or $sample.viewport_size.width -le 0 -or $sample.viewport_size.height -le 0) { return [pscustomobject]$comparison }
    }
    if ($beforeProjection[1] -le 0 -or $afterProjection[1] -le 0) { return [pscustomobject]$comparison }
    $comparison.matrix_within_tolerance = $true
    $comparison.matrix_deltas = @(for ($index = 0; $index -lt 16; $index++) {
        $delta = [math]::Abs([double]$beforeMatrix[$index] - [double]$afterMatrix[$index])
        $tolerance = if ($index -in @(12,13,14)) { $comparison.translation_tolerance_m } else { $comparison.matrix_basis_tolerance }
        if ($delta -gt $tolerance) { $comparison.matrix_within_tolerance = $false }
        $delta
    })
    $comparison.projection_equal = $true
    foreach ($index in @(0,2,3)) { if ([double]$beforeProjection[$index] -ne [double]$afterProjection[$index]) { $comparison.projection_equal = $false } }
    $comparison.fixed_tick_equal = [object]::Equals((Get-CommonOptionalProperty $Before.camera_signature 'fixed_tick'), (Get-CommonOptionalProperty $After.camera_signature 'fixed_tick'))
    $comparison.aspect_changed = [double]$beforeProjection[1] -ne [double]$afterProjection[1]
    $comparison.viewport_changed = $Before.viewport_size.width -ne $After.viewport_size.width -or $Before.viewport_size.height -ne $After.viewport_size.height
    $comparison.before_viewport_aspect = [double]$Before.viewport_size.width / [double]$Before.viewport_size.height
    $comparison.after_viewport_aspect = [double]$After.viewport_size.width / [double]$After.viewport_size.height
    $comparison.viewport_aspect_coherent = [math]::Abs([double]$beforeProjection[1] - $comparison.before_viewport_aspect) -le $comparison.aspect_rounding_tolerance -and [math]::Abs([double]$afterProjection[1] - $comparison.after_viewport_aspect) -le $comparison.aspect_rounding_tolerance
    $comparison.aspect_change_allowed = -not $comparison.aspect_changed -or ($comparison.viewport_changed -and $comparison.viewport_aspect_coherent -and $comparison.before_viewport_aspect -ne $comparison.after_viewport_aspect)
    $comparison.equivalent = $comparison.matrix_within_tolerance -and $comparison.projection_equal -and $comparison.fixed_tick_equal -and $comparison.aspect_change_allowed
    $comparison.reason = if ($comparison.equivalent) { if ($comparison.aspect_changed) { 'Camera pose stable; aspect follows public canvas resize' } else { 'Camera numerically stable' } } elseif (-not $comparison.matrix_within_tolerance) { 'Camera pose changed beyond bounded numeric tolerance' } elseif (-not $comparison.projection_equal) { 'FOV, near or far changed' } elseif (-not $comparison.fixed_tick_equal) { 'Player fixed tick changed' } else { 'Aspect changed without coherent public canvas resize' }
    return [pscustomobject]$comparison
}

function Wait-CommonPlantTransformerInspector {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)][string]$Id, [Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][int]$TimeoutSeconds)
    $idLiteral = ConvertTo-CompactJson -Value $Id
    $nameLiteral = ConvertTo-CompactJson -Value $Name
    return Wait-CdpReady -Socket $Socket -Description 'Visible Transformer inspector for the real GPU plant selection' -TimeoutSeconds $TimeoutSeconds -Expression @"
(() => {
  $commonVisibleUiJavaScript
  const norm=v=>(v??'').replace(/\s+/g,' ').trim(), expectedId=$idLiteral, expectedName=$nameLiteral;
  const panels=[...document.querySelectorAll('.atelier-transform-inspector')].filter(isVisible);
  if(panels.length!==1) return {ready:false,terminal:panels.length>1,reason:'visible Transformer inspectors='+panels.length};
  const panel=panels[0], labels=[...panel.querySelectorAll('.atelier-inspected-object strong')].filter(isVisible), shown=labels.length===1?norm(labels[0].textContent):'';
  // Read the public common selection value only; the visible Transformer form is the action surface.
  const selectors=[...document.querySelectorAll('label')].filter(label=>norm(label.textContent).startsWith('Objet placé à inspecter')).map(label=>label.querySelector('select')).filter(Boolean);
  const selectedId=selectors.length===1?selectors[0].value:'';
  const fields=['Position · m','Rotation relative · °','Échelle'].flatMap(group=>['X','Y','Z'].map(axis=>{
    const ariaLabel=group+' · '+axis, matches=[...panel.querySelectorAll('input[aria-label]')].filter(node=>isVisible(node)&&node.getAttribute('aria-label')===ariaLabel), input=matches.length===1?matches[0]:null;
    return {aria_label:ariaLabel,count:matches.length,value:input?.value??'',disabled:!input||input.matches(':disabled'),finite:!!input&&input.value.trim()!==''&&Number.isFinite(Number(input.value))};
  }));
  const ready=selectedId===expectedId&&shown===norm(expectedName)&&fields.every(field=>field.count===1&&field.finite&&!field.disabled);
  return {ready,terminal:labels.length>1||selectors.length>1,reason:'selected='+selectedId+'; visible object='+shown+'; ready transform fields='+fields.filter(field=>field.count===1&&field.finite&&!field.disabled).length,selected_id:selectedId,object_label:shown,visible_inspector:true,fields,id_injected:false,source_mutation:false};
})()
"@
}

function Invoke-CommonPlantCameraOrbit {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][ValidateRange(-200, 200)][int]$DeltaPixels,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    if ($DeltaPixels -eq 0) { return $null }
    $canvas = Get-CanvasGeometry -Socket $Socket
    $start = [pscustomobject]@{ x = [double]$canvas.center_x; y = [double]$canvas.center_y }
    $end = [pscustomobject]@{ x = [double]$canvas.center_x + $DeltaPixels; y = [double]$canvas.center_y }
    $pointsLiteral = ConvertTo-CompactJson -Value @{ start = $start; end = $end }
    $points = Wait-CdpReady -Socket $Socket -Description 'Exposed plant camera orbit points' -TimeoutSeconds $TimeoutSeconds -Expression @"
(() => {
  const canvas=document.getElementById('forge-authoring-canvas'), expected=$pointsLiteral;
  if (!canvas) return {ready:false,terminal:true,reason:'canvas absent'};
  const rect=canvas.getBoundingClientRect(), inside=point=>point.x>=rect.left&&point.x<=rect.right&&point.y>=rect.top&&point.y<=rect.bottom;
  const exposed=point=>{const hit=document.elementFromPoint(point.x,point.y);return hit===canvas||!!hit&&canvas.contains(hit);};
  const ready=inside(expected.start)&&inside(expected.end)&&exposed(expected.start)&&exposed(expected.end);
  return {ready,terminal:false,reason:ready?'orbit points exposed':'orbit point outside or obscured',start:expected.start,end:expected.end,rect:{left:rect.left,top:rect.top,right:rect.right,bottom:rect.bottom}};
})()
"@
    $before = Wait-CommonPublicCamera -Socket $Socket -TimeoutSeconds $TimeoutSeconds
    Send-CdpMouseDrag -Socket $Socket -StartX $start.x -StartY $start.y -EndX $end.x -EndY $end.y -Steps 6 -Button right
    $beforeLiteral = ConvertTo-CompactJson -Value $before.camera_signature
    $after = Wait-CdpReady -Socket $Socket -Description 'Stable changed plant camera' -TimeoutSeconds $TimeoutSeconds -Expression @"
(async () => {
  const expected=$beforeLiteral;
  const read=()=>{ let diagnostic=null; try { diagnostic=JSON.parse(document.getElementById('forge-authoring-canvas')?.getAttribute('data-tidewater-live-shadows')??'null'); } catch {};
    const signature=diagnostic?.camera_signature, matrix=signature?.world_from_view, projection=signature?.projection;
    return {signature,valid:Array.isArray(matrix)&&matrix.length===16&&matrix.every(Number.isFinite)&&Array.isArray(projection)&&projection.length===4&&projection.every(Number.isFinite)}; };
  const first=read(); await new Promise(resolve=>setTimeout(resolve,350)); const second=read();
  const changed=JSON.stringify(first.signature)!==JSON.stringify(expected), stable=first.valid&&second.valid&&JSON.stringify(first.signature)===JSON.stringify(second.signature);
  return {ready:first.valid&&second.valid&&changed&&stable,terminal:false,reason:!first.valid||!second.valid?'camera diagnostics pending':(!changed?'camera did not change':(!stable?'camera still moving':'camera changed and stabilized')),changed,stable,camera_signature:second.signature,first:first.signature};
})()
"@
    return [pscustomobject]@{
        delta_pixels = $DeltaPixels; start = $start; end = $end; points = $points
        before = $before; after = $after; source_mutation = $false; id_injected = $false
    }
}

function Assert-CommonIsolatedPath {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Root, [Parameter(Mandatory)][string]$Description)
    Assert-DriveEPath -LiteralPath $Path -Description $Description
    Assert-NoExistingReparseAncestor -LiteralPath $Path -Description $Description
    if (-not (Test-PathWithinDirectory -LiteralPath $Path -Directory $Root)) {
        throw "$Description must remain inside the explicit isolated task root: $Root"
    }
}

function Wait-CommonLibrary {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)][int]$TimeoutSeconds)
    [void](Invoke-UiClick -Socket $Socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Invoke-UiClick -Socket $Socket -Text 'Fiche')
    [void](Open-DetailsBySummary -Socket $Socket -Summary 'Bibliothèque de bâtiments importés')
    [void](Invoke-UiClick -Socket $Socket -Text 'Vérifier et charger la bibliothèque')
    [void](Wait-CdpReady -Socket $Socket -Description 'Verified imported library' -TimeoutSeconds $TimeoutSeconds -Expression @'
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const details=[...document.querySelectorAll('summary')].find(node=>norm(node.textContent)==='Bibliothèque de bâtiments importés')?.closest('details');
  const outputs=[...(details?.querySelectorAll('output')??[])].map(node=>norm(node.textContent));
  const error=outputs.find(value=>value.startsWith('Bibliothèque indisponible :'))??'';
  return {ready:outputs.some(value=>value.includes('bâtiments vérifiés et chargés en mémoire')),terminal:!!error,reason:error||outputs.join(' | ')};
})()
'@)
}

function Open-CommonManagedZone {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)][string]$ZoneId, [Parameter(Mandatory)][int]$TimeoutSeconds, [switch]$OpenWorkspace, [switch]$ExpectDocumentReload)
    [void](Invoke-UiClick -Socket $Socket -Text 'Projet' -Selector '[data-authoring-tab-button]')
    [void](Open-DetailsBySummary -Socket $Socket -Summary 'Projets et modules')
    if ($OpenWorkspace) { [void](Invoke-UiClickInDetailsBySummary -Socket $Socket -Summary 'Projets et modules' -Text 'Ouvrir le workspace') }
    $zoneLiteral = ConvertTo-CompactJson -Value $ZoneId
    $button = Wait-CdpReady -Socket $Socket -Description 'Exact managed zone action' -TimeoutSeconds $TimeoutSeconds -Expression @"
(() => { const norm=v=>(v??'').replace(/\s+/g,' ').trim(); const matches=[...document.querySelectorAll('button[title]')].filter(node=>node.title===$zoneLiteral);
  return {ready:matches.length===1&&!matches[0].disabled,terminal:matches.length>1,reason:'managed-zone actions='+matches.length,text:matches.length===1?norm(matches[0].textContent):''}; })()
"@
    $beforeManagedOpen = @(Get-AuditedAuthoringEntries -Snapshot (Get-WorldAuditSnapshot -Socket $Socket) -RequestKind 'open_managed').Count
    $beforeManagedOpenLiteral = ConvertTo-CompactJson -Value $beforeManagedOpen
    $beforeDocument = Invoke-CdpExpression -Socket $Socket -Expression "(() => ({time_origin:performance.timeOrigin,ready_state:document.readyState}))()"
    if (-not [double]::IsFinite([double]$beforeDocument.time_origin) -or [double]$beforeDocument.time_origin -le 0) { throw 'Public managed-open document time origin is invalid.' }
    $documentOriginLiteral = ConvertTo-CompactJson -Value ([double]$beforeDocument.time_origin)
    $opening = [pscustomobject]@{zone_uuid=$ZoneId;request_kind='open_managed';before_count=$beforeManagedOpen;after_count=$null;expect_document_reload=[bool]$ExpectDocumentReload;before_document=$beforeDocument;after_document=$null;audit_reinstalled=$false;library_reloaded=$false;ready=$null;status='managed open requested'}
    $script:managedOpenEvidence.Add($opening)
    [void](Invoke-UiClick -Socket $Socket -Selector "button[title='$ZoneId']" -Text ([string]$button.text))
    if ($ExpectDocumentReload) {
        # The old document can publish a successful native response immediately before navigation.
        # Wait for its replacement before reading session readiness or reloading resident imports.
        $opening.after_document = Wait-CdpReady -Socket $Socket -Description 'Actual new document after managed reopen' -TimeoutSeconds $TimeoutSeconds -Expression @"
(() => {const expectedOrigin=$documentOriginLiteral,currentOrigin=performance.timeOrigin,changed=Number.isFinite(currentOrigin)&&currentOrigin>0&&currentOrigin!==expectedOrigin;
  const ready=changed&&document.readyState==='complete'&&!!window.__TAURI__?.core;
  return {ready,terminal:false,reason:changed?'new managed-open document initializing':'previous managed-open document still active',previous_time_origin:expectedOrigin,time_origin:currentOrigin,document_changed:changed,ready_state:document.readyState};})()
"@
        [void](Install-WorldAuditAfterOwnedReload -Socket $Socket -TimeoutSeconds $TimeoutSeconds)
        $opening.audit_reinstalled = $true
    }
    do {
        $managedReady = Wait-CdpReady -Socket $Socket -Description 'Audited managed open and restored native session' -TimeoutSeconds $TimeoutSeconds -Expression @"
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim(), zone=$zoneLiteral, before=$beforeManagedOpenLiteral, audit=window.__forgeGate2WorldAudit;
  if(!audit) return {ready:true,terminal:false,audit_missing:true,reason:'owned managed-open document reloaded; audit reinstall required'};
  const managedEntries=audit.entries.filter(entry=>entry.command==='execute_studio_command'&&entry.wire?.command==='authoring'&&entry.request_kind==='open_managed');
  const fresh=managedEntries.slice(before), entry=fresh[0], attempts=entry?.attempts??[], response=attempts.at(-1)?.response?.response??'';
  const exact=entry?.wire?.request?.area_id===zone, resolved=entry?.outcome==='resolved', malformed=!!entry?.parse_error||!!entry?.response_parse_error;
  const terminal=fresh.length>1||!!entry&&(!exact||malformed||entry.outcome==='rejected'||entry.outcome==='threw'||resolved&&(attempts.length!==1||response!=='authoring'));
  const canvas=document.getElementById('forge-authoring-canvas'), term=[...document.querySelectorAll('.authoring-zone-information dt')].find(node=>norm(node.textContent)==='Zone');
  const shown=norm(term?.nextElementSibling?.textContent).toLowerCase(), mode=[...document.querySelectorAll('output')].map(node=>norm(node.textContent)).find(value=>value.startsWith('Mode '))??'';
  const sessionOpen=document.documentElement.getAttribute('data-forge-zone-open')==='true', status=norm(document.querySelector('.authoring-command-status')?.textContent);
  const ready=!terminal&&fresh.length===1&&exact&&resolved&&response==='authoring'&&sessionOpen&&shown===zone&&status==='Session synchronisée'&&mode.startsWith('Mode édition')&&canvas?.clientWidth>0&&canvas?.clientHeight>0;
  return {ready,terminal,audit_missing:false,reason:'new managed opens='+fresh.length+'; native response='+response+'; session open='+sessionOpen+'; zone='+shown+'; status='+status,managed_open_count:managedEntries.length,request_area_id:entry?.wire?.request?.area_id??'',audit_entry:entry??null,session_open:sessionOpen,shown_zone:shown,command_status:status};
})()
"@
        $opening.ready = $managedReady
        if ($managedReady.audit_missing) {
            if ($opening.audit_reinstalled) { throw 'Managed open unexpectedly reloaded its document more than once.' }
            [void](Install-WorldAuditAfterOwnedReload -Socket $Socket -TimeoutSeconds $TimeoutSeconds)
            $opening.audit_reinstalled = $true
        }
    } while ($managedReady.audit_missing)
    $opening.after_count = $managedReady.managed_open_count
    $opening.after_document = Invoke-CdpExpression -Socket $Socket -Expression "(() => ({time_origin:performance.timeOrigin,ready_state:document.readyState,document_changed:performance.timeOrigin!==$documentOriginLiteral}))()"
    # OpenManaged may keep the fresh document created by Close. Verify its resident imports
    # after the actual managed session is ready on either the retained or replacement document.
    Wait-CommonLibrary -Socket $Socket -TimeoutSeconds $TimeoutSeconds
    $opening.library_reloaded = $true
    $opening.status = 'passed - exact managed native open and public session restored'
}

function Get-CommonPlacementOptionValues {
    param([Parameter(Mandatory)]$State)
    # Empty choices are normal while reopening. Keep StrictMode's missing-value error per element.
    foreach ($option in $State.placement_options) { [string]$option.value }
}

function Select-CommonObject {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)][string]$Id, [Parameter(Mandatory)][int]$TimeoutSeconds, [switch]$Advanced)
    [void](Invoke-UiClick -Socket $Socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Invoke-UiClick -Socket $Socket -Text 'Placer / manipuler')
    [void](Open-DetailsBySummary -Socket $Socket -Summary 'Sélection et chargement')
    [void](Select-UiOptionByLabel -Socket $Socket -Label 'Objet placé à inspecter' -Value $Id -TimeoutSeconds $TimeoutSeconds)
    [void](Wait-WorldState -Socket $Socket -Description "Common inspector $Id" -TimeoutSeconds $TimeoutSeconds -Predicate {
        param($state) [string]$state.placement_value -ceq $Id -and -not [bool]$state.placement_disabled
    })
    # Selection intentionally switches Studio to Transformer. Reopen the common editing mode.
    [void](Invoke-UiClick -Socket $Socket -Text 'Placer / manipuler')
    [void](Wait-CdpReady -Socket $Socket -Description 'Visible common editing panel after selection' -TimeoutSeconds $TimeoutSeconds -Expression @"
(() => { $commonVisibleUiJavaScript
  const panels=[...document.querySelectorAll('.atelier-placement-tools')].filter(isVisible);
  return {ready:panels.length===1,terminal:panels.length>1,reason:'visible common editing panels='+panels.length}; })()
"@)
    if ($Advanced) {
        $alreadyAdvanced = Invoke-CdpExpression -Socket $Socket -Expression "(() => { $commonVisibleUiJavaScript return [...document.querySelectorAll('.atelier-placement-tools .gold-advanced-view')].some(isVisible); })()"
        if (-not $alreadyAdvanced) { [void](Invoke-UiClick -Socket $Socket -ScopeSelector '.atelier-placement-tools' -Text 'Réglages avancés →') }
        [void](Open-DetailsBySummary -Socket $Socket -Summary 'Position précise et duplication')
        [void](Open-DetailsBySummary -Socket $Socket -Summary 'Rotation et échelle précises')
    }
}

function Complete-CommonPreview {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)]$Before, [Parameter(Mandatory)][int]$TimeoutSeconds)
    $previewState = Wait-NewPreview -Socket $Socket -PreviousCount ([int]$Before.audit_counts.preview_zone) -TimeoutSeconds $TimeoutSeconds
    Assert-PreviewWithoutMutationIpcDelta -Before $Before -After $previewState -Description 'Common object preview'
    $preview = Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $Socket) -RequestKind 'preview_zone'
    $previewReceipt = Get-RequiredAuditReceipt -Entry $preview
    $corrections = $preview.wire.request.request.action.corrections
    $pending = Get-WorldState -Socket $Socket
    $applyText = Invoke-CdpExpression -Socket $Socket -Expression "document.querySelector('.authoring-apply')?.textContent?.trim()??''"
    if ([string]::IsNullOrWhiteSpace([string]$applyText)) { throw 'Common Apply action is absent.' }
    [void](Invoke-UiClick -Socket $Socket -Selector '.authoring-apply' -Text ([string]$applyText))
    [void](Wait-WorldState -Socket $Socket -Description 'Accepted common edit' -TimeoutSeconds $TimeoutSeconds -Predicate {
        param($state) [int]$state.audit_counts.apply_zone -eq ([int]$pending.audit_counts.apply_zone + 1) -and [int]$state.audit_pending -eq 0 -and $state.diff_hidden
    })
    $apply = Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $Socket) -RequestKind 'apply_zone'
    if ([string]$apply.zone_request_sha256 -cne [string]$preview.zone_request_sha256) { throw 'Apply did not reuse the exact reviewed PreviewZone request.' }
    $applyReceipt = Get-RequiredAuditReceipt -Entry $apply
    if (-not (Test-AuditReceiptsEqual -Left $previewReceipt -Right $applyReceipt)) { throw 'Common PreviewZone and ApplyZone receipts differ.' }
    return [pscustomobject]@{ corrections = $corrections; request_sha256 = $preview.zone_request_sha256; apply_entry = $apply.index; receipt = $applyReceipt }
}

function Test-CommonHistoryApplied {
    param([Parameter(Mandatory)]$Before, [Parameter(Mandatory)]$State)
    return [int]$State.audit_counts.apply_zone -eq ([int]$Before.audit_counts.apply_zone + 1) -and
        [int]$State.audit_pending -eq 0 -and [bool]$State.diff_hidden -and
        -not [bool]$State.placement_disabled
}

function Invoke-CommonUndoRedoKey {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][ValidateSet('Undo', 'Redo')][string]$Action,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $before = Get-WorldState -Socket $Socket
    $canvas = Get-CanvasGeometry -Socket $Socket
    Send-CdpMouseClick -Socket $Socket -X $canvas.center_x -Y $canvas.center_y
    if ($Action -ceq 'Undo') {
        Send-CdpKeyStroke -Socket $Socket -Key 'z' -Code 'KeyZ' -VirtualKeyCode 90 -Modifiers 2
    }
    else {
        Send-CdpKeyStroke -Socket $Socket -Key 'y' -Code 'KeyY' -VirtualKeyCode 89 -Modifiers 2
    }
    $applied = Wait-WorldState -Socket $Socket -Description "$Action common ApplyZone command" -TimeoutSeconds $TimeoutSeconds -Predicate {
        param($state) Test-CommonHistoryApplied -Before $before -State $state
    }
    $entry = Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $Socket) -RequestKind 'apply_zone'
    if ([string]$entry.wire.request.request.action.kind -cne $Action.ToLowerInvariant()) {
        throw "$Action did not emit the expected common history action."
    }
    [void](Get-RequiredAuditReceipt -Entry $entry)
    return $applied
}

function Invoke-CommonTransformCase {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)]$Case, [Parameter(Mandatory)][int]$TimeoutSeconds)
    Select-CommonObject -Socket $Socket -Id $Case.id -TimeoutSeconds $TimeoutSeconds -Advanced
    $x = Get-UiControlByLabel -Socket $Socket -Label 'X (m)' -DetailsSummary 'Position précise et duplication'
    $wantedX = [double]$x.value + 1.25
    [void](Set-UiControlByLabel -Socket $Socket -Label 'X (m)' -Value ($wantedX.ToString('R', [Globalization.CultureInfo]::InvariantCulture)) -DetailsSummary 'Position précise et duplication')
    $draftCancellation = $false
    $draftInspectorReopen = $null
    if ($Case.kind -ceq 'manual') {
        $beforeDraft = Get-WorldState -Socket $Socket
        [void](Invoke-UiClick -Socket $Socket -Text 'Prévisualiser le déplacement')
        $previewDraft = Wait-NewPreview -Socket $Socket -PreviousCount ([int]$beforeDraft.audit_counts.preview_zone) -TimeoutSeconds $TimeoutSeconds
        Assert-PreviewWithoutMutationIpcDelta -Before $beforeDraft -After $previewDraft -Description 'Common draft cancellation preview'
        # Preview remounts the common tools in their basic view. Restore only the form, keeping this exact pending preview and selection.
        $draftAdvanced = Invoke-CdpExpression -Socket $Socket -Expression "(() => { $commonVisibleUiJavaScript return [...document.querySelectorAll('.atelier-placement-tools .gold-advanced-view')].some(isVisible); })()"
        if (-not $draftAdvanced) { [void](Invoke-UiClick -Socket $Socket -ScopeSelector '.atelier-placement-tools' -Text 'Réglages avancés →') }
        [void](Open-DetailsBySummary -Socket $Socket -Summary 'Position précise et duplication')
        $draftInspectorReady = Get-WorldState -Socket $Socket
        Assert-NoMutationIpcDelta -Before $previewDraft -After $draftInspectorReady -Description 'Reopening the manual draft form preserves the preview without mutation'
        if ([string]$draftInspectorReady.placement_value -cne [string]$Case.id -or [bool]$draftInspectorReady.diff_hidden -or [int]$draftInspectorReady.audit_counts.preview_zone -ne [int]$previewDraft.audit_counts.preview_zone) { throw 'Reopening the manual draft form changed its selection or pending preview before the cancellation input.' }
        $draftInspectorReopen = [pscustomobject]@{selected_id=$draftInspectorReady.placement_value;preview_count_before=[int]$previewDraft.audit_counts.preview_zone;preview_count_after=[int]$draftInspectorReady.audit_counts.preview_zone;preview_still_present=$true;mutation_ipc_unchanged=$true;selection_changed=$false}
        [void](Set-UiControlByLabel -Socket $Socket -Label 'X (m)' -Value (($wantedX + 0.5).ToString('R', [Globalization.CultureInfo]::InvariantCulture)) -DetailsSummary 'Position précise et duplication')
        $cancelled = Wait-CleanPreviewState -Socket $Socket -TimeoutSeconds $TimeoutSeconds
        Assert-NoMutationIpcDelta -Before $previewDraft -After $cancelled -Description 'Changing a common inspector draft cancels its preview'
        Select-CommonObject -Socket $Socket -Id $Case.id -TimeoutSeconds $TimeoutSeconds -Advanced
        [void](Set-UiControlByLabel -Socket $Socket -Label 'X (m)' -Value ($wantedX.ToString('R', [Globalization.CultureInfo]::InvariantCulture)) -DetailsSummary 'Position précise et duplication')
        $draftCancellation = $true
    }
    $before = Get-WorldState -Socket $Socket
    [void](Invoke-UiClick -Socket $Socket -Text 'Prévisualiser le déplacement')
    $translation = Complete-CommonPreview -Socket $Socket -Before $before -TimeoutSeconds $TimeoutSeconds
    $moved = Get-CommonCorrectionObject -Corrections $translation.corrections -Id $Case.id
    if ($null -eq $moved -or [math]::Abs([double]$moved.transform.translation_m.x - $wantedX) -gt 0.0001) { throw 'Common X edit did not target the selected identity.' }
    Select-CommonObject -Socket $Socket -Id $Case.id -TimeoutSeconds $TimeoutSeconds -Advanced
    [void](Select-UiOptionByLabel -Socket $Socket -Label 'Axe de rotation' -Value 'y')
    [void](Set-UiControlByLabel -Socket $Socket -Label 'Angle relatif (degrés)' -Value '15' -DetailsSummary 'Rotation et échelle précises')
    $before = Get-WorldState -Socket $Socket
    [void](Invoke-UiClick -Socket $Socket -Text 'Prévisualiser la rotation')
    $rotation = Complete-CommonPreview -Socket $Socket -Before $before -TimeoutSeconds $TimeoutSeconds
    $rotated = Get-CommonCorrectionObject -Corrections $rotation.corrections -Id $Case.id
    if ($null -eq $rotated -or (ConvertTo-CompactJson $rotated.transform.rotation) -ceq (ConvertTo-CompactJson $moved.transform.rotation)) { throw 'Common rotation did not change the selected object.' }
    Select-CommonObject -Socket $Socket -Id $Case.id -TimeoutSeconds $TimeoutSeconds -Advanced
    foreach ($entry in @(@('Échelle X','1.2'), @('Échelle Y','0.9'), @('Échelle Z','1.1'))) {
        [void](Set-UiControlByLabel -Socket $Socket -Label $entry[0] -Value $entry[1] -DetailsSummary 'Rotation et échelle précises')
    }
    $before = Get-WorldState -Socket $Socket
    [void](Invoke-UiClick -Socket $Socket -Text "Prévisualiser l’échelle")
    $scaling = Complete-CommonPreview -Socket $Socket -Before $before -TimeoutSeconds $TimeoutSeconds
    $edited = Get-CommonCorrectionObject -Corrections $scaling.corrections -Id $Case.id
    if ($null -eq $edited) { throw 'Common scale did not target the selected identity.' }
    foreach ($entry in @(@('x',1.2), @('y',0.9), @('z',1.1))) {
        if ([math]::Abs([double]$edited.transform.scale.($entry[0]) - $entry[1]) -gt 0.0001) { throw 'Common nonuniform scale was not preserved in the exact proposal.' }
    }
    Select-CommonObject -Socket $Socket -Id $Case.id -TimeoutSeconds $TimeoutSeconds -Advanced
    $before = Get-WorldState -Socket $Socket
    [void](Invoke-UiClick -Socket $Socket -Text 'Prévisualiser le verrouillage')
    $locking = Complete-CommonPreview -Socket $Socket -Before $before -TimeoutSeconds $TimeoutSeconds
    $lockedObject = Get-CommonCorrectionObject -Corrections $locking.corrections -Id $Case.id
    if ($null -eq $lockedObject -or -not [bool]$lockedObject.locked -or (ConvertTo-CompactJson $lockedObject.transform) -cne (ConvertTo-CompactJson $edited.transform)) { throw 'Common lock changed the selected transform or failed to lock it.' }
    Select-CommonObject -Socket $Socket -Id $Case.id -TimeoutSeconds $TimeoutSeconds -Advanced
    Assert-CommonLockedControls -Socket $Socket -Locked $true -TimeoutSeconds $TimeoutSeconds
    $beforeDisabled = Get-WorldState -Socket $Socket
    foreach ($text in @("Prévisualiser l’échelle",'Prévisualiser la suppression')) {
        [void](Invoke-UiClick -Socket $Socket -ScopeSelector '.atelier-placement-tools .gold-advanced-view' -Text $text -AllowDisabled)
    }
    Assert-NoMutationIpcDelta -Before $beforeDisabled -After (Get-WorldState -Socket $Socket) -Description 'Physical clicks on locked common scale/remove controls'
    $before = Get-WorldState -Socket $Socket
    [void](Invoke-UiClick -Socket $Socket -Text 'Prévisualiser une copie à ces coordonnées')
    $duplication = Complete-CommonPreview -Socket $Socket -Before $before -TimeoutSeconds $TimeoutSeconds
    $after = Get-WorldState -Socket $Socket
    $added = @($after.placement_options | Where-Object { @(Get-CommonPlacementOptionValues -State $before) -notcontains [string]$_.value })
    if ($added.Count -ne 1) { throw "Common duplication must expose exactly one new ID, got $($added.Count)." }
    $copy = Get-CommonCorrectionObject -Corrections $duplication.corrections -Id ([string]$added[0].value)
    if ($null -eq $copy -or [string]$copy.asset_id -cne [string]$edited.asset_id -or [string]$copy.asset_hash -cne [string]$edited.asset_hash -or [bool]$copy.locked) { throw 'Common duplicate lost its pin or received a lock.' }
    if ((ConvertTo-CompactJson $copy.transform) -cne (ConvertTo-CompactJson $edited.transform)) { throw 'Common duplicate changed the accepted transform.' }
    $stillLocked = Get-CommonCorrectionObject -Corrections $duplication.corrections -Id $Case.id
    if ($null -eq $stillLocked -or -not [bool]$stillLocked.locked) { throw 'Duplicating a locked object unlocked its original.' }
    Select-CommonObject -Socket $Socket -Id $Case.id -TimeoutSeconds $TimeoutSeconds -Advanced
    $before = Get-WorldState -Socket $Socket
    [void](Invoke-UiClick -Socket $Socket -Text 'Prévisualiser le déverrouillage')
    $unlocking = Complete-CommonPreview -Socket $Socket -Before $before -TimeoutSeconds $TimeoutSeconds
    $unlocked = Get-CommonCorrectionObject -Corrections $unlocking.corrections -Id $Case.id
    if ($null -eq $unlocked -or -not (Test-StandPlacementEquivalent -Left $edited -Right $unlocked)) { throw 'Common unlock failed to restore the edited object without changing its transform.' }
    Select-CommonObject -Socket $Socket -Id $Case.id -TimeoutSeconds $TimeoutSeconds -Advanced
    Assert-CommonLockedControls -Socket $Socket -Locked $false -TimeoutSeconds $TimeoutSeconds
    return [pscustomobject]@{ kind=$Case.kind; name=$Case.name; id=$Case.id; edited=$unlocked; copy=$copy; lock_controls_verified=$true; preview_cancelled_by_draft_change=$draftCancellation; draft_inspector_reopen=$draftInspectorReopen; requests=@($translation.request_sha256,$rotation.request_sha256,$scaling.request_sha256,$locking.request_sha256,$duplication.request_sha256,$unlocking.request_sha256) }
}

function Invoke-CommonDeletion {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)][string]$Id, [Parameter(Mandatory)][int]$TimeoutSeconds)
    Select-CommonObject -Socket $Socket -Id $Id -TimeoutSeconds $TimeoutSeconds -Advanced
    $before = Get-WorldState -Socket $Socket
    [void](Invoke-UiClick -Socket $Socket -Text 'Prévisualiser la suppression')
    $operation = Complete-CommonPreview -Socket $Socket -Before $before -TimeoutSeconds $TimeoutSeconds
    [void](Wait-WorldState -Socket $Socket -Description "Removed common object $Id" -TimeoutSeconds $TimeoutSeconds -Predicate {
        param($state) @(Get-CommonPlacementOptionValues -State $state) -notcontains $Id -and [int]$state.audit_pending -eq 0
    })
    if ($null -ne (Get-CommonCorrectionObject -Corrections $operation.corrections -Id $Id)) { throw 'Deleted object remains in the reviewed corrections.' }
    return $operation
}

function Save-CommonWorkspace {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)][string]$WorkspaceArchive, [Parameter(Mandatory)][string]$ObjectRoot, [Parameter(Mandatory)][string]$ZoneId, [Parameter(Mandatory)][int]$TimeoutSeconds)
    $before = Get-WorldState -Socket $Socket
    [void](Invoke-UiClick -Socket $Socket -Text 'Sauvegarder')
    [void](Wait-WorldState -Socket $Socket -Description 'Common edits saved' -TimeoutSeconds $TimeoutSeconds -Predicate {
        param($state) [int]$state.audit_counts.save -eq ([int]$before.audit_counts.save + 1) -and [int]$state.audit_pending -eq 0 -and [string]$state.command_status -like '*Session synchronisée*'
    })
    $reference = Get-StandWorkspaceReference -Archive $WorkspaceArchive -ZoneId $ZoneId
    $object = Join-Path $ObjectRoot "$($reference.archive_hash).json"
    $snapshot = Get-ArchiveSnapshot -LiteralPath $object
    if ($snapshot.Sha256 -cne [string]$reference.archive_hash) { throw 'Saved zone object hash does not match the managed workspace reference.' }
    return [pscustomobject]@{ path=$object; snapshot=$snapshot; workspace=(Get-ArchiveSnapshot -LiteralPath $WorkspaceArchive); head=(ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($object))).head }
}

function Reopen-CommonWorkspaceZone {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)][string]$ZoneId, [Parameter(Mandatory)][int]$TimeoutSeconds)
    [void](Invoke-UiClick -Socket $Socket -Text 'Projet' -Selector '[data-authoring-tab-button]')
    [void](Open-DetailsBySummary -Socket $Socket -Summary 'Créer ou modifier la zone')
    $before = Get-WorldState -Socket $Socket
    [void](Invoke-UiClick -Socket $Socket -Text 'Fermer la zone')
    [void](Install-WorldAuditAfterOwnedReload -Socket $Socket -TimeoutSeconds $TimeoutSeconds)
    [void](Wait-WorldState -Socket $Socket -Description 'Isolated zone closed' -TimeoutSeconds $TimeoutSeconds -Predicate {
        param($state) [string]$state.command_status -ceq 'Aucune zone ouverte' -and [int]$state.audit_pending -eq 0 -and [int]$state.audit_counts.close -eq ([int]$before.audit_counts.close + 1)
    })
    Wait-CommonLibrary -Socket $Socket -TimeoutSeconds $TimeoutSeconds
    Open-CommonManagedZone -Socket $Socket -ZoneId $ZoneId -TimeoutSeconds $TimeoutSeconds
    [void](Invoke-UiClick -Socket $Socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Invoke-UiClick -Socket $Socket -Text 'Placer / manipuler')
    [void](Open-DetailsBySummary -Socket $Socket -Summary 'Sélection et chargement')
}

if ($SelfTest) {
    if (@(Get-CommonPlacementOptionValues -State ([pscustomobject]@{placement_options=@()})).Count -ne 0) { throw 'Empty common choices must yield an empty value list during reload.' }
    $optionValues = @(Get-CommonPlacementOptionValues -State ([pscustomobject]@{placement_options=@([pscustomobject]@{value='first'},[pscustomobject]@{value='second'})}))
    if ($optionValues.Count -ne 2 -or $optionValues[0] -cne 'first' -or $optionValues[1] -cne 'second') { throw 'Common choice iteration must preserve exact IDs and their order.' }
    $malformedOptionRejected = $false
    try { [void]@(Get-CommonPlacementOptionValues -State ([pscustomobject]@{placement_options=@([pscustomobject]@{text='value absent'})})) }
    catch {
        if ($_.FullyQualifiedErrorId -notlike 'PropertyNotFoundStrict*') { throw }
        $malformedOptionRejected = $true
    }
    if (-not $malformedOptionRejected) { throw 'A malformed common choice without value must still fail closed.' }
    $cameraBefore = [pscustomobject]@{ camera_signature=[pscustomobject]@{world_from_view=@(1.0,0.0,0.0,0.0,0.0,1.0,0.0,0.0,0.0,0.0,1.0,0.0,98.0,14.0,-13.0,1.0);projection=@(1.08,1.25,0.06,60000.0);fixed_tick=$null};viewport_size=[pscustomobject]@{width=1000;height=800} }
    $cameraAfter = ConvertFrom-ExactJson -Json (ConvertTo-CompactJson -Value $cameraBefore)
    if (-not (Compare-CommonPublicCamera -Before $cameraBefore -After $cameraAfter).equivalent) { throw 'Identical numeric cameras must compare equal.' }
    $cameraAfter.camera_signature.world_from_view[4] = -0.0
    $cameraAfter.camera_signature.world_from_view[0] += 0.0000005
    $cameraAfter.camera_signature.world_from_view[12] += 0.00005
    if (-not (Compare-CommonPublicCamera -Before $cameraBefore -After $cameraAfter).equivalent) { throw 'Signed zero and bounded f32 jitter must preserve the camera.' }
    $cameraAfter.camera_signature.projection[1] = 1.0
    $cameraAfter.viewport_size.height = 1000
    if (-not (Compare-CommonPublicCamera -Before $cameraBefore -After $cameraAfter).equivalent) { throw 'Aspect following a coherent public canvas resize must preserve the camera pose.' }
    $cameraAfter.viewport_size.height = 800
    if ((Compare-CommonPublicCamera -Before $cameraBefore -After $cameraAfter).equivalent) { throw 'An aspect change without a matching public canvas resize must fail closed.' }
    $cameraAfter = ConvertFrom-ExactJson -Json (ConvertTo-CompactJson -Value $cameraBefore)
    $cameraAfter.camera_signature.world_from_view[12] += 0.01
    if ((Compare-CommonPublicCamera -Before $cameraBefore -After $cameraAfter).equivalent) { throw 'A real camera translation must fail closed.' }
    $cameraAfter = ConvertFrom-ExactJson -Json (ConvertTo-CompactJson -Value $cameraBefore)
    $cameraAfter.camera_signature.world_from_view[0] += 0.0001
    if ((Compare-CommonPublicCamera -Before $cameraBefore -After $cameraAfter).equivalent) { throw 'A real camera basis change must fail closed.' }
    $cameraAfter = ConvertFrom-ExactJson -Json (ConvertTo-CompactJson -Value $cameraBefore)
    foreach ($index in @(0,2,3)) {
        $original = $cameraAfter.camera_signature.projection[$index]
        $cameraAfter.camera_signature.projection[$index] += 0.001
        if ((Compare-CommonPublicCamera -Before $cameraBefore -After $cameraAfter).equivalent) { throw 'FOV, near and far must remain numerically exact.' }
        $cameraAfter.camera_signature.projection[$index] = $original
    }
    $cameraAfter.camera_signature.world_from_view[0] = [double]::NaN
    if ((Compare-CommonPublicCamera -Before $cameraBefore -After $cameraAfter).equivalent) { throw 'A non-finite camera must fail closed.' }
    foreach ($name in @('Copy-IsolatedFixture','Get-RequiredAuditedEntry','Select-UiOptionByLabel','Invoke-CommonUndoRedoKey','Invoke-CommonPlantCameraOrbit','Send-CdpMouseDrag','Wait-LaunchedExecutableIdentity','Test-CdpListenerClosed')) { [void](Get-Command $name -ErrorAction Stop) }
    $historyBefore = [pscustomobject]@{ audit_counts=[pscustomobject]@{apply_zone=2} }
    $historyApplied = [pscustomobject]@{ audit_counts=[pscustomobject]@{apply_zone=3}; audit_pending=0; diff_hidden=$true; placement_disabled=$false; building_disabled=$true }
    if (-not (Test-CommonHistoryApplied -Before $historyBefore -State $historyApplied)) { throw 'Common history must accept a ready common selector without parametric buildings.' }
    $historyApplied.audit_pending = 1
    if (Test-CommonHistoryApplied -Before $historyBefore -State $historyApplied) { throw 'Pending common history must not count as applied.' }
    $historyApplied.audit_pending = 0
    $historyApplied.audit_counts.apply_zone = 4
    if (Test-CommonHistoryApplied -Before $historyBefore -State $historyApplied) { throw 'Common history requires exactly one ApplyZone command.' }
    $historyApplied.audit_counts.apply_zone = 3
    $historyApplied.placement_disabled = $true
    if (Test-CommonHistoryApplied -Before $historyBefore -State $historyApplied) { throw 'Common history requires its common selector to be ready.' }
    $fixtureCorrections = [pscustomobject]@{ placements=@(); source_object_edits=@([pscustomobject]@{
        source=[pscustomobject]@{ id='original' }; state=[pscustomobject]@{ kind='edited'; placement=[pscustomobject]@{ id='original'; name='edited' } }; copies=@([pscustomobject]@{ id='copy' })
    }) }
    if ((Get-CommonCorrectionObject $fixtureCorrections 'original').name -cne 'edited' -or (Get-CommonCorrectionObject $fixtureCorrections 'copy').id -cne 'copy') { throw 'Common saved-object resolution failed.' }
    $fixtureCorrections.source_object_edits[0].state = [pscustomobject]@{ kind='removed' }
    if ($null -ne (Get-CommonCorrectionObject $fixtureCorrections 'original')) { throw 'Removed source object was restored by the smoke helper.' }
    $fixtureCorrections.source_object_edits[0].copies = @()
    Assert-CommonRemovalRecords -Corrections $fixtureCorrections -Cases @([pscustomobject]@{id='original';kind='source'})
    $missingTombstoneRejected = $false
    try { Assert-CommonRemovalRecords -Corrections ([pscustomobject]@{}) -Cases @([pscustomobject]@{id='missing';kind='source'}) } catch { $missingTombstoneRejected = $true }
    if (-not $missingTombstoneRejected) { throw 'A missing source tombstone must not count as successful deletion.' }
    if ($null -ne (Get-CommonCorrectionObject ([pscustomobject]@{}) 'missing')) { throw 'Missing correction fields must resolve to no object.' }
    Write-Output 'COMMON_PLACEABLE_STUDIO_SELF_TEST_PASS'
    return
}

if (-not (Test-IsWindowsPlatform)) { throw 'Windows/WebView2 is required.' }
$taskRoot = [IO.Path]::GetFullPath($IsolationRoot).TrimEnd('\')
if ($taskRoot -notmatch '^E:\\OpenRPG_Forge\\\.(editor-placeables|placeables-smoke)-[^\\]+$') { throw 'IsolationRoot must be a dedicated .editor-placeables-* or .placeables-smoke-* task directory on E:, outside the original repository.' }
$candidate = [IO.Path]::GetFullPath($CandidateExecutable)
$source = [IO.Path]::GetFullPath($FixtureSourceRoot)
$output = [IO.Path]::GetFullPath($OutputDirectory)
foreach ($entry in @(@($candidate,'Candidate'), @($source,'Fixture source'), @($output,'Output'))) { Assert-CommonIsolatedPath -Path $entry[0] -Root $taskRoot -Description $entry[1] }
if (Test-Path -LiteralPath $output) { throw 'OutputDirectory must be fresh.' }
if ((Test-PathWithinDirectory -LiteralPath $output -Directory $source) -or (Test-PathWithinDirectory -LiteralPath $source -Directory $output) -or (Test-PathWithinDirectory -LiteralPath $output -Directory (Split-Path -Parent $candidate))) { throw 'Fixture source, candidate and output must have separate directories.' }
if ((Get-FileSha256 -LiteralPath $candidate) -cne (Get-NormalizedSha256 -Value $ExpectedSha256)) { throw 'Candidate executable hash mismatch.' }
$webRoot = Join-Path (Split-Path -Parent $candidate) 'web'
Assert-CommonIsolatedPath -Path $webRoot -Root $taskRoot -Description 'Candidate frontend'
Assert-TreeHasNoReparsePoints -LiteralPath $webRoot -Description 'Isolated candidate frontend'
$frontendPins = @(Get-ChildItem -LiteralPath $webRoot -Recurse -Force -File | Sort-Object FullName | ForEach-Object {
    [pscustomobject]@{ path = $_.FullName; sha256 = (Get-FileSha256 -LiteralPath $_.FullName) }
})
$wasmFiles = @(Get-ChildItem -LiteralPath $webRoot -File -Filter 'forge_studio_bg.wasm')
if ($wasmFiles.Count -ne 1) { throw 'Candidate frontend must contain exactly one Studio *_bg.wasm artifact.' }
$wasmPath = $wasmFiles[0].FullName
$wasmBefore = Get-FileSha256 -LiteralPath $wasmPath
if (-not [string]::IsNullOrWhiteSpace($ExpectedWasmSha256) -and $wasmBefore -cne (Get-NormalizedSha256 -Value $ExpectedWasmSha256)) { throw 'Candidate frontend WASM hash mismatch.' }
if ($PlantPoint.Count -notin @(0,2) -or ($PlantPoint.Count -eq 2 -and @($PlantPoint | Where-Object { -not [double]::IsFinite($_) -or $_ -lt 0 -or $_ -gt 1 }).Count -ne 0)) { throw 'PlantPoint must contain two finite normalized canvas coordinates.' }
if ($RequirePlant -and $PlantPoint.Count -ne 2) { throw 'RequirePlant needs inspected GPU click coordinates; no entity-ID injection is allowed.' }
if ($PlantCameraOrbitPixels -ne 0 -and $PlantPoint.Count -ne 2) { throw 'PlantCameraOrbitPixels requires the actual GPU plant-pick path.' }
foreach ($pattern in @($SourceNamePattern) + @($ManualNamePattern)) { [void][regex]::new($pattern) }
Assert-TreeHasNoReparsePoints -LiteralPath $source -Description 'Isolated source fixture'
$zoneId = $ZoneUuid.ToString('D').ToLowerInvariant()
$sourceArchive = Join-Path (Join-Path $source $zoneId) 'authoring.json'
$sourceWorkspace = Join-Path $source 'workspace-v1\authoring.json'
$sourceReference = Get-StandWorkspaceReference -Archive $sourceWorkspace -ZoneId $zoneId
$sourceObject = Join-Path $source "workspace-v1\objects\$($sourceReference.archive_hash).json"
if ((Get-FileSha256 -LiteralPath $sourceObject) -cne [string]$sourceReference.archive_hash) { throw 'Source workspace reference does not match its immutable zone object.' }
$sourceSnapshots = @($sourceArchive, $sourceWorkspace, $sourceObject) | ForEach-Object { [pscustomobject]@{ path=$_; snapshot=(Get-ArchiveSnapshot -LiteralPath $_) } }
$sourceHead = (ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($sourceObject))).head
$manualIds = @(Get-CommonOptionalProperty $sourceHead.corrections 'placements' | ForEach-Object { [string]$_.id })
[void](New-Item -ItemType Directory -Path $output)
$fixture = Join-Path $output 'authoring-root'
$archive = Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $zoneId
$workspaceArchive = Join-Path $fixture 'workspace-v1\authoring.json'
$objectRoot = Join-Path $fixture 'workspace-v1\objects'
$cloneSnapshots = @($archive, (Join-Path $objectRoot "$($sourceReference.archive_hash).json")) | ForEach-Object { [pscustomobject]@{ path=$_; snapshot=(Get-ArchiveSnapshot -LiteralPath $_) } }
$plantMetadataBefore = $null
if ($PlantPoint.Count -eq 2) {
    $plantMetadataBefore = ConvertTo-CompactJson -Value $sourceHead.base.vegetation.imported_source
    foreach ($block in $sourceHead.base.vegetation.imported_source.blocks) {
        $relative = "$zoneId\source-blobs\$($block.instances_sha256).bin"
        foreach ($entry in @(@($source,'source'),@($fixture,'clone'))) {
            $path = Join-Path $entry[0] $relative
            $snapshot = Get-ArchiveSnapshot -LiteralPath $path
            if ($snapshot.Sha256 -cne [string]$block.instances_sha256) { throw 'Plant source block payload does not match its declared pin.' }
            $observed = [pscustomobject]@{path=$path;snapshot=$snapshot}
            if ($entry[1] -ceq 'source') { $sourceSnapshots += $observed } else { $cloneSnapshots += $observed }
        }
    }
}
$profile = Join-Path $output 'webview2-user-data'
[void](New-Item -ItemType Directory -Path $profile)
$savedEnvironment = @{}
foreach ($name in @('FORGE_AUTHORING_ROOT','FORGE_AUTHOR_SUBJECT','WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS','WEBVIEW2_USER_DATA_FOLDER')) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
$process = $null; $socket = $null; $reservation = $null; $port = 0; $failure = $null; $failureState = $null
$cases = [System.Collections.Generic.List[object]]::new()
$screenshots = [System.Collections.Generic.List[object]]::new()
$editedCases = [System.Collections.Generic.List[object]]::new()
$deletions = [System.Collections.Generic.List[object]]::new()
$savedEdited = $null; $savedRemoved = $null; $plantStatus = if ($PlantPoint.Count -eq 2) { 'GPU coordinates supplied; pick not yet observed' } else { 'NOT RUN - GPU coordinates not supplied' }; $plantPickEvidence = $null; $overviewCameraEvidence = $null; $plantCameraOrbitEvidence = $null; $cleanupClosed = $false
$script:managedOpenEvidence = [System.Collections.Generic.List[object]]::new()
try {
    $reservation = New-LoopbackPortReservation; $port = $reservation.Port
    [Environment]::SetEnvironmentVariable('FORGE_AUTHORING_ROOT',$fixture,'Process')
    [Environment]::SetEnvironmentVariable('FORGE_AUTHOR_SUBJECT','common-placeable-isolated-smoke','Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS',"--remote-debugging-address=127.0.0.1 --remote-debugging-port=$port",'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER',$profile,'Process')
    $reservation.Listener.Stop()
    $process = Start-Process -FilePath $candidate -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $output 'candidate.stdout.log') -RedirectStandardError (Join-Path $output 'candidate.stderr.log')
    $identity = Wait-LaunchedExecutableIdentity -Process $process -Candidate $candidate -ExpectedHash (Get-NormalizedSha256 -Value $ExpectedSha256)
    $target = Wait-CdpTarget -Port $port -TimeoutSeconds 60 -Process $process
    $socket = Connect-CdpSocket -WebSocketUrl ([string]$target.webSocketDebuggerUrl)
    [void](Invoke-CdpCommand -Socket $socket -Method 'Runtime.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.bringToFront')
    [void](Wait-CdpReady -Socket $socket -Description 'Common authoring shell' -TimeoutSeconds $InteractionTimeoutSeconds -Expression "(() => ({ready:document.readyState==='complete'&&!!document.querySelector('.authoring-shell'),reason:'shell pending'}))()")
    Wait-CommonLibrary -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Install-WorldAudit -Socket $socket)
    Open-CommonManagedZone -Socket $socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds -OpenWorkspace
    [void](Invoke-UiClick -Socket $socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Invoke-UiClick -Socket $socket -Text 'Placer / manipuler')
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Sélection et chargement')
    $initial = Wait-WorldState -Socket $socket -Description 'Source and authored choices in the common selector' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
        param($state)
        foreach ($pattern in $SourceNamePattern) {
            if (@($state.placement_options | Where-Object { [string]$_.text -match $pattern -and $manualIds -notcontains [string]$_.value }).Count -eq 0) { return $false }
        }
        return @($state.placement_options | Where-Object { $manualIds -contains [string]$_.value -and [string]$_.text -match $ManualNamePattern }).Count -gt 0 -and [int]$state.audit_pending -eq 0
    }
    foreach ($pattern in $SourceNamePattern) {
        $options = @($initial.placement_options | Where-Object { [string]$_.text -match $pattern -and $manualIds -notcontains [string]$_.value })
        if ($options.Count -eq 0) { throw "No inherited source object matches displayed-name pattern '$pattern'." }
        $option = $options | Sort-Object value | Select-Object -First 1
        if (@($cases | ForEach-Object { $_.id }) -contains [string]$option.value) { throw 'Source patterns select the same object twice.' }
        $cases.Add([pscustomobject]@{ kind='source'; id=[string]$option.value; name=[string]$option.text })
    }
    $manual = @($initial.placement_options | Where-Object { $manualIds -contains [string]$_.value -and [string]$_.text -match $ManualNamePattern } | Sort-Object value | Select-Object -First 1)
    if ($manual.Count -ne 1) { throw 'No existing authored placement matches the manual case.' }
    $cases.Add([pscustomobject]@{ kind='manual'; id=[string]$manual[0].value; name=[string]$manual[0].text })
    if ($PlantPoint.Count -eq 2) {
        [void](Invoke-UiClick -Socket $socket -Text 'Nature' -Selector '[data-authoring-tab-button]')
        [void](Open-DetailsBySummary -Socket $socket -Summary "Plantes importées")
        if ($OverviewCamera) {
            $overviewCameraBefore = Wait-CommonPublicCamera -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds
            [void](Invoke-UiClick -Socket $socket -Text "Vue d’ensemble" -Selector 'button' -ScopeSelector '[aria-label="Navigation de la vue"]')
            $overviewCameraAfter = Wait-CommonPublicCamera -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds
            $overviewCameraEvidence = [pscustomobject]@{
                requested = $true; control = "Vue d’ensemble"; before = $overviewCameraBefore; after = $overviewCameraAfter
                source_mutation = $false; id_injected = $false
            }
        }
        if ($PlantCameraOrbitPixels -ne 0) {
            $plantCameraOrbitEvidence = Invoke-CommonPlantCameraOrbit -Socket $socket -DeltaPixels $PlantCameraOrbitPixels -TimeoutSeconds $InteractionTimeoutSeconds
        }
        [void](Invoke-UiClick -Socket $socket -Text 'Sélectionner une plante dans la vue')
        [void](Wait-CdpReady -Socket $socket -Description 'Plant GPU picker armed through its visible control' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => {const norm=v=>(v??'').replace(/\s+/g,' ').trim();const details=[...document.querySelectorAll('summary')].find(s=>norm(s.textContent)==="Plantes importées")?.closest('details');const status=[...(details?.querySelectorAll('output')??[])].map(n=>norm(n.textContent)).find(Boolean)??'';
  return {ready:status.startsWith('Cliquer une plante visible'),terminal:status.startsWith('Sélection de plante indisponible')||status.startsWith('Carte végétale importée absente'),reason:status};})()
'@)
        $cameraBefore = Wait-CommonPublicCamera -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds
        $canvas = Get-CanvasGeometry -Socket $socket
        $clickX = $canvas.left + $canvas.width * $PlantPoint[0]
        $clickY = $canvas.top + $canvas.height * $PlantPoint[1]
        $pointLiteral = ConvertTo-CompactJson -Value @{x=$clickX;y=$clickY}
        $exposed = Invoke-CdpExpression -Socket $socket -Expression "(() => {const point=$pointLiteral;const canvas=document.getElementById('forge-authoring-canvas');return document.elementFromPoint(point.x,point.y)===canvas;})()"
        if (-not $exposed) { throw 'The inspected GPU plant click point is obscured by another DOM element.' }
        Send-CdpMouseClick -Socket $socket -X $clickX -Y $clickY
        $plant = Wait-CdpReady -Socket $socket -Description 'Real GPU plant ID in common inspector' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => { const norm=v=>(v??'').replace(/\s+/g,' ').trim();const details=[...document.querySelectorAll('summary')].find(s=>norm(s.textContent)==="Plantes importées")?.closest('details');const status=[...(details?.querySelectorAll('output')??[])].map(n=>norm(n.textContent)).find(Boolean)??'';
  const marker=[...(details?.querySelectorAll('p')??[])].map(node=>norm(node.textContent)).find(value=>/^Plante : [0-9a-f-]{36}$/.test(value));
  const id=marker?.slice(9)??''; const select=[...document.querySelectorAll('label')].find(node=>norm(node.textContent).startsWith('Objet placé à inspecter'))?.querySelector('select');
  const terminal=/^(Sélection de plante refusée|Sélection GPU impossible|Sélection GPU expirée|Aucune plante visible)/.test(status);
  return {ready:!!id&&select?.value===id&&status==='Plante sélectionnée : '+id+'. Carte intacte.',terminal,reason:status||marker||'GPU pick pending',gpu_status:status,id,name:select?.selectedOptions[0]?.textContent??''}; })()
'@
        # Keep a successful real GPU readback visible even if a later camera check fails closed.
        $plantStatus = 'GPU pick observed; common lifecycle pending'
        $plantPickEvidence = [pscustomobject]@{normalized_point=$PlantPoint;pixel_point=@{x=$clickX;y=$clickY};before=$cameraBefore;after=$null;camera_comparison=$null;transformer_inspector=$null;gpu_status=$plant.gpu_status;selected_id=$plant.id;engine_camera_key_validated=$true;id_injected=$false;lifecycle_repick=$null}
        $plantPickEvidence.transformer_inspector = Wait-CommonPlantTransformerInspector -Socket $socket -Id ([string]$plant.id) -Name ([string]$plant.name) -TimeoutSeconds $InteractionTimeoutSeconds
        $cameraAfter = Wait-CommonPublicCamera -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds
        $plantPickEvidence.after = $cameraAfter
        $plantPickEvidence.camera_comparison = Compare-CommonPublicCamera -Before $cameraBefore -After $cameraAfter
        if (-not $plantPickEvidence.camera_comparison.equivalent) { throw "Public camera changed during the actual GPU plant pick: $($plantPickEvidence.camera_comparison.reason)." }
        $cases.Add([pscustomobject]@{ kind='plant_gpu'; id=[string]$plant.id; name=[string]$plant.name })
    }
    foreach ($case in $cases) {
        if ($case.kind -ceq 'plant_gpu') {
            # Unedited, non-current plants are intentionally absent from the bounded common choices.
            # Reintroduce this plant through the same public physical GPU pick before its lifecycle.
            $repickBefore = Get-WorldState -Socket $socket
            $repickPins = @(@{path=$candidate;sha256=(Get-NormalizedSha256 -Value $ExpectedSha256)}, @{path=$wasmPath;sha256=$wasmBefore}) + @($frontendPins)
            foreach ($entry in @($sourceSnapshots) + @($cloneSnapshots)) { $repickPins += @{path=$entry.path;sha256=$entry.snapshot.Sha256} }
            $repickPins += @{path=$workspaceArchive;sha256=(Get-FileSha256 -LiteralPath $workspaceArchive)}
            $repickPins = @(foreach ($pin in $repickPins) { [pscustomobject]@{path=$pin.path;expected_sha256=$pin.sha256;before_sha256=(Get-FileSha256 -LiteralPath $pin.path);after_sha256=$null} })
            $repick = [pscustomobject]@{status='GPU lifecycle repick pending';normalized_point=$PlantPoint;pixel_point=$null;expected_id=$case.id;selected_id=$null;gpu_status=$null;before=$null;after=$null;original_camera_comparison=$null;original_viewport_equal=$false;camera_comparison=$null;transformer_inspector=$null;audit_before=$repickBefore.audit_counts;audit_after=$null;mutation_ipc_unchanged=$false;preview_count_unchanged=$false;pins=$repickPins;engine_camera_key_validated=$false;id_injected=$false}
            $plantPickEvidence.lifecycle_repick = $repick
            if (-not [bool]$repickBefore.diff_hidden -or [int]$repickBefore.audit_pending -ne 0) { throw 'Plant lifecycle repick requires a clean preview and idle audited IPC.' }
            foreach ($pin in $repickPins) { if ($pin.before_sha256 -cne $pin.expected_sha256) { throw "Plant lifecycle repick pin changed before selection: $($pin.path)." } }
            [void](Invoke-UiClick -Socket $socket -Text 'Nature' -Selector '[data-authoring-tab-button]')
            [void](Open-DetailsBySummary -Socket $socket -Summary 'Plantes importées')
            [void](Invoke-UiClick -Socket $socket -Text 'Sélectionner une plante dans la vue')
            [void](Wait-CdpReady -Socket $socket -Description 'Plant lifecycle GPU picker armed through its visible control' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => {const norm=v=>(v??'').replace(/\s+/g,' ').trim();const details=[...document.querySelectorAll('summary')].find(s=>norm(s.textContent)==="Plantes importées")?.closest('details');const status=[...(details?.querySelectorAll('output')??[])].map(n=>norm(n.textContent)).find(Boolean)??'';
  return {ready:status.startsWith('Cliquer une plante visible'),terminal:status.startsWith('Sélection de plante indisponible')||status.startsWith('Carte végétale importée absente'),reason:status};})()
'@)
            $repick.before = Wait-CommonPublicCamera -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds
            $repick.original_camera_comparison = Compare-CommonPublicCamera -Before $plantPickEvidence.before -After $repick.before
            $repick.original_viewport_equal = $plantPickEvidence.before.viewport_size.width -eq $repick.before.viewport_size.width -and $plantPickEvidence.before.viewport_size.height -eq $repick.before.viewport_size.height
            if (-not $repick.original_camera_comparison.equivalent -or -not $repick.original_viewport_equal) { throw 'Plant lifecycle repick did not restore the original camera and public canvas viewport.' }
            $canvas = Get-CanvasGeometry -Socket $socket
            $clickX = $canvas.left + $canvas.width * $PlantPoint[0]
            $clickY = $canvas.top + $canvas.height * $PlantPoint[1]
            $repick.pixel_point = @{x=$clickX;y=$clickY}
            $pointLiteral = ConvertTo-CompactJson -Value $repick.pixel_point
            $exposed = Invoke-CdpExpression -Socket $socket -Expression "(() => {const point=$pointLiteral;const canvas=document.getElementById('forge-authoring-canvas');return document.elementFromPoint(point.x,point.y)===canvas;})()"
            if (-not $exposed) { throw 'The original lifecycle GPU plant click point is obscured by another DOM element.' }
            Send-CdpMouseClick -Socket $socket -X $clickX -Y $clickY
            $repickedPlant = Wait-CdpReady -Socket $socket -Description 'Real GPU plant ID restored before its common lifecycle' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => { const norm=v=>(v??'').replace(/\s+/g,' ').trim();const details=[...document.querySelectorAll('summary')].find(s=>norm(s.textContent)==="Plantes importées")?.closest('details');const status=[...(details?.querySelectorAll('output')??[])].map(n=>norm(n.textContent)).find(Boolean)??'';
  const marker=[...(details?.querySelectorAll('p')??[])].map(node=>norm(node.textContent)).find(value=>/^Plante : [0-9a-f-]{36}$/.test(value));
  const id=marker?.slice(9)??''; const select=[...document.querySelectorAll('label')].find(node=>norm(node.textContent).startsWith('Objet placé à inspecter'))?.querySelector('select');
  const terminal=/^(Sélection de plante refusée|Sélection GPU impossible|Sélection GPU expirée|Aucune plante visible)/.test(status);
  return {ready:!!id&&select?.value===id&&status==='Plante sélectionnée : '+id+'. Carte intacte.',terminal,reason:status||marker||'GPU pick pending',gpu_status:status,id,name:select?.selectedOptions[0]?.textContent??''}; })()
'@
            $repick.status = 'GPU lifecycle repick observed'
            $repick.selected_id = [string]$repickedPlant.id
            $repick.gpu_status = $repickedPlant.gpu_status
            $repick.engine_camera_key_validated = $true
            if ($repick.selected_id -cne [string]$case.id -or [string]$repickedPlant.name -cne [string]$case.name) { throw 'Physical plant lifecycle repick selected a different original identity or label.' }
            $repick.transformer_inspector = Wait-CommonPlantTransformerInspector -Socket $socket -Id ([string]$case.id) -Name ([string]$case.name) -TimeoutSeconds $InteractionTimeoutSeconds
            $repick.after = Wait-CommonPublicCamera -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds
            $repick.camera_comparison = Compare-CommonPublicCamera -Before $repick.before -After $repick.after
            $repickAfter = Get-WorldState -Socket $socket
            $repick.audit_after = $repickAfter.audit_counts
            foreach ($pin in $repickPins) { $pin.after_sha256 = Get-FileSha256 -LiteralPath $pin.path }
            Assert-NoMutationIpcDelta -Before $repickBefore -After $repickAfter -Description 'Physical plant lifecycle repick without mutation IPC'
            $repick.mutation_ipc_unchanged = $true
            $repick.preview_count_unchanged = [int]$repickBefore.audit_counts.preview_zone -eq [int]$repickAfter.audit_counts.preview_zone
            if (-not $repick.preview_count_unchanged -or -not [bool]$repickAfter.diff_hidden -or [int]$repickAfter.audit_pending -ne 0 -or [string]$repickAfter.placement_value -cne [string]$case.id) { throw 'Physical plant lifecycle repick changed the preview, audited IPC or selected identity.' }
            foreach ($pin in $repickPins) { if ($pin.after_sha256 -cne $pin.before_sha256) { throw "Plant lifecycle repick changed an immutable pin: $($pin.path)." } }
            if (-not $repick.camera_comparison.equivalent) { throw "Public camera changed during the lifecycle GPU plant repick: $($repick.camera_comparison.reason)." }
            $repick.status = 'passed - original plant restored through physical GPU pick'
        }
        $editedCases.Add((Invoke-CommonTransformCase -Socket $socket -Case $case -TimeoutSeconds $InteractionTimeoutSeconds))
    }
    $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'common-edited.png')))
    $savedEdited = Save-CommonWorkspace -Socket $socket -WorkspaceArchive $workspaceArchive -ObjectRoot $objectRoot -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds
    if ($null -ne $plantMetadataBefore -and (ConvertTo-CompactJson -Value $savedEdited.head.base.vegetation.imported_source) -cne $plantMetadataBefore) { throw 'Common plant editing changed the immutable source block metadata or parameter names.' }
    foreach ($case in $editedCases) {
        foreach ($expected in @($case.edited,$case.copy)) {
            $savedObject = Get-CommonCorrectionObject -Corrections $savedEdited.head.corrections -Id ([string]$expected.id)
            if ($null -eq $savedObject -or -not (Test-StandPlacementEquivalent -Left $savedObject -Right $expected)) { throw 'Saved transform, identity or asset pin differs from common inspector proposal.' }
        }
    }
    Reopen-CommonWorkspaceZone -Socket $socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds
    $reopened = Wait-WorldState -Socket $socket -Description 'Saved original and copy choices restored' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
        param($state)
        foreach ($case in $editedCases) {
            if (@(Get-CommonPlacementOptionValues -State $state) -notcontains $case.id -or @(Get-CommonPlacementOptionValues -State $state) -notcontains [string]$case.copy.id) { return $false }
        }
        return [int]$state.audit_pending -eq 0
    }
    foreach ($case in $editedCases) {
        foreach ($expected in @($case.edited,$case.copy)) {
            if (@(Get-CommonPlacementOptionValues -State $reopened) -notcontains [string]$expected.id) { throw 'Edited common object or its copy is absent after Save/Close/Open.' }
            Select-CommonObject -Socket $socket -Id ([string]$expected.id) -TimeoutSeconds $InteractionTimeoutSeconds -Advanced
            foreach ($entry in @(@('Échelle X','x'),@('Échelle Y','y'),@('Échelle Z','z'))) {
                $control = Get-UiControlByLabel -Socket $socket -Label $entry[0] -DetailsSummary 'Rotation et échelle précises'
                if ([math]::Abs([double]$control.value - [double]$expected.transform.scale.($entry[1])) -gt 0.0001) { throw 'Reopened common inspector scale differs from saved transform.' }
            }
        }
    }
    if (-not (Test-ArchiveSnapshotsEqual -Before $savedEdited.workspace -After (Get-ArchiveSnapshot -LiteralPath $workspaceArchive))) { throw 'Reopening altered the saved workspace.' }
    foreach ($case in $editedCases) {
        [void](Invoke-CommonDeletion -Socket $socket -Id $case.id -TimeoutSeconds $InteractionTimeoutSeconds)
        $deletion = Invoke-CommonDeletion -Socket $socket -Id ([string]$case.copy.id) -TimeoutSeconds $InteractionTimeoutSeconds
        [void](Invoke-CommonUndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $InteractionTimeoutSeconds)
        [void](Wait-WorldState -Socket $socket -Description 'Undo restores last copy without restoring removed source' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
            param($state) @(Get-CommonPlacementOptionValues -State $state) -contains [string]$case.copy.id -and @(Get-CommonPlacementOptionValues -State $state) -notcontains $case.id
        })
        [void](Invoke-CommonUndoRedoKey -Socket $socket -Action Redo -TimeoutSeconds $InteractionTimeoutSeconds)
        [void](Wait-WorldState -Socket $socket -Description 'Redo removes last copy without source resurrection' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
            param($state) @(Get-CommonPlacementOptionValues -State $state) -notcontains [string]$case.copy.id -and @(Get-CommonPlacementOptionValues -State $state) -notcontains $case.id
        })
        $deletions.Add([pscustomobject]@{ id=$case.id; copy_id=[string]$case.copy.id; request_sha256=$deletion.request_sha256; undo_redo=$true })
    }
    $savedRemoved = Save-CommonWorkspace -Socket $socket -WorkspaceArchive $workspaceArchive -ObjectRoot $objectRoot -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds
    Assert-CommonRemovalRecords -Corrections $savedRemoved.head.corrections -Cases @($editedCases)
    if ($null -ne $plantMetadataBefore -and (ConvertTo-CompactJson -Value $savedRemoved.head.base.vegetation.imported_source) -cne $plantMetadataBefore) { throw 'Removing edited plants changed the immutable source block metadata.' }
    $removedIds = @($editedCases | ForEach-Object {$_.id;[string]$_.copy.id})
    $remainingIds = @(Get-CommonPlacementOptionValues -State $initial | Where-Object { $removedIds -notcontains [string]$_ })
    Reopen-CommonWorkspaceZone -Socket $socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds
    $final = Wait-WorldState -Socket $socket -Description 'Source choices loaded after final reopen' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
        param($state)
        foreach ($id in $remainingIds) { if (@(Get-CommonPlacementOptionValues -State $state) -notcontains $id) { return $false } }
        return [int]$state.audit_pending -eq 0
    }
    foreach ($case in $editedCases) {
        foreach ($id in @($case.id,[string]$case.copy.id)) {
            if (@(Get-CommonPlacementOptionValues -State $final) -contains $id -or $null -ne (Get-CommonCorrectionObject -Corrections $savedRemoved.head.corrections -Id $id)) { throw 'Deleted original or last copy resurrected after Save/Close/Open.' }
        }
    }
    if (-not (Test-ArchiveSnapshotsEqual -Before $savedRemoved.workspace -After (Get-ArchiveSnapshot -LiteralPath $workspaceArchive)) -or -not (Test-ArchiveSnapshotsEqual -Before $savedRemoved.snapshot -After (Get-ArchiveSnapshot -LiteralPath $savedRemoved.path))) { throw 'Final reopen altered the saved checkpoint.' }
    if ($PlantPoint.Count -eq 2) { $plantStatus = 'passed - real GPU pick and common lifecycle persisted' }
    $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'common-removed-reopened.png')))
}
catch {
    $failure = $_.Exception.Message
    [IO.File]::WriteAllText((Join-Path $output 'failure-detail.txt'),
        (($_ | Out-String) + [Environment]::NewLine + $_.ScriptStackTrace), [Text.UTF8Encoding]::new($false))
    if ($null -ne $socket -and $socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
        try { $failureState = Get-WorldState -Socket $socket } catch { Write-Verbose $_.Exception.Message }
        try { $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'common-failure.png'))) } catch { Write-Warning $_.Exception.Message }
    }
}
finally {
    if ($null -ne $socket) {
        if ($socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
            try { [void](Restore-WorldAudit -Socket $socket) } catch { Write-Warning $_.Exception.Message }
            try {
                $closing = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(2))
                try { Wait-NonGenericTask -Task ($socket.CloseAsync([Net.WebSockets.WebSocketCloseStatus]::NormalClosure,'Common placeable smoke complete',$closing.Token)) } finally { $closing.Dispose() }
            } catch { Write-Verbose $_.Exception.Message }
        }
        $socket.Dispose()
    }
    if ($null -ne $process -and -not $process.HasExited) {
        # Attest this exact owned PID again; never stop a browser or another Studio by name.
        try { [void](Wait-LaunchedExecutableIdentity -Process $process -Candidate $candidate -ExpectedHash (Get-NormalizedSha256 -Value $ExpectedSha256)); [void]$process.CloseMainWindow() } catch { Write-Warning $_.Exception.Message }
        if (-not $process.WaitForExit(5000)) { Stop-Process -Id $process.Id -Force; [void]$process.WaitForExit(5000) }
    }
    if ($null -ne $reservation) { $reservation.Listener.Stop() }
    if ($port -gt 0) { $cleanupClosed = Wait-CdpListenerClosed -Port $port -TimeoutSeconds 10 }
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name,$savedEnvironment[$name],'Process') }
}
$sourcePreserved = $true
foreach ($entry in @($sourceSnapshots) + @($cloneSnapshots)) {
    if (-not (Test-ArchiveSnapshotsEqual -Before $entry.snapshot -After (Get-ArchiveSnapshot -LiteralPath $entry.path))) { $sourcePreserved = $false }
}
if (-not $sourcePreserved) { $failure = "Source or original immutable object changed. $failure" }
if (-not $cleanupClosed) { $failure = "Owned CDP listener did not close. $failure" }
if ((Get-FileSha256 -LiteralPath $candidate) -cne (Get-NormalizedSha256 -Value $ExpectedSha256) -or (Get-FileSha256 -LiteralPath $wasmPath) -cne $wasmBefore) { $failure = "Candidate executable or frontend WASM changed during the smoke. $failure" }
$frontendPreserved = $true
foreach ($pin in $frontendPins) {
    if (-not (Test-Path -LiteralPath $pin.path -PathType Leaf) -or (Get-FileSha256 -LiteralPath $pin.path) -cne $pin.sha256) { $frontendPreserved = $false }
}
if (@(Get-ChildItem -LiteralPath $webRoot -Recurse -Force -File).Count -ne $frontendPins.Count) { $frontendPreserved = $false }
if (-not $frontendPreserved) { $failure = "Candidate frontend or pinned assets changed during the smoke. $failure" }
$ledger = [ordered]@{
    schema='forge.common-placeable-studio-smoke'; version=1
    status=if ([string]::IsNullOrWhiteSpace($failure)) { 'passed' } else { 'failed_closed' }
    failure=$failure; gate_qualified=$false; human_visual_review=$false; playtest='NOT RUN - bounded editing proof'
    isolation_root=$taskRoot; candidate=$candidate; candidate_sha256=(Get-FileSha256 -LiteralPath $candidate)
    frontend_wasm=$wasmPath; frontend_wasm_sha256=$wasmBefore
    frontend_preserved=$frontendPreserved; frontend_pins=$frontendPins
    source_fixture=$source; source_preserved=$sourcePreserved; zone_uuid=$zoneId; owned_process_id=if($null -ne $process){$process.Id}else{$null}
    cdp_listener_closed=$cleanupClosed; cases=@($editedCases); deletions=@($deletions); plant=$plantStatus
    overview_camera_requested=[bool]$OverviewCamera; overview_camera=$overviewCameraEvidence
    managed_zone_opens=@($script:managedOpenEvidence)
    plant_camera_orbit_pixels=[int]$PlantCameraOrbitPixels; plant_camera_orbit=$plantCameraOrbitEvidence
    plant_gpu_pick=$plantPickEvidence; plant_source_metadata_preserved=if($null -ne $plantMetadataBefore){$sourcePreserved -and $null -ne $savedEdited -and $null -ne $savedRemoved}else{$null}
    edited_checkpoint=if($null -ne $savedEdited){$savedEdited.snapshot}else{$null}; removed_checkpoint=if($null -ne $savedRemoved){$savedRemoved.snapshot}else{$null}
    screenshots=@($screenshots); failure_state=$failureState
    limitations=@('No playtest traversal or GPU performance measurement.','Screenshots require human visual inspection.','Plant GPU selection is NOT RUN unless inspected PlantPoint coordinates are supplied.')
}
$ledgerPath = Join-Path $output 'common-placeable-studio-ledger.json'
[IO.File]::WriteAllText($ledgerPath,($ledger | ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))
if ($ledger.status -cne 'passed') { throw "Common placeable Studio smoke failed closed: $failure. Ledger: $ledgerPath" }
Write-Output "COMMON_PLACEABLE_STUDIO_PASS ledger=$ledgerPath plant=$plantStatus"
