using Forge.LiveDirector.Catalog;
using Forge.Network.Operations;

namespace Forge.LiveDirector.World;

public sealed class LiveSpawnService(LiveCatalogService catalog, LiveWorldChooser chooser,
    Func<Guid> objectIdFactory, int maximumInstances = 64)
{
    public sealed record Request(Guid CommandId, Guid BlueprintId, Guid ZoneId, int Count,
        string Placement, long ExpectedRevision, string InstanceId = "default", double X = 0,
        double Y = 0, double Z = 0, double Spacing = 1.5);
    public sealed record SpawnedObject(Guid ObjectId, string InstanceId, double X, double Y,
        double Z);
    public sealed record Result(bool Applied, string Code, long Revision,
        IReadOnlyList<Guid> ObjectIds, IReadOnlyList<SpawnedObject> Instances,
        string SemanticDiff, string AuditEntry);

    private readonly HashSet<Guid> _processedCommands = [];
    private readonly Dictionary<Guid, IReadOnlyList<SpawnedObject>> _previewPlans = [];
    private readonly Dictionary<Guid, AppliedSpawn> _appliedObjects = [];
    private readonly int _maximumInstances = maximumInstances is >= 1 and <= 1024
        ? maximumInstances
        : throw new ArgumentOutOfRangeException(nameof(maximumInstances),
            "LIVE_SPAWN_MAXIMUM_INVALID");
    private long _revision;

    public Result Execute(AuthorizationSubject actor, Request request, bool apply)
    {
        if (request.CommandId == Guid.Empty || request.BlueprintId == Guid.Empty ||
            request.ZoneId == Guid.Empty || request.Count < 1 || request.Count > _maximumInstances ||
            request.Placement is not ("single" or "formation" or "scatter") ||
            string.IsNullOrWhiteSpace(request.InstanceId) || request.InstanceId.Length > 128 ||
            !double.IsFinite(request.X) || !double.IsFinite(request.Y) ||
            !double.IsFinite(request.Z) || !double.IsFinite(request.Spacing) ||
            request.Spacing is <= 0 or > 1000)
            return Reject("LIVE_SPAWN_REQUEST_INVALID", request);
        if (request.ExpectedRevision != _revision) return Reject("LIVE_SPAWN_REVISION_STALE", request);
        if (_processedCommands.Contains(request.CommandId)) return Reject("LIVE_SPAWN_COMMAND_REPLAYED", request);
        var blueprint = catalog.FindAllowed(actor, request.BlueprintId, request.ZoneId);
        if (blueprint is null) return Reject("LIVE_SPAWN_BLUEPRINT_DENIED", request);
        var plan = GetOrCreatePlan(request);
        if (plan.Any(item => chooser.Find(item.ObjectId) is not null))
            return Reject("LIVE_SPAWN_ATOMIC_CONFLICT", request);
        var diff = $"spawn:{blueprint.Name}:count={request.Count}:zone={request.ZoneId}:instance={request.InstanceId}:placement={request.Placement}:origin={request.X:0.###},{request.Y:0.###},{request.Z:0.###}";
        if (!apply) return new(false, "LIVE_SPAWN_PREVIEW", _revision,
            plan.Select(item => item.ObjectId).ToArray(), plan, diff,
            $"previewed:{diff}:actor={actor.AccountId}");

        var entries = plan.Select(item => new LiveWorldChooser.Entry(item.ObjectId,
            request.ZoneId, blueprint.Kind, blueprint.Name, $"live-{blueprint.BlueprintId:N}",
            null, "spawned", [], item.InstanceId, false, item.X, item.Y, item.Z)).ToArray();
        foreach (var entry in entries) chooser.Upsert(entry);
        _processedCommands.Add(request.CommandId);
        _appliedObjects[request.CommandId] = new(request.BlueprintId, request.ZoneId,
            plan.Select(item => item.ObjectId).ToArray());
        _revision++;
        return new(true, "LIVE_SPAWN_APPLIED", _revision,
            plan.Select(item => item.ObjectId).ToArray(), plan, diff,
            $"applied:{diff}:actor={actor.AccountId}:revision={_revision}");
    }

    public Result Compensate(AuthorizationSubject actor, Guid commandId, long expectedRevision,
        bool apply)
    {
        if (commandId == Guid.Empty || expectedRevision != _revision ||
            !_appliedObjects.TryGetValue(commandId, out var applied))
            return Reject("LIVE_SPAWN_COMPENSATION_UNAVAILABLE",
                new(commandId, Guid.Empty, Guid.Empty, 1, "single", expectedRevision));
        if (catalog.FindAllowed(actor, applied.BlueprintId, applied.ZoneId) is null)
            return Reject("LIVE_SPAWN_COMPENSATION_DENIED",
                new(commandId, applied.BlueprintId, applied.ZoneId, 1, "single",
                    expectedRevision));
        var objectIds = applied.ObjectIds;
        var instances = objectIds.Select(id => chooser.Find(id))
            .Where(entry => entry is not null).Cast<LiveWorldChooser.Entry>()
            .Select(entry => new SpawnedObject(entry.ObjectId, entry.InstanceId, entry.X, entry.Y,
                entry.Z)).ToArray();
        var diff = $"spawn.compensate:command={commandId}:objects={objectIds.Count}";
        if (!apply) return new(false, "LIVE_SPAWN_COMPENSATION_PREVIEW", _revision, objectIds,
            instances, diff, $"previewed:{diff}:actor={actor.AccountId}");
        foreach (var objectId in objectIds) chooser.Remove(objectId);
        _appliedObjects.Remove(commandId);
        _previewPlans.Remove(commandId);
        _revision++;
        return new(true, "LIVE_SPAWN_COMPENSATED", _revision, objectIds, instances, diff,
            $"compensated:{diff}:actor={actor.AccountId}:revision={_revision}");
    }

    private IReadOnlyList<SpawnedObject> GetOrCreatePlan(Request request)
    {
        if (_previewPlans.TryGetValue(request.CommandId, out var existing)) return existing;
        var ids = Enumerable.Range(0, request.Count).Select(_ => objectIdFactory()).ToArray();
        if (ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            throw new InvalidOperationException("LIVE_SPAWN_ID_FACTORY_INVALID");
        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(request.Count)));
        var plan = ids.Select((id, index) =>
        {
            var (offsetX, offsetZ) = request.Placement switch
            {
                "formation" => ((index % columns) * request.Spacing,
                    (index / columns) * request.Spacing),
                "scatter" => (((index * 37 % 11) - 5) * request.Spacing,
                    ((index * 53 % 13) - 6) * request.Spacing),
                _ => (0d, 0d)
            };
            return new SpawnedObject(id, request.InstanceId, request.X + offsetX, request.Y,
                request.Z + offsetZ);
        }).ToArray();
        _previewPlans[request.CommandId] = plan;
        return plan;
    }

    private Result Reject(string code, Request request) => new(false, code, _revision, [], [],
        "no changes", $"rejected:{code}:command={request.CommandId}");

    private sealed record AppliedSpawn(Guid BlueprintId, Guid ZoneId,
        IReadOnlyList<Guid> ObjectIds);
}
