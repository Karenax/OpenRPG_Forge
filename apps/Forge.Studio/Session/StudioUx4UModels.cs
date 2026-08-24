using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioCompanionChoiceUx4U(Guid CompanionId, string Label, bool Complete);

internal sealed record StudioCompanionBlueprintChoiceUx4U(BlueprintId BlueprintId, string Label);

internal sealed record StudioCompanionDialogueChoiceUx4U(Guid DialogueId, string Label);

internal sealed record StudioCompanionQuestChoiceUx4U(Guid QuestId, string Label);

internal sealed record StudioCompanionItemChoiceUx4U(BlueprintId BlueprintId, string Label);

internal sealed record StudioCompanionAreaChoiceUx4U(AreaId AreaId, string Label);

internal sealed record StudioCompanionDraftUx4U(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    Guid CompanionId,
    CompanionWorldProfile? Original,
    CompanionWorldProfile Profile);

internal sealed record StudioCompanionDiagnosticUx4U(string Code, string Path, string HumanMessage);

internal sealed record StudioCompanionPreviewUx4U(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioCompanionDiagnosticUx4U> Diagnostics,
    string RecruitmentSummary,
    string ControlSummary,
    string NarrativeSummary,
    string ExperienceSummary,
    string RuntimeBoundary);

internal sealed record StudioCompanionActionResultUx4U(bool Succeeded, string Message);

internal sealed record StudioCompanionApprovalSnapshotUx4U(
    StudioCompanionDraftUx4U? Draft,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioCompanionEvidenceProjectUx4U(
    ProjectDefinition Project,
    Guid CompanionId,
    bool UsesRealModule,
    bool UsesCanonicalFallback);

internal sealed record StudioUx4USmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RecruitableDialogues,
    bool RecruitDismissParty,
    bool OwnershipMovement,
    bool TacticalAiFormation,
    bool EquipmentProgression,
    bool RelationshipBanterQuest,
    bool DeathCohesion,
    bool TrapAbilitiesDirectControl,
    bool HudPartySplit,
    bool Validation,
    bool DryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);

internal sealed record StudioCompanionEvidenceSourceSnapshotUx4U(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool Dirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    IReadOnlyList<EntityId> Selection,
    EntityId? ActiveEntity,
    StudioCompanionApprovalSnapshotUx4U Approval);
