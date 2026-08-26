using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Studio.Session;

internal sealed record StudioUx2EnvCAreaChoice(
    AreaId AreaId,
    string Label,
    bool HasWater,
    bool HasSpatialReverb);

internal sealed record StudioUx2EnvCWindZoneDraft(
    Guid Id,
    Vector3Value Center,
    float Radius,
    float DirectionX,
    float DirectionZ,
    float Speed,
    float GustMultiplier);

internal sealed record StudioUx2EnvCWeatherStateDraft(
    Guid Id,
    string Name,
    WeatherKind Kind,
    float Intensity,
    float Humidity,
    float SurfaceWetness,
    SurfaceWetnessMaterialProfile WetnessMaterial,
    bool Lightning,
    string PrecipitationResource,
    string AudioCue,
    string GameplayEffect,
    float MinimumDurationSeconds,
    float MaximumDurationSeconds,
    float TransitionSeconds,
    PrecipitationVisualProfile Precipitation);

internal sealed record StudioUx2EnvCWaterBodyDraft(
    Guid Id,
    string Name,
    WaterBodyKind Kind,
    Vector3Value Center,
    Vector3Value Size,
    float MaximumDepth,
    float FlowDirectionX,
    float FlowDirectionZ,
    float FlowSpeed,
    float Buoyancy,
    float NavigationCost,
    bool SwimmingEnabled,
    WaterQualityTier Quality,
    string MaterialResource,
    string AudioCue);

internal sealed record StudioUx2EnvCReverbZoneDraft(
    Guid Id,
    string Name,
    Vector3Value Center,
    Vector3Value Size,
    string Preset,
    float Wet,
    float Dry,
    float BlendSeconds,
    string ImpulseResource,
    string LicenseSpdx,
    string ContentSha256,
    string Provenance);

internal sealed record StudioUx2EnvCDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    string AreaSlug,
    float WindDirectionX,
    float WindDirectionZ,
    float WindSpeed,
    float WindGustStrength,
    float WindGustIntervalSeconds,
    bool WindInfluencesVegetation,
    bool WindInfluencesParticles,
    string WindAudioCue,
    float WindGameplayForce,
    IReadOnlyList<StudioUx2EnvCWindZoneDraft> WindZones,
    Guid InitialWeatherStateId,
    IReadOnlyList<StudioUx2EnvCWeatherStateDraft> WeatherStates,
    bool WeatherServerAuthoritative,
    int WeatherDeterministicSeed,
    bool WeatherPersistState,
    bool WeatherReplicateState,
    IReadOnlyList<StudioUx2EnvCWaterBodyDraft> WaterBodies,
    string ReverbDefaultPreset,
    IReadOnlyList<StudioUx2EnvCReverbZoneDraft> ReverbZones);

internal sealed record StudioUx2EnvCDiagnostic(
    string Code,
    string Path,
    string Message,
    bool Blocking = true);

internal sealed record StudioUx2EnvCDiagnosticGroup(
    string Key,
    string Title,
    string State,
    IReadOnlyList<StudioUx2EnvCDiagnostic> Diagnostics);

internal sealed record StudioUx2EnvCSemanticDiff(
    IReadOnlyList<string> Wind,
    IReadOnlyList<string> Weather,
    IReadOnlyList<string> Water,
    IReadOnlyList<string> Reverb)
{
    internal bool HasChanges => Wind.Count > 0 || Weather.Count > 0 || Water.Count > 0 || Reverb.Count > 0;
}

internal sealed record StudioUx2EnvCPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    StudioUx2EnvCSemanticDiff Diff,
    IReadOnlyList<StudioUx2EnvCDiagnosticGroup> Groups,
    bool CanonicalDryRun,
    bool ClimatePlanBuilt,
    bool WeatherSimulationBuilt,
    bool WaterPlansBuilt,
    bool ReverbPlanBuilt,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx2EnvCActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2EnvCApprovalSnapshot(
    StudioUx2EnvCDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2EnvCEvidenceSourceSnapshot(
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
    StudioUx2EnvCApprovalSnapshot Approval);

internal sealed record StudioUx2EnvCSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool CompleteCollections,
    bool InvalidWindBlocked,
    bool InvalidWeatherBlocked,
    bool InvalidWaterBlocked,
    bool InvalidReverbBlocked,
    bool DryRun,
    bool BoundedPlans,
    bool StaleDraftBlocked,
    bool StaleFingerprintBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool LotBPreserved,
    bool EnvironmentTailsPreserved,
    bool AreaNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool StructuredDiagnostics,
    bool HumanText,
    bool Cleanup);
