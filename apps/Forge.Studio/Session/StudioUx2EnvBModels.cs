using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Studio.Session;

internal sealed record StudioUx2EnvBAreaChoice(
    AreaId AreaId,
    string Label,
    bool HasAuthoredEnvironment);

internal sealed record StudioUx2EnvBTransformDraft(
    Vector3Value Position,
    QuaternionValue Rotation,
    Vector3Value Scale);

internal sealed record StudioUx2EnvBCurveDraft(
    float Hour,
    float SunIntensity,
    float MoonIntensity,
    float SunElevationDegrees);

internal sealed record StudioUx2EnvBEventDraft(
    Guid Id,
    float Hour,
    string EventName,
    string ForgeScriptPath,
    bool Required);

internal sealed record StudioUx2EnvBProbeDraft(
    Guid Id,
    string Name,
    StudioUx2EnvBTransformDraft Transform,
    string AssetId,
    BlueprintId? BlueprintId,
    string BlueprintLabel,
    float RadiusMeters,
    bool Realtime,
    IReadOnlyDictionary<string, string> Overrides);

internal sealed record StudioUx2EnvBLightingVolumeDraft(
    Guid Id,
    string Name,
    StudioUx2EnvBTransformDraft Transform,
    string LightingPresetKey,
    FogMode FogMode,
    float BlendDistanceMeters,
    IReadOnlyDictionary<string, string> Overrides);

internal sealed record StudioUx2EnvBFogNoiseDraft(
    string AssetId,
    float ScaleMeters,
    float SpeedX,
    float SpeedZ,
    float Contrast);

internal sealed record StudioUx2EnvBFogVolumeDraft(
    Guid Id,
    string Name,
    StudioUx2EnvBTransformDraft Transform,
    FogMode Mode,
    float Density,
    float HeightFalloff,
    string Color,
    float BlendDistanceMeters,
    IReadOnlyDictionary<string, string> Overrides);

internal sealed record StudioUx2EnvBQualityDraft(
    string Platform,
    AreaFogQualityTier Tier);

internal sealed record StudioUx2EnvBDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    string AreaSlug,
    AreaEnvironmentClass Classification,
    IReadOnlyList<string> Tags,
    string ClassificationKey,
    string ExtensionPack,
    bool DrivesRendering,
    bool DrivesAudio,
    bool DrivesNavigation,
    bool DrivesWeather,
    bool DrivesCamera,
    bool DrivesGameplay,
    DayNightMode DayNightMode,
    float DayLengthMinutes,
    float AmbientIntensity,
    float DirectionalIntensity,
    float ColorTemperatureKelvin,
    bool Hdr,
    bool Shadows,
    string LightingPresetKey,
    string AmbientColor,
    string DirectionalColor,
    bool SimpleControls,
    IReadOnlyList<StudioUx2EnvBCurveDraft> Curves,
    IReadOnlyList<StudioUx2EnvBEventDraft> Events,
    IReadOnlyList<StudioUx2EnvBProbeDraft> Probes,
    IReadOnlyList<StudioUx2EnvBLightingVolumeDraft> LightingVolumes,
    FogMode FogMode,
    float FogDensity,
    float FogStartDistance,
    float FogEndDistance,
    float FogHeight,
    string FogDayColor,
    string FogNightColor,
    StudioUx2EnvBFogNoiseDraft? FogNoise,
    IReadOnlyList<StudioUx2EnvBFogVolumeDraft> FogVolumes,
    IReadOnlyList<StudioUx2EnvBQualityDraft> FogQualityProfiles,
    bool RequiredAtRuntime,
    bool RequireAuthoringRuntimeMetadata);

internal sealed record StudioUx2EnvBDiagnostic(
    string Code,
    string Path,
    string Message,
    bool Blocking = true);

internal sealed record StudioUx2EnvBDiagnosticGroup(
    string Key,
    string Title,
    string State,
    IReadOnlyList<StudioUx2EnvBDiagnostic> Diagnostics);

internal sealed record StudioUx2EnvBSemanticDiff(
    IReadOnlyList<string> Classification,
    IReadOnlyList<string> Cycle,
    IReadOnlyList<string> Lighting,
    IReadOnlyList<string> Fog)
{
    internal bool HasChanges => Classification.Count > 0 || Cycle.Count > 0 ||
                                Lighting.Count > 0 || Fog.Count > 0;
}

internal sealed record StudioUx2EnvBPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    StudioUx2EnvBSemanticDiff Diff,
    IReadOnlyList<StudioUx2EnvBDiagnosticGroup> Groups,
    bool CanonicalDryRun,
    bool TemporalSimulation,
    bool RuntimeFallbackHonest);

internal sealed record StudioUx2EnvBActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2EnvBApprovalSnapshot(
    StudioUx2EnvBDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2EnvBEvidenceSourceSnapshot(
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
    StudioUx2EnvBApprovalSnapshot Approval);

internal sealed record StudioUx2EnvBSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool CompleteCollections,
    bool InvalidClassificationBlocked,
    bool InvalidCycleBlocked,
    bool InvalidLightingBlocked,
    bool InvalidFogBlocked,
    bool DryRun,
    bool StaleDraftBlocked,
    bool StaleFingerprintBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool EnvironmentTailsPreserved,
    bool AreaNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool StructuredDiagnostics,
    bool HumanText,
    bool Cleanup);
