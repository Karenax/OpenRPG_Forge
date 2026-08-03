using Forge.Application.Gameplay;
using Forge.Domain.Gameplay;

namespace Forge.Application.Tests.Gameplay;

public sealed class GameplaySessionServicesTests
{
    [Fact]
    public void SessionPolicies_AreValidatedResolvedRestoredAndReplayable()
    {
        var persistence = new PersistenceProfile(Guid.NewGuid(), 1, "author:test", PersistenceScope.Server, SaveAuthority.Server, SaveConflictPolicy.ServerWins, false, true);
        var current = new SaveEnvelope(persistence, "world-1", 2, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), new Dictionary<string, string> { ["quest"] = "active" });
        var stale = current with { Revision = 1, SavedAt = current.SavedAt.AddMinutes(1), Values = new Dictionary<string, string> { ["quest"] = "done" } };
        Assert.Same(current, GameplaySessionServices.ResolveSave(current, stale));

        var checkpoint = new CheckpointProfile(Guid.NewGuid(), 1, "author:test", "waypoint.safe", 10, PartyRespawnPolicy.EntireParty, CorpsePolicy.TransferInventory, 3);
        Assert.Equal("waypoint.safe", GameplaySessionServices.ValidateCheckpoint(checkpoint).WaypointId);
        var shell = new RuntimeShellProfile(Guid.NewGuid(), 1, "author:test", "aurora-dark", 1.25, true, true, true, ["map", "journal"]);
        Assert.Equal(1.25, GameplaySessionServices.ValidateShell(shell).Scale);

        var exploration = new ControlContext(Guid.NewGuid(), 1, "runtime", "exploration", false, "follow", new HashSet<string> { "move" });
        var cinematic = new ControlContext(Guid.NewGuid(), 1, "runtime", "cinematic", true, "rail", new HashSet<string> { "skip" });
        var stack = GameplaySessionServices.PushControl(GameplaySessionServices.PushControl([], exploration), cinematic);
        var restored = GameplaySessionServices.ReleaseOwner(stack, "cinematic");
        Assert.Equal("exploration", restored.Restored!.OwnerId);

        var expected = GameplaySessionServices.HashState("player:x=1");
        var recording = new ReplayRecording(Guid.NewGuid(), 1, "qa:test", [new ReplayFrame(1, "right", "moved", 42, expected)]);
        Assert.Null(GameplaySessionServices.Replay(recording, _ => "player:x=1"));
        var divergence = GameplaySessionServices.Replay(recording, _ => "player:x=2");
        Assert.Equal(1, divergence!.Tick);
        Assert.Single(divergence.MinimalReproduction.Frames);
    }

    [Fact]
    public void InvalidCanonicalProfiles_ReturnStableCodes()
    {
        var error = Assert.Throws<InvalidOperationException>(() => GameplaySessionServices.ValidateCheckpoint(
            new CheckpointProfile(Guid.Empty, 1, "", "", -1, PartyRespawnPolicy.Individual, CorpsePolicy.Remove, -1)));
        Assert.Equal("GAME_UUID_REQUIRED", error.Message);
    }
}
