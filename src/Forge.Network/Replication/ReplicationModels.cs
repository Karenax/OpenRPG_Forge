namespace Forge.Network.Replication;

public sealed record NetworkVector3(float X, float Y, float Z)
{
    public float DistanceSquared(NetworkVector3 other)
    {
        var x = X - other.X;
        var y = Y - other.Y;
        var z = Z - other.Z;
        return (x * x) + (y * y) + (z * z);
    }
}

public sealed record ReplicatedEntity(
    Guid EntityId,
    Guid ZoneInstanceId,
    NetworkVector3 Position,
    long Revision,
    IReadOnlyDictionary<string, string> Components,
    bool AlwaysRelevant = false);

public sealed record ObserverScope(Guid ConnectionId, Guid ZoneInstanceId, NetworkVector3 Position, float Radius, IReadOnlySet<Guid> ExplicitEntityIds);
public sealed record ReplicationDelta(long FromRevision, long ToRevision, IReadOnlyList<ReplicatedEntity> Upserts, IReadOnlyList<Guid> RemovedEntityIds);
