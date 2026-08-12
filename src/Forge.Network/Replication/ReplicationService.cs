namespace Forge.Network.Replication;

public sealed class ReplicationService
{
    private readonly Dictionary<Guid, ReplicatedEntity> _entities = [];
    private readonly Dictionary<Guid, long> _removed = [];
    private readonly Dictionary<Guid, PredictionTicket> _predictions = [];
    private readonly ReplicationRuntimePolicy _policy;
    private long _revision;

    public ReplicationService(ReplicationRuntimePolicy? policy = null)
    {
        _policy = policy ?? new([new("*", true, true, true)], 4_194_304, true, true, true, true, true, true);
        ValidatePolicy(_policy);
    }

    public long Revision => _revision;

    public long Upsert(Guid actorConnectionId, ReplicatedEntity entity, bool actorHasAuthority)
    {
        if (!actorHasAuthority) throw new UnauthorizedAccessException("NET_REPLICATION_AUTHORITY_REQUIRED");
        if (actorConnectionId == Guid.Empty || entity.EntityId == Guid.Empty || entity.ZoneInstanceId == Guid.Empty)
            throw new ArgumentException("NET_REPLICATION_ID_INVALID", nameof(entity));
        foreach (var component in entity.Components)
        {
            if (string.IsNullOrWhiteSpace(component.Key) || FindRule(component.Key) is null)
                throw new InvalidOperationException("NET_REPLICATION_COMPONENT_FORBIDDEN");
        }
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
        if (observer.ConnectionId == Guid.Empty || observer.Radius is < 0 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(observer), "NET_INTEREST_SCOPE_INVALID");
        var radiusSquared = observer.Radius * observer.Radius;
        var visible = observer.VisibleEntityIds ?? new HashSet<Guid>();
        var party = observer.PartyEntityIds ?? new HashSet<Guid>();
        var owned = observer.OwnedEntityIds ?? new HashSet<Guid>();
        var upserts = _entities.Values
            .Where(entity => entity.Revision > fromRevision)
            .Where(entity => IsRelevant(entity, observer, visible, party, owned, radiusSquared))
            .OrderBy(entity => entity.EntityId)
            .ToArray();
        var removed = _removed.Where(pair => pair.Value > fromRevision).Select(pair => pair.Key).Order().ToArray();
        var estimatedBytes = upserts.Sum(entity => 64 + entity.Components.Sum(pair => pair.Key.Length + pair.Value.Length + 8)) +
            (removed.Length * 16);
        if (estimatedBytes > _policy.MaximumDeltaBytes) throw new InvalidOperationException("NET_REPLICATION_DELTA_BUDGET_EXCEEDED");
        var compressed = upserts.SelectMany(entity => entity.Components.Keys).Any(component => FindRule(component)?.DeltaCompression == true);
        return new(fromRevision, _revision, upserts, removed, compressed, estimatedBytes);
    }

    public PredictionDecision Predict(Guid connectionId, Guid entityId, string componentId, string value,
        long expectedRevision)
    {
        if (!_entities.TryGetValue(entityId, out var entity)) return RejectPrediction("NET_PREDICTION_ENTITY_UNKNOWN", connectionId, entityId, componentId);
        if (entity.OwnerConnectionId != connectionId) return RejectPrediction("NET_PREDICTION_OWNER_REQUIRED", connectionId, entityId, componentId);
        var rule = FindRule(componentId);
        if (rule is null || !rule.OwnerPrediction || !rule.Reconciliation)
            return RejectPrediction("NET_PREDICTION_COMPONENT_FORBIDDEN", connectionId, entityId, componentId);
        if (entity.Revision != expectedRevision) return RejectPrediction("NET_PREDICTION_REVISION_STALE", connectionId, entityId, componentId);
        var ticket = new PredictionTicket(Guid.NewGuid(), connectionId, entityId, componentId, value, expectedRevision);
        _predictions.Add(ticket.PredictionId, ticket);
        return new(true, "NET_PREDICTION_ACCEPTED", ticket, entity.Components.GetValueOrDefault(componentId), entity.Revision,
            $"accepted:NET_PREDICTION_ACCEPTED:connection={connectionId}:entity={entityId}:component={componentId}:revision={entity.Revision}");
    }

    public PredictionDecision Reconcile(Guid predictionId)
    {
        if (!_predictions.Remove(predictionId, out var ticket))
            return RejectPrediction("NET_PREDICTION_UNKNOWN", Guid.Empty, Guid.Empty, "unknown");
        if (!_entities.TryGetValue(ticket.EntityId, out var entity))
            return RejectPrediction("NET_PREDICTION_ENTITY_UNKNOWN", ticket.ConnectionId, ticket.EntityId, ticket.ComponentId);
        var authoritative = entity.Components.GetValueOrDefault(ticket.ComponentId);
        var code = string.Equals(authoritative, ticket.PredictedValue, StringComparison.Ordinal)
            ? "NET_PREDICTION_CONFIRMED"
            : "NET_PREDICTION_CORRECTED";
        return new(true, code, ticket, authoritative, entity.Revision,
            $"accepted:{code}:connection={ticket.ConnectionId}:entity={ticket.EntityId}:component={ticket.ComponentId}:revision={entity.Revision}");
    }

    private bool IsRelevant(ReplicatedEntity entity, ObserverScope observer, IReadOnlySet<Guid> visible,
        IReadOnlySet<Guid> party, IReadOnlySet<Guid> owned, float radiusSquared)
    {
        if (entity.AlwaysRelevant) return true;
        if (_policy.ExplicitSubscriptions && observer.ExplicitEntityIds.Contains(entity.EntityId)) return true;
        if (_policy.InterestByOwnership && (entity.OwnerConnectionId == observer.ConnectionId || owned.Contains(entity.EntityId))) return true;
        if (_policy.InterestByParty && party.Contains(entity.EntityId)) return true;
        if (_policy.InterestByVisibility && visible.Contains(entity.EntityId)) return true;
        var areaMatches = !_policy.InterestByArea || entity.ZoneInstanceId == observer.ZoneInstanceId;
        var distanceMatches = !_policy.InterestByDistance || entity.Position.DistanceSquared(observer.Position) <= radiusSquared;
        return areaMatches && distanceMatches;
    }

    private ComponentReplicationRule? FindRule(string componentId) => _policy.Components.FirstOrDefault(rule =>
        rule.ComponentId == "*" || string.Equals(rule.ComponentId, componentId, StringComparison.Ordinal));

    private static void ValidatePolicy(ReplicationRuntimePolicy policy)
    {
        if (policy.Components.Count == 0 || policy.Components.Any(rule => string.IsNullOrWhiteSpace(rule.ComponentId)) ||
            policy.Components.Select(rule => rule.ComponentId).Distinct(StringComparer.Ordinal).Count() != policy.Components.Count)
            throw new ArgumentException("NET_REPLICATION_POLICY_COMPONENTS_INVALID", nameof(policy));
        if (policy.MaximumDeltaBytes is < 1024 or > 4_194_304)
            throw new ArgumentOutOfRangeException(nameof(policy), "NET_REPLICATION_DELTA_BUDGET_INVALID");
    }

    private PredictionDecision RejectPrediction(string code, Guid connectionId, Guid entityId, string componentId) =>
        new(false, code, null, null, _revision,
            $"rejected:{code}:connection={connectionId}:entity={entityId}:component={componentId}:revision={_revision}");
}
