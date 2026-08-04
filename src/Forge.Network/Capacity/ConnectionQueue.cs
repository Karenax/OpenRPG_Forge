namespace Forge.Network.Capacity;

public sealed record QueueEntry(Guid TicketId, Guid AccountId, DateTimeOffset EnqueuedAt, long Sequence);
public sealed record AdmissionReservation(Guid ReservationId, Guid AccountId, DateTimeOffset ExpiresAt);
public sealed record AdmissionDecision(bool Admitted, string Code, QueueEntry? QueueEntry, AdmissionReservation? Reservation, int Position);

public sealed class ConnectionQueue(int capacity, TimeProvider timeProvider, TimeSpan reservationLifetime)
{
    private readonly Queue<QueueEntry> _queue = [];
    private readonly Dictionary<Guid, AdmissionReservation> _reservations = [];
    private readonly HashSet<Guid> _connected = [];
    private long _sequence;

    public AdmissionDecision Request(Guid accountId)
    {
        ExpireReservations();
        if (accountId == Guid.Empty || _connected.Contains(accountId) || _reservations.Values.Any(x => x.AccountId == accountId) || _queue.Any(x => x.AccountId == accountId)) return new(false, "NET_CAPACITY_ACCOUNT_INVALID", null, null, 0);
        if (OccupiedSlots < capacity) return Reserve(accountId);
        var entry = new QueueEntry(Guid.NewGuid(), accountId, timeProvider.GetUtcNow(), ++_sequence);
        _queue.Enqueue(entry);
        return new(false, "NET_CAPACITY_QUEUED", entry, null, _queue.Count);
    }

    public bool Confirm(Guid reservationId)
    {
        ExpireReservations();
        if (!_reservations.Remove(reservationId, out var reservation)) return false;
        return _connected.Add(reservation.AccountId);
    }

    public AdmissionDecision? Disconnect(Guid accountId)
    {
        _connected.Remove(accountId);
        ExpireReservations();
        return _queue.Count == 0 || OccupiedSlots >= capacity ? null : Reserve(_queue.Dequeue().AccountId);
    }

    public int OccupiedSlots => _connected.Count + _reservations.Count;
    private AdmissionDecision Reserve(Guid accountId)
    {
        var reservation = new AdmissionReservation(Guid.NewGuid(), accountId, timeProvider.GetUtcNow() + reservationLifetime);
        _reservations.Add(reservation.ReservationId, reservation);
        return new(true, "NET_CAPACITY_RESERVED", null, reservation, 0);
    }
    private void ExpireReservations()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var id in _reservations.Where(x => x.Value.ExpiresAt <= now).Select(x => x.Key).ToArray()) _reservations.Remove(id);
    }
}
