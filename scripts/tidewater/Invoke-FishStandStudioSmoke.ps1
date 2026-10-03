#Requires -Version 7.2
# Isolated local Studio smoke. This is Tidewater evidence, not a Gate 2 verdict.
[CmdletBinding(DefaultParameterSetName = 'Run')]
param(
    [Parameter(ParameterSetName = 'Run', Mandatory)][string]$CandidateExecutable,
    [Parameter(ParameterSetName = 'Run', Mandatory)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$ExpectedSha256,
    [Parameter(ParameterSetName = 'Run', Mandatory)][string]$OutputDirectory,
    [Parameter(ParameterSetName = 'Run')][string]$FixtureSourceRoot = 'E:\OpenRPG_Forge\.tidewater-integration\studio-tidewater-review-20260927-001',
    [Parameter(ParameterSetName = 'Run')][guid]$ZoneUuid = 'e620dd19-ead9-5913-a6d6-0f93109148c0',
    [Parameter(ParameterSetName = 'Run')][ValidateRange(30, 180)][int]$InteractionTimeoutSeconds = 120,
    [Parameter(ParameterSetName = 'Run')][switch]$RequireOceanFft,
    [Parameter(ParameterSetName = 'Run')][switch]$RequireOceanGeometry,
    [Parameter(ParameterSetName = 'Run')][switch]$RequireOceanOptics,
    [Parameter(ParameterSetName = 'Run')][switch]$RequireShoreSim,
    [Parameter(ParameterSetName = 'Run')][switch]$RequireGroundBounce,
    [Parameter(ParameterSetName = 'Run')][switch]$RequireAmbientOcclusion,
    [Parameter(ParameterSetName = 'Run')][switch]$RequireLensFlare,
    [Parameter(ParameterSetName = 'Run')][switch]$RequireAirHaze,
    [Parameter(ParameterSetName = 'Run')][switch]$RequireMotionBlurWithOcean,
    [Parameter(ParameterSetName = 'Run')][switch]$RequireGraphicsPersistence,
    [Parameter(ParameterSetName = 'Run')][switch]$CaptureSunFlarePair,
    [Parameter(ParameterSetName = 'Run')][switch]$ReferenceVillageOnly,
    [Parameter(ParameterSetName = 'Run')][switch]$CaptureExposureSweep,
    [Parameter(ParameterSetName = 'Run')][switch]$RequireLiveShadows,
    [Parameter(ParameterSetName = 'Run')][switch]$UseDefaultWater,
    [Parameter(ParameterSetName = 'Run')][switch]$ValidateOnly,
    [Parameter(ParameterSetName = 'SelfTest', Mandatory)][switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# -SelfTest defines the established CDP/fixture helpers without running its Gate 2 proof.
$callerInputs = @{
    CandidateExecutable = $CandidateExecutable
    ExpectedSha256 = $ExpectedSha256
    OutputDirectory = $OutputDirectory
    FixtureSourceRoot = $FixtureSourceRoot
    ZoneUuid = $ZoneUuid
    InteractionTimeoutSeconds = $InteractionTimeoutSeconds
    RequireOceanFft = [bool]$RequireOceanFft
    RequireOceanGeometry = [bool]$RequireOceanGeometry
    RequireOceanOptics = [bool]$RequireOceanOptics
    RequireShoreSim = [bool]$RequireShoreSim
    RequireGroundBounce = [bool]$RequireGroundBounce
    RequireAmbientOcclusion = [bool]$RequireAmbientOcclusion
    RequireLensFlare = [bool]$RequireLensFlare
    RequireAirHaze = [bool]$RequireAirHaze
    RequireMotionBlurWithOcean = [bool]$RequireMotionBlurWithOcean
    RequireGraphicsPersistence = [bool]$RequireGraphicsPersistence
    CaptureSunFlarePair = [bool]$CaptureSunFlarePair
    ReferenceVillageOnly = [bool]$ReferenceVillageOnly
    CaptureExposureSweep = [bool]$CaptureExposureSweep
    RequireLiveShadows = [bool]$RequireLiveShadows
    UseDefaultWater = [bool]$UseDefaultWater
    SelfTest = [bool]$SelfTest
}
. (Join-Path $PSScriptRoot '..\Invoke-Gate2WorldEditingProof.ps1') -SelfTest | Out-Null
foreach ($name in $callerInputs.Keys) { Set-Variable -Name $name -Value $callerInputs[$name] }

$sourcePin = '50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720'
$bundlePin = 'c739c737914261991fd9aba21c4372e7e9cafa31527f7a4139001a4db8e9b335'
$shorePin = '2dda3c022af1f62a3148e811155ecafc83b825d4e7e0c4069124ff69e7fca754'
$bouncePin = '4002ee7219943235fc725a263fbb798d9057edd25d9c8d6fbed53eadd8f86e8d'
# AssetId::deterministic("tidewater/fish-stand/v1"), Forge asset UUID-v5 namespace.
$standAssetId = '941b533a-8d35-54ce-9fff-96d07b6b4439'

function Get-StandArchivePlacements {
    param([Parameter(Mandatory)][string]$Archive)
    $document = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($Archive))
    return @($document.head.corrections.placements)
}

function Measure-LiveShadowEvidence {
    param([Parameter(Mandatory)]$Socket)
    $measurement = Invoke-CdpExpression -Socket $Socket -Expression @'
(async () => {
  const read=()=>JSON.parse(document.getElementById('forge-authoring-canvas')?.getAttribute('data-tidewater-live-shadows')||'null');
  const before=read(), samples=[]; const start=performance.now(); let last=start;
  await new Promise(resolve=>{
    const frame=now=>{ samples.push(now-last); last=now;
      if(now-start>=5000) resolve(); else requestAnimationFrame(frame); };
    requestAnimationFrame(frame);
  });
  const sorted=samples.slice().sort((a,b)=>a-b);
  const elapsed=performance.now()-start;
  const tail=sorted.slice(-Math.max(1,Math.ceil(sorted.length*0.01)));
  const after=read();
  return {before,after,elapsed_ms:elapsed,samples:samples.length,
    shadow_encoded_fps:before&&after?(after.encoded_frames-before.encoded_frames)*1000/elapsed:null,
    raf_fps:samples.length*1000/elapsed,p95_raf_ms:sorted[Math.floor((sorted.length-1)*0.95)],
    one_percent_low_raf_fps:1000/(tail.reduce((sum,value)=>sum+value,0)/tail.length),
    scope:'Browser requestAnimationFrame cadence; render-pass counters are encoded work, not GPU completion fences.'};
})()
'@
    if ($null -eq $measurement.before -or $null -eq $measurement.after -or
        $measurement.after.pending -or
        $measurement.after.generations -ne $measurement.before.generations -or
        $measurement.after.encoded_frames -le $measurement.before.encoded_frames -or
        $measurement.after.wind_time_s -le $measurement.before.wind_time_s) {
        throw 'Live shadows did not advance in the same resident generation.'
    }
    foreach ($layer in 0..2) {
        if ($measurement.after.cascade_layers[$layer] -le $measurement.before.cascade_layers[$layer]) {
            throw "Live shadow cascade $layer stopped advancing."
        }
    }
    return $measurement
}

function Test-StandPlacementEquivalent {
    param([Parameter(Mandatory)]$Left, [Parameter(Mandatory)]$Right)
    foreach ($name in @('id', 'name', 'asset_id', 'asset_hash')) {
        if ([string]$Left.$name -cne [string]$Right.$name) { return $false }
    }
    if ([bool]$Left.locked -ne [bool]$Right.locked) { return $false }
    foreach ($part in @('translation_m', 'rotation', 'scale')) {
        foreach ($axis in @('x', 'y', 'z', 'w')) {
            if ($part -ne 'rotation' -and $axis -eq 'w') { continue }
            $leftBits = [BitConverter]::SingleToInt32Bits([single]$Left.transform.$part.$axis)
            $rightBits = [BitConverter]::SingleToInt32Bits([single]$Right.transform.$part.$axis)
            if ($leftBits -ne $rightBits) { return $false }
        }
    }
    return $true
}

function Get-StandWorkspaceReference {
    param([Parameter(Mandatory)][string]$Archive, [Parameter(Mandatory)][string]$ZoneId)
    $document = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($Archive))
    $matches = @(
        foreach ($project in $document.head.projects) {
            foreach ($module in $project.modules) {
                foreach ($zone in $module.zones) {
                    if ([string]$zone.area_id -ceq $ZoneId) { $zone }
                }
            }
        }
    )
    if ($matches.Count -ne 1) { throw 'Exactly one managed Tidewater zone must exist in the workspace.' }
    return $matches[0]
}

function Open-ManagedTidewaterZone {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$ZoneId,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [switch]$OpenWorkspace
    )
    [void](Invoke-UiClick -Socket $Socket -Text 'Projet' -Selector '[data-authoring-tab-button]')
    [void](Open-DetailsBySummary -Socket $Socket -Summary 'Projets et modules')
    if ($OpenWorkspace) {
        [void](Invoke-UiClickInDetailsBySummary -Socket $Socket -Summary 'Projets et modules' -Text 'Ouvrir le workspace')
    }
    $zoneLiteral = ConvertTo-CompactJson -Value $ZoneId
    [void](Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'Managed Tidewater zone button' -Expression @"
(() => {
  const zone=$zoneLiteral;
  const button=[...document.querySelectorAll('button[title]')].find(node=>node.title===zone);
  const status=[...document.querySelectorAll('details.scene-navigation p[role=status]')].map(node=>node.textContent??'').join(' | ');
  return {ready:!!button && !button.disabled,reason:button ? 'managed zone button disabled' : status||'managed zone button pending'};
})()
"@)
    [void](Invoke-UiClick -Socket $Socket -Selector "button[title='$ZoneId']" -Text 'Ouvrir Tidewater island · zone 1')
    [void](Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'Managed Tidewater edit viewport' -Expression @"
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim(); const zone=$zoneLiteral;
  const canvas=document.getElementById('forge-authoring-canvas');
  const term=[...document.querySelectorAll('.authoring-zone-information dt')].find(node=>norm(node.textContent)==='Zone');
  const shown=norm(term?.nextElementSibling?.textContent).toLowerCase();
  const mode=[...document.querySelectorAll('output')].map(node=>norm(node.textContent)).find(text=>text.startsWith('Mode '))??'';
  const status=norm(document.querySelector('.authoring-command-status')?.textContent);
  return {ready:shown===zone && mode.startsWith('Mode édition') && canvas?.clientWidth>0 && canvas?.clientHeight>0,
    terminal:shown!=='' && shown!==zone, reason:'zone='+shown+'; mode='+mode+'; status='+status};
})()
"@)
}

function Assert-StandPlacement {
    param([Parameter(Mandatory)][object]$Placement)
    if ([string]$Placement.name -cne 'Échoppe Tidewater' -or
        [string]$Placement.asset_id -cne $standAssetId -or
        [string]$Placement.asset_hash -cne $bundlePin -or
        [string]$Placement.id -cnotmatch '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' -or
        $null -eq $Placement.transform) {
        throw 'FishStand placement identity, bundle pin, name or transform differs from the reviewed proposal.'
    }
}

function Wait-StandLibrary {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)][int]$TimeoutSeconds)
    [void](Invoke-UiClick -Socket $Socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Invoke-UiClick -Socket $Socket -Text 'Fiche')
    [void](Open-DetailsBySummary -Socket $Socket -Summary 'Bibliothèque de bâtiments importés')
    [void](Invoke-UiClick -Socket $Socket -Text 'Vérifier et charger la bibliothèque')
    return Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'Pinned FishStand library in Studio' -Expression @'
(() => {
  const n=v=>(v??'').replace(/\s+/g,' ').trim();
  const summary=[...document.querySelectorAll('summary')].find(x=>n(x.textContent)==='Bibliothèque de bâtiments importés');
  const details=summary?.closest('details');
  const outputs=[...(details?.querySelectorAll('output')??[])].map(x=>n(x.textContent));
  const button=[...document.querySelectorAll('button')].find(x=>n(x.textContent)==='Placer l’échoppe Tidewater vérifiée');
  const error=outputs.find(x=>x.startsWith('Bibliothèque indisponible :'))??'';
  return {ready:outputs.some(x=>x.includes('bâtiments vérifiés et chargés en mémoire')) && !!button,
    terminal:!!error, reason:error||outputs.join(' | '), outputs, stand_button_present:!!button};
})()
'@
}

function Get-StandClickCandidates {
    param([Parameter(Mandatory)]$Socket)
    $canvas = Get-CanvasGeometry -Socket $Socket
    foreach ($point in @(
        @(0.54,0.62), @(0.45,0.58), @(0.65,0.62),
        @(0.50,0.73), @(0.36,0.68), @(0.72,0.70)
    )) {
        $x = [double]$canvas.left + [double]$canvas.width * [double]$point[0]
        $y = [double]$canvas.top + [double]$canvas.height * [double]$point[1]
        $literalX = ConvertTo-CompactJson -Value $x
        $literalY = ConvertTo-CompactJson -Value $y
        $hit = Invoke-CdpExpression -Socket $Socket -Expression "(() => { const n=document.elementFromPoint($literalX,$literalY); return {id:n?.id??'',tag:n?.tagName??''}; })()"
        if ([string]$hit.id -ceq 'forge-authoring-canvas') {
            [pscustomobject]@{ x=$x; y=$y; normalized=$point }
        }
    }
}

if ($SelfTest) {
    foreach ($name in @('Copy-IsolatedFixture','Open-FullAuthorRouteZone','Install-WorldAudit',
                         'Invoke-UiClick','Send-CdpMouseClick','Get-WorldState','Wait-CdpTarget')) {
        [void](Get-Command $name -ErrorAction Stop)
    }
    if ([string]::IsNullOrWhiteSpace($sourcePin) -or $standAssetId -cnotmatch '^[0-9a-f-]{36}$') {
        throw 'Tidewater smoke pins are malformed.'
    }
    Write-Output 'TIDEWATER_FISH_STAND_STUDIO_SMOKE_SELF_TEST_PASS'
    return
}

if (-not (Test-IsWindowsPlatform)) { throw 'Windows/WebView2 is required.' }
$candidate = [IO.Path]::GetFullPath($CandidateExecutable)
$source = [IO.Path]::GetFullPath($FixtureSourceRoot)
$output = [IO.Path]::GetFullPath($OutputDirectory)
foreach ($entry in @(@($candidate,'Candidate'),@($source,'Fixture source'),@($output,'Output'))) {
    Assert-DriveEPath -LiteralPath $entry[0] -Description $entry[1]
    Assert-NoExistingReparseAncestor -LiteralPath $entry[0] -Description $entry[1]
}
if (Test-Path -LiteralPath $output) { throw "OutputDirectory must be fresh: $output" }
if (Test-PathWithinDirectory -LiteralPath $output -Directory $source) { throw 'Output must not be inside the fixture source.' }
if (Test-PathWithinDirectory -LiteralPath $source -Directory $output) { throw 'Fixture source must not be inside output.' }
if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Candidate absent: $candidate" }
if (Test-PathWithinDirectory -LiteralPath $output -Directory (Split-Path -Parent $candidate)) {
    throw 'Output must not be inside the candidate directory.'
}
if ((Get-FileSha256 -LiteralPath $candidate) -cne (Get-NormalizedSha256 -Value $ExpectedSha256)) {
    throw 'Candidate executable SHA-256 mismatch.'
}
Assert-TreeHasNoReparsePoints -LiteralPath $source -Description 'Tidewater source fixture'
$zoneId = $ZoneUuid.ToString('D').ToLowerInvariant()
$sourceArchive = Join-Path (Join-Path $source $zoneId) 'authoring.json'
$sourceBefore = Get-ArchiveSnapshot -LiteralPath $sourceArchive
if ($sourceBefore.Sha256 -cne $sourcePin) { throw 'Unexpected Tidewater source fixture hash.' }
if (@(Get-StandArchivePlacements -Archive $sourceArchive).Count -ne 23) { throw 'Tidewater fixture must contain exactly 23 accepted placements.' }
$sourceWorkspaceArchive = Join-Path $source 'workspace-v1\authoring.json'
$sourceWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $sourceWorkspaceArchive
$sourceReference = Get-StandWorkspaceReference -Archive $sourceWorkspaceArchive -ZoneId $zoneId
if ([string]$sourceReference.archive_hash -cne $sourcePin) { throw 'Managed Tidewater source does not reference the pinned zone archive.' }
$sourceObject = Join-Path (Join-Path $source 'workspace-v1\objects') "$sourcePin.json"
$sourceObjectBefore = Get-ArchiveSnapshot -LiteralPath $sourceObject
if ($sourceObjectBefore.Sha256 -cne $sourcePin -or @(Get-StandArchivePlacements -Archive $sourceObject).Count -ne 23) {
    throw 'Managed Tidewater source object is not the pinned 23-placement zone.'
}

$web = Join-Path (Split-Path -Parent $candidate) 'web'
$imports = Join-Path $web 'forge-imports'
Assert-TreeHasNoReparsePoints -LiteralPath $web -Description 'Candidate frontend'
$library = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText((Join-Path $imports 'buildings.json')))
if ($RequireShoreSim) {
    if (-not $RequireOceanOptics) { throw 'ShoreSim requires the optical ocean preview.' }
    if ($library.PSObject.Properties.Name -notcontains 'shore_sim_sha256' -or
        [string]$library.shore_sim_sha256 -cne $shorePin -or
        (Get-FileSha256 -LiteralPath (Join-Path (Join-Path $imports $shorePin) 'package.json')) -cne $shorePin) {
        throw 'Candidate frontend does not pin the verified ShoreSim inputs.'
    }
}
if ($RequireGroundBounce) {
    if ($library.PSObject.Properties.Name -notcontains 'ground_bounce_sha256' -or
        [string]$library.ground_bounce_sha256 -cne $bouncePin -or
        (Get-FileSha256 -LiteralPath (Join-Path (Join-Path $imports $bouncePin) 'package.json')) -cne $bouncePin) {
        throw 'Candidate frontend does not pin the verified ground-bounce inputs.'
    }
}
if ($UseDefaultWater -and (-not $RequireOceanOptics -or -not $RequireShoreSim)) {
    throw 'Default water proof requires the optical ocean and ShoreSim assertions.'
}
if ($RequireMotionBlurWithOcean -and -not $RequireOceanOptics) {
    throw 'Motion blur with ocean proof requires the optical ocean assertion.'
}
if ($library.PSObject.Properties.Name -notcontains 'fish_stand_bundle_sha256' -or
    [string]$library.fish_stand_bundle_sha256 -cne $bundlePin) {
    throw 'Candidate frontend does not pin FishStand bundle 011.'
}
$bundleRoot = Join-Path $imports $bundlePin
$manifest = Join-Path $bundleRoot 'preview-scene.json'
if ((Get-FileSha256 -LiteralPath $manifest) -cne $bundlePin) { throw 'Staged preview manifest pin mismatch.' }
$manifestData = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($manifest))
$fish = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText((Join-Path $bundleRoot 'fish-stand-fish\fish.json')))
if (@($fish.fish).Count -ne 5 -or -not (Test-Path -LiteralPath (Join-Path $bundleRoot 'scene\joe.glb') -PathType Leaf)) {
    throw 'Candidate frontend does not contain five fish and Joe.'
}
if (@(Get-ChildItem -LiteralPath $bundleRoot -File -Recurse).Count -ne 54) {
    throw 'Candidate frontend does not contain the 53 FishStand payloads plus manifest.'
}
$payloadFiles = @(Get-ChildItem -LiteralPath $bundleRoot -File -Recurse | Where-Object { $_.FullName -cne $manifest })
foreach ($payload in $payloadFiles) {
    $relative = [IO.Path]::GetRelativePath($bundleRoot, $payload.FullName).Replace('\', '/')
    $record = $manifestData.files.PSObject.Properties[$relative].Value
    if ($null -eq $record -or [int64]$record.bytes -ne $payload.Length -or
        [string]$record.sha256 -cne (Get-FileSha256 -LiteralPath $payload.FullName)) {
        throw "FishStand staged payload does not match the pinned manifest: $relative"
    }
}

if ($ValidateOnly) {
    [ordered]@{ status='INPUTS_VALID'; candidate=$candidate; candidate_sha256=$ExpectedSha256;
        source_archive_sha256=$sourceBefore.Sha256; zone_uuid=$zoneId; fish_stand_bundle_sha256=$bundlePin;
        staged_files=54; source_placements=23; app_launched=$false } | ConvertTo-Json -Depth 5
    return
}

[void](New-Item -ItemType Directory -Path $output)
$fixture = Join-Path $output 'authoring-root'
$archive = Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $zoneId
$cloneBefore = Get-ArchiveSnapshot -LiteralPath $archive
if ($cloneBefore.Sha256 -cne $sourceBefore.Sha256) { throw 'Isolated authoring copy hash mismatch.' }
$workspaceArchive = Join-Path $fixture 'workspace-v1\authoring.json'
$workspaceBefore = Get-ArchiveSnapshot -LiteralPath $workspaceArchive
if ($workspaceBefore.Sha256 -cne $sourceWorkspaceBefore.Sha256) { throw 'Isolated workspace copy hash mismatch.' }
$objectRoot = Join-Path $fixture 'workspace-v1\objects'
$originalObject = Join-Path $objectRoot "$sourcePin.json"
$originalObjectBefore = Get-ArchiveSnapshot -LiteralPath $originalObject
if ($originalObjectBefore.Sha256 -cne $sourceObjectBefore.Sha256) { throw 'Isolated managed zone copy hash mismatch.' }
$profile = Join-Path $output 'webview2-user-data'
[void](New-Item -ItemType Directory -Path $profile)
$savedEnvironment = @{}
foreach ($name in @('FORGE_AUTHORING_ROOT','FORGE_AUTHOR_SUBJECT','WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS','WEBVIEW2_USER_DATA_FOLDER')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$process = $null
$socket = $null
$reservation = $null
$port = 0
$failure = $null
$previewEntry = $null
$applyEntry = $null
$placed = $null
$saved = $null
$savedObject = $null
$savedObjectPath = $null
$reopened = $null
$playtest = $null
$oceanStepsBefore = $null
$oceanStepsAfter = $null
$shoreStepsBefore = $null
$shoreStepsAfter = $null
$movement = $null
$liveShadowEvidence = @()
$failureState = $null
$browserErrors = @()
$screenshots = [System.Collections.Generic.List[object]]::new()
$attempts = [System.Collections.Generic.List[object]]::new()
try {
    $reservation = New-LoopbackPortReservation
    $port = $reservation.Port
    [Environment]::SetEnvironmentVariable('FORGE_AUTHORING_ROOT', $fixture, 'Process')
    [Environment]::SetEnvironmentVariable('FORGE_AUTHOR_SUBJECT', 'tidewater-fish-stand-cdp-smoke', 'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', "--remote-debugging-address=127.0.0.1 --remote-debugging-port=$port", 'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER', $profile, 'Process')
    $reservation.Listener.Stop()
    $process = Start-Process -FilePath $candidate -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $output 'candidate.stdout.log') `
        -RedirectStandardError (Join-Path $output 'candidate.stderr.log')
    [void](Wait-LaunchedExecutableIdentity -Process $process -Candidate $candidate -ExpectedHash (Get-NormalizedSha256 -Value $ExpectedSha256))
    $target = Wait-CdpTarget -Port $port -TimeoutSeconds 60 -Process $process
    $socket = Connect-CdpSocket -WebSocketUrl ([string]$target.webSocketDebuggerUrl)
    [void](Invoke-CdpCommand -Socket $socket -Method 'Runtime.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.bringToFront')
    [void](Invoke-CdpExpression -Socket $socket -Expression @'
(() => { window.__forgeSmokeErrors=[];
  const record=(kind,parts)=>{window.__forgeSmokeErrors.push(kind+': '+parts.map(String).join(' '));
    if(window.__forgeSmokeErrors.length>200) window.__forgeSmokeErrors.shift();};
  for(const name of ['error','warn','log']) { const previous=console[name].bind(console);
    console[name]=(...args)=>{record(name,args);previous(...args);}; }
  addEventListener('error',event=>record('error',[event.message,event.filename,event.lineno]));
  addEventListener('unhandledrejection',event=>record('rejection',[event.reason]));
  return true; })()
'@)
    [void](Wait-CdpReady -Socket $socket -Description 'Authoring shell before Tidewater library' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => ({ready:document.readyState==='complete' && !!document.querySelector('.authoring-shell'),reason:'authoring shell pending'}))()
'@)
    if ($RequireGraphicsPersistence) {
        $changedPreference = Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const slider=document.getElementById('authoring-graphics-exposure');
  if (!slider) return false;
  slider.value='-0.75'; slider.dispatchEvent(new Event('input',{bubbles:true})); return true; })()
'@
        if (-not $changedPreference) { throw 'Tidewater graphics preference control missing.' }
        [void](Wait-CdpReady -Socket $socket -Description 'Tidewater graphics preference stored' -TimeoutSeconds 15 -Expression @'
(() => { const raw=localStorage.getItem('forge.studio.tidewater.graphics.v1');
  const saved=raw?JSON.parse(raw):null;
  return {ready:saved?.exposure_ev===-0.75,reason:raw??'graphics preference missing'}; })()
'@)
        [void](Invoke-CdpCommand -Socket $socket -Method 'Page.reload')
        [void](Wait-CdpReady -Socket $socket -Description 'Tidewater graphics preference restored after reload' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => { const slider=document.getElementById('authoring-graphics-exposure');
  return {ready:document.readyState==='complete' && slider?.value==='-0.75',reason:slider?.value??'graphics control missing'}; })()
'@)
        $resetPreference = Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  const reset=[...panel.querySelectorAll('button')].find(node=>node.textContent.includes('Réinitialiser les effets'));
  reset?.click(); return !!reset; })()
'@
        if (-not $resetPreference) { throw 'Tidewater graphics preference reset missing.' }
        [void](Wait-CdpReady -Socket $socket -Description 'Tidewater graphics defaults stored after reset' -TimeoutSeconds 15 -Expression @'
(() => { const raw=localStorage.getItem('forge.studio.tidewater.graphics.v1');
  const saved=raw?JSON.parse(raw):null;
  return {ready:saved?.exposure_ev===0.1 && document.getElementById('authoring-graphics-exposure')?.value==='0.1',reason:raw??'graphics preference missing'}; })()
'@)
    }
    # The imported terrain/environment must be resident before opening Tidewater.
    [void](Wait-StandLibrary -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Open-ManagedTidewaterZone -Socket $socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds -OpenWorkspace)
    [void](Install-WorldAudit -Socket $socket)
    $initial = Get-WorldState -Socket $socket
    if (@($initial.placement_options).Count -ne 23) { throw 'Studio did not open all 23 Tidewater placements.' }
    [void](Invoke-UiClick -Socket $socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Invoke-UiClick -Socket $socket -Text 'Placer / manipuler')
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Ajouter un objet depuis le catalogue')
    $preview = $null
    foreach ($point in @(Get-StandClickCandidates -Socket $socket)) {
        $before = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text "Placer l’échoppe Tidewater vérifiée")
        Send-CdpMouseClick -Socket $socket -X $point.x -Y $point.y
        try {
            $preview = Wait-NewPreview -Socket $socket -PreviousCount ([int]$before.audit_counts.preview_zone) -DiffNeedle '1 ajout(s)' -TimeoutSeconds ([Math]::Min($InteractionTimeoutSeconds, 45))
            $attempts.Add([pscustomobject]@{point=$point;preview=$true})
            break
        }
        catch {
            $attempts.Add([pscustomobject]@{point=$point;preview=$false;reason=$_.Exception.Message})
        }
    }
    if ($null -eq $preview) { throw 'Physical Tidewater terrain click produced no accepted FishStand preview.' }
    $previewEntry = Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'preview_zone'
    $proposal = @($previewEntry.wire.request.request.action.corrections.placements)
    $new = @($proposal | Where-Object { $initial.placement_options.value -notcontains [string]$_.id })
    if ($proposal.Count -ne 24 -or $new.Count -ne 1) { throw 'FishStand preview did not add exactly one placement.' }
    $placed = $new[0]
    Assert-StandPlacement -Placement $placed
    if (-not (Test-ArchiveSnapshotsEqual -Before $cloneBefore -After (Get-ArchiveSnapshot -LiteralPath $archive)) -or
        -not (Test-ArchiveSnapshotsEqual -Before $workspaceBefore -After (Get-ArchiveSnapshot -LiteralPath $workspaceArchive)) -or
        -not (Test-ArchiveSnapshotsEqual -Before $originalObjectBefore -After (Get-ArchiveSnapshot -LiteralPath $originalObject))) {
        throw 'Preview changed the isolated legacy source, workspace checkpoint or original managed object.'
    }
    $beforeApply = Get-WorldState -Socket $socket
    [void](Invoke-UiClick -Socket $socket -Text 'Appliquer' -Selector '.authoring-apply')
    [void](Wait-WorldState -Socket $socket -Description 'FishStand Apply accepted' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
        param($state)
        [int]$state.audit_counts.apply_zone -eq ([int]$beforeApply.audit_counts.apply_zone + 1) -and
            [int]$state.audit_pending -eq 0 -and $state.diff_hidden -and
            @($state.placement_options).Count -eq 24
    })
    $applyEntry = Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'apply_zone'
    if (-not (Test-AuditReceiptsEqual -Left (Get-RequiredAuditReceipt -Entry $previewEntry) -Right (Get-RequiredAuditReceipt -Entry $applyEntry))) {
        throw 'FishStand Preview and Apply receipts differ.'
    }
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Sélection et chargement')
    [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value ([string]$placed.id))
    [void](Wait-CdpReady -Socket $socket -Description 'FishStand asset source inspector' -TimeoutSeconds 15 -Expression ("(() => { const s=document.body.textContent??'';return {ready:s.includes('Source acceptée : asset $standAssetId')&&s.includes('SHA-256 $bundlePin'),reason:'source inspector pending'};})()"))
    [void](Invoke-UiClick -Socket $socket -Text 'Placer / manipuler')
    [void](Invoke-UiClick -Socket $socket -Text 'Cadrer cet objet dans la carte')
    Start-Sleep -Seconds 3
    $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'fish-stand-applied.png')))
    $beforeSave = Get-WorldState -Socket $socket
    [void](Invoke-UiClick -Socket $socket -Text 'Sauvegarder')
    [void](Wait-WorldState -Socket $socket -Description 'FishStand Save' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
        param($state)
        [int]$state.audit_counts.save -eq ([int]$beforeSave.audit_counts.save + 1) -and
            [int]$state.audit_pending -eq 0 -and [string]$state.command_status -like '*Session synchronisée*'
    })
    $saved = Get-ArchiveSnapshot -LiteralPath $workspaceArchive
    if ($saved.Sha256 -ceq $workspaceBefore.Sha256) { throw 'FishStand Save did not update the managed workspace checkpoint.' }
    $savedReference = Get-StandWorkspaceReference -Archive $workspaceArchive -ZoneId $zoneId
    if ([string]$savedReference.archive_hash -ceq $sourcePin) { throw 'Managed Tidewater reference did not advance to a new immutable object.' }
    $savedObjectPath = Join-Path $objectRoot "$($savedReference.archive_hash).json"
    $savedObject = Get-ArchiveSnapshot -LiteralPath $savedObjectPath
    if ($savedObject.Sha256 -cne [string]$savedReference.archive_hash -or
        -not (Test-ArchiveSnapshotsEqual -Before $cloneBefore -After (Get-ArchiveSnapshot -LiteralPath $archive)) -or
        -not (Test-ArchiveSnapshotsEqual -Before $originalObjectBefore -After (Get-ArchiveSnapshot -LiteralPath $originalObject))) {
        throw 'Managed Save altered the legacy source or original object, or published an unpinned object.'
    }
    $savedPlacements = @(Get-StandArchivePlacements -Archive $savedObjectPath)
    $savedStand = @($savedPlacements | Where-Object { [string]$_.id -ceq [string]$placed.id })
    if ($savedPlacements.Count -ne 24 -or $savedStand.Count -ne 1 -or
        -not (Test-StandPlacementEquivalent -Left $savedStand[0] -Right $placed)) {
        throw 'Saved FishStand identity, pin, name or exact transform differs from Preview.'
    }
    [void](Invoke-UiClick -Socket $socket -Text 'Projet' -Selector '[data-authoring-tab-button]')
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Créer ou modifier la zone')
    $beforeClose = Get-WorldState -Socket $socket
    [void](Invoke-UiClick -Socket $socket -Text 'Fermer la zone')
    [void](Install-WorldAuditAfterOwnedReload -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Invoke-CdpExpression -Socket $socket -Expression @'
(() => { window.__forgeSmokeErrors=[];
  const record=(kind,parts)=>{window.__forgeSmokeErrors.push(kind+': '+parts.map(String).join(' '));
    if(window.__forgeSmokeErrors.length>500) window.__forgeSmokeErrors.shift();};
  for(const name of ['error','warn','log','info','debug']) { const previous=console[name].bind(console);
    console[name]=(...args)=>{record(name,args);previous(...args);}; }
  addEventListener('error',event=>record('error',[event.message,event.filename,event.lineno]));
  addEventListener('unhandledrejection',event=>record('rejection',[event.reason]));
  return true; })()
'@)
    [void](Wait-WorldState -Socket $socket -Description 'FishStand zone closed' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
        param($state)
        [int]$state.audit_counts.close -eq ([int]$beforeClose.audit_counts.close + 1) -and
            [int]$state.audit_pending -eq 0 -and
            [string]$state.command_status -ceq 'Aucune zone ouverte' -and
            @($state.placement_options).Count -eq 0
    })
    # Close replaces the renderer and reloads the page, so resident imports are gone.
    [void](Wait-StandLibrary -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)
    $beforeManagedOpen = @(Get-AuditedAuthoringEntries -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'open_managed').Count
    [void](Open-ManagedTidewaterZone -Socket $socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds)
    $reopened = Wait-WorldState -Socket $socket -Description 'FishStand reopened in Tidewater' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
        param($state)
        [int]$state.audit_pending -eq 0 -and
            @($state.placement_options).Count -eq 24 -and
            @($state.placement_options.value) -contains [string]$placed.id
    }
    [void](Wait-CdpReady -Socket $socket -Description 'reopened Tidewater terrain projection' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => { const status=document.querySelector('output.authoring-status')?.textContent??'';
  return {ready:status.includes('collisions synchronisées'),reason:status}; })()
'@)
    $afterManagedOpen = @(Get-AuditedAuthoringEntries -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'open_managed').Count
    if ($afterManagedOpen -ne ($beforeManagedOpen + 1)) { throw 'Managed zone did not reopen through the auditable UI route.' }
    $reopenedArchive = Get-ArchiveSnapshot -LiteralPath $workspaceArchive
    if (-not (Test-ArchiveSnapshotsEqual -Before $saved -After $reopenedArchive) -or
        -not (Test-ArchiveSnapshotsEqual -Before $savedObject -After (Get-ArchiveSnapshot -LiteralPath $savedObjectPath))) {
        throw 'Close/Open changed the saved workspace or immutable zone object.'
    }
    [void](Invoke-UiClick -Socket $socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Invoke-UiClick -Socket $socket -Text 'Placer / manipuler')
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Sélection et chargement')
    [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value ([string]$placed.id))
    [void](Invoke-UiClick -Socket $socket -Text 'Placer / manipuler')
    [void](Invoke-UiClick -Socket $socket -Text 'Cadrer cet objet dans la carte')
    Start-Sleep -Seconds 3
    $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'fish-stand-reopened.png')))
    [void](Invoke-UiClick -Socket $socket -Text 'Tester & suivi' -Selector '[data-authoring-tab-button]')
    if ($ReferenceVillageOnly) {
        $changed = Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const url = new URL(location.href); url.searchParams.set('forge_tidewater_reference_village','1');
  history.replaceState(history.state,'',url.href); return location.search.includes('forge_tidewater_reference_village=1'); })()
'@
        if (-not $changed) { throw 'Pinned source village reference URL was not selected.' }
        $sourceProfile = Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  const exposure=document.getElementById('authoring-graphics-exposure');
  const sharp=Array.from(panel ? panel.querySelectorAll('label') : []).find(label=>label.textContent.includes('Netteté'))?.querySelector('input');
  if (!panel || !exposure || !sharp) return false;
  panel.open=true;
  for (const [input,value] of [[exposure,'0.55'],[sharp,'0.45']]) {
    input.value=value; input.dispatchEvent(new Event('input',{bubbles:true}));
  }
  panel.open=false;
  return exposure.value==='0.55' && sharp.value==='0.45'; })()
'@
        if (-not $sourceProfile) { throw 'Source exposure and sharpening controls were not applied.' }
    }
    if (($RequireOceanGeometry -or $RequireOceanOptics) -and -not $UseDefaultWater) {
        $changed = Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const url = new URL(location.href); url.searchParams.set('forge_water_geometry','1');
  history.replaceState(history.state,'',url.href); return location.search.includes('forge_water_geometry=1'); })()
'@
        if (-not $changed) { throw 'Ocean geometry diagnostic URL was not selected.' }
        if ($RequireOceanOptics) {
            $changed = Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const url = new URL(location.href); url.searchParams.set('forge_water_optics','1');
  history.replaceState(history.state,'',url.href); return location.search.includes('forge_water_optics=1'); })()
'@
            if (-not $changed) { throw 'Ocean optics preview URL was not selected.' }
            if ($RequireShoreSim) {
                $changed = Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const url = new URL(location.href); url.searchParams.set('forge_shore_sim','1');
  history.replaceState(history.state,'',url.href); return location.search.includes('forge_shore_sim=1'); })()
'@
                if (-not $changed) { throw 'ShoreSim preview URL was not selected.' }
            }
        }
    }
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.addScriptToEvaluateOnNewDocument' -Parameters @{
        source = @'
(() => { window.__forgeSmokeErrors=[];
  const record=(kind,parts)=>{window.__forgeSmokeErrors.push(kind+': '+parts.map(String).join(' '));
    if(window.__forgeSmokeErrors.length>500) window.__forgeSmokeErrors.shift();};
  for(const name of ['error','warn','log','info','debug']) { const previous=console[name].bind(console);
    console[name]=(...args)=>{record(name,args);previous(...args);}; }
  addEventListener('error',event=>record('error',[event.message,event.filename,event.lineno]));
  addEventListener('unhandledrejection',event=>record('rejection',[event.reason])); })()
'@
    })
    [void](Invoke-UiClick -Socket $socket -Text 'Jouer')
    [void](Install-WorldAuditAfterOwnedReload -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)
    $playtest = Wait-WorldState -Socket $socket -Description 'FishStand playtest projection ready' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
        param($state)
        [string]$state.playtest_status -like 'Simulation active*' -and [int]$state.audit_pending -eq 0
    }
    if ($RequireLiveShadows) {
        [void](Wait-CdpReady -Socket $socket -Description 'Resident live shadow render passes' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => { const raw=document.getElementById('forge-authoring-canvas')?.getAttribute('data-tidewater-live-shadows');
  const state=raw?JSON.parse(raw):null;
  return {ready:state?.encoded_frames>=10 && !state.pending,reason:raw||'shadow diagnostics pending'}; })()
'@)
        $liveShadowEvidence += Measure-LiveShadowEvidence -Socket $socket
    }
    if ($ReferenceVillageOnly) {
        [void](Wait-CdpReady -Socket $socket -Description 'Pinned Tidewater village camera' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => { const canvas=document.getElementById('forge-authoring-canvas');
  const state=canvas?.getAttribute('data-tidewater-reference-village');
  const time=canvas?.getAttribute('data-tidewater-reference-time-ns');
  return {ready:state==='stable' && Number(time)>0,reason:`${state} ${time}`}; })()
'@)
        [void](Wait-CdpReady -Socket $socket -Description 'Pinned Tidewater live cloud bootstrap' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => { const text=document.querySelector('output#authoring-playtest-status')?.textContent??'';
  const submitted=Number(text.match(/nuages dynamiques (\d+) images/)?.[1]??0);
  return {ready:submitted>=10,reason:text}; })()
'@)
        $view = Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const style=document.createElement('style'); style.textContent=`
  .authoring-canvas-wrap {position:fixed!important;inset:0!important;width:100vw!important;height:100vh!important;z-index:2147483647!important;overflow:hidden!important}
  .authoring-canvas-wrap > :not(#forge-authoring-canvas) {display:none!important}
  #forge-authoring-canvas {width:100%!important;height:100%!important}`;
  document.head.append(style); const canvas=document.getElementById('forge-authoring-canvas');
  return {inner_width:innerWidth,inner_height:innerHeight,canvas_width:canvas?.getBoundingClientRect().width,
    canvas_height:canvas?.getBoundingClientRect().height,time_ns:canvas?.getAttribute('data-tidewater-reference-time-ns'),
    graphics_profile:JSON.parse(localStorage.getItem('forge.studio.tidewater.graphics.v1')||'null'),
    grass_status:Array.from(document.querySelectorAll('[data-authoring-tab="nature"] output')).map(el=>el.textContent).find(text=>text.includes('herbe source'))||null,
    playtest_status:document.querySelector('output#authoring-playtest-status')?.textContent}; })()
'@
        Start-Sleep -Seconds 3
        $imagePath = Join-Path $output 'tidewater-studio-village-reference.png'
        $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath $imagePath))
        $calibrated = Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const slider=document.getElementById('authoring-graphics-exposure');
  if (!slider) return false; slider.value='0.4';
  slider.dispatchEvent(new Event('input',{bubbles:true})); return slider.value==='0.4'; })()
'@
        if (-not $calibrated) { throw 'Studio calibrated exposure control was not applied.' }
        [void](Wait-CdpReady -Socket $socket -Description 'Studio calibrated exposure applied' -TimeoutSeconds 20 -Expression @'
(() => { const text=document.querySelector('output#authoring-playtest-status')?.textContent??'';
  return {ready:text.includes('exposition appliquée 0.40 EV'),reason:text}; })()
'@)
        Start-Sleep -Seconds 2
        $calibratedPath = Join-Path $output 'tidewater-studio-village-calibrated-040.png'
        $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath $calibratedPath))
        $exposureSweep = @()
        if ($CaptureExposureSweep) {
            foreach ($sweepEv in @(0.1, 0.0, -0.1, -0.15, 0.05, 0.1)) {
                $evValue = $sweepEv.ToString('0.00', [Globalization.CultureInfo]::InvariantCulture)
                $evLabel = $sweepEv.ToString('0.00', [Globalization.CultureInfo]::InvariantCulture)
                $selected = Invoke-CdpExpression -Socket $socket -Expression @"
(() => { const slider=document.getElementById('authoring-graphics-exposure');
  if (!slider) return false; slider.value='$evValue';
  slider.dispatchEvent(new Event('input',{bubbles:true})); return Number(slider.value)===Number('$evValue'); })()
"@
                if (-not $selected) { throw "Studio exposure sweep control failed at $evLabel EV." }
                [void](Wait-CdpReady -Socket $socket -Description "Studio exposure $evLabel EV applied" -TimeoutSeconds 20 -Expression @"
(() => { const text=document.querySelector('output#authoring-playtest-status')?.textContent??'';
  return {ready:text.includes('exposition appliquée $evLabel EV'),reason:text}; })()
"@)
                Start-Sleep -Seconds 3
                $suffix = [int][Math]::Round($sweepEv * 100)
                $sweepPath = Join-Path $output ('village-exposure-{0:000}-{1:00}.png' -f $suffix, $exposureSweep.Count)
                $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath $sweepPath))
                $exposureSweep += [ordered]@{
                    exposure_ev=$sweepEv; screenshot=$sweepPath
                    screenshot_sha256=(Get-FileSha256 -LiteralPath $sweepPath)
                }
            }
        }
        $referenceLedger = [ordered]@{
            schema='forge.tidewater.studio-village-reference'; version=1; status='captured'
            camera='tidewater-village-source-pinned'; source_time_ns=2000000000
            studio_time_ns=$view.time_ns; temporal_state_paired=$false
            candidate_sha256=(Get-FileSha256 -LiteralPath $candidate)
            source_fixture_sha256=$sourceBefore.Sha256
            source_preserved=(Test-ArchiveSnapshotsEqual -Before $sourceBefore -After (Get-ArchiveSnapshot -LiteralPath $sourceArchive))
            view=$view; screenshot=$imagePath; screenshot_sha256=(Get-FileSha256 -LiteralPath $imagePath)
            calibrated_exposure_ev=0.4; calibrated_screenshot=$calibratedPath
            calibrated_screenshot_sha256=(Get-FileSha256 -LiteralPath $calibratedPath)
            exposure_sweep=$exposureSweep
            live_shadow_evidence=$liveShadowEvidence
            gate_qualified=$false; human_visual_review=$false
        }
        $referenceLedgerPath = Join-Path $output 'tidewater-studio-village-reference.json'
        [IO.File]::WriteAllText($referenceLedgerPath, ($referenceLedger | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
        Write-Output "TIDEWATER_STUDIO_VILLAGE_REFERENCE_CAPTURED ledger=$referenceLedgerPath"
        return
    }
    [void](Wait-CdpReady -Socket $socket -Description 'Tidewater live clouds in playtest' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => { const text=document.querySelector('output#authoring-playtest-status')?.textContent??'';
  const submitted=Number(text.match(/nuages dynamiques (\d+) images/)?.[1]??0);
  return {ready:submitted>=10,reason:text}; })()
'@)
    if ($RequireOceanFft) {
        [void](Wait-CdpReady -Socket $socket -Description 'Tidewater four-cascade ocean FFT bootstrap' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => { const text=document.querySelector('output#authoring-playtest-status')?.textContent??'';
  const step=Number(text.match(/FFT océan prête, étape (\d+)/)?.[1]??0);
  return {ready:step>=120,reason:text}; })()
'@)
        $oceanStepsBefore = Invoke-CdpExpression -Socket $socket -Expression @'
(() => Number(document.querySelector('output#authoring-playtest-status')?.textContent?.match(/FFT océan prête, étape (\d+)/)?.[1]??0))()
'@
        [void](Wait-CdpReady -Socket $socket -Description 'Tidewater ocean FFT advances after bootstrap' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @"
(() => { const text=document.querySelector('output#authoring-playtest-status')?.textContent??'';
  const step=Number(text.match(/FFT océan prête, étape (\d+)/)?.[1]??0);
  return {ready:step>$([int]$oceanStepsBefore+5),reason:text}; })()
"@)
        $oceanStepsAfter = Invoke-CdpExpression -Socket $socket -Expression @'
(() => Number(document.querySelector('output#authoring-playtest-status')?.textContent?.match(/FFT océan prête, étape (\d+)/)?.[1]??0))()
'@
    }
    if ($RequireOceanGeometry) {
        [void](Wait-CdpReady -Socket $socket -Description 'Tidewater ocean source geometry' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => { const text=document.querySelector('output#authoring-playtest-status')?.textContent??'';
  const nodes=Number(text.match(/géométrie océan (\d+) nœuds/)?.[1]??0);
  return {ready:nodes>0,reason:text}; })()
'@)
    }
    if ($RequireOceanOptics) {
        [void](Wait-CdpReady -Socket $socket -Description 'Tidewater source optical material' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => { const text=document.querySelector('output#authoring-playtest-status')?.textContent??'';
  const step=Number(text.match(/optique océan source, rive dérivée (?:true|false), étape (\d+)/)?.[1]??0);
  return {ready:step>120,reason:text}; })()
'@)
    }
    if ($RequireShoreSim) {
        [void](Wait-CdpReady -Socket $socket -Description 'Tidewater live ShoreSim bootstrap' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @'
(() => { const text=document.querySelector('output#authoring-playtest-status')?.textContent??'';
  const step=Number(text.match(/ressac ShoreSim prêt, étape (\d+)/)?.[1]??0);
  return {ready:step>=120,reason:text}; })()
'@)
        $shoreStepsBefore = Invoke-CdpExpression -Socket $socket -Expression @'
(() => Number(document.querySelector('output#authoring-playtest-status')?.textContent?.match(/ressac ShoreSim prêt, étape (\d+)/)?.[1]??0))()
'@
        [void](Wait-CdpReady -Socket $socket -Description 'Tidewater ShoreSim advances with the ocean' -TimeoutSeconds $InteractionTimeoutSeconds -Expression @"
(() => { const text=document.querySelector('output#authoring-playtest-status')?.textContent??'';
  const step=Number(text.match(/ressac ShoreSim prêt, étape (\d+)/)?.[1]??0);
  return {ready:step>$([int]$shoreStepsBefore+5),reason:text}; })()
"@)
        $shoreStepsAfter = Invoke-CdpExpression -Socket $socket -Expression @'
(() => Number(document.querySelector('output#authoring-playtest-status')?.textContent?.match(/ressac ShoreSim prêt, étape (\d+)/)?.[1]??0))()
'@
    }
    $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'fish-stand-playtest-ready.png')))
    $changedExposure = Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  const slider=document.getElementById('authoring-graphics-exposure');
  if (!panel || !slider || slider.disabled) return false;
  panel.open=true; slider.value='-1'; slider.dispatchEvent(new Event('input',{bubbles:true}));
  panel.open=false; return true; })()
'@
    if (-not $changedExposure) { throw 'Tidewater Playtest graphics control missing.' }
    [void](Wait-CdpReady -Socket $socket -Description 'Tidewater graphics setting applied by renderer' -TimeoutSeconds 15 -Expression @'
(() => { const status=document.querySelector('output#authoring-playtest-status')?.textContent??'';
  return {ready:status.includes('exposition appliquée -1.00 EV'),reason:status}; })()
'@)
    $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'fish-stand-playtest-exposure-minus-1.png')))
    [void](Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  const button=[...panel.querySelectorAll('button')].find(button=>button.textContent.includes('Réinitialiser les effets'));
  button?.click(); return !!button; })()
'@)
    [void](Wait-CdpReady -Socket $socket -Description 'Tidewater graphics reset applied by renderer' -TimeoutSeconds 15 -Expression @'
(() => { const status=document.querySelector('output#authoring-playtest-status')?.textContent??'';
  return {ready:status.includes('exposition appliquée 0.10 EV'),reason:status}; })()
'@)
    if ($RequireAmbientOcclusion) {
        foreach ($value in @('0', '2')) {
            $changedAo = Invoke-CdpExpression -Socket $socket -Expression @"
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  const slider=document.getElementById('authoring-graphics-ao');
  if (!panel || !slider || slider.disabled) return false;
  panel.open=true; slider.value='$value'; slider.dispatchEvent(new Event('input',{bubbles:true}));
  panel.open=false; return true; })()
"@
            if (-not $changedAo) { throw 'Tidewater ambient-occlusion control missing.' }
            [void](Wait-CdpReady -Socket $socket -Description "Tidewater ambient occlusion $value applied" -TimeoutSeconds 15 -Expression @"
(() => { const text=document.getElementById('authoring-playtest-status')?.textContent??'';
  return {ready:text.includes('occlusion ambiante $value.00'),reason:text}; })()
"@)
            $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output "fish-stand-ao-$value.png")))
        }
        [void](Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  const button=[...panel.querySelectorAll('button')].find(node=>node.textContent.includes('Réinitialiser les effets'));
  button?.click(); return !!button; })()
'@)
        [void](Wait-CdpReady -Socket $socket -Description 'Tidewater ambient-occlusion reset' -TimeoutSeconds 15 -Expression @'
(() => { const text=document.getElementById('authoring-playtest-status')?.textContent??'';
  return {ready:text.includes('occlusion ambiante 1.00'),reason:text}; })()
'@)
    }
    if ($RequireAirHaze) {
        foreach ($case in @(@{ Value='0'; Expected='0.00' }, @{ Value='1.6'; Expected='1.60' })) {
            $changedHaze = Invoke-CdpExpression -Socket $socket -Expression @"
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  const slider=document.getElementById('authoring-graphics-haze');
  if (!panel || !slider || slider.disabled) return false;
  panel.open=true; slider.value='$($case.Value)'; slider.dispatchEvent(new Event('input',{bubbles:true}));
  panel.open=false; return true; })()
"@
            if (-not $changedHaze) { throw 'Tidewater air-haze control missing.' }
            [void](Wait-CdpReady -Socket $socket -Description "Tidewater air haze $($case.Value) applied" -TimeoutSeconds 15 -Expression @"
(() => { const text=document.getElementById('authoring-playtest-status')?.textContent??'';
  return {ready:text.includes('brume marine $($case.Expected)'),reason:text}; })()
"@)
            $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output "fish-stand-haze-$($case.Value).png")))
        }
        [void](Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  const button=[...panel.querySelectorAll('button')].find(node=>node.textContent.includes('Réinitialiser les effets'));
  button?.click(); return !!button; })()
'@)
        [void](Wait-CdpReady -Socket $socket -Description 'Tidewater air-haze reset' -TimeoutSeconds 15 -Expression @'
(() => { const text=document.getElementById('authoring-playtest-status')?.textContent??'';
  return {ready:text.includes('brume marine 1.60'),reason:text}; })()
'@)
    }
    if ($RequireMotionBlurWithOcean) {
        foreach ($case in @(@{ Value='0'; Expected='flou de mouvement arrêté' },
                            @{ Value='360'; Expected='flou de mouvement 360°' })) {
            $changedBlur = Invoke-CdpExpression -Socket $socket -Expression @"
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  const slider=document.getElementById('authoring-graphics-motion-blur');
  if (!panel || !slider || slider.disabled) return false;
  panel.open=true; slider.value='$($case.Value)'; slider.dispatchEvent(new Event('input',{bubbles:true}));
  panel.open=false; return true; })()
"@
            if (-not $changedBlur) { throw 'Tidewater motion-blur control missing.' }
            [void](Wait-CdpReady -Socket $socket -Description "Tidewater motion blur $($case.Value) applied with ocean" -TimeoutSeconds 15 -Expression @"
(() => { const text=document.getElementById('authoring-playtest-status')?.textContent??'';
  return {ready:text.includes('$($case.Expected)') && text.includes('optique océan source'),reason:text}; })()
"@)
        }
        [void](Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  const button=[...panel.querySelectorAll('button')].find(node=>node.textContent.includes('Réinitialiser les effets'));
  button?.click(); return !!button; })()
'@)
        [void](Wait-CdpReady -Socket $socket -Description 'Tidewater motion blur reset with ocean' -TimeoutSeconds 15 -Expression @'
(() => { const text=document.getElementById('authoring-playtest-status')?.textContent??'';
  return {ready:text.includes('flou de mouvement 180°') && text.includes('optique océan source'),reason:text}; })()
'@)
    }
    if ($RequireLensFlare) {
        foreach ($value in @('0', '2')) {
            $changedFlare = Invoke-CdpExpression -Socket $socket -Expression @"
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  const slider=document.getElementById('authoring-graphics-flare');
  if (!panel || !slider || slider.disabled) return false;
  panel.open=true; slider.value='$value'; slider.dispatchEvent(new Event('input',{bubbles:true}));
  panel.open=false; return true; })()
"@
            if (-not $changedFlare) { throw 'Tidewater lens-flare control missing.' }
            [void](Wait-CdpReady -Socket $socket -Description "Tidewater lens flare $value applied" -TimeoutSeconds 15 -Expression @"
(() => { const text=document.getElementById('authoring-playtest-status')?.textContent??'';
  return {ready:text.includes('flare solaire $value.00'),reason:text}; })()
"@)
            $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output "fish-stand-flare-$value.png")))
        }
        [void](Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  const button=[...panel.querySelectorAll('button')].find(node=>node.textContent.includes('Réinitialiser les effets'));
  button?.click(); return !!button; })()
'@)
        [void](Wait-CdpReady -Socket $socket -Description 'Tidewater lens-flare reset' -TimeoutSeconds 15 -Expression @'
(() => { const text=document.getElementById('authoring-playtest-status')?.textContent??'';
  return {ready:text.includes('flare solaire 1.00'),reason:text}; })()
'@)
    }
    if ($RequireGroundBounce) {
        foreach ($value in @('0', '2')) {
            $changedBounce = Invoke-CdpExpression -Socket $socket -Expression @"
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  const slider=document.getElementById('authoring-graphics-bounce');
  if (!panel || !slider) return false;
  panel.open=true; slider.value='$value'; slider.dispatchEvent(new Event('input',{bubbles:true}));
  panel.open=false; return true; })()
"@
            if (-not $changedBounce) { throw 'Tidewater ground-bounce control missing.' }
            [void](Wait-CdpReady -Socket $socket -Description "Tidewater ground-bounce strength $value applied" -TimeoutSeconds 15 -Expression @"
(() => { const text=document.getElementById('authoring-playtest-status')?.textContent??'';
  return {ready:text.includes('lumière rebond source, intensité $value.00'),reason:text}; })()
"@)
            $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output "fish-stand-bounce-$value.png")))
        }
        [void](Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  const button=[...panel.querySelectorAll('button')].find(node=>node.textContent.includes('Réinitialiser les effets'));
  button?.click(); return !!button; })()
'@)
        [void](Wait-CdpReady -Socket $socket -Description 'Tidewater ground-bounce reset' -TimeoutSeconds 15 -Expression @'
(() => { const text=document.getElementById('authoring-playtest-status')?.textContent??'';
  return {ready:text.includes('lumière rebond source, intensité 1.00'),reason:text}; })()
'@)
    }
    $graphicsControls = Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  if (!panel) return false;
  panel.open=true;
  const ids=['authoring-graphics-auto-exposure','authoring-graphics-fxaa',
    'authoring-graphics-grain','authoring-graphics-motion-blur'];
  return ids.every(id=>!!document.getElementById(id)); })()
'@
    if (-not $graphicsControls) { throw 'Tidewater post-processing controls missing from Studio playtest.' }
    $changedEffects = Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const values=[['authoring-graphics-grain','0.03'],['authoring-graphics-motion-blur','0']];
  for (const [id,value] of values) { const input=document.getElementById(id);
    input.value=value; input.dispatchEvent(new Event('input',{bubbles:true})); }
  return values.every(([id,value])=>document.getElementById(id)?.value===value); })()
'@
    if (-not $changedEffects) { throw 'Tidewater post-processing slider update failed.' }
    [void](Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  const button=[...panel.querySelectorAll('button')].find(node=>node.textContent.includes('Réinitialiser les effets'));
  button?.click(); panel.open=false; return !!button; })()
'@)
    [void](Wait-CdpReady -Socket $socket -Description 'Tidewater post-processing controls reset' -TimeoutSeconds 15 -Expression @'
(() => { const expected=[['authoring-graphics-grain',0.012],['authoring-graphics-motion-blur',180]];
  const ready=expected.every(([id,value])=>Math.abs(Number(document.getElementById(id)?.value)-value)<0.001);
  return {ready,reason:expected.map(([id])=>id+'='+document.getElementById(id)?.value).join(' ')}; })()
'@)
    $beforeToggle = Invoke-CdpExpression -Socket $socket -Expression @'
(() => Number(document.querySelector('output#authoring-playtest-status')?.textContent?.match(/Tick (\d+)/)?.[1]??0))()
'@
    [void](Invoke-CdpExpression -Socket $socket -Expression @'
(() => { for (const id of ['authoring-graphics-auto-exposure','authoring-graphics-fxaa']) {
  const input=document.getElementById(id); input.checked=false;
  input.dispatchEvent(new Event('change',{bubbles:true})); }
  return true; })()
'@)
    [void](Wait-CdpReady -Socket $socket -Description 'Tidewater exposure and FXAA toggles keep simulation alive' -TimeoutSeconds 15 -Expression @"
(() => { const status=document.querySelector('output#authoring-playtest-status')?.textContent??'';
  const tick=Number(status.match(/Tick (\d+)/)?.[1]??0);
  return {ready:tick>$beforeToggle+10 && status.startsWith('Simulation active'),reason:status}; })()
"@)
    [void](Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const panel=document.getElementById('authoring-playtest-graphics');
  const button=[...panel.querySelectorAll('button')].find(node=>node.textContent.includes('Réinitialiser les effets'));
  button?.click(); return !!button; })()
'@)
    [void](Wait-CdpReady -Socket $socket -Description 'Tidewater exposure and FXAA toggles reset' -TimeoutSeconds 15 -Expression @'
(() => { const exposure=document.getElementById('authoring-graphics-auto-exposure');
  const fxaa=document.getElementById('authoring-graphics-fxaa');
  return {ready:exposure?.checked===true && fxaa?.checked===true,reason:`auto=${exposure?.checked} fxaa=${fxaa?.checked}`}; })()
'@)
    $canvasPoint = Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const r=document.getElementById('forge-authoring-canvas')?.getBoundingClientRect();
  if (!r || r.width < 1 || r.height < 1) return null;
  return {x:r.left+r.width/2,y:r.top+r.height/2}; })()
'@
    if ($null -eq $canvasPoint) { throw 'Playtest canvas has no clickable bounds.' }
    Send-CdpMouseClick -Socket $socket -X ([double]$canvasPoint.x) -Y ([double]$canvasPoint.y)
    # A WebView can consume the first CDP click solely to restore window focus
    # after a settings control was operated. A second real click is still a
    # pointer-lock gesture; do not synthesize requestPointerLock via eval.
    Start-Sleep -Milliseconds 300
    $lockedAfterFocus = Invoke-CdpExpression -Socket $socket -Expression "document.pointerLockElement?.id==='forge-authoring-canvas'"
    if (-not $lockedAfterFocus) {
        Send-CdpMouseClick -Socket $socket -X ([double]$canvasPoint.x) -Y ([double]$canvasPoint.y)
    }
    [void](Wait-CdpReady -Socket $socket -Description 'FishStand playtest input capture' -TimeoutSeconds 15 -Expression @'
(() => { const locked=document.pointerLockElement?.id==='forge-authoring-canvas';
  return {ready:locked,reason:document.querySelector('output#authoring-playtest-status')?.textContent??'no status'}; })()
'@)
    if ($RequireOceanGeometry -or $RequireOceanOptics) {
        Send-CdpMouseEvent -Socket $socket -Type mouseMoved -X ([double]$canvasPoint.x + 1500.0) -Y ([double]$canvasPoint.y)
        Start-Sleep -Seconds 1
        $name = if ($RequireOceanOptics) { 'fish-stand-ocean-optics-seaward.png' } else { 'fish-stand-ocean-geometry-seaward.png' }
        $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output $name)))
    }
    if ($CaptureSunFlarePair) {
        if (-not $RequireLensFlare) { throw 'Sun flare pair requires lens-flare control.' }
        # From the seaward heading, turn toward the pinned westward sun and look up.
        Send-CdpMouseEvent -Socket $socket -Type mouseMoved -X ([double]$canvasPoint.x - 750.0) -Y ([double]$canvasPoint.y - 220.0)
        Start-Sleep -Seconds 1
        $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'fish-stand-flare-sun-aim.png')))
        foreach ($value in @('0', '2')) {
            [void](Invoke-CdpExpression -Socket $socket -Expression @"
(() => { const slider=document.getElementById('authoring-graphics-flare');
  slider.value='$value'; slider.dispatchEvent(new Event('input',{bubbles:true})); return true; })()
"@)
            [void](Wait-CdpReady -Socket $socket -Description "Tidewater sunward flare $value applied" -TimeoutSeconds 15 -Expression @"
(() => { const text=document.getElementById('authoring-playtest-status')?.textContent??'';
  return {ready:text.includes('flare solaire $value.00'),reason:text}; })()
"@)
            $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output "fish-stand-flare-sun-$value.png")))
        }
        [void](Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const slider=document.getElementById('authoring-graphics-flare');
  slider.value='1'; slider.dispatchEvent(new Event('input',{bubbles:true})); return true; })()
'@)
    }
    Send-CdpKeyHold -Socket $socket -Key 'w' -Code 'KeyW' -VirtualKeyCode 87 -Milliseconds 900
    $movement = Wait-WorldState -Socket $socket -Description 'FishStand resolved player movement' -TimeoutSeconds 15 -Predicate {
        param($state)
        [string]$state.playtest_status -match 'dernier mouvement observé tick' -and
            [string]$state.playtest_status -match 'entrée active true' -and
            [string]$state.playtest_status -notmatch 'résolu \(0\.0000, 0\.0000\)'
    }
    if ($RequireLiveShadows) {
        $liveShadowEvidence += Measure-LiveShadowEvidence -Socket $socket
        if ($liveShadowEvidence[-1].after.generations -ne $liveShadowEvidence[0].after.generations) {
            throw 'Player camera movement replaced the resident shadow atlas.'
        }
    }
    $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'fish-stand-playtest.png')))
    Send-CdpEscape -Socket $socket
    [void](Invoke-UiClick -Socket $socket -Text "Retour à l’édition")
    [void](Install-WorldAuditAfterOwnedReload -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Wait-WorldState -Socket $socket -Description 'FishStand return to editing' -TimeoutSeconds $InteractionTimeoutSeconds -Predicate {
        param($state)
        [string]$state.playtest_status -like 'Mode édition*' -and [int]$state.audit_pending -eq 0
    })
    if (-not (Test-ArchiveSnapshotsEqual -Before $saved -After (Get-ArchiveSnapshot -LiteralPath $workspaceArchive)) -or
        -not (Test-ArchiveSnapshotsEqual -Before $savedObject -After (Get-ArchiveSnapshot -LiteralPath $savedObjectPath))) {
        throw 'Playtest changed the saved FishStand workspace or zone object.'
    }
}
catch {
    $failure = $_.Exception.Message
    if ($null -ne $socket -and $socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
        try { $failureState = Get-WorldState -Socket $socket } catch { Write-Verbose $_.Exception.Message }
        try { $browserErrors = @(Invoke-CdpExpression -Socket $socket -Expression 'window.__forgeSmokeErrors??[]') }
        catch { Write-Verbose $_.Exception.Message }
        try { $screenshots.Add((Save-CdpScreenshot -Socket $socket -LiteralPath (Join-Path $output 'fish-stand-failure.png'))) }
        catch { Write-Warning "Failure screenshot unavailable: $($_.Exception.Message)" }
    }
}
finally {
    if ($null -ne $socket) {
        if ($socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
            try { [void](Restore-WorldAudit -Socket $socket) } catch { Write-Warning "Audit restore: $($_.Exception.Message)" }
            try {
                $closing = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(2))
                try { Wait-NonGenericTask -Task ($socket.CloseAsync([Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'Tidewater smoke complete', $closing.Token)) }
                finally { $closing.Dispose() }
            } catch { Write-Verbose $_.Exception.Message }
        }
        $socket.Dispose()
    }
    if ($null -ne $process -and -not $process.HasExited) {
        try { [void]$process.CloseMainWindow() } catch { Write-Verbose $_.Exception.Message }
        if (-not $process.WaitForExit(5000)) {
            Stop-Process -Id $process.Id -Force
            [void]$process.WaitForExit(5000)
        }
    }
    if ($null -ne $reservation) { $reservation.Listener.Stop() }
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
}

$sourceAfter = Get-ArchiveSnapshot -LiteralPath $sourceArchive
$sourceWorkspaceAfter = Get-ArchiveSnapshot -LiteralPath $sourceWorkspaceArchive
$sourceObjectAfter = Get-ArchiveSnapshot -LiteralPath $sourceObject
$sourcePreserved = (Test-ArchiveSnapshotsEqual -Before $sourceBefore -After $sourceAfter) -and
    (Test-ArchiveSnapshotsEqual -Before $sourceWorkspaceBefore -After $sourceWorkspaceAfter) -and
    (Test-ArchiveSnapshotsEqual -Before $sourceObjectBefore -After $sourceObjectAfter)
if (-not $sourcePreserved) { $failure = "Source Tidewater fixture changed. $failure" }
$ledger = [ordered]@{
    schema='forge.tidewater.fish-stand-studio-smoke'; version=1
    status=if ([string]::IsNullOrWhiteSpace($failure)) { 'passed' } else { 'failed_closed' }
    failure=$failure; gate_qualified=$false; human_visual_review=$false
    candidate_executable=$candidate; candidate_sha256=(Get-FileSha256 -LiteralPath $candidate)
    source_fixture=$source; source_archive_before=$sourceBefore; source_archive_after=$sourceAfter
    source_workspace_before=$sourceWorkspaceBefore; source_workspace_after=$sourceWorkspaceAfter
    source_object_before=$sourceObjectBefore; source_object_after=$sourceObjectAfter
    source_preserved=$sourcePreserved; isolated_archive_before=$cloneBefore; isolated_workspace_before=$workspaceBefore
    isolated_archive_after=(Get-ArchiveSnapshot -LiteralPath $archive)
    isolated_workspace_after=(Get-ArchiveSnapshot -LiteralPath $workspaceArchive)
    zone_uuid=$zoneId; initial_placements=23; expected_final_placements=24
    fish_stand_asset_id=$standAssetId; fish_stand_bundle_sha256=$bundlePin
    accepted_placement=$placed; saved_workspace=$saved; saved_object_path=$savedObjectPath; saved_object=$savedObject
    authoring_pass=($null -ne $reopened -and $sourcePreserved)
    preview_request_sha256=if($null -ne $previewEntry){$previewEntry.zone_request_sha256}else{$null}
    apply_request_sha256=if($null -ne $applyEntry){$applyEntry.zone_request_sha256}else{$null}
    reopened=($null -ne $reopened); playtest_status=if($null -ne $playtest){$playtest.playtest_status}elseif($null -ne $failureState){$failureState.playtest_status}else{$null}
    ocean_fft_required=[bool]$RequireOceanFft; ocean_fft_steps_before=$oceanStepsBefore; ocean_fft_steps_after=$oceanStepsAfter
    ocean_geometry_required=[bool]$RequireOceanGeometry
    ocean_optics_required=[bool]$RequireOceanOptics
    shore_sim_required=[bool]$RequireShoreSim; default_water=[bool]$UseDefaultWater; shore_sim_steps_before=$shoreStepsBefore; shore_sim_steps_after=$shoreStepsAfter
    ground_bounce_required=[bool]$RequireGroundBounce
    ambient_occlusion_required=[bool]$RequireAmbientOcclusion
    lens_flare_required=[bool]$RequireLensFlare
    air_haze_required=[bool]$RequireAirHaze
    motion_blur_with_ocean_required=[bool]$RequireMotionBlurWithOcean
    graphics_persistence_required=[bool]$RequireGraphicsPersistence
    live_shadows_required=[bool]$RequireLiveShadows; live_shadow_evidence=$liveShadowEvidence
    sun_flare_pair_captured=[bool]$CaptureSunFlarePair
    movement_status=if($null -ne $movement){$movement.playtest_status}else{$null}
    last_command_status=if($null -ne $failureState){$failureState.command_status}else{$null}
    browser_errors=@($browserErrors)
    physical_click_attempts=@($attempts); screenshots=@($screenshots)
    limitations=@('Joe readiness is observed indirectly by accepted playtest synchronization; five fish payloads are pinned, but individual GPU fish meshes are not counted by the Studio DOM diagnostic.','Screenshots are automated evidence, not a human visual review.')
}
$ledgerPath = Join-Path $output 'fish-stand-studio-smoke-ledger.json'
[IO.File]::WriteAllText($ledgerPath, ($ledger | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
if ($ledger.status -cne 'passed') { throw "Tidewater FishStand Studio smoke failed closed: $failure. Ledger: $ledgerPath" }
Write-Output "TIDEWATER_FISH_STAND_STUDIO_SMOKE_PASS ledger=$ledgerPath"
