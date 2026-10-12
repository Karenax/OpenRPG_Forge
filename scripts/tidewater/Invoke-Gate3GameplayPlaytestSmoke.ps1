#Requires -Version 7.2
# Gate 3 Tidewater gameplay playtest smoke.  This is a bounded public-UI
# exercise and never a Gate qualification verdict.
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

    [Parameter(ParameterSetName = 'Run')]
    [guid]$ZoneUuid = 'e620dd19-ead9-5913-a6d6-0f93109148c0',

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(15, 180)]
    [int]$StartupTimeoutSeconds = 60,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(30, 300)]
    [int]$InteractionTimeoutSeconds = 120,

    [Parameter(ParameterSetName = 'Run')]
    [switch]$VerifyResumeInitialCameraDirection,

    [Parameter(ParameterSetName = 'Run')]
    [switch]$VisitInteriorBeforeSave,

    [Parameter(ParameterSetName = 'Run')]
    [switch]$UseFinalPhysicsApproach,

    [Parameter(ParameterSetName = 'Run')]
    [switch]$WaitForReadyBeforeInitialFocus,

    [Parameter(ParameterSetName = 'Run')]
    [switch]$UseGuardianContactPulses,

    [Parameter(ParameterSetName = 'Run')]
    [switch]$VisitInteriorBeforeQuest,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$playtestHarnessHashAtStart = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()

# The Gate 2 helper has a parameter named Candidate and a strict parameter
# set.  Preserve this script's binding before dot-sourcing its CDP/fixture
# helpers; restoring null Run-only values during -SelfTest would retrigger
# validation in PowerShell.
$requestedSelfTest = [bool]$SelfTest
$requestedCandidate = $Candidate
$requestedFixture = $FreshIsolatedFixture
$requestedAuthoringLedger = $AuthoringLedger
$requestedSourcePin = $SourceOriginalHashPinned
$requestedEvidenceRoot = $EvidenceRoot
$requestedZoneUuid = $ZoneUuid
$requestedStartupTimeout = $StartupTimeoutSeconds
$requestedInteractionTimeout = $InteractionTimeoutSeconds
$requestedVerifyResumeInitialCameraDirection = [bool]$VerifyResumeInitialCameraDirection
$requestedVisitInteriorBeforeSave = [bool]$VisitInteriorBeforeSave
$requestedUseFinalPhysicsApproach = [bool]$UseFinalPhysicsApproach
$requestedWaitForReadyBeforeInitialFocus = [bool]$WaitForReadyBeforeInitialFocus
$requestedUseGuardianContactPulses = [bool]$UseGuardianContactPulses
$requestedVisitInteriorBeforeQuest = [bool]$VisitInteriorBeforeQuest
. (Join-Path $PSScriptRoot '..\Invoke-Gate2WorldEditingProof.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Gate3ProofStorage.ps1')
$playtestNavigationReaderPath = Join-Path $PSScriptRoot 'Gate3NavigationEvidence.ps1'
$playtestNavigationReaderHashAtStart = (Get-FileHash -LiteralPath $playtestNavigationReaderPath -Algorithm SHA256).Hash.ToLowerInvariant()
. $playtestNavigationReaderPath
if (-not $requestedSelfTest) {
    $Candidate = $requestedCandidate
    $FreshIsolatedFixture = $requestedFixture
    $AuthoringLedger = $requestedAuthoringLedger
    $SourceOriginalHashPinned = $requestedSourcePin
    $EvidenceRoot = $requestedEvidenceRoot
    $ZoneUuid = $requestedZoneUuid
    $StartupTimeoutSeconds = $requestedStartupTimeout
    $InteractionTimeoutSeconds = $requestedInteractionTimeout
    $VerifyResumeInitialCameraDirection = $requestedVerifyResumeInitialCameraDirection
    $VisitInteriorBeforeSave = $requestedVisitInteriorBeforeSave
    $UseFinalPhysicsApproach = $requestedUseFinalPhysicsApproach
    $WaitForReadyBeforeInitialFocus = $requestedWaitForReadyBeforeInitialFocus
    $UseGuardianContactPulses = $requestedUseGuardianContactPulses
    $VisitInteriorBeforeQuest = $requestedVisitInteriorBeforeQuest
}

$zoneId = 'e620dd19-ead9-5913-a6d6-0f93109148c0'
$gameplaySectionSelector = 'section.authoring-gameplay-controls'
$canonicalProbeRelativePath = 'navigation-canonical-001/canonical-terrain-probe.v2.json'
$canonicalProbeSha256 = 'e0ff230b364e7c650acc5a8a12f81e62cacdcdca9ebcb005d29a7975bf7ebd45'
$canonicalProbeSourceHash = 'f05a68acc294b586940237565c77fe2f7679ae9d2b77f4596f0380680665d50f'
$canonicalProbeGeometryHash = '4d3c185bcc9e10fad557848e2dd6cf279b03e11bba8931f7cb3d1a10df19ae9d'
$canonicalRouteWaypoints = @(
    [pscustomobject]@{ x = 40.0; z = -118.0 },
    [pscustomobject]@{ x = 30.25; z = -247.975 },
    [pscustomobject]@{ x = 30.075; z = -248.325 },
    [pscustomobject]@{ x = 16.0; z = -270.0 }
)
# The terrain probe predates the authored village NPC at its first point.
# Walk around that live capsule, then rejoin the exact first subdivided
# waypoint of the original polyline; keep both paths in the ledger.
$firstDx = $canonicalRouteWaypoints[1].x - $canonicalRouteWaypoints[0].x
$firstDz = $canonicalRouteWaypoints[1].z - $canonicalRouteWaypoints[0].z
$firstSteps = [int][Math]::Ceiling([Math]::Sqrt($firstDx*$firstDx + $firstDz*$firstDz) / 3.0)
$playtestRouteWaypoints = @(
    [pscustomobject]@{ x = 38.5; z = -117.0 },
    [pscustomobject]@{ x = 38.5; z = -120.0 },
    [pscustomobject]@{ x = $canonicalRouteWaypoints[0].x + $firstDx/$firstSteps; z = $canonicalRouteWaypoints[0].z + $firstDz/$firstSteps }
) + @($canonicalRouteWaypoints | Select-Object -Skip 1)
$script:CdpPlaytestMessageId = 0

function Open-Gate3PlaytestZone {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$ZoneId,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )

    $zoneLiteral = ConvertTo-CompactJson -Value ([guid]::Parse($ZoneId).ToString())
    [void](Invoke-UiClick -Socket $Socket -Text 'Modules' -Selector '[data-authoring-tab-button="project"]')
    $browser = Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'managed module and map selectors' -Expression @'
(() => {
  const browsers=[...document.querySelectorAll('.module-map-browser')];
  const modules=[...document.querySelectorAll('.module-map-browser select[aria-label="Module"]')];
  const maps=[...document.querySelectorAll('.module-map-browser select[aria-label="Carte"]')];
  if(browsers.length>1||modules.length>1||maps.length>1) return {ready:false,terminal:true,reason:'ambiguous module or map selectors'};
  const node=modules[0], allOptions=[...(node?.options??[])].filter(option=>option.value!==''), options=allOptions.filter(option=>!option.disabled);
  const error=document.querySelector('.module-map-browser [role="alert"]')?.textContent?.trim()??'';
  if(new Set(allOptions.map(option=>option.value)).size!==allOptions.length) return {ready:false,terminal:true,reason:'duplicate module UUID options'};
  return {ready:!error&&browsers.length===1&&browsers[0].getAttribute('aria-busy')==='false'&&modules.length===1&&maps.length===1&&!node.disabled&&options.length>0,terminal:!!error,
    reason:error||'module options='+options.length,modules:options.map(option=>({value:option.value,text:option.textContent??''}))};
})()
'@
    $mapListExpression = @'
(async () => {
  const zone=__GATE3_ZONE__, module=__GATE3_MODULE__, norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const browsers=[...document.querySelectorAll('.module-map-browser')];
  const modules=[...document.querySelectorAll('.module-map-browser select[aria-label="Module"]')];
  const maps=[...document.querySelectorAll('.module-map-browser select[aria-label="Carte"]')];
  if(browsers.length!==1||modules.length!==1||maps.length!==1) return {ready:false,terminal:true,reason:'expected one module browser, module and map selector'};
  const initialError=norm(browsers[0].querySelector('[role="alert"]')?.textContent);
  if(initialError) return {ready:false,terminal:true,reason:initialError};
  if(browsers[0].getAttribute('aria-busy')!=='false'||modules[0].value!==module||modules[0].disabled) return {ready:false,terminal:false,reason:'module list is loading or selection is pending'};
  const snapshot=()=>JSON.stringify({module:modules[0].value,map:maps[0].value,disabled:maps[0].disabled,options:[...maps[0].options].map(option=>({value:option.value,disabled:option.disabled}))});
  const before=snapshot();
  await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
  if(!browsers[0].isConnected||!modules[0].isConnected||!maps[0].isConnected) return {ready:false,terminal:false,reason:'module selectors remounted'};
  const options=[...maps[0].options].filter(option=>option.value!==''), matches=options.filter(option=>option.value===zone);
  if(new Set(options.map(option=>option.value)).size!==options.length) return {ready:false,terminal:true,reason:'duplicate map UUID options'};
  const error=norm(browsers[0].querySelector('[role="alert"]')?.textContent);
  const empty=[...browsers[0].querySelectorAll('p')].some(node=>norm(node.textContent)==='Ce module ne contient aucune carte.');
  const aligned=options.length>0?!maps[0].disabled:maps[0].disabled&&empty;
  return {ready:!error&&browsers[0].getAttribute('aria-busy')==='false'&&modules[0].value===module&&!modules[0].disabled&&before===snapshot()&&aligned,terminal:!!error,
    reason:error||'exact map matches='+matches.length,matches:matches.length,disabled:matches.length===1?matches[0].disabled:false};
})()
'@
    $matchingModules = @()
    foreach ($module in @($browser.modules)) {
        [void](Select-UiOptionByLabel -Socket $Socket -Label 'Module' -Value ([string]$module.value) -TimeoutSeconds ([Math]::Min(180, $TimeoutSeconds)))
        $moduleLiteral = ConvertTo-CompactJson -Value ([string]$module.value)
        $listed = Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'exact managed map UUID under selected module' -Expression ($mapListExpression.Replace('__GATE3_ZONE__', $zoneLiteral).Replace('__GATE3_MODULE__', $moduleLiteral))
        if ($listed.matches -eq 1) {
            if ($listed.disabled) { throw 'The exact managed map UUID option is disabled.' }
            $matchingModules += [string]$module.value
        }
    }
    if ($matchingModules.Count -ne 1) { throw "Expected the exact managed map UUID in one module, found $($matchingModules.Count)." }
    [void](Select-UiOptionByLabel -Socket $Socket -Label 'Module' -Value $matchingModules[0] -TimeoutSeconds ([Math]::Min(180, $TimeoutSeconds)))
    $selectedModuleLiteral = ConvertTo-CompactJson -Value $matchingModules[0]
    $listed = Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'selected module map list ready before opening' -Expression ($mapListExpression.Replace('__GATE3_ZONE__', $zoneLiteral).Replace('__GATE3_MODULE__', $selectedModuleLiteral))
    if ($listed.matches -ne 1 -or $listed.disabled) { throw 'The exact managed map UUID is unavailable after module reselection.' }
    # Carte resets its displayed value until native OpenManaged succeeds. Emit
    # one ordinary UI change, then await the accepted zone instead of retrying it.
    $selection = Invoke-CdpExpression -Socket $Socket -Expression @"
(async () => {
  const zone=$zoneLiteral, module=$selectedModuleLiteral;
  await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
  const browsers=[...document.querySelectorAll('.module-map-browser')];
  const modules=[...document.querySelectorAll('.module-map-browser select[aria-label="Module"]')];
  const maps=[...document.querySelectorAll('.module-map-browser select[aria-label="Carte"]')];
  if(browsers.length!==1||browsers[0].getAttribute('aria-busy')!=='false'||browsers[0].querySelector('[role="alert"]')||modules.length!==1||maps.length!==1||modules[0].value!==module||modules[0].disabled) return {ready:false,reason:'selected module or map selector changed or is busy'};
  const node=maps[0], matches=[...node.options].filter(option=>option.value===zone);
  if(matches.length!==1||matches[0].disabled||node.disabled) return {ready:false,reason:'exact map option unavailable or ambiguous'};
  node.scrollIntoView({block:'center',inline:'center'});
  await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
  const rect=node.getBoundingClientRect(), style=getComputedStyle(node);
  const hit=document.elementFromPoint(rect.left+rect.width/2,rect.top+rect.height/2);
  const visible=rect.width>0&&rect.height>0&&style.visibility!=='hidden'&&style.display!=='none';
  const unobscured=hit===node||!!hit&&node.contains(hit);
  if(!visible||!unobscured) return {ready:false,reason:'map selector hidden or covered'};
  node.value=matches[0].value;
  node.dispatchEvent(new Event('change',{bubbles:true}));
  return {ready:true,reason:'one public map selection change dispatched',requested_zone:zone};
})()
"@
    if ($null -eq $selection -or $selection.ready -ne $true) { throw "Managed map selector was not publicly usable: $($selection.reason)" }
    [void](Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'managed Tidewater edit viewport' -Expression @"
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim(), zone=$zoneLiteral;
  const canvas=document.getElementById('forge-authoring-canvas');
  const browsers=[...document.querySelectorAll('.module-map-browser')];
  const maps=[...document.querySelectorAll('.module-map-browser select[aria-label="Carte"]')];
  if(browsers.length>1||maps.length>1) return {ready:false,terminal:true,reason:'ambiguous accepted zone identity'};
  const shown=maps[0]?.value??'';
  const zoneOpen=document.documentElement?.getAttribute('data-forge-zone-open')==='true';
  const mode=[...document.querySelectorAll('output')].map(node=>norm(node.textContent)).find(text=>text.startsWith('Mode '))??'';
  const error=browsers[0]?.querySelector('[role="alert"]')?.textContent?.trim()??'';
  return {ready:!error&&browsers.length===1&&maps.length===1&&browsers[0].getAttribute('aria-busy')==='false'&&shown===zone&&zoneOpen&&mode.startsWith('Mode édition')&&canvas?.clientWidth>0&&canvas?.clientHeight>0,terminal:!!error,reason:error||'zone='+shown+'; open='+zoneOpen+'; mode='+mode};
})()
"@)
}

function Get-PlaytestPublicState {
    param([Parameter(Mandatory)]$Socket)
    return Invoke-CdpExpression -Socket $Socket -Expression @"
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const root=document.querySelector('$gameplaySectionSelector');
  if(!root) return {present:false,status:'',input:'',key:'',actions:[],items:[],outputs:[],paragraphs:[],allButtons:[],feedback:'',feedback_count:0,feedback_error:'gameplay controls absent',ready:false,locomotion:null,locomotion_error:null};
  let locomotion=null, locomotion_error=null;
  const diagnostics=document.getElementById('forge-authoring-canvas')?.getAttribute('data-tidewater-live-shadows');
  if(diagnostics) {
    try { locomotion=JSON.parse(diagnostics)?.player_locomotion??null; }
    catch(error) { locomotion_error=String(error); }
  }
    const status=norm(document.querySelector('#authoring-playtest-status')?.textContent);
    const input=norm(document.querySelector('#studio-input-capture-status')?.textContent);
    const key=norm(document.querySelector('#studio-keyboard-event-status')?.textContent);
  const actions=[...root.querySelectorAll('.gameplay-actions button')].map(node=>({text:norm(node.textContent),disabled:!!node.disabled}));
  const items=[...root.querySelectorAll('details[open] li')].map(node=>norm(node.textContent));
  const outputs=[...root.querySelectorAll('output')].map(node=>norm(node.textContent)).filter(Boolean);
  const paragraphs=[...root.querySelectorAll('p')].map(node=>norm(node.textContent)).filter(Boolean);
  const allButtons=[...root.querySelectorAll('button')].map(node=>({text:norm(node.textContent),disabled:!!node.disabled}));
  const feedbackNodes=[...root.querySelectorAll(':scope > p[aria-live="polite"]')];
  const feedback=feedbackNodes.length===1?norm(feedbackNodes[0].textContent):'';
  const feedback_error=feedbackNodes.length===1?null:'expected one direct public action feedback paragraph, got '+feedbackNodes.length;
  return {present:true,status,input,key,actions,items,outputs,paragraphs,allButtons,feedback,feedback_count:feedbackNodes.length,feedback_error,ready:/simulation prête true/i.test(status),locomotion,locomotion_error};
})()
"@
}

function Convert-PlaytestNumber {
    param([Parameter(Mandatory)][string]$Text)
    return [double]::Parse($Text.Replace(',', '.'), [Globalization.CultureInfo]::InvariantCulture)
}

function Get-PlaytestObservation {
    param([Parameter(Mandatory)]$State)
    # This optional diagnostic is already published on the visible canvas.
    # Keep the rounded public status as the movement contract and journal the
    # raw source/Rapier state separately so a drift can be explained.
    $locomotion = if ($State -is [System.Collections.IDictionary]) {
        $State['locomotion']
    } else {
        $property = $State.PSObject.Properties['locomotion']
        if ($null -ne $property) { $property.Value } else { $null }
    }
    $raw = [string]$State.status
    $match = [regex]::Match($raw, 'position \(m\)\s+(?<x>[+-]?\d+(?:[.,]\d+)?),\s*(?<y>[+-]?\d+(?:[.,]\d+)?),\s*(?<z>[+-]?\d+(?:[.,]\d+)?)')
    if (-not $match.Success) {
        return [ordered]@{ ok = $false; raw = $raw; x = $null; y = $null; z = $null; grounded = $null; simulation_ready = $false; input_active = $false; yaw_degrees = $null; tick = $null; locomotion = $locomotion }
    }
    try {
        $x = Convert-PlaytestNumber -Text $match.Groups['x'].Value
        $y = Convert-PlaytestNumber -Text $match.Groups['y'].Value
        $z = Convert-PlaytestNumber -Text $match.Groups['z'].Value
    }
    catch {
        return [ordered]@{ ok = $false; raw = $raw; x = $null; y = $null; z = $null; grounded = $null; simulation_ready = $false; input_active = $false; yaw_degrees = $null; tick = $null; locomotion = $locomotion }
    }
    $groundedMatch = [regex]::Match($raw, 'au sol\s+(?<value>true|false)', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $yawMatch = [regex]::Match($raw, 'cap caméra \(°\)\s+(?<value>[+-]?\d+(?:[.,]\d+)?)', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $tickMatch = [regex]::Match($raw, 'Tick\s+(?<value>\d+)', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $ready = $raw -match 'simulation prête\s+true'
    $active = $raw -match 'entrée active\s+true'
    return [ordered]@{
        ok = $true
        raw = $raw
        x = $x
        y = $y
        z = $z
        grounded = if ($groundedMatch.Success) { $groundedMatch.Groups['value'].Value -ieq 'true' } else { $null }
        simulation_ready = $ready
        input_active = $active
        yaw_degrees = if ($yawMatch.Success) { Convert-PlaytestNumber -Text $yawMatch.Groups['value'].Value } else { $null }
        tick = if ($tickMatch.Success) { [int64]$tickMatch.Groups['value'].Value } else { $null }
        locomotion = $locomotion
    }
}

function Save-PlaytestPublicDiagnostic {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$EvidencePath,
        [Parameter(Mandatory)][int]$RunNumber,
        [Parameter(Mandatory)][string]$Name
    )
    $safeName = $Name -replace '[^A-Za-z0-9_-]', '_'
    $prefix = "run$RunNumber-$safeName"
    $jsonPath = Join-Path $EvidencePath "$prefix-ui.json"
    $screenshotPath = Join-Path $EvidencePath "$prefix.png"
    $payload = [ordered]@{
        schema = 'forge.tidewater.gate3-gameplay-playtest-ui-diagnostic'
        version = 1
        run = $RunNumber
        name = $Name
        captured_at_utc = [DateTimeOffset]::UtcNow.ToString('o')
        json_path = [IO.Path]::GetFullPath($jsonPath)
        screenshot_path = [IO.Path]::GetFullPath($screenshotPath)
        public_dom = $null
        public_observation = $null
        screenshot = $null
        error = $null
    }
    try {
        $payload.public_dom = Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const root=document.querySelector('section.authoring-gameplay-controls');
  const status=norm(document.querySelector('#authoring-playtest-status')?.textContent);
  const input=norm(document.querySelector('#studio-input-capture-status')?.textContent);
  const key=norm(document.querySelector('#studio-keyboard-event-status')?.textContent);
  let locomotion=null, locomotion_error=null;
  const diagnostics=document.getElementById('forge-authoring-canvas')?.getAttribute('data-tidewater-live-shadows');
  if(diagnostics) {
    try { locomotion=JSON.parse(diagnostics)?.player_locomotion??null; }
    catch(error) { locomotion_error=String(error); }
  }
  const canvas=document.getElementById('forge-authoring-canvas');
  const projectionIds=['forge-r2-050-projection-diagnostic','forge-r2-050-placement-projection-diagnostic','forge-r2-050-building-projection-diagnostic'];
  const projection_diagnostics=Object.fromEntries(projectionIds.map(id=>{
    const node=document.getElementById(id),raw=node?.textContent??null;
    let value=null;try{value=raw?JSON.parse(raw):null}catch(error){value={parse_error:String(error)}};
    return [id,{present:!!node,raw,value}];
  }));
  return {
    captured_at_utc:new Date().toISOString(),
    url:String(location.href),
    ready_state:document.readyState,
    pointer_lock:document.pointerLockElement?.id??null,
    active_element:document.activeElement?.id??document.activeElement?.tagName??null,
    root_present:!!root,
    status,input,key,locomotion,locomotion_error,projection_diagnostics,
    visibility_state:document.visibilityState,document_focused:document.hasFocus(),
    canvas_attributes:canvas?Object.fromEntries([...canvas.attributes].map(attr=>[attr.name,attr.value])):null,
    viewport:canvas?{css_width:canvas.clientWidth,css_height:canvas.clientHeight,render_width:canvas.width,render_height:canvas.height,device_pixel_ratio:window.devicePixelRatio}:null,
    all_outputs:[...document.querySelectorAll('output')].map(node=>({id:node.id,text:norm(node.textContent)})),
    actions:[...(root?.querySelectorAll('.gameplay-actions button')??[])].map(n=>({text:norm(n.textContent),disabled:!!n.disabled})),
    items:[...(root?.querySelectorAll('details[open] li')??[])].map(n=>norm(n.textContent)),
    outputs:[...(root?.querySelectorAll('output')??[])].map(n=>norm(n.textContent)).filter(Boolean),
    buttons:[...(root?.querySelectorAll('button')??[])].map(n=>({text:norm(n.textContent),disabled:!!n.disabled})),
    text:root?norm(root.textContent).slice(0,16000):'',
    outer_html:root?root.outerHTML.slice(0,200000):null
  };
})()
'@
        $payload.public_observation = Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $Socket)
        $payload.screenshot = Save-CdpScreenshot -Socket $Socket -LiteralPath $screenshotPath
    }
    catch {
        $payload.error = $_.Exception.Message
    }
    try {
        [IO.File]::WriteAllText($jsonPath, ($payload | ConvertTo-Json -Depth 40), [Text.UTF8Encoding]::new($false))
    }
    catch {
        $writeError = "Diagnostic JSON could not be saved: $($_.Exception.Message)"
        $payload.error = if ($payload.error) { "$($payload.error); $writeError" } else { $writeError }
    }
    return [pscustomobject]$payload
}

function Capture-PlaytestCheckpoint {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$EvidencePath,
        [Parameter(Mandatory)][int]$RunNumber,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Phase,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Diagnostics
    )
    $capture = Save-PlaytestPublicDiagnostic -Socket $Socket -EvidencePath $EvidencePath -RunNumber $RunNumber -Name $Name
    $dom = $capture.public_dom
    $summary = [ordered]@{
        schema = $capture.schema
        version = $capture.version
        run = $capture.run
        name = $capture.name
        captured_at_utc = $capture.captured_at_utc
        json_path = $capture.json_path
        screenshot_path = $capture.screenshot_path
        screenshot = $capture.screenshot
        error = $capture.error
        public_observation = $capture.public_observation
        status = if ($null -ne $dom) { [string]$dom.status } else { $null }
        input = if ($null -ne $dom) { [string]$dom.input } else { $null }
        key = if ($null -ne $dom) { [string]$dom.key } else { $null }
        actions = if ($null -ne $dom) { @($dom.actions) } else { @() }
        items = if ($null -ne $dom) { @($dom.items) } else { @() }
        outputs = if ($null -ne $dom) { @($dom.outputs) } else { @() }
    }
    [void]$Diagnostics.Add([pscustomobject]$summary)
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'public_ui_checkpoint' -Payload $summary
    return [pscustomobject]$summary
}

function Add-PlaytestJournal {
    param(
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)][string]$Event,
        [AllowNull()][object]$Payload
    )
    if ($Journal.Count -ge 4096) { throw 'Public playtest evidence journal quota exceeded; evidence is incomplete.' }
    if ($Journal.Count -lt 4096) {
        [void]$Journal.Add([ordered]@{
            phase = $Phase
            event = $Event
            utc = [DateTimeOffset]::UtcNow.ToString('o')
            payload = $Payload
        })
    }
}

function Wait-PlaytestReady {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [Parameter(Mandatory)][string]$Description
    )
    return Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description $Description -Expression @'
(() => {
  const status=(document.querySelector('#authoring-playtest-status')?.textContent??'').replace(/\s+/g,' ').trim();
  const root=document.querySelector('section.authoring-gameplay-controls');
  const terminal=/projection importée refusée|playtest.*erreur|simulation.*refusée/i.test(status);
  return {ready:!!root&&/simulation prête true/i.test(status),terminal,reason:status||'playtest status pending'};
})()
'@
}

function Focus-PlaytestCanvas {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [ValidateRange(1,200)][int]$PointerCapturePollMilliseconds = 200
    )
    $canvasExpression = @'
(() => {
  const node=document.getElementById('forge-authoring-canvas');
  if(!node) return {ready:false,terminal:false,reason:'canvas pending'};
  const rect=node.getBoundingClientRect(), style=getComputedStyle(node);
  const visible=rect.width>0&&rect.height>0&&style.visibility!=='hidden'&&style.display!=='none';
  const status=document.querySelector('#authoring-playtest-status')?.textContent??'';
  const capture=document.querySelector('#studio-input-capture-status')?.textContent??'';
  const points=[[.5,.5],[.65,.35],[.35,.35],[.65,.65],[.35,.65],[.85,.5],[.15,.5]];
  const observations=points.map(([fx,fy])=>{const x=rect.left+rect.width*fx,y=rect.top+rect.height*fy,hit=document.elementFromPoint(x,y);return {x,y,on_canvas:hit===node||node.contains(hit),hit_tag:hit?.tagName??'',hit_id:hit?.id??'',hit_class:typeof hit?.className==='string'?hit.className:''};});
  const point=observations.find(p=>p.on_canvas);
  return {ready:visible&&!!point,has_focus:document.hasFocus(),visibility_state:document.visibilityState,captured:document.pointerLockElement===node,input_active:/entrée active true/i.test(status),exploration_active:/Exploration active/i.test(capture),x:point?.x??null,y:point?.y??null,target_point:point??null,point_observations:observations,reason:!visible?'canvas hidden':point?'exposed canvas point ready':'canvas points covered'};
})()
'@
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        $canvas = Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'playtest canvas' -Expression $canvasExpression
        # Moving an absolute pointer while already captured rotates the camera.
        if ($canvas.captured -and $canvas.input_active -and $canvas.exploration_active) { return }
        if ($canvas.captured -or $canvas.input_active) {
            Release-PlaytestCapture -Socket $Socket -TimeoutSeconds $TimeoutSeconds
        }
        $targetInfo = Invoke-CdpCommand -Socket $Socket -Method 'Target.getTargetInfo'
        [void](Invoke-CdpCommand -Socket $Socket -Method 'Target.activateTarget' -Parameters @{ targetId = [string]$targetInfo.targetInfo.targetId })
        [void](Invoke-CdpCommand -Socket $Socket -Method 'Page.bringToFront')
        # Browser capture can reject an immediate request after a real Escape
        # release. Keep the complete 1.5 s cooldown from that observed release,
        # including time already spent on public actions/Save. An unknown
        # timeline retains the original full delay. Only the public capture
        # gesture is retried; gameplay actions are not.
        $cooldownMilliseconds = 1500
        $releaseTimes = Get-Variable -Name 'playtestCaptureReleaseTimes' -Scope Script -ErrorAction SilentlyContinue
        if ($null -ne $releaseTimes -and $releaseTimes.Value.ContainsKey($Socket)) {
            $elapsedMilliseconds = ([DateTimeOffset]::UtcNow - $releaseTimes.Value[$Socket]).TotalMilliseconds
            $cooldownMilliseconds = [int][Math]::Ceiling([Math]::Max(0, 1500 - $elapsedMilliseconds))
        }
        Write-Host "GATE3_PUBLIC_CAPTURE_COOLDOWN remaining_ms=$cooldownMilliseconds total_ms=1500"
        if ($cooldownMilliseconds -gt 0) { Start-Sleep -Milliseconds $cooldownMilliseconds }
        $canvas = Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'playtest canvas after capture cooldown' -Expression $canvasExpression
        if ($canvas.captured -and $canvas.input_active -and $canvas.exploration_active) { return }
        if (-not $canvas.has_focus -or $canvas.visibility_state -cne 'visible') { throw 'Public browser document is not focused and visible before pointer capture.' }
        if ($canvas.captured -or $canvas.input_active) { throw 'Pointer capture changed before the absolute focus gesture.' }
        Write-Host ('GATE3_PUBLIC_CANVAS_FOCUS_TARGET ' + ($canvas.target_point | ConvertTo-Json -Compress))
        Send-CdpMouseClick -Socket $Socket -X $canvas.x -Y $canvas.y
        try {
            $pointerCaptureExpression = @'
(() => {
  const capture=(document.querySelector('#studio-input-capture-status')?.textContent??'').replace(/\s+/g,' ').trim();
  const status=(document.querySelector('#authoring-playtest-status')?.textContent??'').replace(/\s+/g,' ').trim();
  return {ready:document.pointerLockElement?.id==='forge-authoring-canvas'&&/Exploration active/i.test(capture)&&/entrée active true/i.test(status),terminal:/Capture refusée/i.test(capture),reason:capture+' | '+status};
})()
'@
            if ($PointerCapturePollMilliseconds -eq 200) {
                # Preserve the historical call, including its default CDP
                # expression timeout and 200 ms polling cadence.
                [void](Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'playtest pointer capture' -Expression $pointerCaptureExpression)
            } else {
                # The local bounded wait changes only the opt-in fast polling
                # cadence. Its readiness predicate, terminal refusal and
                # fail-closed timeout remain identical to Wait-CdpReady.
                $waitPointerCapture = {
                    param($WaitSocket, [int]$WaitTimeoutSeconds, [int]$PollMilliseconds, [string]$Expression)
                    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($WaitTimeoutSeconds)
                    $lastReason = 'not evaluated'
                    while ([DateTimeOffset]::UtcNow -lt $deadline) {
                        $state = Invoke-CdpExpression -Socket $WaitSocket -TimeoutSeconds $WaitTimeoutSeconds -Expression $Expression
                        if ($null -ne $state -and $state.PSObject.Properties.Name -contains 'reason') {
                            $lastReason = [string]$state.reason
                        }
                        $withinDeadline = [DateTimeOffset]::UtcNow -le $deadline
                        if ($withinDeadline -and $null -ne $state -and $state.ready -eq $true) { return $state }
                        if ($null -ne $state -and $state.PSObject.Properties.Name -contains 'terminal' -and $state.terminal -eq $true) {
                            throw "playtest pointer capture reached a terminal failure: $($state.reason)"
                        }
                        if (-not $withinDeadline) { break }
                        Start-Sleep -Milliseconds $PollMilliseconds
                    }
                    throw "playtest pointer capture did not become ready within $WaitTimeoutSeconds seconds. Last state: $lastReason"
                }
                [void](& $waitPointerCapture $Socket $TimeoutSeconds $PointerCapturePollMilliseconds $pointerCaptureExpression)
            }
            return
        } catch {
            if ($attempt -ge 3 -or $_.Exception.Message -notmatch 'Capture refusée') { throw }
            # An external Escape can make the known timeline stale. A browser
            # refusal restores a full cooldown before the next public gesture.
            if ($null -eq (Get-Variable -Name 'playtestCaptureReleaseTimes' -Scope Script -ErrorAction SilentlyContinue)) {
                $script:playtestCaptureReleaseTimes = @{}
            }
            $script:playtestCaptureReleaseTimes[$Socket] = [DateTimeOffset]::UtcNow
        }
    }
}

function Send-Gate3PlaytestKeyEvent {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][ValidateSet('rawKeyDown', 'keyUp')][string]$Type,
        [Parameter(Mandatory)][string]$Key,
        [Parameter(Mandatory)][string]$Code,
        [Parameter(Mandatory)][int]$VirtualKeyCode
    )
    $common = @{
        key = $Key
        code = $Code
        windowsVirtualKeyCode = $VirtualKeyCode
        nativeVirtualKeyCode = $VirtualKeyCode
        modifiers = 0
    }
    [void](Invoke-CdpCommand -Socket $Socket -Method 'Input.dispatchKeyEvent' -Parameters (@{ type = $Type } + $common))
}

function Release-PlaytestCapture {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)][int]$TimeoutSeconds)
    $captureBefore = Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {const status=document.querySelector('#authoring-playtest-status')?.textContent??'';return {pointer_captured:document.pointerLockElement!==null,input_active:/entrée active true/i.test(status)};})()
'@
    Send-CdpKeyStroke -Socket $Socket -Key 'Escape' -Code 'Escape' -VirtualKeyCode 27
    if ($captureBefore.pointer_captured -or $captureBefore.input_active) {
        if ($null -eq (Get-Variable -Name 'playtestCaptureReleaseTimes' -Scope Script -ErrorAction SilentlyContinue)) {
            $script:playtestCaptureReleaseTimes = @{}
        }
        # Record after the acknowledged public keystroke, conservatively.
        $script:playtestCaptureReleaseTimes[$Socket] = [DateTimeOffset]::UtcNow
    }
    [void](Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'public pointer capture released' -Expression @'
(() => {
  const status=document.querySelector('#authoring-playtest-status')?.textContent??'';
  const active=document.pointerLockElement!==null||/entrée active true/i.test(status);
  return {ready:!active,terminal:false,reason:active?'public input capture still active':'public input capture released'};
})()
'@)
}

function Observe-PlaytestFacing {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase
    )
    $state = Get-PlaytestPublicState -Socket $Socket
    $observation = Get-PlaytestObservation -State $state
    if (-not $observation.ok -or $null -eq $observation.yaw_degrees) {
        throw 'Public playtest status did not expose a finite camera yaw.'
    }
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'public_look_observation' -Payload (Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $Socket))
}

function Move-PlaytestAxis {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][ValidateSet('X', 'Z')][string]$Axis,
        [Parameter(Mandatory)][ValidateSet('increase', 'decrease')][string]$Direction,
        [Parameter(Mandatory)][double]$Target,
        [Parameter(Mandatory)][double]$Tolerance,
        [Parameter(Mandatory)][int]$MaxSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase
    )
    $key = switch ($Axis) {
        'X' { if ($Direction -eq 'increase') { 'd' } else { 'a' } }
        'Z' { if ($Direction -eq 'increase') { 's' } else { 'w' } }
    }
    $code = switch ($key) { 'a' { 'KeyA' } 'd' { 'KeyD' } 's' { 'KeyS' } 'w' { 'KeyW' } }
    $vk = switch ($key) { 'a' { 65 } 'd' { 68 } 's' { 83 } 'w' { 87 } }
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($MaxSeconds)
    $reached = $false
    $started = $false
    try {
        Send-Gate3PlaytestKeyEvent -Socket $Socket -Type rawKeyDown -Key $key -Code $code -VirtualKeyCode $vk
        $started = $true
        while ([DateTimeOffset]::UtcNow -lt $deadline) {
            $state = Get-PlaytestPublicState -Socket $Socket
            $observation = Get-PlaytestObservation -State $state
            if (-not $observation.ok -or -not $observation.simulation_ready) { throw "Public playtest status became unavailable while moving $Axis." }
            Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'movement_observation' -Payload $observation
            $value = if ($Axis -eq 'X') { [double]$observation.x } else { [double]$observation.z }
            $reached = if ($Direction -eq 'increase') { $value -ge ($Target - $Tolerance) } else { $value -le ($Target + $Tolerance) }
            if ($reached) { break }
            Start-Sleep -Milliseconds 120
        }
    }
    finally {
        if ($started) { Send-Gate3PlaytestKeyEvent -Socket $Socket -Type keyUp -Key $key -Code $code -VirtualKeyCode $vk }
    }
    if (-not $reached) { throw "Public movement did not reach $Axis=$Target within $MaxSeconds seconds." }
}

function Move-PlaytestToXZ {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][double]$X,
        [Parameter(Mandatory)][double]$Z,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase
    )
    Focus-PlaytestCanvas -Socket $Socket -TimeoutSeconds $TimeoutSeconds
    Observe-PlaytestFacing -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Journal $Journal -Phase $Phase
    [void](Move-PlaytestWASDSegment -Socket $Socket -TargetX $X -TargetZ $Z -Tolerance 1.25 -MaxSeconds $TimeoutSeconds -Journal $Journal -Phase $Phase -RouteId 'public-point-approach')
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'movement_complete' -Payload (Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $Socket))
}

function Get-PlaytestRouteKeys {
    param([double]$DeltaX, [double]$DeltaZ, [double]$YawDegrees)
    # Bevy's horizontal camera rotation maps D to (cos(yaw), -sin(yaw))
    # and W to (-sin(yaw), -cos(yaw)). Choose the closest of eight public
    # WASD directions, using only the camera yaw displayed by the Studio.
    $yaw = $YawDegrees * [Math]::PI / 180
    $right = $DeltaX * [Math]::Cos($yaw) - $DeltaZ * [Math]::Sin($yaw)
    $forward = -$DeltaX * [Math]::Sin($yaw) - $DeltaZ * [Math]::Cos($yaw)
    $sector = (([int][Math]::Round([Math]::Atan2($right, $forward) / ([Math]::PI / 4)) % 8) + 8) % 8
    $letters = switch ($sector) {
        0 { 'w' }; 1 { 'w,d' }; 2 { 'd' }; 3 { 's,d' }
        4 { 's' }; 5 { 's,a' }; 6 { 'a' }; 7 { 'w,a' }
    }
    foreach ($letter in $letters.Split(',')) {
        [pscustomobject]@{ key = $letter; code = 'Key' + $letter.ToUpperInvariant(); vk = [int][char]$letter.ToUpperInvariant() }
    }
}

function Get-PlaytestRouteArrivalDecision {
    param(
        [Parameter(Mandatory)]$Observation,
        [Parameter(Mandatory)][double]$TargetX,
        [Parameter(Mandatory)][double]$TargetZ,
        [Parameter(Mandatory)][double]$Tolerance,
        [AllowNull()][scriptblock]$ArrivalCondition = $null,
        [AllowNull()][object]$ArrivalContext = $null
    )
    # This decision runs before marker distance, progress and timeout logic
    # in the movement loop. A canonical semantic arrival stops movement now.
    if ($null -ne $ArrivalCondition -and [bool](& $ArrivalCondition $Observation $ArrivalContext)) {
        return [ordered]@{stop=$true;arrival_kind='canonical_semantic';marker_distance_evaluated=$false;distance_m=$null}
    }
    $dx = $TargetX - [double]$Observation.x
    $dz = $TargetZ - [double]$Observation.z
    $distance = [Math]::Sqrt($dx*$dx + $dz*$dz)
    return [ordered]@{stop=($null -eq $ArrivalCondition -and $distance -le $Tolerance);arrival_kind='marker_distance';marker_distance_evaluated=$true;distance_m=$distance}
}

function Move-PlaytestWASDSegment {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][double]$TargetX,
        [Parameter(Mandatory)][double]$TargetZ,
        [Parameter(Mandatory)][double]$Tolerance,
        [Parameter(Mandatory)][ValidateRange(1, 600)][int]$MaxSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)][string]$RouteId,
        [AllowNull()][scriptblock]$ArrivalCondition = $null,
        [AllowNull()][object]$ArrivalContext = $null
    )
    $initial = Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $Socket)
    if (-not $initial.ok -or -not $initial.simulation_ready) {
        throw "Public playtest position was not ready before route segment '$Phase'."
    }
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'route_segment_start' -Payload ([ordered]@{
            route_id = $RouteId
            target_x = $TargetX
            target_z = $TargetZ
            tolerance_m = $Tolerance
            initial = $initial
            steering = 'public_camera_yaw_eight_wasd_directions'
        })
    $active = [System.Collections.Generic.List[object]]::new()
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($MaxSeconds)
    $reached = $false
    $last = $initial
    $progressPosition = $initial
    $progressAt = [DateTimeOffset]::UtcNow
    $arrivalDecision = $null
    try {
        while ([DateTimeOffset]::UtcNow -lt $deadline) {
            $last = Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $Socket)
            if (-not $last.ok -or -not $last.simulation_ready -or $null -eq $last.yaw_degrees) {
                throw "Public playtest status became unavailable during route segment '$Phase'."
            }
            $arrivalDecision = Get-PlaytestRouteArrivalDecision -Observation $last -TargetX $TargetX -TargetZ $TargetZ -Tolerance $Tolerance -ArrivalCondition $ArrivalCondition -ArrivalContext $ArrivalContext
            if ($arrivalDecision.stop) {
                $reached = $true
                Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'route_stop_before_marker_progress_timeout' -Payload ([ordered]@{route_id=$RouteId;observation=$last;arrival_decision=$arrivalDecision})
                break
            }
            # Public gameplay actions and browser focus can release pointer
            # capture while the simulation remains ready.  A held key then
            # becomes a no-op, which previously looked like a terrain blocker
            # at (35.67,-172.77).  Release the locally tracked keys, reacquire
            # the canvas through the same visible click path, and continue the
            # current short polyline segment.  No position or domain state is
            # written by this recovery.
            if (-not [bool]$last.input_active) {
                foreach ($entry in @($active.ToArray())) {
                    Send-Gate3PlaytestKeyEvent -Socket $Socket -Type keyUp -Key $entry.key -Code $entry.code -VirtualKeyCode $entry.vk
                }
                $active.Clear()
                Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'public_input_reacquire' -Payload ([ordered]@{
                        route_id = $RouteId
                        observation = $last
                        method = 'public_canvas_focus_and_pointer_capture'
                    })
                Focus-PlaytestCanvas -Socket $Socket -TimeoutSeconds ([Math]::Min(60, $MaxSeconds))
                $last = Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $Socket)
                if (-not $last.ok -or -not [bool]$last.input_active) {
                    throw "Public playtest input capture did not recover during route segment '$Phase'."
                }
                $progressPosition = $last
                $progressAt = [DateTimeOffset]::UtcNow
            }
            $dx = $TargetX - [double]$last.x
            $dz = $TargetZ - [double]$last.z
            $distance = $arrivalDecision.distance_m
            $progressX = [double]$last.x - [double]$progressPosition.x
            $progressZ = [double]$last.z - [double]$progressPosition.z
            if ([Math]::Sqrt($progressX*$progressX + $progressZ*$progressZ) -ge 0.05) {
                $progressPosition = $last
                $progressAt = [DateTimeOffset]::UtcNow
            } elseif (-not $reached -and ([DateTimeOffset]::UtcNow - $progressAt).TotalSeconds -ge 12) {
                throw "Public WASD route segment '$Phase' made no horizontal progress for twelve seconds: $($last.raw)"
            }
            $wanted = if ($reached) { @() } else { @(Get-PlaytestRouteKeys -DeltaX $dx -DeltaZ $dz -YawDegrees ([double]$last.yaw_degrees)) }
            Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'route_movement_observation' -Payload ([ordered]@{
                    route_id = $RouteId
                    target_x = $TargetX
                    target_z = $TargetZ
                    observation = $last
                    distance_m = $distance
                    simultaneous_keys = @($wanted | ForEach-Object { $_.key })
                })
            $release = [System.Collections.Generic.List[object]]::new()
            foreach ($entry in $active) {
                if (@($wanted | Where-Object key -CEQ $entry.key).Count -eq 0) {
                    [void]$release.Add($entry)
                }
            }
            foreach ($entry in $release) {
                Send-Gate3PlaytestKeyEvent -Socket $Socket -Type keyUp -Key $entry.key -Code $entry.code -VirtualKeyCode $entry.vk
                [void]$active.Remove($entry)
            }
            foreach ($entry in $wanted) {
                if (@($active | Where-Object key -CEQ $entry.key).Count -eq 0) {
                    Send-Gate3PlaytestKeyEvent -Socket $Socket -Type rawKeyDown -Key $entry.key -Code $entry.code -VirtualKeyCode $entry.vk
                    [void]$active.Add($entry)
                }
            }
            if ($reached) { break }
            Start-Sleep -Milliseconds 120
        }
    }
    finally {
        foreach ($entry in @($active.ToArray())) {
            Send-Gate3PlaytestKeyEvent -Socket $Socket -Type keyUp -Key $entry.key -Code $entry.code -VirtualKeyCode $entry.vk
        }
    }
    if (-not $reached) {
        throw "Public simultaneous WASD route segment '$Phase' did not reach ($TargetX,$TargetZ) within $MaxSeconds seconds."
    }
    $arrivalEvent = if ($null -ne $ArrivalCondition) { 'route_semantic_arrival_reached' } else { 'route_waypoint_reached' }
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event $arrivalEvent -Payload ([ordered]@{
            route_id = $RouteId
            target_x = $TargetX
            target_z = $TargetZ
            tolerance_m = $Tolerance
            observation = $last
            arrival_decision = $arrivalDecision
        })
    return $last
}

function Move-PlaytestRoute {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][object[]]$Waypoints,
        [Parameter(Mandatory)][double]$Tolerance,
        [Parameter(Mandatory)][ValidateRange(1, 900)][int]$MaxSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)][string]$RouteId
    )
    if ($Waypoints.Count -lt 2) { throw "Route '$RouteId' requires at least two reviewed waypoints." }
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($MaxSeconds)
    Focus-PlaytestCanvas -Socket $Socket -TimeoutSeconds ([Math]::Min(60, $MaxSeconds))
    Observe-PlaytestFacing -Socket $Socket -TimeoutSeconds ([Math]::Min(60, $MaxSeconds)) -Journal $Journal -Phase $Phase
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'route_started' -Payload ([ordered]@{
            route_id = $RouteId
            tolerance_m = $Tolerance
            waypoints = @($Waypoints | ForEach-Object { [ordered]@{ x = [double]$_.x; z = [double]$_.z } })
        })
    $actualStart = Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $Socket)
    if (-not $actualStart.ok -or -not $actualStart.simulation_ready) {
        throw "Public route '$RouteId' did not expose a ready actual start position."
    }
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'route_actual_start' -Payload ([ordered]@{
            route_id = $RouteId
            observation = $actualStart
            method = 'public_observation_before_first_waypoint'
        })
    $arrival = $null
    for ($index = 0; $index -lt $Waypoints.Count; $index += 1) {
        $end = $Waypoints[$index]
        # The preceding public action may stop within its tolerance of the
        # nominal route origin, and a sloped surface can carry the capsule
        # after a segment reaches its target.  Start every segment after the
        # first at the last public observation rather than pretending it was
        # at the authored waypoint; this keeps the polyline public and avoids
        # steering from a stale point into the village actor capsule.
        $start = if ($index -eq 0) { $actualStart } elseif ($null -ne $arrival) { $arrival } else { $Waypoints[$index - 1] }
        $dx = [double]$end.x - [double]$start.x
        $dz = [double]$end.z - [double]$start.z
        # A long endpoint-only key hold can drift outside the reviewed path:
        # eight keyboard directions approximate a line, rather than reproduce
        # its continuous angle. Follow the original polyline at <=3 m spacing.
        $steps = [Math]::Max(1, [int][Math]::Ceiling([Math]::Sqrt($dx*$dx + $dz*$dz) / 3.0))
        for ($step = 1; $step -le $steps; $step += 1) {
            $remaining = [int][Math]::Floor(($deadline - [DateTimeOffset]::UtcNow).TotalSeconds)
            if ($remaining -lt 1) { throw "Public route '$RouteId' exceeded its $MaxSeconds second bound." }
            $fraction = $step / [double]$steps
            $targetX = [double]$start.x + $dx*$fraction
            $targetZ = [double]$start.z + $dz*$fraction
            $stepTolerance = if ($step -eq $steps) { $Tolerance } else { [Math]::Min(0.7, $Tolerance) }
            $arrival = Move-PlaytestWASDSegment -Socket $Socket -TargetX $targetX -TargetZ $targetZ -Tolerance $stepTolerance -MaxSeconds ([Math]::Min($remaining, $MaxSeconds)) -Journal $Journal -Phase "$Phase-waypoint-$index-step-$step" -RouteId $RouteId
        }
    }
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'route_completed' -Payload ([ordered]@{
            route_id = $RouteId
            final = $arrival
            target_x = [double]$Waypoints[$Waypoints.Count - 1].x
            target_z = [double]$Waypoints[$Waypoints.Count - 1].z
            tolerance_m = $Tolerance
        })
    return $arrival
}

function Wait-PlaytestSettled {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $last = $null
    $stable = 0
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $state = Get-PlaytestPublicState -Socket $Socket
        $observation = Get-PlaytestObservation -State $state
        if ($observation.ok -and $observation.simulation_ready -and $observation.grounded) {
            if ($null -ne $last -and [Math]::Abs($observation.x - $last.x) -lt 0.03 -and [Math]::Abs($observation.z - $last.z) -lt 0.03) { $stable += 1 } else { $stable = 0 }
            $last = $observation
            Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'settled_observation' -Payload $observation
            if ($stable -ge 3) { return $state }
        } else { $stable = 0 }
        Start-Sleep -Milliseconds 250
    }
    throw 'Grounded public playtest observation did not become stable within the bound.'
}

function Wait-PlaytestLocomotionSettled {
    param(
        [Parameter(Mandatory)]$Socket,
        [ValidateRange(1, 600)][Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase,
        [ValidateRange(50, 500)][int]$SampleMilliseconds = 120
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastObservation = $null
    $stable = 0
    $sampleIndex = 0
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $state = Get-PlaytestPublicState -Socket $Socket
        $observation = Get-PlaytestObservation -State $state
        if ($observation.ok -and $observation.simulation_ready -and $observation.grounded -and
            $null -ne $lastObservation -and
            [Math]::Abs([double]$observation.x - [double]$lastObservation.x) -lt 0.03 -and
            [Math]::Abs([double]$observation.z - [double]$lastObservation.z) -lt 0.03) {
            $stable += 1
        } else {
            $stable = 0
        }
        Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'locomotion_observation' -Payload ([ordered]@{
                sample_index = $sampleIndex
                sample_interval_ms = $SampleMilliseconds
                observation = $observation
                locomotion = $state.locomotion
                locomotion_error = $state.locomotion_error
                stable_samples = $stable
            })
        if ($observation.ok -and $observation.simulation_ready -and $observation.grounded -and $stable -ge 3) {
            Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'locomotion_settled' -Payload ([ordered]@{
                    sample_index = $sampleIndex
                    sample_interval_ms = $SampleMilliseconds
                    observation = $observation
                    locomotion = $state.locomotion
                    stable_samples = $stable
                })
            return $state
        }
        $lastObservation = $observation
        $sampleIndex += 1
        Start-Sleep -Milliseconds $SampleMilliseconds
    }
    throw "Public locomotion did not become grounded and stable within $TimeoutSeconds seconds during '$Phase'."
}

function Wait-PlaytestCondition {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [Parameter(Mandatory)][scriptblock]$Condition,
        [Parameter(Mandatory)][string]$Description
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastReason = 'public state pending'
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $state = Get-PlaytestPublicState -Socket $Socket
        if ([bool](& $Condition $state)) { return $state }
        $lastReason = [string]$state.feedback
        Start-Sleep -Milliseconds 200
    }
    throw "$Description did not become true within $TimeoutSeconds seconds. Last public feedback: $lastReason"
}

function Invoke-PlaytestGameplayAction {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase
    )
    $before = Get-PlaytestPublicState -Socket $Socket
    $matches = @($before.actions | Where-Object { [string]$_.text -ceq $Text })
    if ($matches.Count -ne 1) { throw "Expected one public gameplay action '$Text', got $($matches.Count)." }
    if ([bool]$matches[0].disabled) { throw "Public gameplay action '$Text' is disabled." }
    if ([int]$before.feedback_count -ne 1) { throw "Public gameplay action '$Text' has no unique feedback paragraph: $($before.feedback_error)" }
    $beforeFeedback = [string]$before.feedback
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'public_action_before_escape' -Payload ([ordered]@{text=$Text;observation=(Get-PlaytestObservation -State $before);locomotion=$before.locomotion;feedback=$beforeFeedback})
    Release-PlaytestCapture -Socket $Socket -TimeoutSeconds $TimeoutSeconds
    $atClick = Get-PlaytestPublicState -Socket $Socket
    if ([int]$atClick.feedback_count -ne 1) { throw "Public gameplay action '$Text' lost its unique feedback paragraph before click: $($atClick.feedback_error)" }
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'public_action_after_escape_before_click' -Payload ([ordered]@{text=$Text;observation=(Get-PlaytestObservation -State $atClick);locomotion=$atClick.locomotion;feedback=[string]$atClick.feedback;limits='Public DOM snapshot immediately before the click; the action receipt remains authoritative for engine processing.'})
    [void](Invoke-UiClick -Socket $Socket -Text $Text -Selector '.gameplay-actions button')
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'public_action_click' -Payload $Text
    Start-Sleep -Milliseconds 250
    $state = Wait-PlaytestCondition -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description "public gameplay action '$Text' settled" -Condition {
        param($current)
        $currentMatches = @($current.actions | Where-Object { [string]$_.text -ceq $Text })
        ([int]$current.feedback_count -eq 1) -and ([string]$current.feedback -cne $beforeFeedback) -and (($currentMatches.Count -eq 0) -or (-not [bool]$currentMatches[0].disabled))
    }
    if ([string]$state.feedback -match 'refus|rejected|hors de portée|ligne de vue|impossible') {
        throw "Public gameplay action '$Text' was refused: $($state.feedback)"
    }
    if ([string]$state.feedback -notmatch '^Action acceptée\s+·\s+étape\s+\d+$') {
        throw "Public gameplay action '$Text' has no positive runtime acknowledgement: $($state.feedback)"
    }
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'public_action_settled' -Payload ([ordered]@{ text = $Text; state = $state })
    return $state
}

function Get-PlaytestInteriorScenario {
    param([Parameter(Mandatory)][string]$Archive)
    $document = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($Archive))
    $buildings = @($document.head.corrections.buildings)
    $gameplay = $document.head.corrections.gameplay
    $doors = @($gameplay.objects | Where-Object { [string]$_.name -ceq 'Porte intérieure' -and [string]$_.kind.kind -ceq 'door' })
    $interiors = @($gameplay.places | Where-Object { [string]$_.name -ceq 'Intérieur' })
    $villages = @($gameplay.places | Where-Object { [string]$_.name -ceq 'Village' })
    if ($buildings.Count -ne 1 -or $doors.Count -ne 1 -or $interiors.Count -ne 1 -or $villages.Count -ne 1) {
        throw 'Interior visit requires one authored room, Porte intérieure, Intérieur and Village.'
    }
    $room = $buildings[0]
    $door = $doors[0]
    $interior = $interiors[0]
    $village = $villages[0]
    $width = [double]$room.dimensions.width_m
    $depth = [double]$room.dimensions.depth_m
    $originX = [double]$room.origin_m.x
    $originZ = [double]$room.origin_m.z
    $doorX = [double]$door.position.x
    $doorZ = [double]$door.position.z
    $interiorX = [double]$interior.position.x
    $interiorZ = [double]$interior.position.z
    $villageX = [double]$village.position.x
    $villageZ = [double]$village.position.z
    $floorY = [double]$room.origin_m.y
    $wall = [double]$room.dimensions.wall_m
    # PlaceDefinition has position + links, with no radius contract. Use the
    # canonical RoomLayout empty volume and an explicit capsule clearance.
    $interiorMargin = 0.35
    foreach ($value in @($width, $depth, $originX, $originZ, $doorX, $doorZ, $interiorX, $interiorZ, $villageX, $villageZ, $floorY, $wall, [double]$room.yaw_degrees)) {
        if (-not [double]::IsFinite($value)) { throw 'Interior scenario has a non-finite coordinate or dimension.' }
    }
    if ($width -lt 4 -or $depth -lt 4 -or [Math]::Abs([double]$room.yaw_degrees) -gt 0.001 -or
        [Math]::Abs($doorZ - ($originZ - $depth / 2.0)) -gt 0.1 -or
        [Math]::Abs($doorX - $originX) -ge $width / 2.0 - 0.5 -or
        [Math]::Abs($interiorX - $originX) -ge $width / 2.0 - 0.5 -or
        [Math]::Abs($interiorZ - $originZ) -ge $depth / 2.0 - 0.5 -or [bool]$door.kind.initially_open) {
        throw 'Interior scenario does not match the reviewed unrotated room and initially closed south door.'
    }
    $placeIds = @($gameplay.places | ForEach-Object { [string]$_.id })
    if (@($placeIds | Select-Object -Unique).Count -ne $placeIds.Count) { throw 'Authored places have duplicate identities.' }
    foreach ($place in @($gameplay.places)) {
        $links = @($place.links | ForEach-Object { [string]$_ })
        if (@($links | Select-Object -Unique).Count -ne $links.Count -or @($links | Where-Object { $_ -cnotin $placeIds }).Count) { throw 'An authored place has a duplicate or invalid link reference.' }
    }
    $reachable = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $pendingPlaces = [System.Collections.Generic.Queue[string]]::new()
    $pendingPlaces.Enqueue([string]$village.id)
    while ($pendingPlaces.Count) {
        $placeId = $pendingPlaces.Dequeue()
        if (-not $reachable.Add($placeId)) { continue }
        $place = @($gameplay.places | Where-Object { [string]$_.id -ceq $placeId })[0]
        foreach ($link in @($place.links)) { $pendingPlaces.Enqueue([string]$link) }
    }
    if (-not $reachable.Contains([string]$interior.id)) { throw 'The authored interior place is not reachable through the Village place references.' }
    # A single-point public patrol has a permanent destination. A dedicated
    # actor authored inside this room must finish its actual exit before the
    # player enters the same doorway; no actor is paused or removed for a test.
    $oneWayExitActors = @($gameplay.actors | Where-Object {
        @($_.patrol).Count -eq 1 -and
        [double]$_.position.x -gt $originX-$width/2.0+$wall -and [double]$_.position.x -lt $originX+$width/2.0-$wall -and
        [double]$_.position.z -gt $originZ-$depth/2.0+$wall -and [double]$_.position.z -lt $originZ+$depth/2.0-$wall -and
        [double]$_.patrol[0].z -lt $originZ-$depth/2.0-$wall
    } | ForEach-Object { [string]$_.id })
    return [ordered]@{
        room_id = [string]$room.id; door_id = [string]$door.id; place_id = [string]$interior.id
        origin_x = $originX; origin_z = $originZ; width_m = $width; depth_m = $depth
        door_x = $doorX; door_z = $doorZ; interior_x = $interiorX; interior_z = $interiorZ
        door_opening_width_m = [double]$room.dimensions.door_width_m
        village_x = $villageX; village_z = $villageZ
        west_clear_x = $originX - $width / 2.0 - 1.0
        south_clear_z = $originZ - $depth / 2.0 - 3.0
        exterior_z = $doorZ - 1.25
        tolerance_m = 0.35
        floor_y = $floorY; wall_m = $wall; interior_margin_m = $interiorMargin
        interior_min_x = $originX - $width / 2.0 + $wall + $interiorMargin
        interior_max_x = $originX + $width / 2.0 - $wall - $interiorMargin
        interior_min_z = $originZ - $depth / 2.0 + $wall + $interiorMargin
        interior_max_z = $originZ + $depth / 2.0 - $wall - $interiorMargin
        floor_support_tolerance_m = 0.0001
        one_way_exit_actors = $oneWayExitActors
        authored_source_hash = [string]$document.receipts[-1].resulting_hash
        place_reference_reachable_from_village = $true
        occupancy_contract = 'RoomLayout interior bounds excluding walls, with 0.35m capsule margin; public grounded anchor and support height on the canonical floor. No PlaceDefinition radius exists.'
    }
}

function Test-PlaytestNavigationActorOutsideDoorwayCorridor {
    param([Parameter(Mandatory)]$Actor,[Parameter(Mandatory)]$Profile,[Parameter(Mandatory)]$Scenario)
    # A conservative envelope around the full actual door opening and the
    # upcoming exterior-to-interior segment. The NPC radius is observed; the
    # existing P01 room margin remains a declared harness clearance, not a new
    # engine radius or contact rule.
    $clearance = [double]$Profile.radius_m + [double]$Scenario.interior_margin_m
    $minX = [double]$Scenario.door_x - [double]$Scenario.door_opening_width_m / 2.0 - $clearance
    $maxX = [double]$Scenario.door_x + [double]$Scenario.door_opening_width_m / 2.0 + $clearance
    $minZ = [Math]::Min([double]$Scenario.exterior_z,[double]$Scenario.interior_z) - $clearance
    $maxZ = [Math]::Max([double]$Scenario.exterior_z,[double]$Scenario.interior_z) + $clearance
    $x = [double]$Actor.feet[0]; $z = [double]$Actor.feet[2]
    return $x -lt $minX -or $x -gt $maxX -or $z -lt $minZ -or $z -gt $maxZ
}

function Wait-PlaytestAuthoredRoomExitActors {
    param([Parameter(Mandatory)]$Socket,[Parameter(Mandatory)]$Scenario,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal)
    $actorIds = @(Get-Gate3NavigationProperty -Value $Scenario -Name 'one_way_exit_actors')
    if ($actorIds.Count -eq 0) { return }
    $headHash = [string](Get-Gate3NavigationProperty -Value $Scenario -Name 'authored_source_hash')
    if ($headHash -cnotmatch '^[0-9a-f]{64}$') { throw 'Room exit actors have no authored head source pin.' }
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $previous = $null
    $fresh = [System.Collections.Generic.List[object]]::new()
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $capture = Get-Gate3PublicNavigationSnapshot -Socket $Socket
        $observed = $capture.observation
        Add-PlaytestJournal -Journal $Journal -Phase 'interior-exit-actors' -Event 'public_navigation_snapshot' -Payload $capture
        if ($null -ne $previous -and [long]$observed.tick -eq [long]$previous.tick) { Start-Sleep -Milliseconds 250; continue }
        [void](Assert-Gate3NavigationObservation -Observation $observed -ExpectedGameplaySourceHash $headHash -Previous $previous)
        $fresh.Add($observed); $previous = $observed
        $allReached = $true
        foreach ($id in $actorIds) {
            $actor = Get-Gate3NavigationActor -Observation $observed -ActorId ([guid]$id)
            if ($actor.status -in @('outside_mesh','empty_source','invalid_geometry','budget_exceeded','disabled')) { throw "Authored room exit actor $id could not leave after the public door Open: $($actor.status)." }
            $blockedReport = $null
            if ($actor.status -ceq 'no_path') {
                try { $blockedReport = Get-Gate3NavigationStationaryReport -Observations @($fresh | Select-Object -Last 3) -ActorId ([guid]$id) -ExpectedStatus no_path -ExpectedGameplaySourceHash $headHash } catch { Write-Verbose $_.Exception.Message }
                if ($null -ne $blockedReport) { throw "Authored room exit actor $id retained settled NoPath after the public door Open." }
            }
            try {
                $report = Get-Gate3NavigationStationaryReport -Observations @($fresh | Select-Object -Last 3) -ActorId ([guid]$id) -ExpectedStatus reached -ExpectedGameplaySourceHash $headHash
                if ([double]$actor.feet[2] -ge [double]$Scenario.door_z-[double]$Scenario.wall_m-[double]$observed.profile.radius_m) { throw 'Reached actor remains in the canonical room/doorway envelope.' }
                if (-not (Test-PlaytestNavigationActorOutsideDoorwayCorridor -Actor $actor -Profile $observed.profile -Scenario $Scenario)) { throw 'Reached actor remains inside the upcoming player doorway corridor.' }
                Add-PlaytestJournal -Journal $Journal -Phase 'interior-exit-actors' -Event 'authored_exit_actor_stationary_outside' -Payload $report
            } catch { $allReached = $false }
        }
        if ($allReached) { return }
        Start-Sleep -Milliseconds 250
    }
    throw 'Authored room exit actors did not reach and remain outside before the player doorway traversal.'
}

function Test-PlaytestInteriorOccupancy {
    param([Parameter(Mandatory)]$Observation, [Parameter(Mandatory)]$Scenario)
    if (-not $Observation.ok -or -not $Observation.simulation_ready -or -not $Observation.grounded -or $null -eq $Observation.locomotion) { return $false }
    $motion = $Observation.locomotion
    if (-not $motion.body_grounded -or -not $motion.rapier_grounded -or @($motion.anchor).Count -ne 3) { return $false }
    $x = [double]$motion.anchor[0]
    $y = [double]$motion.anchor[1]
    $z = [double]$motion.anchor[2]
    $supportY = [double]$motion.ground_height
    if (@($x,$y,$z,$supportY | Where-Object { -not [double]::IsFinite($_) }).Count) { return $false }
    return $Scenario.place_reference_reachable_from_village -and
        $x -ge $Scenario.interior_min_x -and $x -le $Scenario.interior_max_x -and
        $z -ge $Scenario.interior_min_z -and $z -le $Scenario.interior_max_z -and
        $z -gt $Scenario.door_z + $Scenario.wall_m + $Scenario.interior_margin_m -and
        [Math]::Abs($y - $Scenario.floor_y) -le $Scenario.floor_support_tolerance_m -and
        [Math]::Abs($supportY - $Scenario.floor_y) -le $Scenario.floor_support_tolerance_m
}

function Get-PlaytestCommittedReceiptReport {
    param(
        [Parameter(Mandatory)]$SaveDocument,
        [Parameter(Mandatory)]$Gameplay,
        [Parameter(Mandatory)][string]$ExpectedHeadHash,
        [Parameter(Mandatory)][string]$AreaId,
        [switch]$RequireInteriorDoorCycle
    )
    if ([string]$SaveDocument.schema -cne 'forge.game-save' -or [int]$SaveDocument.version -ne 1 -or
        [string]$SaveDocument.source_hash -cne $ExpectedHeadHash -or [string]$SaveDocument.area_id -cne $AreaId -or
        [bool]$SaveDocument.state.requires_physics_revalidation -or
        $SaveDocument.state.last_frame.frame.tick -ne $SaveDocument.state.tick -or
        [string]$SaveDocument.state.last_frame.frame.source_hash -cne $ExpectedHeadHash) { throw 'Committed gameplay save identity or physics frame is not coherent.' }
    $playerId = [string]$Gameplay.player
    $guard = @($Gameplay.actors | Where-Object { [string]$_.name -ceq 'Gardien de la forêt' -and [string]$_.role -ceq 'enemy' })
    $pickup = @($Gameplay.objects | Where-Object { [string]$_.name -ceq 'Sceau' -and [string]$_.kind.kind -ceq 'pickup' })
    $quest = @($Gameplay.quests | Where-Object { [string]$_.name -ceq 'Rapporter le Sceau' })
    if ($guard.Count -ne 1 -or $pickup.Count -ne 1 -or $quest.Count -ne 1) { throw 'Committed receipt audit requires the unique authored gameplay references.' }
    $guardId = [string]$guard[0].id; $pickupId = [string]$pickup[0].id; $itemId = [string]$pickup[0].kind.item; $questId = [string]$quest[0].id
    $receipts = @($SaveDocument.state.receipts)
    $attacks = @($receipts | Where-Object { [string]$_.request.actor -ceq $playerId -and [string]$_.request.action.kind -ceq 'attack' -and [string]$_.request.action.target -ceq $guardId })
    if ($attacks.Count -ne 5) { throw "Committed attack receipt count differs from five: $($attacks.Count)." }
    if (@($attacks.request.command_id | Select-Object -Unique).Count -ne 5 -or @($attacks.sequence | Select-Object -Unique).Count -ne 5) { throw 'Committed attack receipt identities or sequences are duplicated.' }
    foreach ($receipt in $attacks) {
        $damage = @($receipt.events | Where-Object { [string]$_.kind -ceq 'damage' -and [string]$_.target -ceq $guardId -and [bool]$_.contact -and [int]$_.amount -eq 20 })
        if ($receipt.sequence -ne $receipt.request.expected_sequence + 1 -or $receipt.tick -gt $SaveDocument.state.tick -or $damage.Count -ne 1) { throw 'An accepted attack lacks its exact committed contact-damage receipt.' }
    }
    $pickedUp = @($receipts | Where-Object { [string]$_.request.actor -ceq $playerId -and [string]$_.request.action.kind -ceq 'pickup' -and [string]$_.request.action.object -ceq $pickupId })
    $pickupEvents = @($pickedUp.events | Where-Object { [string]$_.kind -ceq 'picked_up' -and [string]$_.object -ceq $pickupId -and [string]$_.item -ceq $itemId -and [int]$_.quantity -eq 1 })
    $completed = @($receipts.events | Where-Object { [string]$_.kind -ceq 'quest_changed' -and [string]$_.quest -ceq $questId -and [string]$_.status -ceq 'completed' })
    if ($pickedUp.Count -ne 1 -or $pickupEvents.Count -ne 1 -or $completed.Count -ne 1 -or
        [int]$SaveDocument.state.actors.$guardId.vitality -ne 0 -or [int]$SaveDocument.state.actors.$playerId.vitality -le 0 -or
        -not [bool]$SaveDocument.state.objects.$pickupId.consumed -or [string]$SaveDocument.state.quests.$questId -cne 'completed') { throw 'Committed pickup, completed quest, consumed Sceau or actor state is inconsistent.' }
    $inventory = $SaveDocument.state.actors.$playerId.inventory
    if (@($inventory.PSObject.Properties | ForEach-Object Name) -contains $itemId -and [int]$inventory.$itemId -ne 0) { throw 'The consumed Sceau remains in the committed player inventory.' }
    $doorReceipts = @()
    if ($RequireInteriorDoorCycle) {
        $door = @($Gameplay.objects | Where-Object { [string]$_.name -ceq 'Porte intérieure' -and [string]$_.kind.kind -ceq 'door' })
        if ($door.Count -ne 1) { throw 'Committed room visit requires the unique authored door.' }
        $doorId = [string]$door[0].id
        $doorReceipts = @($receipts | Where-Object { [string]$_.request.actor -ceq $playerId -and [string]$_.request.action.kind -ceq 'set_door' -and [string]$_.request.action.object -ceq $doorId })
        $opened = @($doorReceipts.events | Where-Object { [string]$_.kind -ceq 'door_changed' -and [string]$_.object -ceq $doorId -and [bool]$_.open })
        $closed = @($doorReceipts.events | Where-Object { [string]$_.kind -ceq 'door_changed' -and [string]$_.object -ceq $doorId -and -not [bool]$_.open })
        if ($doorReceipts.Count -lt 2 -or $opened.Count -ne 1 -or $closed.Count -ne 1 -or [bool]$SaveDocument.state.objects.$doorId.open) { throw 'Committed door open/close receipts are inconsistent with the room visit.' }
    }
    return [ordered]@{schema='forge.tidewater.gate3-committed-gameplay-receipt-audit';version=1;passed=$true;source_hash=[string]$SaveDocument.source_hash;state_hash_reported=[string]$SaveDocument.state_hash;state_hash_independently_recomputed=$false;tick=$SaveDocument.state.tick;attack_receipts=$attacks;pickup_receipt=$pickedUp[0];completed_quest_event=$completed[0];door_receipts=$doorReceipts;limits='Read-only audit of the file created by the acknowledged public Save; no runtime query or state injection.'}
}

function Get-PlaytestPublicActionSequence {
    param([Parameter(Mandatory)][string]$Feedback)
    $match = [regex]::Match($Feedback, '^Action acceptée\s+·\s+étape\s+(?<sequence>\d+)$')
    if (-not $match.Success) { return $null }
    return [int64]$match.Groups['sequence'].Value
}

function Get-PlaytestFinalPhysicsScenario {
    param([Parameter(Mandatory)][string]$Archive)
    $document = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($Archive))
    $gameplay = $document.head.corrections.gameplay
    $guardian = @($gameplay.actors | Where-Object { [string]$_.name -ceq 'Gardien de la forêt' })
    $trap = @($gameplay.objects | Where-Object { [string]$_.name -ceq 'Support piège' })
    $watcher = @($gameplay.actors | Where-Object { [string]$_.name -ceq 'Veilleur du danger' })
    $pickup = @($gameplay.objects | Where-Object { [string]$_.name -ceq 'Sceau' -and [string]$_.kind.kind -ceq 'pickup' })
    if ($guardian.Count -ne 1 -or $trap.Count -ne 1 -or $watcher.Count -ne 1 -or $pickup.Count -ne 1) { throw 'Final physics approach requires the complete public authoring recipe.' }
    foreach ($entry in @(
        @{node=$guardian[0]; x=16.0; z=-270.0}, @{node=$trap[0]; x=16.0; z=-270.0},
        @{node=$watcher[0]; x=19.0; z=-269.0}, @{node=$pickup[0]; x=18.0; z=-268.0}
    )) {
        if (-not [double]::IsFinite([double]$entry.node.position.x) -or -not [double]::IsFinite([double]$entry.node.position.y) -or -not [double]::IsFinite([double]$entry.node.position.z) -or
            [Math]::Abs([double]$entry.node.position.x - $entry.x) -gt 0.001 -or
            [Math]::Abs([double]$entry.node.position.z - $entry.z) -gt 0.001) { throw 'Final physics approach recipe coordinates differ from the reviewed scenario.' }
    }
    if ([string]$guardian[0].role -cne 'enemy' -or [bool]$guardian[0].flees_danger -or
        [string]$watcher[0].role -cne 'friendly' -or -not [bool]$watcher[0].flees_danger -or
        [double]$trap[0].physics.mass_kg -ne 0 -or
        [Math]::Abs([double]$trap[0].physics.body_half_extents_m.x - 0.3) -gt 0.001 -or
        [Math]::Abs([double]$trap[0].physics.body_half_extents_m.z - 0.3) -gt 0.001) { throw 'Final physics approach requires the unchanged fixed trap and authored guardian/watcher roles.' }
    return [ordered]@{
        guardian_id=[string]$guardian[0].id; trap_id=[string]$trap[0].id; watcher_id=[string]$watcher[0].id; pickup_id=[string]$pickup[0].id
        guardian_position=$guardian[0].position; trap_position=$trap[0].position; watcher_position=$watcher[0].position; pickup_position=$pickup[0].position
        forest_route_end=[ordered]@{x=14.7;z=-270.0}
        guardian_contact_waypoints=@(@{x=14.7;z=-270.0;tolerance_m=0.35},@{x=14.7;z=-271.3;tolerance_m=0.35},@{x=16.0;z=-271.3;tolerance_m=0.35},@{x=16.0;z=-270.60;tolerance_m=0.05})
        pickup_waypoints=@(@{x=14.7;z=-271.3},@{x=14.7;z=-267.2},@{x=17.2;z=-267.2})
        guardian_approach_tolerance_m=0.05
        route_claim='Public WASD detour west/south of the fixed trap and west/north of the watcher; actual grounded positions and action acknowledgements required.'
    }
}

function Get-PlaytestPublicKccMovement {
    param([Parameter(Mandatory)][string]$Status)
    $number = '[+-]?\d+(?:[.,]\d+)?'
    $match = [regex]::Match($Status, "dernier mouvement observé tick (?<tick>\d+).*?demandé X/Z \((?<rx>$number), (?<rz>$number)\) m, résolu \((?<ax>$number), (?<az>$number)\) m, (?<contacts>\d+) contact")
    if (-not $match.Success) { return $null }
    $rx = Convert-PlaytestNumber -Text $match.Groups['rx'].Value
    $rz = Convert-PlaytestNumber -Text $match.Groups['rz'].Value
    $ax = Convert-PlaytestNumber -Text $match.Groups['ax'].Value
    $az = Convert-PlaytestNumber -Text $match.Groups['az'].Value
    return [ordered]@{ tick=[int64]$match.Groups['tick'].Value; contacts=[int]$match.Groups['contacts'].Value; requested_distance_m=[Math]::Sqrt($rx*$rx+$rz*$rz); resolved_distance_m=[Math]::Sqrt($ax*$ax+$az*$az) }
}

function Get-PlaytestGuardianContactDecision {
    param([Parameter(Mandatory)]$State, [Parameter(Mandatory)]$Observation, [Parameter(Mandatory)][int64]$BeforeTick)
    $movement = Get-PlaytestPublicKccMovement -Status ([string]$Observation.raw)
    $actionVisible = @($State.actions | Where-Object { $_.text -ceq 'Frapper Gardien de la forêt' -and -not $_.disabled }).Count -eq 1
    $fresh = $null -ne $movement -and $movement.tick -gt $BeforeTick
    $collision = $fresh -and ($movement.contacts -gt 1 -or ($movement.requested_distance_m -gt 0.00001 -and $movement.resolved_distance_m -lt $movement.requested_distance_m * 0.25))
    return [ordered]@{stop_before_next_pulse=($actionVisible -and $collision);kcc=$movement;fresh_movement=$fresh;action_visible=$actionVisible;collision_or_block=$collision}
}

function Wait-PlaytestReleasedHorizontalMotion {
    param([Parameter(Mandatory)]$Socket,[Parameter(Mandatory)][int]$MaxSeconds,[AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($MaxSeconds)
    $previous = $null; $stable = 0
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $state = Get-PlaytestPublicState -Socket $Socket
        $observed = Get-PlaytestObservation -State $state
        if (-not $observed.ok -or -not $observed.simulation_ready -or $null -eq $observed.locomotion) { throw 'Released guardian approach lost its public ready locomotion.' }
        if (@($state.outputs | Where-Object { $_ -match '^Vitalité 0\b' }).Count) { throw 'Released guardian approach observed the defeated player.' }
        $velocity = @($observed.locomotion.velocity)
        if ($velocity.Count -ne 3) { throw 'Released guardian approach has no complete public velocity.' }
        $horizontalSpeed = [Math]::Sqrt([double]$velocity[0]*[double]$velocity[0] + [double]$velocity[2]*[double]$velocity[2])
        if ($null -ne $previous -and [Math]::Abs($observed.x - $previous.x) -lt 0.03 -and [Math]::Abs($observed.z - $previous.z) -lt 0.03 -and [double]::IsFinite($horizontalSpeed) -and $horizontalSpeed -le 0.05) { $stable += 1 } else { $stable = 0 }
        Add-PlaytestJournal -Journal $Journal -Phase 'guardian-outside-before-pulses' -Event 'released_horizontal_observation' -Payload ([ordered]@{observation=$observed;horizontal_speed_m_s=$horizontalSpeed;stable_samples=$stable;grounding_qualification_claim=$false})
        if ($stable -ge 3) { return }
        $previous = $observed
        Start-Sleep -Milliseconds 120
    }
    throw 'Released horizontal movement did not settle before bounded guardian contact pulses.'
}

function Move-PlaytestGuardianContactPulses {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)]$Scenario,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [ValidateRange(1,30)][int]$MaxSeconds = 15,
        [switch]$SkipInitialReleaseSettle
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($MaxSeconds)
    Focus-PlaytestCanvas -Socket $Socket -TimeoutSeconds $MaxSeconds
    if (-not $SkipInitialReleaseSettle) {
        $remaining = [int][Math]::Floor(($deadline - [DateTimeOffset]::UtcNow).TotalSeconds)
        if ($remaining -lt 1) { throw 'Guardian pulse bound expired during public capture.' }
        Wait-PlaytestReleasedHorizontalMotion -Socket $Socket -MaxSeconds ([Math]::Min(4,$remaining)) -Journal $Journal
    }
    $attempt = 0
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $beforeState = Get-PlaytestPublicState -Socket $Socket
        $before = Get-PlaytestObservation -State $beforeState
        if (-not $before.ok -or -not $before.simulation_ready -or $null -eq $before.yaw_degrees) { throw 'Guardian pulse approach lost its ready public position.' }
        if (@($beforeState.outputs | Where-Object { $_ -match '^Vitalité 0\b' }).Count) { throw 'Guardian pulse approach observed the defeated player.' }
        if (-not $before.input_active) {
            $remaining = [int][Math]::Floor(($deadline - [DateTimeOffset]::UtcNow).TotalSeconds)
            if ($remaining -lt 1) { break }
            Focus-PlaytestCanvas -Socket $Socket -TimeoutSeconds $remaining
            continue
        }
        $dx = [double]$Scenario.guardian_position.x - [double]$before.x
        $dz = [double]$Scenario.guardian_position.z - [double]$before.z
        $keys = @(Get-PlaytestRouteKeys -DeltaX $dx -DeltaZ $dz -YawDegrees ([double]$before.yaw_degrees))
        $held = [System.Collections.Generic.List[object]]::new()
        $attempt += 1
        try {
            foreach ($key in $keys) { Send-Gate3PlaytestKeyEvent -Socket $Socket -Type rawKeyDown -Key $key.key -Code $key.code -VirtualKeyCode $key.vk; $held.Add($key) }
            Start-Sleep -Milliseconds 40
        } finally {
            foreach ($key in $held) { Send-Gate3PlaytestKeyEvent -Socket $Socket -Type keyUp -Key $key.key -Code $key.code -VirtualKeyCode $key.vk }
        }
        Start-Sleep -Milliseconds 80
        $afterState = Get-PlaytestPublicState -Socket $Socket
        $after = Get-PlaytestObservation -State $afterState
        if (-not $after.ok -or -not $after.simulation_ready) { throw 'Guardian pulse approach lost readiness after its released key pulse.' }
        $contactDecision = Get-PlaytestGuardianContactDecision -State $afterState -Observation $after -BeforeTick $before.tick
        Add-PlaytestJournal -Journal $Journal -Phase 'guardian_contact_approach' -Event 'public_released_contact_pulse' -Payload ([ordered]@{attempt=$attempt;keys=@($keys.key);hold_ms=40;before=$before;after=$after;contact_decision=$contactDecision})
        if ($contactDecision.stop_before_next_pulse) {
            Add-PlaytestJournal -Journal $Journal -Phase 'guardian_contact_approach' -Event 'stopped_before_public_attack' -Payload ([ordered]@{observation=$after;reason='first fresh public KCC collision/block with attack enabled; no contact success inferred'})
            return
        }
    }
    throw "Guardian released pulses did not expose a fresh public collision/block with attack enabled within $MaxSeconds seconds."
}

function Test-PlaytestExactGuardianContactRefusal {
    param([Parameter(Mandatory)][string]$Message)
    return $Message -ceq "Public gameplay action 'Frapper Gardien de la forêt' was refused: action refused: no physical contact confirmed"
}

function Invoke-PlaytestFirstConfirmedGuardianAttack {
    param([Parameter(Mandatory)]$Socket,[Parameter(Mandatory)]$Scenario,[AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
    $contactRefused = $false
    for ($attempt = 1; $attempt -le 4; $attempt += 1) {
        $remaining = [int][Math]::Floor(($deadline - [DateTimeOffset]::UtcNow).TotalSeconds)
        if ($remaining -lt 1) { break }
        $currentState = Get-PlaytestPublicState -Socket $Socket
        $currentObservation = Get-PlaytestObservation -State $currentState
        if (-not $currentObservation.ok -or -not $currentObservation.simulation_ready) { throw 'Guardian action attempt requires a ready current public observation.' }
        if (@($currentState.outputs | Where-Object { $_ -match '^Vitalité 0\b' }).Count) { throw 'Guardian action attempt observed the defeated player.' }
        $actionReady = @($currentState.actions | Where-Object { $_.text -ceq 'Frapper Gardien de la forêt' -and -not $_.disabled }).Count -eq 1
        if ($contactRefused -or -not $actionReady) {
            Move-PlaytestGuardianContactPulses -Socket $Socket -Scenario $Scenario -Journal $Journal -MaxSeconds ([Math]::Min(15,$remaining)) -SkipInitialReleaseSettle:($attempt -gt 1)
        } else {
            # Availability permits an attempt, never a physical-contact PASS.
            # The public request below must still receive a new real acknowledgement.
            Add-PlaytestJournal -Journal $Journal -Phase 'guardian_contact_approach' -Event 'ready_public_action_attempt_without_contact_success_claim' -Payload ([ordered]@{attempt=$attempt;observation=$currentObservation;action='Frapper Gardien de la forêt';success_claim=$false})
        }
        $remaining = [int][Math]::Floor(($deadline - [DateTimeOffset]::UtcNow).TotalSeconds)
        if ($remaining -lt 1) { break }
        try {
            return (Invoke-PlaytestGameplayAction -Socket $Socket -Text 'Frapper Gardien de la forêt' -TimeoutSeconds ([Math]::Min(15,$remaining)) -Journal $Journal -Phase "attack_1-contact-attempt-$attempt")
        } catch {
            if (-not (Test-PlaytestExactGuardianContactRefusal -Message $_.Exception.Message)) { throw }
            $contactRefused = $true
            Add-PlaytestJournal -Journal $Journal -Phase 'guardian_contact_approach' -Event 'exact_contact_refusal_before_next_released_pulse' -Payload ([ordered]@{attempt=$attempt;message=$_.Exception.Message;state=(Get-PlaytestPublicState -Socket $Socket);success_claim=$false})
        }
    }
    throw 'No public acknowledged Guardian contact attack after at most four attempts within the forty-five-second bound.'
}

function Move-PlaytestFinalPhysicsPickup {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)]$Scenario,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase
    )
    foreach ($point in $Scenario.pickup_waypoints) {
        [void](Move-PlaytestWASDSegment -Socket $Socket -TargetX $point.x -TargetZ $point.z -Tolerance 0.35 -MaxSeconds $TimeoutSeconds -Journal $Journal -Phase "$Phase-detour" -RouteId 'public-trap-watcher-clearance')
    }
    Move-PlaytestToXZ -Socket $Socket -X ([double]$Scenario.pickup_position.x) -Z ([double]$Scenario.pickup_position.z) -TimeoutSeconds $TimeoutSeconds -Journal $Journal -Phase $Phase
}

function Get-PlaytestDoorwayCenteringAnchor {
    param([Parameter(Mandatory)]$Observation)
    $hudX = [double]$Observation.x
    $hudZ = [double]$Observation.z
    if (-not [double]::IsFinite($hudX) -or -not [double]::IsFinite($hudZ)) {
        throw 'Doorway centering has no finite public HUD position.'
    }
    $motion = if ($Observation -is [System.Collections.IDictionary]) { $Observation['locomotion'] } else {
        $motionProperty = $Observation.PSObject.Properties['locomotion']
        if ($null -ne $motionProperty) { $motionProperty.Value } else { $null }
    }
    if ($null -ne $motion) {
        $anchor = if ($motion -is [System.Collections.IDictionary]) { @($motion['anchor']) } else {
            $anchorProperty = $motion.PSObject.Properties['anchor']
            if ($null -ne $anchorProperty) { @($anchorProperty.Value) } else { @() }
        }
        if ($anchor.Count -eq 3 -and @($anchor | Where-Object { -not [double]::IsFinite([double]$_) }).Count -eq 0) {
            return [ordered]@{ x = [double]$anchor[0]; z = [double]$anchor[2]; source = 'public_locomotion_anchor'; hud_x = $hudX; hud_z = $hudZ }
        }
    }
    return [ordered]@{ x = $hudX; z = $hudZ; source = 'public_hud_position'; hud_x = $hudX; hud_z = $hudZ }
}

function Invoke-PlaytestDoorwayCentering {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)]$Scenario,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal
    )
    $maxPulses = 8
    $centerTolerance = 0.10
    $boundSeconds = [Math]::Min(15, $TimeoutSeconds)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($boundSeconds)
    # Use the wall outer face minus the canonical capsule margin as the
    # strict geometry-derived exterior bound; the route staging point remains
    # independent so a settled anchor just past south_clear_z can be accepted.
    $wallOuterBoundZ = [double]$Scenario.door_z - [double]$Scenario.wall_m
    $strictExteriorBoundZ = $wallOuterBoundZ - [double]$Scenario.interior_margin_m
    if (-not [double]::IsFinite($strictExteriorBoundZ) -or [double]$Scenario.interior_margin_m -le 0) {
        throw 'Doorway centering has no strict positive authored exterior margin.'
    }
    $remaining = [int][Math]::Floor(($deadline - [DateTimeOffset]::UtcNow).TotalSeconds)
    if ($remaining -lt 1) { throw 'Doorway centering bound expired before its public settle.' }
    Focus-PlaytestCanvas -Socket $Socket -TimeoutSeconds $remaining
    $remaining = [int][Math]::Floor(($deadline - [DateTimeOffset]::UtcNow).TotalSeconds)
    if ($remaining -lt 1) { throw 'Doorway centering bound expired during public focus.' }
    $settledState = Wait-PlaytestSettled -Socket $Socket -TimeoutSeconds ([Math]::Min($remaining, [int]$boundSeconds)) -Journal $Journal -Phase 'interior-doorway-centering-before-pulses'
    $observation = Get-PlaytestObservation -State $settledState
    if (-not $observation.ok -or -not $observation.simulation_ready -or -not $observation.grounded -or -not $observation.input_active) {
        throw 'Doorway centering requires a ready, grounded public position with active input capture.'
    }
    $anchor = Get-PlaytestDoorwayCenteringAnchor -Observation $observation
    if ([double]$anchor.z -ge $strictExteriorBoundZ) {
        throw "Doorway centering refused an anchor at or beyond the strict exterior bound z=$strictExteriorBoundZ."
    }
    for ($pulse = 1; $pulse -le $maxPulses; $pulse += 1) {
        $remaining = [int][Math]::Floor(($deadline - [DateTimeOffset]::UtcNow).TotalSeconds)
        if ($remaining -lt 1) { break }
        $xError = [double]$Scenario.door_x - [double]$anchor.x
        if ([Math]::Abs($xError) -le $centerTolerance) {
            Add-PlaytestJournal -Journal $Journal -Phase 'interior-doorway-centering' -Event 'doorway_centered' -Payload ([ordered]@{
                    target_x = [double]$Scenario.door_x
                    tolerance_m = $centerTolerance
                    pulses = $pulse - 1
                    observation = $observation
                    anchor = $anchor
                    strict_exterior_bound_z = $strictExteriorBoundZ
                })
            return $settledState
        }
        $remaining = [int][Math]::Floor(($deadline - [DateTimeOffset]::UtcNow).TotalSeconds)
        if ($remaining -lt 1) { break }
        # A center correction must never steer toward the doorway.  The public
        # camera-relative helper remains the sole WASD basis, but only a
        # single lateral key is accepted for this ΔZ=0 pulse.
        $keys = @(Get-PlaytestRouteKeys -DeltaX $xError -DeltaZ 0 -YawDegrees ([double]$observation.yaw_degrees))
        if ($keys.Count -ne 1 -or [string]$keys[0].key -notin @('a', 'd')) {
            throw "Doorway centering requires a lateral-only public key at yaw $($observation.yaw_degrees); refusing a Z or diagonal pulse."
        }
        $held = [System.Collections.Generic.List[object]]::new()
        try {
            foreach ($key in $keys) {
                [void]$held.Add($key)
                Send-Gate3PlaytestKeyEvent -Socket $Socket -Type rawKeyDown -Key $key.key -Code $key.code -VirtualKeyCode $key.vk
            }
            Start-Sleep -Milliseconds 40
        }
        finally {
            foreach ($key in @($held.ToArray())) {
                Send-Gate3PlaytestKeyEvent -Socket $Socket -Type keyUp -Key $key.key -Code $key.code -VirtualKeyCode $key.vk
            }
        }
        $remaining = [int][Math]::Floor(($deadline - [DateTimeOffset]::UtcNow).TotalSeconds)
        if ($remaining -lt 1) { break }
        $beforePulse = $observation
        $beforeAnchor = $anchor
        $settledState = Wait-PlaytestSettled -Socket $Socket -TimeoutSeconds $remaining -Journal $Journal -Phase "interior-doorway-centering-pulse-$pulse"
        $observation = Get-PlaytestObservation -State $settledState
        if (-not $observation.ok -or -not $observation.simulation_ready -or -not $observation.grounded -or -not $observation.input_active) {
            throw "Doorway centering pulse $pulse did not return a ready, grounded public position with active input capture."
        }
        $anchor = Get-PlaytestDoorwayCenteringAnchor -Observation $observation
        if ([double]$anchor.z -ge $strictExteriorBoundZ) {
            throw "Doorway centering pulse $pulse reached the strict exterior bound z=$strictExteriorBoundZ; refusing further movement."
        }
        Add-PlaytestJournal -Journal $Journal -Phase 'interior-doorway-centering' -Event 'doorway_center_pulse' -Payload ([ordered]@{
                pulse = $pulse
                keys = @($keys | ForEach-Object { $_.key })
                hold_ms = 40
                before = $beforePulse
                before_anchor = $beforeAnchor
                after = $observation
                after_anchor = $anchor
                strict_exterior_bound_z = $strictExteriorBoundZ
            })
    }
    $remaining = [int][Math]::Floor(($deadline - [DateTimeOffset]::UtcNow).TotalSeconds)
    if ($remaining -lt 1) { throw 'Doorway centering bound expired before a qualified centered return.' }
    $finalError = [Math]::Abs([double]$Scenario.door_x - [double]$anchor.x)
    if ($finalError -gt $centerTolerance) {
        throw "Doorway centering did not reach x=$($Scenario.door_x) within $centerTolerance m after $maxPulses pulses and $boundSeconds seconds."
    }
    return $settledState
}
function Invoke-PlaytestInteriorVisit {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)]$Scenario,
        [Parameter(Mandatory)][string]$EvidencePath,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Diagnostics,
        [switch]$BeforeQuest
    )
    Add-PlaytestJournal -Journal $Journal -Phase 'interior_visit' -Event 'authored_room_route' -Payload $Scenario
    $start = Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $Socket)
    if (-not $start.ok -or -not $start.simulation_ready) { throw 'Interior approach requires a ready public position.' }
    # Reuse the publicly observed P02 corridor around the imported village
    # geometry and the generic room. No collision or domain state is changed.
    foreach ($point in @(
        @{x=$Scenario.village_x-1.5; z=$Scenario.village_z+1.0; name='npc-west-clearance'},
        @{x=$Scenario.village_x-1.5; z=$Scenario.village_z-2.0; name='npc-south-clearance'},
        @{x=$Scenario.west_clear_x; z=$Scenario.village_z-2.0; name='west-corridor'},
        @{x=$Scenario.west_clear_x; z=$Scenario.south_clear_z; name='west-south'},
        @{x=$Scenario.door_x; z=$Scenario.south_clear_z; name='south-door'},
        @{x=$Scenario.door_x; z=$Scenario.exterior_z; name='door-exterior'}
    )) {
        [void](Move-PlaytestWASDSegment -Socket $Socket -TargetX $point.x -TargetZ $point.z -Tolerance $Scenario.tolerance_m -MaxSeconds $TimeoutSeconds -Journal $Journal -Phase "interior-$($point.name)" -RouteId 'public-room-south-door')
    }
    [void](Wait-PlaytestSettled -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Journal $Journal -Phase 'interior-door-closed')
    [void](Wait-PlaytestCondition -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'closed interior door public action' -Condition { param($s) @($s.actions | Where-Object { $_.text -ceq 'Ouvrir Porte intérieure' -and -not $_.disabled }).Count -eq 1 })
    [void](Capture-PlaytestCheckpoint -Socket $Socket -EvidencePath $EvidencePath -RunNumber 1 -Name 'interior_closed_threshold' -Phase 'interior_visit' -Journal $Journal -Diagnostics $Diagnostics)
    [void](Invoke-PlaytestGameplayAction -Socket $Socket -Text 'Ouvrir Porte intérieure' -TimeoutSeconds $TimeoutSeconds -Journal $Journal -Phase 'interior-open-door')
    [void](Wait-PlaytestCondition -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'opened interior door public action' -Condition { param($s) @($s.actions | Where-Object { $_.text -ceq 'Fermer Porte intérieure' -and -not $_.disabled }).Count -eq 1 })
    # Give the authored exit actor room to leave before the player enters.
    [void](Move-PlaytestWASDSegment -Socket $Socket -TargetX $Scenario.door_x -TargetZ $Scenario.south_clear_z -Tolerance ([Math]::Min(0.10, [double]$Scenario.tolerance_m)) -MaxSeconds $TimeoutSeconds -Journal $Journal -Phase 'interior-player-exit-clearance' -RouteId 'public-room-player-stand-back-after-open')
    Wait-PlaytestAuthoredRoomExitActors -Socket $Socket -Scenario $Scenario -TimeoutSeconds $TimeoutSeconds -Journal $Journal
    [void](Invoke-PlaytestDoorwayCentering -Socket $Socket -Scenario $Scenario -TimeoutSeconds $TimeoutSeconds -Journal $Journal)
    $threshold = Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $Socket)
    [void](Move-PlaytestWASDSegment -Socket $Socket -TargetX $Scenario.interior_x -TargetZ $Scenario.interior_z -Tolerance $Scenario.tolerance_m -MaxSeconds $TimeoutSeconds -Journal $Journal -Phase 'interior-cross-kcc' -RouteId 'public-room-open-threshold' -ArrivalCondition { param($observed,$authored) Test-PlaytestInteriorOccupancy -Observation $observed -Scenario $authored } -ArrivalContext $Scenario)
    $insideState = Wait-PlaytestSettled -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Journal $Journal -Phase 'interior-inside'
    $inside = Get-PlaytestObservation -State $insideState
    if (-not (Test-PlaytestInteriorOccupancy -Observation $inside -Scenario $Scenario)) { throw 'Public grounded KCC anchor did not occupy the canonical room interior on its floor support.' }
    [void](Capture-PlaytestCheckpoint -Socket $Socket -EvidencePath $EvidencePath -RunNumber 1 -Name 'interior_inside' -Phase 'interior_visit' -Journal $Journal -Diagnostics $Diagnostics)
    Add-PlaytestJournal -Journal $Journal -Phase 'interior_visit' -Event 'threshold_to_interior' -Payload ([ordered]@{ threshold=$threshold; interior=$inside; scenario=$Scenario })
    [void](Move-PlaytestWASDSegment -Socket $Socket -TargetX $Scenario.door_x -TargetZ $Scenario.exterior_z -Tolerance $Scenario.tolerance_m -MaxSeconds $TimeoutSeconds -Journal $Journal -Phase 'interior-exit-kcc' -RouteId 'public-room-open-threshold-return')
    [void](Wait-PlaytestSettled -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Journal $Journal -Phase 'interior-outside')
    [void](Wait-PlaytestCondition -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'interior door close action after exit' -Condition { param($s) @($s.actions | Where-Object { $_.text -ceq 'Fermer Porte intérieure' -and -not $_.disabled }).Count -eq 1 })
    [void](Invoke-PlaytestGameplayAction -Socket $Socket -Text 'Fermer Porte intérieure' -TimeoutSeconds $TimeoutSeconds -Journal $Journal -Phase 'interior-close-door')
    [void](Wait-PlaytestCondition -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'interior door closed before save' -Condition { param($s) @($s.actions | Where-Object { $_.text -ceq 'Ouvrir Porte intérieure' -and -not $_.disabled }).Count -eq 1 })
    [void](Capture-PlaytestCheckpoint -Socket $Socket -EvidencePath $EvidencePath -RunNumber 1 -Name 'interior_closed_after_exit' -Phase 'interior_visit' -Journal $Journal -Diagnostics $Diagnostics)
    foreach ($point in @(
        @{x=$Scenario.door_x; z=$Scenario.south_clear_z; name='south'; tolerance=0.75},
        @{x=$Scenario.west_clear_x; z=$Scenario.south_clear_z; name='west-south'; tolerance=0.75},
        @{x=$Scenario.west_clear_x; z=$Scenario.village_z; name='west-village'; tolerance=0.75},
        # The Village point is occupied by the authored NPC capsule. Use
        # the reviewed P02 public action approach bound at this final point.
        @{x=$Scenario.village_x; z=$Scenario.village_z; name='village'; tolerance=1.25}
    )) {
        [void](Move-PlaytestWASDSegment -Socket $Socket -TargetX $point.x -TargetZ $point.z -Tolerance $point.tolerance -MaxSeconds $TimeoutSeconds -Journal $Journal -Phase "interior-return-$($point.name)" -RouteId 'public-room-village-return')
    }
    $returned = Wait-PlaytestSettled -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Journal $Journal -Phase 'interior-return-village'
    $expectedQuest = if ($BeforeQuest) { '^Rapporter le Sceau\s+·\s+Available$' } else { '^Rapporter le Sceau\s+·\s+Completed$' }
    if (@($returned.items | Where-Object { $_ -match $expectedQuest }).Count -ne 1 -or $returned.items -contains 'Sceau × 1') {
        throw 'Interior visit did not preserve the expected quest and Sceau state before save.'
    }
    Add-PlaytestJournal -Journal $Journal -Phase 'interior_visit' -Event 'visited_closed_and_returned' -Payload (Get-PlaytestObservation -State $returned)
}

function Start-ForgeStudioPlaytestProcess {
    param(
        [Parameter(Mandatory)][string]$CandidatePath,
        [Parameter(Mandatory)][string]$CandidateHash,
        [Parameter(Mandatory)][string]$FixtureRoot,
        [Parameter(Mandatory)][string]$EvidencePath,
        [Parameter(Mandatory)][int]$RunNumber,
        [Parameter(Mandatory)][int]$StartupTimeout
    )
    $profile = Join-Path $EvidencePath "webview2-user-data-$RunNumber"
    [void](New-Item -ItemType Directory -Path $profile -Force)
    $reservation = New-LoopbackPortReservation
    $process = $null
    $socket = $null
    try {
        [Environment]::SetEnvironmentVariable('FORGE_AUTHORING_ROOT', $FixtureRoot, 'Process')
        [Environment]::SetEnvironmentVariable('FORGE_AUTHOR_SUBJECT', "tidewater-gate3-gameplay-playtest-smoke-$RunNumber", 'Process')
        [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', "--remote-debugging-address=127.0.0.1 --remote-debugging-port=$($reservation.Port)", 'Process')
        [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER', $profile, 'Process')
        $reservation.Listener.Stop()
        $stdout = Join-Path $EvidencePath "candidate-run-$RunNumber.stdout.log"
        $stderr = Join-Path $EvidencePath "candidate-run-$RunNumber.stderr.log"
        $process = Start-Process -FilePath $CandidatePath -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        [void](Wait-LaunchedExecutableIdentity -Process $process -Candidate $CandidatePath -ExpectedHash $CandidateHash -TimeoutSeconds ([Math]::Min(15, $StartupTimeout)))
        $target = Wait-CdpTarget -Port $reservation.Port -TimeoutSeconds $StartupTimeout -Process $process
        $socket = Connect-CdpSocket -WebSocketUrl ([string]$target.webSocketDebuggerUrl)
        [void](Invoke-CdpCommand -Socket $socket -Method 'Target.activateTarget' -Parameters @{ targetId = [string]$target.id })
        [void](Invoke-CdpCommand -Socket $socket -Method 'Runtime.enable')
        [void](Invoke-CdpCommand -Socket $socket -Method 'Page.enable')
        [void](Invoke-CdpCommand -Socket $socket -Method 'Page.bringToFront')
        [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $StartupTimeout -Description "Studio shell run $RunNumber" -Expression @'
(() => ({ready:document.readyState==='complete'&&!!document.querySelector('.authoring-shell'),terminal:false,reason:'authoring shell pending'}))()
'@)
        return [pscustomobject]@{ Process = $process; Socket = $socket; Reservation = $reservation; Port = $reservation.Port; Profile = $profile; RunNumber = $RunNumber; Forced = $false }
    }
    catch {
        if ($null -ne $socket) { try { $socket.Dispose() } catch {} }
        if ($null -ne $process -and -not $process.HasExited) {
            try { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } catch {}
        }
        if ($null -ne $reservation) { $reservation.Listener.Stop() }
        throw
    }
}

function Stop-ForgeStudioPlaytestProcess {
    param([AllowNull()][object]$Run)
    if ($null -eq $Run) { return [pscustomobject]@{ Forced = $false; Closed = $true } }
    $process = $Run.Process
    $socket = $Run.Socket
    $forced = $false
    $closed = $false
    $gracefulCloseFailure = $null

    # The native host intercepts WM_CLOSE and presents the public authoring
    # confirmation dialog. Keep CDP alive through that interaction so the
    # native frontend draft is explicitly discarded while already-saved
    # archives remain untouched.
    try {
        if ($null -eq $process) { throw 'Owned Studio process handle is missing.' }
        $process.Refresh()
        if (-not $process.HasExited) {
            if ($null -eq $socket -or $socket.State -ne [Net.WebSockets.WebSocketState]::Open) {
                throw 'Owned Studio process is alive but its CDP socket is not open for public close confirmation.'
            }
            Release-PlaytestCapture -Socket $socket -TimeoutSeconds 10
            $process.Refresh()
            if (-not $process.HasExited) {
                $closeAccepted = $process.CloseMainWindow()
                if (-not $closeAccepted) { throw 'Owned Studio window did not accept the public WM_CLOSE request.' }
                try {
                    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds 10 -Description 'native Studio close confirmation' -Expression @'
(() => ({ready:!!document.querySelector('dialog.gold-close-dialog[open]'),terminal:false,reason:'native close dialog pending'}))()
'@)
                    [void](Invoke-UiClick -Socket $socket -Text 'Quitter sans enregistrer' -Selector 'button' -ScopeSelector 'dialog.gold-close-dialog[open]')
                }
                catch {
                    # The public click can close the process before CDP can
                    # return its response. Re-read process state before
                    # treating that disconnect as a cleanup failure.
                    $process.Refresh()
                    if (-not $process.HasExited) { throw }
                }
                $process.Refresh()
                if (-not $process.HasExited) {
                    $closed = $process.WaitForExit(10000)
                } else {
                    $closed = $true
                }
                if (-not $closed) { throw 'Owned Studio process did not exit within 10 seconds after public discard.' }
            }
        } else {
            $closed = $true
        }
    }
    catch {
        $gracefulCloseFailure = $_.Exception.Message
        Write-Verbose "Public Studio close run $($Run.RunNumber): $gracefulCloseFailure"
    }

    # Only dispose CDP after the public close request and bounded process wait;
    # after the discard click the socket may already be disconnected.
    if ($null -ne $socket) {
        try {
            if ($socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
                $closing = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(2))
                try { Wait-NonGenericTask -Task ($socket.CloseAsync([Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'Gate 3 gameplay playtest smoke complete', $closing.Token)) }
                finally { $closing.Dispose() }
            }
        }
        catch { Write-Verbose "CDP close run $($Run.RunNumber): $($_.Exception.Message)" }
        try { $socket.Dispose() } catch { Write-Verbose "CDP dispose run $($Run.RunNumber): $($_.Exception.Message)" }
    }

    # A forced fallback is deliberately reported as failure by the caller.
    # Kill through the owned Process handle after Refresh, avoiding a broad
    # process or window command against an unrelated Studio instance.
    try {
        $process.Refresh()
        if (-not $process.HasExited) {
            $forced = $true
            try { $process.Kill() } catch { Write-Verbose "Owned Studio force-stop run $($Run.RunNumber): $($_.Exception.Message)" }
            try { [void]$process.WaitForExit(10000) } catch { Write-Verbose "Owned Studio force-stop wait run $($Run.RunNumber): $($_.Exception.Message)" }
        }
        $process.Refresh()
        $closed = $process.HasExited
    }
    catch {
        $forced = $true
        $closed = $false
        Write-Verbose "Owned Studio process state run $($Run.RunNumber): $($_.Exception.Message)"
    }
    if ($null -ne $Run.Reservation) { try { $Run.Reservation.Listener.Stop() } catch { Write-Verbose "CDP listener stop run $($Run.RunNumber): $($_.Exception.Message)" } }
    return [pscustomobject]@{ Forced = $forced; Closed = $closed; GracefulCloseFailure = $gracefulCloseFailure }
}

function Close-Gate3PlaytestZone {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)][int]$TimeoutSeconds)
    $toolbar = Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const stops=[...document.querySelectorAll('button')].filter(node=>/^Retour à l[’']édition$/.test(norm(node.textContent)));
  return {count:stops.length,text:stops.length===1?norm(stops[0].textContent):null,disabled:stops.length===1?!!stops[0].disabled:null,gameplay_present:!!document.querySelector('section.authoring-gameplay-controls'),playtest_active:!!document.querySelector('section.authoring-gameplay-controls .gameplay-actions'),buttons:[...document.querySelectorAll('button')].map(node=>({text:norm(node.textContent),disabled:!!node.disabled}))};
})()
'@
    if ($toolbar.gameplay_present -and ($toolbar.count -ne 1 -or ($toolbar.playtest_active -and $toolbar.disabled))) {
        throw "Public Studio return-to-editing button is missing, ambiguous or disabled: $(ConvertTo-CompactJson -Value $toolbar)"
    }
    if ($toolbar.count -eq 1 -and -not $toolbar.disabled) {
        Release-PlaytestCapture -Socket $Socket -TimeoutSeconds $TimeoutSeconds
        [void](Invoke-UiClick -Socket $Socket -Text ([string]$toolbar.text))
        [void](Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'return to authoring after playtest' -Expression @'
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const play=document.querySelector('button.authoring-play');
  const stop=[...document.querySelectorAll('button')].find(node=>/^Retour à l[’']édition$/.test(norm(node.textContent)));
  return {ready:!!play&&!play.disabled&&(!stop||stop.disabled),terminal:false,reason:'play='+!!play+'; stop='+!!stop};
})()
'@)
    }
    [void](Invoke-UiClick -Socket $Socket -Text 'Modules' -Selector '[data-authoring-tab-button="project"]')
    [void](Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'public close map control ready' -Expression @'
(() => {
  const browsers=[...document.querySelectorAll('.module-map-browser')];
  const buttons=[...document.querySelectorAll('.module-map-browser button.module-map-close')];
  if(browsers.length>1||buttons.length>1) return {ready:false,terminal:true,reason:'ambiguous close map control'};
  const error=browsers[0]?.querySelector('[role="alert"]')?.textContent?.trim()??'';
  return {ready:!error&&browsers.length===1&&browsers[0].getAttribute('aria-busy')==='false'&&buttons.length===1&&!buttons[0].disabled,terminal:!!error,reason:error||'close map controls='+buttons.length};
})()
'@)
    [void](Invoke-UiClick -Socket $Socket -Text 'Fermer la carte' -Selector 'button.module-map-close')
    [void](Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'close Tidewater zone' -Expression @'
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const context=norm(document.querySelector('.authoring-context')?.textContent);
  const open=document.documentElement?.getAttribute('data-forge-zone-open');
  const maps=[...document.querySelectorAll('.module-map-browser select[aria-label="Carte"]')];
  if(maps.length>1) return {ready:false,terminal:true,reason:'ambiguous closed map selector'};
  const error=document.querySelector('.module-map-browser [role="alert"]')?.textContent?.trim()??'';
  return {ready:!error&&context==='Aucune carte ouverte'&&open==='false'&&maps.length===1&&maps[0].value==='',terminal:!!error,reason:error||'context='+context+'; open='+open};
})()
'@)
}

function Get-ArchivePlacementCount {
    param([Parameter(Mandatory)][string]$Archive)
    $document = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($Archive))
    return @($document.head.corrections.placements).Count
}

function Get-Gate3PlacementsSnapshot {
    param([Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Placements)
    # Reuse the exact compact representation used by the other authoring proofs.
    # Keep array order and every identity, asset, lock and transform field.
    $canonical = ConvertTo-Json -InputObject @($Placements) -Depth 100 -Compress
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($canonical)
    return [pscustomobject]@{
        Count = $Placements.Count
        CanonicalJson = $canonical
        Sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    }
}

function Assert-Gate3PlacementsPreserved {
    param([Parameter(Mandatory)][object]$Before, [Parameter(Mandatory)][object]$After)
    if ($Before.Count -ne $After.Count -or $Before.Sha256 -cne $After.Sha256 -or
        $Before.CanonicalJson -cne $After.CanonicalJson) {
        throw 'Placement identity or content differs from the pinned source baseline.'
    }
}

function Get-Gate3ArchivePlacementsSnapshot {
    param([Parameter(Mandatory)][string]$Archive)
    $document = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($Archive))
    return Get-Gate3PlacementsSnapshot -Placements @($document.head.corrections.placements)
}

function Assert-Gate3PlacementsLedger {
    param([Parameter(Mandatory)][object]$Ledger, [Parameter(Mandatory)][string]$SourcePin,
        [Parameter(Mandatory)][object]$Baseline)
    # Older receipts have no placement attestation: callers still require and
    # authenticate the original archive bytes before deriving this exact list.
    if ($Ledger.PSObject.Properties.Name -notcontains 'source_placements') { return }
    $receipt = $Ledger.source_placements
    if ([string]$receipt.source_archive_sha256 -cne $SourcePin -or
        $receipt.identity_and_content_preserved -ne $true) {
        throw 'Parent placement attestation does not preserve the pinned source baseline.'
    }
    foreach ($record in @($receipt.before, $receipt.after)) {
        if ($null -eq $record -or $record.count -ne $Baseline.Count -or
            [string]$record.sha256 -cne $Baseline.Sha256 -or
            [string]$record.canonical_json -cne $Baseline.CanonicalJson) {
            throw 'Parent placement attestation differs from the authenticated original archive.'
        }
    }
}

function Get-WorkspaceZoneReference {
    param([Parameter(Mandatory)][string]$Archive, [Parameter(Mandatory)][string]$ZoneId)
    $document = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($Archive))
    $found = @(
        foreach ($project in @($document.head.projects)) {
            foreach ($module in @($project.modules)) {
                foreach ($zone in @($module.zones)) {
                    if ([string]$zone.area_id -ceq $ZoneId) { $zone }
                }
            }
        }
    )
    if ($found.Count -ne 1) { throw 'Expected exactly one managed Tidewater zone in workspace-v1/authoring.json.' }
    return $found[0]
}

if ($requestedSelfTest) {
    foreach ($case in @(
        @{ x = 0; z = -1; yaw = 0; expected = 'w' },
        @{ x = 1; z = 0; yaw = 0; expected = 'd' },
        @{ x = -1; z = 0; yaw = 90; expected = 'w' },
        @{ x = 0; z = -1; yaw = 90; expected = 'd' },
        @{ x = 0; z = 1; yaw = 180; expected = 'w' },
        @{ x = 1; z = -1; yaw = 0; expected = 'w,d' }
    )) {
        $actual = (@(Get-PlaytestRouteKeys -DeltaX $case.x -DeltaZ $case.z -YawDegrees $case.yaw) | ForEach-Object key) -join ','
        if ($actual -cne $case.expected) { throw "Camera-relative WASD basis mismatch: $actual, expected $($case.expected)" }
    }
    $emptyJournal = [System.Collections.Generic.List[object]]::new()
    Add-PlaytestJournal -Journal $emptyJournal -Phase 'binding-self-test' -Event 'empty-journal-accepted' -Payload $null
    if ($emptyJournal.Count -ne 1) { throw 'Public journal did not accept its initial empty collection.' }
    $blockedKcc = Get-PlaytestPublicKccMovement -Status 'Simulation active · Tick 6830 · dernier mouvement observé tick 6830 : demandé X/Z (0.0500, 0.0000) m, résolu (0.0000, 0.0000) m, 20 contact(s) (sol inclus)'
    if ($null -eq $blockedKcc -or $blockedKcc.tick -ne 6830 -or $blockedKcc.contacts -ne 20 -or
        [Math]::Abs($blockedKcc.requested_distance_m - 0.05) -gt 0.000001 -or $blockedKcc.resolved_distance_m -ne 0) {
        throw 'The observed blocked public KCC movement was not parsed exactly.'
    }
    $movingKcc = Get-PlaytestPublicKccMovement -Status 'dernier mouvement observé tick 91 : demandé X/Z (-0,0300, +0,0400) m, résolu (-0,0300, +0,0400) m, 1 contact(s)'
    if ($null -eq $movingKcc -or $movingKcc.tick -ne 91 -or $movingKcc.contacts -ne 1 -or
        [Math]::Abs($movingKcc.requested_distance_m - 0.05) -gt 0.000001 -or
        [Math]::Abs($movingKcc.resolved_distance_m - 0.05) -gt 0.000001) {
        throw 'Signed decimal-comma public KCC movement was not parsed exactly.'
    }
    foreach ($incompleteKcc in @('Simulation active · Tick 91', 'dernier mouvement observé tick 91 : demandé X/Z (0.05, 0.00) m, 20 contact(s)')) {
        if ($null -ne (Get-PlaytestPublicKccMovement -Status $incompleteKcc)) { throw 'Missing public KCC resolution must remain unknown.' }
    }
    $roomScenario = [ordered]@{place_reference_reachable_from_village=$true;interior_min_x=42.6;interior_max_x=49.4;interior_min_z=-147.4;interior_max_z=-142.6;door_z=-148.0;wall_m=0.25;interior_margin_m=0.35;floor_y=6.6496171951293945;floor_support_tolerance_m=0.0001}
    $roomObservation = [ordered]@{ok=$true;simulation_ready=$true;grounded=$true;locomotion=[ordered]@{body_grounded=$true;rapier_grounded=$true;anchor=@(46.14047622680664,6.6496171951293945,-145.84088134765625);ground_height=6.6496171951293945}}
    if (-not (Test-PlaytestInteriorOccupancy -Observation $roomObservation -Scenario $roomScenario)) { throw 'Observed grounded occupancy on the canonical room floor must be accepted independently of the center marker.' }
    $roomObservation.locomotion.anchor[2] = -147.6
    if (Test-PlaytestInteriorOccupancy -Observation $roomObservation -Scenario $roomScenario) { throw 'An anchor inside the doorway clearance must not count as room occupancy.' }
    $roomObservation.locomotion.anchor[2] = -145.84088134765625
    $roomObservation.locomotion.ground_height = 6.7496171951293945
    if (Test-PlaytestInteriorOccupancy -Observation $roomObservation -Scenario $roomScenario) { throw 'A different public support surface must not count as occupancy of this floor.' }
    $roomObservation.locomotion.ground_height = $roomScenario.floor_y
    $roomObservation.grounded = $false
    if (Test-PlaytestInteriorOccupancy -Observation $roomObservation -Scenario $roomScenario) { throw 'An airborne anchor must not count as grounded room occupancy.' }
    $arrivalFixturePath = Join-Path $PSScriptRoot 'fixtures\gate3-public-arrival-observations-20261002.json'
    if ((Get-FileSha256 -LiteralPath $arrivalFixturePath) -cne '6939e77ef6b8bcf1a3429fe7565171fe27ff328ea3e927fbed9c1202bc06749c') { throw 'The immutable public arrival observations differ from their reviewed proof.' }
    $arrivalFixture = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($arrivalFixturePath))
    $roomStop = Get-PlaytestRouteArrivalDecision -Observation $arrivalFixture.room.observation -TargetX 46.0 -TargetZ -145.0 -Tolerance 0.35 -ArrivalCondition { param($observed,$authored) Test-PlaytestInteriorOccupancy -Observation $observed -Scenario $authored } -ArrivalContext $roomScenario
    if (-not $roomStop.stop -or $roomStop.marker_distance_evaluated -or $null -ne $roomStop.distance_m) { throw 'The actual P01-004 interior must stop the movement loop before marker distance/progress/timeout.' }
    $markerStop = Get-PlaytestRouteArrivalDecision -Observation $arrivalFixture.room.observation -TargetX 46.0 -TargetZ -145.0 -Tolerance 0.35
    if ($markerStop.stop -or -not $markerStop.marker_distance_evaluated -or $markerStop.distance_m -le 0.35) { throw 'The preserved P01-004 case must demonstrate why marker-only arrival keeps moving.' }
    $guardianState = [ordered]@{actions=@($arrivalFixture.guardian.actions)}
    $contactStop = Get-PlaytestGuardianContactDecision -State $guardianState -Observation $arrivalFixture.guardian.observation -BeforeTick ($arrivalFixture.guardian.observation.tick - 1)
    if (-not $contactStop.stop_before_next_pulse) { throw 'The actual enabled attack and fresh blocked KCC movement must stop before another pulse.' }
    $staleContact = Get-PlaytestGuardianContactDecision -State $guardianState -Observation $arrivalFixture.guardian.observation -BeforeTick $arrivalFixture.guardian.observation.tick
    if ($staleContact.stop_before_next_pulse) { throw 'Stale public contacts must not count as a fresh collision stop.' }
    $unavailableAttack = Get-PlaytestGuardianContactDecision -State ([ordered]@{actions=@()}) -Observation $arrivalFixture.guardian.observation -BeforeTick ($arrivalFixture.guardian.observation.tick - 1)
    if ($unavailableAttack.stop_before_next_pulse) { throw 'Contacts without an available public attack must not count as a combat approach stop.' }
    if (-not (Test-PlaytestExactGuardianContactRefusal -Message "Public gameplay action 'Frapper Gardien de la forêt' was refused: action refused: no physical contact confirmed")) { throw 'The exact observed physical-contact refusal must permit only a bounded released-pulse retry.' }
    foreach ($refusal in @('action refused: no physical contact confirmed',"Public gameplay action 'Frapper Gardien de la forêt' was refused: action refused: line of sight blocked")) {
        if (Test-PlaytestExactGuardianContactRefusal -Message $refusal) { throw 'A different or incomplete refusal must not trigger the contact retry.' }
    }
    if ((Get-PlaytestPublicActionSequence -Feedback 'Action acceptée · étape 7') -ne 7 -or $null -ne (Get-PlaytestPublicActionSequence -Feedback 'Action acceptée · étape 7 pending')) { throw 'Public action sequence parsing must require the exact acknowledgement.' }
    # Serialized-contract examples only: this never fabricates a UI/runtime
    # result and does not count as candidate evidence.
    $receiptExampleGameplay = ConvertFrom-ExactJson -Json '{"player":"p","actors":[{"id":"g","name":"Gardien de la forêt","role":"enemy"}],"objects":[{"id":"o","name":"Sceau","kind":{"kind":"pickup","item":"i","quantity":1}}],"quests":[{"id":"q","name":"Rapporter le Sceau"}]}'
    $receiptExamples = @(
        foreach ($index in 0..4) {
            [ordered]@{request=[ordered]@{command_id="attack-$index";expected_sequence=$index;actor='p';action=[ordered]@{kind='attack';target='g'}};sequence=$index+1;tick=$index+10;events=@([ordered]@{kind='damage';target='g';contact=$true;amount=20})}
        }
        [ordered]@{request=[ordered]@{command_id='pickup';expected_sequence=5;actor='p';action=[ordered]@{kind='pickup';object='o'}};sequence=6;tick=20;events=@([ordered]@{kind='picked_up';object='o';item='i';quantity=1})}
        [ordered]@{request=[ordered]@{command_id='turn-in';expected_sequence=6;actor='p';action=[ordered]@{kind='choose_dialogue'}};sequence=7;tick=21;events=@([ordered]@{kind='quest_changed';quest='q';status='completed'})}
    )
    $receiptExampleSave = ConvertFrom-ExactJson -Json (ConvertTo-CompactJson -Value ([ordered]@{schema='forge.game-save';version=1;source_hash=('a'*64);area_id='area';state_hash=('b'*64);state=[ordered]@{tick=30;requires_physics_revalidation=$false;last_frame=[ordered]@{frame=[ordered]@{tick=30;source_hash=('a'*64)}};receipts=$receiptExamples;actors=[ordered]@{p=[ordered]@{vitality=100;inventory=[ordered]@{}};g=[ordered]@{vitality=0}};objects=[ordered]@{o=[ordered]@{consumed=$true}};quests=[ordered]@{q='completed'}}}))
    $receiptExampleReport = Get-PlaytestCommittedReceiptReport -SaveDocument $receiptExampleSave -Gameplay $receiptExampleGameplay -ExpectedHeadHash ('a'*64) -AreaId 'area'
    if (-not $receiptExampleReport.passed -or @($receiptExampleReport.attack_receipts).Count -ne 5) { throw 'The coherent serialized contact-attack receipt contract must be accepted.' }
    $receiptExampleSave.state.receipts[0].events[0].contact = $false
    try { [void](Get-PlaytestCommittedReceiptReport -SaveDocument $receiptExampleSave -Gameplay $receiptExampleGameplay -ExpectedHeadHash ('a'*64) -AreaId 'area'); throw 'Receipt without confirmed contact was incorrectly accepted.' }
    catch { if ($_.Exception.Message -cne 'An accepted attack lacks its exact committed contact-damage receipt.') { throw } }
    $receiptExampleSave.state.receipts[0].events[0].contact = $true
    $receiptExampleSave.state.receipts[0].request.command_id = 'attack-1'
    try { [void](Get-PlaytestCommittedReceiptReport -SaveDocument $receiptExampleSave -Gameplay $receiptExampleGameplay -ExpectedHeadHash ('a'*64) -AreaId 'area'); throw 'A duplicated attack receipt was incorrectly accepted.' }
    catch { if ($_.Exception.Message -cne 'Committed attack receipt identities or sequences are duplicated.') { throw } }
    foreach ($name in @(
        'ConvertTo-CompactJson', 'ConvertFrom-ExactJson', 'Invoke-CdpCommand', 'Invoke-CdpExpression',
        'Wait-CdpReady', 'Wait-CdpTarget', 'Connect-CdpSocket', 'New-LoopbackPortReservation',
        'Wait-LaunchedExecutableIdentity', 'Send-CdpMouseClick', 'Invoke-UiClick',
        'Invoke-UiClickInDetailsBySummary', 'Open-DetailsBySummary', 'Copy-IsolatedFixture',
        'Get-ArchiveSnapshot', 'Test-ArchiveSnapshotsEqual', 'Get-FileSha256', 'Get-NormalizedSha256',
        'Assert-DriveEPath', 'Assert-NoExistingReparseAncestor', 'Assert-TreeHasNoReparsePoints',
        'Test-PathWithinDirectory', 'Test-IsWindowsPlatform', 'Save-CdpScreenshot'
    )) {
        [void](Get-Command $name -ErrorAction Stop)
    }
    foreach ($name in @('Open-Gate3PlaytestZone', 'Get-PlaytestPublicState', 'Get-PlaytestObservation',
        'Save-PlaytestPublicDiagnostic', 'Capture-PlaytestCheckpoint', 'Send-Gate3PlaytestKeyEvent',
        'Get-PlaytestRouteKeys', 'Get-PlaytestRouteArrivalDecision', 'Move-PlaytestAxis', 'Move-PlaytestToXZ',
        'Move-PlaytestWASDSegment', 'Move-PlaytestRoute', 'Wait-PlaytestSettled', 'Wait-PlaytestLocomotionSettled', 'Invoke-PlaytestGameplayAction',
        'Get-PlaytestInteriorScenario', 'Test-PlaytestInteriorOccupancy', 'Get-PlaytestCommittedReceiptReport', 'Get-PlaytestPublicActionSequence', 'Invoke-PlaytestInteriorVisit', 'Get-PlaytestFinalPhysicsScenario', 'Move-PlaytestFinalPhysicsPickup',
        'Get-PlaytestPublicKccMovement', 'Get-PlaytestGuardianContactDecision', 'Wait-PlaytestReleasedHorizontalMotion', 'Move-PlaytestGuardianContactPulses', 'Test-PlaytestExactGuardianContactRefusal', 'Invoke-PlaytestFirstConfirmedGuardianAttack',
        'Start-ForgeStudioPlaytestProcess', 'Stop-ForgeStudioPlaytestProcess')) {
        [void](Get-Command $name -ErrorAction Stop)
    }
    $raw = [IO.File]::ReadAllText($PSCommandPath)
    foreach ($needle in @(
        'Jouer', "Retour à l’édition", 'forge-authoring-canvas', 'authoring-playtest-status',
        'studio-input-capture-status', 'studio-keyboard-event-status', 'Exploration active',
        "J’accepte.", "Voici l’objet demandé.", 'Frapper Gardien de la forêt', 'Ramasser Sceau',
        'Sauvegarder la partie', 'Reprendre la partie', 'Inventaire et objectifs',
        'Input.dispatchKeyEvent', 'rawKeyDown', 'keyUp', 'public_camera_yaw_eight_wasd_directions',
        'run1-start_ready', 'run1-save', 'run2-resume_ready', 'run1-failure', 'run2-failure',
        'run2_forest_revisit', 'canonical-terrain-probe.v2.json', $canonicalProbeSha256,
        'simultaneous_keys', 'route_waypoint_reached', 'gate_qualified = $false', 'navigation', 'P05',
        'public_ui_diagnostics', 'locomotion_observation', 'pickup_arrival_locomotion', 'pickup_post_release',
        'source_preserved', 'process.Refresh()', 'CloseMainWindow()',
        'dialog.gold-close-dialog[open]', 'Quitter sans enregistrer', 'WaitForExit(10000)',
        'GracefulCloseFailure'
    )) {
        if ($raw.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "Playtest smoke selector/limit missing: $needle" }
    }
    if ($raw -match 'Runtime\.evaluate[^\r\n]*window\.__') { throw 'SelfTest found a domain-state injection pattern.' }
    $navRoomExitContract=[pscustomobject]@{door_x=46;door_opening_width_m=1.4;exterior_z=-149.25;interior_z=-145;interior_margin_m=0.35}
    $navActorProfileContract=[pscustomobject]@{radius_m=0.4}
    if (-not (Test-PlaytestNavigationActorOutsideDoorwayCorridor -Actor ([pscustomobject]@{feet=@(49,6,-150.5)}) -Profile $navActorProfileContract -Scenario $navRoomExitContract)) { throw 'Declared future stationary NPC target was rejected by the doorway corridor guard.' }
    if (Test-PlaytestNavigationActorOutsideDoorwayCorridor -Actor ([pscustomobject]@{feet=@(46,6,-149.5)}) -Profile $navActorProfileContract -Scenario $navRoomExitContract) { throw 'A stationary NPC capsule inside the upcoming player corridor was accepted.' }
    foreach ($placementCount in @(21, 23)) {
        $placementEntries = @(1..$placementCount | ForEach-Object { [ordered]@{id="placement-$_"; transform=[ordered]@{x=$_; y=0; z=0}} })
        $placementBaseline = Get-Gate3PlacementsSnapshot -Placements $placementEntries
        Assert-Gate3PlacementsPreserved -Before $placementBaseline -After (Get-Gate3PlacementsSnapshot -Placements @(ConvertFrom-ExactJson -Json $placementBaseline.CanonicalJson))
        $placementRecord = [ordered]@{count=$placementBaseline.Count; sha256=$placementBaseline.Sha256; canonical_json=$placementBaseline.CanonicalJson}
        $placementLedger = ConvertFrom-ExactJson -Json (ConvertTo-Json -Depth 100 -Compress -InputObject ([ordered]@{source_placements=[ordered]@{source_archive_sha256=('a'*64); before=$placementRecord; after=$placementRecord; identity_and_content_preserved=$true}}))
        Assert-Gate3PlacementsLedger -Ledger $placementLedger -SourcePin ('a'*64) -Baseline $placementBaseline
        Assert-Gate3PlacementsLedger -Ledger ([pscustomobject]@{schema='historical-ledger'}) -SourcePin ('a'*64) -Baseline $placementBaseline
        $placementLedger.source_placements.after.sha256 = 'b'*64
        $refused = $false
        try { Assert-Gate3PlacementsLedger -Ledger $placementLedger -SourcePin ('a'*64) -Baseline $placementBaseline }
        catch { $refused = $_.Exception.Message -ceq 'Parent placement attestation differs from the authenticated original archive.' }
        if (-not $refused) { throw 'A changed placement attestation was not rejected.' }
        $changedPlacements = @(ConvertFrom-ExactJson -Json $placementBaseline.CanonicalJson)
        $changedPlacements[0].id = 'different-source-placement'
        $refused = $false
        try { Assert-Gate3PlacementsPreserved -Before $placementBaseline -After (Get-Gate3PlacementsSnapshot -Placements $changedPlacements) }
        catch { $refused = $_.Exception.Message -ceq 'Placement identity or content differs from the pinned source baseline.' }
        if (-not $refused) { throw 'A same-count placement identity change was not rejected.' }
    }
    Write-Output 'TIDEWATER_GATE3_GAMEPLAY_PLAYTEST_SMOKE_SELF_TEST_PASS'
    return
}

if (-not (Test-IsWindowsPlatform)) { throw 'Windows/WebView2 is required.' }
if ($UseGuardianContactPulses -and -not $UseFinalPhysicsApproach) { throw 'Guardian pulses require the explicit final physics approach.' }
if ($VisitInteriorBeforeQuest -and -not $VisitInteriorBeforeSave) { throw 'The before-quest room visit requires the requested interior journey.' }
$candidate = [IO.Path]::GetFullPath($Candidate)
$source = [IO.Path]::GetFullPath($FreshIsolatedFixture)
$ledgerInput = [IO.Path]::GetFullPath($AuthoringLedger)
$output = [IO.Path]::GetFullPath($EvidenceRoot)
$sourcePin = Get-NormalizedSha256 -Value $SourceOriginalHashPinned
foreach ($entry in @(@($candidate, 'Candidate'), @($source, 'Fresh isolated authored fixture'), @($ledgerInput, 'Authoring ledger'), @($output, 'Evidence root'))) {
    Assert-Gate3ProofPath -LiteralPath $entry[0] -Description $entry[1]
}
if (Test-Path -LiteralPath $output) { throw "EvidenceRoot must be fresh: $output" }
if ((Test-PathWithinDirectory -LiteralPath $output -Directory $source) -or (Test-PathWithinDirectory -LiteralPath $source -Directory $output)) { throw 'EvidenceRoot and authored fixture must remain separate.' }
if ((Test-PathWithinDirectory -LiteralPath $output -Directory (Split-Path -Parent $candidate)) -or (Test-PathWithinDirectory -LiteralPath (Split-Path -Parent $candidate) -Directory $output)) { throw 'EvidenceRoot and candidate directory must remain separate.' }
if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Candidate is absent: $candidate" }
if (-not (Test-Path -LiteralPath $ledgerInput -PathType Leaf)) { throw "Authoring ledger is absent: $ledgerInput" }
if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "Authored fixture is absent: $source" }
Assert-TreeHasNoReparsePoints -LiteralPath $source -Description 'Fresh authored Tidewater fixture'
$candidateHash = Get-FileSha256 -LiteralPath $candidate
$authoring = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($ledgerInput))
if ([string]$authoring.schema -cne 'forge.tidewater.gate3-gameplay-authoring-smoke' -or [string]$authoring.status -cne 'passed') { throw 'Authoring ledger is not the passed Gate 3 gameplay authoring smoke.' }
if ([bool]$authoring.gate_qualified) { throw 'Authoring ledger incorrectly claims Gate qualification.' }
if (-not [bool]$authoring.source_preserved) { throw 'Authoring ledger does not attest source preservation.' }
if ([string]$authoring.zone_uuid -cne $zoneId) { throw 'Authoring ledger zone UUID differs from Tidewater.' }
if (-not ([IO.Path]::GetFullPath([string]$authoring.isolated_fixture)).Equals($source, [StringComparison]::OrdinalIgnoreCase)) { throw 'FreshIsolatedFixture is not the authorized fixture recorded by the authoring ledger.' }
if ($authoring.PSObject.Properties.Name -contains 'source_original_hash_pinned' -and [string]$authoring.source_original_hash_pinned -cne $sourcePin) { throw 'Source hash pin differs from the authoring ledger.' }
if ($authoring.PSObject.Properties.Name -contains 'candidate_sha256' -and [string]$authoring.candidate_sha256 -cne $candidateHash) { throw 'Candidate hash differs from the authoring ledger pin.' }

$sourceArchive = Join-Path (Join-Path $source $zoneId) 'authoring.json'
$sourceWorkspaceArchive = Join-Path $source 'workspace-v1\authoring.json'
$sourceObject = Join-Path (Join-Path $source 'workspace-v1\objects') "$sourcePin.json"
$sourceBefore = Get-ArchiveSnapshot -LiteralPath $sourceArchive
$sourceWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $sourceWorkspaceArchive
$sourceObjectBefore = Get-ArchiveSnapshot -LiteralPath $sourceObject
if ($sourceBefore.Sha256 -cne $sourcePin -or $sourceObjectBefore.Sha256 -cne $sourcePin) { throw 'Authored fixture does not preserve the pinned source object bytes.' }
$sourcePlacementsBefore = Get-Gate3ArchivePlacementsSnapshot -Archive $sourceArchive
Assert-Gate3PlacementsPreserved -Before $sourcePlacementsBefore -After (Get-Gate3ArchivePlacementsSnapshot -Archive $sourceObject)
Assert-Gate3PlacementsLedger -Ledger $authoring -SourcePin $sourcePin -Baseline $sourcePlacementsBefore
$reference = Get-WorkspaceZoneReference -Archive $sourceWorkspaceArchive -ZoneId $zoneId
$authoredArchivePin = Get-NormalizedSha256 -Value ([string]$authoring.saved_reference.archive_hash)
if ([string]$reference.archive_hash -cne $authoredArchivePin -or
    [string]$reference.head_hash -cne [string]$authoring.saved_reference.head_hash) {
    throw 'Workspace-v1 does not reference the Studio-authored gameplay checkpoint.'
}
$sourceAuthoredObject = Join-Path (Join-Path $source 'workspace-v1/objects') "$authoredArchivePin.json"
$sourceAuthoredBefore = Get-ArchiveSnapshot -LiteralPath $sourceAuthoredObject
if ($sourceAuthoredBefore.Sha256 -cne $authoredArchivePin) { throw 'The authored checkpoint differs from the passed authoring ledger.' }
Assert-Gate3PlacementsPreserved -Before $sourcePlacementsBefore -After (Get-Gate3ArchivePlacementsSnapshot -Archive $sourceAuthoredObject)
$interiorScenario = $null
if ($VisitInteriorBeforeSave) {
    if ($authoring.PSObject.Properties.Name -notcontains 'room_applied' -or -not [bool]$authoring.room_applied) {
        throw 'Interior visit requires a passed authoring ledger that applied the generic room.'
    }
    $interiorScenario = Get-PlaytestInteriorScenario -Archive $sourceAuthoredObject
}
$finalPhysicsScenario = $null
if ($UseFinalPhysicsApproach) {
    $finalPhysicsScenario = Get-PlaytestFinalPhysicsScenario -Archive $sourceAuthoredObject
    # Preserve the exact canonical probe in provenance, and explicitly record
    # this actual final detour around the new overhead trap at its endpoint.
    $playtestRouteWaypoints = @($playtestRouteWaypoints | Select-Object -SkipLast 1) + @([pscustomobject]$finalPhysicsScenario.forest_route_end)
}

[void](New-Item -ItemType Directory -Path $output)
[IO.File]::Copy($playtestNavigationReaderPath,(Join-Path $output 'navigation-reader-source.ps1'))
if ((Get-FileHash -LiteralPath (Join-Path $output 'navigation-reader-source.ps1') -Algorithm SHA256).Hash.ToLowerInvariant() -cne $playtestNavigationReaderHashAtStart) { throw 'Public navigation reader changed after it was loaded for this playtest.' }
$outputCreated = $true
$fixture = Join-Path $output 'playtest-root'
$cloneArchive = Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $zoneId
$cloneBefore = Get-ArchiveSnapshot -LiteralPath $cloneArchive
$cloneWorkspace = Join-Path $fixture 'workspace-v1\authoring.json'
$cloneWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $cloneWorkspace
$cloneOriginal = Join-Path (Join-Path $fixture 'workspace-v1\objects') "$sourcePin.json"
$cloneOriginalBefore = Get-ArchiveSnapshot -LiteralPath $cloneOriginal
$cloneAuthored = Join-Path (Join-Path $fixture 'workspace-v1/objects') "$authoredArchivePin.json"
if ((Get-ArchiveSnapshot -LiteralPath $cloneAuthored).Sha256 -cne $authoredArchivePin) { throw 'Clone did not preserve the Studio-authored checkpoint.' }
if ($cloneBefore.Sha256 -cne $sourceBefore.Sha256 -or $cloneWorkspaceBefore.Sha256 -cne $sourceWorkspaceBefore.Sha256 -or $cloneOriginalBefore.Sha256 -cne $sourceObjectBefore.Sha256) { throw 'Playtest fixture copy does not preserve the authorized source bytes.' }

$savedEnvironment = @{}
foreach ($name in @('FORGE_AUTHORING_ROOT', 'FORGE_AUTHOR_SUBJECT', 'WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', 'WEBVIEW2_USER_DATA_FOLDER')) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$journal = [System.Collections.Generic.List[object]]::new()
$uiDiagnostics = [System.Collections.Generic.List[object]]::new()
$routeMaxSeconds = [Math]::Min(360, [Math]::Max(120, $InteractionTimeoutSeconds * 2))
# Stop the NPC-clearance waypoints within 0.75 m.  The previous 1.5 m bound
# accepted a point still touching the reviewed NPC capsule on the sloped
# village triangle; the stricter public coordinate bound keeps the same
# reviewed polyline while leaving the next segment room to proceed.
$routeToleranceM = 0.75
$forestToVillageWaypoints = @($playtestRouteWaypoints.Clone())
[array]::Reverse($forestToVillageWaypoints)
$run1 = $null
$run2 = $null
$failure = $null
$forcedProcessStop = $false
$run1Stopped = $true
$run2Stopped = $true
$run1Completed = $false
$run2Completed = $false
$resumeInitialCameraDirectionVerified = $false
$interiorVisitCompleted = $false
$committedReceiptReport = $null

try {
    $run1 = Start-ForgeStudioPlaytestProcess -CandidatePath $candidate -CandidateHash $candidateHash -FixtureRoot $fixture -EvidencePath $output -RunNumber 1 -StartupTimeout $StartupTimeoutSeconds
    $run1Stopped = $false
    Open-Gate3PlaytestZone -Socket $run1.Socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Invoke-UiClick -Socket $run1.Socket -Text 'Tester & suivi' -Selector '[data-authoring-tab-button]')
    [void](Invoke-UiClick -Socket $run1.Socket -Text 'Jouer' -Selector 'button.authoring-play')
    # Preserve the historical gesture-first mode. The explicit V6 path waits
    # for reconstruction before requesting pointer capture: an earlier click
    # can be ignored or released during reconstruction while Ready later holds.
    if (-not $WaitForReadyBeforeInitialFocus) {
        Focus-PlaytestCanvas -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds
    }
    [void](Wait-PlaytestReady -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'start public Tidewater playtest')
    $preCapture = Capture-PlaytestCheckpoint -Socket $run1.Socket -EvidencePath $output -RunNumber 1 -Name 'before_input_capture' -Phase 'run1' -Journal $journal -Diagnostics $uiDiagnostics
    Add-PlaytestJournal -Journal $journal -Phase 'run1' -Event 'before_input_capture' -Payload ([ordered]@{ observation = Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $run1.Socket); ui_checkpoint = $preCapture })
    if ($WaitForReadyBeforeInitialFocus) {
        Focus-PlaytestCanvas -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds
    }
    $startReadyCapture = Capture-PlaytestCheckpoint -Socket $run1.Socket -EvidencePath $output -RunNumber 1 -Name 'start_ready' -Phase 'run1' -Journal $journal -Diagnostics $uiDiagnostics
    Add-PlaytestJournal -Journal $journal -Phase 'run1' -Event 'playtest_started' -Payload ([ordered]@{ observation = Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $run1.Socket); ui_checkpoint = $startReadyCapture })
    Observe-PlaytestFacing -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run1'

    if ($VisitInteriorBeforeQuest) {
        Invoke-PlaytestInteriorVisit -Socket $run1.Socket -Scenario $interiorScenario -EvidencePath $output -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Diagnostics $uiDiagnostics -BeforeQuest
        $interiorVisitCompleted = $true
    }

    Move-PlaytestToXZ -Socket $run1.Socket -X 40 -Z -118 -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'village'
    [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'village')
    [void](Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'village dialogue accept action' -Condition { param($s) @($s.actions | Where-Object { $_.text -ceq "J’accepte." }).Count -eq 1 })
    [void](Invoke-PlaytestGameplayAction -Socket $run1.Socket -Text "J’accepte." -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'quest_accept')
    [void](Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'quest active HUD line' -Condition { param($s) @($s.items | Where-Object { $_ -match '^Rapporter le Sceau\s+·\s+Active$' }).Count -eq 1 })

    [void](Move-PlaytestRoute -Socket $run1.Socket -Waypoints $playtestRouteWaypoints -Tolerance $routeToleranceM -MaxSeconds $routeMaxSeconds -Journal $journal -Phase 'forest' -RouteId 'village-npc-clearance-then-canonical-forest')
    # Attack uses a confirmed Rapier contact, rather than range alone. Move
    # the player capsule up to the guard's capsule. The generic player
    # profile has a 0.35 m radius (the optional imported-ocean profile 0.30),
    # the NPC radius is 0.35 and the controller skin 0.01. Positive action
    # acknowledgement below remains mandatory; proximity alone is no PASS.
    if ($UseFinalPhysicsApproach) {
        Add-PlaytestJournal -Journal $journal -Phase 'guardian_contact_approach' -Event 'final_physics_outside_approach' -Payload $finalPhysicsScenario
        $guardianWaypoints = if ($UseGuardianContactPulses) { @($finalPhysicsScenario.guardian_contact_waypoints | Select-Object -SkipLast 1) } else { $finalPhysicsScenario.guardian_contact_waypoints }
        foreach ($point in $guardianWaypoints) {
            [void](Move-PlaytestWASDSegment -Socket $run1.Socket -TargetX $point.x -TargetZ $point.z -Tolerance $point.tolerance_m -MaxSeconds 30 -Journal $journal -Phase 'guardian_contact_approach' -RouteId 'public-guardian-outside-trap')
        }
    } else {
        [void](Move-PlaytestWASDSegment -Socket $run1.Socket -TargetX 16 -TargetZ -270 -Tolerance 0.72 -MaxSeconds 30 -Journal $journal -Phase 'guardian_contact_approach' -RouteId 'public-guardian-contact')
    }
    if (-not $UseFinalPhysicsApproach) {
        [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'forest')
    }
    [void](Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'forest attack action' -Condition { param($s) @($s.actions | Where-Object { $_.text -ceq 'Frapper Gardien de la forêt' -and -not $_.disabled }).Count -eq 1 })
    for ($attack = 1; $attack -le 5; $attack += 1) {
        if ($UseGuardianContactPulses -and $attack -eq 1) {
            [void](Invoke-PlaytestFirstConfirmedGuardianAttack -Socket $run1.Socket -Scenario $finalPhysicsScenario -Journal $journal)
        } else {
            [void](Invoke-PlaytestGameplayAction -Socket $run1.Socket -Text 'Frapper Gardien de la forêt' -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase "attack_$attack")
        }
        Add-PlaytestJournal -Journal $journal -Phase 'forest' -Event 'attack_count' -Payload $attack
    }
    [void](Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'guardian defeated HUD action disappearance' -Condition { param($s) @($s.actions | Where-Object { $_.text -ceq 'Frapper Gardien de la forêt' }).Count -eq 0 })
    [void](Capture-PlaytestCheckpoint -Socket $run1.Socket -EvidencePath $output -RunNumber 1 -Name 'guardian_defeated' -Phase 'forest' -Journal $journal -Diagnostics $uiDiagnostics)

    if ($UseFinalPhysicsApproach) {
        Move-PlaytestFinalPhysicsPickup -Socket $run1.Socket -Scenario $finalPhysicsScenario -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'pickup'
    } else {
        Move-PlaytestToXZ -Socket $run1.Socket -X 18 -Z -268 -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'pickup'
    }
    # The reviewed Sceau point is on a visibly sloped terrain triangle.  A
    # grounded capsule can slide out of interaction range while waiting for
    # the generic settled heuristic, so observe the public action immediately
    # after the public WASD approach and require readiness/grounding there.
    $pickupReadyState = Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'seal pickup action' -Condition {
        param($s)
        $observation = Get-PlaytestObservation -State $s
        $observation.ok -and $observation.simulation_ready -and $observation.grounded -and
            @($s.actions | Where-Object { $_.text -ceq 'Ramasser Sceau' -and -not $_.disabled }).Count -eq 1
    }
    Add-PlaytestJournal -Journal $journal -Phase 'pickup' -Event 'pickup_arrival_locomotion' -Payload ([ordered]@{
            observation = Get-PlaytestObservation -State $pickupReadyState
            locomotion = $pickupReadyState.locomotion
            locomotion_error = $pickupReadyState.locomotion_error
            action = 'Ramasser Sceau'
        })
    [void](Invoke-PlaytestGameplayAction -Socket $run1.Socket -Text 'Ramasser Sceau' -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'pickup')
    [void](Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'seal inventory line' -Condition { param($s) $s.items -contains 'Sceau × 1' })
    [void](Wait-PlaytestLocomotionSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'pickup_post_release' -SampleMilliseconds 120)

    [void](Move-PlaytestRoute -Socket $run1.Socket -Waypoints $forestToVillageWaypoints -Tolerance $routeToleranceM -MaxSeconds $routeMaxSeconds -Journal $journal -Phase 'quest_turn_in' -RouteId 'canonical-forest-village')
    [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'quest_turn_in')
    [void](Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'quest turn-in dialogue action' -Condition { param($s) @($s.actions | Where-Object { $_.text -ceq "Voici l’objet demandé." -and -not $_.disabled }).Count -eq 1 })
    [void](Invoke-PlaytestGameplayAction -Socket $run1.Socket -Text "Voici l’objet demandé." -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'quest_turn_in')
    [void](Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'quest completed HUD line and item consumption' -Condition { param($s) (@($s.items | Where-Object { $_ -match '^Rapporter le Sceau\s+·\s+Completed$' }).Count -eq 1) -and ($s.items -notcontains 'Sceau × 1') })

    if ($VisitInteriorBeforeSave -and -not $VisitInteriorBeforeQuest) {
        Invoke-PlaytestInteriorVisit -Socket $run1.Socket -Scenario $interiorScenario -EvidencePath $output -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Diagnostics $uiDiagnostics
        $interiorVisitCompleted = $true
    }

    [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'save')
    $gameSaveDirectory = Join-Path $fixture '.game-saves'
    $gameSaveHashesBefore = @{}
    if (Test-Path -LiteralPath $gameSaveDirectory -PathType Container) {
        Assert-TreeHasNoReparsePoints -LiteralPath $gameSaveDirectory -Description 'Owned game-save output before public Save'
        foreach ($file in @(Get-ChildItem -LiteralPath $gameSaveDirectory -File -Recurse)) { $gameSaveHashesBefore[$file.FullName] = Get-FileSha256 -LiteralPath $file.FullName }
    }
    Release-PlaytestCapture -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Invoke-UiClick -Socket $run1.Socket -Text 'Sauvegarder la partie')
    [void](Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'public gameplay save acknowledgement' -Condition { param($s) @($s.outputs | Where-Object { $_ -ceq 'Partie sauvegardée.' }).Count -ge 1 })
    Assert-TreeHasNoReparsePoints -LiteralPath $gameSaveDirectory -Description 'Owned committed public game-save output'
    $committedSaveFiles = @(Get-ChildItem -LiteralPath $gameSaveDirectory -Filter 'authoring.json' -File -Recurse | Where-Object { $_.Directory.Name -ceq $zoneId })
    if ($committedSaveFiles.Count -ne 1) { throw 'The public Save did not create exactly one owned Tidewater save file.' }
    $committedSavePath = $committedSaveFiles[0].FullName
    $committedSaveHash = Get-FileSha256 -LiteralPath $committedSavePath
    if ($gameSaveHashesBefore.ContainsKey($committedSavePath) -and $gameSaveHashesBefore[$committedSavePath] -ceq $committedSaveHash) { throw 'The acknowledged public Save did not change its committed file.' }
    $savedDocument = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($committedSavePath))
    $authoredDocument = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($sourceAuthoredObject))
    $committedReceiptReport = Get-PlaytestCommittedReceiptReport -SaveDocument $savedDocument -Gameplay $authoredDocument.head.corrections.gameplay -ExpectedHeadHash ([string]$reference.head_hash) -AreaId $zoneId -RequireInteriorDoorCycle:$VisitInteriorBeforeSave
    $publicAttackSequences = @($journal | Where-Object { $_.event -ceq 'public_action_settled' -and [string]$_.payload.text -ceq 'Frapper Gardien de la forêt' } | ForEach-Object { Get-PlaytestPublicActionSequence -Feedback ([string]$_.payload.state.feedback) })
    if ($publicAttackSequences.Count -ne 5 -or (($publicAttackSequences | Sort-Object) -join ',') -cne (($committedReceiptReport.attack_receipts.sequence | Sort-Object) -join ',')) { throw 'The five public attack acknowledgements do not match their committed receipt sequences.' }
    $committedReceiptReport['save_path'] = $committedSavePath
    $committedReceiptReport['save_sha256'] = $committedSaveHash
    $committedReceiptReport['committed_file_changed'] = $true
    $committedReceiptReport['public_attack_acknowledgement_sequences'] = $publicAttackSequences
    $committedReceiptReport['public_acknowledgements_match_committed_receipts'] = $true
    Add-PlaytestJournal -Journal $journal -Phase 'save' -Event 'committed_public_save_receipts_audited' -Payload $committedReceiptReport
    $saveCapture = Capture-PlaytestCheckpoint -Socket $run1.Socket -EvidencePath $output -RunNumber 1 -Name 'save' -Phase 'run1' -Journal $journal -Diagnostics $uiDiagnostics
    Add-PlaytestJournal -Journal $journal -Phase 'run1' -Event 'save_acknowledged' -Payload ([ordered]@{ state = Get-PlaytestPublicState -Socket $run1.Socket; ui_checkpoint = $saveCapture })
    Close-Gate3PlaytestZone -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds
    $run1Completed = $true
}
catch {
    $failure = "run1: $($_.Exception.Message)"
    if ($null -ne $run1 -and $null -ne $run1.Socket) {
        [void](Capture-PlaytestCheckpoint -Socket $run1.Socket -EvidencePath $output -RunNumber 1 -Name 'failure' -Phase 'run1_failure' -Journal $journal -Diagnostics $uiDiagnostics)
    }
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
        [void](Wait-PlaytestCondition -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'resume public gameplay button' -Condition { param($s) @($s.allButtons | Where-Object { $_.text -ceq 'Reprendre la partie' -and -not $_.disabled }).Count -eq 1 })
        [void](Invoke-UiClick -Socket $run2.Socket -Text 'Reprendre la partie')
        [void](Wait-PlaytestCondition -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'resume acknowledgement' -Condition { param($s) @($s.outputs | Where-Object { $_ -ceq 'Reprise préparée.' }).Count -ge 1 })
        [void](Wait-PlaytestReady -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'resumed public Tidewater playtest')
        $preResumeCapture = Capture-PlaytestCheckpoint -Socket $run2.Socket -EvidencePath $output -RunNumber 2 -Name 'before_input_capture' -Phase 'run2' -Journal $journal -Diagnostics $uiDiagnostics
        Add-PlaytestJournal -Journal $journal -Phase 'run2' -Event 'before_input_capture' -Payload ([ordered]@{ observation = Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $run2.Socket); ui_checkpoint = $preResumeCapture })
        if ($VerifyResumeInitialCameraDirection) {
            $initialYaw = $preCapture.public_observation.yaw_degrees
            $resumeYaw = $preResumeCapture.public_observation.yaw_degrees
            if ($null -eq $initialYaw -or $null -eq $resumeYaw -or
                -not [double]::IsFinite([double]$initialYaw) -or -not [double]::IsFinite([double]$resumeYaw)) {
                throw 'Initial and resumed camera direction must be finite public observations.'
            }
            $yawDifference = [Math]::Abs([Math]::IEEERemainder(([double]$resumeYaw - [double]$initialYaw), 360.0))
            if ($yawDifference -gt 0.2) { throw "Resume changed the accepted initial camera direction by $yawDifference degrees." }
            $resumeInitialCameraDirectionVerified = $true
            Add-PlaytestJournal -Journal $journal -Phase 'run2' -Event 'resume_initial_camera_direction_verified' -Payload ([ordered]@{ initial_yaw_degrees = $initialYaw; resumed_yaw_degrees = $resumeYaw; difference_degrees = $yawDifference; tolerance_degrees = 0.2; observation_before_input_capture = $true; saved_camera_state_claim = $false })
        }
        Focus-PlaytestCanvas -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds
        $resumeReadyCapture = Capture-PlaytestCheckpoint -Socket $run2.Socket -EvidencePath $output -RunNumber 2 -Name 'resume_ready' -Phase 'run2' -Journal $journal -Diagnostics $uiDiagnostics
        $resumed = Get-PlaytestPublicState -Socket $run2.Socket
        $resumedObservation = Get-PlaytestObservation -State $resumed
        if (-not $resumedObservation.ok) { throw 'Resumed public playtest did not expose a finite position.' }
        if (@($resumed.items | Where-Object { $_ -match '^Rapporter le Sceau\s+·\s+Completed$' }).Count -ne 1) { throw 'Resumed HUD did not preserve the completed quest.' }
        if ($resumed.items -contains 'Sceau × 1') { throw 'Resumed HUD duplicated or retained the consumed seal.' }
        Add-PlaytestJournal -Journal $journal -Phase 'run2' -Event 'resume_public_state_verified_at_saved_position' -Payload ([ordered]@{ observation = $resumedObservation; items = @($resumed.items); actions = @($resumed.actions); ui_checkpoint = $resumeReadyCapture })
        [void](Move-PlaytestRoute -Socket $run2.Socket -Waypoints $playtestRouteWaypoints -Tolerance $routeToleranceM -MaxSeconds $routeMaxSeconds -Journal $journal -Phase 'run2_forest_revisit' -RouteId 'village-npc-clearance-then-canonical-forest-resume')
        if ($UseFinalPhysicsApproach) {
            [void](Move-PlaytestWASDSegment -Socket $run2.Socket -TargetX $finalPhysicsScenario.forest_route_end.x -TargetZ $finalPhysicsScenario.forest_route_end.z -Tolerance 0.35 -MaxSeconds 30 -Journal $journal -Phase 'run2_forest_revisit' -RouteId 'public-guardian-outside-trap-resume')
        }
        [void](Wait-PlaytestSettled -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run2_forest_revisit')
        $forestResumeState = Wait-PlaytestCondition -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'resumed forest position and defeated/picked-up consequences' -Condition {
            param($current)
            $observation = Get-PlaytestObservation -State $current
            $nearForest = $observation.ok -and
                [Math]::Abs([double]$observation.x - 16.0) -le 1.75 -and
                [Math]::Abs([double]$observation.z + 270.0) -le 1.75
            $guardianAction = @($current.actions | Where-Object { $_.text -eq 'Frapper Gardien de la forêt' })
            $pickupAction = @($current.actions | Where-Object { $_.text -eq 'Ramasser Sceau' })
            $observation.simulation_ready -and $observation.grounded -and $nearForest -and
                $guardianAction.Count -eq 0 -and $pickupAction.Count -eq 0
        }
        $forestResumeObservation = Get-PlaytestObservation -State $forestResumeState
        if (-not $forestResumeObservation.simulation_ready -or -not $forestResumeObservation.grounded) { throw 'Resumed forest revisit was not simulation-ready and grounded.' }
        if ([Math]::Abs([double]$forestResumeObservation.x - 16.0) -gt 1.75 -or [Math]::Abs([double]$forestResumeObservation.z + 270.0) -gt 1.75) { throw 'Resumed forest revisit did not reach the public target position.' }
        if (@($forestResumeState.items | Where-Object { $_ -match '^Rapporter le Sceau\s+·\s+Completed$' }).Count -ne 1) { throw 'Quest Completed was not preserved at the resumed forest site.' }
        if ($forestResumeState.items -contains 'Sceau × 1') { throw 'Consumed Sceau reappeared at the resumed forest site.' }
        Add-PlaytestJournal -Journal $journal -Phase 'run2_forest_revisit' -Event 'forest_consequences_verified_near_target' -Payload ([ordered]@{ observation = $forestResumeObservation; items = @($forestResumeState.items); actions = @($forestResumeState.actions); target_x = 16; target_z = -270; tolerance_m = 1.75 })
        if ($UseFinalPhysicsApproach) {
            # The outside Guardian point is farther than the Sceau interaction
            # radius. Revisit the actual pickup site before asserting absence.
            Move-PlaytestFinalPhysicsPickup -Socket $run2.Socket -Scenario $finalPhysicsScenario -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run2_pickup_revisit'
            $pickupResumeState = Wait-PlaytestSettled -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run2_pickup_revisit'
            $pickupResumeObservation = Get-PlaytestObservation -State $pickupResumeState
            if (-not $pickupResumeObservation.ok -or -not $pickupResumeObservation.simulation_ready -or -not $pickupResumeObservation.grounded -or
                [Math]::Abs([double]$pickupResumeObservation.x - 18.0) -gt 1.25 -or [Math]::Abs([double]$pickupResumeObservation.z + 268.0) -gt 1.25 -or
                @($pickupResumeState.actions | Where-Object { $_.text -ceq 'Ramasser Sceau' }).Count -ne 0 -or
                $pickupResumeState.items -contains 'Sceau × 1' -or
                @($pickupResumeState.items | Where-Object { $_ -match '^Rapporter le Sceau\s+·\s+Completed$' }).Count -ne 1) { throw 'Resumed pickup-site visit did not preserve consumed Sceau and Completed.' }
            Add-PlaytestJournal -Journal $journal -Phase 'run2_pickup_revisit' -Event 'pickup_absence_verified_at_pickup_site' -Payload ([ordered]@{ observation=$pickupResumeObservation; actions=@($pickupResumeState.actions); items=@($pickupResumeState.items); tolerance_m=1.25 })
            [void](Capture-PlaytestCheckpoint -Socket $run2.Socket -EvidencePath $output -RunNumber 2 -Name 'pickup_site_absence' -Phase 'run2_pickup_revisit' -Journal $journal -Diagnostics $uiDiagnostics)
        }
        Close-Gate3PlaytestZone -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds
        $run2Completed = $true
    }
    catch {
        $failure = "run2: $($_.Exception.Message)"
        if ($null -ne $run2 -and $null -ne $run2.Socket) {
            [void](Capture-PlaytestCheckpoint -Socket $run2.Socket -EvidencePath $output -RunNumber 2 -Name 'failure' -Phase 'run2_failure' -Journal $journal -Diagnostics $uiDiagnostics)
        }
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
    $value=if($null-eq$savedEnvironment[$name]){[NullString]::Value}else{$savedEnvironment[$name]}
    [Environment]::SetEnvironmentVariable($name,$value,'Process')
}
$sourceAfter = Get-ArchiveSnapshot -LiteralPath $sourceArchive
$sourceWorkspaceAfter = Get-ArchiveSnapshot -LiteralPath $sourceWorkspaceArchive
$sourceObjectAfter = Get-ArchiveSnapshot -LiteralPath $sourceObject
$sourceAuthoredAfter = Get-ArchiveSnapshot -LiteralPath $sourceAuthoredObject
$sourcePreserved = (Test-ArchiveSnapshotsEqual -Before $sourceBefore -After $sourceAfter) -and (Test-ArchiveSnapshotsEqual -Before $sourceWorkspaceBefore -After $sourceWorkspaceAfter) -and (Test-ArchiveSnapshotsEqual -Before $sourceObjectBefore -After $sourceObjectAfter) -and (Test-ArchiveSnapshotsEqual -Before $sourceAuthoredBefore -After $sourceAuthoredAfter)
if (-not $sourcePreserved) { $failure = "source fixture changed during UI playtest; $failure" }
$finalPlacementsSnapshot = $null
$placementsPreserved = $false
try {
    if ((Get-FileSha256 -LiteralPath $cloneAuthored) -cne $authoredArchivePin) { throw 'Playtest changed the authored checkpoint object.' }
    $finalPlacementsSnapshot = Get-Gate3ArchivePlacementsSnapshot -Archive $cloneAuthored
    Assert-Gate3PlacementsPreserved -Before $sourcePlacementsBefore -After $finalPlacementsSnapshot
    $placementsPreserved = $true
} catch { $failure = "playtest placement preservation failed: $($_.Exception.Message); $failure" }
if ($forcedProcessStop) { $failure = "owned Studio process required a forced stop; $failure" }
if (-not $run1Stopped -or -not $run2Stopped) { $failure = "owned Studio process did not close cleanly; $failure" }
if (@($uiDiagnostics | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.error) }).Count -gt 0) {
    $failure = "required public UI evidence could not be captured completely; $failure"
}

$ledger = [ordered]@{
    schema = 'forge.tidewater.gate3-gameplay-playtest-smoke'
    version = 1
    harness = $PSCommandPath
    harness_sha256 = $playtestHarnessHashAtStart
    navigation_reader_sha256 = $playtestNavigationReaderHashAtStart
    initial_focus_order = if ($WaitForReadyBeforeInitialFocus) { 'public_ready_then_pointer_capture' } else { 'historical_pointer_capture_then_public_ready' }
    status = if ([string]::IsNullOrWhiteSpace($failure) -and $run1Completed -and $run2Completed) { 'passed' } else { 'failed_closed' }
    failure = $failure
    gate_qualified = $false
    human_visual_review = $false
    authoring_ledger = $ledgerInput
    candidate = $candidate
    candidate_sha256 = $candidateHash
    source_fixture = $source
    source_original_hash_pinned = $sourcePin
    source_archive_before = $sourceBefore
    source_archive_after = $sourceAfter
    source_workspace_before = $sourceWorkspaceBefore
    source_workspace_after = $sourceWorkspaceAfter
    source_object_before = $sourceObjectBefore
    source_object_after = $sourceObjectAfter
    authored_checkpoint = $reference
    authored_object_before = $sourceAuthoredBefore
    authored_object_after = $sourceAuthoredAfter
    source_preserved = $sourcePreserved
    source_placements = [ordered]@{
        source_archive_sha256=$sourcePin
        representation='ConvertTo-Json -Depth 100 -Compress; complete ordered placement array; SHA-256 of UTF-8 without BOM'
        before=[ordered]@{count=$sourcePlacementsBefore.Count; sha256=$sourcePlacementsBefore.Sha256; canonical_json=$sourcePlacementsBefore.CanonicalJson}
        after=if ($null -ne $finalPlacementsSnapshot) { [ordered]@{count=$finalPlacementsSnapshot.Count; sha256=$finalPlacementsSnapshot.Sha256; canonical_json=$finalPlacementsSnapshot.CanonicalJson} } else { $null }
        identity_and_content_preserved=$placementsPreserved
    }
    isolated_fixture = $fixture
    isolated_archive_before = $cloneBefore
    isolated_workspace_before = $cloneWorkspaceBefore
    isolated_original_object_before = $cloneOriginalBefore
    public_ui_diagnostics = @($uiDiagnostics)
    movement_route_provenance = [ordered]@{
        probe_relative_path = $canonicalProbeRelativePath
        probe_sha256 = $canonicalProbeSha256
        probe_source_hash = $canonicalProbeSourceHash
        probe_geometry_hash = $canonicalProbeGeometryHash
        waypoints_xz = @($canonicalRouteWaypoints | ForEach-Object { [ordered]@{ x = [double]$_.x; z = [double]$_.z } })
        actual_playtest_waypoints_xz = @($playtestRouteWaypoints | ForEach-Object { [ordered]@{ x = [double]$_.x; z = [double]$_.z } })
        authored_npc_clearance = 'public WASD detour west of the village NPC capsule at (40,-118); rejoin the exact first subdivided terrain-probe waypoint; no collider removal'
        authored_trap_clearance = if ($UseFinalPhysicsApproach) { $(if ($UseGuardianContactPulses) { 'Actual forest endpoint is (14.7,-270), then west/south approach to (16,-271.3). An already enabled public attack is attempted without a target-contact success claim. Otherwise, or after the exact contact refusal, released 40ms pulses require the first fresh public KCC collision/block with attack enabled. Actual acknowledgement remains mandatory. The fixed trap and watcher stay present.' } else { 'Historical final approach uses the precise target (16,-270.60), tolerance 0.05 m; the fixed trap and watcher stay present.' }) } else { $null }
        reverse_waypoints_xz = @($forestToVillageWaypoints | ForEach-Object { [ordered]@{ x = [double]$_.x; z = [double]$_.z } })
        input = 'public camera-relative WASD key holds; reviewed polyline subdivided at <=3 m spacing; bounded actual-coordinate arrival checks'
        tolerance_m = $routeToleranceM
        max_seconds_per_route = $routeMaxSeconds
        route_claim = 'player movement probe only; no authored navigation or NPC navigation claim'
    }
    run1_completed = $run1Completed
    run2_completed = $run2Completed
    resume_initial_camera_direction_requested = [bool]$VerifyResumeInitialCameraDirection
    resume_initial_camera_direction_verified = $resumeInitialCameraDirectionVerified
    interior_visit_requested = [bool]$VisitInteriorBeforeSave
    interior_visit_completed = $interiorVisitCompleted
    committed_save_receipt_audit = $committedReceiptReport
    interior_visit_phase = if ($VisitInteriorBeforeQuest) { 'before_quest' } else { 'after_completed_quest_before_save' }
    interior_scenario = $interiorScenario
    final_physics_approach_requested = [bool]$UseFinalPhysicsApproach
    guardian_contact_pulses_requested = [bool]$UseGuardianContactPulses
    guardian_contact_refusal_retry = if ($UseGuardianContactPulses) { [ordered]@{max_click_attempts=4;bound_seconds=45;eligible_refusal='exact no physical contact confirmed only';kcc_counts_prove_target_contact=$false;public_before_and_after_escape_observations=$true;released_horizontal_settle_is_not_grounding_qualification=$true} } else { $null }
    final_physics_approach_scenario = $finalPhysicsScenario
    owned_processes_closed = [bool]($run1Stopped -and $run2Stopped)
    forced_process_stop = $forcedProcessStop
    public_source_hash_evidence = [ordered]@{
        pinned_source_archive_before_after_equal = (Test-ArchiveSnapshotsEqual -Before $sourceBefore -After $sourceAfter)
        pinned_workspace_before_after_equal = (Test-ArchiveSnapshotsEqual -Before $sourceWorkspaceBefore -After $sourceWorkspaceAfter)
        pinned_object_before_after_equal = (Test-ArchiveSnapshotsEqual -Before $sourceObjectBefore -After $sourceObjectAfter)
        authored_checkpoint_before_after_equal = (Test-ArchiveSnapshotsEqual -Before $sourceAuthoredBefore -After $sourceAuthoredAfter)
        save_load_used_current_zone_source = [bool]($run1Completed -and $run2Completed)
        direct_domain_hash_read = $false
    }
    journal = @($journal)
    zone_uuid = $zoneId
    limitations = @(
        'All movement, camera look, dialogue, attack, pickup, save and resume inputs are public UI/CDP mouse or keyboard events; no Runtime domain state or Tauri command is injected.',
        'Position, grounded state, camera yaw, simulation readiness and input status are read from the public Studio text output only; the script does not infer hidden KCC or Rapier state.',
        'Movement is bounded coordinate-targeted WASD evidence. The village/forest leg preserves canonical terrain-probe provenance and records the actual waypoints used with public-coordinate arrival checks; it does not claim authored navigation, P05, fire, trap or any whole-map route.',
        $(if ($UseFinalPhysicsApproach) { 'Final-physics mode uses an explicit public west/south detour around the authored trap and a west/north pickup approach around the watcher. Resume verifies Guardian absence near attack range, then Sceau absence separately at the pickup site. These detours acquire movement evidence only when this run passes; they are not inherited from the terrain probe.' } else { 'The canonical forest endpoint is used without the complete-physics detour.' }),
        $(if ($VisitInteriorBeforeSave) { 'The requested interior passage is evidenced only when interior_visit_completed is true: the public door opens, the grounded KCC crosses into the authored room, exits, closes the door and returns to the village before Save. It does not alter imported building collision.' } else { 'Interior passage was not requested in this bounded run.' }),
        $(if ($UseGuardianContactPulses) { 'An enabled public attack may be attempted before supplementary KCC pulses. An exact no-physical-contact refusal still requires bounded released pulses with a fresh KCC collision/block before retry. Neither KCC counts nor action availability prove success: each actual attack acknowledgement and receipt remains required.' } else { 'Guardian short contact pulses were not requested.' }),
        'Public DOM diagnostics and screenshots are captured at run1 start_ready/save, run2 resume_ready, and before owned-process cleanup on run1/run2 failure when a CDP socket exists.',
        'Source hash persistence is evidenced by the pinned source/workspace/object snapshots remaining byte-identical and by the public Save/Resume path accepting the current zone. Studio does not expose the internal save source hash in a public control, so direct domain-hash reading is deliberately absent.',
        'The authored quest UI has no reward field; the first tranche therefore checks unique pickup consumption and Completed status rather than claiming a separate reward item.',
        'After resume, defeated/picked-up action absence is asserted only after a new public WASD visit near the forest target with grounded, simulation-ready status; the village HUD alone is insufficient.',
        'gate_qualified remains false; runtime, human visual and release acceptance require separate review.'
    )
}

if ($outputCreated) {
    $ledgerPath = Join-Path $output 'gate3-gameplay-playtest-smoke-ledger.json'
    [IO.File]::WriteAllText($ledgerPath, ($ledger | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
    if ($ledger.status -cne 'passed') { throw "Gate 3 gameplay playtest smoke failed closed: $failure. Ledger: $ledgerPath" }
    Write-Output "TIDEWATER_GATE3_GAMEPLAY_PLAYTEST_SMOKE_PASS ledger=$ledgerPath"
}
