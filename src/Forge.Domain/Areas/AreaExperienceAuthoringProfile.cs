using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Domain.Areas;

public enum AreaMapExplorationScope { Player, Party, World }
public enum AreaVisibilityPortalMode { Open, Closed, Conditional }
public enum AreaLodSourceMode { Imported, Generated, Hybrid }

public sealed record AreaAudioSnapshotProfile(
    Guid Id,
    string Name,
    IReadOnlyDictionary<string, float> BusVolumes,
    float TransitionSeconds,
    string TriggerEvent,
    string Provenance);

public sealed record AreaVisibilityRoomProfile(
    Guid Id,
    string Name,
    TransformValue Transform,
    bool Exterior,
    float StreamingDistance,
    IReadOnlyDictionary<string, string> Overrides);

public sealed record AreaVisibilityPortalProfile(
    Guid Id,
    string Name,
    Guid SourceRoomId,
    Guid DestinationRoomId,
    TransformValue Transform,
    AreaVisibilityPortalMode Mode,
    string Condition,
    bool TwoWay);

public sealed record AreaOccluderProfile(
    Guid Id,
    string Name,
    TransformValue Transform,
    bool Dynamic,
    string MeshAssetId,
    IReadOnlyDictionary<string, string> Overrides);

public sealed record AreaVisibilityAuthoringProfile(
    IReadOnlyList<AreaVisibilityRoomProfile> Rooms,
    IReadOnlyList<AreaVisibilityPortalProfile> Portals,
    IReadOnlyList<AreaOccluderProfile> Occluders,
    bool OcclusionCulling,
    bool DistanceStreaming,
    bool CellVisualizer,
    float CellSize,
    float StreamingDistance);

public sealed record AreaLodAssetOverride(
    Guid Id,
    string AssetId,
    AreaLodSourceMode SourceMode,
    IReadOnlyList<float> ScreenRatios,
    IReadOnlyList<int> TriangleBudgets,
    float CrossFadeSeconds,
    string Provenance);

public sealed record AreaLodAuthoringProfile(
    AreaLodSourceMode DefaultSourceMode,
    IReadOnlyList<float> DistanceThresholds,
    int MaximumVisibleTriangles,
    int MaximumHlodClusters,
    float HlodClusterSize,
    float PoppingTolerance,
    bool CrossFade,
    bool MeasurePopping,
    IReadOnlyList<AreaLodAssetOverride> AssetOverrides);

public sealed record AreaExperienceAuthoringProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public IReadOnlyList<AreaAudioSnapshotProfile> AudioSnapshots { get; }
    public AreaVisibilityAuthoringProfile Visibility { get; }
    public AreaLodAuthoringProfile Lod { get; }
    public bool RequireRuntimeMetadata { get; }

    public AreaExperienceAuthoringProfile(
        Guid id,
        int schemaVersion,
        string provenance,
        IReadOnlyList<AreaAudioSnapshotProfile> audioSnapshots,
        AreaVisibilityAuthoringProfile visibility,
        AreaLodAuthoringProfile lod,
        bool requireRuntimeMetadata)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance?.Trim() ?? string.Empty;
        AudioSnapshots = (audioSnapshots ?? []).OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        Visibility = visibility ?? CreateDefaultVisibility();
        Lod = lod ?? CreateDefaultLod();
        RequireRuntimeMetadata = requireRuntimeMetadata;
    }

    public static AreaExperienceAuthoringProfile CreateDefault(AreaId areaId) => new(
        CreateStableId(areaId, "area-experience"),
        CurrentSchemaVersion,
        "migration-default",
        [],
        CreateDefaultVisibility(),
        CreateDefaultLod(),
        false);

    public AreaExperienceAuthoringProfile ForArea(AreaId areaId) =>
        !string.Equals(Provenance, "migration-default", StringComparison.Ordinal)
            ? this
            : new(CreateStableId(areaId, "area-experience"), SchemaVersion, Provenance,
                AudioSnapshots, Visibility, Lod, RequireRuntimeMetadata);

    public static Guid CreateStableId(AreaId areaId, string discriminator)
    {
        ArgumentNullException.ThrowIfNull(areaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(discriminator);
        return StableGuid(areaId.Value, discriminator);
    }

    private static AreaVisibilityAuthoringProfile CreateDefaultVisibility() =>
        new([], [], [], true, true, false, 16, 128);

    private static AreaLodAuthoringProfile CreateDefaultLod() =>
        new(AreaLodSourceMode.Hybrid, [20, 50, 100], 2_000_000, 128, 32, .1f, true, true, []);

    private static Guid StableGuid(Guid value, string discriminator)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"openrpg-forge:{value:D}:{discriminator}"));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, guid.Length).CopyTo(guid);
        guid[6] = (byte)((guid[6] & 0x0f) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }
}

public static class AreaExperienceAuthoringValidator
{
    public const string IdentityCode = "area.experience.identity_invalid";
    public const string SchemaCode = "area.experience.schema_unsupported";
    public const string AudioCode = "area.experience.audio_invalid";
    public const string EventCode = "area.experience.event_invalid";
    public const string MapCode = "area.experience.map_invalid";
    public const string VisibilityCode = "area.experience.visibility_invalid";
    public const string LodCode = "area.experience.lod_invalid";

    public static IReadOnlyList<DomainDiagnostic> Validate(AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(area);
        var result = new List<DomainDiagnostic>();
        var root = $"areas/{area.Id}/authoring";
        var profile = area.Authoring.Experience;

        if (profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Provenance))
            Add(result, IdentityCode, "Area experience requires a stable UUID and provenance.", $"{root}/experience/id");
        if (profile.SchemaVersion != AreaExperienceAuthoringProfile.CurrentSchemaVersion)
            Add(result, SchemaCode, "Area experience schema version is unsupported.", $"{root}/experience/schemaVersion");

        if (profile.AudioSnapshots.Select(item => item.Id).Distinct().Count() != profile.AudioSnapshots.Count ||
            profile.AudioSnapshots.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != profile.AudioSnapshots.Count ||
            profile.AudioSnapshots.Any(item => item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Name) ||
                string.IsNullOrWhiteSpace(item.Provenance) || !float.IsFinite(item.TransitionSeconds) ||
                item.TransitionSeconds is < 0 or > 60 || item.BusVolumes.Any(bus =>
                    string.IsNullOrWhiteSpace(bus.Key) || !float.IsFinite(bus.Value) || bus.Value is < 0 or > 4)))
            Add(result, AudioCode, "Audio snapshots require unique IDs/names, bounded buses and transitions.",
                $"{root}/experience/audioSnapshots");

        var extended = !string.Equals(profile.Provenance, "migration-default", StringComparison.Ordinal);
        if (extended && area.Authoring.UserEvents.Any(item => item.Id == Guid.Empty ||
            !item.NamespacedName.StartsWith($"area.{area.Slug}.", StringComparison.Ordinal) ||
            item.ApiVersion < 1 || item.Subscriptions.Any(string.IsNullOrWhiteSpace) ||
            string.IsNullOrWhiteSpace(item.Provenance)))
            Add(result, EventCode, "Area events require area scope, API version, payload, script and subscriptions.",
                $"{root}/userEvents");

        var map = area.Authoring.Map;
        if (map.Id == Guid.Empty || map.SchemaVersion != 1 || string.IsNullOrWhiteSpace(map.Provenance) ||
            !float.IsFinite(map.ExplorationCellSize) || map.ExplorationCellSize is < 1 or > 256 ||
            (!map.Generate2D && !map.Generate3D) || string.IsNullOrWhiteSpace(map.RevealApi) ||
            map.Pins.Any(pin => pin.Id == Guid.Empty || string.IsNullOrWhiteSpace(pin.Label) ||
                !float.IsFinite(pin.X) || !float.IsFinite(pin.Z) || pin.Floor < 0 || pin.Floor >= map.FloorCount))
            Add(result, MapCode, "Map generation, exploration cells, pins and reveal API are invalid.", $"{root}/map");

        ValidateVisibility(profile.Visibility, root, result);
        ValidateLod(profile.Lod, root, result);
        return result;
    }

    private static void ValidateVisibility(
        AreaVisibilityAuthoringProfile profile,
        string root,
        ICollection<DomainDiagnostic> result)
    {
        var roomIds = profile.Rooms.Select(item => item.Id).ToHashSet();
        var allIds = profile.Rooms.Select(item => item.Id)
            .Concat(profile.Portals.Select(item => item.Id))
            .Concat(profile.Occluders.Select(item => item.Id)).ToArray();
        if (allIds.Any(id => id == Guid.Empty) || allIds.Distinct().Count() != allIds.Length ||
            !float.IsFinite(profile.CellSize) || profile.CellSize is < 1 or > 512 ||
            !float.IsFinite(profile.StreamingDistance) || profile.StreamingDistance < profile.CellSize ||
            profile.Rooms.Any(room => string.IsNullOrWhiteSpace(room.Name) || !Finite(room.Transform) ||
                !float.IsFinite(room.StreamingDistance) || room.StreamingDistance < 0) ||
            profile.Portals.Any(portal => string.IsNullOrWhiteSpace(portal.Name) || !Finite(portal.Transform) ||
                portal.SourceRoomId == portal.DestinationRoomId || !roomIds.Contains(portal.SourceRoomId) ||
                !roomIds.Contains(portal.DestinationRoomId) ||
                (portal.Mode == AreaVisibilityPortalMode.Conditional && string.IsNullOrWhiteSpace(portal.Condition))) ||
            profile.Occluders.Any(occluder => string.IsNullOrWhiteSpace(occluder.Name) ||
                !Finite(occluder.Transform) || occluder.Transform.Scale.X <= 0 || occluder.Transform.Scale.Y <= 0 ||
                occluder.Transform.Scale.Z <= 0))
            Add(result, VisibilityCode, "Rooms, visibility portals, occluders or streaming limits are invalid.",
                $"{root}/experience/visibility");
    }

    private static void ValidateLod(
        AreaLodAuthoringProfile profile,
        string root,
        ICollection<DomainDiagnostic> result)
    {
        var ordered = profile.DistanceThresholds.Zip(profile.DistanceThresholds.Skip(1), (left, right) => right > left)
            .All(item => item);
        if (profile.DistanceThresholds.Count is < 1 or > 6 || !ordered ||
            profile.DistanceThresholds.Any(value => !float.IsFinite(value) || value <= 0) ||
            profile.MaximumVisibleTriangles <= 0 || profile.MaximumHlodClusters <= 0 ||
            !float.IsFinite(profile.HlodClusterSize) || profile.HlodClusterSize <= 0 ||
            !float.IsFinite(profile.PoppingTolerance) || profile.PoppingTolerance is < 0 or > 1 ||
            profile.AssetOverrides.Select(item => item.Id).Distinct().Count() != profile.AssetOverrides.Count ||
            profile.AssetOverrides.Select(item => item.AssetId).Distinct(StringComparer.Ordinal).Count() !=
            profile.AssetOverrides.Count || profile.AssetOverrides.Any(item => item.Id == Guid.Empty ||
                string.IsNullOrWhiteSpace(item.AssetId) || string.IsNullOrWhiteSpace(item.Provenance) ||
                item.ScreenRatios.Count == 0 || item.ScreenRatios.Count != item.TriangleBudgets.Count ||
                item.ScreenRatios.Any(value => !float.IsFinite(value) || value is <= 0 or > 1) ||
                item.TriangleBudgets.Any(value => value <= 0) || !float.IsFinite(item.CrossFadeSeconds) ||
                item.CrossFadeSeconds is < 0 or > 10))
            Add(result, LodCode, "LOD/HLOD thresholds, budgets, asset overrides or popping policy are invalid.",
                $"{root}/experience/lod");
    }

    private static bool Finite(TransformValue value) =>
        float.IsFinite(value.Position.X) && float.IsFinite(value.Position.Y) && float.IsFinite(value.Position.Z) &&
        float.IsFinite(value.Rotation.X) && float.IsFinite(value.Rotation.Y) && float.IsFinite(value.Rotation.Z) &&
        float.IsFinite(value.Rotation.W) && float.IsFinite(value.Scale.X) && float.IsFinite(value.Scale.Y) &&
        float.IsFinite(value.Scale.Z);

    private static void Add(ICollection<DomainDiagnostic> result, string code, string message, string path) =>
        result.Add(new DomainDiagnostic(code, message, path));
}

public sealed record AreaExperienceRuntimeHandle(
    AreaId AreaId,
    int AudioSnapshots,
    int UserEvents,
    int MapPins,
    int VisibilityRooms,
    int VisibilityPortals,
    int Occluders,
    int LodOverrides,
    bool FogOfWar);

public sealed record AreaExperienceRuntimeResult(
    AreaExperienceRuntimeHandle? Handle,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class AreaExperienceRuntimeResolver
{
    public const string RequiredMissingCode = "runtime.area_experience.required_missing";

    public static AreaExperienceRuntimeResult Resolve(AreaDefinition area, bool required)
    {
        ArgumentNullException.ThrowIfNull(area);
        var experience = area.Authoring.Experience;
        if (string.Equals(experience.Provenance, "migration-default", StringComparison.Ordinal))
            return required
                ? new(null, [new(RequiredMissingCode, "Required area experience metadata is missing.",
                    $"areas/{area.Id}/authoring/experience")])
                : new(null, []);
        var diagnostics = AreaExperienceAuthoringValidator.Validate(area);
        return diagnostics.Count > 0
            ? new(null, diagnostics)
            : new(new(area.Id, experience.AudioSnapshots.Count, area.Authoring.UserEvents.Count,
                area.Authoring.Map.Pins.Count, experience.Visibility.Rooms.Count,
                experience.Visibility.Portals.Count, experience.Visibility.Occluders.Count,
                experience.Lod.AssetOverrides.Count, area.Authoring.Map.FogOfWar), []);
    }
}
