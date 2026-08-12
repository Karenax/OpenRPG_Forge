using Forge.Domain.Blueprints;
using Forge.Domain.Items;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Domain.Placeables;
using DomainVector3 = Forge.Domain.Spatial.Vector3Value;
using LootSpawnTiming = Forge.RpgKit.Domain.Placeables.LootSpawnTiming;

namespace Forge.RpgKit.Domain.Tests.Placeables;

public sealed class PlaceableSecurityEventsRuntimeAdapterTests
{
    [Fact]
    public void Trap_reuses_detection_disarm_trigger_reset_ownership_vfx_and_friendly_policy()
    {
        var placeable = Placeable();
        var adapter = new PlaceableSecurityEventsRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;
        var hidden = adapter.Detect(handle, placeable, 9, "outsider");
        var detected = adapter.Detect(handle, placeable, 10, "outsider");
        var friendly = adapter.Trigger(handle, placeable, "ash-watch");
        var triggered = adapter.Trigger(handle, placeable, "outsider");
        var disarmed = adapter.Disarm(handle, placeable, 12);

        Assert.False(hidden.Succeeded);
        Assert.True(detected.Succeeded);
        Assert.False(friendly.Succeeded);
        Assert.True(triggered.Succeeded);
        Assert.Contains("damage:ember", triggered.Detail);
        Assert.False(disarmed.Trap.Armed);
        Assert.True(adapter.Reset(handle, placeable, disarmed.Trap).Armed);
    }

    [Fact]
    public void Initial_treasure_honors_timing_seed_and_persisted_generated_state()
    {
        var placeable = Placeable();
        var adapter = new PlaceableSecurityEventsRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;
        var skipped = adapter.Spawn(handle, placeable, LootSpawnTiming.Initialize, new HashSet<string>());
        var spawned = adapter.Spawn(handle, placeable, LootSpawnTiming.Open, new HashSet<string>());
        var restored = adapter.Spawn(handle, placeable, LootSpawnTiming.Open, new HashSet<string>(), spawned.Spawner);

        Assert.Equal("LootSpawnSkipped", skipped.EventName);
        Assert.Equal("LootSpawned", spawned.EventName);
        Assert.Single(spawned.Results);
        Assert.True(restored.ReusedSavedState);
        Assert.Equal(spawned.Results, restored.Results);
    }

    [Fact]
    public void Open_and_close_bindings_preserve_actor_result_previous_state_and_authority()
    {
        var placeable = Placeable();
        var adapter = new PlaceableSecurityEventsRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;
        var opened = adapter.ContainerStateChanged(handle, placeable, true, "player", "success",
            "closed", "server", DateTimeOffset.UnixEpoch);
        var closed = adapter.ContainerStateChanged(handle, placeable, false, "player", "success",
            "open", "server", DateTimeOffset.UnixEpoch);

        Assert.Equal("ContainerOpened", opened.Binding);
        Assert.Equal("player", opened.Payload.ActorId);
        Assert.Equal("closed", opened.Payload.PreviousState);
        Assert.Equal("server", opened.Payload.Authority);
        Assert.Equal("ContainerClosed", closed.Binding);
    }

    [Fact]
    public void Lock_events_emit_controlled_before_after_payload_and_honor_veto()
    {
        var placeable = Placeable();
        var adapter = new PlaceableSecurityEventsRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;
        var vetoed = adapter.ChangeLockState(handle, placeable, LockState.Unlocked, "credential",
            "key:ember", "player", _ => "quest-gate");
        var changed = adapter.ChangeLockState(handle, placeable, LockState.Unlocked, "credential",
            "key:ember", "player");

        Assert.True(vetoed.Changing.Payload.Vetoed);
        Assert.Null(vetoed.Changed);
        Assert.Equal(LockState.Locked, vetoed.Lock.State);
        Assert.Equal("LockStateChanging", changed.Changing.Binding);
        Assert.Equal("LockStateChanged", changed.Changed!.Binding);
        Assert.Equal("credential", changed.Changed.Payload.Method);
        Assert.Equal("key:ember", changed.Changed.Payload.Credential);
        Assert.Equal("player", changed.Changed.Payload.ActorId);
    }

    [Fact]
    public void Contents_changed_is_transactional_and_resolution_is_optional_required_and_stale_safe()
    {
        var placeable = Placeable();
        var adapter = new PlaceableSecurityEventsRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;
        var item = placeable.SecurityEvents!.Loot.LootTable.Entries.Single().ItemDefinitionId;
        var changed = adapter.ContentsChanged(handle, placeable, 4,
            [new(item, -1, "take", "inventory:crypt-pillar", "player")], "player-loot", "player");

        Assert.Equal("ContainerContentsChanged", changed.Binding);
        Assert.Equal(4, changed.Payload.PreviousRevision);
        Assert.Equal(5, changed.Payload.CurrentRevision);
        Assert.Equal(-1, changed.Payload.Delta.Single().QuantityDelta);
        Assert.Equal("player-loot", changed.Payload.Cause);
        Assert.True(adapter.Resolve(placeable with
        {
            SecurityEvents = null,
            SecurityEventsRequiredAtRuntime = false,
        }).OptionalFallback);
        Assert.Equal("WORLD_PLACEABLE_SECURITY_EVENTS_REQUIRED_MISSING", adapter.Resolve(placeable with
        {
            SecurityEvents = null,
            SecurityEventsRequiredAtRuntime = true,
        }).DiagnosticCode);
        Assert.Throws<InvalidOperationException>(() => adapter.Build(
            handle with { ProfileId = Guid.NewGuid() }, placeable));
    }

    internal static PlaceableWorldProfile Placeable()
    {
        var placeableId = Guid.Parse("c2940000-0000-4000-8000-000000000001");
        var itemId = Guid.Parse("c2940000-0000-4000-8000-000000000002");
        var foundation = new PlaceableWorldProfile.FoundationPolicy(
            Guid.Parse("c2940000-0000-4000-8000-000000000003"), 1, "test:r094", "crypt-pillar",
            [new("fr-FR", "Pilier", "Ouvrir")], ["container"], "test",
            new("assets/pillar.glb", ["mesh:pillar"], ["material:stone"], "assets/icon.png",
                "assets/portrait.png", []),
            new("box", new DomainVector3(1, 1, 1), true, true, "world"),
            new([new(Guid.Parse("c2940000-0000-4000-8000-000000000004"), "open", "OnUsed", "true")],
                2, 120, true, "placeable.open", 1, "server", ["true"]), null,
            new(true, true, true, true, false, new Dictionary<string, string> { ["onUsed"] = "scripts/open" }),
            new(DomainVector3.Zero, DomainVector3.Zero, DomainVector3.One, DomainVector3.Zero, .25, null,
                true, null), new Dictionary<string, string>(), null,
            new([], ["lock:crypt-pillar"], ["trap:ember"], ["inventory:crypt-pillar"], [], 2, 2), false);
        var operations = new PlaceableOperationsProfile(
            Guid.Parse("c2940000-0000-4000-8000-000000000005"), 1, "test:r094",
            new(Guid.Parse("c2940000-0000-4000-8000-000000000006"), 24, 160, "party",
                ["container.open"], true, "loot:crypt-pillar", "ui:placeable-chest", true,
                "inventory:crypt-pillar"),
            new(Guid.Parse("c2940000-0000-4000-8000-000000000007"), "closed",
                StateProfiles(), [new(Guid.Parse("c2940000-0000-4000-8000-000000000014"), "closed", "open",
                    "open", 1, null)]),
            new(Guid.Parse("c2940000-0000-4000-8000-000000000015"), 100, 100, 10,
                new Dictionary<string, double> { ["force"] = .5 }, [.75, .5, .25], "vfx:shatter",
                "debris:pillar", true, 20),
            new(Guid.Parse("c2940000-0000-4000-8000-000000000016"), ["destroy"], ["plot.override"],
                "Protected", true, "PlaceableProtectionOverride"),
            new(Guid.Parse("c2940000-0000-4000-8000-000000000017"), PlaceableAuthoredLockState.Locked,
                ["key:ember"], "mechanics", 12, "sha256:042", true, true, true, ["lock.admin"]));
        var securityEvents = new PlaceableSecurityEventsProfile(
            Guid.Parse("c2940000-0000-4000-8000-000000000018"), 1, "test:r094",
            new(Guid.Parse("c2940000-0000-4000-8000-000000000019"), "container:open", "damage:ember",
                10, 12, 30, "ash-watch", "vfx:ember", "ash-watch", true, true),
            new(Guid.Parse("c2940000-0000-4000-8000-000000000020"),
                Guid.Parse("c2940000-0000-4000-8000-000000000021"), PlaceableAuthoredLootSpawnTiming.Open,
                42, true, [], 0,
                new(Guid.Parse("c2940000-0000-4000-8000-000000000021"), 1, "test:r094",
                    [new(itemId, "relic", 1, 1, 1, 1, string.Empty, true)])),
            new(Guid.Parse("c2940000-0000-4000-8000-000000000022"), "1.0.0", "ContainerOpened",
                "ContainerClosed", true, true, true, true),
            new(Guid.Parse("c2940000-0000-4000-8000-000000000023"), "1.0.0", "LockStateChanging",
                "LockStateChanged", true, true, true, true),
            new(Guid.Parse("c2940000-0000-4000-8000-000000000024"), "1.0.0",
                "ContainerContentsChanged", true, true, true, true));
        var compositionEvents = new PlaceableCompositionEventsProfile(
            Guid.Parse("c2940000-0000-4000-8000-000000000025"), 1, "test:r095",
            new(Guid.Parse("c2940000-0000-4000-8000-000000000026"), "1.0.0", "DamageApplied",
                "EntityDestroyed", true, true, true, true, true, true),
            [
                new(Guid.Parse("c2940000-0000-4000-8000-000000000027"), "lever", "mechanism",
                    new(new DomainVector3(0, 1.2f, 0), DomainVector3.Zero, DomainVector3.One,
                        DomainVector3.Zero, .1, null, false, null), ["lever", "vfx"], true, 2),
            ],
            [
                new(Guid.Parse("c2940000-0000-4000-8000-000000000028"), "lever",
                    BlueprintId.From(itemId), ["lever"],
                    new(new DomainVector3(9, 9, 9), DomainVector3.Zero, DomainVector3.One,
                        DomainVector3.Zero, .1, null, false, null), "prefab:crypt-lever"),
            ],
            new(Guid.Parse("c2940000-0000-4000-8000-000000000029"),
            [
                Verb(30, "inspect", 5, [], [], "InspectRequested"),
                Verb(31, "use", 5, [], [], "UseRequested"),
                Verb(32, "open", 5, ["unlocked"], [], "OpenRequested"),
                Verb(33, "talk", 5, ["interlocutor"], [], "TalkRequested"),
                Verb(34, "take", 9, ["portable"], [], "TakeRequested"),
                Verb(35, "push", 10, [], ["anchored"], "PushRequested"),
                Verb(36, "climb", 5, ["climbable"], [], "ClimbRequested"),
                Verb(37, "custom-ring", 6, ["ash-rune"], [], "RingRequested"),
            ]));
        return new(placeableId, 1, "test:r094", BlueprintId.From(Guid.NewGuid()), null, null, null,
            new([], new(false, 0, 0, true, "placeable")), null, foundation, true, operations, true,
            securityEvents, true, compositionEvents, true);

        static PlaceableContextualVerbProfile Verb(int id, string verbId, int priority,
            IReadOnlyList<string> required, IReadOnlyList<string> forbidden, string binding)
            => new(Guid.Parse($"c2940000-0000-4000-8000-{id:D12}"), verbId, $"placeable.{verbId}",
                priority, required, forbidden, binding);
    }

    private static IReadOnlyList<PlaceableVisualStateProfile> StateProfiles()
    {
        var states = new[] { "idle", "open", "closed", "broken", "active", "custom" };
        return states.Select((state, index) => new PlaceableVisualStateProfile(
            Guid.Parse($"c2940000-0000-4000-8000-{index + 8:D12}"), state, $"animation:{state}",
            $"visual:{state}", state != "broken")).ToArray();
    }
}
