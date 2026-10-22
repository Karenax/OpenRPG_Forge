param(
    [Parameter(Mandatory)][string]$LogName,
    [Parameter(Mandatory)][string[]]$CargoArguments,
    [string]$EvidenceDirectory = 'F:\OpenRPG_Forge\Preuves-et-analyses\actives\gate5\20261019'
)
$ErrorActionPreference = 'Stop'
if ($LogName -notmatch '^[a-z0-9][a-z0-9-]*$') { throw 'Use a simple unique log name' }
$storage = 'E:\OpenRPG_Forge'
if ((Get-Item -LiteralPath (Join-Path $storage '.build') -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'Gate 5 development cache must be physically on E:.'
}
$env:CARGO_TARGET_DIR = Join-Path $storage '.build\cargo-target'
$env:CARGO_HOME = Join-Path $storage '.build\cargo-home'
# Share one E: cache and keep incremental file growth bounded.
$env:CARGO_INCREMENTAL = '0'
$env:TEMP = Join-Path $storage '.build\tmp'
$env:TMP = $env:TEMP
$env:RUSTUP_AUTO_INSTALL = '0'
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
foreach ($directory in @($env:CARGO_TARGET_DIR, $env:CARGO_HOME, $env:TEMP, $evidence)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}
$log = Join-Path $evidence ($LogName + '.log')
if (Test-Path -LiteralPath $log) { throw 'Existing validation log preserved; choose another name' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$started = [DateTime]::UtcNow.ToString('o')
$stream = [IO.File]::Open($log, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite)
$writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false))
$writer.AutoFlush = $true
Push-Location -LiteralPath $repo
try {
    # Flush every native output line so progress and errors can be observed
    # during a long build, while retaining the exact Cargo exit code.
    & cargo @CargoArguments 2>&1 | ForEach-Object { $writer.WriteLine($_.ToString()) }
    $result = $LASTEXITCODE
} finally {
    $writer.Dispose()
    Pop-Location
}
$receipt = [ordered]@{
    started_utc = $started
    finished_utc = [DateTime]::UtcNow.ToString('o')
    repository = $repo
    cargo_arguments = $CargoArguments
    exit_code = $result
    log = $log
    target = $env:CARGO_TARGET_DIR
    cargo_home = $env:CARGO_HOME
    incremental = $false
    rustflags = $env:RUSTFLAGS
    temporary_files = $env:TEMP
}
$receipt | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath ($log + '.receipt.json') -Encoding utf8
$receipt | ConvertTo-Json -Depth 4
Get-Content -LiteralPath $log -Tail 18
exit $result
