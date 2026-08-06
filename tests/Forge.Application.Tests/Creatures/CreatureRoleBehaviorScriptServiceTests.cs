using Forge.Application.Creatures;
namespace Forge.Application.Tests.Creatures;

public sealed class CreatureRoleBehaviorScriptServiceTests
{
    [Fact]
    public void HandleReadsRoleScheduleAndAutoLevelPreviewWithoutMutationSurface()
    {
        var profile = CreatureRoleBehaviorTestData.Complete();
        var service = new CreatureRoleBehaviorScriptService(profile, CreatureRoleBehaviorTestData.Progression());

        var snapshot = service.Resolve(service.Handle, 22 * 60, false);

        Assert.Equal(profile.Id, snapshot.Handle.Id);
        Assert.Equal("deity.embers", snapshot.LoreFields["deity"]);
        Assert.Equal("idle.night", snapshot.ActiveIdleSetId);
        Assert.Equal("campfire", snapshot.WaypointId);
        Assert.False(snapshot.AutoLevelLocked);
        Assert.True(snapshot.AutoLevelChangeCount > 0);
    }

    [Fact]
    public void UnknownHandleAndInvalidReferencesAreRejectedWithStableCodes()
    {
        var service = new CreatureRoleBehaviorScriptService(CreatureRoleBehaviorTestData.Complete(),
            CreatureRoleBehaviorTestData.Progression());
        var exception = Assert.Throws<KeyNotFoundException>(() => service.Resolve(
            new CreatureRoleBehaviorHandle(Guid.Parse("a1420000-0000-4000-8000-000000000099"), 1), 0, false));
        var invalid = Assert.Throws<ArgumentException>(() => new CreatureRoleBehaviorScriptService(
            CreatureRoleBehaviorTestData.Complete(), null));

        Assert.Contains("CREATURE_ROLE_BEHAVIOR_HANDLE_INVALID", exception.Message, StringComparison.Ordinal);
        Assert.Contains("creature.autolevel.progression_missing", invalid.Message, StringComparison.Ordinal);
    }
}
