#Requires -Version 7.2
# Isolated qualification toolchain, not the workstation's default Blender.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Destination,
    [switch]$VerifyExisting
)
$ErrorActionPreference = 'Stop'
$version = '4.5.10'
$archiveName = "blender-$version-windows-x64.zip"
$archiveHash = 'ef6d846b8015f47ade6df3f9322ce17419080a5d922fa562b6c966064fe30dce'
$sourceUrl = "https://download.blender.org/release/Blender4.5/$archiveName"
$root = [IO.Path]::GetFullPath($Destination).TrimEnd([char[]]'\/')
if ((Test-Path -LiteralPath $root) -and -not $VerifyExisting) {
    throw 'Destination already exists; refusing overwrite (use -VerifyExisting to verify only)'
}
if ([IO.Path]::GetPathRoot($root) -ne 'E:\') { throw 'Qualification tools must stay on E:' }
$archive = Join-Path $root $archiveName
if (-not $VerifyExisting) {
    $null = New-Item -ItemType Directory -Path $root
    Invoke-WebRequest -Uri $sourceUrl -OutFile $archive -TimeoutSec 600
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $archiveHash) {
    throw 'Blender archive SHA-256 mismatch; archive retained, nothing extracted or executed'
}

# Validate all members before extraction; a trusted hash does not relax path boundaries.
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    $expandedBytes = 0L
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $zip.Entries) {
        $relative = $entry.FullName.Replace('/', '\')
        $target = [IO.Path]::GetFullPath((Join-Path $root $relative))
        if ([IO.Path]::IsPathRooted($relative) -or $relative.Contains(':') -or
            -not $target.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase) -or
            -not $paths.Add($target)) { throw 'Unsafe or duplicate archive member' }
        if ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) {
            throw 'Archive symlink refused'
        }
        $expandedBytes += $entry.Length
        if ($expandedBytes -gt 4GB) { throw 'Archive expansion exceeds 4 GiB budget' }
        if ($VerifyExisting -and $entry.Name) {
            if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "Missing tool file: $target" }
            $stream = $entry.Open()
            try {
                $expected = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))
            } finally { $stream.Dispose() }
            if ((Get-FileHash -LiteralPath $target).Hash -ne $expected) {
                throw "Extracted tool file differs from pinned archive: $target"
            }
        }
    }
} finally { $zip.Dispose() }
if (-not $VerifyExisting) { [IO.Compression.ZipFile]::ExtractToDirectory($archive, $root) }
$executable = Join-Path $root "blender-$version-windows-x64/blender.exe"
$reported = & $executable --background --factory-startup --disable-autoexec --version
if ($LASTEXITCODE -ne 0 -or $reported[0] -notmatch '^Blender 4\.5\.10 LTS(?: \(hash [0-9a-f]+ built [0-9 :\-]+\))?$') {
    throw "Unexpected Blender version: $($reported[0])"
}
[ordered]@{
    schema = 'forge.blender-qualification-toolchain'
    schema_version = 1
    source_url = $sourceUrl
    checksum_source_url = 'https://download.blender.org/release/Blender4.5/blender-4.5.10.sha256'
    archive_sha256 = $archiveHash
    executable = $executable
    executable_sha256 = (Get-FileHash -LiteralPath $executable).Hash.ToLowerInvariant()
    version_output = $reported
    expanded_bytes = $expandedBytes
    default_installation_modified = $false
    worker_qualified = $false
} | ConvertTo-Json -Depth 4 | Write-Output
Write-Output "BLENDER_PORTABLE_ACQUISITION_PASS executable=$executable"
