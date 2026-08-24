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
$summaryPath = Join-Path $artifactRoot "$Evidence.summary.json"
$timings = [ordered]@{}
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
        $testArguments = @(
            'test', $project, '--configuration', 'Debug', '--no-restore',
            '--logger', 'console;verbosity=minimal'
        )
        if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
            $testArguments += @('--filter', $TestFilter)
        }
        Invoke-CheckedDotnet -Name $testName -Arguments $testArguments
    }

    if (-not $NoStudioSmoke) {
        $studioArguments = @(
            '-Page', $Page, '-Evidence', $Evidence, '-Headless', '-Smoke', '-Wait', '-SkipBuild'
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
        studio_smoke = -not $NoStudioSmoke
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
