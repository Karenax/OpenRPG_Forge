[CmdletBinding()]
param(
    [ValidateSet('windows-x64', 'linux-x64')]
    [string]$Platform = 'windows-x64',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDirectory,
    [string]$EnginePath,
    [switch]$Force,
    [switch]$SkipSmoke,
    [switch]$CreateArchive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot 'Common.ps1')

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$distributionRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/distribution'))
$studioProject = Join-Path $repositoryRoot 'apps/Forge.Studio'
$studioProjectFile = Join-Path $studioProject 'Forge.Studio.csproj'
$studioSolution = Join-Path $studioProject 'Forge.Studio.sln'
$cliProject = Join-Path $repositoryRoot 'tools/Forge.Cli/Forge.Cli.csproj'
$exportPresets = Join-Path $studioProject 'export_presets.cfg'
$workspaceSource = Join-Path $repositoryRoot 'apps/Forge.Runtime/content/crypt-of-ashes'
$cryptArtSource = Join-Path $repositoryRoot 'apps/Forge.Runtime/art/kaykit-dungeon-remastered'
$nativeBuildScript = Join-Path $PSScriptRoot 'Build-ForgeScriptNative.ps1'
$nativeLibraryName = if ($Platform -eq 'windows-x64') {
    'forge_script_native.dll'
}
else {
    'libforge_script_native.so'
}
$nativeLibraryDirectoryName = if ($Platform -eq 'windows-x64') { 'win-x64' } else { 'linux-x64' }
$nativeLibrarySource = Join-Path $repositoryRoot "native/ForgeScript/lib/$nativeLibraryDirectoryName/$nativeLibraryName"
$cliRuntimeIdentifier = if ($Platform -eq 'windows-x64') { 'win-x64' } else { 'linux-x64' }

function Invoke-Dotnet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = "artifacts/distribution/studio-$Platform"
}
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
    $resolvedOutput = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $outputPath).Path)
    if (-not $resolvedOutput.StartsWith($allowedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove an output outside the distribution root: $resolvedOutput"
    }
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

foreach ($requiredPath in @($studioProjectFile, $studioSolution, $exportPresets,
        $nativeBuildScript, (Join-Path $workspaceSource 'project.json'))) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Studio distribution input missing: $requiredPath"
    }
}
if (-not (Test-Path -LiteralPath $cryptArtSource -PathType Container)) {
    throw "Studio Crypt art input missing: $cryptArtSource"
}

& $nativeBuildScript -Platform $Platform
if (-not (Test-Path -LiteralPath $nativeLibrarySource -PathType Leaf)) {
    throw "The $Platform ForgeScript native library was not produced: $nativeLibrarySource"
}

if ([string]::IsNullOrWhiteSpace($EnginePath)) {
    $EnginePath = Get-ForgeEnginePath
}
$EnginePath = [System.IO.Path]::GetFullPath($EnginePath)
if (-not (Test-Path -LiteralPath $EnginePath -PathType Leaf)) {
    throw "The configured engine executable does not exist: $EnginePath"
}

$isWindowsHost = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)
$isLinuxHost = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Linux)
if (-not $SkipSmoke -and (($Platform -eq 'windows-x64' -and -not $isWindowsHost) -or
        ($Platform -eq 'linux-x64' -and -not $isLinuxHost))) {
    throw "A $Platform smoke test requires a matching host. Pass -SkipSmoke only for a structural cross-export."
}

$presetName = if ($Platform -eq 'windows-x64') {
    'OpenRPG Forge Studio Windows x64'
}
else {
    'OpenRPG Forge Studio Linux x64'
}
$executableName = if ($Platform -eq 'windows-x64') {
    'OpenRPG_Forge_Studio.exe'
}
else {
    'OpenRPG_Forge_Studio.x86_64'
}
$executablePath = Join-Path $outputPath $executableName
$packPath = Join-Path $outputPath 'OpenRPG_Forge_Studio.pck'
$exportLog = Join-Path $outputPath 'export.log'
$exportMode = if ($Configuration -eq 'Release') { '--export-release' } else { '--export-debug' }

$previousMsBuildServer = $env:DOTNET_CLI_USE_MSBUILD_SERVER
$previousNodeReuse = $env:MSBUILDDISABLENODEREUSE
$previousNuGetLockDirectory = $env:OpenRpgNuGetLockDirectory
$restoreLockRoot = Join-Path $outputPath '.restore-locks'
[System.IO.Directory]::CreateDirectory($restoreLockRoot) | Out-Null
try {
    $env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
    $env:MSBUILDDISABLENODEREUSE = '1'
    $env:OpenRpgNuGetLockDirectory = $restoreLockRoot
    $exportArguments = @(
        '--headless',
        '--quit',
        '--path', "`"$studioProject`"",
        $exportMode,
        "`"$presetName`"",
        "`"$executablePath`"",
        '--log-file', "`"$exportLog`""
    )
    $launch = @{
        FilePath = $EnginePath
        ArgumentList = $exportArguments
        WorkingDirectory = $studioProject
        PassThru = $true
    }
    if ($isWindowsHost) { $launch.WindowStyle = 'Hidden' }
    $exportProcess = Start-Process @launch
    if (-not $exportProcess.WaitForExit(900000)) {
        Stop-Process -Id $exportProcess.Id -Force
        throw "Studio export exceeded the fifteen-minute safety limit. See $exportLog"
    }
    if ($exportProcess.ExitCode -ne 0) {
        throw "Studio export failed with exit code $($exportProcess.ExitCode). See $exportLog"
    }
}
finally {
    if ($null -eq $previousMsBuildServer) {
        Remove-Item Env:DOTNET_CLI_USE_MSBUILD_SERVER -ErrorAction SilentlyContinue
    }
    else { $env:DOTNET_CLI_USE_MSBUILD_SERVER = $previousMsBuildServer }
    if ($null -eq $previousNodeReuse) {
        Remove-Item Env:MSBUILDDISABLENODEREUSE -ErrorAction SilentlyContinue
    }
    else { $env:MSBUILDDISABLENODEREUSE = $previousNodeReuse }
    if ($null -eq $previousNuGetLockDirectory) {
        Remove-Item Env:OpenRpgNuGetLockDirectory -ErrorAction SilentlyContinue
    }
    else { $env:OpenRpgNuGetLockDirectory = $previousNuGetLockDirectory }
    if (Test-Path -LiteralPath $restoreLockRoot) {
        Remove-Item -LiteralPath $restoreLockRoot -Recurse -Force
    }
}

foreach ($requiredFile in @($executablePath, $packPath)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "The Studio export is incomplete: $requiredFile"
    }
}
$studioAssembly = Get-ChildItem -LiteralPath $outputPath -Recurse -Filter 'Forge.Studio.dll' -File |
    Select-Object -First 1
if ($null -eq $studioAssembly) {
    throw 'The Studio export does not contain Forge.Studio.dll.'
}
$exportedNativeLibrary = Get-ChildItem -LiteralPath $outputPath -Recurse -Filter $nativeLibraryName -File |
    Select-Object -First 1
if ($null -eq $exportedNativeLibrary) {
    throw "The Studio export does not contain $nativeLibraryName."
}

# Godot includes the Forge.Cli apphost metadata because Studio builds the CLI project, but it does
# not copy Forge.Cli.dll when ReferenceOutputAssembly=false. Ship an isolated self-contained CLI
# instead, so the exported Studio never depends on a machine-wide .NET installation.
$studioManagedDirectory = Split-Path -Parent $studioAssembly.FullName
Get-ChildItem -LiteralPath $studioManagedDirectory -Filter 'Forge.Cli.*' -File -ErrorAction SilentlyContinue |
    Remove-Item -Force
$cliTarget = Join-Path $studioManagedDirectory 'forge-cli'
$cliRestoreLock = Join-Path $outputPath '.cli.restore.lock.json'
Invoke-Dotnet @(
    'restore', $cliProject, '--runtime', $cliRuntimeIdentifier, '--force',
    "-p:NuGetLockFilePath=$cliRestoreLock", '-p:RestorePackagesWithLockFile=false'
)
Invoke-Dotnet @(
    'publish', $cliProject, '--configuration', $Configuration, '--runtime', $cliRuntimeIdentifier,
    '--self-contained', 'true', '--no-restore', '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=None', '--output', $cliTarget
)
Remove-Item -LiteralPath $cliRestoreLock -Force -ErrorAction SilentlyContinue
$cliExecutableName = if ($Platform -eq 'windows-x64') { 'Forge.Cli.exe' } else { 'Forge.Cli' }
$cliExecutable = Join-Path $cliTarget $cliExecutableName
if (-not (Test-Path -LiteralPath $cliExecutable -PathType Leaf)) {
    throw "The Studio export does not contain the self-contained Forge CLI: $cliExecutable"
}
if ($Platform -eq 'linux-x64' -and $isLinuxHost) {
    & chmod +x $cliExecutable
    if ($LASTEXITCODE -ne 0) { throw 'Unable to mark the bundled Forge CLI as runnable.' }
}
if (($Platform -eq 'windows-x64' -and $isWindowsHost) -or
    ($Platform -eq 'linux-x64' -and $isLinuxHost)) {
    & $cliExecutable '--self-test'
    if ($LASTEXITCODE -ne 0) {
        throw "The bundled Forge CLI failed its dependency smoke test with exit code $LASTEXITCODE."
    }
}

$workspaceTarget = Join-Path $outputPath 'workspaces/crypt-of-ashes'
$sourcePrefix = $workspaceSource.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
foreach ($sourceFile in Get-ChildItem -LiteralPath $workspaceSource -Recurse -File) {
    $relativePath = $sourceFile.FullName.Substring($sourcePrefix.Length)
    $segments = $relativePath -split '[\\/]'
    if ($segments -contains '.forge' -or $sourceFile.Extension -eq '.tmp') { continue }
    $destination = Join-Path $workspaceTarget $relativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $sourceFile.FullName -Destination $destination -Force
}
$cryptArtTarget = Join-Path $workspaceTarget 'art/kaykit-dungeon-remastered'
New-Item -ItemType Directory -Path $cryptArtTarget -Force | Out-Null
foreach ($sourceFile in Get-ChildItem -LiteralPath $cryptArtSource -File |
        Where-Object { $_.Extension -eq '.glb' -or $_.Name -eq 'CREDITS.md' }) {
    Copy-Item -LiteralPath $sourceFile.FullName -Destination (Join-Path $cryptArtTarget $sourceFile.Name) -Force
}

# Bundle the governed WorldGeneration worker next to the exported Studio. The worker still installs
# only the immutable providers declared by the shipped manifest and runs in an isolated cache.
$worldGenerationTarget = Join-Path $outputPath 'world-generation'
New-Item -ItemType Directory -Path $worldGenerationTarget -Force | Out-Null
foreach ($source in @(
        (Join-Path $repositoryRoot 'scripts/Invoke-WorldGenerationWorkspaceWorker.ps1'),
        (Join-Path $repositoryRoot 'scripts/Install-WorldGenerationDependencies.ps1'),
        (Join-Path $repositoryRoot 'third_party/world-generation.dependencies.json'))) {
    Copy-Item -LiteralPath $source -Destination (Join-Path $worldGenerationTarget (Split-Path -Leaf $source)) -Force
}
foreach ($bundleDirectory in @(
        [pscustomobject]@{
            Source = Join-Path $repositoryRoot 'integrations/godot/world_generation_worker'
            Target = Join-Path $worldGenerationTarget 'worker-project'
        },
        [pscustomobject]@{
            Source = Join-Path $repositoryRoot 'integrations/godot/addons/openrpg_world_generation'
            Target = Join-Path $worldGenerationTarget 'addons/openrpg_world_generation'
        })) {
    $bundlePrefix = $bundleDirectory.Source.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    foreach ($sourceFile in Get-ChildItem -LiteralPath $bundleDirectory.Source -Recurse -File) {
        $relativePath = $sourceFile.FullName.Substring($bundlePrefix.Length)
        $destination = Join-Path $bundleDirectory.Target $relativePath
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $sourceFile.FullName -Destination $destination -Force
    }
}

$smokeTested = -not $SkipSmoke
$smokeLog = Join-Path $outputPath 'studio-smoke.log'
if ($smokeTested) {
    $smokeExecutable = $executablePath
    if ($Platform -eq 'windows-x64') {
        $consoleExecutable = Join-Path $outputPath 'OpenRPG_Forge_Studio.console.exe'
        if (Test-Path -LiteralPath $consoleExecutable -PathType Leaf) {
            $smokeExecutable = $consoleExecutable
        }
    }
    elseif ($isLinuxHost) {
        & chmod +x $smokeExecutable
        if ($LASTEXITCODE -ne 0) { throw "Unable to mark the Linux Studio executable as runnable." }
    }
    $smokeArguments = @(
        '--headless', '--log-file', "`"$smokeLog`"", '--',
        '--workspace', "`"$workspaceTarget`"", '--page', 'placement',
        '--studio-distribution-smoke-test'
    )
    $smokeLaunch = @{
        FilePath = $smokeExecutable
        ArgumentList = $smokeArguments
        WorkingDirectory = $outputPath
        PassThru = $true
    }
    if ($isWindowsHost) { $smokeLaunch.WindowStyle = 'Hidden' }
    $smokeProcess = Start-Process @smokeLaunch
    if (-not $smokeProcess.WaitForExit(900000)) {
        Stop-Process -Id $smokeProcess.Id -Force
        throw "The exported Studio smoke test exceeded fifteen minutes. See $smokeLog"
    }
    if ($smokeProcess.ExitCode -ne 0) {
        throw "The exported Studio smoke test failed with exit code $($smokeProcess.ExitCode). See $smokeLog"
    }
    foreach ($marker in @(
            'OPENRPG_FORGE_STUDIO_EDIT_PASS',
             'OPENRPG_FORGE_STUDIO_UX5A_PASS',
             'OPENRPG_FORGE_STUDIO_UX5B_PASS',
             'OPENRPG_FORGE_STUDIO_CRYPT_ART_PASS',
             'OPENRPG_FORGE_STUDIO_DISTRIBUTION_SMOKE_PASS')) {
        if (-not (Select-String -LiteralPath $smokeLog -SimpleMatch $marker -Quiet)) {
            throw "The exported Studio did not emit marker '$marker'. See $smokeLog"
        }
    }
}

& (Join-Path $PSScriptRoot 'New-DistributionCompliance.ps1') -DistributionDirectory $outputPath `
    -Product 'OpenRPG Forge Studio' -Version '0.1.0-dev'

$manifestPath = Join-Path $outputPath 'distribution-manifest.json'
$manifestPrefix = $outputPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
$files = @(
    Get-ChildItem -LiteralPath $outputPath -Recurse -File |
        Where-Object { $_.FullName -ne $manifestPath } |
        Sort-Object FullName |
        ForEach-Object {
            [ordered]@{
                path = $_.FullName.Substring($manifestPrefix.Length).Replace('\', '/')
                bytes = $_.Length
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        }
)
$sourceRevision = (& git -C $repositoryRoot rev-parse HEAD).Trim()
$sourceDirty = @(& git -C $repositoryRoot status --porcelain --untracked-files=normal).Count -gt 0
$manifest = [ordered]@{
    schema_version = 1
    product = 'OpenRPG Forge Studio'
    version = '0.1.0-dev'
    platform = $Platform
    configuration = $Configuration.ToLowerInvariant()
    source_revision = $sourceRevision
    source_dirty = $sourceDirty
    smoke_tested = $smokeTested
    smoke_status = if ($smokeTested) { 'passed' } else { 'not-run-host-unavailable' }
    cli = $studioAssembly.Directory.Name + '/forge-cli/' + $cliExecutableName
    cli_dependency_smoke = if (($Platform -eq 'windows-x64' -and $isWindowsHost) -or
        ($Platform -eq 'linux-x64' -and $isLinuxHost)) { 'passed' } else { 'not-run-host-unavailable' }
    world_generation_worker = 'world-generation/Invoke-WorldGenerationWorkspaceWorker.ps1'
    workspace = 'workspaces/crypt-of-ashes/project.json'
    compliance = 'compliance/sbom.cdx.json'
    files = $files
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8

if ($CreateArchive) {
    $archivePath = "$outputPath.zip"
    if (Test-Path -LiteralPath $archivePath) {
        if (-not $Force) { throw "The archive already exists. Pass -Force to replace it: $archivePath" }
        Remove-Item -LiteralPath $archivePath -Force
    }
    Compress-Archive -LiteralPath $outputPath -DestinationPath $archivePath -CompressionLevel Optimal
    Write-Output "OPENRPG_FORGE_STUDIO_ARCHIVE_READY path=$archivePath sha256=$((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant())"
}

Write-Output "OPENRPG_FORGE_STUDIO_DISTRIBUTION_READY path=$outputPath platform=$Platform files=$($files.Count) assembly=$($studioAssembly.Name) native=$($exportedNativeLibrary.Name) smoke=$smokeTested"
