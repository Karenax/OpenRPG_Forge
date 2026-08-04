using Forge.Network.Replication;

namespace Forge.Network.Security;

public sealed record MovementIntent(Guid IntentId, Guid ConnectionId, Guid CharacterId, long ClientSequence, long ExpectedWorldRevision, NetworkVector3 From, NetworkVector3 To, TimeSpan ClientDelta);
public sealed record IntentGuardDecision(bool Allowed, string Code, int ViolationScore, string AuditEntry);

public sealed class IntentGuard(float maximumUnitsPerSecond, int maximumIntentsPerSecond)
{
    private readonly Dictionary<Guid, Queue<DateTimeOffset>> _recent = [];
    private readonly Dictionary<Guid, long> _lastSequences = [];
    private readonly Dictionary<Guid, int> _scores = [];

    public IntentGuardDecision Validate(MovementIntent intent, long serverRevision, DateTimeOffset receivedAt)
    {
        if (intent.IntentId == Guid.Empty || intent.ConnectionId == Guid.Empty || intent.CharacterId == Guid.Empty) return Reject(intent.ConnectionId, "NET_GUARD_ID_INVALID", 10);
        if (intent.ExpectedWorldRevision != serverRevision) return Reject(intent.ConnectionId, "NET_GUARD_REVISION_STALE", 1);
        if (_lastSequences.TryGetValue(intent.ConnectionId, out var last) && intent.ClientSequence <= last) return Reject(intent.ConnectionId, "NET_GUARD_SEQUENCE_REPLAYED", 5);
        var queue = _recent.GetValueOrDefault(intent.ConnectionId) ?? (_recent[intent.ConnectionId] = new());
        while (queue.TryPeek(out var timestamp) && receivedAt - timestamp >= TimeSpan.FromSeconds(1)) queue.Dequeue();
        if (queue.Count >= maximumIntentsPerSecond) return Reject(intent.ConnectionId, "NET_GUARD_RATE_EXCEEDED", 3);
        if (intent.ClientDelta <= TimeSpan.Zero || intent.ClientDelta > TimeSpan.FromSeconds(1)) return Reject(intent.ConnectionId, "NET_GUARD_DELTA_INVALID", 2);
        var distance = MathF.Sqrt(intent.From.DistanceSquared(intent.To));
        if (distance / intent.ClientDelta.TotalSeconds > maximumUnitsPerSecond) return Reject(intent.ConnectionId, "NET_GUARD_MOVEMENT_EXCEEDED", 5);
        _lastSequences[intent.ConnectionId] = intent.ClientSequence;
        queue.Enqueue(receivedAt);
        return new(true, "NET_GUARD_ALLOWED", _scores.GetValueOrDefault(intent.ConnectionId), $"accepted:NET_GUARD_ALLOWED:connection={intent.ConnectionId}:sequence={intent.ClientSequence}");
    }

    private IntentGuardDecision Reject(Guid connectionId, string code, int score)
    {
        _scores[connectionId] = _scores.GetValueOrDefault(connectionId) + score;
        return new(false, code, _scores[connectionId], $"rejected:{code}:connection={connectionId}:score={_scores[connectionId]}");
    }
}
