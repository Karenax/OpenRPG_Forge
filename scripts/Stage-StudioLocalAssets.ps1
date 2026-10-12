#Requires -Version 7.2
# Stages authenticated static inputs only; never reuses a historical frontend.
[CmdletBinding()]
param(
    [string]$Output = $env:TRUNK_STAGING_DIR,
    [string]$AssetRoot = $env:FORGE_STUDIO_LOCAL_ASSETS,
    [string]$ExpectedManifestSha256
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path -Parent $PSScriptRoot
if (-not $AssetRoot) {
    $AssetRoot = Join-Path (Split-Path -Parent $repository) '.forge-local/studio-assets'
}
if (-not $Output) { throw 'Supply Output or run as a Trunk hook with TRUNK_STAGING_DIR.' }
$assetDirectory = [IO.Path]::GetFullPath($AssetRoot)
$outputDirectory = [IO.Path]::GetFullPath($Output)
$manifestName = 'frontend-static-inputs.json'
$receiptPath = Join-Path $outputDirectory 'forge-studio-local-assets-manifest.json'

function Assert-PhysicalPath([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Reparse point is not allowed: $current"
            }
        }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}

function Get-ClosedInventory([string]$Root, [string[]]$Children) {
    Assert-PhysicalPath $Root
    $files = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $pending = [Collections.Generic.Stack[IO.FileSystemInfo]]::new()
    $entries = if ($Children) {
        @($Children | ForEach-Object { Get-Item -LiteralPath (Join-Path $Root $_) -Force })
    } else {
        @(Get-ChildItem -LiteralPath $Root -Force)
    }
    foreach ($entry in $entries) { $pending.Push($entry) }
    while ($pending.Count) {
        $entry = $pending.Pop()
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Reparse point is not allowed: $($entry.FullName)"
        }
        $relative = [IO.Path]::GetRelativePath($Root, $entry.FullName).Replace('\', '/')
        if (-not $paths.Add($relative)) { throw "Duplicate inventory entry: $relative" }
        if ($entry -is [IO.DirectoryInfo]) {
            foreach ($child in @(Get-ChildItem -LiteralPath $entry.FullName -Force)) { $pending.Push($child) }
        } else {
            $files.Add($relative, (Get-FileHash -LiteralPath $entry.FullName -Algorithm SHA256).Hash.ToLowerInvariant())
        }
    }
    return [pscustomobject]@{ files = $files; paths = $paths }
}

function Assert-Inventory($Inventory, $Pins, $AllowedPaths, [string]$ManifestSha256) {
    $expectedFiles = $Pins.Count
    if ($ManifestSha256) { $expectedFiles++ }
    if ($Inventory.files.Count -ne $expectedFiles -or -not $Inventory.paths.SetEquals($AllowedPaths)) {
        throw 'Static asset inventory is incomplete or contains unexpected entries.'
    }
    foreach ($path in $Pins.Keys) {
        if (-not $Inventory.files.ContainsKey($path) -or $Inventory.files[$path] -cne $Pins[$path]) {
            throw "Static asset SHA-256 mismatch: $path"
        }
    }
    if ($ManifestSha256 -and $Inventory.files[$manifestName] -cne $ManifestSha256) {
        throw 'Static asset manifest changed during staging.'
    }
}

Assert-PhysicalPath $outputDirectory
Assert-PhysicalPath $assetDirectory
if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) { throw 'Output must be an existing Trunk staging directory.' }
if ($assetDirectory -eq $outputDirectory -or
    $assetDirectory.StartsWith($outputDirectory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    $outputDirectory.StartsWith($assetDirectory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Static asset input and output directories must be separate.'
}
foreach ($name in @('forge-imports', 'forge-kit', 'forge-studio-local-assets-manifest.json')) {
    if (Test-Path -LiteralPath (Join-Path $outputDirectory $name)) { throw "Refusing to overwrite existing staged output: $name" }
}
$receipt = [ordered]@{
    schema = 'forge.studio.local-assets.v1'; status = 'LOCAL_ASSETS_ABSENT'
    recorded_at_utc = [DateTime]::UtcNow.ToString('o'); build_only = $true
    runtime_verified = $false; gate_qualified = $false; assets_present = $false
    asset_root = $assetDirectory; source_manifest_sha256 = $null
    source_unchanged_during_staging = $null; destination_verified = $false
    import_files = 0; kit_files = 0; files = @{}
    orchestrator_sha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash.ToLowerInvariant()
}
if (-not (Test-Path -LiteralPath $assetDirectory)) {
    if ($ExpectedManifestSha256) { throw 'An expected manifest was supplied but the local assets are absent.' }
    $receipt | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $receiptPath -Encoding utf8
    Write-Host 'LOCAL_ASSETS_ABSENT: public frontend build contains no forge-imports or forge-kit.'
    return
}
if (-not (Test-Path -LiteralPath $assetDirectory -PathType Container)) { throw 'Static asset root must be a directory.' }
foreach ($name in @('forge-imports', 'forge-kit')) {
    if (-not (Test-Path -LiteralPath (Join-Path $assetDirectory $name) -PathType Container)) { throw "Static asset directory is missing: $name" }
}
$manifestPath = Join-Path $assetDirectory $manifestName
Assert-PhysicalPath $manifestPath
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Required static asset manifest is missing: $manifestName" }
$manifestBytes = [IO.File]::ReadAllBytes($manifestPath)
$manifestSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($manifestBytes)).ToLowerInvariant()
if ($ExpectedManifestSha256 -and $manifestSha256 -cne $ExpectedManifestSha256.ToLowerInvariant()) { throw 'Static asset manifest SHA-256 mismatch.' }
$manifest = [Text.Encoding]::UTF8.GetString($manifestBytes) | ConvertFrom-Json -AsHashtable
if (-not ($manifest -is [Collections.IDictionary]) -or -not $manifest.Contains('files') -or
    -not ($manifest.files -is [Collections.IDictionary])) { throw 'Static asset manifest must contain a files map of paths to SHA-256 values.' }
$pins = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
$allowedPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($path in $manifest.files.Keys) {
    if ($path -cnotmatch '^forge-(imports|kit)/' -or $path -match '[\\:*?"<>|]' -or
        @($path.Split('/') | Where-Object { $_ -in @('', '.', '..') -or $_.EndsWith('.') -or $_.EndsWith(' ') }).Count) {
        throw "Invalid static asset path: $path"
    }
    $pin = $manifest.files[$path]
    if (-not ($pin -is [string]) -or $pin -notmatch '^[a-fA-F0-9]{64}$') { throw "Invalid SHA-256 pin: $path" }
    $pins.Add($path, $pin.ToLowerInvariant())
    $parts = $path.Split('/')
    for ($index = 1; $index -le $parts.Length; $index++) { $null = $allowedPaths.Add(($parts[0..($index - 1)] -join '/')) }
}
$importCount = @($pins.Keys | Where-Object { $_.StartsWith('forge-imports/', [StringComparison]::Ordinal) }).Count
$kitCount = @($pins.Keys | Where-Object { $_.StartsWith('forge-kit/', [StringComparison]::Ordinal) }).Count
if (-not $importCount -or -not $kitCount) { throw 'The manifest must pin both forge-imports and forge-kit.' }
$null = $allowedPaths.Add($manifestName)
Assert-Inventory (Get-ClosedInventory $assetDirectory) $pins $allowedPaths $manifestSha256
foreach ($path in @($pins.Keys | Sort-Object -CaseSensitive)) {
    $destination = Join-Path $outputDirectory $path
    $parent = [IO.Path]::GetDirectoryName($destination)
    Assert-PhysicalPath $parent
    $null = [IO.Directory]::CreateDirectory($parent)
    Assert-PhysicalPath $parent
    [IO.File]::Copy((Join-Path $assetDirectory $path), $destination, $false)
}
Assert-Inventory (Get-ClosedInventory $assetDirectory) $pins $allowedPaths $manifestSha256
$null = $allowedPaths.Remove($manifestName)
Assert-Inventory (Get-ClosedInventory $outputDirectory @('forge-imports', 'forge-kit')) $pins $allowedPaths ''
$receipt.status = 'STAGED'
$receipt.assets_present = $true
$receipt.source_manifest_sha256 = $manifestSha256
$receipt.source_unchanged_during_staging = $true
$receipt.destination_verified = $true
$receipt.import_files = $importCount
$receipt.kit_files = $kitCount
$receipt.files = $pins
$receipt | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $receiptPath -Encoding utf8
Write-Host "STAGED: $importCount forge-imports files and $kitCount forge-kit files, SHA-256 verified."
