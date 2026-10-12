# Gate 3 R3-P02 compatible-retouch smoke for Tidewater.
# This is a bounded public-UI proof. It deliberately leaves qualification,
# navigation, visual acceptance and release decisions to their own evidence.
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
    [Alias('AuthoringLedger', 'PlaytestLedger', 'PriorLedger', 'LedgerPath')]
    [ValidateNotNullOrEmpty()]
    [string]$PreviousLedger,

    [Parameter(ParameterSetName = 'Run')]
    [Alias('SourceOriginalHash')]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$SourceOriginalHashPinned = '50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720',

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
    [switch]$AllowSameCandidate,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Existing smokes expose the governed CDP/fixture helpers on their SelfTest
# path. Preserve this script's bindings because those scripts reuse names.
$retouchRequestedSelfTest = [bool]$SelfTest
$retouchRequestedCandidate = $Candidate
$retouchRequestedFixture = $FreshIsolatedFixture
$retouchRequestedLedger = $PreviousLedger
$retouchRequestedSourcePin = $SourceOriginalHashPinned
$retouchRequestedEvidenceRoot = $EvidenceRoot
$retouchRequestedZoneUuid = $ZoneUuid
$retouchRequestedStartupTimeout = $StartupTimeoutSeconds
$retouchRequestedInteractionTimeout = $InteractionTimeoutSeconds
$retouchRequestedAllowSameCandidate = [bool]$AllowSameCandidate
. (Join-Path $PSScriptRoot 'Invoke-Gate3GameplayAuthoringSmoke.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Invoke-Gate3GameplayPlaytestSmoke.ps1') -SelfTest | Out-Null
if (-not $retouchRequestedSelfTest) {
    $Candidate = $retouchRequestedCandidate
    $FreshIsolatedFixture = $retouchRequestedFixture
    $PreviousLedger = $retouchRequestedLedger
    $SourceOriginalHashPinned = $retouchRequestedSourcePin
    $EvidenceRoot = $retouchRequestedEvidenceRoot
    $ZoneUuid = $retouchRequestedZoneUuid
    $StartupTimeoutSeconds = $retouchRequestedStartupTimeout
    $InteractionTimeoutSeconds = $retouchRequestedInteractionTimeout
    $AllowSameCandidate = $retouchRequestedAllowSameCandidate
}

$retouchZoneId = $ZoneUuid.ToString('D').ToLowerInvariant()
$retouchEditorSummary = 'Personnages, objets et objectifs'
$retouchDoorName = 'Porte intérieure'
$retouchIntendantName = 'Intendant du village'
$retouchInteriorPlaceName = 'Intérieur'
$retouchVillageX = 40.0
$retouchVillageZ = -118.0
$script:CdpRetouchMessageId = 0

function Get-Gate3RetouchOptional {
    param(
        [AllowNull()][object]$Object,
        [Parameter(Mandatory)][string]$Name
    )
    if ($null -eq $Object) {
        return $null
    }
    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name)) {
            return $Object[$Name]
        }
        return $null
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }
    return $property.Value
}

function ConvertTo-Gate3RetouchNumber {
    param(
        [AllowEmptyString()]
        [Parameter(Mandatory)][string]$Text
    )
    [double]$value = 0
    $normal = $Text.Replace(',', '.')
    if (-not [double]::TryParse(
            $normal,
            [Globalization.NumberStyles]::Float,
            [Globalization.CultureInfo]::InvariantCulture,
            [ref]$value
        ) -or -not [double]::IsFinite($value)) {
        throw "Public UI value is not a finite invariant number: '$Text'"
    }
    return $value
}

function Get-Gate3RetouchSaveFile {
    param(
        [Parameter(Mandatory)][string]$FixtureRoot,
        [Parameter(Mandatory)][string[]]$ExpectedSourceHashes,
        [Parameter(Mandatory)][string]$AreaId
    )
    $expectedHashes = @($ExpectedSourceHashes | ForEach-Object {
            Get-NormalizedSha256 -Value $_
        })
    $saveRoot = Join-Path $FixtureRoot '.game-saves'
    if (-not (Test-Path -LiteralPath $saveRoot -PathType Container)) {
        throw "The playtest fixture has no .game-saves directory: $saveRoot"
    }
    $candidates = @(
        Get-ChildItem -LiteralPath $saveRoot -Recurse -File -Force -ErrorAction Stop |
            Where-Object { $_.Name -ceq 'authoring.json' }
    )
    foreach ($item in $candidates) {
        try {
            $document = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($item.FullName))
            $schema = [string](Get-Gate3RetouchOptional -Object $document -Name 'schema')
            $area = [string](Get-Gate3RetouchOptional -Object $document -Name 'area_id')
            $source = [string](Get-Gate3RetouchOptional -Object $document -Name 'source_hash')
            if ($schema -ceq 'forge.game-save' -and
                $area.Equals($AreaId, [StringComparison]::OrdinalIgnoreCase) -and
                $expectedHashes -contains (Get-NormalizedSha256 -Value $source)) {
                return [pscustomobject]@{
                    File = $item
                    Document = $document
                    SourceHash = Get-NormalizedSha256 -Value $source
                }
            }
        }
        catch {
            Write-Verbose "Ignoring non-game-save archive '$($item.FullName)': $($_.Exception.Message)"
        }
    }
    throw "No real Tidewater game save with an authorized source hash ($($expectedHashes -join ', ')) was found below $saveRoot."
}

function Wait-Gate3RetouchSelectedElement {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $nameLiteral = ConvertTo-CompactJson -Value $Name
    return Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description "selected gameplay element '$Name'" -Expression @"
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const expected=$nameLiteral;
  const summary=[...document.querySelectorAll('summary')].find(node=>norm(node.textContent)==='$retouchEditorSummary');
  const scope=summary?.closest('details');
  const label=[...((scope??document).querySelectorAll('label'))].find(node=>norm(node.textContent).startsWith('Nom'));
  const value=String(label?.querySelector('input')?.value??'');
  return {ready:value===expected,terminal:false,reason:'selected='+value};
})()
"@
}

function Wait-Gate3RetouchAuthoringSave {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    return Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'R3-P02 authoring Save' -Expression @'
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const status=norm(document.querySelector('.authoring-command-status')?.textContent);
  const save=norm(document.querySelector('.authoring-command-bar .gold-save-state')?.textContent);
  const context=norm(document.querySelector('.authoring-context')?.textContent);
  const terminal=/refusée|refusé|erreur|invalide|impossible/i.test(status);
  return {ready:status==='Session synchronisée'&&save===''&&context!==''&&context!=='Aucune carte ouverte'&&!terminal,
    terminal,reason:'status='+status+'; save='+save+'; context='+context};
})()
'@
}

function Return-Gate3RetouchToEditing {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    # The playtest toolbar is a sibling of the gameplay HUD, so
    # Get-PlaytestPublicState.allButtons intentionally cannot see it.
    $toolbar = Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const stops=[...document.querySelectorAll('.authoring-play-actions button')]
    .filter(node=>norm(node.textContent)==='Retour à l’édition');
  return {count:stops.length,text:stops.length===1?norm(stops[0].textContent):null,
    disabled:stops.length===1?!!stops[0].disabled:null};
})()
'@
    if ([int]$toolbar.count -ne 1) {
        throw "Public Studio return-to-editing button is missing or ambiguous: $(ConvertTo-CompactJson -Value $toolbar)"
    }
    if ([bool]$toolbar.disabled) {
        throw 'Public Studio return-to-editing button is disabled.'
    }
    if ([int]$toolbar.count -eq 1 -and -not [bool]$toolbar.disabled) {
        # Use the shared public-capture release contract: pointerLockElement can
        # be null while the runtime still advertises active input focus.
        Release-PlaytestCapture -Socket $Socket -TimeoutSeconds $TimeoutSeconds
        [void](Invoke-UiClick -Socket $Socket -Text "Retour à l’édition")
    }
    [void](Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description 'R3-P02 return to editing' -Expression @'
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const play=document.querySelector('button.authoring-play');
  const stop=[...document.querySelectorAll('.authoring-play-actions button')].find(node=>norm(node.textContent)==='Retour à l’édition');
  const disabled=stop?!!stop.disabled:null;
  const status=norm(document.querySelector('.authoring-command-status')?.textContent);
  const event=[...document.querySelectorAll('.authoring-events li[data-kind="error"]')].map(node=>norm(node.textContent)).filter(Boolean).pop()??'';
  const context=norm(document.querySelector('.authoring-context')?.textContent);
  const toolbar=document.querySelector('.authoring-toolbar');
  const busy=toolbar?.getAttribute('aria-busy')??'';
  const failure=[status,event].find(value=>/refusée|refusé|erreur|invalide|impossible|diagnostic/i.test(value))??'';
  const reason='play='+(!!play)+'; play_disabled='+(play?!!play.disabled:null)+'; stop='+(!!stop)+'; stop_disabled='+disabled+'; status='+status+'; event='+event+'; context='+context+'; busy='+busy;
  return {ready:!!play&&!play.disabled&&(!stop||disabled===true),
    terminal:failure!=='',
    reason:failure!==''?reason+'; failure='+failure:reason};
})()
'@
)
}

function Test-Gate3RetouchCompletedQuest {
    param([Parameter(Mandatory)]$State)
    return @($State.items | Where-Object { [string]$_ -match '^Rapporter le Sceau\s+·\s+Completed$' }).Count -eq 1 -and
        @($State.items | Where-Object { [string]$_ -ceq 'Sceau × 1' }).Count -eq 0
}

function Move-Gate3RetouchToDoor {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][double]$DoorX,
        [Parameter(Mandatory)][double]$DoorZ,
        [Parameter(Mandatory)][double]$RoomOriginX,
        [Parameter(Mandatory)][double]$RoomOriginZ,
        [Parameter(Mandatory)][double]$RoomWidth,
        [Parameter(Mandatory)][double]$RoomDepth,
        [Parameter(Mandatory)][double]$VillageX,
        [Parameter(Mandatory)][double]$VillageZ,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase
    )
    $westClearX = $RoomOriginX - ($RoomWidth / 2.0) - 1.0
    $southClearZ = $RoomOriginZ - ($RoomDepth / 2.0) - 3.0
    $startObservation = Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $Socket)
    if (-not $startObservation.ok -or -not [double]::IsFinite([double]$startObservation.z)) {
        throw "Public playtest position was not ready before the room-side route: $($startObservation.raw)"
    }
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'room_side_waypoints' -Payload ([ordered]@{
            west_clear_x = $westClearX
            south_clear_z = $southClearZ
            door_x = $DoorX
            door_z = $DoorZ
            start_z = [double]$startObservation.z
            village_x = $VillageX
            village_z = $VillageZ
            lateral_waypoint = [ordered]@{ x = $VillageX - 1.5; z = [double]$startObservation.z; name = 'village-lateral-clearance' }
            south_clear_margin_m = 3.0
            corridor_tolerance_m = 0.35
            corridor_waypoint_count = 5
            route_waypoint_count = 6
            corridor_waypoints = @(
                [ordered]@{ x = $VillageX - 1.5; z = $VillageZ + 1.0; name = 'npc-west-clearance' }
                [ordered]@{ x = $VillageX - 1.5; z = $VillageZ - 2.0; name = 'npc-south-clearance' }
                [ordered]@{ x = $westClearX; z = $VillageZ - 2.0; name = 'west-corridor' }
                [ordered]@{ x = $westClearX; z = $southClearZ; name = 'west-south' }
                [ordered]@{ x = $DoorX; z = $southClearZ; name = 'south-door' }
            )
            route = 'public P01 corridor: village west/south clearance, west corridor, west-south, then south-door'
        })
    $corridorWaypoints = @(
        [pscustomobject]@{ x = $VillageX - 1.5; z = $VillageZ + 1.0 }
        [pscustomobject]@{ x = $VillageX - 1.5; z = $VillageZ - 2.0 }
        [pscustomobject]@{ x = $westClearX; z = $VillageZ - 2.0 }
        [pscustomobject]@{ x = $westClearX; z = $southClearZ }
        [pscustomobject]@{ x = $DoorX; z = $southClearZ }
    )
    $lateralStartPoint = [pscustomobject]@{ x = $VillageX - 1.5; z = [double]$startObservation.z }
    $routeWaypoints = @($lateralStartPoint) + @($corridorWaypoints)
    [void](Move-PlaytestRoute -Socket $Socket -Waypoints $routeWaypoints -Tolerance 0.35 -MaxSeconds $TimeoutSeconds -Journal $Journal -Phase "$Phase-p01-corridor" -RouteId 'p01-public-village-west-south-door')
    # Keep the historical door approach tolerance after the reviewed corridor.
    Move-PlaytestToXZ -Socket $Socket -X $DoorX -Z $DoorZ -TimeoutSeconds $TimeoutSeconds -Journal $Journal -Phase "$Phase-door"
}

function Move-Gate3RetouchToVillage {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][double]$DoorX,
        [Parameter(Mandatory)][double]$DoorZ,
        [Parameter(Mandatory)][double]$RoomOriginX,
        [Parameter(Mandatory)][double]$RoomOriginZ,
        [Parameter(Mandatory)][double]$RoomWidth,
        [Parameter(Mandatory)][double]$RoomDepth,
        [Parameter(Mandatory)][double]$VillageX,
        [Parameter(Mandatory)][double]$VillageZ,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [AllowEmptyCollection()][Parameter(Mandatory)][System.Collections.Generic.List[object]]$Journal,
        [Parameter(Mandatory)][string]$Phase
    )
    $westClearX = $RoomOriginX - ($RoomWidth / 2.0) - 1.0
    $southClearZ = $RoomOriginZ - ($RoomDepth / 2.0) - 3.0
    Add-PlaytestJournal -Journal $Journal -Phase $Phase -Event 'room_side_waypoints' -Payload ([ordered]@{
            west_clear_x = $westClearX
            south_clear_z = $southClearZ
            door_x = $DoorX
            door_z = $DoorZ
            village_x = $VillageX
            village_z = $VillageZ
            south_clear_margin_m = 3.0
            route = 'public south-side then west-side waypoints around authored room'
        })
    Move-PlaytestToXZ -Socket $Socket -X $DoorX -Z $southClearZ -TimeoutSeconds $TimeoutSeconds -Journal $Journal -Phase "$Phase-south"
    Move-PlaytestToXZ -Socket $Socket -X $westClearX -Z $southClearZ -TimeoutSeconds $TimeoutSeconds -Journal $Journal -Phase "$Phase-west"
    Move-PlaytestToXZ -Socket $Socket -X $VillageX -Z $VillageZ -TimeoutSeconds $TimeoutSeconds -Journal $Journal -Phase "$Phase-village"
}

function Get-Gate3RetouchSourceSnapshots {
    param(
        [Parameter(Mandatory)][string]$SourceRoot,
        [Parameter(Mandatory)][string]$ZoneId,
        [Parameter(Mandatory)][string]$PinnedHash,
        [Parameter(Mandatory)][string]$CurrentHash,
        [Parameter(Mandatory)][string]$SaveRelativePath
    )
    $zoneArchive = Join-Path (Join-Path $SourceRoot $ZoneId) 'authoring.json'
    $workspaceArchive = Join-Path $SourceRoot 'workspace-v1\authoring.json'
    $pinnedObject = Join-Path (Join-Path $SourceRoot 'workspace-v1\objects') "$PinnedHash.json"
    $currentObject = Join-Path (Join-Path $SourceRoot 'workspace-v1\objects') "$CurrentHash.json"
    $save = Join-Path $SourceRoot $SaveRelativePath
    return [ordered]@{
        zone_archive = Get-ArchiveSnapshot -LiteralPath $zoneArchive
        workspace_archive = Get-ArchiveSnapshot -LiteralPath $workspaceArchive
        pinned_object = Get-ArchiveSnapshot -LiteralPath $pinnedObject
        current_object = Get-ArchiveSnapshot -LiteralPath $currentObject
        game_save = Get-ArchiveSnapshot -LiteralPath $save
    }
}

function Get-Gate3RetouchGameplayIdentity {
    param([Parameter(Mandatory)]$Report)
    $actors = @(Get-Gate3RetouchOptional -Object $Report -Name 'actors')
    $objects = @(Get-Gate3RetouchOptional -Object $Report -Name 'objects')
    $places = @(Get-Gate3RetouchOptional -Object $Report -Name 'places')
    $items = @(Get-Gate3RetouchOptional -Object $Report -Name 'items')
    $quests = @(Get-Gate3RetouchOptional -Object $Report -Name 'quests')
    $dialogues = @(Get-Gate3RetouchOptional -Object $Report -Name 'dialogues')
    return [ordered]@{
        actors = @($actors | ForEach-Object {
                [ordered]@{
                    id = [string](Get-Gate3RetouchOptional -Object $_ -Name 'id')
                    role = [string](Get-Gate3RetouchOptional -Object $_ -Name 'role')
                    # Get-Gate3GameplayReport intentionally projects actor
                    # identity without the raw dialogue link. Preserve an
                    # empty value for that projection instead of failing
                    # strict mode; dialogue content is checked separately.
                    dialogue = [string](Get-Gate3RetouchOptional -Object $_ -Name 'dialogue')
                }
            } | Sort-Object { $_.id })
        objects = @($objects | ForEach-Object {
                $kind = Get-Gate3RetouchOptional -Object $_ -Name 'kind'
                [ordered]@{
                    id = [string](Get-Gate3RetouchOptional -Object $_ -Name 'id')
                    kind = [string](Get-Gate3RetouchOptional -Object $kind -Name 'kind')
                    item = [string](Get-Gate3RetouchOptional -Object $kind -Name 'item')
                }
            } | Sort-Object { $_.id })
        places = @($places | ForEach-Object {
                [ordered]@{
                    id = [string](Get-Gate3RetouchOptional -Object $_ -Name 'id')
                    links = @(@(Get-Gate3RetouchOptional -Object $_ -Name 'links') | ForEach-Object { [string]$_ } | Sort-Object)
                }
            } | Sort-Object { $_.id })
        items = @($items | ForEach-Object {
                [ordered]@{
                    id = [string](Get-Gate3RetouchOptional -Object $_ -Name 'id')
                    name = [string](Get-Gate3RetouchOptional -Object $_ -Name 'name')
                    max_stack = [int](Get-Gate3RetouchOptional -Object $_ -Name 'max_stack')
                }
            } | Sort-Object { $_.id })
        quests = @($quests | ForEach-Object {
                [ordered]@{
                    id = [string](Get-Gate3RetouchOptional -Object $_ -Name 'id')
                    giver = [string](Get-Gate3RetouchOptional -Object $_ -Name 'giver')
                    required_item = [string](Get-Gate3RetouchOptional -Object $_ -Name 'required_item')
                    required_quantity = [int](Get-Gate3RetouchOptional -Object $_ -Name 'required_quantity')
                    reward_item = [string](Get-Gate3RetouchOptional -Object $_ -Name 'reward_item')
                    reward_quantity = [int](Get-Gate3RetouchOptional -Object $_ -Name 'reward_quantity')
                }
            } | Sort-Object { $_.id })
        dialogues = @($dialogues | ForEach-Object {
                [ordered]@{
                    id = [string](Get-Gate3RetouchOptional -Object $_ -Name 'id')
                    start = [string](Get-Gate3RetouchOptional -Object $_ -Name 'start')
                    nodes = @(@(Get-Gate3RetouchOptional -Object $_ -Name 'nodes') | ForEach-Object {
                            [ordered]@{
                                id = [string](Get-Gate3RetouchOptional -Object $_ -Name 'id')
                                choices = @(@(Get-Gate3RetouchOptional -Object $_ -Name 'choices') | ForEach-Object {
                                        [string](Get-Gate3RetouchOptional -Object $_ -Name 'id')
                                    } | Sort-Object)
                            }
                        } | Sort-Object { $_.id })
                }
            } | Sort-Object { $_.id })
    }
}

if ($retouchRequestedSelfTest) {
    foreach ($name in @(
        'ConvertTo-CompactJson', 'ConvertFrom-ExactJson', 'Invoke-CdpCommand',
        'Invoke-CdpExpression', 'Wait-CdpReady', 'Wait-CdpTarget', 'Connect-CdpSocket',
        'New-LoopbackPortReservation', 'Wait-LaunchedExecutableIdentity', 'Send-CdpMouseClick',
        'Send-CdpKeyStroke', 'Invoke-UiClick', 'Invoke-UiClickInDetailsBySummary',
        'Open-DetailsBySummary', 'Copy-IsolatedFixture', 'Get-ArchiveSnapshot',
        'Test-ArchiveSnapshotsEqual', 'Get-FileSha256', 'Get-NormalizedSha256',
        'Assert-DriveEPath', 'Assert-NoExistingReparseAncestor', 'Assert-TreeHasNoReparsePoints',
        'Test-PathWithinDirectory', 'Test-IsWindowsPlatform', 'Get-Gate3WorkspaceReference',
        'Get-Gate3GameplayReport', 'Set-Gate3UiTextByLabel', 'Set-UiControlByLabelWithKeyboard',
        'Get-UiControlByLabel', 'Select-GameplayOptionByText', 'Invoke-GameplayPreviewApply',
        'Open-Gate3PlaytestZone', 'Get-PlaytestPublicState', 'Get-PlaytestObservation',
        'Focus-PlaytestCanvas', 'Move-PlaytestToXZ', 'Wait-PlaytestSettled',
        'Observe-PlaytestFacing',
        'Wait-PlaytestCondition', 'Invoke-PlaytestGameplayAction',
        'Get-Gate3RetouchGameplayIdentity', 'Move-Gate3RetouchToDoor', 'Move-Gate3RetouchToVillage',
        'Start-ForgeStudioPlaytestProcess', 'Stop-ForgeStudioPlaytestProcess'
    )) {
        [void](Get-Command $name -ErrorAction Stop)
    }
    foreach ($binding in @(
        [ordered]@{ command = 'Set-Gate3UiTextByLabel'; parameter = 'Text' }
    )) {
        $metadata = (Get-Command $binding.command -ErrorAction Stop).Parameters[$binding.parameter]
        $allowsEmpty = @($metadata.Attributes | Where-Object {
            $_.GetType().Name -eq 'AllowEmptyStringAttribute'
        }).Count -gt 0
        if (-not $allowsEmpty) {
            throw "Public helper binding $($binding.command).$($binding.parameter) does not allow empty text."
        }
    }
    if ($retouchZoneId -cnotmatch '^[0-9a-f-]{36}$') {
        throw 'R3-P02 zone UUID selector is malformed.'
    }
    $raw = [IO.File]::ReadAllText($PSCommandPath)
    foreach ($needle in @(
        'Reprendre la partie',
        'Reprendre avec les retouches compatibles',
        'Tester & suivi',
        'source changed; explicitly review compatible edits',
        'Partie sauvegardée.',
        'Sauvegarder la partie',
        "Retour à l’édition",
        'Porte intérieure',
        'Intendant du village',
        'Examiner le nom, la position et la réplique',
        'functional_hash',
        'door_x_delta_m',
        'preserved_y_z',
        'interior_place_preserved',
        'ordinary_resume_refused',
        'compatible_resume_started',
        'cross_interior_kcc',
        'source_originals_immutable',
        '50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720',
        'authoring_ledger',
        'saved_reference',
        'authored_saved_reference',
        'source_fixture',
        'isolated_fixture',
        'parent_candidate_sha256',
        'candidate_differs_from_parent',
        'candidate_same_as_parent_allowed',
        'AllowSameCandidate',
        'source_current_checkpoint_matches_authoring',
        'gate_qualified = $false',
        'Input.dispatchKeyEvent',
        'Input.dispatchMouseEvent',
        'rawKeyDown',
        'keyUp',
        'Escape',
        'CloseMainWindow()',
        'dialog.gold-close-dialog[open]',
        'Quitter sans enregistrer',
        'GracefulCloseFailure',
        'native_close_confirmed',
        'Runtime domain state',
        'R3-P02'
    )) {
        if ($raw.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) {
            throw "R3-P02 selector or evidence field missing: $needle"
        }
    }
    if ($raw -match 'Runtime\.evaluate[^\r\n]*window\.__') {
        throw 'SelfTest found a domain-state injection pattern.'
    }
    Write-Output 'TIDEWATER_GATE3_COMPATIBLE_RETOUCH_SELF_TEST_PASS'
    return
}

if (-not (Test-IsWindowsPlatform)) {
    throw 'Windows/WebView2 is required.'
}

$candidate = [IO.Path]::GetFullPath($Candidate)
$source = [IO.Path]::GetFullPath($FreshIsolatedFixture)
$ledgerInput = [IO.Path]::GetFullPath($PreviousLedger)
$output = [IO.Path]::GetFullPath($EvidenceRoot)
$sourcePin = Get-NormalizedSha256 -Value $SourceOriginalHashPinned
foreach ($entry in @(
        @($candidate, 'Candidate'),
        @($source, 'Fresh isolated playtest fixture'),
        @($ledgerInput, 'Previous playtest ledger'),
        @($output, 'Evidence root')
    )) {
    Assert-DriveEPath -LiteralPath $entry[0] -Description $entry[1]
    Assert-NoExistingReparseAncestor -LiteralPath $entry[0] -Description $entry[1]
}
if (Test-Path -LiteralPath $output) {
    throw "EvidenceRoot must be fresh: $output"
}
if ((Test-PathWithinDirectory -LiteralPath $output -Directory $source) -or
    (Test-PathWithinDirectory -LiteralPath $source -Directory $output)) {
    throw 'EvidenceRoot and the input fixture must remain separate.'
}
if ((Test-PathWithinDirectory -LiteralPath $output -Directory (Split-Path -Parent $candidate)) -or
    (Test-PathWithinDirectory -LiteralPath (Split-Path -Parent $candidate) -Directory $output)) {
    throw 'EvidenceRoot and the candidate directory must remain separate.'
}
if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
    throw "Candidate is absent: $candidate"
}
if (-not (Test-Path -LiteralPath $ledgerInput -PathType Leaf)) {
    throw "Previous ledger is absent: $ledgerInput"
}
if (-not (Test-Path -LiteralPath $source -PathType Container)) {
    throw "Input playtest fixture is absent: $source"
}
Assert-TreeHasNoReparsePoints -LiteralPath $source -Description 'R3-P02 input fixture'
$candidateHash = Get-NormalizedSha256 -Value (Get-FileSha256 -LiteralPath $candidate)

$previous = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($ledgerInput))
if ([string](Get-Gate3RetouchOptional -Object $previous -Name 'schema') -cne 'forge.tidewater.gate3-gameplay-playtest-smoke' -or
    [string](Get-Gate3RetouchOptional -Object $previous -Name 'status') -cne 'passed') {
    throw 'Previous ledger is not a passed Gate 3 gameplay playtest ledger.'
}
if ([bool](Get-Gate3RetouchOptional -Object $previous -Name 'gate_qualified')) {
    throw 'Previous playtest ledger incorrectly claims Gate qualification.'
}
if (-not [bool](Get-Gate3RetouchOptional -Object $previous -Name 'source_preserved')) {
    throw 'Previous playtest ledger does not attest input source preservation.'
}
foreach ($field in @('run1_completed', 'run2_completed')) {
    if (-not [bool](Get-Gate3RetouchOptional -Object $previous -Name $field)) {
        throw "Previous playtest ledger does not contain a completed $field."
    }
}
$previousCandidateHash = [string](Get-Gate3RetouchOptional -Object $previous -Name 'candidate_sha256')
if ([string]::IsNullOrWhiteSpace($previousCandidateHash)) {
    throw 'Previous playtest ledger has no parent candidate SHA-256 authorization record.'
}
$previousCandidateHash = Get-NormalizedSha256 -Value $previousCandidateHash
if ($previousCandidateHash -notmatch '^[0-9a-f]{64}$') {
    throw 'Previous playtest ledger has an invalid parent candidate SHA-256.'
}
$candidateDiffersFromParent = $candidateHash -cne $previousCandidateHash
if (-not $candidateDiffersFromParent -and -not $AllowSameCandidate) {
    throw 'R3-P02 candidate matches the parent playtest pin; rerun with -AllowSameCandidate and record that explicit choice.'
}
$previousSourcePin = [string](Get-Gate3RetouchOptional -Object $previous -Name 'source_original_hash_pinned')
if ([string]::IsNullOrWhiteSpace($previousSourcePin) -or
    (Get-NormalizedSha256 -Value $previousSourcePin) -cne $sourcePin) {
    throw 'Source hash pin differs from the previous playtest ledger.'
}
$authoringLedgerPathValue = [string](Get-Gate3RetouchOptional -Object $previous -Name 'authoring_ledger')
if ([string]::IsNullOrWhiteSpace($authoringLedgerPathValue)) {
    throw 'Previous playtest ledger has no authoring ledger reference.'
}
$authoringLedgerPath = [IO.Path]::GetFullPath($authoringLedgerPathValue)
Assert-DriveEPath -LiteralPath $authoringLedgerPath -Description 'Passed authoring ledger'
Assert-NoExistingReparseAncestor -LiteralPath $authoringLedgerPath -Description 'Passed authoring ledger'
if (-not (Test-Path -LiteralPath $authoringLedgerPath -PathType Leaf)) {
    throw "Authoring ledger is absent: $authoringLedgerPath"
}
$authoring = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($authoringLedgerPath))
if ([string](Get-Gate3RetouchOptional -Object $authoring -Name 'schema') -cne 'forge.tidewater.gate3-gameplay-authoring-smoke' -or
    [string](Get-Gate3RetouchOptional -Object $authoring -Name 'status') -cne 'passed') {
    throw 'Referenced authoring ledger is not a passed gameplay authoring ledger.'
}
if ([bool](Get-Gate3RetouchOptional -Object $authoring -Name 'gate_qualified') -or
    -not [bool](Get-Gate3RetouchOptional -Object $authoring -Name 'source_preserved')) {
    throw 'Referenced authoring ledger does not preserve the local-only source boundary.'
}
$authoringSavedReference = Get-Gate3RetouchOptional -Object $authoring -Name 'saved_reference'
if ($null -eq $authoringSavedReference) {
    throw 'Referenced authoring ledger has no saved_reference checkpoint.'
}
$authoringArchiveHash = Get-NormalizedSha256 -Value ([string]$authoringSavedReference.archive_hash)
$authoringHeadHash = Get-NormalizedSha256 -Value ([string]$authoringSavedReference.head_hash)
if ($authoringArchiveHash -notmatch '^[0-9a-f]{64}$' -or
    $authoringHeadHash -notmatch '^[0-9a-f]{64}$') {
    throw 'Referenced authoring saved_reference archive/head is malformed.'
}
$authoringFixtureValue = [string](Get-Gate3RetouchOptional -Object $authoring -Name 'isolated_fixture')
if ([string]::IsNullOrWhiteSpace($authoringFixtureValue)) {
    throw 'Referenced authoring ledger has no isolated fixture path.'
}
$authoringFixturePath = [IO.Path]::GetFullPath($authoringFixtureValue)
$previousSourceFixtureValue = [string](Get-Gate3RetouchOptional -Object $previous -Name 'source_fixture')
$previousIsolatedFixtureValue = [string](Get-Gate3RetouchOptional -Object $previous -Name 'isolated_fixture')
if ([string]::IsNullOrWhiteSpace($previousSourceFixtureValue) -or
    [string]::IsNullOrWhiteSpace($previousIsolatedFixtureValue)) {
    throw 'Previous playtest ledger must record both source_fixture and isolated_fixture paths.'
}
$previousSourceFixturePath = [IO.Path]::GetFullPath($previousSourceFixtureValue)
$previousIsolatedFixturePath = [IO.Path]::GetFullPath($previousIsolatedFixtureValue)
if (-not $previousSourceFixturePath.Equals($authoringFixturePath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Previous playtest source_fixture does not point to the passed authoring fixture.'
}
if (-not $previousIsolatedFixturePath.Equals($source, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'FreshIsolatedFixture must be the isolated fixture produced by the passed playtest ledger.'
}
if ($authoringFixturePath.Equals($source, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'FreshIsolatedFixture must remain distinct from the passed authoring fixture.'
}

$sourceArchive = Join-Path (Join-Path $source $retouchZoneId) 'authoring.json'
$sourceWorkspaceArchive = Join-Path $source 'workspace-v1\authoring.json'
foreach ($path in @($sourceArchive, $sourceWorkspaceArchive)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Input fixture archive is absent: $path"
    }
}
$sourceReference = Get-Gate3WorkspaceReference -Archive $sourceWorkspaceArchive -ZoneId $retouchZoneId
$currentArchiveHash = Get-NormalizedSha256 -Value ([string]$sourceReference.archive_hash)
$currentHeadHash = Get-NormalizedSha256 -Value ([string]$sourceReference.head_hash)
if ($currentArchiveHash -ceq $sourcePin -or $currentHeadHash -ceq $sourcePin) {
    throw 'FreshIsolatedFixture incorrectly aliases the legacy source pin as its authored checkpoint.'
}
$previousAuthoredCheckpoint = Get-Gate3RetouchOptional -Object $previous -Name 'authored_checkpoint'
$previousAuthoredHeadHash = if ($null -ne $previousAuthoredCheckpoint) {
    Get-NormalizedSha256 -Value ([string](Get-Gate3RetouchOptional -Object $previousAuthoredCheckpoint -Name 'head_hash'))
} else {
    $authoringHeadHash
}
if ($previousAuthoredHeadHash -notmatch '^[0-9a-f]{64}$') {
    throw 'Previous playtest ledger has no valid authored checkpoint head hash.'
}
$sourceCurrentCheckpointMatchesAuthoring = $currentArchiveHash -ceq $authoringArchiveHash -and
    $currentHeadHash -ceq $authoringHeadHash
if (-not $sourceCurrentCheckpointMatchesAuthoring) {
    throw 'Input fixture workspace reference does not match the passed authoring saved_reference checkpoint.'
}
$sourcePinnedObject = Join-Path (Join-Path $source 'workspace-v1\objects') "$sourcePin.json"
$sourceCurrentObject = Join-Path (Join-Path $source 'workspace-v1\objects') "$currentArchiveHash.json"
if (-not (Test-Path -LiteralPath $sourcePinnedObject -PathType Leaf)) {
    throw "Pinned original object is absent: $sourcePinnedObject"
}
if (-not (Test-Path -LiteralPath $sourceCurrentObject -PathType Leaf)) {
    throw "Current authored gameplay object is absent: $sourceCurrentObject"
}
$sourceArchiveBefore = Get-ArchiveSnapshot -LiteralPath $sourceArchive
$sourceWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $sourceWorkspaceArchive
$sourcePinnedObjectBefore = Get-ArchiveSnapshot -LiteralPath $sourcePinnedObject
$sourceCurrentObjectBefore = Get-ArchiveSnapshot -LiteralPath $sourceCurrentObject
if ($sourceArchiveBefore.Sha256 -cne $sourcePin -or $sourcePinnedObjectBefore.Sha256 -cne $sourcePin) {
    throw 'Input fixture does not preserve SourceOriginalHashPinned.'
}
if ($sourceCurrentObjectBefore.Sha256 -cne $currentArchiveHash) {
    throw 'Workspace reference hash does not match the current authored object bytes.'
}
$sourcePlacementsBefore = Get-Gate3ArchivePlacementsSnapshot -Archive $sourceArchive
Assert-Gate3PlacementsPreserved -Before $sourcePlacementsBefore -After (Get-Gate3ArchivePlacementsSnapshot -Archive $sourcePinnedObject)
Assert-Gate3PlacementsPreserved -Before $sourcePlacementsBefore -After (Get-Gate3ArchivePlacementsSnapshot -Archive $sourceCurrentObject)
Assert-Gate3PlacementsLedger -Ledger $authoring -SourcePin $sourcePin -Baseline $sourcePlacementsBefore
Assert-Gate3PlacementsLedger -Ledger $previous -SourcePin $sourcePin -Baseline $sourcePlacementsBefore
$sourceCurrentDocument = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($sourceCurrentObject))
$sourceCorrections = Get-Gate3RetouchOptional -Object (Get-Gate3RetouchOptional -Object $sourceCurrentDocument.head -Name 'corrections') -Name 'buildings'
$sourceBuildingEntries = @($sourceCorrections)
if ($sourceBuildingEntries.Count -ne 1) {
    throw 'Input playtest object must contain exactly one generic authored room.'
}
$sourceBuilding = $sourceBuildingEntries[0]
$sourceBuildingId = [string]$sourceBuilding.id
$sourceBuildingOrigin = $sourceBuilding.origin_m
$sourceBuildingDimensions = $sourceBuilding.dimensions
$sourceBuildingOriginX = [double]$sourceBuildingOrigin.x
$sourceBuildingOriginZ = [double]$sourceBuildingOrigin.z
$sourceBuildingWidth = [double]$sourceBuildingDimensions.width_m
$sourceBuildingDepth = [double]$sourceBuildingDimensions.depth_m
if ([string]::IsNullOrWhiteSpace($sourceBuildingId) -or
    -not [double]::IsFinite($sourceBuildingOriginX) -or
    -not [double]::IsFinite($sourceBuildingOriginZ) -or
    -not [double]::IsFinite($sourceBuildingWidth) -or
    -not [double]::IsFinite($sourceBuildingDepth) -or
    $sourceBuildingWidth -le 0 -or $sourceBuildingDepth -le 0) {
    throw 'Input generic room has an invalid identity, pose or dimensions.'
}
$sourceReport = Get-Gate3GameplayReport -Archive $sourceCurrentObject
$sourceGameplayIdentity = Get-Gate3RetouchGameplayIdentity -Report $sourceReport
$sourceGameplayIdentityJson = ConvertTo-CompactJson -Value $sourceGameplayIdentity
if (-not $sourceReport.present) {
    throw 'Input playtest object has no authored gameplay correction.'
}
$sourceActorNames = @($sourceReport.actors | ForEach-Object { [string]$_.name })
$sourceObjectNames = @($sourceReport.objects | ForEach-Object { [string]$_.name })
$sourcePlaceNames = @($sourceReport.places | ForEach-Object { [string]$_.name })
if ($sourceActorNames -notcontains $retouchIntendantName -or
    $sourceObjectNames -notcontains $retouchDoorName -or
    $sourcePlaceNames -notcontains 'Village' -or
    $sourcePlaceNames -notcontains 'Intérieur') {
    throw 'Input playtest object is missing the R3-P02 Intendant, door or reviewed places.'
}
$doorBeforeEntries = @($sourceReport.objects | Where-Object {
    [string]$_.name -ceq $retouchDoorName -and [string]$_.kind.kind -ceq 'door'
})
if ($doorBeforeEntries.Count -ne 1) {
    throw 'Input playtest object must contain exactly one Porte intérieure door.'
}
$interiorPlaceEntries = @($sourceReport.places | Where-Object {
    [string]$_.name -ceq $retouchInteriorPlaceName
})
if ($interiorPlaceEntries.Count -ne 1) {
    throw "Input playtest object must contain exactly one $retouchInteriorPlaceName place."
}
$doorBeforePosition = $doorBeforeEntries[0].position
$doorXBefore = [double]$doorBeforePosition.x
$doorYBefore = [double]$doorBeforePosition.y
$doorZBefore = [double]$doorBeforePosition.z
if (-not [double]::IsFinite($doorXBefore) -or -not [double]::IsFinite($doorYBefore) -or
    -not [double]::IsFinite($doorZBefore)) {
    throw "Input door pose is not finite: x=$doorXBefore y=$doorYBefore z=$doorZBefore"
}
$interiorBeforePosition = $interiorPlaceEntries[0].position
$interiorBeforeId = [string]$interiorPlaceEntries[0].id
$interiorXBefore = [double]$interiorBeforePosition.x
$interiorYBefore = [double]$interiorBeforePosition.y
$interiorZBefore = [double]$interiorBeforePosition.z
if ([string]::IsNullOrWhiteSpace($interiorBeforeId) -or
    -not [double]::IsFinite($interiorXBefore) -or
    -not [double]::IsFinite($interiorYBefore) -or
    -not [double]::IsFinite($interiorZBefore)) {
    throw "$retouchInteriorPlaceName has an invalid authored identity or position."
}

$sourceSaveInfo = Get-Gate3RetouchSaveFile -FixtureRoot $source -ExpectedSourceHashes @($currentHeadHash, $previousAuthoredHeadHash) -AreaId $retouchZoneId
$sourceSavePath = $sourceSaveInfo.File.FullName
$sourceSaveSourceHash = [string]$sourceSaveInfo.SourceHash
$sourceSaveRelativePath = [IO.Path]::GetRelativePath($source, $sourceSavePath)
if ($sourceSaveRelativePath.StartsWith('..', [StringComparison]::Ordinal)) {
    throw 'Game save is outside the authorized input fixture.'
}
$sourceSaveBefore = Get-ArchiveSnapshot -LiteralPath $sourceSavePath
$sourceSaveDocumentBefore = $sourceSaveInfo.Document
if ([string](Get-Gate3RetouchOptional -Object $sourceSaveDocumentBefore -Name 'schema') -cne 'forge.game-save' -or
    $null -eq (Get-Gate3RetouchOptional -Object $sourceSaveDocumentBefore -Name 'state')) {
    throw 'Input fixture game save is not a complete forge.game-save document.'
}

[void](New-Item -ItemType Directory -Path $output)
$outputCreated = $true
$fixture = Join-Path $output 'compatible-retouch-root'
$cloneArchive = Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $retouchZoneId
$cloneWorkspace = Join-Path $fixture 'workspace-v1\authoring.json'
$cloneCurrentObject = Join-Path (Join-Path $fixture 'workspace-v1\objects') "$currentArchiveHash.json"
$clonePinnedObject = Join-Path (Join-Path $fixture 'workspace-v1\objects') "$sourcePin.json"
$cloneSavePath = Join-Path $fixture $sourceSaveRelativePath
$cloneArchiveBefore = Get-ArchiveSnapshot -LiteralPath $cloneArchive
$cloneWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $cloneWorkspace
$cloneCurrentObjectBefore = Get-ArchiveSnapshot -LiteralPath $cloneCurrentObject
$clonePinnedObjectBefore = Get-ArchiveSnapshot -LiteralPath $clonePinnedObject
$cloneSaveBefore = Get-ArchiveSnapshot -LiteralPath $cloneSavePath
$cloneReportBefore = Get-Gate3GameplayReport -Archive $cloneCurrentObject
$cloneReferenceBefore = Get-Gate3WorkspaceReference -Archive $cloneWorkspace -ZoneId $retouchZoneId
if ((Get-NormalizedSha256 -Value ([string]$cloneReferenceBefore.archive_hash)) -cne $currentArchiveHash) {
    throw 'Isolated fixture workspace reference changed during the copy.'
}

$savedEnvironment = @{}
foreach ($name in @(
        'FORGE_AUTHORING_ROOT',
        'FORGE_AUTHOR_SUBJECT',
        'WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS',
        'WEBVIEW2_USER_DATA_FOLDER'
    )) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$journal = [System.Collections.Generic.List[object]]::new()
$run1 = $null
$run2 = $null
$run1Stopped = $true
$run2Stopped = $true
$run1NativeCloseConfirmed = $false
$run2NativeCloseConfirmed = $false
$run1Completed = $false
$run2Completed = $false
$ordinaryResumeRefused = $false
$compatibleResumeStarted = $false
$retouchSaved = $false
$crossInteriorKcc = $false
$forcedProcessStop = $false
$failure = $null
$ordinaryRefusalEvidence = $null
$run1DoorSave = $null
$run1DoorSaveAfter = $null
$savedReference = $null
$savedObjectAfter = $null
$savedPlacementsSnapshot = $null
$placementsPreserved = $false
$retouchReportAfter = $null
$retouchGameplayIdentity = $null
$doorAfterEntry = $null
$interiorAfterEntry = $null
$retouchWorkspaceAfter = $null
$retouchCloneOriginalPreserved = $false
$sourcePreserved = $false
$replyBefore = $null
$updatedReply = $null
$doorXAfter = $null
$doorXAfterObserved = $null
$doorYAfterObserved = $null
$doorZAfterObserved = $null
$interiorXAfter = $null
$interiorYAfter = $null
$interiorZAfter = $null
$retouchWorkspaceBefore = $null
$openText = $null
$closeText = $null
$sourceSaveRelativePath = [string]$sourceSaveRelativePath

try {
    try {
        $run1 = Start-ForgeStudioPlaytestProcess -CandidatePath $candidate -CandidateHash $candidateHash -FixtureRoot $fixture -EvidencePath $output -RunNumber 1 -StartupTimeout $StartupTimeoutSeconds
        $run1Stopped = $false
        Open-Gate3PlaytestZone -Socket $run1.Socket -ZoneId $retouchZoneId -TimeoutSeconds $InteractionTimeoutSeconds
        [void](Invoke-UiClick -Socket $run1.Socket -Text 'Tester & suivi' -Selector '[data-authoring-tab-button]')
        # The input fixture comes from a completed P01 playtest and already has
        # a stored game save. Load it through the public control first so the
        # save CAS hash is carried by the UI; a fresh Jouer run would submit
        # expected_stored=null and correctly be rejected by DocumentStore.
        [void](Invoke-UiClick -Socket $run1.Socket -Text 'Reprendre la partie' -Selector 'section.authoring-gameplay-controls button')
        [void](Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'R3-P02 initial save resume acknowledgement' -Condition {
            param($state)
            @($state.outputs | Where-Object { [string]$_ -ceq 'Reprise préparée.' }).Count -ge 1
        })
        [void](Wait-PlaytestReady -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'R3-P02 initial resumed public playtest')
        Focus-PlaytestCanvas -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds
        $initialResumeState = Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'R3-P02 initial save preserves completed quest' -Condition {
            param($state)
            (Test-Gate3RetouchCompletedQuest -State $state) -and $state.ready
        }
        Add-PlaytestJournal -Journal $journal -Phase 'run1' -Event 'initial_save_resume_verified' -Payload ([ordered]@{
                observation = Get-PlaytestObservation -State $initialResumeState
                items = @($initialResumeState.items)
                save_path = $sourceSaveRelativePath
                save_before = $cloneSaveBefore
            })
        Add-PlaytestJournal -Journal $journal -Phase 'run1' -Event 'playtest_started' -Payload (Get-PlaytestObservation -State (Get-PlaytestPublicState -Socket $run1.Socket))
        Move-Gate3RetouchToDoor -Socket $run1.Socket -DoorX $doorXBefore -DoorZ $doorZBefore -RoomOriginX $sourceBuildingOriginX -RoomOriginZ $sourceBuildingOriginZ -RoomWidth $sourceBuildingWidth -RoomDepth $sourceBuildingDepth -VillageX $retouchVillageX -VillageZ $retouchVillageZ -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'door_approach'
        [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'door_approach')
        $openText = "Ouvrir $retouchDoorName"
        [void](Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'visible door open action' -Condition {
            param($state)
            @($state.actions | Where-Object { [string]$_.text -ceq $openText -and -not [bool]$_.disabled }).Count -eq 1
        })
        [void](Invoke-PlaytestGameplayAction -Socket $run1.Socket -Text $openText -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'door_open')
        $closeText = "Fermer $retouchDoorName"
        $openedState = Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'visible open-door state' -Condition {
            param($state)
            @($state.actions | Where-Object { [string]$_.text -ceq $closeText -and -not [bool]$_.disabled }).Count -eq 1
        }
        Add-PlaytestJournal -Journal $journal -Phase 'door_open' -Event 'door_open_visible' -Payload ([ordered]@{
            observation = Get-PlaytestObservation -State $openedState
            action = $closeText
        })
        [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'save_at_rest')
        Release-PlaytestCapture -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds
        [void](Invoke-UiClick -Socket $run1.Socket -Text 'Sauvegarder la partie')
        $run1DoorSave = Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'real game save acknowledgement at rest' -Condition {
            param($state)
            @($state.outputs | Where-Object { [string]$_ -ceq 'Partie sauvegardée.' }).Count -ge 1
        }
        $run1DoorSaveAfter = Get-ArchiveSnapshot -LiteralPath $cloneSavePath
        if (Test-ArchiveSnapshotsEqual -Before $cloneSaveBefore -After $run1DoorSaveAfter) {
            throw 'Public save acknowledgement did not produce a new game-save SHA/mtime.'
        }
        Add-PlaytestJournal -Journal $journal -Phase 'save_at_rest' -Event 'save_acknowledged' -Payload ([ordered]@{
            observation = Get-PlaytestObservation -State $run1DoorSave
            save_path = $sourceSaveRelativePath
            save_before = $cloneSaveBefore
            save_after = $run1DoorSaveAfter
        })
        Return-Gate3RetouchToEditing -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds
        [void](Invoke-UiClick -Socket $run1.Socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
        [void](Wait-GameplayEditor -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds)
        Open-DetailsBySummary -Socket $run1.Socket -Summary $retouchEditorSummary | Out-Null

        Select-GameplayOptionByText -Socket $run1.Socket -Label 'Élément à retoucher' -Text $retouchIntendantName -TimeoutSeconds $InteractionTimeoutSeconds
        [void](Wait-Gate3RetouchSelectedElement -Socket $run1.Socket -Name $retouchIntendantName -TimeoutSeconds $InteractionTimeoutSeconds)
        $replyBeforeControl = Get-UiControlByLabel -Socket $run1.Socket -Label 'Réplique' -DetailsSummary $retouchEditorSummary
        $replyBefore = [string]$replyBeforeControl.value
        $updatedReply = 'Le sceau est au-delà de la forêt. Revenez au village avec le sceau.'
        if ($replyBefore -ceq $updatedReply) {
            throw 'Input fixture already contains the R3-P02 reply retouch; a visible edit would not be exercised.'
        }
        [void](Set-Gate3UiTextByLabel -Socket $run1.Socket -Label 'Réplique' -Text $updatedReply -DetailsSummary $retouchEditorSummary)
        [void](Invoke-GameplayPreviewApply -Socket $run1.Socket -PreviewButton 'Examiner le nom, la position et la réplique' -Description 'retouch Intendant reply' -TimeoutSeconds $InteractionTimeoutSeconds)

        Select-GameplayOptionByText -Socket $run1.Socket -Label 'Élément à retoucher' -Text $retouchDoorName -TimeoutSeconds $InteractionTimeoutSeconds
        [void](Wait-Gate3RetouchSelectedElement -Socket $run1.Socket -Name $retouchDoorName -TimeoutSeconds $InteractionTimeoutSeconds)
        $doorXControl = Get-UiControlByLabel -Socket $run1.Socket -Label 'X (m)' -DetailsSummary $retouchEditorSummary
        $doorYControl = Get-UiControlByLabel -Socket $run1.Socket -Label 'Hauteur du centre (m, vide = terrain)' -DetailsSummary $retouchEditorSummary
        $doorZControl = Get-UiControlByLabel -Socket $run1.Socket -Label 'Z (m)' -DetailsSummary $retouchEditorSummary
        $doorXUiBefore = ConvertTo-Gate3RetouchNumber -Text ([string]$doorXControl.value)
        $doorYUiBefore = ConvertTo-Gate3RetouchNumber -Text ([string]$doorYControl.value)
        $doorZUiBefore = ConvertTo-Gate3RetouchNumber -Text ([string]$doorZControl.value)
        if ([Math]::Abs($doorXUiBefore - $doorXBefore) -gt 0.001 -or
            [Math]::Abs($doorYUiBefore - $doorYBefore) -gt 0.001 -or
            [Math]::Abs($doorZUiBefore - $doorZBefore) -gt 0.001) {
            throw "Door UI pose differs from the source pose: x=$doorXUiBefore y=$doorYUiBefore z=$doorZUiBefore"
        }
        $doorXAfter = $doorXUiBefore + 0.05
        $doorXAfterText = $doorXAfter.ToString('R', [Globalization.CultureInfo]::InvariantCulture)
        [void](Set-Gate3UiTextByLabel -Socket $run1.Socket -Label 'Nom' -Text $retouchDoorName -DetailsSummary $retouchEditorSummary)
        [void](Set-Gate3UiTextByLabel -Socket $run1.Socket -Label 'X (m)' -Text $doorXAfterText -DetailsSummary $retouchEditorSummary)
        [void](Set-Gate3UiTextByLabel -Socket $run1.Socket -Label 'Z (m)' -Text ([string]$doorZControl.value) -DetailsSummary $retouchEditorSummary)
        [void](Set-Gate3UiTextByLabel -Socket $run1.Socket -Label 'Hauteur du centre (m, vide = terrain)' -Text ([string]$doorYControl.value) -DetailsSummary $retouchEditorSummary)
        [void](Invoke-GameplayPreviewApply -Socket $run1.Socket -PreviewButton 'Examiner le nom, la position et la réplique' -Description 'retouch interior door X only' -TimeoutSeconds $InteractionTimeoutSeconds)

        $retouchWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $cloneWorkspace
        [void](Invoke-UiClick -Socket $run1.Socket -Text 'Sauvegarder')
        [void](Wait-Gate3RetouchAuthoringSave -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds)
        $retouchWorkspaceAfter = Get-ArchiveSnapshot -LiteralPath $cloneWorkspace
        if (Test-ArchiveSnapshotsEqual -Before $retouchWorkspaceBefore -After $retouchWorkspaceAfter) {
            throw 'Authoring Save did not update the managed workspace checkpoint after R3-P02 edits.'
        }
        $savedReference = Get-Gate3WorkspaceReference -Archive $cloneWorkspace -ZoneId $retouchZoneId
        $savedArchiveHash = Get-NormalizedSha256 -Value ([string]$savedReference.archive_hash)
        if ($savedArchiveHash -ceq $currentArchiveHash) {
            throw 'R3-P02 Save did not create a new authored gameplay object.'
        }
        $savedObjectPath = Join-Path (Join-Path $fixture 'workspace-v1\objects') "$savedArchiveHash.json"
        $savedObjectAfter = Get-ArchiveSnapshot -LiteralPath $savedObjectPath
        if ($savedObjectAfter.Sha256 -cne $savedArchiveHash) {
            throw 'Retouched gameplay object hash does not match its managed filename.'
        }
        $savedPlacementsSnapshot = Get-Gate3ArchivePlacementsSnapshot -Archive $savedObjectPath
        Assert-Gate3PlacementsPreserved -Before $sourcePlacementsBefore -After $savedPlacementsSnapshot
        $placementsPreserved = $true
        $retouchReportAfter = Get-Gate3GameplayReport -Archive $savedObjectPath
        $retouchGameplayIdentity = Get-Gate3RetouchGameplayIdentity -Report $retouchReportAfter
        $retouchGameplayIdentityJson = ConvertTo-CompactJson -Value $retouchGameplayIdentity
        if ($retouchGameplayIdentityJson -cne $sourceGameplayIdentityJson) {
            throw 'Retouched gameplay object changed an actor/object/place/item/quest/dialogue identity or link.'
        }
        $retouchReplyCount = @(
            $retouchReportAfter.dialogues |
                ForEach-Object { $_.nodes } |
                Where-Object { [string]$_.text -ceq $updatedReply }
        ).Count
        $doorAfterEntry = @($retouchReportAfter.objects | Where-Object {
            [string]$_.name -ceq $retouchDoorName -and [string]$_.kind.kind -ceq 'door'
        })
        $interiorAfterEntry = @($retouchReportAfter.places | Where-Object {
            [string]$_.name -ceq $retouchInteriorPlaceName
        })
        if ($retouchReplyCount -ne 1 -or $doorAfterEntry.Count -ne 1 -or $interiorAfterEntry.Count -ne 1) {
            throw "Retouched object does not contain exactly one updated Intendant reply, door and $retouchInteriorPlaceName place."
        }
        if ([string]$interiorAfterEntry[0].id -cne $interiorBeforeId) {
            throw "$retouchInteriorPlaceName identity changed during the door/text retouch."
        }
        $interiorAfterPosition = $interiorAfterEntry[0].position
        $interiorXAfter = [double]$interiorAfterPosition.x
        $interiorYAfter = [double]$interiorAfterPosition.y
        $interiorZAfter = [double]$interiorAfterPosition.z
        if (-not [double]::IsFinite($interiorXAfter) -or
            -not [double]::IsFinite($interiorYAfter) -or
            -not [double]::IsFinite($interiorZAfter) -or
            [Math]::Abs($interiorXAfter - $interiorXBefore) -gt 0.001 -or
            [Math]::Abs($interiorYAfter - $interiorYBefore) -gt 0.001 -or
            [Math]::Abs($interiorZAfter - $interiorZBefore) -gt 0.001) {
            throw "$retouchInteriorPlaceName pose changed during the door/text retouch."
        }
        $doorAfterPosition = $doorAfterEntry[0].position
        $doorXAfterObserved = [double]$doorAfterPosition.x
        $doorYAfterObserved = [double]$doorAfterPosition.y
        $doorZAfterObserved = [double]$doorAfterPosition.z
        if ([Math]::Abs($doorXAfterObserved - $doorXAfter) -gt 0.001 -or
            [Math]::Abs($doorYAfterObserved - $doorYBefore) -gt 0.001 -or
            [Math]::Abs($doorZAfterObserved - $doorZBefore) -gt 0.001) {
            throw "Retouched door pose is not X+0.05 with Y/Z preserved: x=$doorXAfterObserved y=$doorYAfterObserved z=$doorZAfterObserved"
        }
        $retouchCloneOriginalPreserved =
            (Test-ArchiveSnapshotsEqual -Before $cloneArchiveBefore -After (Get-ArchiveSnapshot -LiteralPath $cloneArchive)) -and
            (Test-ArchiveSnapshotsEqual -Before $cloneCurrentObjectBefore -After (Get-ArchiveSnapshot -LiteralPath $cloneCurrentObject)) -and
            (Test-ArchiveSnapshotsEqual -Before $clonePinnedObjectBefore -After (Get-ArchiveSnapshot -LiteralPath $clonePinnedObject))
        if (-not $retouchCloneOriginalPreserved) {
            throw 'R3-P02 Save altered the legacy archive or an original managed object.'
        }
        $retouchSaved = $true
        Add-PlaytestJournal -Journal $journal -Phase 'authoring_retouch' -Event 'preview_apply_save' -Payload ([ordered]@{
            reply_before = $replyBefore
            reply_after = $updatedReply
            door_before = [ordered]@{ x = $doorXBefore; y = $doorYBefore; z = $doorZBefore }
            door_after = [ordered]@{ x = $doorXAfterObserved; y = $doorYAfterObserved; z = $doorZAfterObserved }
            door_x_delta_m = $doorXAfterObserved - $doorXBefore
            preserved_y_z = ([Math]::Abs($doorYAfterObserved - $doorYBefore) -le 0.001 -and [Math]::Abs($doorZAfterObserved - $doorZBefore) -le 0.001)
            interior_place_before = [ordered]@{ id = $interiorBeforeId; x = $interiorXBefore; y = $interiorYBefore; z = $interiorZBefore }
            interior_place_after = [ordered]@{ id = [string]$interiorAfterEntry[0].id; x = $interiorXAfter; y = $interiorYAfter; z = $interiorZAfter }
            interior_place_preserved = $true
            functional_hash = 'compatible by public compatible-resume path; direct hash computation is not injected'
            old_archive_hash = $currentArchiveHash
            new_archive_hash = $savedArchiveHash
            workspace_before = $retouchWorkspaceBefore
            workspace_after = $retouchWorkspaceAfter
        })

        [void](Invoke-UiClick -Socket $run1.Socket -Text 'Tester & suivi' -Selector '[data-authoring-tab-button]')
        [void](Invoke-UiClick -Socket $run1.Socket -Text 'Reprendre la partie' -Selector 'section.authoring-gameplay-controls button')
        $ordinaryRefusalEvidence = Wait-CdpReady -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'ordinary resume refusal after compatible retouch' -Expression @'
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const root=document.querySelector('section.authoring-gameplay-controls');
  const outputs=[...((root??document).querySelectorAll('output'))].map(node=>norm(node.textContent)).filter(Boolean);
  const text=outputs.join(' | ');
  const buttons=[...((root??document).querySelectorAll('button'))].map(node=>({text:norm(node.textContent),disabled:!!node.disabled}));
  const ordinary=buttons.find(node=>node.text==='Reprendre la partie');
  const compatible=buttons.find(node=>node.text==='Reprendre avec les retouches compatibles');
  const status=norm(document.querySelector('#authoring-playtest-status')?.textContent);
  const play=document.querySelector('button.authoring-play');
  const stop=[...document.querySelectorAll('.authoring-play-actions button')].find(node=>norm(node.textContent)==='Retour à l’édition');
  const refused=/source changed|explicitly review compatible edits|save incompatible|incompatible/i.test(text);
  return {ready:refused&&!!ordinary&&!ordinary.disabled&&!!compatible&&!compatible.disabled&&!!play&&!play.disabled&&(!stop||stop.disabled),
    terminal:false,reason:text||'resume refusal pending',message:text,status};
})()
'@
        $ordinaryResumeRefused = $true
        $ordinaryStateAfter = Get-PlaytestPublicState -Socket $run1.Socket
        if (@($ordinaryStateAfter.allButtons | Where-Object {
                [string]$_.text -ceq "Retour à l’édition" -and -not [bool]$_.disabled
            }).Count -ne 0) {
            throw 'Ordinary Reprendre la partie unexpectedly started a playtest after a source retouch.'
        }
        [void](Invoke-UiClick -Socket $run1.Socket -Text 'Reprendre avec les retouches compatibles' -Selector 'section.authoring-gameplay-controls button')
        [void](Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'compatible resume acknowledgement' -Condition {
            param($state)
            @($state.outputs | Where-Object { [string]$_ -ceq 'Reprise préparée.' }).Count -ge 1
        })
        [void](Wait-PlaytestReady -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'compatible resumed public playtest')
        $compatibleResumeStarted = $true
        Focus-PlaytestCanvas -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds
        [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'compatible_resume_door')
        $resumedDoorState = Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'compatible resume preserves quest and open door' -Condition {
            param($state)
            $observation = Get-PlaytestObservation -State $state
            $observation.ok -and $observation.simulation_ready -and $observation.grounded -and
                (Test-Gate3RetouchCompletedQuest -State $state) -and
                @($state.actions | Where-Object { [string]$_.text -ceq $closeText -and -not [bool]$_.disabled }).Count -eq 1
        }
        $resumedDoorObservation = Get-PlaytestObservation -State $resumedDoorState
        Add-PlaytestJournal -Journal $journal -Phase 'compatible_resume_door' -Event 'door_quest_state_verified' -Payload ([ordered]@{
            observation = $resumedDoorObservation
            actions = @($resumedDoorState.actions)
            items = @($resumedDoorState.items)
            expected_door_x = $doorXAfterObserved
            expected_door_z = $doorZAfterObserved
        })

        Move-Gate3RetouchToVillage -Socket $run1.Socket -DoorX $doorXAfterObserved -DoorZ $doorZAfterObserved -RoomOriginX $sourceBuildingOriginX -RoomOriginZ $sourceBuildingOriginZ -RoomWidth $sourceBuildingWidth -RoomDepth $sourceBuildingDepth -VillageX $retouchVillageX -VillageZ $retouchVillageZ -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'updated_dialogue_village'
        [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'updated_dialogue_village')
        $villageDialogueState = Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'retouched Intendant reply near village' -Condition {
            param($state)
            @($state.paragraphs | Where-Object { [string]$_ -like "*$updatedReply*" }).Count -ge 1 -and
                (Test-Gate3RetouchCompletedQuest -State $state)
        }
        Add-PlaytestJournal -Journal $journal -Phase 'updated_dialogue_village' -Event 'reply_observed' -Payload ([ordered]@{
            observation = Get-PlaytestObservation -State $villageDialogueState
            paragraphs = @($villageDialogueState.paragraphs)
            items = @($villageDialogueState.items)
        })

        Move-Gate3RetouchToDoor -Socket $run1.Socket -DoorX $doorXAfterObserved -DoorZ $doorZAfterObserved -RoomOriginX $sourceBuildingOriginX -RoomOriginZ $sourceBuildingOriginZ -RoomWidth $sourceBuildingWidth -RoomDepth $sourceBuildingDepth -VillageX $retouchVillageX -VillageZ $retouchVillageZ -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'door_threshold'
        [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'door_threshold')
        $thresholdState = Wait-PlaytestCondition -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'open retouched door near threshold' -Condition {
            param($state)
            @($state.actions | Where-Object { [string]$_.text -ceq $closeText -and -not [bool]$_.disabled }).Count -eq 1
        }
        $thresholdObservation = Get-PlaytestObservation -State $thresholdState
        $interiorTargetX = [double]$interiorXAfter
        $interiorTargetZ = [double]$interiorZAfter
        Move-PlaytestToXZ -Socket $run1.Socket -X $interiorTargetX -Z $interiorTargetZ -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'cross_interior_kcc'
        [void](Wait-PlaytestSettled -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'cross_interior_kcc')
        $interiorState = Get-PlaytestPublicState -Socket $run1.Socket
        $interiorObservation = Get-PlaytestObservation -State $interiorState
        if (-not $interiorObservation.ok -or -not $interiorObservation.simulation_ready -or
            -not $interiorObservation.grounded -or
            [Math]::Abs([double]$interiorObservation.x - $interiorTargetX) -gt 1.25 -or
            [Math]::Abs([double]$interiorObservation.z - $interiorTargetZ) -gt 1.25) {
            throw 'Public KCC position did not cross to the authored interior target.'
        }
        $crossInteriorKcc = $true
        Add-PlaytestJournal -Journal $journal -Phase 'cross_interior_kcc' -Event 'threshold_to_interior' -Payload ([ordered]@{
            threshold = $thresholdObservation
            interior = $interiorObservation
            target_x = $interiorTargetX
            target_z = $interiorTargetZ
            place_name = $retouchInteriorPlaceName
            place_id = $interiorBeforeId
            tolerance_m = 1.25
            public_grounded = $interiorObservation.grounded
        })
        Return-Gate3RetouchToEditing -Socket $run1.Socket -TimeoutSeconds $InteractionTimeoutSeconds
        $run1Completed = $true
    }
    catch {
        $failure = "run1: $($_.Exception.Message)"
    }
    finally {
        if ($null -ne $run1) {
            try {
                $stop = Stop-ForgeStudioPlaytestProcess -Run $run1
                $forcedProcessStop = $forcedProcessStop -or [bool]$stop.Forced
                $run1Stopped = [bool]$stop.Closed
                $run1NativeCloseConfirmed = $run1Stopped -and -not [bool]$stop.Forced -and
                    [string]::IsNullOrWhiteSpace([string](Get-Gate3RetouchOptional -Object $stop -Name 'GracefulCloseFailure'))
            }
            catch {
                $forcedProcessStop = $true
                $run1Stopped = $false
                $run1NativeCloseConfirmed = $false
            }
            $run1 = $null
        }
    }

    if ([string]::IsNullOrWhiteSpace($failure)) {
        try {
            $run2 = Start-ForgeStudioPlaytestProcess -CandidatePath $candidate -CandidateHash $candidateHash -FixtureRoot $fixture -EvidencePath $output -RunNumber 2 -StartupTimeout $StartupTimeoutSeconds
            $run2Stopped = $false
            Open-Gate3PlaytestZone -Socket $run2.Socket -ZoneId $retouchZoneId -TimeoutSeconds $InteractionTimeoutSeconds
            [void](Invoke-UiClick -Socket $run2.Socket -Text 'Tester & suivi' -Selector '[data-authoring-tab-button]')
            [void](Invoke-UiClick -Socket $run2.Socket -Text 'Reprendre avec les retouches compatibles' -Selector 'section.authoring-gameplay-controls button')
            [void](Wait-PlaytestCondition -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'second-process compatible resume acknowledgement' -Condition {
                param($state)
                @($state.outputs | Where-Object { [string]$_ -ceq 'Reprise préparée.' }).Count -ge 1
            })
            [void](Wait-PlaytestReady -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'second-process compatible resumed playtest')
            Focus-PlaytestCanvas -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds
            [void](Wait-PlaytestSettled -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run2_resume')
            $run2DoorState = Wait-PlaytestCondition -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'second-process open door and completed quest' -Condition {
                param($state)
                $observation = Get-PlaytestObservation -State $state
                $observation.ok -and $observation.simulation_ready -and $observation.grounded -and
                    (Test-Gate3RetouchCompletedQuest -State $state) -and
                    @($state.actions | Where-Object { [string]$_.text -ceq $closeText -and -not [bool]$_.disabled }).Count -eq 1
            }
            Add-PlaytestJournal -Journal $journal -Phase 'run2_resume' -Event 'second_process_state_verified' -Payload ([ordered]@{
                observation = Get-PlaytestObservation -State $run2DoorState
                actions = @($run2DoorState.actions)
                items = @($run2DoorState.items)
            })
            Move-Gate3RetouchToVillage -Socket $run2.Socket -DoorX $doorXAfterObserved -DoorZ $doorZAfterObserved -RoomOriginX $sourceBuildingOriginX -RoomOriginZ $sourceBuildingOriginZ -RoomWidth $sourceBuildingWidth -RoomDepth $sourceBuildingDepth -VillageX $retouchVillageX -VillageZ $retouchVillageZ -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run2_dialogue_village'
            [void](Wait-PlaytestSettled -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Journal $journal -Phase 'run2_dialogue_village')
            $run2VillageState = Wait-PlaytestCondition -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'second-process retouched reply near village' -Condition {
                param($state)
                @($state.paragraphs | Where-Object { [string]$_ -like "*$updatedReply*" }).Count -ge 1 -and
                    (Test-Gate3RetouchCompletedQuest -State $state)
            }
            Add-PlaytestJournal -Journal $journal -Phase 'run2_dialogue_village' -Event 'second_process_reply_verified' -Payload ([ordered]@{
                observation = Get-PlaytestObservation -State $run2VillageState
                paragraphs = @($run2VillageState.paragraphs)
                items = @($run2VillageState.items)
            })
            Return-Gate3RetouchToEditing -Socket $run2.Socket -TimeoutSeconds $InteractionTimeoutSeconds
            $run2Completed = $true
        }
        catch {
            $failure = "run2: $($_.Exception.Message)"
        }
        finally {
            if ($null -ne $run2) {
                try {
                    $stop = Stop-ForgeStudioPlaytestProcess -Run $run2
                    $forcedProcessStop = $forcedProcessStop -or [bool]$stop.Forced
                    $run2Stopped = [bool]$stop.Closed
                    $run2NativeCloseConfirmed = $run2Stopped -and -not [bool]$stop.Forced -and
                        [string]::IsNullOrWhiteSpace([string](Get-Gate3RetouchOptional -Object $stop -Name 'GracefulCloseFailure'))
                }
                catch {
                    $forcedProcessStop = $true
                    $run2Stopped = $false
                    $run2NativeCloseConfirmed = $false
                }
                $run2 = $null
            }
        }
    }
}
finally {
    if ($null -ne $run1) {
        try {
            $stop = Stop-ForgeStudioPlaytestProcess -Run $run1
            $forcedProcessStop = $forcedProcessStop -or [bool]$stop.Forced
            $run1Stopped = [bool]$stop.Closed
            $run1NativeCloseConfirmed = $run1Stopped -and -not [bool]$stop.Forced -and
                [string]::IsNullOrWhiteSpace([string](Get-Gate3RetouchOptional -Object $stop -Name 'GracefulCloseFailure'))
        }
        catch {
            $forcedProcessStop = $true
            $run1Stopped = $false
            $run1NativeCloseConfirmed = $false
        }
        $run1 = $null
    }
    if ($null -ne $run2) {
        try {
            $stop = Stop-ForgeStudioPlaytestProcess -Run $run2
            $forcedProcessStop = $forcedProcessStop -or [bool]$stop.Forced
            $run2Stopped = [bool]$stop.Closed
            $run2NativeCloseConfirmed = $run2Stopped -and -not [bool]$stop.Forced -and
                [string]::IsNullOrWhiteSpace([string](Get-Gate3RetouchOptional -Object $stop -Name 'GracefulCloseFailure'))
        }
        catch {
            $forcedProcessStop = $true
            $run2Stopped = $false
            $run2NativeCloseConfirmed = $false
        }
        $run2 = $null
    }
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
}

$sourceArchiveAfter = $sourceArchiveBefore
$sourceWorkspaceAfter = $sourceWorkspaceBefore
$sourcePinnedObjectAfter = $sourcePinnedObjectBefore
$sourceCurrentObjectAfter = $sourceCurrentObjectBefore
$sourceSaveAfter = $sourceSaveBefore
try {
    $sourceArchiveAfter = Get-ArchiveSnapshot -LiteralPath $sourceArchive
    $sourceWorkspaceAfter = Get-ArchiveSnapshot -LiteralPath $sourceWorkspaceArchive
    $sourcePinnedObjectAfter = Get-ArchiveSnapshot -LiteralPath $sourcePinnedObject
    $sourceCurrentObjectAfter = Get-ArchiveSnapshot -LiteralPath $sourceCurrentObject
    $sourceSaveAfter = Get-ArchiveSnapshot -LiteralPath $sourceSavePath
    $sourcePreserved =
        (Test-ArchiveSnapshotsEqual -Before $sourceArchiveBefore -After $sourceArchiveAfter) -and
        (Test-ArchiveSnapshotsEqual -Before $sourceWorkspaceBefore -After $sourceWorkspaceAfter) -and
        (Test-ArchiveSnapshotsEqual -Before $sourcePinnedObjectBefore -After $sourcePinnedObjectAfter) -and
        (Test-ArchiveSnapshotsEqual -Before $sourceCurrentObjectBefore -After $sourceCurrentObjectAfter) -and
        (Test-ArchiveSnapshotsEqual -Before $sourceSaveBefore -After $sourceSaveAfter)
}
catch {
    $sourcePreserved = $false
    if ([string]::IsNullOrWhiteSpace($failure)) {
        $failure = "source snapshot: $($_.Exception.Message)"
    }
}
if (-not $sourcePreserved -and [string]::IsNullOrWhiteSpace($failure)) {
    $failure = 'An input fixture archive, source object or real game save changed during the isolated UI smoke.'
}
if ($forcedProcessStop -and [string]::IsNullOrWhiteSpace($failure)) {
    $failure = 'An owned Studio process required a forced stop.'
}
if ((-not $run1Stopped -or -not $run2Stopped) -and [string]::IsNullOrWhiteSpace($failure)) {
    $failure = 'An owned Studio process did not close cleanly.'
}
if ((-not $run1NativeCloseConfirmed -or -not $run2NativeCloseConfirmed) -and
    [string]::IsNullOrWhiteSpace($failure)) {
    $failure = 'Public native close confirmation was not proven without force for every owned Studio process.'
}

$cloneWorkspaceFinal = Get-ArchiveSnapshot -LiteralPath $cloneWorkspace
$cloneCurrentObjectFinal = Get-ArchiveSnapshot -LiteralPath $cloneCurrentObject
$clonePinnedObjectFinal = Get-ArchiveSnapshot -LiteralPath $clonePinnedObject
$cloneSaveFinal = Get-ArchiveSnapshot -LiteralPath $cloneSavePath
$retouchCloneOriginalPreserved = $retouchCloneOriginalPreserved -and
    (Test-ArchiveSnapshotsEqual -Before $cloneArchiveBefore -After (Get-ArchiveSnapshot -LiteralPath $cloneArchive)) -and
    (Test-ArchiveSnapshotsEqual -Before $cloneCurrentObjectBefore -After $cloneCurrentObjectFinal) -and
    (Test-ArchiveSnapshotsEqual -Before $clonePinnedObjectBefore -After $clonePinnedObjectFinal)

$status = if ([string]::IsNullOrWhiteSpace($failure) -and
    $run1Completed -and $run2Completed -and $retouchSaved -and
    $ordinaryResumeRefused -and $compatibleResumeStarted -and
    $crossInteriorKcc -and $sourcePreserved -and $retouchCloneOriginalPreserved -and
    $run1Stopped -and $run2Stopped -and $run1NativeCloseConfirmed -and $run2NativeCloseConfirmed) {
    'passed'
} else {
    'failed_closed'
}

$ledger = [ordered]@{
    schema = 'forge.tidewater.gate3-compatible-retouch-smoke'
    version = 1
    status = $status
    failure = $failure
    gate_qualified = $false
    human_visual_review = $false
    r3_p02 = $true
    candidate = $candidate
    candidate_sha256 = $candidateHash
    parent_candidate_sha256 = $previousCandidateHash
    candidate_differs_from_parent = $candidateDiffersFromParent
    candidate_same_as_parent_allowed = [bool]$AllowSameCandidate
    previous_playtest_ledger = $ledgerInput
    authoring_ledger = $authoringLedgerPath
    authoring_fixture = $authoringFixturePath
    playtest_source_fixture = $previousSourceFixturePath
    playtest_isolated_fixture = $previousIsolatedFixturePath
    input_fixture = $source
    input_fixture_from_previous_playtest = $previousIsolatedFixturePath.Equals($source, [StringComparison]::OrdinalIgnoreCase)
    source_original_hash_pinned = $sourcePin
    source_placements = [ordered]@{
        source_archive_sha256=$sourcePin
        representation='ConvertTo-Json -Depth 100 -Compress; complete ordered placement array; SHA-256 of UTF-8 without BOM'
        before=[ordered]@{count=$sourcePlacementsBefore.Count; sha256=$sourcePlacementsBefore.Sha256; canonical_json=$sourcePlacementsBefore.CanonicalJson}
        after=if ($null -ne $savedPlacementsSnapshot) { [ordered]@{count=$savedPlacementsSnapshot.Count; sha256=$savedPlacementsSnapshot.Sha256; canonical_json=$savedPlacementsSnapshot.CanonicalJson} } else { $null }
        identity_and_content_preserved=$placementsPreserved
    }
    source_current_archive_hash_before = $currentArchiveHash
    source_current_head_hash_before = $currentHeadHash
    source_current_checkpoint_matches_authoring = $sourceCurrentCheckpointMatchesAuthoring
    authored_saved_reference = [ordered]@{
        archive_hash = $authoringArchiveHash
        head_hash = $authoringHeadHash
    }
    source_originals_immutable = $sourcePreserved
    source_snapshots = [ordered]@{
        zone_archive_before = $sourceArchiveBefore
        zone_archive_after = $sourceArchiveAfter
        workspace_before = $sourceWorkspaceBefore
        workspace_after = $sourceWorkspaceAfter
        pinned_object_before = $sourcePinnedObjectBefore
        pinned_object_after = $sourcePinnedObjectAfter
        current_object_before = $sourceCurrentObjectBefore
        current_object_after = $sourceCurrentObjectAfter
        game_save_before = $sourceSaveBefore
        game_save_after = $sourceSaveAfter
    }
    isolated_fixture = $fixture
    isolated_source_snapshots = [ordered]@{
        legacy_zone_before = $cloneArchiveBefore
        legacy_zone_after = Get-ArchiveSnapshot -LiteralPath $cloneArchive
        current_object_before = $cloneCurrentObjectBefore
        current_object_after = $cloneCurrentObjectFinal
        pinned_object_before = $clonePinnedObjectBefore
        pinned_object_after = $clonePinnedObjectFinal
        game_save_before = $cloneSaveBefore
        game_save_after = $cloneSaveFinal
    }
    retouch = [ordered]@{
        saved = $retouchSaved
        preview_apply_save = $retouchSaved
        reply_before = $replyBefore
        reply_after = $updatedReply
        old_archive_hash = $currentArchiveHash
        new_archive_hash = if ($null -ne $savedReference) {
            Get-NormalizedSha256 -Value ([string]$savedReference.archive_hash)
        } else {
            $null
        }
        door_before = [ordered]@{ x = $doorXBefore; y = $doorYBefore; z = $doorZBefore }
        door_after = if ($null -ne $doorAfterEntry) {
            [ordered]@{
                x = [double]$doorAfterEntry[0].position.x
                y = [double]$doorAfterEntry[0].position.y
                z = [double]$doorAfterEntry[0].position.z
            }
        } else {
            $null
        }
        door_x_delta_m = if ($null -ne $doorAfterEntry) {
            [double]$doorAfterEntry[0].position.x - $doorXBefore
        } else {
            $null
        }
        preserved_y_z = if ($null -ne $doorAfterEntry) {
            [Math]::Abs([double]$doorAfterEntry[0].position.y - $doorYBefore) -le 0.001 -and
                [Math]::Abs([double]$doorAfterEntry[0].position.z - $doorZBefore) -le 0.001
        } else {
            $false
        }
        interior_place_before = [ordered]@{ id = $interiorBeforeId; x = $interiorXBefore; y = $interiorYBefore; z = $interiorZBefore }
        interior_place_after = if ($null -ne $interiorAfterEntry) {
            [ordered]@{
                id = [string]$interiorAfterEntry[0].id
                x = [double]$interiorAfterEntry[0].position.x
                y = [double]$interiorAfterEntry[0].position.y
                z = [double]$interiorAfterEntry[0].position.z
            }
        } else {
            $null
        }
        interior_place_preserved = ($null -ne $interiorAfterEntry -and
            [string]$interiorAfterEntry[0].id -ceq $interiorBeforeId -and
            [Math]::Abs([double]$interiorAfterEntry[0].position.x - $interiorXBefore) -le 0.001 -and
            [Math]::Abs([double]$interiorAfterEntry[0].position.y - $interiorYBefore) -le 0.001 -and
            [Math]::Abs([double]$interiorAfterEntry[0].position.z - $interiorZBefore) -le 0.001)
        old_authored_object_unchanged = $retouchCloneOriginalPreserved
        gameplay_identity_and_links_preserved = if ($null -ne $retouchGameplayIdentity) {
            (ConvertTo-CompactJson -Value $retouchGameplayIdentity) -ceq $sourceGameplayIdentityJson
        } else {
            $false
        }
        room = [ordered]@{
            id = $sourceBuildingId
            origin_x = $sourceBuildingOriginX
            origin_z = $sourceBuildingOriginZ
            width_m = $sourceBuildingWidth
            depth_m = $sourceBuildingDepth
            public_route_clearance_m = 1.0
        }
        direct_domain_hash_read = $false
    }
    save = [ordered]@{
        relative_path = $sourceSaveRelativePath
        real_save_present_before = $true
        save_at_rest_acknowledged = $null -ne $run1DoorSave
        ordinary_resume_refused = $ordinaryResumeRefused
        ordinary_refusal_evidence = $ordinaryRefusalEvidence
        compatible_resume_started = $compatibleResumeStarted
        source_archive_hash_checkpoint = $currentArchiveHash
        source_head_hash_checked_in_save = $currentHeadHash
        source_hash_in_save_before_retouch = $sourceSaveSourceHash
        retouched_source_hash = if ($null -ne $savedReference) {
            Get-NormalizedSha256 -Value ([string]$savedReference.archive_hash)
        } else {
            $null
        }
        compatibility_proof = 'ordinary source-mismatch refusal followed by public compatible-resume success'
    }
    run1_completed = $run1Completed
    run2_completed = $run2Completed
    cross_interior_kcc = $crossInteriorKcc
    owned_processes_closed = [bool]($run1Stopped -and $run2Stopped)
    native_close_confirmed = [bool]($run1NativeCloseConfirmed -and $run2NativeCloseConfirmed)
    run1_native_close_confirmed = $run1NativeCloseConfirmed
    run2_native_close_confirmed = $run2NativeCloseConfirmed
    forced_process_stop = $forcedProcessStop
    journal = @($journal)
    zone_uuid = $retouchZoneId
    limitations = @(
        'All edit, open, dialogue, movement, door, save and resume inputs are visible Studio controls or public CDP mouse/keyboard events; no Tauri command, gameplay state injection or manual gameplay JSON write is used.',
        'Input source archives, the pinned object, the current input gameplay object and the real input game save are snapshotted before and after; intentional retouches are written only to the isolated fixture clone.',
        'The compatible decision is evidenced by the ordinary refusal and the public compatible-resume button. The script does not claim a hidden functional-hash calculation.',
        'The interior result is a bounded public KCC position/grounded observation after crossing the open-door threshold. It does not claim authored navigation, a complete room route, P05, fire, traps or visual acceptance.',
        'Dialogue text, quest Completed and consumed-item lines come from the public HUD; hidden domain state is not read.',
        'gate_qualified remains false; human visual, runtime breadth and release acceptance require separate evidence.'
    )
}

if ($outputCreated) {
    $ledgerPath = Join-Path $output 'gate3-compatible-retouch-smoke-ledger.json'
    [IO.File]::WriteAllText($ledgerPath, ($ledger | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
    if ($status -cne 'passed') {
        throw "Gate 3 compatible retouch smoke failed closed: $failure. Ledger: $ledgerPath"
    }
    Write-Output "TIDEWATER_GATE3_COMPATIBLE_RETOUCH_SMOKE_PASS ledger=$ledgerPath"
}
