[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EvidenceRoot,

    [string]$RuntimePath,

    [ValidateSet("low", "medium", "high", "ultra")]
    [string]$Quality = "high",

    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [string]$FfmpegPath,

    [switch]$NoVsync,

    [switch]$SkipVideo,

    [ValidateRange(30, 300)]
    [int]$RuntimeTimeoutSeconds = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-Condition {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-FullPathWithoutResolving {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([IO.Path]::IsPathRooted($Path)) {
        return [IO.Path]::GetFullPath($Path)
    }
    return [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $Path))
}

function Test-PortableRelativePath {
    param([AllowEmptyString()][string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or [IO.Path]::IsPathRooted($Path)) {
        return $false
    }
    if ($Path.Contains("\") -or $Path.Contains(":") -or $Path.StartsWith("/") -or $Path.EndsWith("/")) {
        return $false
    }
    foreach ($component in $Path.Split("/")) {
        if ([string]::IsNullOrWhiteSpace($component) -or $component -in @(".", "..")) {
            return $false
        }
    }
    return $true
}

function Get-PngObservation {
    param([Parameter(Mandatory = $true)][string]$Path)

    $bitmap = [System.Drawing.Bitmap]::new($Path)
    try {
        $colors = [Collections.Generic.HashSet[int]]::new()
        for ($sampleY = 0; $sampleY -lt 9; $sampleY++) {
            $y = [Math]::Min($bitmap.Height - 1, [Math]::Floor(($sampleY + 0.5) * $bitmap.Height / 9))
            for ($sampleX = 0; $sampleX -lt 16; $sampleX++) {
                $x = [Math]::Min($bitmap.Width - 1, [Math]::Floor(($sampleX + 0.5) * $bitmap.Width / 16))
                [void]$colors.Add($bitmap.GetPixel($x, $y).ToArgb())
            }
        }
        return [pscustomobject]@{
            Width = $bitmap.Width
            Height = $bitmap.Height
            SampledUniqueColors = $colors.Count
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

function Assert-NearlyEqual {
    param(
        [Parameter(Mandatory = $true)][double]$Actual,
        [Parameter(Mandatory = $true)][double]$Expected,
        [Parameter(Mandatory = $true)][string]$Field,
        [double]$Tolerance = 0.000001
    )

    Assert-Condition -Condition ([double]::IsFinite($Actual) -and [double]::IsFinite($Expected)) -Message (
        "{0} contains a non-finite value: actual={1}, expected={2}." -f $Field, $Actual, $Expected
    )
    Assert-Condition -Condition ([Math]::Abs($Actual - $Expected) -le $Tolerance) -Message (
        "{0} is {1}, expected {2} (+/- {3})." -f $Field, $Actual, $Expected, $Tolerance
    )
}

function Test-FiniteNumber {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return $false
    }
    try {
        return [double]::IsFinite([double]$Value)
    }
    catch {
        return $false
    }
}

function Invoke-BoundedProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$ArgumentList,
        [Parameter(Mandatory = $true)][ValidateRange(1, 3600)][int]$TimeoutSeconds,
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][string]$StdoutPath,
        [Parameter(Mandatory = $true)][string]$StderrPath,
        [string]$WorkingDirectory,
        [hashtable]$EnvironmentVariables = @{}
    )

    $process = $null
    $stdoutTask = $null
    $stderrTask = $null
    $stdoutText = ""
    $stderrText = ""
    $timedOut = $false
    $stoppedAfterKill = $true
    $stdoutDrained = $false
    $stderrDrained = $false
    try {
        $startInfo = [Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = $FilePath
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        if (-not [string]::IsNullOrWhiteSpace($WorkingDirectory)) {
            $startInfo.WorkingDirectory = $WorkingDirectory
        }
        foreach ($name in $EnvironmentVariables.Keys) {
            Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$name)) -Message "Process environment variable names must not be empty."
            $startInfo.Environment[[string]$name] = [string]$EnvironmentVariables[$name]
        }
        foreach ($argument in $ArgumentList) {
            [void]$startInfo.ArgumentList.Add($argument)
        }

        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $startInfo
        Assert-Condition -Condition ($process.Start()) -Message "Cannot start $Label."
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $timedOut = $true
            try {
                $process.Kill($true)
            }
            catch {
                $stderrText = "[$Label watchdog] process-tree termination failed: $($_.Exception.Message)"
            }
            $stoppedAfterKill = $process.WaitForExit(5000)
        }

        try {
            $stdoutDrained = $stdoutTask.Wait(5000)
            if ($stdoutDrained) {
                $stdoutText = $stdoutTask.GetAwaiter().GetResult()
            }
        }
        catch {
            $stdoutText = "[$Label watchdog] stdout drainage failed: $($_.Exception.Message)"
        }
        try {
            $stderrDrained = $stderrTask.Wait(5000)
            if ($stderrDrained) {
                $capturedStderr = $stderrTask.GetAwaiter().GetResult()
                if ([string]::IsNullOrWhiteSpace($stderrText)) {
                    $stderrText = $capturedStderr
                }
                elseif (-not [string]::IsNullOrWhiteSpace($capturedStderr)) {
                    $stderrText += [Environment]::NewLine + $capturedStderr
                }
            }
        }
        catch {
            if (-not [string]::IsNullOrWhiteSpace($stderrText)) {
                $stderrText += [Environment]::NewLine
            }
            $stderrText += "[$Label watchdog] stderr drainage failed: $($_.Exception.Message)"
        }

        if (-not $stdoutDrained -and [string]::IsNullOrWhiteSpace($stdoutText)) {
            $stdoutText = "[$Label watchdog] stdout did not drain within 5 seconds."
        }
        if (-not $stderrDrained -and [string]::IsNullOrWhiteSpace($stderrText)) {
            $stderrText = "[$Label watchdog] stderr did not drain within 5 seconds."
        }
        [IO.File]::WriteAllText($StdoutPath, $stdoutText, [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText($StderrPath, $stderrText, [Text.UTF8Encoding]::new($false))

        if ($timedOut) {
            throw "$Label exceeded the host timeout of $TimeoutSeconds seconds; stopped_after_kill=$stoppedAfterKill."
        }
        if (-not $stdoutDrained -or -not $stderrDrained) {
            throw "$Label output did not drain inside the bounded five-second window."
        }
        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            Stdout = $stdoutText
            Stderr = $stderrText
        }
    }
    catch {
        $failureMessage = $_.Exception.Message
        if ($null -ne $process) {
            try {
                if (-not $process.HasExited) {
                    $process.Kill($true)
                    [void]$process.WaitForExit(5000)
                }
            }
            catch {
                $stderrText += [Environment]::NewLine + "[$Label watchdog] failure cleanup could not confirm process-tree termination: $($_.Exception.Message)"
            }
        }
        if ([IO.File]::Exists($StdoutPath)) {
            $stdoutText = [IO.File]::ReadAllText($StdoutPath)
        }
        if ([IO.File]::Exists($StderrPath)) {
            $recordedStderr = [IO.File]::ReadAllText($StderrPath)
            if (-not [string]::IsNullOrWhiteSpace($recordedStderr)) {
                $stderrText = $recordedStderr
            }
        }
        if (-not [string]::IsNullOrWhiteSpace($stderrText)) {
            $stderrText += [Environment]::NewLine
        }
        $stderrText += "[$Label failure] $failureMessage"
        [IO.File]::WriteAllText($StdoutPath, $stdoutText, [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText($StderrPath, $stderrText, [Text.UTF8Encoding]::new($false))
        throw
    }
    finally {
        if ($null -ne $process) {
            $process.Dispose()
        }
    }
}

function Get-DisallowedLogLines {
    param([Parameter(Mandatory = $true)][string]$Path)

    $issues = [Collections.Generic.List[string]]::new()
    foreach ($line in [IO.File]::ReadAllLines($Path)) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        try {
            $event = $line | ConvertFrom-Json -Depth 32
            $level = [string]$event.level
            if ($level -in @("WARN", "ERROR")) {
                $issues.Add($line)
            }
        }
        catch {
            if ($line -match '(?i)\b(warn(?:ing)?|error|panic(?:ked)?)\b') {
                $issues.Add($line)
            }
        }
    }
    return @($issues)
}

$evidencePath = Get-FullPathWithoutResolving -Path $EvidenceRoot
$repositoryPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$assetDirectory = (Resolve-Path -LiteralPath (Join-Path $repositoryPath "assets")).Path
Assert-Condition -Condition ([IO.Directory]::Exists($assetDirectory)) -Message "The governed repository asset directory is absent."
$runtimeFullPath = $null
$runtimeLaunch = $null
$gitCommit = (& git -C $repositoryPath rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Cannot resolve the repository commit."
}
$gitTree = (& git -C $repositoryPath show -s --format=%T HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Cannot resolve the repository tree."
}
$gitStatusBefore = @(& git -C $repositoryPath status --porcelain=v1 --untracked-files=all)
if ($LASTEXITCODE -ne 0) {
    throw "Cannot inspect the repository worktree."
}
Assert-Condition -Condition ($gitStatusBefore.Count -eq 0) -Message "Gate 1 visual proof requires a clean exact-commit worktree."
$relativeEvidencePath = [IO.Path]::GetRelativePath($repositoryPath, $evidencePath)
$evidenceIsInsideRepository = -not [IO.Path]::IsPathRooted($relativeEvidencePath) -and
    $relativeEvidencePath -ne ".." -and
    -not $relativeEvidencePath.StartsWith("..$([IO.Path]::DirectorySeparatorChar)") -and
    -not $relativeEvidencePath.StartsWith("../")
Assert-Condition -Condition (-not $evidenceIsInsideRepository) -Message "EvidenceRoot must be outside the Git worktree so acquisition cannot dirty the exact source."

if (-not [string]::IsNullOrWhiteSpace($RuntimePath)) {
    $runtimeFullPath = (Resolve-Path -LiteralPath $RuntimePath).Path
    Assert-Condition -Condition ([IO.File]::Exists($runtimeFullPath)) -Message "RuntimePath must name a file."
    Assert-Condition -Condition $NoVsync.IsPresent -Message "Runtime acquisition requires -NoVsync."
    [void][IO.Directory]::CreateDirectory($evidencePath)
    $runtimeArguments = @(
        "--visual-valley",
        "--quality", $Quality,
        "--gate1-visual-proof", $evidencePath,
        "--no-vsync"
    )
    $runtimeEnvironment = @{
        BEVY_ASSET_ROOT = $repositoryPath
        WGPU_BACKEND = "vulkan"
    }
    $runtimeLaunch = [ordered]@{
        working_directory = $repositoryPath
        bevy_asset_root = $repositoryPath
        asset_directory = $assetDirectory
        assets_referenced_in_place = $true
        assets_copied_to_release_directory = $false
        requested_wgpu_backend = "vulkan"
    }
    $temporaryStdout = [IO.Path]::GetTempFileName()
    $temporaryStderr = [IO.Path]::GetTempFileName()
    try {
        $runtimeResult = Invoke-BoundedProcess `
            -FilePath $runtimeFullPath `
            -ArgumentList $runtimeArguments `
            -TimeoutSeconds $RuntimeTimeoutSeconds `
            -Label "forge_runtime Gate 1 visual proof" `
            -StdoutPath $temporaryStdout `
            -StderrPath $temporaryStderr `
            -WorkingDirectory $repositoryPath `
            -EnvironmentVariables $runtimeEnvironment
        if ($runtimeResult.ExitCode -ne 0) {
            throw "forge_runtime Gate 1 visual proof failed with exit code $($runtimeResult.ExitCode)."
        }
    }
    finally {
        if ([IO.Directory]::Exists($evidencePath)) {
            Move-Item -LiteralPath $temporaryStdout -Destination (Join-Path $evidencePath "runtime.stdout.log") -Force
            Move-Item -LiteralPath $temporaryStderr -Destination (Join-Path $evidencePath "runtime.stderr.log") -Force
        }
        Remove-Item -LiteralPath $temporaryStdout, $temporaryStderr -Force -ErrorAction SilentlyContinue
    }
}

Assert-Condition -Condition ([IO.Directory]::Exists($evidencePath)) -Message "EvidenceRoot does not exist: $evidencePath"
$ledgerPath = Join-Path $evidencePath "gate1-visual-proof.json"
Assert-Condition -Condition ([IO.File]::Exists($ledgerPath)) -Message "Missing Gate 1 visual proof ledger: $ledgerPath"
$ledger = [IO.File]::ReadAllText($ledgerPath) | ConvertFrom-Json -Depth 64

Assert-Condition -Condition ($ledger.schema -eq "forge.gate1-visual-proof") -Message "Unexpected ledger schema."
Assert-Condition -Condition ($ledger.schema_version -eq 3) -Message "Unexpected ledger schema version."
Assert-Condition -Condition ($ledger.status -eq "automated_capture_complete_pending_stutter_and_qualification") -Message "The acquisition did not finish cleanly."
Assert-Condition -Condition ($ledger.build_source_commit -eq $gitCommit) -Message "The runtime binary is not linked to the exact repository commit."
Assert-Condition -Condition (($ledger.protocol.physical_resolution_px -join "x") -eq "1920x1080") -Message "Unexpected final physical resolution."
Assert-Condition -Condition ($ledger.protocol.requested_present_mode -eq "auto_no_vsync") -Message "The runtime did not request the governed no-VSync present mode."
Assert-Condition -Condition ($ledger.protocol.vsync_requested -eq $false) -Message "The proof ledger reports VSync enabled."
Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$ledger.protocol.gpu.name)) -Message "GPU adapter name is absent."
Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$ledger.protocol.gpu.backend)) -Message "GPU backend is absent."
Assert-Condition -Condition ($ledger.protocol.gpu.backend -eq "Vulkan") -Message "Gate 1 visual proof requires the requested Vulkan backend."
Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$ledger.protocol.gpu.driver)) -Message "GPU driver is absent."
Assert-Condition -Condition ($ledger.timeline.same_process -eq $true) -Message "The clear-rain-clear timeline is not declared single-process."
Assert-Condition -Condition (($ledger.timeline.requested_states -join ",") -eq "clear,rain,clear") -Message "Unexpected weather transition sequence."
Assert-Condition -Condition ($ledger.sources.cadle_assets_used -eq $false) -Message "Cadle assets must never be used by the Forge proof."
Assert-Condition -Condition ($ledger.pipeline_warmup.status -eq "ready") -Message "Pipelines were not ready when the proof completed."
Assert-Condition -Condition ($ledger.quality.selection.mode -eq "fixed") -Message "The proof must use one fixed quality tier."
$expectedQuality = $Quality.ToLowerInvariant()
Assert-Condition -Condition ([string]::Equals([string]$ledger.quality.selection.tier, $expectedQuality, [StringComparison]::Ordinal)) -Message "The proof quality selection does not match -Quality $expectedQuality."
Assert-Condition -Condition ([string]::Equals([string]$ledger.quality.startup_tier, $expectedQuality, [StringComparison]::Ordinal)) -Message "The startup quality tier does not match -Quality $expectedQuality."
Assert-Condition -Condition ([string]::Equals([string]$ledger.quality.current_tier, $expectedQuality, [StringComparison]::Ordinal)) -Message "The current quality tier does not match -Quality $expectedQuality."
Assert-Condition -Condition ([string]::Equals([string]$ledger.quality.dynamically_applied_tier, $expectedQuality, [StringComparison]::Ordinal)) -Message "The applied quality tier does not match -Quality $expectedQuality."
Assert-Condition -Condition ($ledger.quality.adaptation_enabled -eq $false) -Message "Quality adaptation must remain disabled during the proof."
Assert-Condition -Condition ($ledger.quality.transition_count -eq 0) -Message "The fixed quality tier changed during the proof."
Assert-Condition -Condition ($ledger.qualification.rendered_png_artifacts_acquired -eq $true) -Message "PNG acquisition is not recorded."
Assert-Condition -Condition ($ledger.qualification.weather_cpu_ecs_projection_observed -eq $true) -Message "Weather CPU/ECS evidence is absent."
Assert-Condition -Condition ($ledger.qualification.weather_gpu_execution_observed -eq $false) -Message "This harness must not invent weather GPU execution evidence."
Assert-Condition -Condition ($ledger.qualification.real_time_stutter_qualified -eq $false) -Message "Paused checkpoint acquisition cannot qualify real-time stutter."
Assert-Condition -Condition ($ledger.qualification.human_review_performed -eq $false) -Message "Automated validation cannot claim human review."
Assert-Condition -Condition ($ledger.qualification.visual_quality_accepted -eq $false) -Message "Automated validation cannot accept visual quality."
Assert-Condition -Condition ($ledger.qualification.gate1_routes_qualified -eq $false) -Message "Artifact acquisition alone cannot close Gate 1 routes."

$expectedRouteStatuses = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
$expectedRouteStatuses.Add("R1-023", "scene_gpu_pass_diagnostics_sampled_at_water_views_for_selected_tier_pending_water_isolation_low_high_browser_and_human_review")
$expectedRouteStatuses.Add("R1-025", "clear_rain_clear_cpu_ecs_endpoints_observed")
$expectedRouteStatuses.Add("R1-026", "color_grade_endpoints_and_scene_postprocess_gpu_pass_diagnostics_sampled_for_selected_tier_pending_low_high_browser_and_human_review")
$expectedRouteStatuses.Add("R1-046", "human_height_png_acquired_pending_human_review")
$expectedRouteStatuses.Add("R1-047", "distant_png_acquired_pending_human_review")
$expectedRouteStatuses.Add("R1-048", "gameplay_controller_frames_acquired_pending_mp4_stutter_and_human_review")
Assert-Condition -Condition (@($ledger.routes).Count -eq $expectedRouteStatuses.Count) -Message "The Gate 1 proof must contain exactly six route entries."
$routeIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($route in @($ledger.routes)) {
    $routeId = [string]$route.route
    Assert-Condition -Condition ($routeIds.Add($routeId)) -Message "Duplicate route evidence entry $routeId."
    Assert-Condition -Condition ($expectedRouteStatuses.ContainsKey($routeId)) -Message "Unexpected route evidence entry $routeId."
    Assert-Condition -Condition ([string]::Equals([string]$route.automated_status, $expectedRouteStatuses[$routeId], [StringComparison]::Ordinal)) -Message "Unexpected automated status for $routeId."
}
foreach ($expectedRouteId in $expectedRouteStatuses.Keys) {
    Assert-Condition -Condition ($routeIds.Contains($expectedRouteId)) -Message "Missing canonical route evidence entry $expectedRouteId."
}
$r1026 = @($ledger.routes | Where-Object route -eq "R1-026")
Assert-Condition -Condition ($r1026.Count -eq 1) -Message "R1-026 must have exactly one route entry."
Assert-Condition -Condition ($r1026[0].automated_status -eq "color_grade_endpoints_and_scene_postprocess_gpu_pass_diagnostics_sampled_for_selected_tier_pending_low_high_browser_and_human_review") -Message "R1-026 must keep the Low/High Browser matrix and human review pending."
$r1048 = @($ledger.routes | Where-Object route -eq "R1-048")
Assert-Condition -Condition ($r1048.Count -eq 1) -Message "R1-048 must have exactly one route entry."
Assert-Condition -Condition ($r1048[0].automated_status -eq "gameplay_controller_frames_acquired_pending_mp4_stutter_and_human_review") -Message "R1-048 must distinguish gameplay acquisition from stutter and human qualification."

$movement = $ledger.movement
Assert-Condition -Condition ($movement.kind -eq "deterministic_fixed_tick_gameplay_controller") -Message "Movement evidence is not sourced from the deterministic gameplay controller."
Assert-Condition -Condition ($movement.gameplay_controller_exercised -eq $true) -Message "The gameplay controller was not exercised."
Assert-Condition -Condition ($movement.interactive_input_enabled -eq $false) -Message "Interactive input must remain disabled during gameplay proof."
Assert-NearlyEqual -Actual $movement.fixed_hz -Expected 60.0 -Field "movement fixed_hz" -Tolerance 0.0001
Assert-NearlyEqual -Actual $movement.fixed_timestep_seconds -Expected (1.0 / 60.0) -Field "movement fixed timestep" -Tolerance 0.000000001
Assert-Condition -Condition ($movement.frame_count -eq 24 -and $movement.intended_video_fps -eq 8) -Message "Unexpected gameplay frame count or video rate."
Assert-Condition -Condition ($movement.command_sequence -gt 0) -Message "Gameplay movement command sequence is absent."
Assert-Condition -Condition ($movement.stop_sequence -eq ($movement.command_sequence + 1)) -Message "Gameplay stop must immediately follow the movement command."
Assert-Condition -Condition ($movement.command_applied_exactly -eq $true -and $movement.stopped_command_applied_exactly -eq $true) -Message "Gameplay movement and stop commands were not applied exactly."
Assert-Condition -Condition ($movement.stop_wait_ticks -gt 0 -and $movement.stop_wait_ticks -le 30) -Message "Gameplay stop did not settle inside its bounded fixed-tick window."
Assert-Condition -Condition ($movement.observed_fixed_ticks -eq 173) -Message "Gameplay proof did not observe the exact 173-tick path."
Assert-Condition -Condition ($movement.grounded_ticks -gt 0 -and $movement.grounded_ticks -le $movement.observed_fixed_ticks) -Message "Gameplay grounded sample count is invalid."
Assert-Condition -Condition ($movement.maximum_ungrounded_run -le 4) -Message "Gameplay grounding continuity exceeded four fixed ticks."
Assert-Condition -Condition ($movement.collision_ticks -gt 0) -Message "Gameplay proof observed no terrain collision."
Assert-Condition -Condition ($movement.backend_identity_stable -eq $true) -Message "Gameplay backend identity changed."
Assert-Condition -Condition ($movement.camera_marker_verified -eq $true) -Message "Movement camera was not verified as ForgePlayerCamera."
Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$movement.camera_entity)) -Message "Gameplay camera entity is absent."
Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$movement.player_entity)) -Message "Gameplay player entity is absent."
Assert-Condition -Condition (@($movement.rapier_collider_handle).Count -eq 2) -Message "Gameplay Rapier collider handle is incomplete."
Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$movement.rapier_context_entity)) -Message "Gameplay Rapier context entity is absent."
Assert-Condition -Condition (@($movement.start_xz_m).Count -eq 2 -and @($movement.end_xz_m).Count -eq 2 -and @($movement.stopped_xz_m).Count -eq 2) -Message "Gameplay movement positions are incomplete."
foreach ($field in @("start_xz_m", "end_xz_m", "stopped_xz_m")) {
    foreach ($value in @($movement.$field)) {
        Assert-Condition -Condition (Test-FiniteNumber -Value $value) -Message "Gameplay movement $field contains a non-finite value."
    }
}
foreach ($field in @("planar_distance_m", "forward_distance_m", "lateral_drift_m")) {
    Assert-Condition -Condition (Test-FiniteNumber -Value $movement.$field) -Message "Gameplay movement $field is non-finite."
}
Assert-Condition -Condition ($movement.start_xz_m[1] -gt 0.0 -and $movement.end_xz_m[1] -lt 0.0) -Message "Gameplay movement did not cross the central Z seam."
Assert-Condition -Condition ($movement.forward_distance_m -ge 8.0) -Message "Gameplay movement covered less than eight metres toward negative Z."
Assert-Condition -Condition ($movement.lateral_drift_m -le 0.75) -Message "Gameplay movement drifted more than 0.75 metres laterally."
Assert-Condition -Condition ($movement.planar_distance_m -ge $movement.forward_distance_m) -Message "Gameplay planar distance is inconsistent with its forward component."
$movementDeltaX = [double]$movement.end_xz_m[0] - [double]$movement.start_xz_m[0]
$movementDeltaZ = [double]$movement.end_xz_m[1] - [double]$movement.start_xz_m[1]
$recalculatedForwardDistance = [double]$movement.start_xz_m[1] - [double]$movement.end_xz_m[1]
$recalculatedLateralDrift = [Math]::Abs($movementDeltaX)
$recalculatedPlanarDistance = [Math]::Sqrt(($movementDeltaX * $movementDeltaX) + ($movementDeltaZ * $movementDeltaZ))
Assert-NearlyEqual -Actual $movement.forward_distance_m -Expected $recalculatedForwardDistance -Field "movement recalculated forward distance" -Tolerance 0.00001
Assert-NearlyEqual -Actual $movement.lateral_drift_m -Expected $recalculatedLateralDrift -Field "movement recalculated lateral drift" -Tolerance 0.00001
Assert-NearlyEqual -Actual $movement.planar_distance_m -Expected $recalculatedPlanarDistance -Field "movement recalculated planar distance" -Tolerance 0.00001
$movementCheckpoints = @($movement.checkpoints)
Assert-Condition -Condition ($movementCheckpoints.Count -eq 24) -Message "Expected 24 gameplay checkpoints."
$firstMovementFixedTick = $movementCheckpoints[0].fixed_tick
for ($index = 0; $index -lt $movementCheckpoints.Count; $index++) {
    $checkpoint = $movementCheckpoints[$index]
    $expectedOffset = [Math]::Floor((($index * 60) + 4) / 8)
    Assert-Condition -Condition ($checkpoint.index -eq $index) -Message "Gameplay checkpoint index mismatch at $index."
    Assert-Condition -Condition ($checkpoint.target_tick_offset -eq $expectedOffset -and $checkpoint.observed_tick_offset -eq $expectedOffset) -Message "Gameplay checkpoint $index is not bound to exact tick offset $expectedOffset."
    Assert-Condition -Condition (($checkpoint.fixed_tick - $firstMovementFixedTick) -eq $expectedOffset) -Message "Gameplay checkpoint $index fixed tick does not match offset $expectedOffset."
    Assert-Condition -Condition (@($checkpoint.player_position_m).Count -eq 3) -Message "Gameplay checkpoint $index has no complete player position."
    Assert-Condition -Condition (@($checkpoint.camera.translation_m).Count -eq 3 -and @($checkpoint.camera.forward).Count -eq 3) -Message "Gameplay checkpoint $index has incomplete camera vectors."
    foreach ($value in @($checkpoint.player_position_m) + @($checkpoint.camera.translation_m) + @($checkpoint.camera.forward) + @($checkpoint.camera.vertical_fov_degrees)) {
        Assert-Condition -Condition (Test-FiniteNumber -Value $value) -Message "Gameplay checkpoint $index contains a non-finite player or camera value."
    }
    Assert-Condition -Condition ([string]$checkpoint.camera_entity -eq [string]$movement.camera_entity) -Message "Gameplay checkpoint $index camera entity differs from the stable binding."
    Assert-Condition -Condition ($checkpoint.camera.forge_player_camera -eq $true) -Message "Gameplay checkpoint $index camera is not ForgePlayerCamera."
    if ($index -gt 0) {
        Assert-Condition -Condition ($checkpoint.fixed_tick -gt $movementCheckpoints[$index - 1].fixed_tick) -Message "Gameplay checkpoint fixed ticks are not strictly monotone."
    }
}
Assert-NearlyEqual -Actual $movementCheckpoints[0].player_position_m[0] -Expected $movement.start_xz_m[0] -Field "movement start X link" -Tolerance 0.000001
Assert-NearlyEqual -Actual $movementCheckpoints[0].player_position_m[2] -Expected $movement.start_xz_m[1] -Field "movement start Z link" -Tolerance 0.000001
Assert-NearlyEqual -Actual $movementCheckpoints[23].player_position_m[0] -Expected $movement.end_xz_m[0] -Field "movement end X link" -Tolerance 0.000001
Assert-NearlyEqual -Actual $movementCheckpoints[23].player_position_m[2] -Expected $movement.end_xz_m[1] -Field "movement end Z link" -Tolerance 0.000001

$stdoutPath = Join-Path $evidencePath "runtime.stdout.log"
$stderrPath = Join-Path $evidencePath "runtime.stderr.log"
Assert-Condition -Condition ([IO.File]::Exists($stdoutPath)) -Message "Missing runtime.stdout.log."
Assert-Condition -Condition ([IO.File]::Exists($stderrPath)) -Message "Missing runtime.stderr.log."
$disallowedLogs = @(
    @(Get-DisallowedLogLines -Path $stdoutPath)
    @(Get-DisallowedLogLines -Path $stderrPath)
)
Assert-Condition -Condition ($disallowedLogs.Count -eq 0) -Message "Runtime logs contain warning, error or panic entries."

$weatherPath = Join-Path $repositoryPath "examples/visual_valley/weather.v1.json"
$weather = [IO.File]::ReadAllText($weatherPath) | ConvertFrom-Json -Depth 32
$checkpoints = @($ledger.timeline.checkpoints)
Assert-Condition -Condition ($checkpoints.Count -eq 5) -Message "Expected five fixed-view weather checkpoints."
$checkpointById = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
foreach ($checkpoint in $checkpoints) {
    Assert-Condition -Condition (-not $checkpointById.ContainsKey($checkpoint.capture_id)) -Message "Duplicate weather checkpoint $($checkpoint.capture_id)."
    $checkpointById[$checkpoint.capture_id] = $checkpoint
    Assert-Condition -Condition ($checkpoint.terrain_materials -gt 0) -Message "Checkpoint $($checkpoint.capture_id) has no terrain material."
    Assert-Condition -Condition ($checkpoint.vegetation_materials -gt 0) -Message "Checkpoint $($checkpoint.capture_id) has no vegetation material."
    Assert-Condition -Condition ($checkpoint.terrain_uniforms_match -eq $true) -Message "Terrain wetness mismatch at $($checkpoint.capture_id)."
    Assert-Condition -Condition ($checkpoint.vegetation_uniforms_match -eq $true) -Message "Vegetation wetness mismatch at $($checkpoint.capture_id)."
    Assert-Condition -Condition ($checkpoint.bloom_requested -eq $checkpoint.bloom_component_present) -Message "Bloom component mismatch at $($checkpoint.capture_id)."
    Assert-Condition -Condition ($checkpoint.color_grading_component_present -eq $true) -Message "Color grading absent at $($checkpoint.capture_id)."
    Assert-Condition -Condition ($checkpoint.rain_shader_uniform_updated -eq $true) -Message "Rain shader uniform was not updated at $($checkpoint.capture_id)."
    Assert-Condition -Condition ($checkpoint.rain_camera_followed -eq $true) -Message "Rain volume did not follow the camera at $($checkpoint.capture_id)."
    Assert-Condition -Condition ($checkpoint.weather_gpu_execution_observed -eq $false) -Message "CPU/ECS checkpoint cannot claim GPU execution."
}

foreach ($id in @("distant-clear", "human-clear", "water-clear", "water-rain", "water-restored")) {
    Assert-Condition -Condition ($checkpointById.ContainsKey($id)) -Message "Missing weather checkpoint $id."
}
$clear = $checkpointById["water-clear"]
$rain = $checkpointById["water-rain"]
$restored = $checkpointById["water-restored"]
Assert-Condition -Condition ($clear.state -eq "clear" -and $rain.state -eq "rain" -and $restored.state -eq "clear") -Message "Water proof does not follow clear-rain-clear."
Assert-NearlyEqual -Actual $clear.current_wetness -Expected $weather.wetness.clear_target -Field "water-clear wetness"
Assert-NearlyEqual -Actual $rain.current_wetness -Expected $weather.wetness.rain_target -Field "water-rain wetness"
Assert-NearlyEqual -Actual $restored.current_wetness -Expected $weather.wetness.clear_target -Field "water-restored wetness"
Assert-Condition -Condition ($clear.clear_target_restored_exactly -eq $true) -Message "Initial clear endpoint is not exact."
Assert-Condition -Condition ($restored.clear_target_restored_exactly -eq $true) -Message "Restored clear endpoint is not exact."
Assert-Condition -Condition ($clear.simulation_seconds -lt $rain.simulation_seconds -and $rain.simulation_seconds -lt $restored.simulation_seconds) -Message "Weather checkpoint times are not monotone."

foreach ($id in @("distant-clear", "human-clear")) {
    Assert-Condition -Condition ($null -eq $checkpointById[$id].gpu_pass_diagnostics) -Message "Non-water checkpoint $id must not claim GPU pass diagnostics."
}
$transmissionExpected = $expectedQuality -in @("high", "ultra")
$bloomExpected = $expectedQuality -ne "low"
$expectedGpuPasses = [ordered]@{
    main_opaque_pass_3d = [pscustomobject]@{
        Path = "render/main_opaque_pass_3d/elapsed_gpu"
        Expected = $true
    }
    main_transmissive_pass_3d = [pscustomobject]@{
        Path = "render/main_transmissive_pass_3d/elapsed_gpu"
        Expected = $transmissionExpected
    }
    bloom = [pscustomobject]@{
        Path = "render/bloom/elapsed_gpu"
        Expected = $bloomExpected
    }
    tonemapping = [pscustomobject]@{
        Path = "render/tonemapping/elapsed_gpu"
        Expected = $true
    }
}
$waterCheckpoints = @($clear, $rain, $restored)
$expectedGpuMeasurementCount = 0
for ($checkpointIndex = 0; $checkpointIndex -lt $waterCheckpoints.Count; $checkpointIndex++) {
    $checkpoint = $waterCheckpoints[$checkpointIndex]
    $gpuPasses = $checkpoint.gpu_pass_diagnostics
    Assert-Condition -Condition ($null -ne $gpuPasses) -Message "Checkpoint $($checkpoint.capture_id) has no GPU pass diagnostics."
    Assert-Condition -Condition ([int]$gpuPasses.sampled_at_frame -eq [int]$checkpoint.requested_frame) -Message "GPU diagnostics are not linked to checkpoint $($checkpoint.capture_id)."
    Assert-Condition -Condition ([int]$gpuPasses.settle_started_at_frame -lt [int]$gpuPasses.sampled_at_frame) -Message "GPU diagnostic settle boundary is not earlier than the sample for $($checkpoint.capture_id)."
    Assert-Condition -Condition ([int]$gpuPasses.minimum_settled_frames_before_sample -eq 8) -Message "Unexpected GPU diagnostic settle-frame contract for $($checkpoint.capture_id)."
    Assert-Condition -Condition ([int]$gpuPasses.settled_frames_before_sample -eq ([int]$gpuPasses.sampled_at_frame - [int]$gpuPasses.settle_started_at_frame)) -Message "GPU diagnostic settled-frame count is inconsistent for $($checkpoint.capture_id)."
    Assert-Condition -Condition (([int]$gpuPasses.sampled_at_frame - [int]$gpuPasses.settle_started_at_frame) -ge [int]$gpuPasses.minimum_settled_frames_before_sample) -Message "GPU diagnostic sample for $($checkpoint.capture_id) was taken before eight settled frames."
    Assert-Condition -Condition ($gpuPasses.render_frame_identity -eq "not_available_from_bevy_diagnostics_store") -Message "GPU diagnostics must not invent a render-frame identity."
    Assert-Condition -Condition ($gpuPasses.quality_tier -eq $expectedQuality) -Message "GPU diagnostics use an unexpected quality tier at $($checkpoint.capture_id)."
    Assert-Condition -Condition ($gpuPasses.weather_state -eq $checkpoint.state) -Message "GPU diagnostics use an unexpected weather state at $($checkpoint.capture_id)."
    Assert-Condition -Condition (Test-FiniteNumber -Value $gpuPasses.wetness) -Message "Checkpoint $($checkpoint.capture_id) has no finite GPU-diagnostic wetness link."
    Assert-NearlyEqual -Actual $gpuPasses.wetness -Expected $checkpoint.current_wetness -Field "$($checkpoint.capture_id) GPU wetness link"
    Assert-Condition -Condition ($gpuPasses.evidence_source -eq "bevy_render_diagnostics_latest_delayed_gpu_measurement") -Message "Unexpected GPU diagnostic evidence source."

    foreach ($passName in $expectedGpuPasses.Keys) {
        $pass = $gpuPasses.$passName
        $expectedPass = $expectedGpuPasses[$passName]
        Assert-Condition -Condition ($null -ne $pass) -Message "Checkpoint $($checkpoint.capture_id) has no $passName diagnostic entry."
        Assert-Condition -Condition ($pass.diagnostic_path -eq $expectedPass.Path) -Message "Checkpoint $($checkpoint.capture_id) uses an unexpected path for $passName."
        Assert-Condition -Condition ($pass.expected -is [bool] -and $pass.expected -eq $expectedPass.Expected) -Message "Checkpoint $($checkpoint.capture_id) has an incorrect expectation for $passName."
        if ($expectedPass.Expected) {
            Assert-Condition -Condition (Test-FiniteNumber -Value $pass.elapsed_gpu_ms) -Message "Checkpoint $($checkpoint.capture_id) has no finite GPU millisecond value for $passName."
            Assert-Condition -Condition ([double]$pass.elapsed_gpu_ms -ge 0.0) -Message "Checkpoint $($checkpoint.capture_id) has a negative GPU millisecond value for $passName."
            Assert-Condition -Condition (Test-FiniteNumber -Value $pass.measurement_sync_age_seconds) -Message "Checkpoint $($checkpoint.capture_id) has no finite measurement synchronization age for $passName."
            Assert-Condition -Condition ([double]$pass.measurement_sync_age_seconds -ge 0.0) -Message "Checkpoint $($checkpoint.capture_id) has a negative measurement synchronization age for $passName."
            Assert-Condition -Condition ($pass.measurement_synced_after_settle_started -is [bool] -and $pass.measurement_synced_after_settle_started) -Message "Checkpoint $($checkpoint.capture_id) has no $passName measurement synchronized during its settle window."
            $expectedGpuMeasurementCount++
        }
        else {
            Assert-Condition -Condition ($null -eq $pass.elapsed_gpu_ms -and $null -eq $pass.measurement_sync_age_seconds -and $null -eq $pass.measurement_synced_after_settle_started) -Message "Checkpoint $($checkpoint.capture_id) reports an unexpected $passName GPU measurement synchronized during the current settle window."
        }
    }
}
Assert-Condition -Condition ($clear.gpu_pass_diagnostics.sampled_at_frame -lt $rain.gpu_pass_diagnostics.sampled_at_frame -and $rain.gpu_pass_diagnostics.sampled_at_frame -lt $restored.gpu_pass_diagnostics.sampled_at_frame) -Message "GPU diagnostic checkpoint frames are not strictly monotone."
$expectedMeasurementsPerCheckpoint = 2 + [int]$transmissionExpected + [int]$bloomExpected
Assert-Condition -Condition ($expectedGpuMeasurementCount -eq (3 * $expectedMeasurementsPerCheckpoint)) -Message "Unexpected count of required GPU pass measurements."

$gradeCases = @(
    [pscustomobject]@{ Checkpoint = $clear; Grade = $weather.post_processing.clear_grade; Label = "clear" },
    [pscustomobject]@{ Checkpoint = $rain; Grade = $weather.post_processing.rain_grade; Label = "rain" },
    [pscustomobject]@{ Checkpoint = $restored; Grade = $weather.post_processing.clear_grade; Label = "restored" }
)
foreach ($entry in $gradeCases) {
    $checkpoint = $entry.Checkpoint
    $grade = $entry.Grade
    $label = $entry.Label
    Assert-NearlyEqual -Actual $checkpoint.mapped_exposure_ev -Expected $grade.exposure_ev -Field "$label exposure"
    Assert-NearlyEqual -Actual $checkpoint.mapped_temperature -Expected ($grade.temperature * 0.1) -Field "$label temperature"
    Assert-NearlyEqual -Actual $checkpoint.mapped_tint -Expected ($grade.tint * 0.1) -Field "$label tint"
    Assert-NearlyEqual -Actual $checkpoint.mapped_post_saturation -Expected $grade.saturation -Field "$label saturation"
    Assert-NearlyEqual -Actual $checkpoint.mapped_contrast -Expected $grade.contrast -Field "$label contrast"
    Assert-NearlyEqual -Actual $checkpoint.mapped_gamma -Expected $grade.gamma -Field "$label gamma"
}

Add-Type -AssemblyName System.Drawing
$captures = @($ledger.captures)
Assert-Condition -Condition ($captures.Count -eq 29) -Message "Expected 29 PNG captures (5 fixed + 24 movement)."
$expectedCapturePaths = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
$expectedCapturePaths.Add("distant-clear", "captures/distant-clear.png")
$expectedCapturePaths.Add("human-clear", "captures/human-clear.png")
$expectedCapturePaths.Add("water-clear", "captures/water-clear.png")
$expectedCapturePaths.Add("water-rain", "captures/water-rain.png")
$expectedCapturePaths.Add("water-restored", "captures/water-restored.png")
for ($index = 0; $index -lt 24; $index++) {
    $expectedCapturePaths.Add(("movement-{0:D3}" -f $index), ("movement/frame-{0:D3}.png" -f $index))
}
$captureIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$capturePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$captureById = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
$artifacts = [Collections.Generic.List[object]]::new()
foreach ($capture in $captures) {
    $captureId = [string]$capture.id
    Assert-Condition -Condition ($captureIds.Add($captureId)) -Message "Duplicate capture id $captureId."
    Assert-Condition -Condition ($expectedCapturePaths.ContainsKey($captureId)) -Message "Unexpected capture id $captureId."
    Assert-Condition -Condition ([string]::Equals([string]$capture.relative_path, $expectedCapturePaths[$captureId], [StringComparison]::Ordinal)) -Message "Capture $captureId does not use its canonical relative path."
    $captureById[$captureId] = $capture
    Assert-Condition -Condition (Test-PortableRelativePath -Path ([string]$capture.relative_path)) -Message "Unsafe capture path $($capture.relative_path)."
    Assert-Condition -Condition ($capturePaths.Add([string]$capture.relative_path)) -Message "Duplicate capture path $($capture.relative_path)."
    Assert-Condition -Condition ($capture.physical_width -eq 1920 -and $capture.physical_height -eq 1080) -Message "Ledger dimensions differ from 1920x1080 for $($capture.id)."
    Assert-Condition -Condition ($capture.verified_frame -ge ($capture.requested_frame + 6)) -Message "Capture $captureId was verified before the six-frame completion window."
    Assert-Condition -Condition (@($capture.camera.translation_m).Count -eq 3 -and @($capture.camera.forward).Count -eq 3) -Message "Capture $captureId has incomplete camera vectors."
    foreach ($value in @($capture.camera.translation_m) + @($capture.camera.forward) + @($capture.camera.vertical_fov_degrees)) {
        Assert-Condition -Condition (Test-FiniteNumber -Value $value) -Message "Capture $captureId contains a non-finite camera value."
    }
    $absolutePath = Join-Path $evidencePath (($capture.relative_path -replace "/", [IO.Path]::DirectorySeparatorChar))
    Assert-Condition -Condition ([IO.File]::Exists($absolutePath)) -Message "Missing PNG $absolutePath."
    $file = Get-Item -LiteralPath $absolutePath
    Assert-Condition -Condition ($file.Length -gt 0 -and $file.Length -eq $capture.bytes) -Message "PNG size mismatch for $($capture.id)."
    $image = Get-PngObservation -Path $absolutePath
    Assert-Condition -Condition ($image.Width -eq 1920 -and $image.Height -eq 1080) -Message "PNG dimensions differ from 1920x1080 for $($capture.id)."
    Assert-Condition -Condition ($image.SampledUniqueColors -ge 4) -Message "PNG appears blank or uniform for $($capture.id)."
    if ($captureId.StartsWith("movement-", [StringComparison]::Ordinal)) {
        Assert-Condition -Condition ($capture.camera.forge_player_camera -eq $true) -Message "Movement capture $($capture.id) did not use ForgePlayerCamera."
        $movementMatch = [regex]::Match($captureId, '^movement-(?<index>\d{3})$')
        Assert-Condition -Condition $movementMatch.Success -Message "Movement capture id is not canonical: $captureId."
        $movementIndex = [int]$movementMatch.Groups["index"].Value
        Assert-Condition -Condition ($movementIndex -ge 0 -and $movementIndex -lt 24) -Message "Movement capture index is out of bounds: $captureId."
        $checkpoint = $movementCheckpoints[$movementIndex]
        Assert-Condition -Condition ($capture.movement_fixed_tick -eq $checkpoint.fixed_tick) -Message "Movement capture $($capture.id) is not linked to its fixed tick."
        for ($axis = 0; $axis -lt 3; $axis++) {
            Assert-NearlyEqual -Actual $capture.camera.translation_m[$axis] -Expected $checkpoint.camera.translation_m[$axis] -Field "$($capture.id) camera translation axis $axis" -Tolerance 0.00001
            Assert-NearlyEqual -Actual $capture.camera.forward[$axis] -Expected $checkpoint.camera.forward[$axis] -Field "$($capture.id) camera forward axis $axis" -Tolerance 0.000001
        }
        Assert-NearlyEqual -Actual $capture.camera.vertical_fov_degrees -Expected $checkpoint.camera.vertical_fov_degrees -Field "$($capture.id) camera FOV" -Tolerance 0.00001
    }
    $artifacts.Add([pscustomobject]@{
        id = [string]$capture.id
        relative_path = [string]$capture.relative_path
        bytes = $file.Length
        sha256 = (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash.ToLowerInvariant()
        sampled_unique_colors = $image.SampledUniqueColors
    })
}

$expectedCaptureIds = @("distant-clear", "human-clear", "water-clear", "water-rain", "water-restored")
for ($index = 0; $index -lt 24; $index++) {
    $expectedCaptureIds += "movement-{0:D3}" -f $index
}
foreach ($expectedCaptureId in $expectedCaptureIds) {
    Assert-Condition -Condition ($captureIds.Contains($expectedCaptureId)) -Message "Missing canonical capture $expectedCaptureId."
}
foreach ($weatherCaptureId in @("distant-clear", "human-clear", "water-clear", "water-rain", "water-restored")) {
    Assert-Condition -Condition ($captureById[$weatherCaptureId].requested_frame -eq $checkpointById[$weatherCaptureId].requested_frame) -Message "Weather checkpoint $weatherCaptureId is not linked to its capture request frame."
}
foreach ($waterCaptureId in @("water-rain", "water-restored")) {
    for ($axis = 0; $axis -lt 3; $axis++) {
        Assert-NearlyEqual -Actual $captureById[$waterCaptureId].camera.translation_m[$axis] -Expected $captureById["water-clear"].camera.translation_m[$axis] -Field "$waterCaptureId water camera translation axis $axis" -Tolerance 0.000001
        Assert-NearlyEqual -Actual $captureById[$waterCaptureId].camera.forward[$axis] -Expected $captureById["water-clear"].camera.forward[$axis] -Field "$waterCaptureId water camera forward axis $axis" -Tolerance 0.000001
    }
    Assert-NearlyEqual -Actual $captureById[$waterCaptureId].camera.vertical_fov_degrees -Expected $captureById["water-clear"].camera.vertical_fov_degrees -Field "$waterCaptureId water camera FOV" -Tolerance 0.000001
}

$artifactById = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
foreach ($artifact in $artifacts) {
    $artifactById[$artifact.id] = $artifact
}
foreach ($id in @("distant-clear", "human-clear", "water-clear", "water-rain", "water-restored", "movement-000", "movement-023")) {
    Assert-Condition -Condition ($artifactById.ContainsKey($id)) -Message "Missing required capture $id."
}
Assert-Condition -Condition ($artifactById["water-clear"].sha256 -ne $artifactById["water-rain"].sha256) -Message "Clear and rain water captures are byte-identical."
Assert-Condition -Condition ($artifactById["water-rain"].sha256 -ne $artifactById["water-restored"].sha256) -Message "Rain and restored water captures are byte-identical."
Assert-Condition -Condition ($artifactById["movement-000"].sha256 -ne $artifactById["movement-023"].sha256) -Message "Movement endpoints are byte-identical."

$videoEvidence = $null
if (-not $SkipVideo) {
    if ([string]::IsNullOrWhiteSpace($FfmpegPath)) {
        $ffmpegCommand = Get-Command ffmpeg -ErrorAction SilentlyContinue
        Assert-Condition -Condition ($null -ne $ffmpegCommand) -Message "ffmpeg was not found; pass -FfmpegPath or -SkipVideo."
        $FfmpegPath = $ffmpegCommand.Source
    }
    else {
        $FfmpegPath = (Resolve-Path -LiteralPath $FfmpegPath).Path
    }
    $ffprobePath = Join-Path (Split-Path -Parent $FfmpegPath) "ffprobe.exe"
    if (-not [IO.File]::Exists($ffprobePath)) {
        $ffprobeCommand = Get-Command ffprobe -ErrorAction SilentlyContinue
        Assert-Condition -Condition ($null -ne $ffprobeCommand) -Message "ffprobe was not found beside ffmpeg or on PATH."
        $ffprobePath = $ffprobeCommand.Source
    }
    $movementPattern = Join-Path $evidencePath "movement/frame-%03d.png"
    $videoPath = Join-Path $evidencePath "gate1-movement.mp4"
    $ffmpegResult = Invoke-BoundedProcess `
        -FilePath $FfmpegPath `
        -ArgumentList @("-hide_banner", "-loglevel", "error", "-y", "-framerate", "8", "-i", $movementPattern, "-c:v", "libx264", "-pix_fmt", "yuv420p", "-movflags", "+faststart", $videoPath) `
        -TimeoutSeconds 60 `
        -Label "ffmpeg Gate 1 movement encoding" `
        -StdoutPath (Join-Path $evidencePath "ffmpeg.stdout.log") `
        -StderrPath (Join-Path $evidencePath "ffmpeg.stderr.log")
    if ($ffmpegResult.ExitCode -ne 0) {
        throw "ffmpeg failed with exit code $($ffmpegResult.ExitCode)."
    }
    $video = Get-Item -LiteralPath $videoPath
    Assert-Condition -Condition ($video.Length -gt 0) -Message "ffmpeg produced an empty MP4."
    $ffprobeResult = Invoke-BoundedProcess `
        -FilePath $ffprobePath `
        -ArgumentList @("-v", "error", "-count_frames", "-select_streams", "v:0", "-show_entries", "stream=codec_name,width,height,nb_read_frames,avg_frame_rate", "-show_entries", "format=duration", "-of", "json", $videoPath) `
        -TimeoutSeconds 30 `
        -Label "ffprobe Gate 1 movement verification" `
        -StdoutPath (Join-Path $evidencePath "ffprobe.stdout.json") `
        -StderrPath (Join-Path $evidencePath "ffprobe.stderr.log")
    if ($ffprobeResult.ExitCode -ne 0) {
        throw "ffprobe failed with exit code $($ffprobeResult.ExitCode)."
    }
    $probe = $ffprobeResult.Stdout | ConvertFrom-Json -Depth 16
    $stream = @($probe.streams)[0]
    Assert-Condition -Condition ($stream.codec_name -eq "h264") -Message "Movement video codec is not H.264."
    Assert-Condition -Condition ($stream.width -eq 1920 -and $stream.height -eq 1080) -Message "Movement video dimensions differ from 1920x1080."
    Assert-Condition -Condition ([int]$stream.nb_read_frames -eq 24) -Message "Movement video does not contain exactly 24 decoded frames."
    Assert-Condition -Condition ($stream.avg_frame_rate -eq "8/1") -Message "Movement video rate is not exactly 8 FPS."
    Assert-NearlyEqual -Actual ([double]$probe.format.duration) -Expected 3.0 -Field "movement video duration" -Tolerance 0.000001
    $videoEvidence = [pscustomobject]@{
        relative_path = "gate1-movement.mp4"
        bytes = $video.Length
        sha256 = (Get-FileHash -LiteralPath $videoPath -Algorithm SHA256).Hash.ToLowerInvariant()
        source_frame_count = 24
        framerate = 8
        codec = [string]$stream.codec_name
        width = [int]$stream.width
        height = [int]$stream.height
        duration_seconds = [double]$probe.format.duration
    }
}

$gitCommitAfter = (& git -C $repositoryPath rev-parse HEAD).Trim()
$gitTreeAfter = (& git -C $repositoryPath show -s --format=%T HEAD).Trim()
$gitStatusAfter = @(& git -C $repositoryPath status --porcelain=v1 --untracked-files=all)
Assert-Condition -Condition ($gitCommitAfter -eq $gitCommit) -Message "Repository HEAD changed during acquisition."
Assert-Condition -Condition ($gitTreeAfter -eq $gitTree) -Message "Repository tree changed during acquisition."
Assert-Condition -Condition ($gitStatusAfter.Count -eq 0) -Message "Repository worktree changed during acquisition."
$runtimeSha256 = $null
if ($null -ne $runtimeFullPath) {
    $runtimeSha256 = (Get-FileHash -LiteralPath $runtimeFullPath -Algorithm SHA256).Hash.ToLowerInvariant()
}
$validationStatus = if ($SkipVideo) {
    "automated_frames_and_native_scene_gpu_pass_diagnostics_sampled_pending_video_browser_water_isolation_wetness_stutter_human_and_target_gpu_qualification"
}
else {
    "automated_artifacts_and_native_scene_gpu_pass_diagnostics_sampled_pending_browser_water_isolation_wetness_stutter_human_and_target_gpu_qualification"
}
$validation = [ordered]@{
    schema = "forge.gate1-visual-proof-validation"
    schema_version = 3
    status = $validationStatus
    repository_commit = $gitCommit
    repository_tree = $gitTree
    runtime_sha256 = $runtimeSha256
    runtime = $runtimeLaunch
    runtime_logs = [ordered]@{
        stdout = [ordered]@{
            relative_path = "runtime.stdout.log"
            bytes = (Get-Item -LiteralPath $stdoutPath).Length
            sha256 = (Get-FileHash -LiteralPath $stdoutPath -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        stderr = [ordered]@{
            relative_path = "runtime.stderr.log"
            bytes = (Get-Item -LiteralPath $stderrPath).Length
            sha256 = (Get-FileHash -LiteralPath $stderrPath -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        disallowed_entries = 0
    }
    ledger_sha256 = (Get-FileHash -LiteralPath $ledgerPath -Algorithm SHA256).Hash.ToLowerInvariant()
    captures = @($artifacts)
    video = $videoEvidence
    checks = [ordered]@{
        clear_rain_clear_same_process = $true
        exact_wetness_restoration = $true
        bounded_grade_endpoints = $true
        fixed_quality = $true
        exact_commit_embedded = $true
        clean_worktree_before_and_after = $true
        bevy_asset_root_is_clean_repository = ($null -ne $runtimeLaunch)
        governed_assets_referenced_in_place = ($null -ne $runtimeLaunch)
        governed_assets_copied_to_release_directory = if ($null -ne $runtimeLaunch) { $false } else { $null }
        no_vsync_requested = $true
        gpu_identity_recorded = $true
        native_scene_gpu_pass_diagnostics_sampled = $true
        native_gpu_pass_measurements = $expectedGpuMeasurementCount
        gpu_measurements_synced_during_settle_window = $true
        render_frame_identity_observed = $false
        water_draw_or_material_isolated = $false
        runtime_warning_error_panic_entries = 0
        png_1920x1080_nonuniform = $true
        gameplay_controller_exercised = $true
        gameplay_fixed_tick_checkpoints = $true
        video_requested = (-not $SkipVideo)
        video_acquired = ($null -ne $videoEvidence)
        real_time_stutter_qualified = $false
        cadle_assets_used = $false
        weather_gpu_execution_observed = $false
        human_review_performed = $false
        gate1_routes_qualified = $false
    }
}
$validationPath = Join-Path $evidencePath "gate1-visual-proof.validation.json"
[IO.File]::WriteAllText(
    $validationPath,
    ($validation | ConvertTo-Json -Depth 64),
    [Text.UTF8Encoding]::new($false)
)

if ($SkipVideo) {
    Write-Output ("FORGE_GATE1_VISUAL_FRAMES_ONLY captures={0} native_scene_gpu_pass_diagnostics_sampled=true gpu_measurements={1} water_isolated=false video_requested=false video_acquired=false gameplay_controller=true weather_gpu_execution=false stutter_qualified=false qualification=pending_video_browser_water_isolation_wetness_stutter_human_target_gpu" -f $captures.Count, $expectedGpuMeasurementCount)
}
else {
    Write-Output ("FORGE_GATE1_VISUAL_PROOF_AUTOMATED_PASS captures={0} native_scene_gpu_pass_diagnostics_sampled=true gpu_measurements={1} water_isolated=false video_requested=true video_acquired=true gameplay_controller=true weather_gpu_execution=false stutter_qualified=false human_review=false routes_qualified=false" -f $captures.Count, $expectedGpuMeasurementCount)
}
