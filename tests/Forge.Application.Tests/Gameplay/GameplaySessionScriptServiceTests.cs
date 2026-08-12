using Forge.Application.Gameplay;
using Forge.Domain.Gameplay;
using Forge.Domain.Modules;

namespace Forge.Application.Tests.Gameplay;

public sealed class GameplaySessionScriptServiceTests
{
    [Fact]
    public void R067_typed_handles_read_shell_respawn_replay_and_reject_stale_or_unauthorized_access()
    {
        var moduleId = ModuleId.From(Guid.Parse("a0673000-0000-4000-8000-000000000001"));
        var profile = GameplaySessionProfile.CreateDefault(moduleId);
        var service = new GameplaySessionScriptService(moduleId, profile);
        var snapshot = service.Read(service.Handle);
        var shell = service.ReadShell(service.Shell(service.Handle));
        var checkpoint = Assert.Single(service.Checkpoints(service.Handle));
        var respawn = service.Respawn(checkpoint,
            new("hero", profile.Checkpoints[0].WaypointId, 0, true, ["hero"], true), true);
        var expected = GameplaySessionServices.HashState("x=1");
        var recording = GameplaySessionServices.Record(profile.Replay, Guid.NewGuid(),
            [new(1, "right", "move", 7, expected)], "script:r067");
        var replay = service.ReplayHeadless(service.Replay(service.Handle), recording, _ => "x=1", true);

        Assert.Equal(profile.Id, snapshot.Id);
        Assert.True(shell.InputAgnostic);
        Assert.True(respawn.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Throws<InvalidOperationException>(() => service.Read(service.Handle with { Generation = 2 }));
        Assert.Throws<InvalidOperationException>(() => service.ReplayHeadless(service.Replay(service.Handle),
            recording, _ => "x=1", false));
    }
}
