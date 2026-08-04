using System.Diagnostics;
using System.Text.Json;

namespace Forge.Network.Observability;

public enum HealthStatus { Healthy, Degraded, Unhealthy }
public sealed record HealthReport(HealthStatus Status, IReadOnlyDictionary<string, string> Checks, DateTimeOffset CheckedAt);
public sealed record MetricSnapshot(IReadOnlyDictionary<string, long> Counters, IReadOnlyDictionary<string, double> Gauges);
public sealed record StructuredLog(DateTimeOffset Timestamp, string Level, string EventName, Guid CorrelationId, IReadOnlyDictionary<string, object?> Properties);
public sealed record ProfileAggregate(string Operation, long Count, double TotalMilliseconds, double MaxMilliseconds, long Failures);

public sealed class ServerMetrics
{
    private readonly Dictionary<string, long> _counters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _gauges = new(StringComparer.Ordinal);
    public void Increment(string name, long amount = 1) { ValidateName(name); _counters[name] = _counters.GetValueOrDefault(name) + amount; }
    public void Gauge(string name, double value) { ValidateName(name); if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value)); _gauges[name] = value; }
    public MetricSnapshot Snapshot() => new(new SortedDictionary<string, long>(_counters, StringComparer.Ordinal), new SortedDictionary<string, double>(_gauges, StringComparer.Ordinal));
    private static void ValidateName(string name) { if (string.IsNullOrWhiteSpace(name) || name.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '.'))) throw new ArgumentException("NET_METRIC_NAME_INVALID", nameof(name)); }
}

public sealed class HealthService(TimeProvider timeProvider)
{
    private readonly SortedDictionary<string, Func<(HealthStatus Status, string Detail)>> _checks = new(StringComparer.Ordinal);
    public void Register(string name, Func<(HealthStatus Status, string Detail)> check) { if (string.IsNullOrWhiteSpace(name) || !_checks.TryAdd(name, check)) throw new ArgumentException("NET_HEALTH_CHECK_INVALID", nameof(name)); }
    public HealthReport Check()
    {
        var results = _checks.ToDictionary(x => x.Key, x => x.Value(), StringComparer.Ordinal);
        var status = results.Count == 0 ? HealthStatus.Degraded : results.Values.Max(x => x.Status);
        return new(status, results.ToDictionary(x => x.Key, x => $"{x.Value.Status}:{x.Value.Detail}", StringComparer.Ordinal), timeProvider.GetUtcNow());
    }
}

public sealed class StructuredLogger(TimeProvider timeProvider)
{
    private static readonly string[] SensitiveTokens = ["password", "secret", "token", "authorization", "cookie"];
    private readonly List<StructuredLog> _entries = [];
    public StructuredLog Write(string level, string eventName, Guid correlationId, IReadOnlyDictionary<string, object?> properties)
    {
        if (correlationId == Guid.Empty || string.IsNullOrWhiteSpace(eventName)) throw new ArgumentException("NET_LOG_IDENTITY_REQUIRED");
        var safe = properties.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => IsSensitive(x.Key) ? (object?)"[REDACTED]" : x.Value, StringComparer.Ordinal);
        var entry = new StructuredLog(timeProvider.GetUtcNow(), level, eventName, correlationId, safe);
        _entries.Add(entry);
        return entry;
    }
    public string ToJson(StructuredLog entry) => JsonSerializer.Serialize(entry, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    public IReadOnlyList<StructuredLog> Entries => _entries;
    private static bool IsSensitive(string key) => SensitiveTokens.Any(token => key.Contains(token, StringComparison.OrdinalIgnoreCase));
}

public sealed class ServerProfiler(TimeProvider timeProvider)
{
    private readonly Dictionary<string, MutableAggregate> _aggregates = new(StringComparer.Ordinal);
    public IDisposable Measure(string operation) => new Scope(this, operation, timeProvider.GetTimestamp());
    public IReadOnlyList<ProfileAggregate> Snapshot() => _aggregates.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new ProfileAggregate(x.Key, x.Value.Count, x.Value.TotalMilliseconds, x.Value.MaxMilliseconds, x.Value.Failures)).ToArray();
    private void Complete(string operation, long started, bool failed)
    {
        var elapsed = timeProvider.GetElapsedTime(started).TotalMilliseconds;
        if (!_aggregates.TryGetValue(operation, out var aggregate)) _aggregates[operation] = aggregate = new();
        aggregate.Count++; aggregate.TotalMilliseconds += elapsed; aggregate.MaxMilliseconds = Math.Max(aggregate.MaxMilliseconds, elapsed); if (failed) aggregate.Failures++;
    }
    private sealed class MutableAggregate { public long Count; public double TotalMilliseconds; public double MaxMilliseconds; public long Failures; }
    private sealed class Scope(ServerProfiler owner, string operation, long started) : IDisposable
    {
        private bool _failed; private bool _disposed;
        public void MarkFailed() => _failed = true;
        public void Dispose() { if (_disposed) return; _disposed = true; owner.Complete(operation, started, _failed); }
    }
}
