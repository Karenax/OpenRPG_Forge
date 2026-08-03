using Forge.RpgKit.Application.Triggers;
using Forge.RpgKit.Domain.Doors;
using Forge.RpgKit.Domain.Placeables;
using Forge.RpgKit.Domain.Triggers;

namespace Forge.RpgKit.Domain.Tests.Triggers;

public sealed class TriggerFoundationTests
{
    [Fact]
    public void Authors_extruded_polygon_vertices_snap_holes_height_and_runtime_preview()
    {
        var trigger = Trigger(); Assert.Empty(TriggerRules.Validate(trigger)); Assert.Contains("holes:1", TriggerRules.RuntimePreview(trigger));
        var edit = TriggerShapeAuthoringService.MoveVertex(trigger.Shape, 1, new(2.24, 0, 0.24), false); Assert.Equal(new Vector3Value(2, 0, 0), edit.Shape.Vertices[1]); Assert.Contains("replace:/shape/vertices/1", edit.Patch);
        var preview = TriggerShapeAuthoringService.MoveVertex(trigger.Shape, 1, new(9, 0, 9), true); Assert.Equal(trigger.Shape, preview.Shape);
    }

    [Fact]
    public void Filters_conditions_cooldown_fire_policy_and_typed_actions()
    {
        var trigger = Trigger(); var state = EmptyState(); var entity = new TriggerEntity("player", "creature", new HashSet<string> { "player" }, new HashSet<string> { "characters" }); var context = new Dictionary<string, string> { ["lighthouse-powered"] = "true" }; var at = DateTimeOffset.UnixEpoch;
        var first = TriggerRuntimeService.Enter(trigger, state, entity, new(1, 0, 1), new(0, 0, 1), context, at); Assert.True(first.Fired); Assert.Equal("start-encounter", first.Actions[0].Kind);
        var second = TriggerRuntimeService.Enter(trigger, first.State, entity, new(1, 0, 1), new(0, 0, 1), context, at.AddSeconds(1)); Assert.False(second.Fired); Assert.Equal("entity-already-fired", second.Reason);
        var filtered = TriggerRuntimeService.Enter(trigger, state, entity with { Tags = new HashSet<string> { "undead" } }, new(0, 0, 0), new(0, 0, 1), context, at); Assert.Equal("filtered", filtered.Reason);
    }

    [Fact]
    public void Emits_enter_exit_payload_and_shares_typed_portal_component()
    {
        var trigger = Trigger(); var entity = new TriggerEntity("player", "creature", new HashSet<string> { "player" }, new HashSet<string> { "characters" }); var at = DateTimeOffset.UnixEpoch;
        var entered = TriggerRuntimeService.Enter(trigger, EmptyState(), entity, new(1, 0, 2), new(0, 0, 1), new Dictionary<string, string> { ["lighthouse-powered"] = "true" }, at);
        Assert.Equal(new Vector3Value(1, 0, 2), entered.Event!.Point); Assert.Contains("characters", entered.Event.Layers); Assert.Equal(PortalDestinationKind.Waypoint, trigger.Portal!.DestinationKind);
        var exited = TriggerRuntimeService.Exit(trigger, entered.State, entity.Id, at.AddSeconds(12), "walked-out"); Assert.Equal(TimeSpan.FromSeconds(12), exited.Event!.DurationInside); Assert.Equal("walked-out", exited.Event.Reason);
    }

    private static TriggerVolume Trigger()
    {
        var shape = new TriggerShape(TriggerShapeKind.ExtrudedPolygon, [new(0, 0, 0), new(2, 0, 0), new(2, 0, 2), new(0, 0, 2)], [[new(0.5, 0, 0.5), new(1, 0, 0.5), new(0.75, 0, 1)]], new(0, 0, 0), 3, 0.5);
        return new(Guid.NewGuid(), 1, "author:test", "lighthouse-threshold", shape, new(new HashSet<string> { "creature" }, new HashSet<string> { "player" }, new HashSet<string> { "undead" }, new HashSet<string> { "characters" }), new HashSet<string> { "lighthouse-powered" }, TimeSpan.FromSeconds(5), TriggerFirePolicy.OncePerEntity, [new("start-encounter", "encounter:glass-wave", new Dictionary<string, string>())], new(PortalDestinationKind.Waypoint, Guid.NewGuid(), "anchor:lighthouse", new(0, 180, 0), true, new HashSet<string>()));
    }
    private static TriggerRuntimeState EmptyState() => new(new Dictionary<string, DateTimeOffset>(), new Dictionary<string, DateTimeOffset>(), new HashSet<string>(), false);
}
