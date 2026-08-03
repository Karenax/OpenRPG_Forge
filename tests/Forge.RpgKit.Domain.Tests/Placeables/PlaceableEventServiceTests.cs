using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Tests.Placeables;

public sealed class PlaceableEventServiceTests
{
    [Fact]
    public void Emits_controlled_before_after_lock_events_and_honors_veto()
    {
        var component = Lock(); var placeable = Guid.NewGuid();
        var vetoed = PlaceableEventService.ChangeLockState(component, placeable, LockState.Unlocked, "credential", "key:lighthouse", "player", "runtime:test", _ => "quest-gate");
        Assert.True(vetoed.Changing.Vetoed); Assert.Null(vetoed.Changed); Assert.Equal(LockState.Locked, vetoed.Lock.State);
        var changed = PlaceableEventService.ChangeLockState(component, placeable, LockState.Unlocked, "credential", "key:lighthouse", "player", "runtime:test");
        Assert.Equal("LockStateChanging", changed.Changing.Name); Assert.Equal("LockStateChanged", changed.Changed!.Name); Assert.Equal(LockState.Unlocked, changed.Lock.State);
    }

    [Fact]
    public void Emits_transactional_container_delta_and_uniform_damage_destroyed_payload()
    {
        var placeable = Guid.NewGuid(); var item = Guid.NewGuid();
        var changed = PlaceableEventService.ContentsChanged(placeable, 4, [new(item, -1, "take", "lighthouse", "player")], "player-loot", "player", "runtime:test");
        Assert.Equal(5, changed.CurrentRevision); Assert.Equal(-1, changed.Delta[0].QuantityDelta); Assert.Equal("player-loot", changed.Cause);
        var damage = PlaceableEventService.DamageEvent(placeable, "player", 20, "force", 50, 30, "runtime:test"); var destroyed = PlaceableEventService.DamageEvent(placeable, "player", 30, "force", 30, 0, "runtime:test");
        Assert.Equal("DamageApplied", damage.Name); Assert.Equal("EntityDestroyed", destroyed.Name); Assert.Equal("placeable", destroyed.EntityKind);
    }

    [Fact]
    public void Snaps_attachments_to_socket_and_preserves_composed_prefab()
    {
        var transform = Transform(new(0, 1.2, 0)); var socket = new AttachmentSocket("lever", "mechanism", transform, new HashSet<string> { "lever", "vfx" }, true, 2);
        var attachment = new PlaceableAttachment(Guid.NewGuid(), "lever", Guid.NewGuid(), new HashSet<string> { "lever" }, Transform(new(9, 9, 9)), "prefab:lighthouse-console");
        var result = PlaceableEventService.Attach([], socket, attachment);
        Assert.Equal(transform, result.Attached.Transform); Assert.Equal("prefab:lighthouse-console", result.Attached.PrefabId); Assert.Contains("attach:", result.Patch);
    }

    [Fact]
    public void Orders_standard_and_custom_verbs_by_context()
    {
        var verbs = new[] { Verb("custom-sing", 5, Set("music")), Verb("open", 5, Set("unlocked")), Verb("inspect", 5, Set()), Verb("take", 9, Set("portable")), new ContextualVerb("push", "Pousser", 10, new HashSet<string>(), new HashSet<string> { "anchored" }, "PushRequested") };
        var resolved = PlaceableEventService.ResolveVerbs(verbs, new HashSet<string> { "music", "unlocked", "portable", "anchored" });
        Assert.Equal(["take", "inspect", "open", "custom-sing"], resolved.Select(item => item.Id));
    }

    private static LockComponent Lock() => new(LockState.Locked, new HashSet<string> { "key:lighthouse" }, "mechanics", 10, null, false, true, false, new HashSet<string>());
    private static PlaceableTransform Transform(Vector3Value position) => new(position, new(0, 0, 0), new(1, 1, 1), new(0, 0, 0), 0, null, false, null);
    private static ContextualVerb Verb(string id, int priority, IReadOnlySet<string> required) => new(id, id, priority, required, new HashSet<string>(), $"{id}-requested");
    private static IReadOnlySet<string> Set(params string[] values) => values.ToHashSet(StringComparer.Ordinal);
}
