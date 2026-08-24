using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Network.Observability;
using Forge.Network.Operations;
using Forge.Network.Persistence;
using Forge.Network.Sessions;
using Forge.Network.Storage;

namespace Forge.Network.Authoring;

public enum PersistenceProviderKind { Embedded, Relational }
public enum MetricsExporterKind { Prometheus, OpenTelemetry }

public sealed record WorldPersistencePolicy(Guid Id, IReadOnlyList<PersistenceProviderKind> Providers,
    bool Transactions, bool Migrations, bool Backups, bool Failover, int BackupRetentionDays);
public sealed record SecureSqlPolicy(Guid Id, bool ServerOnlyRepositories, bool ParameterizedQueries,
    IReadOnlyList<string> AllowedOperationIds, bool ModuleScriptsUseDomainServices, bool RawSqlAllowed);
public sealed record ServerObservabilityPolicy(Guid Id, IReadOnlyList<MetricsExporterKind> Exporters,
    IReadOnlyList<string> Metrics, string HealthEndpoint, string MetricsEndpoint, bool PrivacyFiltering);
public sealed record ServerProfilerPolicy(Guid Id, bool Sampling, bool Instrumentation, bool TraceCapture,
    bool TickBreakdown, bool ScriptSpans, bool DatabaseSpans, bool NetworkSpans, int MaximumCapturedSpans);
public sealed record ReconnectionPolicy(Guid Id, int GracePeriodSeconds, bool SessionTokens,
    bool EntityHandoff, bool PartyState, bool PendingTransactions, bool SafeFallback, bool SingleUseTokens);

public sealed record NetworkPersistenceObservabilityProfile(Guid Id, int SchemaVersion, string Provenance,
    NetworkAuthorityScope AuthorityScope, long ServerRevision, string AuditIdentity, bool RequiredAtRuntime,
    WorldPersistencePolicy Persistence, SecureSqlPolicy Sql, ServerObservabilityPolicy Observability,
    ServerProfilerPolicy Profiler, ReconnectionPolicy Reconnection)
{
    public static NetworkPersistenceObservabilityProfile CreateDefault(string provenance = "openrpg:r123") => new(
        Guid.Parse("a1230000-0000-4000-8000-000000000001"), 1, provenance,
        NetworkAuthorityScope.Server, 0, "network.operator", false,
        new(Guid.Parse("a1230000-0000-4000-8000-000000000002"),
            [PersistenceProviderKind.Embedded, PersistenceProviderKind.Relational], true, true, true, true, 30),
        new(Guid.Parse("a1230000-0000-4000-8000-000000000003"), true, true,
            ["world_flag.upsert", "world_flag.by_scope"], true, false),
        new(Guid.Parse("a1230000-0000-4000-8000-000000000004"),
            [MetricsExporterKind.Prometheus, MetricsExporterKind.OpenTelemetry],
            ["server.tps", "server.tick.ms", "process.memory", "connections.active", "queues.pending", "scripts.hotspots"],
            "/health", "/metrics", true),
        new(Guid.Parse("a1230000-0000-4000-8000-000000000005"), true, true, true, true,
            true, true, true, 100_000),
        new(Guid.Parse("a1230000-0000-4000-8000-000000000006"), 120, true, true, true, true, true, true));
}

public static class NetworkPersistenceObservabilityValidator
{
    public static IReadOnlyList<NetworkDiagnostic> Validate(NetworkPersistenceObservabilityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<NetworkDiagnostic>();
        if (profile.Persistence is null || profile.Sql is null || profile.Observability is null ||
            profile.Profiler is null || profile.Reconnection is null || profile.Persistence.Providers is null ||
            profile.Sql.AllowedOperationIds is null || profile.Observability.Exporters is null ||
            profile.Observability.Metrics is null)
        {
            Add("NET_PERSIST_OBSERVE_INCOMPLETE", "/network/persistenceObservability", profile.Id,
                "The persistence and observability document is incomplete.", diagnostics);
            return diagnostics;
        }
        var ids = new[] { profile.Id, profile.Persistence.Id, profile.Sql.Id, profile.Observability.Id,
            profile.Profiler.Id, profile.Reconnection.Id };
        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            Add("NET_PERSIST_OBSERVE_UUID_INVALID", "/network/persistenceObservability", profile.Id,
                "Profile UUIDs must be non-empty and unique.", diagnostics);
        if (profile.SchemaVersion != 1)
            Add("NET_PERSIST_OBSERVE_SCHEMA_UNSUPPORTED", "/network/persistenceObservability/schemaVersion",
                profile.Id, "Only schema v1 is supported.", diagnostics);
        if (string.IsNullOrWhiteSpace(profile.Provenance) || profile.AuthorityScope != NetworkAuthorityScope.Server ||
            profile.ServerRevision < 0 || string.IsNullOrWhiteSpace(profile.AuditIdentity))
            Add("NET_PERSIST_OBSERVE_IDENTITY_INVALID", "/network/persistenceObservability/identity", profile.Id,
                "Provenance, server scope, revision and audit identity are required.", diagnostics);
        if (profile.Persistence.Providers.Count != 2 || profile.Persistence.Providers.Distinct().Count() != 2 ||
            !profile.Persistence.Transactions || !profile.Persistence.Migrations || !profile.Persistence.Backups ||
            !profile.Persistence.Failover || profile.Persistence.BackupRetentionDays is < 1 or > 3_650)
            Add("NET_WORLD_PERSISTENCE_POLICY_INVALID", "/network/persistenceObservability/persistence",
                profile.Persistence.Id, "Embedded/relational providers, transactions, migrations, backups and failover are required.", diagnostics);
        if (!profile.Sql.ServerOnlyRepositories || !profile.Sql.ParameterizedQueries ||
            profile.Sql.AllowedOperationIds.Count == 0 || profile.Sql.AllowedOperationIds.Any(string.IsNullOrWhiteSpace) ||
            profile.Sql.AllowedOperationIds.Distinct(StringComparer.Ordinal).Count() != profile.Sql.AllowedOperationIds.Count ||
            !profile.Sql.ModuleScriptsUseDomainServices || profile.Sql.RawSqlAllowed)
            Add("NET_SECURE_SQL_POLICY_INVALID", "/network/persistenceObservability/sql", profile.Sql.Id,
                "Server-only parameterized allowlisted repositories without raw script SQL are required.", diagnostics);
        if (profile.Observability.Exporters.Count != 2 || profile.Observability.Exporters.Distinct().Count() != 2 ||
            profile.Observability.Metrics.Count < 6 || profile.Observability.Metrics.Any(string.IsNullOrWhiteSpace) ||
            !profile.Observability.HealthEndpoint.StartsWith("/", StringComparison.Ordinal) ||
            !profile.Observability.MetricsEndpoint.StartsWith("/", StringComparison.Ordinal) ||
            !profile.Observability.PrivacyFiltering)
            Add("NET_OBSERVABILITY_POLICY_INVALID", "/network/persistenceObservability/observability",
                profile.Observability.Id, "Prometheus/OpenTelemetry metrics, endpoints and privacy filtering are required.", diagnostics);
        if (!profile.Profiler.Sampling || !profile.Profiler.Instrumentation || !profile.Profiler.TraceCapture ||
            !profile.Profiler.TickBreakdown || !profile.Profiler.ScriptSpans || !profile.Profiler.DatabaseSpans ||
            !profile.Profiler.NetworkSpans || profile.Profiler.MaximumCapturedSpans is < 100 or > 10_000_000)
            Add("NET_SERVER_PROFILER_POLICY_INVALID", "/network/persistenceObservability/profiler", profile.Profiler.Id,
                "Sampling, instrumentation, traces, breakdown and bounded script/DB/network spans are required.", diagnostics);
        if (profile.Reconnection.GracePeriodSeconds is < 5 or > 3_600 || !profile.Reconnection.SessionTokens ||
            !profile.Reconnection.EntityHandoff || !profile.Reconnection.PartyState ||
            !profile.Reconnection.PendingTransactions || !profile.Reconnection.SafeFallback ||
            !profile.Reconnection.SingleUseTokens)
            Add("NET_RECONNECTION_POLICY_INVALID", "/network/persistenceObservability/reconnection",
                profile.Reconnection.Id, "Grace, tokens, entity/party/transaction recovery and safe fallback are required.", diagnostics);
        return diagnostics;
    }

    private static void Add(string code, string path, Guid id, string message,
        ICollection<NetworkDiagnostic> diagnostics) => diagnostics.Add(new(code, path, message, id));
}

public sealed record NetworkPersistenceObservabilityCommand(Guid CommandId, int SchemaVersion, string Provenance,
    long ExpectedRevision, string ActorId, NetworkRole Role, bool Apply,
    NetworkPersistenceObservabilityProfile Candidate);
public sealed record NetworkPersistenceObservabilityResult(bool Succeeded, bool Applied,
    NetworkPersistenceObservabilityProfile Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics,
    string SemanticDiff, string AuditEntry);

public sealed class NetworkPersistenceObservabilityHistory
{
    private readonly Stack<NetworkPersistenceObservabilityProfile> _undo = [];
    private readonly Stack<NetworkPersistenceObservabilityProfile> _redo = [];
    public NetworkPersistenceObservabilityHistory(NetworkPersistenceObservabilityProfile initial)
    {
        if (NetworkPersistenceObservabilityValidator.Validate(initial).Count != 0)
            throw new ArgumentException("NET_PERSIST_OBSERVE_INITIAL_INVALID", nameof(initial));
        Current = initial;
    }
    public NetworkPersistenceObservabilityProfile Current { get; private set; }

    public NetworkPersistenceObservabilityResult Execute(NetworkPersistenceObservabilityCommand command)
    {
        var diagnostics = NetworkPersistenceObservabilityValidator.Validate(command.Candidate).ToList();
        if (command.CommandId == Guid.Empty || command.SchemaVersion != 1 || string.IsNullOrWhiteSpace(command.Provenance))
            diagnostics.Add(new("NET_PERSIST_OBSERVE_COMMAND_INVALID", "/network/persistenceObservability/command",
                "Command UUID, schema and provenance are required.", command.CommandId));
        if (string.IsNullOrWhiteSpace(command.ActorId) || command.Role != NetworkRole.Administrator)
            diagnostics.Add(new("NET_PERSIST_OBSERVE_PERMISSION_DENIED", "/network/persistenceObservability/audit/actor",
                "Administrator identity is required.", command.Candidate.Id));
        if (command.ExpectedRevision != Current.ServerRevision || command.Candidate.ServerRevision != Current.ServerRevision + 1)
            diagnostics.Add(new("NET_PERSIST_OBSERVE_REVISION_STALE", "/network/persistenceObservability/serverRevision",
                "Candidate must advance the current revision exactly once.", command.Candidate.Id));
        var diff = Diff(Current, command.Candidate);
        if (diagnostics.Count != 0) return Result(false, false, Current, diagnostics, diff, "rejected", command.ActorId);
        if (!command.Apply) return Result(true, false, Current, [], diff, "previewed", command.ActorId);
        _undo.Push(Current); Current = command.Candidate; _redo.Clear();
        return Result(true, true, Current, [], diff, "applied", command.ActorId);
    }

    public NetworkPersistenceObservabilityResult Undo(string actor, NetworkRole role) => Move(_undo, _redo, actor, role, "undo");
    public NetworkPersistenceObservabilityResult Redo(string actor, NetworkRole role) => Move(_redo, _undo, actor, role, "redo");
    private NetworkPersistenceObservabilityResult Move(Stack<NetworkPersistenceObservabilityProfile> source,
        Stack<NetworkPersistenceObservabilityProfile> destination, string actor, NetworkRole role, string action)
    {
        if (source.Count == 0 || role != NetworkRole.Administrator)
            return Result(false, false, Current,
                [new("NET_PERSIST_OBSERVE_HISTORY_DENIED", "/network/persistenceObservability/history",
                    "History action is unavailable or forbidden.", Current.Id)], "no changes", "rejected", actor);
        var previous = Current; destination.Push(previous); Current = source.Pop();
        return Result(true, true, Current, [], Diff(previous, Current), $"compensated:{action}", actor);
    }
    private NetworkPersistenceObservabilityResult Result(bool success, bool applied,
        NetworkPersistenceObservabilityProfile profile, IReadOnlyList<NetworkDiagnostic> diagnostics, string diff,
        string action, string actor) => new(success, applied, profile, diagnostics, diff,
        $"{action}:network.persistence-observability:actor={actor}:revision={profile.ServerRevision}");
    private static string Diff(NetworkPersistenceObservabilityProfile before, NetworkPersistenceObservabilityProfile after)
    {
        var changes = new List<string>();
        if (before.Persistence != after.Persistence) changes.Add("persistence");
        if (before.Sql != after.Sql) changes.Add("sql");
        if (before.Observability != after.Observability) changes.Add("observability");
        if (before.Profiler != after.Profiler) changes.Add("profiler");
        if (before.Reconnection != after.Reconnection) changes.Add("reconnection");
        return changes.Count == 0 ? "revision-only" : string.Join(',', changes);
    }
}

public sealed class NetworkPersistenceObservabilityStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    public string Serialize(NetworkPersistenceObservabilityProfile profile)
    {
        var diagnostics = NetworkPersistenceObservabilityValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return JsonSerializer.Serialize(profile, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }
    public NetworkPersistenceObservabilityProfile Deserialize(string json)
    {
        var profile = JsonSerializer.Deserialize<NetworkPersistenceObservabilityProfile>(json, Options)
            ?? throw new InvalidDataException("NET_PERSIST_OBSERVE_EMPTY");
        var diagnostics = NetworkPersistenceObservabilityValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return profile;
    }
}

public sealed record NetworkPersistenceObservabilityResolution(bool Available,
    NetworkPersistenceObservabilityProfile? Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics);

public sealed record NetworkPersistenceObservabilityRuntime(
    IWorldSnapshotStore Snapshots,
    ConfiguredSecureDataService Data,
    ConfiguredObservabilityService Observability,
    ConfiguredServerProfiler Profiler,
    ReconnectionService Reconnection,
    NetworkPersistenceObservabilityScriptService Scripts);

public sealed class ConfiguredSecureDataService(SecureSqlPolicy policy, ISecureDataStore store)
{
    public DataCommandResult Execute(NamedDataCommand command, Guid actorAccountId)
    {
        if (!policy.AllowedOperationIds.Contains(command.CommandId, StringComparer.Ordinal))
            return new(false, "NET_DATA_OPERATION_NOT_CONFIGURED", command.ExpectedRevision, 0,
                $"rejected:NET_DATA_OPERATION_NOT_CONFIGURED:command={command.CommandId}:actor={actorAccountId}");
        return store.Execute(command, actorAccountId);
    }

    public IReadOnlyList<IReadOnlyDictionary<string, object>> Query(string queryId,
        IReadOnlyList<DataParameter> parameters, Guid actorAccountId)
    {
        if (!policy.AllowedOperationIds.Contains(queryId, StringComparer.Ordinal))
            throw new InvalidOperationException("NET_DATA_OPERATION_NOT_CONFIGURED");
        return store.Query(queryId, parameters, actorAccountId);
    }
}

public sealed class ConfiguredObservabilityService
{
    private readonly ServerObservabilityPolicy _policy;
    private readonly ServerMetrics _metrics = new();
    private readonly HealthService _health;

    public ConfiguredObservabilityService(ServerObservabilityPolicy policy, TimeProvider timeProvider)
    {
        _policy = policy;
        _health = new(timeProvider);
        _health.Register("persistence", () => (HealthStatus.Healthy, "ready"));
        _health.Register("metrics", () => (HealthStatus.Healthy, "ready"));
    }

    public string HealthEndpoint => _policy.HealthEndpoint;
    public string MetricsEndpoint => _policy.MetricsEndpoint;
    public IReadOnlyList<MetricsExporterKind> Exporters => _policy.Exporters;

    public void Gauge(string name, double value)
    {
        EnsureMetric(name);
        _metrics.Gauge(name, value);
    }

    public void Increment(string name, long amount = 1)
    {
        EnsureMetric(name);
        _metrics.Increment(name, amount);
    }

    public MetricSnapshot Snapshot() => _metrics.Snapshot();
    public HealthReport Health() => _health.Check();

    private void EnsureMetric(string name)
    {
        if (!_policy.Metrics.Contains(name, StringComparer.Ordinal))
            throw new InvalidOperationException("NET_METRIC_NOT_CONFIGURED");
    }
}

public sealed class ConfiguredServerProfiler(ServerProfilerPolicy policy, TimeProvider timeProvider)
{
    private readonly ServerProfiler _profiler = new(timeProvider);
    private int _captures;

    public IDisposable Measure(string operation)
    {
        if (_captures >= policy.MaximumCapturedSpans)
            throw new InvalidOperationException("NET_PROFILER_CAPTURE_LIMIT");
        _captures++;
        return _profiler.Measure(operation);
    }

    public IReadOnlyList<ProfileAggregate> Snapshot() => _profiler.Snapshot();
    public int Captures => _captures;
}

public static class NetworkPersistenceObservabilityRuntimeAdapter
{
    public static NetworkPersistenceObservabilityResolution Resolve(NetworkPersistenceObservabilityProfile? profile,
        bool required)
    {
        if (profile is null) return required
            ? new(false, null, [new("NET_PERSIST_OBSERVE_REQUIRED_MISSING", "/network/persistenceObservability",
                "Required persistence and observability profile is missing.")]) : new(false, null, []);
        var diagnostics = NetworkPersistenceObservabilityValidator.Validate(profile);
        return new(diagnostics.Count == 0, diagnostics.Count == 0 ? profile : null, diagnostics);
    }

    public static NetworkPersistenceObservabilityRuntime Build(NetworkPersistenceObservabilityProfile profile,
        TimeProvider? timeProvider = null)
    {
        var resolution = Resolve(profile, profile.RequiredAtRuntime);
        if (!resolution.Available) throw new InvalidOperationException(resolution.Diagnostics[0].Code);
        var clock = timeProvider ?? TimeProvider.System;
        return new(new InMemoryWorldSnapshotStore(clock),
            new ConfiguredSecureDataService(profile.Sql, new InMemorySecureDataStore()),
            new ConfiguredObservabilityService(profile.Observability, clock),
            new ConfiguredServerProfiler(profile.Profiler, clock),
            new ReconnectionService(clock, TimeSpan.FromSeconds(profile.Reconnection.GracePeriodSeconds)),
            new NetworkPersistenceObservabilityScriptService(profile));
    }
}
public readonly record struct NetworkPersistenceObservabilityHandle(Guid ProfileId, int Generation);
public sealed record NetworkPersistenceObservabilitySnapshot(Guid Id, long Revision, int ProviderCount,
    int MetricCount, int MaximumSpans, int ReconnectionGraceSeconds);
public sealed class NetworkPersistenceObservabilityScriptService(NetworkPersistenceObservabilityProfile profile)
{
    public NetworkPersistenceObservabilityHandle Handle { get; } = new(profile.Id, 1);
    public NetworkPersistenceObservabilitySnapshot Read(NetworkPersistenceObservabilityHandle handle)
    {
        if (handle.ProfileId != profile.Id || handle.Generation != 1)
            throw new InvalidOperationException("NET_PERSIST_OBSERVE_HANDLE_INVALID");
        return new(profile.Id, profile.ServerRevision, profile.Persistence.Providers.Count,
            profile.Observability.Metrics.Count, profile.Profiler.MaximumCapturedSpans,
            profile.Reconnection.GracePeriodSeconds);
    }
}
