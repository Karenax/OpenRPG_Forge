using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Items;

namespace Forge.Studio.Session;

internal sealed record StudioItemChoiceUx4S(
    BlueprintId BlueprintId,
    string Label,
    bool HasFoundation,
    bool HasAdvanced,
    bool ReadOnly);

internal sealed record StudioItemDraftUx4S(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    BlueprintId BlueprintId,
    BlueprintDefinition? OriginalBlueprint,
    string Slug,
    string DisplayName,
    ItemFoundationProfile Foundation,
    ItemAdvancedProfile Advanced,
    bool AcceptTypeMigration);

internal sealed record StudioItemDiagnosticUx4S(string Code, string Path, string HumanMessage);

internal sealed record StudioItemPreviewUx4S(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioItemDiagnosticUx4S> Diagnostics,
    string FoundationSummary,
    string GameplaySummary,
    string AdvancedSummary,
    string OperationsSummary,
    string RuntimeSummary);

internal sealed record StudioItemActionResultUx4S(bool Succeeded, string Message);

internal sealed record StudioItemApprovalSnapshotUx4S(
    StudioItemDraftUx4S? Draft,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioItemEvidenceProjectUx4S(
    Forge.Domain.Projects.ProjectDefinition Project,
    BlueprintId BlueprintId,
    bool UsesRealModule,
    bool UsesCanonicalFallback);

internal sealed record StudioUx4SSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool Creation,
    bool IdentityLocalization,
    bool AppearanceInventoryUsage,
    bool Gameplay,
    bool EquipmentCombat,
    bool LootCraftGeneration,
    bool InspectorEvents,
    bool Validation,
    bool DryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);

internal sealed record StudioItemEvidenceSourceSnapshotUx4S(
    Forge.Domain.Projects.ProjectDefinition Project,
    Forge.Application.Commands.ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
    bool Dirty,
    bool CanUndo,
    bool CanRedo,
    Forge.Domain.Areas.AreaId? ActiveAreaId,
    IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
    Forge.Domain.Entities.EntityId? ActiveEntity,
    StudioItemApprovalSnapshotUx4S Approval);
