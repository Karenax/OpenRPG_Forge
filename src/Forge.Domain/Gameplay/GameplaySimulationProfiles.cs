using Forge.Domain.Modules;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;

namespace Forge.Domain.Gameplay;

public sealed record FlyingMovementProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool Required,
    float MaximumSpeed,
    float Acceleration,
    float MaximumAltitude,
    float BankingDegrees,
    float Lift,
    bool RequiresLandingSurface,
    bool VolumetricNavigation,
    float LandingProbeDistance,
    string InputContext);

public enum ProjectileMode { Ballistic, Homing, Hitscan }

public sealed record ProjectileProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool Required,
    ProjectileMode Mode,
    float Speed,
    float Gravity,
    float LifetimeSeconds,
    float HomingStrength,
    float CollisionRadius,
    bool ServerAuthoritative,
    bool Prediction,
    string VfxAssetId,
    string ImpactVfxAssetId,
    string CollisionMask,
    long ServerRevision,
    string AuditTag);

public enum PhysicsBodyMode { Static, Kinematic, Rigid }
public enum PhysicsJointMode { Fixed, Hinge, Slider, Spring }

public sealed record PhysicsBodyProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Name,
    PhysicsBodyMode Mode,
    float Mass,
    float LinearDamping,
    float GravityScale,
    bool IsTrigger,
    bool InteractsWithCharacters,
    Vector3Value BoundaryMinimum,
    Vector3Value BoundaryMaximum,
    string NetworkPolicy,
    bool RequiredAtRuntime);

public sealed record PhysicsJointProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Name,
    PhysicsJointMode Mode,
    Guid BodyAId,
    Guid BodyBId,
    float BreakForce,
    bool CollisionExcluded);

public sealed record PhysicsSceneProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool Required,
    float FixedStepSeconds,
    IReadOnlyList<PhysicsBodyProfile> Bodies,
    IReadOnlyList<PhysicsJointProfile> Joints);

public enum TargetingMode { Self, Entity, Point, Direction, Area, Cone, Line, Chain }

public sealed record TargetingProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool Required,
    TargetingMode Mode,
    float MaximumRange,
    float Radius,
    float AngleDegrees,
    int MaximumTargets,
    bool RequiresLineOfSight,
    string InputAction,
    string PreviewColor,
    IReadOnlyList<string> RequiredTags,
    IReadOnlyList<string> BlockedTags);

public sealed record WorldCalendar(int DaysPerMonth, int MonthsPerYear, int HoursPerDay, int MinutesPerHour);

public sealed record WorldClockState(
    long TotalMinutes,
    float TimeScale,
    bool Paused,
    string PauseDomain,
    long Revision = 0);

public sealed record WorldSchedule(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Key,
    int MinuteOfDay,
    IReadOnlyList<int> DaysOfMonth,
    string EventName,
    string PauseDomain,
    bool ServerAuthoritative);

public sealed record WorldSimulationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool Required,
    WorldCalendar Calendar,
    WorldClockState InitialClock,
    IReadOnlyDictionary<string, float> LocalTimeScales,
    IReadOnlyList<string> PauseDomains,
    IReadOnlyList<WorldSchedule> Schedules);

public sealed record GameplaySimulationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool RequiredAtRuntime,
    FlyingMovementProfile Flight,
    ProjectileProfile Projectiles,
    PhysicsSceneProfile Physics,
    TargetingProfile Targeting,
    WorldSimulationProfile WorldTime)
{
    public const int CurrentSchemaVersion = 1;

    public static GameplaySimulationProfile CreateDefault(ModuleId moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        Guid Stable(string key) => RuntimePolicyIdFactory.Create(moduleId.Value, $"gameplay:simulation:{key}");
        const string provenance = "migration-default:r066";
        var flight = new FlyingMovementProfile(Stable("flight"), 1, provenance, true, false,
            12, 18, 80, 35, 2, true, true, 1.5f, "flight");
        var projectile = new ProjectileProfile(Stable("projectile"), 1, provenance, true, false,
            ProjectileMode.Ballistic, 24, 9.81f, 8, 10, .2f, true, true,
            "vfx:projectile-trail", "vfx:projectile-impact", "world,creature,placeable", 0,
            "gameplay.projectile");
        var bodyA = new PhysicsBodyProfile(Stable("physics:body:crate"), 1, provenance, "Caisse dynamique",
            PhysicsBodyMode.Rigid, 12, .1f, 1, false, true, new(-20, -4, -20), new(20, 40, 20),
            "server", true);
        var bodyB = new PhysicsBodyProfile(Stable("physics:body:anchor"), 1, provenance, "Ancre statique",
            PhysicsBodyMode.Static, 1, 0, 0, false, false, new(-20, -4, -20), new(20, 40, 20),
            "server", false);
        var joint = new PhysicsJointProfile(Stable("physics:joint"), 1, provenance, "Liaison de démonstration",
            PhysicsJointMode.Hinge, bodyA.Id, bodyB.Id, 250, true);
        var physics = new PhysicsSceneProfile(Stable("physics"), 1, provenance, true, false, 1f / 60f,
            [bodyA, bodyB], [joint]);
        var targeting = new TargetingProfile(Stable("targeting"), 1, provenance, true, false,
            TargetingMode.Cone, 18, 4, 65, 6, true, "target", "#55d6be",
            ["targetable"], ["dead", "hidden"]);
        var schedule = new WorldSchedule(Stable("world:schedule:dawn"), 1, provenance, "dawn-bell", 360,
            [1, 2, 3, 4, 5, 6, 7], "world.dawn", "world", true);
        var world = new WorldSimulationProfile(Stable("world"), 1, provenance, true, false,
            new WorldCalendar(28, 12, 24, 60), new WorldClockState(0, 1, false, "gameplay"),
            new Dictionary<string, float>(StringComparer.Ordinal), ["gameplay", "cinematic", "world"],
            [schedule]);
        return new GameplaySimulationProfile(Stable("root"), 1, provenance, true, false, flight,
            projectile, physics, targeting, world);
    }

    public IReadOnlyList<Guid> AllIds() =>
    [
        Id, Flight.Id, Projectiles.Id, Physics.Id, Targeting.Id, WorldTime.Id,
        .. Physics.Bodies.Select(item => item.Id),
        .. Physics.Joints.Select(item => item.Id),
        .. WorldTime.Schedules.Select(item => item.Id),
    ];
}

public sealed record GameplayVectorState(Vector3Value Position, Vector3Value Velocity);
