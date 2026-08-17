using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx2LwBAreaChoice(
    AreaId AreaId,
    string Label,
    bool IsEvidenceTarget);

internal sealed record StudioUx2LwBBindingDraft(
    bool Enabled,
    Guid BindingId,
    string EventName,
    string ForgeScriptApiVersion,
    string? ScriptPath,
    bool Required,
    string Provenance);

internal sealed record StudioUx2LwBDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    StudioUx2LwBBindingDraft Entered,
    StudioUx2LwBBindingDraft Exited);

internal sealed record StudioUx2LwBDiagnostic(
    string Code,
    string Path,
    string Message,
    bool Blocking = true);

internal sealed record StudioUx2LwBEventObservation(
    string EventName,
    string HumanEventName,
    bool ContractValidated,
    bool RuntimeEventObserved,
    string? HumanScriptName,
    bool ScriptRequired,
    string Summary);

internal sealed record StudioUx2LwBSemanticDiff(
    IReadOnlyList<string> Changes)
{
    internal bool HasChanges => Changes.Count > 0;
}

internal sealed record StudioUx2LwBPreview(
    bool Succeeded,
    string Message,
    bool CanApply,
    StudioUx2LwBSemanticDiff Diff,
    IReadOnlyList<StudioUx2LwBDiagnostic> Diagnostics,
    IReadOnlyList<StudioUx2LwBEventObservation> Observations,
    string CanonicalPatch);

internal sealed record StudioUx2LwBActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2LwBApprovalSnapshot(
    StudioUx2LwBDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx2LwBEvidenceSourceSnapshot(
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
    StudioUx2LwBApprovalSnapshot Approval);

internal sealed record StudioUx2LwBSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealScriptCatalog,
    bool RequiredScriptBlocked,
    bool MissingScriptBlocked,
    bool DryRun,
    bool EnteredObserved,
    bool ExitedObserved,
    bool NoScriptExecutionClaim,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool TargetNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool OptionalRemoval,
    bool StructuredDiagnostics,
    bool HumanText);
