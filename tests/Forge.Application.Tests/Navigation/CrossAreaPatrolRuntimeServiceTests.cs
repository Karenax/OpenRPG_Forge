using Forge.Application.Navigation;
using Forge.Domain.Areas;
using Forge.Domain.RuntimePolicies;

namespace Forge.Application.Tests.Navigation;

public sealed class CrossAreaPatrolRuntimeServiceTests
{
    [Fact]
    public void Patrol_waits_for_streaming_then_transfers_and_resumes_at_waypoint()
    {
        var from = AreaId.From(Guid.NewGuid());
        var to = AreaId.From(Guid.NewGuid());
        var resume = Guid.NewGuid();
        var segment = new CrossAreaPatrolSegment(Guid.NewGuid(), from, to, PortalId.From(Guid.NewGuid()), resume,
            true, PatrolOwnershipAuthority.Server);
        var route = new CrossAreaPatrolRoute(Guid.NewGuid(), "guard.elyra", [segment], false, "test");
        var cursor = new CrossAreaPatrolCursor(0, from, Guid.Empty, false, false);

        var waiting = CrossAreaPatrolRuntimeService.Advance(route, cursor, true, false);
        Assert.True(waiting.Cursor.AwaitingStreaming);
        Assert.True(waiting.PrefetchDestination);
        var loaded = CrossAreaPatrolRuntimeService.Advance(route, waiting.Cursor, true, true);
        Assert.Equal(to, loaded.Cursor.CurrentAreaId);
        Assert.Equal(resume, loaded.Cursor.ResumeWaypointId);
        Assert.True(loaded.Cursor.Complete);
        Assert.Throws<InvalidOperationException>(() =>
            CrossAreaPatrolRuntimeService.Advance(route, cursor, false, true));
    }
}
