using Forge.Domain.Spatial;
using Forge.RpgKit.Domain.Creatures;

namespace Forge.RpgKit.Domain.Tests.Creatures;

public sealed class CreatureAiServiceTests
{
    [Fact]
    public void AbilityAndBlockedMovementEmitTypedResults()
    {
        var targeted = new AbilityTargeted(Guid.NewGuid(), Guid.NewGuid(), "ilyne", "lantern-flare", new HashSet<string> { "radiant" }, "entity");
        var impacted = CreatureAiService.Impact(targeted, "stunned", 4, Guid.NewGuid);
        Assert.Equal(targeted.Tags, impacted.Tags); Assert.Equal("stunned", impacted.Result);
        var blocked = CreatureAiService.Blocked(targeted.CreatureId, "fallen-tree", "route", "dynamic-obstacle", Vector3Value.Zero, Guid.NewGuid);
        Assert.Equal(MovementRecoveryStrategy.Avoid, blocked.Recovery); Assert.Equal("fallen-tree", blocked.ObstacleId);
    }

    [Theory]
    [InlineData(AiControllerKind.StateMachine)]
    [InlineData(AiControllerKind.BehaviorTree)]
    [InlineData(AiControllerKind.Utility)]
    [InlineData(AiControllerKind.Script)]
    public void ControllersAreInterchangeableAndBlackboardInspectable(AiControllerKind kind)
    {
        var profile = new AiControllerProfile(Guid.NewGuid(), 1, "author:sample", kind, kind == AiControllerKind.Script ? "script.guard" : "ai.guard",
            new Dictionary<string, AiBlackboardValue> { ["state"] = new("string", "patrol", true), ["target"] = new("entity", "player", false) }, ["patrol", "attack"]);
        Assert.Same(profile, CreatureAiService.Validate(profile));
        var decision = CreatureAiService.Decide(profile, new Dictionary<string, double> { ["patrol"] = .2, ["attack"] = .9 });
        Assert.Equal(2, decision.Blackboard.Count);
        if (kind == AiControllerKind.Utility) Assert.Equal("attack", decision.Action);
        if (kind == AiControllerKind.StateMachine) Assert.Equal("patrol", decision.Action);
        if (kind == AiControllerKind.Script) Assert.Equal("script.guard", decision.Action);
    }
}
