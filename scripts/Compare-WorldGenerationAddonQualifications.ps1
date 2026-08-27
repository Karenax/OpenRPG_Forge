[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BaselineRun,
    [Parameter(Mandatory = $true)][string]$CandidateRun,
    [ValidateSet('addon', 'reference-2km')][string]$QualificationProfile = 'addon',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactDirectory = if ($QualificationProfile -eq 'reference-2km') {
    'artifacts/world-generation/reference-2km'
}
else {
    'artifacts/world-generation/qualification'
}
$qualificationRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $artifactDirectory))
$qualificationPrefix = $qualificationRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

function Resolve-QualificationDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)
    $resolved = [System.IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith($qualificationPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $resolved -PathType Container)) {
        throw "Qualification run '$resolved' is missing or escapes '$qualificationRoot'."
    }
    return $resolved
}

function Read-RequiredJson {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Name
    )
    $path = Join-Path $Root $Name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required qualification artifact '$path' is missing."
    }
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Get-CaptureHashes {
    param([Parameter(Mandatory = $true)][string]$Root)
    $captureRoot = Join-Path $Root 'captures'
    if (-not (Test-Path -LiteralPath $captureRoot -PathType Container)) {
        return [ordered]@{}
    }
    $result = [ordered]@{}
    foreach ($file in Get-ChildItem -LiteralPath $captureRoot -Filter '*.png' -File | Sort-Object Name) {
        $result[$file.Name] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    return $result
}

$baselineRoot = Resolve-QualificationDirectory $BaselineRun
$candidateRoot = Resolve-QualificationDirectory $CandidateRun
if ($baselineRoot -eq $candidateRoot) {
    throw 'Baseline and candidate qualification runs must be different.'
}

$baselineMetrics = Read-RequiredJson -Root $baselineRoot -Name 'metrics.json'
$candidateMetrics = Read-RequiredJson -Root $candidateRoot -Name 'metrics.json'
$baselineProviders = Read-RequiredJson -Root $baselineRoot -Name 'providers.json'
$candidateProviders = Read-RequiredJson -Root $candidateRoot -Name 'providers.json'
$baselineReport = Read-RequiredJson -Root $baselineRoot -Name 'qualification-report.json'
$candidateReport = Read-RequiredJson -Root $candidateRoot -Name 'qualification-report.json'

$baselineRecipe = Join-Path $baselineRoot 'recipe.json'
$candidateRecipe = Join-Path $candidateRoot 'recipe.json'
$baselinePlan = Join-Path $baselineRoot 'execution-plan.json'
$candidatePlan = Join-Path $candidateRoot 'execution-plan.json'
$sameRecipe = (Get-FileHash -LiteralPath $baselineRecipe -Algorithm SHA256).Hash -eq
    (Get-FileHash -LiteralPath $candidateRecipe -Algorithm SHA256).Hash
$samePlan = (Get-FileHash -LiteralPath $baselinePlan -Algorithm SHA256).Hash -eq
    (Get-FileHash -LiteralPath $candidatePlan -Algorithm SHA256).Hash
if (-not $sameRecipe -or -not $samePlan) {
    throw 'Provider comparison requires byte-identical recipe and execution-plan fixtures.'
}
if ($baselineReport.providerKey -eq $candidateReport.providerKey) {
    throw 'Provider comparison requires two distinct provider keys.'
}
if ([string]$baselineReport.qualificationProfile -ne $QualificationProfile -or
    [string]$candidateReport.qualificationProfile -ne $QualificationProfile) {
    throw 'Provider comparison profile does not match the two qualification reports.'
}

$baselineCaptures = Get-CaptureHashes $baselineRoot
$candidateCaptures = Get-CaptureHashes $candidateRoot
$captureNames = @($baselineCaptures.Keys | Sort-Object)
$captureParity = $captureNames.Count -gt 0 -and
    $captureNames.Count -eq $candidateCaptures.Keys.Count -and
    @($captureNames | Where-Object {
        -not $candidateCaptures.Contains($_) -or $candidateCaptures[$_] -ne $baselineCaptures[$_]
    }).Count -eq 0
$baselineDuration = [double]$baselineMetrics.durationsMilliseconds.liveGenerationBake
$candidateDuration = [double]$candidateMetrics.durationsMilliseconds.liveGenerationBake
$baselinePromotionContent = if (
    $null -ne $baselineMetrics.PSObject.Properties['promotionCandidateContentFingerprint'] -and
    -not [string]::IsNullOrWhiteSpace([string]$baselineMetrics.promotionCandidateContentFingerprint)) {
    [string]$baselineMetrics.promotionCandidateContentFingerprint
}
else {
    [string]$baselineMetrics.sceneContentFingerprint
}
$candidatePromotionContent = if (
    $null -ne $candidateMetrics.PSObject.Properties['promotionCandidateContentFingerprint'] -and
    -not [string]::IsNullOrWhiteSpace([string]$candidateMetrics.promotionCandidateContentFingerprint)) {
    [string]$candidateMetrics.promotionCandidateContentFingerprint
}
else {
    [string]$candidateMetrics.sceneContentFingerprint
}
$durationChangeRatio = if ($baselineDuration -gt 0.0) {
    ($candidateDuration / $baselineDuration) - 1.0
}
else {
    $null
}

$functionalParity =
    $baselineMetrics.terrainFingerprint -eq $candidateMetrics.terrainFingerprint -and
    $baselineMetrics.graphFingerprint -eq $candidateMetrics.graphFingerprint -and
    $baselineMetrics.sceneContentFingerprint -eq $candidateMetrics.sceneContentFingerprint -and
    $baselinePromotionContent -eq $candidatePromotionContent -and
    [long]$baselineMetrics.generatedInstanceCount -eq [long]$candidateMetrics.generatedInstanceCount -and
    [double]$baselineMetrics.terrainWidthMeters -eq [double]$candidateMetrics.terrainWidthMeters -and
    [double]$baselineMetrics.terrainDepthMeters -eq [double]$candidateMetrics.terrainDepthMeters -and
    [long]$baselineMetrics.navigationPolygonCount -eq [long]$candidateMetrics.navigationPolygonCount -and
    [long]$baselineMetrics.navigationPathPointCount -eq [long]$candidateMetrics.navigationPathPointCount
if (-not $functionalParity) {
    throw 'Candidate did not preserve the baseline functional output for the shared fixture.'
}

$comparison = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    fixtureScope = if ($QualificationProfile -eq 'reference-2km') {
        'technical-reference-world-2km'
    }
    else {
        'minimal-addon-qualification'
    }
    baseline = [ordered]@{
        providerKey = [string]$baselineReport.providerKey
        providers = @($baselineProviders.providers)
        run = [System.IO.Path]::GetRelativePath($qualificationRoot, $baselineRoot).Replace('\', '/')
        liveGenerationBakeMilliseconds = [long]$baselineDuration
    }
    candidate = [ordered]@{
        providerKey = [string]$candidateReport.providerKey
        providers = @($candidateProviders.providers)
        run = [System.IO.Path]::GetRelativePath($qualificationRoot, $candidateRoot).Replace('\', '/')
        liveGenerationBakeMilliseconds = [long]$candidateDuration
    }
    comparableInputs = [ordered]@{
        recipe = $sameRecipe
        executionPlan = $samePlan
        repeatCount = [long]$baselineMetrics.repeatCount -eq [long]$candidateMetrics.repeatCount
    }
    results = [ordered]@{
        functionalParity = $functionalParity
        deterministicContentParity = $baselineMetrics.sceneContentFingerprint -eq $candidateMetrics.sceneContentFingerprint
        promotionCandidateContentParity = $baselinePromotionContent -eq $candidatePromotionContent
        captureParity = $captureParity
        captureCount = $captureNames.Count
        sceneBytesBaseline = [long]$baselineMetrics.sceneBytes
        sceneBytesCandidate = [long]$candidateMetrics.sceneBytes
        liveGenerationBakeDurationChangeRatio = if ($null -ne $durationChangeRatio) {
            [Math]::Round($durationChangeRatio, 4)
        }
        else {
            $null
        }
    }
    decision = [ordered]@{
        status = 'experimental'
        productionProviderUnchanged = $true
        reason = if ($QualificationProfile -eq 'reference-2km') {
            'The 2 km technical proxy proves output parity, but no human-reviewed visual or authoring-effort improvement over the baseline.'
        }
        else {
            'The minimal fixture proves compatibility but no visual or authoring-effort improvement over the baseline.'
        }
        deferredGates = if ($QualificationProfile -eq 'reference-2km') {
            @(
                'Blind visual scoring.',
                'Measured manual-correction effort.',
                'Windows and Linux export qualification.'
            )
        }
        else {
            @(
                'Complete 2 km reference-world output.',
                'Blind visual scoring.',
                'Measured manual-correction effort.',
                'Windows and Linux export qualification.'
            )
        }
    }
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $comparisonDirectory = Join-Path $qualificationRoot 'comparisons'
    [System.IO.Directory]::CreateDirectory($comparisonDirectory) | Out-Null
    $OutputPath = Join-Path $comparisonDirectory (
        "$($baselineReport.providerKey)-vs-$($candidateReport.providerKey)-" +
        [DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmssZ') + '.json')
}
else {
    $OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
}
if (-not $OutputPath.StartsWith($qualificationPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Comparison output '$OutputPath' escapes '$qualificationRoot'."
}
if (Test-Path -LiteralPath $OutputPath) {
    throw "Comparison output '$OutputPath' already exists."
}
[System.IO.Directory]::CreateDirectory((Split-Path -Parent $OutputPath)) | Out-Null
$comparison | ConvertTo-Json -Depth 24 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM

Write-Output (
    "WORLD_GENERATION_PROVIDER_COMPARISON_PASS profile=$QualificationProfile baseline=$($baselineReport.providerKey) " +
    "candidate=$($candidateReport.providerKey) functional_parity=$([int]$functionalParity) " +
    "capture_parity=$([int]$captureParity) duration_change_ratio=$([Math]::Round($durationChangeRatio, 4)) " +
    "decision=experimental report=$OutputPath")
