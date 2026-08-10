using Forge.Domain.Items;

namespace Forge.Domain.Tests.Items;

public sealed class ItemFoundationProfileTests
{
    [Fact]
    public void CanonicalFoundationValidatesAndSeparatesMutableInstanceState()
    {
        var profile = Complete();
        var instance = new ItemFoundationInstance(Guid.NewGuid(), profile.Id, profile.DefinitionVersion, 72,
            new Dictionary<string, string> { ["identified"] = "true" });

        var runtime = ItemFoundationValidator.Resolve(profile, true);
        var diagnostics = ItemFoundationValidator.ValidateInstance(profile, instance);

        Assert.True(runtime.Succeeded);
        Assert.Empty(ItemFoundationValidator.Validate(profile, "blueprints/item/itemFoundation"));
        Assert.Empty(diagnostics);
        Assert.Equal("Clé des cendres", profile.Texts.Single().Name);
        Assert.Equal("Objet inconnu", profile.Texts.Single().UnknownText);
    }

    [Fact]
    public void TypeMigrationRequiresPreviewAndRemovesOnlyIncompatibleData()
    {
        var profile = Complete();
        var target = new ItemTypeDefinition(Guid.NewGuid(), 1, "test:r044", "quest-relic", ["belt"],
            ["ward"], "rig.relic");

        var preview = ItemFoundationValidator.PreviewTypeMigration(profile, target);
        Assert.True(preview.RequiresAcceptance);
        Assert.Throws<InvalidOperationException>(() =>
            ItemFoundationValidator.MigrateType(profile, target, false));

        var migrated = ItemFoundationValidator.MigrateType(profile, target, true);
        Assert.Equal(target.Id, migrated.TypeId);
        Assert.Empty(migrated.Slots);
        Assert.Empty(migrated.Properties);
        Assert.Equal("rig.relic", migrated.AppearanceRig);
        Assert.Equal(profile.DefinitionVersion + 1, migrated.DefinitionVersion);
    }

    [Fact]
    public void ValidatorReportsIdentityTypeRulesLocalizationAssetsBindingsAndInstanceOverrides()
    {
        var invalidType = new ItemTypeDefinition(Guid.NewGuid(), 2, string.Empty, "Bad Type", [], ["Bad Kind"], "");
        var invalid = Complete() with
        {
            Id = Guid.Empty,
            SchemaVersion = 2,
            Provenance = string.Empty,
            PackageManifestId = string.Empty,
            LicenseSpdx = string.Empty,
            ContentSha256 = "bad",
            TypeId = Guid.NewGuid(),
            Type = invalidType,
            Archetype = "Bad Slug",
            AppearanceAssetId = string.Empty,
            AppearanceRig = string.Empty,
            DefinitionVersion = 0,
            Quality = 6,
            Level = -1,
            BaseValue = -1,
            Slots = ["head"],
            Properties = [new(Guid.Empty, "forbidden", double.NaN, string.Empty, "Bad Script")],
            Texts = [new("bad_locale", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, "")],
            AllowedInstanceOverrides = ["Bad Override"],
        };
        var codes = ItemFoundationValidator.Validate(invalid, "blueprints/item/itemFoundation")
            .Select(item => item.Code).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("item.foundation.uuid_required", codes);
        Assert.Contains("item.foundation.schema_unsupported", codes);
        Assert.Contains("item.foundation.provenance_invalid", codes);
        Assert.Contains("item.foundation.content_governance_invalid", codes);
        Assert.Contains("item.foundation.type_invalid", codes);
        Assert.Contains("item.foundation.type_rules_invalid", codes);
        Assert.Contains("item.foundation.blueprint_invalid", codes);
        Assert.Contains("item.foundation.values_invalid", codes);
        Assert.Contains("item.foundation.slot_invalid", codes);
        Assert.Contains("item.foundation.property_invalid", codes);
        Assert.Contains("item.foundation.localization_invalid", codes);
        Assert.Contains("item.foundation.override_allowlist_invalid", codes);
        Assert.All(ItemFoundationValidator.Validate(invalid, "blueprints/item/itemFoundation"),
            item => Assert.StartsWith("blueprints/item/itemFoundation", item.Path));

        var valid = Complete();
        var instance = new ItemFoundationInstance(Guid.NewGuid(), valid.Id, valid.DefinitionVersion, 1,
            new Dictionary<string, string> { ["forbidden"] = "true" });
        Assert.Equal("item.foundation.instance_override_forbidden",
            Assert.Single(ItemFoundationValidator.ValidateInstance(valid, instance)).Code);
        Assert.True(ItemFoundationValidator.Resolve(null, false).UsedFallback);
        Assert.Equal(ItemFoundationValidator.MissingRequiredCode,
            Assert.Single(ItemFoundationValidator.Resolve(null, true).Diagnostics).Code);
    }

    internal static ItemFoundationProfile Complete()
    {
        var typeId = Guid.Parse("c2440000-0000-4000-8000-000000000002");
        return new(Guid.Parse("c2440000-0000-4000-8000-000000000001"), 1, "test:r044", true,
            "crypt-of-ashes", "CC0-1.0", new string('a', 64), typeId,
            new(typeId, 1, "test:r044", "ritual-key", ["hand", "inventory"], ["utility"], "rig.hand"),
            "ash-key", "asset.items.ash-key", "rig.hand", 3, 2, 4, 75,
            ["hand"], [new(Guid.Parse("c2440000-0000-4000-8000-000000000003"), "utility", 1,
                "effect.ash-key", "OnUse")],
            [new("fr", "Clé des cendres", "Une clé rituelle.", "Une clé forgée pour la crypte.",
                "Objet inconnu", "Clé", "Récompense principale")],
            ["identified", "durability", "owner"]);
    }
}
