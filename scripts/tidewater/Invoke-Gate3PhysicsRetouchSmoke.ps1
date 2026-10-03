#Requires -Version 7.2
# Gate 3 Tidewater physics retouch smoke.  Every GameplayDocument change in
# this script is made through the public Studio form, Preview/Apply and Save.
# It records an authoring proof only; runtime contact, fire and water remain
# unclaimed until a separate playtest observes them.
[CmdletBinding(DefaultParameterSetName = 'Run')]
param(
    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$Candidate,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$FreshIsolatedFixture,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [Alias('AuthoringLedger', 'PriorLedger', 'LedgerPath')]
    [ValidateNotNullOrEmpty()]
    [string]$PreviousLedger,

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
    [ValidateRange(30, 240)]
    [int]$StartupTimeoutSeconds = 120,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(30, 300)]
    [int]$InteractionTimeoutSeconds = 120,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$retouchRequestedSelfTest = [bool]$SelfTest
$retouchRequestedCandidate = $Candidate
$retouchRequestedFixture = $FreshIsolatedFixture
$retouchRequestedLedger = $PreviousLedger
$retouchRequestedSourcePin = $SourceOriginalHashPinned
$retouchRequestedEvidence = $EvidenceRoot
$retouchRequestedZone = $ZoneUuid
$retouchRequestedBrowser = $BrowserExecutableFolder
$retouchRequestedStartup = $StartupTimeoutSeconds
$retouchRequestedInteraction = $InteractionTimeoutSeconds

# This imports only function definitions and runs no Studio process in its
# SelfTest path.  It also imports the governed CDP/fixture helpers.
. (Join-Path $PSScriptRoot 'Invoke-Gate3PhysicsAuthoringSmoke.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Gate3ProofStorage.ps1')
if (-not $retouchRequestedSelfTest) {
    $Candidate = $retouchRequestedCandidate
    $FreshIsolatedFixture = $retouchRequestedFixture
    $PreviousLedger = $retouchRequestedLedger
    $SourceOriginalHashPinned = $retouchRequestedSourcePin
    $EvidenceRoot = $retouchRequestedEvidence
    $ZoneUuid = $retouchRequestedZone
    $BrowserExecutableFolder = $retouchRequestedBrowser
    $StartupTimeoutSeconds = $retouchRequestedStartup
    $InteractionTimeoutSeconds = $retouchRequestedInteraction
}

$zoneId = $ZoneUuid.ToString('D').ToLowerInvariant()
$editorSummary = 'Personnages, objets et objectifs'
$sourceTerrainBlobHash = 'be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3'
$platformName = 'Plateforme de lancer'
$chargeName = 'Charge de lancer'
$fireName = 'Balise inflammable'
$friendlyName = 'Veilleur du danger'
# The launch pair is retouched back onto the measured x=19 corridor.  The
# public throw direction is object-to-player, so this keeps the guardian at
# x=16 within the reviewed physical reach without changing the engine.
$platformX = 19.0
$platformZ = -270.0
$platformY = 58.87091056823731
$chargeY = 59.16591056823731
$fireX = 18.0
$fireZ = -270.0
$terrainAtFire = 58.05
$fireY = $terrainAtFire + 0.02 + 0.25

function Get-RetouchProperty {
    param([AllowNull()][object]$Object, [Parameter(Mandatory)][string]$Name)
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Test-RetouchPosition {
    param([AllowNull()][object]$Position, [Parameter(Mandatory)][double]$X, [Parameter(Mandatory)][double]$Z, [Parameter(Mandatory)][double]$Y)
    if ($null -eq $Position) { return $false }
    try {
        return [Math]::Abs([double]$Position.x - $X) -lt 0.0002 -and
            [Math]::Abs([double]$Position.y - $Y) -lt 0.0002 -and
            [Math]::Abs([double]$Position.z - $Z) -lt 0.0002
    } catch { return $false }
}

function Test-RetouchExtents {
    param([Parameter(Mandatory)][object]$Physics, [Parameter(Mandatory)][double]$Width, [Parameter(Mandatory)][double]$Height, [Parameter(Mandatory)][double]$Depth)
    $extents = Get-RetouchProperty -Object $Physics -Name 'body_half_extents_m'
    if ($null -eq $extents) { return $false }
    return [Math]::Abs([double]$extents.x - $Width / 2.0) -lt 0.0002 -and
        [Math]::Abs([double]$extents.y - $Height / 2.0) -lt 0.0002 -and
        [Math]::Abs([double]$extents.z - $Depth / 2.0) -lt 0.0002
}

function Get-RetouchEntity {
    param([Parameter(Mandatory)][object]$Gameplay, [Parameter(Mandatory)][string]$Collection, [Parameter(Mandatory)][string]$Name)
    $entries = @((Get-RetouchProperty -Object $Gameplay -Name $Collection))
    return @($entries | Where-Object { [string]$_.name -ceq $Name })
}

function Retouch-Prop {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$X,
        [Parameter(Mandatory)][string]$Y,
        [Parameter(Mandatory)][string]$Z,
        [Parameter(Mandatory)][string]$Mass,
        [Parameter(Mandatory)][string]$Width,
        [Parameter(Mandatory)][string]$Height,
        [Parameter(Mandatory)][string]$Depth,
        [Parameter(Mandatory)][bool]$Pushable,
        [Parameter(Mandatory)][bool]$Throwable,
        [AllowEmptyString()][Parameter(Mandatory)][string]$BreakEnergy,
        [switch]$AllowPositionNoop,
        [switch]$AllowPhysicsNoop,
        [switch]$Fire
    )
    Select-GameplayOptionByText -Socket $Socket -Label 'Élément à retoucher' -Text $Name -TimeoutSeconds $InteractionTimeoutSeconds
    Set-GameplayForm -Socket $Socket -Kind 'prop' -Name $Name -X $X -Z $Z -CenterY $Y -Reply '' -TimeoutSeconds $InteractionTimeoutSeconds
    $positionResult = $null
    try {
        $positionResult = Invoke-GameplayPreviewApply -Socket $Socket -PreviewButton 'Examiner le nom, la position et la réplique' -Description "retouch position $Name" -TimeoutSeconds $InteractionTimeoutSeconds
    } catch {
        if (-not $AllowPositionNoop -or $_.Exception.Message -notmatch 'Aucun changement à prévisualiser') { throw }
        $positionResult = [ordered]@{
            description = "retouch position $Name"
            preview_status = 'noop_already_present'
            apply_status = 'not_requested'
            reason = 'Public editor reported that the requested name/position/reply already matches the saved object.'
        }
    }
    Set-GameplayPhysicsForm -Socket $Socket -Mass $Mass -Width $Width -Height $Height -Depth $Depth -Pushable $Pushable -Throwable $Throwable -BreakEnergy $BreakEnergy
    if ($Fire) {
        Set-PhysicsFireControls -Socket $Socket -HeatDamagePerTick 1
    }
    $physicsResult = $null
    try {
        $physicsResult = Invoke-GameplayPreviewApply -Socket $Socket -PreviewButton 'Examiner les propriétés physiques' -Description "retouch physics $Name" -TimeoutSeconds $InteractionTimeoutSeconds
    } catch {
        if (-not $AllowPhysicsNoop -or $_.Exception.Message -notmatch 'Aucun changement à prévisualiser') { throw }
        $physicsResult = [ordered]@{ description="retouch physics $Name"; preview_status='noop_already_present'; apply_status='not_requested'; reason='Public editor reported that the requested physical values already match the saved object.' }
    }
    return [ordered]@{ action='public_retouch_prop'; name=$Name; x=[double]$X; y=[double]$Y; z=[double]$Z; mass_kg=[double]$Mass; width_m=[double]$Width; height_m=[double]$Height; depth_m=[double]$Depth; pushable=$Pushable; throwable=$Throwable; break_energy_j=if([string]::IsNullOrWhiteSpace($BreakEnergy)){$null}else{[double]$BreakEnergy}; fire=$Fire; position_result=$positionResult; physics_result=$physicsResult; authoring_claim='public form Preview/Apply only; no runtime claim' }
}

function Retouch-Friendly {
    param([Parameter(Mandatory)]$Socket)
    Select-GameplayOptionByText -Socket $Socket -Label 'Élément à retoucher' -Text $friendlyName -TimeoutSeconds $InteractionTimeoutSeconds
    Set-GameplayForm -Socket $Socket -Kind 'friendly' -Name $friendlyName -X '20' -Z '-270' -CenterY '' -Reply 'Je surveille le danger.' -TimeoutSeconds $InteractionTimeoutSeconds
    try {
        [void](Invoke-GameplayPreviewApply -Socket $Socket -PreviewButton 'Examiner le nom, la position et la réplique' -Description 'retouch danger friendly position' -TimeoutSeconds $InteractionTimeoutSeconds)
    } catch {
        if ($_.Exception.Message -notmatch 'Aucun changement à prévisualiser') { throw }
    }
    return [ordered]@{ action='public_retouch_actor'; name=$friendlyName; x=20; z=-270; y='canonical'; flees_danger='preserved_public_field'; authoring_claim='public form Preview/Apply only; no runtime claim' }
}

function Close-RetouchStudio {
    param([AllowNull()]$Process, [AllowNull()]$Socket, [int]$TimeoutSeconds)
    $nativeConfirmed = $false
    if ($null -ne $Process -and -not $Process.HasExited -and $null -ne $Socket -and $Socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
        try {
            $Process.Refresh()
            if (-not $Process.CloseMainWindow()) { throw 'Owned Studio window did not accept the public close request.' }
            [void](Wait-CdpReady -Socket $Socket -TimeoutSeconds ([Math]::Min(15, $TimeoutSeconds)) -Description 'native Studio close dialog' -Expression @'
(() => ({ready:!!document.querySelector('dialog.gold-close-dialog[open]'),terminal:false,reason:'close dialog pending'}))()
'@)
            [void](Invoke-UiClick -Socket $Socket -Text 'Quitter sans enregistrer' -ScopeSelector 'dialog.gold-close-dialog[open]')
            $nativeConfirmed = $Process.WaitForExit(10000)
        } catch { $nativeConfirmed = $false }
    }
    return $nativeConfirmed
}

if ($retouchRequestedSelfTest) {
    $raw = [IO.File]::ReadAllText($PSCommandPath)
    foreach ($needle in @(
        'WEBVIEW2_BROWSER_EXECUTABLE_FOLDER', 'Target.activateTarget', 'Preview/Apply',
        'Examiner le nom, la position et la réplique', 'Examiner les propriétés physiques',
        'Charge de lancer', 'Balise inflammable', 'Veilleur du danger',
        'Inflammable', 'Dégâts de chaleur par tick (0 = aucun dégât)',
        'gate_qualified', 'public_retouch_prop', 'source_preserved', 'failed_closed'
    )) {
        if ($raw.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "Retouch smoke self-test missing: $needle" }
    }
    $forbiddenTokens = @(
        ('GameplayDocument' + ' {'),
        ('head.corrections.gameplay' + ' ='),
        ('Invoke-' + 'Tauri'),
        ('Studio' + 'Command')
    )
    foreach ($forbidden in $forbiddenTokens) {
        if ($raw.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) { throw "Retouch smoke contains forbidden direct mutation token: $forbidden" }
    }
    Write-Output 'TIDEWATER_GATE3_PHYSICS_RETOUCH_SMOKE_SELF_TEST_PASS'
    return
}

if (-not (Test-IsWindowsPlatform)) { throw 'Windows/WebView2 is required.' }
$candidate = [IO.Path]::GetFullPath($Candidate)
$source = [IO.Path]::GetFullPath($FreshIsolatedFixture)
$ledgerInput = [IO.Path]::GetFullPath($PreviousLedger)
$output = [IO.Path]::GetFullPath($EvidenceRoot)
$sourcePin = Get-NormalizedSha256 -Value $SourceOriginalHashPinned
foreach ($entry in @(
        @($candidate, 'Candidate'), @($source, 'Base authoring fixture'),
        @($ledgerInput, 'Previous authoring ledger'), @($output, 'Evidence root')
    )) { Assert-Gate3ProofPath -LiteralPath $entry[0] -Description $entry[1] }
if (Test-Path -LiteralPath $output) { throw "EvidenceRoot must be fresh: $output" }
if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Candidate absent: $candidate" }
if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "Base fixture absent: $source" }
if (-not (Test-Path -LiteralPath $ledgerInput -PathType Leaf)) { throw "Previous ledger absent: $ledgerInput" }
Assert-TreeHasNoReparsePoints -LiteralPath $source -Description 'Base Tidewater physics fixture'

$browserRoot = [IO.Path]::GetFullPath($BrowserExecutableFolder)
$browserExe = Join-Path $browserRoot 'msedgewebview2.exe'
if (-not (Test-Path -LiteralPath $browserExe -PathType Leaf)) { throw "WebView2 executable absent: $browserExe" }
$browserVersion = [string](Get-Item -LiteralPath $browserExe -Force).VersionInfo.FileVersion
if ($browserVersion -cne '154.0.4258.37') { throw "WebView2 runtime mismatch: observed=$browserVersion expected=154.0.4258.37" }

$candidateHash = Get-NormalizedSha256 -Value (Get-FileSha256 -LiteralPath $candidate)
$previous = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($ledgerInput))
if ([string]$previous.schema -cne 'forge.tidewater.gate3-physics-authoring-smoke' -or [string]$previous.status -cne 'passed') { throw 'Previous physics authoring ledger is not passed.' }
if ([bool]$previous.gate_qualified -or -not [bool]$previous.source_original_archive_preserved) { throw 'Previous ledger is not an authoring-only preserved-source proof.' }
if ((Get-NormalizedSha256 -Value ([string]$previous.source_original_hash_pinned)) -cne $sourcePin) { throw 'Source pin differs from previous physics ledger.' }
$previousReference = $previous.saved_reference
$expectedArchive = Get-NormalizedSha256 -Value ([string]$previousReference.archive_hash)
$expectedHead = Get-NormalizedSha256 -Value ([string]$previousReference.head_hash)
$sourceWorkspace = Join-Path $source 'workspace-v1\authoring.json'
$sourceZone = Join-Path (Join-Path $source $zoneId) 'authoring.json'
$sourceOriginal = Join-Path (Join-Path $source 'workspace-v1\objects') "$sourcePin.json"
foreach ($path in @($sourceWorkspace, $sourceZone, $sourceOriginal)) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Source archive absent: $path" } }
$sourceReference = Get-Gate3WorkspaceReference -Archive $sourceWorkspace -ZoneId $zoneId
if ((Get-NormalizedSha256 -Value ([string]$sourceReference.archive_hash)) -cne $expectedArchive -or
    (Get-NormalizedSha256 -Value ([string]$sourceReference.head_hash)) -cne $expectedHead) { throw 'Base fixture checkpoint differs from previous ledger.' }
$sourceCurrent = Join-Path (Join-Path $source 'workspace-v1\objects') "$expectedArchive.json"
if (-not (Test-Path -LiteralPath $sourceCurrent -PathType Leaf)) { throw "Current object absent: $sourceCurrent" }
$sourceOriginalBefore = Get-ArchiveSnapshot -LiteralPath $sourceOriginal
$sourceZoneBefore = Get-ArchiveSnapshot -LiteralPath $sourceZone
$sourceCurrentBefore = Get-ArchiveSnapshot -LiteralPath $sourceCurrent
$sourceReport = Get-Gate3GameplayReport -Archive $sourceCurrent
foreach ($name in @($platformName, $chargeName, $fireName, $friendlyName)) {
    if (@($sourceReport.names.Keys | Where-Object { [string]$_ -ceq $name }).Count -ne 1) { throw "Existing authored entity absent or ambiguous: $name" }
}

[void](New-Item -ItemType Directory -Path $output)
$fixture = Join-Path $output 'physics-retouch-root'
$cloneArchive = Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $zoneId
$cloneWorkspace = Join-Path $fixture 'workspace-v1\authoring.json'
$cloneOriginal = Join-Path (Join-Path $fixture 'workspace-v1\objects') "$sourcePin.json"
$cloneCurrent = Join-Path (Join-Path $fixture 'workspace-v1\objects') "$expectedArchive.json"
$cloneZoneBefore = Get-ArchiveSnapshot -LiteralPath $cloneArchive
$cloneWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $cloneWorkspace
$cloneOriginalBefore = Get-ArchiveSnapshot -LiteralPath $cloneOriginal
$cloneCurrentBefore = Get-ArchiveSnapshot -LiteralPath $cloneCurrent

$profile = Join-Path $output 'webview2-user-data'
[void](New-Item -ItemType Directory -Path $profile)
$savedEnvironment = @{}
foreach ($name in @('FORGE_AUTHORING_ROOT', 'FORGE_AUTHOR_SUBJECT', 'WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', 'WEBVIEW2_USER_DATA_FOLDER', 'WEBVIEW2_BROWSER_EXECUTABLE_FOLDER')) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$process = $null
$socket = $null
$reservation = $null
$savedWorkspace = $null
$savedReference = $null
$savedObject = $null
$savedObjectPath = $null
$failure = $null
$steps = [System.Collections.Generic.List[object]]::new()
$uiClosed = $false
$nativeCloseConfirmed = $false
$processClosed = $false
$fireControlsObservation = $null
$sourcePreserved = $false

try {
    $reservation = New-LoopbackPortReservation
    [Environment]::SetEnvironmentVariable('FORGE_AUTHORING_ROOT', $fixture, 'Process')
    [Environment]::SetEnvironmentVariable('FORGE_AUTHOR_SUBJECT', 'tidewater-gate3-physics-retouch-smoke', 'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', "--remote-debugging-address=127.0.0.1 --remote-debugging-port=$($reservation.Port)", 'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER', $profile, 'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_BROWSER_EXECUTABLE_FOLDER', $browserRoot, 'Process')
    $reservation.Listener.Stop()
    $process = Start-Process -FilePath $candidate -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $output 'candidate.stdout.log') -RedirectStandardError (Join-Path $output 'candidate.stderr.log')
    [void](Wait-LaunchedExecutableIdentity -Process $process -Candidate $candidate -ExpectedHash $candidateHash -TimeoutSeconds ([Math]::Min(15, $StartupTimeoutSeconds)))
    $target = Wait-CdpTarget -Port $reservation.Port -TimeoutSeconds $StartupTimeoutSeconds -Process $process
    $socket = Connect-CdpSocket -WebSocketUrl ([string]$target.webSocketDebuggerUrl)
    [void](Invoke-CdpCommand -Socket $socket -Method 'Target.activateTarget' -Parameters @{ targetId = [string]$target.id })
    [void](Invoke-CdpCommand -Socket $socket -Method 'Runtime.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.bringToFront')
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $StartupTimeoutSeconds -Description 'Studio shell' -Expression @'
(() => ({ready:document.readyState==='complete'&&!!document.querySelector('.authoring-shell'),terminal:false,reason:'shell pending'}))()
'@)
    Open-Gate3TidewaterZone -Socket $socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Invoke-UiClick -Socket $socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Wait-GameplayEditor -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Open-DetailsBySummary -Socket $socket -Summary $editorSummary)
    [void](Wait-PhysicsGameplayIdle -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'physics retouch editor idle')

    # The terrain values are derived from the pinned raster and supplied as
    # explicit centers through the public form.  The smaller slab remains a
    # logical support while leaving the cube side reachable for Rapier contact.
    $steps.Add((Retouch-Prop -Socket $socket -Name $platformName -X ([string]$platformX) -Y $platformY.ToString('R',[Globalization.CultureInfo]::InvariantCulture) -Z ([string]$platformZ) -Mass '0' -Width '0.05' -Height '0.05' -Depth '0.05' -Pushable $false -Throwable $false -BreakEnergy '' -AllowPositionNoop))
    $steps.Add((Retouch-Prop -Socket $socket -Name $chargeName -X ([string]$platformX) -Y $chargeY.ToString('R',[Globalization.CultureInfo]::InvariantCulture) -Z ([string]$platformZ) -Mass '30' -Width '0.5' -Height '0.5' -Depth '0.5' -Pushable $false -Throwable $true -BreakEnergy '' -AllowPositionNoop -AllowPhysicsNoop))
    $steps.Add((Retouch-Prop -Socket $socket -Name $fireName -X ([string]$fireX) -Y $fireY.ToString('R',[Globalization.CultureInfo]::InvariantCulture) -Z ([string]$fireZ) -Mass '0' -Width '0.5' -Height '0.5' -Depth '0.5' -Pushable $false -Throwable $false -BreakEnergy '' -AllowPositionNoop -AllowPhysicsNoop -Fire))
    $steps.Add((Retouch-Friendly -Socket $socket))

    [void](Wait-PhysicsGameplayIdle -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'physics retouch before save')
    $beforeSaveWorkspace = Get-ArchiveSnapshot -LiteralPath (Join-Path $fixture 'workspace-v1\authoring.json')
    [void](Invoke-UiClick -Socket $socket -Text 'Sauvegarder')
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'physics retouch save' -Expression @'
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim();
  const status=norm(document.querySelector('.authoring-command-status')?.textContent);
  const save=norm(document.querySelector('.authoring-command-bar .gold-save-state')?.textContent);
  const context=norm(document.querySelector('.authoring-context')?.textContent);
  const busy=document.querySelector('.authoring-toolbar')?.getAttribute('aria-busy');
  const terminal=/refusée|erreur|invalide|impossible/i.test(status);
  return {ready:status==='Session synchronisée'&&save===''&&context!==''&&context!=='Aucune zone ouverte'&&busy==='false'&&!terminal,terminal,reason:'status='+status+'; save='+save+'; context='+context+'; busy='+busy};
})()
'@)
    $savedWorkspace = Get-ArchiveSnapshot -LiteralPath (Join-Path $fixture 'workspace-v1\authoring.json')
    if (Test-ArchiveSnapshotsEqual -Before $beforeSaveWorkspace -After $savedWorkspace) { throw 'Studio Save did not update the managed workspace.' }
    $savedReference = Get-Gate3WorkspaceReference -Archive (Join-Path $fixture 'workspace-v1\authoring.json') -ZoneId $zoneId
    $savedArchiveHash = Get-NormalizedSha256 -Value ([string]$savedReference.archive_hash)
    if ($savedArchiveHash -ceq $expectedArchive) { throw 'Studio Save did not create a new retouched object archive.' }
    $savedObjectPath = Join-Path (Join-Path $fixture 'workspace-v1\objects') "$savedArchiveHash.json"
    if (-not (Test-Path -LiteralPath $savedObjectPath -PathType Leaf)) { throw "Retouched object absent: $savedObjectPath" }
    $savedObject = Get-ArchiveSnapshot -LiteralPath $savedObjectPath
    if ($savedObject.Sha256 -cne $savedArchiveHash) { throw 'Retouched object hash does not match workspace reference.' }
    $savedDocument = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($savedObjectPath))
    $gameplay = $savedDocument.head.corrections.gameplay
    $platform = @(Get-RetouchEntity -Gameplay $gameplay -Collection 'objects' -Name $platformName)
    $charge = @(Get-RetouchEntity -Gameplay $gameplay -Collection 'objects' -Name $chargeName)
    $fire = @(Get-RetouchEntity -Gameplay $gameplay -Collection 'objects' -Name $fireName)
    $friendly = @(Get-RetouchEntity -Gameplay $gameplay -Collection 'actors' -Name $friendlyName)
    if ($platform.Count -ne 1 -or $charge.Count -ne 1 -or $fire.Count -ne 1 -or $friendly.Count -ne 1) { throw 'Retouched names are not unique in the saved GameplayDocument.' }
    $platformPhysics = $platform[0].physics; $chargePhysics = $charge[0].physics; $firePhysics = $fire[0].physics
    if (-not (Test-RetouchPosition -Position $platform[0].position -X $platformX -Y $platformY -Z $platformZ) -or
        -not (Test-RetouchExtents -Physics $platformPhysics -Width 0.05 -Height 0.05 -Depth 0.05) -or
        [double]$platformPhysics.mass_kg -ne 0 -or [bool]$platformPhysics.pushable -or [bool]$platformPhysics.throwable -or
        [string]$charge[0].id -notin @($platformPhysics.supports)) { throw 'Saved public platform retouch differs from the reviewed support recipe.' }
    if (-not (Test-RetouchPosition -Position $charge[0].position -X $platformX -Y $chargeY -Z $platformZ) -or
        -not (Test-RetouchExtents -Physics $chargePhysics -Width 0.5 -Height 0.5 -Depth 0.5) -or
        [double]$chargePhysics.mass_kg -ne 30 -or [bool]$chargePhysics.pushable -or -not [bool]$chargePhysics.throwable) { throw 'Saved public charge retouch differs from the reviewed throwable recipe.' }
    if (-not (Test-RetouchPosition -Position $fire[0].position -X $fireX -Y $fireY -Z $fireZ) -or
        -not (Test-RetouchExtents -Physics $firePhysics -Width 0.5 -Height 0.5 -Depth 0.5) -or
        -not [bool]$firePhysics.flammable -or [double]$firePhysics.heat_damage_per_tick -ne 1) { throw 'Saved fire retouch did not persist the public true/1 controls.' }
    if (-not (Test-RetouchPosition -Position $friendly[0].position -X 20 -Y ([double]$friendly[0].position.y) -Z -270) -or -not [bool]$friendly[0].flees_danger) { throw 'Saved danger friendly retouch did not preserve canonical position/flee control.' }
    $fireControlsObservation = $script:fireControlsObservation

    $sourcePreserved = (Test-ArchiveSnapshotsEqual -Before $cloneZoneBefore -After (Get-ArchiveSnapshot -LiteralPath $cloneArchive)) -and
        (Test-ArchiveSnapshotsEqual -Before $cloneOriginalBefore -After (Get-ArchiveSnapshot -LiteralPath $cloneOriginal)) -and
        (Test-ArchiveSnapshotsEqual -Before $cloneCurrentBefore -After (Get-ArchiveSnapshot -LiteralPath $cloneCurrent))
    if (-not $sourcePreserved) { throw 'Public retouch altered the source/original/current legacy archives.' }
    [void](Invoke-UiClick -Socket $socket -Text 'Projet' -Selector '[data-authoring-tab-button]')
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Créer ou modifier la zone')
    [void](Invoke-UiClick -Socket $socket -Text 'Fermer la zone')
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'close retouched Tidewater zone' -Expression @'
(() => { const norm=v=>(v??'').replace(/\s+/g,' ').trim(); const c=norm(document.querySelector('.authoring-context')?.textContent); return {ready:c==='Aucune zone ouverte'&&document.documentElement?.getAttribute('data-forge-zone-open')==='false',terminal:false,reason:'context='+c}; })()
'@)
    $uiClosed = $true
}
catch {
    $failure = $_.Exception.Message
    if ($null -ne $socket -and $socket.State -eq [Net.WebSockets.WebSocketState]::Open) { try { Save-GameplayUiDiagnostic -Socket $socket -OutputRoot $output -Name 'failure' } catch {} }
}
finally {
    $nativeCloseConfirmed = Close-RetouchStudio -Process $process -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds
    if ($null -ne $process) { try { $process.Refresh(); $processClosed = $process.HasExited } catch { $processClosed = $false } }
    if ($null -ne $socket) { try { $socket.Dispose() } catch {} }
    if ($null -ne $reservation) { try { $reservation.Listener.Stop(); $reservation.Listener.Dispose() } catch {} }
    foreach ($name in $savedEnvironment.Keys) {
        $value = if ($null -eq $savedEnvironment[$name]) { [NullString]::Value } else { $savedEnvironment[$name] }
        [Environment]::SetEnvironmentVariable($name, $value, 'Process')
    }
}

$status = if ([string]::IsNullOrWhiteSpace($failure)) { 'passed' } else { 'failed_closed' }
$ledger = [ordered]@{
    schema='forge.tidewater.gate3-physics-retouch-smoke'; version=1; status=$status; failure=$failure
    gate_qualified=$false; human_visual_review=$false; evidence_kind='public_studio_authoring_only'
    runtime_contact_claim=$false; runtime_fire_claim=$false; runtime_water_claim=$false; navigation_claim=$false
    candidate=$candidate; candidate_sha256=$candidateHash; browser_executable_folder=$browserRoot; browser_runtime_version=$browserVersion
    previous_authoring_ledger=$ledgerInput; source_fixture=$source; source_original_hash_pinned=$sourcePin
    parent_checkpoint=[ordered]@{archive_hash=$expectedArchive; head_hash=$expectedHead}
    isolated_fixture=$fixture; saved_workspace=$savedWorkspace; saved_reference=$savedReference; saved_object=$savedObject; saved_object_path=$savedObjectPath
    source_preserved=$sourcePreserved; fire_controls_ui=$fireControlsObservation; steps=@($steps)
    retouch=[ordered]@{
        terrain_blob=$sourceTerrainBlobHash; platform=[ordered]@{x=$platformX;y=$platformY;z=$platformZ;width_m=0.05;height_m=0.05;depth_m=0.05}
        charge=[ordered]@{x=$platformX;y=$chargeY;z=$platformZ;width_m=0.5;height_m=0.5;depth_m=0.5;mass_kg=30;throwable=$true;support_relation='preserved'}
        fire=[ordered]@{x=$fireX;y=$fireY;z=$fireZ;flammable=$true;heat_damage_per_tick=1}
        friendly=[ordered]@{x=20;z=-270;flees_danger=$true}
        y_policy='explicit centers derived from pinned raster samples plus half-extents and 0.02 m clearance; friendly Y sampled by public editor'
    }
    ui_closed=$uiClosed; owned_process_closed=$processClosed; native_close_confirmed=$nativeCloseConfirmed
    limitations=@('This ledger proves only public Studio retouch, Preview/Apply, Save and source preservation.', 'The saved fields do not prove Rapier contact, throw, impact, support rupture, fire ignition, NPC fleeing or water contact.', 'No direct GameplayDocument or JSON mutation was used.')
}
$ledgerPath = Join-Path $output 'gate3-physics-retouch-smoke-ledger.json'
[IO.File]::WriteAllText($ledgerPath, ($ledger | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
if ($status -cne 'passed') { throw "Gate 3 physics retouch smoke failed closed: $failure. Ledger: $ledgerPath" }
Write-Output "TIDEWATER_GATE3_PHYSICS_RETOUCH_SMOKE_PASS ledger=$ledgerPath"
