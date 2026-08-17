using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioTriggerValidationChoice(
    Guid ProfileId,
    BlueprintId BlueprintId,
    string Label,
    bool HasFoundation,
    bool HasValidation,
    bool ValidationRequiredAtRuntime,
    bool IsEvidence);

internal sealed record StudioTriggerPortalDestinationChoice(
    Guid Id,
    string Label);

internal sealed record StudioTriggerValidationDraft(
    Guid TriggerProfileId,
    BlueprintId BlueprintId,
    AreaId AreaId,
    ProjectRevision BaseRevision,
    bool ValidationEnabled,
    bool ValidationRequiredAtRuntime,
    IReadOnlyList<Guid> KnownPortalDestinationIds,
    IReadOnlyList<string> KnownTargetIds,
    IReadOnlyList<string> KnownScriptIds,
    IReadOnlyList<string> AccessibleLayers,
    TriggerWorldProfile OriginalProfile);

internal sealed record StudioTriggerValidationDiagnostic(
    string Code,
    string Path,
    string Message);

internal sealed record StudioTriggerValidationDiagnosticGroup(
    string Key,
    string Title,
    string State,
    IReadOnlyList<string> Details,
    IReadOnlyList<StudioTriggerValidationDiagnostic> Diagnostics,
    bool Blocking);

internal sealed record StudioTriggerValidationPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    bool RuntimeAvailable,
    bool OptionalFallback,
    IReadOnlyList<StudioTriggerValidationDiagnosticGroup> Groups);

internal sealed record StudioUx1AIActionResult(bool Succeeded, string Message);

internal sealed record StudioUx1AIApprovalSnapshot(
    StudioTriggerValidationDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx1AIEvidenceSourceSnapshot(
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
    StudioUx1AIApprovalSnapshot Approval);

internal sealed record StudioUx1AISmokeResult(
    bool Succeeded,
    bool SelectionExplicit,
    bool FoundationRequired,
    bool ContextCreation,
    bool RequiredFlag,
    bool RuntimeResolution,
    bool DryRun,
    bool InvalidContextBlocked,
    bool MissingTargetBlocked,
    bool MissingScriptBlocked,
    bool MissingPortalBlocked,
    bool InaccessibleBlocked,
    bool OptionalSuppression,
    bool GroupedDiagnostics,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool FoundationNeighborsPreserved,
    bool TriggerNeighborsPreserved,
    bool HumanDiagnostics);
