using Forge.LiveDirector.Catalog;
using Forge.Network.Operations;

namespace Forge.LiveDirector.World;

public sealed class LiveSpawnService(LiveCatalogService catalog, LiveWorldChooser chooser, Func<Guid> objectIdFactory)
{
    public sealed record Request(Guid CommandId, Guid BlueprintId, Guid ZoneId, int Count, string Placement, long ExpectedRevision);
    public sealed record Result(bool Applied, string Code, long Revision, IReadOnlyList<Guid> ObjectIds, string SemanticDiff, string AuditEntry);

    private readonly HashSet<Guid> _processedCommands = [];
    private long _revision;

    public Result Execute(AuthorizationSubject actor, Request request, bool apply)
    {
        if (request.CommandId == Guid.Empty || request.Count is < 1 or > 64 || request.Placement is not ("single" or "formation" or "scatter"))
            return Reject("LIVE_SPAWN_REQUEST_INVALID", request);
        if (request.ExpectedRevision != _revision) return Reject("LIVE_SPAWN_REVISION_STALE", request);
        if (_processedCommands.Contains(request.CommandId)) return Reject("LIVE_SPAWN_COMMAND_REPLAYED", request);
        var blueprint = catalog.FindAllowed(actor, request.BlueprintId, request.ZoneId);
        if (blueprint is null) return Reject("LIVE_SPAWN_BLUEPRINT_DENIED", request);
        var diff = $"spawn:{blueprint.Name}:count={request.Count}:zone={request.ZoneId}:placement={request.Placement}";
        if (!apply) return new(false, "LIVE_SPAWN_PREVIEW", _revision, [], diff, $"previewed:{diff}:actor={actor.AccountId}");

        var ids = Enumerable.Range(0, request.Count).Select(_ => objectIdFactory()).ToArray();
        if (ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Length) throw new InvalidOperationException("LIVE_SPAWN_ID_FACTORY_INVALID");
        foreach (var id in ids)
            chooser.Upsert(new(id, request.ZoneId, blueprint.Kind, blueprint.Name, $"live-{blueprint.BlueprintId:N}", null, "spawned", []));
        _processedCommands.Add(request.CommandId);
        _revision++;
        return new(true, "LIVE_SPAWN_APPLIED", _revision, ids, diff, $"applied:{diff}:actor={actor.AccountId}:revision={_revision}");
    }

    private Result Reject(string code, Request request) => new(false, code, _revision, [], "no changes", $"rejected:{code}:command={request.CommandId}");
}
