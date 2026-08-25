using Forge.Domain.Areas;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Areas;

public sealed class AreaClimateProfileTests
{
    [Fact]
    public void ExteriorDefaultsProvideWindWeatherAndEmptyGovernedVolumes()
    {
        var profile = AreaEnvironmentProfile.CreateDefault(AreaKind.Exterior);

        Assert.Equal(AreaEnvironmentProfile.CurrentSchemaVersion, profile.SchemaVersion);
        Assert.True(profile.Wind.InfluencesVegetation);
        Assert.True(profile.Wind.Speed > 0);
        Assert.Equal(2, profile.Weather.States.Count);
        Assert.True(profile.Weather.ServerAuthoritative);
        Assert.True(profile.Weather.PersistState);
        Assert.Empty(profile.Water.Bodies);
        Assert.Empty(profile.Reverb.Zones);
    }

    [Fact]
    public void RejectsDuplicateIdsUnsafeResourcesAndUngovernedImpulseResponses()
    {
        var id = Guid.NewGuid();
        var body = new WaterBodyProfile(
            id, "River", WaterBodyKind.River, Vector3Value.Zero, new(10, 2, 4), 2,
            1, 0, 1, 1, 2, true, WaterQualityTier.High, string.Empty, "water.river");
        Assert.Throws<ArgumentException>(() => new WaterProfile(1, [body, body]));
        Assert.Throws<ArgumentException>(() => new WeatherStateProfile(
            Guid.NewGuid(), "Rain", WeatherKind.Rain, .5f, .8f, .5f, false,
            "../rain.tres", "rain", "wet", 10, 20, 2));
        Assert.Throws<ArgumentException>(() => new ReverbZoneProfile(
            Guid.NewGuid(), "Hall", Vector3Value.Zero, new(5, 3, 5), "hall", .5f, 1, 1,
            "audio/hall.wav", string.Empty, string.Empty, string.Empty));
    }

    [Fact]
    public void WeatherStatePreservesAuthoredPrecipitationVisuals()
    {
        var precipitation = new PrecipitationVisualProfile(
            1, 0, 0, 0, 0, 13, 18, -9.8f, 3000, 128, .025f, .56f, 2.8f, .32f, "#A8D1FF99");

        var weather = new WeatherStateProfile(
            Guid.NewGuid(), "Vertical rain", WeatherKind.Rain, .58f, .9f, .72f, false,
            string.Empty, "weather.rain", "surface.wet", 420, 720, 60, precipitation);

        Assert.Same(precipitation, weather.Precipitation);
        Assert.Equal("#a8d1ff99", weather.Precipitation.Color);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrecipitationVisualProfile(
            1, 0, 0, 0, 0, 18, 13, -9.8f, 3000, 128, .025f, .56f, 2.8f, .32f, "#a8d1ff99"));
    }
}
