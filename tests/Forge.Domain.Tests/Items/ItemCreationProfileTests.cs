using Forge.Domain.Items;

namespace Forge.Domain.Tests.Items;

public sealed class ItemCreationProfileTests
{
    [Fact]
    public void CanonicalCreationProfileValidatesAllFiveCapabilities()
    {
        var profile = Complete();

        var diagnostics = ItemFoundationValidator.Validate(profile, "blueprints/item/itemFoundation");
        var runtime = ItemFoundationValidator.ResolveCreation(profile);

        Assert.Empty(diagnostics);
        Assert.True(runtime.Succeeded);
        Assert.Equal("ash-key", runtime.Profile!.Stack.CompatibilityKey);
        Assert.Equal(8, runtime.Profile.Wizard.Sections.Count);
    }

    [Fact]
    public void MissingOptionalCreationFallsBackAndRequiredCreationReportsStableDiagnostic()
    {
        var foundation = Complete() with { Creation = null };

        var optional = ItemFoundationValidator.ResolveCreation(foundation);
        var required = ItemFoundationValidator.ResolveCreation(foundation, required: true);

        Assert.True(optional.UsedFallback);
        Assert.Equal(ItemFoundationValidator.CreationMissingRequiredCode,
            Assert.Single(required.Diagnostics).Code);
        Assert.Equal("itemFoundation/creation", required.Diagnostics[0].Path);
    }

    [Fact]
    public void InvalidCreationFieldsProduceStableLocalizedDiagnostics()
    {
        var valid = Complete();
        var creation = valid.Creation!;
        var invalid = valid with
        {
            Creation = creation with
            {
                Id = Guid.Empty,
                SchemaVersion = 2,
                Provenance = string.Empty,
                LicenseSpdx = string.Empty,
                ContentSha256 = "bad",
                Wizard = new([], []),
                Appearance = creation.Appearance with
                {
                    Id = valid.Id,
                    SchemaVersion = 2,
                    Provenance = string.Empty,
                    MeshAssetIds = [],
                    IconAssetId = string.Empty,
                    EquippedPreviewAssetId = string.Empty,
                },
                Inventory = creation.Inventory with { Mass = -1, GridWidth = 0 },
                Stack = creation.Stack with { Maximum = 0, CompatibilityKey = "Bad Stack" },
                Usage = creation.Usage with { CurrentCharges = 99, CooldownMilliseconds = -1 },
            },
        };

        var diagnostics = ItemFoundationValidator.Validate(invalid, "blueprints/item/itemFoundation");
        var codes = diagnostics.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("item.creation.uuid_invalid", codes);
        Assert.Contains("item.creation.schema_unsupported", codes);
        Assert.Contains("item.creation.provenance_invalid", codes);
        Assert.Contains("item.creation.content_governance_invalid", codes);
        Assert.Contains("item.creation.wizard_invalid", codes);
        Assert.Contains("item.creation.appearance_invalid", codes);
        Assert.Contains("item.creation.inventory_policy_invalid", codes);
        Assert.Contains("item.creation.stack_policy_invalid", codes);
        Assert.Contains("item.creation.usage_policy_invalid", codes);
        Assert.All(diagnostics, item => Assert.StartsWith("blueprints/item/itemFoundation", item.Path));
    }

    internal static ItemFoundationProfile Complete()
    {
        var foundation = ItemFoundationProfileTests.Complete();
        return foundation with
        {
            Creation = new(
                Guid.Parse("c2880000-0000-4000-8000-000000000001"), 1, "test:r088", true,
                "crypt-of-ashes", "CC0-1.0", new string('8', 64),
                new(["archetype", "appearance", "slots", "quality", "level", "property", "provenance",
                    "generation-rules"], ["fixed-seed", "respect-type", "preserve-provenance"]),
                new(Guid.Parse("c2880000-0000-4000-8000-000000000002"), 1, "test:r088",
                    ["asset.items.ash-key.mesh"], ["asset.materials.embered-iron"], ["hand-r"],
                    "asset.items.ash-key.icon", ["asset.vfx.ash-glow"], "asset.items.ash-key.equipped"),
                new(1.25, .4, 1, 2, .5, .25),
                new(10, "ash-key", true),
                new(3, 3, 2, 2, 5_000, 100, false, "rest-or-refill")),
        };
    }
}
