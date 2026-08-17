using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx2EnvJAreaChoice(
    AreaId AreaId,
    string Label,
    int FloorCount,
    int PinCount,
    int LodOverrideCount,
    AreaLodSourceMode LodMode);

internal sealed record StudioUx2EnvJPinDraft(
    Guid Id,
    string Label,
    string X,
    string Z,
    string Floor,
    bool InitiallyVisible);

internal sealed record StudioUx2EnvJLodOverrideDraft(
    Guid Id,
    string AssetId,
    AreaLodSourceMode SourceMode,
    string ScreenRatios,
    string TriangleBudgets,
    string CrossFadeSeconds,
    string Provenance);

internal sealed record StudioUx2EnvJDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    string AreaSlug,
    bool MapEnabled,
    bool FogOfWar,
    string Projection,
    string FloorCount,
    bool Generate2D,
    bool Generate3D,
    AreaMapExplorationScope ExplorationScope,
    string ExplorationCellSize,
    string RevealApi,
    IReadOnlyList<StudioUx2EnvJPinDraft> Pins,
    AreaLodSourceMode DefaultSourceMode,
    string DistanceThresholds,
    string MaximumVisibleTriangles,
    string MaximumHlodClusters,
    string HlodClusterSize,
    string PoppingTolerance,
    bool CrossFade,
    bool MeasurePopping,
    IReadOnlyList<StudioUx2EnvJLodOverrideDraft> AssetOverrides);

internal sealed record StudioUx2EnvJMapFloorPlan(
    int Floor,
    int PinCount,
    int ExplorationCellCount,
    bool Generate2D,
    bool Generate3D,
    bool FogOfWar);

internal sealed record StudioUx2EnvJRevealPlan(
    string Scope,
    int RevealedCellCount,
    int RevealedPinCount,
    string Summary);

internal sealed record StudioUx2EnvJHlodHookPlan(
    int CellX,
    int CellZ,
    int SourceEntityCount,
    int MaximumTriangles,
    string Operation);

internal sealed record StudioUx2EnvJPerformancePlan(
    int EntityCount,
    int UniqueAssetCount,
    int EstimatedDrawUnits,
    int OccupiedSectors,
    int NearLodCount,
    int MidLodCount,
    int FarLodCount,
    string EstimatedPopping,
    bool WithinBudget,
    IReadOnlyList<string> Diagnostics);

internal sealed record StudioUx2EnvJDiagnostic(
    string Code,
    string Path,
    string Message,
    bool Blocking = true);

internal sealed record StudioUx2EnvJDiagnosticGroup(
    string Key,
    string Title,
    string State,
    IReadOnlyList<StudioUx2EnvJDiagnostic> Diagnostics);

internal sealed record StudioUx2EnvJDiff(IReadOnlyList<string> Entries)
{
    internal bool HasChanges => Entries.Count > 0;
}

internal sealed record StudioUx2EnvJPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    StudioUx2EnvJDiff Diff,
    IReadOnlyList<StudioUx2EnvJDiagnosticGroup> Groups,
    IReadOnlyList<StudioUx2EnvJMapFloorPlan> Floors,
    StudioUx2EnvJRevealPlan? Reveal,
    IReadOnlyList<StudioUx2EnvJHlodHookPlan> HlodHooks,
    StudioUx2EnvJPerformancePlan? Performance,
    bool CanonicalDryRun,
    bool RenderPlanBuilt,
    bool RevealPlanBuilt,
    bool HlodPlanBuilt,
    bool PerformancePlanBuilt,
    bool GenerationSkipped,
    bool PlayerStateMutationSkipped,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx2EnvJActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2EnvJApprovalSnapshot(
    StudioUx2EnvJDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2EnvJEvidenceSourceSnapshot(
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
    StudioUx2EnvJApprovalSnapshot Approval);

internal sealed record StudioUx2EnvJSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealMapSourceDetected,
    bool InvalidMapBlocked,
    bool InvalidPinBlocked,
    bool InvalidLodBlocked,
    bool InvalidOverrideBlocked,
    bool HlodBudgetBlocked,
    bool CanonicalDryRun,
    bool MapRenderPlan,
    bool RevealPlan,
    bool DeterministicHlodPlan,
    bool PerformancePlan,
    bool NoRuntimeClaim,
    bool StaleRevisionBlocked,
    bool ApprovalFingerprintBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool MapIdentityPreserved,
    bool AuthoringTailsPreserved,
    bool ExperienceTailsPreserved,
    bool AreaNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool StructuredDiagnostics,
    bool HumanText,
    bool Cleanup);
