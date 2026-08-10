using Forge.Network.Events;
using Forge.Network.Observability;
using Forge.Network.Storage;
using Forge.Domain.Quality;

namespace Forge.Network.Tests;

public sealed class StorageEventsObservabilityTests
{
    [Fact]
    public void Secure_store_accepts_only_named_typed_parameterized_operations()
    {
        var store = new InMemorySecureDataStore();
        var actor = Guid.NewGuid();
        var scope = Guid.NewGuid();
        var parameters = new DataParameter[] { new("scope_id", DataParameterKind.Uuid, scope), new("key", DataParameterKind.Text, "gate.open"), new("value", DataParameterKind.Text, "true") };
        Assert.Equal("NET_DATA_COMMAND_NOT_ALLOWED", store.Execute(new("DROP TABLE world", parameters, 0), actor).Code);
        Assert.True(store.Execute(new("world_flag.upsert", parameters, 0), actor).Applied);
        var rows = store.Query("world_flag.by_scope", [new("scope_id", DataParameterKind.Uuid, scope)], actor);
        Assert.Equal("gate.open", Assert.Single(rows)["key"]);
        Assert.Throws<InvalidOperationException>(() => store.Query("SELECT * FROM secrets", [], actor));
    }

    [Fact]
    public void Event_bus_sequences_dispatches_and_rejects_replay()
    {
        var handler = new RecordingHandler("quest.updated");
        var bus = new ServerEventBus();
        bus.Subscribe(handler);
        var evt = new ServerEvent(Guid.NewGuid(), "quest.updated", Guid.NewGuid(), Guid.NewGuid(), 4, DateTimeOffset.UtcNow, new Dictionary<string, string> { ["quest"] = "ashes" });
        Assert.Equal(1, bus.Publish(evt).Sequence);
        Assert.Single(handler.Events);
        Assert.Throws<InvalidOperationException>(() => bus.Publish(evt));
        Assert.Single(bus.Since(0));
    }

    [Fact]
    public void Health_and_metrics_are_deterministic_and_bounded()
    {
        var metrics = new ServerMetrics();
        metrics.Increment("network.connections");
        metrics.Gauge("network.tick_ms", 12.5);
        Assert.Equal(1, metrics.Snapshot().Counters["network.connections"]);
        var health = new HealthService(TimeProvider.System);
        health.Register("content", () => (HealthStatus.Healthy, "matched"));
        health.Register("storage", () => (HealthStatus.Degraded, "memory"));
        Assert.Equal(HealthStatus.Degraded, health.Check().Status);
    }

    [Fact]
    public void Structured_logs_redact_secrets_and_keep_correlation()
    {
        var logger = new StructuredLogger(TimeProvider.System);
        var correlation = Guid.NewGuid();
        var entry = logger.Write("Information", "client.connected", correlation, new Dictionary<string, object?> { ["account"] = "elyra", ["accessToken"] = "do-not-log" });
        Assert.Equal("[REDACTED]", entry.Properties["accessToken"]);
        var json = logger.ToJson(entry);
        Assert.Contains(correlation.ToString(), json);
        Assert.DoesNotContain("do-not-log", json);
    }

    [Fact]
    public void Structured_logs_enforce_category_context_privacy_and_retention()
    {
        var clock = new ManualTimeProvider();
        var policy = new StructuredLogPolicy(StructuredLogSeverity.Information, ["runtime"], true, true, true,
            PlayerPrivacyMode.Hash, 1, 2);
        var logger = new StructuredLogger(clock, policy);
        var objectId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var context = new StructuredLogContext("runtime", objectId, 42, transactionId, "player-elyra");

        logger.Write(StructuredLogSeverity.Information, "runtime.started", Guid.NewGuid(), context,
            new Dictionary<string, object?> { ["accountName"] = "elyra", ["accessToken"] = "hidden" });
        logger.Write(StructuredLogSeverity.Warning, "runtime.warning", Guid.NewGuid(), context,
            new Dictionary<string, object?>());
        var latest = logger.Write(StructuredLogSeverity.Error, "runtime.failed", Guid.NewGuid(), context,
            new Dictionary<string, object?>());

        Assert.Equal(2, logger.Entries.Count);
        Assert.Equal(objectId, latest.ObjectId);
        Assert.Equal(42, latest.SourceLine);
        Assert.Equal(transactionId, latest.TransactionId);
        Assert.StartsWith("sha256:", Assert.IsType<string>(latest.Properties["playerId"]));
        Assert.Throws<InvalidOperationException>(() => logger.Write(StructuredLogSeverity.Debug, "runtime.debug",
            Guid.NewGuid(), context, new Dictionary<string, object?>()));
        Assert.Throws<InvalidOperationException>(() => logger.Write(StructuredLogSeverity.Information, "build.started",
            Guid.NewGuid(), context with { Category = "build" }, new Dictionary<string, object?>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => logger.Write(StructuredLogSeverity.Information, "runtime.bad",
            Guid.NewGuid(), context with { SourceLine = 0 }, new Dictionary<string, object?>()));
    }

    [Fact]
    public void Profiler_aggregates_scopes_without_exposing_payloads()
    {
        var clock = new ManualTimeProvider();
        var profiler = new ServerProfiler(clock);
        using (profiler.Measure("network.tick")) clock.Advance(TimeSpan.FromMilliseconds(4));
        using (profiler.Measure("network.tick")) clock.Advance(TimeSpan.FromMilliseconds(6));
        var aggregate = Assert.Single(profiler.Snapshot());
        Assert.Equal(2, aggregate.Count);
        Assert.Equal(10, aggregate.TotalMilliseconds);
        Assert.Equal(6, aggregate.MaxMilliseconds);
    }

    private sealed class RecordingHandler(string kind) : IServerEventHandler
    {
        public string EventKind => kind;
        public List<ServerEvent> Events { get; } = [];
        public void Handle(ServerEvent serverEvent) => Events.Add(serverEvent);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => _timestamp;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddMilliseconds(_timestamp);
        public void Advance(TimeSpan duration) => _timestamp += (long)duration.TotalMilliseconds;
    }
}
