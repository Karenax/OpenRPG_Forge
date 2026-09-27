[CmdletBinding(DefaultParameterSetName = 'Proof')]
param(
    [Parameter(ParameterSetName = 'Proof', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$CandidateExecutable,

    [Parameter(ParameterSetName = 'Proof', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ExpectedSha256,

    [Parameter(ParameterSetName = 'Proof', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$FixtureSourceRoot,

    [Parameter(ParameterSetName = 'Proof')]
    [guid]$ZoneUuid = '81de8d42-89b9-58b5-b090-406cf4c63d06',

    [Parameter(ParameterSetName = 'Proof')]
    [guid]$PlacementUuid = '63a0e640-ab20-53be-b3d7-7007135019e3',

    [Parameter(ParameterSetName = 'Proof')]
    [guid]$BuildingUuid = '3286c3e1-c399-54c6-b943-6e3598d17526',

    [Parameter(ParameterSetName = 'Proof', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory,

    [Parameter(ParameterSetName = 'Proof')]
    [ValidateSet('All', 'Placements', 'PlacementLock', 'BuildingPlaytest', 'AssetPreviewCancel', 'TransversalV8', 'FullAuthorRoute', 'ProjectionIncremental')]
    [string]$Scenario = 'All',

    # This independent source is intentionally a fresh UI-created zone.  It is
    # not interchangeable with the populated fixtures used by the older routes.
    [Parameter(ParameterSetName = 'Proof')]
    [guid]$FullAuthorRouteZoneUuid = '03992311-9297-5219-8aa3-ed561640a312',

    [Parameter(ParameterSetName = 'Proof')]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$FullAuthorRouteSourceSha256 = '89E0DCB892A81B9589A604459DE1AA1F97690CD502737F81ECAE0782E84118D7',

    [Parameter(ParameterSetName = 'Proof')]
    [ValidateRange(10, 120)]
    [int]$StartupTimeoutSeconds = 45,

    [Parameter(ParameterSetName = 'Proof')]
    [ValidateRange(10, 180)]
    [int]$InteractionTimeoutSeconds = 75,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:CdpMessageId = 0
$script:CdpEvents = [System.Collections.Generic.List[object]]::new()
$script:CdpPendingResponses = @{}
$script:CdpDroppedEventCount = 0
$script:ExpectedTargetUrl = 'http://tauri.localhost/index.html?authoring=1'
$script:ExpectedTargetTitle = 'OpenRPG Forge Studio'
$script:CiStatus = 'NOT RUN — infrastructure billing'
$script:OakAssetId = 'vv-tree-oak-a'
$script:PineAssetId = 'vv-tree-pine-a'

function ConvertTo-CompactJson {
    param([Parameter(Mandatory)][object]$Value)
    return ($Value | ConvertTo-Json -Depth 100 -Compress)
}

function ConvertFrom-ExactJson {
    param([Parameter(Mandatory)][string]$Json)
    $command = Get-Command ConvertFrom-Json
    $arguments = @{}
    if ($command.Parameters.ContainsKey('Depth')) {
        $arguments.Depth = 100
    }
    if ($command.Parameters.ContainsKey('DateKind')) {
        $arguments.DateKind = 'String'
    }
    return ($Json | ConvertFrom-Json @arguments)
}

function Get-NormalizedSha256 {
    param([Parameter(Mandatory)][string]$Value)
    $normalized = $Value.Trim().ToLowerInvariant()
    if ($normalized -cnotmatch '^[0-9a-f]{64}$') {
        throw 'ExpectedSha256 must contain exactly 64 hexadecimal characters.'
    }
    return $normalized
}

function Get-SelectedScenarioNames {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('All', 'Placements', 'PlacementLock', 'BuildingPlaytest', 'AssetPreviewCancel', 'TransversalV8', 'FullAuthorRoute', 'ProjectionIncremental')]
        [string]$RequestedScenario
    )
    switch ($RequestedScenario) {
        'All' { 'placements'; 'placement-lock'; 'building-playtest'; 'asset-preview-cancel'; 'transversal-v8' }
        'Placements' { 'placements'; 'placement-lock' }
        'PlacementLock' { 'placement-lock' }
        'BuildingPlaytest' { 'building-playtest' }
        'AssetPreviewCancel' { 'asset-preview-cancel' }
        'TransversalV8' { 'transversal-v8' }
        'FullAuthorRoute' { 'full-author-route' }
        'ProjectionIncremental' { 'projection-incremental' }
    }
}

function Get-ScenarioCoverage {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('placements', 'placement-lock', 'building-playtest', 'asset-preview-cancel', 'transversal-v8', 'full-author-route', 'projection-incremental')]
        [string]$Name
    )
    switch ($Name) {
        'placements' {
            [ordered]@{ criterion='R2-028'; evidence_scope='physical placement picking and cancellation'; criterion_complete_in_scenario=$true }
            [ordered]@{ criterion='R2-029'; evidence_scope='gizmo gestures, transient previews, apply, cancellation and head changes; placement lock excluded'; criterion_complete_in_scenario=$false }
            [ordered]@{ criterion='R2-030'; evidence_scope='duplicate, delete, Undo, Redo, Save, Close and Open'; criterion_complete_in_scenario=$true }
            [ordered]@{ criterion='R2-031'; evidence_scope='world/local axes, grid and surface snapping, conflict refusals'; criterion_complete_in_scenario=$true }
        }
        'placement-lock' {
            [ordered]@{ criterion='R2-029'; evidence_scope='placement lock, disabled gizmo and observed Undo restoration'; criterion_complete_in_scenario=$false }
        }
        'building-playtest' {
            [ordered]@{ criterion='R2-032'; evidence_scope='building inspector, refusal, lock, persistence and projected parts'; criterion_complete_in_scenario=$true }
            [ordered]@{ criterion='R2-033'; evidence_scope='door and wall passage, pointer lock and return to editing'; criterion_complete_in_scenario=$true }
        }
        'asset-preview-cancel' {
            [ordered]@{ criterion='R2-026'; evidence_scope='cancelled delayed preview over exact governed candidate bytes'; criterion_complete_in_scenario=$true }
        }
        'transversal-v8' {
            [ordered]@{ criterion='R2-037'; evidence_scope='real authoring history summaries and multi-page navigation; workspace history pagination remains outside this scenario'; criterion_complete_in_scenario=$false }
            [ordered]@{ criterion='R2-042'; evidence_scope='runtime exit diagnostics returned by StopPlaytest and rendered after return to edit mode'; criterion_complete_in_scenario=$true }
            [ordered]@{ criterion='R2-046'; evidence_scope='stable vegetation layer copy/reorder/remove identities plus one retried placement duplication'; criterion_complete_in_scenario=$true }
            [ordered]@{ criterion='R2-047'; evidence_scope='locked-density layer removal refusal followed by explicit unlock, override removal, layer removal and exact Undo restoration'; criterion_complete_in_scenario=$true }
            [ordered]@{ criterion='R2-048'; evidence_scope='world-grid snap, external world pivot, metric/degree controls, Apply, Undo and Redo over the exact placement transform'; criterion_complete_in_scenario=$true }
            [ordered]@{ criterion='R2-049'; evidence_scope='density, placement, building, accepted path and draft-river provenance; accepted-river fixture is absent'; criterion_complete_in_scenario=$false }
            [ordered]@{ criterion='R2-051'; evidence_scope='physical gizmo and typed transform request/receipt equivalence plus one synthetic transport retry without duplicate placement'; criterion_complete_in_scenario=$true }
        }
        'full-author-route' {
            [ordered]@{ criterion='R2-037'; evidence_scope='workspace GUI history expanded from 12 to 21 real operations, saved and paginated 0/20/0; author history covered separately by TransversalV8'; criterion_complete_in_scenario=$false }
            [ordered]@{ criterion='R2-043'; evidence_scope='physical canvas valley, forest, path, river, house, clearing, regeneration, Save/Close/Open, Play/Stop, with Preview/Apply/Undo/Redo and projection-ready observation; no human visual review'; criterion_complete_in_scenario=$true }
            [ordered]@{ criterion='R2-049'; evidence_scope='accepted river provenance on the authored route; density, placement, building and path are covered separately by TransversalV8'; criterion_complete_in_scenario=$false }
            [ordered]@{ criterion='R2-052'; evidence_scope='operator timing and five-clone statistics are not yet measured'; criterion_complete_in_scenario=$false }
        }
        'projection-incremental' {
            [ordered]@{ criterion='R2-050'; evidence_scope='native DOM identities for 71 unchanged placements and eight building parts after pose/door edits, plus Play/Stop after reconciliation; deliberately stale play refusal and interactive performance remain outside this scenario'; criterion_complete_in_scenario=$false }
        }
    }
}

function Get-SingleScenarioCriterionCoverage {
    param(
        [AllowNull()][object]$Result,
        [Parameter(Mandatory)][string]$Criterion
    )
    if ($null -eq $Result) {
        return [ordered]@{ scenario='transversal-v8'; criterion=$Criterion; requested=$false; passed=$false; criterion_complete_in_scenario=$false; status='not_requested'; scopes=@() }
    }
    $scopes = @($Result.Coverage | Where-Object { [string]$_.criterion -ceq $Criterion })
    $criteria = @($Result.Criteria | Where-Object { [string]$_.Criterion -ceq $Criterion })
    if ($scopes.Count -ne 1 -or $criteria.Count -ne 1) {
        return [ordered]@{ scenario='transversal-v8'; criterion=$Criterion; requested=$true; passed=$false; criterion_complete_in_scenario=$false; status='failed_closed'; scopes=$scopes; failure='coverage/result cardinality mismatch' }
    }
    $passed = [bool]$Result.Passed -and [bool]$criteria[0].Passed
    $complete = [bool]$scopes[0].criterion_complete_in_scenario
    $status = if (-not $passed) { 'failed_closed' } elseif ($complete) { 'automated_cdp_acquired' } else { 'partial_scope_only' }
    return [ordered]@{ scenario='transversal-v8'; criterion=$Criterion; requested=$true; passed=$passed; criterion_complete_in_scenario=$complete; status=$status; scopes=$scopes }
}

function Get-FullAuthorRouteCriterionCoverage {
    param(
        [AllowNull()][object]$Result,
        [Parameter(Mandatory)][string]$Criterion
    )
    if ($null -eq $Result) {
        return [ordered]@{ scenario='full-author-route'; criterion=$Criterion; requested=$false; passed=$false; criterion_complete_in_scenario=$false; status='not_requested'; scopes=@() }
    }
    $scopes = @($Result.Coverage | Where-Object { [string]$_.criterion -ceq $Criterion })
    $criteria = @($Result.Criteria | Where-Object { [string]$_.Criterion -ceq $Criterion })
    if ($scopes.Count -ne 1 -or $criteria.Count -ne 1) {
        return [ordered]@{ scenario='full-author-route'; criterion=$Criterion; requested=$true; passed=$false; criterion_complete_in_scenario=$false; status='failed_closed'; scopes=$scopes; failure='coverage/result cardinality mismatch' }
    }
    $passed = [bool]$criteria[0].Passed -and [bool]$Result.SourcePreserved -and [bool]$Result.Cleanup.owned_process_closed -and [bool]$Result.Cleanup.cdp_listener_gone
    $complete = [bool]$scopes[0].criterion_complete_in_scenario
    $status = if (-not $passed) { 'failed_closed' } elseif ($complete) { 'automated_cdp_acquired' } else { 'partial_scope_only' }
    return [ordered]@{ scenario='full-author-route'; criterion=$Criterion; requested=$true; passed=$passed; criterion_complete_in_scenario=$complete; status=$status; scopes=$scopes }
}

function Get-FileSha256 {
    param([Parameter(Mandatory)][string]$LiteralPath)
    return (Get-FileHash -LiteralPath $LiteralPath -Algorithm SHA256).Hash.ToLowerInvariant()
}

function ConvertTo-AbsoluteExecutablePathOrNull {
    param([AllowNull()][AllowEmptyString()][string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }
    try {
        if (-not [System.IO.Path]::IsPathRooted($Value)) {
            return $null
        }
        return [System.IO.Path]::GetFullPath($Value)
    }
    catch {
        return $null
    }
}

function Test-TransientWindowsLoaderPath {
    param([AllowNull()][AllowEmptyString()][string]$Path)
    $absolute = ConvertTo-AbsoluteExecutablePathOrNull -Value $Path
    if ($null -eq $absolute -or -not (Test-IsWindowsPlatform)) { return $false }
    $loader = [System.IO.Path]::Combine([Environment]::SystemDirectory, 'ntdll.dll')
    return $absolute.Equals($loader, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-ExactExecutableIdentity {
    param(
        [AllowNull()][AllowEmptyString()][string]$LaunchedPath,
        [Parameter(Mandatory)][string]$Candidate,
        [Parameter(Mandatory)][string]$ExpectedHash,
        [Parameter(Mandatory)][int]$ProcessId,
        [Parameter(Mandatory)][string]$Source
    )
    $actual = ConvertTo-AbsoluteExecutablePathOrNull -Value $LaunchedPath
    $expected = ConvertTo-AbsoluteExecutablePathOrNull -Value $Candidate
    if ($null -eq $actual) {
        throw "Launched PID $ProcessId $Source did not provide a non-empty absolute executable path."
    }
    if ($null -eq $expected) {
        throw "Expected candidate path is not a non-empty absolute path: '$Candidate'."
    }
    if (-not $actual.Equals($expected, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Launched PID $ProcessId $Source resolved to '$actual', expected exact candidate '$expected'."
    }
    if (-not (Test-Path -LiteralPath $actual -PathType Leaf)) {
        throw "Launched PID $ProcessId $Source resolved to missing executable '$actual'."
    }
    $actualHash = Get-FileSha256 -LiteralPath $actual
    if ($actualHash -cne (Get-NormalizedSha256 -Value $ExpectedHash)) {
        throw "Launched PID $ProcessId $Source hash mismatch for '$actual': expected $ExpectedHash, got $actualHash."
    }
    return [pscustomobject]@{ ProcessId=$ProcessId; Path=$actual; Sha256=$actualHash; Source=$Source }
}

function Wait-LaunchedExecutableIdentity {
    param(
        [Parameter(Mandatory)][System.Diagnostics.Process]$Process,
        [Parameter(Mandatory)][string]$Candidate,
        [Parameter(Mandatory)][string]$ExpectedHash,
        [ValidateRange(1, 15)][int]$TimeoutSeconds = 5
    )
    $processId = $Process.Id
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $pathDiagnostic = 'not read'
    $mainModuleDiagnostic = 'not read'
    do {
        try { $Process.Refresh() } catch { $pathDiagnostic = "Refresh failed: $($_.Exception.Message)" }

        $reportedPath = $null
        try {
            $reportedPath = [string]$Process.Path
            $pathDiagnostic = if ([string]::IsNullOrWhiteSpace($reportedPath)) { 'empty' } else { "reported '$reportedPath'" }
        }
        catch {
            $pathDiagnostic = "$($_.Exception.GetType().Name): $($_.Exception.Message)"
        }
        if ($null -ne (ConvertTo-AbsoluteExecutablePathOrNull -Value $reportedPath)) {
            if (-not (Test-TransientWindowsLoaderPath -Path $reportedPath)) {
                return Assert-ExactExecutableIdentity -LaunchedPath $reportedPath -Candidate $Candidate -ExpectedHash $ExpectedHash -ProcessId $processId -Source 'Process.Path'
            }
            $pathDiagnostic += ' (loader module, waiting for process image)'
        }

        $mainModulePath = $null
        try {
            $mainModule = $Process.MainModule
            if ($null -ne $mainModule) { $mainModulePath = [string]$mainModule.FileName }
            $mainModuleDiagnostic = if ([string]::IsNullOrWhiteSpace($mainModulePath)) { 'empty' } else { "reported '$mainModulePath'" }
        }
        catch {
            $mainModuleDiagnostic = "$($_.Exception.GetType().Name): $($_.Exception.Message)"
        }
        if ($null -ne (ConvertTo-AbsoluteExecutablePathOrNull -Value $mainModulePath)) {
            if (-not (Test-TransientWindowsLoaderPath -Path $mainModulePath)) {
                return Assert-ExactExecutableIdentity -LaunchedPath $mainModulePath -Candidate $Candidate -ExpectedHash $ExpectedHash -ProcessId $processId -Source 'Process.MainModule.FileName'
            }
            $mainModuleDiagnostic += ' (loader module, waiting for process image)'
        }

        try {
            if ($Process.HasExited) {
                throw "Launched PID $processId exited before its executable path could be attested. Process.Path: $pathDiagnostic; Process.MainModule.FileName: $mainModuleDiagnostic."
            }
        }
        catch {
            if ($_.Exception.Message -like 'Launched PID * exited before*') { throw }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    throw "Executable path for launched PID $processId remained unavailable for $TimeoutSeconds seconds. Process.Path: $pathDiagnostic; Process.MainModule.FileName: $mainModuleDiagnostic."
}

function Test-IsWindowsPlatform {
    return [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
}

function Test-IsDriveEPath {
    param([Parameter(Mandatory)][string]$LiteralPath)
    $fullPath = [System.IO.Path]::GetFullPath($LiteralPath)
    return [System.IO.Path]::GetPathRoot($fullPath).TrimEnd('\') -ieq 'E:'
}

function Assert-DriveEPath {
    param(
        [Parameter(Mandatory)][string]$LiteralPath,
        [Parameter(Mandatory)][string]$Description
    )
    if (-not (Test-IsDriveEPath -LiteralPath $LiteralPath)) {
        throw "$Description must be an absolute path on physical E:. Received '$LiteralPath'."
    }
}

function Test-PathWithinDirectory {
    param(
        [Parameter(Mandatory)][string]$LiteralPath,
        [Parameter(Mandatory)][string]$Directory
    )
    $candidate = [System.IO.Path]::GetFullPath($LiteralPath).TrimEnd('\')
    $root = [System.IO.Path]::GetFullPath($Directory).TrimEnd('\')
    return $candidate.Equals($root, [StringComparison]::OrdinalIgnoreCase) -or
        $candidate.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoExistingReparseAncestor {
    param(
        [Parameter(Mandatory)][string]$LiteralPath,
        [Parameter(Mandatory)][string]$Description
    )
    $current = [System.IO.Path]::GetFullPath($LiteralPath)
    while (-not (Test-Path -LiteralPath $current)) {
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) {
            throw "$Description has no existing parent directory: $LiteralPath"
        }
        $current = $parent
    }
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        $item = Get-Item -LiteralPath $current -Force
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Description traverses reparse point '$current'; physical E: storage is required."
        }
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $current) {
            break
        }
        $current = $parent
    }
}

function Assert-TreeHasNoReparsePoints {
    param(
        [Parameter(Mandatory)][string]$LiteralPath,
        [Parameter(Mandatory)][string]$Description
    )
    Assert-NoExistingReparseAncestor -LiteralPath $LiteralPath -Description $Description
    $reparse = @(Get-ChildItem -LiteralPath $LiteralPath -Force -Recurse | Where-Object {
        ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
    })
    if ($reparse.Count -ne 0) {
        throw "$Description contains reparse point '$($reparse[0].FullName)'."
    }
}

function Get-ArchiveSnapshot {
    param([Parameter(Mandatory)][string]$LiteralPath)
    if (-not (Test-Path -LiteralPath $LiteralPath -PathType Leaf)) {
        throw "Authoring archive not found: $LiteralPath"
    }
    $item = Get-Item -LiteralPath $LiteralPath -Force
    return [pscustomobject]@{
        Path = $item.FullName
        Sha256 = Get-FileSha256 -LiteralPath $item.FullName
        Bytes = [int64]$item.Length
        LastWriteUtc = ([DateTimeOffset]$item.LastWriteTimeUtc).ToString('o')
        LastWriteUtcTicks = [int64]$item.LastWriteTimeUtc.Ticks
    }
}

function Test-ArchiveSnapshotsEqual {
    param(
        [Parameter(Mandatory)][object]$Before,
        [Parameter(Mandatory)][object]$After
    )
    return [string]$Before.Sha256 -ceq [string]$After.Sha256 -and
        [int64]$Before.Bytes -eq [int64]$After.Bytes -and
        [int64]$Before.LastWriteUtcTicks -eq [int64]$After.LastWriteUtcTicks
}

function Assert-FixtureIdentityHints {
    param(
        [Parameter(Mandatory)][string]$ArchivePath,
        [Parameter(Mandatory)][string]$PlacementId,
        [Parameter(Mandatory)][string]$BuildingId
    )
    $raw = [System.IO.File]::ReadAllText($ArchivePath)
    [void](ConvertFrom-ExactJson -Json $raw)
    foreach ($entry in @(
        [pscustomobject]@{ Name = 'placement'; Value = $PlacementId },
        [pscustomobject]@{ Name = 'building'; Value = $BuildingId }
    )) {
        $needle = '"' + $entry.Value + '"'
        if ($raw.IndexOf($needle, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "Fixture archive does not contain the exact $($entry.Name) UUID $($entry.Value)."
        }
    }
}

function Copy-IsolatedFixture {
    param(
        [Parameter(Mandatory)][string]$SourceRoot,
        [Parameter(Mandatory)][string]$DestinationRoot,
        [Parameter(Mandatory)][string]$ZoneId
    )
    if (Test-Path -LiteralPath $DestinationRoot) {
        throw "Fixture destination must be fresh: $DestinationRoot"
    }
    [void](New-Item -ItemType Directory -Path $DestinationRoot)
    foreach ($item in Get-ChildItem -LiteralPath $SourceRoot -Force) {
        Copy-Item -LiteralPath $item.FullName -Destination $DestinationRoot -Recurse -Force
    }
    Assert-TreeHasNoReparsePoints -LiteralPath $DestinationRoot -Description 'Isolated fixture copy'
    $archive = Join-Path (Join-Path $DestinationRoot $ZoneId) 'authoring.json'
    if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) {
        throw "Isolated fixture copy is missing $ZoneId\authoring.json."
    }
    return (Get-Item -LiteralPath $archive -Force).FullName
}

function Get-AuditedRequestKindFromIpcBody {
    param([Parameter(Mandatory)][string]$Body)
    try {
        $payload = ConvertFrom-ExactJson -Json $Body
        if (-not ($payload.PSObject.Properties.Name -contains 'command') -or $payload.command -isnot [string]) {
            return $null
        }
        $wire = ConvertFrom-ExactJson -Json ([string]$payload.command)
        if ([string]$wire.command -cne 'authoring' -or -not ($wire.PSObject.Properties.Name -contains 'request')) {
            return $null
        }
        return [string]$wire.request.action
    }
    catch {
        return $null
    }
}

function Get-AuditedAuthoringEntries {
    param(
        [Parameter(Mandatory)][object]$Snapshot,
        [string]$RequestKind
    )
    $entries = @($Snapshot.entries | Where-Object {
        $wire = if ($_.PSObject.Properties.Name -contains 'wire') { $_.wire } else { $null }
        [string]$_.command -ceq 'execute_studio_command' -and
            $null -ne $wire -and [string]$wire.command -ceq 'authoring'
    })
    if (-not [string]::IsNullOrWhiteSpace($RequestKind)) {
        $entries = @($entries | Where-Object { [string]$_.request_kind -ceq $RequestKind })
    }
    return $entries
}

function Get-RequiredAuditedEntry {
    param(
        [Parameter(Mandatory)][object]$Snapshot,
        [Parameter(Mandatory)][string]$RequestKind,
        [ValidateRange(0, 32)][int]$FromEnd = 0
    )
    $entries = @(Get-AuditedAuthoringEntries -Snapshot $Snapshot -RequestKind $RequestKind)
    $index = $entries.Count - 1 - $FromEnd
    if ($index -lt 0) {
        throw "Audited '$RequestKind' entry $FromEnd from the end is absent."
    }
    $entry = $entries[$index]
    if ([string]$entry.outcome -cne 'resolved') {
        throw "Audited '$RequestKind' entry did not resolve: $($entry.outcome)."
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$entry.parse_error)) {
        throw "Audited '$RequestKind' request parsing failed: $($entry.parse_error)"
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$entry.response_parse_error)) {
        throw "Audited '$RequestKind' response parsing failed: $($entry.response_parse_error)"
    }
    return $entry
}

function Get-RequiredAuditReceipt {
    param(
        [Parameter(Mandatory)][object]$Entry,
        [ValidateRange(0, 4)][int]$Attempt = 0
    )
    $attempts = @($Entry.attempts)
    if ($attempts.Count -le $Attempt) {
        throw "Audited entry has $($attempts.Count) response attempt(s), expected attempt $Attempt."
    }
    $response = $attempts[$Attempt].response
    if ($null -eq $response -or [string]$response.response -cne 'authoring') {
        throw "Audited response attempt $Attempt is not an authoring response."
    }
    $receipt = $response.zone_receipt
    if ($null -eq $receipt) {
        throw "Audited response attempt $Attempt has no bounded zone receipt."
    }
    foreach ($name in @('command_id','request_sha256','applied_sha256','inverse_sha256','resulting_revision','resulting_hash')) {
        if (-not ($receipt.PSObject.Properties.Name -contains $name) -or [string]::IsNullOrWhiteSpace([string]$receipt.$name)) {
            throw "Audited response receipt is missing '$name'."
        }
    }
    if ([string]$receipt.resulting_hash -cnotmatch '^[0-9a-f]{64}$') {
        throw 'Audited response receipt has an invalid resulting hash.'
    }
    foreach ($hashName in @('request_sha256','applied_sha256','inverse_sha256')) {
        if ([string]$receipt.$hashName -cnotmatch '^[0-9a-f]{64}$') {
            throw "Audited response receipt has an invalid '$hashName'."
        }
    }
    if ($null -ne $receipt.base_change_sha256 -and [string]$receipt.base_change_sha256 -cnotmatch '^[0-9a-f]{64}$') {
        throw 'Audited response receipt has an invalid base_change_sha256.'
    }
    if ([string]$receipt.command_id -cnotmatch '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' -or [string]$receipt.resulting_revision -cnotmatch '^[1-9][0-9]*$') {
        throw 'Audited response receipt has an invalid command id or resulting revision.'
    }
    if (-not ($Entry.PSObject.Properties.Name -contains 'zone_request_sha256') -or [string]$Entry.zone_request_sha256 -cnotmatch '^[0-9a-f]{64}$' -or [string]$Entry.zone_request_sha256 -cne [string]$receipt.request_sha256) {
        throw 'Audited response receipt is not bound to the exact nested zone request.'
    }
    $wireRequest = if ($null -ne $Entry.wire -and $null -ne $Entry.wire.request) { $Entry.wire.request.request } else { $null }
    if ($null -eq $wireRequest -or [string]$wireRequest.command_id -cne [string]$receipt.command_id) {
        throw 'Audited response receipt command id does not match the exact nested zone request.'
    }
    return $receipt
}

function Test-AuditReceiptsEqual {
    param(
        [Parameter(Mandatory)][object]$Left,
        [Parameter(Mandatory)][object]$Right
    )
    return (ConvertTo-CompactJson -Value $Left) -ceq (ConvertTo-CompactJson -Value $Right)
}

function Assert-NormalizedPoint {
    param(
        [Parameter(Mandatory)][object]$Point,
        [Parameter(Mandatory)][string]$Description
    )
    if ($null -eq $Point -or
        -not ($Point.PSObject.Properties.Name -contains 'x') -or
        -not ($Point.PSObject.Properties.Name -contains 'y')) {
        throw "$Description is absent."
    }
    $x = [double]$Point.x
    $y = [double]$Point.y
    if ([double]::IsNaN($x) -or [double]::IsInfinity($x) -or
        [double]::IsNaN($y) -or [double]::IsInfinity($y) -or
        $x -lt 0.0 -or $x -gt 1.0 -or $y -lt 0.0 -or $y -gt 1.0) {
        throw "$Description must contain finite coordinates in [0,1]."
    }
}

function ConvertFrom-GizmoDiagnostic {
    param(
        [Parameter(Mandatory)][string]$Json,
        [string]$ExpectedPlacementId,
        [string]$ExpectedMode,
        [string]$ExpectedRotationSpace
    )
    $diagnostic = ConvertFrom-ExactJson -Json $Json
    if ([string]$diagnostic.schema -cne 'forge-placement-gizmo-diagnostic' -or [int]$diagnostic.version -ne 1) {
        throw 'Gizmo diagnostic schema/version mismatch.'
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedPlacementId) -and
        [string]$diagnostic.placement_id -cne $ExpectedPlacementId) {
        throw "Gizmo diagnostic placement '$($diagnostic.placement_id)' differs from '$ExpectedPlacementId'."
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedMode) -and [string]$diagnostic.mode -cne $ExpectedMode) {
        throw "Gizmo diagnostic mode '$($diagnostic.mode)' differs from '$ExpectedMode'."
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedRotationSpace) -and
        [string]$diagnostic.rotation_space -cne $ExpectedRotationSpace) {
        throw "Gizmo diagnostic rotation space '$($diagnostic.rotation_space)' differs from '$ExpectedRotationSpace'."
    }
    if ([string]$diagnostic.state -ceq 'active') {
        foreach ($name in @('source_transform', 'current_transform', 'pivot_world', 'logical_viewport_size', 'phase', 'axis_handle_screen_normalized', 'rotation_ring_candidates_screen_normalized')) {
            if (-not ($diagnostic.PSObject.Properties.Name -contains $name)) {
                throw "Active gizmo diagnostic is missing '$name'."
            }
        }
        if ([double]$diagnostic.logical_viewport_size.width -le 0 -or [double]$diagnostic.logical_viewport_size.height -le 0) {
            throw 'Gizmo logical viewport size must be positive.'
        }
    }
    return $diagnostic
}

function Get-GizmoAxisPoint {
    param(
        [Parameter(Mandatory)][object]$Diagnostic,
        [Parameter(Mandatory)][ValidateSet('x', 'y', 'z')][string]$Axis
    )
    $matches = @($Diagnostic.axis_handle_screen_normalized | Where-Object { [string]$_.axis -ceq $Axis })
    if ($matches.Count -ne 1) {
        throw "Gizmo diagnostic must expose exactly one '$Axis' axis handle."
    }
    $point = $matches[0].screen_normalized
    Assert-NormalizedPoint -Point $point -Description "Gizmo $Axis handle"
    return [pscustomobject]@{ x = [double]$point.x; y = [double]$point.y }
}

function Get-GizmoRingGesture {
    param(
        [Parameter(Mandatory)][object]$Diagnostic,
        [Parameter(Mandatory)][ValidateSet('x', 'y', 'z')][string]$Axis
    )
    $rings = @($Diagnostic.rotation_ring_candidates_screen_normalized | Where-Object { [string]$_.axis -ceq $Axis })
    if ($rings.Count -ne 1) {
        throw "Gizmo diagnostic must expose exactly one '$Axis' rotation ring."
    }
    $points = [System.Collections.Generic.List[object]]::new()
    foreach ($segment in @($rings[0].segments)) {
        if ($null -eq $segment.start -or $null -eq $segment.end) {
            continue
        }
        try {
            Assert-NormalizedPoint -Point $segment.start -Description 'Rotation segment start'
            Assert-NormalizedPoint -Point $segment.end -Description 'Rotation segment end'
            $points.Add([pscustomobject]@{
                x = ([double]$segment.start.x + [double]$segment.end.x) / 2.0
                y = ([double]$segment.start.y + [double]$segment.end.y) / 2.0
            })
        }
        catch {
            continue
        }
    }
    if ($points.Count -lt 8) {
        throw "Rotation ring '$Axis' has fewer than eight usable projected segments."
    }
    $start = $points[0]
    $end = $null
    foreach ($candidate in $points) {
        $distance = [Math]::Sqrt([Math]::Pow($candidate.x - $start.x, 2) + [Math]::Pow($candidate.y - $start.y, 2))
        if ($distance -ge 0.04 -and $distance -le 0.35) {
            $end = $candidate
            break
        }
    }
    if ($null -eq $end) {
        throw "Rotation ring '$Axis' has no stable separated gesture pair."
    }
    return [pscustomobject]@{ Start = $start; End = $end }
}

function Test-NumericArraysEqual {
    param(
        [Parameter(Mandatory)][object[]]$Left,
        [Parameter(Mandatory)][object[]]$Right,
        [double]$Tolerance = 0.0001
    )
    if ($Left.Count -ne $Right.Count) {
        return $false
    }
    for ($index = 0; $index -lt $Left.Count; $index += 1) {
        if ([Math]::Abs([double]$Left[$index] - [double]$Right[$index]) -gt $Tolerance) {
            return $false
        }
    }
    return $true
}

function Assert-GizmoTransformContract {
    param(
        [Parameter(Mandatory)][object]$Diagnostic,
        [Parameter(Mandatory)][ValidateSet('translation', 'local_translation', 'rotation', 'scale')][string]$Mode,
        [Parameter(Mandatory)][ValidateSet('x', 'y', 'z')][string]$Axis
    )
    $source = $Diagnostic.source_transform
    $current = $Diagnostic.current_transform
    if ($null -eq $source -or $null -eq $current) {
        throw 'Gizmo transform contract requires source and current transforms.'
    }
    $translationSame = Test-NumericArraysEqual -Left @($source.translation_m) -Right @($current.translation_m)
    $rotationSame = Test-NumericArraysEqual -Left @($source.rotation) -Right @($current.rotation)
    $scaleSame = Test-NumericArraysEqual -Left @($source.scale) -Right @($current.scale)
    switch ($Mode) {
        'translation' {
            if ($translationSame -or -not $rotationSame -or -not $scaleSame) {
                throw 'World translation transient must change translation only.'
            }
        }
        'local_translation' {
            if ($translationSame -or -not $rotationSame -or -not $scaleSame) {
                throw 'Local translation transient must change translation only.'
            }
        }
        'rotation' {
            if ($rotationSame -or -not $translationSame -or -not $scaleSame) {
                throw 'Rotation transient around the default object pivot must change rotation only.'
            }
        }
        'scale' {
            if ($scaleSame -or -not $translationSame -or -not $rotationSame) {
                throw 'Scale transient must change scale only.'
            }
            foreach ($value in @($current.scale)) {
                if ([double]$value -le 0) {
                    throw 'Scale transient produced a zero or negative component.'
                }
            }
        }
    }
    if ([string]$Diagnostic.active_axis -cne $Axis) {
        throw "Gizmo selected axis '$($Diagnostic.active_axis)' instead of '$Axis'."
    }
}

function New-CriterionResult {
    param([Parameter(Mandatory)][string]$Criterion)
    return [pscustomobject]@{
        Criterion = $Criterion
        Passed = $false
        Status = 'not_run'
        Cases = [System.Collections.Generic.List[object]]::new()
    }
}

function Add-CriterionCase {
    param(
        [Parameter(Mandatory)][object]$Criterion,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][bool]$Passed,
        [object]$Evidence,
        [string]$Failure,
        [AllowNull()][string]$StartedAtUtc,
        [AllowNull()][string]$CompletedAtUtc,
        [AllowNull()][double]$ElapsedMs
    )
    $Criterion.Cases.Add([pscustomobject]@{
        name = $Name
        passed = $Passed
        evidence = $Evidence
        failure = $Failure
        started_at_utc = $StartedAtUtc
        completed_at_utc = $CompletedAtUtc
        elapsed_ms = $ElapsedMs
    })
}

function Complete-CriterionResult {
    param([Parameter(Mandatory)][object]$Criterion)
    $cases = @($Criterion.Cases)
    $Criterion.Passed = $cases.Count -gt 0 -and @($cases | Where-Object { -not $_.passed }).Count -eq 0
    $Criterion.Status = if ($cases.Count -eq 0) { 'not_run' } elseif ($Criterion.Passed) { 'automated_cdp_acquired' } else { 'failed_closed' }
}

function Complete-CriterionSet {
    param([Parameter(Mandatory)][hashtable]$Criteria)
    foreach ($criterion in $Criteria.Values) { Complete-CriterionResult -Criterion $criterion }
    return @($Criteria.Values | Sort-Object Criterion)
}

function Invoke-CriterionCase {
    param(
        [Parameter(Mandatory)][object]$Criterion,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Action
    )
    $started = [DateTimeOffset]::UtcNow
    $timer = [Diagnostics.Stopwatch]::StartNew()
    try {
        $evidence = & $Action
        $timer.Stop()
        Add-CriterionCase -Criterion $Criterion -Name $Name -Passed $true -Evidence $evidence -Failure $null -StartedAtUtc $started.ToString('o') -CompletedAtUtc ([DateTimeOffset]::UtcNow.ToString('o')) -ElapsedMs $timer.Elapsed.TotalMilliseconds
        return $true
    }
    catch {
        $timer.Stop()
        Add-CriterionCase -Criterion $Criterion -Name $Name -Passed $false -Evidence $null -Failure $_.Exception.Message -StartedAtUtc $started.ToString('o') -CompletedAtUtc ([DateTimeOffset]::UtcNow.ToString('o')) -ElapsedMs $timer.Elapsed.TotalMilliseconds
        return $false
    }
}

function Wait-NonGenericTask {
    param([Parameter(Mandatory)][System.Threading.Tasks.Task]$Task)
    [void]$Task.GetAwaiter().GetResult()
}

function New-LoopbackPortReservation {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    return [pscustomobject]@{
        Listener = $listener
        Port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
}

function Invoke-RestMethodCompat {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [int]$TimeoutSeconds = 2
    )
    $arguments = @{ Uri = $Uri; TimeoutSec = $TimeoutSeconds }
    if ((Get-Command Invoke-RestMethod).Parameters.ContainsKey('NoProxy')) {
        $arguments.NoProxy = $true
    }
    return Invoke-RestMethod @arguments
}

function Assert-CdpTarget {
    param(
        [Parameter(Mandatory)][object]$Target,
        [Parameter(Mandatory)][int]$ExpectedPort
    )
    if ([string]$Target.type -cne 'page') {
        throw "CDP target type must be exactly 'page'."
    }
    if ([string]$Target.title -cne $script:ExpectedTargetTitle) {
        throw "CDP target title '$($Target.title)' differs from '$($script:ExpectedTargetTitle)'."
    }
    if ([string]$Target.url -cne $script:ExpectedTargetUrl) {
        throw "CDP target URL '$($Target.url)' differs from '$($script:ExpectedTargetUrl)'."
    }
    $webSocketUri = $null
    if (-not [Uri]::TryCreate([string]$Target.webSocketDebuggerUrl, [UriKind]::Absolute, [ref]$webSocketUri)) {
        throw 'CDP webSocketDebuggerUrl is not an absolute URI.'
    }
    if ($webSocketUri.Scheme -cne 'ws' -or $webSocketUri.Host -notin @('127.0.0.1', 'localhost')) {
        throw 'CDP websocket must use ws on loopback.'
    }
    if ($webSocketUri.Port -ne $ExpectedPort -or
        -not $webSocketUri.AbsolutePath.StartsWith('/devtools/page/', [StringComparison]::Ordinal) -or
        $webSocketUri.Query.Length -ne 0 -or $webSocketUri.Fragment.Length -ne 0 -or $webSocketUri.UserInfo.Length -ne 0) {
        throw 'CDP websocket endpoint is not the exact loopback page endpoint on the reserved port.'
    }
}

function Wait-CdpTarget {
    param(
        [Parameter(Mandatory)][int]$Port,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [Parameter(Mandatory)][System.Diagnostics.Process]$Process
    )
    $endpoint = "http://127.0.0.1:$Port/json/list"
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastReason = 'endpoint not available'
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if ($Process.HasExited) {
            throw "Studio exited before exposing CDP (exit code $($Process.ExitCode))."
        }
        try {
            $targets = @(Invoke-RestMethodCompat -Uri $endpoint -TimeoutSeconds 2)
            $matching = @($targets | Where-Object {
                [string]$_.type -ceq 'page' -and
                [string]$_.title -ceq $script:ExpectedTargetTitle -and
                [string]$_.url -ceq $script:ExpectedTargetUrl
            })
            if ($matching.Count -gt 1) {
                throw 'More than one exact authoring page target is exposed on the reserved port.'
            }
            if ($matching.Count -eq 1) {
                Assert-CdpTarget -Target $matching[0] -ExpectedPort $Port
                return $matching[0]
            }
            $lastReason = 'exact page URL/title/type not present'
        }
        catch {
            $lastReason = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 200
    }
    throw "Exact authoring CDP target did not appear on loopback port $Port. Last state: $lastReason"
}

function Connect-CdpSocket {
    param([Parameter(Mandatory)][string]$WebSocketUrl)
    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $timeout = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(10))
    try {
        Wait-NonGenericTask -Task ($socket.ConnectAsync([Uri]$WebSocketUrl, $timeout.Token))
        return $socket
    }
    catch {
        $socket.Dispose()
        throw "Unable to connect to CDP websocket: $($_.Exception.Message)"
    }
    finally {
        $timeout.Dispose()
    }
}

function Receive-CdpEnvelope {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][System.Threading.CancellationToken]$CancellationToken
    )
    $stream = [System.IO.MemoryStream]::new()
    try {
        do {
            $buffer = [byte[]]::new(65536)
            $segment = [ArraySegment[byte]]::new($buffer)
            $received = $Socket.ReceiveAsync($segment, $CancellationToken).GetAwaiter().GetResult()
            if ($received.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
                throw 'CDP websocket closed while awaiting a response.'
            }
            if ($received.Count -gt 0) {
                $stream.Write($buffer, 0, $received.Count)
            }
        } while (-not $received.EndOfMessage)
        return ConvertFrom-ExactJson -Json ([System.Text.Encoding]::UTF8.GetString($stream.ToArray()))
    }
    finally {
        $stream.Dispose()
    }
}

function Invoke-CdpCommand {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Method,
        [System.Collections.IDictionary]$Parameters = @{},
        [ValidateRange(1, 180)][int]$TimeoutSeconds = 15
    )
    $script:CdpMessageId += 1
    $messageId = $script:CdpMessageId
    $request = [ordered]@{ id = $messageId; method = $Method; params = $Parameters }
    $payload = [System.Text.Encoding]::UTF8.GetBytes((ConvertTo-CompactJson -Value $request))
    $timeout = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($TimeoutSeconds))
    try {
        Wait-NonGenericTask -Task ($Socket.SendAsync(
            [ArraySegment[byte]]::new($payload),
            [System.Net.WebSockets.WebSocketMessageType]::Text,
            $true,
            $timeout.Token
        ))
        $pendingKey = [string]$messageId
        while ($true) {
            if ($script:CdpPendingResponses.ContainsKey($pendingKey)) {
                $envelope = $script:CdpPendingResponses[$pendingKey]
                $script:CdpPendingResponses.Remove($pendingKey)
            }
            else {
                $envelope = Receive-CdpEnvelope -Socket $Socket -CancellationToken $timeout.Token
            }
            if ($envelope.PSObject.Properties.Name -contains 'id') {
                if ([int]$envelope.id -ne $messageId) {
                    $script:CdpPendingResponses[[string]$envelope.id] = $envelope
                    continue
                }
                if ($envelope.PSObject.Properties.Name -contains 'error') {
                    throw "CDP $Method failed: $($envelope.error.message)"
                }
                return $envelope.result
            }
            if ($envelope.PSObject.Properties.Name -contains 'method') {
                if ($script:CdpEvents.Count -lt 4096) {
                    $script:CdpEvents.Add([pscustomobject]@{
                        Method = [string]$envelope.method
                        ReceivedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
                    })
                }
                else {
                    $script:CdpDroppedEventCount += 1
                }
            }
        }
    }
    finally {
        $timeout.Dispose()
    }
}

function Assert-EditorNavigation {
    param([Parameter(Mandatory)]$Socket)
    $result = Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const toolbar = document.querySelector('.authoring-toolbar');
  if (!toolbar) return { ready: true, legacy: true };
  const visible = e => { const r=e.getBoundingClientRect(); return r.width>0 && r.height>0 && r.top>=0 && r.bottom<=innerHeight && r.left>=0 && r.right<=innerWidth; };
  const tabs=[...toolbar.querySelectorAll('[data-authoring-tab-button]')];
  return {ready:visible(toolbar) && tabs.length===6 && tabs.every(visible), top:toolbar.getBoundingClientRect().top, bottom:toolbar.getBoundingClientRect().bottom, count:tabs.length};
})()
'@
    if (-not $result.ready) { throw "Editor navigation escaped the viewport: $(ConvertTo-CompactJson -Value $result)" }
    return $result
}

function Invoke-CdpExpression {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Expression,
        [ValidateRange(1, 180)][int]$TimeoutSeconds = 15
    )
    $response = Invoke-CdpCommand -Socket $Socket -Method 'Runtime.evaluate' -Parameters @{
        expression = $Expression
        returnByValue = $true
        awaitPromise = $true
        userGesture = $true
    } -TimeoutSeconds $TimeoutSeconds
    if ($response.PSObject.Properties.Name -contains 'exceptionDetails') {
        $description = [string]$response.exceptionDetails.text
        if ($response.exceptionDetails.PSObject.Properties.Name -contains 'exception') {
            $description = [string]$response.exceptionDetails.exception.description
        }
        throw "CDP JavaScript exception: $description"
    }
    if ($response.result.PSObject.Properties.Name -contains 'value') {
        return $response.result.value
    }
    if ([string]$response.result.subtype -eq 'null') {
        return $null
    }
    throw "CDP expression returned no serializable value (type '$($response.result.type)')."
}

function Wait-CdpReady {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Expression,
        [Parameter(Mandatory)][string]$Description,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastReason = 'not evaluated'
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $state = Invoke-CdpExpression -Socket $Socket -Expression $Expression
        if ($null -ne $state -and $state.ready -eq $true) {
            return $state
        }
        if ($null -ne $state -and $state.PSObject.Properties.Name -contains 'terminal' -and $state.terminal -eq $true) {
            throw "$Description reached a terminal failure: $($state.reason)"
        }
        if ($null -ne $state -and $state.PSObject.Properties.Name -contains 'reason') {
            $lastReason = [string]$state.reason
        }
        Start-Sleep -Milliseconds 200
    }
    throw "$Description did not become ready within $TimeoutSeconds seconds. Last state: $lastReason"
}

function Send-CdpMouseEvent {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][ValidateSet('mouseMoved', 'mousePressed', 'mouseReleased')][string]$Type,
        [Parameter(Mandatory)][double]$X,
        [Parameter(Mandatory)][double]$Y,
        [int]$Buttons = 0,
        [ValidateSet('left','right','middle')][string]$Button = 'left'
    )
    $parameters = @{ type = $Type; x = $X; y = $Y; modifiers = 0; buttons = $Buttons; pointerType = 'mouse' }
    if ($Type -ne 'mouseMoved') {
        $parameters.button = $Button
        $parameters.clickCount = 1
    }
    [void](Invoke-CdpCommand -Socket $Socket -Method 'Input.dispatchMouseEvent' -Parameters $parameters)
}

function Send-CdpMouseClick {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][double]$X,
        [Parameter(Mandatory)][double]$Y
    )
    Send-CdpMouseEvent -Socket $Socket -Type mouseMoved -X $X -Y $Y
    Send-CdpMouseEvent -Socket $Socket -Type mousePressed -X $X -Y $Y -Buttons 1
    Send-CdpMouseEvent -Socket $Socket -Type mouseReleased -X $X -Y $Y
}

function Send-CdpMouseDrag {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][double]$StartX,
        [Parameter(Mandatory)][double]$StartY,
        [Parameter(Mandatory)][double]$EndX,
        [Parameter(Mandatory)][double]$EndY,
        [ValidateRange(1, 20)][int]$Steps = 6,
        [ValidateSet('left','right','middle')][string]$Button = 'left',
        [switch]$HoldOpen
    )
    $buttonBit = switch($Button){ 'left' { 1 } 'right' { 2 } 'middle' { 4 } }
    Send-CdpMouseEvent -Socket $Socket -Type mouseMoved -X $StartX -Y $StartY
    Send-CdpMouseEvent -Socket $Socket -Type mousePressed -X $StartX -Y $StartY -Buttons $buttonBit -Button $Button
    for ($index = 1; $index -le $Steps; $index += 1) {
        $ratio = $index / [double]$Steps
        Send-CdpMouseEvent -Socket $Socket -Type mouseMoved -X ($StartX + (($EndX - $StartX) * $ratio)) -Y ($StartY + (($EndY - $StartY) * $ratio)) -Buttons $buttonBit
    }
    if (-not $HoldOpen) {
        Send-CdpMouseEvent -Socket $Socket -Type mouseReleased -X $EndX -Y $EndY -Button $Button
    }
}

function Send-CdpKeyStroke {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Key,
        [Parameter(Mandatory)][string]$Code,
        [Parameter(Mandatory)][int]$VirtualKeyCode,
        [int]$Modifiers = 0
    )
    $common = @{
        key = $Key
        code = $Code
        windowsVirtualKeyCode = $VirtualKeyCode
        nativeVirtualKeyCode = $VirtualKeyCode
        modifiers = $Modifiers
    }
    [void](Invoke-CdpCommand -Socket $Socket -Method 'Input.dispatchKeyEvent' -Parameters (@{ type = 'rawKeyDown' } + $common))
    [void](Invoke-CdpCommand -Socket $Socket -Method 'Input.dispatchKeyEvent' -Parameters (@{ type = 'keyUp' } + $common))
}

function Send-CdpKeyHold {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Key,
        [Parameter(Mandatory)][string]$Code,
        [Parameter(Mandatory)][int]$VirtualKeyCode,
        [ValidateRange(50, 10000)][int]$Milliseconds
    )
    $common = @{
        key = $Key
        code = $Code
        windowsVirtualKeyCode = $VirtualKeyCode
        nativeVirtualKeyCode = $VirtualKeyCode
        modifiers = 0
    }
    [void](Invoke-CdpCommand -Socket $Socket -Method 'Input.dispatchKeyEvent' -Parameters (@{ type = 'rawKeyDown' } + $common))
    Start-Sleep -Milliseconds $Milliseconds
    [void](Invoke-CdpCommand -Socket $Socket -Method 'Input.dispatchKeyEvent' -Parameters (@{ type = 'keyUp' } + $common))
}

function Send-CdpEscape {
    param([Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket)
    Send-CdpKeyStroke -Socket $Socket -Key 'Escape' -Code 'Escape' -VirtualKeyCode 27
}

function Save-CdpScreenshot {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$LiteralPath
    )
    $result = Invoke-CdpCommand -Socket $Socket -Method 'Page.captureScreenshot' -Parameters @{
        format = 'png'
        fromSurface = $true
        captureBeyondViewport = $false
    } -TimeoutSeconds 30
    $bytes = [Convert]::FromBase64String([string]$result.data)
    if ($bytes.Length -lt 1024) {
        throw 'CDP screenshot payload is unexpectedly small.'
    }
    [System.IO.File]::WriteAllBytes($LiteralPath, $bytes)
    return [pscustomobject]@{
        Path = [System.IO.Path]::GetFullPath($LiteralPath)
        Bytes = [int64]$bytes.Length
        Sha256 = Get-FileSha256 -LiteralPath $LiteralPath
        Method = 'Page.captureScreenshot'
    }
}

function Test-CdpListenerClosed {
    param([Parameter(Mandatory)][int]$Port)
    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $connect = $client.ConnectAsync('127.0.0.1', $Port)
        try {
            if ($connect.Wait(250) -and $client.Connected) {
                return $false
            }
        }
        catch {
            return $true
        }
        return $true
    }
    finally {
        $client.Dispose()
    }
}

function Wait-CdpListenerClosed {
    param(
        [Parameter(Mandatory)][int]$Port,
        [ValidateRange(1, 30)][int]$TimeoutSeconds = 10
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (Test-CdpListenerClosed -Port $Port) {
            return $true
        }
        Start-Sleep -Milliseconds 200
    }
    return $false
}

function Get-UiTarget {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Selector,
        [Parameter(Mandatory)][string]$Text,
        [string]$ScopeSelector = 'document'
    )
    $selectorLiteral = ConvertTo-CompactJson -Value $Selector
    $textLiteral = ConvertTo-CompactJson -Value $Text
    $scopeLiteral = ConvertTo-CompactJson -Value $ScopeSelector
    $expression = @"
(async () => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const selector = $selectorLiteral;
  const expected = $textLiteral;
  const scopeSelector = $scopeLiteral;
  const scope = scopeSelector === 'document' ? document : document.querySelector(scopeSelector);
  if (!scope) return { ready:false, terminal:true, reason:'scope absent: ' + scopeSelector };
  const matches = [...scope.querySelectorAll(selector)].filter(node => norm(node.textContent) === expected);
  if (matches.length === 0) return { ready:false, terminal:false, reason:'exact target not rendered yet: ' + expected };
  if (matches.length !== 1) return { ready:false, terminal:true, reason:'expected one exact target, got ' + matches.length + ': ' + expected };
  const node = matches[0];
  node.scrollIntoView({ block:'center', inline:'center' });
  await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  const rect = node.getBoundingClientRect();
  const style = getComputedStyle(node);
  const visible = rect.width > 0 && rect.height > 0 && style.visibility !== 'hidden' && style.display !== 'none';
  const disabled = node.matches(':disabled');
  const x = rect.left + rect.width/2, y = rect.top + rect.height/2;
  const hit = document.elementFromPoint(x, y);
  const unobscured = hit === node || (!!hit && node.contains(hit));
  return { ready:visible && unobscured, terminal:!visible, reason:!visible ? 'target hidden' : (unobscured ? 'target ready' : 'target centre obscured'), x, y, width:rect.width, height:rect.height, disabled, tag:node.tagName, text:norm(node.textContent), hit_tag:hit?.tagName ?? '', hit_id:hit?.id ?? '' };
})()
"@
    return Wait-CdpReady -Socket $Socket -Expression $expression -Description "UI target '$Text'" -TimeoutSeconds 10
}

function Invoke-UiClick {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Text,
        [string]$Selector = 'button',
        [string]$ScopeSelector = 'document',
        [switch]$AllowDisabled
    )
    $target = Get-UiTarget -Socket $Socket -Selector $Selector -Text $Text -ScopeSelector $ScopeSelector
    if ($target.disabled -and -not $AllowDisabled) {
        throw "UI target '$Text' is disabled."
    }
    Send-CdpMouseClick -Socket $Socket -X $target.x -Y $target.y
    return $target
}

function Invoke-UiClickInDetailsBySummary {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Summary,
        [Parameter(Mandatory)][string]$Text
    )
    # Some long inspector panels can leave a sticky element over the geometric
    # centre. Resolve the exact button in its exact details panel, then select a
    # real exposed interior point; the resulting activation is still CDP mouse
    # input, never element.click().
    $summaryLiteral = ConvertTo-CompactJson -Value $Summary
    $textLiteral = ConvertTo-CompactJson -Value $Text
    $expression = @"
(async () => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim(), summaryText=$summaryLiteral, expected=$textLiteral;
  const summaries=[...document.querySelectorAll('summary')].filter(node=>norm(node.textContent)===summaryText);
  if(summaries.length!==1) return {ready:false,terminal:true,reason:'expected one details summary, got '+summaries.length};
  const details=summaries[0].closest('details'); if(!details) return {ready:false,terminal:true,reason:'details parent absent'};
  details.open=true;
  const matches=[...details.querySelectorAll('button')].filter(node=>norm(node.textContent)===expected);
  if(matches.length!==1) return {ready:false,terminal:true,reason:'expected one exact button in details, got '+matches.length};
  const node=matches[0]; node.scrollIntoView({block:'center',inline:'nearest'});
  await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
  const rect=node.getBoundingClientRect(), style=getComputedStyle(node);
  if(rect.width<=0||rect.height<=0||style.visibility==='hidden'||style.display==='none') return {ready:false,terminal:false,reason:'exact details button hidden'};
  if(node.matches(':disabled')) return {ready:false,terminal:false,reason:'exact details button disabled'};
  const fractions=[.5,.25,.75,.15,.85];
  for(const fy of fractions) for(const fx of fractions){
    const x=rect.left+rect.width*fx,y=rect.top+rect.height*fy,hit=document.elementFromPoint(x,y);
    if(hit===node||node.contains(hit)) return {ready:true,reason:'exposed physical point',x,y,fx,fy,text:expected,summary:summaryText};
  }
  return {ready:false,terminal:false,reason:'all exact button interior points obscured',rect:{left:rect.left,top:rect.top,width:rect.width,height:rect.height}};
})()
"@
    $target = Wait-CdpReady -Socket $Socket -Expression $expression -Description "Exposed UI target '$Text' in '$Summary'" -TimeoutSeconds 10
    Send-CdpMouseClick -Socket $Socket -X $target.x -Y $target.y
    return $target
}

function Invoke-UiClickInLabeledFieldset {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$FieldLabel,
        [Parameter(Mandatory)][string]$Text,
        [string]$Selector = 'button'
    )
    $labelLiteral = ConvertTo-CompactJson -Value $FieldLabel
    $textLiteral = ConvertTo-CompactJson -Value $Text
    $selectorLiteral = ConvertTo-CompactJson -Value $Selector
    $expression = @"
(async () => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const labelPrefix = $labelLiteral;
  const expected = $textLiteral;
  const selector = $selectorLiteral;
  const labels = [...document.querySelectorAll('label')].filter(node => norm(node.textContent).startsWith(labelPrefix));
  if (labels.length === 0) return { ready:false, terminal:false, reason:'field label not rendered yet: ' + labelPrefix };
  if (labels.length !== 1) return { ready:false, terminal:true, reason:'expected one field label ' + labelPrefix + ', got ' + labels.length };
  const fieldset = labels[0].closest('fieldset');
  if (!fieldset) return { ready:false, terminal:false, reason:'fieldset not rendered yet for ' + labelPrefix };
  const matches = [...fieldset.querySelectorAll(selector)].filter(node => norm(node.textContent) === expected);
  if (matches.length === 0) return { ready:false, terminal:false, reason:'scoped target not rendered yet: ' + expected };
  if (matches.length !== 1) return { ready:false, terminal:true, reason:'expected one scoped target, got ' + matches.length + ': ' + expected };
  const node = matches[0];
  node.scrollIntoView({ block:'center', inline:'center' });
  await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  const rect = node.getBoundingClientRect();
  const style = getComputedStyle(node);
  const visible = rect.width > 0 && rect.height > 0 && style.visibility !== 'hidden' && style.display !== 'none';
  const disabled = node.matches(':disabled');
  if (!visible) return { ready:false, terminal:false, reason:'scoped target hidden' };
  if (disabled) return { ready:false, terminal:false, reason:'scoped target disabled' };
  const x = rect.left + rect.width/2, y = rect.top + rect.height/2;
  const hit = document.elementFromPoint(x, y);
  const unobscured = hit === node || (!!hit && node.contains(hit));
  return { ready:unobscured, terminal:false, reason:unobscured ? 'scoped target ready' : 'scoped target centre obscured', x, y, width:rect.width, height:rect.height, disabled:false, tag:node.tagName, text:norm(node.textContent), hit_tag:hit?.tagName ?? '', hit_id:hit?.id ?? '' };
})()
"@
    $target = Wait-CdpReady -Socket $Socket -Expression $expression -Description "Scoped UI target '$Text' under '$FieldLabel'" -TimeoutSeconds 10
    Send-CdpMouseClick -Socket $Socket -X $target.x -Y $target.y
    return $target
}

function Invoke-UiClickInFieldsetByLegend {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Legend,
        [Parameter(Mandatory)][string]$Text,
        [string]$DetailsSummary = 'Couches : nom, ordre, densité et espacement'
    )
    $legendLiteral = ConvertTo-CompactJson -Value $Legend
    $textLiteral = ConvertTo-CompactJson -Value $Text
    $summaryLiteral = ConvertTo-CompactJson -Value $DetailsSummary
    $expression = @"
(async () => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const legendText = $legendLiteral, expected = $textLiteral, summaryText = $summaryLiteral;
  const summaries = [...document.querySelectorAll('summary')].filter(node => norm(node.textContent) === summaryText);
  if (summaries.length !== 1) return { ready:false, terminal:true, reason:'expected one details summary, got ' + summaries.length };
  const details = summaries[0].closest('details');
  if (!details) return { ready:false, terminal:true, reason:'details parent absent' };
  details.open = true;
  const fieldsets = [...details.querySelectorAll('fieldset')].filter(fieldset => norm(fieldset.querySelector(':scope > legend')?.textContent) === legendText);
  if (fieldsets.length !== 1) return { ready:false, terminal:true, reason:'expected one direct legend ' + legendText + ', got ' + fieldsets.length };
  const matches = [...fieldsets[0].querySelectorAll('button')].filter(node => norm(node.textContent) === expected);
  if (matches.length !== 1) return { ready:false, terminal:true, reason:'expected one scoped button ' + expected + ', got ' + matches.length };
  const node = matches[0];
  node.scrollIntoView({ block:'center', inline:'center' });
  await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  const rect = node.getBoundingClientRect(), style = getComputedStyle(node);
  const visible = rect.width > 0 && rect.height > 0 && style.visibility !== 'hidden' && style.display !== 'none';
  if (!visible) return { ready:false, terminal:false, reason:'scoped button hidden' };
  if (node.matches(':disabled')) return { ready:false, terminal:false, reason:'scoped button disabled' };
  const x = rect.left + rect.width/2, y = rect.top + rect.height/2, hit = document.elementFromPoint(x, y);
  const unobscured = hit === node || (!!hit && node.contains(hit));
  return { ready:unobscured, terminal:false, reason:unobscured ? 'scoped legend button ready' : 'scoped legend button obscured', x, y, legend:legendText, text:expected };
})()
"@
    $target = Wait-CdpReady -Socket $Socket -Expression $expression -Description "UI target '$Text' in legend '$Legend'" -TimeoutSeconds 10
    Send-CdpMouseClick -Socket $Socket -X $target.x -Y $target.y
    return $target
}

function Set-UiControlByLabel {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][object]$Value,
        [ValidateSet('value', 'checked')][string]$Kind = 'value',
        [string]$DetailsSummary = '',
        [string]$FieldsetLegend = ''
    )
    $labelLiteral = ConvertTo-CompactJson -Value $Label
    $valueLiteral = ConvertTo-CompactJson -Value $Value
    $kindLiteral = ConvertTo-CompactJson -Value $Kind
    $summaryLiteral = ConvertTo-CompactJson -Value $DetailsSummary
    $legendLiteral = ConvertTo-CompactJson -Value $FieldsetLegend
    $expression = @"
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const expected = $labelLiteral;
  const summaryText = $summaryLiteral;
  const legendText = $legendLiteral;
  let scope = document;
  if (summaryText) {
    const summaries = [...document.querySelectorAll('summary')].filter(node => norm(node.textContent) === summaryText);
    if (summaries.length !== 1) throw new Error('Expected one details summary ' + summaryText + ', got ' + summaries.length);
    scope = summaries[0].closest('details');
    if (!scope) throw new Error('Details parent absent for ' + summaryText);
  }
  if (legendText) {
    const fieldsets = [...scope.querySelectorAll('fieldset')].filter(node => norm(node.querySelector(':scope > legend')?.textContent) === legendText);
    if (fieldsets.length !== 1) return { ready:false, terminal:true, reason:'expected one fieldset legend ' + legendText + ', got ' + fieldsets.length };
    scope = fieldsets[0];
  }
  const matches = [...scope.querySelectorAll('label')].filter(label => norm(label.textContent).startsWith(expected));
  if (matches.length !== 1) return { ready:false, terminal:true, reason:'expected one label ' + expected + ', got ' + matches.length };
  const control = matches[0].querySelector('input,select,textarea');
  if (!control) return { ready:false, terminal:true, reason:'no control under label ' + expected };
  if (control.disabled || control.closest('fieldset:disabled')) return { ready:false, terminal:false, reason:'control disabled under label ' + expected };
  const wanted = $kindLiteral === 'checked' ? !!$valueLiteral : String($valueLiteral);
  if ($kindLiteral === 'checked') control.checked = wanted;
  else control.value = wanted;
  control.dispatchEvent(new Event('input', { bubbles:true }));
  control.dispatchEvent(new Event('change', { bubbles:true }));
  const observed = $kindLiteral === 'checked' ? !!control.checked : String(control.value);
  const exact = observed === wanted;
  return { ready:exact, terminal:!exact, reason:exact ? 'control value accepted' : 'control value mismatch', label:expected, kind:$kindLiteral, wanted, observed, disabled:!!control.disabled };
})()
"@
    return Wait-CdpReady -Socket $Socket -Expression $expression -Description "Writable UI control '$Label'" -TimeoutSeconds 10
}

function Get-UiControlByLabel {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Label,
        [string]$DetailsSummary = ''
    )
    $labelLiteral = ConvertTo-CompactJson -Value $Label
    $summaryLiteral = ConvertTo-CompactJson -Value $DetailsSummary
    $expression = @"
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const expected = $labelLiteral;
  const summaryText = $summaryLiteral;
  let scope = document;
  if (summaryText) {
    const summaries = [...document.querySelectorAll('summary')].filter(node => norm(node.textContent) === summaryText);
    if (summaries.length !== 1) throw new Error('Expected one details summary ' + summaryText + ', got ' + summaries.length);
    scope = summaries[0].closest('details');
    if (!scope) throw new Error('Details parent absent for ' + summaryText);
  }
  const matches = [...scope.querySelectorAll('label')].filter(label => norm(label.textContent).startsWith(expected));
  if (matches.length !== 1) throw new Error('Expected one label ' + expected + ', got ' + matches.length);
  const control = matches[0].querySelector('input,select,textarea');
  if (!control) throw new Error('No control under label ' + expected);
  return { value:String(control.value ?? ''), checked:!!control.checked, disabled:!!control.disabled || control.closest('fieldset:disabled') !== null, tag:control.tagName, type:control.type ?? null };
})()
"@
    return Invoke-CdpExpression -Socket $Socket -Expression $expression
}

function Set-UiControlByLabelWithKeyboard {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][string]$Text,
        [string]$DetailsSummary = ''
    )
    $labelLiteral = ConvertTo-CompactJson -Value $Label
    $summaryLiteral = ConvertTo-CompactJson -Value $DetailsSummary
    $textLiteral = ConvertTo-CompactJson -Value $Text
    $focus = @"
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim(), expected=$labelLiteral, summaryText=$summaryLiteral;
  let scope=document;
  if (summaryText) {
    const summaries=[...document.querySelectorAll('summary')].filter(node=>norm(node.textContent)===summaryText);
    if (summaries.length!==1) throw new Error('Expected one details summary ' + summaryText + ', got ' + summaries.length);
    scope=summaries[0].closest('details'); scope.open=true;
  }
  const labels=[...scope.querySelectorAll('label')].filter(node=>norm(node.textContent).startsWith(expected));
  if (labels.length!==1) throw new Error('Expected one label ' + expected + ', got ' + labels.length);
  const control=labels[0].querySelector('input,textarea');
  if (!control || control.disabled || control.closest('fieldset:disabled')) throw new Error('Writable text control absent for ' + expected);
  control.scrollIntoView({block:'center',inline:'center'}); control.focus();
  return { focused:document.activeElement===control, before:String(control.value) };
})()
"@
    $prepared = Invoke-CdpExpression -Socket $Socket -Expression $focus
    if (-not $prepared.focused) { throw "Keyboard input '$Label' did not obtain exact focus." }
    Send-CdpKeyStroke -Socket $Socket -Key 'a' -Code 'KeyA' -VirtualKeyCode 65 -Modifiers 2
    [void](Invoke-CdpCommand -Socket $Socket -Method 'Input.insertText' -Parameters @{ text = $Text })
    Send-CdpKeyStroke -Socket $Socket -Key 'Tab' -Code 'Tab' -VirtualKeyCode 9
    $verify = @"
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim(), expected=$labelLiteral, summaryText=$summaryLiteral, wanted=$textLiteral;
  let scope=document;
  if(summaryText){const summaries=[...document.querySelectorAll('summary')].filter(node=>norm(node.textContent)===summaryText); if(summaries.length!==1) return {ready:false,terminal:true,reason:'summary cardinality changed'}; scope=summaries[0].closest('details');}
  const labels=[...scope.querySelectorAll('label')].filter(node=>norm(node.textContent).startsWith(expected));
  if(labels.length!==1) return {ready:false,terminal:true,reason:'label cardinality changed'};
  const control=labels[0].querySelector('input,textarea');
  const value=String(control?.value ?? '');
  return {ready:!!control && value===wanted && document.activeElement!==control,terminal:false,reason:'value=' + value,value};
})()
"@
    return Wait-CdpReady -Socket $Socket -Expression $verify -Description "Keyboard-entered UI control '$Label'" -TimeoutSeconds 10
}

function Select-UiOptionByLabel {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Label,
        [AllowEmptyString()]
        [Parameter(Mandatory)][string]$Value,
        [ValidateRange(1, 180)][int]$TimeoutSeconds = 10
    )
    $labelLiteral = ConvertTo-CompactJson -Value $Label
    $valueLiteral = ConvertTo-CompactJson -Value $Value
    $expression = @"
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const expected = $labelLiteral;
  const value = $valueLiteral;
  const labels = [...document.querySelectorAll('label')].filter(label => norm(label.textContent).startsWith(expected));
  if (labels.length === 0) return { ready:false, terminal:false, reason:'select label not rendered yet: ' + expected };
  if (labels.length !== 1) return { ready:false, terminal:true, reason:'expected one select label ' + expected + ', got ' + labels.length };
  const select = labels[0].querySelector('select');
  if (!select) return { ready:false, terminal:true, reason:'select absent for ' + expected };
  const disabledFieldset = select.closest('fieldset[disabled]');
  if (select.matches(':disabled')) return { ready:false, terminal:false, reason:'select temporarily disabled for ' + expected + '; own_disabled=' + !!select.disabled + '; ancestor=' + norm(disabledFieldset?.querySelector(':scope > legend')?.textContent) };
  const option = [...select.options].find(item => item.value.toLowerCase() === value.toLowerCase());
  if (!option || option.disabled) return { ready:false, terminal:false, reason:'exact option unavailable: ' + value };
  select.value = option.value;
  select.dispatchEvent(new Event('change', { bubbles:true }));
  const ready = select.value.toLowerCase() === value.toLowerCase();
  return { ready, terminal:false, reason:ready ? 'exact option selected' : 'selection did not stick', value:select.value, text:norm(option.textContent), option_count:select.options.length - 1 };
})()
"@
    $state = Wait-CdpReady -Socket $Socket -Expression $expression -Description "Exact option '$Value' under '$Label'" -TimeoutSeconds $TimeoutSeconds
    if (-not $state.ready) {
        throw "Exact option '$Value' was not selected under '$Label'."
    }
    return $state
}

function Open-DetailsBySummary {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Summary
    )
    $summaryLiteral = ConvertTo-CompactJson -Value $Summary
    $expression = @"
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const expected = $summaryLiteral;
  const matches = [...document.querySelectorAll('summary')].filter(node => norm(node.textContent) === expected);
  if (matches.length !== 1) throw new Error('Expected one summary ' + expected + ', got ' + matches.length);
  const details = matches[0].closest('details');
  if (!details) throw new Error('Summary has no details parent: ' + expected);
  details.open = true;
  return { ready:true, open:details.open };
})()
"@
    return Invoke-CdpExpression -Socket $Socket -Expression $expression
}

function Get-CanvasGeometry {
    param([Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket)
    return Wait-CdpReady -Socket $Socket -Description 'Authoring canvas geometry' -TimeoutSeconds 10 -Expression @'
(() => {
  const canvas = document.getElementById('forge-authoring-canvas');
  if (!canvas) return { ready:false, reason:'canvas absent' };
  const rect = canvas.getBoundingClientRect();
  const ready = rect.width > 0 && rect.height > 0;
  return { ready, reason:ready ? 'canvas ready' : 'canvas has no size', left:rect.left, top:rect.top, width:rect.width, height:rect.height, center_x:rect.left + rect.width/2, center_y:rect.top + rect.height/2 };
})()
'@
}

function Get-WorldState {
    param([Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket)
    return Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const byLabel = prefix => [...document.querySelectorAll('label')].find(node => norm(node.textContent).startsWith(prefix))?.querySelector('select');
  const options = select => select ? [...select.options].slice(1).map(option => ({ value:option.value, text:norm(option.textContent), disabled:!!option.disabled })) : [];
  const audit = window.__forgeGate2WorldAudit;
  const entries = audit?.entries ?? [];
  const authoring = entries.filter(entry => entry.command === 'execute_studio_command' && entry.wire?.command === 'authoring');
  const malformed = entries.filter(entry => entry.command === 'execute_studio_command' && (entry.parse_error || !entry.wire?.command));
  const count = kind => authoring.filter(entry => entry.request_kind === kind).length;
  const placement = byLabel('Objet placé à inspecter');
  const building = byLabel('Bâtiment à inspecter');
  const diff = document.querySelector('.authoring-command-diff');
  const apply = document.querySelector('.authoring-apply');
  const commandStatus = document.querySelector('.authoring-command-status');
  const mode = [...document.querySelectorAll('output')].map(node => norm(node.textContent)).find(text => text.startsWith('Mode ')) ?? '';
  const placementStatus = [...document.querySelectorAll('output')].map(node => norm(node.textContent)).find(text => text.startsWith('Placements') || text.startsWith('Aucun placement manuel') || /^\d+ placements\b/.test(text)) ?? '';
  const buildingStatus = [...document.querySelectorAll('output')].map(node => norm(node.textContent)).find(text => text.startsWith('Bâtiments')) ?? '';
  const buildingDetails = [...document.querySelectorAll('summary')].find(node => norm(node.textContent) === 'Bâtiments paramétriques')?.closest('details');
  const buildingOutputs = buildingDetails ? [...buildingDetails.querySelectorAll('output')].map(node => norm(node.textContent)).filter(Boolean) : [];
  const buildingPickStatus = [...buildingOutputs].reverse().find(text =>
    text.startsWith('Cliquer un bâtiment visible') ||
    text.startsWith('Bâtiment sélectionné :') ||
    text.startsWith('Aucun bâtiment sélectionnable') ||
    text.startsWith('Sélection bâtiment indisponible') ||
    text.startsWith('Sélection refusée :')) ?? '';
  const previewDetails = [...document.querySelectorAll('summary')].find(node => norm(node.textContent) === 'Bibliothèque d’assets')?.closest('details');
  const previewOutputs = previewDetails ? [...previewDetails.querySelectorAll('output')].map(node => norm(node.textContent)).filter(Boolean) : [];
  const placementPickStatus = [...previewOutputs].reverse().find(text =>
    text.startsWith('Cliquer un objet visible') ||
    text.startsWith('Objet sélectionné :') ||
    text.startsWith('Aucun placement sélectionnable') ||
    text.startsWith('Sélection indisponible pendant')) ?? '';
  const selectedAsset = norm(document.querySelector('section[aria-label="Fiche asset"] h3')?.textContent);
  const lastPlaytest = [...document.querySelectorAll('output')].map(node => norm(node.textContent)).find(text => text.startsWith('Dernier playtest ·')) ?? '';
  const diagnosticRaw = document.getElementById('forge-placement-gizmo-diagnostic')?.textContent ?? '';
  let diagnostic = null;
  try { diagnostic = diagnosticRaw ? JSON.parse(diagnosticRaw) : null; } catch {}
  return {
    url:location.href, title:document.title, focused:document.hasFocus(),
    placement_value:placement?.value ?? '', placement_options:options(placement),
    building_value:building?.value ?? '', building_options:options(building),
    placement_disabled:!placement || placement.disabled || placement.closest('fieldset:disabled') !== null,
    building_disabled:!building || building.disabled || building.closest('fieldset:disabled') !== null,
    diff_text:norm(diff?.textContent), diff_hidden:!diff || diff.hidden || norm(diff.textContent) === '',
    apply_disabled:!apply || apply.disabled, command_status:norm(commandStatus?.textContent), mode,
    placement_status:placementStatus, building_status:buildingStatus, building_pick_status:buildingPickStatus,
    playtest_status:norm(document.getElementById('authoring-playtest-status')?.textContent), last_playtest_status:lastPlaytest,
    pointer_lock_id:document.pointerLockElement?.id ?? '', selected_asset: selectedAsset,
    asset_preview_outputs:previewOutputs, placement_pick_status:placementPickStatus, diagnostic_raw:diagnosticRaw, diagnostic,
    audit_present:!!audit, audit_invalid:malformed.length,
    audit_pending:authoring.filter(entry => entry.outcome === 'pending').length,
    audit_counts:{ inspect:count('inspect'), list_zones:count('list_zones'), read_history:count('read_history'), preview_zone:count('preview_zone'), apply_zone:count('apply_zone'), save:count('save'), close:count('close'), open:count('open'), start_playtest:count('start_playtest'), start_playtest_at:count('start_playtest_at'), stop_playtest:count('stop_playtest') },
    asset_transfer:audit?.asset_transfer ? { ...audit.asset_transfer } : null
  };
})()
'@
}

function Get-TransversalState {
    param([Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket)
    return Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const selectFor = prefix => [...document.querySelectorAll('label')].filter(node => norm(node.textContent).startsWith(prefix)).map(node => node.querySelector('select')).filter(Boolean);
  const uniqueSelect = prefix => { const values=selectFor(prefix); return values.length === 1 ? values[0] : null; };
  const options = (select, includePlaceholder=false) => select ? [...select.options].slice(includePlaceholder ? 0 : 1).map(option => ({ value:option.value, text:norm(option.textContent), disabled:!!option.disabled })) : [];
  const detailsFor = summary => {
    const matches=[...document.querySelectorAll('summary')].filter(node => norm(node.textContent) === summary);
    return matches.length === 1 ? matches[0].closest('details') : null;
  };
  const directControl = (scope, prefix) => {
    const labels=scope ? [...scope.querySelectorAll('label')].filter(node => norm(node.textContent).startsWith(prefix)) : [];
    const control=labels.length === 1 ? labels[0].querySelector('input,select,textarea') : null;
    return control ? { value:String(control.value ?? ''), checked:!!control.checked, disabled:!!control.disabled || !!control.closest('fieldset:disabled') } : null;
  };
  const vegetationSelect=uniqueSelect('Couche de végétation');
  const recipeDetails=detailsFor('Couches : nom, ordre, densité et espacement');
  const layerCards=recipeDetails ? [...recipeDetails.querySelectorAll('fieldset')].map(fieldset => {
    const legend=norm(fieldset.querySelector(':scope > legend')?.textContent);
    if (!legend) return null;
    return {
      legend,
      name:directControl(fieldset, 'Nom de la couche'),
      density:directControl(fieldset, 'Densité de la recette (%)'),
      spacing:directControl(fieldset, 'Espacement minimum (m)'),
      buttons:[...fieldset.querySelectorAll('button')].map(node => ({ text:norm(node.textContent), disabled:!!node.disabled }))
    };
  }).filter(Boolean) : [];
  const densitySection=document.querySelector('section[aria-label="Source et verrou de densité"]');
  const densityOutput=norm(densitySection?.querySelector('output')?.textContent);
  const densityProvenance=norm(densitySection?.querySelector('details pre')?.textContent);
  const placementSelect=uniqueSelect('Objet placé à inspecter');
  const placementFieldset=placementSelect?.closest('fieldset') ?? null;
  const placementSource=[...(placementFieldset?.querySelectorAll('p') ?? [])].map(node=>norm(node.textContent)).find(text=>text.startsWith('Source acceptée : asset ')) ?? '';
  const buildingSelect=uniqueSelect('Bâtiment à inspecter');
  const buildingDetails=detailsFor('Bâtiments paramétriques');
  const buildingSource=[...(buildingDetails?.querySelectorAll('p') ?? [])].map(node=>norm(node.textContent)).find(text=>text.startsWith('Porte acceptée —')) ?? '';
  const pathSelect=uniqueSelect('Chemin à éditer');
  const pathDetails=detailsFor('Chemins — points et profil');
  const pathSource=[...(pathDetails?.querySelectorAll('p') ?? [])].map(node=>norm(node.textContent)).find(text=>text.startsWith('Source : ')) ?? '';
  const riverSelect=uniqueSelect('Rivière à éditer');
  const riverDetails=detailsFor('Rivières — tracé, niveau et creusement');
  const riverSource=[...(riverDetails?.querySelectorAll('p') ?? [])].map(node=>norm(node.textContent)).find(text=>text.startsWith('Source : ')) ?? '';
  const history=document.querySelector('section[aria-label="Historique des retouches"]');
  const historyButtons=text => history ? [...history.querySelectorAll('button')].filter(node=>norm(node.textContent)===text) : [];
  const historyEntries=history ? [...history.querySelectorAll('ol > li')].map(li => {
    const direct=[...li.querySelectorAll(':scope > p')].map(node=>norm(node.textContent));
    const identity=[...li.querySelectorAll('p')].map(node=>norm(node.textContent)).find(text=>/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12} · [0-9a-f]{64}$/i.test(text)) ?? '';
    return { label:direct[0] ?? '', audit:direct[1] ?? '', identity, change:li.querySelector('details pre')?.textContent ?? '' };
  }) : [];
  const audit=window.__forgeGate2WorldAudit;
  return {
    layer_value:vegetationSelect?.value ?? '', layer_options:options(vegetationSelect), layer_cards:layerCards,
    density_output:densityOutput, density_provenance:densityProvenance,
    placement_value:placementSelect?.value ?? '', placement_options:options(placementSelect), placement_source:placementSource,
    building_value:buildingSelect?.value ?? '', building_options:options(buildingSelect), building_source:buildingSource,
    path_value:pathSelect?.value ?? '', path_options:options(pathSelect), path_source:pathSource,
    river_value:riverSelect?.value ?? '', river_options:options(riverSelect), river_source:riverSource,
    history_status:norm(history?.querySelector('output')?.textContent), history_entries:historyEntries,
    history_newer_disabled:historyButtons('Plus récentes').length === 1 ? !!historyButtons('Plus récentes')[0].disabled : null,
    history_older_disabled:historyButtons('Plus anciennes').length === 1 ? !!historyButtons('Plus anciennes')[0].disabled : null,
    last_playtest_status:[...document.querySelectorAll('output')].map(node=>norm(node.textContent)).find(text=>text.startsWith('Dernier playtest ·')) ?? '',
    audit_pending:(audit?.entries ?? []).filter(entry=>entry.outcome==='pending').length,
    retry_next:audit?.retry_next ? { ...audit.retry_next } : null
  };
})()
'@
}

function Get-WorkspaceHistoryUi {
    param([Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket)
    return Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const summaries=[...document.querySelectorAll('summary')].filter(node=>norm(node.textContent)==='Historique du workspace');
  const history=summaries.length===1?summaries[0].closest('details'):null;
  const page=[...(history?.querySelectorAll('p')??[])].map(node=>norm(node.textContent)).find(text=>/^Opérations \d+–\d+ sur \d+$/.test(text))??'';
  const button=text=>[...(history?.querySelectorAll('button')??[])].find(node=>norm(node.textContent)===text);
  const projectLabel=[...document.querySelectorAll('label')].find(node=>norm(node.textContent).startsWith('Projet du nouveau module'));
  const select=projectLabel?.querySelector('select');
  return {history_present:!!history, page, entries:[...(history?.querySelectorAll('ul > li')??[])].map(li=>({id:li.title,label:norm(li.querySelector('p')?.textContent),audit:norm(li.querySelector('[data-testid="workspace-history-audit-stamp"]')?.textContent)})), older_disabled:button('Plus anciennes')?.disabled??null, newer_disabled:button('Plus récentes')?.disabled??null, project_options:select?[...select.options].slice(1).map(option=>({value:option.value,text:norm(option.textContent)})):[]};
})()
'@
}

function Wait-TransversalState {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Description,
        [Parameter(Mandatory)][scriptblock]$Predicate,
        [int]$TimeoutSeconds = 30
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $last = $null
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $last = Get-TransversalState -Socket $Socket
        if (& $Predicate $last) { return $last }
        Start-Sleep -Milliseconds 200
    }
    $summary = if ($null -eq $last) { 'no state' } else { ConvertTo-CompactJson -Value $last }
    throw "$Description did not become true within $TimeoutSeconds seconds. Last state: $summary"
}

function Wait-WorldState {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Description,
        [Parameter(Mandatory)][scriptblock]$Predicate,
        [int]$TimeoutSeconds = 30
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $last = $null
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $last = Get-WorldState -Socket $Socket
        if (& $Predicate $last) {
            return $last
        }
        Start-Sleep -Milliseconds 200
    }
    $summary = if ($null -eq $last) { 'no state' } else { ConvertTo-CompactJson -Value $last }
    throw "$Description did not become true within $TimeoutSeconds seconds. Last state: $summary"
}

function Get-MutationCountTotal {
    param([Parameter(Mandatory)][object]$State)
    return [int]$State.audit_counts.preview_zone + [int]$State.audit_counts.apply_zone + [int]$State.audit_counts.save
}

function Assert-NoMutationIpcDelta {
    param(
        [Parameter(Mandatory)][object]$Before,
        [Parameter(Mandatory)][object]$After,
        [Parameter(Mandatory)][string]$Description
    )
    foreach ($kind in @('preview_zone', 'apply_zone', 'save')) {
        if ([int]$Before.audit_counts.$kind -ne [int]$After.audit_counts.$kind) {
            throw "$Description unexpectedly changed '$kind' IPC count."
        }
    }
    if ([int]$After.audit_invalid -ne 0) {
        throw "$Description observed malformed audited IPC."
    }
}

function Assert-PreviewWithoutMutationIpcDelta {
    param(
        [Parameter(Mandatory)][object]$Before,
        [Parameter(Mandatory)][object]$After,
        [Parameter(Mandatory)][string]$Description
    )
    if ([int]$After.audit_counts.preview_zone -ne ([int]$Before.audit_counts.preview_zone + 1)) {
        throw "$Description did not emit exactly one PreviewZone IPC."
    }
    foreach ($kind in @('apply_zone', 'save')) {
        if ([int]$Before.audit_counts.$kind -ne [int]$After.audit_counts.$kind) {
            throw "$Description unexpectedly changed '$kind' IPC count."
        }
    }
    if ([int]$After.audit_invalid -ne 0) {
        throw "$Description observed malformed audited IPC."
    }
}

function Save-WorldScreenshot {
    param(
        [Parameter(Mandatory)][object]$Context,
        [Parameter(Mandatory)][string]$Name
    )
    $path = Join-Path $Context.EvidenceDirectory ($Name + '.png')
    $capture = Save-CdpScreenshot -Socket $Context.Socket -LiteralPath $path
    $Context.Screenshots.Add($capture)
    return $capture
}

function Install-WorldAudit {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [switch]$AssetPreviewDelay
    )
    $assetMode = if ($AssetPreviewDelay) { 'true' } else { 'false' }
    $expression = @"
(() => {
  const norm = value => (value ?? '').replace(/\s+/g, ' ').trim();
  const core = window.__TAURI__?.core;
  if (!core || typeof core.invoke !== 'function') throw new Error('Tauri core.invoke unavailable');
  if (window.__forgeGate2WorldAudit) throw new Error('Gate 2 world audit already installed');
  const fetchDescriptor = Object.getOwnPropertyDescriptor(window, 'fetch');
  const originalFetch = window.fetch;
  if (typeof originalFetch !== 'function') throw new Error('window.fetch unavailable');
  if (fetchDescriptor && !fetchDescriptor.writable && !fetchDescriptor.configurable) throw new Error('window.fetch cannot be replaced/restored safely');
  const storageKey = '__forge_gate2_world_audit_entries_v1';
  let persistedEntries = [];
  try {
    const encoded = sessionStorage.getItem(storageKey);
    if (encoded) persistedEntries = JSON.parse(encoded);
    if (!Array.isArray(persistedEntries)) throw new Error('persisted audit entries are not an array');
  } catch (error) { throw new Error('Cannot restore the cross-reload world audit: ' + error); }
  const audit = {
    mechanism:'window.fetch exact Tauri IPC endpoint; bounded cloned response receipts; sessionStorage continuity across the owned authoring reload; optional one-shot exact ApplyZone transport retry; optional single governed GLB stream shim',
    originalFetch, fetchDescriptor, hadOwnFetch:!!fetchDescriptor,
    storageKey, entries:persistedEntries, asset_mode:$assetMode,
    asset_path:'./forge-kit/assets/vv-tree-oak-a.glb', asset_transfer:null,
    retry_next:null
  };
  const persistEntries = () => sessionStorage.setItem(storageKey, JSON.stringify(audit.entries));
  persistEntries();
  const sha256 = async bytes => [...new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))].map(value => value.toString(16).padStart(2,'0')).join('');
  const stableJson = value => {
    if (value === null || typeof value !== 'object') return JSON.stringify(value);
    if (Array.isArray(value)) return '[' + value.map(stableJson).join(',') + ']';
    return '{' + Object.keys(value).sort().map(key => JSON.stringify(key) + ':' + stableJson(value[key])).join(',') + '}';
  };
  const hashJson = value => sha256(new TextEncoder().encode(stableJson(value)));
  const decodeResponse = raw => {
    let decoded = JSON.parse(raw);
    if (typeof decoded === 'string') decoded = JSON.parse(decoded);
    if (decoded && typeof decoded === 'object' && typeof decoded.Ok === 'string') decoded = JSON.parse(decoded.Ok);
    return decoded;
  };
  const summarizeResponse = async response => {
    const raw = await response.clone().text();
    const decoded = decodeResponse(raw);
    const summary = { response:String(decoded?.response ?? ''), zone_receipt:null };
    const receipt = decoded?.response === 'authoring' ? decoded?.state?.zone_receipt : null;
    if (receipt) {
      summary.zone_receipt = {
        command_id:String(receipt?.request?.command_id ?? ''),
        request_sha256:await hashJson(receipt.request),
        applied_sha256:await hashJson(receipt.applied),
        inverse_sha256:await hashJson(receipt.inverse),
        base_change_sha256:receipt.base_change == null ? null : await hashJson(receipt.base_change),
        resulting_revision:String(receipt.resulting_revision ?? ''),
        resulting_hash:String(receipt.resulting_hash ?? ''),
      };
    }
    return summary;
  };
  const delayedResponse = async (args, rawUrl, signal) => {
    const transfer = { requested_url:rawUrl, canonical_path:audit.asset_path, started_at_ms:performance.now(), state:'fetching_original', chunks:0, bytes_delivered:0, abort_observed:false, completed:false, cancelled_by_consumer:false };
    audit.asset_transfer = transfer;
    const original = await audit.originalFetch.apply(window, args);
    if (!original.ok) throw new Error('Original oak fetch failed with HTTP ' + original.status);
    const bytes = new Uint8Array(await original.arrayBuffer());
    transfer.bytes = bytes.byteLength;
    transfer.sha256 = await sha256(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength));
    transfer.state = 'streaming';
    const headers = new Headers(original.headers);
    headers.set('content-length', String(bytes.byteLength));
    let timer = null;
    let cursor = 0;
    let closed = false;
    let abortHandler = null;
    const stream = new ReadableStream({
      start(controller) {
        const finishAbort = reason => {
          if (closed) return;
          closed = true;
          transfer.abort_observed = true;
          transfer.state = 'aborted';
          transfer.completed_at_ms = performance.now();
          if (timer !== null) clearTimeout(timer);
          try { controller.error(reason instanceof Error ? reason : new DOMException('Aborted', 'AbortError')); } catch {}
        };
        abortHandler = () => finishAbort(new DOMException('Aborted', 'AbortError'));
        if (signal?.aborted) { abortHandler(); return; }
        signal?.addEventListener('abort', abortHandler, { once:true });
        const pump = () => {
          if (closed) return;
          if (signal?.aborted) { abortHandler(); return; }
          if (cursor >= bytes.byteLength) {
            closed = true;
            transfer.state = 'completed'; transfer.completed = true; transfer.completed_at_ms = performance.now();
            signal?.removeEventListener('abort', abortHandler);
            controller.close();
            return;
          }
          const end = Math.min(cursor + 131072, bytes.byteLength);
          controller.enqueue(bytes.slice(cursor, end));
          cursor = end; transfer.chunks += 1; transfer.bytes_delivered = cursor;
          timer = setTimeout(pump, 35);
        };
        timer = setTimeout(pump, 350);
      },
      cancel(reason) {
        if (closed) return;
        closed = true; transfer.cancelled_by_consumer = true; transfer.state = 'consumer_cancelled'; transfer.cancel_reason = String(reason ?? ''); transfer.completed_at_ms = performance.now();
        if (timer !== null) clearTimeout(timer);
        signal?.removeEventListener('abort', abortHandler);
      }
    });
    return new Response(stream, { status:original.status, statusText:original.statusText, headers });
  };
  const wrapper = function(...args) {
    const input = args[0];
    const init = args[1] ?? {};
    const rawUrl = typeof input === 'string' || input instanceof URL ? String(input) : String(input?.url ?? '');
    let parsed = null;
    try { parsed = new URL(rawUrl, window.location.href); } catch {}
    const ipcEndpoint = !!parsed && parsed.pathname === '/execute_studio_command' && (parsed.hostname === 'ipc.localhost' || parsed.protocol === 'ipc:');
    const exactOak = audit.asset_mode && !!parsed && parsed.origin === location.origin && parsed.pathname === '/forge-kit/assets/vv-tree-oak-a.glb' && !parsed.search && !parsed.hash;
    if (exactOak) {
      const signal = init?.signal ?? (input instanceof Request ? input.signal : null);
      return delayedResponse(args, rawUrl, signal);
    }
    let wire = null, requestKind = null, parseError = null;
    if (ipcEndpoint && typeof init?.body === 'string') {
      try {
        const payload = JSON.parse(init.body);
        wire = typeof payload?.command === 'string' ? JSON.parse(payload.command) : null;
        requestKind = wire?.command === 'authoring' && typeof wire?.request?.action === 'string' ? wire.request.action : null;
      } catch (error) { parseError = String(error); }
    }
    const entry = ipcEndpoint ? { index:audit.entries.length, transport:'fetch', url:rawUrl, method:String(init?.method ?? 'GET').toUpperCase(), command:'execute_studio_command', request_kind:requestKind, wire, parse_error:parseError, request_sha256:null, zone_request_sha256:null, response_parse_error:null, attempts:[], started_at_ms:performance.now(), outcome:'pending' } : null;
    if (entry) { audit.entries.push(entry); persistEntries(); }
    if (!entry) return audit.originalFetch.apply(window, args);
    try {
      const commandId = String(wire?.request?.request?.command_id ?? '');
      const requestPayload = wire?.request ?? null;
      const retry = audit.retry_next?.armed === true && audit.retry_next.request_kind === requestKind && audit.retry_next.command_id === commandId;
      return (async () => {
        try {
          entry.request_sha256 = requestPayload == null ? null : await hashJson(requestPayload);
          entry.zone_request_sha256 = wire?.request?.request == null ? null : await hashJson(wire.request.request);
          const first = await audit.originalFetch.apply(window, args);
          try { entry.attempts.push({ index:0, http_status:first?.status ?? null, response:await summarizeResponse(first) }); }
          catch (error) { entry.response_parse_error='attempt 0: ' + String(error); entry.attempts.push({ index:0, http_status:first?.status ?? null, response:null }); }
          let returned = first;
          if (retry) {
            audit.retry_next.armed = false;
            audit.retry_next.matched = true;
            audit.retry_next.matched_entry_index = entry.index;
            persistEntries();
            const second = await audit.originalFetch.apply(window, args);
            try { entry.attempts.push({ index:1, http_status:second?.status ?? null, response:await summarizeResponse(second) }); }
            catch (error) { entry.response_parse_error=(entry.response_parse_error ? entry.response_parse_error + ' | ' : '') + 'attempt 1: ' + String(error); entry.attempts.push({ index:1, http_status:second?.status ?? null, response:null }); }
            returned = second;
          }
          entry.outcome='resolved'; entry.completed_at_ms=performance.now(); entry.http_status=returned?.status ?? null; entry.synthetic_retry=retry; persistEntries();
          return returned;
        } catch (error) {
          entry.outcome='rejected'; entry.completed_at_ms=performance.now(); entry.error=String(error); persistEntries(); throw error;
        }
      })();
    } catch (error) {
      entry.outcome='threw'; entry.completed_at_ms=performance.now(); entry.error=String(error); persistEntries();
      throw error;
    }
  };
  audit.wrapper = wrapper;
  try {
    if (fetchDescriptor) {
      const descriptor = Object.prototype.hasOwnProperty.call(fetchDescriptor, 'value') ? { ...fetchDescriptor, value:wrapper } : { value:wrapper, writable:true, configurable:true, enumerable:!!fetchDescriptor.enumerable };
      Object.defineProperty(window, 'fetch', descriptor);
    } else {
      Object.defineProperty(window, 'fetch', { value:wrapper, writable:true, configurable:true, enumerable:true });
    }
    if (window.fetch !== wrapper) throw new Error('fetch audit installation did not stick');
    window.__forgeGate2WorldAudit = audit;
  } catch (error) {
    try { if (audit.hadOwnFetch) Object.defineProperty(window, 'fetch', audit.fetchDescriptor); else delete window.fetch; } catch (restore) { throw new Error('Audit install and rollback failed: ' + error + ' / ' + restore); }
    throw error;
  }
  return { installed:true, asset_mode:audit.asset_mode, fetch_descriptor:fetchDescriptor ? { writable:!!fetchDescriptor.writable, configurable:!!fetchDescriptor.configurable, enumerable:!!fetchDescriptor.enumerable } : null, core_frozen:Object.isFrozen(core) };
})()
"@
    $result = Invoke-CdpExpression -Socket $Socket -Expression $expression
    if (-not $result.installed) {
        throw 'Temporary world-editing fetch audit was not installed.'
    }
    return $result
}

function Get-WorldAuditSnapshot {
    param([Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket)
    return Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const audit = window.__forgeGate2WorldAudit;
  if (!audit) throw new Error('world audit absent');
  return { mechanism:audit.mechanism, asset_mode:audit.asset_mode, asset_path:audit.asset_path, asset_transfer:audit.asset_transfer ? { ...audit.asset_transfer } : null, retry_next:audit.retry_next ? { ...audit.retry_next } : null, entries:audit.entries.map(entry => ({ ...entry })), fetch_descriptor:audit.fetchDescriptor ? { writable:!!audit.fetchDescriptor.writable, configurable:!!audit.fetchDescriptor.configurable, enumerable:!!audit.fetchDescriptor.enumerable } : null };
})()
'@
}

function Arm-WorldAuditRetry {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][ValidateSet('apply_zone')][string]$RequestKind,
        [Parameter(Mandatory)][string]$CommandId
    )
    if ($CommandId -cnotmatch '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$') {
        throw "Retry command id is not a canonical UUID: $CommandId"
    }
    $kindLiteral = ConvertTo-CompactJson -Value $RequestKind
    $commandLiteral = ConvertTo-CompactJson -Value $CommandId
    $result = Invoke-CdpExpression -Socket $Socket -Expression @"
(() => {
  const audit = window.__forgeGate2WorldAudit;
  if (!audit) throw new Error('world audit absent');
  if (audit.retry_next?.armed) throw new Error('an exact transport retry is already armed');
  audit.retry_next = { armed:true, matched:false, request_kind:$kindLiteral, command_id:$commandLiteral, armed_at_ms:performance.now() };
  return { ...audit.retry_next };
})()
"@
    if (-not $result.armed -or [string]$result.request_kind -cne $RequestKind -or [string]$result.command_id -cne $CommandId) {
        throw 'Exact one-shot transport retry did not arm.'
    }
    return $result
}

function Restore-WorldAudit {
    param([Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket)
    $result = Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const audit = window.__forgeGate2WorldAudit;
  if (!audit) throw new Error('world audit cannot be restored');
  if (audit.hadOwnFetch) Object.defineProperty(window, 'fetch', audit.fetchDescriptor);
  else if (!delete window.fetch) throw new Error('temporary own fetch could not be deleted');
  if (window.fetch !== audit.originalFetch) throw new Error('original fetch was not restored');
  sessionStorage.removeItem(audit.storageKey);
  delete window.__forgeGate2WorldAudit;
  return { restored:true, exact_identity:true };
})()
'@
    if (-not $result.restored -or -not $result.exact_identity) {
        throw 'Original window.fetch descriptor/function was not restored exactly.'
    }
    return $result
}

function Install-WorldAuditAfterOwnedReload {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $expression = @'
(() => {
  const ready = document.readyState === 'complete' && document.title === 'OpenRPG Forge Studio' && !!window.__TAURI__?.core && !window.__forgeGate2WorldAudit;
  return { ready, terminal:false, reason:ready ? 'reloaded document ready' : 'waiting for owned authoring reload' };
})()
'@
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastFailure = 'reload not observed'
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        try {
            $state = Invoke-CdpExpression -Socket $Socket -Expression $expression
            if ($null -ne $state -and $state.ready -eq $true) {
                return Install-WorldAudit -Socket $Socket
            }
            if ($null -ne $state -and $state.PSObject.Properties.Name -contains 'reason') { $lastFailure = [string]$state.reason }
        }
        catch {
            if ($Socket.State -ne [System.Net.WebSockets.WebSocketState]::Open) { throw }
            $lastFailure = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 200
    }
    throw "Owned authoring document reload did not become auditable within $TimeoutSeconds seconds. Last state: $lastFailure"
}

function New-ExactFixtureReadyExpression {
    param(
        [Parameter(Mandatory)][string]$ZoneId,
        [Parameter(Mandatory)][string]$PlacementId,
        [Parameter(Mandatory)][string]$BuildingId
    )
    $zoneLiteral = ConvertTo-CompactJson -Value $ZoneId
    $placementLiteral = ConvertTo-CompactJson -Value $PlacementId
    $buildingLiteral = ConvertTo-CompactJson -Value $BuildingId
    return @"
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim(); const zone=$zoneLiteral; const placement=$placementLiteral; const building=$buildingLiteral;
  const canvas=document.getElementById('forge-authoring-canvas');
  const zoneTerm=[...document.querySelectorAll('.authoring-zone-information dt')].find(node=>norm(node.textContent)==='Zone');
  const shown=norm(zoneTerm?.nextElementSibling?.textContent).toLowerCase();
  const mode=[...document.querySelectorAll('output')].map(node=>norm(node.textContent)).find(text=>text.startsWith('Mode '))??'';
  const selectFor=prefix=>[...document.querySelectorAll('label')].find(node=>norm(node.textContent).startsWith(prefix))?.querySelector('select');
  const ps=selectFor('Objet placé à inspecter'), bs=selectFor('Bâtiment à inspecter');
  const hasP=!!ps && [...ps.options].some(option=>option.value.toLowerCase()===placement);
  const hasB=!!bs && [...bs.options].some(option=>option.value.toLowerCase()===building);
  const width=canvas?.clientWidth??0, height=canvas?.clientHeight??0;
  const terminal=shown===zone && (!hasP || !hasB);
  return {ready:shown===zone && mode.startsWith('Mode édition') && width>0 && height>0 && hasP && hasB, terminal, reason:'zone='+shown+'; mode='+mode+'; canvas='+width+'x'+height+'; placement='+hasP+'; building='+hasB, shown_zone:shown, mode, width, height, exact_placement:hasP, exact_building:hasB};
})()
"@
}

function Open-ExactAuthoringZone {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$ZoneId,
        [Parameter(Mandatory)][string]$PlacementId,
        [Parameter(Mandatory)][string]$BuildingId,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $zoneLiteral = ConvertTo-CompactJson -Value $ZoneId
    [void](Wait-CdpReady -Socket $Socket -Description 'Authoring document' -TimeoutSeconds $TimeoutSeconds -Expression @'
(() => ({ ready:document.readyState === 'complete' && !!document.querySelector('.authoring-shell'), reason:`readyState=${document.readyState}; shell=${!!document.querySelector('.authoring-shell')}` }))()
'@)
    [void](Invoke-CdpExpression -Socket $Socket -Expression @'
(() => { const norm=v=>(v??'').replace(/\s+/g,' ').trim(); const button=[...document.querySelectorAll('button')].find(node=>norm(node.textContent)==='Actualiser la liste'); if(!button) return {ready:false}; button.click(); return {ready:true}; })()
'@)
    $zoneReady = @"
(() => { const norm=v=>(v??'').replace(/\s+/g,' ').trim(); const zone=$zoneLiteral; const label=[...document.querySelectorAll('label')].find(node=>norm(node.textContent).startsWith('Choisir une zone')); const select=label?.querySelector('select'); const option=select ? [...select.options].find(item=>item.value.toLowerCase()===zone) : null; return {ready:!!option && !option.disabled, terminal:!!option?.disabled, reason:option ? 'exact zone listed' : 'exact zone absent'}; })()
"@
    [void](Wait-CdpReady -Socket $Socket -Expression $zoneReady -Description 'Exact zone catalogue entry' -TimeoutSeconds $TimeoutSeconds)
    $open = @"
(async () => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const zone=$zoneLiteral;
  const label=[...document.querySelectorAll('label')].find(node=>norm(node.textContent).startsWith('Choisir une zone'));
  const select=label?.querySelector('select');
  const button=[...document.querySelectorAll('button')].find(node=>norm(node.textContent)==='Ouvrir la zone choisie');
  if(!select||!button) return {ready:false,terminal:true,reason:'zone open controls absent'};
  const option=[...select.options].find(item=>item.value.toLowerCase()===zone);
  if(!option) return {ready:false,terminal:true,reason:'exact zone UUID disappeared before selection'};
  if(option.disabled) return {ready:false,terminal:true,reason:'exact zone is unavailable'};
  select.value=zone;
  select.dispatchEvent(new Event('change',{bubbles:true}));
  if(select.value.toLowerCase()!==zone) return {ready:false,terminal:true,reason:'exact UUID selection refused'};
  await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
  if(button.disabled) return {ready:false,terminal:true,reason:'open button stayed disabled after exact UUID selection'};
  button.click();
  return {ready:true,reason:'open requested'};
})()
"@
    $openResult = Invoke-CdpExpression -Socket $Socket -Expression $open
    if (-not $openResult.ready) {
        throw "Exact zone open request failed closed: $($openResult.reason)"
    }
    $ready = New-ExactFixtureReadyExpression -ZoneId $ZoneId -PlacementId $PlacementId -BuildingId $BuildingId
    return Wait-CdpReady -Socket $Socket -Expression $ready -Description 'Exact fixture identities and edit viewport' -TimeoutSeconds $TimeoutSeconds
}

function Open-FullAuthorRouteZone {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$ZoneId,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    # A full author route starts with an intentionally unpopulated source zone:
    # unlike Open-ExactAuthoringZone, do not demand a pre-existing placement or
    # building before any Studio control has been used.
    $zoneLiteral = ConvertTo-CompactJson -Value $ZoneId
    [void](Wait-CdpReady -Socket $Socket -Description 'Full-author-route document' -TimeoutSeconds $TimeoutSeconds -Expression @'
(() => ({ ready:document.readyState === 'complete' && !!document.querySelector('.authoring-shell'), reason:`readyState=${document.readyState}; shell=${!!document.querySelector('.authoring-shell')}` }))()
'@)
    [void](Invoke-CdpExpression -Socket $Socket -Expression @'
(() => { const norm=v=>(v??'').replace(/\s+/g,' ').trim(); const button=[...document.querySelectorAll('button')].find(node=>norm(node.textContent)==='Actualiser la liste'); if(!button) return {ready:false}; button.click(); return {ready:true}; })()
'@)
    $zoneReady = @"
(() => { const norm=v=>(v??'').replace(/\s+/g,' ').trim(); const zone=$zoneLiteral; const label=[...document.querySelectorAll('label')].find(node=>norm(node.textContent).startsWith('Choisir une zone')); const select=label?.querySelector('select'); const option=select ? [...select.options].find(item=>item.value.toLowerCase()===zone) : null; return {ready:!!option && !option.disabled, terminal:!!option?.disabled, reason:option ? 'fresh exact zone listed' : 'fresh exact zone absent'}; })()
"@
    [void](Wait-CdpReady -Socket $Socket -Expression $zoneReady -Description 'Fresh exact zone catalogue entry' -TimeoutSeconds $TimeoutSeconds)
    $openExpression = @"
(async () => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim(); const zone=$zoneLiteral;
  const label=[...document.querySelectorAll('label')].find(node=>norm(node.textContent).startsWith('Choisir une zone'));
  const select=label?.querySelector('select'); const button=[...document.querySelectorAll('button')].find(node=>norm(node.textContent)==='Ouvrir la zone choisie');
  const option=select ? [...select.options].find(item=>item.value.toLowerCase()===zone) : null;
  if(!select||!button||!option||option.disabled) return {ready:false,terminal:true,reason:'fresh exact zone open controls or UUID unavailable'};
  select.value=zone; select.dispatchEvent(new Event('change',{bubbles:true}));
  await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
  if(button.disabled) return {ready:false,terminal:true,reason:'open button disabled after fresh zone selection'};
  button.click(); return {ready:true,reason:'fresh zone open requested'};
})()
"@
    $opened = Invoke-CdpExpression -Socket $Socket -Expression $openExpression
    if (-not $opened.ready) { throw "Full-author-route zone open failed closed: $($opened.reason)" }
    $readyExpression = @"
(() => {
  const norm=v=>(v??'').replace(/\s+/g,' ').trim(); const zone=$zoneLiteral;
  const canvas=document.getElementById('forge-authoring-canvas');
  const term=[...document.querySelectorAll('.authoring-zone-information dt')].find(node=>norm(node.textContent)==='Zone');
  const shown=norm(term?.nextElementSibling?.textContent).toLowerCase();
  const mode=[...document.querySelectorAll('output')].map(node=>norm(node.textContent)).find(text=>text.startsWith('Mode '))??'';
  const width=canvas?.clientWidth??0, height=canvas?.clientHeight??0;
  return {ready:shown===zone && mode.startsWith('Mode édition') && width>0 && height>0, terminal:shown!=='' && shown!==zone, reason:'zone='+shown+'; mode='+mode+'; canvas='+width+'x'+height, shown_zone:shown, mode, width, height};
})()
"@
    return Wait-CdpReady -Socket $Socket -Expression $readyExpression -Description 'Fresh full-author-route edit viewport' -TimeoutSeconds $TimeoutSeconds
}

function Invoke-FullAuthorRouteScenario {
    param([Parameter(Mandatory)][object]$Context)
    $socket = $Context.Socket
    $criteria = @{
        'R2-037' = New-CriterionResult -Criterion 'R2-037'
        'R2-043' = New-CriterionResult -Criterion 'R2-043'
        'R2-049' = New-CriterionResult -Criterion 'R2-049'
        'R2-052' = New-CriterionResult -Criterion 'R2-052'
    }
    $applyBrush = {
        [void](Invoke-UiClick -Socket $socket -Selector '[data-authoring-tab-button]' -Text 'Terrain')
        $before = Get-WorldState -Socket $socket
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Outil' -Value '0')
        foreach ($setting in @(@('Rayon (m)','24'),@('Intensité (0–1)','0.8'),@('Variation / cible (m)','-4'))) {
            [void](Set-UiControlByLabel -Socket $socket -Label $setting[0] -Value $setting[1] -DetailsSummary 'Édition de zone · Gate 2' -FieldsetLegend 'Brosse de relief · aperçu avant application')
        }
        [void](Invoke-UiClickInFieldsetByLegend -Socket $socket -Legend 'Brosse de relief · aperçu avant application' -Text 'Placer dans la vue' -DetailsSummary 'Édition de zone · Gate 2')
        $canvas = Get-CanvasGeometry -Socket $socket
        Send-CdpMouseDrag -Socket $socket -StartX ($canvas.center_x - 36) -StartY $canvas.center_y -EndX ($canvas.center_x + 36) -EndY $canvas.center_y -Steps 4
        $preview = Wait-NewPreview -Socket $socket -PreviousCount ([int]$before.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds
        Assert-PreviewWithoutMutationIpcDelta -Before $before -After $preview -Description 'FullAuthorRoute physical valley depression preview'
        $previewEntry = Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'preview_zone'
        $previewReceipt = Get-RequiredAuditReceipt -Entry $previewEntry
        [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $applyEntry = Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'apply_zone'
        $applyReceipt = Get-RequiredAuditReceipt -Entry $applyEntry
        if (-not (Test-AuditReceiptsEqual -Left $previewReceipt -Right $applyReceipt)) { throw 'Valley preview and Apply receipts differ.' }
        [void](Invoke-UndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Invoke-UndoRedoKey -Socket $socket -Action Redo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        return [pscustomobject]@{ operation='canvas valley depression'; physical_canvas_gesture=$true; preview_without_mutation=$true; preview_receipt=$previewReceipt; apply_receipt=$applyReceipt; undo_redo=$true; source_archive_sha256=$Context.ArchiveBefore.Sha256 }
    }
    $valleyPassed = Invoke-CriterionCase -Criterion $criteria['R2-043'] -Name 'canvas_valley_preview_apply_undo_redo' -Action $applyBrush
    if ($valleyPassed) {
        $forestPassed = Invoke-CriterionCase -Criterion $criteria['R2-043'] -Name 'forest_seed_42_preview_apply_undo_redo' -Action {
            [void](Invoke-UiClick -Socket $socket -Selector '[data-authoring-tab-button]' -Text 'Nature')
            $before = Get-WorldState -Socket $socket
            [void](Open-DetailsBySummary -Socket $socket -Summary 'Recette de végétation')
            [void](Set-UiControlByLabel -Socket $socket -Label 'Nouvelle seed de végétation' -Value '42' -DetailsSummary 'Recette de végétation')
            [void](Invoke-UiClick -Socket $socket -Text 'Prévisualiser la recette de végétation')
            $preview = Wait-NewPreview -Socket $socket -PreviousCount ([int]$before.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds
            Assert-PreviewWithoutMutationIpcDelta -Before $before -After $preview -Description 'FullAuthorRoute forest seed 42 preview'
            $previewReceipt = Get-RequiredAuditReceipt -Entry (Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'preview_zone')
            [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
            $applyReceipt = Get-RequiredAuditReceipt -Entry (Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'apply_zone')
            if (-not (Test-AuditReceiptsEqual -Left $previewReceipt -Right $applyReceipt)) { throw 'Forest seed 42 preview and Apply receipts differ.' }
            [void](Invoke-UndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
            [void](Invoke-UndoRedoKey -Socket $socket -Action Redo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
            return [pscustomobject]@{ operation='forest seed 42'; preview_without_mutation=$true; preview_receipt=$previewReceipt; apply_receipt=$applyReceipt; undo_redo=$true }
        }
        if ($forestPassed) {
            $applyRoutePreview = {
                param([Parameter(Mandatory)][string]$Operation,[Parameter(Mandatory)][scriptblock]$Prepare)
                $before = Get-WorldState -Socket $socket
                & $Prepare
                $preview = Wait-NewPreview -Socket $socket -PreviousCount ([int]$before.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds
                Assert-PreviewWithoutMutationIpcDelta -Before $before -After $preview -Description "FullAuthorRoute $Operation preview"
                $previewReceipt = Get-RequiredAuditReceipt -Entry (Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'preview_zone')
                [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
                $applyReceipt = Get-RequiredAuditReceipt -Entry (Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'apply_zone')
                if (-not (Test-AuditReceiptsEqual -Left $previewReceipt -Right $applyReceipt)) { throw "$Operation preview and Apply receipts differ." }
                [void](Invoke-UndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
                [void](Invoke-UndoRedoKey -Socket $socket -Action Redo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
                return [pscustomobject]@{ operation=$Operation; preview_without_mutation=$true; preview_receipt=$previewReceipt; apply_receipt=$applyReceipt; undo_redo=$true }
            }
            $pathPassed = Invoke-CriterionCase -Criterion $criteria['R2-043'] -Name 'path_preview_apply_undo_redo' -Action {
                [void](Invoke-UiClick -Socket $socket -Selector '[data-authoring-tab-button]' -Text 'Tracés & eau')
                [void](Open-DetailsBySummary -Socket $socket -Summary 'Chemins — points et profil')
                [void](Invoke-UiClick -Socket $socket -Text 'Nouveau chemin')
                & $applyRoutePreview 'author path' { [void](Invoke-UiClickInDetailsBySummary -Socket $socket -Summary 'Chemins — points et profil' -Text 'Examiner le tracé') }
            }
            if ($pathPassed) {
                $riverPassed = Invoke-CriterionCase -Criterion $criteria['R2-043'] -Name 'river_preview_apply_undo_redo' -Action {
                    [void](Open-DetailsBySummary -Socket $socket -Summary 'Rivières — tracé, niveau et creusement')
                    [void](Invoke-UiClick -Socket $socket -Text 'Nouvelle rivière')
                    # The default river crosses the road at Z=0. Keep it near
                    # the valley and visible from the house, without colliding
                    # with the road, by moving its four Studio draft points.
                    foreach ($pointNumber in 1..4) {
                        $pointExpression = @"
(() => {
  const norm=value=>(value??'').replace(/\s+/g,' ').trim();
  const summaries=[...document.querySelectorAll('summary')].filter(node=>norm(node.textContent)==='Rivières — tracé, niveau et creusement');
  if(summaries.length!==1) return {ready:false,terminal:true,reason:'exact river details absent'};
  const fieldsets=[...summaries[0].closest('details').querySelectorAll('fieldset')].filter(node=>norm(node.querySelector(':scope > legend')?.textContent)==='Point $pointNumber');
  if(fieldsets.length!==1) return {ready:false,terminal:true,reason:'exact river point $pointNumber absent'};
  const labels=[...fieldsets[0].querySelectorAll('label')].filter(node=>norm(node.textContent)==='Z');
  if(labels.length!==1) return {ready:false,terminal:true,reason:'river point Z input absent'};
  const input=labels[0].querySelector('input');
  if(!input||input.disabled) return {ready:false,terminal:true,reason:'river point Z input disabled'};
  input.value='40'; input.dispatchEvent(new Event('input',{bubbles:true})); input.dispatchEvent(new Event('change',{bubbles:true}));
  return {ready:input.value==='40',terminal:input.value!=='40',reason:'river point Z='+input.value,point:$pointNumber};
})()
"@
                        [void](Wait-CdpReady -Socket $socket -Expression $pointExpression -Description "River point $pointNumber authored Z=80" -TimeoutSeconds 10)
                    }
                    & $applyRoutePreview 'author river' { [void](Invoke-UiClickInDetailsBySummary -Socket $socket -Summary 'Rivières — tracé, niveau et creusement' -Text 'Examiner la rivière') }
                }
                if ($riverPassed) {
                    $housePassed = Invoke-CriterionCase -Criterion $criteria['R2-043'] -Name 'house_preview_apply_undo_redo' -Action {
                        [void](Invoke-UiClick -Socket $socket -Selector '[data-authoring-tab-button]' -Text 'Objets')
                        [void](Open-DetailsBySummary -Socket $socket -Summary 'Bâtiments paramétriques')
                        # The road runs from X=-5 to +5 at Z=0. Put the house
                        # beyond its end, on valley ground near Y=5.1 m, so its
                        # walls are visible and the road is not underneath it.
                        [void](Set-UiControlByLabel -Socket $socket -Label 'Origine X (m)' -Value '12' -DetailsSummary 'Bâtiments paramétriques')
                        [void](Set-UiControlByLabel -Socket $socket -Label 'Altitude du sol fini Y (m)' -Value '5.2' -DetailsSummary 'Bâtiments paramétriques')
                        & $applyRoutePreview 'author house' { [void](Invoke-UiClickInDetailsBySummary -Socket $socket -Summary 'Bâtiments paramétriques' -Text 'Prévisualiser une nouvelle pièce') }
                    }
                    if ($housePassed) {
                        $clearingPassed = Invoke-CriterionCase -Criterion $criteria['R2-043'] -Name 'local_clearing_canvas_preview_apply_undo_redo' -Action {
                            [void](Invoke-UiClick -Socket $socket -Selector '[data-authoring-tab-button]' -Text 'Nature')
                            [void](Select-UiOptionByLabel -Socket $socket -Label 'Couche de végétation' -Value 'editor_trees')
                            $before = Get-WorldState -Socket $socket
                            [void](Invoke-UiClick -Socket $socket -Text 'Tracer une clairière dans la vue')
                            $canvas = Get-CanvasGeometry -Socket $socket
                            # Keep the clearing stroke clear of the projected house.
                            Send-CdpMouseDrag -Socket $socket -StartX ($canvas.center_x + 170) -StartY ($canvas.center_y + 110) -EndX ($canvas.center_x + 218) -EndY ($canvas.center_y + 110) -Steps 4
                            $preview = Wait-NewPreview -Socket $socket -PreviousCount ([int]$before.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds
                            Assert-PreviewWithoutMutationIpcDelta -Before $before -After $preview -Description 'FullAuthorRoute local clearing preview'
                            $previewReceipt = Get-RequiredAuditReceipt -Entry (Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'preview_zone')
                            [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
                            $applyReceipt = Get-RequiredAuditReceipt -Entry (Get-RequiredAuditedEntry -Snapshot (Get-WorldAuditSnapshot -Socket $socket) -RequestKind 'apply_zone')
                            if (-not (Test-AuditReceiptsEqual -Left $previewReceipt -Right $applyReceipt)) { throw 'Local clearing preview and Apply receipts differ.' }
                            [void](Invoke-UndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
                            [void](Invoke-UndoRedoKey -Socket $socket -Action Redo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
                            return [pscustomobject]@{ operation='local clearing'; physical_canvas_gesture=$true; preview_without_mutation=$true; preview_receipt=$previewReceipt; apply_receipt=$applyReceipt; undo_redo=$true }
                        }
                        if ($clearingPassed) {
                            $regenerationPassed = Invoke-CriterionCase -Criterion $criteria['R2-043'] -Name 'regenerate_forest_seed_43_preserving_override' -Action {
                                [void](Open-DetailsBySummary -Socket $socket -Summary 'Recette de végétation')
                                [void](Select-UiOptionByLabel -Socket $socket -Label 'Couche de végétation' -Value 'editor_trees')
                                $beforeOverride = Get-TransversalState -Socket $socket
                                if ([string]$beforeOverride.density_output -notlike 'Arbres · retouche manuelle*') { throw 'Clearing did not leave the expected authored tree-density override before regeneration.' }
                                & $applyRoutePreview 'forest regeneration seed 43' {
                                    [void](Set-UiControlByLabel -Socket $socket -Label 'Nouvelle seed de végétation' -Value '43' -DetailsSummary 'Recette de végétation')
                                    [void](Invoke-UiClick -Socket $socket -Text 'Prévisualiser la recette de végétation')
                                }
                                [void](Select-UiOptionByLabel -Socket $socket -Label 'Couche de végétation' -Value 'editor_trees')
                                $afterOverride = Wait-TransversalState -Socket $socket -Description 'Tree override after seed 43 regeneration' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate { param($state) [string]$state.density_output -like 'Arbres · retouche manuelle*' }
                                return [pscustomobject]@{ operation='forest regeneration seed 43'; seed_before='42'; seed_after='43'; override_before=$beforeOverride.density_output; override_after=$afterOverride.density_output; override_preserved=$true }
                            }
                            if ($regenerationPassed) {
                                $savePassed = Invoke-CriterionCase -Criterion $criteria['R2-043'] -Name 'save_close_open_exact_preserves_full_author_route' -Action {
                                    $beforeSave = Get-WorldState -Socket $socket
                                    [void](Invoke-UiClick -Socket $socket -Text 'Sauvegarder')
                                    $saved = Wait-WorldState -Socket $socket -Description 'FullAuthorRoute Save' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate { param($state) [int]$state.audit_counts.save -eq ([int]$beforeSave.audit_counts.save + 1) -and [int]$state.audit_pending -eq 0 }
                                    $savedArchive = Get-ArchiveSnapshot -LiteralPath $Context.ArchivePath
                                    if (Test-ArchiveSnapshotsEqual -Before $Context.ArchiveBefore -After $savedArchive) { throw 'Save did not persist the full-author-route copy.' }
                                    [void](Invoke-UiClick -Socket $socket -Selector '[data-authoring-tab-button]' -Text 'Projet')
                                    [void](Open-DetailsBySummary -Socket $socket -Summary 'Créer ou modifier la zone')
                                    [void](Invoke-UiClick -Socket $socket -Text 'Fermer la zone')
                                    [void](Install-WorldAuditAfterOwnedReload -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
                                    [void](Open-FullAuthorRouteZone -Socket $socket -ZoneId $Context.ZoneId -TimeoutSeconds $Context.InteractionTimeoutSeconds)
                                    $reopened = Wait-TransversalState -Socket $socket -Description 'FullAuthorRoute exact reopened authored content' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate { param($state) @($state.path_options).Count -eq 1 -and @($state.river_options).Count -eq 1 -and @($state.building_options).Count -eq 1 -and [string]$state.density_output -notlike '' }
                                    $sourceAfter = Get-ArchiveSnapshot -LiteralPath $Context.SourceArchiveBefore.Path
                                    if (-not (Test-ArchiveSnapshotsEqual -Before $Context.SourceArchiveBefore -After $sourceAfter)) { throw 'Source archive changed during FullAuthorRoute Save/Close/Open.' }
                                    return [pscustomobject]@{ saved_archive=$savedArchive; source_unchanged=$true; exact_zone_reopened=$Context.ZoneId; paths=@($reopened.path_options).Count; rivers=@($reopened.river_options).Count; buildings=@($reopened.building_options).Count }
                                }
                                if ($savePassed) {
                                    [void](Invoke-CriterionCase -Criterion $criteria['R2-049'] -Name 'accepted_river_provenance_after_reopen' -Action {
                                        $beforeInspect = Get-WorldState -Socket $socket
                                        [void](Invoke-UiClick -Socket $socket -Selector '[data-authoring-tab-button]' -Text 'Tracés & eau')
                                        [void](Open-DetailsBySummary -Socket $socket -Summary 'Rivières — tracé, niveau et creusement')
                                        $rivers = Get-TransversalState -Socket $socket
                                        if (@($rivers.river_options).Count -ne 1) { throw 'Reopened route must expose one accepted river.' }
                                        $riverId = [string]$rivers.river_options[0].value
                                        [void](Select-UiOptionByLabel -Socket $socket -Label 'Rivière à éditer' -Value $riverId)
                                        $accepted = Wait-TransversalState -Socket $socket -Description 'Accepted river source after reopen' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
                                            param($state) [string]$state.river_source -like 'Source : recette de rivière auteur acceptée*Verrou inactif*Niveaux Y absolus en mètres*'
                                        }
                                        $afterInspect = Get-WorldState -Socket $socket
                                        Assert-NoMutationIpcDelta -Before $beforeInspect -After $afterInspect -Description 'FullAuthorRoute accepted river inspection'
                                        return [pscustomobject]@{ accepted_river_id=$riverId; accepted_source=$accepted.river_source; inspector_read_only=$true }
                                    })
                                    [void](Invoke-CriterionCase -Criterion $criteria['R2-043'] -Name 'play_stop_return_edit_with_diagnostics' -Action {
                                    $beforePlay = Get-WorldState -Socket $socket
                                    [void](Invoke-UiClick -Socket $socket -Selector '[data-authoring-tab-button]' -Text 'Tester & suivi')
                                    [void](Invoke-UiClick -Socket $socket -Text 'Jouer')
                                    [void](Install-WorldAuditAfterOwnedReload -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
                                    $playing = Wait-WorldState -Socket $socket -Description 'FullAuthorRoute play ready' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate { param($state) [string]$state.playtest_status -like 'Simulation active*' -and [int]$state.audit_pending -eq 0 }
                                    $observation = Wait-PlaytestObservation -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds
                                    [void](Invoke-UiClick -Socket $socket -Selector '[data-authoring-tab-button]' -Text 'Tester & suivi')
                                    [void](Invoke-UiClick -Socket $socket -Text "Retour à l’édition")
                                    [void](Install-WorldAuditAfterOwnedReload -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
                                    $returned = Wait-WorldState -Socket $socket -Description 'FullAuthorRoute returned diagnostics' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate { param($state) [string]$state.mode -like 'Mode édition*' -and [string]$state.last_playtest_status -like 'Dernier playtest ·*' }
                                    $projected = Wait-CdpReady -Socket $socket -Description 'FullAuthorRoute projection after return' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Expression @'
(() => { const status=document.querySelector('.authoring-status')?.textContent?.trim()??''; const canvas=document.getElementById('forge-authoring-canvas'); const bounds=canvas?.getBoundingClientRect(); return {ready:(status.startsWith('Projection CPU/Rapier acquittée :')||status.startsWith('Terrain chargé côté moteur :'))&&!!bounds&&bounds.width>0&&bounds.height>0,status,canvas_width:bounds?.width??0,canvas_height:bounds?.height??0}; })()
'@
                                    Start-Sleep -Seconds 4
                                    $capture = Save-WorldScreenshot -Context $Context -Name 'full-author-route-reopened-projection-after-play'
                                    $beforeFraming = Get-WorldState -Socket $socket
                                    [void](Invoke-UiClick -Socket $socket -Selector '[data-authoring-tab-button]' -Text 'Objets')
                                    [void](Open-DetailsBySummary -Socket $socket -Summary 'Bâtiments paramétriques')
                                    $buildingOptions = @((Get-TransversalState -Socket $socket).building_options)
                                    if ($buildingOptions.Count -ne 1) { throw 'No unique accepted building to frame after Play/Stop.' }
                                    [void](Select-UiOptionByLabel -Socket $socket -Label 'Bâtiment à inspecter' -Value ([string]$buildingOptions[0].value))
                                    [void](Invoke-UiClick -Socket $socket -Text 'Cadrer ce bâtiment')
                                    Start-Sleep -Seconds 2
                                    $buildingCapture = Save-WorldScreenshot -Context $Context -Name 'full-author-route-building-after-play'
                                    [void](Invoke-UiClick -Socket $socket -Selector '[data-authoring-tab-button]' -Text 'Tracés & eau')
                                    [void](Open-DetailsBySummary -Socket $socket -Summary 'Chemins — points et profil')
                                    $pathOptions = @((Get-TransversalState -Socket $socket).path_options)
                                    if ($pathOptions.Count -ne 1) { throw 'No unique accepted path to frame after Play/Stop.' }
                                    [void](Select-UiOptionByLabel -Socket $socket -Label 'Chemin à éditer' -Value ([string]$pathOptions[0].value))
                                    [void](Invoke-UiClick -Socket $socket -Text 'Cadrer le chemin accepté')
                                    Start-Sleep -Seconds 2
                                    $pathCapture = Save-WorldScreenshot -Context $Context -Name 'full-author-route-path-after-play'
                                    $afterFraming = Get-WorldState -Socket $socket
                                    Assert-NoMutationIpcDelta -Before $beforeFraming -After $afterFraming -Description 'FullAuthorRoute building/path camera framing'
                                    $sourceAfter = Get-ArchiveSnapshot -LiteralPath $Context.SourceArchiveBefore.Path
                                    if (-not (Test-ArchiveSnapshotsEqual -Before $Context.SourceArchiveBefore -After $sourceAfter)) { throw 'Play/Stop changed source archive.' }
                                    return [pscustomobject]@{ started=$playing.playtest_status; observation=$observation; returned=$returned.last_playtest_status; projection_status=$projected.status; overview_screenshot=$capture; building_screenshot=$buildingCapture; path_screenshot=$pathCapture; camera_navigation_read_only=$true; source_unchanged=$true }
                                }) }
                            }
                        }
                    }
                }
            }
        }
    }
    $timedRoute = @($criteria['R2-043'].Cases | Where-Object { $_.passed -and $null -ne $_.elapsed_ms })
    if ($timedRoute.Count -eq 9) {
        Add-CriterionCase -Criterion $criteria['R2-052'] -Name 'single_automation_route_stage_timings' -Passed $true -Evidence @{
            operator='automation'; human_effort=$null; stages=@($timedRoute | ForEach-Object { [ordered]@{ name=$_.name; elapsed_ms=$_.elapsed_ms } })
            stage_total_ms=($timedRoute | Measure-Object -Property elapsed_ms -Sum).Sum
            started_at_utc=$Context.StartedAtUtc.ToString('o'); processor_count=[Environment]::ProcessorCount
            os=[Runtime.InteropServices.RuntimeInformation]::OSDescription
        }
    }
    [void](Invoke-CriterionCase -Criterion $criteria['R2-037'] -Name 'workspace_history_real_gui_multipage' -Action {
        [void](Invoke-UiClick -Socket $socket -Selector '[data-authoring-tab-button]' -Text 'Projet')
        $sourceRoot = Split-Path -Parent (Split-Path -Parent $Context.SourceArchiveBefore.Path)
        $sourceWorkspacePath = Join-Path $sourceRoot 'workspace-v1\authoring.json'
        $isolatedWorkspacePath = Join-Path $Context.FixtureRoot 'workspace-v1\authoring.json'
        $sourceWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $sourceWorkspacePath
        $isolatedWorkspaceBefore = Get-ArchiveSnapshot -LiteralPath $isolatedWorkspacePath
        [void](Open-DetailsBySummary -Socket $socket -Summary 'Projets et modules')
        [void](Invoke-UiClick -Socket $socket -Text 'Ouvrir le workspace')
        [void](Wait-CdpReady -Socket $socket -Description 'Workspace project form after native Open' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Expression @'
(() => { const n=v=>(v??'').replace(/\s+/g,' ').trim(); const label=[...document.querySelectorAll('label')].find(x=>n(x.textContent).startsWith('Projet du nouveau module')); const options=[...(label?.querySelector('select')?.options??[])].slice(1); return {ready:options.length===1&&options[0].value.length>0,reason:'project options='+options.length}; })()
'@)
        [void](Open-DetailsBySummary -Socket $socket -Summary 'Historique du workspace')
        [void](Invoke-UiClick -Socket $socket -Text 'Lire les dernières opérations')
        $initial = Wait-CdpReady -Socket $socket -Description 'Workspace initial history page' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Expression @'
(() => { const n=v=>(v??'').replace(/\s+/g,' ').trim(); const summary=[...document.querySelectorAll('summary')].find(x=>n(x.textContent)==='Historique du workspace'); const page=[...(summary?.closest('details')?.querySelectorAll('p')??[])].map(x=>n(x.textContent)).find(x=>/^Opérations \d+–\d+ sur \d+$/.test(x))??''; const total=Number(page.match(/sur (\d+)$/)?.[1]??0); return {ready:total>0&&total<21,total,page,reason:'initial page='+page}; })()
'@
        $projectOptions = @((Get-WorkspaceHistoryUi -Socket $socket).project_options)
        if ($projectOptions.Count -ne 1 -or [string]$projectOptions[0].text -cne 'R2-004 Project') { throw 'Pinned workspace project identity differs from the expected source.' }
        $projectId = [string]$projectOptions[0].value
        $toCreate = 21 - [int]$initial.total
        if ($toCreate -lt 1 -or $toCreate -gt 18) { throw "Workspace fixture needs an unsafe number of new module actions: $toCreate." }
        for ($index=1; $index -le $toCreate; $index++) {
            $key = 'gate2-history-' + $index.ToString('D2')
            $name = 'Gate2 History ' + $index.ToString('D2')
            [void](Set-UiControlByLabel -Socket $socket -Label 'Projet du nouveau module' -Value $projectId -DetailsSummary 'Projets et modules')
            [void](Set-UiControlByLabel -Socket $socket -Label 'Clé stable du module' -Value $key -DetailsSummary 'Projets et modules')
            [void](Set-UiControlByLabel -Socket $socket -Label 'Nom du module' -Value $name -DetailsSummary 'Projets et modules')
            [void](Invoke-UiClick -Socket $socket -Text 'Examiner le module')
            [void](Invoke-UiClick -Socket $socket -Text 'Appliquer la proposition examinée')
            $nameLiteral=ConvertTo-CompactJson -Value $name
            $readyExpression="(() => { const n=v=>(v??'').replace(/\s+/g,' ').trim(); const root=[...document.querySelectorAll('summary')].find(x=>n(x.textContent)==='Projets et modules')?.closest('details'); const text=n(root?.querySelector(':scope > ul')?.textContent); return {ready:text.includes($nameLiteral),reason:'module listed='+text.includes($nameLiteral)}; })()"
            [void](Wait-CdpReady -Socket $socket -Description "Created workspace module $name" -TimeoutSeconds $Context.InteractionTimeoutSeconds -Expression $readyExpression)
        }
        [void](Invoke-UiClick -Socket $socket -Text 'Sauvegarder le workspace')
        $deadline=[DateTimeOffset]::UtcNow.AddSeconds($Context.InteractionTimeoutSeconds)
        $isolatedWorkspaceAfter=$null
        while([DateTimeOffset]::UtcNow -lt $deadline){$isolatedWorkspaceAfter=Get-ArchiveSnapshot -LiteralPath $isolatedWorkspacePath;if($isolatedWorkspaceAfter.Sha256 -cne $isolatedWorkspaceBefore.Sha256){break};Start-Sleep -Milliseconds 200}
        if($null -eq $isolatedWorkspaceAfter -or $isolatedWorkspaceAfter.Sha256 -ceq $isolatedWorkspaceBefore.Sha256){throw 'Workspace Save did not persist the module actions in the isolated copy.'}
        [void](Invoke-UiClick -Socket $socket -Text 'Lire les dernières opérations')
        $page0=Wait-CdpReady -Socket $socket -Description 'Workspace history first full page' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Expression @'
(() => { const n=v=>(v??'').replace(/\s+/g,' ').trim(); const summary=[...document.querySelectorAll('summary')].find(x=>n(x.textContent)==='Historique du workspace'); const root=summary?.closest('details'); const page=[...(root?.querySelectorAll('p')??[])].map(x=>n(x.textContent)).find(x=>/^Opérations \d+–\d+ sur \d+$/.test(x))??''; const ids=[...(root?.querySelectorAll('ul > li')??[])].map(x=>x.title); return {ready:page==='Opérations 1–20 sur 21'&&ids.length===20,page,ids,reason:'page='+page+' count='+ids.length}; })()
'@
        [void](Invoke-CdpExpression -Socket $socket -Expression @'
(async()=>{const n=v=>(v??'').replace(/\s+/g,' ').trim();const s=[...document.querySelectorAll('summary')].find(x=>n(x.textContent)==='Historique du workspace');s?.scrollIntoView({block:'start'});await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));return {ready:!!s};})()
'@)
        $firstCapture=Save-WorldScreenshot -Context $Context -Name 'r2-037-workspace-history-page-0'
        [void](Assert-EditorNavigation -Socket $socket)
        [void](Invoke-UiClick -Socket $socket -Text 'Plus anciennes' -ScopeSelector 'details.scene-navigation')
        $older=Wait-CdpReady -Socket $socket -Description 'Workspace history older page' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Expression @'
(() => { const n=v=>(v??'').replace(/\s+/g,' ').trim(); const summary=[...document.querySelectorAll('summary')].find(x=>n(x.textContent)==='Historique du workspace'); const root=summary?.closest('details'); const page=[...(root?.querySelectorAll('p')??[])].map(x=>n(x.textContent)).find(x=>/^Opérations \d+–\d+ sur \d+$/.test(x))??''; const ids=[...(root?.querySelectorAll('ul > li')??[])].map(x=>x.title); return {ready:page==='Opérations 21–21 sur 21'&&ids.length===1,page,ids,reason:'page='+page+' count='+ids.length}; })()
'@
        [void](Invoke-CdpExpression -Socket $socket -Expression @'
(async()=>{const n=v=>(v??'').replace(/\s+/g,' ').trim();const s=[...document.querySelectorAll('summary')].find(x=>n(x.textContent)==='Historique du workspace');s?.scrollIntoView({block:'start'});await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));return {ready:!!s};})()
'@)
        $olderCapture=Save-WorldScreenshot -Context $Context -Name 'r2-037-workspace-history-page-20'
        [void](Assert-EditorNavigation -Socket $socket)
        if(@($page0.ids|Where-Object {$older.ids -contains $_}).Count -ne 0){throw 'Workspace GUI history pages overlap.'}
        [void](Invoke-UiClick -Socket $socket -Text 'Plus récentes' -ScopeSelector 'details.scene-navigation')
        $back=Wait-CdpReady -Socket $socket -Description 'Workspace first page restored' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Expression @'
(() => { const n=v=>(v??'').replace(/\s+/g,' ').trim(); const summary=[...document.querySelectorAll('summary')].find(x=>n(x.textContent)==='Historique du workspace'); const root=summary?.closest('details'); const page=[...(root?.querySelectorAll('p')??[])].map(x=>n(x.textContent)).find(x=>/^Opérations \d+–\d+ sur \d+$/.test(x))??''; const ids=[...(root?.querySelectorAll('ul > li')??[])].map(x=>x.title); return {ready:page==='Opérations 1–20 sur 21'&&ids.length===20,page,ids,reason:'page='+page}; })()
'@
        if(($back.ids -join '|') -cne ($page0.ids -join '|')){throw 'Workspace first page identities changed after navigation.'}
        $sourceWorkspaceAfter=Get-ArchiveSnapshot -LiteralPath $sourceWorkspacePath
        if(-not (Test-ArchiveSnapshotsEqual -Before $sourceWorkspaceBefore -After $sourceWorkspaceAfter)){throw 'Pinned source workspace archive changed during GUI history proof.'}
        return [pscustomobject]@{initial_total=$initial.total;created_modules=$toCreate;total=21;page_size=20;offsets=@(0,20,0);page_ids_disjoint=$true;source_workspace_hash_bytes_mtime_exact=$true;isolated_workspace_saved_sha256=$isolatedWorkspaceAfter.Sha256;first_screenshot=$firstCapture;older_screenshot=$olderCapture}
    })
    Add-CriterionCase -Criterion $criteria['R2-052'] -Name 'human_effort_and_five_clone_statistics_unmeasured' -Passed $false -Failure 'The nine timed stages are automation timings, not human author effort; five independent clone statistics are not emitted by this run.' -Evidence @{ automation_operator='automation'; human_effort=$null }
    [void](Invoke-UiClick -Socket $socket -Selector '[data-authoring-tab-button]' -Text 'Terrain')
    foreach ($criterion in $criteria.Values) { Complete-CriterionResult -Criterion $criterion }
    return @($criteria.Values | Sort-Object Criterion)
}

function Invoke-ProjectionIncrementalScenario {
    param([Parameter(Mandatory)][object]$Context)
    $criterion = New-CriterionResult -Criterion 'R2-050'
    $readDiagnostic = {
        $diagnostic = Invoke-CdpExpression -Socket $Context.Socket -Expression @'
(() => {
 const node=document.getElementById('forge-r2-050-projection-diagnostic');
 if(!node) return {ready:false,reason:'forge-r2-050-projection-diagnostic absent'};
 try { return {ready:true,value:JSON.parse(node.textContent)}; } catch(error) { return {ready:false,reason:'invalid diagnostic JSON: '+error}; }
})()
'@
        if (-not $diagnostic.ready) { throw [string]$diagnostic.reason }
        return $diagnostic.value
    }
    [void](Invoke-CriterionCase -Criterion $criterion -Name 'placement_incremental_projection_identity' -Action {
        [void](Wait-ProjectionsReady -Socket $Context.Socket -TimeoutSeconds $Context.InteractionTimeoutSeconds -ExpectedPlacementCount 72)
        [void](Wait-CdpReady -Socket $Context.Socket -Description 'R2-050 baseline Rapier handles observed' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Expression @'
(() => { const n=document.getElementById('forge-r2-050-projection-diagnostic'); try { const p=JSON.parse(n?.textContent??'').placements; const objects=p?.objects??[]; const withCollider=objects.filter(object=>object.parts?.some(part=>part.rapier_collider_handle)).length; return {ready:p?.state==='ready'&&p?.expected_epoch===p?.observed_epoch&&objects.length===72&&withCollider===72,reason:'state='+p?.state+' objects='+objects.length+' withCollider='+withCollider}; } catch(e) { return {ready:false,reason:String(e)}; } })()
'@)
        $before = & $readDiagnostic
        foreach($required in @('schema','read_only','persisted','used_for_projection_or_playtest','gpu_presentation','placements','buildings')) { if($null -eq $before.$required) { throw "R2-050 diagnostic omits '$required'." } }
        if ([string]$before.placements.state -cne 'ready' -or [int64]$before.placements.expected_epoch -ne [int64]$before.placements.observed_epoch) { throw 'Initial placement projection diagnostic is not ready at its expected epoch.' }
        $beforePlacements=@($before.placements.objects)
        if($beforePlacements.Count -ne 72){ throw "R2-050 requires 72 populated placements, got $($beforePlacements.Count)." }
        [void](Open-DetailsBySummary -Socket $Context.Socket -Summary "Bibliothèque d’assets")
        [void](Select-UiOptionByLabel -Socket $Context.Socket -Label 'Objet placé à inspecter' -Value $Context.PlacementId)
        [void](Invoke-UiClick -Socket $Context.Socket -Text 'Afficher le gizmo de translation X/Y/Z')
        $gesture=Invoke-GizmoGesture -Context $Context -Mode translation -Axis x -LeavePreview
        [void](Invoke-ApplyCurrentPreview -Socket $Context.Socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $afterExpression = "(() => { const n=document.getElementById('forge-r2-050-projection-diagnostic'); try { const v=JSON.parse(n?.textContent??''); const p=v?.placements; const objects=p?.objects??[]; const withCollider=objects.filter(object=>object.parts?.some(part=>part.rapier_collider_handle)).length; return {ready:p?.state==='ready'&&p?.expected_epoch===p?.observed_epoch&&objects.length===72&&withCollider===72, value:v, reason:'state='+p?.state+' expected='+p?.expected_epoch+' observed='+p?.observed_epoch+' colliders='+withCollider}; } catch(e) { return {ready:false,reason:String(e)}; } })()"
        $after=Wait-CdpReady -Socket $Context.Socket -Description 'R2-050 ready diagnostic after placement Apply' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Expression $afterExpression
        $afterValue=$after.value; $afterPlacements=@($afterValue.placements.objects)
        if($afterPlacements.Count -ne 72){throw "Placement edit changed placement cardinality to $($afterPlacements.Count)."}
        $target=$Context.PlacementId
        $identity = { param($item) if([string]::IsNullOrWhiteSpace([string]$item.object_id)){throw 'Placement diagnostic omits object_id.'}; return (ConvertTo-CompactJson -Value $item) }
        $beforeMap=@{}; foreach($item in $beforePlacements){$beforeMap[[string]$item.object_id]=&$identity $item}
        $afterMap=@{}; foreach($item in $afterPlacements){$afterMap[[string]$item.object_id]=&$identity $item}
        if(-not $beforeMap.ContainsKey($target) -or -not $afterMap.ContainsKey($target)){throw "Target placement $target is absent from one projection snapshot."}
        $unchanged=@($beforeMap.Keys|Where-Object {$_ -cne $target})
        $changedUntouched = @($unchanged | Where-Object { $beforeMap[$_] -cne $afterMap[$_] })
        if($unchanged.Count -ne 71 -or $changedUntouched.Count -ne 0){
            $first = if($changedUntouched.Count -gt 0){[string]$changedUntouched[0]}else{''}
            throw "Unchanged placement ECS/mesh/material/Rapier identities were not retained exactly: untouched=$($unchanged.Count), changed=$($changedUntouched.Count), first=$first, before=$($beforeMap[$first]), after=$($afterMap[$first]), mutation=$(ConvertTo-CompactJson -Value $afterValue.placements.mutation)."
        }
        return [pscustomobject]@{target=$target; untouched_count=71; untouched_identities_retained=$true; before_epoch=$before.placements.expected_epoch; after_epoch=$afterValue.placements.expected_epoch; mutation=$gesture}
    })
    [void](Invoke-CriterionCase -Criterion $criterion -Name 'building_pose_retains_eight_part_identities' -Action {
        $readyExpression = "(() => { const n=document.getElementById('forge-r2-050-projection-diagnostic'); try { const b=JSON.parse(n?.textContent??'').buildings; const parts=b?.objects?.[0]?.parts??[]; return {ready:b?.state==='ready'&&b?.objects?.length===1&&parts.length===8&&parts.every(part=>part.rapier_collider_handle), reason:'state='+b?.state+' parts='+parts.length}; } catch(e) { return {ready:false,reason:String(e)}; } })()"
        [void](Wait-CdpReady -Socket $Context.Socket -Description 'R2-050 building baseline handles observed' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Expression $readyExpression)
        $before = & $readDiagnostic
        $beforeBuilding = @($before.buildings.objects)[0]
        if ([string]$beforeBuilding.object_id -cne $Context.BuildingId) { throw 'Building diagnostic does not identify the exact fixture building.' }
        [void](Open-DetailsBySummary -Socket $Context.Socket -Summary 'Bâtiments paramétriques')
        [void](Select-UiOptionByLabel -Socket $Context.Socket -Label 'Bâtiment à inspecter' -Value $Context.BuildingId)
        $beforeUi=Get-WorldState -Socket $Context.Socket
        [void](Set-UiControlByLabel -Socket $Context.Socket -Label 'Position X (m)' -Value '21' -DetailsSummary 'Bâtiments paramétriques')
        [void](Invoke-UiClick -Socket $Context.Socket -Text 'Prévisualiser position et orientation')
        $preview=Wait-NewPreview -Socket $Context.Socket -PreviousCount ([int]$beforeUi.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds
        Assert-PreviewWithoutMutationIpcDelta -Before $beforeUi -After $preview -Description 'R2-050 building pose preview'
        [void](Invoke-ApplyCurrentPreview -Socket $Context.Socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $previousHash=[string]$before.buildings.observed_source_hash
        $changedExpression="(() => { const n=document.getElementById('forge-r2-050-projection-diagnostic'); try { const b=JSON.parse(n?.textContent??'').buildings; const parts=b?.objects?.[0]?.parts??[]; return {ready:b?.state==='ready'&&b?.expected_source_hash===b?.observed_source_hash&&b?.observed_source_hash!=='$previousHash'&&b?.objects?.length===1&&parts.length===8&&parts.every(part=>part.rapier_collider_handle), reason:'state='+b?.state+' hash='+b?.observed_source_hash+' parts='+parts.length}; } catch(e) { return {ready:false,reason:String(e)}; } })()"
        [void](Wait-CdpReady -Socket $Context.Socket -Description 'R2-050 building pose projected' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Expression $changedExpression)
        $after = & $readDiagnostic
        $afterBuilding = @($after.buildings.objects)[0]
        $beforeParts = @($beforeBuilding.parts | Sort-Object part_id | ForEach-Object { ConvertTo-CompactJson -Value $_ })
        $afterParts = @($afterBuilding.parts | Sort-Object part_id | ForEach-Object { ConvertTo-CompactJson -Value $_ })
        if (($beforeParts -join '|') -cne ($afterParts -join '|')) { throw 'Building pose recreated at least one ECS, mesh, material or Rapier part identity.' }
        $mutation=$after.buildings.mutation
        if (@($mutation.retained).Count -ne 8 -or @($mutation.spawned).Count -ne 0 -or @($mutation.rebuilt).Count -ne 0 -or @($mutation.removed).Count -ne 0) { throw "Building pose mutation was not exact retained=8: $(ConvertTo-CompactJson -Value $mutation)" }
        return [pscustomobject]@{building_id=$Context.BuildingId; retained_part_count=8; part_identities_retained=$true; before_source_hash=$before.buildings.observed_source_hash; after_source_hash=$after.buildings.observed_source_hash; mutation=$mutation}
    })
    [void](Invoke-CriterionCase -Criterion $criterion -Name 'building_door_edit_reuses_stable_part_identities' -Action {
        $before = & $readDiagnostic
        if ([string]$before.buildings.state -cne 'ready' -or @($before.buildings.objects).Count -ne 1) { throw 'Building part-change baseline is not ready.' }
        $beforeParts=@{}; foreach($part in @($before.buildings.objects[0].parts)){ $beforeParts[[string]$part.part_id]=ConvertTo-CompactJson -Value $part }
        if($beforeParts.Count -ne 8){ throw "Building part-change baseline has $($beforeParts.Count) parts, not eight." }
        $beforeUi=Get-WorldState -Socket $Context.Socket
        [void](Set-UiControlByLabel -Socket $Context.Socket -Label 'Retouche porte : X local absolu (m)' -Value '1.0' -DetailsSummary 'Bâtiments paramétriques')
        [void](Invoke-UiClick -Socket $Context.Socket -Text 'Prévisualiser la retouche de porte')
        $preview=Wait-NewPreview -Socket $Context.Socket -PreviousCount ([int]$beforeUi.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds
        Assert-PreviewWithoutMutationIpcDelta -Before $beforeUi -After $preview -Description 'R2-050 building door preview'
        [void](Invoke-ApplyCurrentPreview -Socket $Context.Socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $previousHash=[string]$before.buildings.observed_source_hash
        $changedExpression="(() => { const n=document.getElementById('forge-r2-050-projection-diagnostic'); try { const b=JSON.parse(n?.textContent??'').buildings; const parts=b?.objects?.[0]?.parts??[]; return {ready:b?.state==='ready'&&b?.expected_source_hash===b?.observed_source_hash&&b?.observed_source_hash!=='$previousHash'&&b?.objects?.length===1&&parts.length===8&&parts.every(part=>part.rapier_collider_handle),reason:'state='+b?.state+' hash='+b?.observed_source_hash+' parts='+parts.length}; } catch(e) { return {ready:false,reason:String(e)}; } })()"
        [void](Wait-CdpReady -Socket $Context.Socket -Description 'R2-050 changed building parts ready' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Expression $changedExpression)
        $after=& $readDiagnostic
        $afterParts=@{}; foreach($part in @($after.buildings.objects[0].parts)){ $afterParts[[string]$part.part_id]=ConvertTo-CompactJson -Value $part }
        if($afterParts.Count -ne 8 -or @($beforeParts.Keys|Where-Object {$afterParts.ContainsKey($_)}).Count -ne 8){throw 'Building door edit changed stable part IDs.'}
        $retained=@($beforeParts.Keys|Where-Object {$beforeParts[$_] -ceq $afterParts[$_]})
        $changedIdentity=@($beforeParts.Keys|Where-Object {$beforeParts[$_] -cne $afterParts[$_]})
        if($retained.Count -ne 8 -or $changedIdentity.Count -ne 0){throw "Building door edit churned part identities: retained=$($retained.Count), changed=$($changedIdentity.Count)."}
        $mutation=$after.buildings.mutation
        if(@($mutation.retained).Count -ne 8 -or @($mutation.rebuilt).Count -ne 0 -or @($mutation.spawned).Count -ne 0 -or @($mutation.removed).Count -ne 0){throw "Building door mutation ledger differs from observed identities: $(ConvertTo-CompactJson -Value $mutation)"}
        return [pscustomobject]@{building_id=$Context.BuildingId; retained_part_ids=@($retained|Sort-Object); mutation=$mutation; before_source_hash=$before.buildings.observed_source_hash; after_source_hash=$after.buildings.observed_source_hash; geometry_change_observed=$false; geometry_change_note='This diagnostic records handles and identities, not per-part transforms; engine unit tests cover geometry updates.'}
    })
    [void](Invoke-CriterionCase -Criterion $criterion -Name 'playtest_after_incremental_reconciliation' -Action {
        [void](Wait-ProjectionsReady -Socket $Context.Socket -TimeoutSeconds $Context.InteractionTimeoutSeconds -ExpectedPlacementCount 72)
        $before = & $readDiagnostic
        if ([string]$before.placements.state -cne 'ready' -or [string]$before.buildings.state -cne 'ready' -or [string]$before.buildings.expected_source_hash -cne [string]$before.buildings.observed_source_hash) { throw 'Incremental projections are not concordant before Play.' }
        [void](Invoke-UiClick -Socket $Context.Socket -Text 'Jouer')
        [void](Install-WorldAuditAfterOwnedReload -Socket $Context.Socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $playing=Wait-WorldState -Socket $Context.Socket -Description 'R2-050 playtest after incremental reconciliation' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate { param($state) [string]$state.playtest_status -like 'Simulation active*' -and [int]$state.audit_pending -eq 0 }
        $observation=Wait-PlaytestObservation -Socket $Context.Socket -TimeoutSeconds $Context.InteractionTimeoutSeconds
        [void](Invoke-UiClick -Socket $Context.Socket -Text "Retour à l’édition")
        [void](Install-WorldAuditAfterOwnedReload -Socket $Context.Socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $returned=Wait-WorldState -Socket $Context.Socket -Description 'R2-050 return after incremental reconciliation' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate { param($state) [string]$state.mode -like 'Mode édition*' -and [string]$state.last_playtest_status -like 'Dernier playtest ·*' }
        $sourceAfter=Get-ArchiveSnapshot -LiteralPath $Context.SourceArchiveBefore.Path
        if(-not (Test-ArchiveSnapshotsEqual -Before $Context.SourceArchiveBefore -After $sourceAfter)){throw 'Play/Stop after incremental reconciliation changed the pinned source.'}
        return [pscustomobject]@{before_placement_epoch=$before.placements.observed_epoch; before_building_source_hash=$before.buildings.observed_source_hash; started=$playing.playtest_status; observation=$observation; returned=$returned.last_playtest_status; source_unchanged=$true; stale_refusal_exercised=$false}
    })
    Complete-CriterionResult -Criterion $criterion
    return @($criterion)
}

function Invoke-ReadOnlyAuditProbe {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $tabbed = Invoke-CdpExpression -Socket $Socket -Expression '(() => ({tabbed:!!document.querySelector("[data-authoring-tab-button]")}))()'
    if ($tabbed.tabbed) {
        [void](Invoke-UiClick -Socket $Socket -Selector '[data-authoring-tab-button]' -Text 'Projet')
        [void](Open-DetailsBySummary -Socket $Socket -Summary 'Créer ou modifier la zone')
    }
    [void](Invoke-UiClick -Socket $Socket -Text 'Relire la session native')
    $state = Wait-WorldState -Socket $Socket -Description 'Read-only IPC audit probe' -TimeoutSeconds $TimeoutSeconds -Predicate {
        param($value)
        [int]$value.audit_invalid -eq 0 -and
            [int]$value.audit_counts.inspect -eq 1 -and
            [int]$value.audit_pending -eq 0 -and
            -not [bool]$value.placement_disabled -and
            -not [bool]$value.building_disabled
    }
    if ((Get-MutationCountTotal -State $state) -ne 0) {
        throw 'Read-only IPC probe emitted a mutation command.'
    }
    if ($tabbed.tabbed) {
        [void](Invoke-UiClick -Socket $Socket -Selector '[data-authoring-tab-button]' -Text 'Terrain')
    }
    return $state
}

function Test-PlacementProjectionStatusReady {
    param([AllowEmptyString()][string]$Status)
    return $Status -match '^\d+ placements · \d+ primitives synchronisées dans Rapier · 0 en attente · Jouer vérifie aussi la source acceptée\.$'
}

function Wait-ProjectionsReady {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [int]$ExpectedPlacementCount = -1
    )
    return Wait-WorldState -Socket $Socket -Description 'Placement and building projections' -TimeoutSeconds $TimeoutSeconds -Predicate {
        param($state)
        (Test-PlacementProjectionStatusReady -Status ([string]$state.placement_status)) -and
            ($ExpectedPlacementCount -lt 0 -or [string]$state.placement_status -match "^$ExpectedPlacementCount placements ") -and
            [string]$state.building_status -match '^Bâtiments acceptés : 8 volumes préparés' -and
            [int]$state.audit_pending -eq 0 -and
            -not [bool]$state.placement_disabled -and
            -not [bool]$state.building_disabled
    }
}

function Wait-CleanPreviewState {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [int]$TimeoutSeconds = 15
    )
    return Wait-WorldState -Socket $Socket -Description 'Clean authoring preview state' -TimeoutSeconds $TimeoutSeconds -Predicate {
        param($state)
        $state.diff_hidden -and $state.apply_disabled
    }
}

function Wait-NewPreview {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][int]$PreviousCount,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [string]$DiffNeedle
    )
    return Wait-WorldState -Socket $Socket -Description 'One new PreviewZone proposal' -TimeoutSeconds $TimeoutSeconds -Predicate {
        param($state)
        $matches = [string]::IsNullOrWhiteSpace($DiffNeedle) -or [string]$state.diff_text -like "*$DiffNeedle*"
        [int]$state.audit_counts.preview_zone -eq ($PreviousCount + 1) -and
            [int]$state.audit_pending -eq 0 -and
            -not $state.diff_hidden -and -not $state.apply_disabled -and $matches
    }
}

function Invoke-ApplyCurrentPreview {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $before = Get-WorldState -Socket $Socket
    if ($before.diff_hidden -or $before.apply_disabled) {
        throw 'No reviewed preview is available to Apply.'
    }
    [void](Invoke-UiClick -Socket $Socket -Text 'Appliquer la retouche prévisualisée')
    return Wait-WorldState -Socket $Socket -Description 'Exact ApplyZone command and cleared diff' -TimeoutSeconds $TimeoutSeconds -Predicate {
        param($state)
        [int]$state.audit_counts.apply_zone -eq ([int]$before.audit_counts.apply_zone + 1) -and
            [int]$state.audit_pending -eq 0 -and
            $state.diff_hidden -and $state.apply_disabled -and
            -not [bool]$state.placement_disabled -and
            -not [bool]$state.building_disabled
    }
}

function Wait-GizmoDiagnostic {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$PlacementId,
        [Parameter(Mandatory)][string]$Mode,
        [string]$RotationSpace,
        [int]$TimeoutSeconds = 15,
        [string]$Phase,
        [string]$Axis
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $last = $null
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $state = Get-WorldState -Socket $Socket
        $last = $state.diagnostic_raw
        if (-not [string]::IsNullOrWhiteSpace([string]$last)) {
            try {
                $diagnostic = ConvertFrom-GizmoDiagnostic -Json ([string]$last) -ExpectedPlacementId $PlacementId -ExpectedMode $Mode -ExpectedRotationSpace $RotationSpace
                $phaseMatches = [string]::IsNullOrWhiteSpace($Phase) -or [string]$diagnostic.phase -ceq $Phase
                $axisMatches = [string]::IsNullOrWhiteSpace($Axis) -or [string]$diagnostic.active_axis -ceq $Axis
                if ([string]$diagnostic.state -ceq 'active' -and $phaseMatches -and $axisMatches) {
                    return $diagnostic
                }
            }
            catch {
                # The diagnostic changes asynchronously; preserve fail-closed timeout below.
            }
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Expected active gizmo diagnostic did not appear. Last raw value: $last"
}

function Convert-NormalizedPointToCanvas {
    param(
        [Parameter(Mandatory)][object]$Canvas,
        [Parameter(Mandatory)][object]$Point
    )
    Assert-NormalizedPoint -Point $Point -Description 'Normalized canvas point'
    return [pscustomobject]@{
        x = [double]$Canvas.left + [double]$Canvas.width * [double]$Point.x
        y = [double]$Canvas.top + [double]$Canvas.height * [double]$Point.y
    }
}

function Get-AxisGesturePoints {
    param(
        [Parameter(Mandatory)][object]$Diagnostic,
        [Parameter(Mandatory)][object]$Canvas,
        [Parameter(Mandatory)][ValidateSet('x', 'y', 'z')][string]$Axis
    )
    $tip = Get-GizmoAxisPoint -Diagnostic $Diagnostic -Axis $Axis
    $start = Convert-NormalizedPointToCanvas -Canvas $Canvas -Point $tip
    $all = @{}
    foreach ($entry in @($Diagnostic.axis_handle_screen_normalized)) {
        if ($null -ne $entry.screen_normalized) {
            $all[[string]$entry.axis] = $entry.screen_normalized
        }
    }
    $others = @($all.Keys | Where-Object { $_ -cne $Axis } | ForEach-Object { $all[$_] })
    $centerX = 0.5
    $centerY = 0.5
    if ($others.Count -ge 2) {
        $centerX = ([double]$others[0].x + [double]$others[1].x) / 2.0
        $centerY = ([double]$others[0].y + [double]$others[1].y) / 2.0
    }
    $dx = [double]$tip.x - $centerX
    $dy = [double]$tip.y - $centerY
    $length = [Math]::Sqrt($dx * $dx + $dy * $dy)
    if ($length -lt 0.01) {
        $dx = 0.08
        $dy = 0.03
        $length = [Math]::Sqrt($dx * $dx + $dy * $dy)
    }
    $endNormalized = [pscustomobject]@{
        x = [Math]::Max(0.02, [Math]::Min(0.98, [double]$tip.x + ($dx / $length) * 0.08))
        y = [Math]::Max(0.02, [Math]::Min(0.98, [double]$tip.y + ($dy / $length) * 0.08))
    }
    $end = Convert-NormalizedPointToCanvas -Canvas $Canvas -Point $endNormalized
    return [pscustomobject]@{ Start = $start; End = $end; StartNormalized = $tip; EndNormalized = $endNormalized }
}

function Invoke-GizmoGesture {
    param(
        [Parameter(Mandatory)][object]$Context,
        [Parameter(Mandatory)][ValidateSet('translation', 'local_translation', 'rotation', 'scale')][string]$Mode,
        [Parameter(Mandatory)][ValidateSet('x', 'y', 'z')][string]$Axis,
        [ValidateSet('release', 'escape', 'mouseleave', 'blur')][string]$Finish = 'release',
        [switch]$LeavePreview,
        [string]$RotationSpace
    )
    $socket = $Context.Socket
    $armText = switch ($Mode) {
        'translation' { 'Afficher le gizmo de translation X/Y/Z' }
        'local_translation' { 'Afficher le gizmo de translation X/Y/Z' }
        'rotation' { 'Afficher les anneaux de rotation X/Y/Z' }
        'scale' { "Afficher les poignées d’échelle locale X/Y/Z" }
    }
    $before = Get-WorldState -Socket $socket
    [void](Invoke-UiClick -Socket $socket -Text $armText)
    $diagnostic = Wait-GizmoDiagnostic -Socket $socket -PlacementId $Context.PlacementId -Mode $Mode -RotationSpace $RotationSpace
    $canvas = Get-CanvasGeometry -Socket $socket
    if ($Mode -ceq 'rotation') {
        $normalized = Get-GizmoRingGesture -Diagnostic $diagnostic -Axis $Axis
        $start = Convert-NormalizedPointToCanvas -Canvas $canvas -Point $normalized.Start
        $end = Convert-NormalizedPointToCanvas -Canvas $canvas -Point $normalized.End
        $geometry = [pscustomobject]@{ Start = $start; End = $end; StartNormalized = $normalized.Start; EndNormalized = $normalized.End }
    }
    else {
        $geometry = Get-AxisGesturePoints -Diagnostic $diagnostic -Canvas $canvas -Axis $Axis
    }
    Send-CdpMouseDrag -Socket $socket -StartX $geometry.Start.x -StartY $geometry.Start.y -EndX $geometry.End.x -EndY $geometry.End.y -Steps 5 -HoldOpen
    $transient = Wait-GizmoDiagnostic -Socket $socket -PlacementId $Context.PlacementId -Mode $Mode -RotationSpace $RotationSpace -Phase 'move' -Axis $Axis
    Assert-GizmoTransformContract -Diagnostic $transient -Mode $Mode -Axis $Axis
    $during = Get-WorldState -Socket $socket
    Assert-NoMutationIpcDelta -Before $before -After $during -Description "$Mode/$Axis transient"
    if (-not $during.diff_hidden -or -not $during.apply_disabled) {
        throw "$Mode/$Axis transient exposed a diff or enabled Apply before mouse-up."
    }
    switch ($Finish) {
        'escape' {
            Send-CdpEscape -Socket $socket
            Send-CdpMouseEvent -Socket $socket -Type mouseReleased -X $geometry.End.x -Y $geometry.End.y
            $after = Wait-CleanPreviewState -Socket $socket
            Assert-NoMutationIpcDelta -Before $before -After $after -Description "$Mode/$Axis Escape cancellation"
        }
        'mouseleave' {
            Send-CdpMouseEvent -Socket $socket -Type mouseMoved -X ($canvas.left - 20) -Y ($canvas.top - 20) -Buttons 1
            Send-CdpMouseEvent -Socket $socket -Type mouseReleased -X ($canvas.left - 20) -Y ($canvas.top - 20)
            $after = Wait-CleanPreviewState -Socket $socket
            Assert-NoMutationIpcDelta -Before $before -After $after -Description "$Mode/$Axis mouseleave cancellation"
        }
        'blur' {
            [void](Invoke-UiClick -Socket $socket -Text "Abandonner l’aperçu")
            $after = Wait-CleanPreviewState -Socket $socket
            Assert-NoMutationIpcDelta -Before $before -After $after -Description "$Mode/$Axis blur cancellation"
        }
        default {
            Send-CdpMouseEvent -Socket $socket -Type mouseReleased -X $geometry.End.x -Y $geometry.End.y
            $after = Wait-NewPreview -Socket $socket -PreviousCount ([int]$before.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds
            if ([int]$after.audit_counts.apply_zone -ne [int]$before.audit_counts.apply_zone -or [int]$after.audit_counts.save -ne [int]$before.audit_counts.save) {
                throw "$Mode/$Axis mouse-up emitted ApplyZone or Save."
            }
            if (-not $LeavePreview) {
                Send-CdpEscape -Socket $socket
                [void](Wait-CleanPreviewState -Socket $socket)
            }
        }
    }
    return [pscustomobject]@{ mode = $Mode; axis = $Axis; finish = $Finish; initial = $diagnostic; transient = $transient; final_state = $after; geometry = $geometry }
}

function Get-AcceptedPlacementTransformFromGizmo {
    param(
        [Parameter(Mandatory)][object]$Context,
        [string]$Mode = 'translation'
    )
    [void](Select-UiOptionByLabel -Socket $Context.Socket -Label 'Objet placé à inspecter' -Value $Context.PlacementId)
    $button = if ($Mode -ceq 'translation') { 'Afficher le gizmo de translation X/Y/Z' } else { throw "Unsupported accepted-transform gizmo mode '$Mode'." }
    [void](Invoke-UiClick -Socket $Context.Socket -Text $button)
    $diagnostic = Wait-GizmoDiagnostic -Socket $Context.Socket -PlacementId $Context.PlacementId -Mode $Mode
    Send-CdpEscape -Socket $Context.Socket
    [void](Wait-CleanPreviewState -Socket $Context.Socket)
    return $diagnostic.source_transform
}

function Parse-PlaytestStatus {
    param([Parameter(Mandatory)][string]$Text)
    $position = [regex]::Match($Text, 'position \(m\) (?<x>-?\d+(?:\.\d+)?), (?<y>-?\d+(?:\.\d+)?), (?<z>-?\d+(?:\.\d+)?)')
    $tick = [regex]::Match($Text, 'Tick (?<tick>\d+)')
    $contacts = [regex]::Match($Text, '(?<contacts>\d+) contact\(s\)')
    if (-not $position.Success -or -not $tick.Success) {
        throw "Playtest status cannot be parsed: $Text"
    }
    return [pscustomobject]@{
        Tick = [int64]$tick.Groups['tick'].Value
        X = [double]$position.Groups['x'].Value
        Y = [double]$position.Groups['y'].Value
        Z = [double]$position.Groups['z'].Value
        Contacts = if ($contacts.Success) { [int]$contacts.Groups['contacts'].Value } else { 0 }
        SimulationReady = $Text -match 'simulation prête true'
        Text = $Text
    }
}

function Convert-WorldToBuildingLocal {
    param(
        [Parameter(Mandatory)][object]$Point,
        [Parameter(Mandatory)][double]$OriginX,
        [Parameter(Mandatory)][double]$OriginZ,
        [Parameter(Mandatory)][double]$YawDegrees
    )
    $radians = $YawDegrees * [Math]::PI / 180.0
    $sin = [Math]::Sin($radians)
    $cos = [Math]::Cos($radians)
    $dx = [double]$Point.X - $OriginX
    $dz = [double]$Point.Z - $OriginZ
    return [pscustomobject]@{
        X = $cos * $dx - $sin * $dz
        Z = $sin * $dx + $cos * $dz
    }
}

function Wait-PlaytestObservation {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [int64]$AfterTick = -1,
        [int]$TimeoutSeconds = 20
    )
    $state = Wait-WorldState -Socket $Socket -Description 'Playtest runtime observation' -TimeoutSeconds $TimeoutSeconds -Predicate {
        param($candidate)
        if ([string]::IsNullOrWhiteSpace([string]$candidate.playtest_status)) { return $false }
        try {
            $parsed = Parse-PlaytestStatus -Text ([string]$candidate.playtest_status)
            return $parsed.SimulationReady -and $parsed.Tick -gt $AfterTick
        }
        catch { return $false }
    }
    return Parse-PlaytestStatus -Text ([string]$state.playtest_status)
}

function Invoke-UndoRedoKey {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][ValidateSet('Undo', 'Redo')][string]$Action,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $before = Get-WorldState -Socket $Socket
    $canvas = Get-CanvasGeometry -Socket $Socket
    Send-CdpMouseClick -Socket $Socket -X $canvas.center_x -Y $canvas.center_y
    if ($Action -ceq 'Undo') {
        Send-CdpKeyStroke -Socket $Socket -Key 'z' -Code 'KeyZ' -VirtualKeyCode 90 -Modifiers 2
    }
    else {
        Send-CdpKeyStroke -Socket $Socket -Key 'y' -Code 'KeyY' -VirtualKeyCode 89 -Modifiers 2
    }
    return Wait-WorldState -Socket $Socket -Description "$Action ApplyZone command" -TimeoutSeconds $TimeoutSeconds -Predicate {
        param($state)
        [int]$state.audit_counts.apply_zone -eq ([int]$before.audit_counts.apply_zone + 1) -and
            [int]$state.audit_pending -eq 0 -and $state.diff_hidden -and
            -not [bool]$state.placement_disabled -and
            -not [bool]$state.building_disabled
    }
}

function Arm-PlacementPicker {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [int]$TimeoutSeconds = 10
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastFailure = $null
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        try {
            [void](Invoke-UiClick -Socket $Socket -Text 'Sélectionner un objet dans la vue')
            $remaining = [Math]::Max(1, [Math]::Min(2, [int][Math]::Ceiling(($deadline - [DateTimeOffset]::UtcNow).TotalSeconds)))
            return Wait-WorldState -Socket $Socket -Description 'Placement picker armed' -TimeoutSeconds $remaining -Predicate {
                param($state)
                [string]$state.placement_pick_status -like 'Cliquer un objet visible*' -or
                    [string]$state.placement_status -like 'Cliquer un objet visible*'
            }
        }
        catch {
            if ($Socket.State -ne [System.Net.WebSockets.WebSocketState]::Open) { throw }
            $lastFailure = $_.Exception.Message
        }
    }
    throw "Placement picker did not arm after retrying physical UI clicks. Last failure: $lastFailure"
}

function Arm-BuildingPicker {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [int]$TimeoutSeconds = 10
    )
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastFailure = $null
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        try {
            [void](Invoke-UiClick -Socket $Socket -Text 'Sélectionner un bâtiment dans la vue')
            $remaining = [Math]::Max(1, [Math]::Min(2, [int][Math]::Ceiling(($deadline - [DateTimeOffset]::UtcNow).TotalSeconds)))
            return Wait-WorldState -Socket $Socket -Description 'Building picker armed' -TimeoutSeconds $remaining -Predicate {
                param($state) [string]$state.building_pick_status -like 'Cliquer un bâtiment visible*'
            }
        }
        catch {
            if ($Socket.State -ne [System.Net.WebSockets.WebSocketState]::Open) { throw }
            $lastFailure = $_.Exception.Message
        }
    }
    throw "Building picker did not arm after retrying physical UI clicks. Last failure: $lastFailure"
}

function Get-VisibleCanvasSurfacePoint {
    param([Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket)
    $canvas = Get-CanvasGeometry -Socket $Socket
    foreach ($candidate in @(
        [pscustomobject]@{ Name='top-left'; X=0.06; Y=0.08 },
        [pscustomobject]@{ Name='top-right'; X=0.94; Y=0.08 },
        [pscustomobject]@{ Name='bottom-left'; X=0.06; Y=0.92 },
        [pscustomobject]@{ Name='bottom-right'; X=0.94; Y=0.92 }
    )) {
        $x = $canvas.left + $canvas.width * [double]$candidate.X
        $y = $canvas.top + $canvas.height * [double]$candidate.Y
        $xLiteral = ConvertTo-CompactJson -Value $x
        $yLiteral = ConvertTo-CompactJson -Value $y
        $hit = Invoke-CdpExpression -Socket $Socket -Expression "(() => { const node=document.elementFromPoint($xLiteral,$yLiteral); return { id:node?.id ?? '', tag:node?.tagName ?? '' }; })()"
        if ([string]$hit.id -ceq 'forge-authoring-canvas') {
            return [pscustomobject]@{ name=$candidate.Name; x=$x; y=$y; hit_id=$hit.id; hit_tag=$hit.tag }
        }
    }
    throw 'No unobscured physical click point was found on forge-authoring-canvas.'
}

function Invoke-ExactFramedPlacementPick {
    param(
        [Parameter(Mandatory)][object]$Context
    )
    $socket = $Context.Socket
    $offsets = @(
        [pscustomobject]@{ Name='upper-centre'; X=0.00; Y=-0.18 },
        [pscustomobject]@{ Name='upper-left'; X=-0.08; Y=-0.10 },
        [pscustomobject]@{ Name='upper-right'; X=0.08; Y=-0.10 },
        [pscustomobject]@{ Name='lower-left'; X=-0.08; Y=0.12 },
        [pscustomobject]@{ Name='lower-right'; X=0.08; Y=0.12 },
        [pscustomobject]@{ Name='centre'; X=0.00; Y=0.00 },
        [pscustomobject]@{ Name='top'; X=0.00; Y=-0.28 },
        [pscustomobject]@{ Name='left'; X=-0.16; Y=0.00 },
        [pscustomobject]@{ Name='right'; X=0.16; Y=0.00 }
    )
    $attempts = [System.Collections.Generic.List[object]]::new()
    foreach ($offset in $offsets) {
        $selectionBeforeArm = Get-WorldState -Socket $socket
        if ([string]::IsNullOrWhiteSpace([string]$selectionBeforeArm.placement_value)) {
            $fallback = @($selectionBeforeArm.placement_options | Where-Object { [string]$_.value -cne $Context.PlacementId })
            if ($fallback.Count -eq 0) { throw 'Physical pick grid requires a distinct fallback placement to keep the inspector rendered.' }
            $fallbackId = [string]$fallback[0].value
            [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $fallbackId)
            [void](Wait-WorldState -Socket $socket -Description 'Fallback placement selected before physical pick attempt' -TimeoutSeconds 10 -Predicate {
                param($state) [string]$state.placement_value -ceq $fallbackId
            })
        }
        [void](Arm-PlacementPicker -Socket $socket -TimeoutSeconds 5)
        $canvas = Get-CanvasGeometry -Socket $socket
        $x = $canvas.center_x + $canvas.width * [double]$offset.X
        $y = $canvas.center_y + $canvas.height * [double]$offset.Y
        $xLiteral = ConvertTo-CompactJson -Value $x
        $yLiteral = ConvertTo-CompactJson -Value $y
        $hit = Invoke-CdpExpression -Socket $socket -Expression "(() => { const node=document.elementFromPoint($xLiteral,$yLiteral); return { id:node?.id ?? '', tag:node?.tagName ?? '' }; })()"
        if ([string]$hit.id -cne 'forge-authoring-canvas') {
            $attempts.Add([pscustomobject]@{ name=$offset.Name; x=$x; y=$y; selected=''; status='not_canvas'; hit_id=$hit.id; hit_tag=$hit.tag })
            continue
        }
        Send-CdpMouseClick -Socket $socket -X $x -Y $y
        try {
            $settled = Wait-WorldState -Socket $socket -Description "Placement picker result for $($offset.Name)" -TimeoutSeconds 5 -Predicate {
                param($state)
                [string]$state.placement_pick_status -like 'Objet sélectionné :*' -or
                    [string]$state.placement_status -like 'Objet sélectionné :*' -or
                    [string]$state.placement_pick_status -like 'Aucun placement sélectionnable*' -or
                    [string]$state.placement_status -like 'Aucun placement sélectionnable*' -or
                    [string]$state.placement_pick_status -like 'Sélection indisponible pendant*'
            }
        }
        catch {
            $attempts.Add([pscustomobject]@{ name=$offset.Name; x=$x; y=$y; selected=''; status='timeout'; failure=$_.Exception.Message })
            continue
        }
        $status = if (-not [string]::IsNullOrWhiteSpace([string]$settled.placement_pick_status)) { [string]$settled.placement_pick_status } else { [string]$settled.placement_status }
        $attempts.Add([pscustomobject]@{
            name = $offset.Name
            x = $x
            y = $y
            selected = [string]$settled.placement_value
            status = $status
        })
        if ([string]$settled.placement_value -ceq $Context.PlacementId -and $status -like "Objet sélectionné : $($Context.PlacementId)*") {
            return [pscustomobject]@{ selected=$settled.placement_value; status=$status; point=$attempts[$attempts.Count - 1]; attempts=@($attempts) }
        }
    }
    throw "Exact placement $($Context.PlacementId) was not picked from the framed physical click grid. Attempts: $(ConvertTo-CompactJson -Value @($attempts))"
}

function Invoke-PlacementsScenario {
    param([Parameter(Mandatory)][object]$Context)
    $socket = $Context.Socket
    $criteria = @{
        'R2-028' = New-CriterionResult -Criterion 'R2-028'
        'R2-029' = New-CriterionResult -Criterion 'R2-029'
        'R2-030' = New-CriterionResult -Criterion 'R2-030'
        'R2-031' = New-CriterionResult -Criterion 'R2-031'
    }
    [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
    [void](Open-DetailsBySummary -Socket $socket -Summary "Bibliothèque d’assets")
    [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $Context.PlacementId)
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Position précise et duplication')

    [void](Invoke-CriterionCase -Criterion $criteria['R2-028'] -Name 'positive_visible_mesh_pick' -Action {
        [void](Invoke-UiClick -Socket $socket -Text 'Cadrer cet objet dans la carte')
        Start-Sleep -Milliseconds 750
        $framed = Get-WorldState -Socket $socket
        $otherOptions = @($framed.placement_options | Where-Object { [string]$_.value -cne $Context.PlacementId })
        if ($otherOptions.Count -eq 0) { throw 'Positive picking requires a distinct preselected placement.' }
        $previousSelection = [string]$otherOptions[0].value
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $previousSelection)
        [void](Wait-WorldState -Socket $socket -Description 'Distinct placement selected before physical pick' -TimeoutSeconds 10 -Predicate {
            param($state) [string]$state.placement_value -ceq $previousSelection
        })
        $before = Get-WorldState -Socket $socket
        $pick = Invoke-ExactFramedPlacementPick -Context $Context
        $after = Get-WorldState -Socket $socket
        $Context.ConfirmedPlacementPickPoint = $pick.point
        Assert-NoMutationIpcDelta -Before $before -After $after -Description 'Positive placement picking'
        return [pscustomobject]@{ previous_selection=$previousSelection; selected=$after.placement_value; status=$pick.status; confirmed_point=$pick.point; attempts=$pick.attempts; screenshot=Save-WorldScreenshot -Context $Context -Name 'r2-028-positive-pick' }
    })

    [void](Invoke-CriterionCase -Criterion $criteria['R2-028'] -Name 'empty_click_clears_selection' -Action {
        $before = Get-WorldState -Socket $socket
        [void](Arm-PlacementPicker -Socket $socket)
        $emptyPoint = Get-VisibleCanvasSurfacePoint -Socket $socket
        Send-CdpMouseClick -Socket $socket -X $emptyPoint.x -Y $emptyPoint.y
        $after = Wait-WorldState -Socket $socket -Description 'Empty placement pick' -TimeoutSeconds 10 -Predicate {
            param($state)
            [string]::IsNullOrEmpty([string]$state.placement_value) -and (
                [string]$state.placement_pick_status -like 'Aucun placement sélectionnable*' -or
                [string]$state.placement_status -like 'Aucun placement sélectionnable*'
            )
        }
        Assert-NoMutationIpcDelta -Before $before -After $after -Description 'Empty placement picking'
        return [pscustomobject]@{ state=$after; click=$emptyPoint }
    })

    [void](Invoke-CriterionCase -Criterion $criteria['R2-028'] -Name 'nonplacement_building_occludes_pick' -Action {
        [void](Open-DetailsBySummary -Socket $socket -Summary 'Bâtiments paramétriques')
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Bâtiment à inspecter' -Value $Context.BuildingId)
        [void](Invoke-UiClick -Socket $socket -Text 'Cadrer ce bâtiment')
        Start-Sleep -Milliseconds 500
        [void](Arm-BuildingPicker -Socket $socket)
        $canvas = Get-CanvasGeometry -Socket $socket
        $occlusionPoint = [pscustomobject]@{ x=$canvas.center_x; y=$canvas.center_y }
        Send-CdpMouseClick -Socket $socket -X $occlusionPoint.x -Y $occlusionPoint.y
        $buildingHit = Wait-WorldState -Socket $socket -Description 'Exact building selected by physical raycast' -TimeoutSeconds 10 -Predicate {
            param($state)
            [string]$state.building_value -ceq $Context.BuildingId -and
                [string]$state.building_pick_status -like "Bâtiment sélectionné : $($Context.BuildingId)*"
        }
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $Context.PlacementId)
        $before = Get-WorldState -Socket $socket
        [void](Arm-PlacementPicker -Socket $socket)
        Send-CdpMouseClick -Socket $socket -X $occlusionPoint.x -Y $occlusionPoint.y
        $after = Wait-WorldState -Socket $socket -Description 'Building-occluded placement pick' -TimeoutSeconds 10 -Predicate {
            param($state)
            [string]::IsNullOrEmpty([string]$state.placement_value) -and (
                [string]$state.placement_pick_status -like 'Aucun placement sélectionnable*' -or
                [string]$state.placement_status -like 'Aucun placement sélectionnable*'
            )
        }
        Assert-NoMutationIpcDelta -Before $before -After $after -Description 'Occluded placement picking'
        return [pscustomobject]@{ framed_building=$Context.BuildingId; building_raycast_status=$buildingHit.building_pick_status; shared_point=$occlusionPoint; placement_selection=$after.placement_value; screenshot=Save-WorldScreenshot -Context $Context -Name 'r2-028-building-occlusion' }
    })

    [void](Invoke-CriterionCase -Criterion $criteria['R2-028'] -Name 'escape_cancels_armed_pick' -Action {
        if ($null -eq $Context.ConfirmedPlacementPickPoint) { throw 'No previously confirmed mesh point is available for Escape cancellation proof.' }
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $Context.PlacementId)
        [void](Invoke-UiClick -Socket $socket -Text 'Cadrer cet objet dans la carte')
        Start-Sleep -Milliseconds 400
        $state = Get-WorldState -Socket $socket
        $otherOptions = @($state.placement_options | Where-Object { [string]$_.value -cne $Context.PlacementId })
        if ($otherOptions.Count -eq 0) { throw 'Escape picking proof requires a distinct retained selection.' }
        $retainedSelection = [string]$otherOptions[0].value
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $retainedSelection)
        $before = Get-WorldState -Socket $socket
        [void](Arm-PlacementPicker -Socket $socket)
        Send-CdpEscape -Socket $socket
        Send-CdpMouseClick -Socket $socket -X $Context.ConfirmedPlacementPickPoint.x -Y $Context.ConfirmedPlacementPickPoint.y
        Start-Sleep -Milliseconds 500
        $after = Get-WorldState -Socket $socket
        if ([string]$after.placement_value -cne $retainedSelection) { throw 'Escape-cancelled picker changed the retained selection at a previously confirmed mesh point.' }
        Assert-NoMutationIpcDelta -Before $before -After $after -Description 'Escape-cancelled placement picker'
        return [pscustomobject]@{ retained_selection=$retainedSelection; confirmed_mesh_point=$Context.ConfirmedPlacementPickPoint; state=$after }
    })

    [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $Context.PlacementId)
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Position précise et duplication')
    [void](Set-UiControlByLabel -Socket $socket -Label 'Déplacement selon les axes locaux' -Value $false -Kind checked)
    [void](Set-UiControlByLabel -Socket $socket -Label 'Anneaux dans le repère local' -Value $false -Kind checked)
    [void](Set-UiControlByLabel -Socket $socket -Label 'Aligner X/Z sur la grille mondiale' -Value $false -Kind checked)
    [void](Set-UiControlByLabel -Socket $socket -Label 'Poser le pivot sur le terrain' -Value $false -Kind checked)

    $casePassed = Invoke-CriterionCase -Criterion $criteria['R2-029'] -Name 'world_translation_transient_preview_apply_exact' -Action {
        $gesture = Invoke-GizmoGesture -Context $Context -Mode translation -Axis x -LeavePreview
        $proposed = $gesture.transient.current_transform
        $applied = Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $Context.PlacementId)
        [void](Invoke-UiClick -Socket $socket -Text 'Afficher le gizmo de translation X/Y/Z')
        $sourceAfterApply = Wait-GizmoDiagnostic -Socket $socket -PlacementId $Context.PlacementId -Mode translation
        if (-not (Test-NumericArraysEqual -Left @($proposed.translation_m) -Right @($sourceAfterApply.source_transform.translation_m)) -or
            -not (Test-NumericArraysEqual -Left @($proposed.rotation) -Right @($sourceAfterApply.source_transform.rotation)) -or
            -not (Test-NumericArraysEqual -Left @($proposed.scale) -Right @($sourceAfterApply.source_transform.scale))) {
            throw 'Applied placement transform differs from the exact transient proposal.'
        }
        Send-CdpEscape -Socket $socket
        [void](Invoke-UndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        return [pscustomobject]@{ gesture = $gesture; applied_state = $applied; exact_source_after_apply = $sourceAfterApply.source_transform; reverted_with_undo = $true }
    }
    if (-not $casePassed) { return @(Complete-CriterionSet -Criteria $criteria) }

    [void](Invoke-CriterionCase -Criterion $criteria['R2-029'] -Name 'local_translation_preview' -Action {
        [void](Set-UiControlByLabel -Socket $socket -Label 'Déplacement selon les axes locaux' -Value $true -Kind checked)
        $result = Invoke-GizmoGesture -Context $Context -Mode local_translation -Axis x
        [void](Set-UiControlByLabel -Socket $socket -Label 'Déplacement selon les axes locaux' -Value $false -Kind checked)
        return $result
    })
    [void](Invoke-CriterionCase -Criterion $criteria['R2-029'] -Name 'world_rotation_preview' -Action {
        [void](Set-UiControlByLabel -Socket $socket -Label 'Anneaux dans le repère local' -Value $false -Kind checked)
        return Invoke-GizmoGesture -Context $Context -Mode rotation -Axis y -RotationSpace world
    })
    [void](Invoke-CriterionCase -Criterion $criteria['R2-029'] -Name 'local_rotation_preview' -Action {
        [void](Set-UiControlByLabel -Socket $socket -Label 'Anneaux dans le repère local' -Value $true -Kind checked)
        $result = Invoke-GizmoGesture -Context $Context -Mode rotation -Axis y -RotationSpace local
        [void](Set-UiControlByLabel -Socket $socket -Label 'Anneaux dans le repère local' -Value $false -Kind checked)
        return $result
    })
    [void](Invoke-CriterionCase -Criterion $criteria['R2-029'] -Name 'local_scale_preview' -Action {
        return Invoke-GizmoGesture -Context $Context -Mode scale -Axis x
    })
    foreach ($cancelCase in @(
        [pscustomobject]@{ Name = 'escape_restores_transient'; Finish = 'escape' },
        [pscustomobject]@{ Name = 'mouseleave_restores_transient'; Finish = 'mouseleave' },
        [pscustomobject]@{ Name = 'blur_restores_transient'; Finish = 'blur' }
    )) {
        [void](Invoke-CriterionCase -Criterion $criteria['R2-029'] -Name $cancelCase.Name -Action {
            return Invoke-GizmoGesture -Context $Context -Mode translation -Axis x -Finish $cancelCase.Finish
        })
    }

    $casePassed = Invoke-CriterionCase -Criterion $criteria['R2-029'] -Name 'head_change_cancels_armed_gizmo' -Action {
        [void](Invoke-UiClick -Socket $socket -Text 'Afficher le gizmo de translation X/Y/Z')
        [void](Wait-GizmoDiagnostic -Socket $socket -PlacementId $Context.PlacementId -Mode translation)
        $before = Get-WorldState -Socket $socket
        [void](Invoke-UndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $cancelled = Wait-WorldState -Socket $socket -Description 'Head change cancelled armed gizmo' -TimeoutSeconds 10 -Predicate {
            param($state) $null -eq $state.diagnostic -or [string]$state.diagnostic.state -in @('idle', 'cancelled')
        }
        [void](Invoke-UndoRedoKey -Socket $socket -Action Redo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        if ([int]$cancelled.audit_counts.apply_zone -le [int]$before.audit_counts.apply_zone) { throw 'Undo did not change the accepted head.' }
        return $cancelled
    }
    if (-not $casePassed) { return @(Complete-CriterionSet -Criteria $criteria) }

    $casePassed = Invoke-CriterionCase -Criterion $criteria['R2-031'] -Name 'world_x_grid_and_surface_snap' -Action {
        [void](Set-UiControlByLabel -Socket $socket -Label 'Aligner X/Z sur la grille mondiale' -Value $true -Kind checked)
        [void](Set-UiControlByLabel -Socket $socket -Label 'Pas de grille (m)' -Value '1' -Kind value)
        [void](Set-UiControlByLabel -Socket $socket -Label 'Poser le pivot sur le terrain' -Value $true -Kind checked)
        [void](Set-UiControlByLabel -Socket $socket -Label 'Décalage depuis la surface (m)' -Value '0.75' -Kind value)
        $result = Invoke-GizmoGesture -Context $Context -Mode translation -Axis x
        $x = [double]$result.transient.current_transform.translation_m[0]
        if ([Math]::Abs($x - [Math]::Round($x)) -gt 0.001) { throw "World X snap is not on the 1 m grid: $x" }
        if ([Math]::Abs([double]$result.transient.current_transform.translation_m[2] - [double]$result.transient.source_transform.translation_m[2]) -gt 0.001) { throw 'World X snap changed the inactive Z coordinate.' }
        if ([Math]::Abs([double]$result.transient.current_transform.translation_m[1] - [double]$result.transient.source_transform.translation_m[1]) -lt 0.001) { throw 'World X surface alignment did not update Y.' }
        return $result
    }
    if (-not $casePassed) { return @(Complete-CriterionSet -Criteria $criteria) }
    $casePassed = Invoke-CriterionCase -Criterion $criteria['R2-031'] -Name 'world_z_grid_and_surface_snap' -Action {
        [void](Set-UiControlByLabel -Socket $socket -Label 'Aligner X/Z sur la grille mondiale' -Value $true -Kind checked)
        [void](Set-UiControlByLabel -Socket $socket -Label 'Pas de grille (m)' -Value '1' -Kind value)
        [void](Set-UiControlByLabel -Socket $socket -Label 'Poser le pivot sur le terrain' -Value $true -Kind checked)
        [void](Set-UiControlByLabel -Socket $socket -Label 'Décalage depuis la surface (m)' -Value '0.75' -Kind value)
        $result = Invoke-GizmoGesture -Context $Context -Mode translation -Axis z
        $z = [double]$result.transient.current_transform.translation_m[2]
        if ([Math]::Abs($z - [Math]::Round($z)) -gt 0.001) { throw "World Z snap is not on the 1 m grid: $z" }
        if ([Math]::Abs([double]$result.transient.current_transform.translation_m[0] - [double]$result.transient.source_transform.translation_m[0]) -gt 0.001) { throw 'World Z snap changed the inactive X coordinate.' }
        if ([Math]::Abs([double]$result.transient.current_transform.translation_m[1] - [double]$result.transient.source_transform.translation_m[1]) -lt 0.001) { throw 'World Z surface alignment did not update Y.' }
        return $result
    }
    if (-not $casePassed) { return @(Complete-CriterionSet -Criteria $criteria) }
    $casePassed = Invoke-CriterionCase -Criterion $criteria['R2-031'] -Name 'world_y_surface_conflict_refused' -Action {
        [void](Set-UiControlByLabel -Socket $socket -Label 'Aligner X/Z sur la grille mondiale' -Value $true -Kind checked)
        [void](Set-UiControlByLabel -Socket $socket -Label 'Pas de grille (m)' -Value '1' -Kind value)
        [void](Set-UiControlByLabel -Socket $socket -Label 'Poser le pivot sur le terrain' -Value $true -Kind checked)
        [void](Set-UiControlByLabel -Socket $socket -Label 'Décalage depuis la surface (m)' -Value '0.75' -Kind value)
        $before = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Afficher le gizmo de translation X/Y/Z')
        $diagnostic = Wait-GizmoDiagnostic -Socket $socket -PlacementId $Context.PlacementId -Mode translation
        $canvas = Get-CanvasGeometry -Socket $socket
        $geometry = Get-AxisGesturePoints -Diagnostic $diagnostic -Canvas $canvas -Axis y
        Send-CdpMouseDrag -Socket $socket -StartX $geometry.Start.x -StartY $geometry.Start.y -EndX $geometry.End.x -EndY $geometry.End.y -Steps 4
        $after = Wait-WorldState -Socket $socket -Description 'Y plus surface refusal' -TimeoutSeconds 10 -Predicate {
            param($state) [string]$state.command_status -like '*Disable surface alignment*' -and $state.diff_hidden
        }
        Assert-NoMutationIpcDelta -Before $before -After $after -Description 'Y plus surface refusal'
        return $after
    }
    if (-not $casePassed) { return @(Complete-CriterionSet -Criteria $criteria) }
    $casePassed = Invoke-CriterionCase -Criterion $criteria['R2-031'] -Name 'local_axis_surface_conflict_refused' -Action {
        [void](Set-UiControlByLabel -Socket $socket -Label 'Déplacement selon les axes locaux' -Value $true -Kind checked)
        [void](Set-UiControlByLabel -Socket $socket -Label 'Poser le pivot sur le terrain' -Value $true -Kind checked)
        [void](Set-UiControlByLabel -Socket $socket -Label 'Décalage depuis la surface (m)' -Value '0.75' -Kind value)
        $before = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Afficher le gizmo de translation X/Y/Z')
        $after = Wait-WorldState -Socket $socket -Description 'Local plus surface refusal' -TimeoutSeconds 10 -Predicate {
            param($state) [string]$state.command_status -like "*Désactiver l’alignement surface*" -and $state.diff_hidden
        }
        Assert-NoMutationIpcDelta -Before $before -After $after -Description 'Local plus surface refusal'
        [void](Set-UiControlByLabel -Socket $socket -Label 'Déplacement selon les axes locaux' -Value $false -Kind checked)
        [void](Set-UiControlByLabel -Socket $socket -Label 'Poser le pivot sur le terrain' -Value $false -Kind checked)
        [void](Set-UiControlByLabel -Socket $socket -Label 'Aligner X/Z sur la grille mondiale' -Value $false -Kind checked)
        return $after
    }
    if (-not $casePassed) { return @(Complete-CriterionSet -Criteria $criteria) }

    $casePassed = Invoke-CriterionCase -Criterion $criteria['R2-030'] -Name 'duplicate_delete_undo_redo_save_close_open' -Action {
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $Context.PlacementId)
        [void](Open-DetailsBySummary -Socket $socket -Summary 'Position précise et duplication')
        $initial = Get-WorldState -Socket $socket
        $initialOptions = @($initial.placement_options)
        $initialCount = $initialOptions.Count
        $xControl = Get-UiControlByLabel -Socket $socket -Label 'X (m)' -DetailsSummary 'Position précise et duplication'
        $copyX = ([double]$xControl.value + 2.5).ToString('0.###', [Globalization.CultureInfo]::InvariantCulture)
        [void](Set-UiControlByLabel -Socket $socket -Label 'X (m)' -Value $copyX -DetailsSummary 'Position précise et duplication')
        $beforeCopy = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Prévisualiser une copie à ces coordonnées')
        [void](Wait-NewPreview -Socket $socket -PreviousCount ([int]$beforeCopy.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds -DiffNeedle '1 ajout(s)')
        $afterCopy = Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds
        $withCopy = Wait-WorldState -Socket $socket -Description 'Duplicated placement accepted' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state) @($state.placement_options).Count -eq ($initialCount + 1)
        }
        $newOptions = @($withCopy.placement_options | Where-Object { $initialOptions.value -notcontains $_.value })
        if ($newOptions.Count -ne 1) { throw "Expected one deterministic duplicate identity, got $($newOptions.Count)." }
        $copyId = [string]$newOptions[0].value
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $copyId)
        [void](Wait-WorldState -Socket $socket -Description 'Duplicated placement inspector selected' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state) [string]$state.placement_value -ceq $copyId
        })
        $beforeDelete = Get-WorldState -Socket $socket
        [void](Invoke-UiClickInLabeledFieldset -Socket $socket -FieldLabel 'Objet placé à inspecter' -Text 'Prévisualiser la suppression')
        [void](Wait-NewPreview -Socket $socket -PreviousCount ([int]$beforeDelete.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds -DiffNeedle '1 retrait(s)')
        [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-WorldState -Socket $socket -Description 'Duplicate deleted' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state) @($state.placement_options).Count -eq $initialCount -and @($state.placement_options.value) -notcontains $copyId
        })
        [void](Invoke-UndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-WorldState -Socket $socket -Description 'Undo restored duplicate' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state) @($state.placement_options.value) -contains $copyId
        })
        [void](Invoke-UndoRedoKey -Socket $socket -Action Redo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-WorldState -Socket $socket -Description 'Redo removed duplicate again' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state) @($state.placement_options.value) -notcontains $copyId
        })
        $beforeSave = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Sauvegarder')
        [void](Wait-WorldState -Socket $socket -Description 'Save after duplicate/delete' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state) [int]$state.audit_counts.save -eq ([int]$beforeSave.audit_counts.save + 1) -and [string]$state.command_status -like '*Session synchronisée*'
        })
        $saved = Get-ArchiveSnapshot -LiteralPath $Context.ArchivePath
        if ($saved.Sha256 -ceq $Context.ArchiveBefore.Sha256) { throw 'Save did not change the isolated fixture archive after accepted edits.' }
        $beforeClose = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Fermer la zone')
        $closeReloadAudit = Install-WorldAuditAfterOwnedReload -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds
        $closed = Wait-WorldState -Socket $socket -Description 'Close isolated zone after owned reload' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state)
            [int]$state.audit_counts.close -eq ([int]$beforeClose.audit_counts.close + 1) -and
                [int]$state.audit_pending -eq 0 -and
                [string]$state.command_status -ceq 'Aucune zone ouverte' -and
                @($state.placement_options).Count -eq 0 -and
                @($state.building_options).Count -eq 0 -and
                [bool]$state.placement_disabled -and
                [bool]$state.building_disabled
        }
        $beforeRefresh = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Actualiser la liste')
        $catalog = Wait-WorldState -Socket $socket -Description 'Saved zone catalogue refresh' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state)
            [int]$state.audit_counts.list_zones -eq ([int]$beforeRefresh.audit_counts.list_zones + 1) -and
                [int]$state.audit_pending -eq 0
        }
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Choisir une zone' -Value $Context.ZoneId -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Invoke-UiClick -Socket $socket -Text 'Ouvrir la zone choisie')
        $reopened = Wait-WorldState -Socket $socket -Description 'Saved placement state reopened' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state)
            [int]$state.audit_counts.open -eq ([int]$beforeClose.audit_counts.open + 1) -and
                [int]$state.audit_pending -eq 0 -and
                @($state.placement_options).Count -eq $initialCount -and
                @($state.placement_options.value) -contains $Context.PlacementId -and
                @($state.placement_options.value) -notcontains $copyId
        }
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        return [pscustomobject]@{ original_id = $Context.PlacementId; duplicate_id = $copyId; initial_count = $initialCount; saved_archive = $saved; close_reload_audit = $closeReloadAudit; closed = $closed; catalog = $catalog; reopened = $reopened; screenshot = Save-WorldScreenshot -Context $Context -Name 'r2-030-reopened-after-delete' }
    }
    if (-not $casePassed) { return @(Complete-CriterionSet -Criteria $criteria) }

    return @(Complete-CriterionSet -Criteria $criteria)
}

function Invoke-PlacementLockScenario {
    param([Parameter(Mandatory)][object]$Context)
    $socket = $Context.Socket
    $criteria = @{
        'R2-029' = New-CriterionResult -Criterion 'R2-029'
    }
    [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
    [void](Open-DetailsBySummary -Socket $socket -Summary "Bibliothèque d’assets")
    [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $Context.PlacementId)

    [void](Invoke-CriterionCase -Criterion $criteria['R2-029'] -Name 'locked_placement_refuses_gizmo' -Action {
        $before = Get-WorldState -Socket $socket
        [void](Invoke-UiClickInLabeledFieldset -Socket $socket -FieldLabel 'Objet placé à inspecter' -Text 'Prévisualiser le verrouillage')
        [void](Wait-NewPreview -Socket $socket -PreviousCount ([int]$before.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds -DiffNeedle 'verrou')
        [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $Context.PlacementId)
        $target = Get-UiTarget -Socket $socket -Selector 'button' -Text 'Afficher le gizmo de translation X/Y/Z'
        if (-not $target.disabled) { throw 'Translation gizmo remained enabled for a locked placement.' }
        $lockedState = Get-WorldState -Socket $socket
        $lockedOptions = @($lockedState.placement_options | Where-Object value -eq $Context.PlacementId)
        if ($lockedOptions.Count -ne 1) { throw "Expected the exact locked placement once in the inspector, got $($lockedOptions.Count)." }
        $lockedScreenshot = Save-WorldScreenshot -Context $Context -Name 'r2-029-locked-placement'
        [void](Invoke-UndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $Context.PlacementId)
        $restoredGizmo = Get-UiTarget -Socket $socket -Selector 'button' -Text 'Afficher le gizmo de translation X/Y/Z'
        if ($restoredGizmo.disabled) { throw 'Undo left the translation gizmo disabled for the restored placement.' }
        $restoredLockAction = Get-UiTarget -Socket $socket -Selector 'button' -Text 'Prévisualiser le verrouillage'
        if ($restoredLockAction.disabled) { throw 'Undo did not restore the enabled placement lock action.' }
        $restoredScreenshot = Save-WorldScreenshot -Context $Context -Name 'r2-029-lock-undone'
        return [pscustomobject]@{ button_disabled_while_locked=$target.disabled; locked_option=$lockedOptions[0]; locked_screenshot=$lockedScreenshot; undo_completed=$true; gizmo_enabled_after_undo=(-not $restoredGizmo.disabled); lock_action_restored_after_undo=(-not $restoredLockAction.disabled); restored_screenshot=$restoredScreenshot }
    })

    return @(Complete-CriterionSet -Criteria $criteria)
}

function Invoke-BuildingPlaytestScenario {
    param([Parameter(Mandatory)][object]$Context)
    $socket = $Context.Socket
    $criteria = @{
        'R2-032' = New-CriterionResult -Criterion 'R2-032'
        'R2-033' = New-CriterionResult -Criterion 'R2-033'
    }
    [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
    $buildingDetails = 'Bâtiments paramétriques'
    [void](Open-DetailsBySummary -Socket $socket -Summary $buildingDetails)
    [void](Select-UiOptionByLabel -Socket $socket -Label 'Bâtiment à inspecter' -Value $Context.BuildingId)

    $requiredLabels = @(
        'Nom du bâtiment', 'Position X (m)', 'Position Y (m)', 'Position Z (m)', 'Orientation Y (degrés)',
        'Largeur (m)', 'Profondeur (m)', 'Hauteur intérieure (m)', 'Largeur de passage de porte (m)',
        'Hauteur de passage de porte (m)', 'Épaisseur des murs (m)', 'Épaisseur du plancher et du toit (m)',
        'Retouche porte : X local absolu (m)'
    )

    [void](Invoke-CriterionCase -Criterion $criteria['R2-032'] -Name 'complete_panel_and_eight_projected_parts' -Action {
        $values = [ordered]@{}
        foreach ($label in $requiredLabels) {
            $values[$label] = Get-UiControlByLabel -Socket $socket -Label $label -DetailsSummary $buildingDetails
        }
        $state = Get-WorldState -Socket $socket
        if ([string]$state.building_status -notmatch '^Bâtiments acceptés : 8 volumes préparés') {
            throw "Building projection did not report eight parts: $($state.building_status)"
        }
        return [pscustomobject]@{ controls = $values; projection = $state.building_status; screenshot = Save-WorldScreenshot -Context $Context -Name 'r2-032-complete-panel' }
    })

    [void](Invoke-CriterionCase -Criterion $criteria['R2-032'] -Name 'invalid_dimensions_fail_without_ipc' -Action {
        $original = Get-UiControlByLabel -Socket $socket -Label 'Largeur (m)' -DetailsSummary $buildingDetails
        [void](Set-UiControlByLabel -Socket $socket -Label 'Largeur (m)' -Value '0' -DetailsSummary $buildingDetails)
        $before = Get-WorldState -Socket $socket
        $beforeStatus = [string]$before.command_status
        [void](Invoke-UiClick -Socket $socket -Text 'Prévisualiser les dimensions')
        $after = Wait-WorldState -Socket $socket -Description 'Invalid building dimensions refusal' -TimeoutSeconds 10 -Predicate {
            param($state)
            [int]$state.audit_pending -eq 0 -and
                [string]$state.command_status -ceq 'Dimensions de pièce finies et dans les limites requises' -and
                [string]$state.command_status -cne $beforeStatus -and
                $state.diff_hidden
        }
        Assert-NoMutationIpcDelta -Before $before -After $after -Description 'Invalid building dimensions'
        [void](Set-UiControlByLabel -Socket $socket -Label 'Largeur (m)' -Value $original.value -DetailsSummary $buildingDetails)
        return $after
    })

    $edited = [ordered]@{}
    [void](Invoke-CriterionCase -Criterion $criteria['R2-032'] -Name 'all_building_fields_apply_lock_undo_redo_save_reopen' -Action {
        foreach ($label in $requiredLabels) { $edited[$label] = Get-UiControlByLabel -Socket $socket -Label $label -DetailsSummary $buildingDetails }
        $newName = [string]$edited['Nom du bâtiment'].value + ' — preuve Gate 2'
        $newX = [double]$edited['Position X (m)'].value + 0.5
        $newY = [double]$edited['Position Y (m)'].value
        $newZ = [double]$edited['Position Z (m)'].value + 0.5
        $newYaw = ([double]$edited['Orientation Y (degrés)'].value + 15.0) % 360.0
        $newWidth = [double]$edited['Largeur (m)'].value + 0.5
        $newDepth = [double]$edited['Profondeur (m)'].value + 0.5
        $newHeight = [double]$edited['Hauteur intérieure (m)'].value + 0.2
        $newDoorWidth = [Math]::Min([double]$edited['Largeur de passage de porte (m)'].value + 0.1, $newWidth - 1.0)
        $newDoorHeight = [Math]::Min([double]$edited['Hauteur de passage de porte (m)'].value + 0.1, $newHeight - 0.2)
        $newWall = [double]$edited['Épaisseur des murs (m)'].value + 0.01
        $newSlab = [double]$edited['Épaisseur du plancher et du toit (m)'].value + 0.01
        $newDoor = [double]$edited['Retouche porte : X local absolu (m)'].value + 0.1
        $invariant = [Globalization.CultureInfo]::InvariantCulture
        $groups = @(
            [pscustomobject]@{ Button = 'Prévisualiser le nom du bâtiment'; Values = @{ 'Nom du bâtiment' = $newName }; Diff = $newName },
            [pscustomobject]@{ Button = 'Prévisualiser position et orientation'; Values = @{ 'Position X (m)' = $newX.ToString('0.###',$invariant); 'Position Y (m)' = $newY.ToString('0.###',$invariant); 'Position Z (m)' = $newZ.ToString('0.###',$invariant); 'Orientation Y (degrés)' = $newYaw.ToString('0.###',$invariant) }; Diff = "orientation $($newYaw.ToString('0.###',$invariant))°" },
            [pscustomobject]@{ Button = 'Prévisualiser les dimensions'; Values = @{ 'Largeur (m)' = $newWidth.ToString('0.###',$invariant); 'Profondeur (m)' = $newDepth.ToString('0.###',$invariant); 'Hauteur intérieure (m)' = $newHeight.ToString('0.###',$invariant); 'Largeur de passage de porte (m)' = $newDoorWidth.ToString('0.###',$invariant); 'Hauteur de passage de porte (m)' = $newDoorHeight.ToString('0.###',$invariant); 'Épaisseur des murs (m)' = $newWall.ToString('0.###',$invariant); 'Épaisseur du plancher et du toit (m)' = $newSlab.ToString('0.###',$invariant) }; Diff = "passage $($newDoorWidth.ToString('0.###',$invariant)) × $($newDoorHeight.ToString('0.###',$invariant)) m" },
            [pscustomobject]@{ Button = 'Prévisualiser la retouche de porte'; Values = @{ 'Retouche porte : X local absolu (m)' = $newDoor.ToString('0.###',$invariant) }; Diff = "porte X=$($newDoor.ToString('0.###',$invariant)) m" }
        )
        foreach ($group in $groups) {
            foreach ($entry in $group.Values.GetEnumerator()) { [void](Set-UiControlByLabel -Socket $socket -Label $entry.Key -Value $entry.Value -DetailsSummary $buildingDetails) }
            $before = Get-WorldState -Socket $socket
            [void](Invoke-UiClick -Socket $socket -Text $group.Button)
            [void](Wait-NewPreview -Socket $socket -PreviousCount ([int]$before.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds -DiffNeedle $group.Diff)
            [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
            [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
            [void](Select-UiOptionByLabel -Socket $socket -Label 'Bâtiment à inspecter' -Value $Context.BuildingId)
        }
        $beforeLock = Get-WorldState -Socket $socket
        [void](Invoke-UiClickInLabeledFieldset -Socket $socket -FieldLabel 'Bâtiment à inspecter' -Text 'Prévisualiser le verrouillage')
        [void](Wait-NewPreview -Socket $socket -PreviousCount ([int]$beforeLock.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds -DiffNeedle 'verrou')
        [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Bâtiment à inspecter' -Value $Context.BuildingId)
        $lockedControl = Get-UiControlByLabel -Socket $socket -Label 'Largeur (m)' -DetailsSummary $buildingDetails
        if (-not $lockedControl.disabled) { throw 'Building edit fields remained enabled after locking.' }
        [void](Invoke-UndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Bâtiment à inspecter' -Value $Context.BuildingId)
        if ((Get-UiControlByLabel -Socket $socket -Label 'Largeur (m)' -DetailsSummary $buildingDetails).disabled) { throw 'Undo did not restore unlocked building controls.' }
        [void](Invoke-UndoRedoKey -Socket $socket -Action Redo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Bâtiment à inspecter' -Value $Context.BuildingId)
        if (-not (Get-UiControlByLabel -Socket $socket -Label 'Largeur (m)' -DetailsSummary $buildingDetails).disabled) { throw 'Redo did not restore building lock.' }
        $beforeSave = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Sauvegarder')
        [void](Wait-WorldState -Socket $socket -Description 'Building save' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state)
            [int]$state.audit_counts.save -eq ([int]$beforeSave.audit_counts.save + 1) -and
                [int]$state.audit_pending -eq 0 -and
                [string]$state.command_status -ceq 'Session synchronisée'
        })
        $savedArchive = Get-ArchiveSnapshot -LiteralPath $Context.ArchivePath
        if ($savedArchive.Sha256 -ceq $Context.ArchiveBefore.Sha256) { throw 'Building Save did not change the isolated archive.' }
        $beforeClose = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Fermer la zone')
        $closeReloadAudit = Install-WorldAuditAfterOwnedReload -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds
        $closed = Wait-WorldState -Socket $socket -Description 'Building zone close after owned reload' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state)
            [int]$state.audit_counts.close -eq ([int]$beforeClose.audit_counts.close + 1) -and
                [int]$state.audit_pending -eq 0 -and
                [string]$state.command_status -ceq 'Aucune zone ouverte' -and
                @($state.placement_options).Count -eq 0 -and
                @($state.building_options).Count -eq 0 -and
                [bool]$state.placement_disabled -and
                [bool]$state.building_disabled
        }
        $beforeRefresh = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Actualiser la liste')
        $catalog = Wait-WorldState -Socket $socket -Description 'Building saved zone catalogue refresh' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state)
            [int]$state.audit_counts.list_zones -eq ([int]$beforeRefresh.audit_counts.list_zones + 1) -and
                [int]$state.audit_pending -eq 0
        }
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Choisir une zone' -Value $Context.ZoneId -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Invoke-UiClick -Socket $socket -Text 'Ouvrir la zone choisie')
        $reopened = Wait-WorldState -Socket $socket -Description 'Building state reopened' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state)
            [int]$state.audit_counts.open -eq ([int]$beforeClose.audit_counts.open + 1) -and
                [int]$state.audit_pending -eq 0 -and
                @($state.building_options).Count -eq 1 -and
                [string]$state.building_options[0].value -ceq $Context.BuildingId -and
                [string]$state.building_options[0].text -like '* (verrouillé) *' -and
                [string]$state.building_status -match '^Bâtiments acceptés : 8 volumes préparés'
        }
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Bâtiment à inspecter' -Value $Context.BuildingId)
        $observed = [ordered]@{}
        foreach ($label in $requiredLabels) { $observed[$label] = Get-UiControlByLabel -Socket $socket -Label $label -DetailsSummary $buildingDetails }
        if (-not $observed['Largeur (m)'].disabled) { throw 'Reopened building did not preserve its lock.' }
        $expectations = @{
            'Nom du bâtiment' = $newName; 'Position X (m)' = $newX; 'Position Y (m)' = $newY; 'Position Z (m)' = $newZ;
            'Orientation Y (degrés)' = $newYaw; 'Largeur (m)' = $newWidth; 'Profondeur (m)' = $newDepth;
            'Hauteur intérieure (m)' = $newHeight; 'Largeur de passage de porte (m)' = $newDoorWidth;
            'Hauteur de passage de porte (m)' = $newDoorHeight; 'Épaisseur des murs (m)' = $newWall;
            'Épaisseur du plancher et du toit (m)' = $newSlab; 'Retouche porte : X local absolu (m)' = $newDoor
        }
        foreach ($entry in $expectations.GetEnumerator()) {
            if ($entry.Key -ceq 'Nom du bâtiment') {
                if ([string]$observed[$entry.Key].value -cne [string]$entry.Value) { throw "Reopened building name mismatch." }
            }
            elseif ([Math]::Abs([double]$observed[$entry.Key].value - [double]$entry.Value) -gt 0.001) {
                throw "Reopened building field '$($entry.Key)' mismatch."
            }
        }
        $reopenedArchive = Get-ArchiveSnapshot -LiteralPath $Context.ArchivePath
        if (-not (Test-ArchiveSnapshotsEqual -Before $savedArchive -After $reopenedArchive)) { throw 'Reopening changed the saved building archive.' }
        $Context.BuildingGeometry = [pscustomobject]@{ OriginX=$newX; OriginY=$newY; OriginZ=$newZ; Yaw=$newYaw; Width=$newWidth; Depth=$newDepth; Wall=$newWall; DoorOffset=$newDoor; DoorWidth=$newDoorWidth }
        $Context.PostSaveArchive = $savedArchive
        return [pscustomobject]@{ expected = $expectations; observed = $observed; saved_archive = $savedArchive; reopened_archive = $reopenedArchive; reopened = $reopened; lock_persisted=$true; close_reload_audit = $closeReloadAudit; closed = $closed; catalog = $catalog; screenshot = Save-WorldScreenshot -Context $Context -Name 'r2-032-reopened-building' }
    })

    [void](Invoke-CriterionCase -Criterion $criteria['R2-033'] -Name 'door_crossing_wall_contact_pointer_lock_return_edit' -Action {
        if ($null -eq $Context.BuildingGeometry -or $null -eq $Context.PostSaveArchive) { throw 'R2-032 saved geometry is required before the playtest proof.' }
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Bâtiment à inspecter' -Value $Context.BuildingId)
        $before = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Jouer devant cette porte')
        $startReloadAudit = Install-WorldAuditAfterOwnedReload -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds
        $started = Wait-WorldState -Socket $socket -Description 'Door playtest started after owned reload' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state)
            [int]$state.audit_counts.start_playtest_at -eq ([int]$before.audit_counts.start_playtest_at + 1) -and
                [int]$state.audit_pending -eq 0 -and
                [string]$state.command_status -ceq 'Playtest · sources gelées, aucune sauvegarde implicite'
        }
        $initial = Wait-PlaytestObservation -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds
        $canvas = Get-CanvasGeometry -Socket $socket
        Send-CdpMouseClick -Socket $socket -X $canvas.center_x -Y $canvas.center_y
        [void](Wait-WorldState -Socket $socket -Description 'Canvas pointer lock' -TimeoutSeconds 10 -Predicate {
            param($state) [string]$state.pointer_lock_id -ceq 'forge-authoring-canvas'
        })
        Send-CdpKeyHold -Socket $socket -Key 'w' -Code 'KeyW' -VirtualKeyCode 87 -Milliseconds 900
        $inside = Wait-PlaytestObservation -Socket $socket -AfterTick $initial.Tick -TimeoutSeconds 15
        Send-CdpKeyHold -Socket $socket -Key 'w' -Code 'KeyW' -VirtualKeyCode 87 -Milliseconds 1400
        $wall = Wait-PlaytestObservation -Socket $socket -AfterTick $inside.Tick -TimeoutSeconds 15
        Send-CdpKeyHold -Socket $socket -Key 's' -Code 'KeyS' -VirtualKeyCode 83 -Milliseconds 1900
        $outside = Wait-PlaytestObservation -Socket $socket -AfterTick $wall.Tick -TimeoutSeconds 15
        $g = $Context.BuildingGeometry
        $initialLocal = Convert-WorldToBuildingLocal -Point $initial -OriginX $g.OriginX -OriginZ $g.OriginZ -YawDegrees $g.Yaw
        $insideLocal = Convert-WorldToBuildingLocal -Point $inside -OriginX $g.OriginX -OriginZ $g.OriginZ -YawDegrees $g.Yaw
        $wallLocal = Convert-WorldToBuildingLocal -Point $wall -OriginX $g.OriginX -OriginZ $g.OriginZ -YawDegrees $g.Yaw
        $outsideLocal = Convert-WorldToBuildingLocal -Point $outside -OriginX $g.OriginX -OriginZ $g.OriginZ -YawDegrees $g.Yaw
        if ($initialLocal.Z -ge (-$g.Depth / 2.0 - 0.5)) { throw "Door playtest did not start outside the facade: local Z=$($initialLocal.Z)." }
        if ($insideLocal.Z -le (-$g.Depth / 2.0 + 0.15) -or $insideLocal.Z -ge ($g.Depth / 2.0 - 0.15)) { throw "W did not place the player inside the room: local Z=$($insideLocal.Z)." }
        if ([Math]::Abs($insideLocal.X - $g.DoorOffset) -gt ($g.DoorWidth / 2.0)) { throw "Door crossing drifted outside the opening: local X=$($insideLocal.X)." }
        $rearWallMinimum = $g.Depth / 2.0 - $g.Wall - 0.8
        $rearWallMaximum = $g.Depth / 2.0 - $g.Wall + 0.2
        if ($wall.Contacts -lt 2 -or $wallLocal.Z -le ($insideLocal.Z + 0.2) -or $wallLocal.Z -lt $rearWallMinimum -or $wallLocal.Z -gt $rearWallMaximum) { throw "Continued W did not report a bounded rear-wall contact after forward progress: contacts=$($wall.Contacts), local Z=$($wallLocal.Z), expected [$rearWallMinimum,$rearWallMaximum]." }
        if ($outsideLocal.Z -ge (-$g.Depth / 2.0 - 0.2)) { throw "S did not return outside through the doorway: local Z=$($outsideLocal.Z)." }
        $movementCapture = Save-WorldScreenshot -Context $Context -Name 'r2-033-after-w-s-crossing'
        Send-CdpEscape -Socket $socket
        [void](Wait-WorldState -Socket $socket -Description 'Pointer lock released by Escape' -TimeoutSeconds 10 -Predicate {
            param($state) [string]::IsNullOrEmpty([string]$state.pointer_lock_id)
        })
        $beforeStop = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text "Retour à l’édition")
        $stopReloadAudit = Install-WorldAuditAfterOwnedReload -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds
        $returned = Wait-WorldState -Socket $socket -Description 'Return to edit after door playtest owned reload' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state)
            [int]$state.audit_counts.stop_playtest -eq ([int]$beforeStop.audit_counts.stop_playtest + 1) -and
                [int]$state.audit_pending -eq 0 -and
                [string]$state.command_status -ceq 'Session synchronisée' -and
                [string]$state.mode -like 'Mode édition*'
        }
        $archiveAfter = Get-ArchiveSnapshot -LiteralPath $Context.ArchivePath
        if (-not (Test-ArchiveSnapshotsEqual -Before $Context.PostSaveArchive -After $archiveAfter)) { throw 'Playtest changed the saved isolated authoring archive.' }
        return [pscustomobject]@{ start_reload_audit=$startReloadAudit; started=$started; initial=$initial; inside=$inside; wall=$wall; outside=$outside; initial_local=$initialLocal; inside_local=$insideLocal; wall_local=$wallLocal; outside_local=$outsideLocal; pointer_lock_acquired=$true; pointer_lock_released=$true; stop_reload_audit=$stopReloadAudit; returned=$returned; archive_unchanged_around_playtest=$true; screenshot=$movementCapture }
    })

    foreach ($criterion in $criteria.Values) { Complete-CriterionResult -Criterion $criterion }
    return @($criteria.Values | Sort-Object Criterion)
}

function Invoke-AssetPreviewCancelScenario {
    param([Parameter(Mandatory)][object]$Context)
    $criterion = New-CriterionResult -Criterion 'R2-026'
    $socket = $Context.Socket
    [void](Wait-CleanPreviewState -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
    [void](Open-DetailsBySummary -Socket $socket -Summary "Bibliothèque d’assets")
    [void](Invoke-CriterionCase -Criterion $criterion -Name 'late_oak_stream_cancel_then_select_pine' -Action {
        $before = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Charger le catalogue local')
        [void](Wait-WorldState -Socket $socket -Description 'Oak and pine catalogue cards' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state)
            $expression = @'
(() => { const norm=v=>(v??'').replace(/\s+/g,' ').trim(); const texts=[...document.querySelectorAll('button')].map(n=>norm(n.textContent)); return { ready:texts.some(t=>t.startsWith('vv-tree-oak-a · ')) && texts.some(t=>t.startsWith('vv-tree-pine-a · ')) }; })()
'@
            return (Invoke-CdpExpression -Socket $socket -Expression $expression).ready
        })
        $oakTextExpression = @'
(() => { const norm=v=>(v??'').replace(/\s+/g,' ').trim(); return [...document.querySelectorAll('button')].map(n=>norm(n.textContent)).find(t=>t.startsWith('vv-tree-oak-a · ')) ?? ''; })()
'@
        $oakText = Invoke-CdpExpression -Socket $socket -Expression $oakTextExpression
        if ([string]::IsNullOrWhiteSpace([string]$oakText)) { throw 'Exact oak catalogue card disappeared.' }
        [void](Invoke-UiClick -Socket $socket -Text ([string]$oakText))
        $pinState = Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const norm=v=>(v??'').replace(/\s+/g,' ').trim(); const section=document.querySelector('section[aria-label="Fiche asset"]'); const title=norm(section?.querySelector('h3')?.textContent); const text=norm(section?.textContent); const match=text.match(/SHA-256 attendu : ([0-9a-f]{64})/i); return { ready:title==='vv-tree-oak-a' && !!match, title, pin:match?.[1]?.toLowerCase() ?? null }; })()
'@
        if (-not $pinState.ready) { throw 'Exact oak fiche/pin was not visible.' }
        [void](Invoke-UiClick -Socket $socket -Text 'Aperçu 3D temporaire')
        $streaming = Wait-WorldState -Socket $socket -Description 'Synthetic delayed oak stream started' -TimeoutSeconds 15 -Predicate {
            param($state) $null -ne $state.asset_transfer -and [string]$state.asset_transfer.state -ceq 'streaming' -and [int]$state.asset_transfer.bytes_delivered -lt [int]$state.asset_transfer.bytes
        }
        [void](Invoke-UiClick -Socket $socket -Text "Retirer l’aperçu / revenir à la carte")
        $pineText = Invoke-CdpExpression -Socket $socket -Expression @'
(() => { const norm=v=>(v??'').replace(/\s+/g,' ').trim(); return [...document.querySelectorAll('button')].map(n=>norm(n.textContent)).find(t=>t.startsWith('vv-tree-pine-a · ')) ?? ''; })()
'@
        if ([string]::IsNullOrWhiteSpace([string]$pineText)) { throw 'Exact pine catalogue card disappeared.' }
        [void](Invoke-UiClick -Socket $socket -Text ([string]$pineText))
        $settled = Wait-WorldState -Socket $socket -Description 'Delayed oak transfer settled after user cancellation' -TimeoutSeconds 25 -Predicate {
            param($state)
            $null -ne $state.asset_transfer -and [string]$state.asset_transfer.state -in @('completed','aborted','consumer_cancelled')
        }
        Start-Sleep -Milliseconds 1200
        $final = Get-WorldState -Socket $socket
        if ([string]$final.selected_asset -cne $script:PineAssetId) { throw "Pine fiche is not selected after cancelling oak: $($final.selected_asset)" }
        if (@($final.asset_preview_outputs | Where-Object { [string]$_ -like '*Aperçu LOD0 chargé*' }).Count -ne 0) { throw 'A late oak preview became visible after cancellation and pine selection.' }
        if ([string]$final.asset_transfer.sha256 -cne [string]$pinState.pin) { throw "Real candidate oak bytes do not match the visible governed pin." }
        Assert-NoMutationIpcDelta -Before $before -After $final -Description 'Asset preview cancellation'
        if (@($final.placement_options).Count -ne @($before.placement_options).Count) { throw 'Asset preview cancellation changed accepted placement options.' }
        $archiveAfter = Get-ArchiveSnapshot -LiteralPath $Context.ArchivePath
        if (-not (Test-ArchiveSnapshotsEqual -Before $Context.ArchiveBefore -After $archiveAfter)) { throw 'Asset preview cancellation changed the isolated authoring archive.' }
        return [pscustomobject]@{ transport = 'synthetic delayed ReadableStream over real candidate bytes; not an HTTP fault fixture'; visible_pin=$pinState.pin; streaming=$streaming.asset_transfer; settled=$settled.asset_transfer; final=$final; archive_unchanged=$true; screenshot=Save-WorldScreenshot -Context $Context -Name 'r2-026-pine-after-oak-cancel' }
    })
    Complete-CriterionResult -Criterion $criterion
    return @($criterion)
}

function Invoke-TransversalV8Scenario {
    param([Parameter(Mandatory)][object]$Context)
    $socket = $Context.Socket
    $criteria = @{
        'R2-037' = New-CriterionResult -Criterion 'R2-037'
        'R2-042' = New-CriterionResult -Criterion 'R2-042'
        'R2-046' = New-CriterionResult -Criterion 'R2-046'
        'R2-047' = New-CriterionResult -Criterion 'R2-047'
        'R2-048' = New-CriterionResult -Criterion 'R2-048'
        'R2-049' = New-CriterionResult -Criterion 'R2-049'
        'R2-051' = New-CriterionResult -Criterion 'R2-051'
    }
    $invariant = [Globalization.CultureInfo]::InvariantCulture
    $treeId = 'editor_trees'
    $grassId = 'editor_grass'
    $treeName = 'Arbres'
    $grassName = 'Herbe'
    $expectedAssetId = '9f2a9ead-817a-52a9-a22e-1f190fa62baa'
    $expectedAssetHash = '41552424a1f15be548fffbdf78a765a5d486f39440d272f954acccf485d0d759'
    $initialPlacementIds = @()
    $duplicateBreadth = $null

    [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Couches : nom, ordre, densité et espacement')
    $initial = Get-TransversalState -Socket $socket
    $initialLayerIds = @($initial.layer_options | ForEach-Object { [string]$_.value })
    $initialPlacementIds = @($initial.placement_options | ForEach-Object { [string]$_.value })
    if (($initialLayerIds -join '|') -cne "$treeId|$grassId") { throw "TransversalV8 requires exact layer order '$treeId|$grassId', got '$($initialLayerIds -join '|')'." }
    if ($initialPlacementIds.Count -ne 72 -or $initialPlacementIds -notcontains $Context.PlacementId) { throw "TransversalV8 requires 72 placements including $($Context.PlacementId)." }
    if (@($initial.building_options).Count -ne 1 -or [string]$initial.building_options[0].value -cne $Context.BuildingId) { throw 'TransversalV8 requires the exact single building fixture.' }

    $casePassed = Invoke-CriterionCase -Criterion $criteria['R2-049'] -Name 'bounded_source_override_lock_inspectors' -Action {
        $before = Get-WorldState -Socket $socket
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Couche de végétation' -Value $treeId)
        $tree = Wait-TransversalState -Socket $socket -Description 'Locked tree density provenance' -TimeoutSeconds 10 -Predicate {
            param($state)
            [string]$state.density_output -ceq 'Arbres · retouche manuelle · verrouillée : déverrouiller avant peinture' -and
                -not [string]::IsNullOrWhiteSpace([string]$state.density_provenance)
        }
        if ([string]$tree.density_provenance -notlike "*$treeId*") { throw 'Tree density provenance omitted the stable layer id.' }
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Couche de végétation' -Value $grassId)
        $grass = Wait-TransversalState -Socket $socket -Description 'Inherited grass density provenance' -TimeoutSeconds 10 -Predicate {
            param($state) [string]$state.density_output -ceq 'Herbe · héritée de la recette · modifiable'
        }
        if ([string]$grass.density_output -like '*Arbres*' -or [string]$grass.density_provenance -like "*$treeId*") { throw 'Density inspector leaked the previously selected layer.' }

        [void](Open-DetailsBySummary -Socket $socket -Summary "Bibliothèque d’assets")
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $Context.PlacementId)
        $placement = Wait-TransversalState -Socket $socket -Description 'Placement accepted provenance' -TimeoutSeconds 10 -Predicate {
            param($state) [string]$state.placement_source -like "*placement $($Context.PlacementId)*"
        }
        if ([string]$placement.placement_source -notlike "Source acceptée : asset $expectedAssetId · SHA-256 $expectedAssetHash*" -or [string]$placement.placement_source -notlike '*Verrou du placement entier : inactif*') {
            throw 'Placement source/hash/override/lock provenance is not the exact accepted fixture contract.'
        }
        $other = @($placement.placement_options | Where-Object { [string]$_.value -cne $Context.PlacementId })[0]
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value ([string]$other.value))
        $otherPlacement = Wait-TransversalState -Socket $socket -Description 'Bounded alternate placement provenance' -TimeoutSeconds 10 -Predicate {
            param($state) [string]$state.placement_source -like "*placement $([string]$other.value)*"
        }
        if ([string]$otherPlacement.placement_source -like "*$($Context.PlacementId)*") { throw 'Placement inspector retained the previous placement provenance.' }
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $Context.PlacementId)

        [void](Open-DetailsBySummary -Socket $socket -Summary 'Bâtiments paramétriques')
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Bâtiment à inspecter' -Value $Context.BuildingId)
        $building = Wait-TransversalState -Socket $socket -Description 'Building door accepted provenance' -TimeoutSeconds 10 -Predicate {
            param($state) [string]$state.building_source -like 'Porte acceptée —*'
        }
        foreach ($needle in @('Recette : 0 m','retouche absolue : 1.5 m','valeur effective : 1.5 m','Verrou du bâtiment entier : inactif')) {
            if ([string]$building.building_source -notlike "*$needle*") { throw "Building provenance omitted '$needle'." }
        }

        [void](Open-DetailsBySummary -Socket $socket -Summary 'Chemins — points et profil')
        $paths = Get-TransversalState -Socket $socket
        if (@($paths.path_options).Count -ne 1) { throw 'The building-playtest fixture must expose exactly one accepted path.' }
        $pathId = [string]$paths.path_options[0].value
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Chemin à éditer' -Value $pathId)
        $acceptedPath = Wait-TransversalState -Socket $socket -Description 'Accepted path provenance' -TimeoutSeconds 10 -Predicate {
            param($state) [string]$state.path_source -like 'Source : tracé auteur accepté*Verrou inactif*'
        }
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Chemin à éditer' -Value '')
        $draftPath = Wait-TransversalState -Socket $socket -Description 'Draft path provenance' -TimeoutSeconds 10 -Predicate {
            param($state) [string]$state.path_source -ceq 'Source : nouveau tracé en brouillon, non présent dans la zone avant Appliquer.'
        }

        [void](Open-DetailsBySummary -Socket $socket -Summary 'Rivières — tracé, niveau et creusement')
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Rivière à éditer' -Value '')
        $draftRiver = Wait-TransversalState -Socket $socket -Description 'Draft river provenance' -TimeoutSeconds 10 -Predicate {
            param($state) [string]$state.river_source -ceq 'Source : nouvelle recette de rivière en brouillon, non présente dans la zone avant Appliquer.'
        }
        if (@($draftRiver.river_options).Count -ne 0) { throw 'This bounded fixture unexpectedly contains an accepted river; update the explicit R2-049 scope before claiming it.' }
        $after = Get-WorldState -Socket $socket
        Assert-NoMutationIpcDelta -Before $before -After $after -Description 'R2-049 bounded inspector collection'
        $archiveAfter = Get-ArchiveSnapshot -LiteralPath $Context.ArchivePath
        if (-not (Test-ArchiveSnapshotsEqual -Before $Context.ArchiveBefore -After $archiveAfter)) { throw 'Read-only provenance inspection changed the isolated archive.' }
        return [pscustomobject]@{
            density_tree=$tree.density_output; density_grass=$grass.density_output
            placement_target=$placement.placement_source; placement_alternate=$otherPlacement.placement_source
            building=$building.building_source; accepted_path_id=$pathId; accepted_path=$acceptedPath.path_source
            draft_path=$draftPath.path_source; draft_river=$draftRiver.river_source
            accepted_river_observed=$false; criterion_complete_in_scenario=$false
            open_gap='No accepted river exists in the building-playtest fixture.'
        }
    }
    if (-not $casePassed) { return @(Complete-CriterionSet -Criteria $criteria) }

    $casePassed = Invoke-CriterionCase -Criterion $criteria['R2-046'] -Name 'layer_copy_reorder_remove_stable_ids' -Action {
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Couche de végétation' -Value $grassId)
        $grassCard = Wait-TransversalState -Socket $socket -Description 'Exact grass layer card' -TimeoutSeconds 10 -Predicate {
            param($state) @($state.layer_cards).Count -eq 1 -and [string]$state.layer_cards[0].legend -ceq $grassName
        }
        if ([double]$grassCard.layer_cards[0].density.value -ne 65 -or [double]$grassCard.layer_cards[0].spacing.value -ne 0) { throw 'Grass fixture recipe is not density 65%, spacing 0 m.' }
        $beforeFirst = Get-WorldState -Socket $socket
        [void](Invoke-UiClickInFieldsetByLegend -Socket $socket -Legend $grassName -Text 'Examiner une copie de recette')
        $firstPreviewState = Wait-NewPreview -Socket $socket -PreviousCount ([int]$beforeFirst.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds
        $firstSnapshot = Get-WorldAuditSnapshot -Socket $socket
        $firstEntry = Get-RequiredAuditedEntry -Snapshot $firstSnapshot -RequestKind 'preview_zone'
        $firstReceipt = Get-RequiredAuditReceipt -Entry $firstEntry
        $beforeSecond = Get-WorldState -Socket $socket
        [void](Invoke-UiClickInFieldsetByLegend -Socket $socket -Legend $grassName -Text 'Examiner une copie de recette')
        $secondPreviewState = Wait-NewPreview -Socket $socket -PreviousCount ([int]$beforeSecond.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds
        $secondSnapshot = Get-WorldAuditSnapshot -Socket $socket
        $secondEntry = Get-RequiredAuditedEntry -Snapshot $secondSnapshot -RequestKind 'preview_zone'
        $secondReceipt = Get-RequiredAuditReceipt -Entry $secondEntry
        if ([string]$firstEntry.request_sha256 -cne [string]$secondEntry.request_sha256 -or [string]$firstEntry.wire.request.request.command_id -cne [string]$secondEntry.wire.request.request.command_id -or -not (Test-AuditReceiptsEqual -Left $firstReceipt -Right $secondReceipt)) {
            throw 'Repeated unchanged layer-copy preview was not deterministic request-for-request and receipt-for-receipt.'
        }
        [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $copied = Wait-TransversalState -Socket $socket -Description 'Copied vegetation layer identity' -TimeoutSeconds 10 -Predicate { param($state) @($state.layer_options).Count -eq 3 }
        $copyOptions = @($copied.layer_options | Where-Object { [string]$_.value -notin $initialLayerIds })
        if ($copyOptions.Count -ne 1) { throw 'Layer copy did not create exactly one new stable id.' }
        $copyId = [string]$copyOptions[0].value
        if ($copyId -cnotmatch '^layer_[0-9a-f]{8}_[0-9a-f]{4}_[0-9a-f]{4}_[0-9a-f]{4}_[0-9a-f]{12}$') { throw "Copied layer id is not the deterministic stable format: $copyId" }
        if ((@($copied.layer_options | ForEach-Object { [string]$_.value }) -join '|') -cne "$treeId|$grassId|$copyId") { throw 'Copied layer was not inserted immediately after its source.' }
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Couche de végétation' -Value $copyId)
        $copyCard = Wait-TransversalState -Socket $socket -Description 'Copied vegetation layer recipe' -TimeoutSeconds 10 -Predicate {
            param($state) @($state.layer_cards).Count -eq 1 -and [string]$state.layer_cards[0].legend -ceq 'Nouvelle couche (copie)'
        }
        if ([double]$copyCard.layer_cards[0].density.value -ne 65 -or [double]$copyCard.layer_cards[0].spacing.value -ne 0 -or [string]$copyCard.density_output -cne 'Nouvelle couche (copie) · héritée de la recette · modifiable') {
            throw 'Copied layer did not preserve the recipe while excluding the authored density mask.'
        }
        $beforeMove = Get-WorldState -Socket $socket
        [void](Invoke-UiClickInFieldsetByLegend -Socket $socket -Legend 'Nouvelle couche (copie)' -Text 'Monter cette couche')
        [void](Wait-NewPreview -Socket $socket -PreviousCount ([int]$beforeMove.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $moved = Wait-TransversalState -Socket $socket -Description 'Reordered stable layer ids' -TimeoutSeconds 10 -Predicate {
            param($state) (@($state.layer_options | ForEach-Object { [string]$_.value }) -join '|') -ceq "$treeId|$copyId|$grassId"
        }
        $beforeRemove = Get-WorldState -Socket $socket
        [void](Invoke-UiClickInFieldsetByLegend -Socket $socket -Legend 'Nouvelle couche (copie)' -Text 'Examiner le retrait de cette couche')
        [void](Wait-NewPreview -Socket $socket -PreviousCount ([int]$beforeRemove.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $removed = Wait-TransversalState -Socket $socket -Description 'Removed copied layer only' -TimeoutSeconds 10 -Predicate {
            param($state) (@($state.layer_options | ForEach-Object { [string]$_.value }) -join '|') -ceq "$treeId|$grassId"
        }
        if ([string]$removed.density_output -notin @('Choisir une couche valide pour inspecter sa densité.','Arbres · retouche manuelle · verrouillée : déverrouiller avant peinture','Herbe · héritée de la recette · modifiable')) { throw 'Layer removal left an incoherent density inspector.' }
        return [pscustomobject]@{ source_ids=$initialLayerIds; copy_id=$copyId; copied_order=@($copied.layer_options.value); reordered=@($moved.layer_options.value); restored=@($removed.layer_options.value); repeated_preview_request_sha256=$firstEntry.request_sha256; repeated_preview_receipt=$firstReceipt; non_layer_identity_breadth_deferred_to_r2_051=$true }
    }
    if (-not $casePassed) { return @(Complete-CriterionSet -Criteria $criteria) }

    $casePassed = Invoke-CriterionCase -Criterion $criteria['R2-047'] -Name 'locked_density_removal_refusal_and_explicit_resolution' -Action {
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Couche de végétation' -Value $treeId)
        $locked = Wait-TransversalState -Socket $socket -Description 'Locked tree layer before conflict' -TimeoutSeconds 10 -Predicate {
            param($state) [string]$state.density_output -ceq 'Arbres · retouche manuelle · verrouillée : déverrouiller avant peinture'
        }
        $archiveBefore = Get-ArchiveSnapshot -LiteralPath $Context.ArchivePath
        $beforeRefusal = Get-WorldState -Socket $socket
        [void](Invoke-UiClickInFieldsetByLegend -Socket $socket -Legend $treeName -Text 'Examiner le retrait de cette couche')
        $refused = Wait-WorldState -Socket $socket -Description 'Locked density layer removal refusal' -TimeoutSeconds 10 -Predicate {
            param($state)
            [string]$state.command_status -like '*[[]DensityLayerRemoved[]]*' -and
                [string]$state.command_status -like "*$treeId*" -and
                $state.diff_hidden -and $state.apply_disabled
        }
        foreach ($needle in @('Régénération refusée ; aucune retouche appliquée.','verrou actif',"déverrouiller n’efface pas le masque")) {
            if ([string]$refused.command_status -notlike "*$needle*") { throw "Locked-layer refusal omitted '$needle'." }
        }
        Assert-NoMutationIpcDelta -Before $beforeRefusal -After $refused -Description 'Locked density layer removal refusal'
        $archiveRefused = Get-ArchiveSnapshot -LiteralPath $Context.ArchivePath
        if (-not (Test-ArchiveSnapshotsEqual -Before $archiveBefore -After $archiveRefused)) { throw 'Locked-layer refusal changed the archive.' }

        $beforeUnlock = Get-WorldState -Socket $socket
        [void](Invoke-UiClickInLabeledFieldset -Socket $socket -FieldLabel 'Couche de végétation' -Text 'Prévisualiser le déverrouillage')
        [void](Wait-NewPreview -Socket $socket -PreviousCount ([int]$beforeUnlock.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds -DiffNeedle 'verrou')
        [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $unlocked = Wait-TransversalState -Socket $socket -Description 'Density override explicitly unlocked' -TimeoutSeconds 10 -Predicate {
            param($state) [string]$state.density_output -ceq 'Arbres · retouche manuelle · modifiable'
        }
        $beforeClear = Get-WorldState -Socket $socket
        [void](Invoke-UiClickInLabeledFieldset -Socket $socket -FieldLabel 'Couche de végétation' -Text 'Prévisualiser le retour complet à la recette')
        [void](Wait-NewPreview -Socket $socket -PreviousCount ([int]$beforeClear.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $cleared = Wait-TransversalState -Socket $socket -Description 'Density override explicitly removed' -TimeoutSeconds 10 -Predicate {
            param($state) [string]$state.density_output -ceq 'Arbres · héritée de la recette · modifiable'
        }
        $beforeRemove = Get-WorldState -Socket $socket
        [void](Invoke-UiClickInFieldsetByLegend -Socket $socket -Legend $treeName -Text 'Examiner le retrait de cette couche')
        [void](Wait-NewPreview -Socket $socket -PreviousCount ([int]$beforeRemove.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $treeRemoved = Wait-TransversalState -Socket $socket -Description 'Resolved tree layer removal' -TimeoutSeconds 10 -Predicate {
            param($state) (@($state.layer_options | ForEach-Object { [string]$_.value }) -join '|') -ceq $grassId
        }

        [void](Invoke-UndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Couche de végétation' -Value $treeId)
        $undoRemoval = Wait-TransversalState -Socket $socket -Description 'Undo restored layer without density override' -TimeoutSeconds 10 -Predicate { param($state) [string]$state.density_output -ceq 'Arbres · héritée de la recette · modifiable' }
        [void](Invoke-UndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Couche de végétation' -Value $treeId)
        $undoClear = Wait-TransversalState -Socket $socket -Description 'Undo restored unlocked density override' -TimeoutSeconds 10 -Predicate { param($state) [string]$state.density_output -ceq 'Arbres · retouche manuelle · modifiable' }
        [void](Invoke-UndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Couche de végétation' -Value $treeId)
        $undoUnlock = Wait-TransversalState -Socket $socket -Description 'Undo restored locked density override' -TimeoutSeconds 10 -Predicate { param($state) [string]$state.density_output -ceq 'Arbres · retouche manuelle · verrouillée : déverrouiller avant peinture' }
        if ((@($undoUnlock.layer_options | ForEach-Object { [string]$_.value }) -join '|') -cne "$treeId|$grassId") { throw 'Three Undo operations did not restore the exact layer order.' }
        $archiveAfter = Get-ArchiveSnapshot -LiteralPath $Context.ArchivePath
        if (-not (Test-ArchiveSnapshotsEqual -Before $archiveBefore -After $archiveAfter)) { throw 'Conflict resolution and Undo changed the unsaved isolated archive.' }
        return [pscustomobject]@{ refusal=$refused.command_status; refused_without_ipc=$true; unlocked=$unlocked.density_output; override_cleared=$cleared.density_output; remaining_after_remove=@($treeRemoved.layer_options.value); undo_remove=$undoRemoval.density_output; undo_clear=$undoClear.density_output; undo_unlock=$undoUnlock.density_output; archive_unchanged=$true }
    }
    if (-not $casePassed) { return @(Complete-CriterionSet -Criteria $criteria) }

    $casePassed = Invoke-CriterionCase -Criterion $criteria['R2-048'] -Name 'world_grid_snap_external_pivot_apply_undo_redo' -Action {
        [void](Open-DetailsBySummary -Socket $socket -Summary "Bibliothèque d’assets")
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $Context.PlacementId)
        [void](Open-DetailsBySummary -Socket $socket -Summary 'Position précise et duplication')
        $snapSurfaceChoice = Set-UiControlByLabel -Socket $socket -Label 'Poser le pivot sur le terrain' -Value $false -Kind checked -DetailsSummary 'Position précise et duplication'
        $source = Get-AcceptedPlacementTransformFromGizmo -Context $Context
        if (-not (Test-NumericArraysEqual -Left @($source.translation_m) -Right @(-84.0,3.0,85.115966796875) -Tolerance 0.0001) -or
            -not (Test-NumericArraysEqual -Left @($source.rotation) -Right @(0.0,0.0,0.0,1.0) -Tolerance 0.0001) -or
            -not (Test-NumericArraysEqual -Left @($source.scale) -Right @(1.0,1.0,1.0) -Tolerance 0.0001)) {
            throw 'External-pivot proof requires the exact untouched target placement transform from the building-playtest fixture.'
        }
        $snapGridChoice = Set-UiControlByLabel -Socket $socket -Label 'Aligner X/Z sur la grille mondiale' -Value $true -Kind checked -DetailsSummary 'Position précise et duplication'
        $snapStepChoice = Set-UiControlByLabel -Socket $socket -Label 'Pas de grille (m)' -Value '1' -DetailsSummary 'Position précise et duplication'
        $snapDraft = @(-82.4,[double]$source.translation_m[1],84.6)
        $coordinateLabels = @('X (m)','Y (m)','Z (m)')
        for ($index = 0; $index -lt 3; $index += 1) {
            [void](Set-UiControlByLabel -Socket $socket -Label $coordinateLabels[$index] -Value ([double]$snapDraft[$index]).ToString('R',$invariant) -DetailsSummary 'Position précise et duplication')
        }
        $beforeSnapPreview = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Prévisualiser le déplacement')
        $snapPreview = Wait-NewPreview -Socket $socket -PreviousCount ([int]$beforeSnapPreview.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds
        if ([int]$snapPreview.audit_counts.apply_zone -ne [int]$beforeSnapPreview.audit_counts.apply_zone -or [int]$snapPreview.audit_counts.save -ne [int]$beforeSnapPreview.audit_counts.save) { throw 'Grid-snap preview emitted ApplyZone or Save.' }
        [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $snapped = Get-AcceptedPlacementTransformFromGizmo -Context $Context
        if (-not (Test-NumericArraysEqual -Left @($snapped.translation_m) -Right @(-82.0,[double]$source.translation_m[1],85.0)) -or
            -not (Test-NumericArraysEqual -Left @($snapped.rotation) -Right @($source.rotation)) -or
            -not (Test-NumericArraysEqual -Left @($snapped.scale) -Right @($source.scale))) {
            throw 'One-metre world-grid snap did not round X/Z while preserving Y, rotation, and scale.'
        }
        [void](Invoke-UndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $snapUndone = Get-AcceptedPlacementTransformFromGizmo -Context $Context
        if (-not (Test-NumericArraysEqual -Left @($snapUndone.translation_m) -Right @($source.translation_m)) -or
            -not (Test-NumericArraysEqual -Left @($snapUndone.rotation) -Right @($source.rotation)) -or
            -not (Test-NumericArraysEqual -Left @($snapUndone.scale) -Right @($source.scale))) { throw 'Undo after world-grid snap did not restore the exact source transform.' }
        $gridChoice = Set-UiControlByLabel -Socket $socket -Label 'Aligner X/Z sur la grille mondiale' -Value $false -Kind checked -DetailsSummary 'Position précise et duplication'
        $surfaceChoice = Set-UiControlByLabel -Socket $socket -Label 'Poser le pivot sur le terrain' -Value $false -Kind checked -DetailsSummary 'Position précise et duplication'
        $pivotX = [double]$source.translation_m[0] + 1.0
        [void](Open-DetailsBySummary -Socket $socket -Summary 'Pivot de rotation (saisie et anneaux)')
        $pivotMode = Set-UiControlByLabel -Socket $socket -Label "Point monde précis (sinon pivot de l’objet)" -Value $true -Kind checked -DetailsSummary 'Pivot de rotation (saisie et anneaux)'
        $pivotControls = @(
            Set-UiControlByLabel -Socket $socket -Label 'Pivot monde X (m)' -Value $pivotX.ToString('R',$invariant) -DetailsSummary 'Pivot de rotation (saisie et anneaux)'
            Set-UiControlByLabel -Socket $socket -Label 'Pivot monde Y (m)' -Value ([double]$source.translation_m[1]).ToString('R',$invariant) -DetailsSummary 'Pivot de rotation (saisie et anneaux)'
            Set-UiControlByLabel -Socket $socket -Label 'Pivot monde Z (m)' -Value ([double]$source.translation_m[2]).ToString('R',$invariant) -DetailsSummary 'Pivot de rotation (saisie et anneaux)'
        )
        [void](Open-DetailsBySummary -Socket $socket -Summary 'Rotation et échelle précises')
        $axisChoice = Set-UiControlByLabel -Socket $socket -Label 'Axe de rotation' -Value 'y' -DetailsSummary 'Rotation et échelle précises'
        $frameChoice = Set-UiControlByLabel -Socket $socket -Label "Axe local de l’objet (sinon axe mondial)" -Value $false -Kind checked -DetailsSummary 'Rotation et échelle précises'
        $angleChoice = Set-UiControlByLabel -Socket $socket -Label 'Angle relatif (degrés)' -Value '180' -DetailsSummary 'Rotation et échelle précises'
        $beforePreview = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Prévisualiser la rotation')
        $preview = Wait-NewPreview -Socket $socket -PreviousCount ([int]$beforePreview.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds
        if ([int]$preview.audit_counts.apply_zone -ne [int]$beforePreview.audit_counts.apply_zone -or [int]$preview.audit_counts.save -ne [int]$beforePreview.audit_counts.save) { throw 'External-pivot preview emitted ApplyZone or Save.' }
        $previewScreenshot = Save-WorldScreenshot -Context $Context -Name 'r2-048-external-pivot-preview'
        [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $target = Get-AcceptedPlacementTransformFromGizmo -Context $Context
        $expectedTranslation = @(($pivotX + 1.0),[double]$source.translation_m[1],[double]$source.translation_m[2])
        $rotationPositive = Test-NumericArraysEqual -Left @($target.rotation) -Right @(0.0,1.0,0.0,0.0) -Tolerance 0.0001
        $rotationNegative = Test-NumericArraysEqual -Left @($target.rotation) -Right @(0.0,-1.0,0.0,0.0) -Tolerance 0.0001
        if (-not (Test-NumericArraysEqual -Left @($target.translation_m) -Right $expectedTranslation -Tolerance 0.0001) -or
            (-not $rotationPositive -and -not $rotationNegative) -or
            -not (Test-NumericArraysEqual -Left @($target.scale) -Right @($source.scale) -Tolerance 0.0001)) {
            throw 'External world-pivot rotation did not produce the exact arc, quaternion, and unchanged scale.'
        }
        [void](Invoke-UndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $undone = Get-AcceptedPlacementTransformFromGizmo -Context $Context
        if (-not (Test-NumericArraysEqual -Left @($undone.translation_m) -Right @($source.translation_m)) -or -not (Test-NumericArraysEqual -Left @($undone.rotation) -Right @($source.rotation)) -or -not (Test-NumericArraysEqual -Left @($undone.scale) -Right @($source.scale))) { throw 'Undo did not restore the exact source transform.' }
        [void](Invoke-UndoRedoKey -Socket $socket -Action Redo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $redone = Get-AcceptedPlacementTransformFromGizmo -Context $Context
        if (-not (Test-NumericArraysEqual -Left @($redone.translation_m) -Right @($target.translation_m)) -or -not (Test-NumericArraysEqual -Left @($redone.rotation) -Right @($target.rotation)) -or -not (Test-NumericArraysEqual -Left @($redone.scale) -Right @($target.scale))) { throw 'Redo did not restore the exact external-pivot target.' }
        [void](Invoke-UiClick -Socket $socket -Text 'Cadrer cet objet dans la carte')
        Start-Sleep -Milliseconds 750
        # The accepted transform is durable, but the inspector's pivot is an
        # operation setting and resets when Undo/Redo replaces the zone head.
        # Re-enter the exact external pivot for the focused visual inspection.
        [void](Open-DetailsBySummary -Socket $socket -Summary 'Pivot de rotation (saisie et anneaux)')
        [void](Set-UiControlByLabel -Socket $socket -Label "Point monde précis (sinon pivot de l’objet)" -Value $true -Kind checked -DetailsSummary 'Pivot de rotation (saisie et anneaux)')
        [void](Set-UiControlByLabel -Socket $socket -Label 'Pivot monde X (m)' -Value $pivotX.ToString('R',$invariant) -DetailsSummary 'Pivot de rotation (saisie et anneaux)')
        [void](Set-UiControlByLabel -Socket $socket -Label 'Pivot monde Y (m)' -Value ([double]$source.translation_m[1]).ToString('R',$invariant) -DetailsSummary 'Pivot de rotation (saisie et anneaux)')
        [void](Set-UiControlByLabel -Socket $socket -Label 'Pivot monde Z (m)' -Value ([double]$source.translation_m[2]).ToString('R',$invariant) -DetailsSummary 'Pivot de rotation (saisie et anneaux)')
        [void](Open-DetailsBySummary -Socket $socket -Summary 'Rotation et échelle précises')
        [void](Set-UiControlByLabel -Socket $socket -Label 'Axe de rotation' -Value 'y' -DetailsSummary 'Rotation et échelle précises')
        [void](Set-UiControlByLabel -Socket $socket -Label "Axe local de l’objet (sinon axe mondial)" -Value $false -Kind checked -DetailsSummary 'Rotation et échelle précises')
        [void](Set-UiControlByLabel -Socket $socket -Label 'Angle relatif (degrés)' -Value '180' -DetailsSummary 'Rotation et échelle précises')
        [void](Invoke-UiClick -Socket $socket -Text 'Afficher les anneaux de rotation X/Y/Z')
        $focusedDiagnostic = Wait-GizmoDiagnostic -Socket $socket -PlacementId $Context.PlacementId -Mode rotation -RotationSpace world
        if (-not (Test-NumericArraysEqual -Left @($focusedDiagnostic.pivot_world) -Right @($pivotX,[double]$source.translation_m[1],[double]$source.translation_m[2]) -Tolerance 0.0001)) { throw 'Focused rotation gizmo lost the exact external world pivot.' }
        $focusedScreenshot = Save-WorldScreenshot -Context $Context -Name 'r2-048-external-pivot-focused-redo'
        $beforeOrbit = Get-WorldState -Socket $socket
        $canvas = Get-CanvasGeometry -Socket $socket
        Send-CdpMouseDrag -Socket $socket -StartX ($canvas.center_x + 180) -StartY ($canvas.center_y + 130) -EndX ($canvas.center_x + 40) -EndY ($canvas.center_y + 130) -Steps 5 -Button right
        Start-Sleep -Milliseconds 750
        $afterOrbit = Get-WorldState -Socket $socket
        Assert-NoMutationIpcDelta -Before $beforeOrbit -After $afterOrbit -Description 'R2-048 read-only camera orbit'
        [void](Invoke-UiClick -Socket $socket -Text 'Afficher les anneaux de rotation X/Y/Z')
        $orbitDiagnostic = Wait-GizmoDiagnostic -Socket $socket -PlacementId $Context.PlacementId -Mode rotation -RotationSpace world
        $orbitScreenshot = Save-WorldScreenshot -Context $Context -Name 'r2-048-external-pivot-orbit-redo'
        $canvas = Get-CanvasGeometry -Socket $socket
        Send-CdpMouseDrag -Socket $socket -StartX ($canvas.center_x + 180) -StartY ($canvas.center_y + 130) -EndX ($canvas.center_x + 80) -EndY ($canvas.center_y + 130) -Steps 5 -Button middle
        Start-Sleep -Milliseconds 750
        [void](Invoke-UiClick -Socket $socket -Text 'Afficher les anneaux de rotation X/Y/Z')
        $panDiagnostic = Wait-GizmoDiagnostic -Socket $socket -PlacementId $Context.PlacementId -Mode rotation -RotationSpace world
        $panScreenshot = Save-WorldScreenshot -Context $Context -Name 'r2-048-external-pivot-orbit-pan-redo'
        $afterPan = Get-WorldState -Socket $socket
        Assert-NoMutationIpcDelta -Before $beforeOrbit -After $afterPan -Description 'R2-048 read-only camera orbit and pan'
        Send-CdpEscape -Socket $socket
        [void](Wait-CleanPreviewState -Socket $socket)
        $Context.TransversalR2048Transform = $redone
        return [pscustomobject]@{ units='metres/degrees'; frame='world'; snap=[pscustomobject]@{ enabled=$snapGridChoice; step=$snapStepChoice; surface=$snapSurfaceChoice; draft=$snapDraft; applied=$snapped; undo=$snapUndone; disabled_before_rotation=$gridChoice; surface_before_rotation=$surfaceChoice }; pivot_mode=$pivotMode; pivot_controls=$pivotControls; axis=$axisChoice; frame_control=$frameChoice; angle=$angleChoice; source=$source; pivot_world=@($pivotX,[double]$source.translation_m[1],[double]$source.translation_m[2]); target=$target; undo=$undone; redo=$redone; focused_diagnostic=$focusedDiagnostic; orbit_diagnostic=$orbitDiagnostic; pan_diagnostic=$panDiagnostic; preview_screenshot=$previewScreenshot; focused_redo_screenshot=$focusedScreenshot; orbit_redo_screenshot=$orbitScreenshot; orbit_pan_redo_screenshot=$panScreenshot }
    }
    if (-not $casePassed) { return @(Complete-CriterionSet -Criteria $criteria) }

    $casePassed = Invoke-CriterionCase -Criterion $criteria['R2-051'] -Name 'physical_typed_equivalence_and_one_shot_retry' -Action {
        if ($null -eq $Context.TransversalR2048Transform) { throw 'R2-048 accepted transform is required before R2-051.' }
        [void](Select-UiOptionByLabel -Socket $socket -Label 'Objet placé à inspecter' -Value $Context.PlacementId)
        [void](Open-DetailsBySummary -Socket $socket -Summary 'Position précise et duplication')
        $equivalenceGridChoice = Set-UiControlByLabel -Socket $socket -Label 'Aligner X/Z sur la grille mondiale' -Value $false -Kind checked -DetailsSummary 'Position précise et duplication'
        $equivalenceSurfaceChoice = Set-UiControlByLabel -Socket $socket -Label 'Poser le pivot sur le terrain' -Value $false -Kind checked -DetailsSummary 'Position précise et duplication'
        $equivalenceFrameChoice = Set-UiControlByLabel -Socket $socket -Label 'Déplacement selon les axes locaux' -Value $false -Kind checked
        $physical = Invoke-GizmoGesture -Context $Context -Mode translation -Axis x -LeavePreview
        $physicalTransform = $physical.transient.current_transform
        $physicalDiff = [string]$physical.final_state.diff_text
        $physicalSnapshot = Get-WorldAuditSnapshot -Socket $socket
        $physicalEntry = Get-RequiredAuditedEntry -Snapshot $physicalSnapshot -RequestKind 'preview_zone'
        $physicalReceipt = Get-RequiredAuditReceipt -Entry $physicalEntry
        if (@($physicalEntry.attempts).Count -ne 1) { throw 'Physical preview unexpectedly used more than one transport attempt.' }
        Send-CdpEscape -Socket $socket
        [void](Wait-CleanPreviewState -Socket $socket)

        $coordinateLabels = @('X (m)','Y (m)','Z (m)')
        for ($index = 0; $index -lt 3; $index += 1) {
            $typed = ([double]$physicalTransform.translation_m[$index]).ToString('R',$invariant)
            [void](Set-UiControlByLabelWithKeyboard -Socket $socket -Label $coordinateLabels[$index] -Text $typed -DetailsSummary 'Position précise et duplication')
        }
        $beforeTyped = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Prévisualiser le déplacement')
        $typedState = Wait-NewPreview -Socket $socket -PreviousCount ([int]$beforeTyped.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds
        $typedSnapshot = Get-WorldAuditSnapshot -Socket $socket
        $typedEntry = Get-RequiredAuditedEntry -Snapshot $typedSnapshot -RequestKind 'preview_zone'
        $typedReceipt = Get-RequiredAuditReceipt -Entry $typedEntry
        if ([string]$physicalEntry.request_sha256 -cne [string]$typedEntry.request_sha256 -or
            [string]$physicalEntry.wire.request.request.command_id -cne [string]$typedEntry.wire.request.request.command_id -or
            $physicalDiff -cne [string]$typedState.diff_text -or
            -not (Test-AuditReceiptsEqual -Left $physicalReceipt -Right $typedReceipt)) {
            throw 'Physical gizmo and keyboard-entered transform did not produce the exact same request, diff, and normalized receipt.'
        }
        [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $typedApplied = Get-AcceptedPlacementTransformFromGizmo -Context $Context
        if (-not (Test-NumericArraysEqual -Left @($typedApplied.translation_m) -Right @($physicalTransform.translation_m)) -or
            -not (Test-NumericArraysEqual -Left @($typedApplied.rotation) -Right @($physicalTransform.rotation)) -or
            -not (Test-NumericArraysEqual -Left @($typedApplied.scale) -Right @($physicalTransform.scale))) { throw 'Typed Apply did not accept the exact physical transient transform.' }
        [void](Invoke-UndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $typedUndone = Get-AcceptedPlacementTransformFromGizmo -Context $Context
        if (-not (Test-NumericArraysEqual -Left @($typedUndone.translation_m) -Right @($Context.TransversalR2048Transform.translation_m)) -or
            -not (Test-NumericArraysEqual -Left @($typedUndone.rotation) -Right @($Context.TransversalR2048Transform.rotation))) { throw 'Undo after typed Apply did not restore the pre-gesture accepted transform.' }

        $duplicateX = [double]$typedUndone.translation_m[0] + 2.5
        $duplicateValues = @($duplicateX,[double]$typedUndone.translation_m[1],[double]$typedUndone.translation_m[2])
        for ($index = 0; $index -lt 3; $index += 1) {
            [void](Set-UiControlByLabelWithKeyboard -Socket $socket -Label $coordinateLabels[$index] -Text ([double]$duplicateValues[$index]).ToString('R',$invariant) -DetailsSummary 'Position précise et duplication')
        }
        $beforeDuplicate = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Prévisualiser une copie à ces coordonnées')
        [void](Wait-NewPreview -Socket $socket -PreviousCount ([int]$beforeDuplicate.audit_counts.preview_zone) -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        $duplicatePreviewSnapshot = Get-WorldAuditSnapshot -Socket $socket
        $duplicatePreviewEntry = Get-RequiredAuditedEntry -Snapshot $duplicatePreviewSnapshot -RequestKind 'preview_zone'
        $duplicatePreviewReceipt = Get-RequiredAuditReceipt -Entry $duplicatePreviewEntry
        $duplicateCommandId = [string]$duplicatePreviewEntry.wire.request.request.command_id
        $armedRetry = Arm-WorldAuditRetry -Socket $socket -RequestKind apply_zone -CommandId $duplicateCommandId
        [void](Invoke-ApplyCurrentPreview -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds -ExpectedPlacementCount ($initialPlacementIds.Count + 1))
        $retrySnapshot = Get-WorldAuditSnapshot -Socket $socket
        $retryEntry = Get-RequiredAuditedEntry -Snapshot $retrySnapshot -RequestKind 'apply_zone'
        if (-not [bool]$retryEntry.synthetic_retry -or @($retryEntry.attempts).Count -ne 2 -or
            $null -eq $retrySnapshot.retry_next -or -not [bool]$retrySnapshot.retry_next.matched -or [bool]$retrySnapshot.retry_next.armed -or
            [int]$retrySnapshot.retry_next.matched_entry_index -ne [int]$retryEntry.index -or
            [string]$retrySnapshot.retry_next.request_kind -cne 'apply_zone' -or [string]$retrySnapshot.retry_next.command_id -cne $duplicateCommandId) {
            throw 'The exact one-shot ApplyZone transport retry was not consumed as two bounded attempts.'
        }
        $retryReceipt0 = Get-RequiredAuditReceipt -Entry $retryEntry -Attempt 0
        $retryReceipt1 = Get-RequiredAuditReceipt -Entry $retryEntry -Attempt 1
        if ([string]$retryReceipt0.command_id -cne $duplicateCommandId -or -not (Test-AuditReceiptsEqual -Left $duplicatePreviewReceipt -Right $retryReceipt0) -or -not (Test-AuditReceiptsEqual -Left $retryReceipt0 -Right $retryReceipt1)) {
            throw 'Preview, first Apply and retried Apply did not return the exact same idempotent receipt.'
        }
        $afterDuplicate = Get-TransversalState -Socket $socket
        $afterIds = @($afterDuplicate.placement_options | ForEach-Object { [string]$_.value })
        $newIds = @($afterIds | Where-Object { $initialPlacementIds -notcontains $_ })
        $missingOriginals = @($initialPlacementIds | Where-Object { $afterIds -notcontains $_ })
        if ($afterIds.Count -ne ($initialPlacementIds.Count + 1) -or $newIds.Count -ne 1 -or $missingOriginals.Count -ne 0) { throw 'Retried duplicate did not preserve every original and add exactly one placement.' }
        [void](Invoke-UndoRedoKey -Socket $socket -Action Undo -TimeoutSeconds $Context.InteractionTimeoutSeconds)
        [void](Wait-ProjectionsReady -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds -ExpectedPlacementCount $initialPlacementIds.Count)
        $afterUndo = Get-TransversalState -Socket $socket
        $afterUndoIds = @($afterUndo.placement_options | ForEach-Object { [string]$_.value })
        if (($afterUndoIds | Sort-Object) -join '|' -cne (($initialPlacementIds | Sort-Object) -join '|')) { throw 'Undo after retried duplicate did not restore the exact original placement-id set.' }
        $Context.TransversalDuplicateBreadth = [pscustomobject]@{ original_count=$initialPlacementIds.Count; all_original_ids_preserved=$true; new_id=$newIds[0]; apply_command_id=$duplicateCommandId; audit_entry_index=$retryEntry.index; attempts=2; exact_receipt_replayed=$true; undo_restored_exact_set=$true }
        return [pscustomobject]@{ controls=[pscustomobject]@{ grid=$equivalenceGridChoice; surface=$equivalenceSurfaceChoice; frame=$equivalenceFrameChoice }; physical_request_sha256=$physicalEntry.request_sha256; typed_request_sha256=$typedEntry.request_sha256; shared_command_id=$physicalEntry.wire.request.request.command_id; shared_receipt=$typedReceipt; physical_transform=$physicalTransform; typed_apply=$typedApplied; typed_undo=$typedUndone; retry_armed=$armedRetry; duplicate=$Context.TransversalDuplicateBreadth; synthetic_transport_fault='one exact ApplyZone request invoked twice; no product test hook' }
    }
    if (-not $casePassed) { return @(Complete-CriterionSet -Criteria $criteria) }

    $casePassed = Invoke-CriterionCase -Criterion $criteria['R2-046'] -Name 'non_layer_placement_identity_breadth' -Action {
        if ($null -eq $Context.TransversalDuplicateBreadth -or -not $Context.TransversalDuplicateBreadth.all_original_ids_preserved -or -not $Context.TransversalDuplicateBreadth.undo_restored_exact_set) { throw 'R2-046 non-layer stable-id breadth was not established by the retried placement duplication.' }
        return $Context.TransversalDuplicateBreadth
    }
    if (-not $casePassed) { return @(Complete-CriterionSet -Criteria $criteria) }

    $casePassed = Invoke-CriterionCase -Criterion $criteria['R2-042'] -Name 'stop_diagnostics_render_after_return_to_edit' -Action {
        $archiveBeforePlaytest = Get-ArchiveSnapshot -LiteralPath $Context.ArchivePath
        $beforePlaytest = Get-WorldState -Socket $socket
        [void](Invoke-UiClick -Socket $socket -Text 'Jouer')
        $startReloadAudit = Install-WorldAuditAfterOwnedReload -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds
        $started = Wait-WorldState -Socket $socket -Description 'Transversal playtest start' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state)
            [int]$state.audit_counts.start_playtest -eq ([int]$beforePlaytest.audit_counts.start_playtest + 1) -and
                [int]$state.audit_pending -eq 0 -and [string]$state.command_status -ceq 'Playtest · sources gelées, aucune sauvegarde implicite'
        }
        $initialPlay = Wait-PlaytestObservation -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds
        $canvas = Get-CanvasGeometry -Socket $socket
        Send-CdpMouseClick -Socket $socket -X $canvas.center_x -Y $canvas.center_y
        [void](Wait-WorldState -Socket $socket -Description 'Transversal playtest pointer lock' -TimeoutSeconds 10 -Predicate { param($state) [string]$state.pointer_lock_id -ceq 'forge-authoring-canvas' })
        Send-CdpKeyHold -Socket $socket -Key 'w' -Code 'KeyW' -VirtualKeyCode 87 -Milliseconds 600
        $moved = Wait-PlaytestObservation -Socket $socket -AfterTick $initialPlay.Tick -TimeoutSeconds 15
        Send-CdpEscape -Socket $socket
        [void](Wait-WorldState -Socket $socket -Description 'Transversal pointer lock released' -TimeoutSeconds 10 -Predicate { param($state) [string]::IsNullOrEmpty([string]$state.pointer_lock_id) })
        $beforeStop = Get-WorldState -Socket $socket
        $liveAtStop = Parse-PlaytestStatus -Text ([string]$beforeStop.playtest_status)
        [void](Invoke-UiClick -Socket $socket -Text "Retour à l’édition")
        $stopReloadAudit = Install-WorldAuditAfterOwnedReload -Socket $socket -TimeoutSeconds $Context.InteractionTimeoutSeconds
        $returned = Wait-WorldState -Socket $socket -Description 'Transversal playtest return diagnostics' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state)
            [int]$state.audit_counts.stop_playtest -eq ([int]$beforeStop.audit_counts.stop_playtest + 1) -and
                [int]$state.audit_pending -eq 0 -and [string]$state.mode -like 'Mode édition*' -and
                [string]$state.last_playtest_status -like 'Dernier playtest ·*'
        }
        $stopSnapshot = Get-WorldAuditSnapshot -Socket $socket
        $stopEntry = Get-RequiredAuditedEntry -Snapshot $stopSnapshot -RequestKind 'stop_playtest'
        $diagnostics = $stopEntry.wire.request.diagnostics
        if ($null -eq $diagnostics) { throw 'StopPlaytest did not carry exit diagnostics.' }
        if ([int64]$diagnostics.fixed_tick -lt [int64]$liveAtStop.Tick -or [int64]$diagnostics.fixed_tick -le 0 -or
            -not [bool]$diagnostics.grounded -or -not [bool]$diagnostics.simulation_ready -or [int]$diagnostics.collisions -lt 1 -or
            [string]$diagnostics.end_reason -cne 'retour_edition' -or [string]::IsNullOrWhiteSpace([string]$diagnostics.runtime_status)) {
            throw 'StopPlaytest diagnostics are not the exact ready, grounded, contacted runtime observation at return time.'
        }
        foreach ($coordinate in @($diagnostics.final_position_m)) {
            $number = [double]$coordinate
            if ([double]::IsNaN($number) -or [double]::IsInfinity($number)) { throw 'StopPlaytest final position contains a non-finite coordinate.' }
        }
        $startSnapshot = Get-RequiredAuditedEntry -Snapshot $stopSnapshot -RequestKind 'start_playtest'
        if ([string]$diagnostics.identity.source_hash -cne [string]$startSnapshot.wire.request.expected_hash) { throw 'StopPlaytest diagnostics are not bound to the exact started source hash.' }
        $formattedPosition = (@($diagnostics.final_position_m) | ForEach-Object { ([double]$_).ToString('0.00',$invariant) }) -join ', '
        foreach ($needle in @("tick $($diagnostics.fixed_tick)","position finale ($formattedPosition) m",'au sol true','simulation prête true',"$($diagnostics.collisions) contact(s)",'retour_edition',[string]$diagnostics.runtime_status)) {
            if (-not ([string]$returned.last_playtest_status).Contains([string]$needle)) { throw "Rendered last-playtest diagnostics omitted '$needle'." }
        }
        Assert-NoMutationIpcDelta -Before $beforePlaytest -After $returned -Description 'R2-042 playtest round trip'
        $archiveAfterPlaytest = Get-ArchiveSnapshot -LiteralPath $Context.ArchivePath
        if (-not (Test-ArchiveSnapshotsEqual -Before $archiveBeforePlaytest -After $archiveAfterPlaytest)) { throw 'Playtest and returned diagnostics changed the isolated authoring archive.' }
        return [pscustomobject]@{ start_reload_audit=$startReloadAudit; stop_reload_audit=$stopReloadAudit; initial=$initialPlay; moved=$moved; live_at_stop=$liveAtStop; stop_request_diagnostics=$diagnostics; rendered=$returned.last_playtest_status; source_identity_exact=$true; archive_unchanged=$true; screenshot=Save-WorldScreenshot -Context $Context -Name 'r2-042-return-diagnostics' }
    }
    if (-not $casePassed) { return @(Complete-CriterionSet -Criteria $criteria) }

    $casePassed = Invoke-CriterionCase -Criterion $criteria['R2-037'] -Name 'authoring_history_real_summaries_and_pagination' -Action {
        $expectedNewCommands = 18
        $expectedTotal = 44
        $beforeHistory = Get-WorldState -Socket $socket
        if ([int]$beforeHistory.audit_counts.apply_zone -ne $expectedNewCommands) { throw "Transversal scenario expected $expectedNewCommands accepted ApplyZone commands before history, got $($beforeHistory.audit_counts.apply_zone)." }
        [void](Invoke-UiClick -Socket $socket -Text "Afficher l’historique des retouches")
        $page0 = Wait-TransversalState -Socket $socket -Description 'Authoring history first page' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state) [string]$state.history_status -like "$expectedTotal commande(s)*" -and @($state.history_entries).Count -eq 32 -and $state.history_older_disabled -eq $false
        }
        $afterPage0 = Get-WorldState -Socket $socket
        if ([int]$afterPage0.audit_counts.read_history -ne ([int]$beforeHistory.audit_counts.read_history + 1)) { throw 'Opening authoring history did not issue exactly one offset-zero read.' }
        $newest = @($page0.history_entries | Select-Object -First $expectedNewCommands)
        $newestIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($entry in $newest) {
            if ([string]$entry.identity -cnotmatch '^(?<id>[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}) · (?<hash>[0-9a-f]{64})$') { throw "History command identity is not bounded UUID plus SHA-256: $($entry.identity)" }
            if (-not $newestIds.Add([string]$Matches.id)) { throw "History repeated command id $($Matches.id)." }
            $prefix = 'Auteur humain : gate2-transversal-v8-cdp-proof · '
            if ([string]$entry.audit -notlike "$prefix*") { throw "New history entry has the wrong audit actor: $($entry.audit)" }
            $timestampText = ([string]$entry.audit).Substring($prefix.Length)
            try { $timestamp = [DateTimeOffset]::Parse($timestampText,$invariant,[Globalization.DateTimeStyles]::RoundtripKind) }
            catch { throw "New history entry has an invalid RFC3339 timestamp: $timestampText" }
            if ($timestamp -lt $Context.StartedAtUtc.AddSeconds(-1) -or $timestamp -gt [DateTimeOffset]::UtcNow.AddSeconds(5)) { throw "History timestamp is outside the owned scenario interval at the native adapter's one-second precision: $timestampText" }
        }
        $newLabels = @($newest | ForEach-Object { [string]$_.label }) -join "`n"
        foreach ($action in @('Retouche','Régénération','Annulation','Rétablissement')) {
            if ($newLabels -notlike "*$action*") { throw "History summaries omitted the real '$action' action." }
        }
        $page0Identity = @($page0.history_entries | ForEach-Object { [string]$_.identity })
        [void](Invoke-UiClick -Socket $socket -Text 'Plus anciennes' -ScopeSelector 'section[aria-label="Historique des retouches"]')
        $page32 = Wait-TransversalState -Socket $socket -Description 'Authoring history older page' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state) @($state.history_entries).Count -eq 12 -and [string]$state.history_entries[0].identity -cne $page0Identity[0] -and $state.history_older_disabled -eq $true
        }
        $page32Identity = @($page32.history_entries | ForEach-Object { [string]$_.identity })
        if (@($page32Identity | Where-Object { $page0Identity -contains $_ }).Count -ne 0) { throw 'Authoring history pages overlap.' }
        [void](Invoke-UiClick -Socket $socket -Text 'Plus récentes' -ScopeSelector 'section[aria-label="Historique des retouches"]')
        $page0Again = Wait-TransversalState -Socket $socket -Description 'Authoring history first page restored' -TimeoutSeconds $Context.InteractionTimeoutSeconds -Predicate {
            param($state) @($state.history_entries).Count -eq 32 -and [string]$state.history_entries[0].identity -ceq $page0Identity[0] -and $state.history_newer_disabled -eq $true
        }
        $afterHistory = Get-WorldState -Socket $socket
        if ([int]$afterHistory.audit_counts.read_history -ne ([int]$beforeHistory.audit_counts.read_history + 3)) { throw 'Authoring history navigation did not issue exact offsets 0, 32, 0.' }
        Assert-NoMutationIpcDelta -Before $beforeHistory -After $afterHistory -Description 'R2-037 authoring history navigation'
        $historySnapshot = Get-WorldAuditSnapshot -Socket $socket
        $historyReads = @(Get-AuditedAuthoringEntries -Snapshot $historySnapshot -RequestKind 'read_history')
        if ($historyReads.Count -ne 3 -or [int]$historyReads[0].wire.request.offset -ne 0 -or [int]$historyReads[1].wire.request.offset -ne 32 -or [int]$historyReads[2].wire.request.offset -ne 0) { throw 'Audited authoring history offsets are not exactly 0, 32, 0.' }
        $archiveAfter = Get-ArchiveSnapshot -LiteralPath $Context.ArchivePath
        if (-not (Test-ArchiveSnapshotsEqual -Before $Context.ArchiveBefore -After $archiveAfter)) { throw 'Transversal unsaved operations or history reads changed authoring.json.' }
        return [pscustomobject]@{ total=$expectedTotal; page_size=32; older_page_size=12; offsets=@(0,32,0); newest_scenario_entries=$expectedNewCommands; newest_command_ids=@($newestIds); pages_disjoint=$true; human_subject='gate2-transversal-v8-cdp-proof'; archive_unchanged=$true; criterion_complete_in_scenario=$false; open_gap='Workspace-history GUI multipage is not exercised by this zone fixture.'; screenshot=Save-WorldScreenshot -Context $Context -Name 'r2-037-authoring-history-page-0' }
    }

    return @(Complete-CriterionSet -Criteria $criteria)
}

function Invoke-StudioScenario {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Candidate,
        [Parameter(Mandatory)][string]$ExpectedHash,
        [Parameter(Mandatory)][string]$SourceRoot,
        [Parameter(Mandatory)][object]$SourceArchiveBefore,
        [Parameter(Mandatory)][string]$ZoneId,
        [Parameter(Mandatory)][string]$PlacementId,
        [Parameter(Mandatory)][string]$BuildingId,
        [Parameter(Mandatory)][string]$RootOutput,
        [Parameter(Mandatory)][int]$StartupTimeout,
        [Parameter(Mandatory)][int]$InteractionTimeout,
        [Parameter(Mandatory)][scriptblock]$Runner,
        [switch]$AssetPreviewDelay
    )
    $evidence = Join-Path $RootOutput $Name
    [void](New-Item -ItemType Directory -Path $evidence)
    $fixture = Join-Path $evidence 'authoring-root'
    $archivePath = Copy-IsolatedFixture -SourceRoot $SourceRoot -DestinationRoot $fixture -ZoneId $ZoneId
    $archiveBefore = Get-ArchiveSnapshot -LiteralPath $archivePath
    if ($archiveBefore.Sha256 -cne $SourceArchiveBefore.Sha256 -or $archiveBefore.Bytes -ne $SourceArchiveBefore.Bytes) {
        throw "$Name fixture copy does not match source archive hash/bytes."
    }
    $userData = Join-Path $evidence 'webview2-user-data'
    [void](New-Item -ItemType Directory -Path $userData)
    Assert-TreeHasNoReparsePoints -LiteralPath $userData -Description "$Name WebView2 user data"
    $startedAt = [DateTimeOffset]::UtcNow
    $context = [pscustomobject]@{
        Name = $Name
        Socket = $null
        EvidenceDirectory = $evidence
        FixtureRoot = $fixture
        ArchivePath = $archivePath
        ArchiveBefore = $archiveBefore
        SourceArchiveBefore = $SourceArchiveBefore
        ZoneId = $ZoneId
        PlacementId = $PlacementId
        BuildingId = $BuildingId
        InteractionTimeoutSeconds = $InteractionTimeout
        StartedAtUtc = $startedAt
        Screenshots = [System.Collections.Generic.List[object]]::new()
        Criteria = @()
        InitialUi = $null
        AuditProbe = $null
        AuditSnapshot = $null
        AuditRestored = $false
        BuildingGeometry = $null
        PostSaveArchive = $null
        ConfirmedPlacementPickPoint = $null
        TransversalR2048Transform = $null
        TransversalDuplicateBreadth = $null
    }
    $process = $null
    $socket = $null
    $reservation = $null
    $target = $null
    $devToolsPort = 0
    $auditInstalled = $false
    $proofFailure = $null
    $processClosed = $false
    $gracefulCloseRequested = $false
    $gracefulCloseAccepted = $false
    $forcedOwnPid = $false
    $listenerClosed = $false
    $environmentRestored = $false
    $trackedEnvironment = @('FORGE_AUTHORING_ROOT','FORGE_AUTHOR_SUBJECT','WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS','WEBVIEW2_USER_DATA_FOLDER')
    $previousEnvironment = @{}
    foreach ($variable in $trackedEnvironment) { $previousEnvironment[$variable] = [Environment]::GetEnvironmentVariable($variable, 'Process') }
    try {
        $reservation = New-LoopbackPortReservation
        $devToolsPort = $reservation.Port
        [Environment]::SetEnvironmentVariable('FORGE_AUTHORING_ROOT', $fixture, 'Process')
        [Environment]::SetEnvironmentVariable('FORGE_AUTHOR_SUBJECT', "gate2-$Name-cdp-proof", 'Process')
        [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', "--remote-debugging-address=127.0.0.1 --remote-debugging-port=$devToolsPort", 'Process')
        [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER', $userData, 'Process')
        try {
            $reservation.Listener.Stop()
            $process = Start-Process -FilePath $Candidate -PassThru -WindowStyle Hidden
        }
        finally { $reservation.Listener.Stop() }
        $launchIdentity = Wait-LaunchedExecutableIdentity -Process $process -Candidate $Candidate -ExpectedHash $ExpectedHash -TimeoutSeconds 5
        $launchedPath = $launchIdentity.Path
        $target = Wait-CdpTarget -Port $devToolsPort -TimeoutSeconds $StartupTimeout -Process $process
        $socket = Connect-CdpSocket -WebSocketUrl ([string]$target.webSocketDebuggerUrl)
        $context.Socket = $socket
        [void](Invoke-CdpCommand -Socket $socket -Method 'Runtime.enable')
        [void](Invoke-CdpCommand -Socket $socket -Method 'Page.enable')
        [void](Invoke-CdpCommand -Socket $socket -Method 'Page.bringToFront')
        if ($Name -ceq 'full-author-route') {
            $context.InitialUi = Open-FullAuthorRouteZone -Socket $socket -ZoneId $ZoneId -TimeoutSeconds $InteractionTimeout
        }
        else {
            $context.InitialUi = Open-ExactAuthoringZone -Socket $socket -ZoneId $ZoneId -PlacementId $PlacementId -BuildingId $BuildingId -TimeoutSeconds $InteractionTimeout
        }
        [void](Install-WorldAudit -Socket $socket -AssetPreviewDelay:$AssetPreviewDelay)
        $auditInstalled = $true
        $context.AuditProbe = Invoke-ReadOnlyAuditProbe -Socket $socket -TimeoutSeconds $InteractionTimeout
        $context.Criteria = @(& $Runner $context)
        $context.AuditSnapshot = Get-WorldAuditSnapshot -Socket $socket
        [void](Restore-WorldAudit -Socket $socket)
        $context.AuditRestored = $true
        $auditInstalled = $false
        if ($Name -ceq 'full-author-route') {
            $ux = [System.Collections.Generic.List[object]]::new()
            foreach ($tabLabel in @('Projet','Terrain','Nature','Tracés & eau','Objets','Tester & suivi')) {
                [void](Invoke-UiClick -Socket $socket -Text $tabLabel -Selector '[data-authoring-tab-button]')
                [void](Assert-EditorNavigation -Socket $socket)
                $panel = Invoke-CdpExpression -Socket $socket -Expression @'
(async () => {
  await new Promise(resolve=>setTimeout(resolve,200));
  const root=document.querySelector('.authoring-panel-scroll');
  const panels=[...root.querySelectorAll('[data-authoring-tab]')].filter(e=>!e.hidden);
  const keys=[...new Set(panels.map(e=>e.dataset.authoringTab))];
  return {ready: keys.length===1 && root.scrollTop===0 && root.scrollWidth<=root.clientWidth+1, tab:keys[0], scrollTop:root.scrollTop, horizontalOverflow:root.scrollWidth-root.clientWidth};
})()
'@
                $ux.Add($panel)
                if (-not $panel.ready) { throw "Editor panel layout failed: $(ConvertTo-CompactJson -Value $panel)" }
                [void](Save-WorldScreenshot -Context $context -Name ("ux-" + $panel.tab))
            }
            [void](Invoke-UiClick -Socket $socket -Text 'Terrain' -Selector '[data-authoring-tab-button]')
            [void](Invoke-UiClick -Socket $socket -Text 'Tracer dans la vue')
            $mode = Invoke-CdpExpression -Socket $socket -Expression "(() => {const e=document.querySelector('.authoring-tool-mode');return {ready:!!e && !e.hidden && e.textContent.includes('Tracer'), text:e?.textContent};})()"
            if (-not $mode.ready) { throw 'Armed brush mode is not visible in the viewport.' }
            [void](Save-WorldScreenshot -Context $context -Name 'ux-brush-mode')
            Send-CdpKeyStroke -Socket $socket -Key 'Escape' -Code 'Escape' -VirtualKeyCode 27
            $cancel = Invoke-CdpExpression -Socket $socket -Expression "(() => ({ready:document.querySelector('.authoring-tool-mode')?.hidden===true}))()"
            if (-not $cancel.ready) { throw 'Escape left a stale brush mode indicator.' }
            [void](Invoke-CdpCommand -Socket $socket -Method 'Emulation.setEmulatedMedia' -Parameters @{ features=@(@{name='prefers-reduced-motion';value='reduce'}) })
            $motion = Invoke-CdpExpression -Socket $socket -Expression "(() => {const e=document.querySelector('[data-authoring-tab=terrain]');return {ready:matchMedia('(prefers-reduced-motion: reduce)').matches && getComputedStyle(e).animationName==='none', animation:getComputedStyle(e).animationName};})()"
            if (-not $motion.ready) { throw 'Editor reduced-motion preference was not respected.' }
            [void](Invoke-CdpCommand -Socket $socket -Method 'Emulation.setEmulatedMedia' -Parameters @{ features=@() })
            [System.IO.File]::WriteAllText((Join-Path $evidence 'ux-checks.json'), (@{panels=@($ux);reduced_motion=$motion;brush_mode=$mode;escape=$cancel;navigation='visible after history scrollIntoView'} | ConvertTo-Json -Depth 10), [System.Text.UTF8Encoding]::new($false))
        }
        [void](Save-WorldScreenshot -Context $context -Name "$Name-final")
    }
    catch {
        $proofFailure = $_.Exception.Message
        if ($null -ne $socket -and $socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
            try {
                [void](Save-WorldScreenshot -Context $context -Name "$Name-failure")
                $layout = Invoke-CdpExpression -Socket $socket -Expression @'
(() => [...document.querySelectorAll('.authoring-tools,.authoring-editor,[aria-label="Édition terrain"],.authoring-toolbar,.authoring-panel-scroll')].map(e=>({tag:e.tagName,cls:e.className,height:e.clientHeight,scrollHeight:e.scrollHeight,scrollTop:e.scrollTop,rect:e.getBoundingClientRect().toJSON(),display:getComputedStyle(e).display})))()
'@
                [System.IO.File]::WriteAllText((Join-Path $evidence 'failure-layout.json'), ($layout | ConvertTo-Json -Depth 8), [System.Text.UTF8Encoding]::new($false))
            } catch { Write-Warning "Failure capture unavailable: $($_.Exception.Message)" }
        }
    }
    finally {
        if ($auditInstalled -and $null -ne $socket -and $socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
            try {
                $context.AuditSnapshot = Get-WorldAuditSnapshot -Socket $socket
                [void](Restore-WorldAudit -Socket $socket)
                $context.AuditRestored = $true
                $auditInstalled = $false
            }
            catch {
                if ($null -eq $proofFailure) { $proofFailure = $_.Exception.Message } else { $proofFailure += " | audit restore failed: $($_.Exception.Message)" }
            }
        }
        if ($null -ne $socket) {
            try {
                if ($socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
                    $closeTimeout = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(2))
                    try { Wait-NonGenericTask -Task ($socket.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'Gate 2 world proof complete', $closeTimeout.Token)) }
                    finally { $closeTimeout.Dispose() }
                }
            }
            catch { Write-Verbose "CDP websocket close failed: $($_.Exception.Message)" }
            finally { $socket.Dispose() }
        }
        foreach ($variable in $trackedEnvironment) { [Environment]::SetEnvironmentVariable($variable, $previousEnvironment[$variable], 'Process') }
        $environmentRestored = $true
        if ($null -ne $reservation) { try { $reservation.Listener.Stop() } catch { Write-Verbose $_.Exception.Message } }
        if ($null -ne $process -and -not $process.HasExited) {
            $gracefulCloseRequested = $true
            try { $gracefulCloseAccepted = $process.CloseMainWindow() } catch { Write-Verbose $_.Exception.Message }
            if (-not $process.WaitForExit(8000)) {
                try { Stop-Process -Id $process.Id -Force -ErrorAction Stop; $forcedOwnPid = $true }
                catch {
                    $process.Refresh()
                    if (-not $process.HasExited) {
                        if ($null -eq $proofFailure) { $proofFailure = "Unable to stop owned PID $($process.Id)." } else { $proofFailure += " | unable to stop owned PID $($process.Id)" }
                    }
                }
                if (-not $process.HasExited) { [void]$process.WaitForExit(5000) }
            }
        }
        if ($null -ne $process) { $processClosed = $process.HasExited }
        if ($devToolsPort -gt 0) { $listenerClosed = Wait-CdpListenerClosed -Port $devToolsPort -TimeoutSeconds 10 }
        if ($null -ne $process -and -not $processClosed) {
            if ($null -eq $proofFailure) { $proofFailure = 'Owned Studio process remained alive.' } else { $proofFailure += ' | owned Studio process remained alive' }
        }
        if ($devToolsPort -gt 0 -and -not $listenerClosed) {
            if ($null -eq $proofFailure) { $proofFailure = 'Owned CDP listener remained reachable.' } else { $proofFailure += ' | owned CDP listener remained reachable' }
        }
    }
    $archiveAfter = $null
    try { $archiveAfter = Get-ArchiveSnapshot -LiteralPath $archivePath }
    catch { if ($null -eq $proofFailure) { $proofFailure = $_.Exception.Message } else { $proofFailure += " | archive snapshot failed: $($_.Exception.Message)" } }
    $sourceAfter = Get-ArchiveSnapshot -LiteralPath $SourceArchiveBefore.Path
    $sourcePreserved = Test-ArchiveSnapshotsEqual -Before $SourceArchiveBefore -After $sourceAfter
    if (-not $sourcePreserved) {
        if ($null -eq $proofFailure) { $proofFailure = 'Source fixture archive changed.' } else { $proofFailure += ' | source fixture archive changed' }
    }
    $criteriaPassed = @($context.Criteria).Count -gt 0 -and @($context.Criteria | Where-Object { -not $_.Passed }).Count -eq 0
    $passed = [string]::IsNullOrWhiteSpace($proofFailure) -and $criteriaPassed -and $context.AuditRestored -and $processClosed -and $listenerClosed -and $environmentRestored
    $completedAt = [DateTimeOffset]::UtcNow
    $ledger = [ordered]@{
        schema = 'forge.gate2.world-editing-proof.scenario'
        schema_version = 1
        recorded_at_utc = $completedAt.ToString('o')
        gate = 'Gate 2'
        scenario = $Name
        coverage = @(Get-ScenarioCoverage -Name $Name)
        gate_qualified = $false
        human_review_performed = $false
        passed = $passed
        status = if ($passed) { 'automated_cdp_acquired' } else { 'failed_closed' }
        failure = $proofFailure
        started_at_utc = $startedAt.ToString('o')
        completed_at_utc = $completedAt.ToString('o')
        ci = [ordered]@{ status = $script:CiStatus }
        candidate = [ordered]@{ path=$Candidate; expected_sha256=$ExpectedHash; after_sha256=(Get-FileSha256 -LiteralPath $Candidate); exact_hash_verified=((Get-FileSha256 -LiteralPath $Candidate) -ceq $ExpectedHash) }
        fixture = [ordered]@{ source_root=$SourceRoot; source_archive_before=$SourceArchiveBefore; source_archive_after=$sourceAfter; source_hash_bytes_mtime_exact=$sourcePreserved; isolated_root=$fixture; isolated_archive_before=$archiveBefore; isolated_archive_after=$archiveAfter }
        identities = [ordered]@{ zone_uuid=$ZoneId; placement_uuid=$PlacementId; building_uuid=$BuildingId }
        target = if ($null -ne $target) { [ordered]@{ type=$target.type; title=$target.title; url=$target.url; websocket_url=$target.webSocketDebuggerUrl } } else { $null }
        initial_ui = $context.InitialUi
        audit_probe = $context.AuditProbe
        criteria = @($context.Criteria)
        invoke_audit = $context.AuditSnapshot
        fetch_audit_restored_exactly = $context.AuditRestored
        screenshots = @($context.Screenshots)
        cdp = [ordered]@{ request_count=$script:CdpMessageId; asynchronous_event_count=$script:CdpEvents.Count; dropped_event_count=$script:CdpDroppedEventCount; asynchronous_event_methods=@($script:CdpEvents | ForEach-Object { $_.Method } | Sort-Object -Unique) }
        cleanup = [ordered]@{ owned_process_id=if($null -ne $process){$process.Id}else{$null}; owned_process_closed=$processClosed; graceful_close_requested=$gracefulCloseRequested; graceful_close_accepted=$gracefulCloseAccepted; forced_only_owned_pid=$forcedOwnPid; environment_restored=$environmentRestored; cdp_listener_gone=$listenerClosed }
        limitations = @('Automated CDP evidence; not a human visual review.','Each scenario uses a fresh isolated fixture copy; the source fixture is read-only and verified afterward.','No Rust command is called directly; product mutations originate from Studio controls and physical CDP input.','gate_qualified remains false.', $script:CiStatus)
    }
    $ledgerPath = Join-Path $evidence "$Name-ledger.json"
    [System.IO.File]::WriteAllText($ledgerPath, ($ledger | ConvertTo-Json -Depth 100), [System.Text.UTF8Encoding]::new($false))
    return [pscustomobject]@{ Name=$Name; Passed=$passed; Failure=$proofFailure; LedgerPath=$ledgerPath; LedgerSha256=(Get-FileSha256 -LiteralPath $ledgerPath); Criteria=@($context.Criteria); Coverage=@($ledger.coverage); SourcePreserved=$sourcePreserved; Cleanup=$ledger.cleanup; Screenshots=@($context.Screenshots) }
}

function Invoke-SelfTest {
    if ((Get-NormalizedSha256 -Value ('A' * 64)) -cne ('a' * 64)) { throw 'SHA normalization self-test failed.' }
    if (-not (Test-IsDriveEPath -LiteralPath 'E:\proof\fresh') -or (Test-IsDriveEPath -LiteralPath 'C:\proof\fresh')) { throw 'E: path policy self-test failed.' }
    if (-not (Test-PathWithinDirectory -LiteralPath 'E:\proof\fresh\child' -Directory 'E:\proof\fresh') -or (Test-PathWithinDirectory -LiteralPath 'E:\proof\other' -Directory 'E:\proof\fresh')) { throw 'Path containment self-test failed.' }
    $syntheticExecutable = 'E:\proof\candidate.exe'
    if ((ConvertTo-AbsoluteExecutablePathOrNull -Value $syntheticExecutable) -cne [System.IO.Path]::GetFullPath($syntheticExecutable)) { throw 'Absolute executable path normalization self-test failed.' }
    if ($null -ne (ConvertTo-AbsoluteExecutablePathOrNull -Value '') -or $null -ne (ConvertTo-AbsoluteExecutablePathOrNull -Value 'candidate.exe')) { throw 'Unavailable/relative executable path fail-closed self-test failed.' }
    $loaderPath = [System.IO.Path]::Combine([Environment]::SystemDirectory, 'ntdll.dll')
    if (-not (Test-TransientWindowsLoaderPath -Path $loaderPath) -or (Test-TransientWindowsLoaderPath -Path $syntheticExecutable)) { throw 'Transient Windows loader path classification self-test failed.' }
    $pathMismatchFailed = $false
    try { [void](Assert-ExactExecutableIdentity -LaunchedPath 'E:\proof\other.exe' -Candidate $syntheticExecutable -ExpectedHash ('0' * 64) -ProcessId 42 -Source 'SelfTest') }
    catch { $pathMismatchFailed = $_.Exception.Message -like "Launched PID 42 SelfTest resolved to*expected exact candidate*" }
    if (-not $pathMismatchFailed) { throw 'Exact launched executable mismatch self-test failed.' }
    $fixtureReadyExpression = New-ExactFixtureReadyExpression -ZoneId 'zone-self-test' -PlacementId 'placement-self-test' -BuildingId 'building-self-test'
    if (-not $fixtureReadyExpression.Contains('const zone="zone-self-test";') -or -not $fixtureReadyExpression.Contains("reason:'zone='+shown") -or $fixtureReadyExpression.Contains('${shown}')) { throw 'Exact fixture readiness expression expansion self-test failed.' }
    $ipc = '{"command":"{\"command\":\"authoring\",\"request\":{\"action\":\"preview_zone\",\"request\":{}}}"}'
    if ((Get-AuditedRequestKindFromIpcBody -Body $ipc) -cne 'preview_zone') { throw 'IPC classification self-test failed.' }
    if ($null -ne (Get-AuditedRequestKindFromIpcBody -Body '{bad json') -or $null -ne (Get-AuditedRequestKindFromIpcBody -Body '{"command":"{}"}')) { throw 'IPC fail-closed parsing self-test failed.' }
    $syntheticReceipt = [pscustomobject][ordered]@{
        command_id = '11111111-1111-4111-8111-111111111111'
        request_sha256 = ('1' * 64)
        applied_sha256 = ('2' * 64)
        inverse_sha256 = ('3' * 64)
        base_change_sha256 = $null
        resulting_revision = '7'
        resulting_hash = ('4' * 64)
    }
    $syntheticResponse = [pscustomobject]@{ response='authoring'; zone_receipt=$syntheticReceipt }
    $syntheticAuditEntry = [pscustomobject]@{
        command = 'execute_studio_command'
        wire = [pscustomobject]@{ command='authoring'; request=[pscustomobject]@{ action='apply_zone'; request=[pscustomobject]@{ command_id=$syntheticReceipt.command_id } } }
        request_kind = 'apply_zone'
        outcome = 'resolved'
        parse_error = $null
        response_parse_error = $null
        synthetic_retry = $true
        request_sha256 = ('5' * 64)
        zone_request_sha256 = $syntheticReceipt.request_sha256
        attempts = @(
            [pscustomobject]@{ index=0; http_status=200; response=$syntheticResponse },
            [pscustomobject]@{ index=1; http_status=200; response=$syntheticResponse }
        )
    }
    $syntheticAudit = [pscustomobject]@{
        retry_next = [pscustomobject]@{ armed=$false; matched=$true; matched_entry_index=1 }
        entries = @(
            [pscustomobject]@{ command='unrelated'; wire=[pscustomobject]@{ command='other' }; request_kind=$null },
            $syntheticAuditEntry
        )
    }
    $auditedEntries = @(Get-AuditedAuthoringEntries -Snapshot $syntheticAudit -RequestKind apply_zone)
    if ($auditedEntries.Count -ne 1 -or $auditedEntries[0].request_sha256 -cne ('5' * 64)) { throw 'Bounded authoring audit selection self-test failed.' }
    $requiredAuditEntry = Get-RequiredAuditedEntry -Snapshot $syntheticAudit -RequestKind apply_zone
    $firstReceipt = Get-RequiredAuditReceipt -Entry $requiredAuditEntry -Attempt 0
    $secondReceipt = Get-RequiredAuditReceipt -Entry $requiredAuditEntry -Attempt 1
    if (-not [bool]$requiredAuditEntry.synthetic_retry -or @($requiredAuditEntry.attempts).Count -ne 2 -or -not (Test-AuditReceiptsEqual -Left $firstReceipt -Right $secondReceipt)) { throw 'One-shot audit retry equivalence self-test failed.' }
    $invalidReceiptEntry = [pscustomobject]@{
        wire = $syntheticAuditEntry.wire
        zone_request_sha256 = $syntheticReceipt.request_sha256
        attempts = @([pscustomobject]@{ response=[pscustomobject]@{ response='authoring'; zone_receipt=[pscustomobject][ordered]@{ command_id=$syntheticReceipt.command_id; request_sha256=$syntheticReceipt.request_sha256; applied_sha256=$syntheticReceipt.applied_sha256; inverse_sha256=$syntheticReceipt.inverse_sha256; base_change_sha256=$null; resulting_revision='7'; resulting_hash='bad' } } })
    }
    $invalidReceiptFailed = $false
    try { [void](Get-RequiredAuditReceipt -Entry $invalidReceiptEntry) }
    catch { $invalidReceiptFailed = $true }
    if (-not $invalidReceiptFailed) { throw 'Invalid audit receipt fail-closed self-test failed.' }
    $mismatchedBindingEntry = [pscustomobject]@{
        wire = $syntheticAuditEntry.wire
        zone_request_sha256 = ('6' * 64)
        attempts = @([pscustomobject]@{ response=$syntheticResponse })
    }
    $mismatchedBindingFailed = $false
    try { [void](Get-RequiredAuditReceipt -Entry $mismatchedBindingEntry) }
    catch { $mismatchedBindingFailed = $true }
    if (-not $mismatchedBindingFailed) { throw 'Mismatched nested-request receipt binding fail-closed self-test failed.' }
    $diagnosticJson = '{"schema":"forge-placement-gizmo-diagnostic","version":1,"state":"active","placement_id":"p","mode":"translation","rotation_space":null,"source_transform":{"translation_m":[0,0,0],"rotation":[0,0,0,1],"scale":[1,1,1]},"current_transform":{"translation_m":[1,0,0],"rotation":[0,0,0,1],"scale":[1,1,1]},"pivot_world":[0,0,0],"logical_viewport_size":{"width":1000,"height":600},"phase":"move","active_axis":"x","click_normalized":{"x":0.5,"y":0.5},"axis_handle_screen_normalized":[{"axis":"x","screen_normalized":{"x":0.7,"y":0.5}},{"axis":"y","screen_normalized":{"x":0.5,"y":0.3}},{"axis":"z","screen_normalized":{"x":0.4,"y":0.6}}],"rotation_ring_candidates_screen_normalized":[]}'
    $diagnostic = ConvertFrom-GizmoDiagnostic -Json $diagnosticJson -ExpectedPlacementId p -ExpectedMode translation
    $point = Get-GizmoAxisPoint -Diagnostic $diagnostic -Axis x
    if ([Math]::Abs($point.x - 0.7) -gt 0.0001) { throw 'Gizmo handle selection self-test failed.' }
    Assert-GizmoTransformContract -Diagnostic $diagnostic -Mode translation -Axis x
    $badDiagnosticFailed = $false
    try { [void](ConvertFrom-GizmoDiagnostic -Json ($diagnosticJson -replace '"version":1','"version":2')) }
    catch { $badDiagnosticFailed = $true }
    if (-not $badDiagnosticFailed) { throw 'Gizmo schema fail-closed self-test failed.' }
    $ringSegments = @()
    for ($index = 0; $index -lt 16; $index += 1) {
        $angleA = 2.0 * [Math]::PI * $index / 16.0
        $angleB = 2.0 * [Math]::PI * ($index + 1) / 16.0
        $ringSegments += [pscustomobject]@{ start=[pscustomobject]@{x=0.5+0.2*[Math]::Cos($angleA);y=0.5+0.2*[Math]::Sin($angleA)}; end=[pscustomobject]@{x=0.5+0.2*[Math]::Cos($angleB);y=0.5+0.2*[Math]::Sin($angleB)} }
    }
    $ringDiagnostic = [pscustomobject]@{ rotation_ring_candidates_screen_normalized=@([pscustomobject]@{axis='y';segments=$ringSegments}) }
    $ring = Get-GizmoRingGesture -Diagnostic $ringDiagnostic -Axis y
    Assert-NormalizedPoint -Point $ring.Start -Description 'Self-test ring start'
    Assert-NormalizedPoint -Point $ring.End -Description 'Self-test ring end'
    $before = [pscustomobject]@{ Sha256=('a'*64);Bytes=12;LastWriteUtcTicks=34 }
    $after = [pscustomobject]@{ Sha256=('a'*64);Bytes=12;LastWriteUtcTicks=34 }
    if (-not (Test-ArchiveSnapshotsEqual -Before $before -After $after)) { throw 'Archive identity self-test failed.' }
    $criterion = New-CriterionResult -Criterion R2-999
    Add-CriterionCase -Criterion $criterion -Name pass -Passed $true -Evidence @{ ok=$true }
    Complete-CriterionResult -Criterion $criterion
    if (-not $criterion.Passed) { throw 'Criterion aggregation pass self-test failed.' }
    Add-CriterionCase -Criterion $criterion -Name fail -Passed $false -Failure expected
    Complete-CriterionResult -Criterion $criterion
    if ($criterion.Passed -or $criterion.Status -cne 'failed_closed') { throw 'Criterion aggregation fail-closed self-test failed.' }
    $local = Convert-WorldToBuildingLocal -Point ([pscustomobject]@{X=10.0;Z=5.0}) -OriginX 10 -OriginZ 0 -YawDegrees 0
    if ([Math]::Abs($local.X) -gt 0.0001 -or [Math]::Abs($local.Z - 5.0) -gt 0.0001) { throw 'World/building coordinate self-test failed.' }
    $play = Parse-PlaytestStatus -Text 'Mode jeu · Tick 42 · position (m) 1.25, 2.00, -3.50 · entrée active true · simulation prête true · dernier mouvement observé tick 42 : demandé X/Z (0.0000, 0.1000) m, résolu (0.0000, 0.0000) m, 2 contact(s) (sol inclus)'
    if ($play.Tick -ne 42 -or $play.Contacts -ne 2 -or -not $play.SimulationReady) { throw 'Playtest status parsing self-test failed.' }
    if (Test-PlacementProjectionStatusReady -Status '72 placements LOD0 projetés · préparation UCX demandée ; Jouer attend les collisions synchronisées.') { throw 'Preliminary LOD0 projection status fail-closed self-test failed.' }
    if (-not (Test-PlacementProjectionStatusReady -Status '72 placements · 78 primitives synchronisées dans Rapier · 0 en attente · Jouer vérifie aussi la source acceptée.')) { throw 'Rapier placement projection status self-test failed.' }
    if (Test-PlacementProjectionStatusReady -Status '72 placements · 77 primitives synchronisées dans Rapier · 1 en attente · Jouer vérifie aussi la source acceptée.') { throw 'Pending Rapier projection status fail-closed self-test failed.' }
    $singleScenarios = @(Get-SelectedScenarioNames -RequestedScenario AssetPreviewCancel)
    if ($singleScenarios.Count -ne 1 -or $singleScenarios[0] -cne 'asset-preview-cancel') { throw 'Single-scenario aggregation self-test failed.' }
    $placementScenarios = @(Get-SelectedScenarioNames -RequestedScenario Placements)
    if ($placementScenarios.Count -ne 2 -or $placementScenarios[0] -cne 'placements' -or $placementScenarios[1] -cne 'placement-lock') { throw 'Placement scenario isolation self-test failed.' }
    $transversalScenarios = @(Get-SelectedScenarioNames -RequestedScenario TransversalV8)
    if ($transversalScenarios.Count -ne 1 -or $transversalScenarios[0] -cne 'transversal-v8') { throw 'TransversalV8 scenario isolation self-test failed.' }
    $fullAuthorRouteScenarios = @(Get-SelectedScenarioNames -RequestedScenario FullAuthorRoute)
    if ($fullAuthorRouteScenarios.Count -ne 1 -or $fullAuthorRouteScenarios[0] -cne 'full-author-route') { throw 'FullAuthorRoute scenario isolation self-test failed.' }
    $fullAuthorRouteCoverage = @(Get-ScenarioCoverage -Name 'full-author-route')
    $expectedFullAuthorRouteCriteria = @('R2-037','R2-043','R2-049','R2-052')
    $observedFullAuthorRouteCriteria = @($fullAuthorRouteCoverage | ForEach-Object { [string]$_.criterion })
    if ($fullAuthorRouteCoverage.Count -ne $expectedFullAuthorRouteCriteria.Count -or ($observedFullAuthorRouteCriteria -join '|') -cne ($expectedFullAuthorRouteCriteria -join '|')) { throw 'FullAuthorRoute fail-closed coverage self-test failed.' }
    $transversalCoverage = @(Get-ScenarioCoverage -Name 'transversal-v8')
    $expectedTransversalCriteria = @('R2-037','R2-042','R2-046','R2-047','R2-048','R2-049','R2-051')
    $observedTransversalCriteria = @($transversalCoverage | ForEach-Object { [string]$_.criterion })
    if ($transversalCoverage.Count -ne $expectedTransversalCriteria.Count -or ($observedTransversalCriteria -join '|') -cne ($expectedTransversalCriteria -join '|') -or @($observedTransversalCriteria | Select-Object -Unique).Count -ne $expectedTransversalCriteria.Count) { throw 'TransversalV8 coverage identity self-test failed.' }
    if (@($transversalCoverage | Where-Object { [string]$_.criterion -ceq 'R2-050' }).Count -ne 0) { throw 'R2-050 must remain outside TransversalV8 coverage.' }
    foreach ($partialCriterion in @('R2-037','R2-049')) {
        if ([bool]($transversalCoverage | Where-Object { [string]$_.criterion -ceq $partialCriterion }).criterion_complete_in_scenario) { throw "$partialCriterion must remain partial in TransversalV8." }
    }
    foreach ($completeCriterion in @('R2-042','R2-046','R2-047','R2-048','R2-051')) {
        if (-not [bool]($transversalCoverage | Where-Object { [string]$_.criterion -ceq $completeCriterion }).criterion_complete_in_scenario) { throw "$completeCriterion completeness self-test failed." }
    }
    $syntheticTransversalResult = [pscustomobject]@{
        Name = 'transversal-v8'
        Passed = $true
        Coverage = $transversalCoverage
        Criteria = @($expectedTransversalCriteria | ForEach-Object { [pscustomobject]@{ Criterion=$_; Passed=$true } })
    }
    if ([string](Get-SingleScenarioCriterionCoverage -Result $syntheticTransversalResult -Criterion 'R2-042').status -cne 'automated_cdp_acquired') { throw 'Complete TransversalV8 aggregation self-test failed.' }
    if ([string](Get-SingleScenarioCriterionCoverage -Result $syntheticTransversalResult -Criterion 'R2-037').status -cne 'partial_scope_only') { throw 'Partial TransversalV8 aggregation self-test failed.' }
    if ([string](Get-SingleScenarioCriterionCoverage -Result $null -Criterion 'R2-042').status -cne 'not_requested') { throw 'Unrequested TransversalV8 aggregation self-test failed.' }
    $allScenarios = @(Get-SelectedScenarioNames -RequestedScenario All)
    if ($allScenarios.Count -ne 5 -or $allScenarios[0] -cne 'placements' -or $allScenarios[1] -cne 'placement-lock' -or $allScenarios[2] -cne 'building-playtest' -or $allScenarios[3] -cne 'asset-preview-cancel' -or $allScenarios[4] -cne 'transversal-v8') { throw 'All-scenario aggregation self-test failed.' }
    Write-Output 'GATE2_WORLD_EDITING_PROOF_SELF_TEST_PASS'
}

if ($SelfTest) {
    Invoke-SelfTest
    return
}

if (-not (Test-IsWindowsPlatform)) {
    throw 'This proof harness requires Windows, WebView2, and a real Tauri candidate.'
}

$resolvedCandidate = [System.IO.Path]::GetFullPath($CandidateExecutable)
$resolvedSource = [System.IO.Path]::GetFullPath($FixtureSourceRoot)
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
foreach ($entry in @(
    [pscustomobject]@{ Path=$resolvedCandidate; Description='CandidateExecutable' },
    [pscustomobject]@{ Path=$resolvedSource; Description='FixtureSourceRoot' },
    [pscustomobject]@{ Path=$resolvedOutput; Description='OutputDirectory' }
)) {
    Assert-DriveEPath -LiteralPath $entry.Path -Description $entry.Description
    Assert-NoExistingReparseAncestor -LiteralPath $entry.Path -Description $entry.Description
}
if (-not (Test-Path -LiteralPath $resolvedCandidate -PathType Leaf)) { throw "Candidate executable not found: $resolvedCandidate" }
if (-not (Test-Path -LiteralPath $resolvedSource -PathType Container)) { throw "Fixture source root not found: $resolvedSource" }
Assert-TreeHasNoReparsePoints -LiteralPath $resolvedSource -Description 'FixtureSourceRoot'
if (Test-Path -LiteralPath $resolvedOutput) { throw "OutputDirectory must be fresh and absent: $resolvedOutput" }
if ((Test-PathWithinDirectory -LiteralPath $resolvedOutput -Directory $resolvedSource) -or (Test-PathWithinDirectory -LiteralPath $resolvedSource -Directory $resolvedOutput)) { throw 'OutputDirectory and FixtureSourceRoot must be disjoint.' }
$expectedHash = Get-NormalizedSha256 -Value $ExpectedSha256
$candidateHashBefore = Get-FileSha256 -LiteralPath $resolvedCandidate
if ($candidateHashBefore -cne $expectedHash) { throw "Candidate SHA-256 mismatch. Expected $expectedHash, got $candidateHashBefore." }
$zoneId = $ZoneUuid.ToString('D').ToLowerInvariant()
$placementId = $PlacementUuid.ToString('D').ToLowerInvariant()
$buildingId = $BuildingUuid.ToString('D').ToLowerInvariant()
$fullAuthorRouteZoneId = $FullAuthorRouteZoneUuid.ToString('D').ToLowerInvariant()
$selectedScenarios = @(Get-SelectedScenarioNames -RequestedScenario $Scenario)
$sourceZoneId = if ($selectedScenarios.Count -eq 1 -and $selectedScenarios[0] -ceq 'full-author-route') { $fullAuthorRouteZoneId } else { $zoneId }
$sourceArchivePath = Join-Path (Join-Path $resolvedSource $sourceZoneId) 'authoring.json'
$sourceArchiveBefore = Get-ArchiveSnapshot -LiteralPath $sourceArchivePath
if ($sourceZoneId -ceq $fullAuthorRouteZoneId) {
    $expectedFullAuthorRouteSourceHash = Get-NormalizedSha256 -Value $FullAuthorRouteSourceSha256
    if ($sourceArchiveBefore.Sha256 -cne $expectedFullAuthorRouteSourceHash) {
        throw "FullAuthorRoute source SHA-256 mismatch. Expected $expectedFullAuthorRouteSourceHash, got $($sourceArchiveBefore.Sha256)."
    }
}
else {
    Assert-FixtureIdentityHints -ArchivePath $sourceArchivePath -PlacementId $placementId -BuildingId $buildingId
}
[void](New-Item -ItemType Directory -Path $resolvedOutput)
$resolvedOutput = (Get-Item -LiteralPath $resolvedOutput -Force).FullName

$results = [System.Collections.Generic.List[object]]::new()
foreach ($name in $selectedScenarios) {
    $script:CdpMessageId = 0
    $script:CdpEvents.Clear()
    $script:CdpPendingResponses = @{}
    $script:CdpDroppedEventCount = 0
    switch ($name) {
        'placements' {
            $results.Add((Invoke-StudioScenario -Name $name -Candidate $resolvedCandidate -ExpectedHash $expectedHash -SourceRoot $resolvedSource -SourceArchiveBefore $sourceArchiveBefore -ZoneId $zoneId -PlacementId $placementId -BuildingId $buildingId -RootOutput $resolvedOutput -StartupTimeout $StartupTimeoutSeconds -InteractionTimeout $InteractionTimeoutSeconds -Runner ${function:Invoke-PlacementsScenario}))
        }
        'placement-lock' {
            $results.Add((Invoke-StudioScenario -Name $name -Candidate $resolvedCandidate -ExpectedHash $expectedHash -SourceRoot $resolvedSource -SourceArchiveBefore $sourceArchiveBefore -ZoneId $zoneId -PlacementId $placementId -BuildingId $buildingId -RootOutput $resolvedOutput -StartupTimeout $StartupTimeoutSeconds -InteractionTimeout $InteractionTimeoutSeconds -Runner ${function:Invoke-PlacementLockScenario}))
        }
        'building-playtest' {
            $results.Add((Invoke-StudioScenario -Name $name -Candidate $resolvedCandidate -ExpectedHash $expectedHash -SourceRoot $resolvedSource -SourceArchiveBefore $sourceArchiveBefore -ZoneId $zoneId -PlacementId $placementId -BuildingId $buildingId -RootOutput $resolvedOutput -StartupTimeout $StartupTimeoutSeconds -InteractionTimeout $InteractionTimeoutSeconds -Runner ${function:Invoke-BuildingPlaytestScenario}))
        }
        'asset-preview-cancel' {
            $results.Add((Invoke-StudioScenario -Name $name -Candidate $resolvedCandidate -ExpectedHash $expectedHash -SourceRoot $resolvedSource -SourceArchiveBefore $sourceArchiveBefore -ZoneId $zoneId -PlacementId $placementId -BuildingId $buildingId -RootOutput $resolvedOutput -StartupTimeout $StartupTimeoutSeconds -InteractionTimeout $InteractionTimeoutSeconds -Runner ${function:Invoke-AssetPreviewCancelScenario} -AssetPreviewDelay))
        }
        'transversal-v8' {
            $results.Add((Invoke-StudioScenario -Name $name -Candidate $resolvedCandidate -ExpectedHash $expectedHash -SourceRoot $resolvedSource -SourceArchiveBefore $sourceArchiveBefore -ZoneId $zoneId -PlacementId $placementId -BuildingId $buildingId -RootOutput $resolvedOutput -StartupTimeout $StartupTimeoutSeconds -InteractionTimeout $InteractionTimeoutSeconds -Runner ${function:Invoke-TransversalV8Scenario}))
        }
        'full-author-route' {
            $results.Add((Invoke-StudioScenario -Name $name -Candidate $resolvedCandidate -ExpectedHash $expectedHash -SourceRoot $resolvedSource -SourceArchiveBefore $sourceArchiveBefore -ZoneId $fullAuthorRouteZoneId -PlacementId $placementId -BuildingId $buildingId -RootOutput $resolvedOutput -StartupTimeout $StartupTimeoutSeconds -InteractionTimeout $InteractionTimeoutSeconds -Runner ${function:Invoke-FullAuthorRouteScenario}))
        }
        'projection-incremental' {
            $results.Add((Invoke-StudioScenario -Name $name -Candidate $resolvedCandidate -ExpectedHash $expectedHash -SourceRoot $resolvedSource -SourceArchiveBefore $sourceArchiveBefore -ZoneId $zoneId -PlacementId $placementId -BuildingId $buildingId -RootOutput $resolvedOutput -StartupTimeout $StartupTimeoutSeconds -InteractionTimeout $InteractionTimeoutSeconds -Runner ${function:Invoke-ProjectionIncrementalScenario}))
        }
    }
}

$sourceArchiveAfter = Get-ArchiveSnapshot -LiteralPath $sourceArchivePath
$sourcePreserved = Test-ArchiveSnapshotsEqual -Before $sourceArchiveBefore -After $sourceArchiveAfter
$candidateHashAfter = Get-FileSha256 -LiteralPath $resolvedCandidate
$allPassed = @($results).Count -eq @($selectedScenarios).Count -and @($results | Where-Object { -not $_.Passed }).Count -eq 0 -and $sourcePreserved -and $candidateHashAfter -ceq $expectedHash
$r2029RequiredScenarios = @('placements', 'placement-lock')
$r2029Results = @($results | Where-Object { $_.Name -in $r2029RequiredScenarios })
$r2029ObservedScenarios = @($r2029Results | ForEach-Object { $_.Name })
$r2029AllRequiredExecuted = @($r2029RequiredScenarios | Where-Object { $r2029ObservedScenarios -notcontains $_ }).Count -eq 0
$r2029Passed = $r2029AllRequiredExecuted -and @($r2029Results | Where-Object { -not $_.Passed }).Count -eq 0
$r2029CoverageStatus = if ($r2029Passed) { 'automated_cdp_acquired' } elseif ($r2029Results.Count -eq 0) { 'not_requested' } elseif (-not $r2029AllRequiredExecuted) { 'partial_scope_only' } else { 'failed_closed' }
$transversalResults = @($results | Where-Object { $_.Name -ceq 'transversal-v8' })
$transversalResult = if ($transversalResults.Count -eq 1) { $transversalResults[0] } else { $null }
$fullAuthorRouteResults = @($results | Where-Object { $_.Name -ceq 'full-author-route' })
$fullAuthorRouteResult = if ($fullAuthorRouteResults.Count -eq 1) { $fullAuthorRouteResults[0] } else { $null }
$projectionResults = @($results | Where-Object { $_.Name -ceq 'projection-incremental' })
$projectionResult = if ($projectionResults.Count -eq 1) { $projectionResults[0] } else { $null }
$criterionCoverage = [ordered]@{
    'R2-029' = [ordered]@{
        required_scenarios = $r2029RequiredScenarios
        observed_scenarios = $r2029ObservedScenarios
        all_required_scenarios_executed = $r2029AllRequiredExecuted
        passed = $r2029Passed
        status = $r2029CoverageStatus
        scopes = @($r2029Results | ForEach-Object { $_.Coverage | Where-Object { $_.criterion -ceq 'R2-029' } })
    }
}
foreach ($criterionId in @('R2-037','R2-042','R2-046','R2-047','R2-048','R2-049','R2-051')) {
    $criterionCoverage[$criterionId] = Get-SingleScenarioCriterionCoverage -Result $transversalResult -Criterion $criterionId
}
$projectionCase = if ($null -ne $projectionResult) { @($projectionResult.Criteria | Where-Object { $_.Criterion -ceq 'R2-050' }) } else { @() }
$projectionPassed = $null -ne $projectionResult -and $projectionCase.Count -eq 1 -and [bool]$projectionCase[0].Passed -and [bool]$projectionResult.SourcePreserved -and [bool]$projectionResult.Cleanup.owned_process_closed
$criterionCoverage['R2-050'] = [ordered]@{ scenario='projection-incremental'; criterion='R2-050'; requested=$null -ne $projectionResult; passed=$projectionPassed; criterion_complete_in_scenario=$false; status=if($null -eq $projectionResult){'not_requested'}elseif($projectionPassed){'partial_scope_only'}else{'failed_closed'}; reason='Placement and building identities are observed; stale/play barrier and interactive performance remain separately open.' }
$fullAuthorRouteCoverage = [ordered]@{}
foreach ($criterionId in @('R2-037','R2-043','R2-049','R2-052')) {
    $fullAuthorRouteCoverage[$criterionId] = Get-FullAuthorRouteCriterionCoverage -Result $fullAuthorRouteResult -Criterion $criterionId
}
$completedAt = [DateTimeOffset]::UtcNow
$topLedger = [ordered]@{
    schema = 'forge.gate2.world-editing-proof'
    schema_version = 1
    recorded_at_utc = $completedAt.ToString('o')
    gate = 'Gate 2'
    scenarios_requested = $selectedScenarios
    passed = $allPassed
    status = if ($allPassed) { 'automated_cdp_acquired' } else { 'failed_closed' }
    gate_qualified = $false
    human_review_performed = $false
    ci = [ordered]@{ status=$script:CiStatus }
    candidate = [ordered]@{ path=$resolvedCandidate; expected_sha256=$expectedHash; before_sha256=$candidateHashBefore; after_sha256=$candidateHashAfter; exact_hash_verified=$candidateHashBefore -ceq $expectedHash -and $candidateHashAfter -ceq $expectedHash }
    source_fixture = [ordered]@{ root=$resolvedSource; archive_before=$sourceArchiveBefore; archive_after=$sourceArchiveAfter; hash_bytes_mtime_exact=$sourcePreserved }
    identities = [ordered]@{ zone_uuid=$zoneId; placement_uuid=$placementId; building_uuid=$buildingId }
    scenarios = @($results)
    criterion_coverage = $criterionCoverage
    full_author_route = [ordered]@{ source_zone_uuid=$fullAuthorRouteZoneId; required_source_archive_sha256=(Get-NormalizedSha256 -Value $FullAuthorRouteSourceSha256); criteria=$fullAuthorRouteCoverage; human_effort=$null; operator='automation'; clone_count_requested=5; clone_statistics='not_emitted_by_single_run' }
    limitations = @('Automated CDP proof only; not human review.','The asset-preview-cancel transport is a synthetic delayed ReadableStream over the candidate real pinned bytes, not a real HTTP fault fixture.','TransversalV8 uses one exact synthetic transport retry over a real ApplyZone request; it is not a product test hook.','FullAuthorRoute exercises valley, vegetation, path, river, house, clearing, regeneration, Save/Close/Open and Play/Stop; GPU presentation and human visual review remain separate.','R2-037 author and workspace histories are complementary in TransversalV8 and FullAuthorRoute; neither single scenario alone is the complete criterion.','R2-049 accepted-river provenance is observed only in FullAuthorRoute; other inspector coverage is in TransversalV8.','R2-050 placement/building identity is in ProjectionIncremental; stale/play barrier and performance remain separate.','R2-052 automation timings are not human author effort; five-clone statistics are not emitted by a single run.','A scenario or subcase that cannot establish its observable contract is recorded failed_closed; no partial result is promoted.','gate_qualified remains false.', $script:CiStatus)
}
$topLedgerPath = Join-Path $resolvedOutput 'GATE2-WORLD-EDITING-CDP-ledger.json'
[System.IO.File]::WriteAllText($topLedgerPath, ($topLedger | ConvertTo-Json -Depth 100), [System.Text.UTF8Encoding]::new($false))
$topLedgerHash = Get-FileSha256 -LiteralPath $topLedgerPath
$reportPath = Join-Path $resolvedOutput 'GATE2-WORLD-EDITING-CDP-REPORT.md'
$rows = @($results | ForEach-Object { "| $($_.Name) | $($_.Passed) | $($_.Failure) | $($_.LedgerPath) |" }) -join [Environment]::NewLine
$report = @"
# Gate 2 — preuve CDP édition du monde

- Verdict du harness : **$(if($allPassed){'PASS automatisé local'}else{'FAIL fail-closed'})**
- ``gate_qualified=false``
- ``human_review_performed=false``
- CI distante : **$($script:CiStatus)**
- Candidat exact : ``$resolvedCandidate``
- SHA-256 : ``$candidateHashAfter``
- Fixture source intacte (hash, octets, mtime) : **$sourcePreserved**
- Couverture R2-029 agrégée (``placements`` + ``placement-lock``) : **$r2029CoverageStatus**
- TransversalV8 R2-037 : **$($criterionCoverage['R2-037'].status)** — historique auteur seulement, pagination workspace non revendiquée.
- TransversalV8 R2-042/R2-046/R2-047/R2-048/R2-051 : **$($criterionCoverage['R2-042'].status)** / **$($criterionCoverage['R2-046'].status)** / **$($criterionCoverage['R2-047'].status)** / **$($criterionCoverage['R2-048'].status)** / **$($criterionCoverage['R2-051'].status)**.
- TransversalV8 R2-049 : **$($criterionCoverage['R2-049'].status)** — aucune rivière acceptée dans cette fixture.
- ProjectionIncremental R2-050 : **$($criterionCoverage['R2-050'].status)** — identités placements et bâtiments ; barrière périmée/playtest et performance hors de cette tranche.
- FullAuthorRoute R2-037/R2-043/R2-049/R2-052 : **$($fullAuthorRouteCoverage['R2-037'].status)** / **$($fullAuthorRouteCoverage['R2-043'].status)** / **$($fullAuthorRouteCoverage['R2-049'].status)** / **$($fullAuthorRouteCoverage['R2-052'].status)**. R2-043 est programmatique ; aucune revue humaine ou preuve GPU implicite.

| Scénario | PASS | Erreur | Ledger |
|---|---:|---|---|
$rows

## Portée

- ``placements`` : R2-028, gestes R2-029 hors verrouillage, R2-030 et R2-031 ; picking, gizmos, snap, duplication/suppression, Undo/Redo/Save/Close/Open.
- ``placement-lock`` : sous-cas R2-029 isolé dans une fixture et un processus frais ; verrouillage, gizmo désactivé, capture puis Undo exact.
- ``building-playtest`` : R2-032 et R2-033, panneau complet, refus, verrou, persistance, huit volumes, passage porte/mur W/S, pointer-lock et retour édition.
- ``asset-preview-cancel`` : R2-026, vrais octets/pin du candidat ralentis par un ``ReadableStream`` synthétique et annulation sans aperçu oak tardif.
- ``transversal-v8`` : R2-042, R2-046, R2-047, R2-048 et R2-051 ; collecte partielle explicite R2-037/R2-049. R2-050 exclu. Le retry R2-051 répète exactement une requête ``ApplyZone`` réelle et compare les deux reçus bornés.
- ``full-author-route`` : source UI vierge ``$fullAuthorRouteZoneId`` épinglée par SHA-256, copie isolée ; neuf opérations R2-043 réelles, source préservée, captures après projection, rivière acceptée inspectée en lecture seule. Historique workspace étendu à 21 opérations dans la copie, sauvegardé et paginé. Effort humain et médiane/p95 sur cinq copies restent hors de ce passage unique.
- ``projection-incremental`` : observation DOM bornée des identités ECS/meshes/matériaux/colliders après retouches placement et bâtiment. Ne prouve pas le rendu GPU ni la performance interactive.

Chaque scénario emploie une copie fraîche de la fixture. Aucun appel direct à une commande Rust n'est effectué : les mutations passent par les contrôles Studio et les événements CDP souris/clavier. Une assertion non observable reste un échec ; ce rapport ne qualifie ni Gate 2 formellement ni Gate 3.

Ledger agrégé : ``$topLedgerPath`` (SHA-256 ``$topLedgerHash``)
"@
[System.IO.File]::WriteAllText($reportPath, $report, [System.Text.UTF8Encoding]::new($false))

if (-not $allPassed) {
    $failures = @($results | Where-Object { -not $_.Passed } | ForEach-Object { "$($_.Name): $($_.Failure)" }) -join ' | '
    throw "Gate 2 world-editing CDP proof failed closed. Ledger: $topLedgerPath. $failures"
}

Write-Output "GATE2_WORLD_EDITING_CDP_PROOF_ACQUIRED gate_qualified=false automated_cdp=true human_review=false ledger=$topLedgerPath report=$reportPath"
