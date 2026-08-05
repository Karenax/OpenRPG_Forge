using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.Rendering;

namespace Forge.EngineAdapter.Tests.Rendering;

public sealed class AreaClimatePlanBuilderTests
{
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
