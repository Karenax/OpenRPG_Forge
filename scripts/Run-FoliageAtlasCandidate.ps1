param(
    [Parameter(Mandatory)][string]$KitRoot,
    [Parameter(Mandatory)][string]$OutputRoot,
    [string]$RuntimeExe = 'E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-release-optical-20260909/release.exe',
    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string]$ExpectedRuntimeSha256 = '2C507A416D46FD39835715DB18022CC845CAD3E190FFAC40353CD92CBFB15BCA',
    [ValidateSet('capture','measure')][string]$Mode = 'capture'
)
$ErrorActionPreference = 'Stop'
if ((Get-FileHash -LiteralPath $RuntimeExe).Hash -ne $ExpectedRuntimeSha256) { throw 'Unexpected pinned runtime binary' }
$proofRoot = [IO.Path]::GetFullPath($OutputRoot)
if ((Test-Path -LiteralPath $proofRoot) -or -not $proofRoot.StartsWith('E:\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Fresh E: output required' }
$candidateKit = (Resolve-Path -LiteralPath $KitRoot).Path
$argumentTemplate = 'E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-release-optical-environment-20260909/walk-0.arguments.json'
$runtimeArguments = [Collections.Generic.List[string]]::new()
foreach ($argument in (Get-Content -Raw -LiteralPath $argumentTemplate | ConvertFrom-Json)) { $runtimeArguments.Add($argument) }
foreach ($pair in @(@('--asset-kit-build','build.json'),@('--asset-kit-preview','preview.json'),@('--prepare-vegetation-catalog','prototype-catalog.json'))) {
    $runtimeArguments[$runtimeArguments.IndexOf($pair[0])+1] = Join-Path $candidateKit $pair[1]
}
if ($Mode -eq 'capture') {
    [void]$runtimeArguments.Remove('--walk-proof-measure-moving')
    $runtimeArguments[$runtimeArguments.IndexOf('--walk-proof')+1] = '500:1:0.22'
    $runtimeArguments[$runtimeArguments.IndexOf('--exit-after-frames')+1] = '1600'
    $runtimeArguments.AddRange([string[]]@('--capture',(Join-Path $proofRoot 'capture.png'),'--capture-frame','1400'))
}
$pinFiles = @($runtimeExe,$argumentTemplate,(Join-Path $candidateKit 'build.json'),(Join-Path $candidateKit 'preview.json'),(Join-Path $candidateKit 'prototype-catalog.json'))
$kitBuild = Get-Content -Raw -LiteralPath (Join-Path $candidateKit 'build.json') | ConvertFrom-Json
foreach ($asset in $kitBuild.assets) { $pinFiles += Join-Path $candidateKit $asset.glb.path }
foreach ($flag in @('--walk-shadow-profile','--water-medium-input')) { $pinFiles += $runtimeArguments[$runtimeArguments.IndexOf($flag)+1] }
$pins = @{}
foreach ($pinFile in $pinFiles) { $pins[$pinFile] = (Get-FileHash -LiteralPath $pinFile).Hash }
[void](New-Item -ItemType Directory -Path $proofRoot)
$pins | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $proofRoot 'pins.json')
$runtimeArguments.ToArray() | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $proofRoot 'arguments.json')
$priorLogFilter = $env:RUST_LOG
try {
    $env:RUST_LOG = 'warn,forge_runtime=info,forge::runtime=info'
    $runtimeProcess = Start-Process -FilePath $runtimeExe -ArgumentList $runtimeArguments.ToArray() -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $proofRoot 'stdout.log') -RedirectStandardError (Join-Path $proofRoot 'stderr.jsonl')
} finally { $env:RUST_LOG = $priorLogFilter }
Write-Output ('RUNTIME_STARTED pid=' + $runtimeProcess.Id + ' mode=' + $Mode)
try {
    if (-not $runtimeProcess.WaitForExit(120000)) { throw 'Runtime exceeded 120 seconds' }
    $runtimeProcess.Refresh()
    if ($runtimeProcess.ExitCode -ne 0) { throw ('Runtime exit ' + $runtimeProcess.ExitCode) }
    foreach ($pinFile in $pinFiles) { if ((Get-FileHash -LiteralPath $pinFile).Hash -ne $pins[$pinFile]) { throw ('Input changed: ' + $pinFile) } }
    if ($Mode -eq 'capture' -and -not (Test-Path -LiteralPath (Join-Path $proofRoot 'capture.png'))) { throw 'Capture missing' }
    if ($Mode -eq 'measure') {
        $movingReport = $null
        foreach ($line in [IO.File]::ReadLines((Join-Path $proofRoot 'stderr.jsonl'))) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            $event = $line | ConvertFrom-Json -Depth 100
            if ($event.level -eq 'ERROR') { throw 'Runtime logged an error' }
            if ($event.event_kind -eq 'walk_moving_measurement_completed') { $movingReport = $event.report }
        }
        if ($null -eq $movingReport) { throw 'Moving report missing' }
        $movingReport | Set-Content -LiteralPath (Join-Path $proofRoot 'report.json')
        $distribution = ($movingReport | ConvertFrom-Json -Depth 100).frame
        Write-Output ('MOVING_REPORT mean_fps=' + $distribution.mean_fps + ' p95_ms=' + $distribution.p95_ms)
    }
    Write-Output 'RUNTIME_EXIT_ZERO_INPUTS_UNCHANGED'
} finally {
    if (-not $runtimeProcess.HasExited) { Stop-Process -Id $runtimeProcess.Id; $runtimeProcess.WaitForExit() }
    @{pid=$runtimeProcess.Id; exited=$runtimeProcess.HasExited; exit_code=$runtimeProcess.ExitCode} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $proofRoot 'process.json')
    Write-Output ('RUNTIME_CLOSED pid=' + $runtimeProcess.Id + ' exited=' + $runtimeProcess.HasExited)
}
