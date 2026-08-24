using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx2EnvGAreaChoice(
    AreaId AreaId,
    string Label,
    int PlaylistCount,
    int GovernedResourceCount);

internal sealed record StudioUx2EnvGScopeChoice(string Value, string Label);

internal sealed record StudioUx2EnvGPlaylistEntry(
    string Resource,
    string Label,
    string Weight);

internal sealed record StudioUx2EnvGListenerGainDraft(
    string Key,
    string Label,
    string Gain);

internal sealed record StudioUx2EnvGDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    string AreaSlug,
    IReadOnlyList<StudioUx2EnvGPlaylistEntry> Playlist,
    AudioOrderMode OrderMode,
    string IntervalSeconds,
    string IntervalVariationSeconds,
    string RelativeVolume,
    string SelectionSeed,
    string NoRepeatWindow,
    string MinimumIntervalSeconds,
    string MaximumIntervalSeconds,
    string MinimumPitchSemitones,
    string MaximumPitchSemitones,
    string MinimumVolume,
    string MaximumVolume,
    AudioModulationDistribution Distribution,
    string Curve,
    string ModulationSeed,
    bool SyncToTempo,
    string TempoBpm,
    string BeatDivision,
    string TargetLoudnessLufs,
    string MixBus,
    string DuckingDb,
    string DistanceScale,
    string AccessibilityVolumeCap,
    AudioMeterMode MeterMode,
    string AreaScope,
    string AmbientBus,
    AudioListenerPolicy ListenerPolicy,
    IReadOnlyList<StudioUx2EnvGListenerGainDraft> ListenerGains,
    IReadOnlyList<StudioUx2EnvGScopeChoice> ScopeChoices,
    bool RequiredAtRuntime,
    string GovernanceSummary);

internal sealed record StudioUx2EnvGResourceResult(
    Guid EmitterId,
    Guid ClipId,
    string Label,
    string Resource,
    string Tags,
    string Locale,
    string Transcript,
    string License,
    string Provenance,
    string Loudness,
    string Duration,
    string Waveform,
    bool DistributionReady);

internal sealed record StudioUx2EnvGAuditionPlan(
    bool Succeeded,
    string Message,
    IReadOnlyList<string> Resources,
    string Bus,
    string NormalizedGain,
    bool Batch,
    bool PlayerInstantiationSkipped,
    bool ResourceLoadingSkipped);

internal sealed record StudioUx2EnvGSequencePlan(
    string Resource,
    string Delay,
    string Volume,
    string Pitch,
    string Bus);

internal sealed record StudioUx2EnvGMixPlan(
    string Scope,
    string Listener,
    string Bus,
    string Gain,
    string Meter,
    bool Allowed,
    bool Personalized,
    bool AccessibilityCapped);

internal sealed record StudioUx2EnvGDiagnostic(
    string Code,
    string Path,
    string Message,
    bool Blocking = true);

internal sealed record StudioUx2EnvGDiagnosticGroup(
    string Key,
    string Title,
    string State,
    IReadOnlyList<StudioUx2EnvGDiagnostic> Diagnostics);

internal sealed record StudioUx2EnvGDiff(IReadOnlyList<string> Entries)
{
    internal bool HasChanges => Entries.Count > 0;
}

internal sealed record StudioUx2EnvGPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    StudioUx2EnvGDiff Diff,
    IReadOnlyList<StudioUx2EnvGDiagnosticGroup> Groups,
    IReadOnlyList<StudioUx2EnvGSequencePlan> Sequence,
    IReadOnlyList<StudioUx2EnvGMixPlan> Mixes,
    bool CanonicalDryRun,
    bool StatisticsComputed,
    bool PlayerInstantiationSkipped,
    bool ResourceLoadingSkipped,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx2EnvGActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2EnvGApprovalSnapshot(
    StudioUx2EnvGDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2EnvGEvidenceSourceSnapshot(
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
    StudioUx2EnvGApprovalSnapshot Approval);

internal sealed record StudioUx2EnvGSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool EmptySourceSupported,
    bool SearchFacets,
    bool WaveformAndGovernance,
    bool AuditionPlansOnly,
    bool InvalidPlaylistBlocked,
    bool InvalidWeightBlocked,
    bool InvalidModulationBlocked,
    bool InvalidMixBlocked,
    bool InvalidListenerBlocked,
    bool CanonicalDryRun,
    bool DeterministicSequence,
    bool GlobalAndPersonalizedMix,
    bool StaleRevisionBlocked,
    bool ApprovalFingerprintBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool EnvironmentLotsPreserved,
    bool AudioTailsPreserved,
    bool AreaNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool StructuredDiagnostics,
    bool HumanText,
    bool Cleanup);
