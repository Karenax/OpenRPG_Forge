using Forge.Network.Authoring;
using Forge.Network.Observability;
using Forge.Network.Operations;
using Forge.Network.Persistence;
using Forge.Network.Sessions;
using Forge.Network.Storage;

namespace Forge.Network.Tests;

public sealed class NetworkPersistenceObservabilityProfileTests
{
    [Fact]
    public void R123_PersistenceAndSql_CoverTransactionsBackupsFailoverAndBoundedRepositories()
    {
        var profile = NetworkPersistenceObservabilityProfile.CreateDefault();
        Assert.Equal(2, profile.Persistence.Providers.Count);
        Assert.True(profile.Persistence.Transactions && profile.Persistence.Migrations);
        Assert.True(profile.Persistence.Backups && profile.Persistence.Failover);
        Assert.True(profile.Sql.ServerOnlyRepositories && profile.Sql.ParameterizedQueries);
        Assert.True(profile.Sql.ModuleScriptsUseDomainServices);
        Assert.False(profile.Sql.RawSqlAllowed);

        var actor = Guid.NewGuid();
        var snapshots = new InMemoryWorldSnapshotStore(TimeProvider.System);
        Assert.Equal(1, snapshots.Save(0, 1, "{\"world\":\"crypt\"}", actor).WorldRevision);
        var data = new InMemorySecureDataStore();
        var scope = Guid.NewGuid();
        var parameters = new DataParameter[]
        {
            new("scope_id", DataParameterKind.Uuid, scope),
            new("key", DataParameterKind.Text, "crypt.gate"),
            new("value", DataParameterKind.Text, "open")
        };
        Assert.True(data.Execute(new("world_flag.upsert", parameters, 0), actor).Applied);
        Assert.Throws<InvalidOperationException>(() => data.Query("SELECT * FROM world_flags", [], actor));
    }

    [Fact]
    public void R123_ObservabilityAndProfiler_ExposeBoundedOperationalSignals()
    {
        var profile = NetworkPersistenceObservabilityProfile.CreateDefault();
        Assert.Equal(2, profile.Observability.Exporters.Count);
        Assert.Contains("server.tps", profile.Observability.Metrics);
        Assert.True(profile.Observability.PrivacyFiltering);
        Assert.True(profile.Profiler.Sampling && profile.Profiler.Instrumentation && profile.Profiler.TraceCapture);
        Assert.True(profile.Profiler.ScriptSpans && profile.Profiler.DatabaseSpans && profile.Profiler.NetworkSpans);

        var metrics = new ServerMetrics();
        metrics.Gauge("server.tps", 60);
        metrics.Increment("connections.active", 2);
        Assert.Equal(60, metrics.Snapshot().Gauges["server.tps"]);
        var health = new HealthService(TimeProvider.System);
        health.Register("persistence", () => (HealthStatus.Healthy, "ready"));
        Assert.Equal(HealthStatus.Healthy, health.Check().Status);
        var profiler = new ServerProfiler(TimeProvider.System);
        using (profiler.Measure("server.tick")) { }
        Assert.Equal("server.tick", Assert.Single(profiler.Snapshot()).Operation);
    }

    [Fact]
    public void R123_Reconnection_UsesBoundExpiringSingleUseTokensAndRecoveryPolicy()
    {
        var profile = NetworkPersistenceObservabilityProfile.CreateDefault();
        Assert.True(profile.Reconnection.EntityHandoff && profile.Reconnection.PartyState);
        Assert.True(profile.Reconnection.PendingTransactions && profile.Reconnection.SafeFallback);
        var account = Guid.NewGuid();
        var session = Guid.NewGuid();
        var service = new ReconnectionService(TimeProvider.System,
            TimeSpan.FromSeconds(profile.Reconnection.GracePeriodSeconds));
        var ticket = service.Issue(account, Guid.NewGuid(), session, 17);
        Assert.True(service.Redeem(ticket.Token, account, session).Accepted);
        Assert.Equal("NET_RECONNECT_TOKEN_INVALID", service.Redeem(ticket.Token, account, session).Code);
    }

    [Fact]
    public void R123_PreviewApplyUndoRedoAndRoundTrip_AreCanonical()
    {
        var initial = NetworkPersistenceObservabilityProfile.CreateDefault();
        var candidate = initial with
        {
            ServerRevision = 1,
            Persistence = initial.Persistence with { BackupRetentionDays = 60 },
            Reconnection = initial.Reconnection with { GracePeriodSeconds = 180 }
        };
        var history = new NetworkPersistenceObservabilityHistory(initial);
        var command = new NetworkPersistenceObservabilityCommand(Guid.NewGuid(), 1, "test:r123", 0,
            "admin:karen", NetworkRole.Administrator, false, candidate);
        Assert.False(history.Execute(command).Applied);
        Assert.True(history.Execute(command with { Apply = true }).Applied);
        Assert.Equal(initial, history.Undo("admin:karen", NetworkRole.Administrator).Profile);
        Assert.Equal(candidate, history.Redo("admin:karen", NetworkRole.Administrator).Profile);
        var store = new NetworkPersistenceObservabilityStore();
        var json = store.Serialize(candidate);
        Assert.Equal(json, store.Serialize(store.Deserialize(json)));
    }

    [Fact]
    public void R123_InvalidPolicyPermissionRevisionAndMissingRuntime_ReturnLocalizedDiagnostics()
    {
        var initial = NetworkPersistenceObservabilityProfile.CreateDefault();
        var invalid = initial with
        {
            ServerRevision = 1,
            Sql = initial.Sql with { RawSqlAllowed = true }
        };
        Assert.Contains(NetworkPersistenceObservabilityValidator.Validate(invalid), item =>
            item.Code == "NET_SECURE_SQL_POLICY_INVALID");
        var rejected = new NetworkPersistenceObservabilityHistory(initial).Execute(new(Guid.NewGuid(), 1,
            "test:r123", 9, "player", NetworkRole.Player, true, invalid));
        Assert.Contains(rejected.Diagnostics, item => item.Code == "NET_PERSIST_OBSERVE_PERMISSION_DENIED");
        Assert.Contains(rejected.Diagnostics, item => item.Code == "NET_PERSIST_OBSERVE_REVISION_STALE");
        Assert.Empty(NetworkPersistenceObservabilityRuntimeAdapter.Resolve(null, false).Diagnostics);
        Assert.Equal("NET_PERSIST_OBSERVE_REQUIRED_MISSING",
            Assert.Single(NetworkPersistenceObservabilityRuntimeAdapter.Resolve(null, true).Diagnostics).Code);
        var scripts = new NetworkPersistenceObservabilityScriptService(initial);
        Assert.Equal(6, scripts.Read(scripts.Handle).MetricCount);
        Assert.Throws<InvalidOperationException>(() => scripts.Read(scripts.Handle with { Generation = 2 }));
    }
}
