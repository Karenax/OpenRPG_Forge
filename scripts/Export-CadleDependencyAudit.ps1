[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputPath = "docs/evidence/cadle-dependency-audit.json"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-PropertyNames {
    param([object]$Value)
    if ($null -eq $Value) {
        return @()
    }
    if ($Value -is [Collections.IDictionary]) {
        return @($Value.Keys | Sort-Object -Unique)
    }
    return @($Value.PSObject.Properties.Name | Sort-Object -Unique)
}

function Get-LicenseText {
    param([object]$Value)
    if ($null -eq $Value) {
        return "UNKNOWN"
    }
    if ($Value -is [string]) {
        return $Value
    }
    return (($Value | ConvertTo-Json -Compress) -replace "\s+", " ")
}

$root = [IO.Path]::GetFullPath($RepositoryRoot)
& (Join-Path $root "scripts/Get-Cadle.ps1") -RepositoryRoot $root -VerifyOnly | Out-Null

$lock = Get-Content -LiteralPath (Join-Path $root "upstream/cadle.lock.json") -Raw | ConvertFrom-Json
$cadle = Join-Path $root (([string]$lock.checkout.path).Replace("/", [IO.Path]::DirectorySeparatorChar))
$packagePath = Join-Path $cadle "package.json"
$packageLockPath = Join-Path $cadle "package-lock.json"
$package = Get-Content -LiteralPath $packagePath -Raw | ConvertFrom-Json
$packageLock = Get-Content -LiteralPath $packageLockPath -Raw | ConvertFrom-Json -AsHashtable
$lockRoot = $packageLock["packages"][""]

$declaredRuntime = Get-PropertyNames $package.dependencies
$declaredDevelopment = Get-PropertyNames $package.devDependencies
$lockedRuntime = Get-PropertyNames $lockRoot["dependencies"]
$lockedDevelopment = Get-PropertyNames $lockRoot["devDependencies"]
$declaredAll = @($declaredRuntime + $declaredDevelopment | Sort-Object -Unique)
$lockedAll = @($lockedRuntime + $lockedDevelopment | Sort-Object -Unique)
$missingFromLock = @($declaredAll | Where-Object { $_ -notin $lockedAll })
$extraInLock = @($lockedAll | Where-Object { $_ -notin $declaredAll })

$packages = @()
foreach ($property in $packageLock["packages"].GetEnumerator() | Sort-Object Key) {
    if (-not $property.Key) {
        continue
    }
    $entry = $property.Value
    $packages += [ordered]@{
        path = $property.Key
        version = [string]$entry["version"]
        license = Get-LicenseText $entry["license"]
        dev = [bool]$entry["dev"]
        optional = [bool]$entry["optional"]
    }
}

$licenseCounts = [ordered]@{}
foreach ($packageEntry in $packages) {
    $license = [string]$packageEntry["license"]
    if (-not $licenseCounts.Contains($license)) {
        $licenseCounts[$license] = 0
    }
    $licenseCounts[$license] = [int]$licenseCounts[$license] + 1
}

$riskPackages = @($packages | Where-Object {
    $_.license -eq "UNKNOWN" -or $_.license -match "(?i)(GPL|AGPL|LGPL|MPL|EPL|CDDL)"
})

$gitStatus = [string]::Join("`n", @(& git -C $cadle status --porcelain)).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Unable to read Cadle Git status."
}

$evidence = [ordered]@{
    schemaVersion = 1
    source = [ordered]@{
        repository = [string]$lock.repository.url
        commit = (& git -C $cadle rev-parse HEAD).Trim()
        tree = (& git -C $cadle rev-parse "HEAD^{tree}").Trim()
        clean = -not [bool]$gitStatus
    }
    manifests = [ordered]@{
        packageVersion = [string]$package.version
        packageLockRootVersion = [string]$lockRoot["version"]
        lockfileVersion = [int]$packageLock["lockfileVersion"]
        packageSha256 = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
        packageLockSha256 = (Get-FileHash -LiteralPath $packageLockPath -Algorithm SHA256).Hash.ToLowerInvariant()
        licenseSha256 = (Get-FileHash -LiteralPath (Join-Path $cadle "LICENSE") -Algorithm SHA256).Hash.ToLowerInvariant()
        noticeSha256 = (Get-FileHash -LiteralPath (Join-Path $cadle "NOTICE") -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    dependencyConsistency = [ordered]@{
        declaredRuntime = $declaredRuntime
        declaredDevelopment = $declaredDevelopment
        lockedRuntime = $lockedRuntime
        lockedDevelopment = $lockedDevelopment
        missingFromLock = $missingFromLock
        extraInLock = $extraInLock
        npmCiExpectedToSucceed = ($missingFromLock.Count -eq 0 -and $extraInLock.Count -eq 0 -and $package.version -eq $lockRoot["version"])
    }
    licenseCounts = $licenseCounts
    riskPackages = $riskPackages
    packages = $packages
}

$absoluteOutput = [IO.Path]::GetFullPath((Join-Path $root $OutputPath))
$rootPrefix = $root.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $absoluteOutput.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputPath escapes the Forge repository: $absoluteOutput"
}

$outputDirectory = Split-Path -Parent $absoluteOutput
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}
$json = $evidence | ConvertTo-Json -Depth 10
[IO.File]::WriteAllText($absoluteOutput, $json + "`n", [Text.UTF8Encoding]::new($false))

Write-Output "CADLE_DEPENDENCY_AUDIT_WRITTEN path=$absoluteOutput packages=$($packages.Count) risks=$($riskPackages.Count) missing_from_lock=$($missingFromLock.Count)"
