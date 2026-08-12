using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Domain.Placeables;
using DomainVector3 = Forge.Domain.Spatial.Vector3Value;

namespace Forge.RpgKit.Domain.Tests.Placeables;

public sealed class PlaceableOperationsRuntimeAdapterTests
{
    [Fact]
    public void Container_maps_capacity_ownership_access_persistence_loot_ui_replication_and_inventory()
    {
        var placeable = Placeable();
        var adapter = new PlaceableOperationsRuntimeAdapter();
        var runtime = adapter.Build(adapter.Resolve(placeable).Handle!, placeable);
        var container = Assert.IsType<ContainerComponent>(runtime.Profile.Container);

        Assert.Equal(24, container.CapacitySlots);
        Assert.Equal(160, container.CapacityMass);
        Assert.Equal("party", container.OwnershipPolicy);
        Assert.Contains("container.withdraw", container.AccessPermissions);
        Assert.True(container.Persistent && container.Replicated);
        Assert.Equal("loot:crypt-pillar", container.LootPolicyId);
        Assert.Equal("ui:placeable-chest", container.UiProfileId);
        Assert.Equal(["inventory:crypt-pillar"], runtime.Profile.InventoryReferences);
        Assert.Empty(adapter.Validate(adapter.Resolve(placeable).Handle!, placeable));
    }

    [Fact]
    public void Visual_state_machine_previews_all_required_states_and_conditioned_transition()
    {
        var placeable = Placeable();
        var adapter = new PlaceableOperationsRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;
        var runtime = adapter.Build(handle, placeable);
        var transition = adapter.PreviewTransition(handle, placeable, "closed", "open",
            new HashSet<string> { "quest:crypt-open" });

        Assert.Equal(["active", "broken", "closed", "custom", "idle", "open"],
            runtime.Profile.StateMachine.States.Select(item => item.Id).Order(StringComparer.Ordinal));
        Assert.Equal("open", transition.CurrentState);
        Assert.Equal(TimeSpan.FromSeconds(1), transition.PreviewDuration);
    }

    [Fact]
    public void Damage_reuses_resistance_visual_stages_destruction_debris_and_repair()
    {
        var placeable = Placeable();
        var adapter = new PlaceableOperationsRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;
        var damaged = adapter.Damage(handle, placeable, 80, "force");
        var destroyed = PlaceableInteractionService.ApplyDamage(damaged.Damageable, 500, "cold");
        var repaired = adapter.Repair(handle, placeable, destroyed.Damageable, 20);

        Assert.Equal(70, damaged.Damageable.CurrentHealth);
        Assert.Equal("damaged:0.75", damaged.VisualStage);
        Assert.True(destroyed.Destroyed);
        Assert.Equal("debris:crypt-pillar", destroyed.DebrisDefinitionId);
        Assert.Equal(20, repaired.CurrentHealth);
    }

    [Fact]
    public void Protection_blocks_players_and_audits_game_master_override()
    {
        var placeable = Placeable();
        var adapter = new PlaceableOperationsRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;
        var blocked = adapter.Authorize(handle, placeable, "destroy", new HashSet<string>(), false,
            "player", string.Empty);
        var overridden = adapter.Authorize(handle, placeable, "destroy", new HashSet<string>(), true,
            "gm:karen", "restore module");

        Assert.False(blocked.Allowed);
        Assert.Equal("RPG_PLACEABLE_PROTECTED", blocked.Code);
        Assert.True(overridden.Allowed);
        Assert.Contains("restore module", overridden.AuditEntry);
        Assert.Equal("PlaceableProtectionOverride", adapter.Build(handle, placeable).ProtectionAuditBinding);
    }

    [Fact]
    public void Lock_supports_credentials_hacking_relock_jam_and_optional_required_resolution()
    {
        var placeable = Placeable();
        var adapter = new PlaceableOperationsRuntimeAdapter();
        var handle = adapter.Resolve(placeable).Handle!;
        var opened = adapter.Unlock(handle, placeable,
            new("player", new HashSet<string> { "key:ember" }, new HashSet<string>(), null, 0, false, false));
        var jammed = adapter.Relock(handle, placeable, opened.Lock, true, "player");
        var hacked = adapter.Unlock(handle, placeable,
            new("player", new HashSet<string>(), new HashSet<string>(), null, 13, true, false));

        Assert.True(opened.Succeeded);
        Assert.Equal("credential", opened.Method);
        Assert.Equal(LockState.Jammed, jammed.Lock.State);
        Assert.True(hacked.Succeeded);
        Assert.Equal("hacking", hacked.Method);
        Assert.True(adapter.Resolve(placeable with { Operations = null, OperationsRequiredAtRuntime = false })
            .OptionalFallback);
        Assert.Equal("WORLD_PLACEABLE_OPERATIONS_REQUIRED_MISSING",
            adapter.Resolve(placeable with { Operations = null, OperationsRequiredAtRuntime = true })
                .DiagnosticCode);
        Assert.Throws<InvalidOperationException>(() => adapter.Build(
            handle with { OperationsId = Guid.NewGuid() }, placeable));
    }

    private static PlaceableWorldProfile Placeable()
    {
        var placeableId = Guid.Parse("c2930000-0000-4000-8000-000000000001");
        var foundation = new PlaceableWorldProfile.FoundationPolicy(
            Guid.Parse("c2930000-0000-4000-8000-000000000002"), 1, "test:r093", "crypt-pillar",
            [new("fr-FR", "Pilier", "Ouvrir")], ["container"], "test",
            new("assets/pillar.glb", ["mesh:pillar"], ["material:stone"], "assets/icon.png",
                "assets/portrait.png", []),
            new("box", new DomainVector3(1, 1, 1), true, true, "world"),
            new([new(Guid.Parse("c2930000-0000-4000-8000-000000000003"), "open", "OnUsed", "true")],
                2, 120, true, "placeable.open", 1, "server", ["true"]), null,
            new(true, true, true, false, false, new Dictionary<string, string> { ["onUsed"] = "scripts/open" }),
            new(DomainVector3.Zero, DomainVector3.Zero, DomainVector3.One, DomainVector3.Zero, .25, null, true, null),
            new Dictionary<string, string>(), null,
            new([], ["lock:crypt-pillar"], [], ["inventory:crypt-pillar"], [], 2, 2), false);
        var operations = new PlaceableOperationsProfile(
            Guid.Parse("c2930000-0000-4000-8000-000000000004"), 1, "test:r093",
            new(Guid.Parse("c2930000-0000-4000-8000-000000000005"), 24, 160, "party",
                ["container.open", "container.deposit", "container.withdraw"], true,
                "loot:crypt-pillar", "ui:placeable-chest", true, "inventory:crypt-pillar"),
            new(Guid.Parse("c2930000-0000-4000-8000-000000000006"), "closed",
                StateProfiles(),
                [new(Guid.Parse("c2930000-0000-4000-8000-000000000020"), "closed", "open", "open", 1,
                    "quest:crypt-open")]),
            new(Guid.Parse("c2930000-0000-4000-8000-000000000021"), 100, 100, 10,
                new Dictionary<string, double> { ["force"] = .5 }, [.75, .5, .25], "vfx:shatter",
                "debris:crypt-pillar", true, 20),
            new(Guid.Parse("c2930000-0000-4000-8000-000000000022"), ["destroy", "move"],
                ["placeable.plot.override"], "Protected", true, "PlaceableProtectionOverride"),
            new(Guid.Parse("c2930000-0000-4000-8000-000000000023"), PlaceableAuthoredLockState.Locked,
                ["key:ember"], "mechanics", 12, "sha256:042", true, true, true, ["lock.admin"]));
        return new(placeableId, 1, "test:r093", BlueprintId.From(Guid.NewGuid()), null, null, null,
            new([], new(false, 0, 0, true, "placeable")), null, foundation, true, operations, true);
    }

    private static IReadOnlyList<PlaceableVisualStateProfile> StateProfiles()
    {
        var states = new[] { "idle", "open", "closed", "broken", "active", "custom" };
        return states.Select((state, index) => new PlaceableVisualStateProfile(
            Guid.Parse($"c2930000-0000-4000-8000-{index + 10:D12}"), state, $"animation:{state}",
            $"visual:{state}", state != "broken")).ToArray();
    }
}
