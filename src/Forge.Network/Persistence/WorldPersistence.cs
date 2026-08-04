using System.Security.Cryptography;
using System.Text;

namespace Forge.Network.Persistence;

public sealed record WorldSnapshot(Guid SnapshotId, long WorldRevision, DateTimeOffset CreatedAt, string Payload, string Sha256, Guid ActorAccountId);

public interface IWorldSnapshotStore
{
    WorldSnapshot Save(long expectedLatestRevision, long worldRevision, string canonicalPayload, Guid actorAccountId);
    WorldSnapshot? LoadLatest();
    WorldSnapshot? Load(Guid snapshotId);
}

public sealed class InMemoryWorldSnapshotStore(TimeProvider timeProvider) : IWorldSnapshotStore
{
    private readonly List<WorldSnapshot> _snapshots = [];

    public WorldSnapshot Save(long expectedLatestRevision, long worldRevision, string canonicalPayload, Guid actorAccountId)
    {
        var latest = LoadLatest();
        if ((latest?.WorldRevision ?? 0) != expectedLatestRevision) throw new InvalidOperationException("NET_PERSISTENCE_REVISION_STALE");
        if (worldRevision <= expectedLatestRevision || actorAccountId == Guid.Empty || string.IsNullOrWhiteSpace(canonicalPayload)) throw new ArgumentException("NET_PERSISTENCE_SNAPSHOT_INVALID");
        var normalized = canonicalPayload.Replace("\r\n", "\n", StringComparison.Ordinal);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        var snapshot = new WorldSnapshot(Guid.NewGuid(), worldRevision, timeProvider.GetUtcNow(), normalized, hash, actorAccountId);
        _snapshots.Add(snapshot);
        return snapshot;
    }

    public WorldSnapshot? LoadLatest() => _snapshots.OrderByDescending(x => x.WorldRevision).FirstOrDefault();
    public WorldSnapshot? Load(Guid snapshotId) => _snapshots.SingleOrDefault(x => x.SnapshotId == snapshotId);
}
