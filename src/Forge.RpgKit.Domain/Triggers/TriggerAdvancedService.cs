using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Triggers;

public sealed record EntityQuery(bool PlayersOnly, IReadOnlySet<string> RequiredTags, IReadOnlySet<string> ExcludedTags, IReadOnlySet<Guid> FactionIds, IReadOnlySet<string> RequiredComponents, string? OwnerId, string? TeamId, string? SandboxPredicate);
public sealed record QueryableEntity(string Id, bool IsPlayer, IReadOnlySet<string> Tags, IReadOnlySet<Guid> FactionIds, IReadOnlySet<string> Components, string? OwnerId, string? TeamId, IReadOnlyDictionary<string, string> State);
public sealed record TrapVolume(TrapComponent Trap, TriggerShape Shape, bool GameMasterVisualization, string VisualizationColor);
public enum GameplayVolumeKind { Damage, Heal, Gravity, Water, Climb, NoBuild, Camera, Audio, Weather, Checkpoint }
public sealed record GameplayVolumeDefinition(GameplayVolumeKind Kind, IReadOnlyDictionary<string, double> NumericParameters, IReadOnlyDictionary<string, string> TextParameters, string EnterEvent, string ExitEvent);
public sealed record GameplayVolumeEffect(string EntityId, GameplayVolumeKind Kind, IReadOnlyDictionary<string, string> Changes, string EventName);
public sealed record TriggerValidationContext(IReadOnlySet<Guid> KnownPortalDestinations, IReadOnlySet<string> KnownTargets, IReadOnlySet<string> KnownScripts, IReadOnlySet<string> AccessibleLayers);
public sealed record TriggerEventTrace(DateTimeOffset At, string EventName, string EntityId, string Result);
public sealed record TriggerDebugOverlay(Guid TriggerId, string Geometry, IReadOnlyList<string> Occupants, IReadOnlyDictionary<string, DateTimeOffset> LastFires, IReadOnlyDictionary<string, TimeSpan> CooldownsRemaining, string FilterSummary, IReadOnlyList<TriggerEventTrace> EventTrace);

public static class TriggerAdvancedService
{
    public static bool Matches(EntityQuery query, QueryableEntity entity)
    {
        if (query.PlayersOnly && !entity.IsPlayer) return false;
        if (!query.RequiredTags.All(entity.Tags.Contains) || query.ExcludedTags.Any(entity.Tags.Contains)) return false;
        if (query.FactionIds.Count > 0 && !query.FactionIds.Overlaps(entity.FactionIds)) return false;
        if (!query.RequiredComponents.All(entity.Components.Contains)) return false;
        if (query.OwnerId is not null && query.OwnerId != entity.OwnerId || query.TeamId is not null && query.TeamId != entity.TeamId) return false;
        return EvaluateSandboxPredicate(query.SandboxPredicate, entity.State);
    }

    public static TrapResult DetectTrap(TrapVolume volume, double perception, string actorFaction) => PlaceableSecurityService.Detect(volume.Trap, perception, actorFaction);
    public static TrapResult DisarmTrap(TrapVolume volume, double skill) => PlaceableSecurityService.Disarm(volume.Trap, skill);
    public static TrapVolume ResetTrap(TrapVolume volume) => volume with { Trap = PlaceableSecurityService.Reset(volume.Trap) };

    public static GameplayVolumeDefinition Library(GameplayVolumeKind kind) => kind switch
    {
        GameplayVolumeKind.Damage => Definition(kind, "health", -10),
        GameplayVolumeKind.Heal => Definition(kind, "health", 10),
        GameplayVolumeKind.Gravity => Definition(kind, "gravity", 0.5),
        GameplayVolumeKind.Water => Definition(kind, "movement", 0.6),
        GameplayVolumeKind.Climb => Definition(kind, "climb", 1),
        GameplayVolumeKind.NoBuild => Definition(kind, "build", 0),
        GameplayVolumeKind.Camera => Definition(kind, "camera", 1),
        GameplayVolumeKind.Audio => Definition(kind, "audio", 1),
        GameplayVolumeKind.Weather => Definition(kind, "weather", 1),
        GameplayVolumeKind.Checkpoint => Definition(kind, "checkpoint", 1),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static GameplayVolumeEffect Apply(GameplayVolumeDefinition definition, string entityId)
        => new(entityId, definition.Kind, definition.NumericParameters.ToDictionary(item => item.Key, item => item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal), definition.EnterEvent);

    public static IReadOnlyList<TriggerDiagnostic> Validate(TriggerVolume trigger, EntityQuery query, TriggerValidationContext context)
    {
        var diagnostics = TriggerRules.Validate(trigger).ToList(); var root = $"triggers/{trigger.Id}";
        if (trigger.Shape.Kind == TriggerShapeKind.ExtrudedPolygon && SelfIntersects(trigger.Shape.Vertices)) diagnostics.Add(new("RPG_TRIGGER_GEOMETRY_SELF_INTERSECTS", $"{root}/shape", "Trigger polygon self-intersects."));
        if (query.RequiredTags.Overlaps(query.ExcludedTags)) diagnostics.Add(new("RPG_TRIGGER_FILTER_CONTRADICTION", $"{root}/filter", "A required tag is also excluded."));
        foreach (var action in trigger.Actions)
        {
            if (!context.KnownTargets.Contains(action.TargetId)) diagnostics.Add(new("RPG_TRIGGER_ACTION_TARGET_MISSING", $"{root}/actions/{action.TargetId}", "Action target is missing."));
            if (action.Parameters.TryGetValue("script", out var script) && !context.KnownScripts.Contains(script)) diagnostics.Add(new("RPG_TRIGGER_SCRIPT_MISSING", $"{root}/actions/{action.TargetId}/script", "Action script is missing."));
        }
        if (trigger.Portal is { } portal && !context.KnownPortalDestinations.Contains(portal.DestinationId)) diagnostics.Add(new("RPG_TRIGGER_PORTAL_DESTINATION_MISSING", $"{root}/portal", "Portal destination is missing."));
        if (trigger.Filter.Layers.Count > 0 && !trigger.Filter.Layers.Overlaps(context.AccessibleLayers)) diagnostics.Add(new("RPG_TRIGGER_INACCESSIBLE", $"{root}/filter/layers", "Trigger is not accessible from configured layers."));
        return diagnostics;
    }

    public static TriggerDebugOverlay Debug(TriggerVolume trigger, TriggerRuntimeState state, DateTimeOffset now, IReadOnlyList<TriggerEventTrace> trace)
    {
        var cooldowns = state.LastFiredAt.ToDictionary(item => item.Key, item => TimeSpan.FromTicks(Math.Max(0, (trigger.Cooldown - (now - item.Value)).Ticks)), StringComparer.Ordinal);
        var summary = $"kinds:{string.Join(',', trigger.Filter.EntityKinds.Order())}|required:{string.Join(',', trigger.Filter.RequiredTags.Order())}|excluded:{string.Join(',', trigger.Filter.ExcludedTags.Order())}|layers:{string.Join(',', trigger.Filter.Layers.Order())}";
        return new(trigger.Id, TriggerRules.RuntimePreview(trigger), state.InsideSince.Keys.Order(StringComparer.Ordinal).ToArray(), state.LastFiredAt, cooldowns, summary, trace.OrderBy(item => item.At).TakeLast(100).ToArray());
    }

    private static GameplayVolumeDefinition Definition(GameplayVolumeKind kind, string key, double value) => new(kind, new Dictionary<string, double> { [key] = value }, new Dictionary<string, string>(), $"{kind}VolumeEntered", $"{kind}VolumeExited");
    private static bool EvaluateSandboxPredicate(string? predicate, IReadOnlyDictionary<string, string> state)
    {
        if (string.IsNullOrWhiteSpace(predicate)) return true;
        var parts = predicate.Split("==", StringSplitOptions.TrimEntries); return parts.Length == 2 && state.GetValueOrDefault(parts[0]) == parts[1];
    }
    private static bool SelfIntersects(IReadOnlyList<Vector3Value> vertices)
    {
        for (var i = 0; i < vertices.Count; i++)
        {
            var a1 = vertices[i]; var a2 = vertices[(i + 1) % vertices.Count];
            for (var j = i + 1; j < vertices.Count; j++)
            {
                if (Math.Abs(i - j) <= 1 || i == 0 && j == vertices.Count - 1) continue;
                var b1 = vertices[j]; var b2 = vertices[(j + 1) % vertices.Count]; if (SegmentsIntersect(a1, a2, b1, b2)) return true;
            }
        }
        return false;
    }
    private static bool SegmentsIntersect(Vector3Value a, Vector3Value b, Vector3Value c, Vector3Value d)
    {
        static double Cross(Vector3Value p, Vector3Value q, Vector3Value r) => ((q.X - p.X) * (r.Z - p.Z)) - ((q.Z - p.Z) * (r.X - p.X));
        return Cross(a, b, c) * Cross(a, b, d) < 0 && Cross(c, d, a) * Cross(c, d, b) < 0;
    }
}
