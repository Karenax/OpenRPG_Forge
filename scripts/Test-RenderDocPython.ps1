#Requires -Version 7.2
# SDK smoke in a disposable tool/profile; capture/injection requires -CaptureProbe.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Output, [switch]$CaptureProbe,
      [string]$ReplayCapture, [string]$ReplaySha256, [switch]$GpuDurations,
      [string]$BridgeCaptureManifest, [string]$InspectEvents)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'ProofProcessCleanup.ps1')
$repository = Split-Path -Parent $PSScriptRoot
$outputRoot = [IO.Path]::GetFullPath($Output)
if ([IO.Path]::GetPathRoot($outputRoot) -ne 'E:\') { throw 'Output must stay on E:' }
if (Test-Path -LiteralPath $outputRoot) { throw 'Existing output refused' }
if ((git -C $repository branch --show-current) -ne 'Rust') { throw 'Requires Rust' }
if ($InspectEvents) {
    if (-not $ReplayCapture -or $CaptureProbe -or $InspectEvents -notmatch '^[1-9][0-9]*(,[1-9][0-9]*){0,15}$') { throw 'Inspect requires replay and 1..16 event IDs' }
    $eventIds = @($InspectEvents.Split(','))
    if (@($eventIds | Select-Object -Unique).Count -ne $eventIds.Count) { throw 'Duplicate inspect event IDs' }
}
if ($BridgeCaptureManifest) {
    if (-not $CaptureProbe -or $ReplayCapture) { throw 'Bridge manifest requires capture-only mode' }
    $BridgeCaptureManifest = [IO.Path]::GetFullPath($BridgeCaptureManifest)
    if ([IO.Path]::GetPathRoot($BridgeCaptureManifest) -ne 'E:\' -or -not (Test-Path -LiteralPath $BridgeCaptureManifest -PathType Leaf)) { throw 'Bridge manifest must be an existing E: file' }
}
if ($GpuDurations -and -not $ReplayCapture) { throw 'GPU durations require an existing replay capture' }
if ($ReplayCapture) {
    if ($CaptureProbe) { throw 'Capture and replay must be separate processes' }
    if ($ReplaySha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'Replay requires SHA-256' }
    $ReplayCapture = [IO.Path]::GetFullPath($ReplayCapture)
    if ([IO.Path]::GetPathRoot($ReplayCapture) -ne 'E:\' -or [IO.Path]::GetExtension($ReplayCapture) -ne '.rdc') { throw 'Replay requires an E: .rdc file' }
    if ((Get-FileHash -LiteralPath $ReplayCapture).Hash -ne $ReplaySha256) { throw 'Capture hash mismatch' }
} elseif ($ReplaySha256) { throw 'Replay hash supplied without capture' }
if ($CaptureProbe) {
    $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Vulkan layer path overrides are ignored when elevated; refusing capture probe'
    }
}
$toolRoot = 'E:/OpenRPG_Forge/.gate1-tools/renderdoc/v1.46/official-win64-acq-001/extract/RenderDoc_1.46_64'
$expected = @{
    'qrenderdoc.exe' = 'C5E9D3F254D80A7649FBD9EAE1C67618CB19278C603710370C220DE469397277'
    'renderdoc.dll' = '809DA38E3867D9FD09CC5C30DD5310500DEE75E999166D7A14AD1EF6E9ECA65D'
}
foreach ($name in $expected.Keys) {
    if ((Get-FileHash -LiteralPath (Join-Path $toolRoot $name)).Hash -ne $expected[$name]) { throw "Tool pin mismatch: $name" }
}
$null = New-Item -ItemType Directory -Path $outputRoot
$execution = Join-Path $outputRoot 'tool'
Copy-Item -LiteralPath $toolRoot -Destination $execution -Recurse
$applicationName = 'forge-gate1-qrenderdoc-' + [Guid]::NewGuid().ToString('N')
$executable = Join-Path $execution "$applicationName.exe"
Copy-Item -LiteralPath (Join-Path $execution 'qrenderdoc.exe') -Destination $executable
$roaming = [Environment]::GetFolderPath('ApplicationData')
$profile = [IO.Path]::GetFullPath((Join-Path $roaming $applicationName))
if ((Split-Path -Parent $profile) -ne $roaming -or (Test-Path -LiteralPath $profile)) { throw 'Unsafe or existing disposable profile' }
$profileConfig = Join-Path $profile 'UI.config'
$null = New-Item -ItemType Directory -Path $profile
@{ rdocConfigData=1; Analytics_TotalOptOut=$true; CheckUpdate_AllowChecks=$false } | ConvertTo-Json | Set-Content -LiteralPath $profileConfig -Encoding utf8
$temporary = Join-Path $outputRoot 'temp'
$null = New-Item -ItemType Directory -Path $temporary
$values = @{
    FORGE_RD_SMOKE_PROFILE=$profileConfig
    FORGE_RD_SMOKE_MODEL=(Join-Path $repository 'scripts/renderdoc')
    FORGE_RD_SMOKE_OUTPUT=(Join-Path $outputRoot 'smoke.json')
    FORGE_RD_CAPTURE_PROBE=([int]$CaptureProbe.IsPresent).ToString()
    FORGE_RD_BRIDGE_MANIFEST=$BridgeCaptureManifest
    FORGE_RD_REPLAY_CAPTURE=$ReplayCapture
    FORGE_RD_REPLAY_SHA256=$ReplaySha256
    FORGE_RD_GPU_DURATIONS=([int]$GpuDurations.IsPresent).ToString()
    FORGE_RD_INSPECT_EVENTS=$InspectEvents
    WGPU_BACKEND='vulkan'
    TEMP=$temporary
    TMP=$temporary
}
$previous = @{}
if ($CaptureProbe) {
    # Process-scoped layer discovery keeps injection and Vulkan on the same DLL.
    # No registry edits; other implicit overlays are outside this diagnostic run.
    $values.VK_IMPLICIT_LAYER_PATH = Join-Path $execution 'renderdoc.json'
}
$process = $null
$hostResult = @{ status='FAIL'; reason='incomplete'; process_id=$null; process_exited=$null }
try {
    foreach ($name in $values.Keys) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $values[$name], 'Process')
    }
    $script = Join-Path $repository 'scripts/renderdoc/smoke_embedded_python.py'
    $proofStartedAt = Get-Date
    $process = Start-Process -FilePath $executable -ArgumentList @('--python', ('"' + $script + '"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $outputRoot 'stdout.log') -RedirectStandardError (Join-Path $outputRoot 'stderr.log')
    $hostResult.process_id = $process.Id
    $timeout = if ($CaptureProbe -or $ReplayCapture) { 180000 } else { 45000 }
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while (-not $process.WaitForExit(1000) -and $watch.ElapsedMilliseconds -lt $timeout) {
        if ($ReplayCapture -or $CaptureProbe) {
            $memory = Get-CimInstance Win32_OperatingSystem
            $sample = @{ elapsed_ms=$watch.ElapsedMilliseconds; free_physical_kib=$memory.FreePhysicalMemory; free_virtual_kib=$memory.FreeVirtualMemory }
            $sample | ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $outputRoot 'memory.jsonl')
            if ([long]$memory.FreePhysicalMemory -lt 2097152 -or [long]$memory.FreeVirtualMemory -lt 2097152) {
                # Only the process we just launched; never stop unrelated user applications.
                if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
                throw 'Capture/replay stopped by memory guard: less than 2 GiB available; no qualification'
            }
        }
    }
    if (-not $process.HasExited) {
        throw "Smoke timed out; cleanup required for owned process $($process.Id)"
    }
    $process.Refresh()
    if ($process.ExitCode -ne 0) { throw "Embedded Python smoke failed: $($process.ExitCode)" }
    $report = Get-Content -LiteralPath $values.FORGE_RD_SMOKE_OUTPUT -Raw | ConvertFrom-Json
    if ($report.status -ne 'PASS') { throw 'Smoke report is not PASS' }
    $hostResult.status = 'PASS'
    $hostResult.reason = 'embedded report PASS'
    $report | ConvertTo-Json -Depth 4
} catch {
    $hostResult.reason = $_.Exception.Message
    throw
} finally {
    try {
        if ($null -ne $process) {
            $hostResult.tool_cleanup = Stop-OwnedProofProcess -Process $process -ExpectedExecutable $executable
        }
        # inputs.json is written before injection, unlike the target PID receipt.
        $inputFile = Join-Path $outputRoot 'inputs.json'
        if ($CaptureProbe -and (Test-Path -LiteralPath $inputFile)) {
            $inputInfo = Get-Content -LiteralPath $inputFile -Raw | ConvertFrom-Json
            $ownedRuntimes = @(Get-CimInstance Win32_Process | Where-Object {
                Test-ProofRuntimeOwnership -Executable $_.ExecutablePath -ExpectedExecutable $inputInfo.runtime -CommandLine $_.CommandLine -OutputRoot $outputRoot
            })
            if ($ownedRuntimes.Count -gt 1) { throw 'Ambiguous runtime ownership; termination refused' }
            if ($ownedRuntimes.Count -eq 1) {
                $runtimeProcess = Get-Process -Id ([int]$ownedRuntimes[0].ProcessId) -ErrorAction SilentlyContinue
                if ($null -ne $runtimeProcess) {
                    if ($runtimeProcess.StartTime -lt $proofStartedAt) { throw 'Runtime PID reused or unrelated; refused' }
                    $hostResult.runtime_cleanup = Stop-OwnedProofProcess -Process $runtimeProcess -ExpectedExecutable $inputInfo.runtime
                }
            } else { $hostResult.runtime_cleanup = 'already_exited' }
        }
    } catch {
        $hostResult.status = 'FAIL'
        $hostResult.cleanup_error = $_.Exception.Message
        throw
    } finally {
    if ($null -ne $process) { $hostResult.process_exited = $process.HasExited }
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    $hostResult | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'host-result.json') -Encoding utf8
    # Move only our exact nonce profile after confirmed process termination; retain evidence.
    if (($null -eq $process -or $process.HasExited) -and (Test-Path -LiteralPath $profile)) {
        if ((Split-Path -Parent $profile) -ne $roaming -or (Split-Path -Leaf $profile) -ne $applicationName) { throw 'Profile ownership check failed' }
        Move-Item -LiteralPath $profile -Destination (Join-Path $outputRoot 'profile-evidence')
    }
    }
}
