using Forge.Domain.RuntimePolicies;

namespace Forge.Network.World;

public sealed record PatrolOwnershipLease(string ActorId, string AuthorityId, long Revision,
    DateTimeOffset ExpiresAt);

public sealed record PatrolTransferDecision(bool Allowed, string Code, long Revision);

public static class CrossAreaPatrolAuthority
{
    public static PatrolTransferDecision Authorize(CrossAreaPatrolSegment segment, PatrolOwnershipLease lease,
        string actorId, string callerAuthorityId, long expectedRevision, DateTimeOffset now, bool callerIsServer)
    {
        ArgumentNullException.ThrowIfNull(segment);
        ArgumentNullException.ThrowIfNull(lease);
        if (segment.Authority == PatrolOwnershipAuthority.Server && !callerIsServer)
            return new(false, "NET_PATROL_SERVER_REQUIRED", lease.Revision);
        if (lease.ActorId != actorId || lease.AuthorityId != callerAuthorityId)
            return new(false, "NET_PATROL_OWNERSHIP_DENIED", lease.Revision);
        if (lease.Revision != expectedRevision)
            return new(false, "NET_PATROL_REVISION_CONFLICT", lease.Revision);
        if (lease.ExpiresAt <= now)
            return new(false, "NET_PATROL_LEASE_EXPIRED", lease.Revision);
        return new(true, "NET_PATROL_TRANSFER_ACCEPTED", lease.Revision + 1);
    }
}
