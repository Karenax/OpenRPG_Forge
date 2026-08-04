namespace Forge.Network.Replication;

public sealed class ReplicationService
{
    private readonly Dictionary<Guid, ReplicatedEntity> _entities = [];
    private readonly Dictionary<Guid, long> _removed = [];
    private long _revision;

    public long Revision => _revision;

    public long Upsert(Guid actorConnectionId, ReplicatedEntity entity, bool actorHasAuthority)
    {
        if (!actorHasAuthority) throw new UnauthorizedAccessException("NET_REPLICATION_AUTHORITY_REQUIRED");
        if (actorConnectionId == Guid.Empty || entity.EntityId == Guid.Empty || entity.ZoneInstanceId == Guid.Empty)
            throw new ArgumentException("NET_REPLICATION_ID_INVALID", nameof(entity));
        _revision++;
        _entities[entity.EntityId] = entity with
        {
            Revision = _revision,
            Components = new SortedDictionary<string, string>(entity.Components.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), StringComparer.Ordinal)
        };
        _removed.Remove(entity.EntityId);
        return _revision;
    }

    public long Remove(Guid actorConnectionId, Guid entityId, bool actorHasAuthority)
    {
        if (!actorHasAuthority) throw new UnauthorizedAccessException("NET_REPLICATION_AUTHORITY_REQUIRED");
        if (actorConnectionId == Guid.Empty || !_entities.Remove(entityId)) throw new KeyNotFoundException("NET_REPLICATION_ENTITY_UNKNOWN");
        _revision++;
        _removed[entityId] = _revision;
        return _revision;
    }

    public ReplicationDelta CreateDelta(ObserverScope observer, long fromRevision)
    {
        if (observer.Radius is < 0 or > 10000) throw new ArgumentOutOfRangeException(nameof(observer), "NET_INTEREST_RADIUS_INVALID");
        var radiusSquared = observer.Radius * observer.Radius;
        var upserts = _entities.Values
            .Where(entity => entity.Revision > fromRevision)
            .Where(entity => entity.AlwaysRelevant || observer.ExplicitEntityIds.Contains(entity.EntityId) ||
                (entity.ZoneInstanceId == observer.ZoneInstanceId && entity.Position.DistanceSquared(observer.Position) <= radiusSquared))
            .OrderBy(entity => entity.EntityId)
            .ToArray();
        var removed = _removed.Where(pair => pair.Value > fromRevision).Select(pair => pair.Key).Order().ToArray();
        return new(fromRevision, _revision, upserts, removed);
    }
}
