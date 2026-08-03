using Forge.Domain.Spatial;
namespace Forge.Domain.Gameplay;

public sealed record FlyingMovementProfile(float MaximumSpeed, float Acceleration, float MaximumAltitude,
    float BankingDegrees, float Lift, bool RequiresLandingSurface);
public enum ProjectileMode { Ballistic, Homing, Hitscan }
public sealed record ProjectileProfile(ProjectileMode Mode, float Speed, float Gravity, float LifetimeSeconds,
    float HomingStrength, float CollisionRadius, bool ServerAuthoritative);
public enum PhysicsBodyMode { Static, Kinematic, Rigid }
public sealed record PhysicsBodyProfile(PhysicsBodyMode Mode, float Mass, float LinearDamping, float GravityScale,
    bool IsTrigger, string NetworkPolicy);
public enum TargetingMode { Self, Entity, Point, Direction, Area, Cone, Line, Chain }
public sealed record TargetingProfile(TargetingMode Mode, float MaximumRange, float Radius, float AngleDegrees,
    int MaximumTargets, bool RequiresLineOfSight);
public sealed record WorldCalendar(int DaysPerMonth, int MonthsPerYear, int HoursPerDay, int MinutesPerHour);
public sealed record WorldClockState(long TotalMinutes, float TimeScale, bool Paused, string PauseDomain);
public sealed record WorldSchedule(string Id, int MinuteOfDay, IReadOnlyList<int> DaysOfMonth, string EventName);
public sealed record GameplayVectorState(Vector3Value Position, Vector3Value Velocity);
