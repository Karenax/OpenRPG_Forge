using Forge.Domain.Entities;
using System.Text.Json.Serialization;

namespace Forge.Domain.Areas;

public sealed record AreaUserEvent
{
    public const int LegacySchemaVersion = 1;
    public const int CurrentSchemaVersion = 2;
    public const string CurrentForgeScriptApiVersion = "1.0.0";

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string NamespacedName { get; }
    public string PayloadSchema { get; }
    public IReadOnlyDictionary<string, string> PayloadFields { get; }
    public string ScriptPath { get; }
    public int ApiVersion { get; }
    public string ForgeScriptApiVersion { get; }
    public IReadOnlyList<string> Subscriptions { get; }
    public bool Required { get; }
    public string Provenance { get; }

    [JsonConstructor]
    public AreaUserEvent(Guid id, string namespacedName, string payloadSchema, string scriptPath,
        int apiVersion = 1, IReadOnlyList<string>? subscriptions = null, bool required = false,
        string provenance = "migration-default", int schemaVersion = LegacySchemaVersion,
        IReadOnlyDictionary<string, string>? payloadFields = null,
        string forgeScriptApiVersion = CurrentForgeScriptApiVersion)
    {
        Id = id;
        SchemaVersion = schemaVersion == 0 ? LegacySchemaVersion : schemaVersion;
        NamespacedName = namespacedName?.Trim() ?? string.Empty;
        PayloadSchema = payloadSchema?.Trim() ?? string.Empty;
        PayloadFields = (payloadFields ?? new Dictionary<string, string>())
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(item => item.Key.Trim(), item => item.Value?.Trim() ?? string.Empty,
                StringComparer.Ordinal);
        ScriptPath = scriptPath?.Trim().Replace('\\', '/') ?? string.Empty;
        ApiVersion = apiVersion;
        ForgeScriptApiVersion = forgeScriptApiVersion?.Trim() ?? string.Empty;
        Subscriptions = (subscriptions ?? []).Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Required = required;
        Provenance = provenance?.Trim() ?? string.Empty;
    }
}

public sealed record AreaMapPin(Guid Id, string Label, float X, float Z, int Floor, bool InitiallyVisible);

public sealed record AreaMapProfile
{
    public bool Enabled { get; }
    public bool FogOfWar { get; }
    public string Projection { get; }
    public int FloorCount { get; }
    public IReadOnlyList<AreaMapPin> Pins { get; }
    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public bool Generate2D { get; }
    public bool Generate3D { get; }
    public AreaMapExplorationScope ExplorationScope { get; }
    public float ExplorationCellSize { get; }
    public string RevealApi { get; }
    public AreaMapProfile(bool enabled, bool fogOfWar, string projection, int floorCount, IReadOnlyList<AreaMapPin> pins)
        : this(enabled, fogOfWar, projection, floorCount, pins, Guid.Empty)
    {
    }

    [JsonConstructor]
    public AreaMapProfile(bool enabled, bool fogOfWar, string projection, int floorCount, IReadOnlyList<AreaMapPin> pins,
        Guid id, int schemaVersion = 1, string provenance = "migration-default",
        bool generate2D = true, bool generate3D = true,
        AreaMapExplorationScope explorationScope = AreaMapExplorationScope.Party,
        float explorationCellSize = 8, string revealApi = "area.map.reveal")
    {
        if (floorCount < 1 || string.IsNullOrWhiteSpace(projection) || pins.Select(pin => pin.Id).Distinct().Count() != pins.Count) throw new ArgumentException("Map profile is invalid.");
        Enabled = enabled; FogOfWar = fogOfWar; Projection = projection.Trim(); FloorCount = floorCount; Pins = pins.ToArray();
        Id = id;
        SchemaVersion = schemaVersion;
        Provenance = provenance?.Trim() ?? string.Empty;
        Generate2D = generate2D;
        Generate3D = generate3D;
        ExplorationScope = explorationScope;
        ExplorationCellSize = explorationCellSize;
        RevealApi = revealApi?.Trim() ?? string.Empty;
    }
}

public sealed record AreaEditorLayer
{
    public const int CurrentSchemaVersion = 1;
    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Provenance { get; }
    public string Name { get; }
    public Guid? ParentId { get; }
    public bool Visible { get; }
    public bool Locked { get; }
    public string Owner { get; }
    public IReadOnlyList<EntityId> EntityIds { get; }
    public bool ExportEnabled { get; }
    public AreaEditorLayer(Guid id, string name, Guid? parentId, bool visible, bool locked, string owner,
        IReadOnlyList<EntityId> entityIds, int schemaVersion = CurrentSchemaVersion,
        string provenance = "migration-default", bool exportEnabled = true)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(owner) || entityIds.Distinct().Count() != entityIds.Count) throw new ArgumentException("Editor layer is invalid.");
        Id = id; SchemaVersion = schemaVersion; Provenance = provenance?.Trim() ?? string.Empty;
        Name = name.Trim(); ParentId = parentId; Visible = visible; Locked = locked; Owner = owner.Trim();
        EntityIds = entityIds.ToArray(); ExportEnabled = exportEnabled;
    }
}

public sealed record AreaAuthoringProfile
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; }
    public IReadOnlyList<AreaUserEvent> UserEvents { get; }
    public AreaMapProfile Map { get; }
    public IReadOnlyList<AreaEditorLayer> Layers { get; }
    public AreaExperienceAuthoringProfile Experience { get; }
    public bool RequireLayerRuntimeMetadata { get; }
    public AreaAuthoringProfile(int schemaVersion, IReadOnlyList<AreaUserEvent> userEvents, AreaMapProfile map,
        IReadOnlyList<AreaEditorLayer> layers, AreaExperienceAuthoringProfile? experience = null,
        bool requireLayerRuntimeMetadata = false)
    {
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        if (userEvents.Select(item => item.Id).Distinct().Count() != userEvents.Count || userEvents.Select(item => item.NamespacedName).Distinct(StringComparer.Ordinal).Count() != userEvents.Count || layers.Select(item => item.Id).Distinct().Count() != layers.Count) throw new ArgumentException("Authoring IDs and event names must be unique.");
        var ids = layers.Select(item => item.Id).ToHashSet(); if (layers.Any(item => item.ParentId is not null && (!ids.Contains(item.ParentId.Value) || item.ParentId == item.Id))) throw new ArgumentException("Layer parent is invalid.");
        SchemaVersion = schemaVersion; UserEvents = userEvents.OrderBy(item => item.NamespacedName, StringComparer.Ordinal).ToArray(); Map = map; Layers = layers.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        Experience = experience ?? AreaExperienceAuthoringProfile.CreateDefault(
            AreaId.From(Guid.Parse("a0550000-0000-4000-8000-000000000001")));
        RequireLayerRuntimeMetadata = requireLayerRuntimeMetadata;
    }
    public static AreaAuthoringProfile CreateDefault() => CreateDefault(
        AreaId.From(Guid.Parse("a0550000-0000-4000-8000-000000000001")));
    public static AreaAuthoringProfile CreateDefault(AreaId areaId) => new(CurrentSchemaVersion, [],
        new(true, true, "orthographic", 1, [],
            AreaExperienceAuthoringProfile.CreateStableId(areaId, "area-map"),
            provenance: "migration-default"), [], AreaExperienceAuthoringProfile.CreateDefault(areaId));
    public AreaAuthoringProfile ForArea(AreaId areaId)
    {
        var experience = Experience.ForArea(areaId);
        var map = Map.Id != Guid.Empty ? Map : new(Map.Enabled, Map.FogOfWar, Map.Projection, Map.FloorCount,
            Map.Pins, AreaExperienceAuthoringProfile.CreateStableId(areaId, "area-map"), Map.SchemaVersion,
            Map.Provenance, Map.Generate2D, Map.Generate3D,
            Map.ExplorationScope, Map.ExplorationCellSize, Map.RevealApi);
        return new(SchemaVersion, UserEvents, map, Layers, experience, RequireLayerRuntimeMetadata);
    }
}
