using System.Text;
using Forge.Domain.Quality;

namespace Forge.Network.Observability;

public enum StructuredLogOutputFormat { Json, Text, JsonAndText }
public enum RemoteLogSinkKind { Syslog, OpenTelemetry, Webhook }

public sealed record RemoteLogSinkPolicy(Guid Id, int SchemaVersion, string Name,
    RemoteLogSinkKind Kind, string Endpoint, bool Enabled, bool Required);

public sealed record NetworkStructuredLogPolicy(Guid Id, int SchemaVersion,
    StructuredLogSeverity MinimumSeverity, IReadOnlyList<string> Categories,
    PlayerPrivacyMode PlayerPrivacy, StructuredLogOutputFormat OutputFormat,
    int RetentionDays, int MaximumEntries, int RotationBytes, int RotationFiles,
    IReadOnlyList<RemoteLogSinkPolicy> RemoteSinks);

public interface IRemoteLogSink
{
    string Name { get; }
    void Emit(StructuredLog entry, string json, string text);
}

public sealed class MemoryRemoteLogSink(string name) : IRemoteLogSink
{
    private readonly List<string> _entries = [];
    public string Name { get; } = name;
    public IReadOnlyList<string> Entries => _entries;
    public void Emit(StructuredLog entry, string json, string text) => _entries.Add(json);
}

public sealed record StructuredLogRouteResult(StructuredLog Entry, string? Json, string? Text,
    int Segment, int SegmentBytes, IReadOnlyList<string> DeliveredSinks);

public sealed class StructuredLogRouter
{
    private readonly NetworkStructuredLogPolicy _policy;
    private readonly StructuredLogger _logger;
    private readonly Dictionary<string, IRemoteLogSink> _sinks;
    private readonly Queue<int> _segments = new();
    private int _segment;
    private int _segmentBytes;

    public StructuredLogRouter(NetworkStructuredLogPolicy policy, TimeProvider timeProvider,
        IReadOnlyList<IRemoteLogSink>? sinks = null)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _logger = new(timeProvider, new(policy.MinimumSeverity, policy.Categories, true, true, true,
            policy.PlayerPrivacy, policy.RetentionDays, policy.MaximumEntries));
        _sinks = (sinks ?? []).ToDictionary(item => item.Name, StringComparer.Ordinal);
        _segments.Enqueue(0);
    }

    public int SegmentCount => _segments.Count;

    public StructuredLogRouteResult Write(StructuredLogSeverity severity, string eventName,
        Guid correlationId, StructuredLogContext context, IReadOnlyDictionary<string, object?> properties)
    {
        var entry = _logger.Write(severity, eventName, correlationId, context, properties);
        var json = _logger.ToJson(entry);
        var text = $"{entry.Timestamp:O} {entry.Level.ToUpperInvariant()} [{entry.Category}] " +
                   $"{entry.EventName} correlation={entry.CorrelationId:D}";
        var payloadBytes = Encoding.UTF8.GetByteCount(_policy.OutputFormat switch
        {
            StructuredLogOutputFormat.Json => json,
            StructuredLogOutputFormat.Text => text,
            _ => json + "\n" + text,
        });
        if (_segmentBytes > 0 && _segmentBytes + payloadBytes > _policy.RotationBytes)
        {
            _segment++;
            _segmentBytes = 0;
            _segments.Enqueue(_segment);
            while (_segments.Count > _policy.RotationFiles) _segments.Dequeue();
        }
        _segmentBytes += payloadBytes;

        var delivered = new List<string>();
        foreach (var sinkPolicy in _policy.RemoteSinks.Where(item => item.Enabled))
        {
            if (!_sinks.TryGetValue(sinkPolicy.Name, out var sink))
            {
                if (sinkPolicy.Required) throw new InvalidOperationException("NET_LOG_REQUIRED_SINK_MISSING");
                continue;
            }
            sink.Emit(entry, json, text);
            delivered.Add(sink.Name);
        }

        return new(entry,
            _policy.OutputFormat is StructuredLogOutputFormat.Json or StructuredLogOutputFormat.JsonAndText ? json : null,
            _policy.OutputFormat is StructuredLogOutputFormat.Text or StructuredLogOutputFormat.JsonAndText ? text : null,
            _segment, _segmentBytes, delivered);
    }
}
