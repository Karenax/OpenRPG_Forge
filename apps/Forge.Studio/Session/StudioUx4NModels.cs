using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioCreatureEventChoiceUx4N(
    BlueprintId BlueprintId,
    string Label,
    bool HasProfile,
    bool IsReadOnly);

internal sealed record StudioCreatureEventDraftUx4N(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    BlueprintId BlueprintId,
    BlueprintDefinition SourceBlueprint,
    CreatureEventProfile? SourceProfile,
    bool RequiredAtRuntime,
    bool CombatEnabled,
    CreatureCombatBoundaryMode CombatMode,
    string CombatHandler,
    bool InventoryEnabled,
    string InventoryAccessedHandler,
    string ItemTransferredHandler,
    bool RestEnabled,
    string RestHandler,
    bool HeartbeatEnabled,
    string HeartbeatIntervalMilliseconds,
    string HeartbeatBudgetMilliseconds,
    bool SuspendOutsideInterest,
    string HeartbeatHandler,
    string CustomEvents);

internal sealed record StudioCreatureEventCandidateUx4N(CreatureEventProfile Profile);

internal sealed record StudioCreatureEventDiagnosticUx4N(
    string Code,
    string Path,
    string HumanMessage);

internal sealed record StudioCreatureEventPreviewUx4N(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioCreatureEventDiagnosticUx4N> Diagnostics,
    string LifecycleSummary,
    string HeartbeatSummary,
    string CustomSummary,
    string RuntimeSummary);

internal sealed record StudioCreatureEventActionResultUx4N(bool Succeeded, string Message);

internal sealed record StudioCreatureEventApprovalSnapshotUx4N(
    StudioCreatureEventDraftUx4N? Draft,
    StudioCreatureEventCandidateUx4N? Candidate,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioCreatureEventEvidenceProjectUx4N(
    ProjectDefinition Project,
    BlueprintId BlueprintId,
    bool UsesRealProfile,
    bool UsesCanonicalFallback);

internal sealed record StudioCreatureEventEvidenceSourceSnapshotUx4N(
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
    StudioCreatureEventApprovalSnapshotUx4N Approval);

internal sealed record StudioUx4NSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool CombatBoundary,
    bool Inventory,
    bool Rest,
    bool Heartbeat,
    bool CustomEvents,
    bool InvalidConfigurationBlocked,
    bool DuplicateBlocked,
    bool DryRun,
    bool RuntimeProjection,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool DamageDeathPreserved,
    bool TemporaryWorkspaceReleased);
