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
    $lock.schemaVersion -notin @(1, 2) -or
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

$allowedUpstream = @("upstream/README.md", "upstream/cadle.lock.json", "upstream/cadle.npm-lock.json")
$unexpectedUpstream = @($tracked | Where-Object {
    $_ -like "upstream/*" -and $_ -notin $allowedUpstream
})
if ($unexpectedUpstream.Count -gt 0) {
    throw "Unexpected tracked upstream files:`n$($unexpectedUpstream -join [Environment]::NewLine)"
}

if ($lock.schemaVersion -eq 2) {
    $overlayRelative = [string]$lock.qualification.overlayPath
    if ($overlayRelative -ne "upstream/cadle.npm-lock.json") {
        throw "Unexpected Cadle qualification overlay path: $overlayRelative"
    }
    $overlayAbsolute = Join-Path $root ($overlayRelative.Replace("/", [IO.Path]::DirectorySeparatorChar))
    if (-not (Test-Path -LiteralPath $overlayAbsolute -PathType Leaf)) {
        throw "Missing Cadle qualification overlay: $overlayAbsolute"
    }
    $overlayHash = (Get-FileHash -LiteralPath $overlayAbsolute -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($overlayHash -ne [string]$lock.qualification.overlaySha256) {
        throw "Cadle qualification overlay hash mismatch: $overlayHash"
    }
}

$mediaPattern = "(?i)\.(png|jpe?g|webp|gif|bmp|tiff?|svg|glb|gltf|bin|fbx|obj|wav|ogg|mp3|flac|mp4|webm)$"
$forbiddenCadleMedia = @($tracked | Where-Object {
    $_ -match "(?i)cadle.*$mediaPattern"
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

# Documentation and evidence may identify local Cadle references by path and hash for in-place review;
# they are not executable distribution configuration and carry no Cadle bytes. Keep the broad scan for
# actual CI, packaging and script surfaces outside those non-executable trees.
$distributionConfigs = @($tracked | Where-Object {
    $_ -notmatch "(?i)^(docs|examples)/" -and (
        $_ -match "(?i)^(\.github/|\.gitlab-ci|azure-pipelines|buildkite|Dockerfile|docker-compose|packaging/|installer/|release/|distribution/)" -or
        $_ -match "(?i)(package|publish|release|cache|artifact).*(\.ps1|\.sh|\.ya?ml|\.json|\.toml)$"
    )
})
foreach ($relative in $distributionConfigs) {
    $content = Get-Content -LiteralPath (Join-Path $root $relative) -Raw
    if ($content -match "(?i)upstream[/\\]cadle|cadle-baseline|tools[/\\]out") {
        throw "Distribution or CI configuration '$relative' references local Cadle material."
    }
}

$checkoutPath = Join-Path $root (([string]$lock.checkout.path).Replace("/", [IO.Path]::DirectorySeparatorChar))
if (Test-Path -LiteralPath $checkoutPath) {
    & (Join-Path $root "scripts/Get-Cadle.ps1") -RepositoryRoot $root -VerifyOnly | Out-Host

    # Detect byte-identical media copied under a different filename. The comparison is local-only and
    # stores no Cadle bytes or hashes in Git; it complements the path/configuration checks above.
    $cadleMediaHashes = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($relative in @(& git -C $checkoutPath ls-files | Where-Object { $_ -match $mediaPattern })) {
        $absolute = Join-Path $checkoutPath $relative
        if (Test-Path -LiteralPath $absolute -PathType Leaf) {
            [void]$cadleMediaHashes.Add((Get-FileHash -LiteralPath $absolute -Algorithm SHA256).Hash)
        }
    }
    foreach ($relative in @($tracked | Where-Object { $_ -match $mediaPattern })) {
        $absolute = Join-Path $root $relative
        if (
            (Test-Path -LiteralPath $absolute -PathType Leaf) -and
            $cadleMediaHashes.Contains((Get-FileHash -LiteralPath $absolute -Algorithm SHA256).Hash)
        ) {
            throw "Tracked media '$relative' is byte-identical to Cadle media."
        }
    }
} elseif ($RequireCheckout) {
    throw "Cadle checkout is required but absent: $checkoutPath"
}

Write-Output "CADLE_GATE_C_BOUNDARY_PASS tracked=$($tracked.Count) checkout_present=$(Test-Path -LiteralPath $checkoutPath)"
