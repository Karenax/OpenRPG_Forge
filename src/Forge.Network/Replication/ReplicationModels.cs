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
    bool AlwaysRelevant = false,
    Guid? OwnerConnectionId = null);

public sealed record ObserverScope(
    Guid ConnectionId,
    Guid ZoneInstanceId,
    NetworkVector3 Position,
    float Radius,
    IReadOnlySet<Guid> ExplicitEntityIds,
    IReadOnlySet<Guid>? VisibleEntityIds = null,
    IReadOnlySet<Guid>? PartyEntityIds = null,
    IReadOnlySet<Guid>? OwnedEntityIds = null);

public sealed record ComponentReplicationRule(
    string ComponentId,
    bool DeltaCompression,
    bool OwnerPrediction,
    bool Reconciliation);

public sealed record ReplicationRuntimePolicy(
    IReadOnlyList<ComponentReplicationRule> Components,
    int MaximumDeltaBytes,
    bool InterestByArea,
    bool InterestByDistance,
    bool InterestByVisibility,
    bool InterestByParty,
    bool InterestByOwnership,
    bool ExplicitSubscriptions);

public sealed record ReplicationDelta(
    long FromRevision,
    long ToRevision,
    IReadOnlyList<ReplicatedEntity> Upserts,
    IReadOnlyList<Guid> RemovedEntityIds,
    bool Compressed = false,
    int EstimatedBytes = 0);

public sealed record PredictionTicket(
    Guid PredictionId,
    Guid ConnectionId,
    Guid EntityId,
    string ComponentId,
    string PredictedValue,
    long ExpectedRevision);

public sealed record PredictionDecision(
    bool Accepted,
    string Code,
    PredictionTicket? Ticket,
    string? AuthoritativeValue,
    long Revision,
    string AuditEntry);
