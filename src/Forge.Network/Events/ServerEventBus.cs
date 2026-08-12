namespace Forge.Network.Events;

public sealed record ServerEvent(Guid EventId, string Kind, Guid CorrelationId, Guid ActorAccountId,
    long WorldRevision, DateTimeOffset OccurredAt, IReadOnlyDictionary<string, string> Data,
    long Sequence = 0, Guid? CausationId = null);

public sealed record ServerEventTrace(Guid EventId, Guid CorrelationId, string EventKind,
    string Handler, int Priority, long Sequence, bool Succeeded, string Detail);

public interface IServerEventHandler
{
    string EventKind { get; }
    int Priority => 0;
    void Handle(ServerEvent serverEvent);
}

public sealed class ServerEventBus
{
    private readonly List<IServerEventHandler> _handlers = [];
    private readonly List<ServerEvent> _events = [];
    private readonly List<ServerEventTrace> _traces = [];
    private readonly HashSet<Guid> _eventIds = [];
    private readonly Queue<ServerEvent> _pending = [];
    private readonly int _maximumPendingEvents;
    private readonly bool _traceEnabled;
    private bool _dispatching;
    private long _sequence;

    public ServerEventBus(int maximumPendingEvents = 256, bool traceEnabled = true)
    {
        if (maximumPendingEvents is < 1 or > 65_536)
            throw new ArgumentOutOfRangeException(nameof(maximumPendingEvents), "NET_EVENT_BACKPRESSURE_LIMIT_INVALID");
        _maximumPendingEvents = maximumPendingEvents;
        _traceEnabled = traceEnabled;
    }

    public IReadOnlyList<ServerEventTrace> Traces => _traces.ToArray();

    public void Subscribe(IServerEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (string.IsNullOrWhiteSpace(handler.EventKind))
            throw new ArgumentException("NET_EVENT_HANDLER_KIND_REQUIRED", nameof(handler));
        if (_handlers.Contains(handler)) throw new InvalidOperationException("NET_EVENT_HANDLER_DUPLICATE");
        _handlers.Add(handler);
    }

    public bool Unsubscribe(IServerEventHandler handler) => _handlers.Remove(handler);

    public ServerEvent Publish(ServerEvent serverEvent)
    {
        if (serverEvent.EventId == Guid.Empty || serverEvent.CorrelationId == Guid.Empty ||
            serverEvent.ActorAccountId == Guid.Empty || string.IsNullOrWhiteSpace(serverEvent.Kind) ||
            serverEvent.WorldRevision < 0 || serverEvent.Data.Any(item =>
                string.IsNullOrWhiteSpace(item.Key) || item.Value is null))
            throw new ArgumentException("NET_EVENT_INVALID", nameof(serverEvent));
        if (!_eventIds.Add(serverEvent.EventId)) throw new InvalidOperationException("NET_EVENT_REPLAYED");
        if (_pending.Count >= _maximumPendingEvents)
        {
            _eventIds.Remove(serverEvent.EventId);
            throw new InvalidOperationException("NET_EVENT_BACKPRESSURE_REJECTED");
        }

        _sequence++;
        var sequenced = serverEvent with
        {
            Sequence = _sequence,
            Data = new SortedDictionary<string, string>(serverEvent.Data.ToDictionary(item => item.Key,
                item => item.Value, StringComparer.Ordinal), StringComparer.Ordinal)
        };
        _events.Add(sequenced);
        _pending.Enqueue(sequenced);
        if (!_dispatching) Drain();
        return sequenced;
    }

    public IReadOnlyList<ServerEvent> Since(long sequence) => _events.Where(item => item.Sequence > sequence)
        .OrderBy(item => item.Sequence).ToArray();

    private void Drain()
    {
        _dispatching = true;
        try
        {
            while (_pending.TryDequeue(out var current))
            {
                foreach (var handler in _handlers.Where(item => item.EventKind == current.Kind)
                             .OrderByDescending(item => item.Priority)
                             .ThenBy(item => item.GetType().FullName, StringComparer.Ordinal))
                {
                    try
                    {
                        handler.Handle(current);
                        Trace(current, handler, true, "handled");
                    }
                    catch (Exception exception)
                    {
                        Trace(current, handler, false, exception.GetType().Name);
                        throw;
                    }
                }
            }
        }
        finally
        {
            _dispatching = false;
        }
    }

    private void Trace(ServerEvent current, IServerEventHandler handler, bool succeeded, string detail)
    {
        if (!_traceEnabled) return;
        _traces.Add(new(current.EventId, current.CorrelationId, current.Kind,
            handler.GetType().FullName ?? handler.GetType().Name, handler.Priority, current.Sequence,
            succeeded, detail));
    }
}
