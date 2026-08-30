[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory)] [string]$QualificationRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-Checked {
    param(
        [Parameter(Mandatory)] [string]$FilePath,
        [Parameter(Mandatory)] [string[]]$Arguments
    )
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$root = [IO.Path]::GetFullPath($RepositoryRoot)
$qualification = [IO.Path]::GetFullPath($QualificationRoot)
$lock = Get-Content -LiteralPath (Join-Path $root "upstream/cadle.lock.json") -Raw | ConvertFrom-Json
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $root "artifacts/cadle-gate-c"))
$allowedPrefix = $allowedRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $qualification.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "QualificationRoot must be below '$allowedRoot': $qualification"
}

& (Join-Path $root "scripts/Get-Cadle.ps1") -RepositoryRoot $root -VerifyOnly | Out-Null
$head = (& git -C $qualification rev-parse HEAD).Trim()
$tree = (& git -C $qualification rev-parse "HEAD^{tree}").Trim()
if ($LASTEXITCODE -ne 0 -or $head -ne [string]$lock.repository.commit -or $tree -ne [string]$lock.repository.tree) {
    throw "Qualification clone is not the locked Cadle snapshot."
}
$delta = [string]::Join("`n", @(& git -C $qualification status --porcelain --untracked-files=no)).TrimEnd()
if ($LASTEXITCODE -ne 0 -or $delta -ne " M package-lock.json") {
    throw "Qualification clone must differ only by package-lock.json:`n$delta"
}
$overlayHash = (Get-FileHash -LiteralPath (Join-Path $qualification "package-lock.json") -Algorithm SHA256).Hash.ToLowerInvariant()
if ($overlayHash -ne [string]$lock.qualification.overlaySha256) {
    throw "Qualification package-lock hash mismatch: $overlayHash"
}

$nodeVersion = (& node --version).TrimStart("v")
$npmVersion = (& npm --version).Trim()
if ($nodeVersion -ne [string]$lock.runtime.nodeVersion -or $npmVersion -ne [string]$lock.runtime.npmVersion) {
    throw "Qualification toolchain mismatch: Node $nodeVersion / npm $npmVersion"
}

Push-Location $qualification
try {
    Invoke-Checked -FilePath "npm" -Arguments @("ci", "--ignore-scripts", "--no-audit", "--no-fund")
    Invoke-Checked -FilePath "npm" -Arguments @("run", "build")
    Invoke-Checked -FilePath "node" -Arguments @("tools/invariants.mjs")
    Invoke-Checked -FilePath "node" -Arguments @("tools/check.mjs")
    Invoke-Checked -FilePath "node" -Arguments @("tools/curvecheck.mjs")
    Invoke-Checked -FilePath "node" -Arguments @("tools/buildcheck.mjs")
} finally {
    Pop-Location
}

$afterHash = (Get-FileHash -LiteralPath (Join-Path $qualification "package-lock.json") -Algorithm SHA256).Hash.ToLowerInvariant()
$afterDelta = [string]::Join("`n", @(& git -C $qualification status --porcelain --untracked-files=no)).TrimEnd()
if ($afterHash -ne [string]$lock.qualification.overlaySha256 -or $afterDelta -ne " M package-lock.json") {
    throw "Cadle qualification changed tracked evidence unexpectedly."
}
& (Join-Path $root "scripts/Get-Cadle.ps1") -RepositoryRoot $root -VerifyOnly | Out-Null
Write-Output "CADLE_QUALIFICATION_PASS commit=$head overlay=$afterHash node=$nodeVersion npm=$npmVersion"
