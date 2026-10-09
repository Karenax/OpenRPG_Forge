$ErrorActionPreference = 'Stop'
$taskRoot = 'F:\OpenRPG_ImportWork\unity-nested-material-texture-storage-v2'
$pythonExe = 'C:\Users\karen\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$state = @{status='waiting_for_prior_unity_controllers'; native_capture_launched=$false; verified_textures=0; completed_packs=@(); full_import=$false; started_utc=[DateTime]::UtcNow.ToString('o')}
function Save-State { $state | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $taskRoot 'RUN-RESULT.v1.json') -Encoding utf8 }
function Require-Hash($path,$expected) { if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw "Pinned texture input changed: $path" } }
function Check-Pack($prepared,$packRoot,$before) {
    foreach ($pin in $queue.original_material_pins.PSObject.Properties) { Require-Hash $pin.Name $pin.Value }
    Require-Hash (Join-Path $packRoot 'manifest.json') $prepared.manifest_sha256
    Require-Hash (Join-Path $packRoot 'project\Assets\Editor\TextureSourceSnapshot.cs') $prepared.exporter_sha256
    Require-Hash (Join-Path $packRoot 'verify_texture_export.py') $prepared.verifier_sha256
    Require-Hash $prepared.program_manifest $prepared.program_manifest_sha256
    foreach ($pin in $prepared.material_pins.PSObject.Properties) { Require-Hash $pin.Name $pin.Value }
    foreach ($copy in $prepared.copies) {
        Require-Hash $copy.original $copy.original_sha256
        Require-Hash $copy.original_meta $copy.original_meta_sha256
        Require-Hash $copy.project_copy $copy.project_copy_sha256
        if ($before) { Require-Hash $copy.project_meta_copy $copy.project_meta_copy_sha256 }
    }
}
try {
    Save-State
    $queue = Get-Content -LiteralPath (Join-Path $taskRoot 'QUEUED.v1.json') -Raw | ConvertFrom-Json
    Require-Hash $PSCommandPath $queue.controller_sha256
    Require-Hash (Join-Path $taskRoot 'PREPARED.v1.json') $queue.prepared_sha256
    foreach ($prior in $queue.wait_for_exact_controllers) {
        while ($true) {
            $process = Get-Process -Id $prior.pid -ErrorAction SilentlyContinue
            if (!$process -or $process.StartTime.ToUniversalTime().Ticks -ne $prior.start_utc_ticks) { break }
            $process.WaitForExit(45000) | Out-Null
        }
    }
    $batch = Get-Content -LiteralPath (Join-Path $taskRoot 'PREPARED.v1.json') -Raw | ConvertFrom-Json
    foreach ($pin in $batch.source_pins.PSObject.Properties) { Require-Hash $pin.Name $pin.Value }
    foreach ($pack in $batch.packs) {
        while (@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count -gt 0) { Start-Sleep -Seconds 5 }
        $packRoot = $pack.root
        $prepared = Get-Content -LiteralPath (Join-Path $packRoot 'PREPARED.v1.json') -Raw | ConvertFrom-Json
        Check-Pack $prepared $packRoot $true
        if (Test-Path -LiteralPath (Join-Path $packRoot 'export')) { throw 'Existing native export preserved' }
        Require-Hash (Join-Path $prepared.project 'ProjectSettings\ProjectSettings.asset') $prepared.initial_player_settings_sha256
        Require-Hash (Join-Path $prepared.project 'ProjectSettings\ProjectVersion.txt') $prepared.initial_project_version_sha256
        & $pythonExe (Join-Path $packRoot 'verify_texture_export.py') $packRoot --self-test *> (Join-Path $packRoot 'controls.log')
        if ($LASTEXITCODE -ne 0) { throw 'Actual texture verifier controls failed' }
        $env:TEMP = Join-Path $packRoot 'temp'; $env:TMP=$env:TEMP
        $env:UPM_CACHE_ROOT='F:\OpenRPG_ImportWork\unity-package-cache'
        $arguments=@('-batchmode','-noUpm','-force-d3d11','-projectPath',$prepared.project,'-executeMethod','OpenRPGImportProbe.TextureSourceSnapshot.Export','--openrpg-manifest',(Join-Path $packRoot 'manifest.json'),'-logFile',(Join-Path $packRoot 'unity.log'),'-quit')
        $unity = Start-Process -FilePath 'F:\Unity\6000.5.8f1\Editor\Unity.exe' -ArgumentList $arguments -PassThru -WindowStyle Hidden
        $state.status='native_imported_texture_capture'; $state.native_capture_launched=$true
        $state.current_pack=$pack.pack_id; $state.unity_pid=$unity.Id; $state.unity_start_utc_ticks=$unity.StartTime.ToUniversalTime().Ticks
        Save-State
        $unity.WaitForExit(); $state.unity_exit_code=$unity.ExitCode
        if ($unity.ExitCode -ne 0) { throw 'Unity texture capture failed; preserved log available' }
        Check-Pack $prepared $packRoot $false
        & $pythonExe (Join-Path $packRoot 'verify_texture_export.py') $packRoot *> (Join-Path $packRoot 'reopen.log')
        if ($LASTEXITCODE -ne 0) { throw 'Independent texture storage reopening failed' }
        $receiptPath=Join-Path $packRoot 'TEXTURE-REOPEN.v1.json'
        $receipt=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        if ($receipt.source_texture_storage_captures_verified -ne $pack.requested_textures -or !$receipt.original_sources_and_materials_unchanged) { throw 'Native texture capture count/source guard differs' }
        Check-Pack $prepared $packRoot $false
        $state.verified_textures += $pack.requested_textures
        $state.completed_packs += @{pack_id=$pack.pack_id; textures=$pack.requested_textures; receipt=$receiptPath; receipt_sha256=(Get-FileHash -LiteralPath $receiptPath -Algorithm SHA256).Hash.ToLowerInvariant(); unity_exit_code=0; verifier_exit_code=0}
        Save-State
    }
    if ($state.verified_textures -ne $batch.requested_textures) { throw 'Incomplete native texture batch' }
    $state.status='source_imported_texture_storage_independently_verified'
    $state.original_sources_unchanged=$true
    $state.original_project_color_space_qualified=$false
    $state.source_normal_decode_qualified=$false
    $state.gpu_rendered=$false
} catch { $state.status='failed_or_unverified'; $state.error=$_.Exception.Message }
finally { $state.finished_utc=[DateTime]::UtcNow.ToString('o'); Save-State }
