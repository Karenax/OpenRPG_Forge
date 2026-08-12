using Forge.Application.Gameplay;
using Forge.Domain.Gameplay;
using Forge.Domain.Modules;

namespace Forge.Application.Tests.Gameplay;

public sealed class GameplaySessionServicesTests
{
    private static readonly GameplaySessionProfile Profile = GameplaySessionProfile.CreateDefault(
        ModuleId.From(Guid.Parse("a0672000-0000-4000-8000-000000000001")));

    [Fact]
    public void R067_persistence_scopes_authority_conflicts_cloud_and_privacy_are_enforced()
    {
        foreach (var scope in Enum.GetValues<PersistenceScope>())
        {
            var policy = Profile.Persistence with
            {
                Scope = scope,
                Authority = SaveAuthority.Server,
                ConflictPolicy = SaveConflictPolicy.ServerWins,
            };
            var saved = GameplaySessionServices.WriteSave(policy,
                new("server", SaveAuthority.Server, true, true), $"owner:{scope}", 2,
                DateTimeOffset.UnixEpoch, new Dictionary<string, string> { ["quest"] = "active" });
            var stale = saved with { Revision = 1, SavedAt = saved.SavedAt.AddMinutes(1) };
            Assert.Equal(saved, GameplaySessionServices.ResolveSave(saved, stale));
        }
        var cloud = Profile.Persistence with { CloudAdapterEnabled = true, CloudAdapter = "test-cloud" };
        Assert.Throws<InvalidOperationException>(() => GameplaySessionServices.WriteSave(cloud,
            new("host", SaveAuthority.Host, false, true), "game", 1, DateTimeOffset.UnixEpoch,
            new Dictionary<string, string>()));
        Assert.Throws<InvalidOperationException>(() => GameplaySessionServices.WriteSave(Profile.Persistence,
            new("host", SaveAuthority.Host, true, false), "game", 1, DateTimeOffset.UnixEpoch,
            new Dictionary<string, string>()));
    }

    [Fact]
    public void R067_checkpoints_resolve_cost_party_corpse_safe_spawn_hooks_and_authority()
    {
        var checkpoint = Profile.Checkpoints[0] with { CurrencyCost = 10 };
        var request = new RespawnRequest("hero", checkpoint.WaypointId, 20, true,
            ["hero", "companion"], true);
        var ready = GameplaySessionServices.Respawn(checkpoint, request, true);
        var unsafeSpawn = GameplaySessionServices.Respawn(checkpoint, request with { SafeSpawnAvailable = false }, true);
        var denied = GameplaySessionServices.Respawn(checkpoint, request, false);

        Assert.True(ready.Succeeded);
        Assert.Equal(10, ready.RemainingCurrency);
        Assert.Equal(["companion", "hero"], ready.RespawnActorIds);
        Assert.Equal(CorpsePolicy.TransferInventory, ready.CorpsePolicy);
        Assert.Equal([checkpoint.BeforeRespawnEvent, checkpoint.AfterRespawnEvent], ready.Events);
        Assert.Equal("GAME_RESPAWN_SAFE_SPAWN_REQUIRED", unsafeSpawn.Code);
        Assert.Equal("GAME_RESPAWN_AUTHORITY_REQUIRED", denied.Code);
    }

    [Fact]
    public void R067_runtime_shell_is_kit_driven_scalable_and_input_agnostic()
    {
        var shell = GameplaySessionServices.ResolveShell(Profile.Shell);
        Assert.Equal("core-rpg", shell.KitId);
        Assert.Equal(["map", "journal", "hud"], shell.VisiblePanels);
        Assert.Contains("inventory", shell.InputActions);
        Assert.True(shell.InputAgnostic);
    }

    [Fact]
    public void R067_control_stack_owns_cursor_camera_inputs_and_restores_previous_context()
    {
        var exploration = Profile.ControlLocks.Contexts.Single(item => item.OwnerId == "exploration");
        var dialogue = Profile.ControlLocks.Contexts.Single(item => item.OwnerId == "dialogue");
        var cinematic = Profile.ControlLocks.Contexts.Single(item => item.OwnerId == "cinematic");
        var stack = GameplaySessionServices.PushControl(
            GameplaySessionServices.PushControl(
                GameplaySessionServices.PushControl([], exploration), dialogue), cinematic);
        var releasedCinematic = GameplaySessionServices.ReleaseOwner(stack, "cinematic");
        var releasedDialogue = GameplaySessionServices.ReleaseOwner(releasedCinematic.Stack, "dialogue");

        Assert.True(stack[^1].AllowedInputActions.SetEquals(["cinematic.skip"]));
        Assert.Equal("dialogue", releasedCinematic.Restored!.OwnerId);
        Assert.Equal("exploration", releasedDialogue.Restored!.OwnerId);
        Assert.False(releasedDialogue.Restored.CursorVisible);
        Assert.Equal("orbit", releasedDialogue.Restored.CameraMode);
    }

    [Fact]
    public void R067_replay_records_headless_reports_divergence_and_minimal_redacted_package()
    {
        var expected = GameplaySessionServices.HashState("player:x=1");
        var recording = GameplaySessionServices.Record(Profile.Replay,
            Guid.Parse("a0672000-0000-4000-8000-000000000010"),
            [new ReplayFrame(1, "right", "moved", 42, expected)], "qa:r067");
        var pass = GameplaySessionServices.ReplayHeadless(Profile.Replay, recording, _ => "player:x=1");
        var divergence = GameplaySessionServices.ReplayHeadless(Profile.Replay, recording, _ => "player:x=2");

        Assert.True(pass.Succeeded);
        Assert.False(divergence.Succeeded);
        Assert.Equal(1, divergence.Divergence!.Tick);
        Assert.Contains("privacy=redacted", divergence.MinimalPackageManifest);
        Assert.Single(divergence.Divergence.MinimalReproduction.Frames);
    }

    [Fact]
    public void R067_legacy_invalid_checkpoint_still_returns_stable_code()
    {
        var error = Assert.Throws<InvalidOperationException>(() => GameplaySessionServices.ValidateCheckpoint(
            new CheckpointProfile(Guid.Empty, 1, "", "", -1, PartyRespawnPolicy.Individual,
                CorpsePolicy.Remove, -1)));
        Assert.Equal("GAME_UUID_REQUIRED", error.Message);
    }
}
