namespace Forge.Network.Persistence;

public sealed record RollbackResult(bool Applied, string Code, WorldSnapshot? RestoredSnapshot, long NewWorldRevision, string AuditEntry);

public sealed class RollbackService(IWorldSnapshotStore store)
{
    public RollbackResult Preview(Guid snapshotId, long currentRevision, Guid actorAccountId) => Execute(snapshotId, currentRevision, actorAccountId, false);
    public RollbackResult Apply(Guid snapshotId, long currentRevision, Guid actorAccountId) => Execute(snapshotId, currentRevision, actorAccountId, true);

    private RollbackResult Execute(Guid snapshotId, long currentRevision, Guid actorAccountId, bool apply)
    {
        if (actorAccountId == Guid.Empty) return Reject("NET_ROLLBACK_ACTOR_REQUIRED", currentRevision, actorAccountId);
        var target = store.Load(snapshotId);
        if (target is null) return Reject("NET_ROLLBACK_SNAPSHOT_UNKNOWN", currentRevision, actorAccountId);
        if (target.WorldRevision >= currentRevision) return Reject("NET_ROLLBACK_TARGET_INVALID", currentRevision, actorAccountId);
        if (!apply) return new(false, "NET_ROLLBACK_PREVIEW", target, currentRevision, $"previewed:rollback:actor={actorAccountId}:from={currentRevision}:snapshot={snapshotId}");
        return new(true, "NET_ROLLBACK_APPLIED", target, currentRevision + 1, $"applied:rollback:actor={actorAccountId}:from={currentRevision}:source={target.WorldRevision}:revision={currentRevision + 1}");
    }

    private static RollbackResult Reject(string code, long revision, Guid actor) => new(false, code, null, revision, $"rejected:{code}:actor={actor}:revision={revision}");
}
