[CmdletBinding()]
param(
    [uri]$BaseUrl = 'http://127.0.0.1:1420/',

    [string]$BrowserExecutable,

    [string]$OutputRoot,

    [string]$ConfigPath = (Join-Path $PSScriptRoot 'gate1-browser-temporal.v1.json'),

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

function Test-LoopbackHost {
    param([Parameter(Mandatory)][string]$HostName)

    try {
        $addresses = @([Net.Dns]::GetHostAddresses($HostName))
        return ($addresses.Count -gt 0 -and @($addresses | Where-Object { -not [Net.IPAddress]::IsLoopback($_) }).Count -eq 0)
    } catch {
        return $false
    }
}

function Assert-FiniteRange {
    param(
        [Parameter(Mandatory)][object]$Value,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][double]$Minimum,
        [Parameter(Mandatory)][double]$Maximum,
        [switch]$MinimumExclusive
    )

    $number = [double]$Value
    $minimumAccepted = if ($MinimumExclusive) { $number -gt $Minimum } else { $number -ge $Minimum }
    if (-not [double]::IsFinite($number) -or -not $minimumAccepted -or $number -gt $Maximum) {
        $operator = if ($MinimumExclusive) { '>' } else { '>=' }
        throw "$Name must be finite and $operator $Minimum and <= $Maximum."
    }
}

function Get-LowercaseSha256 {
    param([Parameter(Mandatory)][string]$LiteralPath)

    return (Get-FileHash -LiteralPath $LiteralPath -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Test-RectangleIntersection {
    param(
        [Parameter(Mandatory)][object]$Left,
        [Parameter(Mandatory)][object]$Right
    )

    return (
        [double]$Left.x -lt ([double]$Right.x + [double]$Right.width) -and
        ([double]$Left.x + [double]$Left.width) -gt [double]$Right.x -and
        [double]$Left.y -lt ([double]$Right.y + [double]$Right.height) -and
        ([double]$Left.y + [double]$Left.height) -gt [double]$Right.y
    )
}

function Assert-NormalizedRectangle {
    param(
        [Parameter(Mandatory)][object]$Rectangle,
        [Parameter(Mandatory)][string]$Description
    )

    Assert-FiniteRange -Value $Rectangle.x -Name "$Description.x" -Minimum 0 -Maximum 1
    Assert-FiniteRange -Value $Rectangle.y -Name "$Description.y" -Minimum 0 -Maximum 1
    Assert-FiniteRange -Value $Rectangle.width -Name "$Description.width" -Minimum 0 -Maximum 1 -MinimumExclusive
    Assert-FiniteRange -Value $Rectangle.height -Name "$Description.height" -Minimum 0 -Maximum 1 -MinimumExclusive
    Assert-Condition -Condition (([double]$Rectangle.x + [double]$Rectangle.width) -le 1.0) -Message "$Description exceeds the normalized canvas width."
    Assert-Condition -Condition (([double]$Rectangle.y + [double]$Rectangle.height) -le 1.0) -Message "$Description exceeds the normalized canvas height."
}

function Assert-TemporalConfig {
    param([Parameter(Mandatory)][object]$Config)

    Assert-Condition -Condition ($Config.schema -ceq 'forge.gate1-browser-temporal-config') -Message 'Unexpected temporal configuration schema.'
    Assert-Condition -Condition ([int]$Config.schema_version -eq 1) -Message 'Unexpected temporal configuration schema version.'
    Assert-Condition -Condition ($Config.protocol_id -ceq 'r1-014-browser-webgpu-clear-high-temporal-v1') -Message 'Unexpected temporal protocol id.'
    Assert-Condition -Condition ($Config.introduced_from_commit -ceq '28985328f0d2de1fa79c705d5c3aa1bc522dbe35') -Message 'The protocol introduction commit changed without a protocol version bump.'

    Assert-Condition -Condition ($Config.route.path -ceq '/') -Message 'R1-014 Browser route must remain at the Studio root.'
    Assert-Condition -Condition ($Config.route.query.forge_scene -ceq 'visual-valley') -Message 'R1-014 Browser scene must be visual-valley.'
    Assert-Condition -Condition ($Config.route.query.forge_weather -ceq 'clear') -Message 'R1-014 Browser weather must be clear.'
    Assert-Condition -Condition ($Config.route.query.forge_quality -ceq 'high') -Message 'R1-014 Browser quality must be high.'
    Assert-Condition -Condition ($Config.server.origin -ceq 'http://127.0.0.1:1420/') -Message 'The registered Trunk origin changed.'
    Assert-Condition -Condition ($Config.server.dist_root -ceq 'apps/forge_studio/dist') -Message 'The registered Studio dist root changed.'
    $bundleFiles = @($Config.server.bundle_files)
    $expectedBundleFiles = @('index.html', 'forge_studio.js', 'forge_studio_bg.wasm', 'styles.css')
    Assert-Condition -Condition ((ConvertTo-CompactJson -Value $bundleFiles) -ceq (ConvertTo-CompactJson -Value $expectedBundleFiles)) -Message 'The registered served-bundle set changed without a protocol version bump.'
    Assert-Condition -Condition ($Config.browser.canvas_selector -ceq '#forge-canvas') -Message 'The canonical Studio canvas selector changed.'
    Assert-Condition -Condition ([int]$Config.browser.viewport_width_px -eq 1920) -Message 'The registered viewport width must be 1920.'
    Assert-Condition -Condition ([int]$Config.browser.viewport_height_px -eq 1080) -Message 'The registered viewport height must be 1080.'
    Assert-Condition -Condition ([double]$Config.browser.device_scale_factor -eq 1.0) -Message 'The registered device scale factor must be 1.'
    Assert-Condition -Condition ($Config.browser.headed_capture_required -eq $true) -Message 'A headed Browser capture is required for qualification.'
    Assert-Condition -Condition ([int]$Config.readiness.minimum_request_animation_frames -ge 120) -Message 'Temporal separation must be at least 120 requestAnimationFrame callbacks.'
    Assert-Condition -Condition ([int]$Config.readiness.timeout_seconds -ge 30) -Message 'The readiness timeout must be at least 30 seconds.'
    Assert-Condition -Condition ($Config.readiness.require_navigator_gpu -eq $true) -Message 'navigator.gpu must remain a readiness requirement.'
    Assert-Condition -Condition ($Config.readiness.require_single_viewport_start -eq $true) -Message 'Exactly one viewport start must remain required.'
    Assert-Condition -Condition ($Config.readiness.require_completed_frame_window -eq $true) -Message 'The completed Studio frame window must remain required.'

    Assert-Condition -Condition ($Config.fixed_context.scene -ceq 'visual_valley') -Message 'The fixed scene contract changed.'
    Assert-Condition -Condition ($Config.fixed_context.weather -ceq 'clear') -Message 'The fixed weather contract changed.'
    Assert-Condition -Condition ($Config.fixed_context.quality_mode -ceq 'fixed') -Message 'The fixed quality mode changed.'
    Assert-Condition -Condition ($Config.fixed_context.quality_tier -ceq 'high') -Message 'The fixed quality tier changed.'
    Assert-Condition -Condition ($Config.fixed_context.camera_owner -ceq 'visual_valley_player_controller') -Message 'The fixed camera owner changed.'
    Assert-Condition -Condition ([int]$Config.fixed_context.active_camera_count -eq 1) -Message 'Exactly one active camera must remain required.'
    Assert-Condition -Condition ($Config.fixed_context.block_browser_input -eq $true) -Message 'Browser input must remain blocked during temporal acquisition.'
    $blockedEvents = @($Config.fixed_context.blocked_event_types)
    foreach ($requiredEvent in @('keydown', 'keyup', 'mousemove', 'wheel', 'pointerdown', 'pointermove')) {
        Assert-Condition -Condition ($blockedEvents -ccontains $requiredEvent) -Message "Input guard is missing '$requiredEvent'."
    }

    Assert-FiniteRange -Value $Config.pixel_difference.changed_channel_delta -Name 'changed_channel_delta' -Minimum 1 -Maximum 255
    Assert-FiniteRange -Value $Config.pixel_difference.vegetation_min_changed_pixel_ratio -Name 'vegetation_min_changed_pixel_ratio' -Minimum 0 -Maximum 1 -MinimumExclusive
    Assert-FiniteRange -Value $Config.pixel_difference.vegetation_min_mean_absolute_rgb_delta -Name 'vegetation_min_mean_absolute_rgb_delta' -Minimum 0 -Maximum 255 -MinimumExclusive
    Assert-FiniteRange -Value $Config.pixel_difference.terrain_max_changed_pixel_ratio -Name 'terrain_max_changed_pixel_ratio' -Minimum 0 -Maximum 1
    Assert-FiniteRange -Value $Config.pixel_difference.terrain_max_mean_absolute_rgb_delta -Name 'terrain_max_mean_absolute_rgb_delta' -Minimum 0 -Maximum 255
    Assert-Condition -Condition ([int]$Config.pixel_difference.changed_channel_delta -eq 6) -Message 'changed_channel_delta changed without a protocol version bump.'
    Assert-Condition -Condition ([double]$Config.pixel_difference.vegetation_min_changed_pixel_ratio -eq 0.002) -Message 'Vegetation changed-pixel threshold changed without a protocol version bump.'
    Assert-Condition -Condition ([double]$Config.pixel_difference.vegetation_min_mean_absolute_rgb_delta -eq 0.1) -Message 'Vegetation mean-delta threshold changed without a protocol version bump.'
    Assert-Condition -Condition ([double]$Config.pixel_difference.terrain_max_changed_pixel_ratio -eq 0.01) -Message 'Terrain changed-pixel threshold changed without a protocol version bump.'
    Assert-Condition -Condition ([double]$Config.pixel_difference.terrain_max_mean_absolute_rgb_delta -eq 0.5) -Message 'Terrain mean-delta threshold changed without a protocol version bump.'

    $excludedRegions = @($Config.excluded_regions)
    $excludedIds = @($excludedRegions | ForEach-Object { [string]$_.id })
    foreach ($requiredId in @('studio_overlay', 'sky', 'water_guard')) {
        Assert-Condition -Condition ($excludedIds -ccontains $requiredId) -Message "Missing excluded region '$requiredId'."
    }
    Assert-Condition -Condition (($excludedIds | Sort-Object -Unique).Count -eq $excludedIds.Count) -Message 'Excluded region ids must be unique.'
    foreach ($excluded in $excludedRegions) {
        Assert-NormalizedRectangle -Rectangle $excluded -Description "excluded region '$($excluded.id)'"
        Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$excluded.basis)) -Message "Excluded region '$($excluded.id)' requires a registration basis."
    }
    $expectedExcludedRectangles = @{
        studio_overlay = @(0.0, 0.0, 0.57, 0.31)
        sky = @(0.57, 0.0, 0.43, 0.28)
        water_guard = @(0.34, 0.31, 0.30, 0.15)
    }
    foreach ($excluded in $excludedRegions) {
        $expected = $expectedExcludedRectangles[[string]$excluded.id]
        Assert-Condition -Condition ($null -ne $expected) -Message "Unexpected excluded region '$($excluded.id)'."
        $actual = @([double]$excluded.x, [double]$excluded.y, [double]$excluded.width, [double]$excluded.height)
        Assert-Condition -Condition ((ConvertTo-CompactJson -Value $actual) -ceq (ConvertTo-CompactJson -Value $expected)) -Message "Excluded region '$($excluded.id)' changed without a protocol version bump."
    }

    $regions = @($Config.regions)
    $regionIds = @($regions | ForEach-Object { [string]$_.id })
    Assert-Condition -Condition (($regionIds | Sort-Object -Unique).Count -eq $regionIds.Count) -Message 'ROI ids must be unique.'
    $vegetationRegions = @($regions | Where-Object { $_.kind -ceq 'vegetation' })
    $terrainRegions = @($regions | Where-Object { $_.kind -ceq 'terrain_witness' })
    Assert-Condition -Condition ($vegetationRegions.Count -eq 3) -Message 'Exactly three vegetation ROIs must remain registered.'
    Assert-Condition -Condition ($terrainRegions.Count -eq 1) -Message 'Exactly one terrain witness ROI must remain registered.'
    foreach ($region in $regions) {
        Assert-Condition -Condition ($region.kind -cin @('vegetation', 'terrain_witness')) -Message "Unsupported ROI kind '$($region.kind)'."
        Assert-NormalizedRectangle -Rectangle $region -Description "ROI '$($region.id)'"
        Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$region.basis)) -Message "ROI '$($region.id)' requires a registration basis."
        foreach ($excluded in $excludedRegions) {
            Assert-Condition -Condition (-not (Test-RectangleIntersection -Left $region -Right $excluded)) -Message "ROI '$($region.id)' intersects excluded region '$($excluded.id)'."
        }
    }
    $expectedRegions = @{
        vegetation_left_foreground = @('vegetation', 0.02, 0.47, 0.17, 0.20)
        vegetation_right_midfield = @('vegetation', 0.69, 0.32, 0.27, 0.22)
        vegetation_right_foreground = @('vegetation', 0.66, 0.69, 0.28, 0.20)
        terrain_witness = @('terrain_witness', 0.56, 0.55, 0.08, 0.06)
    }
    foreach ($region in $regions) {
        $expected = $expectedRegions[[string]$region.id]
        Assert-Condition -Condition ($null -ne $expected) -Message "Unexpected ROI '$($region.id)'."
        $actual = @([string]$region.kind, [double]$region.x, [double]$region.y, [double]$region.width, [double]$region.height)
        Assert-Condition -Condition ((ConvertTo-CompactJson -Value $actual) -ceq (ConvertTo-CompactJson -Value $expected)) -Message "ROI '$($region.id)' changed without a protocol version bump."
    }

    Assert-Condition -Condition ([int]$Config.acceptance.minimum_passing_vegetation_regions -eq 2) -Message 'Two of three vegetation ROIs must pass.'
    Assert-Condition -Condition ($Config.acceptance.require_terrain_witness -eq $true) -Message 'The terrain witness must remain mandatory.'
    Assert-Condition -Condition ([int]$Config.acceptance.maximum_console_errors -eq 0) -Message 'Console errors must remain forbidden.'
    Assert-Condition -Condition ([int]$Config.acceptance.maximum_console_warnings -eq 0) -Message 'Console warnings must remain forbidden.'
    Assert-Condition -Condition ([int]$Config.acceptance.maximum_network_failures -eq 0) -Message 'Network failures must remain forbidden.'
    Assert-Condition -Condition ($Config.acceptance.human_temporal_inspection_required -eq $true) -Message 'Human temporal inspection must remain explicitly required.'

    $historicalPath = Join-Path $repositoryRoot ([string]$Config.registration_basis.historical_capture -replace '/', [IO.Path]::DirectorySeparatorChar)
    Assert-Condition -Condition (Test-Path -LiteralPath $historicalPath -PathType Leaf) -Message 'The historical ROI-registration capture is missing.'
    $historicalHash = Get-LowercaseSha256 -LiteralPath $historicalPath
    Assert-Condition -Condition ($historicalHash -ceq [string]$Config.registration_basis.historical_capture_sha256) -Message 'The historical ROI-registration capture hash changed.'
}

function Get-RegionVerdicts {
    param(
        [Parameter(Mandatory)][object[]]$Metrics,
        [Parameter(Mandatory)][object]$Config
    )

    Assert-Condition -Condition ($Metrics.Count -eq @($Config.regions).Count) -Message 'ROI metric count does not match the registered configuration.'
    $metricIds = @($Metrics | ForEach-Object { [string]$_.id })
    Assert-Condition -Condition (($metricIds | Sort-Object -Unique).Count -eq $metricIds.Count) -Message 'ROI metrics contain duplicate ids.'
    $results = [Collections.Generic.List[object]]::new()
    foreach ($metric in $Metrics) {
        $registered = @($Config.regions | Where-Object { $_.id -ceq $metric.id })
        Assert-Condition -Condition ($registered.Count -eq 1) -Message "Unregistered ROI metric '$($metric.id)'."
        Assert-Condition -Condition ($registered[0].kind -ceq $metric.kind) -Message "ROI metric kind mismatch for '$($metric.id)'."
        if ($metric.kind -ceq 'vegetation') {
            $passed = (
                [double]$metric.changed_pixel_ratio -ge [double]$Config.pixel_difference.vegetation_min_changed_pixel_ratio -and
                [double]$metric.mean_absolute_rgb_delta -ge [double]$Config.pixel_difference.vegetation_min_mean_absolute_rgb_delta
            )
        } else {
            $passed = (
                [double]$metric.changed_pixel_ratio -le [double]$Config.pixel_difference.terrain_max_changed_pixel_ratio -and
                [double]$metric.mean_absolute_rgb_delta -le [double]$Config.pixel_difference.terrain_max_mean_absolute_rgb_delta
            )
        }
        $results.Add([ordered]@{
            id = [string]$metric.id
            kind = [string]$metric.kind
            pixel_rect = $metric.pixel_rect
            pixels = [int64]$metric.pixels
            changed_pixels = [int64]$metric.changed_pixels
            changed_pixel_ratio = [double]$metric.changed_pixel_ratio
            mean_absolute_rgb_delta = [double]$metric.mean_absolute_rgb_delta
            rmse_rgb = [double]$metric.rmse_rgb
            psnr_db = $metric.psnr_db
            passed = $passed
        })
    }
    return @($results)
}

function Get-AutomatedResult {
    param(
        [Parameter(Mandatory)][object[]]$RegionVerdicts,
        [Parameter(Mandatory)][int]$ConsoleErrorCount,
        [Parameter(Mandatory)][int]$ConsoleWarningCount,
        [Parameter(Mandatory)][int]$NetworkFailureCount,
        [Parameter(Mandatory)][object]$Config
    )

    $vegetationPassed = @($RegionVerdicts | Where-Object { $_.kind -ceq 'vegetation' -and $_.passed }).Count
    $terrainPassed = @($RegionVerdicts | Where-Object { $_.kind -ceq 'terrain_witness' -and $_.passed }).Count -eq 1
    $passed = (
        $vegetationPassed -ge [int]$Config.acceptance.minimum_passing_vegetation_regions -and
        $terrainPassed -and
        $ConsoleErrorCount -le [int]$Config.acceptance.maximum_console_errors -and
        $ConsoleWarningCount -le [int]$Config.acceptance.maximum_console_warnings -and
        $NetworkFailureCount -le [int]$Config.acceptance.maximum_network_failures
    )
    return [ordered]@{
        status = if ($passed) { 'pass' } else { 'fail' }
        vegetation_regions_passed = $vegetationPassed
        vegetation_regions_required = [int]$Config.acceptance.minimum_passing_vegetation_regions
        terrain_witness_passed = $terrainPassed
        console_errors = $ConsoleErrorCount
        console_warnings = $ConsoleWarningCount
        network_failures = $NetworkFailureCount
        human_temporal_inspection = 'required_not_performed_by_harness'
        r1_014_claim = 'not_made_by_harness'
    }
}

function Assert-Throws {
    param(
        [Parameter(Mandatory)][scriptblock]$Operation,
        [Parameter(Mandatory)][string]$Description
    )

    $threw = $false
    try {
        & $Operation
    } catch {
        $threw = $true
    }
    Assert-Condition -Condition $threw -Message "Self-test expected failure: $Description."
}

function Invoke-SelfTest {
    param([Parameter(Mandatory)][object]$Config)

    Assert-TemporalConfig -Config $Config
    $route = Get-ExactRoute -Root ([uri]'http://127.0.0.1:1420/') -Config $Config
    Assert-Condition -Condition ($route -ceq 'http://127.0.0.1:1420/?forge_scene=visual-valley&forge_weather=clear&forge_quality=high') -Message 'The exact clear/high route is not deterministic.'

    $badFrames = Copy-JsonValue -Value $Config
    $badFrames.readiness.minimum_request_animation_frames = 119
    Assert-Throws -Description '119-frame separation' -Operation { Assert-TemporalConfig -Config $badFrames }

    $badRoi = Copy-JsonValue -Value $Config
    $badRoi.regions[0].x = 0.60
    $badRoi.regions[0].y = 0.10
    Assert-Throws -Description 'ROI overlapping the sky' -Operation { Assert-TemporalConfig -Config $badRoi }

    $badThreshold = Copy-JsonValue -Value $Config
    $badThreshold.pixel_difference.vegetation_min_changed_pixel_ratio = 0.001
    Assert-Throws -Description 'threshold change without protocol bump' -Operation { Assert-TemporalConfig -Config $badThreshold }

    $syntheticMetrics = @(
        [pscustomobject]@{ id = 'vegetation_left_foreground'; kind = 'vegetation'; pixel_rect = @{}; pixels = 10000; changed_pixels = 30; changed_pixel_ratio = 0.003; mean_absolute_rgb_delta = 0.2; rmse_rgb = 1.0; psnr_db = 48.13 },
        [pscustomobject]@{ id = 'vegetation_right_midfield'; kind = 'vegetation'; pixel_rect = @{}; pixels = 10000; changed_pixels = 25; changed_pixel_ratio = 0.0025; mean_absolute_rgb_delta = 0.15; rmse_rgb = 0.9; psnr_db = 49.05 },
        [pscustomobject]@{ id = 'vegetation_right_foreground'; kind = 'vegetation'; pixel_rect = @{}; pixels = 10000; changed_pixels = 0; changed_pixel_ratio = 0.0; mean_absolute_rgb_delta = 0.0; rmse_rgb = 0.0; psnr_db = $null },
        [pscustomobject]@{ id = 'terrain_witness'; kind = 'terrain_witness'; pixel_rect = @{}; pixels = 10000; changed_pixels = 10; changed_pixel_ratio = 0.001; mean_absolute_rgb_delta = 0.05; rmse_rgb = 0.2; psnr_db = 62.11 }
    )
    $verdicts = Get-RegionVerdicts -Metrics $syntheticMetrics -Config $Config
    $passResult = Get-AutomatedResult -RegionVerdicts $verdicts -ConsoleErrorCount 0 -ConsoleWarningCount 0 -NetworkFailureCount 0 -Config $Config
    Assert-Condition -Condition ($passResult.status -ceq 'pass') -Message 'Synthetic positive matrix did not pass.'
    $failureResult = Get-AutomatedResult -RegionVerdicts $verdicts -ConsoleErrorCount 1 -ConsoleWarningCount 0 -NetworkFailureCount 0 -Config $Config
    Assert-Condition -Condition ($failureResult.status -ceq 'fail') -Message 'Synthetic console failure was not rejected.'
    $warningResult = Get-AutomatedResult -RegionVerdicts $verdicts -ConsoleErrorCount 0 -ConsoleWarningCount 1 -NetworkFailureCount 0 -Config $Config
    Assert-Condition -Condition ($warningResult.status -ceq 'fail') -Message 'Synthetic console warning was not rejected.'
    $syntheticTargets = @(
        [pscustomobject]@{
            id = 'synthetic-background-page'
            type = 'background_page'
            webSocketDebuggerUrl = 'ws://127.0.0.1:54321/devtools/page/background'
        },
        [pscustomobject]@{
            id = 'synthetic-page'
            type = 'page'
            webSocketDebuggerUrl = 'ws://127.0.0.1:54321/devtools/page/synthetic'
        },
        [pscustomobject]@{
            id = 'synthetic-second-page'
            type = 'page'
            webSocketDebuggerUrl = 'ws://127.0.0.1:54321/devtools/page/second'
        }
    )
    $syntheticTarget = Select-CdpTarget -Targets $syntheticTargets -Port 54321
    Assert-Condition -Condition ($null -ne $syntheticTarget -and $syntheticTarget.webSocketDebuggerUrl -ceq 'ws://127.0.0.1:54321/devtools/page/synthetic') -Message 'Synthetic CDP target selection failed.'
    $nestedSyntheticTarget = Select-CdpTarget -Targets (,$syntheticTargets) -Port 54321
    Assert-Condition -Condition ($null -ne $nestedSyntheticTarget -and $nestedSyntheticTarget.webSocketDebuggerUrl -ceq 'ws://127.0.0.1:54321/devtools/page/synthetic') -Message 'Nested multi-target CDP response normalization failed.'

    [pscustomobject]@{
        protocol = $Config.protocol_id
        config_validation = 'pass'
        route_validation = 'pass'
        mutation_rejections = 3
        synthetic_verdicts = 'pass'
        console_warning_rejection = 'pass'
        cdp_target_selection = 'pass'
        cdp_multi_target_normalization = 'pass'
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
    Assert-Condition -Condition ([string]::IsNullOrWhiteSpace($status)) -Message 'Temporal acquisition requires a completely clean Git worktree.'
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

function Get-RemoteValueText {
    param([Parameter(Mandatory)][object]$RemoteObject)

    if ($RemoteObject.PSObject.Properties.Name -contains 'value') {
        if ($RemoteObject.value -is [string]) {
            return [string]$RemoteObject.value
        }
        return ConvertTo-CompactJson -Value $RemoteObject.value
    }
    $descriptionProperty = $RemoteObject.PSObject.Properties['description']
    if ($null -ne $descriptionProperty -and -not [string]::IsNullOrWhiteSpace([string]$descriptionProperty.Value)) {
        return [string]$descriptionProperty.Value
    }
    return [string]$RemoteObject.type
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

function Add-CdpEvent {
    param([Parameter(Mandatory)][object]$Message)

    $method = [string]$Message.method
    switch ($method) {
        'Runtime.consoleAPICalled' {
            $url = $null
            $line = $null
            $stackTrace = Get-OptionalPropertyValue -Object $Message.params -Name 'stackTrace'
            $callFrames = @(Get-OptionalPropertyValue -Object $stackTrace -Name 'callFrames' -Default @())
            if ($callFrames.Count -gt 0) {
                $url = Get-OptionalPropertyValue -Object $callFrames[0] -Name 'url'
                $line = Get-OptionalPropertyValue -Object $callFrames[0] -Name 'lineNumber'
            }
            $script:ConsoleEvents.Add([ordered]@{
                source = 'runtime_console'
                level = [string]$Message.params.type
                timestamp = [double]$Message.params.timestamp
                text = (@($Message.params.args) | ForEach-Object { Get-RemoteValueText -RemoteObject $_ }) -join ' '
                url = $url
                line = $line
            })
        }
        'Log.entryAdded' {
            $entry = $Message.params.entry
            $script:ConsoleEvents.Add([ordered]@{
                source = 'browser_log'
                level = [string]$entry.level
                timestamp = [double]$entry.timestamp
                text = [string]$entry.text
                url = Get-OptionalPropertyValue -Object $entry -Name 'url'
                line = Get-OptionalPropertyValue -Object $entry -Name 'lineNumber'
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
                from_disk_cache = [bool](Get-OptionalPropertyValue -Object $response -Name 'fromDiskCache' -Default $false)
                from_service_worker = [bool](Get-OptionalPropertyValue -Object $response -Name 'fromServiceWorker' -Default $false)
            }
        }
        'Network.loadingFailed' {
            $requestId = [string]$Message.params.requestId
            $requestUrl = if ($script:NetworkRequests.ContainsKey($requestId)) { [string]$script:NetworkRequests[$requestId].url } else { $null }
            $script:NetworkFailures.Add([ordered]@{
                request_id = $requestId
                url = $requestUrl
                type = [string]$Message.params.type
                error_text = [string]$Message.params.errorText
                canceled = [bool](Get-OptionalPropertyValue -Object $Message.params -Name 'canceled' -Default $false)
                blocked_reason = [string](Get-OptionalPropertyValue -Object $Message.params -Name 'blockedReason' -Default '')
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
    $request = [ordered]@{ id = $messageId; method = $Method; params = $Parameters }
    $payload = [Text.Encoding]::UTF8.GetBytes((ConvertTo-CompactJson -Value $request))
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

    $flattenedTargets = @(
        foreach ($target in $Targets) {
            if ($target -is [Array]) {
                foreach ($nestedTarget in $target) {
                    $nestedTarget
                }
            } else {
                $target
            }
        }
    )
    $candidates = @(
        $flattenedTargets |
            Where-Object {
                $_.type -ceq 'page' -and
                -not [string]::IsNullOrWhiteSpace([string]$_.id) -and
                -not [string]::IsNullOrWhiteSpace([string]$_.webSocketDebuggerUrl)
            } |
            Select-Object -First 1
    )
    if ($candidates.Count -ne 1) {
        return $null
    }
    try {
        $wsUri = [uri]([string]$candidates[0].webSocketDebuggerUrl)
    } catch {
        return $null
    }
    if ($wsUri.Scheme -ceq 'ws' -and $wsUri.Port -eq $Port -and (Test-LoopbackHost -HostName $wsUri.DnsSafeHost)) {
        return $candidates[0]
    }
    return $null
}

function Wait-CdpTarget {
    param(
        [Parameter(Mandatory)][int]$Port,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [Parameter(Mandatory)][Diagnostics.Process]$Process
    )

    $endpoint = "http://127.0.0.1:$Port/json/list"
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastError = 'endpoint not ready'
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if ($Process.HasExited) {
            throw "Browser exited before exposing DevTools (exit code $($Process.ExitCode))."
        }
        try {
            $targets = Invoke-RestMethod -Uri $endpoint -TimeoutSec 2 -NoProxy
            $target = Select-CdpTarget -Targets $targets -Port $Port
            if ($null -ne $target) {
                return $target
            }
            $lastError = 'DevTools target did not use the reserved loopback endpoint.'
        } catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 200
    }
    throw "Browser DevTools target did not open on the reserved loopback port. Last error: $lastError"
}

function Get-ExactRoute {
    param(
        [Parameter(Mandatory)][uri]$Root,
        [Parameter(Mandatory)][object]$Config
    )

    Assert-Condition -Condition ($Root.Scheme -ceq 'http') -Message 'The local Trunk endpoint must use http.'
    Assert-Condition -Condition (Test-LoopbackHost -HostName $Root.DnsSafeHost) -Message 'The Trunk endpoint must resolve only to loopback.'
    Assert-Condition -Condition ($Root.AbsoluteUri -ceq [string]$Config.server.origin) -Message 'BaseUrl does not match the registered Trunk origin.'
    Assert-Condition -Condition ([string]::IsNullOrEmpty($Root.Query)) -Message 'BaseUrl must not contain a query.'
    Assert-Condition -Condition ([string]::IsNullOrEmpty($Root.Fragment)) -Message 'BaseUrl must not contain a fragment.'
    $builder = [UriBuilder]::new($Root)
    $builder.Path = [string]$Config.route.path
    $builder.Query = @(
        "forge_scene=$([uri]::EscapeDataString([string]$Config.route.query.forge_scene))",
        "forge_weather=$([uri]::EscapeDataString([string]$Config.route.query.forge_weather))",
        "forge_quality=$([uri]::EscapeDataString([string]$Config.route.query.forge_quality))"
    ) -join '&'
    return $builder.Uri.AbsoluteUri
}

function Get-ReadinessSnapshot {
    param([Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket)

    $raw = Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const gateNode = document.getElementById("gate0-telemetry-ledger");
  const sceneNode = document.getElementById("forge-scene-telemetry-ledger");
  const canvas = document.getElementById("forge-canvas");
  const rect = canvas?.getBoundingClientRect();
  return JSON.stringify({
    document_ready_state: document.readyState,
    navigator_gpu: Boolean(navigator.gpu),
    input_guard: window.__forgeGate1TemporalInputGuard ?? null,
    gate_ledger: gateNode?.textContent ?? "",
    scene_ledger: sceneNode?.textContent ?? "",
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
'@
    return ConvertFrom-ExactJson -Json ([string]$raw)
}

function Assert-ReadySnapshot {
    param(
        [Parameter(Mandatory)][object]$Snapshot,
        [Parameter(Mandatory)][object]$Config,
        [Parameter(Mandatory)][string]$ExpectedCommit
    )

    Assert-Condition -Condition ($Snapshot.document_ready_state -ceq 'complete') -Message 'Document is not complete.'
    Assert-Condition -Condition ($Snapshot.navigator_gpu -eq $true) -Message 'navigator.gpu is not exposed.'
    Assert-Condition -Condition ($null -ne $Snapshot.input_guard -and $Snapshot.input_guard.active -eq $true) -Message 'The camera/player input guard is not active.'
    Assert-Condition -Condition ([int]$Snapshot.input_guard.dispatched_input_events -eq 0) -Message 'The harness unexpectedly dispatched an input event.'
    Assert-Condition -Condition ($null -ne $Snapshot.canvas) -Message 'The canonical canvas is missing.'
    Assert-Condition -Condition ([double]$Snapshot.canvas.width -gt 0 -and [double]$Snapshot.canvas.height -gt 0) -Message 'The canvas has no positive CSS extent.'
    Assert-Condition -Condition ([int]$Snapshot.canvas.backing_width -gt 0 -and [int]$Snapshot.canvas.backing_height -gt 0) -Message 'The canvas has no positive backing extent.'
    Assert-Condition -Condition ([double]$Snapshot.canvas.device_pixel_ratio -eq [double]$Config.browser.device_scale_factor) -Message 'The Browser device pixel ratio drifted.'
    Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$Snapshot.gate_ledger)) -Message 'Gate ledger is empty.'
    Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$Snapshot.scene_ledger)) -Message 'Scene ledger is empty.'

    $gate = ConvertFrom-ExactJson -Json ([string]$Snapshot.gate_ledger)
    $scene = ConvertFrom-ExactJson -Json ([string]$Snapshot.scene_ledger)
    Assert-Condition -Condition ($gate.schema -ceq 'forge.gate0-studio-telemetry' -and [int]$gate.schema_version -eq 1) -Message 'Unexpected Studio gate ledger contract.'
    Assert-Condition -Condition ($gate.target -ceq 'wasm_browser') -Message 'Studio gate ledger target is not wasm_browser.'
    Assert-Condition -Condition ([int]$gate.viewport.starts -eq 1) -Message 'Studio viewport did not start exactly once.'
    Assert-Condition -Condition ([int]$gate.frame.warmup_samples -eq 60 -and [int]$gate.frame.samples -eq 120) -Message 'Studio frame evidence is not the completed 60+120 window.'
    Assert-Condition -Condition ($scene.schema -ceq 'forge.studio-scene-telemetry' -and [int]$scene.schema_version -ge 8) -Message 'Unexpected Studio scene ledger contract.'
    Assert-Condition -Condition ($scene.build_source_commit -ceq $ExpectedCommit) -Message 'The served Studio bundle is not embedded with the exact repository commit.'
    Assert-Condition -Condition ([string]::IsNullOrWhiteSpace([string]$scene.failure_reason)) -Message "Scene ledger reports a failure: $($scene.failure_reason)"
    Assert-Condition -Condition ($scene.parity_status -ceq 'source_manifest_matched') -Message 'Scene source/runtime manifests are not matched.'
    foreach ($manifest in @($scene.selected_manifest, $scene.runtime_manifest)) {
        Assert-Condition -Condition ($manifest.scene -ceq $Config.fixed_context.scene) -Message 'Scene selection drifted.'
        Assert-Condition -Condition ($manifest.weather_state -ceq $Config.fixed_context.weather) -Message 'Weather selection drifted.'
        Assert-Condition -Condition ($manifest.quality_selection.mode -ceq $Config.fixed_context.quality_mode) -Message 'Quality mode drifted.'
        Assert-Condition -Condition ($manifest.quality_selection.tier -ceq $Config.fixed_context.quality_tier) -Message 'Quality tier drifted.'
        Assert-Condition -Condition ($manifest.camera_owner -ceq $Config.fixed_context.camera_owner) -Message 'Camera owner drifted.'
        Assert-Condition -Condition ([int]$manifest.expected_active_camera_count -eq [int]$Config.fixed_context.active_camera_count) -Message 'Expected active camera count drifted.'
    }
    Assert-Condition -Condition ([int]$scene.runtime_observation.active_camera_3d_entities -eq [int]$Config.fixed_context.active_camera_count) -Message 'Runtime active camera count drifted.'
    Assert-Condition -Condition ($scene.runtime_observation.quality_current_tier -ceq $Config.fixed_context.quality_tier) -Message 'Runtime current quality tier drifted.'
    Assert-Condition -Condition ($scene.runtime_observation.quality_applied_tier -ceq $Config.fixed_context.quality_tier) -Message 'Runtime applied quality tier drifted.'
    return [pscustomobject]@{ Gate = $gate; Scene = $scene }
}

function Wait-ReadySnapshot {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][object]$Config,
        [Parameter(Mandatory)][string]$ExpectedCommit
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds([int]$Config.readiness.timeout_seconds)
    $lastState = 'DOM not observed'
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $snapshot = Get-ReadinessSnapshot -Socket $Socket
        try {
            [void](Assert-ReadySnapshot -Snapshot $snapshot -Config $Config -ExpectedCommit $ExpectedCommit)
            return $snapshot
        } catch {
            $lastState = $_.Exception.Message
            if (-not [string]::IsNullOrWhiteSpace([string]$snapshot.scene_ledger)) {
                try {
                    $scene = ConvertFrom-ExactJson -Json ([string]$snapshot.scene_ledger)
                    if (-not [string]::IsNullOrWhiteSpace([string]$scene.failure_reason)) {
                        throw "Scene runtime reached a terminal failure: $($scene.failure_reason)"
                    }
                } catch {
                    if ($_.Exception.Message -like 'Scene runtime reached a terminal failure:*') {
                        throw
                    }
                }
            }
        }
        Start-Sleep -Milliseconds 150
    }
    throw "Timed out waiting for the fixed clear/high Browser scene. Last state: $lastState"
}

function Assert-FixedContextStable {
    param(
        [Parameter(Mandatory)][object]$Before,
        [Parameter(Mandatory)][object]$After,
        [Parameter(Mandatory)][object]$Config,
        [Parameter(Mandatory)][string]$ExpectedCommit
    )

    $beforeLedgers = Assert-ReadySnapshot -Snapshot $Before -Config $Config -ExpectedCommit $ExpectedCommit
    $afterLedgers = Assert-ReadySnapshot -Snapshot $After -Config $Config -ExpectedCommit $ExpectedCommit
    Assert-Condition -Condition ($beforeLedgers.Scene.build_source_commit -ceq $afterLedgers.Scene.build_source_commit) -Message 'Embedded build commit changed between captures.'
    foreach ($side in @('selected_manifest', 'runtime_manifest')) {
        $beforeManifest = $beforeLedgers.Scene.$side
        $afterManifest = $afterLedgers.Scene.$side
        Assert-Condition -Condition ((ConvertTo-CompactJson -Value $beforeManifest.source_hashes) -ceq (ConvertTo-CompactJson -Value $afterManifest.source_hashes)) -Message "$side source hashes changed between captures."
        Assert-Condition -Condition ($beforeManifest.camera_owner -ceq $afterManifest.camera_owner) -Message "$side camera owner changed between captures."
        Assert-Condition -Condition ($beforeManifest.quality_tier -ceq $afterManifest.quality_tier) -Message "$side quality tier changed between captures."
        Assert-Condition -Condition ($beforeManifest.weather_state -ceq $afterManifest.weather_state) -Message "$side weather changed between captures."
    }
    Assert-Condition -Condition ([int]$Before.canvas.backing_width -eq [int]$After.canvas.backing_width -and [int]$Before.canvas.backing_height -eq [int]$After.canvas.backing_height) -Message 'Canvas backing dimensions changed between captures.'
    Assert-Condition -Condition ([double]$Before.canvas.left -eq [double]$After.canvas.left -and [double]$Before.canvas.top -eq [double]$After.canvas.top -and [double]$Before.canvas.width -eq [double]$After.canvas.width -and [double]$Before.canvas.height -eq [double]$After.canvas.height) -Message 'Canvas CSS rectangle changed between captures.'
}

function Get-CanvasScreenshot {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][object]$Canvas
    )

    $result = Invoke-CdpCommand -Socket $Socket -Method 'Page.captureScreenshot' -Parameters ([ordered]@{
        format = 'png'
        fromSurface = $true
        captureBeyondViewport = $true
        clip = [ordered]@{
            x = [double]$Canvas.left + [double]$Canvas.scroll_x
            y = [double]$Canvas.top + [double]$Canvas.scroll_y
            width = [double]$Canvas.width
            height = [double]$Canvas.height
            scale = 1
        }
    }) -TimeoutSeconds 45
    Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$result.data)) -Message 'CDP returned an empty canvas screenshot.'
    return [string]$result.data
}

function Wait-RegisteredAnimationFrames {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][int]$MinimumFrames
    )

    $expression = @"
new Promise((resolve, reject) => {
  const minimumFrames = $MinimumFrames;
  const startedAt = performance.now();
  let callbacks = 0;
  let firstTimestamp = null;
  const timeout = setTimeout(() => reject(new Error("requestAnimationFrame separation timed out")), 30000);
  const step = (timestamp) => {
    callbacks += 1;
    if (firstTimestamp === null) firstTimestamp = timestamp;
    if (callbacks >= minimumFrames) {
      clearTimeout(timeout);
      resolve({
        requested_callbacks: minimumFrames,
        observed_callbacks: callbacks,
        first_callback_timestamp_ms: firstTimestamp,
        last_callback_timestamp_ms: timestamp,
        elapsed_performance_ms: performance.now() - startedAt
      });
    } else {
      requestAnimationFrame(step);
    }
  };
  requestAnimationFrame(step);
})
"@
    $result = Invoke-CdpExpression -Socket $Socket -Expression $expression -TimeoutSeconds 45
    Assert-Condition -Condition ([int]$result.observed_callbacks -ge $MinimumFrames) -Message 'Observed requestAnimationFrame separation is below the registered minimum.'
    return $result
}

function Get-ImageMetrics {
    param(
        [Parameter(Mandatory)][System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)][string]$FirstPngBase64,
        [Parameter(Mandatory)][string]$SecondPngBase64,
        [Parameter(Mandatory)][object]$Config
    )

    $inputJson = ConvertTo-CompactJson -Value ([ordered]@{
        first = $FirstPngBase64
        second = $SecondPngBase64
        changed_channel_delta = [int]$Config.pixel_difference.changed_channel_delta
        regions = $Config.regions
    })
    $expression = @"
(async () => {
  const input = $inputJson;
  const decode = async (encoded) => {
    const binary = atob(encoded);
    const bytes = new Uint8Array(binary.length);
    for (let index = 0; index < binary.length; index += 1) bytes[index] = binary.charCodeAt(index);
    return createImageBitmap(new Blob([bytes], { type: "image/png" }));
  };
  const first = await decode(input.first);
  const second = await decode(input.second);
  if (first.width !== second.width || first.height !== second.height) {
    throw new Error("temporal screenshots have different dimensions");
  }
  const canvas = document.createElement("canvas");
  canvas.width = first.width;
  canvas.height = first.height;
  const context = canvas.getContext("2d", { willReadFrequently: true });
  context.drawImage(first, 0, 0);
  const a = context.getImageData(0, 0, canvas.width, canvas.height).data;
  context.clearRect(0, 0, canvas.width, canvas.height);
  context.drawImage(second, 0, 0);
  const b = context.getImageData(0, 0, canvas.width, canvas.height).data;
  const round = (value) => Math.round(value * 100000000) / 100000000;
  const metrics = input.regions.map((region) => {
    const x0 = Math.max(0, Math.floor(region.x * canvas.width));
    const y0 = Math.max(0, Math.floor(region.y * canvas.height));
    const x1 = Math.min(canvas.width, Math.ceil((region.x + region.width) * canvas.width));
    const y1 = Math.min(canvas.height, Math.ceil((region.y + region.height) * canvas.height));
    let pixels = 0;
    let changedPixels = 0;
    let absoluteSum = 0;
    let squareSum = 0;
    for (let y = y0; y < y1; y += 1) {
      for (let x = x0; x < x1; x += 1) {
        const offset = (y * canvas.width + x) * 4;
        const dr = Math.abs(a[offset] - b[offset]);
        const dg = Math.abs(a[offset + 1] - b[offset + 1]);
        const db = Math.abs(a[offset + 2] - b[offset + 2]);
        if (Math.max(dr, dg, db) >= input.changed_channel_delta) changedPixels += 1;
        absoluteSum += dr + dg + db;
        squareSum += dr * dr + dg * dg + db * db;
        pixels += 1;
      }
    }
    const meanAbsolute = absoluteSum / (pixels * 3);
    const rmse = Math.sqrt(squareSum / (pixels * 3));
    return {
      id: region.id,
      kind: region.kind,
      pixel_rect: { x: x0, y: y0, width: x1 - x0, height: y1 - y0 },
      pixels,
      changed_pixels: changedPixels,
      changed_pixel_ratio: round(changedPixels / pixels),
      mean_absolute_rgb_delta: round(meanAbsolute),
      rmse_rgb: round(rmse),
      psnr_db: rmse === 0 ? null : round(20 * Math.log10(255 / rmse))
    };
  });
  first.close();
  second.close();
  return JSON.stringify({ width: canvas.width, height: canvas.height, metrics });
})()
"@
    $raw = Invoke-CdpExpression -Socket $Socket -Expression $expression -TimeoutSeconds 90
    return ConvertFrom-ExactJson -Json ([string]$raw)
}

function Write-Utf8Json {
    param(
        [Parameter(Mandatory)][string]$LiteralPath,
        [Parameter(Mandatory)][object]$Value
    )

    $json = $Value | ConvertTo-Json -Depth 64
    [IO.File]::WriteAllText($LiteralPath, "$json`n", [Text.UTF8Encoding]::new($false))
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

function Get-ServedBundleEvidence {
    param(
        [Parameter(Mandatory)][uri]$Root,
        [Parameter(Mandatory)][object]$Config
    )

    $distRoot = [IO.Path]::GetFullPath(
        (Join-Path $repositoryRoot ([string]$Config.server.dist_root -replace '/', [IO.Path]::DirectorySeparatorChar))
    )
    $repositoryPrefix = $repositoryRoot.TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar
    ) + [IO.Path]::DirectorySeparatorChar
    Assert-Condition -Condition ($distRoot.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) -Message 'The registered dist root escaped the repository.'
    Assert-Condition -Condition (Test-Path -LiteralPath $distRoot -PathType Container) -Message 'The Studio dist directory is missing; perform an exact Trunk build first.'

    $handler = [Net.Http.SocketsHttpHandler]::new()
    $handler.UseProxy = $false
    $client = [Net.Http.HttpClient]::new($handler, $true)
    $client.Timeout = [TimeSpan]::FromMinutes(5)
    $evidence = [Collections.Generic.List[object]]::new()
    try {
        foreach ($relativePath in @($Config.server.bundle_files)) {
            $normalizedRelativePath = ([string]$relativePath).Replace('/', [IO.Path]::DirectorySeparatorChar)
            $localPath = [IO.Path]::GetFullPath((Join-Path $distRoot $normalizedRelativePath))
            $distPrefix = $distRoot.TrimEnd(
                [IO.Path]::DirectorySeparatorChar,
                [IO.Path]::AltDirectorySeparatorChar
            ) + [IO.Path]::DirectorySeparatorChar
            Assert-Condition -Condition ($localPath.StartsWith($distPrefix, [StringComparison]::OrdinalIgnoreCase)) -Message "Bundle path escaped dist: $relativePath"
            Assert-Condition -Condition (Test-Path -LiteralPath $localPath -PathType Leaf) -Message "Local Studio bundle is missing: $relativePath"

            $localItem = Get-Item -LiteralPath $localPath
            Assert-Condition -Condition ($localItem.Length -gt 0) -Message "Local Studio bundle is empty: $relativePath"
            $localSha256 = Get-LowercaseSha256 -LiteralPath $localPath
            $servedUri = [uri]::new($Root, [string]$relativePath)
            Assert-Condition -Condition ($servedUri.Scheme -ceq 'http' -and (Test-LoopbackHost -HostName $servedUri.DnsSafeHost)) -Message "Bundle URL is not loopback HTTP: $servedUri"

            $response = $client.GetAsync(
                $servedUri,
                [Net.Http.HttpCompletionOption]::ResponseHeadersRead
            ).GetAwaiter().GetResult()
            try {
                Assert-Condition -Condition ([int]$response.StatusCode -eq 200) -Message "Bundle request failed with HTTP $([int]$response.StatusCode): $servedUri"
                $stream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
                $algorithm = [Security.Cryptography.SHA256]::Create()
                try {
                    $buffer = [byte[]]::new(1048576)
                    $servedBytes = [int64]0
                    while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                        [void]$algorithm.TransformBlock($buffer, 0, $read, $buffer, 0)
                        $servedBytes += $read
                    }
                    [void]$algorithm.TransformFinalBlock([byte[]]::new(0), 0, 0)
                    $servedSha256 = [Convert]::ToHexString($algorithm.Hash).ToLowerInvariant()
                } finally {
                    $algorithm.Dispose()
                    $stream.Dispose()
                }
                Assert-Condition -Condition ($servedBytes -eq [int64]$localItem.Length) -Message "Served/local byte count mismatch for $relativePath."
                Assert-Condition -Condition ($servedSha256 -ceq $localSha256) -Message "Served/local SHA-256 mismatch for $relativePath."
                $contentType = if ($null -eq $response.Content.Headers.ContentType) {
                    ''
                } else {
                    $response.Content.Headers.ContentType.ToString()
                }
                $evidence.Add([ordered]@{
                    relative_path = ([string]$relativePath).Replace('\', '/')
                    url = $servedUri.AbsoluteUri
                    content_type = $contentType
                    local_bytes = [int64]$localItem.Length
                    served_bytes = $servedBytes
                    local_sha256 = $localSha256
                    served_sha256 = $servedSha256
                    exact_match = $true
                })
            } finally {
                $response.Dispose()
            }
        }
    } finally {
        $client.Dispose()
    }
    Assert-Condition -Condition ($evidence.Count -eq @($Config.server.bundle_files).Count) -Message 'Served bundle evidence is incomplete.'
    return @($evidence)
}

if ($ValidateOnly -and $SelfTest) {
    throw '-ValidateOnly and -SelfTest are mutually exclusive.'
}

$resolvedConfigPath = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $ConfigPath).Path)
$canonicalConfigPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'gate1-browser-temporal.v1.json'))
Assert-Condition -Condition ($resolvedConfigPath.Equals($canonicalConfigPath, [StringComparison]::OrdinalIgnoreCase)) -Message 'Only the repository canonical v1 temporal configuration is accepted.'
$config = ConvertFrom-ExactJson -Json (Get-Content -LiteralPath $resolvedConfigPath -Raw)
Assert-TemporalConfig -Config $config

if ($ValidateOnly) {
    [pscustomobject]@{
        protocol = $config.protocol_id
        validation = 'pass'
        minimum_request_animation_frames = [int]$config.readiness.minimum_request_animation_frames
        vegetation_regions = @($config.regions | Where-Object { $_.kind -ceq 'vegetation' }).Count
        terrain_witness_regions = @($config.regions | Where-Object { $_.kind -ceq 'terrain_witness' }).Count
        excluded_regions = @($config.excluded_regions).Count
    }
    return
}

if ($SelfTest) {
    Invoke-SelfTest -Config $config
    return
}

$gitIdentity = Get-GitIdentity
$exactRoute = Get-ExactRoute -Root $BaseUrl -Config $config
$resolvedBrowser = Find-BrowserExecutable -RequestedPath $BrowserExecutable
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path (Split-Path -Parent $repositoryRoot) '.gate1-evidence-browser-temporal'
}
$resolvedOutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$repoWithSeparator = $repositoryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
Assert-Condition -Condition (-not $resolvedOutputRoot.StartsWith($repoWithSeparator, [StringComparison]::OrdinalIgnoreCase)) -Message 'Browser temporal evidence must be written outside the Git worktree.'
[void](New-Item -ItemType Directory -Force -Path $resolvedOutputRoot)

$stamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$runDirectory = Join-Path $resolvedOutputRoot "$($gitIdentity.commit.Substring(0, 12))-$stamp"
[void](New-Item -ItemType Directory -Path $runDirectory)
$profileDirectory = Join-Path ([IO.Path]::GetTempPath()) "forge-gate1-browser-temporal-$([guid]::NewGuid().ToString('N'))"
[void](New-Item -ItemType Directory -Path $profileDirectory)

$browserProcess = $null
$socket = $null
$acquisitionCompleted = $false
try {
    $port = New-LoopbackPort
    $browserProcess = Start-ControlledBrowser -Executable $resolvedBrowser -Port $port -ProfileDirectory $profileDirectory -Config $config
    $target = Wait-CdpTarget -Port $port -TimeoutSeconds 30 -Process $browserProcess
    $socket = Connect-CdpSocket -WebSocketUrl ([string]$target.webSocketDebuggerUrl)

    foreach ($domain in @('Runtime.enable', 'Page.enable', 'Network.enable', 'Log.enable')) {
        [void](Invoke-CdpCommand -Socket $socket -Method $domain)
    }
    [void](Invoke-CdpCommand -Socket $socket -Method 'Emulation.setDeviceMetricsOverride' -Parameters ([ordered]@{
        width = [int]$config.browser.viewport_width_px
        height = [int]$config.browser.viewport_height_px
        deviceScaleFactor = [double]$config.browser.device_scale_factor
        mobile = $false
    }))

    $blockedEventsJson = ConvertTo-CompactJson -Value @($config.fixed_context.blocked_event_types)
    $inputGuardScript = @"
(() => {
  const blocked = $blockedEventsJson;
  const stop = (event) => {
    event.preventDefault();
    event.stopImmediatePropagation();
  };
  for (const type of blocked) window.addEventListener(type, stop, { capture: true, passive: false });
  Object.defineProperty(window, "__forgeGate1TemporalInputGuard", {
    configurable: false,
    enumerable: false,
    writable: false,
    value: Object.freeze({ active: true, blocked_event_types: Object.freeze([...blocked]), dispatched_input_events: 0 })
  });
})();
"@
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.addScriptToEvaluateOnNewDocument' -Parameters ([ordered]@{ source = $inputGuardScript }))
    [void](Invoke-CdpCommand -Socket $socket -Method 'Page.navigate' -Parameters ([ordered]@{ url = $exactRoute }))

    $before = Wait-ReadySnapshot -Socket $socket -Config $config -ExpectedCommit ([string]$gitIdentity.commit)
    $browserVersion = Invoke-CdpCommand -Socket $socket -Method 'Browser.getVersion'
    $capturedAtA = [DateTimeOffset]::UtcNow
    $captureA = Get-CanvasScreenshot -Socket $socket -Canvas $before.canvas
    $separation = Wait-RegisteredAnimationFrames -Socket $socket -MinimumFrames ([int]$config.readiness.minimum_request_animation_frames)
    $after = Wait-ReadySnapshot -Socket $socket -Config $config -ExpectedCommit ([string]$gitIdentity.commit)
    Assert-FixedContextStable -Before $before -After $after -Config $config -ExpectedCommit ([string]$gitIdentity.commit)
    $beforeSceneLedger = ConvertFrom-ExactJson -Json ([string]$before.scene_ledger)
    $capturedAtB = [DateTimeOffset]::UtcNow
    $captureB = Get-CanvasScreenshot -Socket $socket -Canvas $after.canvas
    $comparison = Get-ImageMetrics -Socket $socket -FirstPngBase64 $captureA -SecondPngBase64 $captureB -Config $config
    [void](Invoke-CdpExpression -Socket $socket -Expression '0')
    $servedBundles = Get-ServedBundleEvidence -Root $BaseUrl -Config $config
    $gitIdentityAfter = Get-GitIdentity
    Assert-Condition -Condition ($gitIdentityAfter.commit -ceq $gitIdentity.commit) -Message 'Repository commit changed during temporal acquisition.'
    Assert-Condition -Condition ($gitIdentityAfter.tree -ceq $gitIdentity.tree) -Message 'Repository tree changed during temporal acquisition.'
    $gitIdentity['clean_after_acquisition'] = $true

    $regionVerdicts = Get-RegionVerdicts -Metrics @($comparison.metrics) -Config $config
    $responses = @($script:NetworkResponses.Values | Where-Object { -not ([string]$_.url).StartsWith('data:', [StringComparison]::OrdinalIgnoreCase) } | Sort-Object url, request_id)
    $httpFailures = @($responses | Where-Object { [int]$_.status -ge 400 })
    $networkFailures = @($script:NetworkFailures)
    $networkFailureCount = $networkFailures.Count + $httpFailures.Count
    $consoleEntries = @($script:ConsoleEvents | Sort-Object timestamp, source)
    $consoleErrors = @($consoleEntries | Where-Object { $_.level -cin @('error', 'assert') })
    $consoleWarnings = @($consoleEntries | Where-Object { $_.level -cin @('warn', 'warning') })
    $automatedResult = Get-AutomatedResult -RegionVerdicts $regionVerdicts -ConsoleErrorCount $consoleErrors.Count -ConsoleWarningCount $consoleWarnings.Count -NetworkFailureCount $networkFailureCount -Config $config

    $captureAPath = Join-Path $runDirectory 'canvas-frame-a.png'
    $captureBPath = Join-Path $runDirectory 'canvas-frame-b.png'
    [IO.File]::WriteAllBytes($captureAPath, [Convert]::FromBase64String($captureA))
    [IO.File]::WriteAllBytes($captureBPath, [Convert]::FromBase64String($captureB))
    $gateBeforePath = Join-Path $runDirectory 'gate-ledger-before.json'
    $gateAfterPath = Join-Path $runDirectory 'gate-ledger-after.json'
    $sceneBeforePath = Join-Path $runDirectory 'scene-ledger-before.json'
    $sceneAfterPath = Join-Path $runDirectory 'scene-ledger-after.json'
    Write-Utf8Text -LiteralPath $gateBeforePath -Value ([string]$before.gate_ledger)
    Write-Utf8Text -LiteralPath $gateAfterPath -Value ([string]$after.gate_ledger)
    Write-Utf8Text -LiteralPath $sceneBeforePath -Value ([string]$before.scene_ledger)
    Write-Utf8Text -LiteralPath $sceneAfterPath -Value ([string]$after.scene_ledger)

    $consolePath = Join-Path $runDirectory 'console.json'
    $networkPath = Join-Path $runDirectory 'network.json'
    $servedBundlesPath = Join-Path $runDirectory 'served-bundles.json'
    Write-Utf8Json -LiteralPath $consolePath -Value ([ordered]@{
        schema = 'forge.gate1-browser-console'
        schema_version = 1
        entries = $consoleEntries
        error_count = $consoleErrors.Count
        warning_count = $consoleWarnings.Count
    })
    Write-Utf8Json -LiteralPath $networkPath -Value ([ordered]@{
        schema = 'forge.gate1-browser-network'
        schema_version = 1
        responses = $responses
        loading_failures = $networkFailures
        http_failures = $httpFailures
        failure_count = $networkFailureCount
    })
    Write-Utf8Json -LiteralPath $servedBundlesPath -Value ([ordered]@{
        schema = 'forge.gate1-browser-served-bundles'
        schema_version = 1
        exact_matches = @($servedBundles).Count
        files = @($servedBundles)
    })

    $artifactDescriptors = [ordered]@{
        canvas_frame_a = New-ArtifactDescriptor -LiteralPath $captureAPath -Root $runDirectory
        canvas_frame_b = New-ArtifactDescriptor -LiteralPath $captureBPath -Root $runDirectory
        gate_ledger_before = New-ArtifactDescriptor -LiteralPath $gateBeforePath -Root $runDirectory
        gate_ledger_after = New-ArtifactDescriptor -LiteralPath $gateAfterPath -Root $runDirectory
        scene_ledger_before = New-ArtifactDescriptor -LiteralPath $sceneBeforePath -Root $runDirectory
        scene_ledger_after = New-ArtifactDescriptor -LiteralPath $sceneAfterPath -Root $runDirectory
        console = New-ArtifactDescriptor -LiteralPath $consolePath -Root $runDirectory
        network = New-ArtifactDescriptor -LiteralPath $networkPath -Root $runDirectory
        served_bundles = New-ArtifactDescriptor -LiteralPath $servedBundlesPath -Root $runDirectory
    }
    $protocolFiles = @(
        'scripts/gate1-browser-temporal.v1.json',
        'scripts/Invoke-Gate1BrowserTemporal.ps1',
        'scripts/Test-Gate1BrowserTemporal.ps1',
        'scripts/Invoke-StudioTrunk.ps1',
        'apps/forge_studio/src/lib.rs',
        'docs/evidence/gate1/R1_014_BROWSER_TEMPORAL_PROTOCOL.md'
    ) | ForEach-Object {
        $fullPath = Join-Path $repositoryRoot ($_ -replace '/', [IO.Path]::DirectorySeparatorChar)
        [ordered]@{ path = $_; sha256 = Get-LowercaseSha256 -LiteralPath $fullPath }
    }

    $ledger = [ordered]@{
        schema = 'forge.gate1-browser-temporal-ledger'
        schema_version = 1
        protocol_id = $config.protocol_id
        recorded_at_utc = [DateTimeOffset]::UtcNow.ToString('o')
        git = $gitIdentity
        route = $exactRoute
        browser = [ordered]@{
            executable = $resolvedBrowser
            product = [string]$browserVersion.product
            revision = [string]$browserVersion.revision
            protocol_version = [string]$browserVersion.protocolVersion
            user_agent = [string]$browserVersion.userAgent
            headed = $true
            navigator_gpu = [bool]$before.navigator_gpu
            viewport = [ordered]@{
                width_px = [int]$config.browser.viewport_width_px
                height_px = [int]$config.browser.viewport_height_px
                device_scale_factor = [double]$config.browser.device_scale_factor
            }
        }
        fixed_context = [ordered]@{
            build_source_commit = [string]$beforeSceneLedger.build_source_commit
            input_guard = $before.input_guard
            camera_owner = [string]$beforeSceneLedger.runtime_manifest.camera_owner
            active_camera_count = [int]$beforeSceneLedger.runtime_observation.active_camera_3d_entities
            player_controller_hash = [string]$beforeSceneLedger.runtime_manifest.source_hashes.player_controller
            weather = [string]$beforeSceneLedger.runtime_manifest.weather_state
            quality_mode = [string]$beforeSceneLedger.runtime_manifest.quality_selection.mode
            quality_tier = [string]$beforeSceneLedger.runtime_manifest.quality_tier
            stable_between_captures = $true
        }
        captures = [ordered]@{
            canvas = $before.canvas
            frame_a_recorded_at_utc = $capturedAtA.ToString('o')
            frame_b_recorded_at_utc = $capturedAtB.ToString('o')
            request_animation_frame_separation = $separation
            decoded_width_px = [int]$comparison.width
            decoded_height_px = [int]$comparison.height
        }
        registration = [ordered]@{
            config_sha256 = Get-LowercaseSha256 -LiteralPath $resolvedConfigPath
            basis = $config.registration_basis
            changed_channel_delta = [int]$config.pixel_difference.changed_channel_delta
            excluded_regions = $config.excluded_regions
            regions = $config.regions
            thresholds = $config.pixel_difference
        }
        difference_metrics = $regionVerdicts
        console = [ordered]@{ entries = $consoleEntries.Count; errors = $consoleErrors.Count; warnings = $consoleWarnings.Count; artifact = $artifactDescriptors.console }
        network = [ordered]@{ responses = $responses.Count; failures = $networkFailureCount; artifact = $artifactDescriptors.network }
        served_bundles = [ordered]@{
            exact_matches = @($servedBundles).Count
            required_matches = @($config.server.bundle_files).Count
            files = @($servedBundles)
            artifact = $artifactDescriptors.served_bundles
        }
        artifacts = $artifactDescriptors
        protocol_files = @($protocolFiles)
        automated_result = $automatedResult
        limitations = @(
            'requestAnimationFrame callbacks bound temporal separation but are not GPU timestamp queries.',
            'Temporal canvas differences provide Browser presentation evidence without changing the Studio gpu_presentation field.',
            'The harness does not close R1-014: human inspection of both captures and registered ROIs remains required.'
        )
    }
    $ledgerPath = Join-Path $runDirectory 'r1-014-browser-temporal-ledger.json'
    Write-Utf8Json -LiteralPath $ledgerPath -Value $ledger
    $ledgerDescriptor = New-ArtifactDescriptor -LiteralPath $ledgerPath -Root $runDirectory
    $manifest = [ordered]@{
        schema = 'forge.gate1-browser-temporal-artifact-manifest'
        schema_version = 1
        git_commit = $gitIdentity.commit
        artifacts = @($artifactDescriptors.Values) + @($ledgerDescriptor)
    }
    $manifestPath = Join-Path $runDirectory 'artifact-manifest.json'
    Write-Utf8Json -LiteralPath $manifestPath -Value $manifest
    $acquisitionCompleted = $true

    [pscustomobject]@{
        status = $automatedResult.status
        run_directory = $runDirectory
        ledger = $ledgerPath
        ledger_sha256 = $ledgerDescriptor.sha256
        artifact_manifest = $manifestPath
        request_animation_frames = [int]$separation.observed_callbacks
        r1_014 = 'open_human_temporal_inspection_required'
    }
    if ($automatedResult.status -cne 'pass') {
        throw "R1-014 Browser temporal acquisition completed with automated_result=$($automatedResult.status). Evidence was preserved at $runDirectory"
    }
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
    if (Test-Path -LiteralPath $profileDirectory -PathType Container) {
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        $resolvedProfile = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $profileDirectory).Path)
        if ($resolvedProfile.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolvedProfile).StartsWith('forge-gate1-browser-temporal-', [StringComparison]::Ordinal)) {
            Remove-Item -LiteralPath $resolvedProfile -Recurse -Force -ErrorAction SilentlyContinue
        } else {
            Write-Warning "Refusing to remove unexpected Browser profile path: $resolvedProfile"
        }
    }
    if (-not $acquisitionCompleted) {
        Write-Verbose "Incomplete evidence, if any, remains at $runDirectory"
    }
}
