using Forge.Application.Gameplay;
using Forge.Domain.Gameplay;
using Forge.Domain.Spatial;
namespace Forge.Application.Tests.Gameplay;

public sealed class GameplaySimulationTests
{
    [Fact]
    public void Flight_projectiles_physics_targeting_and_clock_are_deterministic()
    {
        var zero = new GameplayVectorState(Vector3Value.Zero, Vector3Value.Zero);
        var flight = GameplaySimulation.Fly(zero, new(10, 5, 20, 30, 1, true), new(1, 1, 0), 1);
        Assert.InRange(flight.Position.Y, 0, 20);
        var hit = GameplaySimulation.Projectile(zero, new(ProjectileMode.Hitscan, 20, 0, 1, 0, .1f, true), new(5, 0, 0), .1f); Assert.Equal(new Vector3Value(5, 0, 0), hit.Position);
        Assert.True(GameplaySimulation.IsValidTarget(new(TargetingMode.Point, 10, 0, 0, 1, false), Vector3Value.Zero, new(5, 0, 0), 1));
        var before = new WorldClockState(0, 2, false, "gameplay"); var after = GameplaySimulation.Advance(before, 30); Assert.Equal(60, after.TotalMinutes);
        Assert.Single(GameplaySimulation.Due(before, after, new(28, 12, 24, 60), [new("dawn", 60, [1], "world.dawn")]));
        Assert.True(GameplaySimulation.Physics(new(Vector3Value.Zero, new(0, 1, 0)), new(PhysicsBodyMode.Rigid, 1, .1f, 1, false, "server"), .1f).Velocity.Y < 1);
    }
}
