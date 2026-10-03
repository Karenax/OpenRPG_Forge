#Requires -Version 7.2
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
    [string]$PlaytestLedger,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$SourceOriginalHashPinned,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$EvidenceRoot,

    [Parameter(ParameterSetName = 'Run')]
    [guid]$ZoneUuid = 'e620dd19-ead9-5913-a6d6-0f93109148c0',

    [Parameter(ParameterSetName = 'Run')]
    [ValidateNotNullOrEmpty()]
    [string]$BrowserExecutableFolder = 'C:\Program Files (x86)\Microsoft\EdgeWebView\Application\154.0.4258.37',

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(30, 180)]
    [int]$StartupTimeoutSeconds = 120,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(5, 60)]
    [int]$DiagnosticTimeoutSeconds = 30,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectionRequestedSelfTest = [bool]$SelfTest
$projectionRequestedCandidate = $Candidate
$projectionRequestedFixture = $FreshIsolatedFixture
$projectionRequestedLedger = $PlaytestLedger
$projectionRequestedSourcePin = $SourceOriginalHashPinned
$projectionRequestedEvidence = $EvidenceRoot
$projectionRequestedZone = $ZoneUuid
$projectionRequestedBrowser = $BrowserExecutableFolder
$projectionRequestedStartup = $StartupTimeoutSeconds
$projectionRequestedDiagnostic = $DiagnosticTimeoutSeconds

. (Join-Path $PSScriptRoot 'Invoke-Gate3GameplayPlaytestSmoke.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Gate3ProofStorage.ps1')

if (-not $projectionRequestedSelfTest) {
    $Candidate = $projectionRequestedCandidate
    $FreshIsolatedFixture = $projectionRequestedFixture
    $PlaytestLedger = $projectionRequestedLedger
    $SourceOriginalHashPinned = $projectionRequestedSourcePin
    $EvidenceRoot = $projectionRequestedEvidence
    $ZoneUuid = $projectionRequestedZone
    $BrowserExecutableFolder = $projectionRequestedBrowser
    $StartupTimeoutSeconds = $projectionRequestedStartup
    $DiagnosticTimeoutSeconds = $projectionRequestedDiagnostic
}
$SelfTest = $projectionRequestedSelfTest

$requestedDiagnostics = @(
    'forge-r2-050-projection-diagnostic',
    'forge-r2-050-placement-projection-diagnostic',
    'forge-r2-050-building-projection-diagnostic',
    'data-tidewater-live-shadows'
)

function Get-DiagnosticDomSnapshot {
    param([Parameter(Mandatory)]$Socket)

    return Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const norm=value=>String(value??'').replace(/\s+/g,' ').trim();
  const wanted=[
    'forge-r2-050-projection-diagnostic',
    'forge-r2-050-placement-projection-diagnostic',
    'forge-r2-050-building-projection-diagnostic',
    'data-tidewater-live-shadows'
  ];
  const nodeDetails=(name,node)=>node?{
    name,found:true,tag:node.tagName?.toLowerCase()??null,id:node.id??null,
    text:norm(node.textContent).slice(0,8000),
    value:('value' in node)?String(node.value??'').slice(0,8000):null,
    disabled:!!node.disabled,hidden:!!node.hidden,
    visible:!!(node.getBoundingClientRect&&node.getBoundingClientRect().width>0&&node.getBoundingClientRect().height>0),
    outer_html:node.outerHTML?.slice(0,24000)??null
  }:{name,found:false};
  const findNamed=name=>{
    if(name==='data-tidewater-live-shadows') return document.querySelector('[data-tidewater-live-shadows]');
    return document.getElementById(name)??document.querySelector(`[data-testid="${name}"],[data-diagnostic="${name}"],[aria-label="${name}"]`);
  };
  const interesting={};
  let interestingCount=0;
  const keyPattern=/(epoch|hash|projection|placement|building|manual|accepted|source|object|state|ready|error|target)/i;
  const walk=(value,path,depth)=>{
    if(interestingCount>=240||depth>5||value===null||value===undefined) return;
    if(typeof value!=='object') {
      if(keyPattern.test(path.split('.').pop()??'')) { interesting[path]=value; interestingCount++; }
      return;
    }
    if(Array.isArray(value)) {
      value.slice(0,80).forEach((entry,index)=>walk(entry,`${path}[${index}]`,depth+1));
      return;
    }
    Object.keys(value).slice(0,160).forEach(key=>walk(value[key],path?`${path}.${key}`:key,depth+1));
  };
  const requested=wanted.map(name=>{
    const node=findNamed(name);
    const detail=nodeDetails(name,node);
    if(name==='data-tidewater-live-shadows'&&node) {
      const raw=node.getAttribute('data-tidewater-live-shadows')??'';
      detail.attribute=raw.slice(0,262144);
      try { const parsed=JSON.parse(raw); detail.json=parsed; walk(parsed,'data-tidewater-live-shadows',0); }
      catch(error) { detail.json_error=String(error); }
    }
    return detail;
  });
  const matchingIds=[...document.querySelectorAll('[id]')].map(node=>({id:node.id,tag:node.tagName?.toLowerCase()??null,text:norm(node.textContent).slice(0,120),visible:!!(node.getBoundingClientRect&&node.getBoundingClientRect().width>0&&node.getBoundingClientRect().height>0)})).filter(item=>/(r2.?050|projection|diagnostic|building|placement)/i.test(item.id)).slice(0,240);
  const canvas=document.getElementById('forge-authoring-canvas');
  const status=norm(document.querySelector('#authoring-playtest-status')?.textContent);
  const input=norm(document.querySelector('#studio-input-capture-status')?.textContent);
  const key=norm(document.querySelector('#studio-keyboard-event-status')?.textContent);
  return {
    captured_at_utc:new Date().toISOString(),url:String(location.href),ready_state:document.readyState,
    visibility_state:document.visibilityState,active_element:document.activeElement?.id??document.activeElement?.tagName??null,
    status,input,key,requested,matching_ids:matchingIds,
    data_tidewater_live_shadows:canvas?.getAttribute('data-tidewater-live-shadows')?.slice(0,262144)??null,
    interesting_public_keys:interesting,
    gameplay_controls_present:!!document.querySelector('section.authoring-gameplay-controls'),
    canvas_present:!!canvas
  };
})()
'@
}

function Save-DiagnosticCapture {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$EvidencePath,
        [Parameter(Mandatory)][string[]]$RequestedNames
    )
    $jsonPath = Join-Path $EvidencePath 'v5-projection-diagnostic-ui.json'
    $screenshotPath = Join-Path $EvidencePath 'v5-projection-diagnostic.png'
    $payload = [ordered]@{
        schema = 'forge.tidewater.gate3-v5-projection-diagnostic-ui'
        version = 1
        captured_at_utc = [DateTimeOffset]::UtcNow.ToString('o')
        requested_public_controls = @($RequestedNames)
        public_dom = $null
        screenshot = $null
        error = $null
    }
    try {
        $payload.public_dom = Get-DiagnosticDomSnapshot -Socket $Socket
        $payload.screenshot = Save-CdpScreenshot -Socket $Socket -LiteralPath $screenshotPath
    }
    catch {
        $payload.error = $_.Exception.Message
    }
    [IO.File]::WriteAllText($jsonPath, ($payload | ConvertTo-Json -Depth 80), [Text.UTF8Encoding]::new($false))
    return [pscustomobject]$payload
}

if ($SelfTest) {
    foreach ($name in @(
        'ConvertTo-CompactJson', 'ConvertFrom-ExactJson', 'Invoke-CdpCommand', 'Invoke-CdpExpression',
        'Wait-CdpReady', 'Wait-CdpTarget', 'Connect-CdpSocket', 'New-LoopbackPortReservation',
        'Wait-LaunchedExecutableIdentity', 'Invoke-UiClick', 'Open-DetailsBySummary',
        'Copy-IsolatedFixture', 'Get-ArchiveSnapshot', 'Test-ArchiveSnapshotsEqual',
        'Get-FileSha256', 'Get-NormalizedSha256', 'Assert-Gate3ProofPath',
        'Assert-TreeHasNoReparsePoints', 'Test-PathWithinDirectory', 'Save-CdpScreenshot',
        'Start-ForgeStudioPlaytestProcess', 'Stop-ForgeStudioPlaytestProcess', 'Open-Gate3PlaytestZone'
    )) { [void](Get-Command $name -ErrorAction Stop) }
    $raw = [IO.File]::ReadAllText($PSCommandPath)
    foreach ($needle in @(
        'forge-r2-050-projection-diagnostic', 'forge-r2-050-placement-projection-diagnostic',
        'forge-r2-050-building-projection-diagnostic', 'data-tidewater-live-shadows',
        'v5-projection-diagnostic-ui.json', 'v5-projection-diagnostic.png',
        'gate_qualified = $false', 'Runtime.enable', 'Page.bringToFront',
        'CloseMainWindow()', 'Quitter sans enregistrer'
    )) { if ($raw.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "Projection diagnostic self-test missing: $needle" } }
    foreach ($forbidden in @(
        ('window.' + '__TAURI__'), ('window.' + '__forgeGate2WorldAudit'),
        ('Game' + 'Save' + 'Request'), ('Studio' + 'Command::' + 'GameSave'),
        ('Runtime' + '.evaluate'), ('element' + '.click()')
    )) { if ($raw.IndexOf($forbidden, [StringComparison]::Ordinal) -ge 0) { throw "Projection diagnostic contains forbidden injection marker: $forbidden" } }
    Write-Output 'TIDEWATER_GATE3_V5_PROJECTION_DIAGNOSTIC_SELF_TEST_PASS'
    return
}

if (-not (Test-IsWindowsPlatform)) { throw 'Windows/WebView2 is required.' }
$candidatePath = [IO.Path]::GetFullPath($Candidate)
$fixturePath = [IO.Path]::GetFullPath($FreshIsolatedFixture)
$ledgerPath = [IO.Path]::GetFullPath($PlaytestLedger)
$outputRoot = [IO.Path]::GetFullPath($EvidenceRoot)
$zoneId = $ZoneUuid.ToString('D').ToLowerInvariant()
$sourcePin = Get-NormalizedSha256 -Value $SourceOriginalHashPinned
foreach ($entry in @(@($candidatePath, 'V5 candidate'), @($fixturePath, 'V5 playtest fixture'), @($ledgerPath, 'V5 playtest ledger'), @($outputRoot, 'Projection diagnostic evidence'))) {
    Assert-Gate3ProofPath -LiteralPath $entry[0] -Description $entry[1]
}
if (Test-Path -LiteralPath $outputRoot) { throw "EvidenceRoot must be fresh: $outputRoot" }
if (-not (Test-Path -LiteralPath $candidatePath -PathType Leaf)) { throw "Candidate is absent: $candidatePath" }
if (-not (Test-Path -LiteralPath $fixturePath -PathType Container)) { throw "Fixture is absent: $fixturePath" }
if (-not (Test-Path -LiteralPath $ledgerPath -PathType Leaf)) { throw "Playtest ledger is absent: $ledgerPath" }
if ((Test-PathWithinDirectory -LiteralPath $outputRoot -Directory $fixturePath) -or (Test-PathWithinDirectory -LiteralPath $fixturePath -Directory $outputRoot)) { throw 'EvidenceRoot and input fixture must remain separate.' }
if ((Test-PathWithinDirectory -LiteralPath $outputRoot -Directory (Split-Path -Parent $candidatePath)) -or (Test-PathWithinDirectory -LiteralPath (Split-Path -Parent $candidatePath) -Directory $outputRoot)) { throw 'EvidenceRoot and candidate directory must remain separate.' }
Assert-TreeHasNoReparsePoints -LiteralPath $fixturePath -Description 'V5 playtest fixture'
$candidateHash = Get-FileSha256 -LiteralPath $candidatePath
$ledger = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($ledgerPath))
if ([string]$ledger.schema -cne 'forge.tidewater.gate3-gameplay-playtest-smoke') { throw "Unsupported V5 playtest ledger schema: $($ledger.schema)" }
$ledgerCandidate = Get-NormalizedSha256 -Value ([string]$ledger.candidate_sha256)
if ($ledgerCandidate -cne $candidateHash) { throw 'Candidate hash differs from the V5 playtest ledger.' }
$ledgerSource = Get-NormalizedSha256 -Value ([string]$ledger.source_original_hash_pinned)
if ($ledgerSource -cne $sourcePin) { throw 'Source hash differs from the V5 playtest ledger.' }
if (-not ([IO.Path]::GetFullPath([string]$ledger.isolated_fixture)).Equals($fixturePath, [StringComparison]::OrdinalIgnoreCase)) { throw 'FreshIsolatedFixture is not the playtest ledger isolated fixture.' }
$inputZone = Join-Path (Join-Path $fixturePath $zoneId) 'authoring.json'
$inputWorkspace = Join-Path $fixturePath 'workspace-v1\authoring.json'
$inputSourceObject = Join-Path (Join-Path $fixturePath 'workspace-v1\objects') "$sourcePin.json"
$inputBefore = Get-ArchiveSnapshot -LiteralPath $inputZone
$inputWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $inputWorkspace
$inputSourceBefore = Get-ArchiveSnapshot -LiteralPath $inputSourceObject
if ($inputBefore.Sha256 -cne $sourcePin -or $inputSourceBefore.Sha256 -cne $sourcePin) { throw 'V5 playtest fixture does not preserve the pinned source bytes.' }
[void](New-Item -ItemType Directory -Path $outputRoot)
$captureFixture = Join-Path $outputRoot 'playtest-fixture'
[void](Copy-IsolatedFixture -SourceRoot $fixturePath -DestinationRoot $captureFixture -ZoneId $zoneId)
$captureZone = Join-Path (Join-Path $captureFixture $zoneId) 'authoring.json'
$captureWorkspace = Join-Path $captureFixture 'workspace-v1\authoring.json'
$captureSource = Join-Path (Join-Path $captureFixture 'workspace-v1\objects') "$sourcePin.json"
$captureBefore = Get-ArchiveSnapshot -LiteralPath $captureZone
$captureWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $captureWorkspace
$captureSourceBefore = Get-ArchiveSnapshot -LiteralPath $captureSource

$savedEnvironment = @{}
foreach ($name in @('FORGE_AUTHORING_ROOT', 'FORGE_AUTHOR_SUBJECT', 'WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', 'WEBVIEW2_USER_DATA_FOLDER', 'WEBVIEW2_BROWSER_EXECUTABLE_FOLDER')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$run = $null
$capture = $null
$stop = $null
$failure = $null
$ownedClosed = $false
$forced = $false
try {
    [Environment]::SetEnvironmentVariable('WEBVIEW2_BROWSER_EXECUTABLE_FOLDER', [IO.Path]::GetFullPath($BrowserExecutableFolder), 'Process')
    $run = Start-ForgeStudioPlaytestProcess -CandidatePath $candidatePath -CandidateHash $candidateHash -FixtureRoot $captureFixture -EvidencePath $outputRoot -RunNumber 1 -StartupTimeout $StartupTimeoutSeconds
    Open-Gate3PlaytestZone -Socket $run.Socket -ZoneId $zoneId -TimeoutSeconds $DiagnosticTimeoutSeconds
    [void](Invoke-UiClick -Socket $run.Socket -Text 'Tester & suivi' -Selector '[data-authoring-tab-button]')
    [void](Wait-CdpReady -Socket $run.Socket -TimeoutSeconds $DiagnosticTimeoutSeconds -Description 'public playtest button' -Expression @'
(() => {
  const play=document.querySelector('button.authoring-play');
  return {ready:!!play&&!play.disabled,terminal:false,reason:play?'playtest button ready':'playtest button pending'};
})()
'@)
    [void](Invoke-UiClick -Socket $run.Socket -Text 'Jouer' -Selector 'button.authoring-play')
    [void](Wait-CdpReady -Socket $run.Socket -TimeoutSeconds $DiagnosticTimeoutSeconds -Description 'suspended public gameplay diagnostics' -Expression @'
(() => {
  const root=document.querySelector('section.authoring-gameplay-controls');
  const status=document.querySelector('#authoring-playtest-status');
  const canvas=document.getElementById('forge-authoring-canvas');
  const text=(status?.textContent??'').replace(/\s+/g,' ').trim();
  const suspendedOrRunning=/simulation|tick|position \(m\)/i.test(text)&&!/mode édition/i.test(text);
  return {ready:!!root&&!!status&&!!canvas&&suspendedOrRunning,terminal:false,reason:'root='+!!root+'; status='+text.slice(0,240)+'; canvas='+!!canvas};
})()
'@)
    $capture = Save-DiagnosticCapture -Socket $run.Socket -EvidencePath $outputRoot -RequestedNames $requestedDiagnostics
}
catch {
    $failure = $_.Exception.Message
    if ($null -ne $run -and $null -ne $run.Socket) {
        try { $capture = Save-DiagnosticCapture -Socket $run.Socket -EvidencePath $outputRoot -RequestedNames $requestedDiagnostics } catch { $failure = "$failure; capture=$($_.Exception.Message)" }
    }
}
finally {
    if ($null -ne $run) {
        $stop = Stop-ForgeStudioPlaytestProcess -Run $run
        $ownedClosed = [bool]$stop.Closed
        $forced = [bool]$stop.Forced
    }
    foreach ($name in $savedEnvironment.Keys) {
        $value = if ($null -eq $savedEnvironment[$name]) { [NullString]::Value } else { $savedEnvironment[$name] }
        [Environment]::SetEnvironmentVariable($name, $value, 'Process')
    }
}

$inputAfter = Get-ArchiveSnapshot -LiteralPath $inputZone
$inputWorkspaceAfter = Get-ArchiveSnapshot -LiteralPath $inputWorkspace
$inputSourceAfter = Get-ArchiveSnapshot -LiteralPath $inputSourceObject
$captureAfter = Get-ArchiveSnapshot -LiteralPath $captureZone
$captureWorkspaceAfter = Get-ArchiveSnapshot -LiteralPath $captureWorkspace
$captureSourceAfter = Get-ArchiveSnapshot -LiteralPath $captureSource
$inputPreserved = (Test-ArchiveSnapshotsEqual -Before $inputBefore -After $inputAfter) -and (Test-ArchiveSnapshotsEqual -Before $inputWorkspaceBefore -After $inputWorkspaceAfter) -and (Test-ArchiveSnapshotsEqual -Before $inputSourceBefore -After $inputSourceAfter)
$capturePreserved = (Test-ArchiveSnapshotsEqual -Before $captureBefore -After $captureAfter) -and (Test-ArchiveSnapshotsEqual -Before $captureWorkspaceBefore -After $captureWorkspaceAfter) -and (Test-ArchiveSnapshotsEqual -Before $captureSourceBefore -After $captureSourceAfter)
$missing = @()
if ($null -ne $capture -and $null -ne $capture.public_dom) {
    foreach ($item in @($capture.public_dom.requested)) { if (-not [bool]$item.found) { $missing += [string]$item.name } }
}
$status = if ($null -ne $capture -and [string]::IsNullOrWhiteSpace([string]$capture.error) -and $ownedClosed -and -not $forced -and $inputPreserved -and $capturePreserved) { 'captured_local_evidence' } else { 'failed_closed' }
$result = [ordered]@{
    schema = 'forge.tidewater.gate3-v5-projection-diagnostic-capture'
    version = 1
    status = $status
    failure = $failure
    gate_qualified = $false
    candidate = $candidatePath
    candidate_sha256 = $candidateHash
    playtest_ledger = $ledgerPath
    source_original_hash_pinned = $sourcePin
    input_fixture = $fixturePath
    isolated_capture_fixture = $captureFixture
    requested_public_controls = @($requestedDiagnostics)
    missing_requested_controls = @($missing)
    capture_json = Join-Path $outputRoot 'v5-projection-diagnostic-ui.json'
    capture_screenshot = Join-Path $outputRoot 'v5-projection-diagnostic.png'
    public_dom = if ($null -ne $capture) { $capture.public_dom } else { $null }
    screenshot = if ($null -ne $capture) { $capture.screenshot } else { $null }
    source_fixture_preserved = $inputPreserved
    isolated_capture_fixture_preserved = $capturePreserved
    owned_process_closed = $ownedClosed
    forced_process_stop = $forced
    cleanup = if ($null -ne $stop) { [ordered]@{ graceful_close_failure = $stop.GracefulCloseFailure; closed = $stop.Closed; forced = $stop.Forced } } else { $null }
    limitations = @(
        'This capture reads public DOM text/attributes and a public screenshot only; it does not inject domain state, Tauri commands or gameplay actions.',
        'The capture intentionally stops at the suspended public playtest state and never waits for simulation_ready or runs a movement/gameplay loop.',
        'Missing requested diagnostic nodes are reported explicitly and do not become a Gate verdict.',
        'gate_qualified remains false; this is a diagnostic handoff for the Tick 0 projection guard.'
    )
}
$resultPath = Join-Path $outputRoot 'gate3-v5-projection-diagnostic-ledger.json'
[IO.File]::WriteAllText($resultPath, ($result | ConvertTo-Json -Depth 80), [Text.UTF8Encoding]::new($false))
if ($status -ne 'captured_local_evidence') { throw "V5 projection diagnostic capture failed closed: $failure. Ledger: $resultPath" }
Write-Output "TIDEWATER_GATE3_V5_PROJECTION_DIAGNOSTIC_CAPTURE_PASS ledger=$resultPath"
