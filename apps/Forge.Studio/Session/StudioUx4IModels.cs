using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioCreatureLootChoiceUx4I(
    BlueprintId BlueprintId,
    string Label,
    bool HasLootProfile,
    bool HasCombatLoot,
    bool HasProgression,
    bool IsReadOnly);

internal sealed record StudioCreatureLootItemChoiceUx4I(string ItemSlug, string Label);
internal sealed record StudioCreatureLootSkillChoiceUx4I(string SkillId, string Label);

internal sealed record StudioCreatureLootDraftUx4I(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    BlueprintId BlueprintId,
    BlueprintDefinition SourceBlueprint,
    CreatureLootProfile SourceLoot,
    bool RequiredAtRuntime,
    IReadOnlyList<CreatureLootProfile.LootEntryPolicy> Entries,
    CreatureLootProfile.TheftProfile Theft,
    CreatureLootProfile.CreatureCorpseProfile Corpse);

internal sealed record StudioCreatureLootDiagnosticUx4I(string Code, string Path, string HumanMessage);

internal sealed record StudioCreatureLootPreviewUx4I(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioCreatureLootDiagnosticUx4I> Diagnostics,
    string DropSummary,
    string TheftSummary,
    string CorpseSummary,
    string RuntimeSummary);

internal sealed record StudioCreatureLootActionResultUx4I(bool Succeeded, string Message);

internal sealed record StudioCreatureLootApprovalSnapshotUx4I(
    StudioCreatureLootDraftUx4I? Draft,
    CreatureLootProfile? Candidate,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioCreatureLootEvidenceSourceSnapshotUx4I(
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
    StudioCreatureLootApprovalSnapshotUx4I Approval);

internal sealed record StudioCreatureLootEvidenceProjectUx4I(
    ProjectDefinition Project,
    BlueprintId BlueprintId,
    bool UsesRealProfile,
    bool UsesCanonicalFallback);

internal sealed record StudioUx4ISmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool Drops,
    bool Pickpocket,
    bool Corpse,
    bool InvalidConfigurationBlocked,
    bool MissingReferenceBlocked,
    bool DryRun,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);
