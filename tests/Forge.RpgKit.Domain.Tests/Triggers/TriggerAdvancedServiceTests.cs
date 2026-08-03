using Forge.RpgKit.Domain.Doors;
using Forge.RpgKit.Domain.Placeables;
using Forge.RpgKit.Domain.Triggers;

namespace Forge.RpgKit.Domain.Tests.Triggers;

public sealed class TriggerAdvancedServiceTests
{
    [Fact]
    public void Evaluates_typed_entity_query_for_players_tags_factions_components_owner_team_and_sandbox()
    {
        var faction = Guid.NewGuid(); var query = new EntityQuery(true, Set("hero"), Set("undead"), new HashSet<Guid> { faction }, Set("inventory"), "party", "village", "quest==active");
        var entity = new QueryableEntity("player", true, Set("hero"), new HashSet<Guid> { faction }, Set("inventory", "dialogue"), "party", "village", new Dictionary<string, string> { ["quest"] = "active" });
        Assert.True(TriggerAdvancedService.Matches(query, entity)); Assert.False(TriggerAdvancedService.Matches(query, entity with { IsPlayer = false })); Assert.False(TriggerAdvancedService.Matches(query, entity with { State = new Dictionary<string, string> { ["quest"] = "done" } }));
    }

    [Fact]
    public void Supports_trap_detection_disarm_reset_and_game_master_visualization()
    {
        var trap = new TrapComponent(Guid.NewGuid(), "enter", "damage:glass", 10, 12, TimeSpan.FromSeconds(30), "glass", "vfx:shards", "village", true, false); var volume = new TrapVolume(trap, Trigger().Shape, true, "#ff3366");
        Assert.False(TriggerAdvancedService.DetectTrap(volume, 9, "outsider").Succeeded); var disarmed = TriggerAdvancedService.DisarmTrap(volume, 13); Assert.False(disarmed.Trap.Armed); Assert.True(TriggerAdvancedService.ResetTrap(volume with { Trap = disarmed.Trap }).Trap.Armed); Assert.True(volume.GameMasterVisualization);
    }

    [Fact]
    public void Provides_all_ten_gameplay_volume_library_entries()
    {
        var definitions = Enum.GetValues<GameplayVolumeKind>().Select(TriggerAdvancedService.Library).ToArray(); Assert.Equal(10, definitions.Length); Assert.Equal(10, definitions.Select(item => item.Kind).Distinct().Count());
        var effect = TriggerAdvancedService.Apply(definitions.Single(item => item.Kind == GameplayVolumeKind.Damage), "player"); Assert.Equal("-10", effect.Changes["health"]); Assert.Equal("DamageVolumeEntered", effect.EventName);
    }

    [Fact]
    public void Validates_geometry_filters_actions_portal_scripts_accessibility_and_builds_debug_overlay()
    {
        var trigger = Trigger() with { Shape = Trigger().Shape with { Vertices = [new(0, 0, 0), new(2, 0, 2), new(0, 0, 2), new(2, 0, 0)] }, Actions = [new("script", "missing-target", new Dictionary<string, string> { ["script"] = "missing-script" })] };
        var query = new EntityQuery(false, Set("hero"), Set("hero"), new HashSet<Guid>(), Set(), null, null, null); var context = new TriggerValidationContext(new HashSet<Guid>(), Set("other-target"), Set("other-script"), Set("vehicles"));
        var codes = TriggerAdvancedService.Validate(trigger, query, context).Select(item => item.Code).ToHashSet(); Assert.Contains("RPG_TRIGGER_GEOMETRY_SELF_INTERSECTS", codes); Assert.Contains("RPG_TRIGGER_FILTER_CONTRADICTION", codes); Assert.Contains("RPG_TRIGGER_ACTION_TARGET_MISSING", codes); Assert.Contains("RPG_TRIGGER_SCRIPT_MISSING", codes); Assert.Contains("RPG_TRIGGER_PORTAL_DESTINATION_MISSING", codes); Assert.Contains("RPG_TRIGGER_INACCESSIBLE", codes);
        var now = DateTimeOffset.UnixEpoch.AddSeconds(10); var state = new TriggerRuntimeState(new Dictionary<string, DateTimeOffset> { ["player"] = now.AddSeconds(-3) }, new Dictionary<string, DateTimeOffset> { ["player"] = now.AddSeconds(-2) }, Set("player"), false); var overlay = TriggerAdvancedService.Debug(trigger, state, now, [new(now, "VolumeEntered", "player", "fired")]);
        Assert.Equal("player", Assert.Single(overlay.Occupants)); Assert.True(overlay.CooldownsRemaining["player"] > TimeSpan.Zero); Assert.Contains("required:player", overlay.FilterSummary);
    }

    private static TriggerVolume Trigger()
    {
        var destination = Guid.NewGuid(); return new(Guid.NewGuid(), 1, "author:test", "volume", new(TriggerShapeKind.ExtrudedPolygon, [new(0, 0, 0), new(2, 0, 0), new(2, 0, 2), new(0, 0, 2)], [], new(0, 0, 0), 2, 0), new(Set("creature"), Set("player"), Set("undead"), Set("characters")), Set(), TimeSpan.FromSeconds(5), TriggerFirePolicy.Repeat, [new("script", "target", new Dictionary<string, string> { ["script"] = "on_enter" })], new(PortalDestinationKind.Waypoint, destination, "anchor", new(0, 0, 0), true, Set()));
    }
    private static IReadOnlySet<string> Set(params string[] values) => values.ToHashSet(StringComparer.Ordinal);
}
