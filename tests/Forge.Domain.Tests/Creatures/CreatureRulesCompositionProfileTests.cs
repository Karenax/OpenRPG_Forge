using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Creatures;

public sealed class CreatureRulesCompositionProfileTests
{
    [Fact]
    public void CompleteProfile_NormalizesEvaluatesAndMitigatesDeterministically()
    {
        var profile = Complete();

        Assert.Empty(CreatureRulesCompositionValidator.Validate(profile));
        var attributes = CreatureRulesCompositionValidator.EvaluateAttributes(profile.Attributes);

        Assert.Equal(CreatureRulesInitialLifeState.Seated, profile.InitialState.LifeState);
        Assert.Equal(7, attributes["agility"]);
        Assert.Equal(16, attributes["evasion"]);
        Assert.Equal(5, CreatureRulesCompositionValidator.Mitigate(profile.Defenses, "glass", 10));
        Assert.Equal(0, CreatureRulesCompositionValidator.Mitigate(profile.Defenses, "radiant", 10));
        Assert.Equal(["guardian", "melee"], profile.Abilities[0].AiTags);
        Assert.True(profile.Inventory.Persistent);
    }

    [Fact]
    public void InvalidSpecializedComponents_ReportFiveLocalizedPaths()
    {
        var source = Complete();
        var invalid = new CreatureRulesCompositionProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.AssetId, source.PreviewTransform,
            source.InitialState with { LifeState = CreatureRulesInitialLifeState.Dead, PoseId = null },
            source.Attributes with { BaseValues = new Dictionary<string, double>() },
            source.Defenses with { Resistances = new Dictionary<string, double> { ["glass"] = 2 } },
            [source.Abilities[0] with { Charges = -1 }],
            source.Inventory with { Containers = [] }, source.Overrides);

        var diagnostics = CreatureRulesCompositionValidator.Validate(invalid, "blueprints/warden/rules");

        Assert.Equal(5, diagnostics.Count);
        Assert.Contains(diagnostics, item => item.Code == "creature.rules.initial_state_invalid" &&
                                             item.Path.Contains("initialState", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == "creature.rules.attributes_invalid" &&
                                             item.Path.Contains("attributes", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == "creature.rules.defenses_invalid" &&
                                             item.Path.Contains("defenses", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == "creature.rules.abilities_invalid" &&
                                             item.Path.Contains("abilities", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == "creature.rules.inventory_invalid" &&
                                             item.Path.Contains("inventory", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateComponentUuidAndAttributeCycle_AreRejectedBeforeRuntime()
    {
        var source = Complete();
        var cyclic = source.Attributes with
        {
            Derived =
            [
                new(source.InitialState.Id, "cycle-a", CreatureRulesAttributeFormulaOperation.Sum, ["cycle-b"], 0),
                new(Guid.Parse("a1780000-0000-4000-8000-000000000015"), "cycle-b",
                    CreatureRulesAttributeFormulaOperation.Sum, ["cycle-a"], 0),
            ],
        };
        var invalid = new CreatureRulesCompositionProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.AssetId, source.PreviewTransform, source.InitialState, cyclic, source.Defenses,
            source.Abilities, source.Inventory, source.Overrides);

        var diagnostics = CreatureRulesCompositionValidator.Validate(invalid);

        Assert.Contains(diagnostics, item => item.Code == "creature.rules.component_uuid_duplicate");
        Assert.Contains(diagnostics, item => item.Code == "creature.rules.attributes_invalid");
    }

    [Fact]
    public void OptionalAbsenceIsSafeAndRequiredAbsenceIsDiagnosed()
    {
        Assert.True(CreatureRulesCompositionRuntimeResolver.Resolve(null, false).Succeeded);
        Assert.Equal(CreatureRulesCompositionValidator.MissingRequiredCode,
            Assert.Single(CreatureRulesCompositionRuntimeResolver.Resolve(null, true).Diagnostics).Code);
    }

    public static CreatureRulesCompositionProfile Complete(BlueprintId? itemBlueprintId = null) => new(
        Guid.Parse("a1780000-0000-4000-8000-000000000001"),
        CreatureRulesCompositionProfile.CurrentSchemaVersion,
        "author:r078",
        "asset.creature-rules.ash-warden",
        TransformValue.Identity,
        new(Guid.Parse("a1780000-0000-4000-8000-000000000002"), CreatureRulesInitialLifeState.Seated,
            "clip.warden.seated", "pose.warden-throne", false),
        new(Guid.Parse("a1780000-0000-4000-8000-000000000003"), "ruleset.crypt",
            new Dictionary<string, double> { ["strength"] = 8, ["agility"] = 6 },
            [new(Guid.Parse("a1780000-0000-4000-8000-000000000004"), "agility", "species.emberborn", 1)],
            [new(Guid.Parse("a1780000-0000-4000-8000-000000000005"), "evasion",
                CreatureRulesAttributeFormulaOperation.Sum, ["agility", "strength"], 1)]),
        new(Guid.Parse("a1780000-0000-4000-8000-000000000006"), 4, 2,
            new Dictionary<string, double> { ["glass"] = .2 },
            new Dictionary<string, double> { ["fortitude"] = 6, ["reflex"] = 4 },
            ["radiant"], 1),
        [new(Guid.Parse("a1780000-0000-4000-8000-000000000007"), "ability.ash-nova", 3, 2, 12,
            ["state.alive"], ["guardian", "melee"], "script.ash-nova")],
        new(Guid.Parse("a1780000-0000-4000-8000-000000000008"),
            [new(Guid.Parse("a1780000-0000-4000-8000-000000000009"), "container.backpack", 8, 100, 100,
                [new(Guid.Parse("a1780000-0000-4000-8000-000000000010"),
                    itemBlueprintId ?? BlueprintId.From(Guid.Parse("a1780000-0000-4000-8000-000000000011")),
                    2, 2, 1)])],
            new Dictionary<string, Guid>
            {
                ["main-hand"] = Guid.Parse("a1780000-0000-4000-8000-000000000010"),
            }, "creature.ash-warden", true),
        new Dictionary<string, string> { ["defense.preview"] = "enabled" });
}
