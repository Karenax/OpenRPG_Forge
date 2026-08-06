using Forge.Application.Creatures;

namespace Forge.Application.Tests.Creatures;

public sealed class CreatureEventScriptServiceTests
{
    [Fact]
    public void HandleReadsBindingsAndDispatchesBudgetedHeartbeatAndCustomEvent()
    {
        var profile = CreatureEventTestData.Complete();
        var service = new CreatureEventScriptService(profile);

        var snapshot = service.Read(service.Handle);
        var heartbeat = service.AdvanceHeartbeat(service.Handle, 2_000, true, "ash-warden");
        var custom = service.PublishCustom(service.Handle, CreatureEventTestData.CustomSchemaId,
            new Dictionary<string, string> { ["count"] = "3", ["source"] = "ash-warden" });

        Assert.Equal(profile.Id, snapshot.Handle.Id);
        Assert.Equal(6, snapshot.BindingCount);
        Assert.True(snapshot.HeartbeatEnabled);
        Assert.Equal(8, Assert.Single(heartbeat.Dispatches).BudgetMilliseconds);
        Assert.Equal("custom.crypt.braziers_lit", custom.EventName);
    }

    [Fact]
    public void UnknownHandleAndInvalidProfileAreRejectedWithStableCodes()
    {
        var service = new CreatureEventScriptService(CreatureEventTestData.Complete());
        var exception = Assert.Throws<KeyNotFoundException>(() => service.Read(
            new CreatureEventHandle(Guid.Parse("a1520000-0000-4000-8000-000000000099"), 1)));
        var profile = CreatureEventTestData.Complete();
        var invalid = new Forge.Domain.Creatures.CreatureEventProfile(profile.Id, profile.SchemaVersion,
            profile.Provenance, profile.RequiredAtRuntime, profile.CombatBoundary, profile.Inventory, profile.Rest,
            profile.Heartbeat! with { IntervalMilliseconds = 100 }, profile.CustomEvents, profile.Overrides);
        var invalidException = Assert.Throws<ArgumentException>(() => new CreatureEventScriptService(invalid));

        Assert.Contains("CREATURE_EVENT_HANDLE_INVALID", exception.Message, StringComparison.Ordinal);
        Assert.Contains("creature.events.heartbeat_interval_invalid", invalidException.Message, StringComparison.Ordinal);
    }
}
