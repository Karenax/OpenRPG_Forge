using Forge.Application.Gameplay;
using Forge.Domain.Gameplay;
using Forge.Domain.Modules;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Gameplay;

public sealed class GameplaySimulationTests
{
    private static readonly GameplaySimulationProfile Profile = GameplaySimulationProfile.CreateDefault(
        ModuleId.From(Guid.Parse("a0662000-0000-4000-8000-000000000001")));

    [Fact]
    public void R066_flight_uses_3d_input_banking_lift_landing_and_volumetric_bounds()
    {
        var state = new GameplayVectorState(new(0, .5f, 0), Vector3Value.Zero);
        var volume = new FlightNavigationVolume(new(-4, 0, -4), new(4, 8, 4), 0);

        var flying = GameplaySimulation.FlyStep(state, Profile.Flight, new(1, 1, 0), .5f, volume);
        var landing = GameplaySimulation.FlyStep(flying.State, Profile.Flight, new(0, -1, 0), 2, volume);

        Assert.InRange(flying.State.Position.Y, 0, 8);
        Assert.True(flying.BankingDegrees > 0);
        Assert.True(flying.AppliedLift > 0);
        Assert.True(flying.InsideNavigationVolume);
        Assert.True(landing.Landed);
        Assert.Equal(0, landing.State.Position.Y);
    }

    [Fact]
    public void R066_projectiles_cover_ballistic_homing_hitscan_collision_prediction_vfx_and_authority()
    {
        var initial = new ProjectileRuntimeState(new(Vector3Value.Zero, new(10, 5, 0)), 0, true, 0);
        var ballistic = GameplaySimulation.StepProjectile(initial, Profile.Projectiles,
            new(8, 0, 0), .1f, true, (_, _, _) => true);
        var homing = GameplaySimulation.StepProjectile(initial,
            Profile.Projectiles with { Mode = ProjectileMode.Homing }, new(8, 0, 0), .1f, true);
        var hitscan = GameplaySimulation.StepProjectile(initial,
            Profile.Projectiles with { Mode = ProjectileMode.Hitscan }, new(8, 0, 0), .1f, true);
        var denied = GameplaySimulation.StepProjectile(initial, Profile.Projectiles,
            new(8, 0, 0), .1f, false);

        Assert.True(ballistic.Impacted);
        Assert.Equal(Profile.Projectiles.ImpactVfxAssetId, ballistic.VfxAssetId);
        Assert.True(ballistic.State.ServerRevision > 0);
        Assert.NotEqual(initial.Motion.Velocity, homing.State.Motion.Velocity);
        Assert.Equal(new Vector3Value(8, 0, 0), hitscan.ImpactPosition);
        Assert.False(denied.AuthorityGranted);
        Assert.Equal(initial, denied.State);
    }

    [Fact]
    public void R066_physics_steps_bodies_joints_boundaries_and_server_policy_deterministically()
    {
        var body = Profile.Physics.Bodies.Single(item => item.Mode == PhysicsBodyMode.Rigid);
        var states = new[]
        {
            new PhysicsBodyRuntimeState(body.Id,
                new GameplayVectorState(new(0, 2, 0), new(1, 0, 0)), false),
        };

        var first = GameplaySimulation.SimulatePhysics(Profile.Physics, states, .1f, true);
        var second = GameplaySimulation.SimulatePhysics(Profile.Physics, states, .1f, true);
        var denied = GameplaySimulation.SimulatePhysics(Profile.Physics, states, .1f, false);

        Assert.Equal(first.SimulatedSeconds, second.SimulatedSeconds);
        Assert.Equal(first.ActiveJointIds, second.ActiveJointIds);
        Assert.Equal(first.Bodies, second.Bodies);
        Assert.Single(first.ActiveJointIds);
        Assert.Contains(first.Bodies, item => item.BodyId == body.Id && item.Motion.Position.Y < 2);
        Assert.Equal(0, denied.SimulatedSeconds);
    }

    [Fact]
    public void R066_targeting_exposes_eight_modes_geometry_validity_and_input_preview()
    {
        var candidate = new TargetCandidate(Guid.Parse("a0662000-0000-4000-8000-000000000010"),
            new(0, 0, -3), ["targetable", "enemy"], true);

        foreach (var mode in Enum.GetValues<TargetingMode>())
        {
            var preview = GameplaySimulation.PreviewTargeting(Profile.Targeting with { Mode = mode },
                Vector3Value.Zero, new(0, 0, -1), [candidate]);
            Assert.NotEmpty(preview.Geometry);
            Assert.Equal(Profile.Targeting.InputAction, preview.InputAction);
            if (mode != TargetingMode.Self) Assert.True(preview.Valid);
        }
        var blocked = GameplaySimulation.PreviewTargeting(Profile.Targeting,
            Vector3Value.Zero, new(0, 0, -1), [candidate with { Tags = ["targetable", "dead"] }]);
        Assert.False(blocked.Valid);
    }

    [Fact]
    public void R066_world_time_supports_calendar_local_scale_pause_schedule_and_serializable_revision()
    {
        var world = Profile.WorldTime with
        {
            LocalTimeScales = new Dictionary<string, float> { ["crypt"] = 2 },
            InitialClock = new WorldClockState(350, 1, false, "gameplay", 4),
        };
        var advanced = GameplaySimulation.AdvanceWorld(world, world.InitialClock, 5, "crypt");
        var paused = GameplaySimulation.AdvanceWorld(world, advanced.Clock, 10, "crypt",
            new HashSet<string>(["gameplay"]));

        Assert.Equal(360, advanced.Clock.TotalMinutes);
        Assert.Equal(5, advanced.Clock.Revision);
        Assert.Single(advanced.DueSchedules);
        Assert.Equal(2, advanced.AppliedScale);
        Assert.Equal(advanced.Clock.TotalMinutes, paused.Clock.TotalMinutes);
        Assert.True(paused.PausedByDomain);
    }
}
