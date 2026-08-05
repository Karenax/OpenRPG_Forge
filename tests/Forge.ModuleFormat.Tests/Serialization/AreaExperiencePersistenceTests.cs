using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class AreaExperiencePersistenceTests
{
    [Fact]
    public void Project_and_area_round_trip_preserve_area_experience_profile()
    {
        var area = new AreaDefinition(AreaId.From(Guid.NewGuid()), "experience", "Experience", []);
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Experience persistence", [], [area]);

        var projectJson = ProjectJsonSerializer.Serialize(project);
        var areaJson = AreaJsonSerializer.Serialize(area);
        var restoredProject = ProjectJsonSerializer.Deserialize(projectJson);
        var restoredArea = AreaJsonSerializer.Deserialize(areaJson);

        Assert.Equal(area.Id.Value, restoredProject.Areas[0].RuntimePolicies.Experience.Id);
        Assert.Equal("Chargement", restoredArea.RuntimePolicies.Experience.Loading.Title);
        Assert.Contains("\"experience\"", projectJson, StringComparison.Ordinal);
        Assert.Equal("0.6.19", ProjectJsonSerializer.CurrentFormatVersion);
        Assert.Equal("1.8.0", AreaJsonSerializer.CurrentFormatVersion);
    }

    [Fact]
    public void Previous_formats_migrate_missing_experience_to_stable_default()
    {
        var area = new AreaDefinition(AreaId.From(Guid.NewGuid()), "legacy", "Legacy", []);
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Legacy experience", [], [area]);
        var projectJson = ProjectJsonSerializer.Serialize(project)
            .Replace(ProjectJsonSerializer.CurrentFormatVersion,
                ProjectJsonSerializer.PreAreaExperienceFormatVersion, StringComparison.Ordinal)
            .Replace("\n        \"experience\": {", "\n        \"experienceRemoved\": {", StringComparison.Ordinal);

        var restored = ProjectJsonSerializer.Deserialize(projectJson);
        Assert.Equal(area.Id.Value, restored.Areas[0].RuntimePolicies.Experience.Id);
        Assert.Equal("Chargement", restored.Areas[0].RuntimePolicies.Experience.Loading.Title);
    }
}
