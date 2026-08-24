using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx2EnvEAreaChoice(
    AreaId AreaId,
    string Label,
    int AdaptiveLayerCount,
    int MusicStateCount,
    int MixBusCount,
    int SnapshotCount);

internal sealed record StudioUx2EnvEAdaptiveLayerDraft(
    string Resource,
    double StartHour,
    double EndHour,
    string Weather,
    string LocationTag,
    double MinimumDanger,
    bool IndoorOnly,
    double Volume,
    double CrossfadeSeconds,
    string VolumeId);

internal sealed record StudioUx2EnvEMusicStateDraft(
    MusicState State,
    string Track,
    IReadOnlyList<string> Stems,
    double TransitionSeconds,
    double CooldownSeconds,
    bool BeatSynchronized,
    double TempoBpm,
    double BeatsPerBar);

internal sealed record StudioUx2EnvEMixBusDraft(
    Guid Id,
    string Name,
    Guid? ParentId,
    double Volume,
    string Snapshot,
    string Platform,
    double DynamicRange,
    bool EnhanceImportantSounds,
    bool MonitorEnabled);

internal sealed record StudioUx2EnvEAudioDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    string AreaSlug,
    string DayMusic,
    string NightMusic,
    string CombatMusic,
    string Ambience,
    double TransitionSeconds,
    IReadOnlyList<string> Playlist,
    AudioOrderMode OrderMode,
    double IntervalSeconds,
    double IntervalVariationSeconds,
    double RelativeVolume,
    IReadOnlyList<StudioUx2EnvEAdaptiveLayerDraft> AdaptiveLayers,
    IReadOnlyList<StudioUx2EnvEMusicStateDraft> MusicStates,
    IReadOnlyList<StudioUx2EnvEMixBusDraft> MixBuses);

internal sealed record StudioUx2EnvESnapshotBusDraft(string BusName, double Volume);

internal sealed record StudioUx2EnvESnapshotDraft(
    Guid Id,
    string Name,
    IReadOnlyList<StudioUx2EnvESnapshotBusDraft> BusVolumes,
    double TransitionSeconds,
    string TriggerEvent,
    string Provenance);

internal sealed record StudioUx2EnvESnapshotsDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    string AreaSlug,
    IReadOnlyList<string> AvailableBusNames,
    IReadOnlyList<StudioUx2EnvESnapshotDraft> Snapshots);

internal sealed record StudioUx2EnvEDiagnostic(
    string Code,
    string Path,
    string Message,
    bool Blocking = true);

internal sealed record StudioUx2EnvEDiagnosticGroup(
    string Key,
    string Title,
    string State,
    IReadOnlyList<StudioUx2EnvEDiagnostic> Diagnostics);

internal sealed record StudioUx2EnvEAudioDiff(
    IReadOnlyList<string> Zone,
    IReadOnlyList<string> Ambience,
    IReadOnlyList<string> Music,
    IReadOnlyList<string> Buses)
{
    internal bool HasChanges => Zone.Count > 0 || Ambience.Count > 0 || Music.Count > 0 || Buses.Count > 0;
}

internal sealed record StudioUx2EnvESnapshotDiff(IReadOnlyList<string> Entries)
{
    internal bool HasChanges => Entries.Count > 0;
}

internal sealed record StudioUx2EnvEAudioPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    StudioUx2EnvEAudioDiff Diff,
    IReadOnlyList<StudioUx2EnvEDiagnosticGroup> Groups,
    bool CanonicalDryRun,
    bool AdaptiveCrossfadePlanBuilt,
    bool MusicTransitionPlanBuilt,
    bool MixPlanBuilt,
    bool ListenerMixPlanBuilt,
    bool PlaylistPlanBuilt,
    bool PlayerInstantiationSkipped,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx2EnvESnapshotPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    StudioUx2EnvESnapshotDiff Diff,
    IReadOnlyList<StudioUx2EnvEDiagnosticGroup> Groups,
    bool CanonicalDryRun,
    bool BusReferencesResolved,
    bool SeparateTransactionDeclared);

internal sealed record StudioUx2EnvEActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2EnvEApprovalSnapshot(
    StudioUx2EnvEAudioDraft? AudioDraft,
    ProjectRevision? AudioRevision,
    string? AudioFingerprint,
    StudioUx2EnvESnapshotsDraft? SnapshotsDraft,
    ProjectRevision? SnapshotsRevision,
    string? SnapshotsFingerprint);

internal sealed record StudioUx2EnvEEvidenceSourceSnapshot(
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
    StudioUx2EnvEApprovalSnapshot Approval);

internal sealed record StudioUx2EnvESmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool EmptySourceSupported,
    bool InvalidAudioBlocked,
    bool InvalidLayerBlocked,
    bool InvalidMusicBlocked,
    bool InvalidBusBlocked,
    bool InvalidSnapshotBlocked,
    bool AudioDryRun,
    bool SnapshotDryRun,
    bool DeterministicPlans,
    bool NoLivePlaybackClaim,
    bool StaleAudioBlocked,
    bool StaleSnapshotBlocked,
    bool ApprovalFingerprintBlocked,
    bool SeparateCommands,
    bool TwoStepUndoRedo,
    bool Persistence,
    bool EnvironmentLotsPreserved,
    bool AudioTailsPreserved,
    bool AuthoringTailsPreserved,
    bool AreaNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool StructuredDiagnostics,
    bool HumanText,
    bool Cleanup);
