using Forge.LiveDirector.Players;
using Forge.LiveDirector.Scripting;
using Forge.LiveDirector.Search;
using Forge.LiveDirector.World;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Tests;

public sealed class LiveDirectorOperationsTests
{
    private static readonly Guid Actor = Guid.Parse("e0000000-0000-4000-8000-000000000001");
    private static readonly Guid Zone = Guid.Parse("e0000000-0000-4000-8000-000000000002");
    private static readonly Guid GmObject = Guid.Parse("e0000000-0000-4000-8000-000000000003");
    private static readonly Guid Player = Guid.Parse("e0000000-0000-4000-8000-000000000004");
    private static readonly Guid Character = Guid.Parse("e0000000-0000-4000-8000-000000000005");
    private static readonly Guid Enemy = Guid.Parse("e0000000-0000-4000-8000-000000000006");

    [Fact]
    public void Group_attack_requires_faction_warning_and_sets_authoritative_objective()
    {
        var (chooser, auth, gm) = World();
        var groups = new LiveGroupCommandService(chooser, auth, 8);
        var selection = groups.Select(gm, Guid.NewGuid(), new(Zone, "creature", "Héros", null, null));
        var command = new LiveGroupCommandService.Attack(Guid.NewGuid(), selection.SelectionId, Enemy, false, 0);
        Assert.Equal("LIVE_ATTACK_FACTION_WARNING_REQUIRED", groups.AttackTarget(gm, command, true).Code);
        Assert.Equal("LIVE_ATTACK_PREVIEW", groups.AttackTarget(gm, command with { FactionWarningAccepted = true }, false).Code);
        var result = groups.AttackTarget(gm, command with { FactionWarningAccepted = true }, true);
        Assert.True(result.Applied);
        Assert.Contains(Enemy.ToString(), chooser.Find(Character)!.State);
    }

    [Fact]
    public void Player_panel_and_confirmed_travel_expose_operational_state()
    {
        var (chooser, auth, gm) = World();
        var players = new LivePlayerService(chooser, auth);
        players.Upsert(new(Player, Character, "karen", "Héros", Zone, 42, null, "player", "connected", ["none"]));
        var panel = Assert.Single(players.List(gm));
        Assert.Equal(42, panel.LatencyMs);
        var command = new LivePlayerService.Travel(Guid.NewGuid(), "summon-player", Player, GmObject, false, 0);
        Assert.Equal("LIVE_PLAYER_TRAVEL_CONFIRMATION_REQUIRED", players.TravelToPlayer(gm, command, true).Code);
        var applied = players.TravelToPlayer(gm, command with { Confirmed = true }, true);
        Assert.True(applied.Applied);
        Assert.Equal("camera:fade-to-director", applied.CameraTransition);
        Assert.Equal("teleported", chooser.Find(Character)!.State);
    }

    [Fact]
    public void Indexed_search_cycles_matches_supports_saved_queries_and_deep_links()
    {
        var (chooser, _, gm) = World();
        var search = new LiveSearchService(chooser);
        var query = new LiveWorldChooser.Query(Zone, "creature", null, null, null);
        var queryId = Guid.NewGuid();
        search.Save(new(queryId, "Créatures", query));
        var first = search.NextSaved(gm, queryId);
        var second = search.NextSaved(gm, queryId);
        Assert.NotEqual(first.Current!.ObjectId, second.Current!.ObjectId);
        Assert.StartsWith("openrpg://live/zone/", first.DeepLink);
        Assert.StartsWith("viewport-focus:", first.VisualFocus);
    }

    [Fact]
    public void Script_runner_allows_only_registered_commands_or_signed_bounded_snippets()
    {
        var authorization = new AuthorizationService();
        var gm = new AuthorizationSubject(Actor, NetworkRole.Administrator, new HashSet<Guid>());
        var scripts = new LiveScriptService(authorization, value => value.EndsWith(":valid", StringComparison.Ordinal));
        scripts.Register(new("announce", args => new Dictionary<string, string> { ["message"] = args["message"] }));
        var command = new LiveScriptService.Request(Guid.NewGuid(), "registered-command", "announce", null, 100, 0, new Dictionary<string, string> { ["message"] = "Bienvenue" });
        var preview = scripts.Run(gm, command, false);
        Assert.Equal("LIVE_SCRIPT_PREVIEW", preview.Code);
        Assert.StartsWith("forgescript:server:", preview.ServerAdapter);
        Assert.Equal("Bienvenue", scripts.Run(gm, command, true).Output["message"]);
        Assert.Equal("LIVE_SCRIPT_FORBIDDEN_API", scripts.Run(gm, new(Guid.NewGuid(), "signed-snippet", "System.IO.File.Delete", "valid", 10, 1, new Dictionary<string, string>()), true).Code);
        Assert.Equal("LIVE_SCRIPT_SIGNATURE_INVALID", scripts.Run(gm, new(Guid.NewGuid(), "signed-snippet", "SetWeather()", "bad", 10, 1, new Dictionary<string, string>()), true).Code);
    }

    private static (LiveWorldChooser Chooser, AuthorizationService Authorization, AuthorizationSubject Gm) World()
    {
        var auth = new AuthorizationService();
        var chooser = new LiveWorldChooser(auth);
        chooser.Upsert(new(GmObject, Zone, "director", "GM", "gm", null, "active", [], "main", false, 0, 0, 0));
        chooser.Upsert(new(Character, Zone, "creature", "Héros", "player", Player, "active", [], "main", false, 5, 0, 5));
        chooser.Upsert(new(Enemy, Zone, "creature", "Adversaire", "enemy", null, "active", [], "main", false, 9, 0, 9));
        return (chooser, auth, new(Actor, NetworkRole.Administrator, new HashSet<Guid> { Zone }));
    }
}
