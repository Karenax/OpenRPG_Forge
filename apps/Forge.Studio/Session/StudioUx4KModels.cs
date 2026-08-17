using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioCreatureMovementChoiceUx4K(BlueprintId BlueprintId, string Label, bool IsReadOnly);

internal sealed record StudioCreatureMovementDraftUx4K(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    BlueprintId BlueprintId,
    BlueprintDefinition SourceBlueprint,
    CreatureFoundationProfile SourceFoundation,
    CreatureFoundationMovementMode Mode,
    string MaximumSpeed,
    string Acceleration,
    string Deceleration,
    string MinimumAltitude,
    string MaximumAltitude,
    string MaximumSlopeDegrees);

internal sealed record StudioCreatureMovementDiagnosticUx4K(string Code, string Path, string HumanMessage);

internal sealed record StudioCreatureMovementPreviewUx4K(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioCreatureMovementDiagnosticUx4K> Diagnostics,
    string MovementSummary,
    string RuntimeSummary);

internal sealed record StudioCreatureMovementActionResultUx4K(bool Succeeded, string Message);

internal sealed record StudioCreatureMovementApprovalSnapshotUx4K(
    StudioCreatureMovementDraftUx4K? Draft,
    CreatureFoundationProfile? CandidateFoundation,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioCreatureMovementEvidenceProjectUx4K(
    ProjectDefinition Project,
    BlueprintId BlueprintId,
    bool UsesRealProfile,
    bool UsesCanonicalFallback);

internal sealed record StudioCreatureMovementEvidenceSourceSnapshotUx4K(
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
    StudioCreatureMovementApprovalSnapshotUx4K Approval);

internal sealed record StudioUx4KSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool SixModes,
    bool CompleteValues,
    bool InvalidConfigurationBlocked,
    bool FlightAltitudeBlocked,
    bool DryRun,
    bool RuntimeProjection,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);
