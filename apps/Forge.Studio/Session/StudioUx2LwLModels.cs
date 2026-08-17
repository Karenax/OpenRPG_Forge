using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioUx2LwLEncounterChoice(
    Guid ProfileId,
    string Label,
    bool HasFoundation,
    bool IsEvidence);

internal sealed record StudioUx2LwLReferenceChoice(
    string Id,
    string Label,
    string Provenance);

internal sealed record StudioUx2LwLDraft(
    Guid ProfileId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    IReadOnlyList<string> SpawnAnchorIds,
    string? ActivationScriptId,
    double SpawnBudget,
    int NetworkActorCap,
    EncounterWorldProfile OriginalProfile);

internal sealed record StudioUx2LwLDiagnostic(
    string Code,
    string Path,
    string Message,
    bool Blocking,
    string Section);

internal sealed record StudioUx2LwLCoverage(
    string Section,
    string Status,
    string Detail,
    bool Evaluated);

internal sealed record StudioUx2LwLDiff(
    string Section,
    string Before,
    string After,
    bool Changed);

internal sealed record StudioUx2LwLPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string CanonicalPatch,
    IReadOnlyList<StudioUx2LwLDiff> Diff,
    IReadOnlyList<StudioUx2LwLDiagnostic> Diagnostics,
    IReadOnlyList<StudioUx2LwLCoverage> Coverage,
    bool DryRunPure,
    string Boundary);

internal sealed record StudioUx2LwLActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2LwLApprovalSnapshot(
    StudioUx2LwLDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx2LwLEvidenceSourceSnapshot(
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
    StudioUx2LwLApprovalSnapshot Approval);

internal sealed record StudioUx2LwLSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool StructuredDiagnostics,
    bool CanonicalAggregation,
    bool RealAnchorValidation,
    bool RealScriptValidation,
    bool LifecycleValidation,
    bool TacticalValidation,
    bool HonestUnresolvedCoverage,
    bool CorrectionRequired,
    bool DryRunPure,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool TargetPreserved,
    bool EncounterNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool CollectionsPreserved,
    bool HumanText,
    bool RuntimeBoundary);
