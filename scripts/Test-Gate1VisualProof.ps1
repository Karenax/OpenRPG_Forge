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

    [switch]$SkipVideo
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

    Assert-Condition -Condition ([Math]::Abs($Actual - $Expected) -le $Tolerance) -Message (
        "{0} is {1}, expected {2} (+/- {3})." -f $Field, $Actual, $Expected, $Tolerance
    )
}

$evidencePath = Get-FullPathWithoutResolving -Path $EvidenceRoot
$repositoryPath = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$runtimeFullPath = $null

if (-not [string]::IsNullOrWhiteSpace($RuntimePath)) {
    $runtimeFullPath = (Resolve-Path -LiteralPath $RuntimePath).Path
    Assert-Condition -Condition ([IO.File]::Exists($runtimeFullPath)) -Message "RuntimePath must name a file."
    $runtimeArguments = @(
        "--visual-valley",
        "--quality", $Quality,
        "--gate1-visual-proof", $evidencePath
    )
    if ($NoVsync) {
        $runtimeArguments += "--no-vsync"
    }
    & $runtimeFullPath @runtimeArguments
    if ($LASTEXITCODE -ne 0) {
        throw "forge_runtime Gate 1 visual proof failed with exit code $LASTEXITCODE."
    }
}

Assert-Condition -Condition ([IO.Directory]::Exists($evidencePath)) -Message "EvidenceRoot does not exist: $evidencePath"
$ledgerPath = Join-Path $evidencePath "gate1-visual-proof.json"
Assert-Condition -Condition ([IO.File]::Exists($ledgerPath)) -Message "Missing Gate 1 visual proof ledger: $ledgerPath"
$ledger = [IO.File]::ReadAllText($ledgerPath) | ConvertFrom-Json -Depth 64

Assert-Condition -Condition ($ledger.schema -eq "forge.gate1-visual-proof") -Message "Unexpected ledger schema."
Assert-Condition -Condition ($ledger.schema_version -eq 1) -Message "Unexpected ledger schema version."
Assert-Condition -Condition ($ledger.status -eq "automated_capture_complete_pending_qualification") -Message "The acquisition did not finish cleanly."
Assert-Condition -Condition ($ledger.timeline.same_process -eq $true) -Message "The clear-rain-clear timeline is not declared single-process."
Assert-Condition -Condition (($ledger.timeline.requested_states -join ",") -eq "clear,rain,clear") -Message "Unexpected weather transition sequence."
Assert-Condition -Condition ($ledger.sources.cadle_assets_used -eq $false) -Message "Cadle assets must never be used by the Forge proof."
Assert-Condition -Condition ($ledger.pipeline_warmup.status -eq "ready") -Message "Pipelines were not ready when the proof completed."
Assert-Condition -Condition ($ledger.quality.selection.mode -eq "fixed") -Message "The proof must use one fixed quality tier."
Assert-Condition -Condition ($ledger.quality.adaptation_enabled -eq $false) -Message "Quality adaptation must remain disabled during the proof."
Assert-Condition -Condition ($ledger.quality.transition_count -eq 0) -Message "The fixed quality tier changed during the proof."
Assert-Condition -Condition ($ledger.qualification.rendered_png_artifacts_acquired -eq $true) -Message "PNG acquisition is not recorded."
Assert-Condition -Condition ($ledger.qualification.weather_cpu_ecs_projection_observed -eq $true) -Message "Weather CPU/ECS evidence is absent."
Assert-Condition -Condition ($ledger.qualification.weather_gpu_execution_observed -eq $false) -Message "This harness must not invent weather GPU execution evidence."
Assert-Condition -Condition ($ledger.qualification.human_review_performed -eq $false) -Message "Automated validation cannot claim human review."
Assert-Condition -Condition ($ledger.qualification.visual_quality_accepted -eq $false) -Message "Automated validation cannot accept visual quality."
Assert-Condition -Condition ($ledger.qualification.gate1_routes_qualified -eq $false) -Message "Artifact acquisition alone cannot close Gate 1 routes."

$routeIds = @($ledger.routes | ForEach-Object { $_.route })
foreach ($route in @("R1-025", "R1-026", "R1-046", "R1-047", "R1-048")) {
    Assert-Condition -Condition ($routeIds -contains $route) -Message "Missing route evidence entry $route."
}

$weatherPath = Join-Path $repositoryPath "examples/visual_valley/weather.v1.json"
$weather = [IO.File]::ReadAllText($weatherPath) | ConvertFrom-Json -Depth 32
$checkpoints = @($ledger.timeline.checkpoints)
Assert-Condition -Condition ($checkpoints.Count -eq 5) -Message "Expected five fixed-view weather checkpoints."
$checkpointById = @{}
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
$captureIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$capturePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$artifacts = [Collections.Generic.List[object]]::new()
foreach ($capture in $captures) {
    Assert-Condition -Condition ($captureIds.Add([string]$capture.id)) -Message "Duplicate capture id $($capture.id)."
    Assert-Condition -Condition (Test-PortableRelativePath -Path ([string]$capture.relative_path)) -Message "Unsafe capture path $($capture.relative_path)."
    Assert-Condition -Condition ($capturePaths.Add([string]$capture.relative_path)) -Message "Duplicate capture path $($capture.relative_path)."
    Assert-Condition -Condition ($capture.physical_width -eq 1920 -and $capture.physical_height -eq 1080) -Message "Ledger dimensions differ from 1920x1080 for $($capture.id)."
    $absolutePath = Join-Path $evidencePath (($capture.relative_path -replace "/", [IO.Path]::DirectorySeparatorChar))
    Assert-Condition -Condition ([IO.File]::Exists($absolutePath)) -Message "Missing PNG $absolutePath."
    $file = Get-Item -LiteralPath $absolutePath
    Assert-Condition -Condition ($file.Length -gt 0 -and $file.Length -eq $capture.bytes) -Message "PNG size mismatch for $($capture.id)."
    $image = Get-PngObservation -Path $absolutePath
    Assert-Condition -Condition ($image.Width -eq 1920 -and $image.Height -eq 1080) -Message "PNG dimensions differ from 1920x1080 for $($capture.id)."
    Assert-Condition -Condition ($image.SampledUniqueColors -ge 4) -Message "PNG appears blank or uniform for $($capture.id)."
    $artifacts.Add([pscustomobject]@{
        id = [string]$capture.id
        relative_path = [string]$capture.relative_path
        bytes = $file.Length
        sha256 = (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash.ToLowerInvariant()
        sampled_unique_colors = $image.SampledUniqueColors
    })
}

$artifactById = @{}
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
    $movementPattern = Join-Path $evidencePath "movement/frame-%03d.png"
    $videoPath = Join-Path $evidencePath "gate1-movement.mp4"
    & $FfmpegPath -hide_banner -loglevel error -y -framerate 8 -i $movementPattern -c:v libx264 -pix_fmt yuv420p -movflags +faststart $videoPath
    if ($LASTEXITCODE -ne 0) {
        throw "ffmpeg failed with exit code $LASTEXITCODE."
    }
    $video = Get-Item -LiteralPath $videoPath
    Assert-Condition -Condition ($video.Length -gt 0) -Message "ffmpeg produced an empty MP4."
    $videoEvidence = [pscustomobject]@{
        relative_path = "gate1-movement.mp4"
        bytes = $video.Length
        sha256 = (Get-FileHash -LiteralPath $videoPath -Algorithm SHA256).Hash.ToLowerInvariant()
        source_frame_count = 24
        framerate = 8
    }
}

$gitCommit = (& git -C $repositoryPath rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Cannot resolve the repository commit."
}
$gitTree = (& git -C $repositoryPath show -s --format=%T HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Cannot resolve the repository tree."
}
$runtimeSha256 = $null
if ($null -ne $runtimeFullPath) {
    $runtimeSha256 = (Get-FileHash -LiteralPath $runtimeFullPath -Algorithm SHA256).Hash.ToLowerInvariant()
}
$validation = [ordered]@{
    schema = "forge.gate1-visual-proof-validation"
    schema_version = 1
    status = "automated_artifacts_valid_pending_human_and_gpu_qualification"
    repository_commit = $gitCommit
    repository_tree = $gitTree
    runtime_sha256 = $runtimeSha256
    ledger_sha256 = (Get-FileHash -LiteralPath $ledgerPath -Algorithm SHA256).Hash.ToLowerInvariant()
    captures = @($artifacts)
    video = $videoEvidence
    checks = [ordered]@{
        clear_rain_clear_same_process = $true
        exact_wetness_restoration = $true
        bounded_grade_endpoints = $true
        fixed_quality = $true
        png_1920x1080_nonuniform = $true
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

Write-Output ("FORGE_GATE1_VISUAL_PROOF_AUTOMATED_PASS captures={0} video={1} gpu_execution=false human_review=false routes_qualified=false" -f $captures.Count, (-not $SkipVideo))
