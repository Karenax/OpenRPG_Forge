using Forge.Domain.Gameplay;
using Forge.Domain.Modules;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Gameplay;

public sealed class GameplaySimulationProfileTests
{
    private static readonly ModuleId ModuleId = ModuleId.From(
        Guid.Parse("a0661000-0000-4000-8000-000000000001"));

    [Fact]
    public void R066_default_profile_has_unique_identity_and_all_five_capabilities()
    {
        var profile = GameplaySimulationProfile.CreateDefault(ModuleId);

        Assert.Empty(GameplaySimulationProfileValidator.Validate(profile));
        Assert.Equal(profile.AllIds().Count, profile.AllIds().Distinct().Count());
        Assert.True(profile.Flight.VolumetricNavigation);
        Assert.True(profile.Projectiles.Prediction);
        Assert.NotEmpty(profile.Physics.Joints);
        Assert.Equal(8, Enum.GetValues<TargetingMode>().Length);
        Assert.NotEmpty(profile.WorldTime.Schedules);
    }

    [Fact]
    public void R066_validation_reports_each_real_invalid_boundary_with_stable_paths()
    {
        var source = GameplaySimulationProfile.CreateDefault(ModuleId);
        var invalidBody = source.Physics.Bodies[0] with
        {
            BoundaryMinimum = Vector3Value.One,
            BoundaryMaximum = Vector3Value.Zero,
            NetworkPolicy = "peer",
        };
        var invalid = source with
        {
            Flight = source.Flight with { MaximumAltitude = 0 },
            Projectiles = source.Projectiles with { VfxAssetId = string.Empty },
            Physics = source.Physics with { Bodies = [invalidBody, .. source.Physics.Bodies.Skip(1)] },
            Targeting = source.Targeting with { PreviewColor = "orange" },
            WorldTime = source.WorldTime with { PauseDomains = [] },
        };

        var diagnostics = GameplaySimulationProfileValidator.Validate(invalid);

        Assert.Contains(diagnostics, item => item.Code == GameplaySimulationProfileValidator.FlightCode);
        Assert.Contains(diagnostics, item => item.Code == GameplaySimulationProfileValidator.ProjectileCode);
        Assert.Contains(diagnostics, item => item.Code == GameplaySimulationProfileValidator.PhysicsCode);
        Assert.Contains(diagnostics, item => item.Code == GameplaySimulationProfileValidator.TargetingCode);
        Assert.Contains(diagnostics, item => item.Code == GameplaySimulationProfileValidator.WorldTimeCode);
        Assert.All(diagnostics, item => Assert.Contains("gameplay/simulation", item.Path));
    }

    [Fact]
    public void R066_optional_absence_is_safe_and_required_absence_is_diagnostic()
    {
        Assert.Empty(GameplaySimulationProfileValidator.ValidateRuntime(null, false));
        Assert.Equal(GameplaySimulationProfileValidator.RequiredMissingCode,
            Assert.Single(GameplaySimulationProfileValidator.ValidateRuntime(null, true)).Code);
    }
}
