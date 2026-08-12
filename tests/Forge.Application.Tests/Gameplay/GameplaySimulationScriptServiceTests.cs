using Forge.Application.Gameplay;
using Forge.Domain.Gameplay;
using Forge.Domain.Modules;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Gameplay;

public sealed class GameplaySimulationScriptServiceTests
{
    [Fact]
    public void R066_typed_handles_preview_each_capability_and_enforce_authority()
    {
        var moduleId = ModuleId.From(Guid.Parse("a0663000-0000-4000-8000-000000000001"));
        var profile = GameplaySimulationProfile.CreateDefault(moduleId);
        var service = new GameplaySimulationScriptService(moduleId, profile);

        var snapshot = service.Read(service.Handle);
        var flight = service.PreviewFlight(service.Flight(service.Handle),
            new(Vector3Value.Zero, Vector3Value.Zero), new(1, 1, 0), .1f,
            new(new(-10, 0, -10), new(10, 20, 10), 0));
        var projectile = service.Launch(service.Projectiles(service.Handle),
            new(new(Vector3Value.Zero, new(1, 0, 0)), 0, true, 0), new(4, 0, 0), .1f, true);
        var physics = service.StepPhysics(service.Physics(service.Handle), [], .1f, true);
        var targeting = service.PreviewTarget(service.Targeting(service.Handle), Vector3Value.Zero,
            new(0, 0, -1), [new(Guid.NewGuid(), new(0, 0, -2), ["targetable"], true)]);
        var world = service.AdvanceWorld(service.WorldTime(service.Handle), profile.WorldTime.InitialClock,
            2, null, null, true);

        Assert.Equal(profile.Id, snapshot.Id);
        Assert.True(flight.AppliedLift > 0);
        Assert.True(projectile.AuthorityGranted);
        Assert.True(physics.AuthorityGranted);
        Assert.True(targeting.Valid);
        Assert.True(world.Clock.Revision > 0);
        Assert.Throws<InvalidOperationException>(() => service.AdvanceWorld(
            service.WorldTime(service.Handle), profile.WorldTime.InitialClock, 1, null, null, false));
        Assert.Throws<InvalidOperationException>(() => service.Read(service.Handle with { Generation = 2 }));
    }
}
