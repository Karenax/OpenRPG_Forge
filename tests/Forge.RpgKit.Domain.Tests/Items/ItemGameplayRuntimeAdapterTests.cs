using Forge.Domain.Items;
using Forge.RpgKit.Domain.Items;

namespace Forge.RpgKit.Domain.Tests.Items;

public sealed class ItemGameplayRuntimeAdapterTests
{
    [Fact]
    public void CanonicalGameplayAdaptsToCriticalTransferModifierAbilityAndEquipmentServices()
    {
        var foundation = Foundation();
        var profile = foundation.Gameplay!;
        var resolved = ItemGameplayRuntimeAdapter.Resolve(foundation);

        var blocked = ItemGameplayRuntimeAdapter.AuthorizeCriticalAction(profile, "drop", false, "player", "");
        var overridden = ItemGameplayRuntimeAdapter.AuthorizeCriticalAction(profile, "drop", true, "gm:karen",
            "recover quest item");
        var transfer = ItemGameplayService.AuthorizeTransfer(resolved.Transfer!, TransferDestination.Container,
            new HashSet<string> { "item.transfer.quest" }, "player");
        var modifiers = ItemGameplayService.ResolveModifiers(resolved.Modifiers, new HashSet<string> { "night" });
        var usage = ItemFoundationRuntimeAdapter.Resolve(foundation).Policies!.Usage;
        var ability = ItemGameplayService.ActivateAbility(Guid.NewGuid(), resolved.Ability!, "crypt-door", usage,
            DateTimeOffset.UnixEpoch);
        var equipment = ItemGameplayService.PreviewEquipment(
            new(Guid.NewGuid(), resolved.RequiredSlots, "forge", "humanoid"), resolved.Slots,
            new HashSet<string> { "hands-two" });

        Assert.Empty(resolved.Diagnostics);
        Assert.False(blocked.Allowed);
        Assert.True(overridden.Allowed);
        Assert.Contains("recover quest item", overridden.AuditEntry);
        Assert.True(transfer.Allowed);
        Assert.Equal(8, modifiers["ward"]);
        Assert.Equal("ItemAbilityRequested", ability.Requested.Name);
        Assert.Equal(2, ability.Usage.CurrentCharges);
        Assert.False(equipment.Allowed);
        Assert.Equal(2, equipment.Conflicts.Count);
    }

    [Fact]
    public void AdapterPreservesOptionalRequiredAndGameMasterOverridePolicy()
    {
        var optional = ItemGameplayRuntimeAdapter.Resolve(null);
        var required = ItemGameplayRuntimeAdapter.Resolve(null, true);
        var profile = Foundation().Gameplay! with
        {
            Critical = Foundation().Gameplay!.Critical with { AllowAuditedGameMasterOverride = false },
        };

        Assert.True(optional.OptionalFallback);
        Assert.Equal(ItemFoundationValidator.GameplayMissingRequiredCode,
            Assert.Single(required.Diagnostics).Code);
        Assert.Equal("RPG_ITEM_GM_OVERRIDE_FORBIDDEN",
            ItemGameplayRuntimeAdapter.AuthorizeCriticalAction(profile, "destroy", true, "gm", "").Code);
    }

    private static ItemFoundationProfile Foundation()
    {
        var typeId = Guid.Parse("c2890000-0000-4000-8000-000000000011");
        var foundation = new ItemFoundationProfile(Guid.Parse("c2890000-0000-4000-8000-000000000012"), 1,
            "test:r089", true, "crypt-of-ashes", "CC0-1.0", new string('8', 64), typeId,
            new(typeId, 1, "test:r089", "ritual-key", ["hand"], ["utility"], "rig.hand"),
            "ash-key", "asset.items.ash-key", "rig.hand", 1, 2, 4, 75, ["hand"],
            [new(Guid.Parse("c2890000-0000-4000-8000-000000000013"), "utility", 1, "effect.ash-key", "OnUse")],
            [new("fr", "Clé", "Courte", "Longue", "Inconnu", "Clé", "")], ["identified"],
            new(Guid.Parse("c2890000-0000-4000-8000-000000000014"), 1, "test:r089", true,
                "crypt-of-ashes", "CC0-1.0", new string('9', 64),
                new(["archetype", "appearance", "slots", "quality", "level", "property", "provenance",
                    "generation-rules"], ["fixed-seed"]),
                new(Guid.Parse("c2890000-0000-4000-8000-000000000015"), 1, "test:r089",
                    ["mesh"], [], ["hand-r"], "icon", [], "preview"),
                new(1, 1, 1, 1, 1, 1), new(1, "ash-key", true),
                new(3, 3, 3, 3, 0, 100, false, "rest")));
        return foundation with { Gameplay = Gameplay(foundation) };
    }

    private static ItemGameplayProfile Gameplay(ItemFoundationProfile foundation) => new(
        Guid.Parse("c2890000-0000-4000-8000-000000000016"), 1, "test:r089", true,
        "crypt-of-ashes", "CC0-1.0", new string('a', 64),
        new(true, true, true, true, true, true, true),
        new(Enum.GetValues<ItemTransferDestination>().ToDictionary(item => item,
            item => item is ItemTransferDestination.Container or ItemTransferDestination.GameMaster),
            ["item.transfer.quest"]),
        [new(Guid.Parse("c2890000-0000-4000-8000-000000000017"), 1, "test:r089", "ward",
            new Dictionary<string, double> { ["value"] = 8 }, "highest", "night", "+8 ward",
            "BeforeItemModifier", "AfterItemModifier")],
        new(Guid.Parse("c2890000-0000-4000-8000-000000000018"), "point",
            new Dictionary<string, double> { ["focus"] = 1 }, 1, 12_000,
            "ItemAbilityRequested", "ItemAbilityResolved"),
        new([
            new("hand-r", "forge", "humanoid", 2, ["hands-two"]),
            new("hand-l", "forge", "humanoid", 2, ["hands-two"]),
            new("hands-two", "forge", "humanoid", 3, ["hand-r", "hand-l"]),
        ], ["hand-r", "hand-l"]));
}
