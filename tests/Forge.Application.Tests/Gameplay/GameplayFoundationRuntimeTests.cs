using Forge.Application.Gameplay;
using Forge.Domain.Gameplay;
using Forge.Domain.Modules;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Gameplay;

public sealed class GameplayFoundationRuntimeTests
{
    private static readonly GameplayFoundationProfile Profile = GameplayFoundationProfile.CreateDefault(
        ModuleId.From(Guid.Parse("c7100000-0000-4000-8000-000000000200")));

    [Fact]
    public void Grounded_step_accelerates_jumps_and_supports_root_motion()
    {
        var state = new GroundedMovementState(Vector3Value.Zero, true, false);
        var frame = GameplayFoundationRuntime.Step(Profile.GroundedMovement, state,
            new(new(1, 0, 0), true, true, false), .1f);
        var root = GameplayFoundationRuntime.Step(Profile.GroundedMovement with { RootMotion = true }, state,
            new(new(1, 0, 0), false, false, false, new(.5f, 0, 0)), .1f);

        Assert.True(frame.Jumped);
        Assert.True(frame.Predicted);
        Assert.InRange(frame.State.Velocity.X, 2.39f, 2.41f);
        Assert.Equal(Profile.GroundedMovement.JumpVelocity, frame.State.Velocity.Y);
        Assert.True(root.UsedRootMotion);
        Assert.Equal(5, root.State.Velocity.X);
    }

    [Fact]
    public void Interaction_focus_filters_cone_tags_verbs_and_hold_progress()
    {
        var target = new InteractionCandidate(Guid.Parse("c7100000-0000-4000-8000-000000000201"),
            new(0, 0, -2), new HashSet<string>(["world"]), new HashSet<string>(["inspect"]));

        var pending = GameplayFoundationRuntime.ResolveFocus(Profile.Interaction, Vector3Value.Zero,
            new(0, 0, -1), [target], "inspect", .1f);
        var ready = GameplayFoundationRuntime.ResolveFocus(Profile.Interaction, Vector3Value.Zero,
            new(0, 0, -1), [target], "inspect", .4f);

        Assert.NotNull(pending);
        Assert.False(pending!.Ready);
        Assert.True(ready!.Ready);
        Assert.Equal(target.Id, ready.Candidate.Id);
        Assert.Null(GameplayFoundationRuntime.ResolveFocus(Profile.Interaction, Vector3Value.Zero,
            new(0, 0, 1), [target], "inspect", 1));
    }

    [Fact]
    public void Camera_input_rebind_autosave_and_script_handles_are_typed()
    {
        var camera = GameplayFoundationRuntime.ResolveCamera(Profile.Cameras);
        var input = GameplayFoundationRuntime.ResolveInput(Profile.Input, "interact", RuntimeInputDevice.Gamepad,
            new HashSet<string>(["exploration"]));
        var rebound = GameplayFoundationRuntime.Rebind(Profile.Input, "interact", RuntimeInputDevice.KeyboardMouse,
            "F", "key-f", "test:r010");
        var scripts = new GameplayFoundationScriptService(new ModuleGameplayControlProfile(
            Guid.Parse("c7100000-0000-4000-8000-000000000200"), 1,
            ModuleGameplayControlProfile.CreateDefault(ModuleId.From(Guid.Parse("c7100000-0000-4000-8000-000000000200"))).Swimming,
            ModuleGameplayControlProfile.CreateDefault(ModuleId.From(Guid.Parse("c7100000-0000-4000-8000-000000000200"))).Climbing,
            ModuleGameplayControlProfile.CreateDefault(ModuleId.From(Guid.Parse("c7100000-0000-4000-8000-000000000200"))).RadialMenu,
            ModuleGameplayControlProfile.CreateDefault(ModuleId.From(Guid.Parse("c7100000-0000-4000-8000-000000000200"))).TimeControl,
            ModuleGameplayControlProfile.CreateDefault(ModuleId.From(Guid.Parse("c7100000-0000-4000-8000-000000000200"))).CombatModes,
            "test:r010", Profile));

        Assert.Equal("orbit", camera.Key);
        Assert.Equal("A", input!.Binding.Binding);
        Assert.Equal("F", rebound.Bindings.Single(item => item.ActionKey == "interact" && item.Device == RuntimeInputDevice.KeyboardMouse).Binding);
        Assert.True(GameplayFoundationRuntime.ShouldAutosave(Profile.Save, null, DateTimeOffset.UnixEpoch));
        var handle = scripts.FindCamera("orbit");
        Assert.NotNull(handle);
        Assert.Equal("orbit", scripts.Read(handle!.Value).Key);
    }
}
