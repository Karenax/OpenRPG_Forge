[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RepositoryRoot,
    [string]$BlenderPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repository = [System.IO.Path]::GetFullPath($RepositoryRoot)
$artifactRoot = Join-Path $repository 'artifacts\verification\assisted-blender-creation'
$sourceRoot = Join-Path $artifactRoot 'source'
$workspaceRoot = Join-Path $artifactRoot 'workspace'
$recipePath = Join-Path $sourceRoot 'forge-pillar.recipe.json'
$requestPath = Join-Path $artifactRoot 'worker-request.json'
$responsePath = Join-Path $artifactRoot 'worker-response.json'
$outputRelativePath = 'assets/generated/forge-pillar/model.glb'
$outputPath = Join-Path $workspaceRoot ($outputRelativePath -replace '/', '\')

[System.IO.Directory]::CreateDirectory($sourceRoot) | Out-Null
[System.IO.Directory]::CreateDirectory($workspaceRoot) | Out-Null

$recipe = [ordered]@{
    schemaVersion = 1
    id = 'ad37c08c-c956-4fbb-9db6-34328e8f6385'
    slug = 'forge-pillar'
    displayName = 'Forge Pillar'
    provenance = 'openrpg-forge:g12-j:assisted-blender-smoke'
    licenseSpdx = 'CC0-1.0'
    primitives = @(
        [ordered]@{
            id = 'd3cd724a-d433-46dc-af0a-9cd7c20d1791'
            name = 'pillar'
            kind = 'cylinder'
            positionMeters = [ordered]@{ x = 0.0; y = 0.0; z = 1.0 }
            rotationDegrees = [ordered]@{ x = 0.0; y = 0.0; z = 0.0 }
            scaleMeters = [ordered]@{ x = 1.0; y = 1.0; z = 2.0 }
        },
        [ordered]@{
            id = 'd8fead10-b8b7-4d6b-9908-3cc34971fe8d'
            name = 'capital'
            kind = 'cube'
            positionMeters = [ordered]@{ x = 0.0; y = 0.0; z = 2.1 }
            rotationDegrees = [ordered]@{ x = 0.0; y = 0.0; z = 15.0 }
            scaleMeters = [ordered]@{ x = 1.3; y = 1.3; z = 0.25 }
        }
    )
    material = [ordered]@{
        name = 'forge_stone'
        baseColor = [ordered]@{ red = 0.32; green = 0.24; blue = 0.18; alpha = 1.0 }
        metallic = 0.05
        roughness = 0.72
    }
    uvProjection = 'cube'
    lodRatios = @(0.5, 0.25)
    generateCollision = $true
    outputRelativePath = $outputRelativePath
}
$recipeJson = $recipe | ConvertTo-Json -Depth 20
[System.IO.File]::WriteAllText($recipePath, $recipeJson + "`n", [System.Text.UTF8Encoding]::new($false))
$sourceHash = (Get-FileHash -LiteralPath $recipePath -Algorithm SHA256).Hash.ToLowerInvariant()

$request = [ordered]@{
    schemaVersion = 1
    jobId = 'b654b0c6-7bc7-46c2-b492-47024802a48f'
    assetId = 'generated:forge-pillar'
    sourceRelativePath = 'forge-pillar.recipe.json'
    outputRelativePath = $outputRelativePath
    sourceSha256 = $sourceHash
    licenseSpdx = 'CC0-1.0'
    provenance = 'openrpg-forge:g12-j:assisted-blender-smoke'
    operations = @(
        'create_primitives',
        'validate_model',
        'normalize_model',
        'generate_lods',
        'generate_collision',
        'export_glb',
        'generate_thumbnail',
        'audit_quality'
    )
    options = [ordered]@{
        applyTransforms = $true
        scaleMeters = 1.0
        pivot = 'ground_center'
        lodRatios = @(0.5, 0.25)
        generateCollision = $true
        animationSources = @()
        materialTextures = @()
        retargetProfile = $null
        creationRecipe = $recipe
    }
}
$requestJson = $request | ConvertTo-Json -Depth 20
[System.IO.File]::WriteAllText($requestPath, $requestJson + "`n", [System.Text.UTF8Encoding]::new($false))

$arguments = @(
    (Join-Path $repository 'tools\asset_pipeline\blender_worker.py'),
    '--request', $requestPath,
    '--response', $responsePath,
    '--source-root', $sourceRoot,
    '--workspace-root', $workspaceRoot
)
if ([string]::IsNullOrWhiteSpace($BlenderPath)) {
    $arguments += '--validate-only'
}
else {
    $arguments += @('--blender', [System.IO.Path]::GetFullPath($BlenderPath))
}

& python @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Assisted Blender creation worker failed with exit code $LASTEXITCODE."
}

$response = Get-Content -LiteralPath $responsePath -Raw | ConvertFrom-Json
if (-not $response.valid) {
    throw 'Assisted Blender creation worker returned an invalid response.'
}
if ([string]::IsNullOrWhiteSpace($BlenderPath)) {
    if ($response.executed) {
        throw 'Assisted Blender validation-only smoke unexpectedly executed Blender.'
    }
    Write-Output 'OPENRPG_FORGE_G12J_BLENDER_CREATION_PASS mode=validation-only recipe=canonical primitives=2 python=forbidden'
    return
}

$outputDirectory = [System.IO.Path]::GetDirectoryName($outputPath)
$outputStem = [System.IO.Path]::GetFileNameWithoutExtension($outputPath)
$previewPath = Join-Path $outputDirectory "$outputStem.preview.png"
$qualityPath = Join-Path $outputDirectory "$outputStem.quality.json"
if (-not $response.executed -or
    -not (Test-Path -LiteralPath $outputPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $previewPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $qualityPath -PathType Leaf)) {
    throw 'Assisted Blender creation did not produce the GLB, preview and quality report.'
}
$bytes = [System.IO.File]::ReadAllBytes($outputPath)
$magic = [System.Text.Encoding]::ASCII.GetString($bytes, 0, 4)
$declaredLength = [BitConverter]::ToUInt32($bytes, 8)
$outputHash = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
$quality = Get-Content -LiteralPath $qualityPath -Raw | ConvertFrom-Json
if ($magic -ne 'glTF' -or
    $declaredLength -ne $bytes.Length -or
    $outputHash -ne $response.outputSha256 -or
    -not $quality.passed -or
    $quality.metrics.baseMeshCount -ne 2 -or
    $quality.metrics.lodMeshCount -ne 4 -or
    $quality.metrics.collisionMeshCount -lt 2 -or
    $quality.metrics.triangleCount -le 0) {
    throw 'Assisted Blender creation artifacts failed structural or quality validation.'
}

Write-Output (
    "OPENRPG_FORGE_G12J_BLENDER_CREATION_PASS mode=blender recipe=canonical primitives=2 " +
    "lods=$($quality.metrics.lodMeshCount) collisions=$($quality.metrics.collisionMeshCount) " +
    "triangles=$($quality.metrics.triangleCount) bytes=$($bytes.Length) python=forbidden")
