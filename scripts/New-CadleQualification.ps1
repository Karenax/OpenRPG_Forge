[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory)] [string]$Destination
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-Sha256 {
    param([Parameter(Mandatory)] [string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Invoke-GitText {
    param(
        [Parameter(Mandatory)] [string]$WorkingDirectory,
        [Parameter(Mandatory)] [string[]]$Arguments
    )

    $lines = @(& git -C $WorkingDirectory @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed in '$WorkingDirectory':`n$([string]::Join([Environment]::NewLine, $lines))"
    }
    return ([string]::Join([Environment]::NewLine, $lines)).Trim()
}

$root = [IO.Path]::GetFullPath($RepositoryRoot)
$actualRoot = [IO.Path]::GetFullPath((Invoke-GitText -WorkingDirectory $root -Arguments @("rev-parse", "--show-toplevel")))
if ($actualRoot -ne $root) {
    throw "RepositoryRoot must be the Forge checkout root: $root"
}

& (Join-Path $root "scripts/Get-Cadle.ps1") -RepositoryRoot $root -VerifyOnly | Out-Null
$lock = Get-Content -LiteralPath (Join-Path $root "upstream/cadle.lock.json") -Raw | ConvertFrom-Json
if ($lock.schemaVersion -ne 2 -or $lock.qualification.mode -ne "forge-derived-package-lock-overlay") {
    throw "Cadle qualification requires lock schema 2 and the reviewed Forge overlay mode."
}

$destinationRoot = [IO.Path]::GetFullPath($Destination)
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $root "artifacts/cadle-gate-c"))
$allowedPrefix = $allowedRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $destinationRoot.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Destination must be a new directory below '$allowedRoot': $destinationRoot"
}
if (Test-Path -LiteralPath $destinationRoot) {
    throw "Qualification destination already exists; refusing to overwrite it: $destinationRoot"
}

$canonical = Join-Path $root (([string]$lock.checkout.path).Replace("/", [IO.Path]::DirectorySeparatorChar))
$parent = Split-Path -Parent $destinationRoot
if (-not (Test-Path -LiteralPath $parent)) {
    New-Item -ItemType Directory -Path $parent | Out-Null
}

& git clone --quiet --no-local --no-hardlinks --no-checkout $canonical $destinationRoot
if ($LASTEXITCODE -ne 0) {
    throw "Unable to create the isolated Cadle qualification clone."
}
Invoke-GitText -WorkingDirectory $destinationRoot -Arguments @("checkout", "--quiet", "--detach", [string]$lock.repository.commit) | Out-Null

$head = Invoke-GitText -WorkingDirectory $destinationRoot -Arguments @("rev-parse", "HEAD")
$tree = Invoke-GitText -WorkingDirectory $destinationRoot -Arguments @("rev-parse", "HEAD^{tree}")
if ($head -ne [string]$lock.repository.commit -or $tree -ne [string]$lock.repository.tree) {
    throw "Qualification clone identity mismatch: commit=$head tree=$tree"
}

$expectedHashes = [ordered]@{
    "package.json" = [string]$lock.qualification.manifestSha256
    "package-lock.json" = [string]$lock.qualification.upstreamPackageLockSha256
    "LICENSE" = [string]$lock.qualification.licenseSha256
    "NOTICE" = [string]$lock.qualification.noticeSha256
}
foreach ($entry in $expectedHashes.GetEnumerator()) {
    $actual = Get-Sha256 -Path (Join-Path $destinationRoot $entry.Key)
    if ($actual -ne $entry.Value) {
        throw "Cadle source evidence hash mismatch for '$($entry.Key)': $actual"
    }
}

$overlay = Join-Path $root (([string]$lock.qualification.overlayPath).Replace("/", [IO.Path]::DirectorySeparatorChar))
if ((Get-Sha256 -Path $overlay) -ne [string]$lock.qualification.overlaySha256) {
    throw "Tracked Cadle qualification overlay hash mismatch."
}
Copy-Item -LiteralPath $overlay -Destination (Join-Path $destinationRoot "package-lock.json")

$delta = Invoke-GitText -WorkingDirectory $destinationRoot -Arguments @("status", "--porcelain", "--untracked-files=no")
if ($delta -ne "M package-lock.json") {
    throw "Qualification clone has an unexpected tracked delta:`n$delta"
}
if ((Get-Sha256 -Path (Join-Path $destinationRoot "package-lock.json")) -ne [string]$lock.qualification.overlaySha256) {
    throw "Qualification overlay changed while being applied."
}

& (Join-Path $root "scripts/Get-Cadle.ps1") -RepositoryRoot $root -VerifyOnly | Out-Null
Write-Output "CADLE_QUALIFICATION_WORKSPACE_PASS commit=$head tree=$tree overlay=$($lock.qualification.overlaySha256) path=$destinationRoot"
