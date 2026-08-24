[CmdletBinding()]
param(
    [string]$Page = 'zones',
    [string]$Evidence = 'studio-increment',
    [string]$Workspace,
    [string]$Capture,
    [string[]]$TestProject = @(
        'tests\Forge.Studio.Core.Tests\Forge.Studio.Core.Tests.csproj'
    ),
    [string]$TestFilter,
    [hashtable]$TestFilterByProject = @{},
    [ValidateSet('Route', 'Wave', 'Full')]
    [string]$SmokeScope = 'Route',
    [switch]$Restore,
    [switch]$SkipBuild,
    [switch]$NoStudioSmoke,
    [switch]$NoMatrixCheck,
    [switch]$CheckGraph
)

. (Join-Path $PSScriptRoot 'Common.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'OpenRPGForge.sln'
$studioProject = Join-Path $repositoryRoot 'apps\Forge.Studio\Forge.Studio.csproj'
$artifactRoot = Join-Path $repositoryRoot 'artifacts\verification\studio-increment'
$evidenceFileName = $Evidence -replace '[^A-Za-z0-9_.-]', '-'
if ([string]::IsNullOrWhiteSpace($evidenceFileName)) {
    throw 'Evidence must contain at least one filename-safe character.'
}
$summaryPath = Join-Path $artifactRoot "$evidenceFileName.summary.json"
$timings = [ordered]@{}
$testResults = [System.Collections.Generic.List[object]]::new()
$overall = [System.Diagnostics.Stopwatch]::StartNew()

function Resolve-ForgePath {
    param([Parameter(Mandatory)][string]$Path)

    $candidate = if ([System.IO.Path]::IsPathRooted($Path)) {
        $Path
    } else {
        Join-Path $repositoryRoot $Path
    }

    return [System.IO.Path]::GetFullPath($candidate)
}

function Invoke-CheckedDotnet {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    $step = [System.Diagnostics.Stopwatch]::StartNew()
    & dotnet @Arguments
    $exitCode = $LASTEXITCODE
    $step.Stop()
    $timings[$Name] = [math]::Round($step.Elapsed.TotalSeconds, 2)
    if ($exitCode -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $exitCode."
    }

    Write-Output "$Name passed in $($timings[$Name])s."
}

function Invoke-CheckedDotnetTest {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Project,
        [string]$Filter
    )

    [System.IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
    $trxName = "$evidenceFileName.$Name.trx"
    $trxPath = Join-Path $artifactRoot $trxName
    if (Test-Path -LiteralPath $trxPath -PathType Leaf) {
        Remove-Item -LiteralPath $trxPath -Force
    }
    $arguments = @(
        'test', $Project, '--configuration', 'Debug', '--no-restore',
        '--logger', 'console;verbosity=minimal',
        '--logger', "trx;LogFileName=$trxName",
        '--results-directory', $artifactRoot
    )
    if (-not [string]::IsNullOrWhiteSpace($Filter)) {
        $arguments += @('--filter', $Filter)
    }

    $step = [System.Diagnostics.Stopwatch]::StartNew()
    & dotnet @arguments
    $exitCode = $LASTEXITCODE
    $step.Stop()
    $timings[$Name] = [math]::Round($step.Elapsed.TotalSeconds, 2)
    if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
        throw "$Name did not produce its TRX result: $trxPath"
    }
    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
    $counters = $trx.SelectSingleNode("//*[local-name()='Counters']")
    if ($null -eq $counters) {
        throw "$Name did not report test counters in $trxPath."
    }
    $result = [pscustomobject][ordered]@{
        project = $Project
        filter = $Filter
        total = [int]$counters.GetAttribute('total')
        executed = [int]$counters.GetAttribute('executed')
        passed = [int]$counters.GetAttribute('passed')
        failed = [int]$counters.GetAttribute('failed')
        skipped = [int]$counters.GetAttribute('notExecuted')
        trx = $trxPath
    }
    $testResults.Add($result)
    if ($result.executed -eq 0) {
        throw "$Name selected zero tests. Use a project-specific filter or remove the project. TRX: $trxPath"
    }
    if ($exitCode -ne 0 -or $result.failed -ne 0) {
        throw "$Name failed: passed=$($result.passed) failed=$($result.failed) skipped=$($result.skipped)."
    }
    Write-Output "$Name passed $($result.passed)/$($result.executed) tests in $($timings[$Name])s."
}

function Invoke-CheckedScript {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    $step = [System.Diagnostics.Stopwatch]::StartNew()
    $pwsh = Join-Path $PSHOME 'pwsh.exe'
    & $pwsh -NoProfile -NonInteractive -File $Path @Arguments
    $exitCode = $LASTEXITCODE
    $step.Stop()
    $timings[$Name] = [math]::Round($step.Elapsed.TotalSeconds, 2)
    if ($exitCode -ne 0) {
        throw "$Path failed with exit code $exitCode."
    }
    Write-Output "$Name passed in $($timings[$Name])s."
}

function Invoke-CheckedStudio {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    $openStudio = Join-Path $PSScriptRoot 'Open-Studio.ps1'
    Invoke-CheckedScript -Name $Name -Path $openStudio -Arguments $Arguments
}

Push-Location $repositoryRoot
try {
    if (-not (Test-Path -LiteralPath $studioProject -PathType Leaf)) {
        throw "Forge.Studio project is missing: $studioProject"
    }

    $resolvedProjects = @($TestProject | ForEach-Object {
        $resolved = Resolve-ForgePath $_
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
            throw "Target test project is missing: $resolved"
        }
        $resolved
    })
    if (-not [string]::IsNullOrWhiteSpace($TestFilter) -and $resolvedProjects.Count -gt 1) {
        throw 'A global -TestFilter cannot target multiple projects safely. Use -TestFilterByProject with one filter per project.'
    }
    if (-not [string]::IsNullOrWhiteSpace($TestFilter) -and $TestFilterByProject.Count -gt 0) {
        throw 'Use either -TestFilter for one project or -TestFilterByProject, never both.'
    }

    if ($Restore) {
        Invoke-CheckedDotnet -Name 'restore-locked' -Arguments @(
            'restore', $solutionPath, '--locked-mode'
        )
    }

    if (-not $SkipBuild) {
        Invoke-CheckedDotnet -Name 'studio-debug-build' -Arguments @(
            'build', $studioProject, '--configuration', 'Debug', '--no-restore', '--nologo'
        )
    }

    foreach ($project in $resolvedProjects) {
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)
        $testName = "tests-$projectName"
        $relativeProject = [System.IO.Path]::GetRelativePath($repositoryRoot, $project).Replace('\', '/')
        $projectFilter = $TestFilter
        $projectFilterWasSpecified = $false
        foreach ($key in @($project, $relativeProject, $projectName, "$projectName.csproj")) {
            if ($TestFilterByProject.ContainsKey($key)) {
                $projectFilter = [string]$TestFilterByProject[$key]
                $projectFilterWasSpecified = $true
                break
            }
        }
        if ($TestFilterByProject.Count -gt 0 -and -not $projectFilterWasSpecified) {
            throw "No project-specific filter was provided for '$relativeProject'. Use an explicit empty string to request its whole suite."
        }
        Invoke-CheckedDotnetTest -Name $testName -Project $project -Filter $projectFilter
    }

    if (-not $NoStudioSmoke) {
        $studioArguments = @(
            '-Page', $Page, '-Evidence', $Evidence, '-SmokeScope', $SmokeScope,
            '-Headless', '-Smoke', '-Wait', '-SkipBuild'
        )
        if (-not [string]::IsNullOrWhiteSpace($Workspace)) {
            $studioArguments += @('-Workspace', $Workspace)
        }
        Invoke-CheckedStudio -Name 'studio-headless-smoke' -Arguments $studioArguments
    }

    if (-not [string]::IsNullOrWhiteSpace($Capture)) {
        $capturePath = Resolve-ForgePath $Capture
        $captureDirectory = Split-Path -Parent $capturePath
        if (-not [string]::IsNullOrWhiteSpace($captureDirectory)) {
            New-Item -ItemType Directory -Path $captureDirectory -Force | Out-Null
        }

        $captureArguments = @(
            '-Page', $Page, '-Evidence', $Evidence, '-Resolution', '1920x1080',
            '-Capture', $capturePath, '-Wait', '-SkipBuild'
        )
        if (-not [string]::IsNullOrWhiteSpace($Workspace)) {
            $captureArguments += @('-Workspace', $Workspace)
        }
        Invoke-CheckedStudio -Name 'studio-forward-plus-fhd-capture' -Arguments $captureArguments
    }

    if (-not $NoMatrixCheck) {
        $matrixScript = Join-Path $PSScriptRoot 'Update-StudioAuroraAccessibilityPlan.ps1'
        Invoke-CheckedScript -Name 'studio-matrix-check' -Path $matrixScript -Arguments @('-Check')
    }

    if ($CheckGraph) {
        $graphProject = Join-Path $repositoryRoot 'tools\Forge.ArchitectureGraph\Forge.ArchitectureGraph.csproj'
        $graphOutput = Join-Path $repositoryRoot 'docs\architecture\graph.json'
        Invoke-CheckedDotnet -Name 'architecture-graph-generate' -Arguments @(
            'run', '--project', $graphProject, '--configuration', 'Release', '--no-restore',
            '--', 'generate', '--output', $graphOutput
        )
        Invoke-CheckedDotnet -Name 'architecture-graph-check' -Arguments @(
            'run', '--project', $graphProject, '--configuration', 'Release', '--no-restore',
            '--', 'check', '--full'
        )
    }

    $overall.Stop()
    $summary = [ordered]@{
        profile = 'studio-increment'
        page = $Page
        evidence = $Evidence
        test_projects = $resolvedProjects
        test_filter = $TestFilter
        test_filters_by_project = $TestFilterByProject
        test_results = @($testResults)
        studio_smoke = -not $NoStudioSmoke
        studio_smoke_scope = $SmokeScope
        forward_plus_fhd_capture = -not [string]::IsNullOrWhiteSpace($Capture)
        matrix_check = -not $NoMatrixCheck
        graph_check = $CheckGraph.IsPresent
        full_gate = $false
        duration_seconds = [math]::Round($overall.Elapsed.TotalSeconds, 2)
        timings_seconds = $timings
    }
    New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
    $summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryPath -Encoding utf8
    Write-Output "OPENRPG_FORGE_STUDIO_INCREMENT_PASS duration=$($summary.duration_seconds)s summary=$summaryPath"
}
finally {
    Pop-Location
}
