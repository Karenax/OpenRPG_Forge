#Requires -Version 7.2
# Gate 3 environment save evidence verifier.  It reads only public Save files
# captured after the Studio Save button; it never starts Studio or edits a save.
[CmdletBinding(DefaultParameterSetName = 'Run')]
param(
    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$BeforeSavePath,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$AfterSavePath,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$EvidenceRoot,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$TrackedActorId,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string[]]$TrackedObjectId,

    [Parameter(ParameterSetName = 'Run')]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$ExpectedSourceHash,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(0.001, 5.0)]
    [double]$ActorMovementToleranceM = 0.20,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$requestedSelfTest = [bool]$SelfTest
$requestedBefore = $BeforeSavePath
$requestedAfter = $AfterSavePath
$requestedEvidence = $EvidenceRoot
$requestedActor = $TrackedActorId
$requestedObjects = $TrackedObjectId
$requestedExpectedSource = $ExpectedSourceHash
$requestedTolerance = $ActorMovementToleranceM
. (Join-Path $PSScriptRoot '..\Invoke-Gate2WorldEditingProof.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Gate3ProofStorage.ps1')
if (-not $requestedSelfTest) {
    $BeforeSavePath = $requestedBefore
    $AfterSavePath = $requestedAfter
    $EvidenceRoot = $requestedEvidence
    $TrackedActorId = $requestedActor
    $TrackedObjectId = $requestedObjects
    $ExpectedSourceHash = $requestedExpectedSource
    $ActorMovementToleranceM = $requestedTolerance
}

function Get-EnvironmentSaveProperty {
    param(
        [Parameter(Mandatory)]$Document,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$PathDescription
    )
    if (-not ($Document.PSObject.Properties.Name -contains $Name)) {
        throw "$PathDescription is missing required save property '$Name'."
    }
    return $Document.$Name
}

function Get-EnvironmentSaveMapEntry {
    param(
        [Parameter(Mandatory)]$Map,
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$MapName,
        [Parameter(Mandatory)][string]$PathDescription
    )
    if ($null -eq $Map) { throw "$PathDescription has no '$MapName' map." }
    $matches = @($Map.PSObject.Properties | Where-Object { $_.Name -ceq $Id })
    if ($matches.Count -ne 1) { throw "$PathDescription '$MapName' does not contain exactly one tracked id '$Id'." }
    return $matches[0].Value
}

function Get-EnvironmentPosition {
    param(
        [Parameter(Mandatory)]$State,
        [Parameter(Mandatory)][string]$PathDescription
    )
    if ($null -eq $State.position -or @($State.position).Count -ne 3) {
        throw "$PathDescription has no finite three-component position."
    }
    $values = @($State.position | ForEach-Object { [double]$_ })
    if ($values | Where-Object { -not [double]::IsFinite($_) }) { throw "$PathDescription has a non-finite position." }
    return $values
}

function Get-EnvironmentPositionDelta {
    param(
        [Parameter(Mandatory)][double[]]$Before,
        [Parameter(Mandatory)][double[]]$After
    )
    $dx = $After[0] - $Before[0]
    $dy = $After[1] - $Before[1]
    $dz = $After[2] - $Before[2]
    return [Math]::Sqrt(($dx * $dx) + ($dy * $dy) + ($dz * $dz))
}

function Get-EnvironmentEventRows {
    param(
        [Parameter(Mandatory)]$State,
        [Parameter(Mandatory)][string]$PathDescription
    )
    $rows = [System.Collections.Generic.List[object]]::new()
    foreach ($receipt in @($State.receipts)) {
        foreach ($event in @($receipt.events)) {
            foreach ($property in @($event.PSObject.Properties)) {
                if ($property.Name -in @('PickedUp', 'QuestChanged', 'Damage', 'Impact', 'DoorChanged', 'Impulse', 'Destroyed', 'Released', 'Burning', 'Fleeing')) {
                    [void]$rows.Add([pscustomobject]@{ kind = $property.Name; payload = $property.Value; source = 'receipt'; path = $PathDescription })
                }
            }
        }
    }
    foreach ($contactProperty in @($State.contacts.PSObject.Properties)) {
        $contact = $contactProperty.Value
        foreach ($event in @($contact.events)) {
            foreach ($property in @($event.PSObject.Properties)) {
                if ($property.Name -in @('PickedUp', 'QuestChanged', 'Damage', 'Impact', 'DoorChanged', 'Impulse', 'Destroyed', 'Released', 'Burning', 'Fleeing')) {
                    [void]$rows.Add([pscustomobject]@{ kind = $property.Name; payload = $property.Value; source = 'contact'; path = $PathDescription })
                }
            }
        }
    }
    if ($null -ne $State.last_frame) {
        foreach ($event in @($State.last_frame.events)) {
            foreach ($property in @($event.PSObject.Properties)) {
                if ($property.Name -in @('PickedUp', 'QuestChanged', 'Damage', 'Impact', 'DoorChanged', 'Impulse', 'Destroyed', 'Released', 'Burning', 'Fleeing')) {
                    [void]$rows.Add([pscustomobject]@{ kind = $property.Name; payload = $property.Value; source = 'last_frame'; path = $PathDescription })
                }
            }
        }
    }
    return @($rows)
}

function Read-PublicEnvironmentSave {
    param([Parameter(Mandatory)][string]$LiteralPath)
    $full = [IO.Path]::GetFullPath($LiteralPath)
    Assert-Gate3ProofPath -LiteralPath $full -Description 'Public game-save file'
    if ([IO.Path]::GetFileName($full) -cne 'authoring.json' -or $full -notmatch '(?i)\\\.game-saves\\[^\\]+\\[^\\]+\\authoring\.json$') {
        throw "Public game-save path must be .game-saves\\<module>\\<area>\\authoring.json: $full"
    }
    $item = Get-Item -LiteralPath $full -Force
    if (-not $item.PSIsContainer -and (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0)) {
        $bytes = [IO.File]::ReadAllBytes($full)
    }
    else { throw "Public game-save must be a regular file: $full" }
    if ($bytes.Length -gt (4 * 1024 * 1024)) { throw "Public game-save exceeds 4 MiB: $full" }
    try { $document = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json -Depth 100 } catch { throw "Public game-save is not valid JSON: $full; $($_.Exception.Message)" }
    $schema = [string](Get-EnvironmentSaveProperty -Document $document -Name 'schema' -PathDescription $full)
    $version = [int](Get-EnvironmentSaveProperty -Document $document -Name 'version' -PathDescription $full)
    if ($schema -cne 'forge.game-save' -or $version -ne 1) { throw "Public game-save schema/version mismatch: $full" }
    $state = Get-EnvironmentSaveProperty -Document $document -Name 'state' -PathDescription $full
    foreach ($name in @('actors', 'objects', 'receipts', 'contacts', 'last_frame', 'requires_physics_revalidation')) {
        [void](Get-EnvironmentSaveProperty -Document $state -Name $name -PathDescription "$full state")
    }
    return [pscustomobject]@{
        path = $full
        file_sha256 = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant()
        document = $document
        state = $state
        source_hash = [string](Get-EnvironmentSaveProperty -Document $document -Name 'source_hash' -PathDescription $full)
        module_id = [string](Get-EnvironmentSaveProperty -Document $document -Name 'module_id' -PathDescription $full)
        area_id = [string](Get-EnvironmentSaveProperty -Document $document -Name 'area_id' -PathDescription $full)
        state_hash = [string](Get-EnvironmentSaveProperty -Document $document -Name 'state_hash' -PathDescription $full)
    }
}

if ($SelfTest) {
    foreach ($name in @('Assert-Gate3ProofPath', 'Read-PublicEnvironmentSave', 'Get-EnvironmentEventRows', 'Get-EnvironmentSaveMapEntry', 'Get-EnvironmentPositionDelta')) {
        [void](Get-Command $name -ErrorAction Stop)
    }
    $raw = [IO.File]::ReadAllText($PSCommandPath)
    foreach ($needle in @('forge.game-save', '.game-saves', 'state_hash', 'last_frame', 'requires_physics_revalidation', 'gate_qualified = $false', 'read_only = $true')) {
        if ($raw.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "Environment save verifier contract missing: $needle" }
    }
    foreach ($forbidden in @(('window.' + '__'), ('Game' + 'Save' + 'Request'), ('Studio' + 'Command::' + 'GameSave'), ('AuthoringRequest::' + 'Apply'), ('AuthoringRequest::' + 'Preview'))) {
        if ($raw.IndexOf($forbidden, [StringComparison]::Ordinal) -ge 0) { throw "Environment save verifier contains forbidden injection marker: $forbidden" }
    }
    $delta = Get-EnvironmentPositionDelta -Before ([double[]]@(0, 0, 0)) -After ([double[]]@(0.3, 0.4, 0))
    if ([Math]::Abs($delta - 0.5) -gt 0.000001) { throw 'Position delta self-test failed.' }
    Write-Output 'TIDEWATER_GATE3_ENVIRONMENT_SAVE_EVIDENCE_SELF_TEST_PASS'
    return
}

$beforePath = [IO.Path]::GetFullPath($BeforeSavePath)
$afterPath = [IO.Path]::GetFullPath($AfterSavePath)
$evidence = [IO.Path]::GetFullPath($EvidenceRoot)
Assert-Gate3ProofPath -LiteralPath $beforePath -Description 'Before public game-save'
Assert-Gate3ProofPath -LiteralPath $afterPath -Description 'After public game-save'
Assert-Gate3ProofPath -LiteralPath $evidence -Description 'Environment save evidence root'
if ($beforePath.Equals($afterPath, [StringComparison]::OrdinalIgnoreCase)) { throw 'BeforeSavePath and AfterSavePath must be distinct captured public saves.' }
if (Test-Path -LiteralPath $evidence) { throw "EvidenceRoot must be fresh: $evidence" }

$failure = $null
$before = $null
$after = $null
$actorComparison = $null
$objectComparisons = @()
$beforeEvents = @()
$afterEvents = @()
try {
    $before = Read-PublicEnvironmentSave -LiteralPath $beforePath
    $after = Read-PublicEnvironmentSave -LiteralPath $afterPath
    if ([string]$before.module_id -cne [string]$after.module_id -or [string]$before.area_id -cne [string]$after.area_id) { throw 'Before/after saves do not describe the same module and area.' }
    if ([string]$before.source_hash -cne [string]$after.source_hash) { throw 'Before/after saves do not preserve the same gameplay source hash.' }
    if ($ExpectedSourceHash -and [string]$after.source_hash -cne $ExpectedSourceHash.ToLowerInvariant()) { throw 'After save source hash differs from the expected pin.' }
    if ([bool]$after.state.requires_physics_revalidation) { throw 'After public save still requires physics revalidation.' }
    $beforeActor = Get-EnvironmentSaveMapEntry -Map $before.state.actors -Id $TrackedActorId -MapName 'actors' -PathDescription $before.path
    $afterActor = Get-EnvironmentSaveMapEntry -Map $after.state.actors -Id $TrackedActorId -MapName 'actors' -PathDescription $after.path
    $beforePosition = Get-EnvironmentPosition -State $beforeActor -PathDescription "$before.path actor $TrackedActorId"
    $afterPosition = Get-EnvironmentPosition -State $afterActor -PathDescription "$after.path actor $TrackedActorId"
    $actorDelta = Get-EnvironmentPositionDelta -Before $beforePosition -After $afterPosition
    $actorComparison = [ordered]@{
        id = $TrackedActorId
        before_position = $beforePosition
        after_position = $afterPosition
        delta_m = $actorDelta
        moved_beyond_tolerance = $actorDelta -gt $ActorMovementToleranceM
        tolerance_m = $ActorMovementToleranceM
        before_fleeing = [bool]$beforeActor.fleeing
        after_fleeing = [bool]$afterActor.fleeing
        before_vitality = [int]$beforeActor.vitality
        after_vitality = [int]$afterActor.vitality
    }
    foreach ($objectId in @($TrackedObjectId | Select-Object -Unique)) {
        $beforeObject = Get-EnvironmentSaveMapEntry -Map $before.state.objects -Id ([string]$objectId) -MapName 'objects' -PathDescription $before.path
        $afterObject = Get-EnvironmentSaveMapEntry -Map $after.state.objects -Id ([string]$objectId) -MapName 'objects' -PathDescription $after.path
        $beforeObjectPosition = Get-EnvironmentPosition -State $beforeObject -PathDescription "$before.path object $objectId"
        $afterObjectPosition = Get-EnvironmentPosition -State $afterObject -PathDescription "$after.path object $objectId"
        $objectComparisons += [ordered]@{
            id = [string]$objectId
            before_position = $beforeObjectPosition
            after_position = $afterObjectPosition
            position_delta_m = Get-EnvironmentPositionDelta -Before $beforeObjectPosition -After $afterObjectPosition
            before_released = [bool]$beforeObject.released
            after_released = [bool]$afterObject.released
            before_destroyed = [bool]$beforeObject.destroyed
            after_destroyed = [bool]$afterObject.destroyed
            before_burning = [bool]$beforeObject.burning
            after_burning = [bool]$afterObject.burning
            state_fields_observed = $true
        }
    }
    $beforeEvents = Get-EnvironmentEventRows -State $before.state -PathDescription $before.path
    $afterEvents = Get-EnvironmentEventRows -State $after.state -PathDescription $after.path
}
catch { $failure = $_.Exception.Message }

$afterFrameContactCount = 0
if ($null -ne $after -and $null -ne $after.state.last_frame -and $null -ne $after.state.last_frame.frame) { $afterFrameContactCount = @($after.state.last_frame.frame.contacts).Count }
$afterStateContactCount = if ($null -ne $after) { @($after.state.contacts.PSObject.Properties).Count } else { 0 }
$afterImpactCount = @($afterEvents | Where-Object { $_.kind -ceq 'Impact' }).Count
$afterRelevantCount = @($afterEvents | Where-Object { $_.kind -in @('Impact', 'Released', 'Destroyed', 'Burning', 'Fleeing', 'Damage') }).Count
$checks = [ordered]@{
    distinct_public_saves = -not $beforePath.Equals($afterPath, [StringComparison]::OrdinalIgnoreCase)
    same_module_area = $null -ne $before -and $null -ne $after -and [string]$before.module_id -ceq [string]$after.module_id -and [string]$before.area_id -ceq [string]$after.area_id
    same_source_hash = $null -ne $before -and $null -ne $after -and [string]$before.source_hash -ceq [string]$after.source_hash
    source_hash_matches_expected = [string]::IsNullOrWhiteSpace($ExpectedSourceHash) -or ($null -ne $after -and [string]$after.source_hash -ceq $ExpectedSourceHash.ToLowerInvariant())
    after_physics_revalidated = $null -ne $after -and -not [bool]$after.state.requires_physics_revalidation
    actor_moved = $null -ne $actorComparison -and [bool]$actorComparison.moved_beyond_tolerance
    tracked_object_states_observed = @($objectComparisons | Where-Object { -not [bool]$_.state_fields_observed }).Count -eq 0 -and @($objectComparisons).Count -gt 0
    physical_or_action_contact_observed = ($afterFrameContactCount -gt 0 -or $afterStateContactCount -gt 0)
    impact_or_environment_event_observed = $afterRelevantCount -gt 0
    state_hash_fields_present = $null -ne $before -and $before.state_hash.Length -eq 64 -and $null -ne $after -and $after.state_hash.Length -eq 64
}
$status = if ($null -eq $failure -and (@($checks.Values | Where-Object { -not [bool]$_ }).Count -eq 0)) { 'passed' } else { 'failed_closed' }

[void](New-Item -ItemType Directory -Path $evidence)
$ledger = [ordered]@{
    schema = 'forge.tidewater.gate3-environment-save-evidence'
    version = 1
    status = $status
    gate_qualified = $false
    human_visual_review = $false
    read_only = $true
    failure = $failure
    before_save = if ($null -ne $before) { [ordered]@{ path = $before.path; file_sha256 = $before.file_sha256; source_hash = $before.source_hash; module_id = $before.module_id; area_id = $before.area_id; state_hash = $before.state_hash } } else { $null }
    after_save = if ($null -ne $after) { [ordered]@{ path = $after.path; file_sha256 = $after.file_sha256; source_hash = $after.source_hash; module_id = $after.module_id; area_id = $after.area_id; state_hash = $after.state_hash } } else { $null }
    tracked_actor = $actorComparison
    tracked_objects = @($objectComparisons)
    event_counts = [ordered]@{ before = @($beforeEvents).Count; after = @($afterEvents).Count; after_impact = $afterImpactCount; after_relevant_environment = $afterRelevantCount }
    physics_counts = [ordered]@{ after_frame_contacts = $afterFrameContactCount; after_state_contact_receipts = $afterStateContactCount }
    checks = $checks
    limitations = @(
        'Both inputs must be byte captures of authoring.json created by the public Sauvegarder la partie button; this verifier never starts Studio and never writes either input.',
        'The verifier checks declared save hashes and structure but does not recompute canonical Rust state_hash; GameSession::resume remains the authoritative integrity check.',
        'A passed report proves saved state differences and observed contact/event fields only; it does not prove human visual acceptance, pathfinding, whole Gate 3 qualification or a causal physical kill without the corresponding event and position evidence.'
    )
}
$ledgerPath = Join-Path $evidence 'gate3-environment-save-evidence-ledger.json'
[IO.File]::WriteAllText($ledgerPath, ($ledger | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
if ($status -cne 'passed') { throw "Gate 3 environment save evidence failed closed: $failure. Ledger: $ledgerPath" }
Write-Output "TIDEWATER_GATE3_ENVIRONMENT_SAVE_EVIDENCE_PASS ledger=$ledgerPath"
