using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioCreatureCapabilityChoiceUx4H(
    BlueprintId BlueprintId,
    string Label,
    bool HasFoundation,
    bool HasRules,
    bool IsReadOnly);

internal sealed record StudioItemChoiceUx4H(BlueprintId BlueprintId, string Label);

internal sealed record StudioCreatureCapabilityDraftUx4H(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    BlueprintId BlueprintId,
    BlueprintDefinition SourceBlueprint,
    CreatureFoundationProfile SourceFoundation,
    CreatureRulesCompositionProfile SourceRules,
    IReadOnlyList<CreatureRulesAbilityInstance> Abilities,
    IReadOnlyList<CreatureRulesInventoryContainer> Containers,
    IReadOnlyDictionary<string, Guid> EquipmentSlots,
    string OwnerId,
    bool Persistent);

internal sealed record StudioCreatureCapabilityDiagnosticUx4H(string Code, string Path, string HumanMessage);

internal sealed record StudioCreatureCapabilityPreviewUx4H(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioCreatureCapabilityDiagnosticUx4H> Diagnostics,
    string AbilitySummary,
    string InventorySummary,
    string RuntimeSummary);

internal sealed record StudioCreatureCapabilityActionResultUx4H(bool Succeeded, string Message);

internal sealed record StudioCreatureCapabilityApprovalSnapshotUx4H(
    StudioCreatureCapabilityDraftUx4H? Draft,
    CreatureFoundationProfile? Candidate,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioCreatureCapabilityEvidenceSourceSnapshotUx4H(
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
    StudioCreatureCapabilityApprovalSnapshotUx4H Approval);

internal sealed record StudioCreatureCapabilityEvidenceProjectUx4H(
    ProjectDefinition Project,
    BlueprintId BlueprintId,
    bool UsesRealProfile,
    bool UsesCanonicalFallback);

internal sealed record StudioUx4HSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool Abilities,
    bool Inventory,
    bool Equipment,
    bool InvalidConfigurationBlocked,
    bool MissingItemBlocked,
    bool DryRun,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);
