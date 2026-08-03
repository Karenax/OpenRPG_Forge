using Forge.RpgKit.Domain.Companions;
using Forge.RpgKit.Domain.Creatures;

namespace Forge.RpgKit.Domain.Tests.Companions;

public sealed class CompanionControlServiceTests
{
    [Fact]
    public void Party_policy_caps_companions_roles_summons_pets_allies_and_server_population()
    {
        var policy = new PartyPolicy(2, 1, 1, 1, 5, new Dictionary<EncounterRole, int> { [EncounterRole.Support] = 1 });
        Assert.Empty(CompanionControlService.ValidateParty(policy, new([Companion("one")], 1, 1, 1)));
        var diagnostics = CompanionControlService.ValidateParty(policy, new([Companion("one"), Companion("two"), Companion("three")], 1, 1, 1));
        Assert.Contains(diagnostics, item => item.Code == "RPG_PARTY_CAPACITY_EXCEEDED"); Assert.Contains(diagnostics, item => item.Code == "RPG_PARTY_ROLE_LIMIT_EXCEEDED");
    }

    [Fact]
    public void Ownership_supports_five_modes_and_audited_revisioned_transfer()
    {
        Assert.Equal(5, Enum.GetValues<CompanionOwnershipKind>().Length);
        var ownership = Ownership(); var at = DateTimeOffset.UnixEpoch;
        var transfer = CompanionControlService.Transfer(Guid.NewGuid(), ownership, CompanionOwnershipKind.Party, new HashSet<string> { "party:one" }, "player:one", "shared control", at);
        Assert.Equal(ownership, transfer.Previous); Assert.Equal(CompanionOwnershipKind.Party, transfer.Current.Kind); Assert.Equal(2, transfer.Current.Revision); Assert.Equal("shared control", transfer.Reason);
        Assert.Throws<InvalidOperationException>(() => CompanionControlService.Transfer(Guid.NewGuid(), ownership with { TransferAllowed = false }, CompanionOwnershipKind.Party, new HashSet<string> { "party" }, "actor", "reason", at));
    }

    [Fact]
    public void Authorized_controller_queues_movement_and_tactical_orders_by_priority()
    {
        var ownership = Ownership(); var ai = Ai();
        var move = new CompanionMovementOrder(Guid.NewGuid(), CompanionMovementOrderKind.Move, "player:one", null, new(4, 0, 8), "wedge", 2);
        var moved = CompanionControlService.IssueMovement(ownership, move, []); Assert.True(moved.Accepted); Assert.Same(move, moved.Queue.Single());
        var attack = new CompanionTacticalOrder(Guid.NewGuid(), CompanionTacticalOrderKind.FocusFire, "player:one", "enemy:one", CompanionStance.Aggressive, null, 5);
        var tactical = CompanionControlService.IssueTactical(ownership, attack, ai, moved.Queue); Assert.True(tactical.Accepted); Assert.Same(attack, tactical.Queue[0]);
        Assert.False(CompanionControlService.IssueMovement(ownership, move with { IssuerId = "intruder" }, []).Accepted);
        Assert.False(CompanionControlService.IssueTactical(ownership, attack with { Kind = CompanionTacticalOrderKind.UseAbility, AbilityId = "forbidden" }, ai, []).Accepted);
    }

    [Fact]
    public void Ai_profile_bounds_autonomy_risk_resources_friendly_fire_role_and_follow_distance()
    {
        Assert.Empty(CompanionControlService.ValidateAi(Ai()));
        Assert.Contains(CompanionControlService.ValidateAi(Ai() with { Autonomy = 2, RiskTolerance = -1, ResourceUsage = 4, FollowDistance = -2 }), item => item.Code == "RPG_COMPANION_AI_INVALID");
    }

    private static CompanionOwnership Ownership() => new(CompanionOwnershipKind.Individual, new HashSet<string> { "player:one" }, true, new HashSet<CompanionOwnershipKind> { CompanionOwnershipKind.Party, CompanionOwnershipKind.Shared }, 1);
    private static CompanionAiProfile Ai() => new(0.6, 0.4, 0.5, false, EncounterRole.Support, 3, new HashSet<string> { "heal", "ward" });
    private static CompanionComponent Companion(string suffix) => new(Guid.NewGuid(), 1, "author:test", Guid.NewGuid(), new(new HashSet<string>(), 0, "shard", 0, [], true, true), "player", CompanionPersistence.Campaign, $"inventory:{suffix}", Forge.Domain.Spatial.TransformValue.Identity, new HashSet<string> { "role:Support" }, new HashSet<string> { "not-recruited", "owner" }, "equipment", "death");
}
