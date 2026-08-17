using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioDoorValidationChoice(
    Guid ProfileId,
    BlueprintId BlueprintId,
    string Label,
    bool HasFoundation,
    bool HasNavigation,
    bool HasValidation,
    bool ValidationRequiredAtRuntime,
    bool IsEvidence);

internal sealed record StudioDoorValidationDraft(
    Guid DoorProfileId,
    BlueprintId BlueprintId,
    ProjectRevision BaseRevision,
    bool ValidationEnabled,
    bool ValidationRequiredAtRuntime,
    IReadOnlyList<Guid> KnownDestinationIds,
    IReadOnlyList<string> KnownAnimationIds,
    IReadOnlyList<string> KnownCollisionProfileIds,
    IReadOnlyList<string> KnownCredentialIds,
    IReadOnlyList<string> KnownScriptIds,
    DoorWorldProfile OriginalProfile);

internal sealed record StudioDoorValidationSuggestion(
    string Value,
    string Label,
    string Provenance);

internal sealed record StudioDoorDestinationSuggestion(
    Guid Id,
    string Label,
    string Provenance,
    bool Protected);

internal sealed record StudioDoorAuthoredCollision(
    string ProfileId,
    string Label,
    string Provenance);

internal sealed record StudioDoorValidationSuggestions(
    IReadOnlyList<StudioDoorDestinationSuggestion> Destinations,
    IReadOnlyList<StudioDoorValidationSuggestion> Animations,
    IReadOnlyList<StudioDoorValidationSuggestion> CollisionProfiles,
    IReadOnlyList<StudioDoorValidationSuggestion> Credentials,
    IReadOnlyList<StudioDoorValidationSuggestion> Scripts,
    StudioDoorAuthoredCollision? AuthoredCollision);

internal sealed record StudioDoorValidationDiagnostic(
    string Code,
    string Path,
    string Message);

internal sealed record StudioDoorValidationDiagnosticGroup(
    string Key,
    string Title,
    string State,
    IReadOnlyList<string> Details,
    IReadOnlyList<StudioDoorValidationDiagnostic> Diagnostics,
    bool Blocking);

internal sealed record StudioDoorValidationPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    bool RuntimeAvailable,
    bool OptionalFallback,
    IReadOnlyList<StudioDoorValidationDiagnosticGroup> Groups);

internal sealed record StudioUx1AJActionResult(bool Succeeded, string Message);

internal sealed record StudioUx1AJApprovalSnapshot(
    StudioDoorValidationDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx1AJEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    Forge.Domain.Areas.AreaId? ActiveAreaId,
    IReadOnlyList<EntityId> SelectedEntityIds,
    EntityId? SelectedEntityId,
    StudioUx1AJApprovalSnapshot Approval);

internal sealed record StudioUx1AJSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool PrerequisitesRequired,
    bool HumanSuggestions,
    bool ProtectedDestinations,
    bool OptionalNotEvaluated,
    bool RequiredAbsentBlocked,
    bool DestinationMissingBlocked,
    bool AnimationMissingBlocked,
    bool RejectionAnimationBlocked,
    bool CollisionAuthoredMissingBlocked,
    bool CollisionMismatchBlocked,
    bool CollisionMissingBlocked,
    bool CredentialMissingBlocked,
    bool ScriptMissingBlocked,
    bool OrphanLinkBlocked,
    bool ReverseLinkBlocked,
    bool DryRun,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool OptionalSuppression,
    bool Persistence,
    bool DoorNeighborsPreserved,
    bool CatalogNeighborsPreserved,
    bool StructuredHumanDiagnostics);
