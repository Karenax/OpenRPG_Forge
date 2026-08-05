using Forge.Application.Gameplay;
using Forge.Domain.Gameplay;
using Forge.Domain.Modules;

namespace Forge.Application.Tests.Gameplay;

public sealed class GameplayControlRuntimeTests
{
    private static readonly ModuleGameplayControlProfile Profile =
        ModuleGameplayControlProfile.CreateDefault(ModuleId.From(Guid.Parse("40000000-0000-4000-8000-000000000001")));

    [Fact]
    public void Movement_resolves_swim_dive_climb_and_resource_recovery()
    {
        var swim = MovementCapabilityRuntime.Resolve(Profile,
            new(true, false, false, false, 20, 100), 1);
        Assert.Equal(MovementCapabilityKind.Swimming, swim.Kind);
        Assert.True(swim.VerticalAcceleration > 0);

        var dive = MovementCapabilityRuntime.Resolve(Profile,
            new(true, true, false, false, 20, 100), 1);
        Assert.Equal(MovementCapabilityKind.Diving, dive.Kind);
        Assert.Equal(19, dive.BreathRemaining);

        var climb = MovementCapabilityRuntime.Resolve(Profile,
            new(false, false, true, true, 45, 100), 1);
        Assert.Equal(MovementCapabilityKind.Climbing, climb.Kind);
        Assert.Equal(88, climb.StaminaRemaining);

        var ground = MovementCapabilityRuntime.Resolve(Profile,
            new(false, false, false, false, 20, 50), 1);
        Assert.Equal(MovementCapabilityKind.Ground, ground.Kind);
        Assert.True(ground.BreathRemaining > 20);
        Assert.True(ground.StaminaRemaining > 50);
    }

    [Fact]
    public void Radial_menu_supports_nesting_and_accessible_labels()
    {
        var root = RadialActionResolver.Open(Profile.RadialMenu);
        Assert.Contains(root.Actions, action => action.Key == "combat");
        Assert.Contains("Interagir avec la cible", root.AccessibleSummary);
        var combat = RadialActionResolver.Open(Profile.RadialMenu, "combat");
        Assert.True(combat.CanNavigateBack);
        Assert.Single(combat.Actions);
        Assert.Equal("attack", combat.Actions[0].Command);
    }

    [Fact]
    public void Tactical_pause_vote_and_planning_queue_are_deterministic()
    {
        var policy = Profile.TimeControl with { };
        policy = new TimeControlPolicy(policy.Id, 1, true, true, .25f, true, 2, true,
            MultiplayerPausePolicy.Vote, .5f, ["player"], "test");
        var pending = TimeControlCoordinator.Request(policy, TimeControlSession.Running,
            RuntimeTimeState.Planning, "player:one", true, false, 4);
        Assert.False(pending.Accepted);
        Assert.Equal("time.vote_pending", pending.Code);
        var accepted = TimeControlCoordinator.Request(policy, pending.Session,
            RuntimeTimeState.Planning, "player:two", true, false, 4);
        Assert.True(accepted.Accepted);
        Assert.Equal(RuntimeTimeState.Planning, accepted.Session.State);
        var queued = TimeControlCoordinator.Queue(policy, accepted.Session,
            new(Guid.NewGuid(), "player:one", "attack", "enemy:one", 2));
        queued = TimeControlCoordinator.Queue(policy, queued.Session,
            new(Guid.NewGuid(), "player:two", "interact", "door:one", 1));
        Assert.True(queued.Accepted);
        Assert.Equal("interact", queued.Session.PlannedActions[0].Command);
        Assert.False(TimeControlCoordinator.Queue(policy, queued.Session,
            new(Guid.NewGuid(), "player:one", "rest", string.Empty, 3)).Accepted);
    }
}
