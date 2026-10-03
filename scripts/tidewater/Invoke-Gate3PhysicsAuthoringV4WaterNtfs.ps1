#Requires -Version 7.2
# Gate 3 V4 authoring launcher.  This wrapper diagnoses WebView startup before
# invoking the unchanged public physics authoring smoke.  It proves neither
# runtime physics nor water contact; those remain separate playtest evidence.
[CmdletBinding(DefaultParameterSetName = 'Prepare')]
param(
    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [ValidateNotNullOrEmpty()]
    [string]$Candidate = 'E:\OpenRPG_Forge\.gate3-evidence\20261002\tidewater-authoring-004-candidate-water-ntfs\forge-studio-candidate.exe',

    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [ValidateNotNullOrEmpty()]
    [string]$FreshIsolatedFixture = 'E:\OpenRPG_Forge\.gate3-evidence\20261002\tidewater-gameplay-authoring-runtime-009\authoring-root',

    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [ValidateNotNullOrEmpty()]
    [string]$PreviousLedger = 'E:\OpenRPG_Forge\.gate3-evidence\20261002\tidewater-gameplay-authoring-runtime-009\gate3-gameplay-authoring-smoke-ledger.json',

    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [ValidateNotNullOrEmpty()]
    [string]$EvidenceRoot = 'E:\OpenRPG_Forge\.gate3-evidence\20261002\tidewater-physics-authoring-runtime-v4-water-ntfs-001',

    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [ValidateNotNullOrEmpty()]
    [string]$BrowserExecutableFolder = 'C:\Program Files (x86)\Microsoft\EdgeWebView\Application\154.0.4258.37',

    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [ValidateNotNullOrEmpty()]
    [string]$RuntimeTemp = 'E:\OpenRPG_Forge\.gate3-evidence\20261002\temporary-physics-authoring-v4-water-ntfs-001',

    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [ValidateNotNullOrEmpty()]
    [string]$ProbeSubject = 'tidewater-gate3-v4-startup-probe',

    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [switch]$ProbeOnly,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [switch]$Run,

    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [switch]$IncludeShoreFire,

    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [switch]$IncludeImpactTarget,

    [Parameter(ParameterSetName = 'Prepare')]
    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(0, 1000000)]
    [int]$ShoreFireHeatDamagePerTick = 1,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$requestedSelfTest = [bool]$SelfTest
$requestedCandidate = $Candidate
$requestedFixture = $FreshIsolatedFixture
$requestedLedger = $PreviousLedger
$requestedEvidenceRoot = $EvidenceRoot
$requestedBrowserFolder = $BrowserExecutableFolder
$requestedRuntimeTemp = $RuntimeTemp
$requestedProbeSubject = $ProbeSubject
$requestedProbeOnly = [bool]$ProbeOnly
$requestedIncludeShoreFire = [bool]$IncludeShoreFire
$requestedIncludeImpactTarget = [bool]$IncludeImpactTarget
$requestedShoreFireHeatDamagePerTick = $ShoreFireHeatDamagePerTick

# This imports only read-only CDP/process helpers.  It does not launch Studio
# on the -SelfTest path.
. (Join-Path $PSScriptRoot '..\Invoke-Gate2WorldEditingProof.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Gate3ProofStorage.ps1')

if (-not $requestedSelfTest) {
    $Candidate = $requestedCandidate
    $FreshIsolatedFixture = $requestedFixture
    $PreviousLedger = $requestedLedger
    $EvidenceRoot = $requestedEvidenceRoot
    $BrowserExecutableFolder = $requestedBrowserFolder
    $RuntimeTemp = $requestedRuntimeTemp
    $ProbeSubject = $requestedProbeSubject
    $ProbeOnly = $requestedProbeOnly
    $IncludeShoreFire = $requestedIncludeShoreFire
    $IncludeImpactTarget = $requestedIncludeImpactTarget
    $ShoreFireHeatDamagePerTick = $requestedShoreFireHeatDamagePerTick
}

$targetScript = Join-Path $PSScriptRoot 'Invoke-Gate3PhysicsAuthoringSmoke.ps1'
$expectedRuntimeVersion = '154.0.4258.37'
$sourcePin = '50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720'

function Get-V4RuntimeMetadata {
    param([Parameter(Mandatory)][string]$Folder)
    $exe = Join-Path $Folder 'msedgewebview2.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "WebView2 executable absent: $exe" }
    $item = Get-Item -LiteralPath $exe -Force
    $version = [string]$item.VersionInfo.FileVersion
    if ($version -cne $expectedRuntimeVersion) {
        throw "WebView2 runtime version mismatch: observed=$version expected=$expectedRuntimeVersion"
    }
    return [ordered]@{
        folder = [IO.Path]::GetFullPath($Folder)
        executable = [IO.Path]::GetFullPath($exe)
        file_version = $version
        product_version = [string]$item.VersionInfo.ProductVersion
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $exe).Hash.ToLowerInvariant()
    }
}

function Invoke-V4RawHttpGet {
    param([Parameter(Mandatory)][string]$Uri)
    $arguments = @{ Uri = $Uri; TimeoutSec = 2; UseBasicParsing = $true }
    if ((Get-Command Invoke-WebRequest).Parameters.ContainsKey('NoProxy')) { $arguments.NoProxy = $true }
    try {
        $response = Invoke-WebRequest @arguments
        return [ordered]@{
            ok = $true
            status_code = [int]$response.StatusCode
            content = [string]$response.Content
            error = $null
        }
    }
    catch {
        return [ordered]@{ ok = $false; status_code = $null; content = $null; error = $_.Exception.Message }
    }
}

function Get-V4WebViewProcessSnapshot {
    $items = [System.Collections.Generic.List[object]]::new()
    foreach ($name in @('msedgewebview2.exe', 'crashpad_handler.exe')) {
        try {
            foreach ($process in @(Get-CimInstance Win32_Process -Filter "Name='$name'" -ErrorAction Stop)) {
                $path = [string]$process.ExecutablePath
                $version = $null
                if (-not [string]::IsNullOrWhiteSpace($path) -and (Test-Path -LiteralPath $path -PathType Leaf)) {
                    try { $version = [string](Get-Item -LiteralPath $path).VersionInfo.FileVersion } catch { }
                }
                $items.Add([ordered]@{
                    name = $name
                    pid = [int]$process.ProcessId
                    parent_pid = [int]$process.ParentProcessId
                    executable_path = $path
                    file_version = $version
                    command_line = [string]$process.CommandLine
                })
            }
        }
        catch {
            $items.Add([ordered]@{ name = $name; query_error = $_.Exception.Message })
        }
    }
    return @($items)
}

function Get-V4EndpointState {
    param([Parameter(Mandatory)][bool]$VersionOk, [Parameter(Mandatory)][bool]$ListOk)
    if ($VersionOk -and $ListOk) { return 'both' }
    if ($VersionOk) { return 'version_only' }
    if ($ListOk) { return 'list_only' }
    return 'none'
}

function Get-V4NativeProcessMetadata {
    param([Parameter(Mandatory)][int]$ProcessId)
    try {
        $process = Get-CimInstance Win32_Process -Filter "ProcessId=$ProcessId" -ErrorAction Stop
        return [ordered]@{
            pid = [int]$process.ProcessId
            parent_pid = [int]$process.ParentProcessId
            executable_path = [string]$process.ExecutablePath
            command_line = [string]$process.CommandLine
            creation_date = [string]$process.CreationDate
        }
    }
    catch {
        return [ordered]@{ pid = $ProcessId; query_error = $_.Exception.Message }
    }
}

function Invoke-V4StartupProbe {
    param(
        [Parameter(Mandatory)][string]$CandidatePath,
        [Parameter(Mandatory)][string]$CandidateHash,
        [Parameter(Mandatory)][string]$FixtureRoot,
        [Parameter(Mandatory)][string]$ProbeRoot,
        [Parameter(Mandatory)][string]$BrowserFolder,
        [Parameter(Mandatory)][string]$ProbeTemp,
        [Parameter(Mandatory)][string]$ProbeSubject,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )
    $profile = Join-Path $ProbeRoot 'webview2-user-data'
    [void](New-Item -ItemType Directory -Force -Path $ProbeRoot, $profile, $ProbeTemp)
    $saved = @{}
    $environmentNames = @(
        'FORGE_AUTHORING_ROOT', 'FORGE_AUTHOR_SUBJECT',
        'WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', 'WEBVIEW2_USER_DATA_FOLDER',
        'WEBVIEW2_BROWSER_EXECUTABLE_FOLDER', 'TEMP', 'TMP'
    )
    foreach ($name in $environmentNames) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
    $reservation = $null
    $process = $null
    $browserSocket = $null
    $pageSocket = $null
    $observations = [System.Collections.Generic.List[object]]::new()
    $probe = [ordered]@{
        schema = 'forge.tidewater.webview-startup-probe'
        status = 'started'
        qualified = $false
        candidate = $CandidatePath
        candidate_sha256 = $CandidateHash
        fixture = $FixtureRoot
        probe_root = $ProbeRoot
        profile = $profile
        runtime_temp = $ProbeTemp
        browser_executable_folder = $BrowserFolder
        requested_environment = $null
        port = $null
        launch_utc = [DateTimeOffset]::UtcNow.ToString('o')
        native_pid = $null
        native_file_version = $null
        native_process_metadata = $null
        native_exit_observed = $false
        native_exit_code = $null
        native_exit_utc = $null
        working_directory = (Get-Location).Path
        launch_arguments = @()
        launch_window_style = 'Hidden'
        endpoint_state = 'none'
        endpoint_observed = $false
        exact_authoring_target_observed = $false
        raw_observations = @()
        cdp_sequence = [System.Collections.Generic.List[object]]::new()
        process_snapshot_start = @()
        process_snapshot_endpoint = @()
        process_snapshot_end = @()
        close = $null
        failure = $null
        environment_restored = $false
    }
    try {
        $reservation = New-LoopbackPortReservation
        $probe.port = [int]$reservation.Port
        [Environment]::SetEnvironmentVariable('FORGE_AUTHORING_ROOT', $FixtureRoot, 'Process')
        [Environment]::SetEnvironmentVariable('FORGE_AUTHOR_SUBJECT', $ProbeSubject, 'Process')
        [Environment]::SetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', "--remote-debugging-address=127.0.0.1 --remote-debugging-port=$($reservation.Port)", 'Process')
        [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER', $profile, 'Process')
        [Environment]::SetEnvironmentVariable('WEBVIEW2_BROWSER_EXECUTABLE_FOLDER', $BrowserFolder, 'Process')
        [Environment]::SetEnvironmentVariable('TEMP', $ProbeTemp, 'Process')
        [Environment]::SetEnvironmentVariable('TMP', $ProbeTemp, 'Process')
        $probe.requested_environment = [ordered]@{
            FORGE_AUTHORING_ROOT = $FixtureRoot
            FORGE_AUTHOR_SUBJECT = $ProbeSubject
            WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = [Environment]::GetEnvironmentVariable('WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', 'Process')
            WEBVIEW2_USER_DATA_FOLDER = $profile
            WEBVIEW2_BROWSER_EXECUTABLE_FOLDER = $BrowserFolder
            TEMP = $ProbeTemp
            TMP = $ProbeTemp
        }
        $reservation.Listener.Stop()
        $stdout = Join-Path $ProbeRoot 'candidate.stdout.log'
        $stderr = Join-Path $ProbeRoot 'candidate.stderr.log'
        $process = Start-Process -FilePath $CandidatePath -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        $probe.native_pid = [int]$process.Id
        $probe.native_process_metadata = Get-V4NativeProcessMetadata -ProcessId ([int]$process.Id)
        try {
            $nativePath = [string](Get-Process -Id $process.Id).Path
            if (-not [string]::IsNullOrWhiteSpace($nativePath) -and (Test-Path -LiteralPath $nativePath -PathType Leaf)) {
                $probe.native_file_version = [string](Get-Item -LiteralPath $nativePath).VersionInfo.FileVersion
            }
        }
        catch { }
        $probe.process_snapshot_start = Get-V4WebViewProcessSnapshot
        [void](Wait-LaunchedExecutableIdentity -Process $process -Candidate $CandidatePath -ExpectedHash $CandidateHash -TimeoutSeconds 15)
        $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
        $lastState = 'none'
        while ([DateTimeOffset]::UtcNow -lt $deadline) {
            $process.Refresh()
            if ($process.HasExited) {
                $probe.native_exit_observed = $true
                $probe.native_exit_code = $process.ExitCode
                $probe.native_exit_utc = [DateTimeOffset]::UtcNow.ToString('o')
                throw "Candidate exited before startup endpoints (exit code $($process.ExitCode))."
            }
            $versionObs = Invoke-V4RawHttpGet -Uri "http://127.0.0.1:$($reservation.Port)/json/version"
            $listObs = Invoke-V4RawHttpGet -Uri "http://127.0.0.1:$($reservation.Port)/json/list"
            $state = Get-V4EndpointState -VersionOk ([bool]$versionObs.ok) -ListOk ([bool]$listObs.ok)
            $lastState = $state
            if ($observations.Count -lt 120) {
                $observations.Add([ordered]@{
                    observed_utc = [DateTimeOffset]::UtcNow.ToString('o')
                    endpoint_state = $state
                    version = $versionObs
                    list = $listObs
                })
            }
            if ($state -eq 'both') {
                $probe.endpoint_observed = $true
                $probe.endpoint_state = 'both'
                $probe.process_snapshot_endpoint = Get-V4WebViewProcessSnapshot
                $versionJson = ConvertFrom-ExactJson -Json ([string]$versionObs.content)
                $targets = @(ConvertFrom-ExactJson -Json ([string]$listObs.content))
                $page = @($targets | Where-Object {
                    [string]$_.type -ceq 'page' -and -not [string]::IsNullOrWhiteSpace([string]$_.webSocketDebuggerUrl)
                } | Select-Object -First 1)
                if ($page.Count -eq 1) {
                    $probe.exact_authoring_target_observed = ([string]$page[0].title -ceq 'OpenRPG Forge Studio' -and [string]$page[0].url -ceq 'http://tauri.localhost/index.html?authoring=1')
                    try {
                        $browserSocket = Connect-CdpSocket -WebSocketUrl ([string]$versionJson.webSocketDebuggerUrl)
                        [void](Invoke-CdpCommand -Socket $browserSocket -Method 'Target.getTargets')
                        $probe.cdp_sequence.Add([ordered]@{ method = 'Target.getTargets'; status = 'passed' })
                        [void](Invoke-CdpCommand -Socket $browserSocket -Method 'Target.activateTarget' -Parameters @{ targetId = [string]$page[0].id })
                        $probe.cdp_sequence.Add([ordered]@{ method = 'Target.activateTarget'; target_id = [string]$page[0].id; status = 'passed' })
                        $pageSocket = Connect-CdpSocket -WebSocketUrl ([string]$page[0].webSocketDebuggerUrl)
                        [void](Invoke-CdpCommand -Socket $pageSocket -Method 'Runtime.enable')
                        $probe.cdp_sequence.Add([ordered]@{ method = 'Runtime.enable'; status = 'passed' })
                        [void](Invoke-CdpCommand -Socket $pageSocket -Method 'Page.enable')
                        $probe.cdp_sequence.Add([ordered]@{ method = 'Page.enable'; status = 'passed' })
                        [void](Invoke-CdpCommand -Socket $pageSocket -Method 'Page.bringToFront')
                        $probe.cdp_sequence.Add([ordered]@{ method = 'Page.bringToFront'; status = 'passed' })
                    }
                    catch {
                        $probe.cdp_sequence.Add([ordered]@{ method = 'activation_or_renderer'; status = 'failed'; error = $_.Exception.Message })
                    }
                }
                break
            }
            Start-Sleep -Milliseconds 250
        }
        $probe.raw_observations = @($observations)
        if (-not $probe.endpoint_observed) {
            $probe.endpoint_state = $lastState
            throw "CDP endpoint state remained '$lastState'; /json/version and /json/list were recorded separately."
        }
        $probe.status = 'endpoint_observed'
    }
    catch {
        $probe.status = 'failed'
        $probe.failure = $_.Exception.Message
        $probe.raw_observations = @($observations)
    }
    finally {
        if ($null -ne $pageSocket) { try { $pageSocket.Dispose() } catch { } }
        if ($null -ne $browserSocket) { try { $browserSocket.Dispose() } catch { } }
        if ($null -ne $process) {
            $close = [ordered]@{ close_main_window = $false; graceful = $false; forced = $false; pid = [int]$process.Id }
            try {
                if (-not $process.HasExited) {
                    $close.close_main_window = [bool]$process.CloseMainWindow()
                    if ($close.close_main_window) { Start-Sleep -Milliseconds 750 }
                }
                if (-not $process.HasExited) {
                    Stop-Process -Id $process.Id -Force -ErrorAction Stop
                    $close.forced = $true
                }
                $process.Refresh()
                $close.graceful = -not [bool]$close.forced
            }
            catch { $close.error = $_.Exception.Message }
        }
        $probe.close = $close
        $probe.process_snapshot_end = Get-V4WebViewProcessSnapshot
        if ($null -ne $reservation) { try { $reservation.Listener.Stop() } catch { } }
        foreach ($name in $environmentNames) {
            $value = if ($null -eq $saved[$name]) { [NullString]::Value } else { [string]$saved[$name] }
            [Environment]::SetEnvironmentVariable($name, $value, 'Process')
        }
        $probe.environment_restored = @($environmentNames | Where-Object { [Environment]::GetEnvironmentVariable($_, 'Process') -cne $saved[$_] }).Count -eq 0
        $probe.raw_observations = @($observations)
        [IO.File]::WriteAllText((Join-Path $ProbeRoot 'startup-probe.json'), ($probe | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
    }
    return [pscustomobject]$probe
}

if ($requestedSelfTest) {
    foreach ($needle in @(
            'Invoke-Gate3PhysicsAuthoringSmoke.ps1', '154.0.4258.37',
            'WEBVIEW2_BROWSER_EXECUTABLE_FOLDER', 'Target.activateTarget',
            '/json/version', '/json/list', 'process_snapshot_start',
            'endpoint_state', 'Runtime.enable', 'TEMP', 'TMP',
            'ProbeOnly', 'ProbeSubject', 'creation_date', 'launch_arguments',
            'tidewater-authoring-004-candidate-water-ntfs',
            'tidewater-physics-authoring-runtime-v4-water-ntfs-001', 'IncludeShoreFire', 'IncludeImpactTarget', 'ShoreFireHeatDamagePerTick', 'Feu de rive', 'Bloc de contrepoids', 'shore_fire'
        )) {
        if ([IO.File]::ReadAllText($PSCommandPath).IndexOf($needle, [StringComparison]::Ordinal) -lt 0) {
            throw "V4 wrapper self-test selector missing: $needle"
        }
    }
    [void](Get-V4RuntimeMetadata -Folder $requestedBrowserFolder)
    if (-not (Test-Path -LiteralPath $targetScript -PathType Leaf)) { throw "Physics authoring target is absent: $targetScript" }
    Write-Output 'TIDEWATER_GATE3_PHYSICS_AUTHORING_V4_WATER_NTFS_SELF_TEST_PASS'
    return
}

if (-not (Test-IsWindowsPlatform)) { throw 'Windows/WebView2 is required.' }
$candidate = [IO.Path]::GetFullPath($Candidate)
$fixture = [IO.Path]::GetFullPath($FreshIsolatedFixture)
$ledger = [IO.Path]::GetFullPath($PreviousLedger)
$evidence = [IO.Path]::GetFullPath($EvidenceRoot)
$browser = [IO.Path]::GetFullPath($BrowserExecutableFolder)
$runtimeTemp = [IO.Path]::GetFullPath($RuntimeTemp)
$runtime = Get-V4RuntimeMetadata -Folder $browser
foreach ($entry in @(
        @($candidate, 'V4 candidate'), @($fixture, 'Base authoring fixture'),
        @($ledger, 'Previous authoring ledger'), @($evidence, 'Fresh V4 evidence root'),
        @($runtimeTemp, 'V4 runtime temp'))) {
    Assert-Gate3ProofPath -LiteralPath $entry[0] -Description $entry[1]
}
if (Test-Path -LiteralPath $evidence) { throw "EvidenceRoot must be fresh: $evidence" }
if (Test-Path -LiteralPath $runtimeTemp) { throw "RuntimeTemp must be fresh: $runtimeTemp" }
if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "V4 candidate absent: $candidate" }
if (-not (Test-Path -LiteralPath $fixture -PathType Container)) { throw "Base authoring fixture absent: $fixture" }
if (-not (Test-Path -LiteralPath $ledger -PathType Leaf)) { throw "Previous ledger absent: $ledger" }
$candidateHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $candidate).Hash.ToLowerInvariant()
$parent = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($ledger))
if ([string]$parent.schema -cne 'forge.tidewater.gate3-gameplay-authoring-smoke' -or [string]$parent.status -cne 'passed' -or [bool]$parent.gate_qualified -or -not [bool]$parent.source_preserved) {
    throw 'Previous authoring ledger is not a passed, source-preserving, non-qualified ledger.'
}
if ((Get-NormalizedSha256 -Value ([string]$parent.source_original_hash_pinned)) -cne $sourcePin) { throw 'Previous ledger source pin differs from the V4 source pin.' }
$parentCandidateHash = Get-NormalizedSha256 -Value ([string]$parent.candidate_sha256)
if ($parentCandidateHash -ceq $candidateHash -and -not $ProbeOnly) { throw 'V4 candidate must differ from the parent authoring candidate.' }
$parentFixtureMatches = @(
    [string]$parent.source_fixture,
    [string]$parent.isolated_fixture
) | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_) -and
    [IO.Path]::GetFullPath($_).Equals($fixture, [StringComparison]::OrdinalIgnoreCase)
}
if (@($parentFixtureMatches).Count -eq 0) { throw 'Base fixture does not match either fixture path recorded by the previous authoring ledger.' }

if (-not $Run -and -not $ProbeOnly) {
    [ordered]@{
        schema = 'forge.tidewater.gate3.physics-authoring-v4-water-ntfs-ready'
        status = 'ready_no_ui_started'
        candidate = $candidate
        candidate_sha256 = $candidateHash
        source_fixture = $fixture
        previous_ledger = $ledger
        browser_runtime = $runtime
        runtime_temp = $runtimeTemp
        evidence_root = $evidence
        include_shore_fire = [bool]$IncludeShoreFire
        include_impact_target = [bool]$IncludeImpactTarget
        gate_qualified = $false
        runtime_claim = 'preparation only; no Studio process started'
    } | ConvertTo-Json -Depth 12
    return
}

$wrapperRoot = "$evidence-wrapper"
$probeRoot = Join-Path $wrapperRoot 'startup-probe'
$probeTemp = "$runtimeTemp-probe"
foreach ($path in @($wrapperRoot, $probeRoot, $probeTemp)) {
    Assert-Gate3ProofPath -LiteralPath $path -Description 'V4 diagnostic path'
    if (Test-Path -LiteralPath $path) { throw "Diagnostic path must be fresh: $path" }
}
[void](New-Item -ItemType Directory -Force -Path $wrapperRoot)
$probe = $null
if ($ProbeOnly) {
    $compareManifestPath = Join-Path $wrapperRoot 'startup-compare-manifest.json'
    try {
        $probe = Invoke-V4StartupProbe -CandidatePath $candidate -CandidateHash $candidateHash -FixtureRoot $fixture -ProbeRoot $probeRoot -BrowserFolder $browser -ProbeTemp $probeTemp -ProbeSubject $ProbeSubject -TimeoutSeconds 60
        $compareStatus = if ([bool]$probe.endpoint_observed) { 'passed_endpoint_observed' } else { 'failed_endpoint_absent' }
        $compareManifest = [ordered]@{
            schema = 'forge.tidewater.webview-startup-compare'
            status = $compareStatus
            candidate = $candidate
            candidate_sha256 = $candidateHash
            parent_candidate_sha256 = $parentCandidateHash
            source_fixture = $fixture
            previous_ledger = $ledger
            browser_runtime = $runtime
            probe_subject = $ProbeSubject
            working_directory = (Get-Location).Path
            launch_arguments = @()
            launch_window_style = 'Hidden'
            probe = $probe
            qualified = $false
            runtime_claim = 'startup comparison only; no authoring or runtime qualification'
        }
        [IO.File]::WriteAllText($compareManifestPath, ($compareManifest | ConvertTo-Json -Depth 40), [Text.UTF8Encoding]::new($false))
        Write-Output "TIDEWATER_GATE3_WEBVIEW_STARTUP_COMPARE status=$compareStatus manifest=$compareManifestPath"
        return
    }
    catch {
        $compareManifest = [ordered]@{
            schema = 'forge.tidewater.webview-startup-compare'
            status = 'failed_wrapper'
            failure = $_.Exception.Message
            candidate = $candidate
            candidate_sha256 = $candidateHash
            parent_candidate_sha256 = $parentCandidateHash
            source_fixture = $fixture
            previous_ledger = $ledger
            browser_runtime = $runtime
            probe_subject = $ProbeSubject
            working_directory = (Get-Location).Path
            launch_arguments = @()
            launch_window_style = 'Hidden'
            probe = $probe
            qualified = $false
        }
        [IO.File]::WriteAllText($compareManifestPath, ($compareManifest | ConvertTo-Json -Depth 40), [Text.UTF8Encoding]::new($false))
        throw "V4 startup comparison failed. Manifest: $compareManifestPath. $($_.Exception.Message)"
    }
}
$manifestPath = Join-Path $wrapperRoot 'v4-authoring-wrapper-manifest.json'
$savedOuter = @{}
foreach ($name in @('WEBVIEW2_BROWSER_EXECUTABLE_FOLDER', 'TEMP', 'TMP')) { $savedOuter[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$authoringFailure = $null
$authoringStatus = 'not_started'
$authoringLedger = Join-Path $evidence 'gate3-physics-authoring-smoke-ledger.json'
try {
    $probe = Invoke-V4StartupProbe -CandidatePath $candidate -CandidateHash $candidateHash -FixtureRoot $fixture -ProbeRoot $probeRoot -BrowserFolder $browser -ProbeTemp $probeTemp -ProbeSubject $ProbeSubject -TimeoutSeconds 60
    if (-not [bool]$probe.endpoint_observed) { throw "V4 startup probe failed before authoring: $($probe.failure)" }
    [void](New-Item -ItemType Directory -Force -Path $runtimeTemp)
    [Environment]::SetEnvironmentVariable('WEBVIEW2_BROWSER_EXECUTABLE_FOLDER', $browser, 'Process')
    [Environment]::SetEnvironmentVariable('TEMP', $runtimeTemp, 'Process')
    [Environment]::SetEnvironmentVariable('TMP', $runtimeTemp, 'Process')
    & $targetScript `
        -Candidate $candidate `
        -FreshIsolatedFixture $fixture `
        -PreviousLedger $ledger `
        -SourceOriginalHashPinned $sourcePin `
        -EvidenceRoot $evidence `
        -IncludeShoreFire:$IncludeShoreFire `
        -IncludeImpactTarget:$IncludeImpactTarget `
        -ShoreFireHeatDamagePerTick $ShoreFireHeatDamagePerTick `
        -StartupTimeoutSeconds 180 `
        -InteractionTimeoutSeconds 120
    $authoringStatus = 'returned'
}
catch {
    $authoringStatus = 'failed'
    $authoringFailure = $_.Exception.Message
}
finally {
    foreach ($name in @('WEBVIEW2_BROWSER_EXECUTABLE_FOLDER', 'TEMP', 'TMP')) {
        $value = if ($null -eq $savedOuter[$name]) { [NullString]::Value } else { [string]$savedOuter[$name] }
        [Environment]::SetEnvironmentVariable($name, $value, 'Process')
    }
    $manifest = [ordered]@{
        schema = 'forge.tidewater.gate3.physics-authoring-v4-water-ntfs-wrapper'
        status = $authoringStatus
        failure = $authoringFailure
        gate_qualified = $false
        candidate = $candidate
        candidate_sha256 = $candidateHash
        parent_candidate_sha256 = $parentCandidateHash
        source_original_hash_pinned = $sourcePin
        source_fixture = $fixture
        previous_ledger = $ledger
        evidence_root = $evidence
        authoring_ledger = if (Test-Path -LiteralPath $authoringLedger) { $authoringLedger } else { $null }
        browser_runtime = $runtime
        runtime_temp = $runtimeTemp
        include_shore_fire = [bool]$IncludeShoreFire
        shore_fire_heat_damage_per_tick = $ShoreFireHeatDamagePerTick
        probe_temp = $probeTemp
        startup_probe = if ($null -ne $probe) { $probe } else { $null }
        process_environment_restored = @('WEBVIEW2_BROWSER_EXECUTABLE_FOLDER', 'TEMP', 'TMP' | Where-Object { [Environment]::GetEnvironmentVariable($_, 'Process') -cne $savedOuter[$_] }).Count -eq 0
        runtime_claim = 'authoring serialization only; no runtime water/contact/physics qualification'
    }
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 40), [Text.UTF8Encoding]::new($false))
}
if ($authoringStatus -ne 'returned') { throw "V4 physics authoring failed. Manifest: $manifestPath. $authoringFailure" }
Write-Output "TIDEWATER_GATE3_PHYSICS_AUTHORING_V4_WATER_NTFS_PASS ledger=$authoringLedger manifest=$manifestPath"
