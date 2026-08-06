using Forge.Application.Creatures;
using Forge.Domain.Creatures;

namespace Forge.Application.Tests.Creatures;

public sealed class CreatureProgressionScriptServiceTests
{
    [Fact]
    public void HandleReadsTypedSnapshotAndSkillCheckWithoutMutationSurface()
    {
        var profile = Profile();
        var service = new CreatureProgressionScriptService(profile,
            new HashSet<string>(["ember-bolt"], StringComparer.Ordinal));

        var snapshot = service.Resolve(service.Handle);
        var check = service.ResolveSkillCheck(service.Handle, "lore", 2, 1);

        Assert.Equal(profile.Id, snapshot.Handle.Id);
        Assert.Equal(3, snapshot.TotalLevel);
        Assert.Equal(3, snapshot.TrackLevels["warden"]);
        Assert.Equal(0.5, snapshot.DispositionValues["mercy"]);
        Assert.Equal(2, snapshot.SkillRanks["lore"]);
        Assert.Equal(1, snapshot.TalentRanks["ember-mastery"]);
        Assert.Equal(["ember-bolt"], snapshot.PreparedAbilityIds);
        Assert.Equal(5, check);
    }

    [Fact]
    public void UnknownHandleIsRejectedWithStableCode()
    {
        var service = new CreatureProgressionScriptService(Profile(),
            new HashSet<string>(["ember-bolt"], StringComparer.Ordinal));

        var exception = Assert.Throws<KeyNotFoundException>(() => service.Resolve(
            new CreatureProgressionHandle(Guid.Parse("a1360000-0000-4000-8000-000000000099"), 1)));

        Assert.Contains("CREATURE_PROGRESSION_HANDLE_INVALID", exception.Message, StringComparison.Ordinal);
    }

    private static CreatureProgressionProfile Profile() => new(
        Guid.Parse("a1360000-0000-4000-8000-000000000001"), 1, "author:test", "forge.generic", true, true,
        [new("warden", "Gardien", 3, 20, 2_400, ["ember-mastery"])],
        [new("mercy", "Impitoyable", "Miséricordieux", 0.5)],
        [new("lore", "Savoir", "insight", 2, 10, 2, true, false, 12)],
        [new("ember-mastery", "Maîtrise des braises", 1, 3, true,
            [new("track", "warden", 2)], ["ember-bolt"], ["combat.on-hit"])],
        new("prepared", AbilityLoadoutMode.Prepared, 2, ["ember-bolt"],
            [new("ember-bolt", 1, 2, 0.5, "focus", 1, ["empowered"])]),
        new Dictionary<string, string>());
}
