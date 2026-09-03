[CmdletBinding()]
param(
    [uri]$BaseUrl = 'http://127.0.0.1:1420/',

    [string]$BrowserExecutable,

    [string]$OutputRoot,

    [string]$ConfigPath = (Join-Path $PSScriptRoot 'gate1-browser-weather-matrix.v1.json'),

    [switch]$ValidateOnly,

    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$script:CdpMessageId = 0
$script:ConsoleEvents = [Collections.Generic.List[object]]::new()
$script:NetworkFailures = [Collections.Generic.List[object]]::new()
$script:NetworkRequests = @{}
$script:NetworkResponses = @{}

function ConvertTo-CompactJson {
    param([Parameter(Mandatory)][object]$Value)

    return ($Value | ConvertTo-Json -Depth 64 -Compress)
}

function ConvertFrom-ExactJson {
    param([Parameter(Mandatory)][string]$Json)

    $parameters = @{ Depth = 64 }
    if ((Get-Command ConvertFrom-Json).Parameters.ContainsKey('DateKind')) {
        $parameters.DateKind = 'String'
    }
    return ($Json | ConvertFrom-Json @parameters)
}

function Copy-JsonValue {
    param([Parameter(Mandatory)][object]$Value)

    return ConvertFrom-ExactJson -Json (ConvertTo-CompactJson -Value $Value)
}

function Assert-Condition {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Throws {
    param(
        [Parameter(Mandatory)][string]$Description,
        [Parameter(Mandatory)][scriptblock]$Operation
    )

    $threw = $false
    try {
        & $Operation
    } catch {
        $threw = $true
    }
    Assert-Condition -Condition $threw -Message "Self-test expected failure: $Description."
}

function Test-LoopbackHost {
    param([Parameter(Mandatory)][string]$HostName)

    try {
        $addresses = @([Net.Dns]::GetHostAddresses($HostName))
        return (
            $addresses.Count -gt 0 -and
            @($addresses | Where-Object { -not [Net.IPAddress]::IsLoopback($_) }).Count -eq 0
        )
    } catch {
        return $false
    }
}

function Get-LowercaseSha256 {
    param([Parameter(Mandatory)][string]$LiteralPath)

    return (Get-FileHash -LiteralPath $LiteralPath -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-ExactPropertyNames {
    param(
        [Parameter(Mandatory)][object]$Value,
        [Parameter(Mandatory)][string[]]$Expected,
        [Parameter(Mandatory)][string]$Context
    )

    $actual = @($Value.PSObject.Properties.Name)
    Assert-Condition -Condition ($actual.Count -eq $Expected.Count) -Message "$Context property count changed."
    foreach ($name in $Expected) {
        Assert-Condition -Condition ($actual -ccontains $name) -Message "$Context is missing exact property '$name'."
    }
}

function Assert-WeatherMatrixConfig {
    param([Parameter(Mandatory)][object]$Config)

    Assert-ExactPropertyNames -Value $Config -Expected @(
        'schema', 'schema_version', 'protocol_id', 'implementation_base_commit', 'route', 'server',
        'browser', 'readiness', 'controller', 'sequence', 'fixed_context', 'acceptance'
    ) -Context 'Browser weather config root'
    Assert-ExactPropertyNames -Value $Config.route -Expected @('path', 'query', 'quality_tiers') -Context 'Browser weather route'
    Assert-ExactPropertyNames -Value $Config.route.query -Expected @('forge_scene', 'forge_weather', 'forge_gate1_weather_proof') -Context 'Browser weather route query'
    Assert-ExactPropertyNames -Value $Config.server -Expected @('origin', 'dist_root', 'bundle_files') -Context 'Browser weather server'
    Assert-ExactPropertyNames -Value $Config.browser -Expected @('canvas_selector', 'viewport_width_px', 'viewport_height_px', 'device_scale_factor', 'headed_capture_required') -Context 'Browser weather browser'
    Assert-ExactPropertyNames -Value $Config.readiness -Expected @(
        'timeout_seconds', 'minimum_stable_layout_observations', 'stable_layout_poll_interval_ms',
        'require_navigator_gpu', 'require_no_page_scrollbars', 'require_canvas_fully_visible',
        'require_backing_matches_css', 'require_single_viewport_start', 'require_completed_frame_window'
    ) -Context 'Browser weather readiness'
    Assert-ExactPropertyNames -Value $Config.controller -Expected @('advance_event', 'ledger_element_id', 'same_bevy_instance_required', 'minimum_settled_frames', 'transition_timeout_ms', 'water_camera') -Context 'Browser weather controller'
    Assert-ExactPropertyNames -Value $Config.controller.water_camera -Expected @('mode', 'anchor_x_m', 'anchor_z_m', 'view_distance_m') -Context 'Browser weather water camera'
    Assert-ExactPropertyNames -Value $Config.fixed_context -Expected @('scene', 'quality_mode', 'camera_owner', 'active_camera_count', 'block_browser_input', 'blocked_event_types') -Context 'Browser weather fixed context'
    Assert-ExactPropertyNames -Value $Config.acceptance -Expected @(
        'required_checkpoints_per_tier', 'required_page_captures_per_tier',
        'required_canvas_captures_per_tier', 'maximum_console_errors', 'maximum_console_warnings',
        'maximum_network_failures', 'require_exact_served_bundles', 'require_low_bloom_absent',
        'require_high_bloom_present', 'require_clear_restored_exactly', 'human_inspection_required',
        'weather_shader_consumption_required_for_route_closure',
        'water_draw_attribution_required_for_route_closure',
        'exact_render_frame_identity_required_for_route_closure'
    ) -Context 'Browser weather acceptance'

    Assert-Condition -Condition ($Config.schema -ceq 'forge.gate1-browser-weather-matrix-config') -Message 'Unexpected Browser weather matrix config schema.'
    Assert-Condition -Condition ([int]$Config.schema_version -eq 1) -Message 'Unexpected Browser weather matrix config schema version.'
    Assert-Condition -Condition ($Config.protocol_id -ceq 'r1-023-025-026-browser-weather-matrix-v1') -Message 'Unexpected Browser weather matrix protocol id.'
    Assert-Condition -Condition ($Config.implementation_base_commit -ceq '83f02928da72774a49d3d62ee68be85335b0bf01') -Message 'The registered implementation base changed without a protocol version bump.'

    Assert-Condition -Condition ($Config.route.path -ceq '/') -Message 'The Browser weather proof route must remain at the Studio root.'
    Assert-Condition -Condition ($Config.route.query.forge_scene -ceq 'visual-valley') -Message 'The Browser weather proof scene must be visual-valley.'
    Assert-Condition -Condition ($Config.route.query.forge_weather -ceq 'clear') -Message 'The Browser weather proof must start at clear.'
    Assert-Condition -Condition ($Config.route.query.forge_gate1_weather_proof -ceq 'v1') -Message 'The Browser weather proof query version changed.'
    Assert-Condition -Condition ((ConvertTo-CompactJson -Value @($Config.route.quality_tiers)) -ceq '["low","high"]') -Message 'The Browser weather matrix must contain exactly Low then High.'

    Assert-Condition -Condition ($Config.server.origin -ceq 'http://127.0.0.1:1420/') -Message 'The registered Trunk origin changed.'
    Assert-Condition -Condition ($Config.server.dist_root -ceq 'apps/forge_studio/dist') -Message 'The registered Studio dist root changed.'
    Assert-Condition -Condition ((ConvertTo-CompactJson -Value @($Config.server.bundle_files)) -ceq '["index.html","forge_studio.js","forge_studio_bg.wasm","styles.css"]') -Message 'The registered served-bundle set changed.'

    Assert-Condition -Condition ($Config.browser.canvas_selector -ceq '#forge-canvas') -Message 'The canonical Studio canvas selector changed.'
    Assert-Condition -Condition ([int]$Config.browser.viewport_width_px -eq 1920) -Message 'The Browser weather proof width must remain 1920.'
    Assert-Condition -Condition ([int]$Config.browser.viewport_height_px -eq 1080) -Message 'The Browser weather proof height must remain 1080.'
    Assert-Condition -Condition ([double]$Config.browser.device_scale_factor -eq 1.0) -Message 'The Browser weather proof device scale factor must remain 1.'
    Assert-Condition -Condition ($Config.browser.headed_capture_required -eq $true) -Message 'A headed Browser capture is required.'

    Assert-Condition -Condition ([int]$Config.readiness.timeout_seconds -eq 120) -Message 'The Browser readiness timeout changed.'
    Assert-Condition -Condition ([int]$Config.readiness.minimum_stable_layout_observations -eq 8) -Message 'Eight stable layout observations are required.'
    Assert-Condition -Condition ([int]$Config.readiness.stable_layout_poll_interval_ms -eq 150) -Message 'The stable-layout polling cadence changed.'
    foreach ($flag in @('require_navigator_gpu', 'require_no_page_scrollbars', 'require_canvas_fully_visible', 'require_backing_matches_css', 'require_single_viewport_start', 'require_completed_frame_window')) {
        Assert-Condition -Condition ($Config.readiness.$flag -eq $true) -Message "Required readiness flag is not true: $flag"
    }

    Assert-Condition -Condition ($Config.controller.advance_event -ceq 'forge-gate1-browser-weather-proof-advance') -Message 'The Rust advance event changed.'
    Assert-Condition -Condition ($Config.controller.ledger_element_id -ceq 'forge-gate1-browser-weather-proof-ledger') -Message 'The Rust proof ledger element changed.'
    Assert-Condition -Condition ($Config.controller.same_bevy_instance_required -eq $true) -Message 'The proof must retain one Bevy instance per tier.'
    Assert-Condition -Condition ([int]$Config.controller.minimum_settled_frames -eq 8) -Message 'The proof must retain eight settled frames per checkpoint.'
    Assert-Condition -Condition ([int]$Config.controller.transition_timeout_ms -eq 30000) -Message 'The bounded transition timeout changed.'
    Assert-Condition -Condition ($Config.controller.water_camera.mode -ceq 'registered_river_anchor') -Message 'The fixed water camera mode changed.'
    Assert-Condition -Condition ([double]$Config.controller.water_camera.anchor_x_m -eq 16.0) -Message 'The water camera anchor X changed.'
    Assert-Condition -Condition ([double]$Config.controller.water_camera.anchor_z_m -eq 160.0) -Message 'The water camera anchor Z changed.'
    Assert-Condition -Condition ([double]$Config.controller.water_camera.view_distance_m -eq 10.0) -Message 'The water camera distance changed.'

    $sequence = @($Config.sequence)
    Assert-Condition -Condition ($sequence.Count -eq 3) -Message 'The Browser weather proof requires exactly three checkpoints.'
    $expected = @(
        @('1', 'initial_clear', 'clear', '0', 'rain'),
        @('2', 'rain', 'rain', '0.78', 'restored_clear'),
        @('3', 'restored_clear', 'clear', '0', '')
    )
    for ($index = 0; $index -lt $sequence.Count; $index += 1) {
        $actual = $sequence[$index]
        Assert-ExactPropertyNames -Value $actual -Expected @('sequence', 'stage', 'weather', 'expected_wetness', 'advance_to') -Context "Browser weather checkpoint $index"
        $expectedAdvance = if ($null -eq $actual.advance_to) { '' } else { [string]$actual.advance_to }
        $actualValues = @(
            [string][int]$actual.sequence,
            [string]$actual.stage,
            [string]$actual.weather,
            [string][double]$actual.expected_wetness,
            $expectedAdvance
        )
        Assert-Condition -Condition ((ConvertTo-CompactJson -Value $actualValues) -ceq (ConvertTo-CompactJson -Value $expected[$index])) -Message "Checkpoint contract drifted at index $index."
    }

    Assert-Condition -Condition ($Config.fixed_context.scene -ceq 'visual_valley') -Message 'The fixed scene changed.'
    Assert-Condition -Condition ($Config.fixed_context.quality_mode -ceq 'fixed') -Message 'The fixed quality mode changed.'
    Assert-Condition -Condition ($Config.fixed_context.camera_owner -ceq 'visual_valley_player_controller') -Message 'The fixed camera owner changed.'
    Assert-Condition -Condition ([int]$Config.fixed_context.active_camera_count -eq 1) -Message 'Exactly one active camera is required.'
    Assert-Condition -Condition ($Config.fixed_context.block_browser_input -eq $true) -Message 'Browser input must remain blocked.'
    $expectedBlockedEvents = @(
        'keydown', 'keyup', 'keypress',
        'mousedown', 'mouseup', 'mousemove', 'wheel', 'click', 'dblclick', 'contextmenu',
        'touchstart', 'touchmove', 'touchend', 'touchcancel',
        'pointerdown', 'pointerup', 'pointermove', 'pointercancel'
    )
    Assert-Condition -Condition ((ConvertTo-CompactJson -Value @($Config.fixed_context.blocked_event_types)) -ceq (ConvertTo-CompactJson -Value $expectedBlockedEvents)) -Message 'The exact Browser input guard event set changed.'

    Assert-Condition -Condition ([int]$Config.acceptance.required_checkpoints_per_tier -eq 3) -Message 'Three checkpoints per tier are required.'
    Assert-Condition -Condition ([int]$Config.acceptance.required_page_captures_per_tier -eq 3) -Message 'Three page captures per tier are required.'
    Assert-Condition -Condition ([int]$Config.acceptance.required_canvas_captures_per_tier -eq 3) -Message 'Three canvas captures per tier are required.'
    foreach ($counter in @('maximum_console_errors', 'maximum_console_warnings', 'maximum_network_failures')) {
        Assert-Condition -Condition ([int]$Config.acceptance.$counter -eq 0) -Message "Acceptance counter changed: $counter"
    }
    foreach ($flag in @('require_exact_served_bundles', 'require_low_bloom_absent', 'require_high_bloom_present', 'require_clear_restored_exactly', 'human_inspection_required', 'weather_shader_consumption_required_for_route_closure', 'water_draw_attribution_required_for_route_closure', 'exact_render_frame_identity_required_for_route_closure')) {
        Assert-Condition -Condition ($Config.acceptance.$flag -eq $true) -Message "Required acceptance boundary is not true: $flag"
    }
}

function Get-ExactRoute {
    param(
        [Parameter(Mandatory)][uri]$Root,
        [Parameter(Mandatory)][object]$Config,
        [Parameter(Mandatory)][ValidateSet('low', 'high')][string]$QualityTier
    )

    Assert-Condition -Condition ($Root.Scheme -ceq 'http') -Message 'The local Trunk endpoint must use http.'
    Assert-Condition -Condition ($Root.AbsoluteUri -ceq [string]$Config.server.origin) -Message 'BaseUrl does not match the registered Trunk origin.'
    Assert-Condition -Condition (Test-LoopbackHost -HostName $Root.DnsSafeHost) -Message 'The Trunk endpoint must resolve only to loopback.'
    Assert-Condition -Condition ([string]::IsNullOrEmpty($Root.Query)) -Message 'BaseUrl must not contain a query.'
    Assert-Condition -Condition ([string]::IsNullOrEmpty($Root.Fragment)) -Message 'BaseUrl must not contain a fragment.'
    Assert-Condition -Condition (@($Config.route.quality_tiers) -ccontains $QualityTier) -Message "Unregistered quality tier: $QualityTier"
    $builder = [UriBuilder]::new($Root)
    $builder.Path = [string]$Config.route.path
    $builder.Query = @(
        "forge_scene=$([uri]::EscapeDataString([string]$Config.route.query.forge_scene))",
        "forge_weather=$([uri]::EscapeDataString([string]$Config.route.query.forge_weather))",
        "forge_quality=$([uri]::EscapeDataString($QualityTier))",
        "forge_gate1_weather_proof=$([uri]::EscapeDataString([string]$Config.route.query.forge_gate1_weather_proof))"
    ) -join '&'
    return $builder.Uri.AbsoluteUri
}

function Assert-ExternalOutputRoot {
    param([Parameter(Mandatory)][string]$Candidate)

    $resolved = [IO.Path]::GetFullPath($Candidate)
    $repositoryPrefix = $repositoryRoot.TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar
    ) + [IO.Path]::DirectorySeparatorChar
    Assert-Condition -Condition (
        -not $resolved.Equals($repositoryRoot, [StringComparison]::OrdinalIgnoreCase) -and
        -not $resolved.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)
    ) -Message 'Browser weather evidence must be written outside the Git worktree.'
    return $resolved
}

function Invoke-SelfTest {
    param([Parameter(Mandatory)][object]$Config)

    Assert-WeatherMatrixConfig -Config $Config
    Assert-Condition -Condition ((Get-ExactRoute -Root ([uri]'http://127.0.0.1:1420/') -Config $Config -QualityTier low) -ceq 'http://127.0.0.1:1420/?forge_scene=visual-valley&forge_weather=clear&forge_quality=low&forge_gate1_weather_proof=v1') -Message 'The Low proof route is not deterministic.'
    Assert-Condition -Condition ((Get-ExactRoute -Root ([uri]'http://127.0.0.1:1420/') -Config $Config -QualityTier high) -ceq 'http://127.0.0.1:1420/?forge_scene=visual-valley&forge_weather=clear&forge_quality=high&forge_gate1_weather_proof=v1') -Message 'The High proof route is not deterministic.'

    $badSettle = Copy-JsonValue -Value $Config
    $badSettle.controller.minimum_settled_frames = 7
    Assert-Throws -Description 'seven settled frames' -Operation { Assert-WeatherMatrixConfig -Config $badSettle }

    $badOrder = Copy-JsonValue -Value $Config
    $badOrder.sequence[1].stage = 'restored_clear'
    Assert-Throws -Description 'reordered transition stages' -Operation { Assert-WeatherMatrixConfig -Config $badOrder }

    $badTiers = Copy-JsonValue -Value $Config
    $badTiers.route.quality_tiers = @('low', 'medium', 'high')
    Assert-Throws -Description 'an unregistered Medium tier' -Operation { Assert-WeatherMatrixConfig -Config $badTiers }

    $badClosure = Copy-JsonValue -Value $Config
    $badClosure.acceptance.water_draw_attribution_required_for_route_closure = $false
    Assert-Throws -Description 'removing the route-closure boundary' -Operation { Assert-WeatherMatrixConfig -Config $badClosure }

    $badInputGuard = Copy-JsonValue -Value $Config
    $badInputGuard.fixed_context.blocked_event_types = @($badInputGuard.fixed_context.blocked_event_types | Where-Object { $_ -cne 'click' })
    Assert-Throws -Description 'an incomplete Browser input guard' -Operation { Assert-WeatherMatrixConfig -Config $badInputGuard }

    $badUnknownProperty = Copy-JsonValue -Value $Config
    $badUnknownProperty | Add-Member -NotePropertyName 'unregistered' -NotePropertyValue $true
    Assert-Throws -Description 'an unknown config property' -Operation { Assert-WeatherMatrixConfig -Config $badUnknownProperty }

    Assert-Throws -Description 'a non-loopback Browser origin' -Operation {
        [void](Get-ExactRoute -Root ([uri]'http://192.0.2.1:1420/') -Config $Config -QualityTier low)
    }
    Assert-Throws -Description 'the Git worktree as evidence root' -Operation {
        [void](Assert-ExternalOutputRoot -Candidate $repositoryRoot)
    }
    Assert-Throws -Description 'a Git worktree child as evidence root' -Operation {
        [void](Assert-ExternalOutputRoot -Candidate (Join-Path $repositoryRoot 'artifacts'))
    }

    $zeroCounts = [ordered]@{}
    foreach ($eventType in @($Config.fixed_context.blocked_event_types)) {
        $zeroCounts[$eventType] = 0
    }
    $cleanInputGuard = Copy-JsonValue -Value ([ordered]@{
        active = $true
        blocked_event_types = @($Config.fixed_context.blocked_event_types)
        blocked_input_events = 0
        blocked_input_events_by_type = $zeroCounts
        first_blocked_input_event = $null
        last_blocked_input_event = $null
    })
    Assert-InputGuardSnapshot -InputGuard $cleanInputGuard -Config $Config

    $blockedPointerMove = Copy-JsonValue -Value ([ordered]@{
        type = 'pointermove'
        is_trusted = $true
        event_time_stamp_ms = 123.0
        observed_at_performance_ms = 456.0
        document_ready_state = 'complete'
        document_has_focus = $true
        visibility_state = 'visible'
        pointer_type = 'mouse'
        buttons = 0
        client_x = 960.0
        client_y = 540.0
        movement_x = 1.0
        movement_y = 0.0
    })
    $contaminatedInputGuard = Copy-JsonValue -Value $cleanInputGuard
    $contaminatedInputGuard.blocked_input_events = 1
    $contaminatedInputGuard.blocked_input_events_by_type.pointermove = 1
    $contaminatedInputGuard.first_blocked_input_event = Copy-JsonValue -Value $blockedPointerMove
    $contaminatedInputGuard.last_blocked_input_event = Copy-JsonValue -Value $blockedPointerMove
    $terminalInputMessage = $null
    try {
        Assert-InputGuardSnapshot -InputGuard $contaminatedInputGuard -Config $Config
    } catch {
        $terminalInputMessage = $_.Exception.Message
    }
    Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$terminalInputMessage) -and $terminalInputMessage.StartsWith('Terminal Browser input contamination:', [StringComparison]::Ordinal)) -Message 'A positive Browser input count did not produce the terminal bounded diagnostic.'

    $badInputCountSum = Copy-JsonValue -Value $cleanInputGuard
    $badInputCountSum.blocked_input_events = 1
    Assert-Throws -Description 'a divergent total/per-type Browser input count' -Operation { Assert-InputGuardSnapshot -InputGuard $badInputCountSum -Config $Config }

    $badInputCountProperty = Copy-JsonValue -Value $cleanInputGuard
    $badInputCountProperty.blocked_input_events_by_type | Add-Member -NotePropertyName 'drag' -NotePropertyValue 0
    Assert-Throws -Description 'an unknown Browser input count property' -Operation { Assert-InputGuardSnapshot -InputGuard $badInputCountProperty -Config $Config }

    $badMissingFirstDiagnostic = Copy-JsonValue -Value $contaminatedInputGuard
    $badMissingFirstDiagnostic.first_blocked_input_event = $null
    Assert-Throws -Description 'a contaminated guard without its first bounded diagnostic' -Operation { Assert-InputGuardSnapshot -InputGuard $badMissingFirstDiagnostic -Config $Config }

    $badMissingLastDiagnostic = Copy-JsonValue -Value $contaminatedInputGuard
    $badMissingLastDiagnostic.last_blocked_input_event = $null
    Assert-Throws -Description 'a contaminated guard without its last bounded diagnostic' -Operation { Assert-InputGuardSnapshot -InputGuard $badMissingLastDiagnostic -Config $Config }

    $badNegativeInputCount = Copy-JsonValue -Value $cleanInputGuard
    $badNegativeInputCount.blocked_input_events_by_type.mousemove = -1
    Assert-Throws -Description 'a negative per-type Browser input count' -Operation { Assert-InputGuardSnapshot -InputGuard $badNegativeInputCount -Config $Config }

    $badResetInputGuard = Copy-JsonValue -Value $contaminatedInputGuard
    $badResetInputGuard.blocked_input_events = 0
    $badResetInputGuard.blocked_input_events_by_type.pointermove = 0
    Assert-Throws -Description 'reset Browser input counters retaining bounded diagnostics' -Operation { Assert-InputGuardSnapshot -InputGuard $badResetInputGuard -Config $Config }

    $abortReceipt = New-BrowserTierAbortReceipt -Config $Config -ExpectedCommit ('a' * 40) -QualityTier high -Route 'http://127.0.0.1:1420/?forge_quality=high' -FailureMessage $terminalInputMessage -FailureType 'System.Management.Automation.RuntimeException' -Snapshot ([pscustomobject]@{
        input_guard = $contaminatedInputGuard
        proof_ledger = (ConvertTo-CompactJson -Value ([ordered]@{
            status = 'collecting'
            checkpoints = @([ordered]@{ sequence = 1 })
            runtime_instance_id = 'self-test-runtime'
            failure_reason = $null
        }))
    }) -SnapshotCaptureStatus captured_at_terminal_detection -SnapshotCaptureError $null -BrowserProcessId 1234 -ArtifactsPresent @()
    Assert-Condition -Condition ($abortReceipt.schema -ceq 'forge.gate1-browser-weather-tier-abort' -and $abortReceipt.failure.category -ceq 'input_contamination' -and $abortReceipt.automated_result.status -ceq 'abort' -and $abortReceipt.browser.snapshot_capture_status -ceq 'captured_at_terminal_detection' -and $abortReceipt.input_guard.blocked_input_events -eq 1 -and $abortReceipt.controller.checkpoint_count -eq 1 -and $abortReceipt.artifact_inventory_status -ceq 'complete' -and $abortReceipt.recorded_before_browser_cleanup -eq $true) -Message 'The bounded Browser tier ABORT receipt contract drifted.'

    [pscustomobject]@{
        status = 'pass'
        protocol = $Config.protocol_id
        rejected_mutations = 6
        rejected_boundary_cases = 3
        input_guard_mutations_rejected = 6
        terminal_input_contamination_cases = 1
        abort_receipt_contract_cases = 1
        tiers = @($Config.route.quality_tiers)
        checkpoints_per_tier = @($Config.sequence).Count
        browser_run = 'not_performed_by_self_test'
    }
}

function Invoke-GitText {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $output = @(& git -C $repositoryRoot @Arguments 2>&1 | ForEach-Object { "$_" })
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }
    return ($output -join [Environment]::NewLine).Trim()
}

function Get-GitIdentity {
    $status = Invoke-GitText -Arguments @('status', '--porcelain=v1', '--untracked-files=all')
    Assert-Condition -Condition ([string]::IsNullOrWhiteSpace($status)) -Message 'Browser weather acquisition requires a completely clean Git worktree.'
    $commit = Invoke-GitText -Arguments @('rev-parse', 'HEAD')
    $tree = Invoke-GitText -Arguments @('rev-parse', 'HEAD^{tree}')
    Assert-Condition -Condition ($commit -cmatch '^[0-9a-f]{40}$') -Message 'Unable to resolve the exact Git commit.'
    Assert-Condition -Condition ($tree -cmatch '^[0-9a-f]{40}$') -Message 'Unable to resolve the exact Git tree.'
    return [ordered]@{
        commit = $commit
        tree = $tree
        worktree = $repositoryRoot
        clean_before_acquisition = $true
    }
}

function Wait-NonGenericTask {
    param([Parameter(Mandatory)][System.Threading.Tasks.Task]$Task)

    [void]$Task.GetAwaiter().GetResult()
}

function Get-OptionalPropertyValue {
    param(
        [AllowNull()][object]$Object,
        [Parameter(Mandatory)][string]$Name,
        [AllowNull()][object]$Default = $null
    )

    if ($null -eq $Object) {
        return $Default
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $Default
    }
    return $property.Value
}

function Get-RemoteValueText {
    param([Parameter(Mandatory)][object]$RemoteObject)

    if ($RemoteObject.PSObject.Properties.Name -contains 'value') {
        if ($RemoteObject.value -is [string]) {
            return [string]$RemoteObject.value
        }
        return ConvertTo-CompactJson -Value $RemoteObject.value
    }
    $description = Get-OptionalPropertyValue -Object $RemoteObject -Name 'description'
    if (-not [string]::IsNullOrWhiteSpace([string]$description)) {
        return [string]$description
    }
    return [string]$RemoteObject.type
}

function Reset-CdpEvents {
    $script:CdpMessageId = 0
    $script:ConsoleEvents = [Collections.Generic.List[object]]::new()
    $script:NetworkFailures = [Collections.Generic.List[object]]::new()
    $script:NetworkRequests = @{}
    $script:NetworkResponses = @{}
}

function Add-CdpEvent {
    param([Parameter(Mandatory)][object]$Message)

    switch ([string]$Message.method) {
        'Runtime.consoleAPICalled' {
            $script:ConsoleEvents.Add([ordered]@{
                source = 'runtime_console'
                level = [string]$Message.params.type
                timestamp = [double]$Message.params.timestamp
                text = (@($Message.params.args) | ForEach-Object { Get-RemoteValueText -RemoteObject $_ }) -join ' '
            })
        }
        'Log.entryAdded' {
            $entry = $Message.params.entry
            $script:ConsoleEvents.Add([ordered]@{
                source = 'browser_log'
                level = [string]$entry.level
                timestamp = [double]$entry.timestamp
                text = [string]$entry.text
            })
        }
        'Network.requestWillBeSent' {
            $script:NetworkRequests[[string]$Message.params.requestId] = [ordered]@{
                url = [string]$Message.params.request.url
                method = [string]$Message.params.request.method
                type = [string]$Message.params.type
            }
        }
        'Network.responseReceived' {
            $response = $Message.params.response
            $script:NetworkResponses[[string]$Message.params.requestId] = [ordered]@{
                request_id = [string]$Message.params.requestId
                url = [string]$response.url
                status = [int]$response.status
                mime = [string]$response.mimeType
                type = [string]$Message.params.type
                protocol = [string](Get-OptionalPropertyValue -Object $response -Name 'protocol' -Default '')
            }
        }
        'Network.loadingFailed' {
            $requestId = [string]$Message.params.requestId
            $script:NetworkFailures.Add([ordered]@{
                request_id = $requestId
                url = if ($script:NetworkRequests.ContainsKey($requestId)) { [string]$script:NetworkRequests[$requestId].url } else { $null }
                type = [string]$Message.params.type
                error_text = [string]$Message.params.errorText
                canceled = [bool](Get-OptionalPropertyValue -Object $Message.params -Name 'canceled' -Default $false)
            })
        }
    }
}

function Receive-CdpMessage {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][System.Threading.CancellationToken]$CancellationToken
    )

    $stream = [IO.MemoryStream]::new()
    try {
        do {
            $buffer = [byte[]]::new(65536)
            $segment = [ArraySegment[byte]]::new($buffer)
            $result = $Socket.ReceiveAsync($segment, $CancellationToken).GetAwaiter().GetResult()
            if ($result.MessageType -eq [Net.WebSockets.WebSocketMessageType]::Close) {
                throw 'Browser DevTools closed the CDP connection.'
            }
            if ($result.Count -gt 0) {
                $stream.Write($buffer, 0, $result.Count)
            }
        } while (-not $result.EndOfMessage)
        return ConvertFrom-ExactJson -Json ([Text.Encoding]::UTF8.GetString($stream.ToArray()))
    } finally {
        $stream.Dispose()
    }
}

function Invoke-CdpCommand {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Method,
        [object]$Parameters = ([ordered]@{}),
        [ValidateRange(1, 180)][int]$TimeoutSeconds = 15
    )

    $script:CdpMessageId += 1
    $messageId = $script:CdpMessageId
    $payload = [Text.Encoding]::UTF8.GetBytes((ConvertTo-CompactJson -Value ([ordered]@{
        id = $messageId
        method = $Method
        params = $Parameters
    })))
    $timeout = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($TimeoutSeconds))
    try {
        $segment = [ArraySegment[byte]]::new($payload)
        Wait-NonGenericTask -Task ($Socket.SendAsync($segment, [Net.WebSockets.WebSocketMessageType]::Text, $true, $timeout.Token))
        while ($true) {
            $message = Receive-CdpMessage -Socket $Socket -CancellationToken $timeout.Token
            if ($message.PSObject.Properties.Name -contains 'id' -and [int]$message.id -eq $messageId) {
                if ($message.PSObject.Properties.Name -contains 'error') {
                    throw "CDP $Method failed: $($message.error.message)"
                }
                return $message.result
            }
            if ($message.PSObject.Properties.Name -contains 'method') {
                Add-CdpEvent -Message $message
            }
        }
    } finally {
        $timeout.Dispose()
    }
}

function Invoke-CdpExpression {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$Expression,
        [ValidateRange(1, 180)][int]$TimeoutSeconds = 15
    )

    $result = Invoke-CdpCommand -Socket $Socket -Method 'Runtime.evaluate' -Parameters ([ordered]@{
        expression = $Expression
        returnByValue = $true
        awaitPromise = $true
    }) -TimeoutSeconds $TimeoutSeconds
    if ($result.PSObject.Properties.Name -contains 'exceptionDetails') {
        throw "CDP expression raised an exception: $($result.exceptionDetails.text)"
    }
    if ($result.result.PSObject.Properties.Name -contains 'value') {
        return $result.result.value
    }
    return $null
}

function Connect-CdpSocket {
    param([Parameter(Mandatory)][string]$WebSocketUrl)

    $uri = [uri]$WebSocketUrl
    Assert-Condition -Condition ($uri.Scheme -ceq 'ws') -Message 'Only a local unencrypted CDP WebSocket is accepted.'
    Assert-Condition -Condition (Test-LoopbackHost -HostName $uri.DnsSafeHost) -Message 'The CDP WebSocket must resolve only to loopback.'
    $socket = [Net.WebSockets.ClientWebSocket]::new()
    $socket.Options.SetRequestHeader('Origin', 'http://127.0.0.1')
    $timeout = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(10))
    try {
        Wait-NonGenericTask -Task ($socket.ConnectAsync($uri, $timeout.Token))
    } catch {
        $socket.Dispose()
        throw "Unable to connect to Browser DevTools: $($_.Exception.Message)"
    } finally {
        $timeout.Dispose()
    }
    return $socket
}

function New-LoopbackPort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try {
        return ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    } finally {
        $listener.Stop()
    }
}

function Find-BrowserExecutable {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        Assert-Condition -Condition (Test-Path -LiteralPath $RequestedPath -PathType Leaf) -Message "Browser executable not found: $RequestedPath"
        return [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $RequestedPath).Path)
    }
    $candidates = @(
        (Join-Path $env:ProgramFiles 'Microsoft/Edge/Application/msedge.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/Edge/Application/msedge.exe'),
        (Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Google/Chrome/Application/chrome.exe'),
        (Join-Path $env:LOCALAPPDATA 'Google/Chrome/Application/chrome.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return [IO.Path]::GetFullPath($candidate)
        }
    }
    throw 'No supported Edge or Chrome executable was found. Pass -BrowserExecutable explicitly.'
}

function Start-ControlledBrowser {
    param(
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][int]$Port,
        [Parameter(Mandatory)][string]$ProfileDirectory,
        [Parameter(Mandatory)][object]$Config
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.UseShellExecute = $false
    foreach ($argument in @(
        "--remote-debugging-address=127.0.0.1",
        "--remote-debugging-port=$Port",
        '--remote-allow-origins=*',
        "--user-data-dir=$ProfileDirectory",
        '--no-first-run',
        '--no-default-browser-check',
        '--disable-background-networking',
        '--disable-component-update',
        '--disable-default-apps',
        '--disable-sync',
        '--metrics-recording-only',
        '--no-service-autorun',
        '--disable-background-timer-throttling',
        '--disable-backgrounding-occluded-windows',
        '--disable-renderer-backgrounding',
        '--force-device-scale-factor=1',
        "--window-size=$($Config.browser.viewport_width_px),$($Config.browser.viewport_height_px)",
        'about:blank'
    )) {
        [void]$startInfo.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::Start($startInfo)
    Assert-Condition -Condition ($null -ne $process) -Message 'Browser process did not start.'
    return $process
}

function Select-CdpTarget {
    param(
        [Parameter(Mandatory)][object[]]$Targets,
        [Parameter(Mandatory)][int]$Port
    )

    $target = @($Targets | Where-Object {
        $_.type -ceq 'page' -and
        -not [string]::IsNullOrWhiteSpace([string]$_.id) -and
        -not [string]::IsNullOrWhiteSpace([string]$_.webSocketDebuggerUrl)
    } | Select-Object -First 1)
    if ($target.Count -ne 1) {
        return $null
    }
    try {
        $wsUri = [uri]([string]$target[0].webSocketDebuggerUrl)
    } catch {
        return $null
    }
    if ($wsUri.Scheme -ceq 'ws' -and $wsUri.Port -eq $Port -and (Test-LoopbackHost -HostName $wsUri.DnsSafeHost)) {
        return $target[0]
    }
    return $null
}

function Wait-CdpTarget {
    param(
        [Parameter(Mandatory)][int]$Port,
        [Parameter(Mandatory)][Diagnostics.Process]$Process
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(30)
    $lastError = 'endpoint not ready'
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if ($Process.HasExited) {
            throw "Browser exited before exposing DevTools (exit code $($Process.ExitCode))."
        }
        try {
            $targets = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/json/list" -TimeoutSec 2 -NoProxy
            $target = Select-CdpTarget -Targets @($targets) -Port $Port
            if ($null -ne $target) {
                return $target
            }
            $lastError = 'DevTools target did not use the reserved loopback endpoint.'
        } catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 200
    }
    throw "Browser DevTools target did not open. Last error: $lastError"
}

function Get-BrowserSnapshot {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][object]$Config
    )

    $proofId = [string]$Config.controller.ledger_element_id
    $raw = Invoke-CdpExpression -Socket $Socket -Expression @"
(() => {
  const gateNode = document.getElementById("gate0-telemetry-ledger");
  const sceneNode = document.getElementById("forge-scene-telemetry-ledger");
  const proofNode = document.getElementById("$proofId");
  const canvas = document.getElementById("forge-canvas");
  const rect = canvas?.getBoundingClientRect();
  return JSON.stringify({
    document_ready_state: document.readyState,
    navigator_gpu: Boolean(navigator.gpu),
    input_guard: window.__forgeGate1BrowserWeatherMatrixInputGuard ?? null,
    gate_ledger: gateNode?.textContent ?? "",
    scene_ledger: sceneNode?.textContent ?? "",
    proof_ledger: proofNode?.textContent ?? "",
    page: {
      inner_width: window.innerWidth,
      inner_height: window.innerHeight,
      client_width: document.documentElement.clientWidth,
      client_height: document.documentElement.clientHeight,
      scroll_width: document.documentElement.scrollWidth,
      scroll_height: document.documentElement.scrollHeight
    },
    canvas: rect ? {
      left: rect.left,
      top: rect.top,
      width: rect.width,
      height: rect.height,
      scroll_x: window.scrollX,
      scroll_y: window.scrollY,
      backing_width: canvas.width,
      backing_height: canvas.height,
      device_pixel_ratio: window.devicePixelRatio
    } : null
  });
})()
"@
    return ConvertFrom-ExactJson -Json ([string]$raw)
}

function Assert-BlockedInputEventDiagnostic {
    param(
        [Parameter(Mandatory)][object]$Diagnostic,
        [Parameter(Mandatory)][object]$Config,
        [Parameter(Mandatory)][string]$Context
    )

    Assert-ExactPropertyNames -Value $Diagnostic -Expected @(
        'type', 'is_trusted', 'event_time_stamp_ms', 'observed_at_performance_ms',
        'document_ready_state', 'document_has_focus', 'visibility_state', 'pointer_type',
        'buttons', 'client_x', 'client_y', 'movement_x', 'movement_y'
    ) -Context $Context
    Assert-Condition -Condition (@($Config.fixed_context.blocked_event_types) -ccontains [string]$Diagnostic.type) -Message "$Context has an unregistered event type."
    Assert-Condition -Condition ($Diagnostic.is_trusted -is [bool]) -Message "$Context is_trusted must be boolean."
    Assert-Condition -Condition ($Diagnostic.document_has_focus -is [bool]) -Message "$Context document_has_focus must be boolean."
    Assert-Condition -Condition ([string]$Diagnostic.document_ready_state -cin @('loading', 'interactive', 'complete')) -Message "$Context document_ready_state is invalid."
    Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$Diagnostic.visibility_state)) -Message "$Context visibility_state is empty."
    foreach ($requiredNumber in @('event_time_stamp_ms', 'observed_at_performance_ms')) {
        Assert-Condition -Condition ($null -ne $Diagnostic.$requiredNumber) -Message "$Context $requiredNumber must not be null."
        $number = [double]$Diagnostic.$requiredNumber
        Assert-Condition -Condition (-not [double]::IsNaN($number) -and -not [double]::IsInfinity($number) -and $number -ge 0) -Message "$Context $requiredNumber must be finite and non-negative."
    }
    foreach ($optionalNumber in @('client_x', 'client_y', 'movement_x', 'movement_y')) {
        $value = $Diagnostic.$optionalNumber
        if ($null -ne $value) {
            $number = [double]$value
            Assert-Condition -Condition (-not [double]::IsNaN($number) -and -not [double]::IsInfinity($number)) -Message "$Context $optionalNumber must be null or finite."
        }
    }
    if ($null -ne $Diagnostic.buttons) {
        $buttons = [double]$Diagnostic.buttons
        Assert-Condition -Condition (-not [double]::IsNaN($buttons) -and -not [double]::IsInfinity($buttons) -and $buttons -ge 0 -and $buttons -eq [Math]::Truncate($buttons)) -Message "$Context buttons must be null or a non-negative integer."
    }
    Assert-Condition -Condition ($null -eq $Diagnostic.pointer_type -or $Diagnostic.pointer_type -is [string]) -Message "$Context pointer_type must be null or a string."
}

function Assert-InputGuardSnapshot {
    param(
        [Parameter(Mandatory)][object]$InputGuard,
        [Parameter(Mandatory)][object]$Config
    )

    Assert-ExactPropertyNames -Value $InputGuard -Expected @(
        'active', 'blocked_event_types', 'blocked_input_events',
        'blocked_input_events_by_type', 'first_blocked_input_event',
        'last_blocked_input_event'
    ) -Context 'Browser weather input guard'
    Assert-Condition -Condition ($InputGuard.active -eq $true) -Message 'The Browser weather input guard is not active.'
    $blockedEventTypes = @($Config.fixed_context.blocked_event_types)
    Assert-Condition -Condition ((ConvertTo-CompactJson -Value @($InputGuard.blocked_event_types)) -ceq (ConvertTo-CompactJson -Value $blockedEventTypes)) -Message 'The installed Browser input guard event set drifted.'
    Assert-ExactPropertyNames -Value $InputGuard.blocked_input_events_by_type -Expected $blockedEventTypes -Context 'Browser weather input guard per-type counts'

    Assert-Condition -Condition ($null -ne $InputGuard.blocked_input_events) -Message 'Browser weather input guard total must not be null.'
    $blockedInputEventsNumber = [double]$InputGuard.blocked_input_events
    Assert-Condition -Condition (-not [double]::IsNaN($blockedInputEventsNumber) -and -not [double]::IsInfinity($blockedInputEventsNumber) -and $blockedInputEventsNumber -ge 0 -and $blockedInputEventsNumber -eq [Math]::Truncate($blockedInputEventsNumber) -and $blockedInputEventsNumber -le [long]::MaxValue) -Message 'Browser weather input guard total must be a non-negative integer.'
    $blockedInputEvents = [long]$InputGuard.blocked_input_events
    $countedByType = [long]0
    foreach ($eventType in $blockedEventTypes) {
        $eventCountValue = $InputGuard.blocked_input_events_by_type.PSObject.Properties[$eventType].Value
        Assert-Condition -Condition ($null -ne $eventCountValue) -Message "Browser weather input guard count for '$eventType' must not be null."
        $eventCountNumber = [double]$eventCountValue
        Assert-Condition -Condition (-not [double]::IsNaN($eventCountNumber) -and -not [double]::IsInfinity($eventCountNumber) -and $eventCountNumber -ge 0 -and $eventCountNumber -eq [Math]::Truncate($eventCountNumber) -and $eventCountNumber -le [long]::MaxValue) -Message "Browser weather input guard count for '$eventType' must be a non-negative integer."
        $eventCount = [long]$eventCountValue
        Assert-Condition -Condition ($countedByType -le [long]::MaxValue - $eventCount) -Message 'Browser weather input guard per-type count sum overflowed.'
        $countedByType += $eventCount
    }
    Assert-Condition -Condition ($countedByType -eq $blockedInputEvents) -Message 'Browser weather input guard total and per-type counts diverged.'

    if ($blockedInputEvents -eq 0) {
        Assert-Condition -Condition ($null -eq $InputGuard.first_blocked_input_event -and $null -eq $InputGuard.last_blocked_input_event) -Message 'A clean Browser input guard must not expose first/last event diagnostics.'
        return
    }

    Assert-Condition -Condition ($null -ne $InputGuard.first_blocked_input_event -and $null -ne $InputGuard.last_blocked_input_event) -Message 'A contaminated Browser input guard must expose bounded first/last diagnostics.'
    Assert-BlockedInputEventDiagnostic -Diagnostic $InputGuard.first_blocked_input_event -Config $Config -Context 'First blocked Browser input event'
    Assert-BlockedInputEventDiagnostic -Diagnostic $InputGuard.last_blocked_input_event -Config $Config -Context 'Last blocked Browser input event'
    foreach ($diagnostic in @($InputGuard.first_blocked_input_event, $InputGuard.last_blocked_input_event)) {
        $diagnosticType = [string]$diagnostic.type
        $eventCount = [long]$InputGuard.blocked_input_events_by_type.PSObject.Properties[$diagnosticType].Value
        Assert-Condition -Condition ($eventCount -gt 0) -Message 'A bounded Browser input diagnostic has no matching per-type count.'
    }
    $boundedDiagnostic = [ordered]@{
        blocked_input_events = $blockedInputEvents
        blocked_input_events_by_type = $InputGuard.blocked_input_events_by_type
        first_blocked_input_event = $InputGuard.first_blocked_input_event
        last_blocked_input_event = $InputGuard.last_blocked_input_event
    }
    throw "Terminal Browser input contamination: $(ConvertTo-CompactJson -Value $boundedDiagnostic)"
}

function Assert-BaseSnapshot {
    param(
        [Parameter(Mandatory)][object]$Snapshot,
        [Parameter(Mandatory)][object]$Config,
        [Parameter(Mandatory)][string]$ExpectedCommit,
        [Parameter(Mandatory)][ValidateSet('low', 'high')][string]$QualityTier
    )

    Assert-Condition -Condition ($Snapshot.document_ready_state -ceq 'complete') -Message 'Document is not complete.'
    Assert-Condition -Condition ($Snapshot.navigator_gpu -eq $true) -Message 'navigator.gpu is not exposed.'
    Assert-Condition -Condition ($null -ne $Snapshot.input_guard) -Message 'The Browser weather input guard is missing.'
    Assert-InputGuardSnapshot -InputGuard $Snapshot.input_guard -Config $Config
    Assert-Condition -Condition ([int]$Snapshot.page.inner_width -eq [int]$Config.browser.viewport_width_px -and [int]$Snapshot.page.inner_height -eq [int]$Config.browser.viewport_height_px) -Message 'The Browser viewport drifted from 1920x1080.'
    Assert-Condition -Condition ([int]$Snapshot.page.client_width -eq [int]$Snapshot.page.inner_width -and [int]$Snapshot.page.client_height -eq [int]$Snapshot.page.inner_height) -Message 'The Browser page exposes a global scrollbar.'
    Assert-Condition -Condition ([int]$Snapshot.page.scroll_width -le [int]$Snapshot.page.client_width -and [int]$Snapshot.page.scroll_height -le [int]$Snapshot.page.client_height) -Message 'The Browser document exceeds the registered viewport.'
    Assert-Condition -Condition ($null -ne $Snapshot.canvas) -Message 'The canonical canvas is missing.'
    Assert-Condition -Condition ([double]$Snapshot.canvas.width -gt 0 -and [double]$Snapshot.canvas.height -gt 0) -Message 'The canvas has no positive CSS extent.'
    Assert-Condition -Condition ([double]$Snapshot.canvas.device_pixel_ratio -eq [double]$Config.browser.device_scale_factor) -Message 'The Browser device pixel ratio drifted.'
    Assert-Condition -Condition ([double]$Snapshot.canvas.scroll_x -eq 0 -and [double]$Snapshot.canvas.scroll_y -eq 0) -Message 'The Browser page moved away from the scroll origin.'
    Assert-Condition -Condition ([double]$Snapshot.canvas.left -ge 0 -and [double]$Snapshot.canvas.top -ge 0 -and ([double]$Snapshot.canvas.left + [double]$Snapshot.canvas.width) -le [double]$Snapshot.page.inner_width -and ([double]$Snapshot.canvas.top + [double]$Snapshot.canvas.height) -le [double]$Snapshot.page.inner_height) -Message 'The full canvas is not visible.'
    Assert-Condition -Condition ([int]$Snapshot.canvas.backing_width -eq [int][Math]::Round([double]$Snapshot.canvas.width * [double]$Snapshot.canvas.device_pixel_ratio) -and [int]$Snapshot.canvas.backing_height -eq [int][Math]::Round([double]$Snapshot.canvas.height * [double]$Snapshot.canvas.device_pixel_ratio)) -Message 'The canvas backing store does not match CSS extent and DPR.'
    foreach ($name in @('gate_ledger', 'scene_ledger', 'proof_ledger')) {
        Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$Snapshot.$name)) -Message "Browser ledger is empty: $name"
    }
    $gate = ConvertFrom-ExactJson -Json ([string]$Snapshot.gate_ledger)
    $scene = ConvertFrom-ExactJson -Json ([string]$Snapshot.scene_ledger)
    Assert-Condition -Condition ($gate.schema -ceq 'forge.gate0-studio-telemetry') -Message 'Unexpected Studio gate ledger.'
    Assert-Condition -Condition ($gate.target -ceq 'wasm_browser') -Message 'Studio target is not wasm_browser.'
    Assert-Condition -Condition ([int]$gate.viewport.starts -eq 1) -Message 'Studio viewport did not start exactly once.'
    Assert-Condition -Condition ([int]$gate.frame.warmup_samples -eq 60 -and [int]$gate.frame.samples -eq 120) -Message 'Studio frame window is incomplete.'
    Assert-Condition -Condition ($scene.schema -ceq 'forge.studio-scene-telemetry' -and [int]$scene.schema_version -ge 8) -Message 'Unexpected Studio scene ledger.'
    Assert-Condition -Condition ($scene.build_source_commit -ceq $ExpectedCommit) -Message 'The served Studio bundle is not bound to the exact Git commit.'
    Assert-Condition -Condition ($scene.parity_status -ceq 'source_manifest_matched' -and [string]::IsNullOrWhiteSpace([string]$scene.failure_reason)) -Message 'Scene source/runtime parity failed.'
    Assert-Condition -Condition ($scene.selected_manifest.scene -ceq 'visual_valley') -Message 'Scene selection drifted.'
    Assert-Condition -Condition ($scene.selected_manifest.weather_state -ceq 'clear') -Message 'The selected manifest must remain the initial clear route.'
    Assert-Condition -Condition ($scene.selected_manifest.quality_selection.mode -ceq 'fixed' -and $scene.selected_manifest.quality_selection.tier -ceq $QualityTier) -Message 'Selected quality tier drifted.'
    Assert-Condition -Condition ($scene.selected_manifest.camera_owner -ceq 'visual_valley_player_controller') -Message 'Camera owner drifted.'
    return [pscustomobject]@{
        Gate = $gate
        Scene = $scene
        Proof = ConvertFrom-ExactJson -Json ([string]$Snapshot.proof_ledger)
    }
}

function Assert-ProofLedger {
    param(
        [Parameter(Mandatory)][object]$Ledger,
        [Parameter(Mandatory)][object]$Config,
        [Parameter(Mandatory)][string]$ExpectedCommit,
        [Parameter(Mandatory)][ValidateSet('low', 'high')][string]$QualityTier,
        [Parameter(Mandatory)][ValidateRange(1, 3)][int]$ExpectedCheckpointCount
    )

    Assert-ExactPropertyNames -Value $Ledger -Expected @(
        'schema', 'schema_version', 'protocol_id', 'build_source_commit', 'runtime_instance_id',
        'requested_quality_tier', 'status', 'checkpoints', 'failure_reason',
        'same_bevy_instance_required', 'minimum_settled_frames', 'transition_timeout_ms',
        'water_camera_anchor_x_m', 'water_camera_anchor_z_m', 'water_camera_view_distance_m',
        'browser_captures_performed_by_controller', 'weather_shader_consumption_proven',
        'water_draw_attribution_proven', 'exact_render_frame_identity_proven',
        'human_review_performed', 'gate1_routes_qualified', 'limitations'
    ) -Context 'Rust Browser weather proof ledger'
    Assert-Condition -Condition ($Ledger.schema -ceq 'forge.gate1-browser-weather-proof-ledger' -and [int]$Ledger.schema_version -eq 1) -Message 'Unexpected Rust Browser weather proof ledger.'
    Assert-Condition -Condition ($Ledger.protocol_id -ceq $Config.protocol_id) -Message 'Rust and driver protocol ids diverged.'
    Assert-Condition -Condition ($Ledger.build_source_commit -ceq $ExpectedCommit) -Message 'Rust proof ledger is not bound to the exact Git commit.'
    Assert-Condition -Condition ($Ledger.requested_quality_tier -ceq $QualityTier) -Message 'Rust proof ledger quality tier drifted.'
    Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$Ledger.runtime_instance_id)) -Message 'Rust proof runtime instance id is empty.'
    Assert-Condition -Condition ($Ledger.same_bevy_instance_required -eq $true) -Message 'Rust proof did not retain its same-instance requirement.'
    Assert-Condition -Condition ([int]$Ledger.minimum_settled_frames -eq [int]$Config.controller.minimum_settled_frames) -Message 'Rust proof settle contract drifted.'
    Assert-Condition -Condition ([int]$Ledger.transition_timeout_ms -eq [int]$Config.controller.transition_timeout_ms) -Message 'Rust and driver transition timeouts diverged.'
    Assert-Condition -Condition ([double]$Ledger.water_camera_anchor_x_m -eq [double]$Config.controller.water_camera.anchor_x_m -and [double]$Ledger.water_camera_anchor_z_m -eq [double]$Config.controller.water_camera.anchor_z_m -and [double]$Ledger.water_camera_view_distance_m -eq [double]$Config.controller.water_camera.view_distance_m) -Message 'Rust and driver water camera contracts diverged.'
    Assert-Condition -Condition ([string]::IsNullOrWhiteSpace([string]$Ledger.failure_reason)) -Message "Rust proof failed: $($Ledger.failure_reason)"
    Assert-Condition -Condition ($Ledger.browser_captures_performed_by_controller -eq $false) -Message 'The Rust controller must not claim external CDP captures.'
    Assert-Condition -Condition ($Ledger.weather_shader_consumption_proven -eq $false) -Message 'The Rust controller invented shader-consumption proof.'
    Assert-Condition -Condition ($Ledger.water_draw_attribution_proven -eq $false) -Message 'The Rust controller invented water draw attribution.'
    Assert-Condition -Condition ($Ledger.exact_render_frame_identity_proven -eq $false) -Message 'The Rust controller invented exact render-frame identity.'
    Assert-Condition -Condition ($Ledger.human_review_performed -eq $false -and $Ledger.gate1_routes_qualified -eq $false) -Message 'The Rust controller improperly closed human or Gate 1 routes.'

    $checkpoints = @($Ledger.checkpoints)
    Assert-Condition -Condition ($checkpoints.Count -eq $ExpectedCheckpointCount) -Message "Expected $ExpectedCheckpointCount Rust checkpoints, observed $($checkpoints.Count)."
    $fixedCameraPose = $null
    for ($index = 0; $index -lt $checkpoints.Count; $index += 1) {
        $checkpoint = $checkpoints[$index]
        $registered = @($Config.sequence)[$index]
        Assert-ExactPropertyNames -Value $checkpoint -Expected @(
            'sequence', 'stage', 'recorded_at_utc', 'runtime_instance_id', 'viewport_starts',
            'requested_weather', 'runtime_target_weather', 'weather_projection_status',
            'weather_evidence_source', 'weather_gpu_execution_observed', 'current_wetness',
            'target_wetness', 'terrain_materials', 'vegetation_materials',
            'terrain_uniforms_match', 'vegetation_uniforms_match',
            'clear_target_restored_exactly', 'rain_visible', 'quality_current_tier',
            'quality_applied_tier', 'settled_frames', 'minimum_settled_frames',
            'weather_simulation_seconds', 'camera_pose', 'camera_pose_matches',
            'active_camera_count', 'water', 'post_processing', 'gpu_presentation'
        ) -Context "Rust Browser weather checkpoint $index"
        Assert-ExactPropertyNames -Value $checkpoint.camera_pose -Expected @('translation_m', 'target_m', 'vertical_fov_degrees') -Context "Rust Browser weather camera pose $index"
        Assert-ExactPropertyNames -Value $checkpoint.water -Expected @(
            'runtime_observation', 'shader_material_assets', 'shader_material_entities',
            'shader_uniforms_match_profile', 'requested_reflection_intensity',
            'applied_material_reflectance', 'requested_reflection_steps',
            'reflection_intensity_status', 'requested_refraction_strength',
            'applied_specular_transmission', 'requested_refraction_steps',
            'screen_space_transmission_steps', 'screen_space_transmission_quality',
            'screen_space_transmission_blur_taps', 'refraction_status',
            'environment_reflection_status', 'screen_space_transmission_status',
            'cpu_ecs_application_status', 'gpu_pass_observed', 'webgpu_execution_status'
        ) -Context "Rust Browser weather water observation $index"
        Assert-ExactPropertyNames -Value $checkpoint.post_processing -Expected @(
            'bloom_requested', 'bloom_component_present', 'color_grading_component_present',
            'mapped_exposure_ev', 'mapped_temperature', 'mapped_tint',
            'mapped_post_saturation', 'mapped_contrast', 'mapped_gamma'
        ) -Context "Rust Browser weather post-processing observation $index"
        $cameraPose = ConvertTo-CompactJson -Value $checkpoint.camera_pose
        if ($null -eq $fixedCameraPose) {
            $fixedCameraPose = $cameraPose
        } else {
            Assert-Condition -Condition ($cameraPose -ceq $fixedCameraPose) -Message 'The registered water camera pose changed inside one Bevy instance.'
        }
        Assert-Condition -Condition ([int]$checkpoint.sequence -eq [int]$registered.sequence -and $checkpoint.stage -ceq [string]$registered.stage) -Message "Rust checkpoint order mismatch at index $index."
        Assert-Condition -Condition ($checkpoint.runtime_instance_id -ceq $Ledger.runtime_instance_id) -Message 'A checkpoint changed Bevy runtime instance.'
        Assert-Condition -Condition ([int]$checkpoint.viewport_starts -eq 1 -and [int]$checkpoint.active_camera_count -eq 1) -Message 'A checkpoint did not retain one viewport and one active camera.'
        Assert-Condition -Condition ($checkpoint.requested_weather -ceq [string]$registered.weather -and $checkpoint.runtime_target_weather -ceq [string]$registered.weather) -Message "Weather target mismatch at checkpoint $($checkpoint.sequence)."
        Assert-Condition -Condition ([Math]::Abs([double]$checkpoint.current_wetness - [double]$registered.expected_wetness) -le 0.000001 -and [Math]::Abs([double]$checkpoint.target_wetness - [double]$registered.expected_wetness) -le 0.000001) -Message "Wetness endpoint mismatch at checkpoint $($checkpoint.sequence)."
        Assert-Condition -Condition ([int]$checkpoint.settled_frames -ge [int]$Config.controller.minimum_settled_frames -and [int]$checkpoint.minimum_settled_frames -eq [int]$Config.controller.minimum_settled_frames) -Message "Settle window mismatch at checkpoint $($checkpoint.sequence)."
        Assert-Condition -Condition ($checkpoint.weather_projection_status -ceq 'observed_cpu_projection' -and $checkpoint.weather_evidence_source -ceq 'main_world_ecs_and_cpu_material_assets_not_gpu_execution') -Message 'Weather evidence source drifted or overclaimed GPU execution.'
        Assert-Condition -Condition ($checkpoint.weather_gpu_execution_observed -eq $false -and $checkpoint.gpu_presentation -ceq 'not_observed') -Message 'Weather checkpoint improperly claims GPU execution or presentation.'
        Assert-Condition -Condition ([int]$checkpoint.terrain_materials -gt 0 -and [int]$checkpoint.vegetation_materials -gt 0 -and $checkpoint.terrain_uniforms_match -eq $true -and $checkpoint.vegetation_uniforms_match -eq $true) -Message 'Wetness material projection is incomplete.'
        Assert-Condition -Condition ($checkpoint.quality_current_tier -ceq $QualityTier -and $checkpoint.quality_applied_tier -ceq $QualityTier) -Message 'Applied quality tier drifted.'
        Assert-Condition -Condition ($checkpoint.camera_pose_matches -eq $true) -Message 'The registered water camera pose was not retained.'
        Assert-Condition -Condition ($checkpoint.water.runtime_observation -ceq 'observed_main_world_components' -and $checkpoint.water.shader_uniforms_match_profile -eq $true) -Message 'Water main-world projection is incomplete.'
        Assert-Condition -Condition ($checkpoint.water.gpu_pass_observed -eq $false -and $checkpoint.water.webgpu_execution_status -ceq 'not_observed_by_water_metrics') -Message 'Water checkpoint improperly claims a WebGPU pass.'
        Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$checkpoint.water.reflection_intensity_status) -and -not [string]::IsNullOrWhiteSpace([string]$checkpoint.water.refraction_status)) -Message 'Water optics diagnostics are incomplete.'
        Assert-Condition -Condition ($checkpoint.post_processing.color_grading_component_present -eq $true) -Message 'Color grading component is absent.'
        if ($QualityTier -ceq 'low') {
            Assert-Condition -Condition ($checkpoint.post_processing.bloom_requested -eq $false -and $checkpoint.post_processing.bloom_component_present -eq $false) -Message 'Low must keep bloom absent.'
        } else {
            Assert-Condition -Condition ($checkpoint.post_processing.bloom_requested -eq $true -and $checkpoint.post_processing.bloom_component_present -eq $true) -Message 'High must keep bloom present.'
        }
        if ($registered.weather -ceq 'clear') {
            Assert-Condition -Condition ($checkpoint.clear_target_restored_exactly -eq $true -and $checkpoint.rain_visible -eq $false) -Message 'Clear endpoint was not restored exactly.'
        } else {
            Assert-Condition -Condition ($checkpoint.rain_visible -eq $true) -Message 'Rain endpoint did not expose the rain batch.'
        }
    }
    if ($ExpectedCheckpointCount -eq 3) {
        Assert-Condition -Condition ($Ledger.status -ceq 'complete_pending_external_validation') -Message 'Final Rust proof status is not complete_pending_external_validation.'
    }
}

function Wait-ProofCheckpoint {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][object]$Config,
        [Parameter(Mandatory)][string]$ExpectedCommit,
        [Parameter(Mandatory)][ValidateSet('low', 'high')][string]$QualityTier,
        [Parameter(Mandatory)][ValidateRange(1, 3)][int]$CheckpointCount
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds([int]$Config.readiness.timeout_seconds)
    $lastState = 'proof ledger not observed'
    $stableLayoutObservations = 0
    $lastCanvasSignature = $null
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $snapshot = Get-BrowserSnapshot -Socket $Socket -Config $Config
        try {
            $ledgers = Assert-BaseSnapshot -Snapshot $snapshot -Config $Config -ExpectedCommit $ExpectedCommit -QualityTier $QualityTier
            Assert-ProofLedger -Ledger $ledgers.Proof -Config $Config -ExpectedCommit $ExpectedCommit -QualityTier $QualityTier -ExpectedCheckpointCount $CheckpointCount
            $signature = ConvertTo-CompactJson -Value ([ordered]@{ page = $snapshot.page; canvas = $snapshot.canvas })
            if ($signature -ceq $lastCanvasSignature) {
                $stableLayoutObservations += 1
            } else {
                $lastCanvasSignature = $signature
                $stableLayoutObservations = 1
            }
            $lastState = "checkpoint $CheckpointCount ready; layout stable $stableLayoutObservations/$([int]$Config.readiness.minimum_stable_layout_observations)"
            if ($stableLayoutObservations -ge [int]$Config.readiness.minimum_stable_layout_observations) {
                return [pscustomobject]@{ Snapshot = $snapshot; Ledgers = $ledgers }
            }
        } catch {
            $lastState = $_.Exception.Message
            $stableLayoutObservations = 0
            $lastCanvasSignature = $null
            $inputGuard = Get-OptionalPropertyValue -Object $snapshot -Name 'input_guard'
            $blockedInputEvents = Get-OptionalPropertyValue -Object $inputGuard -Name 'blocked_input_events'
            if ($null -ne $blockedInputEvents -and [long]$blockedInputEvents -gt 0) {
                $terminalInputException = [InvalidOperationException]::new($lastState)
                $terminalInputException.Data['BrowserWeatherAbortSnapshot'] = [pscustomobject]@{
                    input_guard = $inputGuard
                    proof_ledger = Get-OptionalPropertyValue -Object $snapshot -Name 'proof_ledger'
                }
                throw $terminalInputException
            }
            if (-not [string]::IsNullOrWhiteSpace([string]$snapshot.proof_ledger)) {
                try {
                    $proof = ConvertFrom-ExactJson -Json ([string]$snapshot.proof_ledger
                    )
                    if ($proof.status -ceq 'failed') {
                        throw "Rust Browser weather proof reached a terminal failure: $($proof.failure_reason)"
                    }
                } catch {
                    if ($_.Exception.Message -like 'Rust Browser weather proof reached a terminal failure:*') {
                        throw
                    }
                }
            }
        }
        Start-Sleep -Milliseconds ([int]$Config.readiness.stable_layout_poll_interval_ms)
    }
    throw "Timed out waiting for Browser weather checkpoint $CheckpointCount/$QualityTier. Last state: $lastState"
}

function Send-ProofAdvance {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][object]$Config,
        [Parameter(Mandatory)][ValidateRange(1, 2)][int]$Sequence,
        [Parameter(Mandatory)][ValidateSet('initial_clear', 'rain')][string]$FromStage
    )

    $payload = ConvertTo-CompactJson -Value ([ordered]@{
        schema = 'forge.gate1-browser-weather-proof-advance'
        schema_version = 1
        protocol_id = [string]$Config.protocol_id
        sequence = $Sequence
        from_stage = $FromStage
    })
    $base64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($payload))
    $eventName = [string]$Config.controller.advance_event
    $result = Invoke-CdpExpression -Socket $Socket -Expression @"
(() => {
  const binary = atob("$base64");
  const bytes = Uint8Array.from(binary, character => character.charCodeAt(0));
  const detail = new TextDecoder().decode(bytes);
  window.dispatchEvent(new CustomEvent("$eventName", { detail }));
  return detail;
})()
"@
    Assert-Condition -Condition ([string]$result -ceq $payload) -Message 'The Browser did not echo the exact Rust advance command.'
}

function Get-ScreenshotBase64 {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [AllowNull()][object]$Canvas
    )

    $parameters = [ordered]@{
        format = 'png'
        fromSurface = $true
        captureBeyondViewport = $false
    }
    if ($null -ne $Canvas) {
        $parameters.clip = [ordered]@{
            x = [double]$Canvas.left + [double]$Canvas.scroll_x
            y = [double]$Canvas.top + [double]$Canvas.scroll_y
            width = [double]$Canvas.width
            height = [double]$Canvas.height
            scale = 1
        }
    }
    $result = Invoke-CdpCommand -Socket $Socket -Method 'Page.captureScreenshot' -Parameters $parameters -TimeoutSeconds 45
    Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$result.data)) -Message 'CDP returned an empty screenshot.'
    return [string]$result.data
}

function Get-PngDimensions {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    Assert-Condition -Condition ($Bytes.Length -gt 24) -Message 'PNG capture is too small.'
    $signature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
    for ($index = 0; $index -lt $signature.Length; $index += 1) {
        Assert-Condition -Condition ($Bytes[$index] -eq $signature[$index]) -Message 'Capture does not have a PNG signature.'
    }
    $width = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($Bytes, 16))
    $height = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($Bytes, 20))
    Assert-Condition -Condition ($width -gt 0 -and $height -gt 0) -Message 'PNG capture dimensions are invalid.'
    return [pscustomobject]@{ width = $width; height = $height }
}

function Write-Utf8Json {
    param(
        [Parameter(Mandatory)][string]$LiteralPath,
        [Parameter(Mandatory)][object]$Value
    )

    [IO.File]::WriteAllText($LiteralPath, "$(ConvertTo-Json -InputObject $Value -Depth 64)`n", [Text.UTF8Encoding]::new($false))
}

function Write-Utf8Text {
    param(
        [Parameter(Mandatory)][string]$LiteralPath,
        [Parameter(Mandatory)][string]$Value
    )

    [IO.File]::WriteAllText($LiteralPath, "$Value`n", [Text.UTF8Encoding]::new($false))
}

function New-ArtifactDescriptor {
    param(
        [Parameter(Mandatory)][string]$LiteralPath,
        [Parameter(Mandatory)][string]$Root
    )

    $item = Get-Item -LiteralPath $LiteralPath
    return [ordered]@{
        relative_path = [IO.Path]::GetRelativePath($Root, $item.FullName).Replace('\', '/')
        bytes = [int64]$item.Length
        sha256 = Get-LowercaseSha256 -LiteralPath $item.FullName
    }
}

function ConvertTo-BoundedBlockedInputEventEvidence {
    param([AllowNull()][object]$Diagnostic)

    if ($null -eq $Diagnostic) {
        return $null
    }
    $evidence = [ordered]@{}
    foreach ($name in @(
        'type', 'is_trusted', 'event_time_stamp_ms', 'observed_at_performance_ms',
        'document_ready_state', 'document_has_focus', 'visibility_state', 'pointer_type',
        'buttons', 'client_x', 'client_y', 'movement_x', 'movement_y'
    )) {
        $evidence[$name] = Get-OptionalPropertyValue -Object $Diagnostic -Name $name
    }
    return $evidence
}

function ConvertTo-BoundedInputGuardEvidence {
    param(
        [AllowNull()][object]$Snapshot,
        [Parameter(Mandatory)][object]$Config
    )

    $inputGuard = Get-OptionalPropertyValue -Object $Snapshot -Name 'input_guard'
    if ($null -eq $inputGuard) {
        return $null
    }
    $rawCounts = Get-OptionalPropertyValue -Object $inputGuard -Name 'blocked_input_events_by_type'
    $counts = [ordered]@{}
    foreach ($eventType in @($Config.fixed_context.blocked_event_types)) {
        $counts[$eventType] = Get-OptionalPropertyValue -Object $rawCounts -Name $eventType
    }
    return [ordered]@{
        active = Get-OptionalPropertyValue -Object $inputGuard -Name 'active'
        blocked_input_events = Get-OptionalPropertyValue -Object $inputGuard -Name 'blocked_input_events'
        blocked_input_events_by_type = $counts
        first_blocked_input_event = ConvertTo-BoundedBlockedInputEventEvidence -Diagnostic (Get-OptionalPropertyValue -Object $inputGuard -Name 'first_blocked_input_event')
        last_blocked_input_event = ConvertTo-BoundedBlockedInputEventEvidence -Diagnostic (Get-OptionalPropertyValue -Object $inputGuard -Name 'last_blocked_input_event')
    }
}

function New-BrowserTierAbortReceipt {
    param(
        [Parameter(Mandatory)][object]$Config,
        [Parameter(Mandatory)][string]$ExpectedCommit,
        [Parameter(Mandatory)][ValidateSet('low', 'high')][string]$QualityTier,
        [Parameter(Mandatory)][string]$Route,
        [Parameter(Mandatory)][string]$FailureMessage,
        [Parameter(Mandatory)][string]$FailureType,
        [AllowNull()][object]$Snapshot,
        [Parameter(Mandatory)][ValidateSet('captured', 'captured_at_terminal_detection', 'failed', 'not_connected')][string]$SnapshotCaptureStatus,
        [AllowNull()][object]$SnapshotCaptureError,
        [AllowNull()][object]$BrowserProcessId,
        [ValidateSet('complete', 'unavailable')][string]$ArtifactInventoryStatus = 'complete',
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$ArtifactsPresent
    )

    $inputGuardEvidence = ConvertTo-BoundedInputGuardEvidence -Snapshot $Snapshot -Config $Config
    $inputContaminationObserved = $FailureMessage.StartsWith('Terminal Browser input contamination:', [StringComparison]::Ordinal)
    if ($null -ne $inputGuardEvidence -and $null -ne $inputGuardEvidence.blocked_input_events) {
        try {
            $inputContaminationObserved = $inputContaminationObserved -or ([long]$inputGuardEvidence.blocked_input_events -gt 0)
        } catch {
            # The receipt remains fail-closed while retaining the unparsed bounded value.
        }
    }

    $controller = [ordered]@{
        proof_status = $null
        checkpoint_count = $null
        runtime_instance_id = $null
        failure_reason = $null
        proof_parse_error = $null
    }
    $proofLedger = Get-OptionalPropertyValue -Object $Snapshot -Name 'proof_ledger'
    if (-not [string]::IsNullOrWhiteSpace([string]$proofLedger)) {
        try {
            $proof = ConvertFrom-ExactJson -Json ([string]$proofLedger)
            $controller.proof_status = Get-OptionalPropertyValue -Object $proof -Name 'status'
            $checkpoints = Get-OptionalPropertyValue -Object $proof -Name 'checkpoints'
            $controller.checkpoint_count = @($checkpoints).Count
            $controller.runtime_instance_id = Get-OptionalPropertyValue -Object $proof -Name 'runtime_instance_id'
            $controller.failure_reason = Get-OptionalPropertyValue -Object $proof -Name 'failure_reason'
        } catch {
            $controller.proof_parse_error = $_.Exception.Message
        }
    }

    return [ordered]@{
        schema = 'forge.gate1-browser-weather-tier-abort'
        schema_version = 1
        recorded_at_utc = [DateTimeOffset]::UtcNow.ToString('o')
        recorded_before_browser_cleanup = $true
        protocol_id = [string]$Config.protocol_id
        expected_commit = $ExpectedCommit
        quality_tier = $QualityTier
        route = $Route
        failure = [ordered]@{
            category = if ($inputContaminationObserved) { 'input_contamination' } else { 'browser_tier_failure' }
            exception_type = $FailureType
            message = $FailureMessage
        }
        browser = [ordered]@{
            process_id = $BrowserProcessId
            snapshot_capture_status = $SnapshotCaptureStatus
            snapshot_capture_error = $SnapshotCaptureError
        }
        input_guard = $inputGuardEvidence
        controller = $controller
        artifact_inventory_status = $ArtifactInventoryStatus
        artifacts_present_before_abort_receipt = @($ArtifactsPresent)
        automated_result = [ordered]@{
            status = 'abort'
            input_contamination_observed = $inputContaminationObserved
            tier_qualified = $false
            gate1_routes_qualified = $false
        }
    }
}

function Write-BrowserTierAbortReceipt {
    param(
        [Parameter(Mandatory)][string]$TierDirectory,
        [Parameter(Mandatory)][string]$RunDirectory,
        [Parameter(Mandatory)][object]$Config,
        [Parameter(Mandatory)][string]$ExpectedCommit,
        [Parameter(Mandatory)][ValidateSet('low', 'high')][string]$QualityTier,
        [Parameter(Mandatory)][string]$Route,
        [Parameter(Mandatory)][string]$FailureMessage,
        [Parameter(Mandatory)][string]$FailureType,
        [AllowNull()][object]$Snapshot,
        [Parameter(Mandatory)][ValidateSet('captured', 'captured_at_terminal_detection', 'failed', 'not_connected')][string]$SnapshotCaptureStatus,
        [AllowNull()][object]$SnapshotCaptureError,
        [AllowNull()][object]$BrowserProcessId
    )

    $resolvedRunDirectory = [IO.Path]::GetFullPath($RunDirectory)
    $resolvedTierDirectory = [IO.Path]::GetFullPath($TierDirectory)
    $expectedTierDirectory = [IO.Path]::GetFullPath((Join-Path $resolvedRunDirectory $QualityTier))
    Assert-Condition -Condition ($resolvedTierDirectory.Equals($expectedTierDirectory, [StringComparison]::OrdinalIgnoreCase)) -Message 'Browser tier ABORT receipt escaped its exact run/tier directory.'
    Assert-Condition -Condition (Test-Path -LiteralPath $resolvedTierDirectory -PathType Container) -Message 'Browser tier ABORT receipt directory is missing.'

    $receiptPath = Join-Path $resolvedTierDirectory 'abort-receipt.json'
    Assert-Condition -Condition (-not (Test-Path -LiteralPath $receiptPath)) -Message 'Browser tier ABORT receipt already exists; refusing to overwrite it.'
    $artifactsPresent = @(
        Get-ChildItem -LiteralPath $resolvedTierDirectory -File -Recurse |
            Sort-Object FullName |
            ForEach-Object { New-ArtifactDescriptor -LiteralPath $_.FullName -Root $resolvedRunDirectory }
    )
    Assert-Condition -Condition ($artifactsPresent.Count -le 13) -Message 'Browser tier produced an unexpected unbounded artifact set before ABORT.'
    $receipt = New-BrowserTierAbortReceipt -Config $Config -ExpectedCommit $ExpectedCommit -QualityTier $QualityTier -Route $Route -FailureMessage $FailureMessage -FailureType $FailureType -Snapshot $Snapshot -SnapshotCaptureStatus $SnapshotCaptureStatus -SnapshotCaptureError $SnapshotCaptureError -BrowserProcessId $BrowserProcessId -ArtifactsPresent $artifactsPresent
    $receiptJson = "$(ConvertTo-Json -InputObject $receipt -Depth 64)`n"
    $temporaryReceiptPath = "$receiptPath.tmp-$([guid]::NewGuid().ToString('N'))"
    try {
        [IO.File]::WriteAllText($temporaryReceiptPath, $receiptJson, [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporaryReceiptPath, $receiptPath)
        return New-ArtifactDescriptor -LiteralPath $receiptPath -Root $resolvedRunDirectory
    } catch {
        $receiptWriteException = [IO.IOException]::new("Unable to persist the atomic Browser tier ABORT receipt: $($_.Exception.Message)", $_.Exception)
        $receiptWriteException.Data['AbortReceiptJson'] = $receiptJson
        throw $receiptWriteException
    } finally {
        if (Test-Path -LiteralPath $temporaryReceiptPath -PathType Leaf) {
            [IO.File]::Delete($temporaryReceiptPath)
        }
    }
}

function Get-ServedBundleEvidence {
    param(
        [Parameter(Mandatory)][uri]$Root,
        [Parameter(Mandatory)][object]$Config
    )

    $distRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot ([string]$Config.server.dist_root -replace '/', [IO.Path]::DirectorySeparatorChar)))
    $repositoryPrefix = $repositoryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    Assert-Condition -Condition ($distRoot.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) -Message 'The registered dist root escaped the repository.'
    Assert-Condition -Condition (Test-Path -LiteralPath $distRoot -PathType Container) -Message 'The Studio dist directory is missing; perform an exact Trunk build first.'
    $handler = [Net.Http.SocketsHttpHandler]::new()
    $handler.UseProxy = $false
    $client = [Net.Http.HttpClient]::new($handler, $true)
    $client.Timeout = [TimeSpan]::FromMinutes(5)
    $evidence = [Collections.Generic.List[object]]::new()
    try {
        foreach ($relativePath in @($Config.server.bundle_files)) {
            $localPath = [IO.Path]::GetFullPath((Join-Path $distRoot (([string]$relativePath).Replace('/', [IO.Path]::DirectorySeparatorChar))))
            $distPrefix = $distRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
            Assert-Condition -Condition ($localPath.StartsWith($distPrefix, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $localPath -PathType Leaf)) -Message "Local bundle escaped or is missing: $relativePath"
            $localItem = Get-Item -LiteralPath $localPath
            $localHash = Get-LowercaseSha256 -LiteralPath $localPath
            $servedUri = [uri]::new($Root, [string]$relativePath)
            $bytes = $client.GetByteArrayAsync($servedUri).GetAwaiter().GetResult()
            $servedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
            Assert-Condition -Condition ($bytes.Length -eq $localItem.Length -and $servedHash -ceq $localHash) -Message "Served/local bundle mismatch: $relativePath"
            $evidence.Add([ordered]@{
                relative_path = ([string]$relativePath).Replace('\', '/')
                url = $servedUri.AbsoluteUri
                bytes = [int64]$bytes.Length
                sha256 = $servedHash
                exact_match = $true
            })
        }
    } finally {
        $client.Dispose()
    }
    return @($evidence)
}

function Remove-ControlledProfile {
    param([Parameter(Mandatory)][string]$ProfileDirectory)

    if (-not (Test-Path -LiteralPath $ProfileDirectory -PathType Container)) {
        return
    }
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $resolved = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $ProfileDirectory).Path)
    if ($resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolved).StartsWith('forge-gate1-browser-weather-', [StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue
    } else {
        Write-Warning "Refusing to remove unexpected Browser profile path: $resolved"
    }
}

function Invoke-BrowserTier {
    param(
        [Parameter(Mandatory)][ValidateSet('low', 'high')][string]$QualityTier,
        [Parameter(Mandatory)][object]$Config,
        [Parameter(Mandatory)][string]$ExpectedCommit,
        [Parameter(Mandatory)][string]$BrowserPath,
        [Parameter(Mandatory)][string]$RunDirectory
    )

    Reset-CdpEvents
    $tierDirectory = Join-Path $RunDirectory $QualityTier
    [void](New-Item -ItemType Directory -Path $tierDirectory)
    $profileDirectory = Join-Path ([IO.Path]::GetTempPath()) "forge-gate1-browser-weather-$QualityTier-$([guid]::NewGuid().ToString('N'))"
    [void](New-Item -ItemType Directory -Path $profileDirectory)
    $route = Get-ExactRoute -Root $BaseUrl -Config $Config -QualityTier $QualityTier
    $browserProcess = $null
    $socket = $null
    try {
        $port = New-LoopbackPort
        $browserProcess = Start-ControlledBrowser -Executable $BrowserPath -Port $port -ProfileDirectory $profileDirectory -Config $Config
        $target = Wait-CdpTarget -Port $port -Process $browserProcess
        $socket = Connect-CdpSocket -WebSocketUrl ([string]$target.webSocketDebuggerUrl)
        foreach ($domain in @('Runtime.enable', 'Page.enable', 'Network.enable', 'Log.enable')) {
            [void](Invoke-CdpCommand -Socket $socket -Method $domain)
        }
        [void](Invoke-CdpCommand -Socket $socket -Method 'Emulation.setDeviceMetricsOverride' -Parameters ([ordered]@{
            width = [int]$Config.browser.viewport_width_px
            height = [int]$Config.browser.viewport_height_px
            deviceScaleFactor = [double]$Config.browser.device_scale_factor
            mobile = $false
        }))
        $blocked = ConvertTo-CompactJson -Value @($Config.fixed_context.blocked_event_types)
        $inputGuard = @"
(() => {
  const blocked = $blocked;
  const countsByType = Object.seal(Object.fromEntries(blocked.map(type => [type, 0])));
  const finiteOrNull = value => Number.isFinite(value) ? value : null;
  const state = {
    active: true,
    blocked_event_types: Object.freeze([...blocked]),
    blocked_input_events: 0,
    blocked_input_events_by_type: countsByType,
    first_blocked_input_event: null,
    last_blocked_input_event: null
  };
  Object.seal(state);
  const stop = event => {
    const diagnostic = Object.freeze({
      type: String(event.type),
      is_trusted: event.isTrusted === true,
      event_time_stamp_ms: finiteOrNull(Number(event.timeStamp)),
      observed_at_performance_ms: finiteOrNull(performance.now()),
      document_ready_state: document.readyState,
      document_has_focus: document.hasFocus(),
      visibility_state: document.visibilityState,
      pointer_type: typeof event.pointerType === "string" && event.pointerType.length > 0 ? event.pointerType : null,
      buttons: typeof event.buttons === "number" && Number.isInteger(event.buttons) ? event.buttons : null,
      client_x: finiteOrNull(Number(event.clientX)),
      client_y: finiteOrNull(Number(event.clientY)),
      movement_x: finiteOrNull(Number(event.movementX)),
      movement_y: finiteOrNull(Number(event.movementY))
    });
    state.blocked_input_events += 1;
    countsByType[event.type] += 1;
    if (state.first_blocked_input_event === null) state.first_blocked_input_event = diagnostic;
    state.last_blocked_input_event = diagnostic;
    event.preventDefault();
    event.stopImmediatePropagation();
  };
  for (const type of blocked) window.addEventListener(type, stop, { capture: true, passive: false });
  Object.defineProperty(window, "__forgeGate1BrowserWeatherMatrixInputGuard", {
    configurable: false,
    enumerable: false,
    writable: false,
    value: state
  });
})();
"@
        [void](Invoke-CdpCommand -Socket $socket -Method 'Page.addScriptToEvaluateOnNewDocument' -Parameters ([ordered]@{ source = $inputGuard }))
        [void](Invoke-CdpCommand -Socket $socket -Method 'Page.navigate' -Parameters ([ordered]@{ url = $route }))

        $artifacts = [Collections.Generic.List[object]]::new()
        $capturedStages = [Collections.Generic.List[object]]::new()
        $latest = $null
        for ($checkpointCount = 1; $checkpointCount -le 3; $checkpointCount += 1) {
            $latest = Wait-ProofCheckpoint -Socket $socket -Config $Config -ExpectedCommit $ExpectedCommit -QualityTier $QualityTier -CheckpointCount $checkpointCount
            $registered = @($Config.sequence)[$checkpointCount - 1]
            $stage = [string]$registered.stage
            $pageBytes = [Convert]::FromBase64String((Get-ScreenshotBase64 -Socket $socket -Canvas $null))
            $canvasBytes = [Convert]::FromBase64String((Get-ScreenshotBase64 -Socket $socket -Canvas $latest.Snapshot.canvas))
            $pageDimensions = Get-PngDimensions -Bytes $pageBytes
            $canvasDimensions = Get-PngDimensions -Bytes $canvasBytes
            Assert-Condition -Condition ($pageDimensions.width -eq [int]$Config.browser.viewport_width_px -and $pageDimensions.height -eq [int]$Config.browser.viewport_height_px) -Message "Page PNG dimensions drifted for $QualityTier/$stage."
            Assert-Condition -Condition ($canvasDimensions.width -eq [int]$latest.Snapshot.canvas.backing_width -and $canvasDimensions.height -eq [int]$latest.Snapshot.canvas.backing_height) -Message "Canvas PNG dimensions drifted for $QualityTier/$stage."
            $pagePath = Join-Path $tierDirectory "$checkpointCount-$stage-page.png"
            $canvasPath = Join-Path $tierDirectory "$checkpointCount-$stage-canvas.png"
            $ledgerPath = Join-Path $tierDirectory "$checkpointCount-$stage-proof-ledger.json"
            [IO.File]::WriteAllBytes($pagePath, $pageBytes)
            [IO.File]::WriteAllBytes($canvasPath, $canvasBytes)
            Write-Utf8Text -LiteralPath $ledgerPath -Value ([string]$latest.Snapshot.proof_ledger)
            foreach ($path in @($pagePath, $canvasPath, $ledgerPath)) {
                $artifacts.Add((New-ArtifactDescriptor -LiteralPath $path -Root $RunDirectory))
            }
            $capturedStages.Add([ordered]@{
                sequence = $checkpointCount
                stage = $stage
                recorded_at_utc = [DateTimeOffset]::UtcNow.ToString('o')
                page = New-ArtifactDescriptor -LiteralPath $pagePath -Root $RunDirectory
                canvas = New-ArtifactDescriptor -LiteralPath $canvasPath -Root $RunDirectory
                controller_ledger = New-ArtifactDescriptor -LiteralPath $ledgerPath -Root $RunDirectory
            })
            if ($checkpointCount -eq 1) {
                Send-ProofAdvance -Socket $socket -Config $Config -Sequence 1 -FromStage initial_clear
            } elseif ($checkpointCount -eq 2) {
                Send-ProofAdvance -Socket $socket -Config $Config -Sequence 2 -FromStage rain
            }
        }

        $finalSnapshot = Get-BrowserSnapshot -Socket $socket -Config $Config
        $finalLedgers = Assert-BaseSnapshot -Snapshot $finalSnapshot -Config $Config -ExpectedCommit $ExpectedCommit -QualityTier $QualityTier
        Assert-ProofLedger -Ledger $finalLedgers.Proof -Config $Config -ExpectedCommit $ExpectedCommit -QualityTier $QualityTier -ExpectedCheckpointCount 3
        $latest = [pscustomobject]@{
            Snapshot = $finalSnapshot
            Ledgers = $finalLedgers
        }
        $browserVersion = Invoke-CdpCommand -Socket $socket -Method 'Browser.getVersion'
        $responses = @($script:NetworkResponses.Values | Where-Object { -not ([string]$_.url).StartsWith('data:', [StringComparison]::OrdinalIgnoreCase) } | Sort-Object url, request_id)
        $httpFailures = @($responses | Where-Object { [int]$_.status -ge 400 })
        $loadingFailures = @($script:NetworkFailures)
        $networkFailureCount = $httpFailures.Count + $loadingFailures.Count
        $consoleEntries = @($script:ConsoleEvents | Sort-Object timestamp, source)
        $consoleErrors = @($consoleEntries | Where-Object { $_.level -cin @('error', 'assert') })
        $consoleWarnings = @($consoleEntries | Where-Object { $_.level -cin @('warn', 'warning') })
        Assert-Condition -Condition ($consoleErrors.Count -le [int]$Config.acceptance.maximum_console_errors) -Message "$QualityTier Browser console errors: $($consoleErrors.Count)"
        Assert-Condition -Condition ($consoleWarnings.Count -le [int]$Config.acceptance.maximum_console_warnings) -Message "$QualityTier Browser console warnings: $($consoleWarnings.Count)"
        Assert-Condition -Condition ($networkFailureCount -le [int]$Config.acceptance.maximum_network_failures) -Message "$QualityTier Browser network failures: $networkFailureCount"

        $consolePath = Join-Path $tierDirectory 'console.json'
        $networkPath = Join-Path $tierDirectory 'network.json'
        $gatePath = Join-Path $tierDirectory 'final-gate-ledger.json'
        $scenePath = Join-Path $tierDirectory 'final-scene-ledger.json'
        Write-Utf8Json -LiteralPath $consolePath -Value ([ordered]@{
            schema = 'forge.gate1-browser-weather-console'
            schema_version = 1
            entries = $consoleEntries
            error_count = $consoleErrors.Count
            warning_count = $consoleWarnings.Count
        })
        Write-Utf8Json -LiteralPath $networkPath -Value ([ordered]@{
            schema = 'forge.gate1-browser-weather-network'
            schema_version = 1
            responses = $responses
            loading_failures = $loadingFailures
            http_failures = $httpFailures
            failure_count = $networkFailureCount
        })
        Write-Utf8Text -LiteralPath $gatePath -Value ([string]$latest.Snapshot.gate_ledger)
        Write-Utf8Text -LiteralPath $scenePath -Value ([string]$latest.Snapshot.scene_ledger)
        foreach ($path in @($consolePath, $networkPath, $gatePath, $scenePath)) {
            $artifacts.Add((New-ArtifactDescriptor -LiteralPath $path -Root $RunDirectory))
        }

        return [pscustomobject]@{
            tier = $QualityTier
            route = $route
            runtime_instance_id = [string]$latest.Ledgers.Proof.runtime_instance_id
            build_source_commit = [string]$latest.Ledgers.Proof.build_source_commit
            source_hashes = $latest.Ledgers.Scene.selected_manifest.source_hashes
            browser = [ordered]@{
                executable = $BrowserPath
                product = [string]$browserVersion.product
                revision = [string]$browserVersion.revision
                protocol_version = [string]$browserVersion.protocolVersion
                user_agent = [string]$browserVersion.userAgent
                headed_requested = $true
                headed_observed = $false
                headed_observation = 'not_attested_by_cdp'
            }
            captures = @($capturedStages)
            console = [ordered]@{ entries = $consoleEntries.Count; errors = $consoleErrors.Count; warnings = $consoleWarnings.Count }
            network = [ordered]@{ responses = $responses.Count; failures = $networkFailureCount }
            controller_ledger = $latest.Ledgers.Proof
            artifacts = @($artifacts)
        }
    } catch {
        $failureRecord = $_
        $failureMessage = [string]$failureRecord.Exception.Message
        $failureType = [string]$failureRecord.Exception.GetType().FullName
        $abortSnapshot = $null
        $snapshotCaptureStatus = 'not_connected'
        $snapshotCaptureError = $null
        if ($failureRecord.Exception.Data.Contains('BrowserWeatherAbortSnapshot')) {
            $abortSnapshot = $failureRecord.Exception.Data['BrowserWeatherAbortSnapshot']
            $snapshotCaptureStatus = 'captured_at_terminal_detection'
        } elseif ($null -ne $socket -and $socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
            try {
                $abortSnapshot = Get-BrowserSnapshot -Socket $socket -Config $Config
                $snapshotCaptureStatus = 'captured'
            } catch {
                $snapshotCaptureStatus = 'failed'
                $snapshotCaptureError = $_.Exception.Message
            }
        }
        $browserProcessId = if ($null -ne $browserProcess) { [int]$browserProcess.Id } else { $null }
        $abortReceipt = $null
        $abortReceiptFailure = $null
        $abortReceiptFallbackJson = $null
        try {
            $abortReceipt = Write-BrowserTierAbortReceipt -TierDirectory $tierDirectory -RunDirectory $RunDirectory -Config $Config -ExpectedCommit $ExpectedCommit -QualityTier $QualityTier -Route $route -FailureMessage $failureMessage -FailureType $failureType -Snapshot $abortSnapshot -SnapshotCaptureStatus $snapshotCaptureStatus -SnapshotCaptureError $snapshotCaptureError -BrowserProcessId $browserProcessId
        } catch {
            $abortReceiptFailure = $_.Exception.Message
            if ($_.Exception.Data.Contains('AbortReceiptJson')) {
                $abortReceiptFallbackJson = [string]$_.Exception.Data['AbortReceiptJson']
            }
        }
        if ($null -ne $abortReceipt) {
            throw "$failureMessage`nABORT receipt written before Browser cleanup: $($abortReceipt.relative_path) ($($abortReceipt.sha256))"
        }
        if ([string]::IsNullOrWhiteSpace($abortReceiptFallbackJson)) {
            $fallbackReceipt = New-BrowserTierAbortReceipt -Config $Config -ExpectedCommit $ExpectedCommit -QualityTier $QualityTier -Route $route -FailureMessage $failureMessage -FailureType $failureType -Snapshot $abortSnapshot -SnapshotCaptureStatus $snapshotCaptureStatus -SnapshotCaptureError $snapshotCaptureError -BrowserProcessId $browserProcessId -ArtifactInventoryStatus unavailable -ArtifactsPresent @()
            $abortReceiptFallbackJson = ConvertTo-CompactJson -Value $fallbackReceipt
        }
        throw "$failureMessage`nABORT receipt file write failed before Browser cleanup: $abortReceiptFailure`nBounded ABORT receipt fallback JSON: $abortReceiptFallbackJson"
    } finally {
        if ($null -ne $socket) {
            if ($socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
                try {
                    [void](Invoke-CdpCommand -Socket $socket -Method 'Browser.close' -TimeoutSeconds 5)
                } catch {
                    Write-Verbose "Browser.close did not return cleanly: $($_.Exception.Message)"
                }
            }
            $socket.Dispose()
        }
        if ($null -ne $browserProcess) {
            try {
                if (-not $browserProcess.WaitForExit(5000)) {
                    Stop-Process -Id $browserProcess.Id -Force -ErrorAction SilentlyContinue
                }
            } finally {
                $browserProcess.Dispose()
            }
        }
        Remove-ControlledProfile -ProfileDirectory $profileDirectory
    }
}

if ($ValidateOnly -and $SelfTest) {
    throw '-ValidateOnly and -SelfTest are mutually exclusive.'
}

$resolvedConfigPath = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $ConfigPath).Path)
$canonicalConfigPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'gate1-browser-weather-matrix.v1.json'))
Assert-Condition -Condition ($resolvedConfigPath.Equals($canonicalConfigPath, [StringComparison]::OrdinalIgnoreCase)) -Message 'Only the repository canonical Browser weather matrix v1 configuration is accepted.'
$config = ConvertFrom-ExactJson -Json (Get-Content -LiteralPath $resolvedConfigPath -Raw)
Assert-WeatherMatrixConfig -Config $config

if ($ValidateOnly) {
    [pscustomobject]@{
        protocol = $config.protocol_id
        validation = 'pass'
        tiers = @($config.route.quality_tiers)
        checkpoints_per_tier = @($config.sequence).Count
        settled_frames = [int]$config.controller.minimum_settled_frames
        browser_run = 'not_performed_by_validation'
    }
    return
}

if ($SelfTest) {
    Invoke-SelfTest -Config $config
    return
}

$gitIdentity = Get-GitIdentity
[void](Get-ExactRoute -Root $BaseUrl -Config $config -QualityTier low)
$resolvedBrowser = Find-BrowserExecutable -RequestedPath $BrowserExecutable
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path (Split-Path -Parent $repositoryRoot) '.gate1-evidence-browser-weather-matrix'
}
$resolvedOutputRoot = Assert-ExternalOutputRoot -Candidate $OutputRoot
[void](New-Item -ItemType Directory -Force -Path $resolvedOutputRoot)

$servedBundles = Get-ServedBundleEvidence -Root $BaseUrl -Config $config
$stamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$runDirectory = Join-Path $resolvedOutputRoot "$($gitIdentity.commit.Substring(0, 12))-$stamp"
[void](New-Item -ItemType Directory -Path $runDirectory)
$bundlePath = Join-Path $runDirectory 'served-bundles.json'
Write-Utf8Json -LiteralPath $bundlePath -Value ([ordered]@{
    schema = 'forge.gate1-browser-weather-served-bundles'
    schema_version = 1
    exact_matches = @($servedBundles).Count
    files = @($servedBundles)
})

$runs = [Collections.Generic.List[object]]::new()
foreach ($tier in @($config.route.quality_tiers)) {
    $runs.Add((Invoke-BrowserTier -QualityTier ([string]$tier) -Config $config -ExpectedCommit ([string]$gitIdentity.commit) -BrowserPath $resolvedBrowser -RunDirectory $runDirectory))
}
Assert-Condition -Condition ($runs.Count -eq 2) -Message 'The Browser weather matrix did not produce exactly Low and High runs.'
Assert-Condition -Condition ($runs[0].runtime_instance_id -cne $runs[1].runtime_instance_id) -Message 'Low and High unexpectedly reused one Browser/Bevy runtime instance.'
Assert-Condition -Condition ((ConvertTo-CompactJson -Value $runs[0].source_hashes) -ceq (ConvertTo-CompactJson -Value $runs[1].source_hashes)) -Message 'Low and High source manifests diverged.'
Assert-Condition -Condition ((ConvertTo-CompactJson -Value $runs[0].controller_ledger.checkpoints[0].camera_pose) -ceq (ConvertTo-CompactJson -Value $runs[1].controller_ledger.checkpoints[0].camera_pose)) -Message 'Low and High did not use the same registered water camera pose.'

$gitIdentityAfter = Get-GitIdentity
Assert-Condition -Condition ($gitIdentityAfter.commit -ceq $gitIdentity.commit -and $gitIdentityAfter.tree -ceq $gitIdentity.tree) -Message 'Git identity changed during Browser weather acquisition.'
$gitIdentity['clean_after_acquisition'] = $true

$protocolPaths = @(
    'scripts/gate1-browser-weather-matrix.v1.json',
    'scripts/Invoke-Gate1BrowserWeatherMatrix.ps1',
    'scripts/Test-Gate1BrowserWeatherMatrix.ps1',
    'scripts/Invoke-StudioTrunk.ps1',
    'apps/forge_studio/src/lib.rs',
    'apps/forge_studio/src/gate1_browser_weather_matrix.rs',
    'apps/forge_studio/src/gate1_browser_weather_matrix_web.rs',
    'docs/evidence/gate1/R1_023_026_BROWSER_WEATHER_MATRIX_PROTOCOL.md'
)
$protocolFiles = @($protocolPaths | ForEach-Object {
    $path = Join-Path $repositoryRoot ($_ -replace '/', [IO.Path]::DirectorySeparatorChar)
    Assert-Condition -Condition (Test-Path -LiteralPath $path -PathType Leaf) -Message "Protocol file is missing: $_"
    [ordered]@{ path = $_; sha256 = Get-LowercaseSha256 -LiteralPath $path }
})
$allArtifacts = [Collections.Generic.List[object]]::new()
$allArtifacts.Add((New-ArtifactDescriptor -LiteralPath $bundlePath -Root $runDirectory))
foreach ($run in $runs) {
    foreach ($artifact in @($run.artifacts)) {
        $allArtifacts.Add($artifact)
    }
}

$ledger = [ordered]@{
    schema = 'forge.gate1-browser-weather-matrix-ledger'
    schema_version = 1
    protocol_id = $config.protocol_id
    recorded_at_utc = [DateTimeOffset]::UtcNow.ToString('o')
    git = $gitIdentity
    browser_matrix = @($runs)
    served_bundles = [ordered]@{
        exact_matches = @($servedBundles).Count
        required_matches = @($config.server.bundle_files).Count
        files = @($servedBundles)
        artifact = New-ArtifactDescriptor -LiteralPath $bundlePath -Root $runDirectory
    }
    protocol_files = $protocolFiles
    automated_result = [ordered]@{
        status = 'pass_pending_human_and_precise_gpu_evidence'
        browser_pixels_acquired = $true
        low_clear_rain_clear_same_bevy_instance = $true
        high_clear_rain_clear_same_bevy_instance = $true
        checkpoints = 6
        page_captures = 6
        canvas_captures = 6
        weather_shader_consumption_proven = $false
        water_draw_attribution_proven = $false
        exact_render_frame_identity_proven = $false
        gpu_presentation_field = 'not_observed'
        human_review_performed = $false
        r1_023 = 'open'
        r1_025 = 'open'
        r1_026 = 'open'
    }
    limitations = @(
        'The Browser captures are presented pixels acquired through CDP, but the Studio gpu_presentation field remains not_observed.',
        'CPU/ECS wetness uniforms do not prove shader consumption.',
        'Water optics observations do not attribute an exact draw, material invocation, or render frame.',
        'Animated water, rain and vegetation prevent pixel equality between initial and restored clear captures.',
        'Human inspection and precise GPU evidence remain required; this harness never closes R1-023, R1-025 or R1-026.'
    )
}
$ledgerPath = Join-Path $runDirectory 'r1-023-025-026-browser-weather-matrix-ledger.json'
Write-Utf8Json -LiteralPath $ledgerPath -Value $ledger
$allArtifacts.Add((New-ArtifactDescriptor -LiteralPath $ledgerPath -Root $runDirectory))
$manifest = [ordered]@{
    schema = 'forge.gate1-browser-weather-matrix-artifact-manifest'
    schema_version = 1
    git_commit = $gitIdentity.commit
    artifacts = @($allArtifacts)
}
$manifestPath = Join-Path $runDirectory 'artifact-manifest.json'
Write-Utf8Json -LiteralPath $manifestPath -Value $manifest

[pscustomobject]@{
    status = 'pass_pending_human_and_precise_gpu_evidence'
    run_directory = $runDirectory
    ledger = $ledgerPath
    ledger_sha256 = Get-LowercaseSha256 -LiteralPath $ledgerPath
    artifact_manifest = $manifestPath
    tiers = @($config.route.quality_tiers)
    checkpoints = 6
    captures = 12
    r1_023 = 'open'
    r1_025 = 'open'
    r1_026 = 'open'
}
