using Godot;

namespace Forge.EngineAdapter.World;

public readonly record struct GodotTerrainGroundedPatrolStep(
    Vector3 Position,
    Vector3 Direction,
    bool Arrived);

public static class GodotTerrainGroundedPatrolMotion
{
    public const float ArrivalDistanceMeters = .02f;
    public const float GroundClearanceMeters = .025f;

    public static GodotTerrainGroundedPatrolStep Advance(
        Vector3 current,
        Vector3 target,
        double speedMetersPerSecond,
        double deltaSeconds,
        Func<float, float, float>? groundHeightAt)
    {
        if (!double.IsFinite(speedMetersPerSecond) || speedMetersPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(speedMetersPerSecond));
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        if (groundHeightAt is null)
            return AdvanceUnconstrained(current, target, speedMetersPerSecond, deltaSeconds);

        var horizontal = new Vector3(target.X - current.X, 0, target.Z - current.Z);
        var distance = horizontal.Length();
        if (distance <= ArrivalDistanceMeters)
        {
            var snappedTarget = Snap(target, groundHeightAt);
            return new(snappedTarget, Vector3.Zero, true);
        }

        var direction = horizontal / distance;
        var distanceStep = Math.Min(distance, speedMetersPerSecond * deltaSeconds);
        var candidate = current + direction * (float)distanceStep;
        candidate = Snap(candidate, groundHeightAt);
        return new(candidate, direction, distanceStep + ArrivalDistanceMeters >= distance);
    }

    public static Vector3 Snap(Vector3 position, Func<float, float, float> groundHeightAt)
    {
        ArgumentNullException.ThrowIfNull(groundHeightAt);
        var ground = groundHeightAt(position.X, position.Z);
        if (!float.IsFinite(ground))
            throw new InvalidDataException("The terrain sampler returned a non-finite patrol height.");
        position.Y = ground + GroundClearanceMeters;
        return position;
    }

    public static Vector3 GroundedVisualOffset(Vector3 minimum, Vector3 maximum)
    {
        if (!minimum.IsFinite() || !maximum.IsFinite() ||
            maximum.X < minimum.X || maximum.Y < minimum.Y || maximum.Z < minimum.Z)
            throw new ArgumentException("Grounded visual bounds must be finite and ordered.");
        var center = (minimum + maximum) * .5f;
        return new Vector3(-center.X, -minimum.Y, -center.Z);
    }

    private static GodotTerrainGroundedPatrolStep AdvanceUnconstrained(
        Vector3 current,
        Vector3 target,
        double speedMetersPerSecond,
        double deltaSeconds)
    {
        var displacement = target - current;
        var distance = displacement.Length();
        if (distance <= ArrivalDistanceMeters)
            return new(target, Vector3.Zero, true);

        var direction = displacement / distance;
        var distanceStep = Math.Min(distance, speedMetersPerSecond * deltaSeconds);
        return new(
            current + direction * (float)distanceStep,
            direction,
            distanceStep + ArrivalDistanceMeters >= distance);
    }
}
