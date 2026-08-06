using Forge.Domain.Items;

namespace Forge.Domain.Tests.Items;

public sealed class ItemAdvancedProfileTests
{
    [Fact]
    public void Validator_accepts_complete_advanced_item_contract()
    {
        Assert.Empty(ItemAdvancedValidator.Validate(CreateProfile(), "items/test"));
    }

    [Fact]
    public void Economy_requirements_identification_and_on_hit_are_runtime_resolved()
    {
        var profile = CreateProfile();
        var context = Context(42);

        var value = ItemAdvancedRuntimeResolver.ResolveEconomicValue(profile, context);
        var requirements = ItemAdvancedRuntimeResolver.ResolveRequirements(profile, context);
        var properties = ItemAdvancedRuntimeResolver.ResolveIdentifiedProperties(profile, 20);
        var proc = ItemAdvancedRuntimeResolver.ResolveOnHit(profile, "hit", new Dictionary<Guid, DateTimeOffset>(),
            DateTimeOffset.UnixEpoch, context);

        Assert.Equal(216m, value);
        Assert.True(requirements.Allowed);
        Assert.Contains("damage", properties);
        Assert.True(proc.Triggered);
        Assert.Equal("burn", proc.EffectId);
    }

    [Fact]
    public void Durability_contextual_loot_and_generation_are_deterministic()
    {
        var profile = CreateProfile();
        var context = Context(77);

        var wear = ItemAdvancedRuntimeResolver.ApplyWear(profile, 5, 2, 1);
        var lootA = ItemAdvancedRuntimeResolver.ResolveLootContext(profile, "chest", "crypt", "ashes", "hard",
            new HashSet<string>(["undead"]), "eclipse", context);
        var lootB = ItemAdvancedRuntimeResolver.ResolveLootContext(profile, "chest", "crypt", "ashes", "hard",
            new HashSet<string>(["undead"]), "eclipse", context);
        var generatedA = ItemAdvancedRuntimeResolver.Generate(profile, context);
        var generatedB = ItemAdvancedRuntimeResolver.Generate(profile, context);

        Assert.Equal(1, wear.Current);
        Assert.False(wear.Broken);
        Assert.Equal(lootA, lootB);
        Assert.NotNull(lootA.RuleId);
        Assert.Equal(generatedA.Seed, generatedB.Seed);
        Assert.Equal(generatedA.BudgetSpent, generatedB.BudgetSpent);
        Assert.Equal(generatedA.Explanation, generatedB.Explanation);
        Assert.Equal(generatedA.Affixes, generatedB.Affixes);
        Assert.InRange(generatedA.Affixes.Count, 1, 2);
        Assert.Contains("seed=84", generatedA.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Container_cycle_and_depth_guards_are_enforced()
    {
        var bag = Guid.Parse("10100000-0000-4000-8000-000000000001");
        var pouch = Guid.Parse("10100000-0000-4000-8000-000000000002");
        var item = Guid.Parse("10100000-0000-4000-8000-000000000003");
        var parents = new Dictionary<Guid, Guid?> { [bag] = null, [pouch] = bag };

        Assert.True(ItemAdvancedRuntimeResolver.CanNest(pouch, item, parents, 3));
        Assert.False(ItemAdvancedRuntimeResolver.CanNest(item, item, parents, 3));
        Assert.False(ItemAdvancedRuntimeResolver.CanNest(pouch, bag,
            new Dictionary<Guid, Guid?> { [pouch] = bag, [bag] = item }, 3));
    }

    [Fact]
    public void Validator_reports_invalid_equipment_and_generator_data()
    {
        var profile = CreateProfile() with
        {
            Weapon = CreateProfile().Weapon! with { AttacksPerSecond = 0 },
            Generator = CreateProfile().Generator! with { MaximumAffixes = 0 }
        };

        var diagnostics = ItemAdvancedValidator.Validate(profile, "items/test");
        Assert.Contains(diagnostics, item => item.Code == "item.advanced.weapon_invalid");
        Assert.Contains(diagnostics, item => item.Code == "item.advanced.generator_invalid");
    }

    [Fact]
    public void Unique_registry_reservation_commit_and_release_are_authoritative()
    {
        var profile = CreateProfile();
        var now = DateTimeOffset.Parse("2026-08-06T12:00:00Z");
        var denied = ItemUniqueRegistryResolver.Reserve(profile, ItemUniqueRegistryState.Empty, "player-1",
            "party-1", "season-1", now, false);
        var reserved = ItemUniqueRegistryResolver.Reserve(profile, ItemUniqueRegistryState.Empty, "player-1",
            "party-1", "season-1", now, true);
        var duplicate = ItemUniqueRegistryResolver.Reserve(profile, reserved.State, "player-2", "party-2",
            "season-1", now, true);
        var committed = ItemUniqueRegistryResolver.Commit(reserved.State, reserved.Reservation!.Token, true);
        var released = ItemUniqueRegistryResolver.Release(profile, committed, reserved.Reservation.Token, true, true);

        Assert.Equal("item.unique.server_authority_required", denied.Code);
        Assert.True(reserved.Accepted);
        Assert.False(duplicate.Accepted);
        Assert.True(Assert.Single(committed.Reservations).Committed);
        Assert.Empty(released.Reservations);
    }

    [Fact]
    public void Loot_crafting_and_ammo_policies_produce_bounded_runtime_decisions()
    {
        var profile = CreateProfile();
        var now = DateTimeOffset.Parse("2026-08-06T12:00:00Z");
        var deniedLoot = ItemLootOwnershipResolver.Resolve(profile, new("player-2", "player-1", "party-1",
            "party-1", now, now.AddSeconds(30), false, null, false, true));
        var publicLoot = ItemLootOwnershipResolver.Resolve(profile, new("player-2", "player-1", "party-2",
            "party-1", now, now.AddSeconds(121), false, null, false, true));
        var recipe = Assert.Single(profile.Recipes);
        var craft = ItemCraftingResolver.Resolve(profile, recipe.Id,
            new("forge", new HashSet<string>(["smithing"]), 2,
                new Dictionary<string, int> { ["tag:ember-metal"] = 2 }, true));
        var shotA = ItemAmmoResolver.Fire(profile, 5, 18,
            Guid.Parse("10400000-0000-4000-8000-000000000001"));
        var shotB = ItemAmmoResolver.Fire(profile, 5, 18,
            Guid.Parse("10400000-0000-4000-8000-000000000001"));

        Assert.False(deniedLoot.Allowed);
        Assert.True(publicLoot.Allowed);
        Assert.True(craft.Allowed);
        Assert.Equal(2, craft.Consumed["tag:ember-metal"]);
        Assert.Equal(4, shotA.Remaining);
        Assert.Equal(shotA, shotB);
    }

    public static ItemAdvancedProfile CreateProfile() => new(
        Guid.Parse("10200000-0000-4000-8000-000000000001"), 1, "test:g13-k-m", true,
        new("gold", 100m, 1.2, 1.5, 0.5, 1, new Dictionary<string, decimal> { ["capital"] = 120m }),
        new(3, 5, ["blades"], ["attuned"], ["keepers"], "quest.ashes.complete"),
        new("unknown", 12, 5m, "gold", true,
            [new("unknown", 0, ["name"]), new("identified", 12, ["damage", "proc"])]),
        new(true, "keeper-01", "embers", true,
            [new(Guid.Parse("10200000-0000-4000-8000-000000000002"), "keeper-01", "embers", "seal-1",
                DateTimeOffset.UnixEpoch, "test:ownership")]),
        [new(Guid.Parse("10200000-0000-4000-8000-000000000003"), "hit", 1, 2, "target.burning",
            "burn", "wielder", true, "test:proc")],
        new("longsword", 1.1, false,
            [new("slash", "physical", 4, 8, 1.8, "attack.slash", ["blade"])], ["martial"]),
        new(["torso", "arms"], 35, 4, 2, "steel", new Dictionary<string, double> { ["fire"] = 0.15 },
            ["scratched", "broken"]),
        new("arrow", "assets/projectiles/arrow.glb", 40, 0.25, "fire", true),
        new(true, 10, 10, 1, 2, "smithing", 3m, "item.broken", "visual.broken"),
        new(16, 80, 4, 0.75, ["loot"], ["inventory.open"], true, true),
        [new(Guid.Parse("10200000-0000-4000-8000-000000000004"), "chest", "crypt", "ashes", 1, 20,
            "hard", ["undead"], "quest.ashes.complete", "eclipse", 3, "test:loot")],
        new(true, "unique:ember-blade", ItemUniqueScope.World, 30, true),
        new(ItemLootOwnershipMode.NeedGreed, 120, true, true),
        [new(Guid.Parse("10200000-0000-4000-8000-000000000005"), "forge-ember-blade",
            [new("tag:ember-metal", 2, true, 1)], "forge", "smithing", 12, 2, 8, ["ash-dust"], "test:recipe")],
        new(true, 6, 1, 2, ["weapon"], new Dictionary<string, int> { ["common"] = 80, ["rare"] = 20 },
            [new(Guid.Parse("10200000-0000-4000-8000-000000000006"), "keen", "weapon", 2, 5, "common",
                    ["heavy"], ["critical"], "test:affix"),
                new(Guid.Parse("10200000-0000-4000-8000-000000000007"), "ember", "weapon", 4, 2, "rare",
                    [], ["fire-damage"], "test:affix")], 7));

    public static ItemRuntimeContext Context(int seed) => new(5, 20, new HashSet<string>(["blades", "smithing"]),
        new HashSet<string>(["attuned"]), new HashSet<string>(["keepers"]),
        new HashSet<string>(["quest.ashes.complete", "target.burning"]), "capital", "gold", 1, 10, seed,
        "player-1", true);
}
