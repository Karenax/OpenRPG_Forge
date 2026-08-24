[CmdletBinding()]
param(
    [string]$WorkspaceRoot,
    [string]$OutputRoot,
    [ValidateNotNullOrEmpty()]
    [string[]]$Resolutions = @('1920x1080', '2560x1440'),
    [ValidateRange(600, 600)]
    [int]$StressFrames = 600
)

. (Join-Path $PSScriptRoot 'Common.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runtimeRoot = Join-Path $repositoryRoot 'apps\Forge.Runtime'
if ([string]::IsNullOrWhiteSpace($WorkspaceRoot)) {
    $WorkspaceRoot = Join-Path $runtimeRoot 'content\crypt-of-ashes'
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot 'artifacts\verification\runtime-combat-feedback'
}
$WorkspaceRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($WorkspaceRoot)
$OutputRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputRoot)
if (-not (Test-Path -LiteralPath $WorkspaceRoot -PathType Container)) {
    throw "The runtime combat-feedback workspace does not exist: $WorkspaceRoot"
}

$enginePath = Get-ForgeEnginePath
$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$runRoot = Join-Path $OutputRoot "run-$runId"
[System.IO.Directory]::CreateDirectory($runRoot) | Out-Null
$spawnId = '20000000-0000-4000-8000-000000000010'
$expectedCaptures = @(
    '01-pre-impact.png',
    '02-impact-peak.png',
    '03-delayed-chip.png',
    '04-stable-ready.png',
    '05-objective-toast-ping.png',
    '06-fixtures-not-gameplay.png'
)

function Wait-ForRuntimeMarkers {
    param(
        [Parameter(Mandatory)]$Process,
        [Parameter(Mandatory)][string]$LogPath,
        [Parameter(Mandatory)][string[]]$Markers,
        [int]$TimeoutSeconds = 75
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $log = ''
    $passed = $false
    do {
        Start-Sleep -Milliseconds 250
        if (Test-Path -LiteralPath $LogPath -PathType Leaf) {
            $log = Get-Content -LiteralPath $LogPath -Raw -ErrorAction SilentlyContinue
            if ($null -eq $log) { $log = '' }
            if ($log -match 'OPENRPG_FORGE_RUNTIME_STARTUP_FAILED|SCRIPT ERROR|Unhandled exception') {
                break
            }
            $passed = $true
            foreach ($marker in $Markers) {
                if ($log -notmatch [regex]::Escape($marker)) {
                    $passed = $false
                    break
                }
            }
        }
        if ($Process.HasExited -and -not $passed) { break }
    } while (-not $passed -and [DateTime]::UtcNow -lt $deadline)

    if (-not $Process.HasExited) {
        $exitDeadline = [DateTime]::UtcNow.AddSeconds($(if ($passed) { 10 } else { 1 }))
        while (-not $Process.HasExited -and [DateTime]::UtcNow -lt $exitDeadline) {
            Start-Sleep -Milliseconds 100
        }
        if (-not $Process.HasExited) {
            Stop-Process -Id $Process.Id -Force
            $Process.WaitForExit()
        }
    }
    if (-not $passed) {
        throw "Runtime markers were not observed. See $LogPath."
    }
}

function Start-RuntimeValidation {
    param(
        [Parameter(Mandatory)][string]$LogPath,
        [Parameter(Mandatory)][string[]]$RuntimeArguments,
        [Parameter(Mandatory)][string[]]$Markers,
        [switch]$Headless
    )

    $engineArguments = @('--rendering-method', 'forward_plus', '--fixed-fps', '60')
    if ($Headless) { $engineArguments += '--headless' }
    $engineArguments += @('--path', $runtimeRoot, '--log-file', $LogPath, '--')
    $engineArguments += @('--workspace', $WorkspaceRoot, '--spawn-id', $spawnId)
    $engineArguments += $RuntimeArguments
    $process = Start-Process `
        -FilePath $enginePath `
        -ArgumentList $engineArguments `
        -WorkingDirectory $runtimeRoot `
        -WindowStyle Hidden `
        -PassThru
    Wait-ForRuntimeMarkers -Process $process -LogPath $LogPath -Markers $Markers
}

function Get-PngDimensions {
    param([Parameter(Mandatory)][string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 24 -or
        $bytes[0] -ne 137 -or $bytes[1] -ne 80 -or $bytes[2] -ne 78 -or $bytes[3] -ne 71) {
        throw "The capture is not a valid PNG: $Path"
    }
    $width = ([int]$bytes[16] -shl 24) -bor ([int]$bytes[17] -shl 16) -bor
        ([int]$bytes[18] -shl 8) -bor [int]$bytes[19]
    $height = ([int]$bytes[20] -shl 24) -bor ([int]$bytes[21] -shl 16) -bor
        ([int]$bytes[22] -shl 8) -bor [int]$bytes[23]
    return [pscustomobject]@{ Width = $width; Height = $height }
}

$modes = @(
    [pscustomobject]@{ Name = 'normal'; Arguments = @() },
    [pscustomobject]@{ Name = 'reduced'; Arguments = @('--reduce-motion') }
)

$baseSmokeMarkers = @(
    'OPENRPG_FORGE_COMBAT_FEEDBACK_VISUAL_PASS',
    'OPENRPG_FORGE_COMBAT_FEEDBACK_FIXTURES_PASS',
    'OPENRPG_FORGE_COMBAT_FEEDBACK_PERFORMANCE_PASS',
    'OPENRPG_FORGE_COMBAT_FEEDBACK_AUDIO_ROUTE_PASS',
    'OPENRPG_FORGE_COMBAT_FEEDBACK_SFX_MUTE_PASS',
    'OPENRPG_FORGE_COMBAT_FEEDBACK_ACCESSIBILITY_PASS'
)

foreach ($mode in $modes) {
    $smokeLog = Join-Path $runRoot "stress-$($mode.Name).log"
    $smokeArguments = @('--combat-feedback-smoke-test') + $mode.Arguments
    $smokeMarkers = @($baseSmokeMarkers)
    if ($mode.Name -eq 'reduced') {
        $smokeMarkers += 'OPENRPG_FORGE_COMBAT_FEEDBACK_REDUCED_MOTION_PASS'
    }
    Start-RuntimeValidation -LogPath $smokeLog -RuntimeArguments $smokeArguments `
        -Markers $smokeMarkers -Headless
    Write-Output "RUNTIME_COMBAT_FEEDBACK_STRESS_PASS mode=$($mode.Name) frames=$StressFrames log=$smokeLog"
}

$textScaleSmokeLog = Join-Path $runRoot 'stress-text-scale-2.log'
Start-RuntimeValidation -LogPath $textScaleSmokeLog `
    -RuntimeArguments @('--combat-feedback-smoke-test', '--combat-feedback-text-scale', '2.0') `
    -Markers $baseSmokeMarkers -Headless
Write-Output "RUNTIME_COMBAT_FEEDBACK_STRESS_PASS mode=text-scale-2 frames=$StressFrames log=$textScaleSmokeLog"

$suiteCases = [System.Collections.Generic.List[object]]::new()
foreach ($resolution in $Resolutions) {
    if ($resolution -notmatch '^(?<width>[1-9][0-9]*)x(?<height>[1-9][0-9]*)$') {
        throw "Invalid capture resolution '$resolution'. Expected WIDTHxHEIGHT."
    }
    $expectedWidth = [int]$Matches.width
    $expectedHeight = [int]$Matches.height
    foreach ($mode in $modes) {
        $caseName = "$resolution-$($mode.Name)"
        $caseRoot = Join-Path $runRoot $caseName
        [System.IO.Directory]::CreateDirectory($caseRoot) | Out-Null
        $logPath = Join-Path $caseRoot 'runtime.log'
        $captureArguments = @('--combat-feedback-capture', $caseRoot) + $mode.Arguments
        $engineArguments = @(
            '--rendering-method', 'forward_plus',
            '--fixed-fps', '60',
            '--resolution', $resolution,
            '--path', $runtimeRoot,
            '--log-file', $logPath,
            '--',
            '--workspace', $WorkspaceRoot,
            '--spawn-id', $spawnId
        ) + $captureArguments + @('--combat-feedback-capture-size', $resolution)
        $process = Start-Process `
            -FilePath $enginePath `
            -ArgumentList $engineArguments `
            -WorkingDirectory $runtimeRoot `
            -WindowStyle Hidden `
            -PassThru
        Wait-ForRuntimeMarkers -Process $process -LogPath $logPath `
            -Markers @('OPENRPG_FORGE_COMBAT_FEEDBACK_CAPTURE_PASS')

        $reportPath = Join-Path $caseRoot 'capture-report.json'
        if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
            throw "The capture report is missing: $reportPath"
        }
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        if (-not $report.passed -or $report.resolution -ne $resolution -or
            $report.mode -ne $mode.Name -or @($report.captures).Count -ne $expectedCaptures.Count) {
            throw "The capture report contract failed for '$caseName'. See $reportPath."
        }
        $actualGameplay = @($report.captures | Where-Object { $_.actualGameplay }).Count
        $fixtureCaptures = @($report.captures | Where-Object { -not $_.actualGameplay }).Count
        if ($actualGameplay -ne 5 -or $fixtureCaptures -ne 1) {
            throw "The gameplay/fixture labels are incorrect for '$caseName'."
        }

        foreach ($captureName in $expectedCaptures) {
            $capturePath = Join-Path $caseRoot $captureName
            if (-not (Test-Path -LiteralPath $capturePath -PathType Leaf)) {
                throw "The expected capture is missing: $capturePath"
            }
            $length = (Get-Item -LiteralPath $capturePath).Length
            if ($length -lt 50000) {
                throw "The capture is unexpectedly small ($length bytes): $capturePath"
            }
            $dimensions = Get-PngDimensions -Path $capturePath
            if ($dimensions.Width -ne $expectedWidth -or $dimensions.Height -ne $expectedHeight) {
                throw "Unexpected PNG dimensions for '$capturePath': $($dimensions.Width)x$($dimensions.Height)."
            }
        }

        $suiteCases.Add([pscustomobject]@{
            name = $caseName
            resolution = $resolution
            mode = $mode.Name
            report = $reportPath
            log = $logPath
            captures = $expectedCaptures
            authoritativeDamage = $report.authoritative.damage
            pools = $report.pools
            textScale = $report.textScale
        })
        Write-Output "RUNTIME_COMBAT_FEEDBACK_CAPTURE_CASE_PASS case=$caseName captures=6 report=$reportPath"
    }
}

# A dedicated 2x pass proves that the responsive HUD remains readable without
# multiplying every expensive resolution/motion combination in the suite.
$textScaleResolution = $Resolutions | Where-Object { $_ -eq '1920x1080' } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($textScaleResolution)) {
    $textScaleResolution = $Resolutions[0]
}
if ($textScaleResolution -notmatch '^(?<width>[1-9][0-9]*)x(?<height>[1-9][0-9]*)$') {
    throw "Invalid text-scale capture resolution '$textScaleResolution'."
}
$textScaleWidth = [int]$Matches.width
$textScaleHeight = [int]$Matches.height
$textScaleCaseName = "$textScaleResolution-text-scale-2"
$textScaleCaseRoot = Join-Path $runRoot $textScaleCaseName
[System.IO.Directory]::CreateDirectory($textScaleCaseRoot) | Out-Null
$textScaleLogPath = Join-Path $textScaleCaseRoot 'runtime.log'
$textScaleArguments = @(
    '--rendering-method', 'forward_plus',
    '--fixed-fps', '60',
    '--resolution', $textScaleResolution,
    '--path', $runtimeRoot,
    '--log-file', $textScaleLogPath,
    '--',
    '--workspace', $WorkspaceRoot,
    '--spawn-id', $spawnId,
    '--combat-feedback-capture', $textScaleCaseRoot,
    '--combat-feedback-text-scale', '2.0',
    '--combat-feedback-capture-size', $textScaleResolution
)
$textScaleProcess = Start-Process `
    -FilePath $enginePath `
    -ArgumentList $textScaleArguments `
    -WorkingDirectory $runtimeRoot `
    -WindowStyle Hidden `
    -PassThru
Wait-ForRuntimeMarkers -Process $textScaleProcess -LogPath $textScaleLogPath `
    -Markers @('OPENRPG_FORGE_COMBAT_FEEDBACK_CAPTURE_PASS')

$textScaleReportPath = Join-Path $textScaleCaseRoot 'capture-report.json'
if (-not (Test-Path -LiteralPath $textScaleReportPath -PathType Leaf)) {
    throw "The text-scale capture report is missing: $textScaleReportPath"
}
$textScaleReport = Get-Content -LiteralPath $textScaleReportPath -Raw | ConvertFrom-Json
if (-not $textScaleReport.passed -or
    $textScaleReport.resolution -ne $textScaleResolution -or
    $textScaleReport.mode -ne 'normal' -or
    [Math]::Abs([double]$textScaleReport.textScale - 2.0) -gt 0.001 -or
    @($textScaleReport.captures).Count -ne $expectedCaptures.Count) {
    throw "The text-scale capture report contract failed. See $textScaleReportPath."
}
$textScaleActualGameplay = @($textScaleReport.captures | Where-Object { $_.actualGameplay }).Count
$textScaleFixtureCaptures = @($textScaleReport.captures | Where-Object { -not $_.actualGameplay }).Count
if ($textScaleActualGameplay -ne 5 -or $textScaleFixtureCaptures -ne 1) {
    throw "The gameplay/fixture labels are incorrect for '$textScaleCaseName'."
}
foreach ($captureName in $expectedCaptures) {
    $capturePath = Join-Path $textScaleCaseRoot $captureName
    if (-not (Test-Path -LiteralPath $capturePath -PathType Leaf)) {
        throw "The expected text-scale capture is missing: $capturePath"
    }
    $length = (Get-Item -LiteralPath $capturePath).Length
    if ($length -lt 50000) {
        throw "The text-scale capture is unexpectedly small ($length bytes): $capturePath"
    }
    $dimensions = Get-PngDimensions -Path $capturePath
    if ($dimensions.Width -ne $textScaleWidth -or $dimensions.Height -ne $textScaleHeight) {
        throw "Unexpected PNG dimensions for '$capturePath': $($dimensions.Width)x$($dimensions.Height)."
    }
}
$suiteCases.Add([pscustomobject]@{
    name = $textScaleCaseName
    resolution = $textScaleResolution
    mode = 'normal'
    report = $textScaleReportPath
    log = $textScaleLogPath
    captures = $expectedCaptures
    authoritativeDamage = $textScaleReport.authoritative.damage
    pools = $textScaleReport.pools
    textScale = $textScaleReport.textScale
})
Write-Output "RUNTIME_COMBAT_FEEDBACK_CAPTURE_CASE_PASS case=$textScaleCaseName captures=6 report=$textScaleReportPath"

$suiteReportPath = Join-Path $runRoot 'capture-suite-report.json'
$suiteReport = [pscustomobject]@{
    schemaVersion = 1
    passed = $true
    renderer = 'forward_plus'
    stressFrames = $StressFrames
    workspaceRoot = $WorkspaceRoot
    runRoot = $runRoot
    cases = $suiteCases
    marker = 'OPENRPG_FORGE_COMBAT_FEEDBACK_CAPTURE_SUITE_PASS'
}
[System.IO.File]::WriteAllText(
    $suiteReportPath,
    ($suiteReport | ConvertTo-Json -Depth 20) + [System.Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
Write-Output "OPENRPG_FORGE_COMBAT_FEEDBACK_CAPTURE_SUITE_PASS cases=$($suiteCases.Count) captures=$($suiteCases.Count * 6) report=$suiteReportPath"
