using Forge.Domain.Areas;
using Forge.Domain.Modules;
using Forge.Domain.Rules;
using Forge.Domain.Validation;

namespace Forge.Domain.Tests.Rules;

public sealed class RulesProfileTests
{
    [Fact]
    public void Defaults_are_generic_versioned_and_owner_scoped()
    {
        var areaId = AreaId.From(Guid.NewGuid());
        var moduleId = ModuleId.From(Guid.NewGuid());
        var module = new ModuleDefinition(moduleId, "module", "Module", string.Empty, areaId);
        var area = new AreaDefinition(areaId, "area", "Area", []);

        Assert.Equal("forge.generic", module.RuntimePolicies.Rules.Progression.RulesetId);
        Assert.Equal("standard", module.RuntimePolicies.Rules.ActiveDifficultyKey);
        Assert.Equal(moduleId.Value, module.RuntimePolicies.Rules.Id);
        Assert.Equal(areaId.Value, area.RuntimePolicies.Rules.Id);
        Assert.True(area.RuntimePolicies.Rules.Combat.ServerAuthoritative);
        Assert.Empty(RuntimePolicyValidator.Validate(module));
        Assert.Empty(RuntimePolicyValidator.Validate(area));
    }

    [Fact]
    public void Progression_curve_and_perception_keys_are_validated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProgressionPolicy(Guid.NewGuid(), 1, "rules",
            [new(1, 0), new(2, 0)], 1, 1, new Dictionary<string, double> { ["quest"] = 1 }, true, "test"));
        var area = AreaId.From(Guid.NewGuid());
        var duplicate = new[]
        {
            new EnvironmentPerceptionModifier(Guid.NewGuid(), "vision", 1, 0, "fog"),
            new EnvironmentPerceptionModifier(Guid.NewGuid(), "vision", .5, 0, "fog"),
        };
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EnvironmentPerceptionProfile(area.Value, 1, "rules", duplicate, "test"));
    }
}
