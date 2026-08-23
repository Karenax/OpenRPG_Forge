using Forge.Application.Projects;
using Forge.Application.Blueprints;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx3EBlueprintChoice(
    Guid Id,
    string Label,
    bool IsReadOnly,
    bool HasArtisticStyle,
    bool HasAssetBudget);

internal sealed record StudioUx3EWorkspaceAssetChoice(
    string RelativePath,
    string Label);

internal sealed record StudioUx3EStyleDraft(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    Guid BlueprintId,
    double TargetMetersPerUnit,
    int TargetTexelDensity,
    double ScaleTolerancePercent,
    double TexelTolerancePercent,
    string TargetShaderFamily,
    string TargetEra,
    ArtisticRealism TargetRealism,
    IReadOnlyList<string> TargetPalette,
    string TargetProvenance,
    double AssetMetersPerUnit,
    int AssetTexelDensity,
    string AssetShaderFamily,
    string AssetEra,
    ArtisticRealism AssetRealism,
    IReadOnlyList<string> AssetPalette,
    string AssetProvenance,
    bool Approved);

internal sealed record StudioUx3EBudgetDraft(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    Guid BlueprintId,
    Guid ProfileId,
    string Provenance,
    AssetBudgetMetrics Metrics,
    IReadOnlyList<AssetPlatformBudget> Platforms,
    bool RequiredAtRuntime,
    bool Approved);

internal sealed record StudioUx3EMetricResult(
    bool Succeeded,
    string Message,
    double WidthMeters,
    double HeightMeters,
    double DepthMeters,
    int MaximumTexturePixels,
    int EstimatedTexelDensity,
    string ShaderFamily,
    string Provenance);

internal sealed record StudioUx3EDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx3EStylePreview(
    bool Succeeded,
    bool CanApply,
    bool DryRunSucceeded,
    bool BuildDiagnosticLocalized,
    bool RuntimeClaimsBounded,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<StudioUx3EDiagnostic> Diagnostics,
    int CompatibilityWarningCount);

internal sealed record StudioUx3EBudgetPreview(
    bool Succeeded,
    bool CanApply,
    bool DryRunSucceeded,
    bool BuildGateEvaluated,
    bool RuntimeClaimsBounded,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<StudioUx3EDiagnostic> Diagnostics,
    IReadOnlyList<AssetBudgetEvaluation> Evaluations);

internal sealed record StudioUx3EActionResult(bool Succeeded, string Message);

internal sealed record StudioUx3EApprovalSnapshot(
    StudioUx3EStyleDraft? StyleDraft,
    ProjectRevision? StyleRevision,
    string? StyleProjectFingerprint,
    string? StyleDraftFingerprint,
    StudioUx3EBudgetDraft? BudgetDraft,
    ProjectRevision? BudgetRevision,
    string? BudgetProjectFingerprint,
    string? BudgetDraftFingerprint);

internal sealed record StudioUx3EEvidenceProject(
    ProjectDefinition Project,
    Guid EditableBlueprintId,
    string GlbRelativePath,
    string TextureRelativePath,
    bool UsesRealModule,
    bool UsesCanonicalFallback);

internal sealed record StudioUx3ESmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool UsesRealModuleEvidence,
    bool UsesCanonicalFallbackEvidence,
    bool MetricExtraction,
    bool SixCompatibilityWarnings,
    bool InvalidStyleBlocked,
    bool SevenBudgetAxes,
    bool PlatformPassWarningFail,
    bool BuildGateBlocked,
    bool DryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool SeparateApprovals,
    bool CanonicalCommands,
    bool UndoRedo,
    bool Persistence,
    bool ModuleTailsPreserved,
    bool AuthoringTailsPreserved,
    bool BlueprintTailsPreserved,
    bool BlueprintNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
