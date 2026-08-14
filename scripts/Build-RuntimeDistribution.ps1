[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDirectory = 'artifacts/distribution/runtime-windows-x64',
    [string]$EnginePath,
    [switch]$Force,
    [switch]$SkipSmoke,
    [switch]$CreateArchive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$distributionRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/distribution'))
$runtimeProject = Join-Path $repositoryRoot 'apps/Forge.Runtime'
$runtimeSolution = Join-Path $runtimeProject 'Forge.Runtime.sln'
$contentSource = Join-Path $runtimeProject 'content'

if (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot $OutputDirectory
}

$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$allowedPrefix = $distributionRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $outputPath.StartsWith($allowedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The output directory must be a child of $distributionRoot."
}

if (Test-Path -LiteralPath $outputPath) {
    if (-not $Force) {
        throw "The output directory already exists. Pass -Force to replace this generated distribution: $outputPath"
    }

    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

if (-not (Test-Path -LiteralPath $runtimeSolution -PathType Leaf)) {
    throw "The runtime export solution is missing: $runtimeSolution"
}

if ([string]::IsNullOrWhiteSpace($EnginePath)) {
    $engineCommand = Get-Command 'Godot_v4.7.1-stable_mono_win64_console.exe' -ErrorAction SilentlyContinue
    if ($null -eq $engineCommand) {
        throw 'The OpenRPG Forge runtime builder requires the Godot 4.7.1 .NET console executable on PATH.'
    }

    $EnginePath = $engineCommand.Source
}

$EnginePath = [System.IO.Path]::GetFullPath($EnginePath)
if (-not (Test-Path -LiteralPath $EnginePath -PathType Leaf)) {
    throw "The configured engine executable does not exist: $EnginePath"
}

$executablePath = Join-Path $outputPath 'OpenRPG_Forge_Runtime.exe'
$exportLog = Join-Path $outputPath 'export.log'
$exportMode = if ($Configuration -eq 'Release') { '--export-release' } else { '--export-debug' }

$previousMsBuildServer = $env:DOTNET_CLI_USE_MSBUILD_SERVER
$previousNodeReuse = $env:MSBUILDDISABLENODEREUSE
$previousNuGetLockDirectory = $env:OpenRpgNuGetLockDirectory
$restoreLockRoot = Join-Path $outputPath '.restore-locks'
[System.IO.Directory]::CreateDirectory($restoreLockRoot) | Out-Null
try {
    # Godot starts a nested dotnet publish. Isolate it from long-lived MSBuild nodes so a
    # previous RID-specific restore cannot leak stale NuGet state into the export. The
    # distribution restore must also leave the repository's canonical lock files untouched.
    $env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
    $env:MSBUILDDISABLENODEREUSE = '1'
    $env:OpenRpgNuGetLockDirectory = $restoreLockRoot
    $exportArguments = @(
        '--headless',
        '--quit',
        '--path', "`"$runtimeProject`"",
        $exportMode,
        '"OpenRPG Forge Windows x64"',
        "`"$executablePath`"",
        '--log-file', "`"$exportLog`""
    )
    $exportProcess = Start-Process -FilePath $EnginePath -ArgumentList $exportArguments `
        -WorkingDirectory $runtimeProject -PassThru -WindowStyle Hidden
    if (-not $exportProcess.WaitForExit(300000)) {
        Stop-Process -Id $exportProcess.Id -Force
        throw "Runtime export exceeded the five-minute safety limit. See $exportLog"
    }
    if ($exportProcess.ExitCode -ne 0) {
        throw "Runtime export failed with exit code $($exportProcess.ExitCode). See $exportLog"
    }
}
finally {
    if ($null -eq $previousMsBuildServer) {
        Remove-Item Env:DOTNET_CLI_USE_MSBUILD_SERVER -ErrorAction SilentlyContinue
    }
    else {
        $env:DOTNET_CLI_USE_MSBUILD_SERVER = $previousMsBuildServer
    }
    if ($null -eq $previousNodeReuse) {
        Remove-Item Env:MSBUILDDISABLENODEREUSE -ErrorAction SilentlyContinue
    }
    else {
        $env:MSBUILDDISABLENODEREUSE = $previousNodeReuse
    }
    if ($null -eq $previousNuGetLockDirectory) {
        Remove-Item Env:OpenRpgNuGetLockDirectory -ErrorAction SilentlyContinue
    }
    else {
        $env:OpenRpgNuGetLockDirectory = $previousNuGetLockDirectory
    }
    if (Test-Path -LiteralPath $restoreLockRoot) {
        Remove-Item -LiteralPath $restoreLockRoot -Recurse -Force
    }
}

$requiredExportFiles = @(
    $executablePath,
    (Join-Path $outputPath 'OpenRPG_Forge_Runtime.pck')
)
foreach ($requiredFile in $requiredExportFiles) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "The runtime export is incomplete: $requiredFile"
    }
}

$runtimeAssembly = Get-ChildItem -LiteralPath $outputPath -Recurse -Filter 'Forge.Runtime.dll' -File |
    Select-Object -First 1
if ($null -eq $runtimeAssembly) {
    throw 'The runtime export does not contain Forge.Runtime.dll.'
}

# The canonical module loader intentionally uses regular files so packages remain inspectable,
# editable and recoverable. Ship that workspace beside the executable, while excluding editor
# caches and interrupted-write temporaries. Render resources remain present in the PCK as well.
$contentTarget = Join-Path $outputPath 'content'
$sourcePrefix = $contentSource.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
foreach ($sourceFile in Get-ChildItem -LiteralPath $contentSource -Recurse -File) {
    $relativePath = $sourceFile.FullName.Substring($sourcePrefix.Length)
    $segments = $relativePath -split '[\\/]'
    if ($segments -contains '.forge' -or $sourceFile.Extension -eq '.tmp') {
        continue
    }

    $destination = Join-Path $contentTarget $relativePath
    $destinationDirectory = Split-Path -Parent $destination
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    Copy-Item -LiteralPath $sourceFile.FullName -Destination $destination -Force
}

$smokeLog = Join-Path $outputPath 'runtime-smoke.log'
if (-not $SkipSmoke) {
    $consoleExecutable = Join-Path $outputPath 'OpenRPG_Forge_Runtime.console.exe'
    $smokeExecutable = if (Test-Path -LiteralPath $consoleExecutable -PathType Leaf) {
        $consoleExecutable
    }
    else {
        $executablePath
    }
    # Start-Process waits correctly for both GUI-subsystem release exports and optional console
    # exports. Direct invocation of a GUI executable can leave $LASTEXITCODE stale in Windows
    # PowerShell even though the engine ran the smoke scenario successfully.
    $smokeProcess = Start-Process -FilePath $smokeExecutable -ArgumentList @(
        '--headless', '--log-file', "`"$smokeLog`"", '--', '--smoke-test'
    ) -WorkingDirectory $outputPath -PassThru -Wait -WindowStyle Hidden
    $smokeExitCode = $smokeProcess.ExitCode

    if ($smokeExitCode -ne 0) {
        throw "The exported runtime smoke test failed with exit code $smokeExitCode. See $smokeLog"
    }

    if (-not (Select-String -LiteralPath $smokeLog -SimpleMatch 'OPENRPG_FORGE_RUNTIME_SMOKE_PASS' -Quiet)) {
        throw "The exported runtime did not emit its success marker. See $smokeLog"
    }
}

& (Join-Path $PSScriptRoot 'New-DistributionCompliance.ps1') -DistributionDirectory $outputPath `
    -Product 'OpenRPG Forge Runtime' -Version '0.1.0-dev'

$manifestPath = Join-Path $outputPath 'distribution-manifest.json'
$manifestPrefix = $outputPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
$files = @(
    Get-ChildItem -LiteralPath $outputPath -Recurse -File |
        Where-Object { $_.FullName -ne $manifestPath } |
        Sort-Object FullName |
        ForEach-Object {
            [ordered]@{
                path = $_.FullName.Substring($manifestPrefix.Length).Replace('\\', '/')
                bytes = $_.Length
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        }
)

$sourceRevision = (& git -C $repositoryRoot rev-parse HEAD).Trim()
$sourceDirty = @(& git -C $repositoryRoot status --porcelain --untracked-files=normal).Count -gt 0
$manifest = [ordered]@{
    schema_version = 1
    product = 'OpenRPG Forge Runtime'
    version = '0.1.0-dev'
    platform = 'windows-x64'
    configuration = $Configuration.ToLowerInvariant()
    source_revision = $sourceRevision
    source_dirty = $sourceDirty
    smoke_tested = -not $SkipSmoke
    compliance = 'compliance/sbom.cdx.json'
    files = $files
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8

if ($CreateArchive) {
    $archivePath = "$outputPath.zip"
    if (Test-Path -LiteralPath $archivePath) {
        if (-not $Force) {
            throw "The archive already exists. Pass -Force to replace it: $archivePath"
        }

        Remove-Item -LiteralPath $archivePath -Force
    }

    Compress-Archive -LiteralPath $outputPath -DestinationPath $archivePath -CompressionLevel Optimal
    Write-Output "OPENRPG_FORGE_RUNTIME_ARCHIVE_READY path=$archivePath sha256=$((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant())"
}

Write-Output "OPENRPG_FORGE_RUNTIME_DISTRIBUTION_READY path=$outputPath files=$($files.Count) assembly=$($runtimeAssembly.Name) smoke=$(-not $SkipSmoke)"
