# Gate 3 P01 room/door/place retouch smoke for Tidewater.
# The copy is edited only through the public Studio controls.  This script
# creates a fresh authored checkpoint before invoking the existing public
# gameplay loop, because moving a Place changes the functional save hash.
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
    [Alias('PlaytestLedger', 'PriorLedger', 'LedgerPath')]
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
    [ValidateRange(30, 240)]
    [int]$StartupTimeoutSeconds = 120,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(30, 300)]
    [int]$InteractionTimeoutSeconds = 150,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(0.0001, 100.0)]
    [double]$MeasuredFloorY = 6.6496172428,

    [Parameter(ParameterSetName = 'Run')]
    [switch]$AllowCandidateUpgrade,

    [Parameter(ParameterSetName = 'Run')]
    [switch]$VerifyResumeInitialCameraDirection,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$p01RequestedSelfTest = [bool]$SelfTest
$p01RequestedCandidate = $Candidate
$p01RequestedFixture = $FreshIsolatedFixture
$p01RequestedLedger = $PreviousLedger
$p01RequestedSourcePin = $SourceOriginalHashPinned
$p01RequestedEvidenceRoot = $EvidenceRoot
$p01RequestedZoneUuid = $ZoneUuid
$p01RequestedStartupTimeout = $StartupTimeoutSeconds
$p01RequestedInteractionTimeout = $InteractionTimeoutSeconds
$p01RequestedMeasuredFloorY = $MeasuredFloorY
$p01RequestedAllowCandidateUpgrade = [bool]$AllowCandidateUpgrade
$p01RequestedVerifyResumeInitialCameraDirection = [bool]$VerifyResumeInitialCameraDirection

. (Join-Path $PSScriptRoot 'Gate3ProofStorage.ps1')
. (Join-Path $PSScriptRoot 'Invoke-Gate3GameplayAuthoringSmoke.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Invoke-Gate3GameplayPlaytestSmoke.ps1') -SelfTest | Out-Null

if (-not $p01RequestedSelfTest) {
    $Candidate = $p01RequestedCandidate
    $FreshIsolatedFixture = $p01RequestedFixture
    $PreviousLedger = $p01RequestedLedger
    $SourceOriginalHashPinned = $p01RequestedSourcePin
    $EvidenceRoot = $p01RequestedEvidenceRoot
    $ZoneUuid = $p01RequestedZoneUuid
    $StartupTimeoutSeconds = $p01RequestedStartupTimeout
    $InteractionTimeoutSeconds = $p01RequestedInteractionTimeout
    $MeasuredFloorY = $p01RequestedMeasuredFloorY
    $AllowCandidateUpgrade = $p01RequestedAllowCandidateUpgrade
    $VerifyResumeInitialCameraDirection = $p01RequestedVerifyResumeInitialCameraDirection
}

$p01ZoneId = $ZoneUuid.ToString('D').ToLowerInvariant()
$p01EditorSummary = 'Personnages, objets et objectifs'
$p01RoomName = 'Pièce'
$p01RoomX = 46.0
$p01RoomZ = -145.0
$p01DoorName = 'Porte intérieure'
$p01DoorX = 46.0
$p01DoorY = $MeasuredFloorY + 1.0
$p01DoorZ = -148.0
$p01PlaceName = 'Intérieur'
$p01OriginalPin = '50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720'
$script:P01Socket = $null

function Get-P01Property {
    param(
        [AllowNull()][object]$Object,
        [Parameter(Mandatory)][string]$Name
    )
    if ($null -eq $Object) { return $null }
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name)) { return $Object[$Name] }
        return $null
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-P01ZoneReference {
    param(
        [Parameter(Mandatory)][string]$WorkspaceArchive,
        [Parameter(Mandatory)][string]$AreaId
    )
    $workspace = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($WorkspaceArchive))
    $found = @(
        foreach ($project in @($workspace.head.projects)) {
            foreach ($module in @($project.modules)) {
                foreach ($zone in @($module.zones)) {
                    if ([string]$zone.area_id -ieq $AreaId) { $zone }
                }
            }
        }
    )
    if ($found.Count -ne 1) { throw "Expected one P01 Tidewater zone reference, found $($found.Count)." }
    return $found[0]
}

function Get-P01ObjectDocument {
    param(
        [Parameter(Mandatory)][string]$FixtureRoot,
        [Parameter(Mandatory)][string]$AreaId
    )
    $workspace = Join-Path $FixtureRoot 'workspace-v1\authoring.json'
    $reference = Get-P01ZoneReference -WorkspaceArchive $workspace -AreaId $AreaId
    $objectPath = Join-Path (Join-Path $FixtureRoot 'workspace-v1\objects') "$($reference.archive_hash).json"
    if (-not (Test-Path -LiteralPath $objectPath -PathType Leaf)) { throw "P01 current object is absent: $objectPath" }
    return [pscustomobject]@{
        Reference = $reference
        Path = $objectPath
        Document = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($objectPath))
    }
}

function Wait-P01SelectedElement {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $nameLiteral = ConvertTo-CompactJson -Value $Name
    return Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description "P01 selected element '$Name'" -Expression @"
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim(), expected=$nameLiteral;
  const summary=[...document.querySelectorAll('summary')].find(node=>norm(node.textContent)==='$p01EditorSummary');
  const scope=summary?.closest('details');
  const label=[...(scope??document).querySelectorAll('label')].find(node=>norm(node.textContent).startsWith('Nom'));
  const value=String(label?.querySelector('input')?.value??'');
  return {ready:value===expected,terminal:false,reason:'selected='+value};
})()
"@
}

function Wait-P01RoomInspector {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    return Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'P01 selected building inspector' -Expression @'
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const root=document.querySelector('.atelier-building-inspector');
  const labels=root?[...root.querySelectorAll('label')].filter(node=>norm(node.textContent).startsWith('Position X (m)')):[];
  const input=labels.length===1?labels[0].querySelector('input'):null;
  return {ready:!!input&&!input.disabled&&!input.closest('fieldset:disabled'),terminal:labels.length>1,reason:'position labels='+labels.length};
})()
'@
}

function Set-P01RoomInspectorField {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][string]$Text
    )
    $labelLiteral = ConvertTo-CompactJson -Value $Label
    $textLiteral = ConvertTo-CompactJson -Value $Text
    $focus = Invoke-CdpExpression -Socket $Socket -Expression @"
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim(), expected=$labelLiteral;
  const root=document.querySelector('.atelier-building-inspector');
  const labels=root?[...root.querySelectorAll('label')].filter(node=>norm(node.textContent).startsWith(expected)):[];
  if(labels.length!==1) throw new Error('Expected one room inspector label '+expected+', got '+labels.length);
  const control=labels[0].querySelector('input');
  if(!control || control.disabled || control.closest('fieldset:disabled')) throw new Error('Room inspector control unavailable: '+expected);
  control.scrollIntoView({block:'center',inline:'center'}); control.focus();
  return {focused:document.activeElement===control};
})()
"@
    if (-not $focus.focused) { throw "P01 room field '$Label' did not obtain focus." }
    [void](Send-CdpKeyStroke -Socket $Socket -Key 'a' -Code 'KeyA' -VirtualKeyCode 65 -Modifiers 2)
    [void](Invoke-CdpCommand -Socket $Socket -Method 'Input.insertText' -Parameters @{ text = $Text })
    [void](Send-CdpKeyStroke -Socket $Socket -Key 'Tab' -Code 'Tab' -VirtualKeyCode 9)
    $verify = Invoke-CdpExpression -Socket $Socket -Expression @"
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim(), expected=$labelLiteral, wanted=$textLiteral;
  const root=document.querySelector('.atelier-building-inspector');
  const labels=root?[...root.querySelectorAll('label')].filter(node=>norm(node.textContent).startsWith(expected)):[];
  const value=String(labels[0]?.querySelector('input')?.value??'');
  return {ready:labels.length===1&&value===wanted,terminal:false,reason:'value='+value,value};
})()
"@
    if (-not $verify.ready) { throw "P01 room field '$Label' did not persist public value '$Text'." }
    return $verify
}

function Wait-P01AuthoringSave {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    return Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'P01 Studio Save' -Expression @'
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const status=norm(document.querySelector('.authoring-command-status')?.textContent);
  const save=norm(document.querySelector('.authoring-command-bar .gold-save-state')?.textContent);
  const context=norm(document.querySelector('.authoring-context')?.textContent);
  const busy=document.querySelector('.authoring-toolbar')?.getAttribute('aria-busy');
  const terminal=/refusée|refusé|erreur|invalide|impossible/i.test(status);
  return {ready:status==='Session synchronisée'&&save===''&&context!==''&&context!=='Aucune zone ouverte'&&busy==='false'&&!terminal,
    terminal,reason:'status='+status+'; save='+save+'; context='+context+'; busy='+busy};
})()
'@
}

function Get-P01GameplayIdentity {
    param([Parameter(Mandatory)]$Report)
    return [ordered]@{
        actors = @($Report.actors | ForEach-Object { [ordered]@{ id=[string]$_.id; name=[string]$_.name; role=[string]$_.role; dialogue=[string](Get-P01Property -Object $_ -Name 'dialogue'); x=[double]$_.position.x; y=[double]$_.position.y; z=[double]$_.position.z } })
        objects = @($Report.objects | ForEach-Object { [ordered]@{ id=[string]$_.id; name=[string]$_.name; kind=[string]$_.kind.kind; x=[double]$_.position.x; y=[double]$_.position.y; z=[double]$_.position.z; supports=@((Get-P01Property -Object $_.physics -Name 'supports') | ForEach-Object {[string]$_}) } })
        places = @($Report.places | ForEach-Object { [ordered]@{ id=[string]$_.id; name=[string]$_.name; x=[double]$_.position.x; y=[double]$_.position.y; z=[double]$_.position.z; links=@((Get-P01Property -Object $_ -Name 'links') | ForEach-Object {[string]$_}) } })
        quests = @($Report.quests | ForEach-Object { [ordered]@{ id=[string]$_.id; giver=[string]$_.giver; required_item=[string]$_.required_item; reward_item=[string](Get-P01Property -Object $_ -Name 'reward_item'); reward_quantity=[int]$_.reward_quantity } })
    }
}

function Close-P01Studio {
    param(
        [Parameter(Mandatory)]$Process,
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    if ($Process.HasExited) { return $true }
    $Process.Refresh()
    if (-not $Process.CloseMainWindow()) { throw 'P01 owned Studio window did not accept WM_CLOSE.' }
    [void](Wait-CdpReady -Socket $Socket -TimeoutSeconds ([Math]::Min(15, $TimeoutSeconds)) -Description 'P01 native close dialog' -Expression @'
(() => ({ready:!!document.querySelector('dialog.gold-close-dialog[open]'),terminal:false,reason:'close dialog pending'}))()
'@)
    [void](Invoke-UiClick -Socket $Socket -Text 'Quitter sans enregistrer' -ScopeSelector 'dialog.gold-close-dialog[open]')
    return $Process.WaitForExit(10000)
}

if ($p01RequestedSelfTest) {
    foreach ($name in @(
        'ConvertTo-CompactJson', 'ConvertFrom-ExactJson', 'Invoke-CdpCommand', 'Invoke-CdpExpression',
        'Wait-CdpReady', 'Wait-CdpTarget', 'Connect-CdpSocket', 'New-LoopbackPortReservation',
        'Wait-LaunchedExecutableIdentity', 'Invoke-UiClick', 'Set-UiControlByLabel',
        'Set-UiControlByLabelWithKeyboard', 'Select-UiOptionByLabel', 'Open-DetailsBySummary',
        'Copy-IsolatedFixture', 'Get-ArchiveSnapshot', 'Test-ArchiveSnapshotsEqual',
        'Get-FileSha256', 'Get-NormalizedSha256', 'Assert-Gate3ProofPath', 'Assert-NoExistingReparseAncestor',
        'Assert-TreeHasNoReparsePoints', 'Test-PathWithinDirectory', 'Open-Gate3TidewaterZone',
        'Wait-GameplayEditor', 'Wait-GameplayForm', 'Wait-GameplayPreview', 'Wait-GameplayApply',
        'Send-CdpKeyStroke',
        'Invoke-GameplayPreviewApply', 'Get-Gate3GameplayReport', 'Start-ForgeStudioPlaytestProcess',
        'Stop-ForgeStudioPlaytestProcess'
    )) { [void](Get-Command $name -ErrorAction Stop) }
    $raw = [IO.File]::ReadAllText($PSCommandPath)
    foreach ($needle in @(
        'Bâtiment à inspecter', 'Position X (m)', 'Position Y (m)', 'Position Z (m)',
        'Prévisualiser position et orientation', 'Personnages, objets et objectifs',
        'Élément à retoucher', 'Examiner le nom, la position et la réplique',
        'Sauvegarder', 'Quitter sans enregistrer', '6.6496172428', '46', '-145', '-148',
        'workspace-v1\\objects', 'Copy-IsolatedFixture', 'source_preserved',
        'gate_qualified = $false', 'parent_candidate_sha256', 'gate3-physics-authoring-smoke',
        'parent_ledger_schema', 'zone_uuid',
        'candidate_differs_from_parent', 'AllowCandidateUpgrade', 'VerifyResumeInitialCameraDirection'
    )) {
        if ($raw.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "P01 selector/contract missing: $needle" }
    }
    if ($raw -match 'Runtime\.evaluate[^\r\n]*window\.__') { throw 'P01 SelfTest found a domain-state injection pattern.' }
    $serializationProbe = [ordered]@{
        schema = 'forge.tidewater.gate3-gameplay-authoring-smoke'
        saved_reference = [ordered]@{ archive_hash = ('a' * 64); head_hash = ('b' * 64) }
        room = [ordered]@{ name = 'Pièce'; x = 46.0; y = 6.6496172428; z = -145.0 }
    }
    $serializationJson = [string](ConvertTo-CompactJson -Value $serializationProbe)
    $serializationDocument = ConvertFrom-ExactJson -Json $serializationJson
    if ([string]$serializationDocument.schema -cne 'forge.tidewater.gate3-gameplay-authoring-smoke' -or
        [string]$serializationDocument.room.name -cne 'Pièce' -or
        [string]$serializationDocument.saved_reference.archive_hash -cne ('a' * 64)) {
        throw 'P01 ledger serialization probe did not round-trip as an object.'
    }
    if ($raw.IndexOf('$p01AuthoringLedger', [StringComparison]::Ordinal) -lt 0) {
        throw 'P01 ledger variable must remain distinct from the dot-sourced AuthoringLedger parameter.'
    }
    Write-Output 'TIDEWATER_GATE3_ROOM_RETOUCH_SMOKE_SELF_TEST_PASS'
    return
}

if (-not (Test-IsWindowsPlatform)) { throw 'Windows/WebView2 is required.' }
$candidate = [IO.Path]::GetFullPath($Candidate)
$source = [IO.Path]::GetFullPath($FreshIsolatedFixture)
$ledgerInput = [IO.Path]::GetFullPath($PreviousLedger)
$output = [IO.Path]::GetFullPath($EvidenceRoot)
$sourcePin = Get-NormalizedSha256 -Value $SourceOriginalHashPinned
foreach ($entry in @(@($candidate, 'Candidate'), @($source, 'P01 input fixture'), @($ledgerInput, 'P01 parent ledger'), @($output, 'P01 evidence root'))) {
    Assert-Gate3ProofPath -LiteralPath $entry[0] -Description $entry[1]
    Assert-NoExistingReparseAncestor -LiteralPath $entry[0] -Description $entry[1]
}
if (Test-Path -LiteralPath $output) { throw "P01 EvidenceRoot must be fresh: $output" }
if ((Test-PathWithinDirectory -LiteralPath $output -Directory $source) -or (Test-PathWithinDirectory -LiteralPath $source -Directory $output)) { throw 'P01 output and input fixture must remain separate.' }
if ((Test-PathWithinDirectory -LiteralPath $output -Directory (Split-Path -Parent $candidate)) -or (Test-PathWithinDirectory -LiteralPath (Split-Path -Parent $candidate) -Directory $output)) { throw 'P01 output and candidate directory must remain separate.' }
if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Candidate is absent: $candidate" }
if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "P01 input fixture is absent: $source" }
if (-not (Test-Path -LiteralPath $ledgerInput -PathType Leaf)) { throw "P01 parent ledger is absent: $ledgerInput" }
Assert-TreeHasNoReparsePoints -LiteralPath $source -Description 'P01 input fixture'
$candidateSha256 = Get-NormalizedSha256 -Value (Get-FileSha256 -LiteralPath $candidate)

$parent = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($ledgerInput))
$parentSchema = [string](Get-P01Property -Object $parent -Name 'schema')
$parentStatus = [string](Get-P01Property -Object $parent -Name 'status')
$parentIsGameplayPlaytest = $parentSchema -ceq 'forge.tidewater.gate3-gameplay-playtest-smoke'
$parentIsPhysicsAuthoring = $parentSchema -ceq 'forge.tidewater.gate3-physics-authoring-smoke'
if ((-not $parentIsGameplayPlaytest -and -not $parentIsPhysicsAuthoring) -or $parentStatus -cne 'passed') {
    throw 'P01 parent ledger must be a passed gameplay playtest or physics authoring ledger.'
}
if ([bool](Get-P01Property -Object $parent -Name 'gate_qualified')) { throw 'P01 parent ledger incorrectly claims Gate qualification.' }
$parentPreservesSource = if ($parentIsGameplayPlaytest) {
    [bool](Get-P01Property -Object $parent -Name 'source_preserved')
} else {
    # The physics authoring smoke deliberately leaves source_preserved null
    # because its source fixture is an older authoring checkpoint.  Its two
    # explicit archive attestations are the source-preservation contract for
    # this handoff.
    [bool](Get-P01Property -Object $parent -Name 'source_original_archive_preserved') -and
        [bool](Get-P01Property -Object $parent -Name 'isolated_legacy_archives_preserved')
}
if (-not $parentPreservesSource) { throw 'P01 parent ledger does not attest preservation of the pinned source and legacy archives.' }
if ((Get-NormalizedSha256 -Value ([string](Get-P01Property -Object $parent -Name 'source_original_hash_pinned'))) -cne $sourcePin) { throw 'P01 parent source pin differs.' }
$parentFixture = [IO.Path]::GetFullPath([string](Get-P01Property -Object $parent -Name 'isolated_fixture'))
if (-not $parentFixture.Equals($source, [StringComparison]::OrdinalIgnoreCase)) { throw 'P01 input fixture is not the parent playtest isolated fixture.' }
$parentCandidateValue = [string](Get-P01Property -Object $parent -Name 'candidate_sha256')
$parentCandidateSha256 = if ([string]::IsNullOrWhiteSpace($parentCandidateValue)) {
    $null
} else {
    Get-NormalizedSha256 -Value $parentCandidateValue
}
$candidateDiffersFromParent = $null -ne $parentCandidateSha256 -and $parentCandidateSha256 -cne $candidateSha256
if ($candidateDiffersFromParent -and -not $AllowCandidateUpgrade) {
    throw 'P01 candidate differs from the parent candidate pin; rerun with -AllowCandidateUpgrade and record the new candidate explicitly.'
}

$sourceArchive = Join-Path (Join-Path $source $p01ZoneId) 'authoring.json'
$sourceWorkspace = Join-Path $source 'workspace-v1\authoring.json'
$sourceOriginalObject = Join-Path (Join-Path $source 'workspace-v1\objects') "$sourcePin.json"
foreach ($path in @($sourceArchive, $sourceWorkspace, $sourceOriginalObject)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "P01 source file is absent: $path" }
}
$sourceArchiveBefore = Get-ArchiveSnapshot -LiteralPath $sourceArchive
$sourceWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $sourceWorkspace
$sourceOriginalBefore = Get-ArchiveSnapshot -LiteralPath $sourceOriginalObject
if ($sourceArchiveBefore.Sha256 -cne $sourcePin -or $sourceOriginalBefore.Sha256 -cne $sourcePin) { throw 'P01 input does not preserve the pinned original bytes.' }
$sourceCurrent = Get-P01ObjectDocument -FixtureRoot $source -AreaId $p01ZoneId
$sourceCurrentReport = Get-Gate3GameplayReport -Archive $sourceCurrent.Path
if (-not $sourceCurrentReport.present) { throw 'P01 input has no gameplay document.' }
$sourceBuildings = @($sourceCurrent.Document.head.corrections.buildings)
if ($sourceBuildings.Count -ne 1 -or [string]$sourceBuildings[0].name -cne $p01RoomName) { throw 'P01 input must contain one generic Pièce.' }
$sourceBuilding = $sourceBuildings[0]
$sourceBuildingId = [string]$sourceBuilding.id
$sourceIdentityBefore = Get-P01GameplayIdentity -Report $sourceCurrentReport
$sourcePlacementIdsBefore = @($sourceCurrent.Document.head.corrections.placements | ForEach-Object { [string]$_.id })

[void](New-Item -ItemType Directory -Path $output)
$fixture = Join-Path $output 'room-retouch-root'
$cloneArchive = Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $p01ZoneId
$cloneWorkspace = Join-Path $fixture 'workspace-v1\authoring.json'
$cloneOriginal = Join-Path (Join-Path $fixture 'workspace-v1\objects') "$sourcePin.json"
$cloneBefore = Get-ArchiveSnapshot -LiteralPath $cloneArchive
$cloneWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $cloneWorkspace
$cloneOriginalBefore = Get-ArchiveSnapshot -LiteralPath $cloneOriginal
if ($cloneBefore.Sha256 -cne $sourceArchiveBefore.Sha256 -or $cloneWorkspaceBefore.Sha256 -cne $sourceWorkspaceBefore.Sha256 -or $cloneOriginalBefore.Sha256 -cne $sourceOriginalBefore.Sha256) { throw 'P01 isolated copy does not preserve the parent fixture.' }

$savedEnvironment = @{}
foreach ($name in @('FORGE_AUTHORING_ROOT', 'FORGE_AUTHOR_SUBJECT', 'WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', 'WEBVIEW2_USER_DATA_FOLDER')) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$profile = Join-Path $output 'webview2-user-data'
$logs = Join-Path $output 'studio-logs'
[void](New-Item -ItemType Directory -Path $profile, $logs)
$process = $null
$socket = $null
$reservation = $null
$failure = $null
$nativeCloseConfirmed = $false
$forcedProcessStop = $false
$uiClosed = $false
$savedReference = $null
$savedObjectPath = $null
$savedObject = $null
$savedReport = $null
$playtestLedgerPath = $null
$playtestFailure = $null

try {
    $reservation = New-LoopbackPortReservation
    $port = $reservation.Port
    [Environment]::SetEnvironmentVariable('FORGE_AUTHORING_ROOT', $fixture, 'Process')
    [Environment]::SetEnvironmentVariable('FORGE_AUTHOR_SUBJECT', 'tidewater-gate3-room-retouch-smoke', 'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', "--remote-debugging-address=127.0.0.1 --remote-debugging-port=$port", 'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER', $profile, 'Process')
    $reservation.Listener.Stop()
    $process = Start-Process -FilePath $candidate -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $logs 'candidate.stdout.log') `
        -RedirectStandardError (Join-Path $logs 'candidate.stderr.log')
    [void](Wait-LaunchedExecutableIdentity -Process $process -Candidate $candidate -ExpectedHash $candidateSha256)
    $target = Wait-CdpTarget -Port $port -TimeoutSeconds $StartupTimeoutSeconds -Process $process
    $socket = Connect-CdpSocket -WebSocketUrl ([string]$target.webSocketDebuggerUrl)
    $script:P01Socket = $socket
    [void](Invoke-CdpCommand -Socket $socket -Method 'Runtime.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.bringToFront')
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'P01 Studio shell' -Expression @'
(() => ({ready:document.readyState==='complete'&&!!document.querySelector('.authoring-shell'),terminal:false,reason:'authoring shell pending'}))()
'@)
    Open-Gate3TidewaterZone -Socket $socket -ZoneId $p01ZoneId -TimeoutSeconds $InteractionTimeoutSeconds

    [void](Invoke-UiClick -Socket $socket -Text 'Construire' -Selector '[data-authoring-tab-button]')
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'P01 building controls' -Expression @'
(() => ({ready:!!document.querySelector('.atelier-building-controls'),terminal:false,reason:'building controls pending'}))()
'@)
    [void](Select-UiOptionByLabel -Socket $socket -Label 'Bâtiment à inspecter' -Value $sourceBuildingId -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Wait-P01RoomInspector -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Invoke-UiClick -Socket $socket -Text 'Position' -Selector '.atelier-building-subjects button')
    [void](Set-P01RoomInspectorField -Socket $socket -Label 'Position X (m)' -Text '46')
    [void](Set-P01RoomInspectorField -Socket $socket -Label 'Position Y (m)' -Text $MeasuredFloorY.ToString('R', [Globalization.CultureInfo]::InvariantCulture))
    [void](Set-P01RoomInspectorField -Socket $socket -Label 'Position Z (m)' -Text '-145')
    [void](Set-P01RoomInspectorField -Socket $socket -Label 'Orientation Y (degrés)' -Text '0')
    [void](Invoke-UiClick -Socket $socket -Text 'Prévisualiser position et orientation' -Selector 'button' -ScopeSelector '.atelier-building-inspector')
    [void](Wait-GameplayPreview -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'P01 room pose preview')
    [void](Invoke-UiClick -Socket $socket -Text 'Appliquer' -Selector 'button.authoring-apply')
    [void](Wait-GameplayApply -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'P01 room pose apply')

    [void](Invoke-UiClick -Socket $socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Wait-GameplayEditor -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Open-DetailsBySummary -Socket $socket -Summary $p01EditorSummary)
    [void](Select-GameplayOptionByText -Socket $socket -Label 'Élément à retoucher' -Text $p01PlaceName -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Wait-P01SelectedElement -Socket $socket -Name $p01PlaceName -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Set-UiControlByLabelWithKeyboard -Socket $socket -Label 'Nom' -Text $p01PlaceName -DetailsSummary $p01EditorSummary)
    [void](Set-UiControlByLabelWithKeyboard -Socket $socket -Label 'X (m)' -Text '46' -DetailsSummary $p01EditorSummary)
    [void](Set-UiControlByLabelWithKeyboard -Socket $socket -Label 'Z (m)' -Text '-145' -DetailsSummary $p01EditorSummary)
    [void](Set-Gate3UiTextByLabel -Socket $socket -Label 'Hauteur du centre (m, vide = terrain)' -Text '' -DetailsSummary $p01EditorSummary)
    [void](Invoke-GameplayPreviewApply -Socket $socket -PreviewButton 'Examiner le nom, la position et la réplique' -Description 'P01 interior place retouch' -TimeoutSeconds $InteractionTimeoutSeconds)

    [void](Select-GameplayOptionByText -Socket $socket -Label 'Élément à retoucher' -Text $p01DoorName -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Wait-P01SelectedElement -Socket $socket -Name $p01DoorName -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Set-UiControlByLabelWithKeyboard -Socket $socket -Label 'Nom' -Text $p01DoorName -DetailsSummary $p01EditorSummary)
    [void](Set-UiControlByLabelWithKeyboard -Socket $socket -Label 'X (m)' -Text '46' -DetailsSummary $p01EditorSummary)
    [void](Set-UiControlByLabelWithKeyboard -Socket $socket -Label 'Z (m)' -Text '-148' -DetailsSummary $p01EditorSummary)
    [void](Set-UiControlByLabelWithKeyboard -Socket $socket -Label 'Hauteur du centre (m, vide = terrain)' -Text $p01DoorY.ToString('R', [Globalization.CultureInfo]::InvariantCulture) -DetailsSummary $p01EditorSummary)
    [void](Set-Gate3UiTextByLabel -Socket $socket -Label 'Réplique' -Text '' -DetailsSummary $p01EditorSummary)
    [void](Invoke-GameplayPreviewApply -Socket $socket -PreviewButton 'Examiner le nom, la position et la réplique' -Description 'P01 interior door retouch' -TimeoutSeconds $InteractionTimeoutSeconds)

    [void](Invoke-UiClick -Socket $socket -Text 'Sauvegarder')
    [void](Wait-P01AuthoringSave -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)
    $savedReference = Get-P01ZoneReference -WorkspaceArchive $cloneWorkspace -AreaId $p01ZoneId
    if ([string]$savedReference.archive_hash -ceq $sourcePin) { throw 'P01 Studio Save did not create a new managed object.' }
    $savedObjectPath = Join-Path (Join-Path $fixture 'workspace-v1\objects') "$($savedReference.archive_hash).json"
    if (-not (Test-Path -LiteralPath $savedObjectPath -PathType Leaf)) { throw 'P01 saved managed object is absent.' }
    $savedObject = Get-ArchiveSnapshot -LiteralPath $savedObjectPath
    if ($savedObject.Sha256 -cne [string]$savedReference.archive_hash) { throw 'P01 saved managed object hash does not match the workspace reference.' }
    $savedDocument = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($savedObjectPath))
    if (@($savedDocument.head.corrections.placements | ForEach-Object { [string]$_.id }).Count -ne 23) { throw 'P01 Save changed the reviewed placement count.' }
    $savedReport = Get-Gate3GameplayReport -Archive $savedObjectPath
    if (-not $savedReport.present) { throw 'P01 saved object has no gameplay correction.' }
    $savedIdentity = Get-P01GameplayIdentity -Report $savedReport
    $savedBuilding = @($savedDocument.head.corrections.buildings | Where-Object { [string]$_.id -ceq $sourceBuildingId })
    if ($savedBuilding.Count -ne 1) { throw 'P01 generic room identity was not preserved.' }
    if ([Math]::Abs([double]$savedBuilding[0].origin_m.x - $p01RoomX) -gt 0.0001 -or
        [Math]::Abs([double]$savedBuilding[0].origin_m.y - $MeasuredFloorY) -gt 0.0001 -or
        [Math]::Abs([double]$savedBuilding[0].origin_m.z - $p01RoomZ) -gt 0.0001) { throw 'P01 saved room pose differs from the measured public input.' }
    $doorSaved = @($savedReport.objects | Where-Object { [string]$_.name -ceq $p01DoorName -and [string]$_.kind.kind -ceq 'door' })
    $placeSaved = @($savedReport.places | Where-Object { [string]$_.name -ceq $p01PlaceName })
    if ($doorSaved.Count -ne 1 -or $placeSaved.Count -ne 1) { throw 'P01 saved door/place identity is incomplete.' }
    if ([Math]::Abs([double]$doorSaved[0].position.x - $p01DoorX) -gt 0.0001 -or
        [Math]::Abs([double]$doorSaved[0].position.y - $p01DoorY) -gt 0.0001 -or
        [Math]::Abs([double]$doorSaved[0].position.z - $p01DoorZ) -gt 0.0001) { throw 'P01 saved door pose differs from the reviewed measured room pose.' }
    if ([Math]::Abs([double]$placeSaved[0].position.x - $p01RoomX) -gt 0.0001 -or
        [Math]::Abs([double]$placeSaved[0].position.z - $p01RoomZ) -gt 0.0001) { throw 'P01 saved Interior place pose differs from the room pose.' }
    $savedPlacementIds = @($savedDocument.head.corrections.placements | ForEach-Object { [string]$_.id })
    if ((ConvertTo-CompactJson -Value $sourcePlacementIdsBefore) -cne (ConvertTo-CompactJson -Value $savedPlacementIds)) { throw 'P01 altered imported placement identities.' }
    if ((ConvertTo-CompactJson -Value $sourceIdentityBefore.actors) -cne (ConvertTo-CompactJson -Value $savedIdentity.actors) -or
        (ConvertTo-CompactJson -Value $sourceIdentityBefore.quests) -cne (ConvertTo-CompactJson -Value $savedIdentity.quests)) { throw 'P01 changed actor/quest identities or links.' }

    [void](Invoke-UiClick -Socket $socket -Text 'Projet' -Selector '[data-authoring-tab-button]')
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Créer ou modifier la zone')
    [void](Invoke-UiClick -Socket $socket -Text 'Fermer la zone')
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'P01 close authored zone' -Expression @'
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const context=norm(document.querySelector('.authoring-context')?.textContent);
  const open=document.documentElement?.getAttribute('data-forge-zone-open');
  return {ready:context==='Aucune zone ouverte'&&open==='false',terminal:false,reason:'context='+context+'; open='+open};
})()
'@)
    $uiClosed = $true
}
catch {
    $failure = $_.Exception.Message
}
finally {
    if ($null -ne $process -and -not $process.HasExited -and $null -ne $socket -and $socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
        try { $nativeCloseConfirmed = Close-P01Studio -Process $process -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds } catch { $failure = "close: $($_.Exception.Message); $failure" }
    }
    if ($null -ne $socket) {
        try { if ($socket.State -eq [Net.WebSockets.WebSocketState]::Open) { $socket.Dispose() } } catch { }
    }
    if ($null -ne $process -and -not $process.HasExited) {
        try { [void]$process.CloseMainWindow() } catch { }
        if (-not $process.WaitForExit(5000)) {
            Stop-Process -Id $process.Id -Force
            [void]$process.WaitForExit(5000)
            $forcedProcessStop = $true
        }
    }
    if ($null -ne $reservation) { $reservation.Listener.Stop() }
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
}

$sourceArchiveAfter = Get-ArchiveSnapshot -LiteralPath $sourceArchive
$sourceWorkspaceAfter = Get-ArchiveSnapshot -LiteralPath $sourceWorkspace
$sourceOriginalAfter = Get-ArchiveSnapshot -LiteralPath $sourceOriginalObject
$sourcePreserved = (Test-ArchiveSnapshotsEqual -Before $sourceArchiveBefore -After $sourceArchiveAfter) -and
    (Test-ArchiveSnapshotsEqual -Before $sourceWorkspaceBefore -After $sourceWorkspaceAfter) -and
    (Test-ArchiveSnapshotsEqual -Before $sourceOriginalBefore -After $sourceOriginalAfter)
if (-not $sourcePreserved) { $failure = "P01 source fixture changed; $failure" }
if ($forcedProcessStop) { $failure = "P01 owned Studio process required a forced stop; $failure" }
if ($null -ne $process -and -not $process.HasExited) { $failure = "P01 owned Studio process remained alive; $failure" }
if (-not $uiClosed) { $failure = "P01 zone was not closed through the public UI; $failure" }

$authoringLedgerPath = Join-Path $output 'gate3-room-retouch-authoring-ledger.json'
$authoringStatus = if ([string]::IsNullOrWhiteSpace($failure) -and $null -ne $savedReference -and $null -ne $savedReport) { 'passed' } else { 'failed_closed' }
$p01AuthoringLedger = [ordered]@{
    # Keep the established authoring-ledger schema so the existing public
    # playtest smoke can consume this checkpoint without a parallel format.
    schema = 'forge.tidewater.gate3-gameplay-authoring-smoke'
    version = 1
    status = $authoringStatus
    failure = $failure
    gate_qualified = $false
    human_visual_review = $false
    candidate = $candidate
    candidate_sha256 = $candidateSha256
    source_fixture = $source
    source_original_hash_pinned = $sourcePin
    source_archive_before = $sourceArchiveBefore
    source_archive_after = $sourceArchiveAfter
    source_workspace_before = $sourceWorkspaceBefore
    source_workspace_after = $sourceWorkspaceAfter
    source_object_before = $sourceOriginalBefore
    source_object_after = $sourceOriginalAfter
    source_preserved = $sourcePreserved
    parent_candidate_sha256 = $parentCandidateSha256
    candidate_differs_from_parent = $candidateDiffersFromParent
    candidate_upgrade_explicitly_allowed = [bool]$AllowCandidateUpgrade
    parent_ledger = $ledgerInput
    parent_ledger_schema = $parentSchema
    parent_evidence_kind = [string](Get-P01Property -Object $parent -Name 'evidence_kind')
    parent_saved_reference = Get-P01Property -Object $parent -Name 'saved_reference'
    zone_uuid = $p01ZoneId
    isolated_fixture = $fixture
    isolated_archive_before = $cloneBefore
    isolated_workspace_before = $cloneWorkspaceBefore
    saved_reference = $savedReference
    saved_object = $savedObject
    room = [ordered]@{ id = $sourceBuildingId; name = $p01RoomName; x = $p01RoomX; y = $MeasuredFloorY; z = $p01RoomZ; width = [double]$sourceBuilding.dimensions.width_m; depth = [double]$sourceBuilding.dimensions.depth_m }
    door = [ordered]@{ name = $p01DoorName; x = $p01DoorX; y = $p01DoorY; z = $p01DoorZ }
    place = [ordered]@{ name = $p01PlaceName; x = $p01RoomX; z = $p01RoomZ; y_policy = 'public canonical terrain after clearing Y' }
    gameplay_identity_before = $sourceIdentityBefore
    gameplay_identity_after = if ($null -ne $savedReport) { Get-P01GameplayIdentity -Report $savedReport } else { $null }
    imported_placement_ids_preserved = if ($null -ne $savedObjectPath) { [bool]((ConvertTo-CompactJson -Value $sourcePlacementIdsBefore) -ceq (ConvertTo-CompactJson -Value @((ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($savedObjectPath))).head.corrections.placements | ForEach-Object { [string]$_.id }))) } else { $false }
    measured_floor_y = $MeasuredFloorY
    ui_closed = $uiClosed
    native_close_confirmed = $nativeCloseConfirmed
    forced_process_stop = $forcedProcessStop
    limitations = @(
        'P01 records a public Studio authoring checkpoint and delegates the fresh gameplay Save/Resume proof to the existing public playtest smoke.',
        'The room Y was supplied from the reviewed raster measurement; the Interior place Y remains canonical terrain because the public editor clears it before Preview.',
        'No imported placement, source object or source archive is edited; all room, place and door edits occur in the isolated copied workspace through public controls.',
        'gate_qualified remains false; this is a bounded P01/P02 preparation proof.'
    )
}
$authoringLedgerJson = [string](ConvertTo-CompactJson -Value $p01AuthoringLedger)
[IO.File]::WriteAllText((Join-Path $output 'authoring-ledger-runtime-type.txt'), "type=$($p01AuthoringLedger.GetType().FullName)`njson_type=$($authoringLedgerJson.GetType().FullName)`njson_length=$($authoringLedgerJson.Length)", [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($authoringLedgerPath, $authoringLedgerJson, [Text.UTF8Encoding]::new($false))
if ($authoringStatus -cne 'passed') { throw "P01 room authoring failed closed: $failure. Ledger: $authoringLedgerPath" }

# A moved Place makes the prior gameplay save incompatible.  The established
# playtest smoke starts a new public GameSession on this authored checkpoint,
# performs the first loop and writes a new real save before its resume run.
$playtestOutput = Join-Path $output 'playtest-runtime'
$playtestScript = Join-Path $PSScriptRoot 'Invoke-Gate3GameplayPlaytestSmoke.ps1'
$pwsh = Join-Path $PSHOME 'pwsh.exe'
if (-not (Test-Path -LiteralPath $pwsh -PathType Leaf)) { $pwsh = (Get-Command pwsh -ErrorAction Stop).Source }
$playtestArguments = @(
    '-NoProfile', '-File', $playtestScript,
    '-Candidate', $candidate,
    '-FreshIsolatedFixture', $fixture,
    '-AuthoringLedger', $authoringLedgerPath,
    '-SourceOriginalHashPinned', $sourcePin,
    '-EvidenceRoot', $playtestOutput,
    '-ZoneUuid', $ZoneUuid.ToString('D'),
    '-StartupTimeoutSeconds', [string]$StartupTimeoutSeconds,
    '-InteractionTimeoutSeconds', [string]$InteractionTimeoutSeconds
)
if ($VerifyResumeInitialCameraDirection) {
    $playtestArguments += '-VerifyResumeInitialCameraDirection'
}
$playtestLedgerPath = Join-Path $playtestOutput 'gate3-gameplay-playtest-smoke-ledger.json'
try {
    & $pwsh @playtestArguments
    if ($LASTEXITCODE -ne 0) { throw "P01 delegated public playtest exited with code $LASTEXITCODE." }
    if (-not (Test-Path -LiteralPath $playtestLedgerPath -PathType Leaf)) { throw 'P01 delegated playtest did not produce a ledger.' }
}
catch {
    $playtestFailure = $_.Exception.Message
    $failure = "P01 fresh gameplay loop failed: $playtestFailure"
}

$finalStatus = if ([string]::IsNullOrWhiteSpace($failure) -and $null -ne $playtestLedgerPath) { 'passed' } else { 'failed_closed' }
$finalLedger = [ordered]@{
    schema = 'forge.tidewater.gate3-gameplay-room-retouch-smoke'
    version = 1
    status = $finalStatus
    failure = $failure
    gate_qualified = $false
    human_visual_review = $false
    authoring_ledger = $authoringLedgerPath
    authoring = $p01AuthoringLedger
    playtest_ledger = $playtestLedgerPath
    playtest_failure = $playtestFailure
    candidate = $candidate
    candidate_sha256 = $candidateSha256
    parent_candidate_sha256 = $parentCandidateSha256
    candidate_differs_from_parent = $candidateDiffersFromParent
    candidate_upgrade_explicitly_allowed = [bool]$AllowCandidateUpgrade
    parent_ledger = $ledgerInput
    parent_ledger_schema = $parentSchema
    parent_evidence_kind = [string](Get-P01Property -Object $parent -Name 'evidence_kind')
    parent_saved_reference = Get-P01Property -Object $parent -Name 'saved_reference'
    zone_uuid = $p01ZoneId
    source_fixture = $source
    isolated_fixture = $fixture
    source_original_hash_pinned = $sourcePin
    source_preserved = $sourcePreserved
    room = Get-P01Property -Object $p01AuthoringLedger -Name 'room'
    door = Get-P01Property -Object $p01AuthoringLedger -Name 'door'
    place = Get-P01Property -Object $p01AuthoringLedger -Name 'place'
    measured_floor_y = $MeasuredFloorY
    fresh_save_required_after_place_change = $true
    limitations = @(
        'A PASS here is local evidence only: it does not close Gate 3 or claim visual, navigation or P05 acceptance.',
        'The delegated gameplay smoke is the source of truth for the new save and its second-process Resume; P02 must use that playtest-root and ledger.',
        'The source fixture and legacy 50bfd original remain byte-identical; the P01 copy intentionally contains the new authored revision.'
    )
}
$finalLedgerPath = Join-Path $output 'gate3-room-retouch-smoke-ledger.json'
$finalLedgerJson = [string](ConvertTo-CompactJson -Value $finalLedger)
[IO.File]::WriteAllText($finalLedgerPath, $finalLedgerJson, [Text.UTF8Encoding]::new($false))
if ($finalStatus -cne 'passed') { throw "P01 room retouch smoke failed closed: $failure. Ledger: $finalLedgerPath" }
Write-Output "TIDEWATER_GATE3_ROOM_RETOUCH_SMOKE_PASS ledger=$finalLedgerPath playtest=$playtestLedgerPath"
