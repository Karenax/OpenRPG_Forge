using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioUx2LwJEncounterChoice(
    Guid ProfileId,
    string Label,
    bool IsComplete,
    bool IsEvidence);

internal sealed record StudioUx2LwJFactionChoice(
    string Slug,
    string Label,
    string Provenance);

internal sealed record StudioUx2LwJRouteChoice(
    Guid Id,
    string Label,
    string Provenance,
    int PointCount);

internal sealed record StudioUx2LwJDraft(
    Guid ProfileId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    bool ReinforcementEnabled,
    int MinimumAlertLevel,
    string? FactionSlug,
    Guid? RouteId,
    int MaximumActors,
    double ReinforcementDelaySeconds,
    string CancelCondition,
    EncounterWorldProfile.VisibilityMode VisibilityMode,
    double MinimumCameraDistance,
    bool RequireLineOfSightBlocked,
    string SpawnEffectId,
    double DeadAfterSeconds,
    double EscapedAfterSeconds,
    double AbandonedAfterSeconds,
    bool CleanupOnAreaUnload,
    bool PreservePersistentActors,
    EncounterWorldProfile OriginalProfile);

internal sealed record StudioUx2LwJDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2LwJDiff(string Section, string Before, string After, bool Changed);

internal sealed record StudioUx2LwJRuntimePreview(
    string Reinforcement,
    string Visibility,
    string Cleanup,
    string Boundary);

internal sealed record StudioUx2LwJPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string CanonicalPatch,
    IReadOnlyList<StudioUx2LwJDiff> Diff,
    IReadOnlyList<StudioUx2LwJDiagnostic> Diagnostics,
    StudioUx2LwJRuntimePreview Runtime,
    bool DryRunPure);

internal sealed record StudioUx2LwJActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2LwJApprovalSnapshot(
    StudioUx2LwJDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx2LwJEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    IReadOnlyList<EntityId> SelectedEntityIds,
    EntityId? SelectedEntityId,
    StudioUx2LwJApprovalSnapshot Approval);

internal sealed record StudioUx2LwJSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool FoundationRequired,
    bool RealReferencesOnly,
    bool OptionalFallback,
    bool ReinforcementValidation,
    bool VisibilityValidation,
    bool CleanupValidation,
    bool RuntimeResolution,
    bool DryRunPure,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool TargetNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool CollectionsPreserved,
    bool StructuredDiagnostics,
    bool HumanText);
