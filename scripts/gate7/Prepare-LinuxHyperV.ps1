<# Fresh Ubuntu qualification guest, separate from the Docker SDK and old QEMU disk.
Only the pinned publisher image is converted. No host directory is shared.
The new dynamic disk is capped at 64 GiB; initial storage is about 2 GiB / ten files.
https://learn.microsoft.com/windows-server/virtualization/hyper-v/plan/should-i-create-a-generation-1-or-2-virtual-machine-in-hyper-v
#>
[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]{1,48}$')][string]$RunId,
    [switch]$ResumeSparseFailure)
$ErrorActionPreference='Stop'
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator Hyper-V access required' }
Import-Module Hyper-V
$gateName='Forge-Gate7-Linux-Clean'
$gateRoot='E:\OpenRPG_Forge\.build\vm-qualification'
$gateVmRoot=Join-Path $gateRoot 'linux-clean-hyperv'
$gateImage=Join-Path $gateRoot 'inputs\noble-server-cloudimg-amd64.img'
$gateSeed=Join-Path $gateRoot 'inputs\linux-clean-hyperv-seed-001.iso'
$gateConverter='E:\OpenRPG_Forge\.build\tools\qemu-11.1.0\qemu-img.exe'
if (Get-VM -Name $gateName -ErrorAction SilentlyContinue) { throw 'Existing qualification VM preserved' }
if (Test-Path -LiteralPath $gateVmRoot) {
    if (-not $ResumeSparseFailure) { throw 'Existing qualification disk preserved' }
    $gateFailed=Get-Content -LiteralPath (Join-Path $gateVmRoot 'preparation-001.receipt.json') -Raw | ConvertFrom-Json
    if ($gateFailed.succeeded -or $gateFailed.error -notmatch '0xC03A001A' -or
        $gateFailed.source_image_sha256 -ne '04e8f2461a16a44e78a370284471f29616cdd9baf1893df25807b71f985b6c94' -or
        -not ((Get-Item -LiteralPath (Join-Path $gateVmRoot 'system.vhdx')).Attributes -band [IO.FileAttributes]::SparseFile)) {
        throw 'Only the recorded, unstarted sparse conversion failure can be resumed'
    }
} elseif ($ResumeSparseFailure) { throw 'Recorded sparse conversion failure required' }
foreach ($gatePath in @($gateRoot,$gateImage,$gateSeed,$gateConverter)+$(if (Test-Path -LiteralPath $gateVmRoot) { @($gateVmRoot) } else { @() })) {
    $gateCursor=Get-Item -LiteralPath $gatePath -Force
    while ($gateCursor) {
        if ($gateCursor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Physical development paths required' }
        $gateParent=Split-Path -Parent $gateCursor.FullName
        if (-not $gateParent -or $gateParent -eq $gateCursor.FullName) { break }
        $gateCursor=Get-Item -LiteralPath $gateParent -Force
    }
}
if ((Get-FileHash -LiteralPath $gateImage -Algorithm SHA256).Hash.ToLowerInvariant() -ne '04e8f2461a16a44e78a370284471f29616cdd9baf1893df25807b71f985b6c94') { throw 'Ubuntu publisher image differs' }
if ((Get-FileHash -LiteralPath $gateSeed -Algorithm SHA256).Hash.ToLowerInvariant() -ne 'd61024a4361d5bd03a81b31db73ecb03ca75443ef7a8b53415bae4c0fc27534e') { throw 'Qualification seed differs' }
if ((Get-PSDrive E).Free -lt 20GB) { throw '20 GiB free on E: required' }
$gateSwitch=Get-VMSwitch -Name 'Default Switch'
if ($gateSwitch.SwitchType -ne 'Internal') { throw 'Existing internal NAT switch required' }
if (-not (Test-Path -LiteralPath $gateVmRoot)) { New-Item -ItemType Directory -Path $gateVmRoot | Out-Null }
$gateReceipt=Join-Path $gateVmRoot ($RunId+'.receipt.json')
if (Test-Path -LiteralPath $gateReceipt) { throw 'Existing preparation receipt preserved' }
Start-Transcript -LiteralPath (Join-Path $gateVmRoot ($RunId+'.log')) | Out-Null
$gateResult=@{schema='forge.gate7.clean-linux-hyperv-preparation.v1'; started_utc=[DateTime]::UtcNow.ToString('o');
    succeeded=$false; host_filesystem_shared=$false; development_sdk_requested=$false;
    source_image_sha256='04e8f2461a16a44e78a370284471f29616cdd9baf1893df25807b71f985b6c94';
    seed_sha256='d61024a4361d5bd03a81b31db73ecb03ca75443ef7a8b53415bae4c0fc27534e';
    memory_mib=4096; vcpus=2; disk_max_gib=64; host_restart_requested=$false; os_qualified=$false}
try {
    $gateDisk=Join-Path $gateVmRoot $(if ($ResumeSparseFailure) { 'system-hyperv.vhdx' } else { 'system.vhdx' })
    if (Test-Path -LiteralPath $gateDisk) { throw 'Existing conversion output preserved' }
    $gateConverted=Join-Path $gateVmRoot 'system-nonsparse.vhdx'
    if (-not ($ResumeSparseFailure -and (Test-Path -LiteralPath $gateConverted))) {
        if (Test-Path -LiteralPath $gateConverted) { throw 'Existing intermediate conversion preserved' }
        & $gateConverter convert -S 0 -f qcow2 -O vhdx -o subformat=dynamic $gateImage $gateConverted
        if ($LASTEXITCODE -ne 0) { throw ('Publisher image conversion failed: '+$LASTEXITCODE) }
    }
    # QEMU on Windows still marks fully allocated output SparseFile. Materialize
    # the verified, small conversion into a new ordinary NTFS file, not a clone.
    & $gateConverter compare -f qcow2 -F vhdx $gateImage $gateConverted
    if ($LASTEXITCODE -ne 0) { throw 'Converted guest disk contents differ from publisher image' }
    $gateInput=[IO.File]::OpenRead($gateConverted)
    try {
        $gateOutput=[IO.File]::Open($gateDisk,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try { $gateInput.CopyTo($gateOutput,1MB); $gateOutput.Flush($true) } finally { $gateOutput.Dispose() }
    } finally { $gateInput.Dispose() }
    if ((Get-FileHash -LiteralPath $gateDisk).Hash -ne (Get-FileHash -LiteralPath $gateConverted).Hash) { throw 'Materialized disk bytes differ' }
    $gateAttributes=(Get-Item -LiteralPath $gateDisk).Attributes
    if ($gateAttributes -band ([IO.FileAttributes]::SparseFile -bor [IO.FileAttributes]::Compressed -bor [IO.FileAttributes]::Encrypted)) { throw 'Hyper-V requires a nonsparse, uncompressed and unencrypted disk' }
    & $gateConverter compare -f qcow2 -F vhdx $gateImage $gateDisk
    if ($LASTEXITCODE -ne 0) { throw 'Converted guest disk contents differ from publisher image' }
    $gateResult.converted_image_matches_publisher=$true
    $gateResult.resumed_sparse_failure=[bool]$ResumeSparseFailure
    Resize-VHD -Path $gateDisk -SizeBytes 64GB
    $gateVm=New-VM -Name $gateName -Generation 2 -MemoryStartupBytes 4GB -VHDPath $gateDisk -Path $gateVmRoot -SwitchName $gateSwitch.Name
    $gateResult.vm_id=[string]$gateVm.Id
    Set-VM -VM $gateVm -AutomaticCheckpointsEnabled $false -AutomaticStartAction Nothing -AutomaticStopAction ShutDown
    Set-VMProcessor -VM $gateVm -Count 2
    Set-VMMemory -VM $gateVm -DynamicMemoryEnabled $false
    Add-VMDvdDrive -VM $gateVm -Path $gateSeed
    $gateDrive=Get-VMHardDiskDrive -VM $gateVm
    Set-VMFirmware -VM $gateVm -EnableSecureBoot On -SecureBootTemplate MicrosoftUEFICertificateAuthority -FirstBootDevice $gateDrive
    $gateResult.secure_boot=[string](Get-VMFirmware -VM $gateVm).SecureBoot
    $gateResult.secure_boot_template='MicrosoftUEFICertificateAuthority'
    $gateAdapter=Get-VMNetworkAdapter -VM $gateVm
    $gateResult.mac_address=$gateAdapter.MacAddress
    $gateResult.vm_name=$gateName
    $gateResult.disk=$gateDisk
    $gateResult | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $gateVmRoot 'launch.json') -Encoding UTF8
    Start-VM -VM $gateVm
    $gateAddress=$null
    for ($gateAttempt=0; $gateAttempt -lt 60 -and -not $gateAddress; $gateAttempt++) {
        $gateAdapter=Get-VMNetworkAdapter -VM $gateVm
        $gateResult.mac_address=$gateAdapter.MacAddress
        $gateCandidates=@($gateAdapter.IPAddresses)
        if (-not $gateCandidates.Count -and $gateAdapter.MacAddress -match '^[0-9A-Fa-f]{12}$' -and $gateAdapter.MacAddress -ne '000000000000') {
            $gateCandidates=@(Get-NetNeighbor -AddressFamily IPv4 -ErrorAction SilentlyContinue |
                Where-Object { ($_.LinkLayerAddress -replace '-','') -eq $gateAdapter.MacAddress } |
                Select-Object -ExpandProperty IPAddress)
        }
        $gateAddress=$gateCandidates | Where-Object { $_ -match '^(10\.|192\.168\.|172\.(1[6-9]|2[0-9]|3[01])\.)' } | Select-Object -First 1
        if (-not $gateAddress) { Start-Sleep -Seconds 2 }
    }
    $gateResult.address=$gateAddress
    $gateResult.vm_state=[string](Get-VM -Id $gateVm.Id).State
    $gateResult.succeeded=($gateResult.vm_state -eq 'Running')
    $gateResult.disk_allocated_bytes=(Get-Item -LiteralPath $gateDisk).Length
} catch {
    $gateResult.error=$_.Exception.Message
    $gateResult.script_stack=$_.ScriptStackTrace
    throw
} finally {
    $gateResult.finished_utc=[DateTime]::UtcNow.ToString('o')
    $gateResult | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $gateReceipt -Encoding UTF8
    Stop-Transcript | Out-Null
}
