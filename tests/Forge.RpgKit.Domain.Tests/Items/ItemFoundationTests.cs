using Forge.RpgKit.Application.Items;
using Forge.RpgKit.Domain.Items;

namespace Forge.RpgKit.Domain.Tests.Items;

public sealed class ItemFoundationTests
{
    [Fact]
    public void Wizard_creates_versioned_localized_blueprint_and_separate_instance()
    {
        var type = Type("lantern", Set("hand"), Set("light"), "humanoid"); var types = new Dictionary<Guid, ItemType> { [type.Id] = type };
        var request = new ItemWizardRequest("lanterne-des-eclats", "quest-light", type.Id, "asset:lantern", "CC0-1.0", new string('a', 64), 3, 2, new HashSet<string> { "hand" }, [new(Guid.NewGuid(), "light", 8, "effect:glow", null)], [new("fr", "Lanterne des Éclats", "Une lanterne", "Elle révèle le verre.", "Objet voilé", "Lanterne", "Module original")], 75, "author:test");
        var preview = ItemAuthoringService.Create(new(0, []), request, types, 0, true); Assert.Empty(preview.State.Items);
        var created = ItemAuthoringService.Create(new(0, []), request, types, 0, false); Assert.Single(created.State.Items); Assert.Empty(ItemRules.Validate(created.Item, types));
        var instance = new ItemInstance(Guid.NewGuid(), created.Item.Id, created.Item.SchemaVersion, 100, new Dictionary<string, string> { ["identified"] = "false" });
        Assert.Equal(created.Item.Id, instance.DefinitionId);
    }

    [Fact]
    public void Type_change_requires_preview_then_removes_incompatible_parts()
    {
        var oldType = Type("lantern", Set("hand"), Set("light"), "humanoid"); var target = Type("relic", Set("belt"), Set("ward"), "none");
        var item = new ItemDefinition(Guid.NewGuid(), 1, "author:test", "lantern", oldType.Id, "light", "asset:lantern", "CC0-1.0", new string('b', 64), 1, 1, new HashSet<string> { "hand" }, [new(Guid.NewGuid(), "light", 1, "effect:light", null)], [new("fr", "Lanterne", "Courte", "Longue", "Inconnu", "Lanterne", "")], 10);
        var state = new ItemAuthoringState(4, [item]);
        Assert.Throws<InvalidOperationException>(() => ItemAuthoringService.MigrateType(state, item.Id, target, "humanoid", 4, false, false));
        var migrated = ItemAuthoringService.MigrateType(state, item.Id, target, "humanoid", 4, true, false);
        Assert.Empty(migrated.Item.Properties); Assert.Empty(migrated.Item.Slots); Assert.Equal(target.Id, migrated.Item.TypeId);
    }

    [Fact]
    public void Validator_reports_asset_localization_slots_properties_and_economy()
    {
        var type = Type("relic", Set("belt"), Set("ward"), "none");
        var invalid = new ItemDefinition(Guid.NewGuid(), 1, "author:test", "bad", type.Id, "bad", "", "", "short", -1, -1, new HashSet<string> { "hand" }, [new(Guid.NewGuid(), "light", double.NaN, "", null)], [], -1);
        var codes = ItemRules.Validate(invalid, new Dictionary<Guid, ItemType> { [type.Id] = type }).Select(item => item.Code).ToHashSet();
        Assert.Contains("RPG_ITEM_SLOT_INVALID", codes); Assert.Contains("RPG_ITEM_PROPERTY_INVALID", codes); Assert.Contains("RPG_ITEM_LOCALIZATION_INVALID", codes); Assert.Contains("RPG_ITEM_ASSET_INVALID", codes); Assert.Contains("RPG_ITEM_ECONOMY_INVALID", codes);
    }

    private static ItemType Type(string slug, IReadOnlySet<string> slots, IReadOnlySet<string> properties, string rig) => new(Guid.NewGuid(), 1, "author:test", slug, slots, properties, rig);
    private static IReadOnlySet<string> Set(params string[] values) => values.ToHashSet(StringComparer.Ordinal);
}
