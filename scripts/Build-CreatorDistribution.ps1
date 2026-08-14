[CmdletBinding()]
param(
    [string]$OutputDirectory = 'artifacts/distribution/creator-windows-x64',
    [string]$EnginePath,
    [switch]$Force,
    [switch]$CreateArchive,
    [switch]$SkipGameLauncherTemplate
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$distributionRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/distribution'))
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

if ([string]::IsNullOrWhiteSpace($EnginePath)) {
    $engineCommand = Get-Command 'Godot_v4.7.1-stable_mono_win64.exe' -ErrorAction SilentlyContinue
    if ($null -eq $engineCommand) {
        throw 'The Creator builder requires the Godot 4.7.1 .NET executable on PATH.'
    }
    $EnginePath = $engineCommand.Source
}
$EnginePath = [System.IO.Path]::GetFullPath($EnginePath)
if (-not (Test-Path -LiteralPath $EnginePath -PathType Leaf)) {
    throw "The configured engine executable does not exist: $EnginePath"
}

function Invoke-Dotnet {
    param([Parameter(Mandatory)][string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Copy-PortableTree {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )
    $sourcePath = [System.IO.Path]::GetFullPath($Source)
    $prefix = $sourcePath.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    foreach ($sourceFile in Get-ChildItem -LiteralPath $sourcePath -Recurse -File -Force) {
        $relativePath = $sourceFile.FullName.Substring($prefix.Length)
        $segments = $relativePath -split '[\\/]'
        $isEngineCache = $segments -contains '.godot'
        if ($segments -contains '.forge' -or $segments -contains '.git' -or
            $segments -contains 'shader_cache' -or
            ((($segments -contains 'bin') -or ($segments -contains 'obj')) -and -not $isEngineCache) -or
            $sourceFile.Extension -eq '.tmp') {
            continue
        }

        $targetFile = Join-Path $Destination $relativePath
        New-Item -ItemType Directory -Path (Split-Path -Parent $targetFile) -Force | Out-Null
        Copy-Item -LiteralPath $sourceFile.FullName -Destination $targetFile -Force
    }
}

Invoke-Dotnet @('build', (Join-Path $repositoryRoot 'apps/Forge.Toolset/Forge.Toolset.csproj'),
    '--configuration', 'Debug')
Invoke-Dotnet @('build', (Join-Path $repositoryRoot 'apps/Forge.Runtime/Forge.Runtime.csproj'),
    '--configuration', 'Debug')

$buildRoot = Join-Path $outputPath '.build'
$launcherPublish = Join-Path $buildRoot 'launcher'
$launcherRestoreLock = Join-Path $buildRoot 'launcher.restore.lock.json'
Invoke-Dotnet @('restore', (Join-Path $repositoryRoot 'apps/Forge.CreatorLauncher/Forge.CreatorLauncher.csproj'),
    '--runtime', 'win-x64', '--force', "-p:NuGetLockFilePath=$launcherRestoreLock",
    '-p:RestorePackagesWithLockFile=false')
Invoke-Dotnet @('publish', (Join-Path $repositoryRoot 'apps/Forge.CreatorLauncher/Forge.CreatorLauncher.csproj'),
    '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true',
    '--no-restore', '-p:PublishSingleFile=true', '-p:DebugType=None', '--output', $launcherPublish)

$launcher = Get-ChildItem -LiteralPath $launcherPublish -Filter 'OpenRPG_Forge_Creator.exe' -File |
    Select-Object -First 1
if ($null -eq $launcher) {
    throw 'The Creator launcher publish did not produce OpenRPG_Forge_Creator.exe.'
}
Copy-Item -LiteralPath $launcher.FullName -Destination (Join-Path $outputPath $launcher.Name) -Force

$mcpTarget = Join-Path $outputPath 'mcp'
$mcpRestoreLock = Join-Path $buildRoot 'mcp.restore.lock.json'
Invoke-Dotnet @('restore', (Join-Path $repositoryRoot 'apps/Forge.McpHost/Forge.McpHost.csproj'),
    '--runtime', 'win-x64', '--force', "-p:NuGetLockFilePath=$mcpRestoreLock",
    '-p:RestorePackagesWithLockFile=false')
Invoke-Dotnet @('publish', (Join-Path $repositoryRoot 'apps/Forge.McpHost/Forge.McpHost.csproj'),
    '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true',
    '--no-restore', '-p:DebugType=None', '--output', $mcpTarget)

$engineTarget = Join-Path $outputPath 'engine'
New-Item -ItemType Directory -Path $engineTarget -Force | Out-Null
Copy-Item -LiteralPath $EnginePath -Destination (Join-Path $engineTarget 'OpenRPG_Forge_Editor.exe') -Force
$engineDirectory = Split-Path -Parent $EnginePath
$godotSharpSource = Join-Path $engineDirectory 'GodotSharp'
if (-not (Test-Path -LiteralPath $godotSharpSource -PathType Container)) {
    throw "The engine .NET support directory is missing: $godotSharpSource"
}
Copy-PortableTree -Source $godotSharpSource -Destination (Join-Path $engineTarget 'GodotSharp')

$workspaceTarget = Join-Path $outputPath 'workspace'
New-Item -ItemType Directory -Path $workspaceTarget -Force | Out-Null
foreach ($fileName in @('AGENTS.md', 'PROJECT_CONTEXT.md', 'Directory.Build.props', 'global.json', 'README.md')) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot $fileName) -Destination $workspaceTarget -Force
}
foreach ($directoryName in @('src', 'asset_manifests', 'native', 'third_party')) {
    Copy-PortableTree -Source (Join-Path $repositoryRoot $directoryName) `
        -Destination (Join-Path $workspaceTarget $directoryName)
}
foreach ($applicationName in @('Forge.Toolset', 'Forge.Runtime')) {
    Copy-PortableTree -Source (Join-Path $repositoryRoot "apps/$applicationName") `
        -Destination (Join-Path $workspaceTarget "apps/$applicationName")
}

# The old Crypt prototype is intentionally not shipped in the one-click Creator.
# Forgotten Forest is the single immediately usable example presented to creators.
$obsoletePrototype = Join-Path $workspaceTarget 'apps/Forge.Runtime/content/crypt-of-ashes'
if (Test-Path -LiteralPath $obsoletePrototype) {
    Remove-Item -LiteralPath $obsoletePrototype -Recurse -Force
}

if (-not $SkipGameLauncherTemplate) {
    $gameLauncherTemplate = Join-Path $workspaceTarget 'templates/game-windows-x64'
    & (Join-Path $PSScriptRoot 'Build-GameLauncherTemplate.ps1') `
        -OutputDirectory $gameLauncherTemplate -EnginePath $EnginePath -Force
    if ($LASTEXITCODE -ne 0) {
        throw "The game launcher template build failed with exit code $LASTEXITCODE."
    }
}

Remove-Item -LiteralPath $buildRoot -Recurse -Force

$launcherPath = Join-Path $outputPath 'OpenRPG_Forge_Creator.exe'
$launcherVerification = Start-Process -FilePath $launcherPath -ArgumentList '--verify' -WorkingDirectory $outputPath `
    -PassThru -Wait -WindowStyle Hidden
if ($launcherVerification.ExitCode -ne 0) {
    throw 'The portable Creator launcher rejected the assembled distribution layout.'
}

$readme = @'
OPENRPG FORGE CREATOR - PORTABLE WINDOWS X64

Double-cliquez OpenRPG_Forge_Creator.exe.
Le moteur 3D embarqué reste une dépendance interne sous engine/.
Le workspace et les modules fournis sont sous workspace/.
Le host MCP gouverné et autonome est sous mcp/.

Cette préversion n'est pas signée. Windows peut afficher un avertissement SmartScreen.
'@
$readme | Set-Content -LiteralPath (Join-Path $outputPath 'LISEZ-MOI.txt') -Encoding utf8

& (Join-Path $PSScriptRoot 'New-DistributionCompliance.ps1') -DistributionDirectory $outputPath `
    -Product 'OpenRPG Forge Creator' -Version '0.1.0-dev'

$manifestPath = Join-Path $outputPath 'distribution-manifest.json'
$prefix = $outputPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
$files = @(
    Get-ChildItem -LiteralPath $outputPath -Recurse -File |
        Where-Object { $_.FullName -ne $manifestPath } |
        Sort-Object FullName |
        ForEach-Object {
            [ordered]@{
                path = $_.FullName.Substring($prefix.Length).Replace('\\', '/')
                bytes = $_.Length
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        }
)
$manifest = [ordered]@{
    schema_version = 1
    product = 'OpenRPG Forge Creator'
    version = '0.1.0-dev'
    platform = 'windows-x64'
    source_revision = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    source_dirty = @(& git -C $repositoryRoot status --porcelain --untracked-files=normal).Count -gt 0
    launcher_verified = $true
    compliance = 'compliance/sbom.cdx.json'
    files = $files
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8

if ($CreateArchive) {
    $archivePath = "$outputPath.zip"
    if (Test-Path -LiteralPath $archivePath) {
        if (-not $Force) { throw "The archive already exists: $archivePath" }
        Remove-Item -LiteralPath $archivePath -Force
    }
    Compress-Archive -LiteralPath $outputPath -DestinationPath $archivePath -CompressionLevel Optimal
    Write-Output "OPENRPG_FORGE_CREATOR_ARCHIVE_READY path=$archivePath sha256=$((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant())"
}

$gameLauncherTemplateStatus = if ($SkipGameLauncherTemplate) { 'skipped' } else { 'bundled' }
Write-Output "OPENRPG_FORGE_CREATOR_DISTRIBUTION_READY path=$outputPath files=$($files.Count) launcher=verified mcp=bundled game_launcher_template=$gameLauncherTemplateStatus"
