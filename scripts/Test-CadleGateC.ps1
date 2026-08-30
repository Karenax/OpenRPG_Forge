[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$RequireCheckout
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = [IO.Path]::GetFullPath($RepositoryRoot)
$actualRoot = [IO.Path]::GetFullPath((& git -C $root rev-parse --show-toplevel 2>&1 | Out-String).Trim())
if ($LASTEXITCODE -ne 0 -or $actualRoot -ne $root) {
    throw "RepositoryRoot is not the Forge checkout root: $root"
}

$lockPath = Join-Path $root "upstream/cadle.lock.json"
$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
if (
    $lock.schemaVersion -ne 1 -or
    $lock.repository.commit -notmatch "^[0-9a-f]{40}$" -or
    $lock.repository.tree -notmatch "^[0-9a-f]{40}$"
) {
    throw "Invalid Cadle lockfile."
}

& git -C $root check-ignore --quiet "upstream/cadle/.gate-c-ignore-probe"
if ($LASTEXITCODE -ne 0) {
    throw "upstream/cadle/ is not ignored."
}
& git -C $root check-ignore --quiet "artifacts/cadle-baseline/.gate-c-ignore-probe"
if ($LASTEXITCODE -ne 0) {
    throw "Local Cadle baseline artifacts are not ignored."
}

$tracked = @(& git -C $root ls-files)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to enumerate tracked files."
}

$allowedUpstream = @("upstream/README.md", "upstream/cadle.lock.json")
$unexpectedUpstream = @($tracked | Where-Object {
    $_ -like "upstream/*" -and $_ -notin $allowedUpstream
})
if ($unexpectedUpstream.Count -gt 0) {
    throw "Unexpected tracked upstream files:`n$($unexpectedUpstream -join [Environment]::NewLine)"
}

$forbiddenCadleMedia = @($tracked | Where-Object {
    $_ -match "(?i)cadle.*\.(png|jpe?g|webp|gif|bmp|tiff?|glb|gltf|bin|fbx|obj|wav|ogg|mp3|flac|mp4|webm)$"
})
if ($forbiddenCadleMedia.Count -gt 0) {
    throw "Cadle media must remain local and untracked:`n$($forbiddenCadleMedia -join [Environment]::NewLine)"
}

$productionManifests = @($tracked | Where-Object {
    $_ -match "^(apps|crates|tools)/.*(Cargo\.toml|package\.json)$" -or $_ -eq "Cargo.toml"
})
foreach ($relative in $productionManifests) {
    $content = Get-Content -LiteralPath (Join-Path $root $relative) -Raw
    if ($content -match "(?i)upstream[/\\]cadle|three(?:\.js)?|postprocessing|vite") {
        throw "Production manifest '$relative' depends on the Cadle/Three/Vite reference stack."
    }
}

$checkoutPath = Join-Path $root (([string]$lock.checkout.path).Replace("/", [IO.Path]::DirectorySeparatorChar))
if (Test-Path -LiteralPath $checkoutPath) {
    & (Join-Path $root "scripts/Get-Cadle.ps1") -RepositoryRoot $root -VerifyOnly | Out-Host
} elseif ($RequireCheckout) {
    throw "Cadle checkout is required but absent: $checkoutPath"
}

Write-Output "CADLE_GATE_C_BOUNDARY_PASS tracked=$($tracked.Count) checkout_present=$(Test-Path -LiteralPath $checkoutPath)"
