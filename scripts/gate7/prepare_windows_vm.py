"""Prepare a disposable Windows evaluation guest; no host disks or folders are attached."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import secrets
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--qemu", type=Path, required=True)
    args = parser.parse_args()
    root = args.root.resolve(strict=True)
    vm = root / "windows"
    if vm.exists():
        raise ValueError("Existing Windows guest preserved")
    iso = root / "inputs/26300.9457.260913-1737.26h2_ge_release_svc_refresh_CLIENTENTERPRISEEVAL_OEMRET_x64FRE_fr-fr.iso"
    with iso.open("rb") as stream:
        if hashlib.file_digest(stream, "sha256").hexdigest() != "4614fb8cdd960bc9ecf1b8ab9bc708335cf7b350b152c045ff6259ec5ff4fc01":
            raise ValueError("Windows media differs from the Microsoft publisher digest")
    sys.path.insert(0, str(root / "inputs/pycdlib-1.22.0-py3-none-any.whl"))
    import pycdlib
    vm.mkdir()
    key = vm / "ssh_ed25519"
    subprocess.run(["C:/Windows/System32/OpenSSH/ssh-keygen.exe", "-q", "-t", "ed25519", "-N", "", "-C", "forge-gate7-local-windows", "-f", str(key)], check=True)
    public = key.with_suffix(".pub").read_text("utf-8").strip()
    password = "Forge7_" + secrets.token_urlsafe(18)
    # Password exists only in the local seed ISO; never print or put it in tracked sources/evidence.
    answer = f'''<?xml version="1.0" encoding="utf-8"?>
<unattend xmlns="urn:schemas-microsoft-com:unattend" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State">
 <settings pass="windowsPE">
  <component name="Microsoft-Windows-International-Core-WinPE" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
   <SetupUILanguage><UILanguage>fr-FR</UILanguage></SetupUILanguage><InputLocale>040c:0000040c</InputLocale><SystemLocale>fr-FR</SystemLocale><UILanguage>fr-FR</UILanguage><UserLocale>fr-FR</UserLocale>
  </component>
  <component name="Microsoft-Windows-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
   <DiskConfiguration><Disk wcm:action="add"><DiskID>0</DiskID><WillWipeDisk>true</WillWipeDisk>
    <CreatePartitions>
     <CreatePartition wcm:action="add"><Order>1</Order><Type>EFI</Type><Size>260</Size></CreatePartition>
     <CreatePartition wcm:action="add"><Order>2</Order><Type>MSR</Type><Size>16</Size></CreatePartition>
     <CreatePartition wcm:action="add"><Order>3</Order><Type>Primary</Type><Extend>true</Extend></CreatePartition>
    </CreatePartitions>
    <ModifyPartitions>
     <ModifyPartition wcm:action="add"><Order>1</Order><PartitionID>1</PartitionID><Format>FAT32</Format><Label>System</Label></ModifyPartition>
     <ModifyPartition wcm:action="add"><Order>2</Order><PartitionID>3</PartitionID><Format>NTFS</Format><Label>Windows</Label><Letter>C</Letter></ModifyPartition>
    </ModifyPartitions>
   </Disk><WillShowUI>OnError</WillShowUI></DiskConfiguration>
   <ImageInstall><OSImage><InstallFrom><MetaData wcm:action="add"><Key>/IMAGE/INDEX</Key><Value>1</Value></MetaData></InstallFrom><InstallTo><DiskID>0</DiskID><PartitionID>3</PartitionID></InstallTo><WillShowUI>OnError</WillShowUI></OSImage></ImageInstall>
   <UserData><AcceptEula>true</AcceptEula><FullName>Forge Qualification</FullName><Organization>Local qualification</Organization></UserData>
   <DynamicUpdate><Enable>false</Enable></DynamicUpdate>
  </component>
 </settings>
 <settings pass="specialize"><component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS"><ComputerName>FORGE-G7-WIN</ComputerName><TimeZone>Romance Standard Time</TimeZone></component></settings>
 <settings pass="oobeSystem"><component name="Microsoft-Windows-Shell-Setup" processorArchitecture="amd64" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS">
  <OOBE><HideEULAPage>true</HideEULAPage><HideOnlineAccountScreens>true</HideOnlineAccountScreens><HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE><ProtectYourPC>3</ProtectYourPC></OOBE>
  <UserAccounts><LocalAccounts><LocalAccount wcm:action="add"><Name>forge</Name><DisplayName>Forge Qualification</DisplayName><Group>Administrateurs</Group><Password><Value>{password}</Value><PlainText>true</PlainText></Password></LocalAccount></LocalAccounts></UserAccounts>
  <AutoLogon><Enabled>true</Enabled><Username>forge</Username><LogonCount>1</LogonCount><Password><Value>{password}</Value><PlainText>true</PlainText></Password></AutoLogon>
  <FirstLogonCommands><SynchronousCommand wcm:action="add"><Order>1</Order><Description>Local qualification access</Description><CommandLine>powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "&amp; ((Get-Volume -FileSystemLabel 'FORGESEED').DriveLetter + ':/bootstrap.ps1')"</CommandLine></SynchronousCommand></FirstLogonCommands>
 </component></settings>
</unattend>'''.encode("utf-8")
    ET.fromstring(answer)  # Validate the actual generated bytes before producing boot media.
    bootstrap = f'''$ErrorActionPreference = 'Stop'
if ($env:COMPUTERNAME -ne 'FORGE-G7-WIN') {{ throw 'Qualification guest required' }}
New-Item -ItemType Directory -Force C:/ForgeQualification | Out-Null
Start-Transcript C:/ForgeQualification/bootstrap.log
Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,BuildNumber
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
'{public}' | Set-Content C:/ProgramData/ssh/administrators_authorized_keys -Encoding ascii
icacls.exe C:/ProgramData/ssh/administrators_authorized_keys /inheritance:r /grant '*S-1-5-32-544:F' '*S-1-5-18:F'
Set-Service sshd -StartupType Automatic
Start-Service sshd
if (-not (Get-NetFirewallRule -Name ForgeQualificationSsh -ErrorAction SilentlyContinue)) {{ New-NetFirewallRule -Name ForgeQualificationSsh -DisplayName ForgeQualificationSsh -Direction Inbound -Protocol TCP -LocalPort 22 -Action Allow -RemoteAddress 10.0.2.2 }}
Stop-Transcript
'''.encode("utf-8-sig")
    seed = pycdlib.PyCdlib()
    seed.new(interchange_level=3, joliet=3, rock_ridge="1.09", vol_ident="FORGESEED")
    for name, iso_name, data in [("autounattend.xml", "AUTOUNATTEND.XML;1", answer), ("bootstrap.ps1", "BOOTSTRAP.PS1;1", bootstrap)]:
        seed.add_fp(io.BytesIO(data), len(data), iso_path="/" + iso_name, rr_name=name, joliet_path="/" + name)
    seed_path = vm / "seed.iso"
    seed.write(str(seed_path))
    seed.close()
    disk = vm / "system.qcow2"
    subprocess.run([str(args.qemu / "qemu-img.exe"), "create", "-f", "qcow2", str(disk), "80G"], check=True)
    nvram = vm / "uefi-vars.fd"
    shutil.copyfile(args.qemu / "share/edk2-i386-vars.fd", nvram)
    arguments = ["-name", "forge-gate7-windows", "-machine", "q35", "-accel", "whpx", "-smp", "4", "-m", "8192",
                 "-cpu", "qemu64,+sse4.1,+sse4.2,+popcnt,+cx16,+lahf-lm",
                 "-drive", f"file={args.qemu / 'share/edk2-x86_64-code.fd'},if=pflash,format=raw,readonly=on",
                 "-drive", f"file={nvram},if=pflash,format=raw",
                 "-drive", f"file={disk},if=ide,format=qcow2", "-drive", f"file={iso},media=cdrom,readonly=on",
                 "-drive", f"file={seed_path},media=cdrom,readonly=on",
                 "-netdev", "user,id=n0,hostfwd=tcp:127.0.0.1:2237-:22", "-device", "e1000e,netdev=n0",
                 "-qmp", "tcp:127.0.0.1:4437,server=on,wait=off", "-display", "none", "-boot", "once=d,order=c"]
    with (vm / "launch.json").open("x", encoding="utf-8") as stream:
        json.dump({"schema":"forge.gate7.local-vm.v1", "executable":str(args.qemu / "qemu-system-x86_64.exe"),
                   "arguments":arguments,"memory_mib":8192,"vcpus":4,"disk_max_gib":80,
                   "ssh_address":"127.0.0.1","ssh_port":2237,"ssh_key":str(key),"user":"forge",
                   "host_filesystem_shared":False,"os_installation_qualified":False,
                   "tpm_emulator_configured":False,"windows_hardware_checks_bypassed":False}, stream, indent=2)
    print(json.dumps({"launch":str(vm / "launch.json"),"started":False,"secrets_printed":False}))


if __name__ == "__main__":
    main()
