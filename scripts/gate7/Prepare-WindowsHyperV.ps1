<#
Prepare a Windows 11 qualification VM with supported virtual security devices.
Inspect is read-only. EnableHost requires explicit owner authorization and an
elevated Windows PowerShell; it NEVER restarts the host. Create requires Hyper-V
already operational, preserves existing VMs and never starts the new VM.
Microsoft references:
https://learn.microsoft.com/windows-server/virtualization/hyper-v/get-started/install-hyper-v
https://learn.microsoft.com/powershell/module/hyper-v/set-vmkeyprotector
https://learn.microsoft.com/powershell/module/hyper-v/enable-vmtpm
#>
[CmdletBinding()]
param(
    [ValidateSet('Inspect', 'EnableHost', 'Create')][string]$Mode = 'Inspect',
    [string]$SwitchName = 'Default Switch'
)
$ErrorActionPreference = 'Stop'
$gateRoot = 'E:\OpenRPG_Forge\.build\vm-qualification'
$gateName = 'Forge-Gate7-Windows'
$gateIsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if ($Mode -eq 'Inspect') {
    $gateState = @{
        schema = 'forge.gate7.hyperv-preflight.v1'
        utc = [DateTime]::UtcNow.ToString('o')
        elevated = $gateIsAdmin
        hyperv_commands_available = [bool](Get-Command New-VM -ErrorAction SilentlyContinue)
        vmms_status = [string](Get-Service vmms -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Status)
        host_features_changed = $false
        host_restart_requested = $false
        vm_created = $false
    }
    if ($gateIsAdmin) {
        $gateState.feature_state = [string](Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All).State
    } else {
        $gateState.feature_state = 'Unknown: administrator inspection required'
    }
    $gateState | ConvertTo-Json -Depth 4
    return
}
if (-not $gateIsAdmin) { throw 'Run this requested operation in an elevated Windows PowerShell. No changes made.' }
if ($Mode -eq 'EnableHost') {
    # A reboot is an owner action. Keep the live Studio and other work running.
    $gateReceiptPath = Join-Path $gateRoot 'hyperv-host-enable.json'
    if (Test-Path -LiteralPath $gateReceiptPath) { throw 'An activation receipt already exists; inspect it before any retry.' }
    try {
        $gateEnabled = Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -All -NoRestart
        $gateReceipt = @{ schema='forge.gate7.hyperv-host-feature.v1'; utc=[DateTime]::UtcNow.ToString('o');
            succeeded=$true; restart_needed=[bool]$gateEnabled.RestartNeeded; automatic_restart=$false;
            next_step='Owner chooses restart time; after restart use -Mode Inspect, then -Mode Create' }
    } catch {
        $gateReceipt = @{ schema='forge.gate7.hyperv-host-feature.v1'; utc=[DateTime]::UtcNow.ToString('o');
            succeeded=$false; automatic_restart=$false; error=$_.Exception.Message }
        $gateReceipt | ConvertTo-Json | Set-Content -LiteralPath $gateReceiptPath -Encoding UTF8
        throw
    }
    $gateReceipt | ConvertTo-Json | Set-Content -LiteralPath $gateReceiptPath -Encoding UTF8
    $gateReceipt | ConvertTo-Json
    return
}

Import-Module Hyper-V
if ((Get-Service vmms).Status -ne 'Running') { throw 'Hyper-V VM management must already be running.' }
if (Get-VM -Name $gateName -ErrorAction SilentlyContinue) { throw 'Existing qualification VM preserved.' }
$gateSwitch = Get-VMSwitch -Name $SwitchName -ErrorAction Stop
if ($gateSwitch.SwitchType -ne 'Internal') { throw 'Use an existing internal NAT switch; no external host network is modified.' }
$gateRoot = [IO.Path]::GetFullPath($gateRoot)
$gateVm = Join-Path $gateRoot 'windows-hyperv'
$gateMedia = Join-Path $gateRoot 'inputs\26300.9457.260913-1737.26h2_ge_release_svc_refresh_CLIENTENTERPRISEEVAL_OEMRET_x64FRE_fr-fr.iso'
$gateSeed = Join-Path $gateRoot 'windows\seed.iso'
foreach ($gatePath in @($gateRoot, (Split-Path $gateRoot), (Split-Path (Split-Path $gateRoot)), $gateMedia, $gateSeed)) {
    $gateItem = Get-Item -LiteralPath $gatePath -Force
    if ($gateItem.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Qualification inputs must be physical paths on E:.' }
}
if (Test-Path -LiteralPath $gateVm) { throw 'Existing VM directory preserved.' }
if ((Get-FileHash -LiteralPath $gateMedia -Algorithm SHA256).Hash.ToLowerInvariant() -ne '4614fb8cdd960bc9ecf1b8ab9bc708335cf7b350b152c045ff6259ec5ff4fc01') { throw 'Microsoft ISO digest mismatch.' }
if ((Get-PSDrive E).Free -lt 20GB) { throw 'At least 20 GiB free on E: is required before creating the dynamic disk.' }
$gateSeedHash = (Get-FileHash -LiteralPath $gateSeed -Algorithm SHA256).Hash.ToLowerInvariant()
$gateDisk = Join-Path $gateVm 'system.vhdx'
New-Item -ItemType Directory -Path $gateVm | Out-Null
$gateMachine = New-VM -Name $gateName -Generation 2 -MemoryStartupBytes 8GB -NewVHDPath $gateDisk -NewVHDSizeBytes 80GB -Path $gateVm -SwitchName $gateSwitch.Name
Set-VM -VM $gateMachine -AutomaticCheckpointsEnabled $false -AutomaticStartAction Nothing -AutomaticStopAction ShutDown
Set-VMProcessor -VM $gateMachine -Count 4
Set-VMMemory -VM $gateMachine -DynamicMemoryEnabled $false
Set-VMKeyProtector -VM $gateMachine -NewLocalKeyProtector
Enable-VMTPM -VM $gateMachine
$gateDvd = Add-VMDvdDrive -VM $gateMachine -Path $gateMedia -Passthru
Add-VMDvdDrive -VM $gateMachine -Path $gateSeed
Set-VMFirmware -VM $gateMachine -EnableSecureBoot On -SecureBootTemplate MicrosoftWindows -FirstBootDevice $gateDvd
$gateDescriptor = @{
    schema='forge.gate7.hyperv-windows-vm.v1'; vm_id=[string]$gateMachine.Id; vm_name=$gateName
    memory_mib=8192; vcpus=4; disk_max_gib=80; disk=$gateDisk; generation=2
    security=(Get-VMSecurity -VM $gateMachine | Select-Object TpmEnabled)
    secure_boot=[string](Get-VMFirmware -VM $gateMachine).SecureBoot
    seed_sha256=$gateSeedHash; host_filesystem_shared=$false
    host_network_modified=$false; started=$false; os_installation_qualified=$false
    next_step='Start this VM after reviewing this descriptor; install from the verified Microsoft media.'
}
$gateDescriptor | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $gateVm 'launch.json') -Encoding UTF8
$gateDescriptor | ConvertTo-Json -Depth 6
