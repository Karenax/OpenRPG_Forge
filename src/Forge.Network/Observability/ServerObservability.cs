using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Quality;

namespace Forge.Network.Observability;

public enum HealthStatus { Healthy, Degraded, Unhealthy }
public sealed record HealthReport(HealthStatus Status, IReadOnlyDictionary<string, string> Checks, DateTimeOffset CheckedAt);
public sealed record MetricSnapshot(IReadOnlyDictionary<string, long> Counters, IReadOnlyDictionary<string, double> Gauges);
public sealed record StructuredLog(
    DateTimeOffset Timestamp,
    string Level,
    string EventName,
    Guid CorrelationId,
    IReadOnlyDictionary<string, object?> Properties,
    string Category = "general",
    Guid? ObjectId = null,
    int? SourceLine = null,
    Guid? TransactionId = null);
public sealed record StructuredLogContext(
    string Category,
    Guid? ObjectId = null,
    int? SourceLine = null,
    Guid? TransactionId = null,
    string? PlayerId = null);
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

public sealed class StructuredLogger
{
    private static readonly string[] SensitiveTokens = ["password", "secret", "token", "authorization", "cookie"];
    private readonly List<StructuredLog> _entries = [];
    private readonly TimeProvider _timeProvider;
    private readonly StructuredLogPolicy _policy;

    public StructuredLogger(TimeProvider timeProvider)
        : this(timeProvider, new(StructuredLogSeverity.Trace, ["*"], true, true, true,
            PlayerPrivacyMode.Redact, 30, 100_000))
    {
    }

    public StructuredLogger(TimeProvider timeProvider, StructuredLogPolicy policy)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        if (policy.RetentionDays is < 1 or > 365 || policy.MaximumEntries is < 1 or > 1_000_000)
            throw new ArgumentException("NET_LOG_POLICY_INVALID", nameof(policy));
    }

    public StructuredLog Write(string level, string eventName, Guid correlationId, IReadOnlyDictionary<string, object?> properties)
    {
        if (!Enum.TryParse<StructuredLogSeverity>(level, ignoreCase: true, out var severity))
            throw new ArgumentException("NET_LOG_SEVERITY_INVALID", nameof(level));
        return Write(severity, eventName, correlationId, new("general"), properties);
    }

    public StructuredLog Write(StructuredLogSeverity severity, string eventName, Guid correlationId,
        StructuredLogContext context, IReadOnlyDictionary<string, object?> properties)
    {
        if (correlationId == Guid.Empty || string.IsNullOrWhiteSpace(eventName) || string.IsNullOrWhiteSpace(context.Category))
            throw new ArgumentException("NET_LOG_IDENTITY_REQUIRED");
        if (severity < _policy.MinimumSeverity) throw new InvalidOperationException("NET_LOG_SEVERITY_FILTERED");
        if (!_policy.Categories.Contains("*", StringComparer.Ordinal) &&
            !_policy.Categories.Contains(context.Category, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("NET_LOG_CATEGORY_FILTERED");
        if (context.SourceLine is < 1) throw new ArgumentOutOfRangeException(nameof(context), "NET_LOG_SOURCE_LINE_INVALID");
        var safe = properties.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(
            x => x.Key,
            x => IsSensitive(x.Key) ? (object?)"[REDACTED]" : IsPlayerIdentity(x.Key) ? ProtectPlayer(x.Value) : x.Value,
            StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(context.PlayerId)) safe["playerId"] = ProtectPlayer(context.PlayerId);
        var entry = new StructuredLog(_timeProvider.GetUtcNow(), severity.ToString(), eventName.Trim(), correlationId,
            safe, context.Category.Trim().ToLowerInvariant(), _policy.IncludeObjectIds ? context.ObjectId : null,
            _policy.IncludeSourceLines ? context.SourceLine : null,
            _policy.IncludeTransactions ? context.TransactionId : null);
        _entries.Add(entry);
        Retain(entry.Timestamp);
        return entry;
    }
    public string ToJson(StructuredLog entry) => JsonSerializer.Serialize(entry, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    public IReadOnlyList<StructuredLog> Entries => _entries;
    private static bool IsSensitive(string key) => SensitiveTokens.Any(token => key.Contains(token, StringComparison.OrdinalIgnoreCase));
    private static bool IsPlayerIdentity(string key) => key.Contains("player", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("account", StringComparison.OrdinalIgnoreCase) || key.Contains("user", StringComparison.OrdinalIgnoreCase);
    private object? ProtectPlayer(object? value)
    {
        if (_policy.PlayerPrivacy == PlayerPrivacyMode.Redact) return "[PLAYER_REDACTED]";
        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        return $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..16]}";
    }
    private void Retain(DateTimeOffset now)
    {
        var oldest = now.AddDays(-_policy.RetentionDays);
        _entries.RemoveAll(item => item.Timestamp < oldest);
        if (_entries.Count > _policy.MaximumEntries)
            _entries.RemoveRange(0, _entries.Count - _policy.MaximumEntries);
    }
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
