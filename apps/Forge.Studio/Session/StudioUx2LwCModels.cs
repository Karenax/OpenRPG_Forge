using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx2LwCAreaChoice(
    AreaId AreaId,
    string Label,
    int EventCount,
    bool IsEvidenceTarget);

internal sealed record StudioUx2LwCEventChoice(
    Guid EventId,
    string Label,
    bool RequiresUpgrade);

internal sealed record StudioUx2LwCScriptChoice(
    string RelativePath,
    string DisplayName,
    IReadOnlyList<string> Handlers,
    string Provenance);

internal sealed record StudioUx2LwCPayloadFieldDraft(
    string Name,
    string Type,
    string Value);

internal sealed record StudioUx2LwCDraft(
    AreaId AreaId,
    Guid EventId,
    bool IsNew,
    bool Delete,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    string NamespacedName,
    string PayloadSchema,
    int PayloadVersion,
    string ForgeScriptApiVersion,
    string ScriptPath,
    IReadOnlyList<string> Subscriptions,
    bool Required,
    string Provenance,
    IReadOnlyList<StudioUx2LwCPayloadFieldDraft> PayloadFields);

internal sealed record StudioUx2LwCDiagnostic(
    string Code,
    string Path,
    string Message,
    bool Blocking = true);

internal sealed record StudioUx2LwCSemanticDiff(IReadOnlyList<string> Changes)
{
    internal bool HasChanges => Changes.Count > 0;
}

internal sealed record StudioUx2LwCPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    StudioUx2LwCSemanticDiff Diff,
    IReadOnlyList<StudioUx2LwCDiagnostic> Diagnostics,
    string CanonicalPatch,
    string PayloadSummary,
    string ScriptSummary,
    bool Compiled,
    bool Executed);

internal sealed record StudioUx2LwCActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2LwCApprovalSnapshot(
    StudioUx2LwCDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx2LwCEvidenceSourceSnapshot(
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
    StudioUx2LwCApprovalSnapshot Approval);

internal sealed record StudioUx2LwCSmokeResult(
    bool Succeeded,
    bool ExplicitAreaSelection,
    bool ExplicitEventSelection,
    bool RealScriptCatalog,
    bool InvalidPayloadBlocked,
    bool MissingScriptBlocked,
    bool MissingHandlerBlocked,
    bool LegacyDispatchBlocked,
    bool DryRunPure,
    bool CompiledWithoutExecution,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool TargetNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool CollectionPreserved,
    bool OptionalRemoval,
    bool StructuredDiagnostics,
    bool HumanText,
    bool SourceUntouched);
