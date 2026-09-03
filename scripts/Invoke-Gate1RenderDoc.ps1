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
$requiredQtCoreFileVersion = "5.15.2.0"
$requiredActionSchema = "forge-gate1-renderdoc-actions/v2"
$requiredAnalysisSchema = "forge-gate1-renderdoc-analysis/v2"
$requiredPositiveMetrics = @("draws", "instances", "triangles")
$qrenderdocProfileMutexName = "Global\OpenRPGForge-Gate1-QRenderDoc-Profile-v1"
$qrenderdocProfilePrefix = "forge-gate1-qrenderdoc-"
$qrenderdocProfileEvidenceFile = "qrenderdoc-profile-final.UI.config"
$layerReadySentence = "The RenderDoc vulkan layer appears to be correctly registered."
$readyFile = "gate1-renderdoc.ready.json"
$signalFile = "gate1-renderdoc.capture.signal.json"
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

function Get-LowerSha256ForText {
    param([Parameter(Mandatory = $true)][string]$Text)

    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
        return [Convert]::ToHexString($algorithm.ComputeHash($bytes)).ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Get-DirectoryTreeFingerprint {
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-Condition -Condition (-not [IO.File]::Exists($Path)) -Message "Profile path is a file, not a directory: $Path"
    if (-not [IO.Directory]::Exists($Path)) {
        return "absent"
    }

    $root = (Resolve-Path -LiteralPath $Path).Path
    $records = [Collections.Generic.List[string]]::new()
    $records.Add("D|.")
    foreach ($item in @(Get-ChildItem -LiteralPath $root -Force -Recurse | Sort-Object -Property FullName)) {
        $relative = [IO.Path]::GetRelativePath($root, $item.FullName).Replace('\', '/')
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            $records.Add("L|$relative|$($item.Attributes)")
        }
        elseif ($item.PSIsContainer) {
            $records.Add("D|$relative")
        }
        else {
            $records.Add("F|$relative|$($item.Length)|$(Get-LowerSha256 -Path $item.FullName)")
        }
    }
    return Get-LowerSha256ForText -Text ($records -join "`n")
}

function Assert-NoReparsePointInTree {
    param([Parameter(Mandatory = $true)][string]$Path)

    $root = Get-Item -LiteralPath $Path -Force
    Assert-Condition -Condition (($root.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) -Message "Governed directory is a reparse point: $Path"
    $reparsePoints = @(
        Get-ChildItem -LiteralPath $Path -Force -Recurse |
            Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }
    )
    Assert-Condition -Condition ($reparsePoints.Count -eq 0) -Message "Governed directory tree contains a reparse point: $Path"
}

function Copy-GovernedDirectoryTree {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    Assert-Condition -Condition ([IO.Directory]::Exists($Source)) -Message "Directory tree source is absent: $Source"
    Assert-Condition -Condition (-not [IO.Directory]::Exists($Destination) -and -not [IO.File]::Exists($Destination)) -Message "Directory tree destination already exists: $Destination"
    Assert-NoReparsePointInTree -Path $Source
    Copy-Item -LiteralPath $Source -Destination $Destination -Recurse -Force
    Assert-Condition -Condition ([IO.Directory]::Exists($Destination)) -Message "Directory tree copy was not created: $Destination"
    Assert-NoReparsePointInTree -Path $Destination
}

function Remove-GovernedDirectoryTree {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$LeafPrefix
    )

    $rootPath = Get-FullPathWithoutResolving -Path $Root
    $targetPath = Get-FullPathWithoutResolving -Path $Path
    Assert-Condition -Condition (-not [string]::Equals($rootPath, $targetPath, [StringComparison]::OrdinalIgnoreCase)) -Message "Refusing to remove governed root: $targetPath"
    Assert-Condition -Condition (Test-PathInsideRoot -Root $rootPath -Candidate $targetPath) -Message "Refusing to remove directory outside governed root: $targetPath"
    Assert-Condition -Condition ([string]::Equals([IO.Directory]::GetParent($targetPath).FullName, $rootPath, [StringComparison]::OrdinalIgnoreCase)) -Message "Governed removal target is not a direct child of its root: $targetPath"
    Assert-Condition -Condition ([IO.Path]::GetFileName($targetPath).StartsWith($LeafPrefix, [StringComparison]::Ordinal)) -Message "Governed removal target has an unexpected name: $targetPath"
    Assert-Condition -Condition (-not [IO.File]::Exists($targetPath)) -Message "Governed removal target is a file: $targetPath"
    if ([IO.Directory]::Exists($targetPath)) {
        Assert-NoReparsePointInTree -Path $targetPath
        [IO.Directory]::Delete($targetPath, $true)
    }
    Assert-Condition -Condition (-not [IO.Directory]::Exists($targetPath) -and -not [IO.File]::Exists($targetPath)) -Message "Governed directory cleanup failed: $targetPath"
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
        $Value -is [int64] -or $Value -is [uint64] -or
        $Value -is [Numerics.BigInteger]
    $maximum = [Numerics.BigInteger]::Parse(
        "18446744073709551615",
        [Globalization.CultureInfo]::InvariantCulture
    )
    Assert-Condition -Condition (
        $isIntegerType -and
        [Numerics.BigInteger]$Value -ge [Numerics.BigInteger]::Zero -and
        [Numerics.BigInteger]$Value -le $maximum
    ) -Message "$Path must be an unsigned 64-bit JSON integer."
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
    Assert-Condition -Condition ($Analysis.schema -ceq $requiredAnalysisSchema) -Message "$Path schema mismatch."
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
        "actions_visited",
        "present_actions"
    ) -Path "$Path.metrics"
    foreach ($field in @("draws", "instances", "triangles", "lines", "points", "actions_visited", "present_actions")) {
        Assert-NonNegativeInteger -Value $Analysis.metrics.$field -Path "$Path.metrics.$field"
    }
    foreach ($field in $requiredPositiveMetrics) {
        Assert-Condition -Condition ($Analysis.metrics.$field -gt 0) -Message "$Path.metrics.$field must be positive for R1-037 qualification."
    }
    Assert-Condition -Condition (
        $Analysis.metrics.present_actions -eq 1 -and
        $Analysis.metrics.present_actions -eq $Analysis.capture.present_count
    ) -Message "$Path actual Present action count is not exactly one or diverges from capture.present_count."
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
        $Metrics.present_actions,
        $Metrics.draws_by_topology.TriangleList,
        $Metrics.draws_by_topology.LineList,
        $Metrics.draws_by_topology.PointList
    ) -join ":"
}

function Assert-NormalizedActionTree {
    param(
        [AllowEmptyCollection()][object[]]$Actions,
        [Parameter(Mandatory = $true)][string[]]$ExpectedFields,
        [Parameter(Mandatory = $true)][string]$Path
    )

    for ($index = 0; $index -lt $Actions.Count; $index++) {
        $action = $Actions[$index]
        $actionPath = "${Path}[$index]"
        Assert-ExactProperties -Value $action -Expected $ExpectedFields -Path $actionPath
        Assert-NonNegativeInteger -Value $action.event_id -Path "$actionPath.event_id"
        Assert-NonNegativeInteger -Value $action.num_indices -Path "$actionPath.num_indices"
        Assert-NonNegativeInteger -Value $action.num_instances -Path "$actionPath.num_instances"
        Assert-Condition -Condition ($action.instanced -is [bool]) -Message "$actionPath.instanced must be boolean."
        Assert-Condition -Condition ($action.indirect -is [bool]) -Message "$actionPath.indirect must be boolean."
        Assert-Condition -Condition ($action.indirect_resolved -is [bool]) -Message "$actionPath.indirect_resolved must be boolean."
        Assert-NormalizedActionTree -Actions @($action.children) -ExpectedFields $ExpectedFields -Path "$actionPath.children"
    }
}

function Measure-PresentActions {
    param([AllowEmptyCollection()][object[]]$Actions)

    [uint64]$count = 0
    foreach ($action in $Actions) {
        if (@($action.flags) -ccontains "Present") {
            $count++
        }
        $count += Measure-PresentActions -Actions @($action.children)
    }
    return $count
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
$renderdocDllPath = Join-Path $renderDocPath "renderdoc.dll"
$qtCorePath = Join-Path $renderDocPath "Qt5Core.dll"
$adapterPath = Join-Path $repositoryPath "scripts\renderdoc\gate1_renderdoc_qrenderdoc.py"
$modelPath = Join-Path $repositoryPath "scripts\renderdoc\gate1_renderdoc_model.py"
$manifestPath = Join-Path $repositoryPath "scripts\gate1-renderdoc.v1.json"
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
$profileMutex = $null
$profileMutexAcquired = $false
$executionViewPath = $null
$activeProfilePath = $null
$roamingAppDataPath = $null
$realProfileFingerprintBefore = $null
$realProfileFingerprintAfter = $null
try {
    foreach ($path in @($qrenderdocPath, $renderdoccmdPath, $renderdocDllPath, $qtCorePath, $adapterPath, $modelPath, $manifestPath)) {
        Assert-Condition -Condition ([IO.File]::Exists($path)) -Message "Required acquisition component is absent: $path"
    }
    $adapterSha256 = Get-LowerSha256 -Path $adapterPath
    $modelSha256 = Get-LowerSha256 -Path $modelPath
    $manifestSha256 = Get-LowerSha256 -Path $manifestPath
    $qrenderdocSha256 = Get-LowerSha256 -Path $qrenderdocPath
    $renderdoccmdSha256 = Get-LowerSha256 -Path $renderdoccmdPath
    $renderdocDllSha256 = Get-LowerSha256 -Path $renderdocDllPath
    $qtCoreSha256 = Get-LowerSha256 -Path $qtCorePath
    $qtCoreFileVersion = (Get-Item -LiteralPath $qtCorePath).VersionInfo.FileVersion
    $manifest = Read-JsonDocument -Path $manifestPath
    Assert-ExactProperties -Value $manifest -Expected @(
        "schema",
        "route",
        "created_from_commit",
        "created_from_tree",
        "tool",
        "qualification_matrix",
        "workload",
        "capture",
        "normalized_input",
        "analysis",
        "qualification_boundary"
    ) -Path "RenderDoc manifest"
    Assert-Condition -Condition ($manifest.schema -ceq "forge-gate1-renderdoc-lock/v1" -and $manifest.route -ceq "R1-037") -Message "RenderDoc manifest identity mismatch."
    Assert-Condition -Condition ($manifest.tool.release -ceq $requiredRenderDocVersion -and $manifest.tool.release_commit -ceq $requiredRenderDocCommit) -Message "RenderDoc manifest tool pin mismatch."
    Assert-Condition -Condition ($manifest.normalized_input.schema -ceq $requiredActionSchema) -Message "RenderDoc manifest normalized action schema mismatch."
    Assert-Condition -Condition ($manifest.analysis.output_schema -ceq $requiredAnalysisSchema) -Message "RenderDoc manifest analysis schema mismatch."
    Assert-Condition -Condition ((@($manifest.analysis.required_positive_metrics) -join "`n") -ceq ($requiredPositiveMetrics -join "`n")) -Message "RenderDoc manifest required positive metrics mismatch."
    Assert-Condition -Condition ($manifest.workload.ready_receipt_schema.name -ceq "forge.gate1-renderdoc-ready" -and $manifest.workload.ready_receipt_schema.version -eq 1) -Message "RenderDoc manifest ready schema mismatch."
    Assert-Condition -Condition ($manifest.capture.signal_schema.name -ceq "forge.gate1-renderdoc-capture-signal" -and $manifest.capture.signal_schema.version -eq 1) -Message "RenderDoc manifest signal schema mismatch."
    Assert-Condition -Condition ($manifest.capture.runtime_ledger_schema.name -ceq "forge.gate1-renderdoc-runtime" -and $manifest.capture.runtime_ledger_schema.version -eq 1) -Message "RenderDoc manifest runtime ledger schema mismatch."
    Assert-Condition -Condition ($manifest.capture.adapter_receipt_schema -ceq "forge-gate1-renderdoc-adapter/v1") -Message "RenderDoc manifest adapter receipt schema mismatch."
    Assert-Condition -Condition ($manifest.capture.launcher -ceq "forge-gate1-qrenderdoc-<nonce>.exe --python") -Message "RenderDoc manifest launcher mismatch."
    Assert-ExactProperties -Value $manifest.capture.automation_profile -Expected @(
        "strategy",
        "application_name_prefix",
        "location",
        "ui_config_filename",
        "json_magic_field",
        "json_magic_version",
        "analytics_total_opt_out",
        "global_mutex",
        "real_profile_application_name",
        "staged_bundle_complete",
        "staged_binary_hash_and_signature_required",
        "transient_host_profile_removed",
        "real_profile_fingerprint_unchanged",
        "adapter_config_path_check_before_injection"
    ) -Path "RenderDoc manifest capture.automation_profile"
    Assert-Condition -Condition (
        $manifest.capture.automation_profile.strategy -ceq "full_bundle_mirror_unique_executable_basename" -and
        $manifest.capture.automation_profile.application_name_prefix -ceq $qrenderdocProfilePrefix -and
        $manifest.capture.automation_profile.location -ceq "QStandardPaths::AppDataLocation" -and
        $manifest.capture.automation_profile.ui_config_filename -ceq "UI.config" -and
        $manifest.capture.automation_profile.json_magic_field -ceq "rdocConfigData" -and
        $manifest.capture.automation_profile.json_magic_version -eq 1 -and
        $manifest.capture.automation_profile.analytics_total_opt_out -eq $true -and
        $manifest.capture.automation_profile.global_mutex -ceq $qrenderdocProfileMutexName -and
        $manifest.capture.automation_profile.real_profile_application_name -ceq "qrenderdoc" -and
        $manifest.capture.automation_profile.staged_bundle_complete -eq $true -and
        $manifest.capture.automation_profile.staged_binary_hash_and_signature_required -eq $true -and
        $manifest.capture.automation_profile.transient_host_profile_removed -eq $true -and
        $manifest.capture.automation_profile.real_profile_fingerprint_unchanged -eq $true -and
        $manifest.capture.automation_profile.adapter_config_path_check_before_injection -eq $true
    ) -Message "RenderDoc manifest automation profile contract mismatch."
    Assert-Condition -Condition ($qrenderdocSha256 -ceq ([string]$manifest.tool.sha256.qrenderdoc_exe).ToLowerInvariant()) -Message "qrenderdoc.exe SHA-256 does not match the pinned manifest."
    Assert-Condition -Condition ($renderdoccmdSha256 -ceq ([string]$manifest.tool.sha256.renderdoccmd_exe).ToLowerInvariant()) -Message "renderdoccmd.exe SHA-256 does not match the pinned manifest."
    Assert-Condition -Condition ($renderdocDllSha256 -ceq ([string]$manifest.tool.sha256.renderdoc_dll).ToLowerInvariant()) -Message "renderdoc.dll SHA-256 does not match the pinned manifest."
    Assert-Condition -Condition ($qtCoreSha256 -ceq ([string]$manifest.tool.sha256.qt5core_dll).ToLowerInvariant()) -Message "Qt5Core.dll SHA-256 does not match the pinned manifest."
    Assert-Condition -Condition ($qtCoreFileVersion -ceq $requiredQtCoreFileVersion -and $manifest.tool.embedded_qt_core_file_version -ceq $requiredQtCoreFileVersion) -Message "The embedded Qt Core version is not the pinned 5.15.2.0 build."

    foreach ($toolPath in @($qrenderdocPath, $renderdoccmdPath, $renderdocDllPath)) {
        $signature = Get-AuthenticodeSignature -LiteralPath $toolPath
        Assert-Condition -Condition ($signature.Status -eq [Management.Automation.SignatureStatus]::Valid) -Message "RenderDoc tool Authenticode signature is not valid: $toolPath ($($signature.Status))."
        Assert-Condition -Condition ($null -ne $signature.SignerCertificate) -Message "RenderDoc tool has no signing certificate: $toolPath"
        Assert-Condition -Condition ($signature.SignerCertificate.Thumbprint -ceq $manifest.tool.authenticode_signer_thumbprint) -Message "RenderDoc tool signer does not match the pinned manifest: $toolPath"
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

    $executionViewName = ".qrenderdoc-execution-$([Guid]::NewGuid().ToString('N').ToLowerInvariant())"
    $executionViewPath = Join-Path $outputPath $executionViewName
    Copy-GovernedDirectoryTree -Source $renderDocPath -Destination $executionViewPath
    $stagedQrenderdocPath = Join-Path $executionViewPath "qrenderdoc.exe"
    $stagedRenderdoccmdPath = Join-Path $executionViewPath "renderdoccmd.exe"
    $stagedRenderdocDllPath = Join-Path $executionViewPath "renderdoc.dll"
    $stagedQtCorePath = Join-Path $executionViewPath "Qt5Core.dll"
    Assert-Condition -Condition (
        (Get-LowerSha256 -Path $stagedQrenderdocPath) -ceq $qrenderdocSha256 -and
        (Get-LowerSha256 -Path $stagedRenderdoccmdPath) -ceq $renderdoccmdSha256 -and
        (Get-LowerSha256 -Path $stagedRenderdocDllPath) -ceq $renderdocDllSha256 -and
        (Get-LowerSha256 -Path $stagedQtCorePath) -ceq $qtCoreSha256
    ) -Message "The complete RenderDoc execution mirror does not match the pinned bundle."
    foreach ($stagedToolPath in @($stagedQrenderdocPath, $stagedRenderdoccmdPath, $stagedRenderdocDllPath)) {
        $stagedSignature = Get-AuthenticodeSignature -LiteralPath $stagedToolPath
        Assert-Condition -Condition ($stagedSignature.Status -eq [Management.Automation.SignatureStatus]::Valid) -Message "Staged RenderDoc tool Authenticode signature is not valid: $stagedToolPath ($($stagedSignature.Status))."
        Assert-Condition -Condition ($null -ne $stagedSignature.SignerCertificate -and $stagedSignature.SignerCertificate.Thumbprint -ceq $manifest.tool.authenticode_signer_thumbprint) -Message "Staged RenderDoc tool signer does not match the pinned manifest: $stagedToolPath"
    }

    $profileMutex = [Threading.Mutex]::new($false, $qrenderdocProfileMutexName)
    try {
        $profileMutexAcquired = $profileMutex.WaitOne(0)
    }
    catch [Threading.AbandonedMutexException] {
        $profileMutexAcquired = $true
    }
    Assert-Condition -Condition $profileMutexAcquired -Message "Another governed QRenderDoc profile operation already holds $qrenderdocProfileMutexName."
    $roamingAppDataPath = Get-FullPathWithoutResolving -Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData))
    Assert-Condition -Condition ([IO.Path]::IsPathRooted($roamingAppDataPath) -and [IO.Directory]::Exists($roamingAppDataPath)) -Message "Cannot resolve the Windows RoamingAppData known folder."
    Assert-NoReparsePointInExistingAncestors -Path $roamingAppDataPath
    $realQrenderdocProfilePath = Join-Path $roamingAppDataPath $manifest.capture.automation_profile.real_profile_application_name
    $realProfileFingerprintBefore = Get-DirectoryTreeFingerprint -Path $realQrenderdocProfilePath

    foreach ($weather in @("clear", "rain")) {
        for ($iteration = 1; $iteration -le $Repetitions; $iteration++) {
            $runName = "{0}-{1:d2}" -f $weather, $iteration
            $runPath = Join-Path $outputPath $runName
            Initialize-EmptyDirectory -Path $runPath -Label "RenderDoc run $runName"
            $nonce = [Guid]::NewGuid().ToString("N").ToLowerInvariant()
            $captureId = "gate1-renderdoc-$runName"
            $profileApplicationName = "$qrenderdocProfilePrefix$nonce"
            $activeProfilePath = Join-Path $roamingAppDataPath $profileApplicationName
            Assert-Condition -Condition (-not [IO.Directory]::Exists($activeProfilePath) -and -not [IO.File]::Exists($activeProfilePath)) -Message "Disposable QRenderDoc profile already exists: $activeProfilePath"
            Initialize-EmptyDirectory -Path $activeProfilePath -Label "Disposable QRenderDoc profile $runName"
            $profileConfigPath = Join-Path $activeProfilePath $manifest.capture.automation_profile.ui_config_filename
            Write-JsonAtomically -Path $profileConfigPath -Value ([ordered]@{
                    rdocConfigData = 1
                    Analytics_TotalOptOut = $true
                })
            $profileSeedSha256 = Get-LowerSha256 -Path $profileConfigPath
            $isolatedQrenderdocPath = Join-Path $executionViewPath "$profileApplicationName.exe"
            Assert-Condition -Condition (-not [IO.File]::Exists($isolatedQrenderdocPath) -and -not [IO.Directory]::Exists($isolatedQrenderdocPath)) -Message "Isolated QRenderDoc executable already exists: $isolatedQrenderdocPath"
            [IO.File]::Copy($stagedQrenderdocPath, $isolatedQrenderdocPath, $false)
            Assert-Condition -Condition ((Get-LowerSha256 -Path $isolatedQrenderdocPath) -ceq $qrenderdocSha256) -Message "Isolated QRenderDoc executable hash mismatch."
            $isolatedSignature = Get-AuthenticodeSignature -LiteralPath $isolatedQrenderdocPath
            Assert-Condition -Condition ($isolatedSignature.Status -eq [Management.Automation.SignatureStatus]::Valid) -Message "Isolated QRenderDoc Authenticode signature is not valid: $($isolatedSignature.Status)."
            Assert-Condition -Condition ($null -ne $isolatedSignature.SignerCertificate -and $isolatedSignature.SignerCertificate.Thumbprint -ceq $manifest.tool.authenticode_signer_thumbprint) -Message "Isolated QRenderDoc signer does not match the pinned manifest."

            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_RUNTIME" -Value $runtimePath
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_REPO_ROOT" -Value $repositoryPath
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_OUTPUT_DIR" -Value $runPath
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_NONCE" -Value $nonce
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_WEATHER" -Value $weather
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_SOURCE_COMMIT" -Value $gitCommit
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_QRENDERDOC_SHA256" -Value $qrenderdocSha256
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_RENDERDOCCMD_SHA256" -Value $renderdoccmdSha256
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_RENDERDOC_DLL_SHA256" -Value $renderdocDllSha256
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_CAPTURE_ID" -Value $captureId
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_PROFILE_CONFIG" -Value $profileConfigPath
            Set-ProcessEnvironment -Saved $environmentBefore -Name "FORGE_GATE1_RENDERDOC_PROFILE_SEED_SHA256" -Value $profileSeedSha256

            try {
                $captureResult = Invoke-CapturedCommand -FilePath $isolatedQrenderdocPath -ArgumentList @("--python", $adapterPath) -WorkingDirectory $repositoryPath -TimeoutSeconds 240
                $logPath = Join-Path $runPath "qrenderdoc.log"
                $log = @(
                    "exit_code=$($captureResult.ExitCode)",
                    "--- stdout ---",
                    $captureResult.Stdout,
                    "--- stderr ---",
                    $captureResult.Stderr
                ) -join [Environment]::NewLine
                Write-TextAtomically -Path $logPath -Text ($log + [Environment]::NewLine)
                $profileFinal = Read-JsonDocument -Path $profileConfigPath
                Assert-Condition -Condition ($profileFinal.rdocConfigData -eq 1) -Message "$runName disposable QRenderDoc profile magic mismatch."
                Assert-Condition -Condition ($profileFinal.Analytics_TotalOptOut -is [bool] -and $profileFinal.Analytics_TotalOptOut) -Message "$runName disposable QRenderDoc profile lost analytics opt-out."
                $profileEvidencePath = Join-Path $runPath $qrenderdocProfileEvidenceFile
                [IO.File]::Copy($profileConfigPath, $profileEvidencePath, $false)
                $profileFinalSha256 = Get-LowerSha256 -Path $profileEvidencePath
            }
            finally {
                if ($null -ne $activeProfilePath) {
                    Remove-GovernedDirectoryTree -Root $roamingAppDataPath -Path $activeProfilePath -LeafPrefix $qrenderdocProfilePrefix
                    $activeProfilePath = $null
                }
            }
            Assert-Condition -Condition (-not [IO.Directory]::Exists((Join-Path $roamingAppDataPath $profileApplicationName))) -Message "$runName disposable QRenderDoc profile was not removed."
            Assert-CommandSucceeded -Result $captureResult -Label "qrenderdoc governed capture $runName"
            Assert-Condition -Condition (-not [IO.File]::Exists((Join-Path $runPath $adapterFailureFile))) -Message "qrenderdoc adapter reported failure for $runName."

            $readyPath = Join-Path $runPath $readyFile
            $signalPath = Join-Path $runPath $signalFile
            $ledgerPath = Join-Path $runPath $runtimeLedgerFile
            $actionsPath = Join-Path $runPath $actionFile
            $analysisPath = Join-Path $runPath $analysisFile
            $receiptPath = Join-Path $runPath $adapterFile
            $ready = Read-JsonDocument -Path $readyPath
            $signal = Read-JsonDocument -Path $signalPath
            $ledger = Read-JsonDocument -Path $ledgerPath
            $actions = Read-JsonDocument -Path $actionsPath
            $analysis = Read-JsonDocument -Path $analysisPath
            $receipt = Read-JsonDocument -Path $receiptPath

            Assert-ExactProperties -Value $ready -Expected @($manifest.workload.ready_receipt_required_fields) -Path "$runName.ready"
            Assert-Condition -Condition ($ready.schema -ceq "forge.gate1-renderdoc-ready" -and $ready.schema_version -eq 1) -Message "$runName ready schema mismatch."
            Assert-Condition -Condition ($ready.status -ceq "ready_frozen_pending_external_capture_signal") -Message "$runName ready status mismatch."
            Assert-Condition -Condition ($ready.nonce -ceq $nonce -and $ready.source_commit -ceq $gitCommit) -Message "$runName ready binding mismatch."
            Assert-Condition -Condition ($ready.required_backend -ceq "Vulkan" -and $ready.required_renderdoc_version -ceq $requiredRenderDocVersion) -Message "$runName ready tool/backend mismatch."
            Assert-Condition -Condition ($ready.virtual_time_paused -eq $true -and $ready.freeze_settle_frames -eq 30) -Message "$runName ready freeze contract mismatch."
            Assert-ExactProperties -Value $ready.frozen_state -Expected @($manifest.workload.ready_receipt_frozen_state_fields) -Path "$runName.ready.frozen_state"
            Assert-ExactProperties -Value $ready.frozen_state.sources -Expected @($manifest.workload.ready_receipt_source_fields) -Path "$runName.ready.frozen_state.sources"
            Assert-ExactProperties -Value $ready.frozen_state.seeds -Expected @($manifest.workload.ready_receipt_seed_fields) -Path "$runName.ready.frozen_state.seeds"
            foreach ($field in @($manifest.workload.ready_receipt_seed_fields)) {
                Assert-NonNegativeInteger -Value $ready.frozen_state.seeds.$field -Path "$runName.ready.frozen_state.seeds.$field"
            }
            Assert-ExactProperties -Value $ready.frozen_state.tick -Expected @($manifest.workload.ready_receipt_tick_fields) -Path "$runName.ready.frozen_state.tick"
            foreach ($field in @($manifest.workload.ready_receipt_tick_fields)) {
                Assert-NonNegativeInteger -Value $ready.frozen_state.tick.$field -Path "$runName.ready.frozen_state.tick.$field"
            }
            Assert-Condition -Condition (
                $ready.frozen_state.tick.virtual_elapsed_nanoseconds -eq 0 -and
                $ready.frozen_state.tick.fixed_elapsed_nanoseconds -eq 0 -and
                $ready.frozen_state.tick.fixed_tick -eq 0 -and
                $ready.frozen_state.tick.fixed_timestep_nanoseconds -gt 0
            ) -Message "$runName ready tick is not the deterministic frozen tick."
            Assert-Condition -Condition ($ready.frozen_state.vsync_requested -is [bool] -and -not $ready.frozen_state.vsync_requested) -Message "$runName ready state did not prove the AutoNoVsync request."
            Assert-Condition -Condition ($ready.frozen_state.expected_weather -ceq $weather) -Message "$runName ready weather mismatch."
            Assert-ExactProperties -Value $ready.frozen_state.camera -Expected @($manifest.workload.ready_receipt_camera_fields) -Path "$runName.ready.frozen_state.camera"
            Assert-ExactProperties -Value $ready.frozen_state.window -Expected @($manifest.workload.ready_receipt_window_fields) -Path "$runName.ready.frozen_state.window"
            Assert-Condition -Condition ($ready.frozen_state.window.physical_width -eq 1920 -and $ready.frozen_state.window.physical_height -eq 1080 -and $ready.frozen_state.window.visible -eq $true) -Message "$runName ready window mismatch."
            Assert-ExactProperties -Value $ready.frozen_state.gpu -Expected @($manifest.workload.ready_receipt_gpu_fields) -Path "$runName.ready.frozen_state.gpu"
            Assert-Condition -Condition ($ready.frozen_state.gpu.backend -ceq "Vulkan" -and -not [string]::IsNullOrWhiteSpace([string]$ready.frozen_state.gpu.name)) -Message "$runName ready GPU identity mismatch."
            Assert-ExactProperties -Value $ready.frozen_state.governed_scene_hashes -Expected @($manifest.workload.ready_receipt_governed_scene_hash_fields) -Path "$runName.ready.frozen_state.governed_scene_hashes"
            Assert-ExactProperties -Value $ready.files -Expected @($manifest.workload.ready_receipt_file_fields) -Path "$runName.ready.files"
            Assert-ExactProperties -Value $ready.qualification -Expected @($manifest.workload.ready_receipt_qualification_fields) -Path "$runName.ready.qualification"
            Assert-Condition -Condition ($ready.frozen_state.sources.cadle_assets_used -eq $false) -Message "$runName did not exclude Cadle assets."

            Assert-ExactProperties -Value $signal -Expected @($manifest.capture.signal_fields) -Path "$runName.signal"
            Assert-Condition -Condition ($signal.schema -ceq $manifest.capture.signal_schema.name -and $signal.schema_version -eq $manifest.capture.signal_schema.version) -Message "$runName signal schema mismatch."
            Assert-Condition -Condition ($signal.nonce -ceq $nonce -and $signal.process_id -eq $ready.process_id -and $signal.source_commit -ceq $gitCommit -and $signal.capture_id -ceq $captureId) -Message "$runName signal binding mismatch."
            Assert-Condition -Condition ($signal.ready_sha256 -ceq (Get-LowerSha256 -Path $readyPath)) -Message "$runName signal ready SHA-256 mismatch."
            Assert-ExactProperties -Value $signal.renderdoc -Expected @($manifest.capture.signal_renderdoc_fields) -Path "$runName.signal.renderdoc"
            Assert-Condition -Condition ($signal.renderdoc.capture_triggered -eq $true -and $signal.renderdoc.target_control_ident -gt 0 -and $signal.renderdoc.target_api -ceq "Vulkan" -and $signal.renderdoc.tool_version -ceq $requiredRenderDocVersion) -Message "$runName signal RenderDoc identity mismatch."
            Assert-Condition -Condition ($signal.renderdoc.qrenderdoc_sha256 -ceq $qrenderdocSha256 -and $signal.renderdoc.renderdoccmd_sha256 -ceq $renderdoccmdSha256 -and $signal.renderdoc.renderdoc_dll_sha256 -ceq $renderdocDllSha256) -Message "$runName signal RenderDoc hashes do not match the pinned tools."

            Assert-ExactProperties -Value $ledger -Expected @($manifest.capture.runtime_ledger_fields) -Path "$runName.runtime"
            Assert-Condition -Condition ($ledger.schema -ceq "forge.gate1-renderdoc-runtime" -and $ledger.schema_version -eq 1) -Message "$runName runtime ledger schema mismatch."
            Assert-Condition -Condition ($ledger.status -ceq "stable_capture_window_completed_pending_external_capture_analysis") -Message "$runName runtime ledger status mismatch."
            Assert-Condition -Condition ($ledger.nonce -ceq $nonce -and $ledger.process_id -eq $ready.process_id -and $ledger.source_commit -ceq $gitCommit -and $ledger.capture_id -ceq $captureId) -Message "$runName runtime ledger binding mismatch."
            Assert-Condition -Condition ($ledger.capture_window_frames -eq 120 -and $ledger.frozen_state_stable -eq $true) -Message "$runName runtime capture window is not stable and complete."
            Assert-Condition -Condition ($ledger.frozen_state_sha256 -ceq $ready.frozen_state_sha256) -Message "$runName frozen-state hash changed."
            Assert-Condition -Condition ($ledger.ready_sha256 -ceq $signal.ready_sha256 -and $ledger.renderdoc.target_control_ident -eq $signal.renderdoc.target_control_ident) -Message "$runName runtime ledger signal binding changed."
            $readyFrozenStateJson = $ready.frozen_state | ConvertTo-Json -Depth 100 -Compress
            Assert-Condition -Condition (($ledger.frozen_state_initial | ConvertTo-Json -Depth 100 -Compress) -ceq $readyFrozenStateJson) -Message "$runName initial frozen state changed."
            Assert-Condition -Condition (($ledger.frozen_state_final | ConvertTo-Json -Depth 100 -Compress) -ceq $readyFrozenStateJson) -Message "$runName final frozen state changed."

            Assert-ExactProperties -Value $actions -Expected @($manifest.normalized_input.root_fields) -Path "$runName.actions"
            Assert-Condition -Condition ($actions.schema -ceq $requiredActionSchema) -Message "$runName normalized action schema mismatch."
            Assert-ExactProperties -Value $actions.capture -Expected @($manifest.normalized_input.capture_fields) -Path "$runName.actions.capture"
            Assert-Condition -Condition ($actions.capture.captured_frame_count -eq 1 -and $actions.capture.present_count -eq 1) -Message "$runName is not exactly one captured/presented frame."
            Assert-Condition -Condition ($actions.capture.local_replay_support -ceq "Supported" -and $actions.capture.pipeline_type -ceq "Vulkan" -and $actions.capture.degraded -eq $false) -Message "$runName replay contract mismatch."
            Assert-NormalizedActionTree -Actions @($actions.actions) -ExpectedFields @($manifest.normalized_input.action_fields) -Path "$runName.actions.actions"
            $presentActions = Measure-PresentActions -Actions @($actions.actions)
            Assert-Condition -Condition ($presentActions -eq 1 -and $presentActions -eq $actions.capture.present_count) -Message "$runName normalized Present action count mismatch."
            Assert-RenderDocAnalysis -Analysis $analysis -Path "$runName.analysis"
            Assert-Condition -Condition ($analysis.metrics.present_actions -eq $presentActions) -Message "$runName analysis Present count diverges from normalized actions."

            Assert-ExactProperties -Value $receipt -Expected @($manifest.capture.adapter_receipt_fields) -Path "$runName.adapter"
            Assert-Condition -Condition ($receipt.schema -ceq $manifest.capture.adapter_receipt_schema -and $receipt.status -ceq "PASS") -Message "$runName adapter receipt did not PASS."
            Assert-Condition -Condition ($receipt.source_commit -ceq $gitCommit -and $receipt.nonce -ceq $nonce -and $receipt.weather -ceq $weather -and $receipt.capture_id -ceq $captureId) -Message "$runName adapter receipt binding mismatch."
            Assert-ExactProperties -Value $receipt.target -Expected @($manifest.capture.adapter_target_fields) -Path "$runName.adapter.target"
            Assert-Condition -Condition ($receipt.target.process_id -eq $ready.process_id -and $receipt.target.target_control_ident -eq $signal.renderdoc.target_control_ident -and $receipt.target.api -ceq "Vulkan") -Message "$runName adapter target binding mismatch."
            Assert-ExactProperties -Value $receipt.capture -Expected @($manifest.capture.adapter_capture_fields) -Path "$runName.adapter.capture"
            Assert-Condition -Condition ($receipt.capture.captured_frame_count -eq 1) -Message "$runName adapter receipt is not a single-frame capture."
            Assert-ExactProperties -Value $receipt.files -Expected @($manifest.capture.adapter_file_fields) -Path "$runName.adapter.files"
            Assert-ExactProperties -Value $receipt.qualification -Expected @($manifest.capture.adapter_qualification_fields) -Path "$runName.adapter.qualification"
            Assert-Condition -Condition ($receipt.qualification.runtime_state_qualified -eq $true -and $receipt.qualification.external_capture_observed -eq $true -and $receipt.qualification.renderdoc_analysis_completed -eq $true) -Message "$runName adapter qualification is incomplete."
            Assert-Condition -Condition ($receipt.qualification.renderdoc_metrics_are_fps_evidence -eq $false) -Message "$runName improperly labels RenderDoc metrics as FPS evidence."
            $capturePath = Join-Path $runPath $receipt.capture.path
            Assert-Condition -Condition ([IO.File]::Exists($capturePath)) -Message "$runName capture path is absent."
            Assert-Condition -Condition ((Get-Item -LiteralPath $capturePath).Length -eq $receipt.capture.byte_size) -Message "$runName capture byte size mismatch."
            Assert-Condition -Condition ((Get-LowerSha256 -Path $capturePath) -ceq $receipt.capture.sha256) -Message "$runName capture SHA-256 mismatch."
            Assert-Condition -Condition ((Get-LowerSha256 -Path $readyPath) -ceq $receipt.files.$readyFile) -Message "$runName ready SHA-256 mismatch."
            Assert-Condition -Condition ((Get-LowerSha256 -Path $signalPath) -ceq $receipt.files.$signalFile) -Message "$runName signal SHA-256 mismatch."
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
                    qrenderdoc_profile = [ordered]@{
                        application_name = $profileApplicationName
                        seed_sha256 = $profileSeedSha256
                        final_config_file = "$runName/$qrenderdocProfileEvidenceFile"
                        final_config_sha256 = $profileFinalSha256
                        transient_profile_removed = $true
                    }
                    metrics = $analysis.metrics
                })
        }
    }

    $realProfileFingerprintAfter = Get-DirectoryTreeFingerprint -Path $realQrenderdocProfilePath
    Assert-Condition -Condition ($realProfileFingerprintAfter -ceq $realProfileFingerprintBefore) -Message "The real qrenderdoc profile changed during governed acquisition."
    Remove-GovernedDirectoryTree -Root $outputPath -Path $executionViewPath -LeafPrefix ".qrenderdoc-execution-"
    $executionViewPath = $null
    $profileMutex.ReleaseMutex()
    $profileMutexAcquired = $false
    $profileMutex.Dispose()
    $profileMutex = $null

    Assert-Condition -Condition ($results.Count -eq 2 * $Repetitions) -Message "The acquisition did not produce the complete clear/rain repetition matrix."
    Assert-Condition -Condition (@($results.nonce | Sort-Object -Unique).Count -eq $results.Count) -Message "Acquisition nonces are not unique."
    Assert-Condition -Condition (@($results.capture_id | Sort-Object -Unique).Count -eq $results.Count) -Message "Capture identifiers are not unique."
    Assert-Condition -Condition (@($results.capture_file | Sort-Object -Unique).Count -eq $results.Count) -Message "Capture artifact paths are not unique."
    Assert-Condition -Condition (@($results.runtime_sha256 | Sort-Object -Unique).Count -eq 1) -Message "Runtime binary changed between captures."
    $allDrawCallsObserved = @($results | Where-Object { $_.metrics.draws -gt 0 }).Count -eq $results.Count
    $allInstancesObserved = @($results | Where-Object { $_.metrics.instances -gt 0 }).Count -eq $results.Count
    $allTrianglesObserved = @($results | Where-Object { $_.metrics.triangles -gt 0 }).Count -eq $results.Count
    Assert-Condition -Condition ($allDrawCallsObserved -and $allInstancesObserved -and $allTrianglesObserved) -Message "At least one capture lacks required GPU work metrics."
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
            renderdoc_dll_sha256 = $renderdocDllSha256
            qt5core_sha256 = $qtCoreSha256
            qt_core_file_version = $qtCoreFileVersion
            adapter_sha256 = $adapterSha256
            model_sha256 = $modelSha256
            manifest_sha256 = $manifestSha256
            cargo_version = $cargoVersion
        }
        vulkan_layer = [ordered]@{
            check = "renderdoccmd vulkanlayer explain"
            expected_sentence_observed = $true
            host_configuration_modified = $false
        }
        qrenderdoc_profile = [ordered]@{
            strategy = $manifest.capture.automation_profile.strategy
            real_profile_fingerprint_before = $realProfileFingerprintBefore
            real_profile_fingerprint_after = $realProfileFingerprintAfter
            real_profile_unchanged = $true
            transient_profiles_removed = $true
            execution_view_removed = $true
            host_profile_write_scope = "disposable_nonce_profile_only"
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
            draw_calls_observed = $allDrawCallsObserved
            triangles_observed = $allTrianglesObserved
            instances_observed = $allInstancesObserved
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
    try {
        if ($null -ne $activeProfilePath -and $null -ne $roamingAppDataPath) {
            Remove-GovernedDirectoryTree -Root $roamingAppDataPath -Path $activeProfilePath -LeafPrefix $qrenderdocProfilePrefix
        }
    }
    finally {
        try {
            if ($null -ne $executionViewPath) {
                Remove-GovernedDirectoryTree -Root $outputPath -Path $executionViewPath -LeafPrefix ".qrenderdoc-execution-"
            }
        }
        finally {
            if ($profileMutexAcquired) {
                $profileMutex.ReleaseMutex()
            }
            if ($null -ne $profileMutex) {
                $profileMutex.Dispose()
            }
            Restore-ProcessEnvironment -Saved $environmentBefore
        }
    }
}

Write-Host "Gate 1 RenderDoc R1-037 PASS: $validationPath"
