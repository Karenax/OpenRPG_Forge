[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$GodotProjectPath,

    [ValidateSet('godot-flow', 'gf-pcgodot')]
    [string]$GraphProvider = 'godot-flow',

    [switch]$Force,
    [switch]$BuildPcgFromSource,
    [switch]$SkipTerrain3D,
    [switch]$SkipPcgGodot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repositoryRoot 'third_party/world-generation.dependencies.json'
$integrationSource = Join-Path $repositoryRoot 'integrations/godot/addons/openrpg_world_generation'
$projectRoot = [System.IO.Path]::GetFullPath($GodotProjectPath)
$projectPrefix = $projectRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar

if (-not (Test-Path (Join-Path $projectRoot 'project.godot'))) {
    throw "'$projectRoot' is not a Godot project: project.godot was not found."
}
if (-not (Test-Path $manifestPath)) {
    throw "Dependency manifest '$manifestPath' was not found."
}
if (-not (Test-Path $integrationSource -PathType Container)) {
    throw "OpenRPG integration addon '$integrationSource' was not found."
}
if ($BuildPcgFromSource -and $SkipPcgGodot) {
    throw 'BuildPcgFromSource cannot be combined with SkipPcgGodot.'
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$terrain = @($manifest.dependencies | Where-Object backend -eq 'terrain3d')
$graphProviders = @($manifest.dependencies | Where-Object backend -eq 'pcgodot')
$pcg = @($graphProviders | Where-Object providerKey -eq $GraphProvider)
if ($manifest.schemaVersion -ne 2 -or $terrain.Count -ne 1 -or
    $graphProviders.Count -ne 2 -or $pcg.Count -ne 1) {
    throw 'The world-generation dependency manifest does not match schema version 2.'
}
$terrain = $terrain[0]
$pcg = $pcg[0]
if ([string]$terrain.archive -notmatch '^https://' -or
    [string]$terrain.sha256 -notmatch '^[0-9a-f]{64}$') {
    throw 'Terrain3D must use an HTTPS archive and a lowercase SHA-256 pin.'
}
if ([string]$pcg.repository -notmatch '^https://' -or
    [string]$pcg.revision -notmatch '^[0-9a-f]{40}$' -or
    @($pcg.sourceDirectories).Count -lt 1 -or
    @($pcg.sourceDirectories).Count -gt 2 -or
    @($pcg.sourceDirectories).Count -ne @($pcg.godotTargets).Count) {
    throw 'The PCG backend must use an HTTPS repository and an immutable lowercase Git commit.'
}

function Resolve-ProjectTarget {
    param([Parameter(Mandatory = $true)][string]$RelativePath)
    if ([System.IO.Path]::IsPathRooted($RelativePath) -or $RelativePath -match '(^|[\\/])\.\.([\\/]|$)') {
        throw "Project target '$RelativePath' is not a safe relative path."
    }
    $resolved = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $RelativePath))
    if (-not $resolved.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Project target '$resolved' escapes '$projectRoot'."
    }
    return $resolved
}

$terrainTarget = Resolve-ProjectTarget ([string]$terrain.godotTarget)
$pcgSourceDirectories = @($pcg.sourceDirectories | ForEach-Object { [string]$_ })
$pcgGodotTargets = @($pcg.godotTargets | ForEach-Object { [string]$_ })
$pcgResolvedTargets = @($pcgGodotTargets | ForEach-Object { Resolve-ProjectTarget $_ })
$integrationTarget = Resolve-ProjectTarget ([string]$manifest.integrationAddon.godotTarget)
$receiptTarget = Resolve-ProjectTarget '.openrpg/world-generation/provider-installation.json'

if ($WhatIfPreference) {
    if (-not $SkipTerrain3D) { $PSCmdlet.ShouldProcess($terrainTarget, "Install pinned Terrain3D $($terrain.version)") | Out-Null }
    if (-not $SkipPcgGodot) {
        for ($targetIndex = 0; $targetIndex -lt $pcgResolvedTargets.Count; $targetIndex++) {
            $verb = if ($targetIndex -eq 0) {
                "Install pinned $($pcg.displayName) commit $($pcg.revision)"
            }
            else {
                "Merge pinned $($pcg.displayName) runtime binaries"
            }
            $PSCmdlet.ShouldProcess($pcgResolvedTargets[$targetIndex], $verb) | Out-Null
        }
        if ($BuildPcgFromSource) {
            $PSCmdlet.ShouldProcess(
                $pcgResolvedTargets[0],
                "Build the pinned $($pcg.displayName) editor extension for the current platform") | Out-Null
        }
    }
    $PSCmdlet.ShouldProcess($integrationTarget, 'Install the OpenRPG world-generation bridge') | Out-Null
    $PSCmdlet.ShouldProcess($receiptTarget, 'Write the versioned provider installation receipt') | Out-Null
    return
}

$temporaryParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$temporaryPrefix = $temporaryParent.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar
$tempRoot = Join-Path $temporaryParent ("openrpg-worldgen-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

function Copy-DirectoryChecked {
    param([string]$Source, [string]$Destination)
    if (-not (Test-Path $Source)) { throw "Source directory '$Source' does not exist." }
    $resolvedDestination = [System.IO.Path]::GetFullPath($Destination)
    if (-not $resolvedDestination.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Destination '$resolvedDestination' escapes '$projectRoot'."
    }
    if ((Test-Path $Destination) -and -not $Force) {
        throw "Destination '$Destination' already exists. Re-run with -Force to replace it."
    }
    if ($PSCmdlet.ShouldProcess($Destination, "Install from $Source")) {
        $parent = Split-Path -Parent $Destination
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
        $staging = Join-Path $parent ("." + [System.IO.Path]::GetFileName($Destination) + ".staging-" + [guid]::NewGuid().ToString('N'))
        $backup = Join-Path $parent ("." + [System.IO.Path]::GetFileName($Destination) + ".backup-" + [guid]::NewGuid().ToString('N'))
        try {
            Copy-Item -LiteralPath $Source -Destination $staging -Recurse
            if (Test-Path $Destination) { Move-Item -LiteralPath $Destination -Destination $backup }
            Move-Item -LiteralPath $staging -Destination $Destination
            if (Test-Path $backup) { Remove-Item -LiteralPath $backup -Recurse -Force }
        }
        catch {
            if (Test-Path $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
            if ((Test-Path $backup) -and -not (Test-Path $Destination)) {
                Move-Item -LiteralPath $backup -Destination $Destination
            }
            throw
        }
    }
}

function Copy-DirectoryContentsChecked {
    param([string]$Source, [string]$Destination)
    if (-not (Test-Path $Source -PathType Container)) { throw "Source directory '$Source' does not exist." }
    $resolvedDestination = [System.IO.Path]::GetFullPath($Destination)
    if (-not $resolvedDestination.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Destination '$resolvedDestination' escapes '$projectRoot'."
    }
    $files = @(Get-ChildItem -LiteralPath $Source -File -Recurse)
    $collisions = @($files | Where-Object {
        $relative = [System.IO.Path]::GetRelativePath($Source, $_.FullName)
        Test-Path (Join-Path $Destination $relative)
    })
    if ($collisions.Count -gt 0 -and -not $Force) {
        throw "Destination '$Destination' contains $($collisions.Count) conflicting PCG file(s). Re-run with -Force to replace only those files."
    }
    if (-not $PSCmdlet.ShouldProcess($Destination, "Merge files from $Source")) { return }

    $backupRoot = Join-Path $tempRoot ("pcg-bin-backup-" + [guid]::NewGuid().ToString('N'))
    $written = [System.Collections.Generic.List[string]]::new()
    try {
        foreach ($file in $files) {
            $relative = [System.IO.Path]::GetRelativePath($Source, $file.FullName)
            $target = [System.IO.Path]::GetFullPath((Join-Path $Destination $relative))
            if (-not $target.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "PCG binary target '$target' escapes '$projectRoot'."
            }
            if (Test-Path $target) {
                $backup = Join-Path $backupRoot $relative
                New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
                Copy-Item -LiteralPath $target -Destination $backup
            }
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            $written.Add($target)
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        }
    }
    catch {
        foreach ($target in $written) { if (Test-Path $target) { Remove-Item -LiteralPath $target -Force } }
        if (Test-Path $backupRoot) {
            foreach ($backup in Get-ChildItem -LiteralPath $backupRoot -File -Recurse) {
                $relative = [System.IO.Path]::GetRelativePath($backupRoot, $backup.FullName)
                $target = Join-Path $Destination $relative
                New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
                Copy-Item -LiteralPath $backup.FullName -Destination $target -Force
            }
        }
        throw
    }
}

function Get-ProviderEvidence {
    param([Parameter(Mandatory = $true)][object]$Dependency)
    $evidence = @()
    foreach ($relativePathValue in @($Dependency.evidencePaths)) {
        $relativePath = [string]$relativePathValue
        $target = Resolve-ProjectTarget $relativePath
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
            throw "Provider evidence '$relativePath' was not installed."
        }
        $evidence += [ordered]@{
            relativePath = $relativePath.Replace('\\', '/')
            sha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    if ($evidence.Count -eq 0) {
        throw "Provider '$($Dependency.providerKey)' has no evidence paths."
    }
    return @($evidence)
}

function Write-ProviderReceipt {
    param([Parameter(Mandatory = $true)][object[]]$Providers)

    $receiptDirectory = Split-Path -Parent $receiptTarget
    New-Item -ItemType Directory -Path $receiptDirectory -Force | Out-Null
    $staging = Join-Path $receiptDirectory (
        '.provider-installation.staging-' + [guid]::NewGuid().ToString('N') + '.json')
    $backup = Join-Path $receiptDirectory (
        '.provider-installation.backup-' + [guid]::NewGuid().ToString('N') + '.json')
    $receipt = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        installer = 'openrpg-forge/world-generation-dependencies@1'
        providers = @($Providers)
    }
    try {
        $receipt | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $staging -Encoding utf8NoBOM
        if (Test-Path -LiteralPath $receiptTarget) {
            Move-Item -LiteralPath $receiptTarget -Destination $backup
        }
        Move-Item -LiteralPath $staging -Destination $receiptTarget
        if (Test-Path -LiteralPath $backup) {
            Remove-Item -LiteralPath $backup -Force
        }
    }
    catch {
        if (Test-Path -LiteralPath $staging) {
            Remove-Item -LiteralPath $staging -Force
        }
        if ((Test-Path -LiteralPath $backup) -and -not (Test-Path -LiteralPath $receiptTarget)) {
            Move-Item -LiteralPath $backup -Destination $receiptTarget
        }
        throw
    }
}

try {
    $installedProviders = [System.Collections.Generic.List[object]]::new()
    if (-not $SkipTerrain3D) {
        $archive = Join-Path $tempRoot 'terrain3d.zip'
        Invoke-WebRequest -Uri $terrain.archive -OutFile $archive
        $actualHash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne [string]$terrain.sha256) {
            throw "Terrain3D SHA-256 mismatch. Expected $($terrain.sha256), received $actualHash."
        }
        $expanded = Join-Path $tempRoot 'terrain3d'
        Expand-Archive -Path $archive -DestinationPath $expanded
        $plugin = Get-ChildItem $expanded -Recurse -Filter plugin.cfg |
            Where-Object FullName -Match '[\\/]addons[\\/]terrain_3d[\\/]plugin\.cfg$' |
            Select-Object -First 1
        if ($null -eq $plugin) { throw 'Terrain3D addon directory was not found in the verified archive.' }
        Copy-DirectoryChecked -Source $plugin.Directory.FullName -Destination $terrainTarget
        $installedProviders.Add([ordered]@{
            backend = 'terrain3d'
            providerKey = [string]$terrain.providerKey
            repository = [string]$terrain.repository
            versionOrRevision = [string]$terrain.version
            sourceArchiveSha256 = [string]$terrain.sha256
            evidence = @(Get-ProviderEvidence -Dependency $terrain)
        })
    }

    if (-not $SkipPcgGodot) {
        $git = Get-Command git -ErrorAction SilentlyContinue
        if ($null -eq $git) { throw 'Git is required to install the pinned PCG backend and its submodules.' }
        $clone = Join-Path $tempRoot 'godot_flow'
        & $git.Source clone --filter=blob:none --no-checkout ([string]$pcg.repository) $clone
        if ($LASTEXITCODE -ne 0) { throw 'Unable to clone the PCG backend repository.' }
        & $git.Source -C $clone checkout --detach ([string]$pcg.revision)
        if ($LASTEXITCODE -ne 0) { throw "Unable to checkout PCG revision $($pcg.revision)." }
        & $git.Source -C $clone submodule update --init --recursive
        if ($LASTEXITCODE -ne 0) { throw 'Unable to initialize PCG backend submodules.' }
        $resolved = (& $git.Source -C $clone rev-parse HEAD).Trim()
        if ($resolved -ne [string]$pcg.revision) { throw "PCG revision mismatch: $resolved." }
        $treeRevision = (& $git.Source -C $clone rev-parse 'HEAD^{tree}').Trim()
        if ($treeRevision -notmatch '^[0-9a-f]{40}$') {
            throw "PCG tree revision is invalid: $treeRevision."
        }
        $submodules = @()
        foreach ($lineValue in @(& $git.Source -C $clone submodule status --recursive)) {
            $line = [string]$lineValue
            if ($line -notmatch '^[ +\-U]?([0-9a-f]{40})\s+([^\s]+)') {
                throw "Unable to parse PCG submodule status '$line'."
            }
            $submodules += [ordered]@{
                path = $Matches[2].Replace('\\', '/')
                revision = $Matches[1]
            }
        }
        $sourceBuild = $null
        if ($BuildPcgFromSource) {
            $scons = Get-Command scons -ErrorAction SilentlyContinue
            if ($null -eq $scons) {
                throw 'SCons is required when BuildPcgFromSource is selected.'
            }
            Push-Location $clone
            try {
                & $scons.Source target=editor
                if ($LASTEXITCODE -ne 0) {
                    throw "Unable to build $($pcg.displayName) from pinned sources."
                }
            }
            finally {
                Pop-Location
            }
            $platformName = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
                    [System.Runtime.InteropServices.OSPlatform]::Windows)) {
                'windows'
            }
            elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
                    [System.Runtime.InteropServices.OSPlatform]::Linux)) {
                'linux'
            }
            elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
                    [System.Runtime.InteropServices.OSPlatform]::OSX)) {
                'macos'
            }
            else {
                throw 'The current platform is not supported by the pinned PCG source build.'
            }
            $architecture = switch ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture) {
                'X64' { 'x86_64' }
                'Arm64' { 'arm64' }
                default { throw "Unsupported PCG source-build architecture '$($_)'." }
            }
            $extension = if ($platformName -eq 'windows') { '.dll' } elseif ($platformName -eq 'linux') { '.so' } else { '' }
            $builtLibrary = Get-ChildItem -LiteralPath (Join-Path $clone 'demo') -File -Recurse |
                Where-Object Name -Like "libflow.$platformName.editor.$architecture*$extension" |
                Select-Object -First 1
            if ($null -eq $builtLibrary) {
                throw "The pinned PCG source build produced no editor library for $platformName/$architecture."
            }
            $artifactRelativePath = $null
            for ($sourceIndex = 0; $sourceIndex -lt $pcgSourceDirectories.Count; $sourceIndex++) {
                $sourceRoot = [System.IO.Path]::GetFullPath(
                    (Join-Path $clone $pcgSourceDirectories[$sourceIndex]))
                $sourcePrefix = $sourceRoot.TrimEnd(
                    [System.IO.Path]::DirectorySeparatorChar,
                    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
                if ($builtLibrary.FullName.StartsWith(
                        $sourcePrefix,
                        [System.StringComparison]::OrdinalIgnoreCase)) {
                    $relativeLibrary = [System.IO.Path]::GetRelativePath($sourceRoot, $builtLibrary.FullName)
                    $artifactRelativePath = (Join-Path $pcgGodotTargets[$sourceIndex] $relativeLibrary).Replace('\', '/')
                    break
                }
            }
            if ([string]::IsNullOrWhiteSpace($artifactRelativePath)) {
                throw "Built PCG library '$($builtLibrary.FullName)' is outside the declared source directories."
            }
            $sourceBuild = [ordered]@{
                target = 'editor'
                platform = $platformName
                architecture = $architecture
                artifact = $artifactRelativePath
                sha256 = (Get-FileHash -LiteralPath $builtLibrary.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        }
        for ($sourceIndex = 0; $sourceIndex -lt $pcgSourceDirectories.Count; $sourceIndex++) {
            $sourceDirectory = Join-Path $clone $pcgSourceDirectories[$sourceIndex]
            if ($sourceIndex -eq 0) {
                Copy-DirectoryChecked -Source $sourceDirectory -Destination $pcgResolvedTargets[$sourceIndex]
            }
            else {
                Copy-DirectoryContentsChecked -Source $sourceDirectory -Destination $pcgResolvedTargets[$sourceIndex]
            }
        }
        if ($null -ne $sourceBuild -and $sourceBuild.platform -eq 'linux') {
            $gdextensionEvidence = @($pcg.evidencePaths | Where-Object {
                [System.IO.Path]::GetFileName([string]$_) -eq 'flow.gdextension'
            })
            if ($gdextensionEvidence.Count -ne 1) {
                throw 'The PCG provider must declare exactly one flow.gdextension evidence path.'
            }
            $gdextensionPath = Resolve-ProjectTarget ([string]$gdextensionEvidence[0])
            $gdextensionText = Get-Content -LiteralPath $gdextensionPath -Raw
            $libraryKey = "linux.$($sourceBuild.architecture).editor"
            if ($gdextensionText -notmatch "(?m)^$([regex]::Escape($libraryKey))\s*=") {
                $libraryEntry = "$libraryKey = `"res://$($sourceBuild.artifact)`""
                $gdextensionText = $gdextensionText -replace '(?m)^\[libraries\]\s*$', "[libraries]`n$libraryEntry"
                Set-Content -LiteralPath $gdextensionPath -Value $gdextensionText -Encoding utf8NoBOM
            }
        }
        $pcgReceipt = [ordered]@{
            backend = 'pcgodot'
            providerKey = [string]$pcg.providerKey
            repository = [string]$pcg.repository
            versionOrRevision = [string]$pcg.revision
            treeRevision = $treeRevision
            submodules = @($submodules)
            evidence = @(Get-ProviderEvidence -Dependency $pcg)
        }
        if ($null -ne $sourceBuild) {
            $pcgReceipt.sourceBuild = $sourceBuild
        }
        $installedProviders.Add($pcgReceipt)
    }

    Copy-DirectoryChecked -Source $integrationSource -Destination $integrationTarget
    Write-ProviderReceipt -Providers @($installedProviders)

    Write-Host ''
    Write-Host 'World-generation dependencies installed.'
    Write-Host 'In Godot, enable these editor plugins:'
    if (-not $SkipTerrain3D) { Write-Host '  - Terrain3D' }
    if (-not $SkipPcgGodot) { Write-Host "  - $($pcg.displayName)" }
    Write-Host '  - OpenRPG World Generation Bridge'
    Write-Host "Provider receipt: $receiptTarget"
    Write-Host 'Then create or assign the reusable graphs under res://world/generation/graphs/.'
}
finally {
    $resolvedTemporary = [System.IO.Path]::GetFullPath($tempRoot)
    if (-not $resolvedTemporary.StartsWith($temporaryPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        [System.IO.Path]::GetFileName($resolvedTemporary) -notmatch '^openrpg-worldgen-[0-9a-f]{32}$') {
        throw "Refusing to clean unexpected temporary path '$resolvedTemporary'."
    }
    if (Test-Path $resolvedTemporary) { Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force }
}
