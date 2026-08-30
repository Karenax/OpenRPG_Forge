[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$VerifyOnly
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-GitText {
    param(
        [Parameter(Mandatory)] [string]$WorkingDirectory,
        [Parameter(Mandatory)] [string[]]$Arguments
    )

    $lines = @(& git -C $WorkingDirectory @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        $rendered = [string]::Join([Environment]::NewLine, $lines)
        throw "git $($Arguments -join ' ') failed in '$WorkingDirectory':`n$rendered"
    }

    return ([string]::Join([Environment]::NewLine, $lines)).Trim()
}

function Assert-RepositoryRoot {
    param([Parameter(Mandatory)] [string]$Path)

    $resolved = [IO.Path]::GetFullPath($Path)
    $actual = Invoke-GitText -WorkingDirectory $resolved -Arguments @("rev-parse", "--show-toplevel")
    $actual = [IO.Path]::GetFullPath($actual)
    if ($actual -ne $resolved) {
        throw "RepositoryRoot must be the Forge checkout root. Expected '$resolved', Git reported '$actual'."
    }

    return $resolved
}

$root = Assert-RepositoryRoot -Path $RepositoryRoot
$lockPath = Join-Path $root "upstream/cadle.lock.json"
if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
    throw "Missing Cadle lockfile: $lockPath"
}

$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
if ($lock.schemaVersion -notin @(1, 2)) {
    throw "Unsupported Cadle lock schemaVersion '$($lock.schemaVersion)'."
}

$expectedUrl = [string]$lock.repository.url
$expectedCommit = [string]$lock.repository.commit
$expectedTree = [string]$lock.repository.tree
$checkoutRelative = ([string]$lock.checkout.path).Replace("/", [IO.Path]::DirectorySeparatorChar)
if ($expectedCommit -notmatch "^[0-9a-f]{40}$") {
    throw "The locked Cadle commit must be a full lowercase SHA-1."
}
if ($expectedTree -notmatch "^[0-9a-f]{40}$") {
    throw "The locked Cadle tree must be a full lowercase SHA-1."
}

$target = [IO.Path]::GetFullPath((Join-Path $root $checkoutRelative))
$rootPrefix = $root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $target.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -or $target -eq $root) {
    throw "The Cadle checkout escapes the Forge repository: $target"
}

if (-not (Test-Path -LiteralPath $target)) {
    if ($VerifyOnly) {
        throw "Cadle checkout is absent: $target"
    }

    New-Item -ItemType Directory -Path $target | Out-Null
    Invoke-GitText -WorkingDirectory $target -Arguments @("init", "--quiet") | Out-Null
    Invoke-GitText -WorkingDirectory $target -Arguments @("remote", "add", "origin", $expectedUrl) | Out-Null
}

if (-not (Test-Path -LiteralPath (Join-Path $target ".git"))) {
    throw "Cadle target exists but is not a standalone Git checkout: $target"
}

$actualUrl = Invoke-GitText -WorkingDirectory $target -Arguments @("remote", "get-url", "origin")
if ($actualUrl -ne $expectedUrl) {
    throw "Cadle remote substitution detected. Expected '$expectedUrl', found '$actualUrl'."
}

$dirtyBefore = Invoke-GitText -WorkingDirectory $target -Arguments @("status", "--porcelain")
if ($dirtyBefore) {
    throw "Cadle checkout is dirty. Gate C refuses to overwrite it:`n$dirtyBefore"
}

if (-not $VerifyOnly) {
    Invoke-GitText -WorkingDirectory $target -Arguments @(
        "fetch", "--no-tags", "--depth=1", "origin", $expectedCommit
    ) | Out-Null
    $fetchedCommit = Invoke-GitText -WorkingDirectory $target -Arguments @("rev-parse", "FETCH_HEAD")
    if ($fetchedCommit -ne $expectedCommit) {
        throw "Fetched Cadle commit '$fetchedCommit' does not match lock '$expectedCommit'."
    }

    $currentLines = @(& git -C $target rev-parse HEAD 2>$null)
    $currentCommit = if ($LASTEXITCODE -eq 0) {
        ([string]::Join([Environment]::NewLine, $currentLines)).Trim()
    } else {
        ""
    }
    if ($currentCommit -ne $expectedCommit) {
        Invoke-GitText -WorkingDirectory $target -Arguments @("switch", "--detach", $expectedCommit) | Out-Null
    }
}

$head = Invoke-GitText -WorkingDirectory $target -Arguments @("rev-parse", "HEAD")
if ($head -ne $expectedCommit) {
    throw "Cadle HEAD '$head' does not match lock '$expectedCommit'."
}

& git -C $target symbolic-ref --quiet HEAD 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) {
    throw "Cadle must use a detached HEAD."
}

$dirtyAfter = Invoke-GitText -WorkingDirectory $target -Arguments @("status", "--porcelain")
if ($dirtyAfter) {
    throw "Cadle checkout changed during acquisition:`n$dirtyAfter"
}

$tree = Invoke-GitText -WorkingDirectory $target -Arguments @("rev-parse", "HEAD^{tree}")
if ($tree -ne $expectedTree) {
    throw "Cadle tree '$tree' does not match lock '$expectedTree'."
}
Write-Output "CADLE_ACQUISITION_PASS commit=$head tree=$tree path=$target"
