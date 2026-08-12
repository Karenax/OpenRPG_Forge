using Forge.Domain.Gameplay;
using Forge.Domain.Spatial;

namespace Forge.Application.Gameplay;

public sealed record FlightNavigationVolume(Vector3Value Minimum, Vector3Value Maximum,
    float LandingHeight);

public sealed record FlyingMovementResolution(GameplayVectorState State, float BankingDegrees,
    float AppliedLift, bool Landed, bool InsideNavigationVolume);

public sealed record ProjectileRuntimeState(GameplayVectorState Motion, float AgeSeconds, bool Active,
    long ServerRevision);

public sealed record ProjectileResolution(ProjectileRuntimeState State, bool Impacted,
    Vector3Value ImpactPosition, string? VfxAssetId, bool Predicted, bool AuthorityGranted,
    string AuditTag);

public sealed record PhysicsBodyRuntimeState(Guid BodyId, GameplayVectorState Motion, bool Sleeping);

public sealed record PhysicsSceneResolution(IReadOnlyList<PhysicsBodyRuntimeState> Bodies,
    IReadOnlyList<Guid> ActiveJointIds, bool AuthorityGranted, float SimulatedSeconds);

public sealed record TargetCandidate(Guid Id, Vector3Value Position, IReadOnlyList<string> Tags,
    bool Visible);

public sealed record TargetingPreview(IReadOnlyList<Vector3Value> Geometry,
    IReadOnlyList<Guid> ValidTargetIds, string Color, string InputAction, bool Valid, string Reason);

public sealed record WorldSimulationResolution(WorldClockState Clock,
    IReadOnlyList<WorldSchedule> DueSchedules, float AppliedScale, bool PausedByDomain);

public static class GameplaySimulation
{
    public static GameplayVectorState Fly(GameplayVectorState state, FlyingMovementProfile profile,
        Vector3Value normalizedInput, float deltaSeconds) =>
        FlyStep(state, profile, normalizedInput, deltaSeconds,
            new FlightNavigationVolume(new(-10_000, 0, -10_000),
                new(10_000, profile.MaximumAltitude, 10_000), 0)).State;

    public static FlyingMovementResolution FlyStep(GameplayVectorState state,
        FlyingMovementProfile profile, Vector3Value normalizedInput, float deltaSeconds,
        FlightNavigationVolume volume)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.Enabled || deltaSeconds <= 0) return new(state, 0, 0, false, true);
        var input = NormalizeOrZero(normalizedInput);
        var desired = Scale(input, profile.MaximumSpeed);
        desired = new(desired.X, desired.Y + profile.Lift * MathF.Max(0, input.Y), desired.Z);
        var velocity = MoveTowards(state.Velocity, desired, profile.Acceleration * deltaSeconds);
        var candidate = Add(state.Position, Scale(velocity, deltaSeconds));
        var maximumY = MathF.Min(profile.MaximumAltitude, volume.Maximum.Y);
        var inside = candidate.X >= volume.Minimum.X && candidate.X <= volume.Maximum.X &&
                     candidate.Z >= volume.Minimum.Z && candidate.Z <= volume.Maximum.Z;
        candidate = new(Math.Clamp(candidate.X, volume.Minimum.X, volume.Maximum.X),
            Math.Clamp(candidate.Y, volume.Minimum.Y, maximumY),
            Math.Clamp(candidate.Z, volume.Minimum.Z, volume.Maximum.Z));
        var landingRequested = profile.RequiresLandingSurface && input.Y < -.25f &&
                               candidate.Y - volume.LandingHeight <= profile.LandingProbeDistance;
        if (landingRequested)
        {
            candidate = new(candidate.X, volume.LandingHeight, candidate.Z);
            velocity = new(velocity.X, 0, velocity.Z);
        }
        var lateral = input.X;
        return new(new(candidate, velocity), lateral * profile.BankingDegrees,
            profile.Lift * MathF.Max(0, input.Y), landingRequested, inside);
    }

    public static GameplayVectorState Projectile(GameplayVectorState state, ProjectileProfile profile,
        Vector3Value target, float deltaSeconds) =>
        StepProjectile(new ProjectileRuntimeState(state, 0, true, profile.ServerRevision), profile,
            target, deltaSeconds, true).State.Motion;

    public static ProjectileResolution StepProjectile(ProjectileRuntimeState state,
        ProjectileProfile profile, Vector3Value target, float deltaSeconds, bool authorityGranted,
        Func<Vector3Value, Vector3Value, float, bool>? collision = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!state.Active || !profile.Enabled || deltaSeconds <= 0)
            return new(state, false, state.Motion.Position, null, false, authorityGranted, profile.AuditTag);
        if (profile.ServerAuthoritative && !authorityGranted)
            return new(state, false, state.Motion.Position, null, profile.Prediction, false, profile.AuditTag);

        var from = state.Motion.Position;
        var velocity = state.Motion.Velocity;
        Vector3Value to;
        if (profile.Mode == ProjectileMode.Hitscan)
        {
            to = target;
            velocity = Vector3Value.Zero;
        }
        else
        {
            if (profile.Mode == ProjectileMode.Homing)
                velocity = MoveTowards(velocity,
                    Scale(NormalizeOrZero(Subtract(target, from)), profile.Speed),
                    profile.HomingStrength * deltaSeconds);
            if (profile.Mode == ProjectileMode.Ballistic)
                velocity = new(velocity.X, velocity.Y - profile.Gravity * deltaSeconds, velocity.Z);
            to = Add(from, Scale(velocity, deltaSeconds));
        }

        var age = state.AgeSeconds + deltaSeconds;
        var impacted = profile.Mode == ProjectileMode.Hitscan ||
                       collision?.Invoke(from, to, profile.CollisionRadius) == true;
        var active = !impacted && age < profile.LifetimeSeconds;
        var revision = authorityGranted ? Math.Max(profile.ServerRevision, state.ServerRevision) + 1 :
            state.ServerRevision;
        return new(new(new(to, velocity), age, active, revision), impacted, to,
            impacted ? profile.ImpactVfxAssetId : profile.VfxAssetId, profile.Prediction,
            authorityGranted, profile.AuditTag);
    }

    public static GameplayVectorState Physics(GameplayVectorState state, PhysicsBodyProfile profile,
        float deltaSeconds)
    {
        if (profile.Mode == PhysicsBodyMode.Static) return state;
        var factor = MathF.Max(0, 1 - profile.LinearDamping * deltaSeconds);
        var velocity = new Vector3Value(state.Velocity.X * factor,
            (state.Velocity.Y - 9.81f * profile.GravityScale * deltaSeconds) * factor,
            state.Velocity.Z * factor);
        var position = Add(state.Position, Scale(velocity, deltaSeconds));
        position = Clamp(position, profile.BoundaryMinimum, profile.BoundaryMaximum);
        return new(position, velocity);
    }

    public static PhysicsSceneResolution SimulatePhysics(PhysicsSceneProfile profile,
        IReadOnlyList<PhysicsBodyRuntimeState> states, float deltaSeconds, bool authorityGranted)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(states);
        if (!profile.Enabled || deltaSeconds <= 0 || !authorityGranted &&
            profile.Bodies.Any(body => body.NetworkPolicy == "server"))
            return new(states, [], authorityGranted, 0);
        var byId = states.ToDictionary(item => item.BodyId);
        var elapsed = 0f;
        while (elapsed + profile.FixedStepSeconds <= deltaSeconds + .000001f)
        {
            foreach (var body in profile.Bodies.OrderBy(item => item.Id))
            {
                var current = byId.GetValueOrDefault(body.Id,
                    new PhysicsBodyRuntimeState(body.Id,
                        new GameplayVectorState(Vector3Value.Zero, Vector3Value.Zero), false));
                if (!current.Sleeping && !body.IsTrigger)
                    byId[body.Id] = current with
                    {
                        Motion = Physics(current.Motion, body, profile.FixedStepSeconds),
                    };
            }
            elapsed += profile.FixedStepSeconds;
        }
        return new(byId.Values.OrderBy(item => item.BodyId).ToArray(),
            profile.Joints.OrderBy(item => item.Id).Select(item => item.Id).ToArray(), true, elapsed);
    }

    public static bool IsValidTarget(TargetingProfile profile, Vector3Value origin,
        Vector3Value target, int selectedCount) =>
        Length(Subtract(target, origin)) <= profile.MaximumRange && selectedCount <= profile.MaximumTargets &&
        selectedCount > 0;

    public static TargetingPreview PreviewTargeting(TargetingProfile profile, Vector3Value origin,
        Vector3Value direction, IReadOnlyList<TargetCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(candidates);
        var forward = NormalizeOrZero(direction);
        var valid = candidates.Where(candidate => IsCandidateValid(profile, origin, forward, candidate))
            .OrderBy(candidate => Length(Subtract(candidate.Position, origin))).ThenBy(candidate => candidate.Id)
            .Take(profile.MaximumTargets).Select(candidate => candidate.Id).ToArray();
        var geometry = BuildTargetGeometry(profile, origin, forward);
        var selectionValid = profile.Mode is TargetingMode.Self or TargetingMode.Point or TargetingMode.Direction ||
                             valid.Length > 0;
        return new(geometry, valid, profile.PreviewColor, profile.InputAction, selectionValid,
            selectionValid ? "targeting.valid" : "targeting.no_valid_candidate");
    }

    public static WorldClockState Advance(WorldClockState clock, float realMinutes)
    {
        if (clock.Paused) return clock;
        return clock with
        {
            TotalMinutes = checked(clock.TotalMinutes + (long)MathF.Floor(realMinutes * clock.TimeScale)),
            Revision = clock.Revision + 1,
        };
    }

    public static WorldSimulationResolution AdvanceWorld(WorldSimulationProfile profile,
        WorldClockState clock, float realMinutes, string? areaId = null,
        IReadOnlySet<string>? pausedDomains = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var paused = clock.Paused || pausedDomains?.Contains(clock.PauseDomain) == true;
        var localScale = areaId is not null && profile.LocalTimeScales.TryGetValue(areaId, out var value)
            ? value : 1;
        var effective = clock with { TimeScale = clock.TimeScale * localScale, Paused = paused };
        var after = Advance(effective, realMinutes) with { TimeScale = clock.TimeScale, Paused = clock.Paused };
        return new(after, Due(clock, after, profile.Calendar, profile.Schedules), localScale, paused);
    }

    public static IReadOnlyList<WorldSchedule> Due(WorldClockState before, WorldClockState after,
        WorldCalendar calendar, IReadOnlyList<WorldSchedule> schedules)
    {
        if (after.TotalMinutes <= before.TotalMinutes) return [];
        var dayMinutes = calendar.HoursPerDay * calendar.MinutesPerHour;
        var firstDay = before.TotalMinutes / dayMinutes;
        var lastDay = after.TotalMinutes / dayMinutes;
        var result = new List<WorldSchedule>();
        for (var day = firstDay; day <= lastDay; day++)
        {
            var dayOfMonth = (int)(day % calendar.DaysPerMonth) + 1;
            foreach (var schedule in schedules.OrderBy(item => item.MinuteOfDay).ThenBy(item => item.Id))
            {
                var instant = checked(day * dayMinutes + schedule.MinuteOfDay);
                if (schedule.DaysOfMonth.Contains(dayOfMonth) && instant > before.TotalMinutes &&
                    instant <= after.TotalMinutes)
                    result.Add(schedule);
            }
        }
        return result;
    }

    private static bool IsCandidateValid(TargetingProfile profile, Vector3Value origin,
        Vector3Value forward, TargetCandidate candidate)
    {
        if (profile.RequiresLineOfSight && !candidate.Visible ||
            profile.RequiredTags.Any(tag => !candidate.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) ||
            profile.BlockedTags.Any(tag => candidate.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
            return false;
        var offset = Subtract(candidate.Position, origin);
        var distance = Length(offset);
        if (distance > profile.MaximumRange) return false;
        return profile.Mode switch
        {
            TargetingMode.Self => distance < .001f,
            TargetingMode.Area => distance <= profile.Radius,
            TargetingMode.Cone => AngleDegrees(forward, NormalizeOrZero(offset)) <= profile.AngleDegrees / 2,
            TargetingMode.Line => DistanceToLine(candidate.Position, origin,
                Add(origin, Scale(forward, profile.MaximumRange))) <= MathF.Max(.1f, profile.Radius),
            _ => true,
        };
    }

    private static IReadOnlyList<Vector3Value> BuildTargetGeometry(TargetingProfile profile,
        Vector3Value origin, Vector3Value forward)
    {
        if (profile.Mode == TargetingMode.Cone)
        {
            var radians = profile.AngleDegrees / 2 * MathF.PI / 180;
            var left = new Vector3Value(forward.X * MathF.Cos(radians) - forward.Z * MathF.Sin(radians), 0,
                forward.X * MathF.Sin(radians) + forward.Z * MathF.Cos(radians));
            var right = new Vector3Value(forward.X * MathF.Cos(-radians) - forward.Z * MathF.Sin(-radians), 0,
                forward.X * MathF.Sin(-radians) + forward.Z * MathF.Cos(-radians));
            return [origin, Add(origin, Scale(left, profile.MaximumRange)),
                Add(origin, Scale(right, profile.MaximumRange)), origin];
        }
        if (profile.Mode is TargetingMode.Area or TargetingMode.Point)
        {
            var center = Add(origin, Scale(forward, MathF.Min(profile.MaximumRange, 6)));
            return Enumerable.Range(0, 17).Select(index =>
            {
                var angle = index / 16f * MathF.Tau;
                return new Vector3Value(center.X + MathF.Cos(angle) * profile.Radius, center.Y,
                    center.Z + MathF.Sin(angle) * profile.Radius);
            }).ToArray();
        }
        return [origin, Add(origin, Scale(forward, profile.MaximumRange))];
    }

    private static float DistanceToLine(Vector3Value point, Vector3Value start, Vector3Value end)
    {
        var line = Subtract(end, start);
        var lengthSquared = Dot(line, line);
        if (lengthSquared <= .000001f) return Length(Subtract(point, start));
        var t = Math.Clamp(Dot(Subtract(point, start), line) / lengthSquared, 0, 1);
        return Length(Subtract(point, Add(start, Scale(line, t))));
    }

    private static float AngleDegrees(Vector3Value a, Vector3Value b) =>
        MathF.Acos(Math.Clamp(Dot(a, b), -1, 1)) * 180 / MathF.PI;

    private static Vector3Value Clamp(Vector3Value value, Vector3Value minimum, Vector3Value maximum) =>
        new(Math.Clamp(value.X, minimum.X, maximum.X), Math.Clamp(value.Y, minimum.Y, maximum.Y),
            Math.Clamp(value.Z, minimum.Z, maximum.Z));

    private static Vector3Value Add(Vector3Value a, Vector3Value b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    private static Vector3Value Subtract(Vector3Value a, Vector3Value b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    private static Vector3Value Scale(Vector3Value a, float scalar) =>
        new(a.X * scalar, a.Y * scalar, a.Z * scalar);
    private static float Dot(Vector3Value a, Vector3Value b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static float Length(Vector3Value value) => MathF.Sqrt(Dot(value, value));
    private static Vector3Value NormalizeOrZero(Vector3Value value)
    {
        var length = Length(value);
        return length <= .000001f ? Vector3Value.Zero : Scale(value, 1 / length);
    }
    private static Vector3Value MoveTowards(Vector3Value current, Vector3Value target, float maximumDelta)
    {
        var delta = Subtract(target, current);
        var length = Length(delta);
        return length <= maximumDelta || length <= .000001f
            ? target
            : Add(current, Scale(delta, maximumDelta / length));
    }
}
