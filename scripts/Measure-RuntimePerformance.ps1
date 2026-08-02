[CmdletBinding()]
param(
    [ValidateRange(1, 20)]
    [int]$Iterations = 5,
    [string]$ModulePackage = ''
)

. (Join-Path $PSScriptRoot 'Common.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$enginePath = Get-ForgeEnginePath
$runtimePath = Join-Path $repositoryRoot 'apps\Forge.Runtime'
$artifactRoot = Join-Path $repositoryRoot 'artifacts\performance'
if ([string]::IsNullOrWhiteSpace($ModulePackage)) {
    $ModulePackage = Join-Path $repositoryRoot 'artifacts\verification\crypt-of-ashes.orpmod'
}

$ModulePackage = [System.IO.Path]::GetFullPath($ModulePackage)
if (-not (Test-Path -LiteralPath $ModulePackage)) {
    throw "Module package not found: $ModulePackage"
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$runs = @()
for ($iteration = 1; $iteration -le $Iterations; $iteration++) {
    $logPath = Join-Path $artifactRoot "runtime-$iteration.log"
    $arguments = @(
        '--headless',
        '--path', $runtimePath,
        '--log-file', $logPath,
        '--',
        '--module-package', $ModulePackage,
        '--smoke-test'
    )
    $process = Start-Process `
        -FilePath $enginePath `
        -ArgumentList $arguments `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Runtime performance iteration $iteration failed with exit code $($process.ExitCode)."
    }

    $log = Get-Content -LiteralPath $logPath -Raw -Encoding UTF8
    $match = [regex]::Match(
        $log,
        'OPENRPG_FORGE_RUNTIME_PERF startup_ms=([0-9]+(?:[.,][0-9]+)?) nodes=([0-9]+) entities=([0-9]+) collisions=([0-9]+)')
    if (-not $match.Success -or $log -notmatch 'OPENRPG_FORGE_RUNTIME_SMOKE_PASS') {
        throw "Runtime performance iteration $iteration did not emit a valid performance/smoke marker."
    }

    $milliseconds = [double]::Parse(
        $match.Groups[1].Value.Replace(',', '.'),
        [System.Globalization.CultureInfo]::InvariantCulture)
    $runs += [ordered]@{
        iteration = $iteration
        startupMilliseconds = $milliseconds
        nodes = [int]$match.Groups[2].Value
        entities = [int]$match.Groups[3].Value
        collisions = [int]$match.Groups[4].Value
        log = $logPath
    }
}

$measure = $runs.startupMilliseconds | Measure-Object -Minimum -Maximum -Average
$report = [ordered]@{
    measuredAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    engine = '4.7.1-stable-mono'
    configuration = 'headless release gate'
    iterations = $Iterations
    startupMilliseconds = [ordered]@{
        minimum = [math]::Round($measure.Minimum, 3)
        average = [math]::Round($measure.Average, 3)
        maximum = [math]::Round($measure.Maximum, 3)
    }
    runs = $runs
}
$reportPath = Join-Path $artifactRoot 'runtime-performance.json'
$json = $report | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText(
    $reportPath,
    $json,
    [System.Text.UTF8Encoding]::new($false))
Write-Output (
    "OPENRPG_FORGE_PERFORMANCE_PASS iterations=$Iterations " +
    "min_ms=$($report.startupMilliseconds.minimum) " +
    "avg_ms=$($report.startupMilliseconds.average) " +
    "max_ms=$($report.startupMilliseconds.maximum) artifact=$reportPath")
