using Forge.Network.Capacity;
using Forge.Network.Persistence;
using Forge.Network.Replication;
using Forge.Network.Security;
using Forge.Network.Sessions;
using Forge.Network.Testing;

namespace Forge.Network.Tests;

public sealed class ResilienceDeterminismTests
{
    [Fact]
    public void Intent_guard_rejects_teleport_replay_and_rate_abuse()
    {
        var guard = new IntentGuard(10, 2);
        var connection = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        MovementIntent Intent(long sequence, NetworkVector3 to) => new(Guid.NewGuid(), connection, Guid.NewGuid(), sequence, 4, new(0, 0, 0), to, TimeSpan.FromSeconds(1));
        Assert.Equal("NET_GUARD_MOVEMENT_EXCEEDED", guard.Validate(Intent(1, new(20, 0, 0)), 4, now).Code);
        Assert.True(guard.Validate(Intent(1, new(2, 0, 0)), 4, now).Allowed);
        Assert.Equal("NET_GUARD_SEQUENCE_REPLAYED", guard.Validate(Intent(1, new(2, 0, 0)), 4, now).Code);
        Assert.True(guard.Validate(Intent(2, new(2, 0, 0)), 4, now).Allowed);
        Assert.Equal("NET_GUARD_RATE_EXCEEDED", guard.Validate(Intent(3, new(2, 0, 0)), 4, now).Code);
    }

    [Fact]
    public void Reconnection_ticket_is_bound_expiring_and_single_use()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var service = new ReconnectionService(clock, TimeSpan.FromMinutes(1));
        var account = Guid.NewGuid(); var session = Guid.NewGuid();
        var ticket = service.Issue(account, Guid.NewGuid(), session, 12);
        Assert.Equal("NET_RECONNECT_IDENTITY_MISMATCH", service.Redeem(ticket.Token, Guid.NewGuid(), session).Code);
        var fresh = service.Issue(account, Guid.NewGuid(), session, 12);
        Assert.True(service.Redeem(fresh.Token, account, session).Accepted);
        Assert.Equal("NET_RECONNECT_TOKEN_INVALID", service.Redeem(fresh.Token, account, session).Code);
        var expired = service.Issue(account, Guid.NewGuid(), session, 12); clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal("NET_RECONNECT_TOKEN_EXPIRED", service.Redeem(expired.Token, account, session).Code);
    }

    [Fact]
    public void Rollback_is_previewed_and_compensates_with_new_revision()
    {
        var store = new InMemoryWorldSnapshotStore(TimeProvider.System);
        var actor = Guid.NewGuid();
        var snapshot = store.Save(0, 2, "{\"state\":\"safe\"}", actor);
        var rollback = new RollbackService(store);
        Assert.Equal("NET_ROLLBACK_PREVIEW", rollback.Preview(snapshot.SnapshotId, 5, actor).Code);
        var applied = rollback.Apply(snapshot.SnapshotId, 5, actor);
        Assert.True(applied.Applied);
        Assert.Equal(6, applied.NewWorldRevision);
        Assert.Equal(snapshot.Sha256, applied.RestoredSnapshot!.Sha256);
    }

    [Fact]
    public void Capacity_queue_is_fifo_and_reservation_is_confirmed()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var queue = new ConnectionQueue(1, clock, TimeSpan.FromSeconds(10));
        var first = Guid.NewGuid(); var second = Guid.NewGuid(); var third = Guid.NewGuid();
        var reservation = queue.Request(first).Reservation!;
        Assert.True(queue.Confirm(reservation.ReservationId));
        Assert.Equal(1, queue.Request(second).Position);
        Assert.Equal(2, queue.Request(third).Position);
        var promoted = queue.Disconnect(first)!;
        Assert.Equal(second, promoted.Reservation!.AccountId);
    }

    [Fact]
    public void Network_harness_replays_faults_deterministically_for_two_clients()
    {
        static string Run()
        {
            var harness = new DeterministicNetworkHarness(new(2, 3, 2, true));
            harness.Send("client-a", "server", "a1");
            harness.Send("client-b", "server", "b1");
            harness.Send("server", "client-a", "dropped");
            harness.Send("server", "client-b", "state-1");
            harness.Advance(2);
            return harness.Digest();
        }
        Assert.Equal(Run(), Run());
        Assert.Equal("client-b:state-1,state-1|server:b1,b1,a1", Run());
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
