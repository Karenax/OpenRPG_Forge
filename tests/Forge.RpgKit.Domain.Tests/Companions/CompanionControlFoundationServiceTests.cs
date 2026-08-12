using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Application.Companions;
using Forge.RpgKit.Domain.Companions;

namespace Forge.RpgKit.Domain.Tests.Companions;

public sealed class CompanionControlFoundationServiceTests
{
    [Fact]
    public void R075Ownership_supports_five_modes_and_deterministic_preview_apply_transfer()
    {
        var profile = Profile();
        Assert.Equal(5, Enum.GetValues<CompanionOwnershipKind>().Length);
        Assert.Equal(CompanionOwnershipKind.Individual,
            CompanionControlFoundationService.CreateOwnership(profile).Kind);

        var controllers = new HashSet<string>(["party:one"], StringComparer.Ordinal);
        var preview = CompanionControlFoundationService.Transfer(profile, profile.Id, "party", controllers,
            "player:owner", "share party control", DateTimeOffset.UnixEpoch, true);
        var applied = CompanionControlFoundationService.Transfer(profile, profile.Id, "party", controllers,
            "player:owner", "share party control", DateTimeOffset.UnixEpoch, false);

        Assert.Equal(preview.PlanId, applied.PlanId);
        Assert.Equal(preview.TransactionId, applied.TransactionId);
        Assert.Equal(preview.SemanticDiff, applied.SemanticDiff);
        Assert.Equal(CompanionOwnershipKind.Party, applied.Current.Kind);
        Assert.Equal(2, applied.Current.Revision);
        Assert.Equal(applied.TransactionId, applied.Audit.EventId);
        Assert.True(CompanionControlFoundationService.Resolve(profile with
        { Control = null, ControlRequiredAtRuntime = false }).OptionalFallback);
        Assert.Equal("RPG_COMPANION_CONTROL_REQUIRED",
            Assert.Single(CompanionControlFoundationService.Resolve(profile with { Control = null }).Diagnostics)
                .Code);
    }

    [Fact]
    public void R075Movement_and_tactical_orders_share_authority_profiles_and_priority_queue()
    {
        var profile = Profile();
        IReadOnlyList<object> queue = [];
        foreach (var kind in profile.Control!.Movement.CommandKinds)
        {
            var target = kind is "guard" or "interact" ? "target:one" : null;
            Vector3Value? destination = kind == "move" ? new Vector3Value(4, 0, 8) : null;
            var result = CompanionControlFoundationService.IssueMovement(profile, kind, "player:owner", target,
                destination, 10, queue);
            Assert.True(result.Accepted, kind);
            queue = result.Queue;
        }

        foreach (var kind in profile.Control.Tactical.CommandKinds)
        {
            var target = kind is "attack" or "focus-fire" or "protect" ? "enemy:one" : null;
            var ability = kind == "use-ability" ? "heal" : null;
            var result = CompanionControlFoundationService.IssueTactical(profile, kind, "player:owner", target,
                "balanced", ability, 20, queue);
            Assert.True(result.Accepted, kind);
            queue = result.Queue;
        }

        Assert.Equal(12, queue.Count);
        Assert.False(CompanionControlFoundationService.IssueMovement(profile, "move", "intruder", null,
            new Vector3Value(1, 0, 1), 10, []).Accepted);
        Assert.False(CompanionControlFoundationService.IssueTactical(profile, "use-ability", "player:owner", null,
            "balanced", "forbidden", 20, []).Accepted);
    }

    [Fact]
    public void R075Ai_and_equipment_are_bounded_permissioned_lockable_comparable_and_owned()
    {
        var profile = Profile();
        var ai = CompanionControlFoundationService.CreateAi(profile);
        Assert.Empty(CompanionControlService.ValidateAi(ai));
        Assert.False(ai.AllowFriendlyFire);
        Assert.Equal("Support", ai.Role.ToString());

        var equipment = profile.Control!.Equipment;
        var suggested = equipment.SuggestedLoadout["main-hand"];
        var equipped = CompanionControlFoundationService.Equip(profile, new Dictionary<string, Guid>(), suggested,
            "main-hand", "player:owner");
        Assert.True(equipped.Accepted);
        Assert.Contains("replace:/equipment/main-hand", equipped.SemanticDiff);
        Assert.False(CompanionControlFoundationService.Equip(profile, new Dictionary<string, Guid>(),
            equipment.LockedItemIds[0], "main-hand", "player:owner").Accepted);
        Assert.False(CompanionControlFoundationService.Equip(profile, new Dictionary<string, Guid>(), suggested,
            "main-hand", "intruder").Accepted);
        Assert.True(CompanionControlFoundationService.CreateEquipment(profile).AllowCompare);
    }

    internal static CompanionWorldProfile Profile()
    {
        var profile = CompanionFoundationServiceTests.Profile();
        return profile with
        {
            Control = new(Guid.Parse("75000000-0000-4000-8000-000000000001"), 1, "test:r075",
                new(Guid.Parse("75000000-0000-4000-8000-000000000002"), 1, "test:r075", "individual",
                    ["player:owner"], true, ["party", "shared", "game-master"], 1),
                new(Guid.Parse("75000000-0000-4000-8000-000000000003"), 1, "test:r075",
                    ["move", "follow", "hold", "rally", "guard", "interact"], "wedge", "party-waypoints",
                    0, 100),
                new(Guid.Parse("75000000-0000-4000-8000-000000000004"), 1, "test:r075",
                    ["attack", "defend", "focus-fire", "protect", "disengage", "use-ability"],
                    ["passive", "defensive", "balanced", "aggressive"], ["heal", "shield"], 0, 100),
                new(Guid.Parse("75000000-0000-4000-8000-000000000005"), 1, "test:r075", .6, .4, .5, false,
                    "Support", 3, ["heal", "shield"],
                    Guid.Parse("75000000-0000-4000-8000-000000000006"),
                    Guid.Parse("75000000-0000-4000-8000-000000000007"),
                    "replace:/companion/control/ai|role:Support", "owner-or-gm"),
                new(Guid.Parse("75000000-0000-4000-8000-000000000008"), 1, "test:r075", "full",
                    [Guid.Parse("75000000-0000-4000-8000-000000000009")],
                    new Dictionary<string, Guid>
                    {
                        ["main-hand"] = Guid.Parse("75000000-0000-4000-8000-000000000010"),
                    }, true, "player:owner")),
            ControlRequiredAtRuntime = true,
        };
    }
}
