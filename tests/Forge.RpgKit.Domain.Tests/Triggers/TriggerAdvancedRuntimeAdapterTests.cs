using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Application.Triggers;
using Forge.RpgKit.Domain.Triggers;

namespace Forge.RpgKit.Domain.Tests.Triggers;

public sealed class TriggerAdvancedRuntimeAdapterTests
{
    [Fact]
    public void Trap_supports_detection_disarm_payload_reset_ownership_and_gm_visualization()
    {
        var trigger = Trigger();
        var adapter = new TriggerAdvancedRuntimeAdapter();
        var handle = adapter.Resolve(trigger).Handle!;
        var runtime = adapter.Build(handle, trigger);
        var detected = adapter.Detect(handle, trigger, 10, "outsider");
        var disarmed = adapter.Disarm(handle, trigger, 12);
        var reset = adapter.Reset(handle, trigger, disarmed.Trap);

        Assert.True(detected.Succeeded);
        Assert.True(disarmed.Succeeded);
        Assert.False(disarmed.Trap.Armed);
        Assert.True(reset.Trap.Armed);
        Assert.Equal("damage:ember-burst", runtime.Trap.Trap.PayloadId);
        Assert.Equal("ash-watch", runtime.Trap.Trap.OwnerId);
        Assert.True(runtime.Trap.GameMasterVisualization);
        Assert.Equal("#ff7a45", runtime.Trap.VisualizationColor);
    }

    [Fact]
    public void Typed_target_query_covers_players_tags_factions_components_owner_team_and_sandbox()
    {
        var trigger = Trigger();
        var adapter = new TriggerAdvancedRuntimeAdapter();
        var handle = adapter.Resolve(trigger).Handle!;
        var runtime = adapter.Build(handle, trigger);
        var entity = new QueryableEntity("player", true, Set("player", "hero"),
            runtime.TargetFilter.FactionIds, Set("inventory", "health"), "party", "heroes",
            new Dictionary<string, string> { ["quest"] = "active" });

        Assert.True(adapter.Matches(handle, trigger, entity));
        Assert.False(adapter.Matches(handle, trigger, entity with { IsPlayer = false }));
        Assert.False(adapter.Matches(handle, trigger, entity with
        {
            State = new Dictionary<string, string> { ["quest"] = "complete" },
        }));
    }

    [Fact]
    public void Gameplay_library_maps_all_ten_typed_volume_effects_and_bindings()
    {
        var trigger = Trigger();
        var adapter = new TriggerAdvancedRuntimeAdapter();
        var handle = adapter.Resolve(trigger).Handle!;
        var runtime = adapter.Build(handle, trigger);

        Assert.Equal(10, runtime.GameplayVolumes.Count);
        Assert.Equal(10, runtime.GameplayVolumes.Select(item => item.Kind).Distinct().Count());
        Assert.All(runtime.GameplayVolumes, item =>
        {
            Assert.Equal($"{item.Kind}VolumeEntered", item.EnterEvent);
            Assert.Equal($"{item.Kind}VolumeExited", item.ExitEvent);
        });
        var effect = adapter.Apply(handle, trigger, TriggerGameplayVolumeKind.Damage, "player");
        Assert.Equal("DamageVolumeEntered", effect.EventName);
        Assert.Equal("-10", effect.Changes["health"]);
    }

    [Fact]
    public void Occupancy_overlay_is_bounded_and_resolution_is_optional_required_and_stale_safe()
    {
        var trigger = Trigger();
        var adapter = new TriggerAdvancedRuntimeAdapter();
        var handle = adapter.Resolve(trigger).Handle!;
        var now = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var state = new TriggerRuntimeState(
            new Dictionary<string, DateTimeOffset> { ["player"] = now.AddSeconds(-3) },
            new Dictionary<string, DateTimeOffset> { ["player"] = now.AddSeconds(-2) },
            Set("player"), false);
        var overlay = adapter.Debug(handle, trigger, state, now,
            [new(now, "DamageVolumeEntered", "player", "fired")]);

        Assert.Equal("player", Assert.Single(overlay.Occupants));
        Assert.True(overlay.CooldownsRemaining["player"] > TimeSpan.Zero);
        Assert.Contains("required:player", overlay.FilterSummary);
        Assert.Equal("DamageVolumeEntered", Assert.Single(overlay.EventTrace).EventName);
        Assert.True(adapter.Resolve(trigger with
        {
            Advanced = null,
            AdvancedRequiredAtRuntime = false,
        }).OptionalFallback);
        Assert.Equal("WORLD_TRIGGER_ADVANCED_REQUIRED_MISSING", adapter.Resolve(trigger with
        {
            Advanced = null,
            AdvancedRequiredAtRuntime = true,
        }).DiagnosticCode);
        Assert.Throws<InvalidOperationException>(() => adapter.Build(
            handle with { ProfileId = Guid.NewGuid() }, trigger));
    }

    internal static TriggerWorldProfile Trigger()
    {
        var id = Guid.Parse("a0960000-0000-4000-8000-000000000001");
        var destination = Guid.Parse("a0960000-0000-4000-8000-000000000002");
        var foundation = new TriggerWorldProfile.FoundationPolicy(
            Guid.Parse("a0960000-0000-4000-8000-000000000003"), 1, "test:r096", "advanced-trigger",
            new(TriggerWorldProfile.ShapeKind.ExtrudedPolygon2D,
                [new(-2, 0, -2), new(2, 0, -2), new(2, 0, 2), new(-2, 0, 2)], [], new(4, 3, 4),
                3, .25, true),
            new(["creature"], ["player"], ["undead"], ["characters"]), ["quest"], 5,
            TriggerWorldProfile.FirePolicy.OncePerEntity,
            [new(Guid.Parse("a0960000-0000-4000-8000-000000000004"), "script", "encounter",
                new Dictionary<string, string>())],
            new(TriggerWorldProfile.DestinationKind.Waypoint, destination, "anchor", new(0, 180, 0), true,
                ["portal.travel"]),
            new(Vector3Value.Zero, Vector3Value.Zero, Vector3Value.One, null),
            new Dictionary<string, string> { ["editor.overlay"] = "volume" },
            new(Guid.Parse("a0960000-0000-4000-8000-000000000005"), 1, "test:r096", "1.0.0",
                "VolumeEntered", null, null),
            new(Guid.Parse("a0960000-0000-4000-8000-000000000006"), 1, "test:r096", "1.0.0",
                "VolumeExited", null, null));
        var advanced = new TriggerAdvancedProfile(
            Guid.Parse("a0960000-0000-4000-8000-000000000007"), 1, "test:r096", "1.0.0",
            new(Guid.Parse("a0960000-0000-4000-8000-000000000008"), "enter", "damage:ember-burst",
                10, 12, 30, "ash-watch", "vfx:ember-burst", "ash-watch", true, false, true, "#ff7a45",
                "TrapDetected", "TrapDisarmed", "TrapTriggered", "TrapReset"),
            new(Guid.Parse("a0960000-0000-4000-8000-000000000009"), true, ["player"], ["undead"],
                [Guid.Parse("a0960000-0000-4000-8000-000000000010")], ["inventory", "health"],
                "party", "heroes", "quest==active"),
            Enum.GetValues<TriggerGameplayVolumeKind>().Select((kind, index) => Volume(kind, index)).ToArray(),
            new(Guid.Parse("a0960000-0000-4000-8000-000000000020"), true, true, true, true, true, true,
                100));
        return new(id, 1, "test:r096", BlueprintId.Parse("a0960000-0000-4000-8000-000000000021"),
            AreaId.Parse("a0960000-0000-4000-8000-000000000022"), null, null, null, "test.trigger", null,
            null, null, null, null, foundation, true, advanced, true);
    }

    private static TriggerGameplayVolumeProfile Volume(TriggerGameplayVolumeKind kind, int index)
    {
        var parameter = kind switch
        {
            TriggerGameplayVolumeKind.Damage => ("health", -10d),
            TriggerGameplayVolumeKind.Heal => ("health", 10d),
            TriggerGameplayVolumeKind.Gravity => ("gravity", .5d),
            TriggerGameplayVolumeKind.Water => ("movement", .6d),
            TriggerGameplayVolumeKind.Climb => ("climb", 1d),
            TriggerGameplayVolumeKind.NoBuild => ("build", 0d),
            TriggerGameplayVolumeKind.Camera => ("camera", 1d),
            TriggerGameplayVolumeKind.Audio => ("audio", 1d),
            TriggerGameplayVolumeKind.Weather => ("weather", 1d),
            TriggerGameplayVolumeKind.Checkpoint => ("checkpoint", 1d),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return new(Guid.Parse($"a0960000-0000-4000-8000-{(30 + index):000000000000}"), kind,
            new Dictionary<string, double> { [parameter.Item1] = parameter.Item2 },
            new Dictionary<string, string>(), $"{kind}VolumeEntered", $"{kind}VolumeExited", true);
    }

    private static IReadOnlySet<string> Set(params string[] values) =>
        values.ToHashSet(StringComparer.Ordinal);
}
