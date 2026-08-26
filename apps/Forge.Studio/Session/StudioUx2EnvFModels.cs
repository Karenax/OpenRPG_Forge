using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx2EnvFAreaChoice(
    AreaId AreaId,
    string Label,
    int DefinitionCount,
    int ClipCount,
    int InstanceCount);

internal sealed record StudioUx2EnvFMixBusChoice(Guid? Id, string Label);

internal sealed record StudioUx2EnvFClipDraft(
    Guid Id,
    string ResourcePath,
    string Weight,
    IReadOnlyList<string> Tags,
    string Variant,
    string Locale,
    string LoudnessLufs,
    string PreviewResourcePath,
    string MimeType,
    string MemoryBytes,
    string LicenseSpdx,
    string Provenance,
    string ContentSha256,
    string Transcript,
    IReadOnlyList<string> WaveformPeaks,
    string DurationSeconds);

internal sealed record StudioUx2EnvFDefinitionDraft(
    Guid Id,
    int SchemaVersion,
    string Name,
    Guid ClipSetId,
    int ClipSetSchemaVersion,
    string ClipSetName,
    AudioClipSelectionMode Selection,
    IReadOnlyList<StudioUx2EnvFClipDraft> Clips,
    string ClipSetProvenance,
    AudioLoopMode LoopMode,
    string RepeatCount,
    string RegionStartSeconds,
    string RegionEndSeconds,
    string CrossfadeSeconds,
    string StopEvent,
    string InnerRadius,
    string OuterRadius,
    string AttenuationCurve,
    string ConeAngleDegrees,
    string ConeAttenuationDb,
    string Occlusion,
    string HeightOffset,
    bool VirtualizeOutsideRange,
    Guid? MixBusId,
    string StartDelaySeconds,
    string ActivatedScript,
    string CompletedScript,
    bool Required,
    string Provenance,
    string PackageManifest,
    string LicenseSpdx,
    string ContentSha256);

internal sealed record StudioUx2EnvFInstanceDraft(
    Guid Id,
    Guid DefinitionId,
    string PositionX,
    string PositionY,
    string PositionZ,
    string RotationX,
    string RotationY,
    string RotationZ,
    string RotationW,
    string ScaleX,
    string ScaleY,
    string ScaleZ,
    bool Enabled,
    string Provenance,
    string Volume,
    bool Muted,
    IReadOnlyList<string> Conditions);

internal sealed record StudioUx2EnvFDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    string AreaSlug,
    IReadOnlyList<StudioUx2EnvFDefinitionDraft> Definitions,
    IReadOnlyList<StudioUx2EnvFInstanceDraft> Instances,
    IReadOnlyList<StudioUx2EnvFMixBusChoice> MixBuses);

internal sealed record StudioUx2EnvFDiagnostic(
    string Code,
    string Path,
    string Message,
    bool Blocking = true);

internal sealed record StudioUx2EnvFDiagnosticGroup(
    string Key,
    string Title,
    string State,
    IReadOnlyList<StudioUx2EnvFDiagnostic> Diagnostics);

internal sealed record StudioUx2EnvFDiff(IReadOnlyList<string> Entries)
{
    internal bool HasChanges => Entries.Count > 0;
}

internal sealed record StudioUx2EnvFPlaybackPlan(
    string Emitter,
    string Clip,
    string Loop,
    string Position,
    bool Autoplay,
    float Volume,
    IReadOnlyList<string> Conditions);

internal sealed record StudioUx2EnvFPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    StudioUx2EnvFDiff Diff,
    IReadOnlyList<StudioUx2EnvFDiagnosticGroup> Groups,
    IReadOnlyList<StudioUx2EnvFPlaybackPlan> PlaybackPlans,
    bool CanonicalDryRun,
    bool DeterministicPlansBuilt,
    bool PlayerInstantiationSkipped,
    bool ResourceLoadingSkipped,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx2EnvFActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2EnvFApprovalSnapshot(
    StudioUx2EnvFDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2EnvFEvidenceSourceSnapshot(
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
    StudioUx2EnvFApprovalSnapshot Approval);

internal sealed record StudioUx2EnvFSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool EmptySourceSupported,
    bool InvalidDefinitionBlocked,
    bool InvalidClipBlocked,
    bool InvalidLoopBlocked,
    bool InvalidSpatialBlocked,
    bool InvalidInstanceBlocked,
    bool CanonicalDryRun,
    bool DeterministicPlans,
    bool NoLivePlaybackClaim,
    bool StaleRevisionBlocked,
    bool ApprovalFingerprintBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool ClipOrderPreserved,
    bool HiddenClipTailsPreserved,
    bool EnvironmentLotsPreserved,
    bool AudioTailsPreserved,
    bool AreaNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool StructuredDiagnostics,
    bool HumanText,
    bool Cleanup);
