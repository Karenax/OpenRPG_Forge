using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Lifecycle;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Areas;

public sealed record AreaTransitionRuntimeResult(
    bool Succeeded,
    PortalLinkTravelResolution? Travel,
    AreaStreamingResult? Streaming,
    RuntimeLifecycleDispatchResult? Lifecycle,
    IReadOnlyList<string> Diagnostics);

public static class AreaTransitionRuntimeService
{
    public static async Task<AreaTransitionRuntimeResult> TransitionAsync(ProjectDefinition project,
        AreaId sourceAreaId, PortalId portalId, EntityId? actorEntityId, Vector3Value incomingVelocity,
        float incomingYawDegrees, AreaStreamingCoordinator? streaming = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        PortalLinkTravelResolution travel;
        try
        {
            travel = PortalLinkRuntimeService.Resolve(project, sourceAreaId, portalId, incomingVelocity,
                incomingYawDegrees);
        }
        catch (InvalidOperationException exception)
        {
            return new(false, null, null, null, [exception.Message]);
        }

        var destination = project.Areas.Single(item => item.Id == travel.DestinationAreaId);
        AreaStreamingResult? streamed = null;
        if (travel.PreloadDestination && destination.World.Loading.RequiredResources.Count > 0)
        {
            if (streaming is null)
                return new(false, travel, null, null, ["area.portal.streaming_required"]);
            streamed = await streaming.LoadAsync(destination.World.Loading, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        var lifecycle = new RuntimeLifecycleCoordinator();
        var loaded = lifecycle.LoadProject(project, RuntimeAuthority.SinglePlayer, RuntimeLifecycleCause.Startup);
        if (!loaded.Succeeded)
            return new(false, travel, streamed, loaded, loaded.Diagnostics.Select(item => item.Code).ToArray());
        var entered = lifecycle.EnterInitialArea(sourceAreaId, actorEntityId, RuntimeLifecycleCause.Startup);
        if (!entered.Succeeded)
            return new(false, travel, streamed, entered, entered.Diagnostics.Select(item => item.Code).ToArray());
        var started = lifecycle.StartWorld(RuntimeLifecycleCause.Startup);
        if (!started.Succeeded)
            return new(false, travel, streamed, started, started.Diagnostics.Select(item => item.Code).ToArray());
        var transitioned = lifecycle.Transition(travel.DestinationAreaId, actorEntityId, portalId,
            RuntimeLifecycleCause.PortalTransition);
        return new(transitioned.Succeeded, travel, streamed, transitioned,
            transitioned.Diagnostics.Select(item => item.Code).ToArray());
    }
}
