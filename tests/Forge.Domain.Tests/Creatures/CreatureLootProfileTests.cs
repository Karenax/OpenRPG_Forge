using Forge.Domain.Combat;
using Forge.Domain.Creatures;

namespace Forge.Domain.Tests.Creatures;

public sealed class CreatureLootProfileTests
{
    private static readonly IReadOnlySet<string> Items =
        new HashSet<string>(["warden-ember", "ash-coin"], StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> Skills =
        new HashSet<string>(["sleight", "awareness"], StringComparer.Ordinal);

    [Fact]
    public void CompleteProfile_ValidatesAndBuildsOwnedInstancedCorpseLoot()
    {
        var profile = Complete();
        var at = DateTimeOffset.Parse("2026-08-05T12:00:00Z");

        var diagnostics = CreatureLootValidator.Validate(profile, availableItemSlugs: Items, availableSkillIds: Skills);
        var death = CreatureLootRuntimeResolver.ResolveDeath(profile,
            [new LootDrop("warden-ember", 1), new LootDrop("ash-coin", 3)], 0.2, "player-1", "party-1", at, true);

        Assert.Empty(diagnostics);
        Assert.True(death.Succeeded);
        Assert.False(death.UsedLegacyFallback);
        Assert.Empty(death.Grants);
        Assert.True(death.Corpse.Created);
        Assert.Equal(CreatureCorpseMode.RagdollContainer, death.Corpse.Mode);
        Assert.Equal(at.AddSeconds(45), death.Corpse.ExpiresAt);
        Assert.Equal(2, death.Corpse.Contents.Count);
        Assert.Contains(death.Corpse.Contents, item => item.ItemSlug == "warden-ember" && item.OwnerId == "party-1");
        Assert.Contains(death.Corpse.Contents, item => item.ItemSlug == "ash-coin" && item.InstanceKey == "player:player-1");
    }

    [Fact]
    public void PickpocketRequiresConsentTracksCooldownCrimeAndAudit()
    {
        var profile = Complete();
        var at = DateTimeOffset.Parse("2026-08-05T12:00:00Z");
        var transaction = Guid.Parse("a1370000-0000-4000-8000-000000000010");

        var denied = CreatureLootRuntimeResolver.ResolvePickpocket(profile, transaction, 0.2,
            "player-1", "party-1", 14, 4, false, at, null);
        var success = CreatureLootRuntimeResolver.ResolvePickpocket(profile, transaction, 0.2,
            "player-1", "party-1", 14, 4, true, at, null);
        var cooldown = CreatureLootRuntimeResolver.ResolvePickpocket(profile, transaction, 0.2,
            "player-1", "party-1", 14, 4, true, at.AddSeconds(5), at);
        var detected = CreatureLootRuntimeResolver.ResolvePickpocket(profile, transaction, 0.2,
            "player-1", "party-1", 5, 20, true, at, null);

        Assert.Equal("creature.loot.theft_consent_required", Assert.Single(denied.Diagnostics).Code);
        Assert.True(success.Succeeded);
        Assert.False(success.Detected);
        Assert.Equal("ash-coin", success.Grant!.ItemSlug);
        Assert.Contains(transaction.ToString("D"), success.Audit, StringComparison.Ordinal);
        Assert.Equal("creature.loot.theft_cooldown", Assert.Single(cooldown.Diagnostics).Code);
        Assert.True(detected.Detected);
        Assert.Equal("crime.theft", detected.CrimeId);
    }

    [Fact]
    public void OptionalAbsenceUsesLegacyCombatLootAndRequiredAbsenceIsDiagnosed()
    {
        var loot = new[] { new LootDrop("warden-ember", 1) };
        var optional = CreatureLootRuntimeResolver.ResolveDeath(null, loot, 0, "player", "party", DateTimeOffset.UnixEpoch);
        var required = CreatureLootRuntimeResolver.ResolveDeath(null, loot, 0, "player", "party", DateTimeOffset.UnixEpoch, true);

        Assert.True(optional.Succeeded);
        Assert.True(optional.UsedLegacyFallback);
        Assert.Equal("warden-ember", Assert.Single(optional.Grants).ItemSlug);
        Assert.False(required.Succeeded);
        Assert.Equal("creature.loot.required_missing", Assert.Single(required.Diagnostics).Code);
    }

    [Fact]
    public void InvalidProfileReportsStableLocalizedDiagnostics()
    {
        var invalid = new CreatureLootProfile(Guid.Empty, 99, "", true,
            [new(Guid.Empty, "missing", null, 0, -1, 2, true, false,
                LootOwnershipMode.FixedOwner, LootInstancingMode.Shared, null)],
            new(true, "missing", "other", -1, -1, true, "", -1, ""),
            new(CreatureCorpseMode.RagdollContainer, 0, null, null, true, "", 0, CorpseCleanupMode.Timed),
            new Dictionary<string, string> { ["invalid key"] = "" });

        var diagnostics = CreatureLootValidator.Validate(invalid, "blueprints/test/creatureLoot", Items, Skills);

        Assert.Contains(diagnostics, item => item.Code == "creature.loot.uuid_required");
        Assert.Contains(diagnostics, item => item.Code == "creature.loot.entry_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.loot.item_missing");
        Assert.Contains(diagnostics, item => item.Code == "creature.loot.theft_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.loot.theft_skill_missing");
        Assert.Contains(diagnostics, item => item.Code == "creature.loot.corpse_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.loot.override_invalid");
        Assert.All(diagnostics, item => Assert.StartsWith("blueprints/test/creatureLoot", item.Path));
    }

    internal static CreatureLootProfile Complete() => new(
        Guid.Parse("a1370000-0000-4000-8000-000000000001"), 1, "author:test", true,
        [
            new(Guid.Parse("a1370000-0000-4000-8000-000000000002"), "warden-ember", "main-hand", 1, 1, 1,
                true, false, LootOwnershipMode.Party, LootInstancingMode.PerParty, null),
            new(Guid.Parse("a1370000-0000-4000-8000-000000000003"), "ash-coin", "pouch", 1, 3, 0.5,
                true, true, LootOwnershipMode.Killer, LootInstancingMode.PerPlayer, null),
        ],
        new(true, "sleight", "awareness", 12, 10, true, "crime.theft", 30, "loot.pickpocket"),
        new(CreatureCorpseMode.RagdollContainer, 45, "corpse.standard", "ragdoll.humanoid", true,
            "party", 8, CorpseCleanupMode.Timed),
        new Dictionary<string, string> { ["sparkle"] = "rare-only" });
}
