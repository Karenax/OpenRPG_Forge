using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Tests.Placeables;

public sealed class PlaceableCompositionEventsRuntimeAdapterTests
{
    [Fact]
    public void Damage_and_destruction_use_uniform_typed_entity_payloads()
    {
        var placeable = PlaceableSecurityEventsRuntimeAdapterTests.Placeable();
        var adapter = new PlaceableCompositionEventsRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;
        var damaged = adapter.Damage(handle, placeable, "player", 20, "force", 50, 30,
            new Dictionary<string, string> { ["weapon"] = "hammer" });
        var destroyed = adapter.Damage(handle, placeable, "player", 30, "force", 30, 0);

        Assert.Equal("DamageApplied", damaged.Binding);
        Assert.Equal("DamageApplied", damaged.Payload.Name);
        Assert.Equal("placeable", damaged.Payload.EntityKind);
        Assert.Equal("player", damaged.Payload.SourceId);
        Assert.Equal(20, damaged.Payload.Amount);
        Assert.Equal("force", damaged.Payload.DamageType);
        Assert.Equal(50, damaged.Payload.PreviousHealth);
        Assert.Equal(30, damaged.Payload.CurrentHealth);
        Assert.Equal("hammer", damaged.Payload.Context["weapon"]);
        Assert.Equal("EntityDestroyed", destroyed.Binding);
        Assert.Equal("EntityDestroyed", destroyed.Payload.Name);
    }

    [Fact]
    public void Attachment_snaps_to_compatible_socket_and_keeps_reversible_prefab_patch()
    {
        var placeable = PlaceableSecurityEventsRuntimeAdapterTests.Placeable();
        var adapter = new PlaceableCompositionEventsRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;
        var runtime = adapter.Build(handle, placeable);
        var authored = Assert.Single(runtime.Attachments);
        var socket = Assert.Single(runtime.Sockets);
        var attached = adapter.Attach(handle, placeable, authored.Id);

        Assert.Equal(socket.LocalTransform, attached.Attached.Transform);
        Assert.Equal("prefab:crypt-lever", attached.Attached.PrefabId);
        Assert.Equal("lever", attached.Attached.SocketId);
        Assert.Contains("attach:/sockets/lever", attached.Patch);
        Assert.Contains("detach:/sockets/lever", attached.InversePatch);
    }

    [Fact]
    public void Multi_verbs_cover_standard_and_custom_actions_with_contextual_ordering()
    {
        var placeable = PlaceableSecurityEventsRuntimeAdapterTests.Placeable();
        var adapter = new PlaceableCompositionEventsRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;
        var available = adapter.ResolveVerbs(handle, placeable,
            new HashSet<string> { "unlocked", "interlocutor", "portable", "climbable", "ash-rune" });
        var anchored = adapter.ResolveVerbs(handle, placeable,
            new HashSet<string>
                { "unlocked", "interlocutor", "portable", "climbable", "ash-rune", "anchored" });

        Assert.Equal(8, available.Count);
        Assert.Equal(["push", "take", "custom-ring", "inspect", "use", "open", "talk", "climb"],
            available.Select(item => item.Id));
        Assert.DoesNotContain(anchored, item => item.Id == "push");
        Assert.Equal("RingRequested", available.Single(item => item.Id == "custom-ring").EventName);
    }

    [Fact]
    public void Composition_resolution_is_optional_required_and_stale_safe()
    {
        var placeable = PlaceableSecurityEventsRuntimeAdapterTests.Placeable();
        var adapter = new PlaceableCompositionEventsRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;

        Assert.True(adapter.Resolve(placeable with
        {
            CompositionEvents = null,
            CompositionEventsRequiredAtRuntime = false,
        }).OptionalFallback);
        Assert.Equal("WORLD_PLACEABLE_COMPOSITION_EVENTS_REQUIRED_MISSING", adapter.Resolve(placeable with
        {
            CompositionEvents = null,
            CompositionEventsRequiredAtRuntime = true,
        }).DiagnosticCode);
        Assert.Throws<InvalidOperationException>(() => adapter.Build(
            handle with { ProfileId = Guid.NewGuid() }, placeable));
    }
}
