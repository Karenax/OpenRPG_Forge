using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioUx2LwGEncounterChoice(
    Guid ProfileId,
    string Label,
    bool IsComplete,
    bool IsEvidence);

internal sealed record StudioUx2LwGScriptChoice(
    string Id,
    string Label,
    string Provenance);

internal sealed record StudioUx2LwGDraft(
    Guid ProfileId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    Guid SelectionId,
    string PartyAssessmentProviderId,
    string Strategy,
    int DeterministicSeed,
    bool PreviewComposition,
    Guid ActivationId,
    EncounterActivationMode ActivationMode,
    double IntervalSeconds,
    string? ScriptId,
    EncounterSpawnDistribution Distribution,
    EncounterWorldProfile OriginalProfile);

internal sealed record StudioUx2LwGDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2LwGDiff(string Section, string Before, string After, bool Changed);

internal sealed record StudioUx2LwGRuntimePreview(
    string PartyAssessment,
    string Composition,
    string Activation,
    string Boundary);

internal sealed record StudioUx2LwGPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string CanonicalPatch,
    IReadOnlyList<StudioUx2LwGDiff> Diff,
    IReadOnlyList<StudioUx2LwGDiagnostic> Diagnostics,
    StudioUx2LwGRuntimePreview Runtime,
    bool DryRunPure);

internal sealed record StudioUx2LwGActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2LwGApprovalSnapshot(
    StudioUx2LwGDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx2LwGEvidenceSourceSnapshot(
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
    StudioUx2LwGApprovalSnapshot Approval);

internal sealed record StudioUx2LwGSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool FoundationRequired,
    bool StrategyValidation,
    bool ProviderGuard,
    bool ActivationValidation,
    bool RealScriptValidation,
    bool DeterministicPreview,
    bool DryRunPure,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool TargetNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool CollectionsPreserved,
    bool StructuredDiagnostics,
    bool HumanText);
