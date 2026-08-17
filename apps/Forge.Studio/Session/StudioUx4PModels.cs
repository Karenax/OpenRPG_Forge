using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioCreatureEventAiChoiceUx4P(
    BlueprintId BlueprintId,
    string Label,
    bool HasFoundation,
    bool HasProfile,
    bool IsReadOnly);

internal sealed record StudioCreatureEventAiDraftUx4P(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    BlueprintId BlueprintId,
    BlueprintDefinition SourceBlueprint,
    CreatureFoundationProfile SourceFoundation,
    CreatureEventAiCompositionProfile? SourceProfile,
    bool Enabled,
    bool RequiredAtRuntime,
    string AssetId,
    string PerceptionDetectedHandler,
    string PerceptionLostHandler,
    string AttackDeclaredHandler,
    string AttackResolvedHandler,
    string AbilityTargetedHandler,
    string AbilityImpactedHandler,
    string BlockedHandler,
    string RecoveryOrder,
    CreatureEventAiControllerKind ControllerKind,
    string ControllerDefinitionId,
    string Blackboard,
    string AllowedActions,
    string Overrides);

internal sealed record StudioCreatureEventAiCandidateUx4P(CreatureFoundationProfile Foundation);

internal sealed record StudioCreatureEventAiDiagnosticUx4P(string Code, string Path, string HumanMessage);

internal sealed record StudioCreatureEventAiPreviewUx4P(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioCreatureEventAiDiagnosticUx4P> Diagnostics,
    string PerceptionSummary,
    string CombatSummary,
    string BlockedSummary,
    string ControllerSummary,
    string RuntimeSummary);

internal sealed record StudioCreatureEventAiActionResultUx4P(bool Succeeded, string Message);

internal sealed record StudioCreatureEventAiApprovalSnapshotUx4P(
    StudioCreatureEventAiDraftUx4P? Draft,
    StudioCreatureEventAiCandidateUx4P? Candidate,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioCreatureEventAiEvidenceProjectUx4P(
    ProjectDefinition Project,
    BlueprintId BlueprintId,
    bool UsesRealProfile,
    bool UsesCanonicalFallback);

internal sealed record StudioCreatureEventAiEvidenceSourceSnapshotUx4P(
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
    StudioCreatureEventAiApprovalSnapshotUx4P Approval);

internal sealed record StudioUx4PSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool Perception,
    bool PhysicalAttack,
    bool Ability,
    bool BlockedRecovery,
    bool AiController,
    bool OptionalRemoval,
    bool InvalidBindingBlocked,
    bool InvalidBlackboardBlocked,
    bool InvalidRecoveryBlocked,
    bool DryRun,
    bool RuntimeProjection,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);
