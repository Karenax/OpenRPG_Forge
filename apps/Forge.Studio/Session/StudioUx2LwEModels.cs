using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioUx2LwETriggerChoice(
    Guid ProfileId,
    BlueprintId BlueprintId,
    string Label,
    bool HasFoundation,
    bool HasRest,
    bool HasClue,
    bool HasNarrative,
    bool IsEvidence);

internal sealed record StudioUx2LwEReferenceChoice<T>(
    T Id,
    string Label,
    string Provenance);

internal sealed record StudioUx2LwEDraft(
    Guid ProfileId,
    BlueprintId BlueprintId,
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    bool RestEnabled,
    bool RestSafe,
    string DangerQuery,
    bool WholePartyRequired,
    bool AllowCampSetup,
    string AllowedMessageKey,
    string DeniedMessageKey,
    bool ClueEnabled,
    string SkillId,
    int Difficulty,
    string TraceId,
    double DirectionDegrees,
    double AgeHours,
    string? FalseClueId,
    Guid? QuestId,
    bool NarrativeEnabled,
    IReadOnlyList<BlueprintId> Participants,
    int Priority,
    bool Once,
    double CooldownSeconds,
    string MultiplayerPolicy,
    Guid? DialogueId,
    TriggerWorldProfile OriginalProfile);

internal sealed record StudioUx2LwEDiagnostic(
    string Code,
    string Path,
    string Message);

internal sealed record StudioUx2LwEDiff(
    string Section,
    string Before,
    string After,
    bool Changed);

internal sealed record StudioUx2LwEPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string CanonicalPatch,
    IReadOnlyList<StudioUx2LwEDiff> Diff,
    IReadOnlyList<StudioUx2LwEDiagnostic> Diagnostics,
    string RestSummary,
    string ClueSummary,
    string NarrativeSummary,
    bool DryRunPure);

internal sealed record StudioUx2LwEActionResult(
    bool Succeeded,
    string Message);

internal sealed record StudioUx2LwEApprovalSnapshot(
    StudioUx2LwEDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx2LwEEvidenceSourceSnapshot(
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
    StudioUx2LwEApprovalSnapshot Approval);

internal sealed record StudioUx2LwESmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool FoundationRequired,
    bool RestValidation,
    bool ClueValidation,
    bool NarrativeValidation,
    bool RuntimePreview,
    bool OptionalRemoval,
    bool DryRunPure,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool TargetNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool CollectionPreserved,
    bool StructuredDiagnostics,
    bool HumanText);
