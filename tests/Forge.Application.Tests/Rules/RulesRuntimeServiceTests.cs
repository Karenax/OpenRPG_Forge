using Forge.Application.Rules;
using Forge.Domain.Modules;
using Forge.Domain.Rules;
using Forge.Domain.RuntimePolicies;

namespace Forge.Application.Tests.Rules;

public sealed class RulesRuntimeServiceTests
{
    [Fact]
    public void Progression_is_data_driven_by_source_group_difficulty_and_curve()
    {
        var policy = new ProgressionPolicy(Guid.NewGuid(), 1, "custom", [new(1, 0), new(2, 100), new(3, 300)],
            2, 1.5, new Dictionary<string, double> { ["quest"] = 2 }, true, "test");
        var result = RulesRuntimeService.ResolveProgression(policy, 50, "quest", 2, 50, 1.25);
        Assert.Equal(187.5, result.AwardedExperience);
        Assert.Equal(2, result.Level);
        Assert.Equal(300, result.NextLevelExperience);
        Assert.Contains("source:2", result.Trace);
    }

    [Fact]
    public void Difficulty_mutators_compose_and_perception_is_sense_condition_driven()
    {
        var module = ModuleId.From(Guid.NewGuid());
        var defaults = ModuleRulesProfile.CreateDefault(module);
        var mutators = new MutatorSet(defaults.Mutators.Id, 1,
            defaults.Mutators.Mutators.Select(item => item with { Enabled = true }).ToArray(), "test");
        var profile = new ModuleRulesProfile(defaults.Id, 1, defaults.Progression, defaults.Difficulties,
            defaults.ActiveDifficultyKey, mutators, "test");
        var difficulty = RulesRuntimeService.ResolveDifficulty(profile);
        Assert.Equal(.65, difficulty.IncomingDamageMultiplier, 5);
        Assert.Equal(.7, difficulty.EconomyMultiplier, 5);
        Assert.Equal(1.25, difficulty.ProgressionMultiplier, 5);

        var perception = new EnvironmentPerceptionProfile(Guid.NewGuid(), 1, "custom",
            [new EnvironmentPerceptionModifier(Guid.NewGuid(), "vision", .5, .1, "fog")], "test");
        var result = RulesRuntimeService.ResolvePerception(perception, "vision", 20, .8,
            new HashSet<string>(["fog"], StringComparer.Ordinal));
        Assert.Equal(10, result.Range);
        Assert.Equal(.5, result.Confidence, 5);
    }

    [Fact]
    public void Ruleset_plugin_registers_six_capability_families_and_handles_optional_absence()
    {
        var catalog = RulesetPluginCatalog.CreateDefault(Guid.NewGuid());
        var resolved = RulesetPluginRuntimeService.Resolve(catalog, ["content.core"]);

        Assert.True(resolved.Succeeded);
        Assert.True(resolved.ExtensionPresent);
        Assert.NotEmpty(resolved.Registration!.Attributes);
        Assert.NotEmpty(resolved.Registration.Effects);
        Assert.NotEmpty(resolved.Registration.Abilities);
        Assert.NotEmpty(resolved.Registration.Inventories);
        Assert.NotEmpty(resolved.Registration.ProgressionTracks);
        Assert.NotEmpty(resolved.Registration.UiPanels);

        var optional = new RulesetPluginCatalog(Guid.NewGuid(), 1, false, false, string.Empty, [], "test");
        var ignored = RulesetPluginRuntimeService.Resolve(optional, []);
        Assert.True(ignored.Succeeded);
        Assert.False(ignored.ExtensionPresent);

        var required = catalog with { };
        var missing = RulesetPluginRuntimeService.Resolve(required, []);
        Assert.False(missing.Succeeded);
        Assert.Equal(RulesetPluginRuntimeService.ContentMissingCode, missing.DiagnosticCode);
    }
}
