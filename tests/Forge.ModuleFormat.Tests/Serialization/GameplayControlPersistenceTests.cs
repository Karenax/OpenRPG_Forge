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
        Assert.Equal(project.Module.RuntimePolicies.Gameplay.Foundation.AllIds(), gameplay.Foundation.AllIds());
        Assert.Equal(project.Module.RuntimePolicies.Gameplay.Simulation.AllIds(), gameplay.Simulation.AllIds());
        Assert.Equal(project.Module.RuntimePolicies.Gameplay.Simulation.Projectiles.VfxAssetId,
            gameplay.Simulation.Projectiles.VfxAssetId);
        Assert.Single(gameplay.Simulation.Physics.Joints);
        Assert.Single(gameplay.Simulation.WorldTime.Schedules);
        Assert.Equal(project.Module.RuntimePolicies.Gameplay.Session.AllIds(), gameplay.Session.AllIds());
        Assert.Single(gameplay.Session.Checkpoints);
        Assert.Equal(3, gameplay.Session.ControlLocks.Contexts.Count);
        Assert.True(gameplay.Session.Replay.HeadlessReplay);
        Assert.Equal(8, gameplay.Foundation.Cameras.Profiles.Count);
        Assert.Equal("SHA-256", gameplay.Foundation.Save.ChecksumAlgorithm);
        Assert.Contains("\"gameplay\"", json, StringComparison.Ordinal);
    }
}
