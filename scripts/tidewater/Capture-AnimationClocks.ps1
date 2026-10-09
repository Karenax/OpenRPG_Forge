#Requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CaptureCandidate,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$CaptureCandidateSha256,
    [Parameter(Mandatory)][string]$CaptureFixture,
    [Parameter(Mandatory)][string]$CaptureOutput,
    [Parameter(Mandatory)][string]$CaptureInventory,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$CaptureInventorySha256
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Invoke-FishStandStudioSmoke.ps1') -SelfTest | Out-Null
$PSDefaultParameterValues['Invoke-CdpExpression:TimeoutSeconds'] = 120
$PSDefaultParameterValues['Invoke-CdpCommand:TimeoutSeconds'] = 120
function Get-AnimationWorldState {
    param([Parameter(Mandatory)]$Socket)
    $animationState = Get-WorldState -Socket $Socket
    $animationDiagnostic = Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {const raw=document.getElementById('forge-authoring-canvas')?.getAttribute('data-tidewater-live-shadows');
if(!raw)throw Error('Actual animation diagnostic absent');const diagnostic=JSON.parse(raw);
if(!diagnostic.wind_pose||!diagnostic.scene_wind||!diagnostic.cloud_clock||typeof diagnostic.virtual_time_s!=='number')throw Error('Actual animation clock fields absent');
return {raw,diagnostic};})()
'@
    $animationState.diagnostic_raw = $animationDiagnostic.raw
    $animationState.diagnostic = $animationDiagnostic.diagnostic
    $animationState | Add-Member -NotePropertyName diagnostic_attribute -NotePropertyValue 'data-tidewater-live-shadows'
    return $animationState
}
$captureZone = 'e620dd19-ead9-5913-a6d6-0f93109148c0'
$captureExe = [IO.Path]::GetFullPath($CaptureCandidate)
$captureSource = [IO.Path]::GetFullPath($CaptureFixture)
$captureOut = [IO.Path]::GetFullPath($CaptureOutput)
if ($captureOut -notmatch '^E:\\OpenRPG_Forge\\\.(placeables-smoke|tidewater-integration)[^\\]*\\.+') { throw 'Dedicated workspace evidence output required.' }
if (Test-Path -LiteralPath $captureOut) { throw 'Fresh capture output required.' }
if ((Test-PathWithinDirectory -LiteralPath $captureOut -Directory $captureSource) -or
    (Test-PathWithinDirectory -LiteralPath $captureSource -Directory $captureOut)) { throw 'Source and output must be disjoint.' }
if ((Get-FileSha256 -LiteralPath $captureExe) -cne $CaptureCandidateSha256) { throw 'Candidate pin mismatch.' }
if ((Get-FileSha256 -LiteralPath $CaptureInventory) -cne $CaptureInventorySha256) { throw 'Independent inventory pin mismatch.' }
$captureExpected = Get-Content -LiteralPath $CaptureInventory -Raw | ConvertFrom-Json
if ($captureExpected.schema -cne 'forge.tidewater.stand-smoke-inventory' -or $captureExpected.version -ne 1 -or
    $captureExpected.area_id -cne $captureZone -or $captureExpected.source_commit -cne '1438b1abfcaee3267092b75573014f4d9b4a983c') { throw 'Wrong inventory source.' }
$captureArchive = Join-Path $captureSource "$captureZone/authoring.json"
if ((Get-FileSha256 -LiteralPath $captureArchive) -cne $captureExpected.fixture_sha256) { throw 'Fixture pin mismatch.' }
Assert-TreeHasNoReparsePoints -LiteralPath $captureSource -Description 'Capture source'
$captureSourcePins = @(Get-ChildItem -LiteralPath $captureSource -Recurse -Force -File | ForEach-Object {
    [pscustomobject]@{path=$_.FullName;sha256=(Get-FileSha256 -LiteralPath $_.FullName)}
})
$captureWeb = Join-Path (Split-Path -Parent $captureExe) 'web'
Assert-TreeHasNoReparsePoints -LiteralPath $captureWeb -Description 'Staged frontend'
if ((Get-FileSha256 -LiteralPath (Join-Path $captureWeb 'forge-imports/buildings.json')) -cne $captureExpected.building_index_sha256) { throw 'Library index pin mismatch.' }
foreach ($pin in $captureExpected.resource_pins.PSObject.Properties) {
    $path = [IO.Path]::GetFullPath((Join-Path $captureWeb $pin.Name))
    if (-not (Test-PathWithinDirectory -LiteralPath $path -Directory $captureWeb) -or
        (Get-FileSha256 -LiteralPath $path) -cne [string]$pin.Value) { throw 'Imported resource pin mismatch.' }
}
[void](New-Item -ItemType Directory -Path $captureOut)
$captureClone = Join-Path $captureOut 'authoring-root'
[void](Copy-IsolatedFixture -SourceRoot $captureSource -DestinationRoot $captureClone -ZoneId $captureZone)
$captureClonePins = @(Get-ChildItem -LiteralPath $captureClone -Recurse -Force -File | ForEach-Object {
    [pscustomobject]@{path=$_.FullName;sha256=(Get-FileSha256 -LiteralPath $_.FullName)}
})
$captureEnv = @{}
foreach ($name in @('FORGE_AUTHORING_ROOT','WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS','WEBVIEW2_USER_DATA_FOLDER')) { $captureEnv[$name]=[Environment]::GetEnvironmentVariable($name,'Process') }
$captureProcess=$null; $captureSocket=$null; $captureReservation=$null
$captureFailure=$null; $captureBefore=$null; $captureHeld=$null; $captureLater=$null; $captureImage=$null; $captureListenerClosed=$false
try {
    $captureReservation=New-LoopbackPortReservation
    $capturePort=$captureReservation.Port
    [Environment]::SetEnvironmentVariable('FORGE_AUTHORING_ROOT',$captureClone,'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER',(Join-Path $captureOut 'webview2-user-data'),'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS',"--remote-debugging-address=127.0.0.1 --remote-debugging-port=$capturePort",'Process')
    $captureReservation.Listener.Stop()
    $captureProcess=Start-Process -FilePath $captureExe -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $captureOut 'candidate.stdout') -RedirectStandardError (Join-Path $captureOut 'candidate.stderr')
    [void](Wait-LaunchedExecutableIdentity -Process $captureProcess -Candidate $captureExe -ExpectedHash $CaptureCandidateSha256)
    $captureTarget=Wait-CdpTarget -Port $capturePort -TimeoutSeconds 60 -Process $captureProcess
    $captureSocket=Connect-CdpSocket -WebSocketUrl $captureTarget.webSocketDebuggerUrl
    [void](Invoke-CdpCommand -Socket $captureSocket -Method 'Page.enable')
    [void](Invoke-CdpCommand -Socket $captureSocket -Method 'Runtime.enable')
    [void](Invoke-CdpCommand -Socket $captureSocket -Method 'Page.bringToFront')
    [void](Wait-CdpReady -Socket $captureSocket -Description 'Authoring shell' -TimeoutSeconds 120 -Expression "(() => ({ready:document.readyState==='complete'&&!!document.querySelector('.authoring-shell')}))()")
    [void](Wait-StandLibrary -Socket $captureSocket -TimeoutSeconds 120)
    [void](Install-WorldAudit -Socket $captureSocket)
    Open-ManagedTidewaterZone -Socket $captureSocket -ZoneId $captureZone -TimeoutSeconds 120 -OpenWorkspace
    [void](Invoke-UiClick -Socket $captureSocket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Invoke-UiClick -Socket $captureSocket -Text 'Placer / manipuler')
    [void](Open-DetailsBySummary -Socket $captureSocket -Summary 'Sélection et chargement')
    [void](Wait-WorldState -Socket $captureSocket -Description 'Exact unchanged source inventory' -TimeoutSeconds 120 -Predicate {param($state) Test-StandObjectInventory -State $state -Expected @($captureExpected.entries)})
    [void](Invoke-CdpExpression -Socket $captureSocket -Expression "(() => {const url=new URL(location.href);url.searchParams.set('forge_tidewater_reference_village','1');history.replaceState(history.state,'',url.href);return true;})()")
    [void](Invoke-UiClick -Socket $captureSocket -Text 'Jouer')
    [void](Install-WorldAuditAfterOwnedReload -Socket $captureSocket -TimeoutSeconds 120)
    [void](Wait-CdpReady -Socket $captureSocket -Description 'Ready pinned camera and actual clock diagnostics' -TimeoutSeconds 120 -Expression @'
(() => {const canvas=document.getElementById('forge-authoring-canvas');const raw=canvas?.getAttribute('data-tidewater-live-shadows');const d=raw?JSON.parse(raw):null;const status=document.getElementById('authoring-playtest-status')?.textContent??'';
return {ready:canvas?.getAttribute('data-tidewater-reference-village')==='stable'&&status.includes('simulation prête true')&&d?.encoded_frames>=10&&!d.pending&&!!d.wind_pose&&!!d.scene_wind&&!!d.cloud_clock,reason:status};})()
'@)
    $captureBefore=Get-AnimationWorldState -Socket $captureSocket
    [void](Invoke-CdpExpression -Socket $captureSocket -Expression @'
(() => {const input=document.getElementById('authoring-playtest-hold');if(!input||input.disabled)throw Error('Public comparison hold unavailable');input.checked=true;input.dispatchEvent(new Event('change',{bubbles:true}));return input.checked;})()
'@)
    [void](Wait-CdpReady -Socket $captureSocket -Description 'Comparison hold and drained ocean transaction' -TimeoutSeconds 120 -Expression @'
(() => {const raw=document.getElementById('forge-authoring-canvas')?.getAttribute('data-tidewater-live-shadows');const d=raw?JSON.parse(raw):null;return {ready:d?.comparison_hold===true&&!d.pending&&d.ocean_clock?.paused===true&&d.ocean_clock?.quiescent===true,reason:raw};})()
'@)
    $captureHeld=Get-AnimationWorldState -Socket $captureSocket
    Start-Sleep -Seconds 2
    $captureLater=Get-AnimationWorldState -Socket $captureSocket
    [void](Invoke-CdpCommand -Socket $captureSocket -Method 'Emulation.setDeviceMetricsOverride' -Parameters @{width=1920;height=1080;deviceScaleFactor=1;mobile=$false})
    [void](Invoke-CdpExpression -Socket $captureSocket -Expression @'
(() => {const style=document.createElement('style');style.textContent='.authoring-canvas-wrap{position:fixed!important;inset:0!important;width:100vw!important;height:100vh!important;z-index:2147483647!important;overflow:hidden!important}.authoring-canvas-wrap>:not(#forge-authoring-canvas){display:none!important}#forge-authoring-canvas{width:100%!important;height:100%!important}';document.head.append(style);return true;})()
'@)
    Start-Sleep -Seconds 2
    $captureLater=Get-AnimationWorldState -Socket $captureSocket
    $captureImage=Save-CdpScreenshot -Socket $captureSocket -LiteralPath (Join-Path $captureOut 'village-held.png')
    foreach ($kind in @('preview_zone','apply_zone','save')) { if ([int]$captureLater.audit_counts.$kind -ne 0) { throw 'Unexpected authoring mutation during read-only capture.' } }
} catch {
    $captureFailure=$_.Exception.Message
    [IO.File]::WriteAllText((Join-Path $captureOut 'failure-detail.txt'),(($_|Out-String)+$_.ScriptStackTrace),[Text.UTF8Encoding]::new($false))
} finally {
    if ($null -ne $captureSocket) { $captureSocket.Dispose() }
    if ($null -ne $captureProcess -and -not $captureProcess.HasExited) {
        [void](Wait-LaunchedExecutableIdentity -Process $captureProcess -Candidate $captureExe -ExpectedHash $CaptureCandidateSha256)
        [void]$captureProcess.CloseMainWindow()
        if (-not $captureProcess.WaitForExit(5000)) { Stop-Process -Id $captureProcess.Id -Force; [void]$captureProcess.WaitForExit(5000) }
    }
    if ($null -ne $captureReservation) { $captureReservation.Listener.Stop(); $captureListenerClosed=Wait-CdpListenerClosed -Port $captureReservation.Port -TimeoutSeconds 10 }
    foreach ($name in $captureEnv.Keys) { [Environment]::SetEnvironmentVariable($name,$captureEnv[$name],'Process') }
}
foreach ($pin in @($captureSourcePins)+@($captureClonePins)) {
    if ((Get-FileSha256 -LiteralPath $pin.path) -cne $pin.sha256) { $captureFailure='Original or copied source changed.' }
}
if (-not $captureListenerClosed) { $captureFailure='Owned listener not closed.' }
if ((Get-FileSha256 -LiteralPath $captureExe) -cne $CaptureCandidateSha256) { $captureFailure='Candidate changed.' }
$captureReceipt=[ordered]@{schema='forge.tidewater.animation-clock-observation';version=1;status=if($null -eq $captureFailure){'observed'}else{'failed_closed'};failure=$captureFailure;candidate_sha256=$CaptureCandidateSha256;inventory_sha256=$CaptureInventorySha256;before=$captureBefore;held=$captureHeld;later=$captureLater;screenshot=$captureImage;source_pins=$captureSourcePins;clone_pins=$captureClonePins;listener_closed=$captureListenerClosed;gpu_completion_verified=$false;visual_parity=$false;temporal_parity_qualified=$false}
[IO.File]::WriteAllText((Join-Path $captureOut 'animation-clock-observation.json'),($captureReceipt|ConvertTo-Json -Depth 100),[Text.UTF8Encoding]::new($false))
if ($null -ne $captureFailure) { throw $captureFailure }
Write-Output 'ANIMATION_CLOCK_OBSERVATION_COMPLETE - no temporal parity qualification'
