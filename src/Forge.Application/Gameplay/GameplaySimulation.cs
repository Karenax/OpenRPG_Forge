using Forge.Domain.Gameplay;
using Forge.Domain.Spatial;
namespace Forge.Application.Gameplay;

public static class GameplaySimulation
{
    public static GameplayVectorState Fly(GameplayVectorState state, FlyingMovementProfile profile,
        Vector3Value normalizedInput, float deltaSeconds)
    {
        var desired = Scale(normalizedInput, profile.MaximumSpeed); var velocity = MoveTowards(state.Velocity, desired, profile.Acceleration * deltaSeconds);
        var position = Add(state.Position, Scale(velocity, deltaSeconds)); position = new(position.X, Math.Clamp(position.Y, 0, profile.MaximumAltitude), position.Z);
        return new(position, velocity);
    }
    public static GameplayVectorState Projectile(GameplayVectorState state, ProjectileProfile profile,
        Vector3Value target, float deltaSeconds)
    {
        if (profile.Mode == ProjectileMode.Hitscan) return new(target, Vector3Value.Zero);
        var velocity = state.Velocity; if (profile.Mode == ProjectileMode.Homing) velocity = MoveTowards(velocity, Scale(Normalize(Subtract(target, state.Position)), profile.Speed), profile.HomingStrength * deltaSeconds);
        if (profile.Mode == ProjectileMode.Ballistic) velocity = new(velocity.X, velocity.Y - profile.Gravity * deltaSeconds, velocity.Z);
        return new(Add(state.Position, Scale(velocity, deltaSeconds)), velocity);
    }
    public static GameplayVectorState Physics(GameplayVectorState state, PhysicsBodyProfile profile, float deltaSeconds)
    {
        if (profile.Mode == PhysicsBodyMode.Static) return state; var factor = MathF.Max(0, 1 - profile.LinearDamping * deltaSeconds);
        var velocity = new Vector3Value(state.Velocity.X * factor, (state.Velocity.Y - 9.81f * profile.GravityScale * deltaSeconds) * factor, state.Velocity.Z * factor);
        return new(Add(state.Position, Scale(velocity, deltaSeconds)), velocity);
    }
    public static bool IsValidTarget(TargetingProfile profile, Vector3Value origin, Vector3Value target, int selectedCount)
    {
        var distance = Length(Subtract(target, origin)); return distance <= profile.MaximumRange && selectedCount <= profile.MaximumTargets && selectedCount > 0;
    }
    public static WorldClockState Advance(WorldClockState clock, float realMinutes)
    {
        if (clock.Paused) return clock; return clock with { TotalMinutes = checked(clock.TotalMinutes + (long)MathF.Floor(realMinutes * clock.TimeScale)) };
    }
    public static IReadOnlyList<WorldSchedule> Due(WorldClockState before, WorldClockState after, WorldCalendar calendar,
        IReadOnlyList<WorldSchedule> schedules)
    {
        if (after.TotalMinutes <= before.TotalMinutes) return []; var dayMinutes = calendar.HoursPerDay * calendar.MinutesPerHour;
        return schedules.Where(schedule => Enumerable.Range((int)(before.TotalMinutes / dayMinutes), (int)(after.TotalMinutes / dayMinutes - before.TotalMinutes / dayMinutes + 1)).Any(day =>
            schedule.DaysOfMonth.Contains(day % calendar.DaysPerMonth + 1) && day * dayMinutes + schedule.MinuteOfDay > before.TotalMinutes && day * dayMinutes + schedule.MinuteOfDay <= after.TotalMinutes)).ToArray();
    }
    private static Vector3Value Add(Vector3Value a, Vector3Value b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z); private static Vector3Value Subtract(Vector3Value a, Vector3Value b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z); private static Vector3Value Scale(Vector3Value a, float s) => new(a.X * s, a.Y * s, a.Z * s); private static float Length(Vector3Value a) => MathF.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z); private static Vector3Value Normalize(Vector3Value a) { var l = Length(a); return l == 0 ? Vector3Value.Zero : Scale(a, 1 / l); }
    private static Vector3Value MoveTowards(Vector3Value a, Vector3Value b, float max) { var d = Subtract(b, a); var l = Length(d); return l <= max || l == 0 ? b : Add(a, Scale(d, max / l)); }
}
