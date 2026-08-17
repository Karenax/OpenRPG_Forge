using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx2EnvIAreaChoice(
    AreaId AreaId,
    string Label,
    int CueCount,
    int ScatterCount);

internal sealed record StudioUx2EnvICueChoice(string CueId, string Label);

internal sealed record StudioUx2EnvIScatterDraft(
    Guid Id,
    string CueId,
    string CenterX,
    string CenterY,
    string CenterZ,
    string SizeX,
    string SizeY,
    string SizeZ,
    bool SurfaceOnly,
    string DeterministicSeed,
    string MinimumIntervalSeconds,
    string MaximumIntervalSeconds,
    string Volume,
    bool RequireNavigable,
    string BiomeTags,
    string ExclusionTags,
    string Provenance);

internal sealed record StudioUx2EnvIDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    string AreaSlug,
    Guid? SelectedEmitterId,
    IReadOnlyList<StudioUx2EnvIScatterDraft> ScatterEmitters);

internal sealed record StudioUx2EnvIScatterSample(
    string Emitter,
    int Sequence,
    string Position,
    string Delay,
    string Volume,
    string FilterState);

internal sealed record StudioUx2EnvIFilterProbe(
    string Scenario,
    string Result,
    string Explanation);

internal sealed record StudioUx2EnvIEventStep(
    string Operation,
    string EventType,
    string Cue,
    string RuntimeState,
    string Detail);

internal sealed record StudioUx2EnvIScriptSnapshot(
    int SchemaVersion,
    int CueCount,
    int ScatterCount,
    int VoiceCount,
    int PatrolCount,
    bool CopiedValues,
    bool PersistentMutationExcluded);

internal sealed record StudioUx2EnvIDiagnostic(
    string Code,
    string Path,
    string Message,
    bool Blocking = true);

internal sealed record StudioUx2EnvIDiagnosticGroup(
    string Key,
    string Title,
    string State,
    IReadOnlyList<StudioUx2EnvIDiagnostic> Diagnostics);

internal sealed record StudioUx2EnvIDiff(IReadOnlyList<string> Entries)
{
    internal bool HasChanges => Entries.Count > 0;
}

internal sealed record StudioUx2EnvIPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    StudioUx2EnvIDiff Diff,
    IReadOnlyList<StudioUx2EnvIDiagnosticGroup> Groups,
    IReadOnlyList<StudioUx2EnvIScatterSample> Samples,
    IReadOnlyList<StudioUx2EnvIFilterProbe> FilterProbes,
    IReadOnlyList<StudioUx2EnvIEventStep> Events,
    StudioUx2EnvIScriptSnapshot? ScriptSnapshot,
    bool CanonicalDryRun,
    bool DeterministicScatter,
    bool TypedEvents,
    bool PlayerInstantiationSkipped,
    bool ScriptDispatchSkipped,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx2EnvIActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2EnvIApprovalSnapshot(
    StudioUx2EnvIDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2EnvIEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    IReadOnlyList<Forge.Domain.Entities.EntityId> SelectedEntityIds,
    Forge.Domain.Entities.EntityId? SelectedEntityId,
    StudioUx2EnvIApprovalSnapshot Approval);

internal sealed record StudioUx2EnvISmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealModuleScatterDetected,
    bool InvalidCueBlocked,
    bool InvalidGeometryBlocked,
    bool InvalidIntervalBlocked,
    bool NonFiniteBlocked,
    bool InvalidVolumeBlocked,
    bool CanonicalDryRun,
    bool DeterministicScatter,
    bool SurfaceAndVolumeScatter,
    bool NavigationBiomeExclusion,
    bool TypedEventSequence,
    bool ScriptSnapshotCopied,
    bool StaleRevisionBlocked,
    bool ApprovalFingerprintBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool ExperienceTailsPreserved,
    bool RuntimePolicyTailsPreserved,
    bool AreaNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool StructuredDiagnostics,
    bool HumanText,
    bool BoundedRuntimeClaim,
    bool Cleanup);
