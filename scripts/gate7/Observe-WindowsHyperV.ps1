<#
Bounded observation of the one disposable Gate 7 VM. Run elevated in Windows
PowerShell. BootInstallation starts only an OFF VM and holds Space during the
DVD firmware prompt; no keyboard input is sent after those first fifteen seconds.
RestartInstallation is explicit, only after inspecting an installation boot failure.
No host keyboard, VM recreation, security bypass or automatic host restart.
Thumbnails use Microsoft's documented Hyper-V WMI RGB565 API.
https://learn.microsoft.com/windows/win32/hyperv_v2/getvirtualsystemthumbnailimage-msvm-virtualsystemmanagementservice
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]{1,48}$')][string]$RunId,
    [switch]$BootInstallation,
    [switch]$RestartInstallation,
    [switch]$StartInstalled,
    [ValidateRange(1,30)][int]$CaptureCount = 1,
    [ValidateRange(15,60)][int]$IntervalSeconds = 30
)
$ErrorActionPreference = 'Stop'
if (([int][bool]$BootInstallation + [int][bool]$RestartInstallation + [int][bool]$StartInstalled) -gt 1) { throw 'Choose exactly one guest start mode.' }
$gateRoot = 'E:\OpenRPG_Forge\.build\vm-qualification\windows-hyperv'
$gateDescriptor = Get-Content -LiteralPath (Join-Path $gateRoot 'launch.json') -Raw | ConvertFrom-Json
if ($gateDescriptor.vm_name -ne 'Forge-Gate7-Windows') { throw 'Unexpected qualification descriptor' }
$gateOut = Join-Path $gateRoot $RunId
if (Test-Path -LiteralPath $gateOut) { throw 'Observation directory already exists; preserve it.' }
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator Hyper-V access required' }
Import-Module Hyper-V
$gateVm = Get-VM -Id ([Guid]$gateDescriptor.vm_id)
if ($gateVm.Name -ne $gateDescriptor.vm_name) { throw 'Unexpected VM identity' }
New-Item -ItemType Directory -Path $gateOut | Out-Null
Start-Transcript -LiteralPath (Join-Path $gateOut 'operation.log') | Out-Null
try {
    if ($StartInstalled) {
        $gateDeployment = Get-Content -LiteralPath (Join-Path $gateRoot 'offline-deploy-005\receipt.json') -Raw | ConvertFrom-Json
        if (-not $gateDeployment.succeeded -or $gateVm.State -ne 'Off') { throw 'Expected the stopped guest with its successful offline deployment receipt.' }
        if (@(Get-VMDvdDrive -VM $gateVm | Where-Object Path).Count) { throw 'Installed boot requires detached optical media.' }
        Start-VM -VM $gateVm
    }
    $gateSystem = Get-WmiObject -Namespace root/virtualization/v2 -Class Msvm_ComputerSystem -Filter "Name='$($gateVm.Id)'"
    $gateService = Get-WmiObject -Namespace root/virtualization/v2 -Class Msvm_VirtualSystemManagementService
    if ($BootInstallation -or $RestartInstallation) {
        if ($BootInstallation) {
            if ($gateVm.State -ne 'Off') { throw 'Installation boot requires the VM to be OFF; running guests are preserved.' }
            Start-VM -VM $gateVm
        } else {
            if ($gateVm.State -ne 'Running') { throw 'Expected the already inspected running guest at its failed installation boot menu.' }
            Restart-VM -VM $gateVm -Force
        }
        $gateKeyboard = @($gateSystem.GetRelated('Msvm_Keyboard'))
        if ($gateKeyboard.Count -ne 1) { throw 'Expected one keyboard belonging to the qualification guest' }
        try {
            for ($gateKey = 0; $gateKey -lt 30; $gateKey++) {
                Start-Sleep -Milliseconds 500
                $gateResult = $gateKeyboard[0].PressKey(32)
                Write-Output "Guest firmware Space-down $gateKey returned $($gateResult.ReturnValue)"
                # Firmware may not expose the guest keyboard during a reset.
                if ($gateResult.ReturnValue -notin @(0,32775)) { throw "Guest Space failed: $($gateResult.ReturnValue)" }
            }
        } finally {
            $gateRelease = $gateKeyboard[0].ReleaseKey(32)
            Write-Output "Guest firmware Space-up returned $($gateRelease.ReturnValue)"
        }
    }
    Add-Type -AssemblyName System.Drawing
    for ($gateIndex = 0; $gateIndex -lt $CaptureCount; $gateIndex++) {
        $gateVm = Get-VM -Id ([Guid]$gateDescriptor.vm_id)
        $gateState = @{ utc=[DateTime]::UtcNow.ToString('o'); vm_id=[string]$gateVm.Id; state=[string]$gateVm.State;
            uptime_seconds=$gateVm.Uptime.TotalSeconds; addresses=@((Get-VMNetworkAdapter -VM $gateVm).IPAddresses);
            os_installation_qualified=$false }
        if ($gateVm.State -eq 'Running') {
            $gateSettings = @($gateSystem.GetRelated('Msvm_VirtualSystemSettingData', 'Msvm_SettingsDefineState', $null, $null, 'SettingData', 'ManagedElement', $false, $null))
            if ($gateSettings.Count -ne 1) { throw 'Expected exactly one current guest settings object' }
            $gateParams = $gateService.GetMethodParameters('GetVirtualSystemThumbnailImage')
            $gateParams.TargetSystem = $gateSettings[0].Path.Path
            $gateParams.WidthPixels = [UInt16]1024
            $gateParams.HeightPixels = [UInt16]768
            $gateImage = $gateService.InvokeMethod('GetVirtualSystemThumbnailImage', $gateParams, $null)
            if ($gateImage.ReturnValue -ne 0) { throw "Guest thumbnail failed: $($gateImage.ReturnValue)" }
            [byte[]]$gatePixels = $gateImage.ImageData
            $gateOffset = 0
            # Windows 11 build 26300 returns an octet-string length in network
            # byte order, including the four-byte prefix itself. This was checked
            # against the preserved raw response from installation-005.
            $gateNetworkLength = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($gatePixels,0))
            if ($gatePixels.Length -eq (1024*768*2+4) -and $gateNetworkLength -eq $gatePixels.Length) {
                $gateOffset = 4
                $gateState.rgb565_prefix = 'big_endian_total_octet_length'
            }
            # Some Windows providers prefix the octet array with its byte count.
            # Accept that form only when the prefix and exact expected size agree.
            if ($gatePixels.Length -eq (1024*768*2+4) -and [BitConverter]::ToUInt32($gatePixels,0) -eq (1024*768*2)) {
                $gateOffset = 4
                $gateState.rgb565_prefix = 'little_endian_pixel_byte_count'
            }
            if ($gatePixels.Length - $gateOffset -ne (1024*768*2)) {
                [IO.File]::WriteAllBytes((Join-Path $gateOut ('{0:d3}.rgb565' -f $gateIndex)), $gatePixels)
                Write-Output ('Thumbnail prefix: ' + [BitConverter]::ToString($gatePixels,0,[Math]::Min(16,$gatePixels.Length)))
                $gateImage.Properties | ForEach-Object { "Thumbnail property $($_.Name): type=$($_.Type); array=$($_.IsArray); count=$(@($_.Value).Count)" }
                throw "Unexpected RGB565 thumbnail length: $($gatePixels.Length); width=$($gateParams.WidthPixels); height=$($gateParams.HeightPixels)"
            }
            $gateBitmap = New-Object Drawing.Bitmap(1024, 768, [Drawing.Imaging.PixelFormat]::Format16bppRgb565)
            try {
                $gateBits = $gateBitmap.LockBits((New-Object Drawing.Rectangle(0,0,1024,768)), [Drawing.Imaging.ImageLockMode]::WriteOnly, [Drawing.Imaging.PixelFormat]::Format16bppRgb565)
                try { [Runtime.InteropServices.Marshal]::Copy($gatePixels, $gateOffset, $gateBits.Scan0, 1024*768*2) }
                finally { $gateBitmap.UnlockBits($gateBits) }
                $gateFile = '{0:d3}.png' -f $gateIndex
                $gateBitmap.Save((Join-Path $gateOut $gateFile), [Drawing.Imaging.ImageFormat]::Png)
                $gateState.image = $gateFile
            } finally { $gateBitmap.Dispose() }
        }
        $gateState | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $gateOut ('{0:d3}.json' -f $gateIndex)) -Encoding UTF8
        $gateState | ConvertTo-Json -Compress
        if ($gateIndex + 1 -lt $CaptureCount) { Start-Sleep -Seconds $IntervalSeconds }
    }
    @{ succeeded=$true; utc=[DateTime]::UtcNow.ToString('o'); vm_id=[string]$gateVm.Id; captures=$CaptureCount } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $gateOut 'receipt.json') -Encoding UTF8
} catch {
    @{ succeeded=$false; utc=[DateTime]::UtcNow.ToString('o'); error=$_.Exception.Message } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $gateOut 'receipt.json') -Encoding UTF8
    throw
} finally { Stop-Transcript | Out-Null }
