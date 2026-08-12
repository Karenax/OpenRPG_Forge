using Forge.Network.Authoring;
using Forge.Network.Capacity;
using Forge.Network.Operations;
using Forge.Network.Persistence;

namespace Forge.Network.Tests;

public sealed class NetworkResilienceCapacityProfileTests
{
    [Fact]
    public void R124_BackupPolicyAndRollback_CoverSnapshotWalRehearsalMigrationAndPitr()
    {
        var profile = NetworkResilienceCapacityProfile.CreateDefault();
        Assert.True(profile.Backup.WriteAheadLog && profile.Backup.RestoreRehearsal);
        Assert.True(profile.Backup.Migrations && profile.Backup.PointInTimeRecovery);
        Assert.True(profile.Backup.DestructiveConfirmation);
        var actor = Guid.NewGuid();
        var store = new InMemoryWorldSnapshotStore(TimeProvider.System);
        var snapshot = store.Save(0, 2, "{\"state\":\"safe\"}", actor);
        var rollback = new RollbackService(store);
        Assert.Equal("NET_ROLLBACK_PREVIEW", rollback.Preview(snapshot.SnapshotId, 5, actor).Code);
        Assert.Equal(6, rollback.Apply(snapshot.SnapshotId, 5, actor).NewWorldRevision);
    }

    [Fact]
    public void R124_CapacityPolicyAndQueue_CoverCapsReservedSlotsLoadSheddingPositionAndMetrics()
    {
        var profile = NetworkResilienceCapacityProfile.CreateDefault();
        Assert.True(profile.Capacity.AdmissionQueue && profile.Capacity.LoadShedding);
        Assert.True(profile.Capacity.PositionVisible && profile.Capacity.MetricsEnabled);
        Assert.InRange(profile.Capacity.ReservedAdministratorSlots, 1, profile.Capacity.MaximumPlayers - 1);
        var queue = new ConnectionQueue(1, TimeProvider.System, TimeSpan.FromSeconds(profile.Capacity.ReservationSeconds));
        var first = queue.Request(Guid.NewGuid());
        Assert.True(queue.Confirm(first.Reservation!.ReservationId));
        Assert.Equal(1, queue.Request(Guid.NewGuid()).Position);
    }

    [Fact]
    public void R124_CommandHistoryRoundTripRuntimeAndScriptContracts_AreCanonical()
    {
        var initial = NetworkResilienceCapacityProfile.CreateDefault();
        var candidate = initial with { ServerRevision = 1, Capacity = initial.Capacity with { MaximumPlayers = 96 } };
        var history = new NetworkResilienceCapacityHistory(initial);
        var command = new NetworkResilienceCapacityCommand(Guid.NewGuid(), 1, "test:r124", 0,
            "admin:karen", NetworkRole.Administrator, false, candidate);
        Assert.False(history.Execute(command).Applied);
        Assert.True(history.Execute(command with { Apply = true }).Applied);
        Assert.Equal(initial, history.Undo("admin:karen", NetworkRole.Administrator).Profile);
        Assert.Equal(candidate, history.Redo("admin:karen", NetworkRole.Administrator).Profile);
        var store = new NetworkResilienceCapacityStore();
        Assert.Equal(store.Serialize(candidate), store.Serialize(store.Deserialize(store.Serialize(candidate))));
        Assert.Empty(NetworkResilienceCapacityRuntimeAdapter.Resolve(null, false).Diagnostics);
        Assert.Equal("NET_RESILIENCE_REQUIRED_MISSING", Assert.Single(NetworkResilienceCapacityRuntimeAdapter.Resolve(null, true).Diagnostics).Code);
        var scripts = new NetworkResilienceCapacityScriptService(initial);
        Assert.Equal(64, scripts.Read(scripts.Handle).MaximumPlayers);
    }

    [Fact]
    public void R124_InvalidPermissionRevisionAndPolicies_ReturnLocalizedDiagnostics()
    {
        var initial = NetworkResilienceCapacityProfile.CreateDefault();
        var invalid = initial with { ServerRevision = 1, Capacity = initial.Capacity with { ReservedAdministratorSlots = 64 } };
        Assert.Contains(NetworkResilienceCapacityValidator.Validate(invalid), item => item.Code == "NET_CAPACITY_POLICY_INVALID");
        var result = new NetworkResilienceCapacityHistory(initial).Execute(new(Guid.NewGuid(), 1, "test:r124", 9,
            "player", NetworkRole.Player, true, invalid));
        Assert.Contains(result.Diagnostics, item => item.Code == "NET_RESILIENCE_PERMISSION_DENIED");
        Assert.Contains(result.Diagnostics, item => item.Code == "NET_RESILIENCE_REVISION_STALE");
    }
}
