[CmdletBinding(DefaultParameterSetName = 'Capture')]
param(
    [Parameter(ParameterSetName = 'Capture')]
    [string]$ExecutablePath,
    [Parameter(ParameterSetName = 'Capture')]
    [string]$OutputDirectory,
    [Parameter(ParameterSetName = 'Capture')]
    [ValidateRange(0, 60)]
    [int]$WarmupSeconds = 1,
    [Parameter(ParameterSetName = 'Capture')]
    [ValidateRange(5, 120)]
    [int]$WindowTimeoutSeconds = 30,
    [Parameter(ParameterSetName = 'Capture')]
    [ValidateRange(5, 180)]
    [int]$TelemetryTimeoutSeconds = 45,
    [Parameter(ParameterSetName = 'Capture')]
    [switch]$KeepOpen,
    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$expectedBranch = 'Rust'
$expectedWindowTitle = 'OpenRPG Forge Studio · Gate 0 proof'
$expectedCdpTitle = 'OpenRPG Forge Studio'
$expectedCdpUrl = 'http://tauri.localhost/index.html?forge_gate0_probe=tauri-permission'
$expectedCdpQuery = '?forge_gate0_probe=tauri-permission'
$buildCommandLabel = 'cargo tauri build --no-bundle --config apps/forge_studio/tauri.gate0-proof.conf.json'
$proofConfigurationRelativePath = 'apps/forge_studio/tauri.gate0-proof.conf.json'
$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$telemetrySchemaPath = Join-Path $repositoryRoot 'apps/forge_studio/gate0-telemetry.schema.json'
$captureSchemaPath = Join-Path $repositoryRoot 'apps/forge_studio/gate0-window-capture.schema.json'
$proofConfigurationPath = Join-Path $repositoryRoot $proofConfigurationRelativePath
$canonicalExecutable = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'target/release/forge-studio.exe'))
$script:CdpMessageId = 0

function Assert-FileExists {
    param(
        [Parameter(Mandatory)]
        [string]$LiteralPath,
        [Parameter(Mandatory)]
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $LiteralPath -PathType Leaf)) {
        throw "$Description not found: $LiteralPath"
    }
}

function ConvertTo-CompactJson {
    param(
        [Parameter(Mandatory)]
        [object]$Value
    )

    return ($Value | ConvertTo-Json -Depth 24 -Compress)
}

function Wait-NonGenericTask {
    param(
        [Parameter(Mandatory)]
        [System.Threading.Tasks.Task]$Task
    )

    # PowerShell 7.6 / .NET 10 can surface the internal VoidTaskResult from a
    # non-generic Task. Suppress it so callers return only their declared data.
    [void]$Task.GetAwaiter().GetResult()
}

function Copy-JsonValue {
    param(
        [Parameter(Mandatory)]
        [object]$Value
    )

    return ((ConvertTo-CompactJson -Value $Value) | ConvertFrom-Json -Depth 24)
}

function Assert-JsonSchema {
    param(
        [Parameter(Mandatory)]
        [string]$Json,
        [Parameter(Mandatory)]
        [string]$SchemaPath,
        [Parameter(Mandatory)]
        [string]$Description
    )

    $validationErrors = @()
    $isValid = Test-Json -Json $Json -SchemaFile $SchemaPath -ErrorAction SilentlyContinue -ErrorVariable validationErrors
    if (-not $isValid) {
        $details = ($validationErrors | ForEach-Object { $_.Exception.Message }) -join ' | '
        if ([string]::IsNullOrWhiteSpace($details)) {
            $details = 'schema validator returned false'
        }
        throw "$Description failed JSON Schema validation: $details"
    }
}

function Assert-PositiveFiniteNumber {
    param(
        [Parameter(Mandatory)]
        [object]$Value,
        [Parameter(Mandatory)]
        [string]$Name
    )

    $number = [double]$Value
    if (-not [double]::IsFinite($number) -or $number -le 0.0) {
        throw "$Name must be a finite positive number."
    }
}

function Assert-NonNegativeFiniteNumber {
    param(
        [Parameter(Mandatory)]
        [object]$Value,
        [Parameter(Mandatory)]
        [string]$Name
    )

    $number = [double]$Value
    if (-not [double]::IsFinite($number) -or $number -lt 0.0) {
        throw "$Name must be a finite non-negative number."
    }
}

function Assert-IsoTimestamp {
    param(
        [Parameter(Mandatory)]
        [string]$Value,
        [Parameter(Mandatory)]
        [string]$Name
    )

    $javascriptUtcPattern = '^\d{4}-(?:0[1-9]|1[0-2])-(?:0[1-9]|[12]\d|3[01])T(?:[01]\d|2[0-3]):[0-5]\d:[0-5]\d\.\d{3}Z$'
    $roundTripUtcPattern = '^\d{4}-(?:0[1-9]|1[0-2])-(?:0[1-9]|[12]\d|3[01])T(?:[01]\d|2[0-3]):[0-5]\d:[0-5]\d\.\d{7}\+00:00$'
    $parsed = [DateTimeOffset]::MinValue
    $culture = [System.Globalization.CultureInfo]::InvariantCulture
    if ($Value -cmatch $javascriptUtcPattern) {
        $styles = [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal
        $valid = [DateTimeOffset]::TryParseExact($Value, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", $culture, $styles, [ref]$parsed)
        $roundTrip = $parsed.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", $culture)
    }
    elseif ($Value -cmatch $roundTripUtcPattern) {
        $valid = [DateTimeOffset]::TryParseExact($Value, 'o', $culture, [System.Globalization.DateTimeStyles]::None, [ref]$parsed)
        $roundTrip = $parsed.ToString('o', $culture)
    }
    else {
        $valid = $false
        $roundTrip = ''
    }
    if (-not $valid -or $parsed.Offset -ne [TimeSpan]::Zero -or $roundTrip -cne $Value) {
        throw "$Name must be canonical RFC3339 UTC: JavaScript ISO (fffZ) or .NET round-trip UTC (fffffff+00:00)."
    }
    return $parsed
}

function Assert-TelemetryLedger {
    param(
        [Parameter(Mandatory)]
        [object]$Ledger,
        [string]$RawJson
    )

    if ([string]::IsNullOrWhiteSpace($RawJson)) {
        $RawJson = ConvertTo-CompactJson -Value $Ledger
    }
    Assert-JsonSchema -Json $RawJson -SchemaPath $telemetrySchemaPath -Description 'Gate 0 Studio telemetry ledger'

    if ($Ledger.schema -ne 'forge.gate0-studio-telemetry' -or $Ledger.schema_version -ne 1) {
        throw 'Telemetry ledger identity is not the Gate 0 Studio v1 contract.'
    }
    if ($Ledger.target -ne 'tauri_studio') {
        throw "Telemetry target must be tauri_studio, got '$($Ledger.target)'."
    }
    [void](Assert-IsoTimestamp -Value $Ledger.recorded_at_utc -Name 'recorded_at_utc')
    if ($Ledger.webgpu.status -ne 'ready' -or $Ledger.webgpu.diagnostic_injection -ne $false -or $null -ne $Ledger.webgpu.reason) {
        throw 'WebGPU must be ready without diagnostic injection or fallback reason.'
    }

    Assert-NonNegativeFiniteNumber -Value $Ledger.loading.wasm_started_ms -Name 'loading.wasm_started_ms'
    Assert-NonNegativeFiniteNumber -Value $Ledger.loading.webgpu_ready_ms -Name 'loading.webgpu_ready_ms'
    Assert-NonNegativeFiniteNumber -Value $Ledger.loading.scene_ready_ms -Name 'loading.scene_ready_ms'
    if ([int64]$Ledger.memory.wasm_linear_bytes -le 0) {
        throw 'memory.wasm_linear_bytes must be greater than zero.'
    }

    if ($Ledger.frame.warmup_samples -ne 60 -or $Ledger.frame.samples -ne 120) {
        throw 'Frame evidence must contain exactly 60 warm-up samples and 120 measured samples.'
    }
    foreach ($field in @('duration_ms', 'mean_ms', 'p50_ms', 'p95_ms', 'p99_ms', 'max_ms', 'mean_fps', 'one_percent_low_fps')) {
        Assert-PositiveFiniteNumber -Value $Ledger.frame.$field -Name "frame.$field"
    }

    if ($null -eq $Ledger.interaction.handler_to_post_paint) {
        throw 'An actual hierarchy interaction must be measured through the post-paint boundary.'
    }
    if ($Ledger.interaction.stable_canvas_observations -lt 1 -or $Ledger.interaction.unstable_canvas_observations -ne 0) {
        throw 'The measured interaction did not preserve one stable viewport instance.'
    }
    foreach ($field in @('mean_ms', 'p50_ms', 'p95_ms', 'p99_ms', 'max_ms')) {
        Assert-PositiveFiniteNumber -Value $Ledger.interaction.handler_to_post_paint.$field -Name "interaction.handler_to_post_paint.$field"
    }
    if ($Ledger.interaction.handler_to_post_paint.samples -lt 1) {
        throw 'The interaction latency distribution contains no measured sample.'
    }

    if ($Ledger.viewport.starts -ne 1 -or $Ledger.viewport.canvas_width_px -lt 1 -or $Ledger.viewport.canvas_height_px -lt 1) {
        throw 'Viewport evidence requires exactly one start and positive canvas dimensions.'
    }
    if ($Ledger.permissions.allowed.command -ne 'execute_studio_command' -or $Ledger.permissions.allowed.status -ne 'succeeded' -or [string]::IsNullOrWhiteSpace([string]$Ledger.permissions.allowed.reason)) {
        throw 'The typed Forge command was not positively proven through the Tauri ACL.'
    }
    if ($Ledger.permissions.denied.command -ne 'plugin:window|set_title' -or $Ledger.permissions.denied.status -ne 'explicitly_denied' -or [string]::IsNullOrWhiteSpace([string]$Ledger.permissions.denied.reason)) {
        throw 'The forbidden Tauri window command was not explicitly denied by the ACL.'
    }
}

function Assert-FileDescriptor {
    param(
        [Parameter(Mandatory)]
        [object]$Descriptor,
        [Parameter(Mandatory)]
        [string]$Description
    )

    $path = [System.IO.Path]::GetFullPath([string]$Descriptor.path)
    Assert-FileExists -LiteralPath $path -Description $Description
    $item = Get-Item -LiteralPath $path
    if ([int64]$Descriptor.bytes -ne $item.Length) {
        throw "$Description byte length does not match its ledger descriptor."
    }
    $actualHash = Get-LowercaseSha256 -LiteralPath $path
    if ([string]$Descriptor.sha256 -cne $actualHash) {
        throw "$Description SHA-256 does not match its ledger descriptor."
    }
}

function Assert-PathWithinDirectory {
    param(
        [Parameter(Mandatory)]
        [string]$LiteralPath,
        [Parameter(Mandatory)]
        [string]$Directory,
        [Parameter(Mandatory)]
        [string]$Description
    )

    $canonicalDirectory = [System.IO.Path]::GetFullPath($Directory).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    )
    $directoryPrefix = $canonicalDirectory + [System.IO.Path]::DirectorySeparatorChar
    $canonicalPath = [System.IO.Path]::GetFullPath($LiteralPath)
    if (-not $canonicalPath.StartsWith($directoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description path escapes the canonical output directory '$canonicalDirectory'."
    }
    return $canonicalPath
}

function Assert-CaptureLedger {
    param(
        [Parameter(Mandatory)]
        [object]$Ledger,
        [string]$RawJson,
        [string]$ExpectedExecutablePath,
        [string]$ExpectedOutputDirectory,
        [string]$ExpectedLedgerPath,
        [switch]$ValidateFiles
    )

    if ([string]::IsNullOrWhiteSpace($RawJson)) {
        $RawJson = ConvertTo-CompactJson -Value $Ledger
    }
    Assert-JsonSchema -Json $RawJson -SchemaPath $captureSchemaPath -Description 'Gate 0 Studio capture ledger'
    [void](Assert-IsoTimestamp -Value $Ledger.recorded_at_utc -Name 'capture.recorded_at_utc')

    if ($Ledger.git.branch -ne $expectedBranch -or $Ledger.git.source_tree_clean -ne $true) {
        throw 'Capture evidence must originate from a clean Rust branch.'
    }
    if ($Ledger.build.command -ne $buildCommandLabel -or $Ledger.build.succeeded -ne $true -or $Ledger.build.head_commit_sha -ne $Ledger.git.commit_sha -or $Ledger.build.head_tree_sha -ne $Ledger.git.tree_sha) {
        throw 'Capture build provenance does not match the recorded clean Git source.'
    }
    $buildStarted = Assert-IsoTimestamp -Value $Ledger.build.started_at_utc -Name 'build.started_at_utc'
    $buildCompleted = Assert-IsoTimestamp -Value $Ledger.build.completed_at_utc -Name 'build.completed_at_utc'
    if ($buildCompleted -lt $buildStarted) {
        throw 'Build completion precedes its start timestamp.'
    }
    $executableLastWrite = Assert-IsoTimestamp -Value $Ledger.executable.last_write_utc -Name 'executable.last_write_utc'
    if ($executableLastWrite.UtcDateTime -lt $buildStarted.UtcDateTime) {
        throw 'Executable last-write timestamp predates the controlled build.'
    }
    if ($Ledger.executable.file_name -ne 'forge-studio.exe' -or $Ledger.executable.built_after_build_started -ne $true -or $Ledger.executable.process_path_verified -ne $true) {
        throw 'Capture evidence is not linked to the controlled forge-studio.exe build.'
    }
    $ledgerExecutablePath = [System.IO.Path]::GetFullPath([string]$Ledger.executable.path)
    $launchedProcessPath = [System.IO.Path]::GetFullPath([string]$Ledger.executable.launched_process_path)
    if (-not $ledgerExecutablePath.Equals($launchedProcessPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Recorded process path differs from the hashed executable path.'
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedExecutablePath)) {
        $expectedPath = [System.IO.Path]::GetFullPath($ExpectedExecutablePath)
        if (-not $ledgerExecutablePath.Equals($expectedPath, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Capture ledger references a non-canonical executable path.'
        }
    }
    if ($Ledger.telemetry.schema_validated -ne $true -or $Ledger.telemetry.semantic_checks_passed -ne $true) {
        throw 'The capture references telemetry that was not strictly validated.'
    }
    if ($Ledger.window.title -ne $expectedWindowTitle -or $Ledger.window.foreground_requested -ne $true -or $Ledger.window.foreground_request_succeeded -ne $true -or $Ledger.window.foreground_verified -ne $true) {
        throw 'The exact proof window was not verifiably foregrounded.'
    }
    if ($Ledger.capture.sampled_unique_colors -lt 4 -or $Ledger.capture.non_blank -ne $true) {
        throw 'The acquired image failed the minimum non-blank pixel check.'
    }
    if ($Ledger.capture.width_px -ne $Ledger.window.width_px -or $Ledger.capture.height_px -ne $Ledger.window.height_px) {
        throw 'Capture pixel dimensions differ from the recorded window dimensions.'
    }
    if ($Ledger.review.status -ne 'uninspected' -or $Ledger.review.human_review_performed -ne $false -or $Ledger.review.human_review_required -ne $true) {
        throw 'Automated acquisition must not claim that human visual review occurred.'
    }
    if ($Ledger.qualification.acquisition_status -ne 'acquired' -or $Ledger.qualification.machine_checks_passed -ne $true -or $Ledger.qualification.proof_status -ne 'pending_human_review' -or $Ledger.qualification.qualified -ne $false) {
        throw 'Automated capture can be ACQUIRED but cannot be marked QUALIFIED.'
    }

    if (-not [string]::IsNullOrWhiteSpace($ExpectedOutputDirectory)) {
        $capturePath = Assert-PathWithinDirectory -LiteralPath $Ledger.capture.path -Directory $ExpectedOutputDirectory -Description 'Capture'
        $telemetryPath = Assert-PathWithinDirectory -LiteralPath $Ledger.telemetry.ledger.path -Directory $ExpectedOutputDirectory -Description 'Telemetry'
        $captureLedgerPath = Assert-PathWithinDirectory -LiteralPath $Ledger.ledger.path -Directory $ExpectedOutputDirectory -Description 'Capture ledger'
        if (-not [string]::IsNullOrWhiteSpace($ExpectedLedgerPath)) {
            $expectedCaptureLedgerPath = [System.IO.Path]::GetFullPath($ExpectedLedgerPath)
            if (-not $captureLedgerPath.Equals($expectedCaptureLedgerPath, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Capture ledger self-path differs from the file being validated.'
            }
        }
        if ($capturePath.Equals($telemetryPath, [StringComparison]::OrdinalIgnoreCase) -or $capturePath.Equals($captureLedgerPath, [StringComparison]::OrdinalIgnoreCase) -or $telemetryPath.Equals($captureLedgerPath, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Capture, telemetry, and capture-ledger paths must be distinct.'
        }
    }

    if ($ValidateFiles) {
        $ledgerConfigurationPath = [System.IO.Path]::GetFullPath([string]$Ledger.build.configuration.path)
        if (-not $ledgerConfigurationPath.Equals($proofConfigurationPath, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Capture ledger references a non-canonical Gate 0 proof configuration.'
        }
        Assert-FileDescriptor -Descriptor $Ledger.build.configuration -Description 'Gate 0 proof configuration'
        Assert-FileDescriptor -Descriptor $Ledger.executable -Description 'Studio executable'
        Assert-FileDescriptor -Descriptor $Ledger.telemetry.ledger -Description 'Studio telemetry ledger'
        Assert-FileDescriptor -Descriptor $Ledger.capture -Description 'Studio window capture'

        $linkedTelemetryJson = Get-Content -LiteralPath $Ledger.telemetry.ledger.path -Raw
        $linkedTelemetry = $linkedTelemetryJson | ConvertFrom-Json -Depth 24
        Assert-TelemetryLedger -Ledger $linkedTelemetry -RawJson $linkedTelemetryJson

        $linkedImage = [System.Drawing.Image]::FromFile([string]$Ledger.capture.path)
        try {
            if ($linkedImage.Width -ne $Ledger.capture.width_px -or $linkedImage.Height -ne $Ledger.capture.height_px) {
                throw 'Capture image dimensions do not match the capture ledger.'
            }
        }
        finally {
            $linkedImage.Dispose()
        }
    }
}

function Assert-ActionThrows {
    param(
        [Parameter(Mandatory)]
        [scriptblock]$Action,
        [Parameter(Mandatory)]
        [string]$Description
    )

    $didThrow = $false
    try {
        & $Action
    }
    catch {
        $didThrow = $true
    }
    if (-not $didThrow) {
        throw "Negative self-test was incorrectly accepted: $Description"
    }
}

function Invoke-SelfTest {
    $taskPipelineOutput = @(Wait-NonGenericTask -Task ([System.Threading.Tasks.Task]::CompletedTask))
    if ($taskPipelineOutput.Count -ne 0) {
        throw 'Non-generic Task completion leaked an object into the PowerShell pipeline.'
    }

    $telemetry = [ordered]@{
        schema = 'forge.gate0-studio-telemetry'
        schema_version = 1
        recorded_at_utc = '2026-08-30T10:00:00.0000000+00:00'
        target = 'tauri_studio'
        webgpu = [ordered]@{
            status = 'ready'
            diagnostic_injection = $false
            reason = $null
        }
        loading = [ordered]@{
            wasm_started_ms = 1.0
            webgpu_ready_ms = 2.0
            scene_ready_ms = 3.0
        }
        memory = [ordered]@{
            wasm_linear_bytes = 65536
            js_heap_used_bytes = $null
            js_heap_limit_bytes = $null
            js_heap_api_available = $false
        }
        frame = [ordered]@{
            warmup_samples = 60
            samples = 120
            duration_ms = 1920.0
            mean_ms = 16.0
            p50_ms = 16.0
            p95_ms = 17.0
            p99_ms = 18.0
            max_ms = 19.0
            mean_fps = 62.5
            one_percent_low_fps = 52.6
            hitches_over_16_7_ms = 2
            hitches_over_33_3_ms = 0
        }
        interaction = [ordered]@{
            handler_to_post_paint = [ordered]@{
                samples = 1
                mean_ms = 8.0
                p50_ms = 8.0
                p95_ms = 8.0
                p99_ms = 8.0
                max_ms = 8.0
            }
            stable_canvas_observations = 1
            unstable_canvas_observations = 0
        }
        viewport = [ordered]@{
            starts = 1
            canvas_width_px = 1280
            canvas_height_px = 720
        }
        permissions = [ordered]@{
            allowed = [ordered]@{
                command = 'execute_studio_command'
                status = 'succeeded'
                reason = 'typed command accepted'
            }
            denied = [ordered]@{
                command = 'plugin:window|set_title'
                status = 'explicitly_denied'
                reason = 'explicitly denied by ACL'
            }
        }
        limitations = @(
            'Frame time is measured in the WASM update loop.',
            'Human visual review remains required.'
        )
    }
    Assert-TelemetryLedger -Ledger $telemetry

    $interactionBaseline = [pscustomobject]@{
        StableCanvasObservations = 0
        UnstableCanvasObservations = 0
        LatencySamples = 0
    }
    if (-not (Test-NewStableInteraction -Ledger $telemetry -Baseline $interactionBaseline)) {
        throw 'Positive self-test did not recognize a new stable post-click observation.'
    }
    $noNewInteractionBaseline = [pscustomobject]@{
        StableCanvasObservations = 1
        UnstableCanvasObservations = 0
        LatencySamples = 1
    }
    if (Test-NewStableInteraction -Ledger $telemetry -Baseline $noNewInteractionBaseline) {
        throw 'Interaction counters without an increment were incorrectly accepted.'
    }
    $negativeInteraction = Copy-JsonValue -Value $telemetry
    $negativeInteraction.viewport.starts = 2
    Assert-ActionThrows -Description 'viewport restarted after scripted interaction' -Action {
        [void](Test-NewStableInteraction -Ledger $negativeInteraction -Baseline $interactionBaseline)
    }
    $negativeInteraction = Copy-JsonValue -Value $telemetry
    $negativeInteraction.interaction.unstable_canvas_observations = 1
    Assert-ActionThrows -Description 'unstable canvas observation after scripted interaction' -Action {
        [void](Test-NewStableInteraction -Ledger $negativeInteraction -Baseline $interactionBaseline)
    }

    $initialBrowserTelemetry = Copy-JsonValue -Value $telemetry
    $initialBrowserTelemetry.recorded_at_utc = $null
    $initialBrowserTelemetry.target = 'wasm_browser'
    $initialBrowserTelemetry.webgpu.status = 'checking'
    $initialBrowserTelemetry.webgpu.reason = $null
    $initialBrowserTelemetry.loading.webgpu_ready_ms = $null
    $initialBrowserTelemetry.loading.scene_ready_ms = $null
    $initialBrowserTelemetry.memory.wasm_linear_bytes = $null
    $initialBrowserTelemetry.frame = $null
    $initialBrowserTelemetry.interaction.handler_to_post_paint = $null
    $initialBrowserTelemetry.interaction.stable_canvas_observations = 0
    $initialBrowserTelemetry.viewport.starts = 0
    $initialBrowserTelemetry.viewport.canvas_width_px = $null
    $initialBrowserTelemetry.viewport.canvas_height_px = $null
    $initialBrowserTelemetry.permissions.allowed.status = 'not_run'
    $initialBrowserTelemetry.permissions.allowed.reason = $null
    $initialBrowserTelemetry.permissions.denied.status = 'not_run'
    $initialBrowserTelemetry.permissions.denied.reason = $null
    $initialBrowserJson = ConvertTo-CompactJson -Value $initialBrowserTelemetry
    Assert-JsonSchema -Json $initialBrowserJson -SchemaPath $telemetrySchemaPath -Description 'Initial browser telemetry'
    Assert-ActionThrows -Description 'initial browser state used as Tauri qualification' -Action {
        Assert-TelemetryLedger -Ledger $initialBrowserTelemetry -RawJson $initialBrowserJson
    }

    $injectedFailureTelemetry = Copy-JsonValue -Value $initialBrowserTelemetry
    $injectedFailureTelemetry.recorded_at_utc = '2026-08-30T10:00:01.0000000+00:00'
    $injectedFailureTelemetry.webgpu.status = 'unavailable'
    $injectedFailureTelemetry.webgpu.diagnostic_injection = $true
    $injectedFailureTelemetry.webgpu.reason = 'injected negative-path proof'
    $injectedFailureJson = ConvertTo-CompactJson -Value $injectedFailureTelemetry
    Assert-JsonSchema -Json $injectedFailureJson -SchemaPath $telemetrySchemaPath -Description 'Injected negative telemetry'
    Assert-ActionThrows -Description 'negative injection used as Tauri qualification' -Action {
        Assert-TelemetryLedger -Ledger $injectedFailureTelemetry -RawJson $injectedFailureJson
    }

    $unknownFieldTelemetry = Copy-JsonValue -Value $initialBrowserTelemetry
    $unknownFieldTelemetry | Add-Member -NotePropertyName unexpected -NotePropertyValue $true
    Assert-ActionThrows -Description 'unknown telemetry property' -Action {
        $unknownFieldJson = ConvertTo-CompactJson -Value $unknownFieldTelemetry
        Assert-JsonSchema -Json $unknownFieldJson -SchemaPath $telemetrySchemaPath -Description 'Unknown-field telemetry'
    }

    $negativeTelemetry = Copy-JsonValue -Value $telemetry
    $negativeTelemetry.permissions.denied.status = 'unexpectedly_allowed'
    Assert-ActionThrows -Description 'denied command unexpectedly allowed' -Action {
        Assert-TelemetryLedger -Ledger $negativeTelemetry
    }

    $negativeTelemetry = Copy-JsonValue -Value $telemetry
    $negativeTelemetry.viewport.starts = 2
    Assert-ActionThrows -Description 'viewport started twice' -Action {
        Assert-TelemetryLedger -Ledger $negativeTelemetry
    }

    $negativeTelemetry = Copy-JsonValue -Value $telemetry
    $negativeTelemetry.frame.samples = 119
    Assert-ActionThrows -Description '119 measured frame samples' -Action {
        Assert-TelemetryLedger -Ledger $negativeTelemetry
    }

    $negativeTelemetry = Copy-JsonValue -Value $telemetry
    $negativeTelemetry.frame.warmup_samples = 59
    Assert-ActionThrows -Description '59 warm-up frame samples' -Action {
        Assert-TelemetryLedger -Ledger $negativeTelemetry
    }

    $negativeTelemetry = Copy-JsonValue -Value $telemetry
    $negativeTelemetry.permissions.allowed.status = 'failed'
    Assert-ActionThrows -Description 'typed Forge command failed' -Action {
        Assert-TelemetryLedger -Ledger $negativeTelemetry
    }

    $negativeTelemetry = Copy-JsonValue -Value $telemetry
    $negativeTelemetry.interaction.handler_to_post_paint = $null
    Assert-ActionThrows -Description 'missing interaction latency' -Action {
        Assert-TelemetryLedger -Ledger $negativeTelemetry
    }

    $negativeTelemetry = Copy-JsonValue -Value $telemetry
    $negativeTelemetry.recorded_at_utc = '2026-08-30T12:00:00.000+02:00'
    Assert-ActionThrows -Description 'non-UTC telemetry timestamp' -Action {
        Assert-TelemetryLedger -Ledger $negativeTelemetry
    }

    $cdpPort = 43123
    $cdpTarget = [pscustomobject]@{
        type = 'page'
        title = $expectedCdpTitle
        url = $expectedCdpUrl
        webSocketDebuggerUrl = "ws://127.0.0.1:$cdpPort/devtools/page/gate0"
    }
    Assert-CdpTarget -Target $cdpTarget -ExpectedPort $cdpPort

    $negativeCdpTarget = Copy-JsonValue -Value $cdpTarget
    $negativeCdpTarget.url = "$expectedCdpUrl&extra=true"
    Assert-ActionThrows -Description 'non-exact CDP page query' -Action {
        Assert-CdpTarget -Target $negativeCdpTarget -ExpectedPort $cdpPort
    }
    $negativeCdpTarget = Copy-JsonValue -Value $cdpTarget
    $negativeCdpTarget.title = $expectedWindowTitle
    Assert-ActionThrows -Description 'non-exact CDP title' -Action {
        Assert-CdpTarget -Target $negativeCdpTarget -ExpectedPort $cdpPort
    }
    $negativeCdpTarget = Copy-JsonValue -Value $cdpTarget
    $negativeCdpTarget.webSocketDebuggerUrl = "wss://127.0.0.1:$cdpPort/devtools/page/gate0"
    Assert-ActionThrows -Description 'non-ws CDP endpoint' -Action {
        Assert-CdpTarget -Target $negativeCdpTarget -ExpectedPort $cdpPort
    }
    $negativeCdpTarget = Copy-JsonValue -Value $cdpTarget
    $negativeCdpTarget.webSocketDebuggerUrl = "ws://192.0.2.1:$cdpPort/devtools/page/gate0"
    Assert-ActionThrows -Description 'non-loopback CDP host' -Action {
        Assert-CdpTarget -Target $negativeCdpTarget -ExpectedPort $cdpPort
    }
    $negativeCdpTarget = Copy-JsonValue -Value $cdpTarget
    $wrongCdpPort = $cdpPort + 1
    $negativeCdpTarget.webSocketDebuggerUrl = "ws://127.0.0.1:$wrongCdpPort/devtools/page/gate0"
    Assert-ActionThrows -Description 'wrong CDP port' -Action {
        Assert-CdpTarget -Target $negativeCdpTarget -ExpectedPort $cdpPort
    }

    $capture = [ordered]@{
        schema = 'forge.gate0-studio-window-capture'
        schema_version = 1
        recorded_at_utc = '2026-08-30T10:01:00.0000000+00:00'
        ledger = [ordered]@{
            path = 'E:\proof\artifacts\capture-ledger.json'
        }
        git = [ordered]@{
            branch = 'Rust'
            commit_sha = ('a' * 40)
            tree_sha = ('b' * 40)
            source_tree_clean = $true
        }
        build = [ordered]@{
            command = $buildCommandLabel
            started_at_utc = '2026-08-30T10:00:00.0000000+00:00'
            completed_at_utc = '2026-08-30T10:00:30.0000000+00:00'
            head_commit_sha = ('a' * 40)
            head_tree_sha = ('b' * 40)
            configuration = [ordered]@{
                path = 'E:\proof\tauri.gate0-proof.conf.json'
                bytes = 100
                sha256 = ('c' * 64)
            }
            succeeded = $true
        }
        executable = [ordered]@{
            path = 'E:\proof\target\release\forge-studio.exe'
            file_name = 'forge-studio.exe'
            launched_process_path = 'E:\proof\target\release\forge-studio.exe'
            process_path_verified = $true
            bytes = 1024
            sha256 = ('d' * 64)
            last_write_utc = '2026-08-30T10:00:20.0000000+00:00'
            built_after_build_started = $true
        }
        telemetry = [ordered]@{
            ledger = [ordered]@{
                path = 'E:\proof\artifacts\telemetry.json'
                bytes = 1024
                sha256 = ('e' * 64)
            }
            schema_validated = $true
            semantic_checks_passed = $true
        }
        window = [ordered]@{
            process_id = 1234
            title = $expectedWindowTitle
            foreground_requested = $true
            foreground_request_succeeded = $true
            foreground_verified = $true
            left_px = 100
            top_px = 100
            width_px = 1600
            height_px = 900
        }
        capture = [ordered]@{
            path = 'E:\proof\artifacts\capture.png'
            bytes = 4096
            sha256 = ('f' * 64)
            width_px = 1600
            height_px = 900
            sampled_unique_colors = 4
            non_blank = $true
            method = 'foreground_copy_from_screen'
        }
        review = [ordered]@{
            status = 'uninspected'
            method = 'human_visual_review_required'
            human_review_performed = $false
            human_review_required = $true
        }
        qualification = [ordered]@{
            acquisition_status = 'acquired'
            machine_checks_passed = $true
            proof_status = 'pending_human_review'
            qualified = $false
        }
        limitations = @(
            'A human must inspect composition and readability.',
            'Screen capture can include desktop occlusion.'
        )
    }
    $selfTestExecutablePath = [string]$capture.executable.path
    $selfTestOutputDirectory = 'E:\proof\artifacts'
    $selfTestLedgerPath = [string]$capture.ledger.path
    Assert-CaptureLedger -Ledger $capture -ExpectedExecutablePath $selfTestExecutablePath -ExpectedOutputDirectory $selfTestOutputDirectory -ExpectedLedgerPath $selfTestLedgerPath

    $negativeCapture = Copy-JsonValue -Value $capture
    $negativeCapture.capture.non_blank = $false
    Assert-ActionThrows -Description 'blank image' -Action {
        Assert-CaptureLedger -Ledger $negativeCapture -ExpectedExecutablePath $selfTestExecutablePath
    }

    $negativeCapture = Copy-JsonValue -Value $capture
    $negativeCapture.capture.sampled_unique_colors = 3
    Assert-ActionThrows -Description 'only three sampled colors' -Action {
        Assert-CaptureLedger -Ledger $negativeCapture -ExpectedExecutablePath $selfTestExecutablePath
    }

    $negativeCapture = Copy-JsonValue -Value $capture
    $negativeCapture.git.source_tree_clean = $false
    Assert-ActionThrows -Description 'dirty source tree' -Action {
        Assert-CaptureLedger -Ledger $negativeCapture -ExpectedExecutablePath $selfTestExecutablePath
    }

    $negativeCapture = Copy-JsonValue -Value $capture
    $negativeCapture.git.branch = 'main'
    Assert-ActionThrows -Description 'non-Rust source branch' -Action {
        Assert-CaptureLedger -Ledger $negativeCapture -ExpectedExecutablePath $selfTestExecutablePath
    }

    $negativeCapture = Copy-JsonValue -Value $capture
    $negativeCapture.window.title = 'OpenRPG Forge Studio'
    Assert-ActionThrows -Description 'wrong window title' -Action {
        Assert-CaptureLedger -Ledger $negativeCapture -ExpectedExecutablePath $selfTestExecutablePath
    }

    $negativeCapture = Copy-JsonValue -Value $capture
    $negativeCapture.window.foreground_verified = $false
    Assert-ActionThrows -Description 'unverified foreground focus' -Action {
        Assert-CaptureLedger -Ledger $negativeCapture -ExpectedExecutablePath $selfTestExecutablePath
    }

    $negativeCapture = Copy-JsonValue -Value $capture
    $negativeCapture.qualification.qualified = $true
    Assert-ActionThrows -Description 'automatic human qualification claim' -Action {
        Assert-CaptureLedger -Ledger $negativeCapture -ExpectedExecutablePath $selfTestExecutablePath
    }

    $negativeCapture = Copy-JsonValue -Value $capture
    $negativeCapture.review.human_review_performed = $true
    Assert-ActionThrows -Description 'automatic human review claim' -Action {
        Assert-CaptureLedger -Ledger $negativeCapture -ExpectedExecutablePath $selfTestExecutablePath
    }

    $negativeCapture = Copy-JsonValue -Value $capture
    $negativeCapture.executable.path = 'E:\proof\copied\forge-studio.exe'
    Assert-ActionThrows -Description 'arbitrary executable path' -Action {
        Assert-CaptureLedger -Ledger $negativeCapture -ExpectedExecutablePath $selfTestExecutablePath
    }

    $negativeCapture = Copy-JsonValue -Value $capture
    $negativeCapture.executable.last_write_utc = '2026-08-30T09:59:00.0000000+00:00'
    Assert-ActionThrows -Description 'stale executable timestamp' -Action {
        Assert-CaptureLedger -Ledger $negativeCapture -ExpectedExecutablePath $selfTestExecutablePath
    }

    $negativeCapture = Copy-JsonValue -Value $capture
    $negativeCapture.recorded_at_utc = '2026-08-30T12:01:00.0000000+02:00'
    Assert-ActionThrows -Description 'non-UTC capture timestamp' -Action {
        Assert-CaptureLedger -Ledger $negativeCapture
    }

    $negativeCapture = Copy-JsonValue -Value $capture
    $negativeCapture.capture.width_px = 1599
    Assert-ActionThrows -Description 'capture and window dimension mismatch' -Action {
        Assert-CaptureLedger -Ledger $negativeCapture
    }

    foreach ($escapedPath in @(
        @{ Field = 'capture'; Path = 'E:\proof\outside\capture.png' },
        @{ Field = 'telemetry'; Path = 'E:\proof\outside\telemetry.json' },
        @{ Field = 'ledger'; Path = 'E:\proof\outside\capture-ledger.json' }
    )) {
        $negativeCapture = Copy-JsonValue -Value $capture
        switch ($escapedPath.Field) {
            'capture' { $negativeCapture.capture.path = $escapedPath.Path }
            'telemetry' { $negativeCapture.telemetry.ledger.path = $escapedPath.Path }
            'ledger' { $negativeCapture.ledger.path = $escapedPath.Path }
        }
        Assert-ActionThrows -Description "$($escapedPath.Field) path escaping output directory" -Action {
            Assert-CaptureLedger -Ledger $negativeCapture -ExpectedOutputDirectory $selfTestOutputDirectory -ExpectedLedgerPath $selfTestLedgerPath
        }
    }

    # A porcelain/stat-cache `.M` is intentionally not an input: canonical Git
    # content equality is sufficient when both diffs and the untracked set are clean.
    $statOnlyCleanliness = New-GitCleanlinessResult `
        -IndexMatchesHead $true `
        -WorktreeMatchesIndex $true `
        -UntrackedOutput ''
    if (-not $statOnlyCleanliness.IsClean -or -not [string]::IsNullOrWhiteSpace($statOnlyCleanliness.Status)) {
        throw 'Content-clean Git self-test was rejected as stat-dirty.'
    }

    $stagedDirtyCleanliness = New-GitCleanlinessResult `
        -IndexMatchesHead $false `
        -WorktreeMatchesIndex $true `
        -UntrackedOutput ''
    if ($stagedDirtyCleanliness.IsClean -or $stagedDirtyCleanliness.Status -notmatch 'staged in the index') {
        throw 'Staged-content Git self-test was incorrectly accepted.'
    }

    $worktreeDirtyCleanliness = New-GitCleanlinessResult `
        -IndexMatchesHead $true `
        -WorktreeMatchesIndex $false `
        -UntrackedOutput ''
    if ($worktreeDirtyCleanliness.IsClean -or $worktreeDirtyCleanliness.Status -notmatch 'worktree content') {
        throw 'Tracked-worktree Git self-test was incorrectly accepted.'
    }

    $untrackedDirtyCleanliness = New-GitCleanlinessResult `
        -IndexMatchesHead $true `
        -WorktreeMatchesIndex $true `
        -UntrackedOutput "first.tmp$([Environment]::NewLine)nested/second.tmp"
    if ($untrackedDirtyCleanliness.IsClean -or $untrackedDirtyCleanliness.Status -notmatch 'untracked: first\.tmp' -or $untrackedDirtyCleanliness.Status -notmatch 'untracked: nested/second\.tmp') {
        throw 'Untracked-content Git self-test was incorrectly accepted.'
    }

    Write-Output 'FORGE_STUDIO_WINDOW_PROOF_SELF_TEST_OK'
}

function Invoke-Git {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $output = @(& git --no-optional-locks -C $repositoryRoot @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        $details = $output -join [Environment]::NewLine
        throw "Git command failed: git $($Arguments -join ' ')$([Environment]::NewLine)$details"
    }
    return (($output -join [Environment]::NewLine).Trim())
}

function Test-GitContentDiff {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $output = @(& git --no-optional-locks -C $repositoryRoot @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 0) {
        return $true
    }
    if ($exitCode -eq 1) {
        return $false
    }

    $details = $output -join [Environment]::NewLine
    throw "Git content comparison failed: git $($Arguments -join ' ')$([Environment]::NewLine)$details"
}

function New-GitCleanlinessResult {
    param(
        [Parameter(Mandatory)]
        [bool]$IndexMatchesHead,
        [Parameter(Mandatory)]
        [bool]$WorktreeMatchesIndex,
        [AllowEmptyString()]
        [string]$UntrackedOutput
    )

    $status = [System.Collections.Generic.List[string]]::new()
    if (-not $IndexMatchesHead) {
        $status.Add('tracked content staged in the index differs from HEAD')
    }
    if (-not $WorktreeMatchesIndex) {
        $status.Add('tracked worktree content differs from the index')
    }
    foreach ($untrackedPath in @($UntrackedOutput -split '[\r\n]+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        $status.Add("untracked: $untrackedPath")
    }

    return [pscustomobject]@{
        IsClean = $status.Count -eq 0
        Status = $status -join [Environment]::NewLine
    }
}

function Get-GitSnapshot {
    # `git status` can report a stat-only `.M` on Windows when Tauri rewrites a
    # CRLF checkout as byte-identical canonical LF. Compare Git content instead
    # and keep the proof read-only by disabling optional index locks.
    $indexMatchesHead = Test-GitContentDiff -Arguments @(
        'diff', '--cached', '--quiet', '--exit-code', '--no-ext-diff',
        '--ignore-submodules=none', 'HEAD', '--'
    )
    $worktreeMatchesIndex = Test-GitContentDiff -Arguments @(
        'diff', '--quiet', '--exit-code', '--no-ext-diff',
        '--ignore-submodules=none', '--'
    )
    $untrackedOutput = Invoke-Git -Arguments @(
        'ls-files', '--others', '--exclude-standard', '--directory',
        '--no-empty-directory'
    )
    $cleanliness = New-GitCleanlinessResult `
        -IndexMatchesHead $indexMatchesHead `
        -WorktreeMatchesIndex $worktreeMatchesIndex `
        -UntrackedOutput $untrackedOutput
    return [pscustomobject]@{
        Branch = Invoke-Git -Arguments @('branch', '--show-current')
        CommitSha = Invoke-Git -Arguments @('rev-parse', 'HEAD')
        TreeSha = Invoke-Git -Arguments @('rev-parse', 'HEAD^{tree}')
        IsClean = $cleanliness.IsClean
        Status = $cleanliness.Status
    }
}

function Assert-GitSnapshot {
    param(
        [Parameter(Mandatory)]
        [object]$Snapshot,
        [Parameter(Mandatory)]
        [string]$Description
    )

    if ($Snapshot.Branch -cne $expectedBranch) {
        throw "$Description is on branch '$($Snapshot.Branch)', expected '$expectedBranch'."
    }
    if ($Snapshot.CommitSha -cnotmatch '^[0-9a-f]{40}$' -or $Snapshot.TreeSha -cnotmatch '^[0-9a-f]{40}$') {
        throw "$Description does not resolve to canonical 40-character Git object IDs."
    }
    if (-not $Snapshot.IsClean) {
        throw "$Description is not clean. Commit or remove source changes before acquisition:$([Environment]::NewLine)$($Snapshot.Status)"
    }
}

function Get-LowercaseSha256 {
    param(
        [Parameter(Mandatory)]
        [string]$LiteralPath
    )

    return (Get-FileHash -LiteralPath $LiteralPath -Algorithm SHA256).Hash.ToLowerInvariant()
}

function New-LoopbackPortReservation {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    return [pscustomobject]@{
        Listener = $listener
        Port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
}

function Assert-CdpTarget {
    param(
        [Parameter(Mandatory)]
        [object]$Target,
        [Parameter(Mandatory)]
        [int]$ExpectedPort
    )

    if ($Target.type -cne 'page') {
        throw "CDP target type must be exactly 'page'."
    }
    if ([string]$Target.title -cne $expectedCdpTitle) {
        throw "CDP target title '$($Target.title)' does not match '$expectedCdpTitle'."
    }
    if ([string]$Target.url -cne $expectedCdpUrl) {
        throw "CDP target URL '$($Target.url)' does not match '$expectedCdpUrl'."
    }
    $pageUri = $null
    if (-not [Uri]::TryCreate([string]$Target.url, [UriKind]::Absolute, [ref]$pageUri)) {
        throw 'CDP target page URL is not an absolute URI.'
    }
    if ($pageUri.Scheme -cne 'http' -or $pageUri.Host -cne 'tauri.localhost' -or $pageUri.AbsolutePath -cne '/index.html' -or $pageUri.Query -cne $expectedCdpQuery -or -not [string]::IsNullOrEmpty($pageUri.Fragment)) {
        throw 'CDP target page URI components do not match the exact Gate 0 proof route.'
    }

    $webSocketUri = $null
    if (-not [Uri]::TryCreate([string]$Target.webSocketDebuggerUrl, [UriKind]::Absolute, [ref]$webSocketUri)) {
        throw 'CDP webSocketDebuggerUrl is not an absolute URI.'
    }
    if ($webSocketUri.Scheme -cne 'ws') {
        throw "CDP debugger scheme must be ws, got '$($webSocketUri.Scheme)'."
    }
    if ($webSocketUri.Host -notin @('127.0.0.1', 'localhost')) {
        throw "CDP debugger host '$($webSocketUri.Host)' is not loopback."
    }
    if ($webSocketUri.Port -ne $ExpectedPort) {
        throw "CDP debugger port $($webSocketUri.Port) differs from reserved port $ExpectedPort."
    }
    if (-not $webSocketUri.AbsolutePath.StartsWith('/devtools/page/', [StringComparison]::Ordinal)) {
        throw 'CDP debugger path is not a page target endpoint.'
    }
    if (-not [string]::IsNullOrEmpty($webSocketUri.Query) -or -not [string]::IsNullOrEmpty($webSocketUri.Fragment) -or -not [string]::IsNullOrEmpty($webSocketUri.UserInfo)) {
        throw 'CDP debugger URI contains unexpected query, fragment, or user information.'
    }
}

function Wait-CdpTarget {
    param(
        [Parameter(Mandatory)]
        [int]$Port,
        [Parameter(Mandatory)]
        [int]$TimeoutSeconds,
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process
    )

    $endpoint = "http://127.0.0.1:$Port/json/list"
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastError = $null
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if ($Process.HasExited) {
            throw "Studio exited before opening WebView2 DevTools (exit code $($Process.ExitCode))."
        }
        try {
            $targets = @(Invoke-RestMethod -Uri $endpoint -TimeoutSec 2 -NoProxy)
            $matchingTargets = @($targets | Where-Object {
                [string]$_.url -ceq $expectedCdpUrl -or
                [string]$_.title -ceq $expectedCdpTitle
            })
            foreach ($target in $matchingTargets) {
                try {
                    Assert-CdpTarget -Target $target -ExpectedPort $Port
                    return $target
                }
                catch {
                    $lastError = $_.Exception.Message
                }
            }
            if ($matchingTargets.Count -eq 0) {
                $lastError = "DevTools endpoint opened but exposed no exact Gate 0 proof URL/title at $endpoint."
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 200
    }
    throw "WebView2 DevTools did not expose the Gate 0 proof target on ephemeral loopback port $Port. No capture was accepted. Last error: $lastError"
}

function Connect-CdpSocket {
    param(
        [Parameter(Mandatory)]
        [string]$WebSocketUrl
    )

    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    $timeout = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(10))
    try {
        Wait-NonGenericTask -Task ($socket.ConnectAsync([Uri]$WebSocketUrl, $timeout.Token))
    }
    catch {
        $socket.Dispose()
        throw "Unable to connect to the local WebView2 DevTools target: $($_.Exception.Message)"
    }
    finally {
        $timeout.Dispose()
    }
    return $socket
}

function Invoke-CdpExpression {
    param(
        [Parameter(Mandatory)]
        [System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)]
        [string]$Expression
    )

    $script:CdpMessageId += 1
    $messageId = $script:CdpMessageId
    $request = [ordered]@{
        id = $messageId
        method = 'Runtime.evaluate'
        params = [ordered]@{
            expression = $Expression
            returnByValue = $true
            awaitPromise = $true
        }
    }
    $payload = [System.Text.Encoding]::UTF8.GetBytes((ConvertTo-CompactJson -Value $request))
    $timeout = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(10))
    try {
        $sendSegment = [ArraySegment[byte]]::new($payload)
        Wait-NonGenericTask -Task ($Socket.SendAsync($sendSegment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $timeout.Token))

        while ($true) {
            $stream = [System.IO.MemoryStream]::new()
            try {
                do {
                    $buffer = [byte[]]::new(65536)
                    $receiveSegment = [ArraySegment[byte]]::new($buffer)
                    $result = $Socket.ReceiveAsync($receiveSegment, $timeout.Token).GetAwaiter().GetResult()
                    if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
                        throw 'WebView2 DevTools closed the CDP connection.'
                    }
                    if ($result.Count -gt 0) {
                        $stream.Write($buffer, 0, $result.Count)
                    }
                } while (-not $result.EndOfMessage)
                $responseText = [System.Text.Encoding]::UTF8.GetString($stream.ToArray())
            }
            finally {
                $stream.Dispose()
            }

            $response = $responseText | ConvertFrom-Json -Depth 24
            if ($response.PSObject.Properties.Name -contains 'id' -and [int]$response.id -eq $messageId) {
                if ($response.PSObject.Properties.Name -contains 'error') {
                    throw "CDP Runtime.evaluate failed: $($response.error.message)"
                }
                if ($response.result.PSObject.Properties.Name -contains 'exceptionDetails') {
                    throw "CDP expression raised an exception: $($response.result.exceptionDetails.text)"
                }
                return $response.result.result.value
            }
        }
    }
    finally {
        $timeout.Dispose()
    }
}

function Get-TelemetryTerminalFailure {
    param(
        [Parameter(Mandatory)]
        [object]$Ledger
    )

    if ($Ledger.target -and $Ledger.target -ne 'tauri_studio') {
        return "unexpected telemetry target '$($Ledger.target)'"
    }
    if ($Ledger.webgpu.diagnostic_injection -eq $true) {
        return 'diagnostic WebGPU injection is active'
    }
    if ($Ledger.webgpu.status -eq 'unavailable') {
        return "WebGPU unavailable: $($Ledger.webgpu.reason)"
    }
    if ($Ledger.permissions.allowed.status -eq 'failed') {
        return "typed command failed: $($Ledger.permissions.allowed.reason)"
    }
    if ($Ledger.permissions.denied.status -in @('browser_backend_absent', 'unexpectedly_allowed', 'probe_failed')) {
        return "ACL denial probe ended as '$($Ledger.permissions.denied.status)': $($Ledger.permissions.denied.reason)"
    }
    if ($null -ne $Ledger.frame -and ($Ledger.frame.warmup_samples -ne 60 -or $Ledger.frame.samples -ne 120)) {
        return "frame collector reported warmup=$($Ledger.frame.warmup_samples), samples=$($Ledger.frame.samples)"
    }
    if ($Ledger.viewport.starts -gt 1) {
        return "viewport started $($Ledger.viewport.starts) times"
    }
    if ($Ledger.interaction.unstable_canvas_observations -gt 0) {
        return 'the measured interaction replaced or destabilized the canvas'
    }
    return $null
}

function Test-TelemetryReady {
    param(
        [Parameter(Mandatory)]
        [object]$Ledger
    )

    return (
        -not [string]::IsNullOrWhiteSpace([string]$Ledger.recorded_at_utc) -and
        $Ledger.webgpu.status -eq 'ready' -and
        $null -ne $Ledger.frame -and
        $null -ne $Ledger.interaction.handler_to_post_paint -and
        $Ledger.viewport.starts -eq 1 -and
        $null -ne $Ledger.viewport.canvas_width_px -and
        $null -ne $Ledger.viewport.canvas_height_px -and
        $Ledger.permissions.allowed.status -eq 'succeeded' -and
        $Ledger.permissions.denied.status -eq 'explicitly_denied'
    )
}

function Test-NewStableInteraction {
    param(
        [Parameter(Mandatory)]
        [object]$Ledger,
        [Parameter(Mandatory)]
        [object]$Baseline
    )

    if ($Ledger.viewport.starts -ne 1) {
        throw "Viewport start count changed after interaction: $($Ledger.viewport.starts)."
    }
    if ($Ledger.interaction.unstable_canvas_observations -ne $Baseline.UnstableCanvasObservations) {
        throw 'Unstable canvas observations changed after the scripted interaction.'
    }
    if ($Ledger.interaction.stable_canvas_observations -lt $Baseline.StableCanvasObservations) {
        throw 'Stable canvas observation counter regressed after interaction.'
    }
    $latencySamples = 0
    if ($null -ne $Ledger.interaction.handler_to_post_paint) {
        $latencySamples = [int64]$Ledger.interaction.handler_to_post_paint.samples
    }
    if ($latencySamples -lt $Baseline.LatencySamples) {
        throw 'Interaction latency sample counter regressed after interaction.'
    }
    return (
        $Ledger.interaction.stable_canvas_observations -ge
        ($Baseline.StableCanvasObservations + 1) -and
        $latencySamples -ge ($Baseline.LatencySamples + 1)
    )
}

function Get-QualifiedTelemetryFromCdp {
    param(
        [Parameter(Mandatory)]
        [System.Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory)]
        [int]$TimeoutSeconds
    )

    $preInteractionDeadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $baseline = $null
    $lastPreInteractionState = 'ledger element not populated'
    while ([DateTimeOffset]::UtcNow -lt $preInteractionDeadline) {
        $interactionAttemptJson = Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const ledgerNode = document.getElementById("gate0-telemetry-ledger");
  const canvas = document.getElementById("forge-canvas");
  const buttons = document.querySelectorAll(".hierarchy button");
  const ledgerText = ledgerNode?.textContent ?? "";
  if (!ledgerText) {
    return JSON.stringify({ status: "not_ready", reason: "ledger_missing", ledger: "" });
  }
  let ledger;
  try {
    ledger = JSON.parse(ledgerText);
  } catch {
    return JSON.stringify({ status: "ledger_invalid", ledger: ledgerText });
  }
  if (
    ledger.webgpu?.status !== "ready" ||
    ledger.viewport?.starts !== 1 ||
    !canvas ||
    buttons.length < 2
  ) {
    return JSON.stringify({
      status: "not_ready",
      reason: "webgpu_viewport_or_dom_not_ready",
      ledger: ledgerText
    });
  }
  buttons[1].click();
  return JSON.stringify({ status: "clicked", ledger: ledgerText });
})()
'@
        try {
            $interactionAttempt = ([string]$interactionAttemptJson) | ConvertFrom-Json -Depth 24
        }
        catch {
            throw "CDP interaction precondition response is not valid JSON: $($_.Exception.Message)"
        }
        if ($interactionAttempt.status -eq 'ledger_invalid') {
            throw 'The hidden telemetry ledger was invalid before interaction.'
        }
        if (-not [string]::IsNullOrWhiteSpace([string]$interactionAttempt.ledger)) {
            try {
                $preInteractionLedger = ([string]$interactionAttempt.ledger) | ConvertFrom-Json -Depth 24
            }
            catch {
                throw "The hidden telemetry ledger is not valid JSON before interaction: $($_.Exception.Message)"
            }
            $terminalFailure = Get-TelemetryTerminalFailure -Ledger $preInteractionLedger
            if (-not [string]::IsNullOrWhiteSpace([string]$terminalFailure)) {
                throw "Gate 0 telemetry reached a terminal failure before interaction: $terminalFailure"
            }
            $lastPreInteractionState = "webgpu=$($preInteractionLedger.webgpu.status), viewport_starts=$($preInteractionLedger.viewport.starts), canvas=$($null -ne $preInteractionLedger.viewport.canvas_width_px)"
        }
        if ($interactionAttempt.status -eq 'clicked') {
            Assert-JsonSchema -Json ([string]$interactionAttempt.ledger) -SchemaPath $telemetrySchemaPath -Description 'Pre-interaction telemetry ledger'
            if ($preInteractionLedger.webgpu.status -ne 'ready' -or $preInteractionLedger.viewport.starts -ne 1) {
                throw 'Interaction was attempted before WebGPU and the single viewport start were ready.'
            }
            $baselineLatencySamples = 0
            if ($null -ne $preInteractionLedger.interaction.handler_to_post_paint) {
                $baselineLatencySamples = [int64]$preInteractionLedger.interaction.handler_to_post_paint.samples
            }
            $baseline = [pscustomobject]@{
                StableCanvasObservations = [int64]$preInteractionLedger.interaction.stable_canvas_observations
                UnstableCanvasObservations = [int64]$preInteractionLedger.interaction.unstable_canvas_observations
                LatencySamples = $baselineLatencySamples
            }
            break
        }
        Start-Sleep -Milliseconds 100
    }
    if ($null -eq $baseline) {
        throw "Timed out before WebGPU, the single viewport start, and the proof UI were ready. Last state: $lastPreInteractionState"
    }

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastState = 'no post-interaction ledger observation'
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $rawJson = Invoke-CdpExpression -Socket $Socket -Expression @'
document.getElementById("gate0-telemetry-ledger")?.textContent ?? ""
'@
        if (-not [string]::IsNullOrWhiteSpace([string]$rawJson)) {
            try {
                $ledger = ([string]$rawJson) | ConvertFrom-Json -Depth 24
            }
            catch {
                throw "The hidden telemetry ledger is not valid JSON: $($_.Exception.Message)"
            }
            $terminalFailure = Get-TelemetryTerminalFailure -Ledger $ledger
            if (-not [string]::IsNullOrWhiteSpace([string]$terminalFailure)) {
                throw "Gate 0 telemetry reached a terminal failure: $terminalFailure"
            }
            $newStableInteraction = Test-NewStableInteraction -Ledger $ledger -Baseline $baseline
            if ($newStableInteraction -and (Test-TelemetryReady -Ledger $ledger)) {
                Assert-TelemetryLedger -Ledger $ledger -RawJson ([string]$rawJson)
                return [pscustomobject]@{
                    RawJson = [string]$rawJson
                    Ledger = $ledger
                }
            }
            $lastState = "webgpu=$($ledger.webgpu.status), frame=$($null -ne $ledger.frame), stable=$($ledger.interaction.stable_canvas_observations)/$($baseline.StableCanvasObservations + 1), latency_samples=$($ledger.interaction.handler_to_post_paint.samples)/$($baseline.LatencySamples + 1), viewport_starts=$($ledger.viewport.starts), allowed=$($ledger.permissions.allowed.status), denied=$($ledger.permissions.denied.status)"
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Timed out waiting for strictly qualifying Studio telemetry. Last state: $lastState"
}

Assert-FileExists -LiteralPath $telemetrySchemaPath -Description 'Telemetry schema'
Assert-FileExists -LiteralPath $captureSchemaPath -Description 'Capture schema'
Assert-FileExists -LiteralPath $proofConfigurationPath -Description 'Gate 0 proof configuration'

foreach ($jsonFile in @($telemetrySchemaPath, $captureSchemaPath, $proofConfigurationPath)) {
    [void]((Get-Content -LiteralPath $jsonFile -Raw) | ConvertFrom-Json -Depth 24)
}

if ($SelfTest) {
    Invoke-SelfTest
    return
}

if (-not $IsWindows) {
    throw 'Studio window acquisition requires an interactive Windows desktop.'
}

if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath = $canonicalExecutable
}
$resolvedExecutable = [System.IO.Path]::GetFullPath($ExecutablePath)
if (-not $resolvedExecutable.Equals($canonicalExecutable, [StringComparison]::OrdinalIgnoreCase)) {
    throw "ExecutablePath must be the controlled Gate 0 build output '$canonicalExecutable'. Arbitrary or copied executables are rejected."
}

$previousWebViewArguments = [Environment]::GetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', 'Process')
if (-not [string]::IsNullOrWhiteSpace($previousWebViewArguments)) {
    throw 'WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS is already set. Clear it before acquisition so renderer arguments remain reproducible.'
}

$initialGit = Get-GitSnapshot
Assert-GitSnapshot -Snapshot $initialGit -Description 'Pre-build source'

$configurationBefore = Get-Item -LiteralPath $proofConfigurationPath
$configurationHashBefore = Get-LowercaseSha256 -LiteralPath $proofConfigurationPath

if (Test-Path -LiteralPath $canonicalExecutable -PathType Leaf) {
    Remove-Item -LiteralPath $canonicalExecutable -Force
}

$buildStartedUtc = [DateTimeOffset]::UtcNow
$previousTargetDirectory = [Environment]::GetEnvironmentVariable('CARGO_TARGET_DIR', 'Process')
$env:CARGO_TARGET_DIR = Join-Path $repositoryRoot 'target'
try {
    Push-Location $repositoryRoot
    try {
        & cargo tauri build --no-bundle --config $proofConfigurationRelativePath
        $buildExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}
finally {
    [Environment]::SetEnvironmentVariable('CARGO_TARGET_DIR', $previousTargetDirectory, 'Process')
}
$buildCompletedUtc = [DateTimeOffset]::UtcNow
if ($buildExitCode -ne 0) {
    throw "Controlled Gate 0 Studio build failed with exit code $buildExitCode."
}

Assert-FileExists -LiteralPath $canonicalExecutable -Description 'Controlled Studio executable'
$postBuildGit = Get-GitSnapshot
Assert-GitSnapshot -Snapshot $postBuildGit -Description 'Post-build source'
if ($postBuildGit.CommitSha -cne $initialGit.CommitSha -or $postBuildGit.TreeSha -cne $initialGit.TreeSha) {
    throw 'Git HEAD or its source tree changed during the controlled build.'
}
$configurationHashAfterBuild = Get-LowercaseSha256 -LiteralPath $proofConfigurationPath
if ($configurationHashAfterBuild -cne $configurationHashBefore) {
    throw 'The Gate 0 proof configuration changed during the controlled build.'
}

$executableBeforeRun = Get-Item -LiteralPath $canonicalExecutable
$executableHashBeforeRun = Get-LowercaseSha256 -LiteralPath $canonicalExecutable
$builtAfterBuildStarted = $executableBeforeRun.LastWriteTimeUtc -ge $buildStartedUtc.UtcDateTime
if (-not $builtAfterBuildStarted) {
    throw 'The canonical executable timestamp predates the controlled build. A stale executable cannot be used as Gate 0 evidence.'
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts/gate-0/studio-window'
}
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null
$resolvedOutput = [System.IO.Path]::GetFullPath((Get-Item -LiteralPath $resolvedOutput).FullName)

Add-Type -AssemblyName System.Drawing
if (-not ('ForgeGate0WindowCapture.NativeMethods' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace ForgeGate0WindowCapture {
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static class NativeMethods {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr window, out Rect rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
    }
}
'@
}

$process = $null
$socket = $null
$bitmap = $null
$graphics = $null
$capturePath = $null
$telemetryPath = $null
$ledgerPath = $null
$acquisitionCompleted = $false
$portReservation = $null

try {
    $portReservation = New-LoopbackPortReservation
    $devToolsPort = $portReservation.Port
    $devToolsArguments = "--remote-debugging-address=127.0.0.1 --remote-debugging-port=$devToolsPort"
    [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', $devToolsArguments, 'Process')
    try {
        $portReservation.Listener.Stop()
        $process = Start-Process -FilePath $canonicalExecutable -PassThru
    }
    finally {
        if ($null -ne $portReservation) {
            try {
                $portReservation.Listener.Stop()
            }
            catch {
                Write-Verbose "Unable to release loopback port reservation cleanly: $($_.Exception.Message)"
            }
        }
        [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', $null, 'Process')
    }

    $windowDeadline = [DateTimeOffset]::UtcNow.AddSeconds($WindowTimeoutSeconds)
    $windowHandle = [IntPtr]::Zero
    while ([DateTimeOffset]::UtcNow -lt $windowDeadline) {
        if ($process.HasExited) {
            throw "Studio exited before exposing a window (exit code $($process.ExitCode))."
        }
        $process.Refresh()
        $windowHandle = $process.MainWindowHandle
        if ($windowHandle -ne [IntPtr]::Zero) {
            break
        }
        Start-Sleep -Milliseconds 200
    }
    if ($windowHandle -eq [IntPtr]::Zero) {
        throw "Studio did not expose a main window within $WindowTimeoutSeconds seconds."
    }

    $process.Refresh()
    $actualProcessPath = [System.IO.Path]::GetFullPath($process.Path)
    $processPathVerified = $actualProcessPath.Equals($canonicalExecutable, [StringComparison]::OrdinalIgnoreCase)
    if (-not $processPathVerified) {
        throw "Launched process path '$actualProcessPath' does not match controlled executable '$canonicalExecutable'."
    }

    $cdpTarget = Wait-CdpTarget -Port $devToolsPort -TimeoutSeconds $WindowTimeoutSeconds -Process $process
    $socket = Connect-CdpSocket -WebSocketUrl $cdpTarget.webSocketDebuggerUrl
    $telemetryEvidence = Get-QualifiedTelemetryFromCdp -Socket $socket -TimeoutSeconds $TelemetryTimeoutSeconds

    $stamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
    $capturePath = Join-Path $resolvedOutput "studio-window-$stamp.png"
    $telemetryPath = Join-Path $resolvedOutput "studio-window-$stamp.telemetry.json"
    $ledgerPath = Join-Path $resolvedOutput "studio-window-$stamp.json"
    [System.IO.File]::WriteAllText($telemetryPath, $telemetryEvidence.RawJson, [System.Text.UTF8Encoding]::new($false))
    $persistedTelemetryJson = Get-Content -LiteralPath $telemetryPath -Raw
    $persistedTelemetry = $persistedTelemetryJson | ConvertFrom-Json -Depth 24
    Assert-TelemetryLedger -Ledger $persistedTelemetry -RawJson $persistedTelemetryJson

    $process.Refresh()
    if ($process.MainWindowTitle -cne $expectedWindowTitle) {
        throw "Studio window title is '$($process.MainWindowTitle)', expected '$expectedWindowTitle'."
    }

    $foregroundRequested = $true
    $foregroundRequestSucceeded = [ForgeGate0WindowCapture.NativeMethods]::SetForegroundWindow($windowHandle)
    if (-not $foregroundRequestSucceeded) {
        throw 'SetForegroundWindow rejected the Gate 0 proof window.'
    }
    $foregroundDeadline = [DateTimeOffset]::UtcNow.AddSeconds(5)
    $foregroundVerified = $false
    while ([DateTimeOffset]::UtcNow -lt $foregroundDeadline) {
        if ([ForgeGate0WindowCapture.NativeMethods]::GetForegroundWindow() -eq $windowHandle) {
            $foregroundVerified = $true
            break
        }
        Start-Sleep -Milliseconds 100
    }
    if (-not $foregroundVerified) {
        throw 'The operating system did not foreground the Gate 0 proof window.'
    }

    if ($WarmupSeconds -gt 0) {
        Start-Sleep -Seconds $WarmupSeconds
    }
    if ([ForgeGate0WindowCapture.NativeMethods]::GetForegroundWindow() -ne $windowHandle) {
        throw 'The Gate 0 proof window lost foreground focus before capture.'
    }

    $rect = [ForgeGate0WindowCapture.Rect]::new()
    if (-not [ForgeGate0WindowCapture.NativeMethods]::GetWindowRect($windowHandle, [ref]$rect)) {
        $win32Error = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
        throw "GetWindowRect failed with Win32 error $win32Error."
    }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) {
        throw "Studio returned invalid window bounds: $width x $height."
    }

    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen(
        $rect.Left,
        $rect.Top,
        0,
        0,
        [System.Drawing.Size]::new($width, $height),
        [System.Drawing.CopyPixelOperation]::SourceCopy
    )
    if ([ForgeGate0WindowCapture.NativeMethods]::GetForegroundWindow() -ne $windowHandle) {
        throw 'The Gate 0 proof window lost foreground focus during capture.'
    }
    $bitmap.Save($capturePath, [System.Drawing.Imaging.ImageFormat]::Png)

    $stepX = [Math]::Max(1, [int]($width / 64))
    $stepY = [Math]::Max(1, [int]($height / 36))
    $sampledColors = [System.Collections.Generic.HashSet[int]]::new()
    for ($x = 0; $x -lt $width; $x += $stepX) {
        for ($y = 0; $y -lt $height; $y += $stepY) {
            [void]$sampledColors.Add($bitmap.GetPixel($x, $y).ToArgb())
        }
    }
    if ($sampledColors.Count -lt 4) {
        throw "Captured image has only $($sampledColors.Count) sampled colors; the non-blank machine check requires at least four."
    }

    $process.Refresh()
    if ($process.MainWindowTitle -cne $expectedWindowTitle) {
        throw 'The proof window title changed during capture.'
    }

    $finalGit = Get-GitSnapshot
    Assert-GitSnapshot -Snapshot $finalGit -Description 'Capture source'
    if ($finalGit.CommitSha -cne $initialGit.CommitSha -or $finalGit.TreeSha -cne $initialGit.TreeSha) {
        throw 'Git HEAD or its source tree changed between build and capture.'
    }
    $configurationHashAfterCapture = Get-LowercaseSha256 -LiteralPath $proofConfigurationPath
    if ($configurationHashAfterCapture -cne $configurationHashBefore) {
        throw 'The Gate 0 proof configuration changed between build and capture.'
    }
    $executableHashAfterRun = Get-LowercaseSha256 -LiteralPath $canonicalExecutable
    if ($executableHashAfterRun -cne $executableHashBeforeRun) {
        throw 'The Studio executable changed between launch and capture.'
    }

    $captureFile = Get-Item -LiteralPath $capturePath
    $telemetryFile = Get-Item -LiteralPath $telemetryPath
    $executableAfterRun = Get-Item -LiteralPath $canonicalExecutable
    $captureHash = Get-LowercaseSha256 -LiteralPath $capturePath
    $telemetryHash = Get-LowercaseSha256 -LiteralPath $telemetryPath

    $ledger = [ordered]@{
        schema = 'forge.gate0-studio-window-capture'
        schema_version = 1
        recorded_at_utc = [DateTimeOffset]::UtcNow.ToString('o')
        ledger = [ordered]@{
            path = $ledgerPath
        }
        git = [ordered]@{
            branch = $finalGit.Branch
            commit_sha = $finalGit.CommitSha
            tree_sha = $finalGit.TreeSha
            source_tree_clean = $finalGit.IsClean
        }
        build = [ordered]@{
            command = $buildCommandLabel
            started_at_utc = $buildStartedUtc.ToString('o')
            completed_at_utc = $buildCompletedUtc.ToString('o')
            head_commit_sha = $postBuildGit.CommitSha
            head_tree_sha = $postBuildGit.TreeSha
            configuration = [ordered]@{
                path = $configurationBefore.FullName
                bytes = $configurationBefore.Length
                sha256 = $configurationHashBefore
            }
            succeeded = $true
        }
        executable = [ordered]@{
            path = $executableAfterRun.FullName
            file_name = $executableAfterRun.Name
            launched_process_path = $actualProcessPath
            process_path_verified = $processPathVerified
            bytes = $executableAfterRun.Length
            sha256 = $executableHashAfterRun
            last_write_utc = ([DateTimeOffset]$executableAfterRun.LastWriteTimeUtc).ToString('o')
            built_after_build_started = $builtAfterBuildStarted
        }
        telemetry = [ordered]@{
            ledger = [ordered]@{
                path = $telemetryFile.FullName
                bytes = $telemetryFile.Length
                sha256 = $telemetryHash
            }
            schema_validated = $true
            semantic_checks_passed = $true
        }
        window = [ordered]@{
            process_id = $process.Id
            title = $process.MainWindowTitle
            foreground_requested = $foregroundRequested
            foreground_request_succeeded = $foregroundRequestSucceeded
            foreground_verified = $foregroundVerified
            left_px = $rect.Left
            top_px = $rect.Top
            width_px = $width
            height_px = $height
        }
        capture = [ordered]@{
            path = $captureFile.FullName
            bytes = $captureFile.Length
            sha256 = $captureHash
            width_px = $width
            height_px = $height
            sampled_unique_colors = $sampledColors.Count
            non_blank = $true
            method = 'foreground_copy_from_screen'
        }
        review = [ordered]@{
            status = 'uninspected'
            method = 'human_visual_review_required'
            human_review_performed = $false
            human_review_required = $true
        }
        qualification = [ordered]@{
            acquisition_status = 'acquired'
            machine_checks_passed = $true
            proof_status = 'pending_human_review'
            qualified = $false
        }
        limitations = @(
            'The capture requires an interactive Windows desktop and can include occluding windows.',
            'Machine checks acquire pixels and runtime telemetry; human visual review remains required.'
        )
    }
    $ledgerJson = ConvertTo-CompactJson -Value $ledger
    Assert-CaptureLedger -Ledger $ledger -RawJson $ledgerJson -ExpectedExecutablePath $canonicalExecutable -ExpectedOutputDirectory $resolvedOutput -ExpectedLedgerPath $ledgerPath -ValidateFiles
    [System.IO.File]::WriteAllText($ledgerPath, $ledgerJson, [System.Text.UTF8Encoding]::new($false))
    $persistedLedgerJson = Get-Content -LiteralPath $ledgerPath -Raw
    $persistedLedger = $persistedLedgerJson | ConvertFrom-Json -Depth 24
    Assert-CaptureLedger -Ledger $persistedLedger -RawJson $persistedLedgerJson -ExpectedExecutablePath $canonicalExecutable -ExpectedOutputDirectory $resolvedOutput -ExpectedLedgerPath $ledgerPath -ValidateFiles

    $acquisitionCompleted = $true
    Write-Output "FORGE_STUDIO_WINDOW_CAPTURE_ACQUIRED image=$capturePath telemetry=$telemetryPath ledger=$ledgerPath sha256=$captureHash qualified=false human_review_required=true"
}
finally {
    [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', $previousWebViewArguments, 'Process')
    if ($null -ne $portReservation) {
        try {
            $portReservation.Listener.Stop()
        }
        catch {
            Write-Verbose "Unable to release loopback port reservation during cleanup: $($_.Exception.Message)"
        }
    }
    if ($null -ne $graphics) {
        $graphics.Dispose()
    }
    if ($null -ne $bitmap) {
        $bitmap.Dispose()
    }
    if ($null -ne $socket) {
        try {
            if ($socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
                $closeTimeout = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(2))
                try {
                    Wait-NonGenericTask -Task ($socket.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'Gate 0 acquisition complete', $closeTimeout.Token))
                }
                finally {
                    $closeTimeout.Dispose()
                }
            }
        }
        catch {
            Write-Verbose "Unable to close CDP socket cleanly: $($_.Exception.Message)"
        }
        finally {
            $socket.Dispose()
        }
    }
    if ($null -ne $process -and -not $KeepOpen -and -not $process.HasExited) {
        [void]$process.CloseMainWindow()
        if (-not $process.WaitForExit(5000)) {
            Stop-Process -Id $process.Id -Force
        }
    }
    if (-not $acquisitionCompleted) {
        foreach ($partialArtifact in @($ledgerPath, $capturePath, $telemetryPath)) {
            if (-not [string]::IsNullOrWhiteSpace($partialArtifact) -and (Test-Path -LiteralPath $partialArtifact -PathType Leaf)) {
                Remove-Item -LiteralPath $partialArtifact -Force
            }
        }
    }
}
