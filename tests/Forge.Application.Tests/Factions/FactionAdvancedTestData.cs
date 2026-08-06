using Forge.Domain.Factions;

namespace Forge.Application.Tests.Factions;

internal static class FactionAdvancedTestData
{
    internal static FactionCatalog Complete()
    {
        var guild = Guid.Parse("b2020000-0000-4000-8000-000000000001");
        var watch = Guid.Parse("b2020000-0000-4000-8000-000000000002");
        var cult = Guid.Parse("b2020000-0000-4000-8000-000000000003");
        var created = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        return new(
            [
                new(guild, 1, "author:test", "ember-guild", "Guilde des braises", false, [],
                    ["guild"], new Dictionary<string, string> { ["law"] = "protect" }),
                new(watch, 1, "author:test", "ash-watch", "Veilleurs", false, [guild],
                    ["guard"]),
                new(cult, 1, "author:test", "glass-cult", "Culte", false, [],
                    ["cult"]),
            ],
            [new(guild, cult, -80, "war", "hostile")],
            [new("hostile", -100, -1, 1), new("neutral", 0, 100, 1)],
            [],
            new(
                Guid.Parse("b2020000-0000-4000-8000-000000000010"), 1, "author:test", true,
                [new(Guid.Parse("b2020000-0000-4000-8000-000000000011"), watch, true, true, true)],
                [new(Guid.Parse("b2020000-0000-4000-8000-000000000012"), watch, .5, .5, 40, 600, -10, true)],
                [new(Guid.Parse("b2020000-0000-4000-8000-000000000013"), 1, "author:test", "warden",
                    watch, "captain", null, 90, 1, true, null)],
                [new(Guid.Parse("b2020000-0000-4000-8000-000000000014"), 1, "runtime:test", watch,
                    DynamicFactionKind.Runtime, created, null, true, true, 0, [])]),
            true);
    }
}
