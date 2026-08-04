using Forge.LiveDirector.World;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Tests;

public sealed class LiveWorldActionTests
{
    private static readonly Guid ActorId = Guid.Parse("b0000000-0000-4000-8000-000000000001");
    private static readonly Guid ZoneA = Guid.Parse("b0000000-0000-4000-8000-000000000002");
    private static readonly Guid ZoneB = Guid.Parse("b0000000-0000-4000-8000-000000000003");
    private static readonly Guid TargetId = Guid.Parse("b0000000-0000-4000-8000-000000000004");
    private static readonly Guid DestinationId = Guid.Parse("b0000000-0000-4000-8000-000000000005");

    [Fact]
    public void Focus_enforces_privacy_and_does_not_mutate_world()
    {
        var (service, chooser, gm) = Create(privateTarget: true);
        var denied = service.Execute(gm, Request("director.focus", 0, new Dictionary<string, string>()), true);
        Assert.Equal("LIVE_FOCUS_PRIVACY_DENIED", denied.Code);

        var applied = service.Execute(gm, Request("director.focus", 0, new Dictionary<string, string> { ["privacyOverride"] = "approved" }), true);
        Assert.True(applied.Applied);
        Assert.Equal("active", chooser.Find(TargetId)!.State);
        Assert.Contains("director.focus", applied.AuditEntry);
    }

    [Fact]
    public void Relocate_requires_safe_navigation_and_player_consent_then_moves_atomically()
    {
        var (service, chooser, gm) = Create(playerTarget: true);
        Assert.Equal("LIVE_RELOCATE_NAVIGATION_UNSAFE", service.Execute(gm, Request("world.relocate", 0, new Dictionary<string, string>(), DestinationId), true).Code);
        Assert.Equal("LIVE_RELOCATE_CONSENT_REQUIRED", service.Execute(gm, Request("world.relocate", 0, new Dictionary<string, string> { ["navigationSafe"] = "true" }, DestinationId), true).Code);

        var request = Request("world.relocate", 0, new Dictionary<string, string> { ["navigationSafe"] = "true", ["consent"] = "granted", ["partyPolicy"] = "target-only" }, DestinationId);
        Assert.Equal("LIVE_ACTION_PREVIEW", service.Execute(gm, request, false).Code);
        var result = service.Execute(gm, request, true);

        Assert.True(result.Applied);
        Assert.Equal(ZoneB, chooser.Find(TargetId)!.ZoneId);
        Assert.Equal((10d, 2d, 30d), (result.Target!.X, result.Target.Y, result.Target.Z));
    }

    [Fact]
    public void Heal_and_freeze_validate_scope_reason_and_preserve_configurable_state()
    {
        var (service, chooser, gm) = Create();
        Assert.Equal("LIVE_HEAL_REASON_REQUIRED", service.Execute(gm, Request("world.heal", 0, new Dictionary<string, string> { ["scope"] = "all" }), true).Code);
        var healed = service.Execute(gm, Request("world.heal", 0, new Dictionary<string, string> { ["scope"] = "all", ["reason"] = "encounter recovery" }), true);
        Assert.Equal("restored:all", healed.Target!.State);

        var frozen = service.Execute(gm, Request("world.freeze", 1, new Dictionary<string, string> { ["ai"] = "suspended", ["movement"] = "blocked", ["collision"] = "preserved", ["visibility"] = "hidden" }), true);
        Assert.Contains("ai:suspended", frozen.Target!.State);
        Assert.Contains("collision:preserved", chooser.Find(TargetId)!.State);
    }

    [Fact]
    public void Destroy_requires_confirmation_cause_and_loot_policy_then_removes_target()
    {
        var (service, chooser, gm) = Create();
        Assert.Equal("LIVE_DESTROY_CONFIRMATION_REQUIRED", service.Execute(gm, Request("world.destroy", 0, new Dictionary<string, string> { ["cause"] = "GM ruling" }), true).Code);
        var result = service.Execute(gm, Request("world.destroy", 0, new Dictionary<string, string> { ["cause"] = "GM ruling", ["confirmed"] = "true", ["lootPolicy"] = "preserve", ["questImpact"] = "recorded", ["rollback"] = "checkpoint-only" }), true);

        Assert.True(result.Applied);
        Assert.Null(result.Target);
        Assert.Null(chooser.Find(TargetId));
        Assert.Contains("lootPolicy=preserve", result.SemanticDiff);
    }

    private static (LiveWorldActionService Service, LiveWorldChooser Chooser, AuthorizationSubject Gm) Create(bool privateTarget = false, bool playerTarget = false)
    {
        var authorization = new AuthorizationService();
        var chooser = new LiveWorldChooser(authorization);
        chooser.Upsert(new(TargetId, ZoneA, "creature", "Elyra", "player", playerTarget ? ActorId : null, "active", [], "crypt", privateTarget, 1, 0, 1));
        chooser.Upsert(new(DestinationId, ZoneB, "waypoint", "Camp", "safe", null, "active", [], "surface", false, 10, 2, 30));
        return (new(chooser, authorization), chooser, new(ActorId, NetworkRole.Administrator, new HashSet<Guid> { ZoneA, ZoneB }));
    }

    private static LiveWorldActionService.Request Request(string kind, long revision, IReadOnlyDictionary<string, string> arguments, Guid? destination = null) =>
        new(Guid.NewGuid(), kind, TargetId, destination, revision, arguments);
}
