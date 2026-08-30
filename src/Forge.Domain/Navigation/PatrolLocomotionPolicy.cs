namespace Forge.Domain.Navigation;

public static class PatrolLocomotionPolicy
{
    public const double MinimumRunSpeedMetersPerSecond = 2.4;

    public static bool IsWalkingPace(double speedMetersPerSecond) =>
        double.IsFinite(speedMetersPerSecond) &&
        speedMetersPerSecond > 0 &&
        speedMetersPerSecond < MinimumRunSpeedMetersPerSecond;
}
