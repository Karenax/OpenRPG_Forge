using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx2EnvDAreaChoice(
    AreaId AreaId,
    string Label,
    int BudgetCount,
    SkyProfileMode SkyMode);

internal sealed record StudioUx2EnvDLightBudgetDraft(
    string Platform,
    double MaximumDynamicLights,
    double MaximumShadowCastingLights,
    double ShadowAtlasSize,
    double WarningThreshold);

internal sealed record StudioUx2EnvDDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    string AreaSlug,
    PbrMaterialWorkflow MaterialWorkflow,
    ToneMappingProfile ToneMapping,
    RenderQualityTier Quality,
    bool ForwardPlus,
    bool PerPixelLighting,
    bool MaterialInstances,
    bool HdrOutput,
    double Exposure,
    double WhitePoint,
    string LutResource,
    AreaVisualTuningProfile VisualTuning,
    IReadOnlyList<StudioUx2EnvDLightBudgetDraft> LightBudgets,
    SkyProfileMode SkyMode,
    string PanoramaResource,
    string ZenithColor,
    string HorizonColor,
    string GroundColor,
    double StarIntensity,
    double CloudCoverage,
    double MoonIntensity,
    double EnergyMultiplier,
    double TransitionSeconds,
    string SimulationPlatform,
    double SimulatedDynamicLights,
    double SimulatedShadowCastingLights,
    double SimulatedTransitionElapsedSeconds);

internal sealed record StudioUx2EnvDDiagnostic(
    string Code,
    string Path,
    string Message,
    bool Blocking = true);

internal sealed record StudioUx2EnvDDiagnosticGroup(
    string Key,
    string Title,
    string State,
    IReadOnlyList<StudioUx2EnvDDiagnostic> Diagnostics);

internal sealed record StudioUx2EnvDSemanticDiff(
    IReadOnlyList<string> Pbr,
    IReadOnlyList<string> Color,
    IReadOnlyList<string> Budgets,
    IReadOnlyList<string> Sky)
{
    internal bool HasChanges => Pbr.Count > 0 || Color.Count > 0 || Budgets.Count > 0 || Sky.Count > 0;
}

internal sealed record StudioUx2EnvDPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    StudioUx2EnvDSemanticDiff Diff,
    IReadOnlyList<StudioUx2EnvDDiagnosticGroup> Groups,
    bool CanonicalDryRun,
    bool RenderPlanBuilt,
    bool VisualQualityPlanBuilt,
    bool SkyTransitionPlanBuilt,
    bool BudgetDiagnosticsBuilt,
    bool AdapterInstantiationSkipped,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx2EnvDActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2EnvDApprovalSnapshot(
    StudioUx2EnvDDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2EnvDEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    IReadOnlyList<Forge.Domain.Entities.EntityId> SelectedEntityIds,
    Forge.Domain.Entities.EntityId? SelectedEntityId,
    StudioUx2EnvDApprovalSnapshot Approval);

internal sealed record StudioUx2EnvDSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool CompleteBudgets,
    bool InvalidRenderingBlocked,
    bool InvalidBudgetBlocked,
    bool InvalidSkyBlocked,
    bool DryRun,
    bool RenderPlans,
    bool PlanWarnings,
    bool StaleDraftBlocked,
    bool StaleFingerprintBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool LotBPreserved,
    bool LotCPreserved,
    bool EnvironmentTailsPreserved,
    bool AreaNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool StructuredDiagnostics,
    bool HumanText,
    bool Cleanup);
