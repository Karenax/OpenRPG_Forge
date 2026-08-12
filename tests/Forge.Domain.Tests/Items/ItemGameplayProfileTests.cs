using Forge.Domain.Items;

namespace Forge.Domain.Tests.Items;

public sealed class ItemGameplayProfileTests
{
    [Fact]
    public void CanonicalGameplayProfileValidatesFiveCapabilities()
    {
        var foundation = Complete();

        var diagnostics = ItemFoundationValidator.Validate(foundation, "blueprints/item/itemFoundation");
        var runtime = ItemFoundationValidator.ResolveGameplay(foundation);

        Assert.Empty(diagnostics);
        Assert.True(runtime.Succeeded);
        Assert.Equal(8, runtime.Profile!.Transfer.AllowedDestinations.Count);
        Assert.Equal(2, runtime.Profile.Equipment.RequiredSlots.Count);
    }

    [Fact]
    public void OptionalGameplayFallsBackAndRequiredGameplayReportsStableDiagnostic()
    {
        var foundation = Complete() with { Gameplay = null };

        Assert.True(ItemFoundationValidator.ResolveGameplay(foundation).UsedFallback);
        Assert.Equal(ItemFoundationValidator.GameplayMissingRequiredCode,
            Assert.Single(ItemFoundationValidator.ResolveGameplay(foundation, true).Diagnostics).Code);
    }

    [Fact]
    public void InvalidGameplayProducesLocalizedStableDiagnostics()
    {
        var valid = Complete();
        var gameplay = valid.Gameplay!;
        var invalid = valid with
        {
            Gameplay = gameplay with
            {
                Id = Guid.Empty,
                SchemaVersion = 2,
                Provenance = string.Empty,
                LicenseSpdx = string.Empty,
                ContentSha256 = "bad",
                Transfer = new(new Dictionary<ItemTransferDestination, bool>(), ["Bad Permission"]),
                Modifiers = [gameplay.Modifiers[0] with
                {
                    Id = Guid.Empty,
                    StackingRule = "replace",
                    Parameters = new Dictionary<string, double> { ["value"] = double.NaN },
                }],
                Ability = gameplay.Ability! with { AbilityDefinitionId = Guid.Empty, ChargeCost = 0 },
                Equipment = new([gameplay.Equipment.SlotDefinitions[0] with { Id = "Bad Slot" }], ["missing"]),
            },
        };

        var diagnostics = ItemFoundationValidator.Validate(invalid, "blueprints/item/itemFoundation");
        var codes = diagnostics.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("item.gameplay.uuid_invalid", codes);
        Assert.Contains("item.gameplay.schema_unsupported", codes);
        Assert.Contains("item.gameplay.provenance_invalid", codes);
        Assert.Contains("item.gameplay.content_governance_invalid", codes);
        Assert.Contains("item.gameplay.transfer_policy_invalid", codes);
        Assert.Contains("item.gameplay.modifier_invalid", codes);
        Assert.Contains("item.gameplay.ability_invalid", codes);
        Assert.Contains("item.gameplay.equipment_slot_invalid", codes);
        Assert.Contains("item.gameplay.equipment_policy_invalid", codes);
        Assert.All(diagnostics, item => Assert.StartsWith("blueprints/item/itemFoundation", item.Path));
    }

    internal static ItemFoundationProfile Complete()
    {
        var foundation = ItemCreationProfileTests.Complete();
        return foundation with
        {
            Gameplay = new(Guid.Parse("c2890000-0000-4000-8000-000000000001"), 1, "test:r089", true,
                "crypt-of-ashes", "CC0-1.0", new string('9', 64),
                new(true, true, true, true, true, true, true),
                new(Enum.GetValues<ItemTransferDestination>().ToDictionary(item => item,
                    item => item is ItemTransferDestination.Container or ItemTransferDestination.GameMaster),
                    ["item.transfer.quest"]),
                [new(Guid.Parse("c2890000-0000-4000-8000-000000000002"), 1, "test:r089", "ward",
                    new Dictionary<string, double> { ["value"] = 8 }, "highest", "night",
                    "+8 ward at night", "BeforeItemModifier", "AfterItemModifier")],
                new(Guid.Parse("c2890000-0000-4000-8000-000000000003"), "point",
                    new Dictionary<string, double> { ["focus"] = 1 }, 1, 12_000,
                    "ItemAbilityRequested", "ItemAbilityResolved"),
                new([
                    new("hand-r", "forge", "humanoid", 2, ["hands-two"]),
                    new("hand-l", "forge", "humanoid", 2, ["hands-two"]),
                    new("hands-two", "forge", "humanoid", 3, ["hand-r", "hand-l"]),
                ], ["hand-r", "hand-l"])),
        };
    }
}
