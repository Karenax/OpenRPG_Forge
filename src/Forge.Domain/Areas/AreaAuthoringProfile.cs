using Forge.Domain.Entities;

namespace Forge.Domain.Areas;

public sealed record AreaUserEvent
{
    public Guid Id { get; }
    public string NamespacedName { get; }
    public string PayloadSchema { get; }
    public string ScriptPath { get; }
    public AreaUserEvent(Guid id, string namespacedName, string payloadSchema, string scriptPath)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(namespacedName) || !namespacedName.Contains('.', StringComparison.Ordinal) || string.IsNullOrWhiteSpace(payloadSchema) || string.IsNullOrWhiteSpace(scriptPath)) throw new ArgumentException("Area event is invalid.");
        Id = id; NamespacedName = namespacedName.Trim(); PayloadSchema = payloadSchema.Trim(); ScriptPath = scriptPath.Trim();
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
    public AreaMapProfile(bool enabled, bool fogOfWar, string projection, int floorCount, IReadOnlyList<AreaMapPin> pins)
    {
        if (floorCount < 1 || string.IsNullOrWhiteSpace(projection) || pins.Select(pin => pin.Id).Distinct().Count() != pins.Count) throw new ArgumentException("Map profile is invalid.");
        Enabled = enabled; FogOfWar = fogOfWar; Projection = projection.Trim(); FloorCount = floorCount; Pins = pins.ToArray();
    }
}

public sealed record AreaEditorLayer
{
    public Guid Id { get; }
    public string Name { get; }
    public Guid? ParentId { get; }
    public bool Visible { get; }
    public bool Locked { get; }
    public string Owner { get; }
    public IReadOnlyList<EntityId> EntityIds { get; }
    public AreaEditorLayer(Guid id, string name, Guid? parentId, bool visible, bool locked, string owner, IReadOnlyList<EntityId> entityIds)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(owner) || entityIds.Distinct().Count() != entityIds.Count) throw new ArgumentException("Editor layer is invalid.");
        Id = id; Name = name.Trim(); ParentId = parentId; Visible = visible; Locked = locked; Owner = owner.Trim(); EntityIds = entityIds.ToArray();
    }
}

public sealed record AreaAuthoringProfile
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; }
    public IReadOnlyList<AreaUserEvent> UserEvents { get; }
    public AreaMapProfile Map { get; }
    public IReadOnlyList<AreaEditorLayer> Layers { get; }
    public AreaAuthoringProfile(int schemaVersion, IReadOnlyList<AreaUserEvent> userEvents, AreaMapProfile map, IReadOnlyList<AreaEditorLayer> layers)
    {
        if (schemaVersion != CurrentSchemaVersion) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        if (userEvents.Select(item => item.Id).Distinct().Count() != userEvents.Count || userEvents.Select(item => item.NamespacedName).Distinct(StringComparer.Ordinal).Count() != userEvents.Count || layers.Select(item => item.Id).Distinct().Count() != layers.Count) throw new ArgumentException("Authoring IDs and event names must be unique.");
        var ids = layers.Select(item => item.Id).ToHashSet(); if (layers.Any(item => item.ParentId is not null && (!ids.Contains(item.ParentId.Value) || item.ParentId == item.Id))) throw new ArgumentException("Layer parent is invalid.");
        SchemaVersion = schemaVersion; UserEvents = userEvents.OrderBy(item => item.NamespacedName, StringComparer.Ordinal).ToArray(); Map = map; Layers = layers.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
    }
    public static AreaAuthoringProfile CreateDefault() => new(CurrentSchemaVersion, [], new(true, true, "orthographic", 1, []), []);
}
