using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Domain.Gameplay;

public static class GameplaySimulationProfileValidator
{
    public const string IdentityCode = "gameplay.simulation.identity_invalid";
    public const string FlightCode = "gameplay.simulation.flight_invalid";
    public const string ProjectileCode = "gameplay.simulation.projectile_invalid";
    public const string PhysicsCode = "gameplay.simulation.physics_invalid";
    public const string PhysicsReferenceCode = "gameplay.simulation.physics_reference_invalid";
    public const string TargetingCode = "gameplay.simulation.targeting_invalid";
    public const string WorldTimeCode = "gameplay.simulation.world_time_invalid";
    public const string AreaReferenceCode = "gameplay.simulation.area_reference_missing";
    public const string RequiredMissingCode = "runtime.gameplay.simulation_required_missing";

    private static readonly HashSet<string> NetworkPolicies =
        new(["server", "owner", "local-preview"], StringComparer.Ordinal);

    public static IReadOnlyList<DomainDiagnostic> Validate(GameplaySimulationProfile profile,
        IReadOnlySet<string>? areaIds = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<DomainDiagnostic>();
        const string root = "module/runtimePolicies/gameplay/simulation";
        if (profile.Id == Guid.Empty || profile.SchemaVersion != GameplaySimulationProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance) || profile.AllIds().Any(id => id == Guid.Empty) ||
            profile.AllIds().Distinct().Count() != profile.AllIds().Count)
            diagnostics.Add(new(IdentityCode,
                "Gameplay simulation requires unique UUIDs, schema v1 and provenance.", root));

        var flight = profile.Flight;
        if (flight.SchemaVersion != 1 || string.IsNullOrWhiteSpace(flight.Provenance) ||
            flight.MaximumSpeed <= 0 || flight.Acceleration <= 0 || flight.MaximumAltitude <= 0 ||
            flight.BankingDegrees is < 0 or > 89 || flight.Lift < 0 || flight.LandingProbeDistance <= 0 ||
            string.IsNullOrWhiteSpace(flight.InputContext) || flight.Required && !flight.Enabled)
            diagnostics.Add(new(FlightCode,
                "Flight requires bounded speed, acceleration, altitude, banking, lift, landing and input.",
                $"{root}/flight/{flight.Id:D}"));

        var projectile = profile.Projectiles;
        if (projectile.SchemaVersion != 1 || string.IsNullOrWhiteSpace(projectile.Provenance) ||
            projectile.Speed <= 0 || projectile.Gravity < 0 || projectile.LifetimeSeconds <= 0 ||
            projectile.HomingStrength < 0 || projectile.CollisionRadius <= 0 ||
            string.IsNullOrWhiteSpace(projectile.VfxAssetId) || string.IsNullOrWhiteSpace(projectile.ImpactVfxAssetId) ||
            string.IsNullOrWhiteSpace(projectile.CollisionMask) || projectile.ServerRevision < 0 ||
            string.IsNullOrWhiteSpace(projectile.AuditTag) || projectile.Required && !projectile.Enabled)
            diagnostics.Add(new(ProjectileCode,
                "Projectile motion, collision, VFX, prediction and authority metadata are invalid.",
                $"{root}/projectiles/{projectile.Id:D}"));

        var physics = profile.Physics;
        if (physics.SchemaVersion != 1 || string.IsNullOrWhiteSpace(physics.Provenance) ||
            physics.FixedStepSeconds is <= 0 or > .1f || physics.Required && !physics.Enabled ||
            physics.Bodies.Any(body => body.SchemaVersion != 1 || string.IsNullOrWhiteSpace(body.Provenance) ||
                string.IsNullOrWhiteSpace(body.Name) || body.Mass <= 0 || body.LinearDamping < 0 ||
                body.GravityScale < 0 || !NetworkPolicies.Contains(body.NetworkPolicy) ||
                !BoundsValid(body.BoundaryMinimum, body.BoundaryMaximum)))
            diagnostics.Add(new(PhysicsCode,
                "Physics bodies require stable bounds, mass, damping, gravity and network policy.",
                $"{root}/physics/{physics.Id:D}"));
        var bodyIds = physics.Bodies.Select(item => item.Id).ToHashSet();
        if (physics.Joints.Any(joint => joint.SchemaVersion != 1 || string.IsNullOrWhiteSpace(joint.Provenance) ||
                string.IsNullOrWhiteSpace(joint.Name) || joint.BodyAId == joint.BodyBId || joint.BreakForce <= 0 ||
                !bodyIds.Contains(joint.BodyAId) || !bodyIds.Contains(joint.BodyBId)))
            diagnostics.Add(new(PhysicsReferenceCode,
                "Physics joints must reference two distinct existing bodies and a positive break force.",
                $"{root}/physics/{physics.Id:D}/joints"));

        var targeting = profile.Targeting;
        if (targeting.SchemaVersion != 1 || string.IsNullOrWhiteSpace(targeting.Provenance) ||
            targeting.MaximumRange <= 0 || targeting.Radius < 0 || targeting.AngleDegrees is < 0 or > 360 ||
            targeting.MaximumTargets < 1 || string.IsNullOrWhiteSpace(targeting.InputAction) ||
            !ValidColor(targeting.PreviewColor) || targeting.RequiredTags.Any(string.IsNullOrWhiteSpace) ||
            targeting.BlockedTags.Any(string.IsNullOrWhiteSpace) || targeting.Required && !targeting.Enabled)
            diagnostics.Add(new(TargetingCode,
                "Targeting mode, geometry, validity tags, preview and input adapter are invalid.",
                $"{root}/targeting/{targeting.Id:D}"));

        var world = profile.WorldTime;
        var dayMinutes = (long)world.Calendar.HoursPerDay * world.Calendar.MinutesPerHour;
        if (world.SchemaVersion != 1 || string.IsNullOrWhiteSpace(world.Provenance) ||
            world.Calendar.DaysPerMonth is < 1 or > 366 || world.Calendar.MonthsPerYear is < 1 or > 100 ||
            world.Calendar.HoursPerDay is < 1 or > 100 || world.Calendar.MinutesPerHour is < 1 or > 1_000 ||
            world.InitialClock.TotalMinutes < 0 || world.InitialClock.TimeScale < 0 ||
            string.IsNullOrWhiteSpace(world.InitialClock.PauseDomain) || world.InitialClock.Revision < 0 ||
            world.PauseDomains.Count == 0 || world.PauseDomains.Any(string.IsNullOrWhiteSpace) ||
            world.LocalTimeScales.Any(item => string.IsNullOrWhiteSpace(item.Key) ||
                !float.IsFinite(item.Value) || item.Value is < 0 or > 1_000) ||
            world.Schedules.Any(schedule => schedule.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(schedule.Provenance) || string.IsNullOrWhiteSpace(schedule.Key) ||
                schedule.MinuteOfDay < 0 || schedule.MinuteOfDay >= dayMinutes ||
                schedule.DaysOfMonth.Count == 0 || schedule.DaysOfMonth.Any(day => day < 1 ||
                    day > world.Calendar.DaysPerMonth) || string.IsNullOrWhiteSpace(schedule.EventName) ||
                !world.PauseDomains.Contains(schedule.PauseDomain, StringComparer.Ordinal)) ||
            world.Required && !world.Enabled)
            diagnostics.Add(new(WorldTimeCode,
                "World calendar, clock, local scales, pause domains or schedules are invalid.",
                $"{root}/worldTime/{world.Id:D}"));
        if (areaIds is not null && world.LocalTimeScales.Keys.Any(key => !areaIds.Contains(key)))
            diagnostics.Add(new(AreaReferenceCode,
                "World simulation local time scale references an unknown area.",
                $"{root}/worldTime/{world.Id:D}/localTimeScales"));
        return diagnostics;
    }

    public static IReadOnlyList<DomainDiagnostic> ValidateRuntime(GameplaySimulationProfile? profile,
        bool required)
    {
        if (profile is null || !profile.Enabled)
            return required
                ? [new(RequiredMissingCode, "Required gameplay simulation is unavailable.",
                    "module/runtimePolicies/gameplay/simulation")]
                : [];
        return Validate(profile);
    }

    private static bool BoundsValid(Vector3Value minimum, Vector3Value maximum) =>
        minimum.X < maximum.X && minimum.Y < maximum.Y && minimum.Z < maximum.Z;

    private static bool ValidColor(string value) => value.Length is 7 or 9 && value[0] == '#' &&
        value[1..].All(Uri.IsHexDigit);
}
