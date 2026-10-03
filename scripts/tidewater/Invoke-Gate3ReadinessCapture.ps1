#Requires -Version 7.2
# Captures only diagnostics published in the DOM of an isolated public playtest.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CandidatePath,
    [Parameter(Mandatory)][string]$FixtureSource,
    [Parameter(Mandatory)][string]$EvidencePath,
    [string]$BrowserFolder = 'C:\Program Files (x86)\Microsoft\EdgeWebView\Application\154.0.4258.37',
    [ValidateRange(0, 60)][int]$ObservationDelaySeconds = 20,
    [switch]$AwaitObjectCollisionBlock,
    [switch]$AwaitSimulationReady,
    [string]$AuthoringLedger,
    [ValidateRange(0, 60)][int]$WarmupSeconds = 0,
    [ValidateRange(0, 120)][int]$SampleSeconds = 0,
    [switch]$ShowPublicMetrics,
    [ValidateRange(640, 3840)][int]$TargetCanvasWidth = 1920,
    [ValidateRange(360, 2160)][int]$TargetCanvasHeight = 1080,
    [ValidateRange(30, 240)][int]$PublicStateTimeoutSeconds = 240
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Invoke-Gate3GameplayPlaytestSmoke.ps1') -SelfTest | Out-Null
$captureZone = 'e620dd19-ead9-5913-a6d6-0f93109148c0'
$capturePin = '50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720'
$captureCandidate = [IO.Path]::GetFullPath($CandidatePath)
$captureSource = [IO.Path]::GetFullPath($FixtureSource)
$captureOutput = [IO.Path]::GetFullPath($EvidencePath)
Assert-DriveEPath -LiteralPath $captureOutput -Description 'Readiness capture output'
Assert-TreeHasNoReparsePoints -LiteralPath $captureSource -Description 'Readiness source fixture'
if (Test-Path -LiteralPath $captureOutput) { throw 'Evidence output must be fresh.' }
if ((Test-PathWithinDirectory -LiteralPath $captureOutput -Directory $captureSource) -or (Test-PathWithinDirectory -LiteralPath $captureSource -Directory $captureOutput)) { throw 'Source and output must be separate.' }
if (-not (Test-Path -LiteralPath $captureCandidate -PathType Leaf)) { throw 'Candidate absent.' }
if ($AwaitObjectCollisionBlock -and $AwaitSimulationReady) { throw 'Choose one public state target.' }
if (($WarmupSeconds -gt 0 -or $SampleSeconds -gt 0 -or $ShowPublicMetrics) -and -not $AwaitSimulationReady) { throw 'Runtime observation requires simulation-ready state.' }
if ($AwaitSimulationReady) {
    if ([string]::IsNullOrWhiteSpace($AuthoringLedger)) { throw 'Simulation-ready observation requires the concordant authoring ledger.' }
    $captureAuthoring = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText([IO.Path]::GetFullPath($AuthoringLedger)))
    if ($captureAuthoring.schema -cne 'forge.tidewater.gate3-gameplay-authoring-smoke' -or $captureAuthoring.status -cne 'passed') { throw 'Expected a passed gameplay authoring ledger.' }
    if ([bool]$captureAuthoring.gate_qualified -or -not [bool]$captureAuthoring.source_preserved -or [string]$captureAuthoring.zone_uuid -cne $captureZone) { throw 'Authoring ledger provenance or scope is invalid.' }
    if (-not [IO.Path]::GetFullPath([string]$captureAuthoring.isolated_fixture).Equals($captureSource, [StringComparison]::OrdinalIgnoreCase)) { throw 'Authoring ledger does not name this source fixture.' }
    if ($captureAuthoring.candidate_sha256 -cne (Get-FileSha256 -LiteralPath $captureCandidate)) { throw 'Candidate differs from authoring ledger.' }
    if ($captureAuthoring.source_original_hash_pinned -cne $capturePin) { throw 'Authoring original source differs from the pin.' }
}
$captureReference = Get-WorkspaceZoneReference -Archive (Join-Path $captureSource 'workspace-v1\authoring.json') -ZoneId $captureZone
$captureAuthoredHash = Get-NormalizedSha256 -Value ([string]$captureReference.archive_hash)
if ($AwaitSimulationReady -and ([string]$captureAuthoring.saved_reference.archive_hash -cne $captureAuthoredHash -or [string]$captureAuthoring.saved_reference.head_hash -cne [string]$captureReference.head_hash)) { throw 'Authoring checkpoint differs from the source fixture reference.' }
$captureRelativeArchives = @("$captureZone\authoring.json", 'workspace-v1\authoring.json', "workspace-v1\objects\$capturePin.json", "workspace-v1\objects\$captureAuthoredHash.json")
$captureSourceBefore = @($captureRelativeArchives | ForEach-Object { Get-ArchiveSnapshot -LiteralPath (Join-Path $captureSource $_) })
if ($captureSourceBefore[0].Sha256 -cne $capturePin -or $captureSourceBefore[2].Sha256 -cne $capturePin) { throw 'Original source pin differs.' }
if ($captureSourceBefore[3].Sha256 -cne $captureAuthoredHash) { throw 'Authored archive bytes differ from the workspace reference.' }
[void](New-Item -ItemType Directory -Path $captureOutput)
$captureFixture = Join-Path $captureOutput 'playtest-root'
[void](Copy-IsolatedFixture -SourceRoot $captureSource -DestinationRoot $captureFixture -ZoneId $captureZone)
$captureFixtureBefore = @($captureRelativeArchives | ForEach-Object { Get-ArchiveSnapshot -LiteralPath (Join-Path $captureFixture $_) })
$captureSavedEnvironment = @{}
foreach ($captureName in @('FORGE_AUTHORING_ROOT','FORGE_AUTHOR_SUBJECT','WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS','WEBVIEW2_USER_DATA_FOLDER','WEBVIEW2_BROWSER_EXECUTABLE_FOLDER')) { $captureSavedEnvironment[$captureName] = [Environment]::GetEnvironmentVariable($captureName, 'Process') }
$captureRun = $null
$captureStop = $null
$captureFailure = $null
$captureStateFailure = $null
$capturePublic = $null
$captureScreen = $null
$captureSamples = [System.Collections.Generic.List[object]]::new()
$captureReadyElapsed = $null
$captureMetricsClick = $null
$captureCandidateHash = Get-FileSha256 -LiteralPath $captureCandidate
$captureEnvironment = $null
if ($AwaitSimulationReady) {
    $captureOs = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
    $captureCpu = @(Get-CimInstance Win32_Processor -ErrorAction Stop | Select-Object Name, NumberOfCores, NumberOfLogicalProcessors)
    $captureGpu = @(Get-CimInstance Win32_VideoController -ErrorAction Stop | Select-Object Name, DriverVersion, PNPDeviceID)
    $captureProcesses = @(Get-CimInstance Win32_Process -ErrorAction Stop | Where-Object { $_.Name -match '^(cargo|rustc|rust-lld|link|clang|trunk|forge-studio(?:-candidate)?|forge-runtime|renderdoccmd|qrenderdoc|PresentMon|RadeonGPUProfiler|WinPixGpuCapturer)\.exe$' } | Select-Object Name, ProcessId, ParentProcessId, CreationDate)
    $captureEnvironment = [ordered]@{
        recorded_at_utc = [DateTimeOffset]::UtcNow.ToString('O')
        os = [ordered]@{ caption=$captureOs.Caption; version=$captureOs.Version; build_number=$captureOs.BuildNumber; architecture=$captureOs.OSArchitecture }
        memory_total_mib = [Math]::Round([double]$captureOs.TotalVisibleMemorySize / 1024.0, 2)
        memory_available_mib = [Math]::Round([double]$captureOs.FreePhysicalMemory / 1024.0, 2)
        cpu = $captureCpu; gpu = $captureGpu; observed_workloads_before_launch = $captureProcesses
        compiler_or_linker_active = @($captureProcesses | Where-Object { $_.Name -match '^(cargo|rustc|rust-lld|link|clang|trunk)\.exe$' }).Count -gt 0
        profiler_scope = 'named external profiler processes only; a complete profiler or system-load audit is not claimed'
        browser_folder = $BrowserFolder
        browser_executable_sha256 = Get-FileSha256 -LiteralPath (Join-Path $BrowserFolder 'msedgewebview2.exe')
    }
    [IO.File]::WriteAllText((Join-Path $captureOutput 'hardware-workload-preflight.json'), (ConvertTo-CompactJson -Value $captureEnvironment), [Text.UTF8Encoding]::new($false))
}
try {
    [Environment]::SetEnvironmentVariable('WEBVIEW2_BROWSER_EXECUTABLE_FOLDER', $BrowserFolder, 'Process')
    $captureRun = Start-ForgeStudioPlaytestProcess -CandidatePath $captureCandidate -CandidateHash $captureCandidateHash -FixtureRoot $captureFixture -EvidencePath $captureOutput -RunNumber 1 -StartupTimeout 90
    Open-Gate3PlaytestZone -Socket $captureRun.Socket -ZoneId $captureZone -TimeoutSeconds 90
    if ($AwaitSimulationReady) { [void](Invoke-UiClick -Socket $captureRun.Socket -Text 'Tester & suivi' -Selector '[data-authoring-tab-button]') }
    $captureReadyClock = [Diagnostics.Stopwatch]::StartNew()
    [void](Invoke-UiClick -Socket $captureRun.Socket -Text 'Jouer' -Selector 'button.authoring-play')
    [void](Wait-CdpReady -Socket $captureRun.Socket -TimeoutSeconds 30 -Description 'public playtest HUD' -Expression "(() => ({ready:!!document.querySelector('section.authoring-gameplay-controls')&&!!document.getElementById('authoring-playtest-status'),terminal:false,reason:'public playtest HUD pending'}))()")
    if ($AwaitSimulationReady) {
        try {
            Focus-PlaytestCanvas -Socket $captureRun.Socket -TimeoutSeconds $PublicStateTimeoutSeconds
            [void](Wait-PlaytestReady -Socket $captureRun.Socket -TimeoutSeconds $PublicStateTimeoutSeconds -Description 'actual authored Tidewater simulation-ready observation')
            $captureReadyClock.Stop()
            $captureReadyElapsed = $captureReadyClock.Elapsed.TotalMilliseconds
            if ($ShowPublicMetrics) {
                Release-PlaytestCapture -Socket $captureRun.Socket -TimeoutSeconds 15
                [void](Open-DetailsBySummary -Socket $captureRun.Socket -Summary 'Diagnostics du rendu et du playtest')
                $captureMetricsClick = Invoke-UiClick -Socket $captureRun.Socket -Text 'Afficher les mesures dans la vue 3D' -Selector 'label'
                Focus-PlaytestCanvas -Socket $captureRun.Socket -TimeoutSeconds 30
            }
            for ($captureSecond=0;$captureSecond -lt $WarmupSeconds;$captureSecond++) { Start-Sleep -Seconds 1 }
            for ($captureSecond=0;$captureSecond -lt $SampleSeconds;$captureSecond++) {
                $captureSample = Invoke-CdpExpression -Socket $captureRun.Socket -Expression @'
(() => {
  const canvas=document.getElementById('forge-authoring-canvas'),raw=canvas?.getAttribute('data-tidewater-live-shadows')??null;
  let live=null;try{live=raw?JSON.parse(raw):null}catch(error){live={parse_error:String(error)}};
  return {captured_at_utc:new Date().toISOString(),status:document.getElementById('authoring-playtest-status')?.textContent??'',visibility_state:document.visibilityState,focused:document.hasFocus(),viewport:canvas?{css_width:canvas.clientWidth,css_height:canvas.clientHeight,render_width:canvas.width,render_height:canvas.height,device_pixel_ratio:window.devicePixelRatio}:null,live};
})()
'@
                $captureSamples.Add($captureSample)
                Start-Sleep -Seconds 1
            }
            [IO.File]::WriteAllText((Join-Path $captureOutput 'playtest-public-samples.json'), (ConvertTo-CompactJson -Value $captureSamples.ToArray()), [Text.UTF8Encoding]::new($false))
        }
        catch { $captureStateFailure = $_.Exception.Message }
    } elseif ($AwaitObjectCollisionBlock) {
        try {
        [void](Wait-CdpReady -Socket $captureRun.Socket -TimeoutSeconds $PublicStateTimeoutSeconds -Description 'post-warmup objects/collisions Tick 0 or ready state' -Expression @'
(() => {
  const status=(document.getElementById('authoring-playtest-status')?.textContent??'').replace(/\s+/g,' ').trim();
  return {ready:/objets\/collisions.*Tick\s+0/i.test(status)||/simulation prête\s+true/i.test(status),terminal:false,reason:status||'public status pending'};
})()
'@)
        }
        catch { $captureStateFailure = $_.Exception.Message }
    } elseif ($ObservationDelaySeconds -gt 0) { Start-Sleep -Seconds $ObservationDelaySeconds }
    $capturePublic = Invoke-CdpExpression -Socket $captureRun.Socket -Expression @'
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const parse=raw=>{try{return raw?JSON.parse(raw):null}catch(error){return {parse_error:String(error),raw}}};
  const ids=['forge-r2-050-projection-diagnostic','forge-r2-050-placement-projection-diagnostic','forge-r2-050-building-projection-diagnostic'];
  const diagnostics=Object.fromEntries(ids.map(id=>{const node=document.getElementById(id),raw=node?.textContent??null;return [id,{present:!!node,raw,value:parse(raw)}]}));
  const canvas=document.getElementById('forge-authoring-canvas'),liveRaw=canvas?.getAttribute('data-tidewater-live-shadows')??null;
  return {
    captured_at_utc:new Date().toISOString(),
    url:location.href,
    status:norm(document.getElementById('authoring-playtest-status')?.textContent),
    input:norm(document.getElementById('studio-input-capture-status')?.textContent),
    keyboard:norm(document.getElementById('studio-keyboard-event-status')?.textContent),
    viewport:canvas?{css_width:canvas.clientWidth,css_height:canvas.clientHeight,render_width:canvas.width,render_height:canvas.height,bounding_width:canvas.getBoundingClientRect().width,bounding_height:canvas.getBoundingClientRect().height,device_pixel_ratio:window.devicePixelRatio}:null,
    graphics_controls:[...document.querySelectorAll('[id^="authoring-graphics-"]')].map(node=>({id:node.id,tag:node.tagName,type:node.type??null,value:node.value??null,checked:node.checked??null,text:node.textContent??null})),
    diagnostics,
    live_shadows:{present:liveRaw!==null,raw:liveRaw,value:parse(liveRaw)},
    all_diagnostic_nodes:[...document.querySelectorAll('[id*="diagnostic"],[id*="projection"],[id*="physics"]')].map(node=>({id:node.id,tag:node.tagName,text:node.textContent??'',attributes:Object.fromEntries([...node.attributes].filter(attr=>attr.name.startsWith('data-')).map(attr=>[attr.name,attr.value]))})),
    outputs:[...document.querySelectorAll('output')].map(node=>({id:node.id,text:norm(node.textContent),attributes:Object.fromEntries([...node.attributes].map(attr=>[attr.name,attr.value]))})),
    gameplay:document.querySelector('section.authoring-gameplay-controls')?.textContent??null,
    canvas_attributes:canvas?Object.fromEntries([...canvas.attributes].map(attr=>[attr.name,attr.value])):null
  };
})()
'@
    [IO.File]::WriteAllText((Join-Path $captureOutput 'public-readiness.json'), (ConvertTo-CompactJson -Value $capturePublic), [Text.UTF8Encoding]::new($false))
    $captureScreen = Save-CdpScreenshot -Socket $captureRun.Socket -LiteralPath (Join-Path $captureOutput 'public-readiness.png')
}
catch { $captureFailure = $_.Exception.Message }
finally {
    if ($null -ne $captureRun) { $captureStop = Stop-ForgeStudioPlaytestProcess -Run $captureRun }
    foreach ($captureName in $captureSavedEnvironment.Keys) { $captureValue=if($null-eq$captureSavedEnvironment[$captureName]){[NullString]::Value}else{$captureSavedEnvironment[$captureName]}; [Environment]::SetEnvironmentVariable($captureName,$captureValue,'Process') }
}
$captureSourceAfter = @($captureRelativeArchives | ForEach-Object { Get-ArchiveSnapshot -LiteralPath (Join-Path $captureSource $_) })
$captureFixtureAfter = @($captureRelativeArchives | ForEach-Object { Get-ArchiveSnapshot -LiteralPath (Join-Path $captureFixture $_) })
$captureSourcePreserved = $true
$captureFixturePreserved = $true
for ($captureIndex=0;$captureIndex -lt $captureRelativeArchives.Count;$captureIndex++) {
    $captureSourcePreserved = $captureSourcePreserved -and (Test-ArchiveSnapshotsEqual -Before $captureSourceBefore[$captureIndex] -After $captureSourceAfter[$captureIndex])
    $captureFixturePreserved = $captureFixturePreserved -and (Test-ArchiveSnapshotsEqual -Before $captureFixtureBefore[$captureIndex] -After $captureFixtureAfter[$captureIndex])
}
$captureEffectiveFailure = if ($null-ne$captureFailure) { $captureFailure } elseif ($null-ne$captureStateFailure) { $captureStateFailure } elseif (-not$captureSourcePreserved -or -not$captureFixturePreserved) { 'Tracked source or fixture archives changed.' } elseif ($null-eq$captureStop -or -not$captureStop.Closed -or $captureStop.Forced) { 'Owned process did not close cleanly through the public confirmation.' } else { $null }
$captureProof = [ordered]@{
    schema='forge.gate3.public-readiness-capture';version=1;status=if($null-eq$captureFailure -and $null-eq$captureStateFailure -and $null-ne$capturePublic -and $captureSourcePreserved -and $captureFixturePreserved -and $captureStop.Closed -and -not$captureStop.Forced){'captured'}else{'failed_closed'}
    failure=$captureEffectiveFailure;gate_qualified=$false;human_visual_review=$false;candidate=$captureCandidate;candidate_sha256=$captureCandidateHash;source_fixture=$captureSource;fixture=$captureFixture;authored_archive_hash=$captureAuthoredHash;authored_head_hash=[string]$captureReference.head_hash;source_original_hash_pinned=$capturePin;source_preserved=$captureSourcePreserved;fixture_archives_preserved=$captureFixturePreserved
    source_before=$captureSourceBefore;source_after=$captureSourceAfter;fixture_before=$captureFixtureBefore;fixture_after=$captureFixtureAfter;public_status=if($null-ne$capturePublic){$capturePublic.status}else{$null};closure=$captureStop;screenshot=$captureScreen
    diagnostics_json=Join-Path $captureOutput 'public-readiness.json';runtime_injection=$false;direct_tauri_call=$false;public_ui_only=$true
    requested_state=if($AwaitSimulationReady){'simulation_ready_with_public_pointer_gesture'}elseif($AwaitObjectCollisionBlock){'post_terrain_objects_collisions_tick0_or_ready'}else{'fixed_delay_observation'};state_failure=$captureStateFailure
    ready_elapsed_ms=$captureReadyElapsed;ready_elapsed_scope='public click, native transport, reconstruction and poll delay included; no GPU presentation claim'
    warmup_seconds_requested=$WarmupSeconds;sample_seconds_requested=$SampleSeconds;sample_count=$captureSamples.Count;metrics_public_click=$captureMetricsClick
    viewport_target=[ordered]@{width=$TargetCanvasWidth;height=$TargetCanvasHeight};viewport_observed=if($null-ne$capturePublic){$capturePublic.viewport}else{$null}
    target_canvas_dimensions_match=if($null-ne$capturePublic){$capturePublic.viewport.css_width-eq$TargetCanvasWidth -and $capturePublic.viewport.css_height-eq$TargetCanvasHeight -and $capturePublic.viewport.render_width-eq$TargetCanvasWidth -and $capturePublic.viewport.render_height-eq$TargetCanvasHeight}else{$false}
    quality_tier='not_observed_on_authored_tidewater';gpu_frame_time='not_observed';compositor_presentation='not_observed';sampling_cadence_is_frame_rate=$false
    hardware_workload_preflight=$captureEnvironment
    harness=$PSCommandPath;harness_sha256=Get-FileSha256 -LiteralPath $PSCommandPath
}
[IO.File]::WriteAllText((Join-Path $captureOutput 'proof.json'), (ConvertTo-CompactJson -Value $captureProof), [Text.UTF8Encoding]::new($false))
$captureProof | ConvertTo-Json -Depth 8
if ($captureProof.status -ne 'captured') { throw "Readiness capture failed closed: $captureEffectiveFailure" }
