using Forge.LiveDirector.World;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Tests;

public sealed class LiveDirectorRulesGroupTests
{
    private static readonly Guid Actor = Guid.Parse("d0000000-0000-4000-8000-000000000001");
    private static readonly Guid Zone = Guid.Parse("d0000000-0000-4000-8000-000000000002");
    private static readonly Guid Creature = Guid.Parse("d0000000-0000-4000-8000-000000000003");
    private static readonly Guid Door = Guid.Parse("d0000000-0000-4000-8000-000000000004");
    private static readonly Guid Trigger = Guid.Parse("d0000000-0000-4000-8000-000000000005");
    private static readonly Guid Destination = Guid.Parse("d0000000-0000-4000-8000-000000000006");

    [Fact]
    public void Xp_preview_reports_level_effect_and_apply_is_audited()
    {
        var (service, _, gm) = Rules();
        var request = new LiveProgressionWorldService.Request(Guid.NewGuid(), "progression.adjust", Creature, 0, new Dictionary<string, string> { ["amount"] = "2500", ["reason"] = "story milestone" });
        var preview = service.Execute(gm, request, false);
        Assert.Contains("level=1->3", preview.SemanticDiff);
        Assert.True(service.Execute(gm, request, true).Applied);
    }

    [Fact]
    public void Door_and_trigger_actions_validate_state_portal_actor_and_emit_script_trace()
    {
        var (service, chooser, gm) = Rules();
        var relink = new LiveProgressionWorldService.Request(Guid.NewGuid(), "door.set-state", Door, 0, new Dictionary<string, string> { ["state"] = "relinked", ["portal"] = "surface-exit" });
        Assert.True(service.Execute(gm, relink, true).Applied);
        Assert.Contains("surface-exit", chooser.Find(Door)!.State);
        var simulate = new LiveProgressionWorldService.Request(Guid.NewGuid(), "trigger.simulate", Trigger, 1, new Dictionary<string, string> { ["event"] = "enter", ["actor"] = Creature.ToString(), ["payload"] = "{quest:ashes}" });
        var result = service.Execute(gm, simulate, true);
        Assert.Equal(3, result.ScriptTrace.Count);
        Assert.Contains("trigger.simulate", result.AuditEntry);
    }

    [Fact]
    public void Multi_selection_is_capped_and_group_move_reports_partial_failure()
    {
        var authorization = new AuthorizationService();
        var chooser = new LiveWorldChooser(authorization);
        chooser.Upsert(new(Creature, Zone, "creature", "Mobile", "group", null, "active", []));
        var blocked = Guid.Parse("d0000000-0000-4000-8000-000000000007");
        chooser.Upsert(new(blocked, Zone, "creature", "Bloqué", "group", null, "movement:blocked", []));
        chooser.Upsert(new(Destination, Zone, "waypoint", "Destination", "move", null, "active", [], "main", false, 9, 0, 4));
        var gm = new AuthorizationSubject(Actor, NetworkRole.Administrator, new HashSet<Guid> { Zone });
        var groups = new LiveGroupCommandService(chooser, authorization, 8);
        var selection = groups.Select(gm, Guid.NewGuid(), new(Zone, "creature", null, null, null));
        Assert.Equal(2, selection.ObjectIds.Count);
        var command = new LiveGroupCommandService.Movement(Guid.NewGuid(), selection.SelectionId, Destination, "wedge", 0);
        Assert.Equal("LIVE_MOVEMENT_PREVIEW", groups.Move(gm, command, false).Code);
        var result = groups.Move(gm, command, true);
        Assert.Equal("LIVE_MOVEMENT_PARTIAL", result.Code);
        Assert.Single(result.Moved);
        Assert.Equal("LIVE_PATH_BLOCKED", result.Failed[blocked]);
    }

    private static (LiveProgressionWorldService Service, LiveWorldChooser Chooser, AuthorizationSubject Gm) Rules()
    {
        var authorization = new AuthorizationService();
        var chooser = new LiveWorldChooser(authorization);
        chooser.Upsert(new(Creature, Zone, "creature", "Gardien", "ember", null, "active", []));
        chooser.Upsert(new(Door, Zone, "door", "Porte", "portal", null, "door:locked", []));
        chooser.Upsert(new(Trigger, Zone, "trigger", "Seuil", "volume", null, "active", []));
        return (new(chooser, authorization, xp => (int)(xp / 1000) + 1), chooser, new(Actor, NetworkRole.Administrator, new HashSet<Guid> { Zone }));
    }
}
