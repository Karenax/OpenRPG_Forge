using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioUx2LwHEncounterChoice(
    Guid ProfileId,
    string Label,
    bool IsFoundationComplete,
    bool HasScenario,
    bool IsEvidence);

internal sealed record StudioUx2LwHReferenceChoice<T>(T Id, string Label, string Provenance);

internal sealed record StudioUx2LwHQuestChoice(
    Guid Id,
    string Label,
    string Provenance,
    IReadOnlyList<StudioUx2LwHReferenceChoice<string>> Objectives);

internal sealed record StudioUx2LwHDraft(
    Guid ProfileId,
    bool IsNew,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    Guid ScenarioId,
    string Provenance,
    EncounterSpawnDistribution Distribution,
    IReadOnlyList<EncounterScenarioProfile.SpawnAnchorPolicy> SpawnAnchors,
    EncounterScenarioProfile.ActivatorPolicy Activator,
    EncounterScenarioProfile.LifecyclePolicy Lifecycle,
    IReadOnlyList<EncounterScenarioProfile.PhasePolicy> Phases,
    IReadOnlyDictionary<string, string> Overrides,
    bool RequiredAtRuntime,
    EncounterWorldProfile OriginalProfile);

internal sealed record StudioUx2LwHDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2LwHDiff(string Section, string Before, string After, bool Changed);

internal sealed record StudioUx2LwHRuntimePreview(
    string Anchors,
    string Activator,
    string Lifecycle,
    string Waves,
    string Boundary);

internal sealed record StudioUx2LwHPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string CanonicalPatch,
    IReadOnlyList<StudioUx2LwHDiff> Diff,
    IReadOnlyList<StudioUx2LwHDiagnostic> Diagnostics,
    StudioUx2LwHRuntimePreview Runtime,
    bool DryRunPure);

internal sealed record StudioUx2LwHActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2LwHApprovalSnapshot(
    StudioUx2LwHDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx2LwHEvidenceSourceSnapshot(
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
    StudioUx2LwHApprovalSnapshot Approval);

internal sealed record StudioUx2LwHSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool ExplicitCreation,
    bool FoundationRequired,
    bool AnchorValidation,
    bool ActivatorValidation,
    bool LifecycleValidation,
    bool RealScriptValidation,
    bool WaveValidation,
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
