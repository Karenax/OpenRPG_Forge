[CmdletBinding()]
param(
    [string]$DistributionDirectory = 'artifacts/distribution/runtime-windows-x64',
    [switch]$RunSmoke
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not [System.IO.Path]::IsPathRooted($DistributionDirectory)) {
    $DistributionDirectory = Join-Path $repositoryRoot $DistributionDirectory
}

$distributionPath = [System.IO.Path]::GetFullPath($DistributionDirectory)
$manifestPath = Join-Path $distributionPath 'distribution-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Distribution manifest missing: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schema_version -ne 1 -or $manifest.product -ne 'OpenRPG Forge Runtime') {
    throw 'Unsupported runtime distribution manifest.'
}

foreach ($file in $manifest.files) {
    $filePath = Join-Path $distributionPath ([string]$file.path).Replace('/', '\\')
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        throw "Distribution file missing: $($file.path)"
    }

    $actualLength = (Get-Item -LiteralPath $filePath).Length
    if ($actualLength -ne [long]$file.bytes) {
        throw "Distribution size mismatch: $($file.path)"
    }

    $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne [string]$file.sha256) {
        throw "Distribution hash mismatch: $($file.path)"
    }
}

$requiredPaths = @(
    'OpenRPG_Forge_Runtime.exe',
    'OpenRPG_Forge_Runtime.pck',
    'content/crypt-of-ashes/project.json'
)
foreach ($relativePath in $requiredPaths) {
    $candidate = Join-Path $distributionPath $relativePath.Replace('/', '\\')
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Required runtime distribution file missing: $relativePath"
    }
}

$runtimeAssembly = Get-ChildItem -LiteralPath $distributionPath -Recurse -Filter 'Forge.Runtime.dll' -File |
    Select-Object -First 1
if ($null -eq $runtimeAssembly) {
    throw 'Forge.Runtime.dll is missing from the runtime distribution.'
}

& (Join-Path $PSScriptRoot 'Test-DistributionCompliance.ps1') -DistributionDirectory $distributionPath

if ($RunSmoke) {
    $smokeLog = Join-Path $distributionPath 'runtime-verification-smoke.log'
    $consoleExecutable = Join-Path $distributionPath 'OpenRPG_Forge_Runtime.console.exe'
    $smokeExecutable = if (Test-Path -LiteralPath $consoleExecutable -PathType Leaf) {
        $consoleExecutable
    }
    else {
        Join-Path $distributionPath 'OpenRPG_Forge_Runtime.exe'
    }
    $smokeProcess = Start-Process -FilePath $smokeExecutable -ArgumentList @(
        '--headless', '--log-file', "`"$smokeLog`"", '--', '--smoke-test'
    ) -WorkingDirectory $distributionPath -PassThru -Wait -WindowStyle Hidden
    $smokeExitCode = $smokeProcess.ExitCode

    if ($smokeExitCode -ne 0 -or
        -not (Select-String -LiteralPath $smokeLog -SimpleMatch 'OPENRPG_FORGE_RUNTIME_SMOKE_PASS' -Quiet)) {
        throw "The runtime distribution smoke test failed. See $smokeLog"
    }
}

Write-Output "OPENRPG_FORGE_RUNTIME_DISTRIBUTION_VERIFY_PASS files=$($manifest.files.Count) smoke=$RunSmoke source=$($manifest.source_revision)"
