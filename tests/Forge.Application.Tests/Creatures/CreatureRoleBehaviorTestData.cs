using Forge.Domain.Creatures;

namespace Forge.Application.Tests.Creatures;

internal static class CreatureRoleBehaviorTestData
{
    internal static CreatureRoleBehaviorProfile Complete() => new(
        Guid.Parse("a1430000-0000-4000-8000-000000000001"), 1, "author:test", true,
        new("forge.generic", new Dictionary<string, string>
        {
            ["deity"] = "deity.embers",
            ["occupation"] = "crypt-warden",
        }, ["guardian", "mystic"]),
        new("ambient.warden",
        [
            new(Guid.Parse("a1430000-0000-4000-8000-000000000002"), "idle.day", ["idle", "inspect"], 2, 2, 5),
            new(Guid.Parse("a1430000-0000-4000-8000-000000000003"), "idle.night", ["look-around", "warm-hands"], 1, 3, 8),
        ],
        [new(Guid.Parse("a1430000-0000-4000-8000-000000000004"), "night-watch", 1200, 360,
            "idle.night", "campfire", "warden-warning")],
        [new(Guid.Parse("a1430000-0000-4000-8000-000000000005"), "inspect-brazier", "brazier", "inspect", 12)],
        new(true, true, true, AmbientResumeMode.Resume, .5)),
        new(Guid.Parse("a1430000-0000-4000-8000-000000000006"), "warden-balanced", 42, 6, true, false,
            [new("warden", 2), new("mystic", 1)],
            [new("awareness", 2, 10), new("lore", 3, 10)], [new("ember-stance", 4)]),
        new Dictionary<string, string> { ["ambient.radius"] = "4" });

    internal static CreatureProgressionProfile Progression() => new(
        Guid.Parse("a1430000-0000-4000-8000-000000000010"), 1, "author:test", "forge.generic", true, false,
        [new("warden", "Gardien", 1, 20, 0, []), new("mystic", "Mystique", 1, 20, 0, [])], [],
        [
            new("awareness", "Vigilance", "insight", 0, 10, 0, false, false, 10),
            new("lore", "Savoir", "insight", 0, 10, 0, false, false, 10),
        ],
        [new("ember-stance", "Posture des braises", 0, 1, true, [], [], [])],
        new("standard", AbilityLoadoutMode.Learned, 0, [], []), new Dictionary<string, string>());
}
