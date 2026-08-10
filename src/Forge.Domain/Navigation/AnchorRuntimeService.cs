using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Domain.Navigation;

public sealed record SpawnAnchorResolution(
    AnchorInstance Instance,
    AnchorDefinition Definition,
    bool UsedFallback,
    string Reason);

public sealed record PortalTravelResolution(
    AreaId SourceAreaId,
    Guid SourceAnchorInstanceId,
    AreaId TargetAreaId,
    Guid TargetAnchorInstanceId,
    TransformValue TargetTransform,
    float ArrivalYawDegrees,
    Vector3Value ArrivalVelocity,
    bool Preload);

public static class AnchorRuntimeService
{
    public static SpawnAnchorResolution ResolveSpawn(AreaDefinition area, string profile, string team,
        IReadOnlySet<Guid>? blockedInstanceIds = null)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(team);
        var blocked = blockedInstanceIds ?? new HashSet<Guid>();
        var definitions = area.Navigation.Anchors.Definitions.ToDictionary(item => item.Id);
        var candidates = area.Navigation.Anchors.Instances.Where(item => item.Spawn is not null &&
                definitions.TryGetValue(item.DefinitionId, out var definition) && definition.Kind == AnchorKind.Spawn &&
                string.Equals(item.Spawn.Profile, profile, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(item.Spawn.Team, team, StringComparison.OrdinalIgnoreCase) || item.Spawn.Team == "*"))
            .OrderByDescending(item => item.Spawn!.Priority).ThenBy(item => item.DisplayOrder).ThenBy(item => item.Id)
            .ToArray();
        var selected = candidates.FirstOrDefault(item => !blocked.Contains(item.Id));
        if (selected is not null)
            return new(selected, definitions[selected.DefinitionId], false, "priority-match");
        foreach (var candidate in candidates)
        {
            var fallbackId = candidate.Spawn!.FallbackInstanceId;
            var fallback = area.Navigation.Anchors.Instances.SingleOrDefault(item => item.Id == fallbackId);
            if (fallback is null || fallback.Spawn is null || blocked.Contains(fallback.Id)) continue;
            return new(fallback, definitions[fallback.DefinitionId], true, "fallback");
        }
        throw new InvalidOperationException("anchor.spawn.unavailable");
    }

    public static PortalTravelResolution ResolvePortal(ProjectDefinition project, AreaId sourceAreaId,
        Guid sourceAnchorInstanceId, Vector3Value incomingVelocity, float incomingYawDegrees)
    {
        ArgumentNullException.ThrowIfNull(project);
        var sourceArea = project.Areas.SingleOrDefault(item => item.Id == sourceAreaId)
            ?? throw new InvalidOperationException("anchor.portal.source_area_missing");
        var source = sourceArea.Navigation.Anchors.Instances.SingleOrDefault(item => item.Id == sourceAnchorInstanceId)
            ?? throw new InvalidOperationException("anchor.portal.source_missing");
        var portal = source.Portal ?? throw new InvalidOperationException("anchor.portal.profile_missing");
        var targetArea = project.Areas.SingleOrDefault(item => item.Id == portal.TargetAreaId)
            ?? throw new InvalidOperationException("anchor.portal.target_area_missing");
        var target = targetArea.Navigation.Anchors.Instances.SingleOrDefault(item =>
            item.Id == portal.TargetAnchorInstanceId)
            ?? throw new InvalidOperationException("anchor.portal.target_missing");
        var yaw = portal.Orientation switch
        {
            PortalOrientationPolicy.Preserve => incomingYawDegrees,
            PortalOrientationPolicy.FixedYaw => portal.FixedYawDegrees,
            PortalOrientationPolicy.AnchorRotation => QuaternionYaw(target.Transform.Rotation),
            _ => throw new InvalidOperationException("anchor.portal.orientation_invalid"),
        };
        var velocity = portal.Velocity switch
        {
            PortalVelocityPolicy.Preserve => incomingVelocity,
            PortalVelocityPolicy.Reset => Vector3Value.Zero,
            PortalVelocityPolicy.RedirectForward => new Vector3Value(
                MathF.Sin(yaw * MathF.PI / 180) * Magnitude(incomingVelocity), incomingVelocity.Y,
                MathF.Cos(yaw * MathF.PI / 180) * Magnitude(incomingVelocity)),
            _ => throw new InvalidOperationException("anchor.portal.velocity_invalid"),
        };
        return new(sourceAreaId, sourceAnchorInstanceId, targetArea.Id, target.Id, target.Transform, yaw,
            velocity, portal.Preload);
    }

    private static float Magnitude(Vector3Value value) => MathF.Sqrt(value.X * value.X + value.Z * value.Z);

    private static float QuaternionYaw(QuaternionValue value)
    {
        var siny = 2 * (value.W * value.Y + value.X * value.Z);
        var cosy = 1 - 2 * (value.Y * value.Y + value.Z * value.Z);
        return MathF.Atan2(siny, cosy) * 180 / MathF.PI;
    }
}
