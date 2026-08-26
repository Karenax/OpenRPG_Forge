using Forge.Domain.Gameplay;

namespace Forge.Application.Gameplay;

public sealed record WorldLabelPresentation(
    bool Visible,
    float UniformScale,
    float Opacity);

public static class WorldLabelPolicy
{
    public static WorldLabelPresentation Resolve(
        GameplayCameraProfile profile,
        float distance,
        bool isLocalHero,
        bool firstPerson)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!float.IsFinite(distance) || distance < 0)
            throw new ArgumentOutOfRangeException(nameof(distance));
        if (isLocalHero && firstPerson && profile.HideLocalHeroLabelInFirstPerson)
            return new(false, profile.WorldLabelNearScale, 0);
        if (distance >= profile.WorldLabelMaximumDistance)
            return new(false, profile.WorldLabelFarScale, 0);

        var scaleAmount = Math.Clamp(
            (distance - profile.WorldLabelNearDistance) /
            (profile.WorldLabelFarDistance - profile.WorldLabelNearDistance),
            0,
            1);
        var scale = profile.WorldLabelNearScale +
                    ((profile.WorldLabelFarScale - profile.WorldLabelNearScale) * scaleAmount);
        var opacity = distance <= profile.WorldLabelFarDistance
            ? 1
            : 1 - ((distance - profile.WorldLabelFarDistance) /
                   (profile.WorldLabelMaximumDistance - profile.WorldLabelFarDistance));
        return new(true, scale, Math.Clamp(opacity, 0, 1));
    }
}
