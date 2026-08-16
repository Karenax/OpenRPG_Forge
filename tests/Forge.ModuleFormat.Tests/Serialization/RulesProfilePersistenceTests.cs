using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class RulesProfilePersistenceTests
{
    [Fact]
    public void Project_json_round_trip_preserves_module_and_area_rules_profiles()
    {
        var area = new AreaDefinition(AreaId.From(Guid.NewGuid()), "rules", "Rules", []);
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Rules persistence", [], [area]);
        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json);

        Assert.Equal("forge.generic", restored.Module.RuntimePolicies.Rules.Progression.RulesetId);
        Assert.Equal("standard", restored.Module.RuntimePolicies.Rules.ActiveDifficultyKey);
        Assert.Equal(2, restored.Module.RuntimePolicies.Rules.Mutators.Mutators.Count);
        Assert.Equal("forge.generic", restored.Module.RuntimePolicies.Rules.Plugins.ActivePackageKey);
        Assert.NotEmpty(Assert.Single(restored.Module.RuntimePolicies.Rules.Plugins.Packages).UiPanels);
        Assert.Equal(area.Id.Value, restored.Areas[0].RuntimePolicies.Rules.Perception.Id);
        Assert.True(restored.Areas[0].RuntimePolicies.Rules.Combat.ServerAuthoritative);
        Assert.Contains("\"rules\"", json, StringComparison.Ordinal);
        Assert.Equal("0.6.36", ProjectJsonSerializer.CurrentFormatVersion);
        Assert.Equal("1.9.0", AreaJsonSerializer.CurrentFormatVersion);
    }

    [Fact]
    public void Previous_formats_migrate_missing_rules_profiles_to_stable_defaults()
    {
        var area = new AreaDefinition(AreaId.From(Guid.NewGuid()), "legacy", "Legacy", []);
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Legacy rules", [], [area]);
        var projectJson = ProjectJsonSerializer.Serialize(project)
            .Replace(ProjectJsonSerializer.CurrentFormatVersion, ProjectJsonSerializer.PreRulesProfileFormatVersion,
                StringComparison.Ordinal)
            .Replace("\n        \"rules\": {", "\n        \"rulesRemoved\": {", StringComparison.Ordinal);

        Assert.Equal("forge.generic", ProjectJsonSerializer.Deserialize(projectJson)
            .Module.RuntimePolicies.Rules.Progression.RulesetId);
    }

    [Fact]
    public void Missing_ruleset_plugin_catalog_migrates_to_deterministic_default()
    {
        var project = TestProjectFactory.CreateValid();
        var node = System.Text.Json.Nodes.JsonNode.Parse(ProjectJsonSerializer.Serialize(project))!.AsObject();
        node["project"]!["module"]!["runtimePolicies"]!["rules"]!.AsObject().Remove("plugins");

        var first = ProjectJsonSerializer.Deserialize(node.ToJsonString());
        var second = ProjectJsonSerializer.Deserialize(node.ToJsonString());

        var firstCatalog = first.Module.RuntimePolicies.Rules.Plugins;
        var secondCatalog = second.Module.RuntimePolicies.Rules.Plugins;
        Assert.Equal(firstCatalog.Id, secondCatalog.Id);
        Assert.Equal(firstCatalog.ActivePackageKey, secondCatalog.ActivePackageKey);
        Assert.Equal(Assert.Single(firstCatalog.Packages).Id, Assert.Single(secondCatalog.Packages).Id);
        Assert.Equal("forge.generic", firstCatalog.ActivePackageKey);
    }
}
