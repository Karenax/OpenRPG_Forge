#Requires -Version 7.2
# Gate 3 G3-10 performance evidence harvester.  It collects one bounded
# 1920x1080 High Studio observation and, when explicitly requested, one native
# Release metrics report for the exact pinned Tidewater scene.  It never
# promotes evidence to a Gate verdict and it never changes authored sources.
[CmdletBinding(DefaultParameterSetName = 'Run')]
param(
    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$Candidate,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$FreshIsolatedFixture,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$AuthoringLedger,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$SourceOriginalHashPinned,

    [Parameter(ParameterSetName = 'Run', Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$EvidenceRoot,

    [Parameter(ParameterSetName = 'Run')]
    [guid]$ZoneUuid = 'e620dd19-ead9-5913-a6d6-0f93109148c0',

    [Parameter(ParameterSetName = 'Run')]
    [string]$NativeRuntime,

    [Parameter(ParameterSetName = 'Run')]
    [string]$NativeScene,

    [Parameter(ParameterSetName = 'Run')]
    [string]$NativeSceneSha256,

    [Parameter(ParameterSetName = 'Run')]
    [string]$NativeBuildProof,

    [Parameter(ParameterSetName = 'Run')]
    [string]$ImportsManifestPath,

    [Parameter(ParameterSetName = 'Run')]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$ExpectedImportsManifestSha256 = '7fea2f2991cc8c3ef79f11634ce1253254319552eb94680c59f094bcb9db736b',

    [Parameter(ParameterSetName = 'Run')]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$ExpectedTerrainSha256 = '5e5e88f902df94fec5e40e3ce0f0e279245d5dca1aebfe1be224bae7bb4ee258',

    [Parameter(ParameterSetName = 'Run')]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$ExpectedEnvironmentSha256 = 'dddf01a25b409c01992bf372117646583b1f72f1f173046151ca0b33753bd097',

    [Parameter(ParameterSetName = 'Run')]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$ExpectedPlantsSha256 = '1fec8a063214ad05ceac19f277dbd17e131e04d2714378bae9239efd12224f83',

    [Parameter(ParameterSetName = 'Run')]
    [ValidatePattern('^[0-9A-Fa-f]{64}$')]
    [string]$ExpectedOceanSha256 = 'ba9da93d9012341d180712e1ddcf8143f7878770e19ce7ba477269aa24e9b879',

    [Parameter(ParameterSetName = 'Run')]
    [ValidateNotNullOrEmpty()]
    [string]$BrowserExecutableFolder = 'C:\Program Files (x86)\Microsoft\EdgeWebView\Application\154.0.4258.37',

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(15, 300)]
    [int]$StartupTimeoutSeconds = 180,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(30, 300)]
    [int]$InteractionTimeoutSeconds = 180,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(640, 3840)]
    [int]$ViewportWidth = 1920,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateRange(360, 2160)]
    [int]$ViewportHeight = 1080,

    [Parameter(ParameterSetName = 'Run')]
    [ValidateSet('high')]
    [string]$Quality = 'high',

    [Parameter(ParameterSetName = 'Run')]
    [switch]$RunNativeMetrics,

    [Parameter(ParameterSetName = 'Run')]
    [switch]$IncludeNativeWalkMeasurement,

    [Parameter(ParameterSetName = 'SelfTest', Mandatory)]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The shared gameplay smoke imports the governed CDP/fixture helpers through a
# strict parameter set.  Preserve this script's binding before dot-sourcing so
# -SelfTest never asks PowerShell to validate null Run parameters.
$performanceRequestedSelfTest = [bool]$SelfTest
$performanceRequestedCandidate = $Candidate
$performanceRequestedFixture = $FreshIsolatedFixture
$performanceRequestedAuthoringLedger = $AuthoringLedger
$performanceRequestedSourcePin = $SourceOriginalHashPinned
$performanceRequestedEvidenceRoot = $EvidenceRoot
$performanceRequestedZoneUuid = $ZoneUuid
$performanceRequestedNativeRuntime = $NativeRuntime
$performanceRequestedNativeScene = $NativeScene
$performanceRequestedNativeSceneSha256 = $NativeSceneSha256
$performanceRequestedNativeBuildProof = $NativeBuildProof
$performanceRequestedImportsManifestPath = $ImportsManifestPath
$performanceRequestedExpectedImports = $ExpectedImportsManifestSha256
$performanceRequestedExpectedTerrain = $ExpectedTerrainSha256
$performanceRequestedExpectedEnvironment = $ExpectedEnvironmentSha256
$performanceRequestedExpectedPlants = $ExpectedPlantsSha256
$performanceRequestedExpectedOcean = $ExpectedOceanSha256
$performanceRequestedBrowserFolder = $BrowserExecutableFolder
$performanceRequestedStartup = $StartupTimeoutSeconds
$performanceRequestedInteraction = $InteractionTimeoutSeconds
$performanceRequestedViewportWidth = $ViewportWidth
$performanceRequestedViewportHeight = $ViewportHeight
$performanceRequestedQuality = $Quality
$performanceRequestedRunNative = [bool]$RunNativeMetrics
$performanceRequestedWalk = [bool]$IncludeNativeWalkMeasurement

. (Join-Path $PSScriptRoot 'Invoke-Gate3GameplayPlaytestSmoke.ps1') -SelfTest | Out-Null
. (Join-Path $PSScriptRoot 'Gate3ProofStorage.ps1')

# Restore all values after helper dot-sourcing.  This is deliberately done for
# optional values too: helper scripts share the caller scope in PowerShell.
$Candidate = $performanceRequestedCandidate
$FreshIsolatedFixture = $performanceRequestedFixture
$AuthoringLedger = $performanceRequestedAuthoringLedger
$SourceOriginalHashPinned = $performanceRequestedSourcePin
$EvidenceRoot = $performanceRequestedEvidenceRoot
$ZoneUuid = $performanceRequestedZoneUuid
$NativeRuntime = $performanceRequestedNativeRuntime
$NativeScene = $performanceRequestedNativeScene
$NativeSceneSha256 = $performanceRequestedNativeSceneSha256
$NativeBuildProof = $performanceRequestedNativeBuildProof
$ImportsManifestPath = $performanceRequestedImportsManifestPath
$ExpectedImportsManifestSha256 = $performanceRequestedExpectedImports
$ExpectedTerrainSha256 = $performanceRequestedExpectedTerrain
$ExpectedEnvironmentSha256 = $performanceRequestedExpectedEnvironment
$ExpectedPlantsSha256 = $performanceRequestedExpectedPlants
$ExpectedOceanSha256 = $performanceRequestedExpectedOcean
$BrowserExecutableFolder = $performanceRequestedBrowserFolder
$StartupTimeoutSeconds = $performanceRequestedStartup
$InteractionTimeoutSeconds = $performanceRequestedInteraction
$ViewportWidth = $performanceRequestedViewportWidth
$ViewportHeight = $performanceRequestedViewportHeight
$Quality = $performanceRequestedQuality
$RunNativeMetrics = $performanceRequestedRunNative
$IncludeNativeWalkMeasurement = $performanceRequestedWalk
$SelfTest = $performanceRequestedSelfTest

$script:PerformanceZoneId = ([string]$ZoneUuid).ToLowerInvariant()
$script:PerformanceMessageId = 0

function Get-PerformanceProperty {
    param(
        [Parameter(Mandatory)][object]$Object,
        [Parameter(Mandatory)][string]$Name
    )
    if ($null -eq $Object -or -not ($Object.PSObject.Properties.Name -contains $Name)) {
        return $null
    }
    return $Object.$Name
}

function Get-PerformanceNormalizedHash {
    param([Parameter(Mandatory)][string]$Value)
    return Get-NormalizedSha256 -Value $Value
}

function Get-PerformancePathProperty {
    param(
        [Parameter(Mandatory)][object]$Object,
        [Parameter(Mandatory)][string[]]$Names
    )
    foreach ($name in $Names) {
        $property = Get-PerformanceProperty -Object $Object -Name $name
        if (-not [string]::IsNullOrWhiteSpace([string]$property)) {
            return [string]$property
        }
    }
    return $null
}

function Get-PerformanceFixtureEvidence {
    param(
        [Parameter(Mandatory)][string]$SourceRoot,
        [Parameter(Mandatory)][string]$SourcePin,
        [Parameter(Mandatory)][string]$ZoneId,
        [Parameter(Mandatory)]$Authoring
    )
    Assert-TreeHasNoReparsePoints -LiteralPath $SourceRoot -Description 'Fresh Tidewater performance fixture'
    $zoneArchive = Join-Path (Join-Path $SourceRoot $ZoneId) 'authoring.json'
    $workspaceArchive = Join-Path $SourceRoot 'workspace-v1\authoring.json'
    $sourceObject = Join-Path (Join-Path $SourceRoot 'workspace-v1\objects') "$SourcePin.json"
    foreach ($path in @($zoneArchive, $workspaceArchive, $sourceObject)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Performance fixture archive is absent: $path" }
    }
    $zoneBefore = Get-ArchiveSnapshot -LiteralPath $zoneArchive
    $workspaceBefore = Get-ArchiveSnapshot -LiteralPath $workspaceArchive
    $originalBefore = Get-ArchiveSnapshot -LiteralPath $sourceObject
    if ([string]$zoneBefore.Sha256 -cne $SourcePin -or [string]$originalBefore.Sha256 -cne $SourcePin) {
        throw 'Performance fixture does not preserve the pinned original Tidewater bytes.'
    }
    $reference = Get-WorkspaceZoneReference -Archive $workspaceArchive -ZoneId $ZoneId
    $authoredHash = Get-PerformanceNormalizedHash -Value ([string]$reference.archive_hash)
    $authoredObject = Join-Path (Join-Path $SourceRoot 'workspace-v1\objects') "$authoredHash.json"
    if (-not (Test-Path -LiteralPath $authoredObject -PathType Leaf)) { throw "Authored performance checkpoint is absent: $authoredObject" }
    $authoredBefore = Get-ArchiveSnapshot -LiteralPath $authoredObject
    if ([string]$authoredBefore.Sha256 -cne $authoredHash) { throw 'Workspace archive_hash does not match the authored checkpoint bytes.' }
    $savedReference = Get-PerformanceProperty -Object $Authoring -Name 'saved_reference'
    if ($null -ne $savedReference) {
        $ledgerArchive = Get-PerformancePathProperty -Object $savedReference -Names @('archive_hash')
        $ledgerHead = Get-PerformancePathProperty -Object $savedReference -Names @('head_hash')
        if ($ledgerArchive -and (Get-PerformanceNormalizedHash -Value $ledgerArchive) -cne $authoredHash) { throw 'Authoring ledger saved_reference.archive_hash differs from the fixture workspace reference.' }
        if ($ledgerHead -and [string]$reference.head_hash -cne $ledgerHead) { throw 'Authoring ledger saved_reference.head_hash differs from the fixture workspace reference.' }
    }
    return [ordered]@{
        zone_archive = $zoneBefore
        workspace_archive = $workspaceBefore
        source_original = $originalBefore
        authored_checkpoint = $authoredBefore
        authored_archive_hash = $authoredHash
        workspace_zone_reference = $reference
    }
}

function Assert-PerformanceAuthoringLedger {
    param(
        [Parameter(Mandatory)][string]$LedgerPath,
        [Parameter(Mandatory)][string]$CandidatePath,
        [Parameter(Mandatory)][string]$FixtureRoot,
        [Parameter(Mandatory)][string]$SourcePin,
        [Parameter(Mandatory)][string]$ExpectedImports,
        [Parameter(Mandatory)][string]$ExpectedTerrain,
        [Parameter(Mandatory)][string]$ExpectedEnvironment,
        [Parameter(Mandatory)][string]$ExpectedPlants,
        [Parameter(Mandatory)][string]$ExpectedOcean
    )
    if (-not (Test-Path -LiteralPath $LedgerPath -PathType Leaf)) { throw "Authoring ledger is absent: $LedgerPath" }
    $authoring = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($LedgerPath))
    $schema = [string](Get-PerformanceProperty -Object $authoring -Name 'schema')
    if ($schema -notmatch '^forge\.tidewater\.gate3\-.*(?:authoring|retouch)\-smoke$') { throw "Unsupported Tidewater authoring ledger schema: $schema" }
    if ([string](Get-PerformanceProperty -Object $authoring -Name 'status') -cne 'passed') { throw 'Performance source ledger is not passed.' }
    if ([bool](Get-PerformanceProperty -Object $authoring -Name 'gate_qualified')) { throw 'Input authoring ledger must not claim Gate qualification.' }
    $preserved = Get-PerformanceProperty -Object $authoring -Name 'source_preserved'
    if ($null -eq $preserved) { $preserved = Get-PerformanceProperty -Object $authoring -Name 'source_original_archive_preserved' }
    if (-not [bool]$preserved) { throw 'Input authoring ledger does not attest original source preservation.' }
    $ledgerSource = Get-PerformancePathProperty -Object $authoring -Names @('source_original_hash_pinned')
    if ($null -eq $ledgerSource -or (Get-PerformanceNormalizedHash -Value $ledgerSource) -cne $SourcePin) { throw 'Source hash pin differs from the authoring ledger.' }
    $ledgerFixture = Get-PerformancePathProperty -Object $authoring -Names @('isolated_fixture', 'source_fixture')
    if ($null -eq $ledgerFixture -or -not ([IO.Path]::GetFullPath($ledgerFixture)).Equals($FixtureRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'FreshIsolatedFixture is not the fixture recorded by the authoring ledger.' }
    $candidateHash = Get-FileSha256 -LiteralPath $CandidatePath
    $ledgerCandidate = Get-PerformancePathProperty -Object $authoring -Names @('candidate_sha256')
    if ($null -eq $ledgerCandidate) {
        $candidateBuild = Get-PerformanceProperty -Object $authoring -Name 'candidate_build_proof'
        $ledgerCandidate = Get-PerformancePathProperty -Object $candidateBuild -Names @('executable_sha256')
    }
    if ($null -eq $ledgerCandidate -or (Get-PerformanceNormalizedHash -Value $ledgerCandidate) -cne $candidateHash) { throw 'Candidate hash differs from the authoring ledger pin.' }
    $kit = Get-PerformanceProperty -Object $authoring -Name 'candidate_kit_and_imports'
    if ($null -eq $kit) { throw 'Authoring ledger has no candidate_kit_and_imports source proof.' }
    $expected = [ordered]@{
        imports_manifest_sha256 = $ExpectedImports
        terrain_sha256 = $ExpectedTerrain
        environment_sha256 = $ExpectedEnvironment
        plants_sha256 = $ExpectedPlants
        ocean_sha256 = $ExpectedOcean
    }
    foreach ($name in $expected.Keys) {
        $actual = Get-PerformancePathProperty -Object $kit -Names @($name)
        if ($null -eq $actual -or (Get-PerformanceNormalizedHash -Value $actual) -cne $expected[$name]) { throw "candidate_kit_and_imports.$name differs from the pinned source-water import hash." }
    }
    $buildProof = Get-PerformanceProperty -Object $authoring -Name 'candidate_build_proof'
    if ($null -ne $buildProof -and [string](Get-PerformanceProperty -Object $buildProof -Name 'status') -notin @('BUILD_ONLY', 'BUILD_PASS', 'passed')) {
        throw "Candidate build proof is not a build-only/pass proof: $([string](Get-PerformanceProperty -Object $buildProof -Name 'status'))"
    }
    return [ordered]@{
        schema = $schema
        candidate_sha256 = $candidateHash
        source_original_hash_pinned = $SourcePin
        imports = $expected
        build_proof_status = if ($null -ne $buildProof) { [string](Get-PerformanceProperty -Object $buildProof -Name 'status') } else { $null }
        ledger_sha256 = Get-FileSha256 -LiteralPath $LedgerPath
        authoring = $authoring
    }
}

function Get-PerformanceHardwareSnapshot {
    param([Parameter(Mandatory)][string]$BrowserFolder)
    $browserExe = Join-Path ([IO.Path]::GetFullPath($BrowserFolder)) 'msedgewebview2.exe'
    if (-not (Test-Path -LiteralPath $browserExe -PathType Leaf)) { throw "WebView2 executable absent: $browserExe" }
    $browserItem = Get-Item -LiteralPath $browserExe -Force
    $os = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
    $cpu = @(Get-CimInstance Win32_Processor -ErrorAction Stop)
    $gpu = @(Get-CimInstance Win32_VideoController -ErrorAction Stop)
    if ($cpu.Count -eq 0 -or $gpu.Count -eq 0) { throw 'Hardware preflight did not expose a processor and a display adapter.' }
    return [ordered]@{
        collected_at_utc = [DateTimeOffset]::UtcNow.ToString('o')
        powershell = $PSVersionTable.PSVersion.ToString()
        os = [ordered]@{
            caption = [string]$os.Caption
            version = [string]$os.Version
            build = [string]$os.BuildNumber
            architecture = [string]$os.OSArchitecture
            total_memory_mib = [Math]::Round(([double]$os.TotalVisibleMemorySize / 1024.0), 1)
            free_memory_mib = [Math]::Round(([double]$os.FreePhysicalMemory / 1024.0), 1)
        }
        cpu = @($cpu | ForEach-Object {
            [ordered]@{
                name = [string]$_.Name
                logical_processors = [int]$_.NumberOfLogicalProcessors
                manufacturer = [string]$_.Manufacturer
            }
        })
        adapters = @($gpu | ForEach-Object {
            [ordered]@{
                name = [string]$_.Name
                driver_version = [string]$_.DriverVersion
                adapter_compatibility = [string]$_.AdapterCompatibility
            }
        })
        webview2 = [ordered]@{
            executable = [IO.Path]::GetFullPath($browserExe)
            file_version = [string]$browserItem.VersionInfo.FileVersion
            product_version = [string]$browserItem.VersionInfo.ProductVersion
            sha256 = Get-FileSha256 -LiteralPath $browserExe
        }
    }
}

function Get-PerformanceDomSnapshot {
    param([Parameter(Mandatory)]$Socket)
    return Invoke-CdpExpression -Socket $Socket -Expression @'
(() => {
  const norm=value=>String(value??'').replace(/\s+/g,' ').trim();
  const text=id=>norm(document.getElementById(id)?.textContent);
  const json=id=>{const raw=document.getElementById(id)?.textContent??''; if(!raw) return null; try{return JSON.parse(raw)}catch(error){return {serialization_error:String(error),raw:raw.slice(0,4000)}}};
  const canvas=document.getElementById('forge-canvas')??document.getElementById('forge-authoring-canvas');
  const rect=canvas?.getBoundingClientRect();
  const viewport=canvas?{css_width:canvas.clientWidth,css_height:canvas.clientHeight,render_width:canvas.width,render_height:canvas.height,bounding_width:rect?.width??0,bounding_height:rect?.height??0,device_pixel_ratio:window.devicePixelRatio}:null;
  const scene=document.querySelector('main.studio-shell');
  const firstStatus=norm(document.querySelector('.status-stack span')?.textContent);
  return {
    captured_at_utc:new Date().toISOString(), url:String(location.href), title:document.title, ready_state:document.readyState,
    visibility_state:document.visibilityState, focused:document.hasFocus(), scene:scene?.dataset.forgeScene??'', mode:scene?.dataset.forgeMode??'',
    webgpu_status:firstStatus, frame_summary:text('gate0-frame-summary'), viewport_summary:text('studio-viewport-resolution'),
    interaction_summary:text('gate0-interaction-summary'), parity_summary:text('studio-scene-parity-summary'),
    viewport, telemetry:json('gate0-telemetry-ledger'), scene_telemetry:json('forge-scene-telemetry-ledger'),
    quality:json('studio-quality-ledger'), quality_status:text('studio-quality-status'), effective_quality:text('studio-effective-quality-status'),
    quality_motion_status:text('studio-quality-motion-status'), quality_motion:json('studio-quality-motion-proof'),
    export_status:text('studio-export-diagnostics-status'), capture_status:text('studio-capture-viewport-status'),
    export_button:!!document.querySelector('#studio-export-diagnostics'), capture_button:!!document.querySelector('#studio-capture-viewport'),
    high_buttons:[...document.querySelectorAll('button')].filter(node=>norm(node.textContent)==='High').map(node=>({disabled:!!node.disabled,visible:node.getBoundingClientRect().width>0&&node.getBoundingClientRect().height>0})),
    controls:[...document.querySelectorAll('button')].map(node=>({id:node.id,text:norm(node.textContent),disabled:!!node.disabled})).filter(item=>item.id.startsWith('studio-')||item.text==='High')
  };
})()
'@
}

function Wait-PerformanceStudioReady {
    param(
        [Parameter(Mandatory)]$Socket,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [Parameter(Mandatory)][string]$Description
    )
    return Wait-CdpReady -Socket $Socket -TimeoutSeconds $TimeoutSeconds -Description $Description -Expression @'
(() => {
  const canvas=document.getElementById('forge-canvas')??document.getElementById('forge-authoring-canvas');
  const root=document.querySelector('main.studio-shell');
  const telemetry=(document.getElementById('gate0-telemetry-ledger')?.textContent??'').trim();
  const rect=canvas?.getBoundingClientRect();
  const ready=!!root&&!!canvas&&!!rect&&rect.width>0&&rect.height>0&&telemetry.length>2;
  const webgpu=(document.querySelector('.status-stack span')?.textContent??'').replace(/\s+/g,' ').trim();
  return {ready,terminal:/WebGPU indisponible|Configuration invalide|Contrat runtime en échec/i.test(webgpu),reason:'canvas='+(rect?.width??0)+'x'+(rect?.height??0)+'; webgpu='+webgpu};
})()
'@
}

function Get-PerformanceStatusFilename {
    param([Parameter(Mandatory)][string]$Status)
    $match = [regex]::Match($Status, '(?<name>forge-[A-Za-z0-9_.-]+\.(?:json|png))', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($match.Success) { return $match.Groups['name'].Value }
    return $null
}

function Get-PerformanceCaptureDimensions {
    param([Parameter(Mandatory)][string]$Status)
    $match = [regex]::Match($Status, 'Capture GPU reçue\s+(?<width>\d+)\s+×\s+(?<height>\d+)', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $match.Success) { return $null }
    return [ordered]@{ width = [int]$match.Groups['width'].Value; height = [int]$match.Groups['height'].Value }
}

function Invoke-PerformanceStudioCollection {
    param(
        [Parameter(Mandatory)]$Run,
        [Parameter(Mandatory)][string]$EvidencePath,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [Parameter(Mandatory)][int]$TargetWidth,
        [Parameter(Mandatory)][int]$TargetHeight
    )
    $socket = $Run.Socket
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $TimeoutSeconds -Description 'authoring shell before public performance scene' -Expression @'
(() => ({ready:!!document.querySelector('main.authoring-shell'),terminal:false,reason:'authoring shell pending'}))()
'@)
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Aide & diagnostics')
    [void](Invoke-UiClick -Socket $socket -Text 'Scènes de preuve' -Selector 'a' -ScopeSelector 'details.forge-technical-menu[open]')
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $TimeoutSeconds -Description 'public Studio scene shell' -Expression @'
(() => {
  const root=document.querySelector('main.studio-shell');
  const canvas=document.getElementById('forge-canvas');
  const rect=canvas?.getBoundingClientRect();
  return {ready:!!root&&!!rect&&rect.width>0&&rect.height>0,terminal:false,reason:'Studio scene shell pending'};
})()
'@)
    [void](Wait-PerformanceStudioReady -Socket $socket -TimeoutSeconds $TimeoutSeconds -Description 'Studio public performance canvas')
    [void](Open-DetailsBySummary -Socket $socket -Summary 'Scènes de preuve')
    [void](Invoke-UiClick -Socket $socket -Text 'Vallée WorldSketch' -Selector 'a' -ScopeSelector 'details.scene-navigation[open]')
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $TimeoutSeconds -Description 'WorldSketch public scene reload' -Expression @'
(() => {
  const root=document.querySelector('main.studio-shell');
  const canvas=document.getElementById('forge-canvas');
  const scene=root?.dataset.forgeScene??'';
  const rect=canvas?.getBoundingClientRect();
  return {ready:scene==='world-sketch-visual-valley'&&!!rect&&rect.width>0&&rect.height>0,terminal:false,reason:'scene='+scene};
})()
'@)
    [void](Wait-PerformanceStudioReady -Socket $socket -TimeoutSeconds $TimeoutSeconds -Description 'WorldSketch telemetry')
    $qualityDetails = Invoke-CdpExpression -Socket $socket -Expression @'
(() => {
  const norm=value=>String(value??'').replace(/\s+/g,' ').trim();
  const button=[...document.querySelectorAll('button')].find(node=>norm(node.textContent)==='High');
  const summaries=[];
  for(let node=button?.parentElement;node;node=node.parentElement) {
    if(node.tagName==='DETAILS'&&!node.open) {
      const summary=[...node.children].find(child=>child.tagName==='SUMMARY');
      if(summary) summaries.unshift(norm(summary.textContent));
    }
  }
  return summaries;
})()
'@
    foreach ($summary in @($qualityDetails)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$summary)) { [void](Open-DetailsBySummary -Socket $socket -Summary ([string]$summary)) }
    }
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $TimeoutSeconds -Description 'enabled visible public High quality control' -Expression @'
(() => {
  const norm=value=>String(value??'').replace(/\s+/g,' ').trim();
  const buttons=[...document.querySelectorAll('button')].filter(node=>norm(node.textContent)==='High');
  const ready=buttons.length===1&&!buttons[0].disabled&&buttons[0].getBoundingClientRect().width>0&&buttons[0].getBoundingClientRect().height>0;
  const status=norm(document.getElementById('studio-quality-status')?.textContent);
  return {ready,terminal:false,reason:'High controls='+buttons.length+'; quality='+status};
})()
'@)
    $before = Get-PerformanceDomSnapshot -Socket $socket
    $high = @($before.high_buttons | Where-Object { -not [bool]$_.disabled -and [bool]$_.visible })
    if ($high.Count -ne 1) { throw "Expected exactly one enabled public High quality control, got $($high.Count)." }
    [void](Invoke-UiClick -Socket $socket -Text 'High' -Selector 'button')
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $TimeoutSeconds -Description 'public High quality observation' -Expression @'
(() => {
  const raw=(document.getElementById('studio-quality-ledger')?.textContent??'').trim();
  let value=null; try{value=raw?JSON.parse(raw):null}catch{}
  const requested=String(value?.requested_tier??'').toLowerCase();
  return {ready:requested==='high',terminal:false,reason:'requested_tier='+requested};
})()
'@)
    $highState = Get-PerformanceDomSnapshot -Socket $socket
    $exportTarget = Invoke-UiClick -Socket $socket -Text 'Exporter le diagnostic JSON' -Selector '#studio-export-diagnostics'
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $TimeoutSeconds -Description 'public JSON diagnostic export' -Expression @'
(() => { const text=(document.getElementById('studio-export-diagnostics-status')?.textContent??'').replace(/\s+/g,' ').trim(); return {ready:/Téléchargement JSON demandé/i.test(text),terminal:/Export refusé/i.test(text),reason:text||'export pending'}; })()
'@)
    $exportState = Get-PerformanceDomSnapshot -Socket $socket
    $captureTarget = Invoke-UiClick -Socket $socket -Text 'Capturer le viewport PNG' -Selector '#studio-capture-viewport'
    [void](Wait-CdpReady -Socket $socket -TimeoutSeconds $TimeoutSeconds -Description 'public viewport GPU capture' -Expression @'
(() => { const text=(document.getElementById('studio-capture-viewport-status')?.textContent??'').replace(/\s+/g,' ').trim(); return {ready:/Capture GPU reçue/i.test(text),terminal:/Capture refusée|Capture expirée/i.test(text),reason:text||'capture pending'}; })()
'@)
    $captureState = Get-PerformanceDomSnapshot -Socket $socket
    $diagnosticScreenshotPath = Join-Path $EvidencePath 'studio-public-dom-checkpoint.png'
    $diagnosticScreenshot = Save-CdpScreenshot -Socket $socket -LiteralPath $diagnosticScreenshotPath
    $viewport = $captureState.viewport
    $cssMatch = $null -ne $viewport -and [int]$viewport.css_width -eq $TargetWidth -and [int]$viewport.css_height -eq $TargetHeight
    $captureDimensions = Get-PerformanceCaptureDimensions -Status ([string]$captureState.capture_status)
    return [ordered]@{
        status = 'passed_public_observation'
        scene = [string]$captureState.scene
        quality_requested = 'high'
        quality_status = [string]$captureState.quality_status
        quality_ledger = $captureState.quality
        start_dom = $before
        high_dom = $highState
        export = [ordered]@{
            control = 'Exporter le diagnostic JSON'
            click_target = $exportTarget
            status = [string]$exportState.export_status
            advertised_filename = Get-PerformanceStatusFilename -Status ([string]$exportState.export_status)
            file_hash_verified = $false
            local_file_verification = 'not_verified_by_public_control'
        }
        capture = [ordered]@{
            control = 'Capturer le viewport PNG'
            click_target = $captureTarget
            status = [string]$captureState.capture_status
            advertised_filename = Get-PerformanceStatusFilename -Status ([string]$captureState.capture_status)
            gpu_dimensions = $captureDimensions
            file_hash_verified = $false
            local_file_verification = 'not_verified_by_public_control'
        }
        dimensions = [ordered]@{
            requested_css_width = $TargetWidth
            requested_css_height = $TargetHeight
            observed = $viewport
            css_dimensions_match_requested = $cssMatch
            render_buffer_dimensions_match_css = $null -ne $viewport -and [int]$viewport.render_width -eq [int]$viewport.css_width -and [int]$viewport.render_height -eq [int]$viewport.css_height
        }
        telemetry = $captureState.telemetry
        scene_telemetry = $captureState.scene_telemetry
        diagnostic_screenshot = $diagnosticScreenshot
        limitations = @(
            'Public export/capture statuses confirm a download request only; the local download file is not verified here.',
            'Page.captureScreenshot is a CDP diagnostic checkpoint and is not the product PNG capture.',
            'Web frame samples measure the producer/update loop; no GPU timestamp or compositor swap receipt is claimed.'
        )
    }
}

function Format-PerformanceArgument {
    param([Parameter(Mandatory)][string]$Value)
    if ($Value -match '[\s"]') { return '"' + $Value.Replace('"', '\"') + '"' }
    return $Value
}

function Get-PerformanceNativePlan {
    param(
        [Parameter(Mandatory)][string]$RuntimePath,
        [Parameter(Mandatory)][string]$ScenePath,
        [Parameter(Mandatory)][string]$MetricsPath,
        [Parameter(Mandatory)][string]$QualityName,
        [Parameter(Mandatory)][int]$WarmupFrames,
        [Parameter(Mandatory)][int]$ExitAfterFrames
    )
    $arguments = @('--scene', $ScenePath, '--quality', $QualityName, '--metrics', $MetricsPath, '--metrics-warmup-frames', [string]$WarmupFrames, '--exit-after-frames', [string]$ExitAfterFrames)
    return [ordered]@{
        executable = $RuntimePath
        arguments = $arguments
        command_display = ((@('"' + $RuntimePath + '"') + @($arguments | ForEach-Object { Format-PerformanceArgument -Value ([string]$_) })) -join ' ')
        warmup_frames = $WarmupFrames
        exit_after_frames = $ExitAfterFrames
        quality = $QualityName
    }
}

function Invoke-PerformanceNativeRun {
    param(
        [Parameter(Mandatory)][string]$RuntimePath,
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$MetricsPath,
        [Parameter(Mandatory)][string]$EvidencePath,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [Parameter(Mandatory)][string]$ExpectedSchema
    )
    $stdout = Join-Path $EvidencePath "$Name.stdout.log"
    $stderr = Join-Path $EvidencePath "$Name.stderr.log"
    $process = $null
    $forced = $false
    try {
        $process = Start-Process -FilePath $RuntimePath -ArgumentList $Arguments -WorkingDirectory (Split-Path -Parent $RuntimePath) -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
        while (-not $process.HasExited -and [DateTimeOffset]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 250; $process.Refresh() }
        $process.Refresh()
        if (-not $process.HasExited) {
            $forced = $true
            try { $process.Kill() } catch {}
            try { [void]$process.WaitForExit(10000) } catch {}
            throw "Owned native performance process exceeded ${TimeoutSeconds}s and was stopped; metrics are incomplete."
        }
        if ([int]$process.ExitCode -ne 0) { throw "Native performance process exited with code $($process.ExitCode). See $stderr." }
        if (-not (Test-Path -LiteralPath $MetricsPath -PathType Leaf)) { throw "Native performance process exited without $MetricsPath." }
        $document = ConvertFrom-ExactJson -Json ([IO.File]::ReadAllText($MetricsPath))
        if ([string](Get-PerformanceProperty -Object $document -Name 'schema') -cne $ExpectedSchema) { throw "Native report schema mismatch: $([string](Get-PerformanceProperty -Object $document -Name 'schema'))." }
        return [ordered]@{
            status = 'passed_measurement_input'
            owned_pid = [int]$process.Id
            exit_code = [int]$process.ExitCode
            forced_stop = $forced
            metrics_path = [IO.Path]::GetFullPath($MetricsPath)
            metrics_sha256 = Get-FileSha256 -LiteralPath $MetricsPath
            stdout_path = [IO.Path]::GetFullPath($stdout)
            stderr_path = [IO.Path]::GetFullPath($stderr)
            report = $document
        }
    }
    catch {
        if ($null -ne $process) {
            try { $process.Refresh() } catch {}
            if (-not $process.HasExited) { $forced = $true; try { $process.Kill() } catch {} }
        }
        throw
    }
}

function Invoke-PerformanceNativeCollection {
    param(
        [Parameter(Mandatory)][string]$RuntimePath,
        [Parameter(Mandatory)][string]$ScenePath,
        [Parameter(Mandatory)][string]$SceneHash,
        [Parameter(Mandatory)][string]$EvidencePath,
        [Parameter(Mandatory)][string]$SourcePin,
        [Parameter(Mandatory)][string]$QualityName,
        [Parameter(Mandatory)][int]$TimeoutSeconds,
        [switch]$IncludeWalk
    )
    $metricsPath = Join-Path $EvidencePath 'native-release-high-metrics.json'
    $plan = Get-PerformanceNativePlan -RuntimePath $RuntimePath -ScenePath $ScenePath -MetricsPath $metricsPath -QualityName $QualityName -WarmupFrames 60 -ExitAfterFrames 600
    $measurement = Invoke-PerformanceNativeRun -RuntimePath $RuntimePath -Arguments ([string[]]$plan.arguments) -MetricsPath $metricsPath -EvidencePath $EvidencePath -Name 'native-release-high-metrics' -TimeoutSeconds $TimeoutSeconds -ExpectedSchema 'forge.runtime-metrics'
    $reportSource = Get-PerformanceProperty -Object $measurement.report -Name 'source_hash'
    if ($null -eq $reportSource -or (Get-PerformanceNormalizedHash -Value ([string]$reportSource)) -cne $SourcePin) { throw 'Native runtime metrics source_hash differs from the pinned Tidewater source.' }
    $result = [ordered]@{
        status = $measurement.status
        source_hash = $SourcePin
        scene_path = [IO.Path]::GetFullPath($ScenePath)
        scene_sha256 = $SceneHash
        plan = $plan
        report = $measurement
        presentation_claim = 'runtime metrics report requested a native window; actual compositor swap/GPU presentation latency is not claimed.'
        walk = [ordered]@{ status = 'not_requested'; reason = 'Additional moving measurement is opt-in and was not automatically multiplied into the baseline run.' }
    }
    if ($IncludeWalk) {
        $walkPath = Join-Path $EvidencePath 'native-walk-moving-measurement.json'
        $walkArguments = @('--scene', $ScenePath, '--quality', $QualityName, '--player-camera', '--walk-proof', '1400:-0.5:1', '--walk-proof-measure-moving', '--exit-after-frames', '2600')
        $walk = Invoke-PerformanceNativeRun -RuntimePath $RuntimePath -Arguments $walkArguments -MetricsPath $walkPath -EvidencePath $EvidencePath -Name 'native-walk-moving-measurement' -TimeoutSeconds ([Math]::Max($TimeoutSeconds, 180)) -ExpectedSchema 'forge.walk-moving-measurement'
        $result.walk = [ordered]@{
            status = $walk.status
            scene_path = [IO.Path]::GetFullPath($ScenePath)
            scene_sha256 = $SceneHash
            source_hash = $SourcePin
            report = $walk
            limitation = 'Moving replay is a separate measurement input; it is not a gameplay or Gate pass.'
        }
    }
    return $result
}

if ($SelfTest) {
    foreach ($name in @(
        'Assert-Gate3ProofPath', 'ConvertFrom-ExactJson', 'Get-NormalizedSha256', 'Get-FileSha256',
        'Get-ArchiveSnapshot', 'Get-WorkspaceZoneReference', 'Copy-IsolatedFixture',
        'Wait-CdpReady', 'Invoke-CdpExpression', 'Invoke-UiClick', 'Open-DetailsBySummary',
        'Start-ForgeStudioPlaytestProcess', 'Stop-ForgeStudioPlaytestProcess', 'Save-CdpScreenshot'
    )) { [void](Get-Command $name -ErrorAction Stop) }
    foreach ($name in @('Get-PerformanceProperty', 'Get-PerformanceFixtureEvidence', 'Assert-PerformanceAuthoringLedger', 'Get-PerformanceHardwareSnapshot', 'Get-PerformanceDomSnapshot', 'Wait-PerformanceStudioReady', 'Invoke-PerformanceStudioCollection', 'Get-PerformanceNativePlan', 'Invoke-PerformanceNativeRun', 'Invoke-PerformanceNativeCollection')) { [void](Get-Command $name -ErrorAction Stop) }
    $raw = [IO.File]::ReadAllText($PSCommandPath)
    foreach ($needle in @(
        'Aide & diagnostics', 'Scènes de preuve', 'Vallée WorldSketch', 'High', 'Exporter le diagnostic JSON', 'Capturer le viewport PNG',
        'gate0-telemetry-ledger', 'forge-scene-telemetry-ledger', 'studio-quality-ledger',
        'css_width', 'render_width', 'device_pixel_ratio', 'forge.runtime-metrics', 'forge.walk-moving-measurement',
        'candidate_kit_and_imports', 'source_original_hash_pinned', 'NativeSceneSha256', 'NativeBuildProof', 'RunNativeMetrics',
        'IncludeNativeWalkMeasurement', '1920', '1080', 'gate_qualified = $false',
        'compositor swap/GPU presentation latency is not claimed', 'Additional moving measurement is opt-in'
    )) { if ($raw.IndexOf($needle, [StringComparison]::Ordinal) -lt 0) { throw "Performance evidence contract missing: $needle" } }
    foreach ($forbidden in @(('window.' + '__TAURI__'), ('element.' + 'click()'), ('window.' + '__forgeGate2WorldAudit'), ('Game' + 'Save' + 'Request'), ('Studio' + 'Command::' + 'GameSave'))) {
        if ($raw.IndexOf($forbidden, [StringComparison]::Ordinal) -ge 0) { throw "Performance harvester contains forbidden injection marker: $forbidden" }
    }
    $filename = Get-PerformanceStatusFilename -Status 'Téléchargement JSON demandé · forge-diagnostic-123.json · fichier local non vérifié'
    if ($filename -cne 'forge-diagnostic-123.json') { throw "Filename parser self-test failed: $filename" }
    $dimensions = Get-PerformanceCaptureDimensions -Status 'Capture GPU reçue 1920 × 1080 · téléchargement forge-viewport-123.png demandé'
    if ($null -eq $dimensions -or $dimensions.width -ne 1920 -or $dimensions.height -ne 1080) { throw 'Capture dimensions parser self-test failed.' }
    $plan = Get-PerformanceNativePlan -RuntimePath 'E:\forge_runtime.exe' -ScenePath 'E:\scene.json' -MetricsPath 'E:\metrics.json' -QualityName 'high' -WarmupFrames 60 -ExitAfterFrames 600
    if ($plan.arguments.Count -ne 10 -or $plan.arguments[0] -cne '--scene' -or $plan.arguments[4] -cne '--metrics') { throw 'Native plan self-test failed.' }
    Write-Output 'TIDEWATER_GATE3_PERFORMANCE_EVIDENCE_SELF_TEST_PASS'
    return
}

if (-not (Test-IsWindowsPlatform)) { throw 'Windows/WebView2 is required.' }
if ($IncludeNativeWalkMeasurement -and -not $RunNativeMetrics) { throw 'IncludeNativeWalkMeasurement requires RunNativeMetrics.' }
$candidatePath = [IO.Path]::GetFullPath($Candidate)
$sourceRoot = [IO.Path]::GetFullPath($FreshIsolatedFixture)
$ledgerPath = [IO.Path]::GetFullPath($AuthoringLedger)
$outputRoot = [IO.Path]::GetFullPath($EvidenceRoot)
$sourcePin = Get-PerformanceNormalizedHash -Value $SourceOriginalHashPinned
$zoneId = ([string]$ZoneUuid).ToLowerInvariant()
foreach ($entry in @(@($candidatePath, 'Performance candidate'), @($sourceRoot, 'Fresh authored Tidewater fixture'), @($ledgerPath, 'Performance authoring ledger'), @($outputRoot, 'Performance evidence root'))) {
    Assert-Gate3ProofPath -LiteralPath $entry[0] -Description $entry[1]
}
if (Test-Path -LiteralPath $outputRoot) { throw "EvidenceRoot must be fresh: $outputRoot" }
if (-not (Test-Path -LiteralPath $candidatePath -PathType Leaf)) { throw "Candidate is absent: $candidatePath" }
if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) { throw "Authored fixture is absent: $sourceRoot" }
if ((Test-PathWithinDirectory -LiteralPath $outputRoot -Directory $sourceRoot) -or (Test-PathWithinDirectory -LiteralPath $sourceRoot -Directory $outputRoot)) { throw 'EvidenceRoot and authored fixture must remain separate.' }
if ((Test-PathWithinDirectory -LiteralPath $outputRoot -Directory (Split-Path -Parent $candidatePath)) -or (Test-PathWithinDirectory -LiteralPath (Split-Path -Parent $candidatePath) -Directory $outputRoot)) { throw 'EvidenceRoot and candidate directory must remain separate.' }
$candidateHash = Get-FileSha256 -LiteralPath $candidatePath
$authoringInfo = Assert-PerformanceAuthoringLedger -LedgerPath $ledgerPath -CandidatePath $candidatePath -FixtureRoot $sourceRoot -SourcePin $sourcePin -ExpectedImports (Get-PerformanceNormalizedHash -Value $ExpectedImportsManifestSha256) -ExpectedTerrain (Get-PerformanceNormalizedHash -Value $ExpectedTerrainSha256) -ExpectedEnvironment (Get-PerformanceNormalizedHash -Value $ExpectedEnvironmentSha256) -ExpectedPlants (Get-PerformanceNormalizedHash -Value $ExpectedPlantsSha256) -ExpectedOcean (Get-PerformanceNormalizedHash -Value $ExpectedOceanSha256)
$fixtureInfo = Get-PerformanceFixtureEvidence -SourceRoot $sourceRoot -SourcePin $sourcePin -ZoneId $zoneId -Authoring $authoringInfo.authoring
$hardware = Get-PerformanceHardwareSnapshot -BrowserFolder $BrowserExecutableFolder
if (-not [string]::IsNullOrWhiteSpace($ImportsManifestPath)) {
    $importsPath = [IO.Path]::GetFullPath($ImportsManifestPath)
    Assert-Gate3ProofPath -LiteralPath $importsPath -Description 'Imported source manifest'
    if (-not (Test-Path -LiteralPath $importsPath -PathType Leaf)) { throw "Imports manifest is absent: $importsPath" }
    $importsHash = Get-FileSha256 -LiteralPath $importsPath
    if ($importsHash -cne (Get-PerformanceNormalizedHash -Value $ExpectedImportsManifestSha256)) { throw 'Imports manifest bytes do not match the pinned imports_manifest_sha256.' }
}
$nativePath = $null
$nativeScenePath = $null
$nativeSceneHash = $null
$nativeBuildProofPath = $null
$nativeBuildProofHash = $null
if ($RunNativeMetrics) {
    if ([string]::IsNullOrWhiteSpace($NativeRuntime) -or [string]::IsNullOrWhiteSpace($NativeScene) -or [string]::IsNullOrWhiteSpace($NativeSceneSha256) -or [string]::IsNullOrWhiteSpace($NativeBuildProof)) { throw 'RunNativeMetrics requires NativeRuntime, NativeScene, NativeSceneSha256 and NativeBuildProof.' }
    $nativePath = [IO.Path]::GetFullPath($NativeRuntime)
    $nativeScenePath = [IO.Path]::GetFullPath($NativeScene)
    $nativeSceneHash = Get-PerformanceNormalizedHash -Value $NativeSceneSha256
    $nativeBuildProofPath = [IO.Path]::GetFullPath($NativeBuildProof)
    Assert-Gate3ProofPath -LiteralPath $nativePath -Description 'Release native runtime'
    Assert-Gate3ProofPath -LiteralPath $nativeScenePath -Description 'Exact pinned Tidewater scene'
    Assert-Gate3ProofPath -LiteralPath $nativeBuildProofPath -Description 'Native Release build proof'
    if (-not (Test-Path -LiteralPath $nativePath -PathType Leaf)) { throw "Native runtime is absent: $nativePath" }
    if (-not (Test-Path -LiteralPath $nativeScenePath -PathType Leaf)) { throw "Native scene is absent: $nativeScenePath" }
    if (-not (Test-Path -LiteralPath $nativeBuildProofPath -PathType Leaf)) { throw "Native build proof is absent: $nativeBuildProofPath" }
    $nativeBuildProofRaw = [IO.File]::ReadAllText($nativeBuildProofPath)
    $nativeBuildProofDocument = ConvertFrom-ExactJson -Json $nativeBuildProofRaw
    $nativeBuildStatus = [string](Get-PerformanceProperty -Object $nativeBuildProofDocument -Name 'status')
    if ($nativeBuildStatus -notin @('BUILD_ONLY', 'BUILD_PASS', 'passed')) { throw "Native build proof is not a build-only/pass proof: $nativeBuildStatus" }
    if ($nativeBuildProofRaw -notmatch '(?i)\brelease\b') { throw 'Native build proof does not contain an explicit Release profile marker.' }
    $nativeBuildProofHash = Get-FileSha256 -LiteralPath $nativeBuildProofPath
    $observedSceneHash = Get-FileSha256 -LiteralPath $nativeScenePath
    if ($observedSceneHash -cne $nativeSceneHash) { throw 'Native scene bytes differ from NativeSceneSha256.' }
}

[void](New-Item -ItemType Directory -Path $outputRoot)
$studioEvidence = Join-Path $outputRoot 'studio'
$nativeEvidence = Join-Path $outputRoot 'native'
[void](New-Item -ItemType Directory -Path $studioEvidence)
[void](New-Item -ItemType Directory -Path $nativeEvidence)
[IO.File]::WriteAllText((Join-Path $outputRoot 'hardware-preflight.json'), ($hardware | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
$sourceSnapshots = $fixtureInfo
$savedEnvironment = @{}
foreach ($name in @('FORGE_AUTHORING_ROOT', 'FORGE_AUTHOR_SUBJECT', 'WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS', 'WEBVIEW2_USER_DATA_FOLDER', 'WEBVIEW2_BROWSER_EXECUTABLE_FOLDER')) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$run = $null
$cleanup = [ordered]@{ owned_process_closed = $true; forced_process_stop = $false; graceful_close_failure = $null; cdp_listener_closed = $null }
$studio = $null
$native = [ordered]@{ status = if ($RunNativeMetrics) { 'pending' } else { 'not_requested' }; source_hash = $sourcePin; scene_path = $nativeScenePath; scene_sha256 = $nativeSceneHash; build_proof_path = $nativeBuildProofPath; build_proof_sha256 = $nativeBuildProofHash }
$failure = $null
try {
    $fixture = Join-Path $outputRoot 'performance-fixture'
    [void](Copy-IsolatedFixture -SourceRoot $sourceRoot -DestinationRoot $fixture -ZoneId $zoneId)
    [Environment]::SetEnvironmentVariable('WEBVIEW2_BROWSER_EXECUTABLE_FOLDER', [IO.Path]::GetFullPath($BrowserExecutableFolder), 'Process')
    $run = Start-ForgeStudioPlaytestProcess -CandidatePath $candidatePath -CandidateHash $candidateHash -FixtureRoot $fixture -EvidencePath $studioEvidence -RunNumber 1 -StartupTimeout $StartupTimeoutSeconds
    $studio = Invoke-PerformanceStudioCollection -Run $run -EvidencePath $studioEvidence -TimeoutSeconds $InteractionTimeoutSeconds -TargetWidth $ViewportWidth -TargetHeight $ViewportHeight
    if ($RunNativeMetrics) {
        $native = Invoke-PerformanceNativeCollection -RuntimePath $nativePath -ScenePath $nativeScenePath -SceneHash $nativeSceneHash -EvidencePath $nativeEvidence -SourcePin $sourcePin -QualityName $Quality -TimeoutSeconds ([Math]::Max($InteractionTimeoutSeconds, 180)) -IncludeWalk:$IncludeNativeWalkMeasurement
    }
}
catch {
    $failure = $_.Exception.Message
    if ($null -ne $run -and $null -ne $run.Socket) {
        try {
            $failureDom = Get-PerformanceDomSnapshot -Socket $run.Socket
            $failureScreenshot = Save-CdpScreenshot -Socket $run.Socket -LiteralPath (Join-Path $studioEvidence 'studio-public-failure.png')
            $studio = [ordered]@{
                status = 'failed_public_collection'
                failure = $failure
                failure_dom = $failureDom
                diagnostic_screenshot = $failureScreenshot
            }
            [IO.File]::WriteAllText((Join-Path $studioEvidence 'studio-public-failure.json'), ($studio | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
        }
        catch {
            $studio = [ordered]@{ status = 'failed_public_collection'; failure = $failure; diagnostic_failure = $_.Exception.Message }
        }
    }
}
finally {
    if ($null -ne $run) {
        try {
            $close = Stop-ForgeStudioPlaytestProcess -Run $run
            $cleanup.owned_process_closed = [bool]$close.Closed
            $cleanup.forced_process_stop = [bool]$close.Forced
            $cleanup.graceful_close_failure = $close.GracefulCloseFailure
            $cleanup.cdp_listener_closed = Wait-CdpListenerClosed -Port ([int]$run.Port) -TimeoutSeconds 10
            if ($close.Forced -and $null -eq $failure) { $failure = 'Studio process required a forced stop; performance evidence is failed closed.' }
            if (-not $close.Closed -and $null -eq $failure) { $failure = 'Owned Studio process did not close.' }
            if (-not $cleanup.cdp_listener_closed -and $null -eq $failure) { $failure = 'Owned Studio CDP listener remained reachable after close.' }
        }
        catch { if ($null -eq $failure) { $failure = "Studio cleanup failed: $($_.Exception.Message)" } }
    }
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
}

$sourceAfter = [ordered]@{}
try {
    $sourceAfter.zone_archive = Get-ArchiveSnapshot -LiteralPath ([string]$sourceSnapshots.zone_archive.Path)
    $sourceAfter.workspace_archive = Get-ArchiveSnapshot -LiteralPath ([string]$sourceSnapshots.workspace_archive.Path)
    $sourceAfter.source_original = Get-ArchiveSnapshot -LiteralPath ([string]$sourceSnapshots.source_original.Path)
    $sourceAfter.authored_checkpoint = Get-ArchiveSnapshot -LiteralPath ([string]$sourceSnapshots.authored_checkpoint.Path)
    foreach ($name in @('zone_archive', 'workspace_archive', 'source_original', 'authored_checkpoint')) {
        if (-not (Test-ArchiveSnapshotsEqual -Before $sourceSnapshots.$name -After $sourceAfter.$name)) { $failure = if ($failure) { "$failure; source snapshot changed: $name" } else { "Source snapshot changed: $name" } }
    }
}
catch { $failure = if ($failure) { "$failure; source postflight failed: $($_.Exception.Message)" } else { "Source postflight failed: $($_.Exception.Message)" } }
$status = if ($null -eq $failure -and $null -ne $studio -and (!$RunNativeMetrics -or [string]$native.status -eq 'passed_measurement_input')) { 'passed_local_evidence' } else { 'failed_closed' }
$report = [ordered]@{
    schema = 'forge.tidewater.gate3-performance-evidence'
    version = 1
    recorded_at_utc = [DateTimeOffset]::UtcNow.ToString('o')
    status = $status
    failure = $failure
    gate_qualified = $false
    evidence_kind = 'public_studio_telemetry_plus_optional_native_release_metrics'
    campaign = [ordered]@{
        first_run = 'one 1920x1080 CSS target, High, one Studio public collection'
        requested_viewport = [ordered]@{ width = $ViewportWidth; height = $ViewportHeight }
        quality = $Quality
        target_budgets_recorded_only = [ordered]@{
            mean_fps_min = 60
            p95_frame_ms_max = 16.7
            one_percent_low_fps_min = 45
            threshold_source = 'PROJECT_CONTEXT.md Gate 3 performance target; measurement does not imply acceptance'
        }
        automatic_matrix_expansion = $false
        expansion_policy = 'Add another resolution/profile only after a recorded dimension mismatch, source discrepancy or unexplained result.'
        native_walk_measurement = if ($IncludeNativeWalkMeasurement) { 'explicitly_requested' } else { 'not_automatically_requested' }
    }
    source = [ordered]@{
        candidate = $candidatePath
        candidate_sha256 = $candidateHash
        authoring_ledger = $ledgerPath
        authoring_ledger_sha256 = $authoringInfo.ledger_sha256
        source_original_hash_pinned = $sourcePin
        authored_fixture = $sourceRoot
        authored_checkpoint_archive_hash = $fixtureInfo.authored_archive_hash
        native_build_proof_path = $nativeBuildProofPath
        native_build_proof_sha256 = $nativeBuildProofHash
        imports_manifest_path = if (-not [string]::IsNullOrWhiteSpace($ImportsManifestPath)) { [IO.Path]::GetFullPath($ImportsManifestPath) } else { $null }
        imports_manifest_sha256 = Get-PerformanceNormalizedHash -Value $ExpectedImportsManifestSha256
        terrain_sha256 = Get-PerformanceNormalizedHash -Value $ExpectedTerrainSha256
        environment_sha256 = Get-PerformanceNormalizedHash -Value $ExpectedEnvironmentSha256
        plants_sha256 = Get-PerformanceNormalizedHash -Value $ExpectedPlantsSha256
        ocean_sha256 = Get-PerformanceNormalizedHash -Value $ExpectedOceanSha256
    }
    hardware_preflight = $hardware
    studio = $studio
    native = $native
    source_after = $sourceAfter
    cleanup = $cleanup
    limitations = @(
        'This is local evidence preparation and does not qualify Gate 3 or provide human visual approval.',
        'Studio frame samples are producer/update-loop observations; GPU timestamp, compositor swap and end-to-end presentation latency remain unobserved.',
        'Native forge.runtime-metrics reports the requested window and asynchronous render diagnostics; gpu_presentation is not inferred from those fields.',
        'The public JSON/PNG controls report download requests; the local browser download bytes are not verified by this harness.',
        'Native and Studio reports are separate inputs and must not be merged into an RPG gameplay or navigation verdict.',
        'The exact native scene hash and source/import pins are required before a native run is accepted.'
    )
}
try { [IO.File]::WriteAllText((Join-Path $outputRoot 'gate3-performance-evidence-ledger.json'), ($report | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false)) } catch { if ($null -eq $failure) { $failure = "Performance ledger write failed: $($_.Exception.Message)" } }
if ($null -ne $failure) { throw $failure }
Write-Output ("TIDEWATER_GATE3_PERFORMANCE_EVIDENCE_LOCAL_PASS " + (Join-Path $outputRoot 'gate3-performance-evidence-ledger.json'))
