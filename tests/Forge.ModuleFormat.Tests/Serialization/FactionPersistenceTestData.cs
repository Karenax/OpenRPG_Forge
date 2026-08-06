using Forge.Domain.Factions;

namespace Forge.ModuleFormat.Tests.Serialization;

internal static class FactionPersistenceTestData
{
    internal static FactionCatalog Complete()
    {
        var parent = Guid.Parse("b2030000-0000-4000-8000-000000000001");
        var watch = Guid.Parse("b2030000-0000-4000-8000-000000000002");
        var cult = Guid.Parse("b2030000-0000-4000-8000-000000000003");
        var created = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        return new(
            [
                new(parent, 1, "author:test", "ember-guild", "Guilde des braises", false, [],
                    ["guild"], new Dictionary<string, string> { ["law"] = "protect" }),
                new(watch, 1, "author:test", "ash-watch", "Veilleurs", false, [parent],
                    ["guard"]),
                new(cult, 1, "author:test", "glass-cult", "Culte", false, [],
                    ["cult"]),
            ],
            [new(parent, cult, -80, "ancestral-war", "hostile")],
            [new("hostile", -100, -1, 1), new("neutral", 0, 100, 1)],
            [],
            new(
                Guid.Parse("b2030000-0000-4000-8000-000000000010"), 1, "author:test", true,
                [new(Guid.Parse("b2030000-0000-4000-8000-000000000011"), watch, true, true, true)],
                [new(Guid.Parse("b2030000-0000-4000-8000-000000000012"), watch, .6, .5, 30, 600, -12, true)],
                [
                    new(Guid.Parse("b2030000-0000-4000-8000-000000000013"), 1, "author:test", "warden",
                        watch, "captain", null, 95, 1, true, null),
                    new(Guid.Parse("b2030000-0000-4000-8000-000000000014"), 1, "author:test", "warden",
                        cult, "initiate", "Ember Pilgrim", -20, .25, false, created.AddDays(5)),
                ],
                [new(Guid.Parse("b2030000-0000-4000-8000-000000000015"), 1, "runtime:test", watch,
                    DynamicFactionKind.Temporary, created, created.AddDays(10), true, true, 1,
                    [new(Guid.Parse("b2030000-0000-4000-8000-000000000016"), cult,
                        FactionDiplomacyStatus.War, created, null, "crypt-conflict")])]),
            true);
    }
}
