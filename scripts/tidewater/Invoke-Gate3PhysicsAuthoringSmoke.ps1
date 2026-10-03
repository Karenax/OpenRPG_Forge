#Requires -Version 7.2
# Gate 3 Tidewater physics authoring smoke. This is an isolated public-UI
# authoring proof only; it does not qualify contacts, fire, danger or nav.
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
    [Alias('AuthoringLedger', 'AuthoringStudioLedger', 'StudioLedger', 'PriorLedger', 'LedgerPath')]
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
    [ValidateRange(30, 180)]
    [int]$StartupTimeoutSeconds = 60,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(30, 240)]
    [int]$InteractionTimeoutSeconds = 120,

    [Parameter(ParameterSetName = 'Run')]
    [switch]$IncludeShoreFire,

    [Parameter(ParameterSetName = 'Run')]
    [switch]$IncludeImpactTarget,

    [Parameter(ParameterSetName = 'Run')]
    [switch]$CompletePhysicsFixture,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(0, 1000000)]
    [int]$ShoreFireHeatDamagePerTick = 1,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Reuse the governed CDP, fixture and Gameplay UI helpers. The helper's
# SelfTest path only defines functions and never starts Studio or mutates a
# fixture. Restore this script's bindings after dot-sourcing because the
# helper declares some of the same parameter names.
$physicsRequestedSelfTest = [bool]$SelfTest
$physicsRequestedCandidate = $Candidate
$physicsRequestedFixture = $FreshIsolatedFixture
$physicsRequestedLedger = $PreviousLedger
$physicsRequestedSourcePin = $SourceOriginalHashPinned
$physicsRequestedEvidenceRoot = $EvidenceRoot
$physicsRequestedZoneUuid = $ZoneUuid
$physicsRequestedStartupTimeout = $StartupTimeoutSeconds
$physicsRequestedInteractionTimeout = $InteractionTimeoutSeconds
$physicsRequestedIncludeShoreFire = [bool]$IncludeShoreFire
$physicsRequestedIncludeImpactTarget = [bool]$IncludeImpactTarget
$physicsRequestedCompletePhysicsFixture = [bool]$CompletePhysicsFixture
$physicsRequestedShoreFireHeatDamagePerTick = $ShoreFireHeatDamagePerTick
. (Join-Path $PSScriptRoot 'Invoke-Gate3GameplayAuthoringSmoke.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Gate3ProofStorage.ps1')
if (-not $physicsRequestedSelfTest) {
    $Candidate = $physicsRequestedCandidate
    $FreshIsolatedFixture = $physicsRequestedFixture
    $PreviousLedger = $physicsRequestedLedger
    $SourceOriginalHashPinned = $physicsRequestedSourcePin
    $EvidenceRoot = $physicsRequestedEvidenceRoot
    $ZoneUuid = $physicsRequestedZoneUuid
    $StartupTimeoutSeconds = $physicsRequestedStartupTimeout
    $InteractionTimeoutSeconds = $physicsRequestedInteractionTimeout
    $IncludeShoreFire = $physicsRequestedIncludeShoreFire
    $IncludeImpactTarget = $physicsRequestedIncludeImpactTarget
    $CompletePhysicsFixture = $physicsRequestedCompletePhysicsFixture
    $ShoreFireHeatDamagePerTick = $physicsRequestedShoreFireHeatDamagePerTick
}

$zoneId = $ZoneUuid.ToString('D').ToLowerInvariant()
$editorSummary = 'Personnages, objets et objectifs'
$physicsTerrainBlobName = 'be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3.bin'
$physicsTerrainBlobHash = 'be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3'
$physicsPlatformName = 'Plateforme de lancer'
$physicsLaunchChargeName = 'Charge de lancer'
$physicsTrapSupportName = 'Support piège'
$physicsTrapChargeName = 'Charge piège'
$physicsFireName = 'Balise inflammable'
$physicsShoreFireName = 'Feu de rive'
$physicsDangerPlaceName = 'Poste de danger'
$physicsDangerFriendlyName = 'Veilleur du danger'
$physicsPlatformX = 19.0
$physicsPlatformZ = -270.0
$physicsPlatformY = 58.87091056823731
$physicsLaunchChargeY = 59.16591056823731
$physicsTrapSupportX = 16.0
$physicsTrapSupportZ = -270.0
$physicsTrapSupportY = 59.546617736816406
$physicsTrapChargeY = 59.8666177368164
$physicsDangerX = 24.0
$physicsDangerZ = -285.0
$physicsFireX = 25.0
$physicsFireZ = -284.0
$physicsShoreFireX = 40.0
$physicsShoreFireZ = -32.0
$physicsShoreFireY = -0.21999999999999997
$physicsImpactTargetName = 'Bloc de contrepoids'
$physicsImpactTargetX = 17.5
$physicsImpactTargetZ = -270.0
# Measured on the pinned Tidewater raster at the target footprint.  This is
# deliberately explicit so the optional dynamic target cannot acquire an
# invented terrain height during authoring.
$physicsImpactTargetY = 58.817839403
$physicsPlatformWidth = 0.5
$physicsPlatformDepth = 0.5
$physicsFireY = $null
$physicsShorePushName = 'Bloc poussable de rive'
$physicsShorePushX = 41.5
$physicsShorePushZ = -32.0
if ($CompletePhysicsFixture) {
    $IncludeShoreFire = $true
    $ShoreFireHeatDamagePerTick = 0
    $physicsPlatformY = 58.99091056823731
    $physicsLaunchChargeY = 59.28591056823731
    $physicsPlatformWidth = 0.05
    $physicsPlatformDepth = 0.05
    $physicsFireX = 18.0
    $physicsFireZ = -270.0
    $physicsFireY = 58.32
    $physicsDangerX = 19.0
    $physicsDangerZ = -269.0
}
$script:fireControlsMode = $null
$script:fireControlsObservation = $null

function Get-PhysicsProperty {
    param(
        [AllowNull()][object]$Object,
        [Parameter(Mandatory)][string]$Name
    )
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Test-PhysicsPosition {
    param(
        [AllowNull()][object]$Position,
        [Parameter(Mandatory)][double]$X,
        [Parameter(Mandatory)][double]$Z,
        [double]$Tolerance = 0.0002
    )
    if ($null -eq $Position) { return $false }
    try {
        return [double]::IsFinite([double]$Position.x) -and
            [double]::IsFinite([double]$Position.y) -and
            [double]::IsFinite([double]$Position.z) -and
            [Math]::Abs([double]$Position.x - $X) -lt $Tolerance -and
            [Math]::Abs([double]$Position.z - $Z) -lt $Tolerance
    }
    catch {
        return $false
    }
}

function Test-PhysicsHalfExtents {
    param(
        [Parameter(Mandatory)][object]$Physics,
        [Parameter(Mandatory)][double]$Width,
        [Parameter(Mandatory)][double]$Height,
        [Parameter(Mandatory)][double]$Depth,
        [double]$Tolerance = 0.0002
    )
    $extents = Get-PhysicsProperty -Object $Physics -Name 'body_half_extents_m'
    if ($null -eq $extents) { return $false }
    return [Math]::Abs([double]$extents.x - ($Width / 2.0)) -lt $Tolerance -and
        [Math]::Abs([double]$extents.y - ($Height / 2.0)) -lt $Tolerance -and
        [Math]::Abs([double]$extents.z - ($Depth / 2.0)) -lt $Tolerance
}

function Get-PhysicsGameplayReceipts {
    param([Parameter(Mandatory)][object]$Document)
    return @(
        foreach ($receipt in @($Document.receipts)) {
            $request = Get-PhysicsProperty -Object $receipt -Name 'request'
            $action = Get-PhysicsProperty -Object $request -Name 'action'
            $corrections = Get-PhysicsProperty -Object $action -Name 'corrections'
            if ($null -ne $corrections -and $null -ne (Get-PhysicsProperty -Object $corrections -Name 'gameplay')) {
                $receipt
            }
        }
    )
}

function Get-PhysicsLastReceiptHash {
    param([Parameter(Mandatory)][object]$Document)
    $receipts = @(Get-PhysicsGameplayReceipts -Document $Document)
    if ($receipts.Count -eq 0) { return $null }
    return [string](Get-PhysicsProperty -Object $receipts[-1] -Name 'resulting_hash')
}

function Wait-PhysicsGameplayIdle {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [Parameter(Mandatory)][string]$Description
    )
    return Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description $Description -Expression @'
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const editor=document.querySelector('details.authoring-gameplay-editor');
  const toolbar=document.querySelector('.authoring-toolbar');
  const busy=toolbar?.getAttribute('aria-busy');
  const status=norm(document.querySelector('.authoring-command-status')?.textContent);
  const terminal=/refusée|refusé|erreur|invalide|impossible|absent/i.test(status);
  return {ready:!!editor&&busy==='false'&&!terminal,terminal,reason:'busy='+busy+'; status='+status};
})()
'@
}

function Set-PhysicsFireControls {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][ValidateRange(0, 1000000)][int]$HeatDamagePerTick
    )
    if ([string]$script:fireControlsMode -cne 'present') {
        throw "Gate 3 physics smoke requires both public fire controls; observed mode '$script:fireControlsMode'."
    }
    [void](Set-UiControlByLabel -Socket $Socket -Label 'Inflammable' -Value $true -Kind checked -DetailsSummary 'Propriétés physiques')
    [void](Set-UiControlByLabel -Socket $Socket -Label 'Dégâts de chaleur par tick (0 = aucun dégât)' -Value ([string]$HeatDamagePerTick) -DetailsSummary 'Propriétés physiques')
    $flammable = Get-UiControlByLabel -Socket $Socket -Label 'Inflammable' -DetailsSummary 'Propriétés physiques'
    $heat = Get-UiControlByLabel -Socket $Socket -Label 'Dégâts de chaleur par tick (0 = aucun dégât)' -DetailsSummary 'Propriétés physiques'
    if (-not [bool]$flammable.checked -or [string]$heat.value -cne ([string]$HeatDamagePerTick)) {
        throw "Public fire controls did not retain true/${HeatDamagePerTick}: flammable=$($flammable.checked), heat=$($heat.value)."
    }
    $script:fireControlsObservation = [ordered]@{
        mode='present'
        flammable=true
        heat_damage_per_tick=$HeatDamagePerTick
        defaults_after_form=[ordered]@{ flammable=$false; heat_damage_per_tick=0 }
        authoring_claim='fire fields authored; runtime ignition, damage and death unclaimed'
    }
}

function Set-PhysicsRecipeForm {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$Kind,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$X,
        [AllowEmptyString()][Parameter(Mandatory)][string]$CenterY,
        [Parameter(Mandatory)][string]$Z,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [Parameter(Mandatory)][string]$Mass,
        [Parameter(Mandatory)][string]$Width,
        [Parameter(Mandatory)][string]$Height,
        [Parameter(Mandatory)][string]$Depth,
        [Parameter(Mandatory)][bool]$Pushable,
        [Parameter(Mandatory)][bool]$Throwable,
        [AllowEmptyString()][Parameter(Mandatory)][string]$BreakEnergy,
        [ValidateRange(0, 1000000)][int]$HeatDamagePerTick = 1,
        [switch]$Fire
    )
    [void](Wait-GameplayForm -Socket $Socket -TimeoutSeconds $TimeoutSeconds)
    Set-GameplayForm -Socket $Socket -Kind $Kind -Name $Name -X $X -Z $Z -CenterY $CenterY -Reply '' -TimeoutSeconds $TimeoutSeconds
    # The shared helper deliberately writes fire defaults false/0. A fire
    # recipe changes the two public controls only after this form is complete.
    Set-GameplayPhysicsForm -Socket $Socket -Mass $Mass -Width $Width -Height $Height -Depth $Depth -Pushable $Pushable -Throwable $Throwable -BreakEnergy $BreakEnergy
    if ($Fire) { Set-PhysicsFireControls -Socket $Socket -HeatDamagePerTick $HeatDamagePerTick }
}

function Add-PhysicsRecipeProp {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][hashtable]$Recipe,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $centerY = ''
    if ($Recipe.ContainsKey('CenterY') -and $null -ne $Recipe.CenterY) {
        $centerY = ([double]$Recipe.CenterY).ToString('R', [Globalization.CultureInfo]::InvariantCulture)
    }
    $breakEnergy = ''
    if ($Recipe.ContainsKey('BreakEnergy')) { $breakEnergy = [string]$Recipe.BreakEnergy }
    $heatDamagePerTick = 1
    if ($Recipe.ContainsKey('HeatDamagePerTick')) { $heatDamagePerTick = [int]$Recipe.HeatDamagePerTick }
    $formArgs = @{
        Socket=$Socket
        Kind='prop'
        Name=[string]$Recipe.Name
        X=([double]$Recipe.X).ToString('R', [Globalization.CultureInfo]::InvariantCulture)
        Z=([double]$Recipe.Z).ToString('R', [Globalization.CultureInfo]::InvariantCulture)
        CenterY=$centerY
        TimeoutSeconds=$TimeoutSeconds
        Mass=[string]$Recipe.Mass
        Width=[string]$Recipe.Width
        Height=[string]$Recipe.Height
        Depth=[string]$Recipe.Depth
        Pushable=[bool]$Recipe.Pushable
        Throwable=[bool]$Recipe.Throwable
        BreakEnergy=$breakEnergy
        HeatDamagePerTick=$heatDamagePerTick
    }
    if ($Recipe.ContainsKey('Fire') -and [bool]$Recipe.Fire) { $formArgs.Fire=$true }
    Set-PhysicsRecipeForm @formArgs
    $result = Invoke-GameplayPreviewApply -Socket $Socket -PreviewButton "Examiner l’ajout" -Description "add physics prop $($Recipe.Name)" -TimeoutSeconds $TimeoutSeconds
    $observedY = 'canonical'
    if (-not [string]::IsNullOrWhiteSpace([string]$formArgs.CenterY)) { $observedY = [double]$Recipe.CenterY }
    return [ordered]@{ action='add_prop'; name=[string]$Recipe.Name; x=[double]$Recipe.X; z=[double]$Recipe.Z; y=$observedY; result=$result; runtime_claim='authoring_only' }
}

function Add-PhysicsPlace {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][double]$X,
        [Parameter(Mandatory)][double]$Z,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    [void](Wait-GameplayForm -Socket $Socket -TimeoutSeconds $TimeoutSeconds)
    $xText = $X.ToString('R', [Globalization.CultureInfo]::InvariantCulture)
    $zText = $Z.ToString('R', [Globalization.CultureInfo]::InvariantCulture)
    Set-GameplayForm -Socket $Socket -Kind 'place' -Name $Name -X $xText -Z $zText -Reply '' -TimeoutSeconds $TimeoutSeconds
    $result = Invoke-GameplayPreviewApply -Socket $Socket -PreviewButton "Examiner l’ajout" -Description "add danger place $Name" -TimeoutSeconds $TimeoutSeconds
    return [ordered]@{ action='add_place'; name=$Name; x=$X; z=$Z; y='canonical'; result=$result; runtime_claim='authoring_only' }
}

function Add-PhysicsDangerFriendly {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][double]$X,
        [Parameter(Mandatory)][double]$Z,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    [void](Wait-GameplayForm -Socket $Socket -TimeoutSeconds $TimeoutSeconds)
    $xText = $X.ToString('R', [Globalization.CultureInfo]::InvariantCulture)
    $zText = $Z.ToString('R', [Globalization.CultureInfo]::InvariantCulture)
    Set-GameplayForm -Socket $Socket -Kind 'friendly' -Name $Name -X $xText -Z $zText -Reply 'Je surveille le danger.' -TimeoutSeconds $TimeoutSeconds
    $addResult = Invoke-GameplayPreviewApply -Socket $Socket -PreviewButton "Examiner l’ajout" -Description "add danger friendly $Name" -TimeoutSeconds $TimeoutSeconds
    Select-GameplayOptionByText -Socket $Socket -Label 'Élément à retoucher' -Text $Name -TimeoutSeconds $TimeoutSeconds
    [void](Open-DetailsBySummary -Socket $Socket -Summary 'Statistiques du personnage sélectionné')
    $fleeControlBefore = Get-UiControlByLabel -Socket $Socket -Label 'Réagit au danger par la fuite' -DetailsSummary 'Statistiques du personnage sélectionné'
    if ([bool]$fleeControlBefore.disabled) {
        throw "Public flee control is disabled for $Name."
    }
    if ([bool]$fleeControlBefore.checked) {
        $retouchResult = [ordered]@{
            description = "set danger flee flag $Name"
            status = 'noop_already_present'
            field = 'flees_danger'
            requested = $true
            observed_before = $true
            observed_after = $true
            preview_status = 'not_requested'
            apply_status = 'not_requested'
            public_control = 'Réagit au danger par la fuite'
        }
    }
    else {
        [void](Set-UiControlByLabel -Socket $Socket -Label 'Réagit au danger par la fuite' -Value $true -Kind checked -DetailsSummary 'Statistiques du personnage sélectionné')
        $fleeControlAfter = Get-UiControlByLabel -Socket $Socket -Label 'Réagit au danger par la fuite' -DetailsSummary 'Statistiques du personnage sélectionné'
        if (-not [bool]$fleeControlAfter.checked) {
            throw "Public flee control did not retain true for $Name."
        }
        $retouchResult = Invoke-GameplayPreviewApply -Socket $Socket -PreviewButton 'Examiner les statistiques' -Description "set danger flee flag $Name" -TimeoutSeconds $TimeoutSeconds
        $retouchResult = [ordered]@{
            description = [string]$retouchResult.description
            status = 'changed_and_applied'
            field = 'flees_danger'
            requested = $true
            observed_before = $false
            observed_after = $true
            preview_status = [string]$retouchResult.preview_status
            apply_status = [string]$retouchResult.apply_status
            public_control = 'Réagit au danger par la fuite'
        }
    }
    return [ordered]@{ action='add_danger_friendly'; name=$Name; x=$X; z=$Z; flees_danger=$true; add_result=$addResult; retouch_result=$retouchResult; runtime_claim='authoring_only; no danger or navigation claim' }
}

function Attach-PhysicsSupport {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][string]$Parent,
        [Parameter(Mandatory)][string]$Child,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    [void](Wait-GameplayForm -Socket $Socket -TimeoutSeconds $TimeoutSeconds)
    Select-GameplayOptionByText -Socket $Socket -Label 'Élément à retoucher' -Text $Parent -TimeoutSeconds $TimeoutSeconds
    [void](Open-DetailsBySummary -Socket $Socket -Summary 'Liaisons et mécanismes')
    Select-GameplayOptionByText -Socket $Socket -Label 'Charge portée' -Text $Child -TimeoutSeconds $TimeoutSeconds
    $result = Invoke-GameplayPreviewApply -Socket $Socket -PreviewButton 'Examiner le support et sa charge' -Description "attach $Child to $Parent" -TimeoutSeconds $TimeoutSeconds
    return [ordered]@{ action='attach_support'; parent=$Parent; child=$Child; result=$result; runtime_claim='logical support relation only; no physical contact claim' }
}

function Set-PhysicsActorFleeFlag {
    param([Parameter(Mandatory)]$Socket, [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][bool]$FleesDanger, [Parameter(Mandatory)][int]$TimeoutSeconds)
    Select-GameplayOptionByText -Socket $Socket -Label 'Élément à retoucher' -Text $Name -TimeoutSeconds $TimeoutSeconds
    [void](Open-DetailsBySummary -Socket $Socket -Summary 'Statistiques du personnage sélectionné')
    $before = Get-UiControlByLabel -Socket $Socket -Label 'Réagit au danger par la fuite' -DetailsSummary 'Statistiques du personnage sélectionné'
    if ([bool]$before.disabled) { throw "Public flee control is disabled for $Name." }
    $result = 'noop_already_present'
    if ([bool]$before.checked -ne $FleesDanger) {
        [void](Set-UiControlByLabel -Socket $Socket -Label 'Réagit au danger par la fuite' -Value $FleesDanger -Kind checked -DetailsSummary 'Statistiques du personnage sélectionné')
        $after = Get-UiControlByLabel -Socket $Socket -Label 'Réagit au danger par la fuite' -DetailsSummary 'Statistiques du personnage sélectionné'
        if ([bool]$after.checked -ne $FleesDanger) { throw "Public flee control did not retain $FleesDanger for $Name." }
        $result = Invoke-GameplayPreviewApply -Socket $Socket -PreviewButton 'Examiner les statistiques' -Description "set flee flag $Name" -TimeoutSeconds $TimeoutSeconds
    }
    return [ordered]@{action='set_actor_flee_flag';name=$Name;field='flees_danger';observed_before=[bool]$before.checked;requested=$FleesDanger;result=$result;runtime_claim='authoring_only'}
}

function Get-PhysicsSavedEntity {
    param(
        [Parameter(Mandatory)][object]$Gameplay,
        [Parameter(Mandatory)][string]$Collection,
        [Parameter(Mandatory)][string]$Name
    )
    $entries = @(Get-PhysicsProperty -Object $Gameplay -Name $Collection)
    return @($entries | Where-Object { [string]$_.name -ceq $Name })
}

function Assert-PhysicsParentGameplayPreserved {
    param([Parameter(Mandatory)][object]$Before, [Parameter(Mandatory)][object]$After,
        [Parameter(Mandatory)][bool]$GuardianFleeRetouched)
    foreach ($property in $Before.PSObject.Properties) {
        $collection = [string]$property.Name
        if ($collection -in @('actors','objects','places','dialogues','items','quests')) {
            foreach ($entry in @($property.Value)) {
                $matches = @((Get-PhysicsProperty -Object $After -Name $collection) | Where-Object { [string]$_.id -ceq [string]$entry.id })
                if ($matches.Count -ne 1) { throw "Final gameplay did not preserve parent $collection entry $($entry.id)." }
                $beforeFields = [ordered]@{}; $afterFields = [ordered]@{}
                foreach ($field in $entry.PSObject.Properties) {
                    if ($GuardianFleeRetouched -and $collection -ceq 'actors' -and [string]$entry.name -ceq 'Gardien de la forêt' -and $field.Name -ceq 'flees_danger') { continue }
                    $beforeFields[$field.Name] = $field.Value
                    $afterFields[$field.Name] = $matches[0].PSObject.Properties[$field.Name].Value
                }
                if (($beforeFields | ConvertTo-Json -Depth 100 -Compress) -cne ($afterFields | ConvertTo-Json -Depth 100 -Compress)) {
                    throw "Final gameplay altered parent $collection entry $($entry.id) beyond the explicit guardian flee flag."
                }
            }
        } elseif ((ConvertTo-Json -InputObject $property.Value -Depth 100 -Compress) -cne (ConvertTo-Json -InputObject $After.PSObject.Properties[$collection].Value -Depth 100 -Compress)) {
            throw "Final gameplay altered parent field $collection."
        }
    }
}

if ($physicsRequestedSelfTest) {
    foreach ($name in @(
        'ConvertTo-CompactJson', 'ConvertFrom-ExactJson', 'Invoke-CdpCommand',
        'Invoke-CdpExpression', 'Wait-CdpReady', 'Wait-CdpTarget', 'Connect-CdpSocket',
        'New-LoopbackPortReservation', 'Wait-LaunchedExecutableIdentity', 'Wait-NonGenericTask',
        'Send-CdpMouseClick', 'Invoke-UiClick', 'Invoke-UiClickInDetailsBySummary',
        'Open-DetailsBySummary', 'Set-UiControlByLabel', 'Get-UiControlByLabel',
        'Set-UiControlByLabelWithKeyboard', 'Select-GameplayOptionByText',
        'Copy-IsolatedFixture', 'Get-ArchiveSnapshot', 'Test-ArchiveSnapshotsEqual',
        'Get-FileSha256', 'Get-NormalizedSha256', 'Assert-DriveEPath',
        'Assert-NoExistingReparseAncestor', 'Assert-TreeHasNoReparsePoints',
        'Test-PathWithinDirectory', 'Test-IsWindowsPlatform', 'Open-Gate3TidewaterZone',
        'Wait-GameplayEditor', 'Wait-GameplayForm', 'Wait-GameplayPreview',
        'Wait-GameplayApply', 'Invoke-GameplayPreviewApply', 'Set-GameplayForm',
        'Set-GameplayPhysicsForm', 'Get-Gate3WorkspaceReference', 'Get-Gate3ArchivePlacements',
        'Get-Gate3GameplayReport', 'Save-GameplayUiDiagnostic', 'Get-PhysicsProperty',
        'Wait-PhysicsGameplayIdle', 'Set-PhysicsFireControls', 'Set-PhysicsRecipeForm',
        'Assert-Gate3ProofPath',
        'Add-PhysicsRecipeProp', 'Attach-PhysicsSupport', 'Add-PhysicsDangerFriendly'
    )) {
        [void](Get-Command $name -ErrorAction Stop)
    }
    foreach ($binding in @(
        [ordered]@{ command='Set-GameplayForm'; parameter='Reply' },
        [ordered]@{ command='Set-GameplayForm'; parameter='CenterY' },
        [ordered]@{ command='Set-GameplayPhysicsForm'; parameter='BreakEnergy' },
        [ordered]@{ command='Set-PhysicsRecipeForm'; parameter='CenterY' },
        [ordered]@{ command='Set-PhysicsRecipeForm'; parameter='BreakEnergy' }
    )) {
        $metadata = (Get-Command $binding.command -ErrorAction Stop).Parameters[$binding.parameter]
        $allowsEmpty = @($metadata.Attributes | Where-Object { $_.GetType().Name -eq 'AllowEmptyStringAttribute' }).Count -gt 0
        if (-not $allowsEmpty) { throw "Public helper binding $($binding.command).$($binding.parameter) does not allow an empty string." }
    }
    $raw = [IO.File]::ReadAllText($PSCommandPath)
    foreach ($needle in @(
        'Invoke-Gate3GameplayAuthoringSmoke.ps1', 'PreviousLedger',
        'Plateforme de lancer', 'Charge de lancer', 'Support piège', 'Charge piège', 'Bloc de contrepoids',
        'Balise inflammable', 'Veilleur du danger', '58.87091056823731',
        '59.16591056823731', '59.546617736816406', '59.8666177368164',
        'Masse (kg)', 'Propriétés physiques', 'Inflammable',
        'Dégâts de chaleur par tick (0 = aucun dégât)', "Examiner l’ajout",
        'Examiner le support et sa charge', 'Réagit au danger par la fuite',
        'Statistiques du personnage sélectionné', 'noop_already_present', 'changed_and_applied', 'Wait-PhysicsGameplayIdle',
        'Quitter sans enregistrer', 'gate_qualified=$false', 'authoring_only',
        'runtime_claim', 'source_original_archive_preserved', 'head_receipts',
        'parent_candidate_sha256', 'candidate_differs_from_parent', 'IncludeShoreFire', 'IncludeImpactTarget',
        'Feu de rive', '-0.21999999999999997', 'shore_fire', 'sampled_floor_y=-0.52'
    )) {
        if ($raw.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "Physics smoke self-test selector missing: $needle" }
    }
    $forbiddenTokens = @(
        ('Invoke-' + 'Tauri'),
        ('Tauri' + 'Command'),
        ('GameplayDocument' + ' {'),
        ('head.corrections.gameplay' + ' =')
    )
    foreach ($forbidden in $forbiddenTokens) {
        if ($raw.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) { throw "Physics smoke contains forbidden direct mutation/runtime token: $forbidden" }
    }
    if ($raw -notmatch 'Set-GameplayPhysicsForm[\s\S]{0,700}Set-PhysicsFireControls') {
        throw 'Fire controls are not applied after the shared defaults helper.'
    }
    Write-Output 'TIDEWATER_GATE3_PHYSICS_AUTHORING_SMOKE_SELF_TEST_PASS'
    return
}

if (-not (Test-IsWindowsPlatform)) { throw 'Windows/WebView2 is required.' }
$candidate = [IO.Path]::GetFullPath($Candidate)
$source = [IO.Path]::GetFullPath($FreshIsolatedFixture)
$ledgerInput = [IO.Path]::GetFullPath($PreviousLedger)
$output = [IO.Path]::GetFullPath($EvidenceRoot)
$sourcePin = Get-NormalizedSha256 -Value $SourceOriginalHashPinned
foreach ($entry in @(
    @($candidate, 'Candidate'),
        @($source, 'Base authoring fixture'),
        @($ledgerInput, 'Previous authoring ledger'),
        @($output, 'Evidence root')
    )) {
    Assert-Gate3ProofPath -LiteralPath $entry[0] -Description $entry[1]
}
if (Test-Path -LiteralPath $output) { throw "EvidenceRoot must be fresh: $output" }
if ((Test-PathWithinDirectory -LiteralPath $output -Directory $source) -or
    (Test-PathWithinDirectory -LiteralPath $source -Directory $output)) {
    throw 'EvidenceRoot and base fixture must remain separate.'
}
if ((Test-PathWithinDirectory -LiteralPath $output -Directory (Split-Path -Parent $candidate)) -or
    (Test-PathWithinDirectory -LiteralPath (Split-Path -Parent $candidate) -Directory $output)) {
    throw 'EvidenceRoot and candidate directory must remain separate.'
}
if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "Candidate absent: $candidate" }
if (-not (Test-Path -LiteralPath $ledgerInput -PathType Leaf)) { throw "Previous authoring ledger absent: $ledgerInput" }
if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "Base authoring fixture absent: $source" }
Assert-TreeHasNoReparsePoints -LiteralPath $source -Description 'Base Tidewater authoring fixture'

$candidateSha256 = Get-NormalizedSha256 -Value (Get-FileSha256 -LiteralPath $candidate)
$proofPath = Join-Path (Split-Path -Parent $candidate) 'build-proof.json'
$candidateProof = $null
if (Test-Path -LiteralPath $proofPath -PathType Leaf) {
    $candidateProof = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($proofPath))
    if ($candidateProof.PSObject.Properties.Name -contains 'executable_sha256' -and
        (Get-NormalizedSha256 -Value ([string]$candidateProof.executable_sha256)) -cne $candidateSha256) {
        throw 'Candidate executable differs from build-proof.json.'
    }
}
$candidateKitAndImports = Assert-Gate3CandidateInputs -CandidateExecutable $candidate

$previous = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($ledgerInput))
if ([string](Get-PhysicsProperty -Object $previous -Name 'schema') -cne 'forge.tidewater.gate3-gameplay-authoring-smoke' -or
    [string](Get-PhysicsProperty -Object $previous -Name 'status') -cne 'passed') {
    throw 'Previous ledger is not a passed Studio authoring ledger.'
}
if ([bool](Get-PhysicsProperty -Object $previous -Name 'gate_qualified')) {
    throw 'Previous ledger incorrectly claims Gate qualification.'
}
if (-not [bool](Get-PhysicsProperty -Object $previous -Name 'source_preserved')) {
    throw 'Previous ledger does not attest source preservation.'
}
$previousSourcePin = Get-PhysicsProperty -Object $previous -Name 'source_original_hash_pinned'
if (-not [string]::IsNullOrWhiteSpace([string]$previousSourcePin) -and
    (Get-NormalizedSha256 -Value ([string]$previousSourcePin)) -cne $sourcePin) {
    throw 'SourceOriginalHashPinned differs from the previous authoring ledger.'
}
$previousCandidateHash = Get-PhysicsProperty -Object $previous -Name 'candidate_sha256'
if ([string]::IsNullOrWhiteSpace([string]$previousCandidateHash)) {
    throw 'Previous authoring ledger has no parent candidate SHA-256 authorization record.'
}
$previousCandidateHash = Get-NormalizedSha256 -Value ([string]$previousCandidateHash)
if ($previousCandidateHash -ceq $candidateSha256 -and -not $CompletePhysicsFixture) {
    throw 'Physics authoring V2 requires candidate SHA-256 to differ from the parent V1 candidate.'
}
$previousFixtureMatches = $false
foreach ($field in @('source_fixture', 'isolated_fixture')) {
    $value = [string](Get-PhysicsProperty -Object $previous -Name $field)
    if (-not [string]::IsNullOrWhiteSpace($value) -and
        [IO.Path]::GetFullPath($value).Equals($source, [StringComparison]::OrdinalIgnoreCase)) {
        $previousFixtureMatches = $true
    }
}
if (-not $previousFixtureMatches) { throw 'Base fixture is not the fixture recorded by the previous ledger.' }

$previousReference = Get-PhysicsProperty -Object $previous -Name 'saved_reference'
$expectedCheckpointArchiveHash = Get-NormalizedSha256 -Value ([string](Get-PhysicsProperty -Object $previousReference -Name 'archive_hash'))
$expectedCheckpointHeadHash = Get-NormalizedSha256 -Value ([string](Get-PhysicsProperty -Object $previousReference -Name 'head_hash'))
if ($expectedCheckpointArchiveHash -notmatch '^[0-9a-f]{64}$' -or $expectedCheckpointHeadHash -notmatch '^[0-9a-f]{64}$') {
    throw 'Previous ledger saved_reference archive/head hashes are malformed.'
}

$sourceArchive = Join-Path (Join-Path $source $zoneId) 'authoring.json'
$sourceWorkspaceArchive = Join-Path $source 'workspace-v1\authoring.json'
$sourceOriginalObject = Join-Path (Join-Path $source 'workspace-v1\objects') "$sourcePin.json"
foreach ($path in @($sourceArchive, $sourceWorkspaceArchive, $sourceOriginalObject)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Base source archive is absent: $path" }
}
$sourceReference = Get-Gate3WorkspaceReference -Archive $sourceWorkspaceArchive -ZoneId $zoneId
$currentCheckpointArchiveHash = Get-NormalizedSha256 -Value ([string]$sourceReference.archive_hash)
$currentCheckpointHeadHash = Get-NormalizedSha256 -Value ([string]$sourceReference.head_hash)
if ($currentCheckpointArchiveHash -cne $expectedCheckpointArchiveHash -or
    $currentCheckpointHeadHash -cne $expectedCheckpointHeadHash) {
    throw "Base workspace checkpoint differs from the parent ledger: archive=$currentCheckpointArchiveHash/head=$currentCheckpointHeadHash expected archive=$expectedCheckpointArchiveHash/head=$expectedCheckpointHeadHash"
}
$sourceCurrentObject = Join-Path (Join-Path $source 'workspace-v1\objects') "$currentCheckpointArchiveHash.json"
if (-not (Test-Path -LiteralPath $sourceCurrentObject -PathType Leaf)) { throw "Current gameplay object is absent: $sourceCurrentObject" }

$sourceZoneBefore = Get-ArchiveSnapshot -LiteralPath $sourceArchive
$sourceWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $sourceWorkspaceArchive
$sourceOriginalBefore = Get-ArchiveSnapshot -LiteralPath $sourceOriginalObject
$sourceCurrentBefore = Get-ArchiveSnapshot -LiteralPath $sourceCurrentObject
if ($sourceZoneBefore.Sha256 -cne $sourcePin -or $sourceOriginalBefore.Sha256 -cne $sourcePin) {
    throw 'Original Tidewater zone/object does not match SourceOriginalHashPinned.'
}
if ($sourceCurrentBefore.Sha256 -cne $currentCheckpointArchiveHash) {
    throw 'Current authored gameplay object hash does not match workspace archive_hash.'
}
if (@(Get-Gate3ArchivePlacements -Archive $sourceCurrentObject).Count -ne 23) {
    throw 'Current authored object does not preserve the 23 Tidewater placements.'
}
$sourceCurrentDocument = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($sourceCurrentObject))
$sourceCurrentReport = Get-Gate3GameplayReport -Archive $sourceCurrentObject
if (-not $sourceCurrentReport.present) { throw 'Current authored object has no GameplayDocument.' }
if ((Get-PhysicsLastReceiptHash -Document $sourceCurrentDocument) -cne $currentCheckpointHeadHash) {
    throw 'Current authored object receipts do not end at the parent head_hash.'
}
$forbiddenPhysicsNames = @(
    $physicsPlatformName, $physicsLaunchChargeName, $physicsTrapSupportName,
    $physicsTrapChargeName, $physicsFireName, $physicsDangerPlaceName,
    $physicsDangerFriendlyName
)
if ($IncludeShoreFire) { $forbiddenPhysicsNames += $physicsShoreFireName }
if ($IncludeImpactTarget) { $forbiddenPhysicsNames += $physicsImpactTargetName }
if ($CompletePhysicsFixture) { $forbiddenPhysicsNames += $physicsShorePushName }
foreach ($name in $forbiddenPhysicsNames) {
    if (@($sourceCurrentReport.names.Keys | Where-Object { [string]$_ -ceq $name }).Count -gt 0) {
        throw "Base authored document already contains the new physics recipe name: $name"
    }
}

[void](New-Item -ItemType Directory -Path $output)
$fixture = Join-Path $output 'physics-authoring-root'
$cloneArchive = Copy-IsolatedFixture -SourceRoot $source -DestinationRoot $fixture -ZoneId $zoneId
$cloneWorkspaceArchive = Join-Path $fixture 'workspace-v1\authoring.json'
$cloneOriginalObject = Join-Path (Join-Path $fixture 'workspace-v1\objects') "$sourcePin.json"
$cloneCurrentObject = Join-Path (Join-Path $fixture 'workspace-v1\objects') "$currentCheckpointArchiveHash.json"
$cloneZoneBefore = Get-ArchiveSnapshot -LiteralPath $cloneArchive
$cloneWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $cloneWorkspaceArchive
$cloneOriginalBefore = Get-ArchiveSnapshot -LiteralPath $cloneOriginalObject
$cloneCurrentBefore = Get-ArchiveSnapshot -LiteralPath $cloneCurrentObject
$cloneReferenceBefore = Get-Gate3WorkspaceReference -Archive $cloneWorkspaceArchive -ZoneId $zoneId
if ($cloneZoneBefore.Sha256 -cne $sourceZoneBefore.Sha256 -or
    $cloneOriginalBefore.Sha256 -cne $sourceOriginalBefore.Sha256 -or
    $cloneCurrentBefore.Sha256 -cne $sourceCurrentBefore.Sha256 -or
    [string]$cloneReferenceBefore.archive_hash -cne [string]$sourceReference.archive_hash -or
    [string]$cloneReferenceBefore.head_hash -cne [string]$sourceReference.head_hash) {
    throw 'Isolated physics fixture does not preserve the parent checkpoint and original source.'
}

$profile = Join-Path $output 'webview2-user-data'
[void](New-Item -ItemType Directory -Path $profile)
$savedEnvironment = @{}
foreach ($name in @('FORGE_AUTHORING_ROOT', 'FORGE_AUTHOR_SUBJECT', 'WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', 'WEBVIEW2_USER_DATA_FOLDER')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$process = $null
$socket = $null
$reservation = $null
$port = 0
$failure = $null
$forcedProcessStop = $false
$nativeCloseConfirmed = $false
$uiClosed = $false
$savedWorkspace = $null
$savedReference = $null
$savedObjectPath = $null
$savedObject = $null
$savedDocument = $null
$savedGameplayReport = $null
$steps = [System.Collections.Generic.List[object]]::new()
$sourcePreserved = $false
$cloneLegacyPreserved = $false
$headReceipts = $null

try {
    $reservation = New-LoopbackPortReservation
    $port = $reservation.Port
    [Environment]::SetEnvironmentVariable('FORGE_AUTHORING_ROOT', $fixture, 'Process')
    [Environment]::SetEnvironmentVariable('FORGE_AUTHOR_SUBJECT', 'tidewater-gate3-physics-authoring-smoke', 'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', "--remote-debugging-address=127.0.0.1 --remote-debugging-port=$port", 'Process')
    [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER', $profile, 'Process')
    $reservation.Listener.Stop()
    $process = Start-Process -FilePath $candidate -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $output 'candidate.stdout.log') `
        -RedirectStandardError (Join-Path $output 'candidate.stderr.log')
    [void](Wait-LaunchedExecutableIdentity -Process $process -Candidate $candidate -ExpectedHash $candidateSha256)
    $target = Wait-CdpTarget -Port $port -TimeoutSeconds $StartupTimeoutSeconds -Process $process
    $socket = Connect-CdpSocket -WebSocketUrl ([string]$target.webSocketDebuggerUrl)
    [void](Invoke-CdpCommand -Socket $socket -Method 'Runtime.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.enable')
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.bringToFront')
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'Studio authoring shell' -Expression @'
(() => ({ready:document.readyState==='complete'&&!!document.querySelector('.authoring-shell'),reason:'authoring shell pending'}))()
'@)
    Open-Gate3TidewaterZone -Socket $socket -ZoneId $zoneId -TimeoutSeconds $InteractionTimeoutSeconds
    [void](Invoke-UiClick -Socket $socket -Text 'Objets' -Selector '[data-authoring-tab-button]')
    [void](Wait-GameplayEditor -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds)
    [void](Open-DetailsBySummary -Socket $socket -Summary $editorSummary)
    [void](Wait-PhysicsGameplayIdle -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'physics gameplay editor idle')

    $steps.Add((Add-PhysicsRecipeProp -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Recipe @{
        Name=$physicsPlatformName; X=$physicsPlatformX; Z=$physicsPlatformZ; CenterY=$physicsPlatformY
        Mass='0'; Width=([string]$physicsPlatformWidth); Height='0.05'; Depth=([string]$physicsPlatformDepth); Pushable=$false; Throwable=$false; BreakEnergy=''
    }))
    $steps.Add((Add-PhysicsRecipeProp -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Recipe @{
        Name=$physicsLaunchChargeName; X=$physicsPlatformX; Z=$physicsPlatformZ; CenterY=$physicsLaunchChargeY
        Mass='30'; Width='0.5'; Height='0.5'; Depth='0.5'; Pushable=$false; Throwable=$true; BreakEnergy=''
    }))
    $steps.Add((Attach-PhysicsSupport -Socket $socket -Parent $physicsPlatformName -Child $physicsLaunchChargeName -TimeoutSeconds $InteractionTimeoutSeconds))

    if ($IncludeImpactTarget) {
        # Optional R3-B target.  It is authored as a separate dynamic body;
        # no support relation or runtime outcome is implied by this recipe.
        $steps.Add((Add-PhysicsRecipeProp -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Recipe @{
            Name=$physicsImpactTargetName; X=$physicsImpactTargetX; Z=$physicsImpactTargetZ; CenterY=$physicsImpactTargetY
            Mass='100'; Width='0.5'; Height='1'; Depth='0.5'; Pushable=$true; Throwable=$false; BreakEnergy=''
        }))
    }

    # The trap height is explicit and finite from the reviewed preflight. The
    # UI is required to accept it; no fallback writes or invented terrain Y
    # are allowed if the public form rejects the value.
    $steps.Add((Add-PhysicsRecipeProp -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Recipe @{
        Name=$physicsTrapSupportName; X=$physicsTrapSupportX; Z=$physicsTrapSupportZ; CenterY=$physicsTrapSupportY
        Mass='0'; Width='0.6'; Height='0.1'; Depth='0.6'; Pushable=$false; Throwable=$false; BreakEnergy='5'
    }))
    $steps.Add((Add-PhysicsRecipeProp -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Recipe @{
        Name=$physicsTrapChargeName; X=$physicsTrapSupportX; Z=$physicsTrapSupportZ; CenterY=$physicsTrapChargeY
        Mass='20'; Width='0.5'; Height='0.5'; Depth='0.5'; Pushable=$false; Throwable=$false; BreakEnergy=''
    }))
    $steps.Add((Attach-PhysicsSupport -Socket $socket -Parent $physicsTrapSupportName -Child $physicsTrapChargeName -TimeoutSeconds $InteractionTimeoutSeconds))

    $steps.Add((Add-PhysicsPlace -Socket $socket -Name $physicsDangerPlaceName -X $physicsDangerX -Z $physicsDangerZ -TimeoutSeconds $InteractionTimeoutSeconds))
    $steps.Add((Add-PhysicsDangerFriendly -Socket $socket -Name $physicsDangerFriendlyName -X $physicsDangerX -Z $physicsDangerZ -TimeoutSeconds $InteractionTimeoutSeconds))
    $steps.Add((Add-PhysicsRecipeProp -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Recipe @{
        Name=$physicsFireName; X=$physicsFireX; Z=$physicsFireZ; CenterY=$physicsFireY
        Mass='0'; Width='0.5'; Height='0.5'; Depth='0.5'; Pushable=$false; Throwable=$false; BreakEnergy=''; Fire=$true
    }))
    if ($IncludeShoreFire) {
        # The Y value is derived from the pinned ShoreSim raster at (40,-32):
        # floor=-0.5199999999999999 m plus the 0.30 m authored placement offset.
        $steps.Add((Add-PhysicsRecipeProp -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Recipe @{
            Name=$physicsShoreFireName; X=$physicsShoreFireX; Z=$physicsShoreFireZ; CenterY=$physicsShoreFireY
            Mass='0'; Width='0.5'; Height='0.5'; Depth='0.5'; Pushable=$false; Throwable=$false; BreakEnergy=''; Fire=$true; HeatDamagePerTick=$ShoreFireHeatDamagePerTick
        }))
    }

    if ($CompletePhysicsFixture) {
        $steps.Add((Add-PhysicsRecipeProp -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Recipe @{
            Name=$physicsShorePushName; X=$physicsShorePushX; Z=$physicsShorePushZ
            Mass='10'; Width='0.5'; Height='0.5'; Depth='0.5'; Pushable=$true; Throwable=$false; BreakEnergy=''
        }))
        $steps.Add((Set-PhysicsActorFleeFlag -Socket $socket -Name 'Gardien de la forêt' -FleesDanger $false -TimeoutSeconds $InteractionTimeoutSeconds))
    }

    [void](Wait-PhysicsGameplayIdle -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'physics authoring before save')
    $beforeSaveWorkspace = Get-ArchiveSnapshot -LiteralPath $cloneWorkspaceArchive
    [void](Invoke-UiClick -Socket $socket -Text 'Sauvegarder')
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'Physics authoring save' -Expression @'
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const status=norm(document.querySelector('.authoring-command-status')?.textContent);
  const saveState=norm(document.querySelector('.authoring-command-bar .gold-save-state')?.textContent);
  const context=norm(document.querySelector('.authoring-context')?.textContent);
  const busy=document.querySelector('.authoring-toolbar')?.getAttribute('aria-busy');
  const terminal=/refusée|erreur|invalide|impossible/i.test(status);
  return {ready:status==='Session synchronisée'&&saveState===''&&context!==''&&context!=='Aucune zone ouverte'&&busy==='false'&&!terminal,terminal,reason:'status='+status+'; save='+saveState+'; context='+context+'; busy='+busy};
})()
'@)
    $savedWorkspace = Get-ArchiveSnapshot -LiteralPath $cloneWorkspaceArchive
    if (Test-ArchiveSnapshotsEqual -Before $beforeSaveWorkspace -After $savedWorkspace) { throw 'Studio Save did not update the managed physics workspace checkpoint.' }
    $savedReference = Get-Gate3WorkspaceReference -Archive $cloneWorkspaceArchive -ZoneId $zoneId
    $savedArchiveHash = Get-NormalizedSha256 -Value ([string]$savedReference.archive_hash)
    $savedHeadHash = Get-NormalizedSha256 -Value ([string]$savedReference.head_hash)
    if ($savedArchiveHash -eq $currentCheckpointArchiveHash -or $savedHeadHash -eq $currentCheckpointHeadHash) {
        throw 'Studio Save did not advance both the managed archive and head checkpoint.'
    }
    if ($savedArchiveHash -notmatch '^[0-9a-f]{64}$' -or $savedHeadHash -notmatch '^[0-9a-f]{64}$') {
        throw 'Studio Save returned malformed archive/head hashes.'
    }
    $savedObjectPath = Join-Path (Join-Path $fixture 'workspace-v1\objects') "$savedArchiveHash.json"
    if (-not (Test-Path -LiteralPath $savedObjectPath -PathType Leaf)) { throw "Saved physics object is absent: $savedObjectPath" }
    $savedObject = Get-ArchiveSnapshot -LiteralPath $savedObjectPath
    if ($savedObject.Sha256 -cne $savedArchiveHash) { throw 'Saved physics object hash does not match workspace archive_hash.' }
    if (@(Get-Gate3ArchivePlacements -Archive $savedObjectPath).Count -ne 23) { throw 'Saved physics object did not preserve the 23 source placements.' }
    $savedDocument = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($savedObjectPath))
    $headReceipts = [ordered]@{
        gameplay_receipt_count=@(Get-PhysicsGameplayReceipts -Document $savedDocument).Count
        prior_gameplay_receipt_count=@(Get-PhysicsGameplayReceipts -Document $sourceCurrentDocument).Count
        last_resulting_hash=Get-PhysicsLastReceiptHash -Document $savedDocument
        workspace_head_hash=$savedHeadHash
    }
    if ([int]$headReceipts.gameplay_receipt_count -le [int]$headReceipts.prior_gameplay_receipt_count -or
        [string]$headReceipts.last_resulting_hash -cne $savedHeadHash) {
        throw 'Saved archive head receipts do not include the new public gameplay commands.'
    }
    $savedGameplayReport = Get-Gate3GameplayReport -Archive $savedObjectPath
    if (-not $savedGameplayReport.present) { throw 'Saved physics object has no GameplayDocument.' }
    $gameplay = $savedDocument.head.corrections.gameplay
    Assert-PhysicsParentGameplayPreserved -Before $sourceCurrentDocument.head.corrections.gameplay -After $gameplay -GuardianFleeRetouched ([bool]$CompletePhysicsFixture)
    foreach ($field in $sourceCurrentDocument.head.corrections.PSObject.Properties) {
        if ($field.Name -ceq 'gameplay') { continue }
        if ((ConvertTo-Json -InputObject $field.Value -Depth 100 -Compress) -cne
            (ConvertTo-Json -InputObject $savedDocument.head.corrections.PSObject.Properties[$field.Name].Value -Depth 100 -Compress)) {
            throw "Public physics authoring altered non-gameplay head correction $($field.Name)."
        }
    }
    $platformEntry = @(Get-PhysicsSavedEntity -Gameplay $gameplay -Collection 'objects' -Name $physicsPlatformName)
    $launchEntry = @(Get-PhysicsSavedEntity -Gameplay $gameplay -Collection 'objects' -Name $physicsLaunchChargeName)
    $impactTargetEntry = if ($IncludeImpactTarget) {
        @(Get-PhysicsSavedEntity -Gameplay $gameplay -Collection 'objects' -Name $physicsImpactTargetName)
    }
    else { @() }
    $trapSupportEntry = @(Get-PhysicsSavedEntity -Gameplay $gameplay -Collection 'objects' -Name $physicsTrapSupportName)
    $trapChargeEntry = @(Get-PhysicsSavedEntity -Gameplay $gameplay -Collection 'objects' -Name $physicsTrapChargeName)
    $fireEntry = @(Get-PhysicsSavedEntity -Gameplay $gameplay -Collection 'objects' -Name $physicsFireName)
    $shoreFireEntry = if ($IncludeShoreFire) { @(Get-PhysicsSavedEntity -Gameplay $gameplay -Collection 'objects' -Name $physicsShoreFireName) } else { @() }
    $dangerPlaceEntry = @(Get-PhysicsSavedEntity -Gameplay $gameplay -Collection 'places' -Name $physicsDangerPlaceName)
    $dangerFriendlyEntry = @(Get-PhysicsSavedEntity -Gameplay $gameplay -Collection 'actors' -Name $physicsDangerFriendlyName)
    $shorePushEntry = if ($CompletePhysicsFixture) { @(Get-PhysicsSavedEntity -Gameplay $gameplay -Collection 'objects' -Name $physicsShorePushName) } else { @() }
    $guardianEntry = @(Get-PhysicsSavedEntity -Gameplay $gameplay -Collection 'actors' -Name 'Gardien de la forêt')
    if ($platformEntry.Count -ne 1 -or $launchEntry.Count -ne 1 -or
        ($IncludeImpactTarget -and $impactTargetEntry.Count -ne 1) -or $trapSupportEntry.Count -ne 1 -or
        $trapChargeEntry.Count -ne 1 -or $fireEntry.Count -ne 1 -or
        ($IncludeShoreFire -and $shoreFireEntry.Count -ne 1) -or $dangerPlaceEntry.Count -ne 1 -or
        $dangerFriendlyEntry.Count -ne 1 -or ($CompletePhysicsFixture -and ($shorePushEntry.Count -ne 1 -or $guardianEntry.Count -ne 1))) {
        throw 'Saved physics authoring does not contain exactly one entry for every new recipe element.'
    }
    $platformPhysics = $platformEntry[0].physics
    $launchPhysics = $launchEntry[0].physics
    $trapSupportPhysics = $trapSupportEntry[0].physics
    $trapChargePhysics = $trapChargeEntry[0].physics
    $firePhysics = $fireEntry[0].physics
    if (-not (Test-PhysicsPosition -Position $platformEntry[0].position -X $physicsPlatformX -Z $physicsPlatformZ) -or
        [Math]::Abs([double]$platformEntry[0].position.y - $physicsPlatformY) -ge 0.0002 -or
        -not (Test-PhysicsHalfExtents -Physics $platformPhysics -Width $physicsPlatformWidth -Height 0.05 -Depth $physicsPlatformDepth) -or
        [double]$platformPhysics.mass_kg -ne 0 -or [bool]$platformPhysics.pushable -or [bool]$platformPhysics.throwable -or
        [bool]$platformPhysics.flammable -or [double]$platformPhysics.heat_damage_per_tick -ne 0 -or
        $null -ne $platformPhysics.break_energy_j) {
        throw 'Saved launch platform does not match the public fixed 0 kg recipe.'
    }
    if (-not (Test-PhysicsPosition -Position $launchEntry[0].position -X $physicsPlatformX -Z $physicsPlatformZ) -or
        [Math]::Abs([double]$launchEntry[0].position.y - $physicsLaunchChargeY) -ge 0.0002 -or
        -not (Test-PhysicsHalfExtents -Physics $launchPhysics -Width 0.5 -Height 0.5 -Depth 0.5) -or
        [double]$launchPhysics.mass_kg -ne 30 -or [bool]$launchPhysics.pushable -or -not [bool]$launchPhysics.throwable -or
        [string]$launchEntry[0].id -notin @($platformPhysics.supports)) {
        throw 'Saved 30 kg launch charge does not match the public supported recipe.'
    }
    if ($IncludeImpactTarget) {
        $impactTargetPhysics = $impactTargetEntry[0].physics
        $impactTargetBreakEnergy = Get-PhysicsProperty -Object $impactTargetPhysics -Name 'break_energy_j'
        if (-not (Test-PhysicsPosition -Position $impactTargetEntry[0].position -X $physicsImpactTargetX -Z $physicsImpactTargetZ) -or
            [Math]::Abs([double]$impactTargetEntry[0].position.y - $physicsImpactTargetY) -ge 0.0002 -or
            -not (Test-PhysicsHalfExtents -Physics $impactTargetPhysics -Width 0.5 -Height 1.0 -Depth 0.5) -or
            [double]$impactTargetPhysics.mass_kg -ne 100 -or -not [bool]$impactTargetPhysics.pushable -or
            [bool]$impactTargetPhysics.throwable -or $null -ne $impactTargetBreakEnergy) {
            throw 'Saved impact target does not match the public measured recipe.'
        }
    }
    if (-not (Test-PhysicsPosition -Position $trapSupportEntry[0].position -X $physicsTrapSupportX -Z $physicsTrapSupportZ) -or
        [Math]::Abs([double]$trapSupportEntry[0].position.y - $physicsTrapSupportY) -ge 0.0002 -or
        -not (Test-PhysicsHalfExtents -Physics $trapSupportPhysics -Width 0.6 -Height 0.1 -Depth 0.6) -or
        [double]$trapSupportPhysics.mass_kg -ne 0 -or [bool]$trapSupportPhysics.pushable -or [bool]$trapSupportPhysics.throwable -or
        [double]$trapSupportPhysics.break_energy_j -ne 5 -or
        [string]$trapChargeEntry[0].id -notin @($trapSupportPhysics.supports)) {
        throw 'Saved breakable trap support does not match the public recipe.'
    }
    if (-not (Test-PhysicsPosition -Position $trapChargeEntry[0].position -X $physicsTrapSupportX -Z $physicsTrapSupportZ) -or
        [Math]::Abs([double]$trapChargeEntry[0].position.y - $physicsTrapChargeY) -ge 0.0002 -or
        -not (Test-PhysicsHalfExtents -Physics $trapChargePhysics -Width 0.5 -Height 0.5 -Depth 0.5) -or
        [double]$trapChargePhysics.mass_kg -ne 20 -or [bool]$trapChargePhysics.pushable -or [bool]$trapChargePhysics.throwable) {
        throw 'Saved 20 kg trap charge does not match the public recipe.'
    }
    if (-not (Test-PhysicsPosition -Position $fireEntry[0].position -X $physicsFireX -Z $physicsFireZ) -or
        ($null -ne $physicsFireY -and [Math]::Abs([double]$fireEntry[0].position.y - $physicsFireY) -ge 0.0002) -or
        -not (Test-PhysicsHalfExtents -Physics $firePhysics -Width 0.5 -Height 0.5 -Depth 0.5) -or
        -not [bool]$firePhysics.flammable -or [double]$firePhysics.heat_damage_per_tick -ne 1) {
        throw 'Saved fire prop does not match the public v2 true/1 controls.'
    }
    if ($IncludeShoreFire) {
        $shoreFirePhysics = $shoreFireEntry[0].physics
        if (-not (Test-PhysicsPosition -Position $shoreFireEntry[0].position -X $physicsShoreFireX -Z $physicsShoreFireZ) -or
            [Math]::Abs([double]$shoreFireEntry[0].position.y - $physicsShoreFireY) -ge 0.0002 -or
            -not (Test-PhysicsHalfExtents -Physics $shoreFirePhysics -Width 0.5 -Height 0.5 -Depth 0.5) -or
            [double]$shoreFirePhysics.mass_kg -ne 0 -or [bool]$shoreFirePhysics.pushable -or
            [bool]$shoreFirePhysics.throwable -or -not [bool]$shoreFirePhysics.flammable -or
            [double]$shoreFirePhysics.heat_damage_per_tick -ne $ShoreFireHeatDamagePerTick) {
            throw "Saved shore fire does not match the public true/$ShoreFireHeatDamagePerTick recipe."
        }
    }
    if (-not (Test-PhysicsPosition -Position $dangerPlaceEntry[0].position -X $physicsDangerX -Z $physicsDangerZ) -or
        -not (Test-PhysicsPosition -Position $dangerFriendlyEntry[0].position -X $physicsDangerX -Z $physicsDangerZ) -or
        [string]$dangerFriendlyEntry[0].role -cne 'friendly' -or
        -not [bool]$dangerFriendlyEntry[0].flees_danger) {
        throw 'Saved danger friendly does not match the public place/role/flees_danger recipe.'
    }
    if ($CompletePhysicsFixture) {
        $shorePushPhysics = $shorePushEntry[0].physics
        $shorePushSupportParents = @($gameplay.objects | Where-Object { [string]$shorePushEntry[0].id -in @($_.physics.supports) })
        if (-not (Test-PhysicsPosition -Position $shorePushEntry[0].position -X $physicsShorePushX -Z $physicsShorePushZ) -or
            -not (Test-PhysicsHalfExtents -Physics $shorePushPhysics -Width 0.5 -Height 0.5 -Depth 0.5) -or
            [double]$shorePushPhysics.mass_kg -ne 10 -or -not [bool]$shorePushPhysics.pushable -or [bool]$shorePushPhysics.throwable -or
            $shorePushSupportParents.Count -ne 0 -or [bool]$guardianEntry[0].flees_danger -or [string]$guardianEntry[0].role -cne 'enemy') {
            throw 'Saved complete fixture differs from the independent shore Push and static guardian recipe.'
        }
    }
    $cloneLegacyPreserved = (Test-ArchiveSnapshotsEqual -Before $cloneZoneBefore -After (Get-ArchiveSnapshot -LiteralPath $cloneArchive)) -and
        (Test-ArchiveSnapshotsEqual -Before $cloneOriginalBefore -After (Get-ArchiveSnapshot -LiteralPath $cloneOriginalObject)) -and
        (Test-ArchiveSnapshotsEqual -Before $cloneCurrentBefore -After (Get-ArchiveSnapshot -LiteralPath $cloneCurrentObject))
    if (-not $cloneLegacyPreserved) { throw 'Physics Save altered the legacy zone or original/current source objects.' }

    [void](Invoke-UiClick -Socket $socket -Text 'Projet' -Selector '[data-authoring-tab-button]')
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Créer ou modifier la zone')
    [void](Invoke-UiClick -Socket $socket -Text 'Fermer la zone')
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $InteractionTimeoutSeconds -Description 'Close physics authored Tidewater zone' -Expression @'
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
    if ($null -ne $socket -and $socket.State -eq [Net.WebSockets.WebSocketState]::Open -and (Test-Path -LiteralPath $output -PathType Container)) {
        try { Save-GameplayUiDiagnostic -Socket $socket -OutputRoot $output -Name 'failure' }
        catch { Write-Verbose "Failure UI capture: $($_.Exception.Message)" }
    }
}
finally {
    # Keep CDP alive while the owned native window shows its public close
    # confirmation. Do not inject a close command or close another session.
    if ($null -ne $process -and -not $process.HasExited -and $null -ne $socket -and
        $socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
        try {
            $process.Refresh()
            if (-not $process.CloseMainWindow()) { throw 'Owned Studio window did not accept public close request.' }
            [void](Wait-CdpReady -Socket $socket -TimeoutSeconds 10 -Description 'Native Studio close confirmation' -Expression @'
(() => ({ready:!!document.querySelector('dialog.gold-close-dialog[open]'),terminal:false,reason:'native close dialog pending'}))()
'@)
            [void](Invoke-UiClick -Socket $socket -Text 'Quitter sans enregistrer' -ScopeSelector 'dialog.gold-close-dialog[open]')
            $nativeCloseConfirmed = $process.WaitForExit(10000)
        }
        catch { Write-Verbose "Native close confirmation: $($_.Exception.Message)" }
    }
    if ($null -ne $socket) {
        if ($socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
            try {
                $closing = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(2))
                try { Wait-NonGenericTask -Task ($socket.CloseAsync([Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'Gate 3 physics authoring smoke complete', $closing.Token)) }
                finally { $closing.Dispose() }
            }
            catch { Write-Verbose "CDP close: $($_.Exception.Message)" }
        }
        $socket.Dispose()
    }
    if ($null -ne $process -and -not $process.HasExited) {
        try { [void]$process.CloseMainWindow() } catch { Write-Verbose "CloseMainWindow: $($_.Exception.Message)" }
        if (-not $process.WaitForExit(5000)) {
            Stop-Process -Id $process.Id -Force
            [void]$process.WaitForExit(5000)
            $forcedProcessStop = $true
        }
    }
    if ($null -ne $reservation) { $reservation.Listener.Stop() }
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
}

$sourcePreserved = (Test-ArchiveSnapshotsEqual -Before $sourceZoneBefore -After (Get-ArchiveSnapshot -LiteralPath $sourceArchive)) -and
    (Test-ArchiveSnapshotsEqual -Before $sourceWorkspaceBefore -After (Get-ArchiveSnapshot -LiteralPath $sourceWorkspaceArchive)) -and
    (Test-ArchiveSnapshotsEqual -Before $sourceOriginalBefore -After (Get-ArchiveSnapshot -LiteralPath $sourceOriginalObject)) -and
    (Test-ArchiveSnapshotsEqual -Before $sourceCurrentBefore -After (Get-ArchiveSnapshot -LiteralPath $sourceCurrentObject))
if (-not $sourcePreserved) { $failure = "Base source archive/object changed. $failure" }
$processClosed = $null -eq $process -or $process.HasExited
if (-not $processClosed) { $failure = "Owned Studio process remained alive. $failure" }
if ($forcedProcessStop) { $failure = "Studio required a forced stop after public close. $failure" }
$ledgerStatus = 'failed_closed'
if ([string]::IsNullOrWhiteSpace($failure)) { $ledgerStatus = 'passed' }

$ledger = [ordered]@{
    schema='forge.tidewater.gate3-physics-authoring-smoke'
    version=1
    status=$ledgerStatus
    failure=$failure
    gate_qualified=$false
    human_visual_review=$false
    evidence_kind='public_studio_authoring_only'
    runtime_contact_claim=$false
    runtime_fire_claim=$false
    runtime_danger_claim=$false
    navigation_claim=$false
    candidate=$candidate
    candidate_sha256=$candidateSha256
    include_shore_fire=[bool]$IncludeShoreFire
    include_impact_target=[bool]$IncludeImpactTarget
    complete_physics_fixture=[bool]$CompletePhysicsFixture
    parent_candidate_sha256=$previousCandidateHash
    candidate_differs_from_parent=($candidateSha256 -cne $previousCandidateHash)
    candidate_build_proof=$candidateProof
    candidate_kit_and_imports=$candidateKitAndImports
    previous_authoring_ledger=$ledgerInput
    previous_saved_reference=$previousReference
    source_fixture=$source
    source_original_hash_pinned=$sourcePin
    parent_checkpoint=[ordered]@{ archive_hash=$currentCheckpointArchiveHash; head_hash=$currentCheckpointHeadHash }
    source_zone_before=$sourceZoneBefore
    source_zone_after=Get-ArchiveSnapshot -LiteralPath $sourceArchive
    source_workspace_before=$sourceWorkspaceBefore
    source_workspace_after=Get-ArchiveSnapshot -LiteralPath $sourceWorkspaceArchive
    source_original_before=$sourceOriginalBefore
    source_original_after=Get-ArchiveSnapshot -LiteralPath $sourceOriginalObject
    source_current_before=$sourceCurrentBefore
    source_current_after=Get-ArchiveSnapshot -LiteralPath $sourceCurrentObject
    source_original_archive_preserved=$sourcePreserved
    source_preserved=$sourcePreserved
    zone_uuid=$zoneId
    isolated_fixture=$fixture
    isolated_zone_before=$cloneZoneBefore
    isolated_workspace_before=$cloneWorkspaceBefore
    isolated_original_before=$cloneOriginalBefore
    isolated_current_before=$cloneCurrentBefore
    isolated_legacy_archives_preserved=$cloneLegacyPreserved
    saved_workspace=$savedWorkspace
    saved_reference=$savedReference
    saved_object=$savedObject
    saved_object_path=$savedObjectPath
    head_receipts=$headReceipts
    fire_controls_ui=$script:fireControlsObservation
    recipe=[ordered]@{
        launch_platform=[ordered]@{ name=$physicsPlatformName; x=$physicsPlatformX; y=$physicsPlatformY; z=$physicsPlatformZ; dimensions_m="$($physicsPlatformWidth)x0.05x$($physicsPlatformDepth)"; mass_kg=0; pushable=$false; throwable=$false }
        launch_charge=[ordered]@{ name=$physicsLaunchChargeName; x=$physicsPlatformX; y=$physicsLaunchChargeY; z=$physicsPlatformZ; dimensions_m='0.5x0.5x0.5'; mass_kg=30; pushable=$false; throwable=$true; supported_by=$physicsPlatformName; impulse_recipe='engine UI computes direction and uses speed=5, up=3; no runtime contact claim' }
        impact_target=if ($IncludeImpactTarget) {
            [ordered]@{ name=$physicsImpactTargetName; x=$physicsImpactTargetX; y=$physicsImpactTargetY; z=$physicsImpactTargetZ; dimensions_m='0.5x1x0.5'; mass_kg=100; pushable=$true; throwable=$false; runtime_claim='authoring only; initial stability and impact remain unclaimed' }
        } else { $null }
        trap_support=[ordered]@{ name=$physicsTrapSupportName; x=$physicsTrapSupportX; y=$physicsTrapSupportY; z=$physicsTrapSupportZ; dimensions_m='0.6x0.1x0.6'; mass_kg=0; break_energy_j=5; runtime_claim='authoring only; fixed support and break threshold persisted; no break proof' }
        trap_charge=[ordered]@{ name=$physicsTrapChargeName; x=$physicsTrapSupportX; y=$physicsTrapChargeY; z=$physicsTrapSupportZ; dimensions_m='0.5x0.5x0.5'; mass_kg=20; supported_by=$physicsTrapSupportName; runtime_claim='authoring only; drop/contact unclaimed' }
        fire=[ordered]@{ name=$physicsFireName; x=$physicsFireX; y=$physicsFireY; z=$physicsFireZ; flammable=$true; heat_damage_per_tick=1; risk='rapid heat damage/death is a runtime risk; no runtime claim' }
        shore_push=if ($CompletePhysicsFixture) { [ordered]@{name=$physicsShorePushName;x=$physicsShorePushX;y='public_canonical_terrain';z=$physicsShorePushZ;dimensions_m='0.5x0.5x0.5';mass_kg=10;pushable=$true;logical_support=$false;runtime_claim='authoring_only'} } else { $null }
        guardian=if ($CompletePhysicsFixture) { [ordered]@{name='Gardien de la forêt';flees_danger=$false;role='enemy';purpose='Remain available to the authored trap; Veilleur du danger retains the independent flee behavior.';runtime_claim='authoring_only'} } else { $null }
        shore_fire=if ($IncludeShoreFire) {
            [ordered]@{
                name=$physicsShoreFireName; x=$physicsShoreFireX; y=$physicsShoreFireY; z=$physicsShoreFireZ
                dimensions_m='0.5x0.5x0.5'; mass_kg=0; pushable=$false; throwable=$false
                flammable=$true; heat_damage_per_tick=$ShoreFireHeatDamagePerTick
                terrain_source=[ordered]@{
                    raster_sha256=$physicsTerrainBlobHash; origin_xz='(-1024,-1024)'; spacing_m=1
                    sampled_floor_y=-0.52; placement_offset_m=0.30
                }
                water_route='Public route must reach this measured shore candidate before Extinguish; authoring does not claim water contact.'
                runtime_claim='authoring only; no wet/contact/extinguish claim'
            }
        } else { $null }
        danger_friendly=[ordered]@{ name=$physicsDangerFriendlyName; place=$physicsDangerPlaceName; x=$physicsDangerX; z=$physicsDangerZ; role='friendly'; flees_danger=$true; runtime_claim='authoring only; no danger/nav claim' }
    }
    source_terrain_blob=[ordered]@{ name=$physicsTerrainBlobName; sha256=$physicsTerrainBlobHash; use='Y recipe provenance only; no asset/module branch selected' }
    steps=@($steps)
    ui_closed=$uiClosed
    owned_process_closed=$processClosed
    forced_process_stop=$forcedProcessStop
    native_close_confirmed=$nativeCloseConfirmed
    limitations=@(
        'This smoke proves only public Studio authoring, serialization, archive preservation and receipts.',
        'No Rapier contact, impulse, break/release, fire, heat damage, danger response or navigation result is claimed.',
    'The direct trap height is a measured preflight coordinate; the UI accepting it does not prove a visual mount or reachable projectile.',
    'Bloc de contrepoids is optional and uses the measured raster Y; its pushable body is an authoring receipt only, with no initial stability, contact, impact or guardian outcome claimed.',
        'No Tidewater asset branch or asset choice is made; every new entity is a generic Gameplay prop/actor/place.',
        'The fire fields are authored true/1 only when both public v2 controls are present; absent or legacy controls fail closed.'
        "Feu de rive is an optional generic prop at the measured ShoreSim raster point (40,-32), with Y=-0.22 from floor -0.52 plus a 0.30 m placement offset; heat_damage_per_tick=$ShoreFireHeatDamagePerTick is an authoring value and no water-contact or extinguish result is claimed here."
    )
}
$ledgerPath = Join-Path $output 'gate3-physics-authoring-smoke-ledger.json'
[IO.File]::WriteAllText($ledgerPath, ($ledger | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
if ($ledger.status -cne 'passed') { throw "Gate 3 physics authoring smoke failed closed: $failure. Ledger: $ledgerPath" }
if ($CompletePhysicsFixture) {
    # This separate replay authorization describes the publicly authored final
    # gameplay checkpoint. Preserve the base gameplay ledger and the full
    # extension proof; no previous runtime result is inherited.
    $finalGameplayLedger = [ordered]@{
        schema='forge.tidewater.gate3-gameplay-authoring-smoke';version=1;status='passed'
        evidence_kind='public_studio_gameplay_authoring_extension';gate_qualified=$false;human_visual_review=$false
        source_preserved=$sourcePreserved;zone_uuid=$zoneId;candidate=$candidate;candidate_sha256=$candidateSha256
        isolated_fixture=$fixture;source_fixture=$source;source_original_hash_pinned=$sourcePin
        saved_reference=$savedReference;saved_object_path=$savedObjectPath;saved_object=$savedObject
        gameplay_report=$savedGameplayReport;parent_gameplay_authoring_ledger=$ledgerInput
        parent_gameplay_authoring_ledger_sha256=Get-FileSha256 -LiteralPath $ledgerInput
        parent_saved_reference=$previousReference;physics_extension_ledger=$ledgerPath
        physics_extension_ledger_sha256=Get-FileSha256 -LiteralPath $ledgerPath
        authoring_steps=@($steps);runtime_results_inherited=$false;owned_process_closed=$processClosed;forced_process_stop=$forcedProcessStop
        parent_gameplay_entries_preserved=$true;explicit_parent_change='Gardien de la forêt.flees_danger=false'
        room_applied=Get-PhysicsProperty -Object $previous -Name 'room_applied'
        room_result=Get-PhysicsProperty -Object $previous -Name 'room_result'
        measured_floor_y=Get-PhysicsProperty -Object $previous -Name 'measured_floor_y'
        room_origin_x=Get-PhysicsProperty -Object $previous -Name 'room_origin_x'
        room_attestation_provenance=[ordered]@{parent_gameplay_authoring_ledger=$ledgerInput;non_gameplay_head_corrections_identical=$true;existing_gameplay_entries_preserved=$true;runtime_results_inherited=$false}
    }
    $finalGameplayLedgerPath = Join-Path $output 'gate3-final-gameplay-authoring-ledger.json'
    [IO.File]::WriteAllText($finalGameplayLedgerPath, ($finalGameplayLedger | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
    Write-Output "TIDEWATER_GATE3_FINAL_GAMEPLAY_AUTHORING_LEDGER ledger=$finalGameplayLedgerPath"
}
Write-Output "TIDEWATER_GATE3_PHYSICS_AUTHORING_SMOKE_PASS ledger=$ledgerPath"
