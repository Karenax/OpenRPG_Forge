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
        Assert.Equal("forge.generic", module.RuntimePolicies.Rules.Plugins.ActivePackageKey);
        var package = Assert.Single(module.RuntimePolicies.Rules.Plugins.Packages);
        Assert.All(new[] { package.Attributes, package.Effects, package.Abilities, package.Inventories,
            package.ProgressionTracks, package.UiPanels }, registry => Assert.NotEmpty(registry));
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

    [Fact]
    public void Plugin_validator_reports_missing_active_package_and_incomplete_registries()
    {
        var package = new RulesetPluginPackage(Guid.NewGuid(), 1, "custom", "1.0.0", "content.custom",
            ["health"], [], ["attack"], ["inventory"], ["level"], ["sheet"], "test");
        var catalog = new RulesetPluginCatalog(Guid.NewGuid(), 1, true, true, "missing", [package], "test");

        var diagnostics = RulesetPluginValidator.Validate(catalog, "module/rules/plugins");

        Assert.Contains(diagnostics, item => item.Code == RulesetPluginValidator.ActiveMissingCode &&
            item.Path.EndsWith("activePackageKey", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == RulesetPluginValidator.RegistryMissingCode &&
            item.Path.Contains(package.Id.ToString("D"), StringComparison.Ordinal));
    }
}
