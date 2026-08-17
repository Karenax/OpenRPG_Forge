using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioCreatureInteractionChoiceUx4J(
    BlueprintId BlueprintId,
    string Label,
    bool HasProfile,
    bool IsReadOnly);

internal sealed record StudioCreatureInteractionFactionChoiceUx4J(Guid FactionId, string Label);
internal sealed record StudioCreatureInteractionDialogueChoiceUx4J(DialogueId DialogueId, string Label);

internal sealed record StudioCreatureInteractionDraftUx4J(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    BlueprintId BlueprintId,
    BlueprintDefinition SourceBlueprint,
    CreatureFoundationProfile SourceFoundation,
    CreatureInteractionCompositionProfile SourceProfile,
    bool RequiredAtRuntime,
    CreatureInteractionFactionProfile Factions,
    CreatureInteractionPerceptionProfile Perception,
    IReadOnlyList<CreatureInteractionDialogueBinding> DialogueBindings,
    CreatureInteractionProtectionPolicy Protection);

internal sealed record StudioCreatureInteractionDiagnosticUx4J(string Code, string Path, string HumanMessage);

internal sealed record StudioCreatureInteractionPreviewUx4J(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioCreatureInteractionDiagnosticUx4J> Diagnostics,
    string FactionSummary,
    string PerceptionSummary,
    string DialogueSummary,
    string ProtectionSummary,
    string RuntimeSummary);

internal sealed record StudioCreatureInteractionActionResultUx4J(bool Succeeded, string Message);

internal sealed record StudioCreatureInteractionApprovalSnapshotUx4J(
    StudioCreatureInteractionDraftUx4J? Draft,
    CreatureFoundationProfile? CandidateFoundation,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioCreatureInteractionEvidenceSourceSnapshotUx4J(
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
    StudioCreatureInteractionApprovalSnapshotUx4J Approval);

internal sealed record StudioCreatureInteractionEvidenceProjectUx4J(
    ProjectDefinition Project,
    BlueprintId BlueprintId,
    bool UsesRealProfile,
    bool UsesCanonicalFallback);

internal sealed record StudioUx4JSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool Factions,
    bool Perception,
    bool Dialogue,
    bool Protection,
    bool InvalidConfigurationBlocked,
    bool MissingFactionBlocked,
    bool MissingDialogueBlocked,
    bool DryRun,
    bool RuntimeProjection,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);
