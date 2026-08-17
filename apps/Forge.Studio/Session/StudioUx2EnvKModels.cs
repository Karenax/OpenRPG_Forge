using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx2EnvKProfileChoice(string Label, int WaterBodyCount);

internal sealed record StudioUx2EnvKDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    bool Refraction,
    bool ScreenSpaceReflections,
    bool DepthColor,
    string FallbackQuality,
    string AntiAliasing,
    int AnisotropicLevel,
    string Upscaling,
    bool CapabilityFallback);

internal sealed record StudioUx2EnvKWaterPlan(
    string Area,
    string WaterBody,
    string Quality,
    string Material,
    string SurfaceEffects,
    string Physics);

internal sealed record StudioUx2EnvKGraphicsPlan(
    string Setting,
    string Requested,
    string Fallback,
    string RuntimeState);

internal sealed record StudioUx2EnvKDiagnostic(
    string Code,
    string Path,
    string Message,
    bool Blocking = true);

internal sealed record StudioUx2EnvKDiagnosticGroup(
    string Key,
    string Title,
    string State,
    IReadOnlyList<StudioUx2EnvKDiagnostic> Diagnostics);

internal sealed record StudioUx2EnvKDiff(IReadOnlyList<string> Entries)
{
    internal bool HasChanges => Entries.Count > 0;
}

internal sealed record StudioUx2EnvKPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    StudioUx2EnvKDiff Diff,
    IReadOnlyList<StudioUx2EnvKDiagnosticGroup> Groups,
    IReadOnlyList<StudioUx2EnvKWaterPlan> WaterPlans,
    IReadOnlyList<StudioUx2EnvKGraphicsPlan> GraphicsPlans,
    bool CanonicalDryRun,
    bool AreaWaterReadOnly,
    bool RendererMutationSkipped,
    bool HardwareDetectionSkipped,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx2EnvKActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2EnvKApprovalSnapshot(
    StudioUx2EnvKDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2EnvKEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    Forge.Domain.Areas.AreaId? ActiveAreaId,
    IReadOnlyList<Forge.Domain.Entities.EntityId> SelectedEntityIds,
    Forge.Domain.Entities.EntityId? SelectedEntityId,
    StudioUx2EnvKApprovalSnapshot Approval);

internal sealed record StudioUx2EnvKSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealModuleProfileDetected,
    bool RealModuleWaterDetected,
    bool InvalidWaterQualityBlocked,
    bool InvalidAntiAliasingBlocked,
    bool InvalidAnisotropyBlocked,
    bool InvalidUpscalingBlocked,
    bool CanonicalDryRun,
    bool WaterPlanBounded,
    bool GraphicsPlanBounded,
    bool StaleRevisionBlocked,
    bool ApprovalFingerprintBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool CreatorTailsPreserved,
    bool PostLaunchTailsPreserved,
    bool ExtensionTailsPreserved,
    bool RuntimePolicyTailsPreserved,
    bool ProjectNeighborsPreserved,
    bool StructuredDiagnostics,
    bool HumanText,
    bool BoundedRuntimeClaim,
    bool Cleanup);
