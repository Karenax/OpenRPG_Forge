[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$planner = Join-Path $PSScriptRoot 'Get-VerificationPlan.ps1'
$executor = Join-Path $PSScriptRoot 'Invoke-TargetedVerification.ps1'

function Assert-VerificationCondition {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )
    if (-not $Condition) {
        throw $Message
    }
}

foreach ($script in @($planner, $executor, (Join-Path $PSScriptRoot 'Verify-StudioIncrement.ps1'))) {
    $tokens = $null
    $errors = $null
    $null = [System.Management.Automation.Language.Parser]::ParseFile(
        $script, [ref]$tokens, [ref]$errors)
    Assert-VerificationCondition ($errors.Count -eq 0) "PowerShell syntax errors in $script."
}

$splitPlan = & $planner -RepositoryRoot $repositoryRoot -NoGraph -ChangedPath @(
    'tests/Forge.Studio.Core.Tests/Navigation/StudioPageCatalogTests.cs',
    'tests/Forge.Network.Tests/NetworkBetaScenarioTests.cs') | ConvertFrom-Json
$testSteps = @($splitPlan.steps | Where-Object kind -eq 'dotnet-test')
Assert-VerificationCondition (-not [string]::IsNullOrWhiteSpace($splitPlan.workspace_fingerprint)) `
    'Plan did not bind itself to the selected workspace state.'
Assert-VerificationCondition ($testSteps.Count -eq 2) 'Expected one independent test step per project.'
$studioStep = $testSteps | Where-Object target -like '*Forge.Studio.Core.Tests.csproj'
$networkStep = $testSteps | Where-Object target -like '*Forge.Network.Tests.csproj'
Assert-VerificationCondition (
    $studioStep.filter -eq 'FullyQualifiedName~StudioPageCatalogTests') `
    'Studio test filter was not scoped to its own class.'
Assert-VerificationCondition (
    $networkStep.filter -eq 'FullyQualifiedName~NetworkBetaScenarioTests') `
    'Network test filter was not scoped to its own class.'

$studioPlan = & $planner -RepositoryRoot $repositoryRoot -Profile Studio `
    -Page zones -Evidence ux1-aa-verbs -StudioSmokeScope Route | ConvertFrom-Json
$smoke = @($studioPlan.steps | Where-Object kind -eq 'studio-smoke')
Assert-VerificationCondition ($smoke.Count -eq 1) 'Studio profile must contain exactly one scoped smoke.'
Assert-VerificationCondition ($smoke[0].arguments -contains 'Route') 'Studio smoke scope was lost from the plan.'
Assert-VerificationCondition (
    [array]::IndexOf(@($studioPlan.steps.kind), 'dotnet-test') -lt
    [array]::IndexOf(@($studioPlan.steps.kind), 'studio-smoke')) `
    'Fast Studio tests must precede the engine smoke.'

$visualPlan = & $planner -RepositoryRoot $repositoryRoot -Profile Studio `
    -Page quality -Evidence ux7-h-visual-regression -StudioSmokeScope Route | ConvertFrom-Json
$visualRuntimeBuild = @($visualPlan.steps | Where-Object id -eq 'studio-visual-runtime-debug-build')
Assert-VerificationCondition ($visualRuntimeBuild.Count -eq 1) `
    'UX7-H Studio verification must build the real Runtime prerequisite exactly once.'
Assert-VerificationCondition (
    [array]::IndexOf(@($visualPlan.steps.id), 'studio-visual-runtime-debug-build') -lt
    [array]::IndexOf(@($visualPlan.steps.kind), 'studio-smoke')) `
    'The UX7-H Runtime build must precede the Studio visual smoke.'

$publicationPlan = & $planner -RepositoryRoot $repositoryRoot -Stage Publication | ConvertFrom-Json
Assert-VerificationCondition ($publicationPlan.steps.Count -eq 1) 'Publication must select exactly the Full gate.'
Assert-VerificationCondition ($publicationPlan.steps[0].kind -eq 'full-gate') 'Publication did not select Full.'

$documentationPlan = & $planner -RepositoryRoot $repositoryRoot -NoGraph `
    -ChangedPath 'docs/planning/TARGETED_VERIFICATION.md' | ConvertFrom-Json
Assert-VerificationCondition (
    @($documentationPlan.steps | Where-Object kind -eq 'text-check').Count -eq 1) `
    'Documentation-only changes must receive a bounded text validation.'

$planOnly = & $executor -RepositoryRoot $repositoryRoot -NoGraph -PlanOnly `
    -ChangedPath 'tests/Forge.Studio.Core.Tests/Navigation/StudioPageCatalogTests.cs' | ConvertFrom-Json
Assert-VerificationCondition ([bool]$planOnly.policy.sequential) 'Executor plan is not sequential.'
Assert-VerificationCondition ([bool]$planOnly.policy.fail_on_zero_tests) 'Executor plan permits zero tests.'
Assert-VerificationCondition ([bool]$planOnly.policy.fail_fast) 'Executor plan is not fail-fast.'

Write-Output 'OPENRPG_FORGE_TARGETED_VERIFICATION_PLANNER_TEST_PASS cases=6'
