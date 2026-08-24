using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Rules;

namespace Forge.Studio.Session;

internal sealed record StudioUx2LwDAreaChoice(
    AreaId AreaId,
    string Label,
    bool IsEvidenceTarget);

internal sealed record StudioUx2LwDDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    AreaPvpMode PvpMode,
    FriendlyFireMode FriendlyFire,
    bool CrimeEnabled,
    bool AggroEnabled,
    bool EnvironmentalDamageEnabled,
    bool ServerAuthoritative,
    string Provenance);

internal sealed record StudioUx2LwDDiagnostic(
    string Code,
    string Path,
    string Message,
    bool Blocking = true);

internal sealed record StudioUx2LwDSemanticDiff(IReadOnlyList<string> Changes)
{
    internal bool HasChanges => Changes.Count > 0;
}

internal sealed record StudioUx2LwDRuntimeProjection(
    string Authority,
    string Pvp,
    string FriendlyFire,
    string Crime,
    string Aggro,
    string EnvironmentalDamage,
    string Boundary,
    bool EvaluatedLive);

internal sealed record StudioUx2LwDPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    StudioUx2LwDSemanticDiff Diff,
    IReadOnlyList<StudioUx2LwDDiagnostic> Diagnostics,
    string CanonicalPatch,
    StudioUx2LwDRuntimeProjection Runtime);

internal sealed record StudioUx2LwDActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2LwDApprovalSnapshot(
    StudioUx2LwDDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx2LwDEvidenceSourceSnapshot(
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
    StudioUx2LwDApprovalSnapshot Approval);

internal sealed record StudioUx2LwDSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool DryRunPure,
    bool RuntimeProjection,
    bool ServerAuthorityDiagnostic,
    bool AggroPolicyApplied,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool TargetNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool HeartbeatsPreserved,
    bool RestPreserved,
    bool CinematicsPreserved,
    bool PerceptionPreserved,
    bool ExperiencePreserved,
    bool StructuredDiagnostics,
    bool HumanText,
    bool SourceUntouched);
