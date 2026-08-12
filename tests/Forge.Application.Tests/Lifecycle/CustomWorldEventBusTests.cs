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

    [Fact]
    public void R052ConfiguredBusValidatesTypedPayloadAndClockSnapshotRoundTrips()
    {
        var moduleId = Guid.Parse("52000000-0000-4000-8000-000000000001");
        var baseline = ProjectWorldProfile.CreateDefault(moduleId);
        var schema = new CustomEventSchema(Guid.Parse("52000000-0000-4000-8000-000000000002"), 1,
            "author:r052", "crypt", "braziers_lit", 1,
            new Dictionary<string, string> { ["count"] = "int", ["source"] = "guid" }, false,
            "1.0", "OnBraziersLit", EventAuthorityScope.Server);
        var profile = new ProjectWorldProfile(moduleId, 1, "author:r052", true, false, [schema],
            baseline.Clock with { Speed = 2, InitialMinute = 350 });
        var bus = new CustomWorldEventBus();
        bus.Register(profile);

        var published = bus.Publish(schema.Id, 1, new Dictionary<string, string>
        {
            ["count"] = "3",
            ["source"] = moduleId.ToString("D"),
        }, DateTimeOffset.UnixEpoch);
        Assert.Equal("crypt.braziers_lit", published.QualifiedName);
        Assert.Throws<InvalidOperationException>(() => bus.Publish(schema.Id, 1,
            new Dictionary<string, string> { ["count"] = "three", ["source"] = "invalid" },
            DateTimeOffset.UnixEpoch));

        var snapshot = CustomWorldEventBus.CreateInitialSnapshot(profile.Clock);
        var advanced = CustomWorldEventBus.AdvanceClock(profile.Clock, snapshot, 5, string.Empty);
        Assert.Equal(360, advanced.Snapshot.TotalMinutes);
        Assert.Equal(1, advanced.Snapshot.Revision);
        Assert.Contains(WorldAstronomicalEventKind.Dawn, advanced.CrossedEvents);
    }
}
