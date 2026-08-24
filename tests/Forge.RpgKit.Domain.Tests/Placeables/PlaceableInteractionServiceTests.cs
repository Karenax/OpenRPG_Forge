using Forge.RpgKit.Domain.Placeables;

namespace Forge.RpgKit.Domain.Tests.Placeables;

public sealed class PlaceableInteractionServiceTests
{
    [Fact]
    public void Executes_typed_interaction_and_previews_visual_transition()
    {
        var definition = Definition(); var profile = Profile();
        var executed = PlaceableInteractionService.Execute(definition, "open", "player:ilyne", new(1, 2, 3), "success", "runtime:test", DateTimeOffset.UnixEpoch);
        Assert.Equal("open", executed.Verb); Assert.Equal(new Vector3Value(1, 2, 3), executed.TargetPoint); Assert.Equal("success", executed.Result);
        var transition = PlaceableInteractionService.Transition(profile.StateMachine, "closed", "open", new HashSet<string> { "lighthouse-powered" });
        Assert.Equal("open", transition.CurrentState); Assert.Equal(TimeSpan.FromSeconds(1), transition.PreviewDuration);
    }

    [Fact]
    public void Validates_container_state_dependencies_physics_and_budgets()
    {
        var definition = Definition(); Assert.Empty(PlaceableInteractionService.Validate(definition, Profile()));
        var invalid = Profile() with { Container = Profile().Container! with { CapacitySlots = 0 }, LockReferences = [], TrapReferences = [], InventoryReferences = [], PhysicsProfiles = [], DrawCallBudget = 0, ScriptBudget = 0 };
        var codes = PlaceableInteractionService.Validate(definition, invalid).Select(item => item.Code).ToHashSet();
        Assert.Contains("RPG_PLACEABLE_CONTAINER_INVALID", codes); Assert.Contains("RPG_PLACEABLE_LOCK_REFERENCE_MISSING", codes); Assert.Contains("RPG_PLACEABLE_TRAP_REFERENCE_MISSING", codes); Assert.Contains("RPG_PLACEABLE_INVENTORY_REFERENCE_MISSING", codes); Assert.Contains("RPG_PLACEABLE_PHYSICS_PROFILE_MISSING", codes); Assert.Contains("RPG_PLACEABLE_BUDGET_EXCEEDED", codes);
    }

    [Fact]
    public void Applies_resisted_damage_visual_stages_destruction_debris_and_repair()
    {
        var damageable = Profile().Damageable!;
        var damaged = PlaceableInteractionService.ApplyDamage(damageable, 80, "glass"); Assert.Equal(70, damaged.Damageable.CurrentHealth); Assert.Equal("damaged:0.75", damaged.VisualStage);
        var destroyed = PlaceableInteractionService.ApplyDamage(damaged.Damageable, 500, "force"); Assert.True(destroyed.Destroyed); Assert.Equal("debris:glass", destroyed.DebrisDefinitionId);
        var repaired = PlaceableInteractionService.Repair(destroyed.Damageable, 25); Assert.Equal(25, repaired.CurrentHealth);
    }

    private static PlaceableRuntimeProfile Profile() => new(new(12, 80, "party", new HashSet<string> { "container.open" }, true, "loot:lighthouse", "ui:chest", true), new("closed", [new("closed", "idle-closed", "visual:closed", true), new("open", "idle-open", "visual:open", false), new("broken", "idle-broken", "visual:broken", false)], [new("closed", "open", "open", TimeSpan.FromSeconds(1), "lighthouse-powered"), new("open", "closed", "close", TimeSpan.FromSeconds(1), "")]), new(100, 100, 10, new Dictionary<string, double> { ["glass"] = 0.5 }, [0.75, 0.5, 0.25], "vfx:shatter", "debris:glass", true, 20), ["lock:lighthouse"], ["trap:glass"], ["inventory:lighthouse"], ["physics:static-breakable"], 4, 2);

    private static PlaceableDefinition Definition() => new(Guid.NewGuid(), 1, "author:test", "lighthouse-chest", [new("fr", "Coffre du phare", "Ouvrir")], new HashSet<string> { "container" }, "Module de démonstration", new(["asset:chest"], ["asset:glass"], "asset:icon", "asset:portrait", []), new("box", new(1, 1, 1), true, true, "world"), new([new("open", "Ouvrir", "InteractionExecuted", "lighthouse-powered")], 2, 120, true, TimeSpan.FromSeconds(1), "server", "Ouvrir", new HashSet<string> { "lighthouse-powered" }), null, new(true, true, true, true, true, ["on_open", "on_destroyed"]), new(new(0, 0, 0), new(0, 0, 0), new(1, 1, 1), new(0, 0, 0), 0, null, true, null));
}
