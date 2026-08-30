[CmdletBinding()]
param(
    [string]$DistributionDirectory = 'artifacts/distribution/creator-windows-x64',
    [switch]$RunSmoke,
    [int]$SmokeTimeoutSeconds = 240
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
    throw "Creator distribution manifest missing: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schema_version -ne 1 -or $manifest.product -ne 'OpenRPG Forge Creator') {
    throw 'Unsupported Creator distribution manifest.'
}
foreach ($file in $manifest.files) {
    $filePath = Join-Path $distributionPath ([string]$file.path).Replace('/', '\\')
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        throw "Creator distribution file missing: $($file.path)"
    }
    if ((Get-Item -LiteralPath $filePath).Length -ne [long]$file.bytes) {
        throw "Creator distribution size mismatch: $($file.path)"
    }
    $hash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne [string]$file.sha256) {
        throw "Creator distribution hash mismatch: $($file.path)"
    }
}

$launcher = Join-Path $distributionPath 'OpenRPG_Forge_Creator.exe'
& (Join-Path $PSScriptRoot 'Test-DistributionCompliance.ps1') -DistributionDirectory $distributionPath
$launcherVerification = Start-Process -FilePath $launcher -ArgumentList '--verify' -WorkingDirectory $distributionPath `
    -PassThru -Wait -WindowStyle Hidden
if ($launcherVerification.ExitCode -ne 0) {
    throw 'The Creator launcher layout verification failed.'
}

if (-not $manifest.mcp_inventory_verified -or -not $manifest.mcp_smoke_verified -or
    [int]$manifest.mcp_tool_count -ne 275) {
    throw 'The Creator manifest does not prove the complete 275-tool MCP host.'
}
$mcpHost = Join-Path $distributionPath 'mcp/Forge.McpHost.exe'
$mcpWorkspace = Join-Path $distributionPath 'workspace/apps/Forge.Runtime/content/crypt-of-ashes'
& $mcpHost '--tool-inventory'
if ($LASTEXITCODE -ne 0) {
    throw "The distributed MCP inventory failed with exit code $LASTEXITCODE."
}
& $mcpHost '--workspace' $mcpWorkspace '--self-test'
if ($LASTEXITCODE -ne 0) {
    throw "The distributed MCP self-test failed with exit code $LASTEXITCODE."
}

if ($RunSmoke) {
    # The editor writes import and filesystem caches even in headless mode. Exercise a byte-for-byte
    # copy so verification never invalidates the distribution manifest it just checked.
    # Keep the copy root short enough for the deepest governed asset and editor-cache paths.
    $verificationRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/s'))
    $smokeRoot = Join-Path $verificationRoot ([Guid]::NewGuid().ToString('N').Substring(0, 8))
    $smokeDistribution = Join-Path $smokeRoot 'distribution'
    New-Item -ItemType Directory -Path $smokeDistribution -Force | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $distributionPath -Force) {
        Copy-Item -LiteralPath $item.FullName -Destination $smokeDistribution -Recurse -Force
    }
    $log = Join-Path $smokeRoot 'creator-distribution-smoke.log'
    try {
        $engine = Join-Path $smokeDistribution 'engine/OpenRPG_Forge_Editor.exe'
        $toolset = Join-Path $smokeDistribution 'workspace/apps/Forge.Toolset'
        $process = Start-Process -FilePath $engine -ArgumentList @(
            '--headless', '--editor', '--path', "`"$toolset`"", '--log-file', "`"$log`"",
            '--', '--workspace-smoke-test') -WorkingDirectory $smokeDistribution -PassThru -WindowStyle Hidden
        $deadline = [DateTime]::UtcNow.AddSeconds($SmokeTimeoutSeconds)
        $passed = $false
        while ([DateTime]::UtcNow -lt $deadline -and -not $process.HasExited) {
            if (Test-Path -LiteralPath $log) {
                if (Select-String -LiteralPath $log -SimpleMatch 'OPENRPG_FORGE_TOOLSET_T32_SMOKE_PASS' -Quiet) {
                    $passed = $true
                    break
                }
                if (Select-String -LiteralPath $log -Pattern '(?m)^ERROR:|OPENRPG_FORGE_.*_FAILED' -Quiet) {
                    break
                }
            }
            Start-Sleep -Milliseconds 500
            $process.Refresh()
        }
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
            [void]$process.WaitForExit(10000)
        }
        if (-not $passed) { throw "The portable Creator smoke test failed or timed out. See $log" }
        Copy-Item -LiteralPath $log -Destination (Join-Path $verificationRoot 'latest.log') -Force
    }
    finally {
        if (Test-Path -LiteralPath $log -PathType Leaf) {
            Copy-Item -LiteralPath $log -Destination (Join-Path $verificationRoot 'latest.log') -Force
        }
        if (Test-Path -LiteralPath $smokeRoot) {
            $resolvedSmokeRoot = [System.IO.Path]::GetFullPath($smokeRoot)
            $verificationPrefix = $verificationRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
                [System.IO.Path]::DirectorySeparatorChar
            if (-not $resolvedSmokeRoot.StartsWith($verificationPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Refusing to clean unexpected smoke directory: $resolvedSmokeRoot"
            }
            $cleanupDeadline = [DateTime]::UtcNow.AddSeconds(15)
            do {
                try {
                    Remove-Item -LiteralPath $resolvedSmokeRoot -Recurse -Force -ErrorAction Stop
                }
                catch [System.UnauthorizedAccessException] {
                    if ([DateTime]::UtcNow -ge $cleanupDeadline) { throw }
                    Start-Sleep -Milliseconds 500
                }
            } while (Test-Path -LiteralPath $resolvedSmokeRoot)
        }
    }
}

Write-Output "OPENRPG_FORGE_CREATOR_DISTRIBUTION_VERIFY_PASS files=$($manifest.files.Count) smoke=$RunSmoke mcp_tools=$($manifest.mcp_tool_count) source=$($manifest.source_revision)"
