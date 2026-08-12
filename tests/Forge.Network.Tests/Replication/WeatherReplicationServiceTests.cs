using Forge.Domain.Areas;
using Forge.Network.Replication;

namespace Forge.Network.Tests.Replication;

public sealed class WeatherReplicationServiceTests
{
    [Fact]
    public void PublishesIntegrityBoundEnvelopeAndAppliesOnlyNewerRevision()
    {
        var area = AreaId.From(Guid.Parse("a4200000-0000-4000-8000-000000000001"));
        var envelope = WeatherReplicationService.Publish(area, Guid.NewGuid(), 2, 7, .5,
            Guid.NewGuid(), 2, "weather.rain", "surface.wet", true);

        var applied = WeatherReplicationService.Apply(envelope, null);
        var stale = WeatherReplicationService.Apply(envelope, envelope);
        var tampered = WeatherReplicationService.Apply(envelope with { GameplayEffect = "surface.frozen" }, null);

        Assert.True(applied.Applied);
        Assert.Equal("NET_WEATHER_APPLIED", applied.Code);
        Assert.False(stale.Applied);
        Assert.Equal("NET_WEATHER_REVISION_STALE", stale.Code);
        Assert.Equal("NET_WEATHER_ENVELOPE_INVALID", tampered.Code);
    }

    [Fact]
    public void RejectsPublicationWithoutServerAuthority()
    {
        Assert.Throws<UnauthorizedAccessException>(() => WeatherReplicationService.Publish(
            AreaId.From(Guid.NewGuid()), Guid.NewGuid(), 1, 1, 0, Guid.NewGuid(), 1, "", "", false));
    }
}
