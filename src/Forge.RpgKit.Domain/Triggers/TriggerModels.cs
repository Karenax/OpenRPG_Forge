using Forge.RpgKit.Domain.Doors;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Triggers;

public enum TriggerShapeKind { ExtrudedPolygon, Box, Sphere, Capsule, ConvexHull }
public enum TriggerFirePolicy { Once, OncePerEntity, Repeat, WhileInside }
public sealed record TriggerShape(TriggerShapeKind Kind, IReadOnlyList<Vector3Value> Vertices, IReadOnlyList<IReadOnlyList<Vector3Value>> Holes, Vector3Value Size, double Height, double SnapIncrement);
public sealed record TriggerFilter(IReadOnlySet<string> EntityKinds, IReadOnlySet<string> RequiredTags, IReadOnlySet<string> ExcludedTags, IReadOnlySet<string> Layers);
public sealed record TriggerAction(string Kind, string TargetId, IReadOnlyDictionary<string, string> Parameters);
public sealed record TriggerVolume(Guid Id, int SchemaVersion, string Provenance, string Slug, TriggerShape Shape, TriggerFilter Filter, IReadOnlySet<string> Conditions, TimeSpan Cooldown, TriggerFirePolicy FirePolicy, IReadOnlyList<TriggerAction> Actions, PortalComponent? Portal);
public sealed record TriggerDiagnostic(string Code, string Path, string Message);
public sealed record TriggerEntity(string Id, string Kind, IReadOnlySet<string> Tags, IReadOnlySet<string> Layers);
public sealed record VolumeEntered(Guid EventId, int SchemaVersion, string Provenance, Guid TriggerId, string EntityId, Vector3Value Point, Vector3Value Direction, IReadOnlySet<string> Layers, IReadOnlyDictionary<string, string> State, DateTimeOffset EnteredAt);
public sealed record VolumeExited(Guid EventId, int SchemaVersion, string Provenance, Guid TriggerId, string EntityId, TimeSpan DurationInside, string Reason, DateTimeOffset ExitedAt);
public sealed record TriggerRuntimeState(IReadOnlyDictionary<string, DateTimeOffset> InsideSince, IReadOnlyDictionary<string, DateTimeOffset> LastFiredAt, IReadOnlySet<string> FiredEntities, bool FiredGlobally);
public sealed record TriggerEnterResult(TriggerRuntimeState State, VolumeEntered? Event, IReadOnlyList<TriggerAction> Actions, bool Fired, string Reason);
public sealed record TriggerExitResult(TriggerRuntimeState State, VolumeExited? Event);

public static class TriggerRules
{
    public static IReadOnlyList<TriggerDiagnostic> Validate(TriggerVolume trigger)
    {
        var diagnostics = new List<TriggerDiagnostic>(); var root = $"triggers/{trigger.Id}";
        if (trigger.Id == Guid.Empty || trigger.SchemaVersion != 1 || string.IsNullOrWhiteSpace(trigger.Provenance) || string.IsNullOrWhiteSpace(trigger.Slug)) diagnostics.Add(new("RPG_TRIGGER_IDENTITY_INVALID", root, "Trigger identity is invalid."));
        if (trigger.Shape.Kind == TriggerShapeKind.ExtrudedPolygon && (trigger.Shape.Vertices.Count < 3 || trigger.Shape.Height <= 0)) diagnostics.Add(new("RPG_TRIGGER_POLYGON_INVALID", $"{root}/shape", "Extruded polygon requires three vertices and positive height."));
        if (trigger.Shape.Holes.Any(hole => hole.Count < 3)) diagnostics.Add(new("RPG_TRIGGER_HOLE_INVALID", $"{root}/shape/holes", "Polygon holes require at least three vertices."));
        if (trigger.Shape.Kind != TriggerShapeKind.ExtrudedPolygon && (trigger.Shape.Size.X <= 0 || trigger.Shape.Size.Y <= 0 || trigger.Shape.Size.Z <= 0)) diagnostics.Add(new("RPG_TRIGGER_SHAPE_INVALID", $"{root}/shape", "3D shape size must be positive."));
        if (trigger.Shape.SnapIncrement < 0 || trigger.Cooldown < TimeSpan.Zero || trigger.Actions.Count == 0) diagnostics.Add(new("RPG_TRIGGER_POLICY_INVALID", root, "Snap, cooldown and actions are invalid."));
        if (trigger.Portal is { } portal && (portal.DestinationId == Guid.Empty || string.IsNullOrWhiteSpace(portal.SpawnAnchorId))) diagnostics.Add(new("RPG_TRIGGER_PORTAL_INVALID", $"{root}/portal", "Portal destination and spawn anchor are required."));
        return diagnostics;
    }

    public static TriggerShape Snap(TriggerShape shape)
    {
        if (shape.SnapIncrement <= 0) return shape;
        Vector3Value SnapVertex(Vector3Value value) { double S(double coordinate) => Math.Round(coordinate / shape.SnapIncrement, MidpointRounding.AwayFromZero) * shape.SnapIncrement; return new(S(value.X), S(value.Y), S(value.Z)); }
        return shape with { Vertices = shape.Vertices.Select(SnapVertex).ToArray(), Holes = shape.Holes.Select(hole => (IReadOnlyList<Vector3Value>)hole.Select(SnapVertex).ToArray()).ToArray() };
    }

    public static string RuntimePreview(TriggerVolume trigger) => $"{trigger.Shape.Kind}|vertices:{trigger.Shape.Vertices.Count}|holes:{trigger.Shape.Holes.Count}|height:{trigger.Shape.Height:0.##}|actions:{trigger.Actions.Count}";
}

public static class TriggerRuntimeService
{
    public static TriggerEnterResult Enter(TriggerVolume trigger, TriggerRuntimeState state, TriggerEntity entity, Vector3Value point, Vector3Value direction, IReadOnlyDictionary<string, string> context, DateTimeOffset at)
    {
        if (!Matches(trigger.Filter, entity)) return new(state, null, [], false, "filtered");
        var inside = state.InsideSince.ToDictionary(); inside[entity.Id] = at; var updated = state with { InsideSince = inside };
        var conditions = trigger.Conditions.All(condition => context.GetValueOrDefault(condition) == "true"); if (!conditions) return new(updated, null, [], false, "condition-failed");
        if (trigger.FirePolicy == TriggerFirePolicy.Once && state.FiredGlobally) return new(updated, null, [], false, "already-fired");
        if (trigger.FirePolicy == TriggerFirePolicy.OncePerEntity && state.FiredEntities.Contains(entity.Id)) return new(updated, null, [], false, "entity-already-fired");
        if (state.LastFiredAt.TryGetValue(entity.Id, out var last) && at - last < trigger.Cooldown) return new(updated, null, [], false, "cooldown");
        var lastFired = state.LastFiredAt.ToDictionary(); lastFired[entity.Id] = at; var firedEntities = state.FiredEntities.Append(entity.Id).ToHashSet(StringComparer.Ordinal);
        updated = updated with { LastFiredAt = lastFired, FiredEntities = firedEntities, FiredGlobally = state.FiredGlobally || trigger.FirePolicy == TriggerFirePolicy.Once };
        var entered = new VolumeEntered(Guid.NewGuid(), 1, trigger.Provenance, trigger.Id, entity.Id, point, direction, entity.Layers, context, at);
        return new(updated, entered, trigger.Actions, true, "fired");
    }

    public static TriggerExitResult Exit(TriggerVolume trigger, TriggerRuntimeState state, string entityId, DateTimeOffset at, string reason)
    {
        if (!state.InsideSince.TryGetValue(entityId, out var enteredAt)) return new(state, null);
        var inside = state.InsideSince.ToDictionary(); inside.Remove(entityId); var updated = state with { InsideSince = inside };
        return new(updated, new(Guid.NewGuid(), 1, trigger.Provenance, trigger.Id, entityId, at - enteredAt, reason, at));
    }

    private static bool Matches(TriggerFilter filter, TriggerEntity entity) => (filter.EntityKinds.Count == 0 || filter.EntityKinds.Contains(entity.Kind)) && filter.RequiredTags.All(entity.Tags.Contains) && !filter.ExcludedTags.Any(entity.Tags.Contains) && (filter.Layers.Count == 0 || filter.Layers.Overlaps(entity.Layers));
}
