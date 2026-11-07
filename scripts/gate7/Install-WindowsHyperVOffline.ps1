<#
Deploy the verified Microsoft WIM to the NEW, RAW VHDX of the ONE Gate 7 VM.
No disk cleaning, host boot changes, TPM bypass or automatic host restart.
Refuse any existing partition table, system disk, attached VHD or changed input.
https://learn.microsoft.com/windows-hardware/manufacture/desktop/capture-and-apply-windows-using-a-single-wim
https://learn.microsoft.com/windows-hardware/manufacture/desktop/windows-setup-automation-overview
The local answer file contains a disposable password: never print or export it.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]{1,48}$')][string]$RunId,
    [switch]$ResumeAppliedImage,
    [switch]$CreateExplicitBootStore
)
$ErrorActionPreference='Stop'
$gateRoot='E:\OpenRPG_Forge\.build\vm-qualification'
$gateVmRoot=Join-Path $gateRoot 'windows-hyperv'
$env:TEMP='E:\OpenRPG_Forge\.build\tmp'; $env:TMP=$env:TEMP
$gateOut=Join-Path $gateVmRoot $RunId
if (Test-Path -LiteralPath $gateOut) { throw 'Existing deployment evidence preserved' }
if ($ResumeAppliedImage) {
    $gatePrevious=Get-Content (Join-Path $gateVmRoot 'offline-deploy-001\receipt.json') -Raw | ConvertFrom-Json
    if ($gatePrevious.succeeded -ne $false -or -not $gatePrevious.disk_preserved -or $gatePrevious.error -notmatch '0x08') {
        throw 'Resume is limited to the inspected image applied before the known private-seed XML error'
    }
}
if ($CreateExplicitBootStore) {
    $gateBootFailure=Get-Content (Join-Path $gateVmRoot 'offline-deploy-003\receipt.json') -Raw | ConvertFrom-Json
    if (-not $ResumeAppliedImage -or $gateBootFailure.succeeded -ne $false -or $gateBootFailure.error -ne 'Guest EFI provisioning failed: 183') {
        throw 'Explicit file boot store is limited to the inspected BCDBoot registry-name collision'
    }
}
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Elevated Hyper-V and offline image access required' }
Import-Module Hyper-V
$gateDescriptor=Get-Content (Join-Path $gateVmRoot 'launch.json') -Raw | ConvertFrom-Json
$gateVm=Get-VM -Id ([Guid]$gateDescriptor.vm_id)
if ($gateVm.Name -ne 'Forge-Gate7-Windows') { throw 'Wrong VM identity' }
$gateDisk=Join-Path $gateVmRoot 'system.vhdx'
$gateIso=Join-Path $gateRoot 'inputs\26300.9457.260913-1737.26h2_ge_release_svc_refresh_CLIENTENTERPRISEEVAL_OEMRET_x64FRE_fr-fr.iso'
$gateSeed=Join-Path $gateRoot 'windows\seed.iso'
$gateBootstrap=Join-Path $PSScriptRoot 'Bootstrap-WindowsGuest.ps1'
foreach ($gatePath in @($gateDisk,$gateIso,$gateSeed,$gateBootstrap)) {
    $gateCursor=Get-Item -LiteralPath $gatePath -Force
    while ($gateCursor) {
        if ($gateCursor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Physical qualification paths required' }
        $gateParent=Split-Path -Parent $gateCursor.FullName
        if (-not $gateParent -or $gateParent -eq $gateCursor.FullName) { break }
        $gateCursor=Get-Item -LiteralPath $gateParent -Force
    }
}
if ((Get-FileHash $gateIso -Algorithm SHA256).Hash.ToLowerInvariant() -ne '4614fb8cdd960bc9ecf1b8ab9bc708335cf7b350b152c045ff6259ec5ff4fc01') { throw 'Microsoft media changed' }
if ((Get-FileHash $gateSeed -Algorithm SHA256).Hash.ToLowerInvariant() -ne $gateDescriptor.seed_sha256) { throw 'Private seed changed' }
if (-not (Get-VMSecurity -VM $gateVm).TpmEnabled -or (Get-VMFirmware -VM $gateVm).SecureBoot -ne 'On') { throw 'Supported Windows security devices required' }
$gateDrive=@(Get-VMHardDiskDrive -VM $gateVm)
if ($gateDrive.Count -ne 1 -or [IO.Path]::GetFullPath($gateDrive[0].Path) -ne [IO.Path]::GetFullPath($gateDisk)) { throw 'Unexpected VM disk attachment' }
if ((Get-PSDrive E).Free -lt 35GB) { throw '35 GiB free on E: required for offline deployment' }
New-Item -ItemType Directory -Path $gateOut | Out-Null
$gateScratch=Join-Path $gateOut 'scratch'
New-Item -ItemType Directory -Path $gateScratch | Out-Null
Start-Transcript -LiteralPath (Join-Path $gateOut 'operation.log') | Out-Null
$gateVhdMounted=$false; $gateOwnedIsos=@(); $gateSucceeded=$false
try {
    # The operator has inspected the boot failure. Existing installed disks are
    # still refused below even if the guest changed since that observation.
    if ($gateVm.State -eq 'Running') { Stop-VM -VM $gateVm -TurnOff -Force }
    if ((Get-VM -Id $gateVm.Id).State -ne 'Off') { throw 'Guest must be fully off' }
    if ((Get-VHD -Path $gateDisk).Attached) { throw 'Existing mounted VHD preserved' }
    $gateDiskInfo=Mount-VHD -Path $gateDisk -NoDriveLetter -Passthru | Get-Disk
    $gateVhdMounted=$true
    function Assert-GateDisk {
        $gateCurrent=Get-Disk -Number $gateDiskInfo.Number
        $gateBacking=Get-VHD -DiskNumber $gateCurrent.Number
        if ($gateCurrent.IsBoot -or $gateCurrent.IsSystem -or $gateCurrent.Size -ne 80GB -or
            [IO.Path]::GetFullPath($gateBacking.Path) -ne [IO.Path]::GetFullPath($gateDisk)) { throw 'Refuse non-qualification/system disk' }
    }
    Assert-GateDisk
    if (-not $ResumeAppliedImage -and ($gateDiskInfo.PartitionStyle -ne 'RAW' -or @(Get-Partition -DiskNumber $gateDiskInfo.Number -ErrorAction SilentlyContinue).Count)) { throw 'Existing partitions preserved: offline provisioning refuses this disk' }
    if ($gateDiskInfo.IsOffline) { Set-Disk -Number $gateDiskInfo.Number -IsOffline $false }
    if ($gateDiskInfo.IsReadOnly) { throw 'Read-only virtual disk preserved' }
    $gateMediaVolumes=@()
    foreach ($gateMedia in @($gateIso,$gateSeed)) {
        $gateImage=Get-DiskImage -ImagePath $gateMedia
        if (-not $gateImage.Attached) {
            $gateImage=Mount-DiskImage -ImagePath $gateMedia -StorageType ISO -Access ReadOnly -PassThru
            $gateOwnedIsos+=,$gateMedia
        }
        $gateVolume=@($gateImage | Get-Volume)
        if ($gateVolume.Count -ne 1 -or -not $gateVolume[0].DriveLetter) { throw 'Expected a single readable optical volume' }
        $gateMediaVolumes+=,($gateVolume[0].DriveLetter.ToString()+':\')
    }
    $gateWim=Join-Path $gateMediaVolumes[0] 'sources\install.wim'
    $gateImageInfo=Get-WindowsImage -ImagePath $gateWim -Index 1 -LogPath (Join-Path $gateOut 'inspect-image.log') -ScratchDirectory $gateScratch
    if ($gateImageInfo.EditionId -ne 'EnterpriseEval' -or $gateImageInfo.Architecture -ne 9) { throw 'Expected original x64 Enterprise evaluation image' }
    $gateImageInfo | Select-Object ImageName,EditionId,Architecture,Version
    $gateAnswer=New-Object Xml.XmlDocument
    $gateAnswerText=[IO.File]::ReadAllText((Join-Path $gateMediaVolumes[1] 'autounattend.xml'))
    # Preserve the historical seed. Repair only its one known Python \\b escape
    # in the bootstrap path, then strictly parse the complete private XML.
    $gateAnswerText=$gateAnswerText.Replace((':'+[char]8+'ootstrap.ps1'),':/bootstrap.ps1')
    $gateAnswer.LoadXml($gateAnswerText)
    $gateNs=New-Object Xml.XmlNamespaceManager($gateAnswer.NameTable)
    $gateNs.AddNamespace('u','urn:schemas-microsoft-com:unattend')
    foreach ($gateNode in @($gateAnswer.SelectNodes('/u:unattend/u:settings[@pass="windowsPE"]',$gateNs))) { [void]$gateNode.ParentNode.RemoveChild($gateNode) }
    $gatePublic=(Get-Content (Join-Path $gateRoot 'windows\ssh_ed25519.pub') -Raw).Trim()
    if ($gatePublic -notmatch '^ssh-ed25519 [A-Za-z0-9+/=]+(?: [A-Za-z0-9_-]+)?$') { throw 'Unexpected qualification public key format' }
    $gateFirstLogon=$gateAnswer.SelectSingleNode('//u:FirstLogonCommands/u:SynchronousCommand/u:CommandLine',$gateNs)
    if (-not $gateFirstLogon) { throw 'Expected one private-seed first-logon bootstrap' }
    $gateFirstLogon.InnerText='powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\ForgeQualification\bootstrap.ps1 -PublicKey "'+$gatePublic+'"'
    Assert-GateDisk
    if ($ResumeAppliedImage) {
        $gateParts=@(Get-Partition -DiskNumber $gateDiskInfo.Number)
        # Initialize-Disk also created a small MSR in this first deployment.
        # Keep both reserved partitions. Admit only the exact GPT independently
        # inspected read-only after the refusal, not an arbitrary existing disk.
        $gateInspection=Get-Content (Join-Path $gateVmRoot 'gpt-inspection-001.json') -Raw | ConvertFrom-Json
        if ($gateDiskInfo.PartitionStyle -ne 'GPT' -or -not $gateInspection.gpt_crcs_valid -or
            [Guid]$gateDiskInfo.Guid -ne [Guid]$gateInspection.disk_id -or $gateParts.Count -ne $gateInspection.partitions.Count) { throw 'Previously inspected guest GPT identity changed' }
        foreach ($gateExpected in $gateInspection.partitions) {
            $gateActual=@($gateParts | Where-Object { $_.PartitionNumber -eq $gateExpected.number })
            if ($gateActual.Count -ne 1 -or [Guid]$gateActual[0].Guid -ne [Guid]$gateExpected.id -or
                [Guid]$gateActual[0].GptType -ne [Guid]$gateExpected.type -or
                $gateActual[0].Offset -ne ($gateExpected.start_lba*512) -or $gateActual[0].Size -ne $gateExpected.bytes) { throw 'Previously inspected guest partition changed' }
        }
        $gateEfi=@($gateParts | Where-Object { $_.GptType -eq '{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}' })
        $gateOsPartition=@($gateParts | Where-Object { $_.GptType -eq '{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}' })
        if ($gateEfi.Count -ne 1 -or $gateOsPartition.Count -ne 1 -or
            ($gateEfi[0] | Get-Volume).FileSystemLabel -ne 'ForgeG7EFI' -or
            ($gateOsPartition[0] | Get-Volume).FileSystemLabel -ne 'ForgeG7Windows') { throw 'Existing guest partition identities do not match the inspected deployment' }
        $gateEfi=$gateEfi[0]; $gateOsPartition=$gateOsPartition[0]
        foreach ($gatePartition in @($gateEfi,$gateOsPartition)) {
            if (-not [char]::IsLetter($gatePartition.DriveLetter)) { $gatePartition | Add-PartitionAccessPath -AssignDriveLetter }
        }
        $gateEfi=Get-Partition -DiskNumber $gateDiskInfo.Number -PartitionNumber $gateEfi.PartitionNumber
        $gateOsPartition=Get-Partition -DiskNumber $gateDiskInfo.Number -PartitionNumber $gateOsPartition.PartitionNumber
    } else {
        Initialize-Disk -Number $gateDiskInfo.Number -PartitionStyle GPT | Out-Null
    $gateEfi=New-Partition -DiskNumber $gateDiskInfo.Number -Size 260MB -GptType '{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}' -AssignDriveLetter
    Assert-GateDisk
    Format-Volume -Partition $gateEfi -FileSystem FAT32 -NewFileSystemLabel ForgeG7EFI -Confirm:$false -Force | Out-Null
    if (-not @(Get-Partition -DiskNumber $gateDiskInfo.Number | Where-Object { $_.GptType -eq '{e3c9e316-0b5c-4db8-817d-f92df00215ae}' }).Count) {
        New-Partition -DiskNumber $gateDiskInfo.Number -Size 16MB -GptType '{e3c9e316-0b5c-4db8-817d-f92df00215ae}' | Out-Null
    }
    $gateOsPartition=New-Partition -DiskNumber $gateDiskInfo.Number -UseMaximumSize -GptType '{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}' -AssignDriveLetter
    Assert-GateDisk
        Format-Volume -Partition $gateOsPartition -FileSystem NTFS -NewFileSystemLabel ForgeG7Windows -Confirm:$false -Force | Out-Null
    }
    if (-not [char]::IsLetter($gateEfi.DriveLetter) -or -not [char]::IsLetter($gateOsPartition.DriveLetter)) { throw 'Expected assigned letters on the two new guest volumes' }
    $gateOsRoot=$gateOsPartition.DriveLetter.ToString()+':\'
    $gateEfiRoot=$gateEfi.DriveLetter.ToString()+':'
    if ($ResumeAppliedImage) {
        foreach ($gateRequired in @('Windows\System32\config\SYSTEM','Windows\System32\winload.efi','Windows\System32\ntoskrnl.exe')) {
            if (-not (Test-Path -LiteralPath (Join-Path $gateOsRoot $gateRequired) -PathType Leaf)) { throw 'Previously applied Windows image incomplete; no rewrite attempted' }
        }
    } else {
        & "$env:SystemRoot\System32\dism.exe" /Apply-Image "/ImageFile:$gateWim" /Index:1 "/ApplyDir:$gateOsRoot" /CheckIntegrity /Verify "/ScratchDir:$gateScratch" "/LogPath:$(Join-Path $gateOut 'dism.log')"
        if ($LASTEXITCODE -ne 0) { throw "DISM apply failed: $LASTEXITCODE; preserve the disk and logs" }
    }
    $gateGuestTools=Join-Path $gateOsRoot 'ForgeQualification'
    New-Item -ItemType Directory -Path $gateGuestTools -Force | Out-Null
    $gateGuestBootstrap=Join-Path $gateGuestTools 'bootstrap.ps1'
    if (Test-Path -LiteralPath $gateGuestBootstrap) {
        if ((Get-FileHash $gateGuestBootstrap).Hash -ne (Get-FileHash $gateBootstrap).Hash) { throw 'Previously copied guest bootstrap changed' }
    } else { Copy-Item -LiteralPath $gateBootstrap -Destination $gateGuestBootstrap }
    $gatePanther=Join-Path $gateOsRoot 'Windows\Panther'
    New-Item -ItemType Directory -Path $gatePanther -Force | Out-Null
    $gateAnswerPath=Join-Path $gatePanther 'Unattend.xml'
    if (Test-Path -LiteralPath $gateAnswerPath) {
        $gateExistingAnswer=New-Object Xml.XmlDocument
        $gateExistingAnswer.Load($gateAnswerPath)
        if (-not $CreateExplicitBootStore -or $gateExistingAnswer.OuterXml -ne $gateAnswer.OuterXml) { throw 'Existing image answer file preserved' }
    } else { $gateAnswer.Save($gateAnswerPath) }
    Assert-GateDisk
    if ($CreateExplicitBootStore) {
        # Every BCDEdit operation names an explicit file. Never unload or modify
        # the host's BCD00000000 hive to work around BCDBoot's collision.
        $gateBootDirectory=Join-Path ($gateEfiRoot+'\') 'EFI\Microsoft\Boot'
        $gateBootManager=Join-Path $gateBootDirectory 'bootmgfw.efi'
        $gateOriginalManagers=@('Windows\Boot\EFI\bootmgfw.efi','Windows\Boot\EFI_EX\bootmgfw.efi') |
            ForEach-Object { Join-Path $gateOsRoot $_ } | Where-Object { Test-Path -LiteralPath $_ }
        $gateOriginalHashes=@($gateOriginalManagers | ForEach-Object { Get-FileHash -LiteralPath $_ } | Select-Object -ExpandProperty Hash)
        if (-not (Test-Path -LiteralPath $gateBootManager) -or (Get-FileHash $gateBootManager).Hash -notin $gateOriginalHashes) { throw 'Guest boot manager must match the original applied Microsoft image' }
        $gateFallback=Join-Path ($gateEfiRoot+'\') 'EFI\Boot\bootx64.efi'
        if (-not (Test-Path -LiteralPath $gateFallback)) {
            New-Item -ItemType Directory -Path (Split-Path $gateFallback) -Force | Out-Null
            Copy-Item -LiteralPath $gateBootManager -Destination $gateFallback
        }
        if ((Get-FileHash $gateFallback).Hash -notin $gateOriginalHashes) { throw 'Guest fallback loader differs from the original applied Microsoft image' }
        $gateStore=Join-Path $gateBootDirectory 'BCD.forge-staging'
        if (Test-Path -LiteralPath $gateStore) { throw 'Existing guest BCD staging preserved' }
        & "$env:SystemRoot\System32\bcdedit.exe" /createstore $gateStore
        if ($LASTEXITCODE -ne 0) { throw "Explicit guest BCD creation failed: $LASTEXITCODE" }
        function Invoke-GateBcd([string[]]$BcdArguments) {
            & "$env:SystemRoot\System32\bcdedit.exe" /store $gateStore @BcdArguments
            if ($LASTEXITCODE -ne 0) { throw "Explicit guest BCD operation failed: $LASTEXITCODE" }
        }
        $gateLoader='{'+[Guid]::NewGuid().ToString()+'}'
        Invoke-GateBcd -BcdArguments @('/create','{bootmgr}','/d','Windows Boot Manager')
        # PARTITION on a host-mounted VHD can encode a native-host VHD boot
        # reference. HD_PARTITION names the internal GPT partition instead;
        # the guest must never resolve the host E: path to its own VHDX.
        Invoke-GateBcd -BcdArguments @('/set','{bootmgr}','device',('HD_PARTITION='+$gateEfiRoot))
        Invoke-GateBcd -BcdArguments @('/set','{bootmgr}','path','\EFI\Microsoft\Boot\bootmgfw.efi')
        Invoke-GateBcd -BcdArguments @('/create',$gateLoader,'/d','Windows 11 Qualification','/application','osloader')
        Invoke-GateBcd -BcdArguments @('/set',$gateLoader,'device',('HD_PARTITION='+$gateOsPartition.DriveLetter+':'))
        Invoke-GateBcd -BcdArguments @('/set',$gateLoader,'osdevice',('HD_PARTITION='+$gateOsPartition.DriveLetter+':'))
        Invoke-GateBcd -BcdArguments @('/set',$gateLoader,'path','\Windows\System32\winload.efi')
        Invoke-GateBcd -BcdArguments @('/set',$gateLoader,'systemroot','\Windows')
        Invoke-GateBcd -BcdArguments @('/set',$gateLoader,'locale','fr-FR')
        Invoke-GateBcd -BcdArguments @('/displayorder',$gateLoader)
        Invoke-GateBcd -BcdArguments @('/default',$gateLoader)
        Invoke-GateBcd -BcdArguments @('/timeout','0')
        Invoke-GateBcd -BcdArguments @('/enum','all')
        $gateFinalStore=Join-Path $gateBootDirectory 'BCD'
        if (Test-Path -LiteralPath $gateFinalStore) {
            $gateBackup=Join-Path $gateBootDirectory 'BCD.before-forge-explicit'
            if (Test-Path -LiteralPath $gateBackup) { throw 'Existing guest BCD backup preserved' }
            Move-Item -LiteralPath $gateFinalStore -Destination $gateBackup -Force
        }
        Move-Item -LiteralPath $gateStore -Destination $gateFinalStore -Force
    } else {
        & "$env:SystemRoot\System32\bcdboot.exe" (Join-Path $gateOsRoot 'Windows') /s $gateEfiRoot /f UEFI /v
        if ($LASTEXITCODE -ne 0) { throw "Guest EFI provisioning failed: $LASTEXITCODE" }
    }
    $gateSucceeded=$true
} catch {
    @{ succeeded=$false; utc=[DateTime]::UtcNow.ToString('o'); error=$_.Exception.Message; script_stack=$_.ScriptStackTrace; disk_preserved=$true; os_qualified=$false } | ConvertTo-Json | Set-Content (Join-Path $gateOut 'receipt.json') -Encoding UTF8
    throw
} finally {
    if ($gateVhdMounted) { Dismount-VHD -Path $gateDisk }
    foreach ($gateMedia in $gateOwnedIsos) { Dismount-DiskImage -ImagePath $gateMedia | Out-Null }
    Stop-Transcript | Out-Null
}
if ($gateSucceeded) {
    # Preserve both source ISOs on E:, but remove their guest attachment after use.
    Get-VMDvdDrive -VM $gateVm | Set-VMDvdDrive -Path $null
    Set-VMFirmware -VM $gateVm -FirstBootDevice $gateDrive[0]
    Start-VM -VM $gateVm
    @{ schema='forge.gate7.windows-offline-deployment.v1'; succeeded=$true; utc=[DateTime]::UtcNow.ToString('o');
        vm_id=[string]$gateVm.Id; edition='EnterpriseEval'; original_media_sha256='4614fb8cdd960bc9ecf1b8ab9bc708335cf7b350b152c045ff6259ec5ff4fc01';
        image_index=1; first_guest_boot_started=$true; tpm_and_secure_boot_preserved=$true;
        resumed_applied_image=[bool]$ResumeAppliedImage;
        explicit_guest_bcd_file=[bool]$CreateExplicitBootStore;
        host_boot_configuration_changed=$false; host_restart_requested=$false; os_qualified=$false;
        bootstrap_sha256=(Get-FileHash $gateBootstrap -Algorithm SHA256).Hash.ToLowerInvariant() } | ConvertTo-Json | Set-Content (Join-Path $gateOut 'receipt.json') -Encoding UTF8
}
