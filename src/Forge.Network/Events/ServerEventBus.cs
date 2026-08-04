namespace Forge.Network.Events;

public sealed record ServerEvent(Guid EventId, string Kind, Guid CorrelationId, Guid ActorAccountId, long WorldRevision, DateTimeOffset OccurredAt, IReadOnlyDictionary<string, string> Data, long Sequence = 0);

public interface IServerEventHandler
{
    string EventKind { get; }
    void Handle(ServerEvent serverEvent);
}

public sealed class ServerEventBus
{
    private readonly List<IServerEventHandler> _handlers = [];
    private readonly List<ServerEvent> _events = [];
    private readonly HashSet<Guid> _eventIds = [];
    private long _sequence;

    public void Subscribe(IServerEventHandler handler)
    {
        if (_handlers.Contains(handler)) throw new InvalidOperationException("NET_EVENT_HANDLER_DUPLICATE");
        _handlers.Add(handler);
    }

    public ServerEvent Publish(ServerEvent serverEvent)
    {
        if (serverEvent.EventId == Guid.Empty || serverEvent.CorrelationId == Guid.Empty || serverEvent.ActorAccountId == Guid.Empty || string.IsNullOrWhiteSpace(serverEvent.Kind))
            throw new ArgumentException("NET_EVENT_INVALID", nameof(serverEvent));
        if (!_eventIds.Add(serverEvent.EventId)) throw new InvalidOperationException("NET_EVENT_REPLAYED");
        _sequence++;
        var sequenced = serverEvent with { Sequence = _sequence, Data = new SortedDictionary<string, string>(serverEvent.Data.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal), StringComparer.Ordinal) };
        _events.Add(sequenced);
        foreach (var handler in _handlers.Where(x => x.EventKind == sequenced.Kind)) handler.Handle(sequenced);
        return sequenced;
    }

    public IReadOnlyList<ServerEvent> Since(long sequence) => _events.Where(x => x.Sequence > sequence).OrderBy(x => x.Sequence).ToArray();
}
