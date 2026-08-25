using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.Rendering;
using Godot;

namespace Forge.EngineAdapter.Tests.Rendering;

public sealed class AreaClimatePlanBuilderTests
{
    [Fact]
    public void ProceduralRainFallsVerticallyWithoutWindSpread()
    {
        var wind = new WindProfile(1, 3.5f, .8f, 2.2f, .4f, 8, true, true, "wind", 0, []);

        var rain = GodotClimateAdapter.DescribePrecipitationMotion(WeatherKind.Rain, wind);
        var snow = GodotClimateAdapter.DescribePrecipitationMotion(WeatherKind.Snow, wind);

        Assert.Equal(Vector3.Down, rain.Direction);
        Assert.Equal(0, rain.SpreadDegrees);
        Assert.Equal(0, rain.Gravity.X);
        Assert.Equal(0, rain.Gravity.Z);
        Assert.True(rain.MinimumVelocity >= 16 && rain.MaximumVelocity >= rain.MinimumVelocity);
        Assert.NotEqual(0, snow.Direction.X);
        Assert.True(snow.SpreadDegrees > 0);
    }

    [Fact]
    public void AuthoredRainMotionCanIgnoreWindAndUseModuleSpeeds()
    {
        var wind = new WindProfile(1, 1, .5f, 8, 2, 1, true, true, "wind", 0, []);
        var visual = new PrecipitationVisualProfile(
            1, 0, 0, 0, 0, 13, 18, -9.8f, 3000, 128, .025f, .56f, 2.8f, .32f, "#a8d1ff99");

        var motion = GodotClimateAdapter.DescribePrecipitationMotion(WeatherKind.Rain, wind, visual);

        Assert.Equal(Vector3.Down, motion.Direction);
        Assert.Equal(-9.8f, motion.Gravity.Y);
        Assert.Equal(13, motion.MinimumVelocity);
        Assert.Equal(18, motion.MaximumVelocity);
    }

    [Fact]
    public void ResolvesLocalWindReverbAndMobileWaterBudget()
    {
        var source = AreaEnvironmentProfile.CreateDefault(AreaKind.Exterior);
        var windZone = new WindZoneProfile(Guid.NewGuid(), Vector3Value.Zero, 5, -1, 0, 8, 2);
        var body = new WaterBodyProfile(
            Guid.NewGuid(), "Lake", WaterBodyKind.Plane, Vector3Value.Zero, new(20, 2, 15), 2,
            0, 1, 0, 1, 2, true, WaterQualityTier.Ultra, string.Empty, "water.lake");
        var reverb = new ReverbZoneProfile(
            Guid.NewGuid(), "Cave", Vector3Value.Zero, new(10, 5, 10), "cave", .65f, 1, 1,
            string.Empty, string.Empty, string.Empty, "test");
        var profile = new AreaEnvironmentProfile(
            3, source.Classification, source.Tags, source.Lighting, source.Fog, source.Audio,
            source.Rendering, source.Sky,
            new(1, 1, 0, 2, .2f, 4, true, true, "wind", 0, [windZone]),
            source.Weather,
            new(1, [body]),
            new(1, "neutral", [reverb]));

        var plan = AreaClimatePlanBuilder.Build(
            profile, profile.Weather.InitialStateId, Vector3Value.Zero, "mobile");

        Assert.Equal(windZone.Id, plan.ActiveWindZone?.Id);
        Assert.Equal(reverb.Id, plan.ActiveReverbZone?.Id);
        Assert.Equal(WaterQualityTier.Medium, plan.EffectiveWaterQuality);
        Assert.Contains(plan.Diagnostics, item => item.Code == AreaClimatePlanBuilder.WaterQualityDowngradedCode);
    }
}
