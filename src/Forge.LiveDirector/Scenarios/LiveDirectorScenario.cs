using Forge.LiveDirector.Audit;
using Forge.LiveDirector.Catalog;
using Forge.LiveDirector.Narrative;
using Forge.LiveDirector.World;
using Forge.Network.Operations;
using Forge.Network.Scenarios;

namespace Forge.LiveDirector.Scenarios;

public static class LiveDirectorScenario
{
    public sealed record Result(bool Passed, string NetworkDigest, string SpawnCode, string QuestCode, string RejectedPlayerCode, int AuditEntries, string AuditHash);

    public static Result Run()
    {
        var network = NetworkBetaScenario.Run();
        var authorization = new AuthorizationService();
        var gm = new AuthorizationSubject(Guid.Parse("ab000000-0000-4000-8000-000000000001"), NetworkRole.Administrator, new HashSet<Guid>());
        var player = new AuthorizationSubject(Guid.Parse("ab000000-0000-4000-8000-000000000002"), NetworkRole.Player, new HashSet<Guid>());
        var zone = Guid.Parse("ab000000-0000-4000-8000-000000000003");
        var blueprint = Guid.Parse("ab000000-0000-4000-8000-000000000004");
        var authorizationWithZone = gm with { ScopedZoneIds = new HashSet<Guid> { zone } };
        var chooser = new LiveWorldChooser(authorization);
        var catalog = new LiveCatalogService(authorization);
        catalog.Replace([new(blueprint, "Gardien live", "creature", "crypt", 10, new HashSet<Guid> { zone }, true)]);
        var nextId = Guid.Parse("ab000000-0000-4000-8000-000000000100");
        var spawn = new LiveSpawnService(catalog, chooser, () => nextId);
        var spawnRequest = new LiveSpawnService.Request(Guid.Parse("ab000000-0000-4000-8000-000000000005"), blueprint, zone, 1, "single", 0);
        var spawnResult = spawn.Execute(authorizationWithZone, spawnRequest, true);
        var rejected = spawn.Execute(player, spawnRequest with { CommandId = Guid.Parse("ab000000-0000-4000-8000-000000000006"), ExpectedRevision = 1 }, true);

        var audit = new LiveAuditJournal(() => new DateTimeOffset(2026, 8, 4, 15, 0, 0, TimeSpan.Zero));
        var quests = new LiveQuestService(authorization, audit);
        var questId = Guid.Parse("ab000000-0000-4000-8000-000000000007");
        quests.Upsert(new(questId, "Cendres", "started", new Dictionary<string, IReadOnlySet<string>> { ["started"] = new HashSet<string> { "completed" } }));
        var questResult = quests.Transition(authorizationWithZone, new(Guid.Parse("ab000000-0000-4000-8000-000000000008"), questId, "completed", "all", [], "session finale", 0), true);
        var hash = audit.Entries.LastOrDefault()?.Hash ?? "none";
        var passed = network.Passed && spawnResult.Applied && questResult.Applied && rejected.Code == "LIVE_SPAWN_BLUEPRINT_DENIED" && audit.Verify();
        return new(passed, network.SharedDigest, spawnResult.Code, questResult.Code, rejected.Code, audit.Entries.Count, hash);
    }
}
