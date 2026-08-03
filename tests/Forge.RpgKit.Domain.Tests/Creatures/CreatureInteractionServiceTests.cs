using Forge.Domain.Spatial;
using Forge.RpgKit.Domain.Creatures;

namespace Forge.RpgKit.Domain.Tests.Creatures;

public sealed class CreatureInteractionServiceTests
{
    [Fact]
    public void DialogueSelectionUsesContextStatePriorityFallbackAndProtection()
    {
        var bindings = new[] { new CreatureDialogueBinding("greeting", 10, "ilyne.loyal", null, "loyal", false), new("greeting", 1, "ilyne.default", null, null, true) };
        var policy = new CreatureProtectionPolicy(false, true, false, new HashSet<string> { "inspect" });
        var accepted = CreatureInteractionService.RequestDialogue(Guid.NewGuid(), "player", "greeting", "loyal", bindings, policy, Guid.NewGuid);
        Assert.True(accepted.Accepted); Assert.Equal("ilyne.loyal", accepted.Starting!.DialogueId);
        Assert.Equal("RPG_CREATURE_DIALOGUE_PROTECTED", CreatureInteractionService.RequestDialogue(Guid.NewGuid(), "player", "interrupt", null, bindings, policy, Guid.NewGuid).RefusalCode);
    }

    [Fact]
    public void PerceptionDiffAndAttackLifecycleAreTypedAndDeterministic()
    {
        var creature = Guid.NewGuid(); var pos = new Vector3Value(2, 0, 0);
        var previous = new[] { new PerceptionResult("old", SenseKind.Hearing, .4, pos, 10) };
        var current = new[] { new PerceptionResult("new", SenseKind.Vision, .8, pos, 20) };
        var diff = CreatureInteractionService.DiffPerception(creature, previous, current, Guid.NewGuid);
        Assert.Equal("new", Assert.Single(diff.Detected).StimulusId); Assert.Equal("old", Assert.Single(diff.Lost).StimulusId);
        var network = new AttackNetworkContext("server", 42, 3, false);
        var declared = new AttackDeclared(Guid.NewGuid(), creature, "ilyne", "lantern", "sentinel", network);
        var resolved = CreatureInteractionService.ResolveAttack(declared, "hit", 7, Guid.NewGuid);
        Assert.Equal(declared.Network, resolved.Network); Assert.Equal(7, resolved.AppliedDamage);
    }
}
