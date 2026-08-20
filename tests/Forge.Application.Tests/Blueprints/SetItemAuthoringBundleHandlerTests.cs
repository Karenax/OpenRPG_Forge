using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Items;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Blueprints;

public sealed class SetItemAuthoringBundleHandlerTests
{
    private readonly SetItemAuthoringBundleHandler _handler = new(new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public void Dry_run_apply_undo_and_redo_keep_the_complete_item_atomic()
    {
        var state = State();
        var id = BlueprintId.From(Guid.Parse("d4010000-0000-4000-8000-000000000010"));
        var foundation = Foundation(id, "ember-compass");
        var advanced = Advanced();
        var command = new SetItemAuthoringBundleCommand(CommandId.From(Guid.NewGuid()), state.Revision, id,
            "ember-compass", "Boussole de braise", foundation, advanced, false, true);

        var preview = _handler.Handle(state, command);
        var applied = _handler.Handle(state, command with { DryRun = false });
        var history = new ProjectCommandHistory(state);
        history.Record(applied);

        Assert.True(preview.Succeeded);
        Assert.Same(state, preview.State);
        Assert.True(applied.Succeeded);
        Assert.Equal(state.Revision.Next(), applied.NewRevision);
        var item = history.State.Project.Blueprints.Single(candidate => candidate.Id == id);
        Assert.Equal(foundation, item.ItemFoundation);
        Assert.Equal(advanced, item.ItemAdvanced);
        Assert.Equal("set_item_authoring_bundle", applied.Audit.CommandName);
        Assert.DoesNotContain(history.Undo().State.Project.Blueprints, candidate => candidate.Id == id);
        Assert.Equal(foundation.Id, history.Redo().State.Project.Blueprints.Single(candidate => candidate.Id == id)
            .ItemFoundation!.Id);
    }

    [Fact]
    public void Stale_invalid_duplicate_and_unapproved_lossy_type_changes_are_rejected()
    {
        var state = State();
        var existing = state.Project.Blueprints.Single();
        var foundation = Foundation(existing.Id, existing.Slug);
        var advanced = Advanced();
        var initial = _handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision, existing.Id,
            existing.Slug, existing.DisplayName, foundation, advanced, false, false));
        Assert.True(initial.Succeeded);

        var target = new ItemTypeDefinition(Guid.NewGuid(), 1, "test:ux4-s", "relic", ["belt"], ["ward"],
            "relic-rig");
        var changed = foundation with { TypeId = target.Id, Type = target, AppearanceRig = target.RequiredRig };
        var stale = _handler.Handle(initial.State, new(CommandId.From(Guid.NewGuid()), state.Revision, existing.Id,
            existing.Slug, existing.DisplayName, foundation, advanced, false, false));
        var lossy = _handler.Handle(initial.State, new(CommandId.From(Guid.NewGuid()), initial.State.Revision,
            existing.Id, existing.Slug, existing.DisplayName, changed, advanced, false, false));
        var invalid = _handler.Handle(initial.State, new(CommandId.From(Guid.NewGuid()), initial.State.Revision,
            existing.Id, existing.Slug, existing.DisplayName, foundation with { ContentSha256 = "invalid" },
            advanced, false, false));

        Assert.Equal("command.revision_conflict", Assert.Single(stale.Diagnostics).Code);
        Assert.Equal("item.foundation.type_migration_preview_required", Assert.Single(lossy.Diagnostics).Code);
        Assert.Contains(invalid.Diagnostics, item => item.Code == "item.foundation.content_governance_invalid");
        Assert.Equal(initial.State.Revision, invalid.State.Revision);
    }

    private static ProjectState State()
    {
        var item = new BlueprintDefinition(BlueprintId.From(Guid.Parse("d4010000-0000-4000-8000-000000000001")),
            "ash-key", "Clé de braise", BlueprintKind.Item);
        var area = new AreaDefinition(AreaId.From(Guid.Parse("d4010000-0000-4000-8000-000000000002")),
            "item-lab", "Atelier d’objets", []);
        return new(new ProjectDefinition(ProjectId.From(Guid.Parse("d4010000-0000-4000-8000-000000000003")),
            "Item authoring", [item], [area]), new ProjectRevision(3));
    }

    private static ItemFoundationProfile Foundation(BlueprintId blueprintId, string slug)
    {
        var typeId = Guid.Parse("d4010000-0000-4000-8000-000000000020");
        var result = new ItemFoundationProfile(Guid.Parse("d4010000-0000-4000-8000-000000000021"), 1,
            "test:ux4-s", true, "project-content", "CC0-1.0", new string('a', 64), typeId,
            new(typeId, 1, "test:ux4-s", "relic", ["hand", "inventory"], ["utility"], "hand-rig"),
            slug, "asset.item.ember-compass", "hand-rig", 1, 2, 3, 125, ["hand"],
            [new(Guid.Parse("d4010000-0000-4000-8000-000000000022"), "utility", 2,
                "effect.compass", "OnUse")],
            [new("fr", "Boussole de braise", "Une boussole.", "Une boussole rituelle complète.",
                "Objet inconnu", "Boussole", "Preuve UX4-S")], ["identified", "durability", "owner"]);
        var creation = ItemCreationProfile.CreateDefault(
            Guid.Parse("d4010000-0000-4000-8000-000000000023"),
            Guid.Parse("d4010000-0000-4000-8000-000000000024"), "test:ux4-s") with
        {
            RequiredAtRuntime = true,
            ContentSha256 = new string('b', 64),
            Wizard = new(["archetype", "appearance", "slots", "quality", "level", "property", "provenance",
                "generation-rules"], ["fixed-seed", "preserve-provenance"]),
            Inventory = new(1.25, .4, 1, 2, .5, .25),
            Stack = new(10, "ember-compass", true),
            Usage = new(3, 3, 2, 2, 5_000, 100, false, "rest-or-refill")
        };
        var gameplay = new ItemGameplayProfile(Guid.Parse("d4010000-0000-4000-8000-000000000025"), 1,
            "test:ux4-s", true, "project-content", "CC0-1.0", new string('c', 64),
            new(true, true, true, true, true, true, true),
            new(Enum.GetValues<ItemTransferDestination>().ToDictionary(item => item, item =>
                item is ItemTransferDestination.Container or ItemTransferDestination.GameMaster),
                ["item.transfer.quest"]),
            [new(Guid.Parse("d4010000-0000-4000-8000-000000000026"), 1, "test:ux4-s", "ward",
                new Dictionary<string, double> { ["value"] = 8 }, "highest", "night", "+8 garde",
                "BeforeItemModifier", "AfterItemModifier")],
            new(Guid.Parse("d4010000-0000-4000-8000-000000000027"), "point",
                new Dictionary<string, double> { ["focus"] = 1 }, 1, 12_000,
                "ItemAbilityRequested", "ItemAbilityResolved"),
            new([new("hand", "forge", "hand-rig", 1, [])], ["hand"]));
        var operations = new ItemOperationsProfile(Guid.Parse("d4010000-0000-4000-8000-000000000028"), 1,
            "test:ux4-s", true, "project-content", "CC0-1.0", new string('d', 64),
            new(Guid.Parse("d4010000-0000-4000-8000-000000000029"), 1, "test:ux4-s",
                [new(blueprintId.Value, "relic", 1, 1, 1, 2, "quest-active", true)]),
            new(Guid.Parse("d4010000-0000-4000-8000-000000000030"), 1, "test:ux4-s", 0,
                [new(Guid.Parse("d4010000-0000-4000-8000-000000000031"), "backpack", null, 0, 0, 1,
                    ["quest"], "asset.item.ember-compass.preview")], true, true, true),
            new("1.0", Enum.GetValues<ItemLifecycleEventType>().Select((kind, index) =>
                new ItemLifecycleBindingPolicy(Stable(blueprintId.Value, $"event-{index}"), 1, "test:ux4-s", kind,
                    $"On{kind}", "server")).ToArray()));
        return result with { Creation = creation, Gameplay = gameplay, Operations = operations };
    }

    private static ItemAdvancedProfile Advanced() => ItemAdvancedProfile.CreateDefault(
        Guid.Parse("d4010000-0000-4000-8000-000000000040"), "test:ux4-s") with
    {
        Economy = new("gold", 125, 1.2, 1.4, .8, 1, new Dictionary<string, decimal> { ["crypt"] = 150 }),
        Requirements = new(3, 2, ["lore"], ["ember-attuned"], ["surface-alliance"], "quest.active"),
        OnHitProcs = [new(Guid.Parse("d4010000-0000-4000-8000-000000000041"), "hit", .25, 2,
            "target.marked", "effect.ember", "wielder", true, "test:ux4-s")],
        Weapon = new("ritual", 1, false,
            [new("primary", "fire", 2, 6, 1.5, "attack.primary", ["ritual"])], ["ritual"]),
        Armor = new(["hand"], 5, .2, 0, "ember-steel", new Dictionary<string, double> { ["fire"] = .25 },
            ["cracked"]),
        Ammo = new("ember", "asset.projectile.ember", 12, .5, "fire", true),
        Container = new(4, 10, 2, .75, ["gem"], ["inventory.open"], true, true),
        LootContexts = [new(Guid.Parse("d4010000-0000-4000-8000-000000000042"), "chest", "crypt",
            "ash-watch", 1, 20, "normal", ["undead"], "quest.active", "eclipse", 5, "test:ux4-s")],
        Unique = new(true, "unique:ember-compass", ItemUniqueScope.World, 60, true),
        LootOwnership = new(ItemLootOwnershipMode.Personal, 120, true, true),
        Recipes = [new(Guid.Parse("d4010000-0000-4000-8000-000000000043"), "repair-ember-compass",
            [new("tag:ember-metal", 2, true, 1)], "forge", "smithing", 10, 1, 6, [], "test:ux4-s")],
        Generator = new(true, 6, 1, 2, ["relic"], new Dictionary<string, int>
        {
            ["common"] = 80,
            ["rare"] = 20
        }, [new(Guid.Parse("d4010000-0000-4000-8000-000000000044"), "keen", "relic", 2,
            5, "common", [], ["critical"], "test:ux4-s")], 17)
    };

    private static Guid Stable(Guid scope, string key)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{scope:D}|{key}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
