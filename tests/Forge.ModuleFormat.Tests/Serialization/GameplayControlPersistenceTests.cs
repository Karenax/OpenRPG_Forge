using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class GameplayControlPersistenceTests
{
    [Fact]
    public void Project_json_round_trip_preserves_gameplay_control_profile()
    {
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Gameplay persistence", [],
            [new AreaDefinition(AreaId.From(Guid.NewGuid()), "arena", "Arena", [])]);

        var json = ProjectJsonSerializer.Serialize(project);
        var restored = ProjectJsonSerializer.Deserialize(json);
        var gameplay = restored.Module.RuntimePolicies.Gameplay;

        Assert.Equal(project.Module.RuntimePolicies.Gameplay.Id, gameplay.Id);
        Assert.Equal(project.Module.RuntimePolicies.Gameplay.Swimming.Id, gameplay.Swimming.Id);
        Assert.Equal(project.Module.RuntimePolicies.Gameplay.Swimming.WaterVolumeTags,
            gameplay.Swimming.WaterVolumeTags);
        Assert.Equal(project.Module.RuntimePolicies.Gameplay.RadialMenu.Actions.Select(action => action.Key),
            gameplay.RadialMenu.Actions.Select(action => action.Key));
        Assert.Contains(gameplay.RadialMenu.Actions, action => action.ParentKey == "combat");
        Assert.Equal("realtime-pause", gameplay.CombatModes.DefaultProviderId);
        Assert.Contains("\"gameplay\"", json, StringComparison.Ordinal);
    }
}
