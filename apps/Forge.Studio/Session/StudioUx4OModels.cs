using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioCreatureDamageChoiceUx4O(
    BlueprintId BlueprintId,
    string Label,
    bool HasDamageProfile,
    bool IsReadOnly);

internal sealed record StudioCreatureDamageDraftUx4O(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    BlueprintId BlueprintId,
    BlueprintDefinition SourceBlueprint,
    CreatureEventProfile? SourceProfile,
    bool DamageDeathEnabled,
    string BeforeDamageHandler,
    string DamageAppliedHandler,
    string DeathHandler,
    string BeforeDamageBudgetMilliseconds,
    string MaximumAdjustmentRatio);

internal sealed record StudioCreatureDamageCandidateUx4O(CreatureEventProfile Profile);

internal sealed record StudioCreatureDamageDiagnosticUx4O(
    string Code,
    string Path,
    string HumanMessage);

internal sealed record StudioCreatureDamagePreviewUx4O(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioCreatureDamageDiagnosticUx4O> Diagnostics,
    string DamageSummary,
    string DeathSummary,
    string ValidationSummary,
    string RuntimeSummary);

internal sealed record StudioCreatureDamageActionResultUx4O(bool Succeeded, string Message);

internal sealed record StudioCreatureDamageApprovalSnapshotUx4O(
    StudioCreatureDamageDraftUx4O? Draft,
    StudioCreatureDamageCandidateUx4O? Candidate,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioCreatureDamageEvidenceProjectUx4O(
    ProjectDefinition Project,
    BlueprintId BlueprintId,
    bool UsesRealProfile,
    bool UsesCanonicalFallback);

internal sealed record StudioCreatureDamageEvidenceSourceSnapshotUx4O(
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
    StudioCreatureDamageApprovalSnapshotUx4O Approval);

internal sealed record StudioUx4OSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool DamageLifecycle,
    bool DeathPayload,
    bool CreatureValidation,
    bool OptionalRemoval,
    bool InvalidBudgetBlocked,
    bool InvalidRatioBlocked,
    bool DryRun,
    bool RuntimeProjection,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool Ux4NBindingsPreserved,
    bool TemporaryWorkspaceReleased);
