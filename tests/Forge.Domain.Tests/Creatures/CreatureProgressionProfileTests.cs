using Forge.Domain.Creatures;

namespace Forge.Domain.Tests.Creatures;

public sealed class CreatureProgressionProfileTests
{
    private static readonly IReadOnlySet<string> Abilities =
        new HashSet<string>(["ember-bolt", "warding-step"], StringComparer.Ordinal);

    [Fact]
    public void CompleteProfile_ValidatesDataDrivenProgressionAndResolvesRuntimeSnapshot()
    {
        var profile = Complete();

        var diagnostics = CreatureProgressionValidator.Validate(profile, availableAbilityIds: Abilities);
        var runtime = CreatureProgressionRuntimeResolver.Resolve(profile, Abilities, required: true);

        Assert.Empty(diagnostics);
        Assert.True(runtime.Succeeded);
        Assert.Equal(5, runtime.TotalLevel);
        Assert.Equal(3, runtime.SkillRanks["lore"]);
        Assert.Equal(["ember-bolt"], runtime.PreparedAbilityIds);
        Assert.Equal(6.5, CreatureProgressionRuntimeResolver.ResolveSkillCheck(runtime, "lore", 2.5, 1));
        Assert.Equal("forge.generic", profile.RulesetId);
        Assert.Equal(["scholar", "warden"], profile.Tracks.Select(item => item.TrackId));
    }

    [Fact]
    public void InvalidProfile_ReportsStableLocalizedDiagnostics()
    {
        var invalid = new CreatureProgressionProfile(
            Guid.Empty, 99, "", "?", false, true,
            [new("one", "", 0, 0, -1, ["missing"]), new("two", "Two", 1, 1, 0, [])],
            [new("ethics", "", "", 2)],
            [new("lore", "Lore", "insight", 1, 1, 0, false, true, -1)],
            [new("mastery", "Mastery", 1, 1, true, [new("skill", "missing", 1)], ["missing-power"], [])],
            new("", AbilityLoadoutMode.Prepared, 0, ["missing-power"],
                [new("missing-power", 1, 0, 0, null, 0, [])]),
            new Dictionary<string, string> { ["invalid key"] = "" });

        var diagnostics = CreatureProgressionValidator.Validate(
            invalid, "blueprints/test/creatureProgression", Abilities);

        Assert.Contains(diagnostics, item => item.Code == "creature.progression.uuid_required");
        Assert.Contains(diagnostics, item => item.Code == "creature.progression.multiclass_forbidden");
        Assert.Contains(diagnostics, item => item.Code == "creature.progression.track_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.progression.disposition_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.progression.skill_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.progression.talent_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.progression.loadout_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.progression.ability_missing");
        Assert.Contains(diagnostics, item => item.Code == "creature.progression.override_invalid");
        Assert.All(diagnostics, item => Assert.StartsWith("blueprints/test/creatureProgression", item.Path));
    }

    [Fact]
    public void RuntimeResolver_IgnoresOptionalAbsenceAndDiagnosesRequiredAbsence()
    {
        var optional = CreatureProgressionRuntimeResolver.Resolve(null, Abilities);
        var required = CreatureProgressionRuntimeResolver.Resolve(null, Abilities, required: true);

        Assert.True(optional.Succeeded);
        Assert.True(optional.IgnoredOptionalProfile);
        Assert.False(required.Succeeded);
        Assert.Equal("creature.progression.required_missing", Assert.Single(required.Diagnostics).Code);
    }

    internal static CreatureProgressionProfile Complete() => new(
        Guid.Parse("a1320000-0000-4000-8000-000000000001"),
        1,
        "author:test",
        "Forge.Generic",
        allowMultipleTracks: true,
        requiredAtRuntime: true,
        [
            new("warden", "Gardien", 3, 20, 2_400, ["ember-mastery"]),
            new("scholar", "Érudit", 2, 10, 900, []),
        ],
        [new("mercy", "Impitoyable", "Miséricordieux", 0.65)],
        [new("lore", "Savoir ancien", "insight", 3, 10, 4, true, true, 12)],
        [new("ember-mastery", "Maîtrise des braises", 1, 3, true,
            [new("track", "warden", 2), new("skill", "lore", 2)], ["ember-bolt"], ["combat.on-hit"])],
        new("prepared", AbilityLoadoutMode.Prepared, 3, ["ember-bolt", "warding-step"],
            [new("ember-bolt", 2, 3, 4.5, "focus", 2, ["empowered"])]),
        new Dictionary<string, string> { ["xp-rate"] = "1.0" });
}
