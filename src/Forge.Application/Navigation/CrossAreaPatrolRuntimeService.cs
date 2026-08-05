using Forge.Domain.Areas;
using Forge.Domain.RuntimePolicies;

namespace Forge.Application.Navigation;

public sealed record CrossAreaPatrolCursor(int SegmentIndex, AreaId CurrentAreaId, Guid ResumeWaypointId,
    bool AwaitingStreaming, bool Complete);

public sealed record CrossAreaPatrolTransition(CrossAreaPatrolCursor Cursor, PortalId PortalId,
    AreaId DestinationAreaId, bool PrefetchDestination, PatrolOwnershipAuthority Authority);

public static class CrossAreaPatrolRuntimeService
{
    public static CrossAreaPatrolTransition Advance(CrossAreaPatrolRoute route, CrossAreaPatrolCursor cursor,
        bool callerOwnsActor, bool destinationLoaded)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(cursor);
        if (!callerOwnsActor) throw new InvalidOperationException("PATROL_OWNERSHIP_REQUIRED");
        if (cursor.Complete) throw new InvalidOperationException("PATROL_ROUTE_COMPLETE");
        if (cursor.SegmentIndex < 0 || cursor.SegmentIndex >= route.Segments.Count)
            throw new InvalidOperationException("PATROL_CURSOR_INVALID");
        var segment = route.Segments[cursor.SegmentIndex];
        if (segment.FromAreaId != cursor.CurrentAreaId)
            throw new InvalidOperationException("PATROL_AREA_MISMATCH");
        if (!destinationLoaded)
            return new CrossAreaPatrolTransition(cursor with { AwaitingStreaming = true }, segment.PortalId,
                segment.ToAreaId, segment.PrefetchDestination, segment.Authority);

        var nextIndex = cursor.SegmentIndex + 1;
        var complete = nextIndex >= route.Segments.Count && !route.Loop;
        if (nextIndex >= route.Segments.Count) nextIndex = 0;
        var next = new CrossAreaPatrolCursor(nextIndex, segment.ToAreaId, segment.ResumeWaypointId, false, complete);
        return new CrossAreaPatrolTransition(next, segment.PortalId, segment.ToAreaId,
            segment.PrefetchDestination, segment.Authority);
    }
}
