#Requires -Version 7.2
# Bounded local acquisition; a successful process is not a Gate 1 verdict.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Runtime,
    [Parameter(Mandatory)][string]$Output,
    [ValidateSet('clear', 'rain')][string]$Weather = 'clear',
    [switch]$Cycle
)
$ErrorActionPreference = 'Stop'
$runtimePath = (Resolve-Path -LiteralPath $Runtime).Path
$outputPath = [IO.Path]::GetFullPath($Output)
if ([IO.Path]::GetPathRoot($outputPath) -ne 'E:\' -or (Test-Path -LiteralPath $outputPath)) {
    throw 'Requires a fresh output directory on E:'
}
$reference = Get-Content 'E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-measures-closure-20260910-b/receipt.json' -Raw | ConvertFrom-Json
$pins = @($reference.pins | Where-Object { $_.Path -notlike '*.exe' })
foreach ($pin in $pins) {
    if ((Get-FileHash -LiteralPath $pin.Path).Hash -ne $pin.Hash) { throw "Changed input: $($pin.Path)" }
}
$pins += [pscustomobject]@{ Path = $runtimePath; Hash = (Get-FileHash -LiteralPath $runtimePath).Hash }
$arguments = [Collections.Generic.List[string]]::new()
foreach ($argument in $reference.arguments) {
    if ($argument -eq '--player-camera') { break }
    $arguments.Add($argument)
}
$arguments[$arguments.IndexOf('--weather') + 1] = $Weather
$arguments.Add('--prewarm-quality')
if ($Cycle) {
    $arguments.Add('--quality-cycle-proof')
    $arguments.Add((Join-Path $outputPath 'cycle'))
} else {
    foreach ($argument in @('--player-camera', '--walk-proof', '1:0:1', '--capture',
        (Join-Path $outputPath 'capture.png'), '--capture-frame', '300', '--metrics',
        (Join-Path $outputPath 'metrics.json'), '--metrics-warmup-frames', '120',
        '--exit-after-frames', '2400')) { $arguments.Add($argument) }
}
New-Item -ItemType Directory -Path $outputPath | Out-Null
$started = [DateTime]::UtcNow
$previousLog = $env:RUST_LOG
try {
    $env:RUST_LOG = 'warn,forge=info'
    $process = Start-Process -FilePath $runtimePath -ArgumentList $arguments.ToArray() -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $outputPath 'runtime.jsonl') -RedirectStandardError (Join-Path $outputPath 'stderr.log')
} finally { $env:RUST_LOG = $previousLog }
$reason = $null
try {
    while (-not $process.WaitForExit(1000)) {
        $process.Refresh()
        if (([DateTime]::UtcNow - $started).TotalSeconds -gt 245) { $reason = 'host_deadline'; break }
        if ($process.WorkingSet64 -gt 3GB) { $reason = 'host_memory_limit'; break }
    }
} finally {
    if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
}
$unchanged = @($pins | Where-Object { (Get-FileHash -LiteralPath $_.Path).Hash -ne $_.Hash }).Count -eq 0
$receipt = [ordered]@{
    scope = 'profile_preparation_and_optional_hot_cycle_not_performance_verdict'
    runtime = $runtimePath; arguments = $arguments.ToArray(); pins = $pins
    process_id = $process.Id; exit_code = $process.ExitCode; process_exited = $process.HasExited
    elapsed_seconds = ([DateTime]::UtcNow - $started).TotalSeconds
    interrupted = $reason; inputs_unchanged = $unchanged; gate1_pass = $false
}
$receipt | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $outputPath 'receipt.json')
$receipt | ConvertTo-Json -Depth 3
if ($reason -or -not $unchanged -or $process.ExitCode -ne 0) { throw 'Acquisition failed; inspect preserved logs' }
