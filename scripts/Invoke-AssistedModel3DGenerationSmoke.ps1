[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RepositoryRoot,
    [string]$BlenderPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repository = [System.IO.Path]::GetFullPath($RepositoryRoot)
$verificationRoot = [System.IO.Path]::GetFullPath((Join-Path $repository 'artifacts\verification'))
$workspaceRoot = [System.IO.Path]::GetFullPath((Join-Path $verificationRoot 'assisted-model3d-generation'))
if (-not $workspaceRoot.StartsWith($verificationRoot + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Assisted model 3D smoke workspace escaped artifacts/verification.'
}
if (Test-Path -LiteralPath $workspaceRoot) {
    Remove-Item -LiteralPath $workspaceRoot -Recurse -Force
}
[System.IO.Directory]::CreateDirectory($workspaceRoot) | Out-Null

$cliProject = Join-Path $repository 'tools\Forge.Cli\Forge.Cli.csproj'
$cliOutput = & dotnet run --project $cliProject --configuration Release --no-build -- `
    assets generate-model3d $workspaceRoot `
    --slug 'g12k-provider-idol' `
    --name 'G12K Provider Idol' `
    --prompt 'A compact weathered stone idol with a strong readable silhouette' `
    --max-triangles '10000' `
    --lod '0.5,0.25' `
    --collision 'true' `
    --seed '20260806' `
    --approve 'true' `
    --apply 'true'
if ($LASTEXITCODE -ne 0) {
    throw "Assisted model 3D CLI failed with exit code $LASTEXITCODE."
}
if (($cliOutput -join "`n") -notmatch 'OPENRPG_FORGE_G12K_MODEL3D_CLI_PASS mode=applied') {
    throw 'Assisted model 3D CLI did not emit its applied marker.'
}

$sourcePath = Join-Path $workspaceRoot 'assets\staging\ai-3d\g12k-provider-idol\g12k-provider-idol.obj'
$receiptPath = Join-Path $workspaceRoot 'assets\staging\ai-3d\g12k-provider-idol\receipt.json'
$outputRelativePath = 'assets/generated/g12k-provider-idol/model.glb'
$outputPath = Join-Path $workspaceRoot ($outputRelativePath -replace '/', '\')
$requests = @(Get-ChildItem -LiteralPath (Join-Path $workspaceRoot 'assets\roundtrip') -Filter '*.worker-request.json')
if ($requests.Count -ne 1 -or -not (Test-Path -LiteralPath $sourcePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
    throw 'Assisted model 3D staging did not produce exactly one governed worker request, OBJ and receipt.'
}
$requestPath = $requests[0].FullName
$responsePath = Join-Path $workspaceRoot 'worker-response.json'
$sourceHashBefore = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
$receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
if (-not $receipt.humanAccepted -or $receipt.licenseSpdx -ne 'CC0-1.0' -or
    $receipt.provider.key -ne 'deterministic-offline-3d' -or
    $receipt.sourceSha256 -ne $sourceHashBefore -or $receipt.sourceTriangleCount -ne 12) {
    throw 'Assisted model 3D governance receipt is invalid.'
}

$arguments = @(
    (Join-Path $repository 'tools\asset_pipeline\blender_worker.py'),
    '--request', $requestPath,
    '--response', $responsePath,
    '--source-root', $workspaceRoot,
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
    throw "Assisted model 3D Blender worker failed with exit code $LASTEXITCODE."
}
$response = Get-Content -LiteralPath $responsePath -Raw | ConvertFrom-Json
if (-not $response.valid -or
    (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $sourceHashBefore) {
    throw 'Assisted model 3D worker validation failed or changed its immutable staging source.'
}
if ([string]::IsNullOrWhiteSpace($BlenderPath)) {
    if ($response.executed) {
        throw 'Assisted model 3D validation-only smoke unexpectedly executed Blender.'
    }
    Write-Output 'OPENRPG_FORGE_G12K_MODEL3D_BLENDER_PASS mode=validation-only provider=offline source=obj license=cc0 staging=immutable acceptance=human'
    return
}

$outputDirectory = [System.IO.Path]::GetDirectoryName($outputPath)
$outputStem = [System.IO.Path]::GetFileNameWithoutExtension($outputPath)
$previewPath = Join-Path $outputDirectory "$outputStem.preview.png"
$qualityPath = Join-Path $outputDirectory "$outputStem.quality.json"
if (-not $response.executed -or -not (Test-Path -LiteralPath $outputPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $previewPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $qualityPath -PathType Leaf)) {
    throw 'Assisted model 3D Blender worker did not produce GLB, preview and quality report.'
}
$bytes = [System.IO.File]::ReadAllBytes($outputPath)
$magic = [System.Text.Encoding]::ASCII.GetString($bytes, 0, 4)
$declaredLength = [BitConverter]::ToUInt32($bytes, 8)
$outputHash = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
$quality = Get-Content -LiteralPath $qualityPath -Raw | ConvertFrom-Json
if ($magic -ne 'glTF' -or $declaredLength -ne $bytes.Length -or
    $outputHash -ne $response.outputSha256 -or -not $quality.passed -or
    $quality.metrics.baseMeshCount -ne 1 -or $quality.metrics.lodMeshCount -ne 2 -or
    $quality.metrics.collisionMeshCount -lt 1 -or $quality.metrics.triangleCount -le 0) {
    throw 'Assisted model 3D Blender artifacts failed structural or quality validation.'
}

Write-Output (
    "OPENRPG_FORGE_G12K_MODEL3D_BLENDER_PASS mode=blender provider=offline source=obj license=cc0 " +
    "staging=immutable acceptance=human lods=$($quality.metrics.lodMeshCount) " +
    "collisions=$($quality.metrics.collisionMeshCount) triangles=$($quality.metrics.triangleCount) bytes=$($bytes.Length)")
