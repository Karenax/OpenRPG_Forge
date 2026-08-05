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
        Assert.Equal(area.Id.Value, restored.Areas[0].RuntimePolicies.Rules.Perception.Id);
        Assert.True(restored.Areas[0].RuntimePolicies.Rules.Combat.ServerAuthoritative);
        Assert.Contains("\"rules\"", json, StringComparison.Ordinal);
        Assert.Equal("0.6.18", ProjectJsonSerializer.CurrentFormatVersion);
        Assert.Equal("1.8.0", AreaJsonSerializer.CurrentFormatVersion);
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
}
