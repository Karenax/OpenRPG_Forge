using Forge.Application.Lifecycle;
using Forge.Domain.Lifecycle;

namespace Forge.Application.Tests.Lifecycle;

public sealed class CustomWorldEventBusTests
{
    [Fact]
    public void NamespacedTypedEvent_ValidatesPayloadAndNotifiesTypedSubscriber()
    {
        var bus = new CustomWorldEventBus();
        var schema = new CustomEventSchema(Guid.NewGuid(), 1, "author:test", "crypt", "braziers_lit", 2, new Dictionary<string, string> { ["count"] = "int" }, false);
        bus.Register(schema);
        CustomWorldEvent? received = null;
        bus.Subscribe(schema.Id, value => received = value);
        var published = bus.Publish(schema.Id, 2, new Dictionary<string, string> { ["count"] = "3" }, DateTimeOffset.UnixEpoch);
        Assert.Equal("crypt.braziers_lit", published.QualifiedName);
        Assert.Same(published, received);
        Assert.Equal("PROJECT_CUSTOM_EVENT_PAYLOAD_INVALID", Assert.Throws<InvalidOperationException>(() => bus.Publish(schema.Id, 1, new Dictionary<string, string>(), DateTimeOffset.UnixEpoch)).Message);
    }

    [Fact]
    public void WorldClock_UsesAreaTimezoneAndEmitsAstronomicalCrossings()
    {
        var profile = new WorldClockProfile(Guid.NewGuid(), 1, "author:test", 28, 12, 24, 60, 4, false, 0,
            new Dictionary<string, int> { ["mountain"] = 60 },
            new Dictionary<WorldAstronomicalEventKind, int> { [WorldAstronomicalEventKind.Dawn] = 360, [WorldAstronomicalEventKind.Sunset] = 1080 });
        Assert.Same(profile, CustomWorldEventBus.ValidateClock(profile));
        Assert.Equal(360, CustomWorldEventBus.LocalMinute(profile, "mountain", 300));
        Assert.Equal([WorldAstronomicalEventKind.Dawn], CustomWorldEventBus.CrossedAstronomicalEvents(profile, 299, 301, "mountain"));
    }
}
