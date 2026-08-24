[CmdletBinding()]
param(
    [string]$PlanPath,
    [string]$RepositoryRoot,
    [string[]]$ChangedPath,
    [string[]]$FeatureId,
    [ValidateSet('Development', 'Lot', 'Milestone', 'Publication')]
    [string]$Stage = 'Development',
    [ValidateSet('Auto', 'Core', 'Studio', 'Runtime', 'Assets', 'Network', 'Distribution', 'Full')]
    [string]$Profile = 'Auto',
    [string]$Page = 'zones',
    [string]$Evidence = 'ux1-aa-verbs',
    [ValidateSet('Route', 'Wave', 'Full')]
    [string]$StudioSmokeScope = 'Route',
    [ValidateSet('Studio', 'Runtime', 'All')]
    [string]$DistributionProduct = 'All',
    [string[]]$DistributionDirectory,
    [switch]$NoGraph,
    [switch]$Restore,
    [switch]$PlanOnly,
    [switch]$AllowStalePlan
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}
$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
. (Join-Path $PSScriptRoot 'Common.ps1')

function Resolve-RepositoryFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [switch]$AllowDirectory
    )

    $candidate = if ([System.IO.Path]::IsPathRooted($Path)) {
        [System.IO.Path]::GetFullPath($Path)
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $Path))
    }
    $prefix = $RepositoryRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Verification target escapes the repository: $Path"
    }
    $pathType = if ($AllowDirectory) { 'Container' } else { 'Leaf' }
    if (-not (Test-Path -LiteralPath $candidate -PathType $pathType)) {
        throw "Verification target is missing: $candidate"
    }
    return $candidate
}

function Get-WorkspaceFingerprint {
    param(
        [string[]]$Paths,
        [Parameter(Mandatory)][string]$Revision
    )

    $entries = [System.Collections.Generic.List[string]]::new()
    $entries.Add("HEAD=$Revision")
    foreach ($path in @($Paths | Sort-Object -Unique)) {
        $fullPath = Join-Path $RepositoryRoot ([string]$path)
        if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            $entries.Add("$path=$((Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant())")
        }
        elseif (Test-Path -LiteralPath $fullPath -PathType Container) {
            foreach ($file in Get-ChildItem -LiteralPath $fullPath -File -Recurse | Sort-Object FullName) {
                $relative = [System.IO.Path]::GetRelativePath($RepositoryRoot, $file.FullName).Replace('\', '/')
                $entries.Add("$relative=$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant())")
            }
        }
        else {
            $entries.Add("$path=<missing>")
        }
    }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($entries -join "`n")
    return [System.Convert]::ToHexString(
        [System.Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Write-CommandOutput {
    param([object[]]$Output)
    foreach ($line in $Output) {
        Write-Host ([string]$line)
    }
}

function Get-TrxCounters {
    param([Parameter(Mandatory)][string]$Path)

    [xml]$document = Get-Content -LiteralPath $Path -Raw
    $counters = $document.SelectSingleNode("//*[local-name()='Counters']")
    if ($null -eq $counters) {
        throw "TRX counters are missing: $Path"
    }
    return [pscustomobject]@{
        total = [int]$counters.GetAttribute('total')
        executed = [int]$counters.GetAttribute('executed')
        passed = [int]$counters.GetAttribute('passed')
        failed = [int]$counters.GetAttribute('failed')
        skipped = [int]$counters.GetAttribute('notExecuted')
    }
}

function Invoke-External {
    param(
        [Parameter(Mandatory)][string]$Command,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    $output = @(& $Command @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    Write-CommandOutput -Output $output
    return [pscustomobject]@{ exit_code = $exitCode; output = @($output | ForEach-Object { [string]$_ }) }
}

function Invoke-DotnetTestStep {
    param(
        [Parameter(Mandatory)]$Step,
        [Parameter(Mandatory)][string]$RunRoot
    )

    $project = Resolve-RepositoryFile ([string]$Step.target)
    $safeId = ([string]$Step.id) -replace '[^A-Za-z0-9_.-]', '-'
    $trxName = "$safeId.trx"
    $arguments = @(
        'test', $project,
        '--configuration', ([string]$Step.configuration),
        '--logger', 'console;verbosity=minimal',
        '--logger', "trx;LogFileName=$trxName",
        '--results-directory', $RunRoot,
        '--nologo'
    )
    if (-not $Restore) {
        $arguments += '--no-restore'
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$Step.filter)) {
        $arguments += @('--filter', [string]$Step.filter)
    }

    $execution = Invoke-External -Command 'dotnet' -Arguments $arguments
    $trxPath = Join-Path $RunRoot $trxName
    if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
        throw "dotnet test did not produce its TRX result: $trxPath"
    }
    $counts = Get-TrxCounters -Path $trxPath
    if ($counts.executed -eq 0) {
        $exception = [System.InvalidOperationException]::new(
            "Zero tests executed for '$($Step.id)'. The selection is invalid. TRX: $trxPath")
        $exception.Data['TestCounts'] = $counts
        $exception.Data['Artifact'] = $trxPath
        throw $exception
    }
    if ($execution.exit_code -ne 0 -or $counts.failed -ne 0) {
        $exception = [System.InvalidOperationException]::new(
            "Test step '$($Step.id)' failed: passed=$($counts.passed) failed=$($counts.failed) skipped=$($counts.skipped).")
        $exception.Data['TestCounts'] = $counts
        $exception.Data['Artifact'] = $trxPath
        throw $exception
    }
    return [pscustomobject]@{
        exit_code = $execution.exit_code
        tests = $counts
        artifact = $trxPath
    }
}

function Invoke-PythonTestStep {
    param([Parameter(Mandatory)]$Step)

    $testDirectory = Resolve-RepositoryFile ([string]$Step.target) -AllowDirectory
    $pattern = if (@($Step.arguments).Count -gt 0) { [string]$Step.arguments[0] } else { 'test_*.py' }
    $python = Get-Command python -ErrorAction SilentlyContinue
    $command = if ($null -ne $python) { $python.Source } else { 'py' }
    $prefix = if ($null -ne $python) { @() } else { @('-3') }
    $execution = Invoke-External -Command $command -Arguments ($prefix + @(
            '-m', 'unittest', 'discover', '-s', $testDirectory, '-p', $pattern, '-v'))
    $combined = $execution.output -join "`n"
    $matches = [regex]::Matches($combined, 'Ran\s+(\d+)\s+tests?')
    if ($matches.Count -eq 0) {
        throw "Python test count was not reported for '$($Step.id)'."
    }
    $executed = [int]$matches[$matches.Count - 1].Groups[1].Value
    if ($executed -eq 0) {
        $counts = [pscustomobject]@{ total = 0; executed = 0; passed = 0; failed = 0; skipped = 0 }
        $exception = [System.InvalidOperationException]::new(
            "Zero Python tests executed for '$($Step.id)'. The selection is invalid.")
        $exception.Data['TestCounts'] = $counts
        throw $exception
    }
    $skippedMatch = [regex]::Match($combined, '(?m)^(?:OK|FAILED).*skipped=(\d+)')
    $skipped = if ($skippedMatch.Success) { [int]$skippedMatch.Groups[1].Value } else { 0 }
    if ($execution.exit_code -ne 0 -or $combined -notmatch '(?m)^OK(?:\s|$)') {
        $failureMatch = [regex]::Match($combined, '(?m)^FAILED.*failures=(\d+)')
        $errorMatch = [regex]::Match($combined, '(?m)^FAILED.*errors=(\d+)')
        $failed = $(if ($failureMatch.Success) { [int]$failureMatch.Groups[1].Value } else { 0 }) +
            $(if ($errorMatch.Success) { [int]$errorMatch.Groups[1].Value } else { 0 })
        if ($failed -eq 0) { $failed = $executed }
        $counts = [pscustomobject]@{
            total = $executed
            executed = $executed
            passed = [math]::Max(0, $executed - $failed - $skipped)
            failed = $failed
            skipped = $skipped
        }
        $exception = [System.InvalidOperationException]::new(
            "Python test step '$($Step.id)' failed after $executed tests.")
        $exception.Data['TestCounts'] = $counts
        throw $exception
    }
    return [pscustomobject]@{
        exit_code = $execution.exit_code
        tests = [pscustomobject]@{
            total = $executed; executed = $executed; passed = $executed - $skipped
            failed = 0; skipped = $skipped
        }
        artifact = ''
    }
}

function Invoke-PowerShellSyntaxStep {
    param([Parameter(Mandatory)]$Step)

    $path = Resolve-RepositoryFile ([string]$Step.target)
    $tokens = $null
    $errors = $null
    $null = [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors)
    if ($errors.Count -gt 0) {
        $details = @($errors | ForEach-Object { "$($_.Extent.StartLineNumber): $($_.Message)" }) -join '; '
        throw "PowerShell syntax failed for '$path': $details"
    }
    Write-Host "PowerShell syntax passed: $path"
    return [pscustomobject]@{ exit_code = 0; tests = $null; artifact = '' }
}

function Invoke-TextCheckStep {
    param([Parameter(Mandatory)]$Step)

    $checked = 0
    foreach ($relativePath in @($Step.arguments | ForEach-Object { [string]$_ })) {
        $path = Resolve-RepositoryFile $relativePath
        $lineNumber = 0
        foreach ($line in Get-Content -LiteralPath $path -Encoding utf8) {
            $lineNumber++
            if ($line -match '[ \t]+$') {
                throw "Trailing whitespace in '$relativePath' at line $lineNumber."
            }
            if ($line -match '^(<<<<<<<|=======|>>>>>>>)') {
                throw "Merge conflict marker in '$relativePath' at line $lineNumber."
            }
        }
        $checked++
    }
    if ($checked -eq 0) {
        throw "Text check '$($Step.id)' selected zero files."
    }
    Write-Host "Text checks passed: files=$checked"
    return [pscustomobject]@{ exit_code = 0; tests = $null; artifact = '' }
}

function Invoke-ScriptStep {
    param([Parameter(Mandatory)]$Step)

    $allowedScripts = @(
        'Update-AuroraParityTracker.ps1',
        'Test-AuroraEvidencePaths.ps1',
        'Update-CompleteFunctionBacklog.ps1',
        'Update-StudioAuroraAccessibilityPlan.ps1',
        'Test-TargetedVerificationPlanner.ps1',
        'Verify-StudioDistribution.ps1',
        'Verify-RuntimeDistribution.ps1'
    )
    $path = Resolve-RepositoryFile ([string]$Step.target)
    if ([System.IO.Path]::GetFileName($path) -notin $allowedScripts) {
        throw "Plan requested a non-allowlisted script: $path"
    }
    $pwsh = Join-Path $PSHOME 'pwsh.exe'
    $execution = Invoke-External -Command $pwsh -Arguments (@(
            '-NoProfile', '-NonInteractive', '-File', $path) + @($Step.arguments | ForEach-Object { [string]$_ }))
    if ($execution.exit_code -ne 0) {
        throw "Script step '$($Step.id)' failed with exit code $($execution.exit_code)."
    }
    return [pscustomobject]@{ exit_code = 0; tests = $null; artifact = '' }
}

function Invoke-StudioSmokeStep {
    param([Parameter(Mandatory)]$Step)

    $path = Resolve-RepositoryFile ([string]$Step.target)
    if ([System.IO.Path]::GetFileName($path) -ne 'Open-Studio.ps1') {
        throw "Studio smoke must use scripts/Open-Studio.ps1: $path"
    }
    $arguments = @('-NoProfile', '-NonInteractive', '-File', $path) +
        @($Step.arguments | ForEach-Object { [string]$_ }) +
        @('-Headless', '-Smoke', '-Wait', '-SkipBuild')
    $execution = Invoke-External -Command (Join-Path $PSHOME 'pwsh.exe') -Arguments $arguments
    if ($execution.exit_code -ne 0) {
        throw "Studio smoke '$($Step.id)' failed with exit code $($execution.exit_code)."
    }
    return [pscustomobject]@{ exit_code = 0; tests = $null; artifact = 'artifacts/verification/studio-shell/studio-interactive.log' }
}

function Invoke-DotnetBuildStep {
    param([Parameter(Mandatory)]$Step)

    $target = Resolve-RepositoryFile ([string]$Step.target)
    $arguments = @('build', $target, '--configuration', [string]$Step.configuration, '--nologo')
    if (-not $Restore) {
        $arguments += '--no-restore'
    }
    $execution = Invoke-External -Command 'dotnet' -Arguments $arguments
    if ($execution.exit_code -ne 0) {
        throw "Build step '$($Step.id)' failed with exit code $($execution.exit_code)."
    }
    return [pscustomobject]@{ exit_code = 0; tests = $null; artifact = '' }
}

function Invoke-DotnetRunStep {
    param([Parameter(Mandatory)]$Step)

    $target = Resolve-RepositoryFile ([string]$Step.target)
    $arguments = @('run', '--project', $target, '--configuration', [string]$Step.configuration)
    if (-not $Restore) { $arguments += '--no-restore' }
    $arguments += '--'
    $arguments += @($Step.arguments | ForEach-Object { [string]$_ })
    $execution = Invoke-External -Command 'dotnet' -Arguments $arguments
    $combined = $execution.output -join "`n"
    if ($execution.exit_code -ne 0 -or
        (-not [string]::IsNullOrWhiteSpace([string]$Step.expected_marker) -and
            $combined -notmatch [regex]::Escape([string]$Step.expected_marker))) {
        throw "dotnet run step '$($Step.id)' failed or missed marker '$($Step.expected_marker)'."
    }
    return [pscustomobject]@{ exit_code = 0; tests = $null; artifact = '' }
}

function Invoke-GraphStep {
    param([Parameter(Mandatory)]$Step, [switch]$Generate)

    $target = Resolve-RepositoryFile ([string]$Step.target)
    $arguments = @('run', '--project', $target, '--configuration', 'Release')
    if (-not $Restore) { $arguments += '--no-restore' }
    $arguments += @('--', $(if ($Generate) { 'generate' } else { 'check' }))
    if ($Generate) {
        $arguments += @('--output', (Join-Path $RepositoryRoot 'docs/architecture/graph.json'))
    }
    else {
        $arguments += @($Step.arguments | ForEach-Object { [string]$_ })
    }
    $execution = Invoke-External -Command 'dotnet' -Arguments $arguments
    if ($execution.exit_code -ne 0) {
        throw "Architecture graph step '$($Step.id)' failed with exit code $($execution.exit_code)."
    }
    return [pscustomobject]@{ exit_code = 0; tests = $null; artifact = 'docs/architecture/graph.json' }
}

function Invoke-EngineSmokeStep {
    param(
        [Parameter(Mandatory)]$Step,
        [Parameter(Mandatory)][string]$RunRoot
    )

    $projectPath = Resolve-RepositoryFile ([string]$Step.target) -AllowDirectory
    $enginePath = Get-ForgeEnginePath
    $safeId = ([string]$Step.id) -replace '[^A-Za-z0-9_.-]', '-'
    $logPath = Join-Path $RunRoot "$safeId.log"
    $importLogPath = Join-Path $RunRoot "$safeId-import.log"
    Invoke-ForgeProjectImport -EnginePath $enginePath -ProjectPath $projectPath -LogPath $importLogPath
    $provided = @($Step.arguments | ForEach-Object { [string]$_ })
    $separator = [array]::IndexOf($provided, '--')
    if ($separator -ge 0) {
        $engineArguments = if ($separator -gt 0) { @($provided[0..($separator - 1)]) } else { @() }
        $userArguments = if ($separator + 1 -lt $provided.Count) { @($provided[($separator + 1)..($provided.Count - 1)]) } else { @() }
    }
    else {
        $engineArguments = @($provided)
        $userArguments = @()
    }
    if ($engineArguments -contains '--headless' -and $engineArguments -notcontains '--rendering-method') {
        $engineArguments = @('--rendering-method', 'gl_compatibility') + $engineArguments
    }
    $engineArguments += @('--path', $projectPath, '--log-file', $logPath, '--') + $userArguments
    $launch = @{
        FilePath = $enginePath
        ArgumentList = $engineArguments
        WorkingDirectory = $projectPath
        PassThru = $true
        WindowStyle = 'Hidden'
    }
    $process = Start-Process @launch
    $timeout = if ([int]$Step.timeout_seconds -gt 0) { [int]$Step.timeout_seconds } else { 120 }
    if (-not $process.WaitForExit($timeout * 1000)) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        throw "Engine smoke '$($Step.id)' timed out after $timeout seconds. Log: $logPath"
    }
    $log = if (Test-Path -LiteralPath $logPath -PathType Leaf) {
        Get-Content -LiteralPath $logPath -Raw
    }
    else { '' }
    if ($process.ExitCode -ne 0 -or
        $log -notmatch [regex]::Escape([string]$Step.expected_marker) -or
        $log -match '(?m)^ERROR:|OPENRPG_FORGE_.*_(FAILED|INVALID)') {
        throw "Engine smoke '$($Step.id)' failed or missed marker '$($Step.expected_marker)'. Log: $logPath"
    }
    Write-Host "Engine smoke passed: $($Step.expected_marker)"
    return [pscustomobject]@{ exit_code = 0; tests = $null; artifact = $logPath }
}

function Invoke-FullGateStep {
    param([Parameter(Mandatory)]$Step)

    $path = Resolve-RepositoryFile ([string]$Step.target)
    if ([System.IO.Path]::GetFileName($path) -ne 'Verify.ps1') {
        throw "Full gate must use scripts/Verify.ps1: $path"
    }
    $execution = Invoke-External -Command (Join-Path $PSHOME 'pwsh.exe') -Arguments @(
        '-NoProfile', '-NonInteractive', '-File', $path, '-Profile', 'Full')
    if ($execution.exit_code -ne 0 -or
        ($execution.output -join "`n") -notmatch 'OPENRPG_FORGE_VERIFICATION=PASS') {
        throw "Canonical full gate failed with exit code $($execution.exit_code)."
    }
    return [pscustomobject]@{ exit_code = 0; tests = $null; artifact = 'artifacts/verification' }
}

if (-not [string]::IsNullOrWhiteSpace($PlanPath)) {
    $resolvedPlan = Resolve-RepositoryFile $PlanPath
    $plan = Get-Content -LiteralPath $resolvedPlan -Raw | ConvertFrom-Json
}
else {
    $planner = Join-Path $PSScriptRoot 'Get-VerificationPlan.ps1'
    $plannerArguments = @{
        RepositoryRoot = $RepositoryRoot
        Stage = $Stage
        Profile = $Profile
        Page = $Page
        Evidence = $Evidence
        StudioSmokeScope = $StudioSmokeScope
        DistributionProduct = $DistributionProduct
        OutputFormat = 'Json'
    }
    if ($PSBoundParameters.ContainsKey('ChangedPath')) { $plannerArguments.ChangedPath = $ChangedPath }
    if ($PSBoundParameters.ContainsKey('FeatureId')) { $plannerArguments.FeatureId = $FeatureId }
    if ($PSBoundParameters.ContainsKey('DistributionDirectory')) {
        $plannerArguments.DistributionDirectory = $DistributionDirectory
    }
    if ($NoGraph) { $plannerArguments.NoGraph = $true }
    $planJson = & $planner @plannerArguments
    if (-not $?) {
        throw 'Verification planner failed.'
    }
    $plan = ($planJson -join "`n") | ConvertFrom-Json
}

if ([int]$plan.schema_version -ne 1) {
    throw "Unsupported verification plan schema: $($plan.schema_version)"
}
if (@($plan.steps).Count -eq 0) {
    throw 'Verification plan contains zero steps; execution is forbidden.'
}
Push-Location $RepositoryRoot
try {
    $currentRevision = (& git rev-parse HEAD).Trim()
}
finally {
    Pop-Location
}
if (-not $AllowStalePlan -and [string]$plan.revision -ne $currentRevision) {
    throw "Verification plan is stale: planned=$($plan.revision) current=$currentRevision"
}
if (-not $AllowStalePlan -and
    $plan.PSObject.Properties.Name -contains 'workspace_fingerprint') {
    $currentFingerprint = Get-WorkspaceFingerprint -Paths @($plan.changed_paths) -Revision $currentRevision
    if ([string]$plan.workspace_fingerprint -ne $currentFingerprint) {
        throw "Verification plan is stale: the selected workspace files changed after planning."
    }
}

if ($PlanOnly) {
    $plan | ConvertTo-Json -Depth 10
    return
}

$runId = "{0}-{1}" -f ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')),
    ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$runRoot = Join-Path $RepositoryRoot "artifacts/verification/targeted/$runId"
[System.IO.Directory]::CreateDirectory($runRoot) | Out-Null
$plan | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $runRoot 'plan.json') -Encoding utf8

$results = [System.Collections.Generic.List[object]]::new()
$overall = [System.Diagnostics.Stopwatch]::StartNew()
$totalExecuted = 0
$totalPassed = 0
$totalFailed = 0
$totalSkipped = 0

Push-Location $RepositoryRoot
try {
    if ($Restore) {
        Write-Output 'TARGETED_VERIFICATION_STEP_BEGIN id=restore-locked kind=dotnet-restore'
        $restoreWatch = [System.Diagnostics.Stopwatch]::StartNew()
        $restoreResult = Invoke-External -Command 'dotnet' -Arguments @(
            'restore', (Join-Path $RepositoryRoot 'OpenRPGForge.sln'), '--locked-mode')
        $restoreWatch.Stop()
        if ($restoreResult.exit_code -ne 0) {
            throw "Locked restore failed with exit code $($restoreResult.exit_code)."
        }
        $results.Add([pscustomobject][ordered]@{
            id = 'restore-locked'; kind = 'dotnet-restore'; status = 'passed'
            duration_seconds = [math]::Round($restoreWatch.Elapsed.TotalSeconds, 2)
            tests = $null; artifact = ''
        })
    }

    foreach ($step in @($plan.steps)) {
        Write-Output "TARGETED_VERIFICATION_STEP_BEGIN id=$($step.id) kind=$($step.kind)"
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            $outcome = switch ([string]$step.kind) {
                'dotnet-test' { Invoke-DotnetTestStep -Step $step -RunRoot $runRoot }
                'python-unittest' { Invoke-PythonTestStep -Step $step }
                'powershell-syntax' { Invoke-PowerShellSyntaxStep -Step $step }
                'text-check' { Invoke-TextCheckStep -Step $step }
                'script' { Invoke-ScriptStep -Step $step }
                'studio-smoke' { Invoke-StudioSmokeStep -Step $step }
                'dotnet-build' { Invoke-DotnetBuildStep -Step $step }
                'dotnet-run' { Invoke-DotnetRunStep -Step $step }
                'graph-check' { Invoke-GraphStep -Step $step }
                'graph-generate' { Invoke-GraphStep -Step $step -Generate }
                'engine-smoke' { Invoke-EngineSmokeStep -Step $step -RunRoot $runRoot }
                'full-gate' { Invoke-FullGateStep -Step $step }
                default { throw "Unsupported verification step kind: $($step.kind)" }
            }
            $watch.Stop()
            if ($null -ne $outcome.tests) {
                $totalExecuted += [int]$outcome.tests.executed
                $totalPassed += [int]$outcome.tests.passed
                $totalFailed += [int]$outcome.tests.failed
                $totalSkipped += [int]$outcome.tests.skipped
            }
            $results.Add([pscustomobject][ordered]@{
                id = [string]$step.id
                kind = [string]$step.kind
                status = 'passed'
                duration_seconds = [math]::Round($watch.Elapsed.TotalSeconds, 2)
                tests = $outcome.tests
                artifact = [string]$outcome.artifact
            })
            Write-Output "TARGETED_VERIFICATION_STEP_PASS id=$($step.id) duration=$([math]::Round($watch.Elapsed.TotalSeconds, 2))s"
        }
        catch {
            $watch.Stop()
            $failureTests = if ($_.Exception.Data.Contains('TestCounts')) {
                $_.Exception.Data['TestCounts']
            }
            else { $null }
            $failureArtifact = if ($_.Exception.Data.Contains('Artifact')) {
                [string]$_.Exception.Data['Artifact']
            }
            else { '' }
            if ($null -ne $failureTests) {
                $totalExecuted += [int]$failureTests.executed
                $totalPassed += [int]$failureTests.passed
                $totalFailed += [int]$failureTests.failed
                $totalSkipped += [int]$failureTests.skipped
            }
            $results.Add([pscustomobject][ordered]@{
                id = [string]$step.id
                kind = [string]$step.kind
                status = 'failed'
                duration_seconds = [math]::Round($watch.Elapsed.TotalSeconds, 2)
                tests = $failureTests
                artifact = $failureArtifact
                error = $_.Exception.Message
            })
            throw
        }
    }
}
finally {
    Pop-Location
    $overall.Stop()
    $summary = [pscustomobject][ordered]@{
        schema_version = 1
        run_id = $runId
        revision = $currentRevision
        stage = [string]$plan.stage
        profile = [string]$plan.profile
        status = if (@($results | Where-Object status -eq 'failed').Count -eq 0 -and
            $results.Count -eq @($plan.steps).Count + $(if ($Restore) { 1 } else { 0 })) { 'passed' } else { 'failed' }
        duration_seconds = [math]::Round($overall.Elapsed.TotalSeconds, 2)
        step_count = @($plan.steps).Count
        test_counts = [pscustomobject][ordered]@{
            executed = $totalExecuted
            passed = $totalPassed
            failed = $totalFailed
            skipped = $totalSkipped
        }
        results = @($results)
    }
    $summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $runRoot 'summary.json') -Encoding utf8
}

Write-Output (
    "OPENRPG_FORGE_TARGETED_VERIFICATION_PASS steps=$(@($plan.steps).Count) " +
    "tests=$totalExecuted duration=$([math]::Round($overall.Elapsed.TotalSeconds, 2))s " +
    "summary=$(Join-Path $runRoot 'summary.json')")
