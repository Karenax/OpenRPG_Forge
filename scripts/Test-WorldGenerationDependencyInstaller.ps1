[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$installer = Join-Path $PSScriptRoot 'Install-WorldGenerationDependencies.ps1'
$manifestPath = Join-Path $repositoryRoot 'third_party/world-generation.dependencies.json'
$temporaryParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$temporaryPrefix = $temporaryParent.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$tempRoot = Join-Path $temporaryParent ("openrpg-worldgen-installer-test-" + [guid]::NewGuid().ToString('N'))

try {
    New-Item -ItemType Directory -Path $tempRoot | Out-Null
    Set-Content -LiteralPath (Join-Path $tempRoot 'project.godot') -Value '[application]' -Encoding utf8NoBOM

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $graphProviders = @($manifest.dependencies | Where-Object backend -eq 'pcgodot')
    if ($manifest.schemaVersion -ne 2 -or
        @($graphProviders | Where-Object providerKey -eq 'godot-flow').Count -ne 1 -or
        @($graphProviders | Where-Object providerKey -eq 'gf-pcgodot').Count -ne 1) {
        throw 'The dependency manifest does not expose exactly the two isolated Graph PCG candidates in schema v2.'
    }
    & $installer `
        -GodotProjectPath $tempRoot `
        -GraphProvider 'gf-pcgodot' `
        -SkipTerrain3D `
        -WhatIf

    & $installer `
        -GodotProjectPath $tempRoot `
        -GraphProvider 'godot-flow' `
        -SkipTerrain3D `
        -BuildPcgFromSource `
        -WhatIf

    $receipt = Join-Path $tempRoot '.openrpg\world-generation\provider-installation.json'
    if (Test-Path -LiteralPath $receipt) {
        throw 'WhatIf unexpectedly wrote a provider installation receipt.'
    }

    Write-Output 'WORLD_GENERATION_DEPENDENCY_INSTALLER_TEST_PASS schema=2 graph_providers=2 isolated_owner=1 source_build_whatif=1 whatif_readonly=1'
}
finally {
    $resolvedTemporary = [System.IO.Path]::GetFullPath($tempRoot)
    if (-not $resolvedTemporary.StartsWith($temporaryPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        [System.IO.Path]::GetFileName($resolvedTemporary) -notmatch '^openrpg-worldgen-installer-test-[0-9a-f]{32}$') {
        throw "Refusing to clean unexpected test path '$resolvedTemporary'."
    }
    if (Test-Path -LiteralPath $resolvedTemporary) {
        Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force
    }
}
