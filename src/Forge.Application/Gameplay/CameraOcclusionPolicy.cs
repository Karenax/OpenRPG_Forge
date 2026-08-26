using Forge.Domain.Gameplay;

namespace Forge.Application.Gameplay;

public enum CameraOccluderTreatment
{
    None,
    Fade,
    Hide,
}

public sealed record CameraOcclusionResolution(
    float CameraDistance,
    CameraOccluderTreatment OccluderTreatment);

public static class CameraOcclusionPolicy
{
    public static CameraOcclusionResolution Resolve(
        GameplayCameraProfile profile,
        float idealDistance,
        float? obstacleDistance)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!float.IsFinite(idealDistance) || idealDistance < 0)
            throw new ArgumentOutOfRangeException(nameof(idealDistance));
        if (obstacleDistance is { } obstacle && (!float.IsFinite(obstacle) || obstacle < 0))
            throw new ArgumentOutOfRangeException(nameof(obstacleDistance));
        if (!profile.CollisionAvoidance || obstacleDistance is null)
            return new(idealDistance, CameraOccluderTreatment.None);

        return profile.OcclusionStrategy switch
        {
            CameraOcclusionStrategy.Retract =>
                new(Math.Min(idealDistance, obstacleDistance.Value), CameraOccluderTreatment.None),
            CameraOcclusionStrategy.FadeOccluder =>
                new(idealDistance, CameraOccluderTreatment.Fade),
            CameraOcclusionStrategy.HideOccluder =>
                new(idealDistance, CameraOccluderTreatment.Hide),
            _ => throw new ArgumentOutOfRangeException(nameof(profile)),
        };
    }
}
