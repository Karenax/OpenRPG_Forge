[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot,

    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,

    [Parameter(Mandatory = $true)]
    [string]$CargoTargetDirectory,

    [Parameter(Mandatory = $true)]
    [string]$RenderDocRoot,

    [ValidateSet("Vulkan")]
    [string]$Backend = "Vulkan",

    [ValidateRange(2, 4)]
    [int]$Repetitions = 2
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$requiredRenderDocVersion = "1.46"
$requiredRenderDocCommit = "e4bd23b671d3d5a747ff5221dbe08a63eb6ca200"
$layerReadySentence = "The RenderDoc vulkan layer appears to be correctly registered."
$readyFile = "gate1-renderdoc.ready.json"
$runtimeLedgerFile = "gate1-renderdoc.runtime.json"
$actionFile = "gate1-renderdoc.actions.json"
$analysisFile = "gate1-renderdoc.analysis.json"
$adapterFile = "gate1-renderdoc.adapter.json"
$adapterFailureFile = "gate1-renderdoc.adapter.failure.json"

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

    return [IO.Path]::GetFullPath($Path).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar
    )
}

function Test-PathInsideRoot {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Candidate
    )

    $rootPath = Get-FullPathWithoutResolving -Path $Root
    $candidatePath = Get-FullPathWithoutResolving -Path $Candidate
    if ([string]::Equals($rootPath, $candidatePath, [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }
    $prefix = $rootPath + [IO.Path]::DirectorySeparatorChar
    return $candidatePath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoReparsePointInExistingAncestors {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = Get-FullPathWithoutResolving -Path $Path
    $root = [IO.Path]::GetPathRoot($fullPath)
    Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($root)) -Message "Path has no filesystem root: $fullPath"
    $relative = $fullPath.Substring($root.Length)
    $current = $root
    foreach ($component in $relative.Split(
            @([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar),
            [StringSplitOptions]::RemoveEmptyEntries
        )) {
        $current = Join-Path $current $component
        if (-not [IO.Directory]::Exists($current) -and -not [IO.File]::Exists($current)) {
            break
        }
        $item = Get-Item -LiteralPath $current -Force
        Assert-Condition -Condition (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) -Message "Reparse points are forbidden in governed paths: $current"
    }
}

function Initialize-EmptyDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    Assert-Condition -Condition (-not [IO.File]::Exists($Path)) -Message "$Label must be a directory, not a file: $Path"
    Assert-NoReparsePointInExistingAncestors -Path $Path
    if ([IO.Directory]::Exists($Path)) {
        $entries = @([IO.Directory]::EnumerateFileSystemEntries($Path))
        Assert-Condition -Condition ($entries.Count -eq 0) -Message "$Label must be absent or empty: $Path"
    }
    else {
        [void][IO.Directory]::CreateDirectory($Path)
    }
    Assert-NoReparsePointInExistingAncestors -Path $Path
}

function Write-TextAtomically {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Text
    )

    Assert-Condition -Condition (-not [IO.File]::Exists($Path)) -Message "Refusing to replace existing artifact: $Path"
    $temporary = "$Path.tmp-$([Guid]::NewGuid().ToString('N'))"
    $stream = $null
    $writer = $null
    try {
        $stream = [IO.FileStream]::new(
            $temporary,
            [IO.FileMode]::CreateNew,
            [IO.FileAccess]::Write,
            [IO.FileShare]::None
        )
        $writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false), 4096, $true)
        $writer.Write($Text)
        $writer.Flush()
        $stream.Flush($true)
        $writer.Dispose()
        $writer = $null
        $stream.Dispose()
        $stream = $null
        [IO.File]::Move($temporary, $Path, $false)
    }
    finally {
        if ($null -ne $writer) {
            $writer.Dispose()
        }
        if ($null -ne $stream) {
            $stream.Dispose()
        }
        if ([IO.File]::Exists($temporary)) {
            [IO.File]::Delete($temporary)
        }
    }
}

function Write-JsonAtomically {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Value
    )

    $json = ($Value | ConvertTo-Json -Depth 100) + [Environment]::NewLine
    Write-TextAtomically -Path $Path -Text $json
}

function Read-JsonDocument {
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-Condition -Condition ([IO.File]::Exists($Path)) -Message "Required JSON artifact is absent: $Path"
    return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json -Depth 100
}

function Get-LowerSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-Condition -Condition ([IO.File]::Exists($Path)) -Message "Cannot hash absent file: $Path"
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-ExactProperties {
    param(
        [Parameter(Mandatory = $true)][object]$Value,
        [Parameter(Mandatory = $true)][string[]]$Expected,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $actualNames = @($Value.PSObject.Properties.Name | Sort-Object)
    $expectedNames = @($Expected | Sort-Object)
    Assert-Condition -Condition (($actualNames -join "`n") -ceq ($expectedNames -join "`n")) -Message "$Path has an unexpected field set: $($actualNames -join ', ')"
}

function Assert-NonNegativeInteger {
    param(
        [AllowNull()][object]$Value,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $isIntegerType = $Value -is [byte] -or $Value -is [sbyte] -or
        $Value -is [int16] -or $Value -is [uint16] -or
        $Value -is [int32] -or $Value -is [uint32] -or
        $Value -is [int64] -or $Value -is [uint64]
    Assert-Condition -Condition ($isIntegerType -and [decimal]$Value -ge 0) -Message "$Path must be a non-negative JSON integer."
}

function Invoke-CapturedCommand {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$ArgumentList = @(),
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [ValidateRange(1, 3600)][int]$TimeoutSeconds = 120
    )

    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $FilePath
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $ArgumentList) {
        [void]$start.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        Assert-Condition -Condition $process.Start() -Message "Could not start governed command: $FilePath"
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $completed = $process.WaitForExit($TimeoutSeconds * 1000)
        if (-not $completed) {
            try {
                $process.Kill($true)
            }
            catch {
            }
            $process.WaitForExit()
            throw "Governed command timed out after $TimeoutSeconds seconds: $FilePath"
        }
        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            Stdout = $stdoutTask.GetAwaiter().GetResult()
            Stderr = $stderrTask.GetAwaiter().GetResult()
        }
    }
    finally {
        $process.Dispose()
    }
}

function Assert-CommandSucceeded {
    param(
        [Parameter(Mandatory = $true)][object]$Result,
        [Parameter(Mandatory = $true)][string]$Label
    )

    Assert-Condition -Condition ($Result.ExitCode -eq 0) -Message "$Label failed with exit code $($Result.ExitCode). stdout=$($Result.Stdout) stderr=$($Result.Stderr)"
}

function Set-ProcessEnvironment {
    param(
        [Parameter(Mandatory = $true)][Collections.Generic.Dictionary[string, object]]$Saved,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Value
    )

    if (-not $Saved.ContainsKey($Name)) {
        $Saved[$Name] = [Environment]::GetEnvironmentVariable($Name, "Process")
    }
    [Environment]::SetEnvironmentVariable($Name, $Value, "Process")
}

function Restore-ProcessEnvironment {
    param([Parameter(Mandatory = $true)][Collections.Generic.Dictionary[string, object]]$Saved)

    foreach ($entry in $Saved.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, "Process")
    }
}

function Assert-RenderDocAnalysis {
    param(
        [Parameter(Mandatory = $true)][object]$Analysis,
        [Parameter(Mandatory = $true)][string]$Path
    )

    Assert-ExactProperties -Value $Analysis -Expected @("schema", "status", "capture", "metrics") -Path $Path
    Assert-Condition -Condition ($Analysis.schema -ceq "forge-gate1-renderdoc-analysis/v1") -Message "$Path schema mismatch."
    Assert-Condition -Condition ($Analysis.status -ceq "PASS") -Message "$Path did not PASS."
    Assert-ExactProperties -Value $Analysis.capture -Expected @(
        "captured_frame_count",
        "present_count",
        "local_replay_support",
        "pipeline_type",
        "degraded"
    ) -Path "$Path.capture"
    Assert-Condition -Condition ($Analysis.capture.captured_frame_count -eq 1 -and $Analysis.capture.present_count -eq 1) -Message "$Path capture is not exactly one frame and one Present."
    Assert-Condition -Condition ($Analysis.capture.local_replay_support -ceq "Supported" -and $Analysis.capture.pipeline_type -ceq "Vulkan" -and $Analysis.capture.degraded -eq $false) -Message "$Path capture qualification mismatch."
    Assert-ExactProperties -Value $Analysis.metrics -Expected @(
        "draws",
        "instances",
        "triangles",
        "lines",
        "points",
        "draws_by_topology",
        "actions_visited"
    ) -Path "$Path.metrics"
    foreach ($field in @("draws", "instances", "triangles", "lines", "points", "actions_visited")) {
        Assert-NonNegativeInteger -Value $Analysis.metrics.$field -Path "$Path.metrics.$field"
    }
    Assert-ExactProperties -Value $Analysis.metrics.draws_by_topology -Expected @(
        "TriangleList",
        "LineList",
        "PointList"
    ) -Path "$Path.metrics.draws_by_topology"
    foreach ($field in @("TriangleList", "LineList", "PointList")) {
        Assert-NonNegativeInteger -Value $Analysis.metrics.draws_by_topology.$field -Path "$Path.metrics.draws_by_topology.$field"
    }
    $topologyDraws = [uint64]$Analysis.metrics.draws_by_topology.TriangleList +
        [uint64]$Analysis.metrics.draws_by_topology.LineList +
        [uint64]$Analysis.metrics.draws_by_topology.PointList
    Assert-Condition -Condition ($topologyDraws -eq [uint64]$Analysis.metrics.draws) -Message "$Path topology draw sum does not equal draws."
}

function Get-AnalysisSignature {
    param([Parameter(Mandatory = $true)][object]$Metrics)

    return @(
        $Metrics.draws,
        $Metrics.instances,
        $Metrics.triangles,
        $Metrics.lines,
        $Metrics.points,
        $Metrics.actions_visited,
        $Metrics.draws_by_topology.TriangleList,
        $Metrics.draws_by_topology.LineList,
        $Metrics.draws_by_topology.PointList
    ) -join ":"
}

Assert-Condition -Condition ([OperatingSystem]::IsWindows()) -Message "Gate 1 RenderDoc acquisition requires Windows."
Assert-Condition -Condition ($Backend -ceq "Vulkan") -Message "Only the Vulkan qualification backend is implemented."
Assert-Condition -Condition ([IO.Path]::IsPathRooted($OutputRoot)) -Message "OutputRoot must be an absolute external path."
Assert-Condition -Condition ([IO.Path]::IsPathRooted($CargoTargetDirectory)) -Message "CargoTargetDirectory must be an absolute external path."
Assert-Condition -Condition ([IO.Path]::IsPathRooted($RenderDocRoot)) -Message "RenderDocRoot must be an absolute path."

$repositoryPath = (Resolve-Path -LiteralPath $RepoRoot).Path.TrimEnd('\')
$outputPath = Get-FullPathWithoutResolving -Path $OutputRoot
$cargoTargetPath = Get-FullPathWithoutResolving -Path $CargoTargetDirectory
$renderDocPath = (Resolve-Path -LiteralPath $RenderDocRoot).Path.TrimEnd('\')
$qrenderdocPath = Join-Path $renderDocPath "qrenderdoc.exe"
$renderdoccmdPath = Join-Path $renderDocPath "renderdoccmd.exe"
$adapterPath = Join-Path $repositoryPath "scripts\renderdoc\gate1_renderdoc_qrenderdoc.py"
$modelPath = Join-Path $repositoryPath "scripts\renderdoc\gate1_renderdoc_model.py"
$validationPath = Join-Path $outputPath "gate1-renderdoc.validation.json"
$failurePath = Join-Path $outputPath "gate1-renderdoc.failure.json"

$gitTopLevelResult = Invoke-CapturedCommand -FilePath "git.exe" -ArgumentList @("-C", $repositoryPath, "rev-parse", "--show-toplevel") -WorkingDirectory $repositoryPath
Assert-CommandSucceeded -Result $gitTopLevelResult -Label "Git top-level lookup"
$gitTopLevel = Get-FullPathWithoutResolving -Path $gitTopLevelResult.Stdout.Trim()
Assert-Condition -Condition ([string]::Equals($repositoryPath, $gitTopLevel, [StringComparison]::OrdinalIgnoreCase)) -Message "RepoRoot must be the exact Git worktree root."

$gitCommitResult = Invoke-CapturedCommand -FilePath "git.exe" -ArgumentList @("-C", $repositoryPath, "rev-parse", "HEAD") -WorkingDirectory $repositoryPath
Assert-CommandSucceeded -Result $gitCommitResult -Label "Git commit lookup"
$gitCommit = $gitCommitResult.Stdout.Trim()
Assert-Condition -Condition ($gitCommit -cmatch '^[0-9a-f]{40}$') -Message "Cannot bind the acquisition to an exact Git commit."
$gitStatusResult = Invoke-CapturedCommand -FilePath "git.exe" -ArgumentList @("-C", $repositoryPath, "status", "--porcelain=v1", "--untracked-files=all") -WorkingDirectory $repositoryPath
Assert-CommandSucceeded -Result $gitStatusResult -Label "Git cleanliness check"
Assert-Condition -Condition ([string]::IsNullOrWhiteSpace($gitStatusResult.Stdout)) -Message "Gate 1 RenderDoc acquisition requires a clean exact-commit worktree."

$worktreeResult = Invoke-CapturedCommand -FilePath "git.exe" -ArgumentList @("-C", $repositoryPath, "worktree", "list", "--porcelain") -WorkingDirectory $repositoryPath
Assert-CommandSucceeded -Result $worktreeResult -Label "Git worktree enumeration"
$registeredWorktrees = @(
    $worktreeResult.Stdout -split "`r?`n" |
        Where-Object { $_.StartsWith("worktree ", [StringComparison]::Ordinal) } |
        ForEach-Object { Get-FullPathWithoutResolving -Path $_.Substring("worktree ".Length) }
)
Assert-Condition -Condition ($registeredWorktrees.Count -gt 0) -Message "Git reported no registered worktree."
foreach ($worktree in $registeredWorktrees) {
    Assert-Condition -Condition (-not (Test-PathInsideRoot -Root $worktree -Candidate $outputPath)) -Message "OutputRoot overlaps registered worktree $worktree."
    Assert-Condition -Condition (-not (Test-PathInsideRoot -Root $worktree -Candidate $cargoTargetPath)) -Message "CargoTargetDirectory overlaps registered worktree $worktree."
}
Assert-Condition -Condition (-not (Test-PathInsideRoot -Root $cargoTargetPath -Candidate $outputPath)) -Message "OutputRoot must not overlap CargoTargetDirectory."
Assert-Condition -Condition (-not (Test-PathInsideRoot -Root $outputPath -Candidate $cargoTargetPath)) -Message "CargoTargetDirectory must not overlap OutputRoot."
Initialize-EmptyDirectory -Path $outputPath -Label "OutputRoot"
Initialize-EmptyDirectory -Path $cargoTargetPath -Label "CargoTargetDirectory"
Assert-NoReparsePointInExistingAncestors -Path $renderDocPath

$environmentBefore = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
$results = [Collections.Generic.List[object]]::new()
$validation = $null
try {
    foreach ($path in @($qrenderdocPath, $renderdoccmdPath, $adapterPath, $modelPath)) {
        Assert-Condition -Condition ([IO.File]::Exists($path)) -Message "Required acquisition component is absent: $path"
    }
    $adapterSha256 = Get-LowerSha256 -Path $adapterPath
    $modelSha256 = Get-LowerSha256 -Path $modelPath
    $qrenderdocSha256 = Get-LowerSha256 -Path $qrenderdocPath
    $renderdoccmdSha256 = Get-LowerSha256 -Path $renderdoccmdPath

    foreach ($toolPath in @($qrenderdocPath, $renderdoccmdPath)) {
        $signature = Get-AuthenticodeSignature -LiteralPath $toolPath
        Assert-Condition -Condition ($signature.Status -eq [Management.Automation.SignatureStatus]::Valid) -Message "RenderDoc tool Authenticode signature is not valid: $toolPath ($($signature.Status))."
        Assert-Condition -Condition ($null -ne $signature.SignerCertificate) -Message "RenderDoc tool has no signing certificate: $toolPath"
    }

    $renderdoccmdVersionResult = Invoke-CapturedCommand -FilePath $renderdoccmdPath -ArgumentList @("version") -WorkingDirectory $renderDocPath
    Assert-CommandSucceeded -Result $renderdoccmdVersionResult -Label "renderdoccmd version"
    $renderdoccmdVersion = ($renderdoccmdVersionResult.Stdout + $renderdoccmdVersionResult.Stderr).Trim()
    Assert-Condition -Condition ($renderdoccmdVersion.Contains("renderdoccmd x64 v$requiredRenderDocVersion built from $requiredRenderDocCommit", [StringComparison]::Ordinal)) -Message "renderdoccmd is not the pinned RenderDoc build."
    $qrenderdocVersionResult = Invoke-CapturedCommand -FilePath $qrenderdocPath -ArgumentList @("--version") -WorkingDirectory $renderDocPath
    Assert-CommandSucceeded -Result $qrenderdocVersionResult -Label "qrenderdoc version"
    $qrenderdocVersion = ($qrenderdocVersionResult.Stdout + $qrenderdocVersionResult.Stderr).Trim()
    Assert-Condition -Condition ($qrenderdocVersion.Contains("QRenderDoc v$requiredRenderDocVersion ($requiredRenderDocCommit)", [StringComparison]::Ordinal)) -Message "qrenderdoc is not the pinned RenderDoc build."

    $layerResult = Invoke-CapturedCommand -FilePath $renderdoccmdPath -ArgumentList @("vulkanlayer", "--explain") -WorkingDirectory $renderDocPath
    Assert-CommandSucceeded -Result $layerResult -Label "RenderDoc Vulkan layer read-only check"
    $layerOutput = ($layerResult.Stdout + [Environment]::NewLine + $layerResult.Stderr).Trim()
    Assert-Condition -Condition $layerOutput.Contains($layerReadySentence, [StringComparison]::Ordinal) -Message "The RenderDoc Vulkan layer is not already correctly registered; this orchestrator will not alter host registration."

    $cargoCommand = Get-Command cargo -CommandType Application -ErrorAction Stop | Select-Object -First 1
    $cargoVersionResult = Invoke-CapturedCommand -FilePath $cargoCommand.Source -ArgumentList @("--version") -WorkingDirectory $repositoryPath
    Assert-CommandSucceeded -Result $cargoVersionResult -Label "Cargo version"
    $cargoVersion = $cargoVersionResult.Stdout.Trim()
    Assert-Condition -Condition ($cargoVersion -cmatch '^cargo 1\.98\.0(?: |$)') -Message "Gate 1 RenderDoc acquisition requires Cargo 1.98.0."

    Set-ProcessEnvironment -Saved $environmentBefore -Name "CARGO_TARGET_DIR" -Value $cargoTargetPath
    Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_SOURCE_COMMIT" -Value $gitCommit
    Set-ProcessEnvironment -Saved $environmentBefore -Name "WGPU_BACKEND" -Value "vulkan"
    Set-ProcessEnvironment -Saved $environmentBefore -Name "BEVY_ASSET_ROOT" -Value $repositoryPath
    Set-ProcessEnvironment -Saved $environmentBefore -Name "RUST_LOG" -Value "info,forge=info,wgpu_hal=warn,wgpu_core=warn,naga=warn"

    $buildResult = Invoke-CapturedCommand -FilePath $cargoCommand.Source -ArgumentList @("build", "--release", "-p", "forge_runtime", "--locked", "-j", "1") -WorkingDirectory $repositoryPath -TimeoutSeconds 1800
    Assert-CommandSucceeded -Result $buildResult -Label "Governed forge_runtime release build"
    $runtimePath = Join-Path $cargoTargetPath "release\forge_runtime.exe"
    Assert-Condition -Condition ([IO.File]::Exists($runtimePath)) -Message "Governed build did not produce forge_runtime.exe."
    $runtimeSha256 = Get-LowerSha256 -Path $runtimePath
    $cadleBuildPaths = @(
        [IO.Directory]::EnumerateFileSystemEntries($cargoTargetPath, "*cadle*", [IO.SearchOption]::AllDirectories)
    )
    Assert-Condition -Condition ($cadleBuildPaths.Count -eq 0) -Message "The external Cargo target contains a Cadle-named copied artifact."

    foreach ($weather in @("clear", "rain")) {
        for ($iteration = 1; $iteration -le $Repetitions; $iteration++) {
            $runName = "{0}-{1:d2}" -f $weather, $iteration
            $runPath = Join-Path $outputPath $runName
            Initialize-EmptyDirectory -Path $runPath -Label "RenderDoc run $runName"
            $nonce = [Guid]::NewGuid().ToString("N").ToLowerInvariant()
            $captureId = "gate1-renderdoc-$runName"

            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_RUNTIME" -Value $runtimePath
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_REPO_ROOT" -Value $repositoryPath
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_OUTPUT_DIR" -Value $runPath
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_NONCE" -Value $nonce
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_WEATHER" -Value $weather
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_SOURCE_COMMIT" -Value $gitCommit
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_QRENDERDOC_SHA256" -Value $qrenderdocSha256
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_RENDERDOCCMD_SHA256" -Value $renderdoccmdSha256
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_CAPTURE_ID" -Value $captureId

            $captureResult = Invoke-CapturedCommand -FilePath $qrenderdocPath -ArgumentList @("--python", $adapterPath) -WorkingDirectory $repositoryPath -TimeoutSeconds 240
            $logPath = Join-Path $runPath "qrenderdoc.log"
            $log = @(
                "exit_code=$($captureResult.ExitCode)",
                "--- stdout ---",
                $captureResult.Stdout,
                "--- stderr ---",
                $captureResult.Stderr
            ) -join [Environment]::NewLine
            Write-TextAtomically -Path $logPath -Text ($log + [Environment]::NewLine)
            Assert-CommandSucceeded -Result $captureResult -Label "qrenderdoc governed capture $runName"
            Assert-Condition -Condition (-not [IO.File]::Exists((Join-Path $runPath $adapterFailureFile))) -Message "qrenderdoc adapter reported failure for $runName."

            $readyPath = Join-Path $runPath $readyFile
            $ledgerPath = Join-Path $runPath $runtimeLedgerFile
            $actionsPath = Join-Path $runPath $actionFile
            $analysisPath = Join-Path $runPath $analysisFile
            $receiptPath = Join-Path $runPath $adapterFile
            $ready = Read-JsonDocument -Path $readyPath
            $ledger = Read-JsonDocument -Path $ledgerPath
            $actions = Read-JsonDocument -Path $actionsPath
            $analysis = Read-JsonDocument -Path $analysisPath
            $receipt = Read-JsonDocument -Path $receiptPath

            Assert-Condition -Condition ($ready.schema -ceq "forge.gate1-renderdoc-ready" -and $ready.schema_version -eq 1) -Message "$runName ready schema mismatch."
            Assert-Condition -Condition ($ready.status -ceq "ready_frozen_pending_external_capture_signal") -Message "$runName ready status mismatch."
            Assert-Condition -Condition ($ready.nonce -ceq $nonce -and $ready.source_commit -ceq $gitCommit) -Message "$runName ready binding mismatch."
            Assert-Condition -Condition ($ready.required_backend -ceq "Vulkan" -and $ready.required_renderdoc_version -ceq $requiredRenderDocVersion) -Message "$runName ready tool/backend mismatch."
            Assert-Condition -Condition ($ready.virtual_time_paused -eq $true -and $ready.freeze_settle_frames -eq 30) -Message "$runName ready freeze contract mismatch."
            Assert-Condition -Condition ($ready.frozen_state.expected_weather -ceq $weather) -Message "$runName ready weather mismatch."
            Assert-Condition -Condition ($ready.frozen_state.gpu.backend -ceq "Vulkan" -and -not [string]::IsNullOrWhiteSpace([string]$ready.frozen_state.gpu.name)) -Message "$runName ready GPU identity mismatch."
            Assert-Condition -Condition ($ready.frozen_state.sources.cadle_assets_used -eq $false) -Message "$runName did not exclude Cadle assets."

            Assert-Condition -Condition ($ledger.schema -ceq "forge.gate1-renderdoc-runtime" -and $ledger.schema_version -eq 1) -Message "$runName runtime ledger schema mismatch."
            Assert-Condition -Condition ($ledger.status -ceq "stable_capture_window_completed_pending_external_capture_analysis") -Message "$runName runtime ledger status mismatch."
            Assert-Condition -Condition ($ledger.nonce -ceq $nonce -and $ledger.source_commit -ceq $gitCommit -and $ledger.capture_id -ceq $captureId) -Message "$runName runtime ledger binding mismatch."
            Assert-Condition -Condition ($ledger.capture_window_frames -eq 120 -and $ledger.frozen_state_stable -eq $true) -Message "$runName runtime capture window is not stable and complete."
            Assert-Condition -Condition ($ledger.frozen_state_sha256 -ceq $ready.frozen_state_sha256) -Message "$runName frozen-state hash changed."

            Assert-Condition -Condition ($actions.schema -ceq "forge-gate1-renderdoc-actions/v1") -Message "$runName normalized action schema mismatch."
            Assert-Condition -Condition ($actions.capture.captured_frame_count -eq 1 -and $actions.capture.present_count -eq 1) -Message "$runName is not exactly one captured/presented frame."
            Assert-Condition -Condition ($actions.capture.local_replay_support -ceq "Supported" -and $actions.capture.pipeline_type -ceq "Vulkan" -and $actions.capture.degraded -eq $false) -Message "$runName replay contract mismatch."
            Assert-RenderDocAnalysis -Analysis $analysis -Path "$runName.analysis"

            Assert-Condition -Condition ($receipt.schema -ceq "forge-gate1-renderdoc-adapter/v1" -and $receipt.status -ceq "PASS") -Message "$runName adapter receipt did not PASS."
            Assert-Condition -Condition ($receipt.source_commit -ceq $gitCommit -and $receipt.nonce -ceq $nonce -and $receipt.weather -ceq $weather -and $receipt.capture_id -ceq $captureId) -Message "$runName adapter receipt binding mismatch."
            Assert-Condition -Condition ($receipt.qualification.runtime_state_qualified -eq $true -and $receipt.qualification.external_capture_observed -eq $true -and $receipt.qualification.renderdoc_analysis_completed -eq $true) -Message "$runName adapter qualification is incomplete."
            Assert-Condition -Condition ($receipt.qualification.renderdoc_metrics_are_fps_evidence -eq $false) -Message "$runName improperly labels RenderDoc metrics as FPS evidence."
            $capturePath = Join-Path $runPath $receipt.capture.path
            Assert-Condition -Condition ([IO.File]::Exists($capturePath)) -Message "$runName capture path is absent."
            Assert-Condition -Condition ((Get-LowerSha256 -Path $capturePath) -ceq $receipt.capture.sha256) -Message "$runName capture SHA-256 mismatch."
            Assert-Condition -Condition ((Get-LowerSha256 -Path $readyPath) -ceq $receipt.files.$readyFile) -Message "$runName ready SHA-256 mismatch."
            Assert-Condition -Condition ((Get-LowerSha256 -Path $ledgerPath) -ceq $receipt.files.$runtimeLedgerFile) -Message "$runName runtime ledger SHA-256 mismatch."
            Assert-Condition -Condition ((Get-LowerSha256 -Path $actionsPath) -ceq $receipt.files.$actionFile) -Message "$runName action document SHA-256 mismatch."
            Assert-Condition -Condition ((Get-LowerSha256 -Path $analysisPath) -ceq $receipt.files.$analysisFile) -Message "$runName analysis SHA-256 mismatch."

            $results.Add([pscustomobject]@{
                    run = $runName
                    weather = $weather
                    repetition = $iteration
                    nonce = $nonce
                    capture_id = $captureId
                    process_id = [uint32]$ready.process_id
                    gpu = $ready.frozen_state.gpu
                    frozen_state_sha256 = [string]$ready.frozen_state_sha256
                    source_hashes = $ready.frozen_state.sources
                    runtime_sha256 = $runtimeSha256
                    capture_file = "$runName/$($receipt.capture.path)"
                    capture_sha256 = [string]$receipt.capture.sha256
                    ready_sha256 = Get-LowerSha256 -Path $readyPath
                    ledger_sha256 = Get-LowerSha256 -Path $ledgerPath
                    actions_sha256 = Get-LowerSha256 -Path $actionsPath
                    analysis_sha256 = Get-LowerSha256 -Path $analysisPath
                    adapter_sha256 = Get-LowerSha256 -Path $receiptPath
                    metrics = $analysis.metrics
                })
        }
    }

    Assert-Condition -Condition ($results.Count -eq 2 * $Repetitions) -Message "The acquisition did not produce the complete clear/rain repetition matrix."
    Assert-Condition -Condition (@($results.nonce | Sort-Object -Unique).Count -eq $results.Count) -Message "Acquisition nonces are not unique."
    Assert-Condition -Condition (@($results.capture_id | Sort-Object -Unique).Count -eq $results.Count) -Message "Capture identifiers are not unique."
    Assert-Condition -Condition (@($results.capture_file | Sort-Object -Unique).Count -eq $results.Count) -Message "Capture artifact paths are not unique."
    Assert-Condition -Condition (@($results.runtime_sha256 | Sort-Object -Unique).Count -eq 1) -Message "Runtime binary changed between captures."
    $gpuSignatures = @($results | ForEach-Object { $_.gpu | ConvertTo-Json -Depth 20 -Compress } | Sort-Object -Unique)
    Assert-Condition -Condition ($gpuSignatures.Count -eq 1) -Message "GPU identity changed between captures."
    $sourceSignatures = @($results | ForEach-Object { $_.source_hashes | ConvertTo-Json -Depth 20 -Compress } | Sort-Object -Unique)
    Assert-Condition -Condition ($sourceSignatures.Count -eq 1) -Message "Governed source hashes changed between captures."

    foreach ($weather in @("clear", "rain")) {
        $weatherResults = @($results | Where-Object { $_.weather -ceq $weather })
        Assert-Condition -Condition ($weatherResults.Count -eq $Repetitions) -Message "$weather repetition matrix is incomplete."
        Assert-Condition -Condition (@($weatherResults.frozen_state_sha256 | Sort-Object -Unique).Count -eq 1) -Message "$weather frozen state is not deterministic across repetitions."
        $metricSignatures = @($weatherResults | ForEach-Object { Get-AnalysisSignature -Metrics $_.metrics } | Sort-Object -Unique)
        Assert-Condition -Condition ($metricSignatures.Count -eq 1) -Message "$weather RenderDoc metrics are not deterministic across repetitions."
    }

    $runtimeSha256After = Get-LowerSha256 -Path $runtimePath
    Assert-Condition -Condition ($runtimeSha256After -ceq $runtimeSha256) -Message "Runtime binary changed after capture."
    $gitStatusAfter = Invoke-CapturedCommand -FilePath "git.exe" -ArgumentList @("-C", $repositoryPath, "status", "--porcelain=v1", "--untracked-files=all") -WorkingDirectory $repositoryPath
    Assert-CommandSucceeded -Result $gitStatusAfter -Label "Final Git cleanliness check"
    Assert-Condition -Condition ([string]::IsNullOrWhiteSpace($gitStatusAfter.Stdout)) -Message "Capture mutated the exact-commit worktree."

    $validation = [ordered]@{
        schema = "forge-gate1-renderdoc-validation/v1"
        status = "PASS"
        source_commit = $gitCommit
        backend = "Vulkan"
        weather_matrix = @("clear", "rain")
        repetitions_per_weather = $Repetitions
        tools = [ordered]@{
            renderdoc_version = $requiredRenderDocVersion
            renderdoc_commit = $requiredRenderDocCommit
            qrenderdoc_sha256 = $qrenderdocSha256
            renderdoccmd_sha256 = $renderdoccmdSha256
            adapter_sha256 = $adapterSha256
            model_sha256 = $modelSha256
            cargo_version = $cargoVersion
        }
        vulkan_layer = [ordered]@{
            check = "renderdoccmd vulkanlayer explain"
            expected_sentence_observed = $true
            host_configuration_modified = $false
        }
        build = [ordered]@{
            profile = "release"
            locked = $true
            jobs = 1
            external_target_directory = $cargoTargetPath
            runtime_sha256 = $runtimeSha256
            cadle_assets_copied = $false
        }
        stability = [ordered]@{
            runtime_binary_stable = $true
            gpu_identity_stable = $true
            source_hashes_stable = $true
            frozen_state_stable_per_weather = $true
            renderdoc_metrics_stable_per_weather = $true
        }
        results = @($results)
        qualification = [ordered]@{
            r1_037_local_vulkan_renderdoc_qualified = $true
            draw_calls_observed = $true
            triangles_observed = $true
            instances_observed = $true
            renderdoc_metrics_are_fps_evidence = $false
            d3d12_qualified = $false
            webgpu_qualified = $false
        }
    }
    Write-JsonAtomically -Path $validationPath -Value $validation
}
catch {
    if ([IO.Directory]::Exists($outputPath) -and -not [IO.File]::Exists($failurePath)) {
        try {
            Write-JsonAtomically -Path $failurePath -Value ([ordered]@{
                    schema = "forge-gate1-renderdoc-orchestrator-failure/v1"
                    status = "FAIL"
                    source_commit = if ($null -ne $gitCommit) { $gitCommit } else { $null }
                    error_type = $_.Exception.GetType().FullName
                    message = $_.Exception.Message
                })
        }
        catch {
        }
    }
    throw
}
finally {
    Restore-ProcessEnvironment -Saved $environmentBefore
}

Write-Host "Gate 1 RenderDoc R1-037 PASS: $validationPath"
