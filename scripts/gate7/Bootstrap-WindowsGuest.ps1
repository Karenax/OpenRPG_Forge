<# Administration tools only, inside the disposable Windows qualification guest. #>
[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^ssh-ed25519 [A-Za-z0-9+/=]+(?: [A-Za-z0-9_-]+)?$')][string]$PublicKey)
$ErrorActionPreference = 'Stop'
if ($env:COMPUTERNAME -ne 'FORGE-G7-WIN') { throw 'Qualification guest required' }
New-Item -ItemType Directory -Force C:/ForgeQualification | Out-Null
Start-Transcript C:/ForgeQualification/bootstrap.log | Out-Null
try {
    $gateGateway = $null
    for ($gateAttempt=0; $gateAttempt -lt 30 -and -not $gateGateway; $gateAttempt++) {
        $gateGateway = Get-NetRoute -AddressFamily IPv4 -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue |
            Where-Object { $_.NextHop -ne '0.0.0.0' } | Sort-Object RouteMetric | Select-Object -First 1 -ExpandProperty NextHop
        if (-not $gateGateway) { Start-Sleep -Seconds 2 }
    }
    if (-not $gateGateway) { throw 'Guest NAT gateway unavailable; no broad SSH firewall rule created' }
    Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0 | Out-Null
    New-Item -ItemType Directory -Force C:/ProgramData/ssh | Out-Null
    $PublicKey | Set-Content C:/ProgramData/ssh/administrators_authorized_keys -Encoding ascii
    & icacls.exe C:/ProgramData/ssh/administrators_authorized_keys /inheritance:r /grant '*S-1-5-32-544:F' '*S-1-5-18:F'
    if ($LASTEXITCODE -ne 0) { throw 'Administrator SSH key ACL failed' }
    New-Item -ItemType Directory -Force (Join-Path $env:USERPROFILE '.ssh') | Out-Null
    $PublicKey | Set-Content (Join-Path $env:USERPROFILE '.ssh/authorized_keys') -Encoding ascii
    $gateDefaultRule = Get-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -ErrorAction SilentlyContinue
    if ($gateDefaultRule) { $gateDefaultRule | Set-NetFirewallRule -RemoteAddress $gateGateway }
    if (-not (Get-NetFirewallRule -Name ForgeQualificationSsh -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -Name ForgeQualificationSsh -DisplayName ForgeQualificationSsh -Direction Inbound -Protocol TCP -LocalPort 22 -Action Allow -RemoteAddress $gateGateway | Out-Null
    }
    Set-Service sshd -StartupType Automatic
    Start-Service sshd
    $gateOs = Get-CimInstance Win32_OperatingSystem
    @{ schema='forge.gate7.windows-guest-bootstrap.v1'; utc=[DateTime]::UtcNow.ToString('o');
        succeeded=$true; computer=$env:COMPUTERNAME; os=$gateOs.Caption; version=$gateOs.Version;
        build=$gateOs.BuildNumber; ssh_remote_address=$gateGateway; development_sdk_installed=$false;
        os_qualification_complete=$false } | ConvertTo-Json | Set-Content C:/ForgeQualification/bootstrap.receipt.json -Encoding UTF8
} catch {
    @{ succeeded=$false; utc=[DateTime]::UtcNow.ToString('o'); error=$_.Exception.Message } | ConvertTo-Json | Set-Content C:/ForgeQualification/bootstrap.receipt.json -Encoding UTF8
    throw
} finally { Stop-Transcript | Out-Null }
