using Forge.Application.Lifecycle;
using Forge.Domain.Lifecycle;

namespace Forge.Application.Tests.Lifecycle;

public sealed class ProjectGameplayEventBusTests
{
    [Fact]
    public void R051ConfiguredBusCoversAllFiveFamiliesAndConfiguredDeathPipeline()
    {
        var profile = ProjectGameplayEventProfile.CreateDefault(Guid.NewGuid());
        var bus = new ProjectGameplayEventBus();
        var now = DateTimeOffset.Parse("2026-08-10T00:00:00Z");
        ProjectGameplayEvent[] events =
        [
            new PlayerConnectionEvent(Guid.NewGuid(), "player", PlayerConnectionStage.Joining,
                "authenticated", EventAuthorityScope.Server, 1, now),
            new InventoryChangedEvent(Guid.NewGuid(), "player", "potion", InventoryChangeKind.Acquired,
                1, "loot", "chest", EventAuthorityScope.Server, 2, now),
            new ItemUsedEvent(Guid.NewGuid(), "player", "potion", null, "player", 1,
                "use", EventAuthorityScope.Server, 3, now),
            new EquipmentChangedEvent(Guid.NewGuid(), "player", "sword", "main-hand",
                EquipmentChangeKind.Equipped, "loadout", EventAuthorityScope.Server, 4, now),
            new ProgressionChangedEvent(Guid.NewGuid(), "player", 1, 2, "xp",
                EventAuthorityScope.Server, 5, now),
        ];

        Assert.All(events, item => Assert.True(bus.Publish(item, profile).Accepted));
        var pipeline = ProjectGameplayEventBus.CreateDeathPipeline(profile, "player", "trap", 6, now, "safe");
        Assert.Equal(Enum.GetValues<LifeCycleStage>(), pipeline.Select(item => item.Stage));
        Assert.All(pipeline, item => Assert.True(bus.Publish(item, profile).Accepted));
        Assert.Equal(10, bus.Journal.Count);

        var noRespawn = new ProjectGameplayEventProfile(profile.Id, profile.SchemaVersion, "test:r051",
            true, false, false, false, true, profile.Bindings);
        Assert.Equal([LifeCycleStage.Dying, LifeCycleStage.Dead],
            ProjectGameplayEventBus.CreateDeathPipeline(noRespawn, "player", "trap", 20, now, null)
                .Select(item => item.Stage));
    }

    [Fact]
    public void R051ConfiguredBusRejectsMissingRequiredOrWrongAuthorityWithoutJournalMutation()
    {
        var profile = ProjectGameplayEventProfile.CreateDefault(Guid.NewGuid());
        var bus = new ProjectGameplayEventBus();
        var local = new PlayerConnectionEvent(Guid.NewGuid(), "player", PlayerConnectionStage.Ready,
            "authenticated", EventAuthorityScope.Local, 1, DateTimeOffset.UnixEpoch);

        var missing = bus.Publish(local, null, required: true);
        var wrongAuthority = bus.Publish(local, profile);

        Assert.False(missing.Accepted);
        Assert.Equal(ProjectGameplayEventProfileValidator.MissingRequiredCode, missing.DiagnosticCode);
        Assert.False(wrongAuthority.Accepted);
        Assert.Equal(ProjectGameplayEventProfileValidator.BindingContractInvalidCode,
            wrongAuthority.DiagnosticCode);
        Assert.Empty(bus.Journal);
    }

    [Fact]
    public void TypedProjectEvents_AreValidatedJournaledAndVetoedAtomically()
    {
        var bus = new ProjectGameplayEventBus();
        bus.RegisterVeto(@event => @event is ItemUsedEvent used && used.ItemId == "cursed" ? "RULE_ITEM_CURSED" : null);
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var connection = new PlayerConnectionEvent(Guid.NewGuid(), "player-1", PlayerConnectionStage.Ready, "authenticated", EventAuthorityScope.Server, 1, now);
        Assert.True(bus.Publish(connection).Accepted);

        var inventory = new InventoryChangedEvent(Guid.NewGuid(), "player-1", "potion", InventoryChangeKind.Acquired, 1, "loot", "chest-1", EventAuthorityScope.Server, 2, now);
        Assert.True(bus.Publish(inventory).Accepted);
        var rejected = bus.Publish(new ItemUsedEvent(Guid.NewGuid(), "player-1", "cursed", null, null, 1, "use", EventAuthorityScope.Server, 3, now));
        Assert.False(rejected.Accepted);
        Assert.Equal("RULE_ITEM_CURSED", rejected.DiagnosticCode);
        Assert.Equal(2, bus.Journal.Count);

        Assert.True(bus.Publish(new EquipmentChangedEvent(Guid.NewGuid(), "player-1", "sword", "main-hand", EquipmentChangeKind.Equipped, "loadout", EventAuthorityScope.Server, 4, now)).Accepted);
        Assert.True(bus.Publish(new ProgressionChangedEvent(Guid.NewGuid(), "player-1", 1, 2, "xp", EventAuthorityScope.Server, 5, now)).Accepted);
    }

    [Fact]
    public void DeathPipeline_IsOrderedAndBindingHasStableValidation()
    {
        var pipeline = ProjectGameplayEventBus.CreateDeathPipeline("player-1", "trap", 10, DateTimeOffset.UnixEpoch, true, "safe-1");
        Assert.Equal(Enum.GetValues<LifeCycleStage>(), pipeline.Select(item => item.Stage));
        Assert.Equal([10L, 11L, 12L, 13L, 14L], pipeline.Select(item => item.ServerRevision));
        var binding = new EventBinding(Guid.NewGuid(), 1, "author:test", "1.0", "inventory.changed", "OnInventoryChanged", EventAuthorityScope.Server);
        Assert.Same(binding, ProjectGameplayEventBus.ValidateBinding(binding));
    }
}
