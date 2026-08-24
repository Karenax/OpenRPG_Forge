using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioUx2LwIEncounterChoice(
    Guid ProfileId,
    string Label,
    bool IsComplete,
    bool IsEvidence);

internal sealed record StudioUx2LwIFactionChoice(
    Guid Id,
    string Slug,
    string Label,
    string Provenance);

internal sealed record StudioUx2LwIDraft(
    Guid ProfileId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    bool RespawnEnabled,
    double DelaySeconds,
    int MaximumCycles,
    string Condition,
    bool ResetWhenEmpty,
    bool Persistent,
    double AntiFarmMultiplier,
    string? SpawnFactionSlug,
    bool SchedulerEnabled,
    bool HeartbeatEnabled,
    double IntervalSeconds,
    int BudgetPerMinute,
    string CustomEventNamespace,
    EncounterWorldProfile OriginalProfile);

internal sealed record StudioUx2LwIDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2LwIDiff(string Section, string Before, string After, bool Changed);

internal sealed record StudioUx2LwIRuntimePreview(
    string Respawn,
    string Faction,
    string Heartbeat,
    string CustomEvent,
    string Boundary);

internal sealed record StudioUx2LwIPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string CanonicalPatch,
    IReadOnlyList<StudioUx2LwIDiff> Diff,
    IReadOnlyList<StudioUx2LwIDiagnostic> Diagnostics,
    StudioUx2LwIRuntimePreview Runtime,
    bool DryRunPure);

internal sealed record StudioUx2LwIActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2LwIApprovalSnapshot(
    StudioUx2LwIDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx2LwIEvidenceSourceSnapshot(
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
    StudioUx2LwIApprovalSnapshot Approval);

internal sealed record StudioUx2LwISmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool FoundationRequired,
    bool OptionalFallback,
    bool RespawnValidation,
    bool FactionValidation,
    bool SchedulerValidation,
    bool RuntimeResolution,
    bool DormantPreserved,
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
