using Forge.Application.Creatures;
using Forge.Domain.Combat;
using Forge.Domain.Creatures;

namespace Forge.Application.Tests.Creatures;

public sealed class CreatureLootScriptServiceTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-08-05T12:00:00Z");

    [Fact]
    public void HandleReadsSnapshotAndResolvesDeathAndTheftWithoutMutationSurface()
    {
        var profile = Profile();
        var service = Service(profile);

        var snapshot = service.Resolve(service.Handle);
        var death = service.ResolveDeath(service.Handle, 0.2, "player", "party", At);
        var theft = service.ResolvePickpocket(service.Handle,
            Guid.Parse("a1390000-0000-4000-8000-000000000010"), 0.2, "player", "party",
            14, 4, true, At, null);

        Assert.Equal(profile.Id, snapshot.Handle.Id);
        Assert.Equal(CreatureCorpseMode.RagdollContainer, snapshot.Corpse.Mode);
        Assert.Equal(2, death.Corpse.Contents.Count);
        Assert.Equal("ash-coin", theft.Grant!.ItemSlug);
        Assert.Contains("success:True", theft.Audit, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownHandleAndUnresolvedReferencesAreRejectedWithStableCodes()
    {
        var service = Service(Profile());
        var exception = Assert.Throws<KeyNotFoundException>(() => service.Resolve(
            new CreatureLootHandle(Guid.Parse("a1390000-0000-4000-8000-000000000099"), 1)));
        var invalid = Assert.Throws<ArgumentException>(() => new CreatureLootScriptService(
            Profile(), [], new HashSet<string>(StringComparer.Ordinal)));

        Assert.Contains("CREATURE_LOOT_HANDLE_INVALID", exception.Message, StringComparison.Ordinal);
        Assert.Contains("creature.loot.item_missing", invalid.Message, StringComparison.Ordinal);
        Assert.Contains("creature.loot.theft_skill_missing", invalid.Message, StringComparison.Ordinal);
    }

    private static CreatureLootScriptService Service(CreatureLootProfile profile) => new(
        profile,
        [new LootDrop("warden-ember", 1), new LootDrop("ash-coin", 3)],
        new HashSet<string>(["sleight", "awareness"], StringComparer.Ordinal));

    private static CreatureLootProfile Profile() => new(
        Guid.Parse("a1390000-0000-4000-8000-000000000001"), 1, "author:test", true,
        [
            new(Guid.Parse("a1390000-0000-4000-8000-000000000002"), "warden-ember", "main-hand", 1, 1, 1,
                true, false, LootOwnershipMode.Party, LootInstancingMode.PerParty, null),
            new(Guid.Parse("a1390000-0000-4000-8000-000000000003"), "ash-coin", "pouch", 1, 3, 0.5,
                true, true, LootOwnershipMode.Killer, LootInstancingMode.PerPlayer, null),
        ],
        new(true, "sleight", "awareness", 12, 10, true, "crime.theft", 30, "loot.pickpocket"),
        new(CreatureCorpseMode.RagdollContainer, 45, "corpse.standard", "ragdoll.humanoid", true,
            "party", 8, CorpseCleanupMode.Timed), new Dictionary<string, string>());
}
