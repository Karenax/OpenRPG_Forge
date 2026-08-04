namespace Forge.Network.Storage;

public enum DataParameterKind { Text, Integer, Boolean, Uuid }
public sealed record DataParameter(string Name, DataParameterKind Kind, object Value);
public sealed record NamedDataCommand(string CommandId, IReadOnlyList<DataParameter> Parameters, long ExpectedRevision);
public sealed record DataCommandResult(bool Applied, string Code, long Revision, int AffectedRows, string AuditEntry);

public interface ISecureDataStore
{
    DataCommandResult Execute(NamedDataCommand command, Guid actorAccountId);
    IReadOnlyList<IReadOnlyDictionary<string, object>> Query(string queryId, IReadOnlyList<DataParameter> parameters, Guid actorAccountId);
}

public sealed class InMemorySecureDataStore : ISecureDataStore
{
    private readonly Dictionary<Guid, Dictionary<string, object>> _worldFlags = [];
    private long _revision;

    public DataCommandResult Execute(NamedDataCommand command, Guid actorAccountId)
    {
        if (actorAccountId == Guid.Empty) return Reject("NET_DATA_ACTOR_REQUIRED", command.CommandId);
        if (command.ExpectedRevision != _revision) return Reject("NET_DATA_REVISION_STALE", command.CommandId);
        if (command.CommandId != "world_flag.upsert") return Reject("NET_DATA_COMMAND_NOT_ALLOWED", command.CommandId);
        var parameters = Validate(command.Parameters, ["scope_id", "key", "value"]);
        if (parameters is null) return Reject("NET_DATA_PARAMETERS_INVALID", command.CommandId);
        var scope = (Guid)parameters["scope_id"].Value;
        var key = (string)parameters["key"].Value;
        var value = (string)parameters["value"].Value;
        if (scope == Guid.Empty || string.IsNullOrWhiteSpace(key) || key.Length > 80 || value.Length > 4000) return Reject("NET_DATA_VALUE_INVALID", command.CommandId);
        if (!_worldFlags.TryGetValue(scope, out var flags)) _worldFlags[scope] = flags = new(StringComparer.Ordinal);
        flags[key] = value;
        _revision++;
        return new(true, "NET_DATA_APPLIED", _revision, 1, $"applied:{command.CommandId}:actor={actorAccountId}:scope={scope}:revision={_revision}");
    }

    public IReadOnlyList<IReadOnlyDictionary<string, object>> Query(string queryId, IReadOnlyList<DataParameter> parameters, Guid actorAccountId)
    {
        if (actorAccountId == Guid.Empty) throw new UnauthorizedAccessException("NET_DATA_ACTOR_REQUIRED");
        if (queryId != "world_flag.by_scope") throw new InvalidOperationException("NET_DATA_QUERY_NOT_ALLOWED");
        var validated = Validate(parameters, ["scope_id"]) ?? throw new ArgumentException("NET_DATA_PARAMETERS_INVALID", nameof(parameters));
        var scope = (Guid)validated["scope_id"].Value;
        if (!_worldFlags.TryGetValue(scope, out var flags)) return [];
        return flags.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => (IReadOnlyDictionary<string, object>)new Dictionary<string, object> { ["key"] = x.Key, ["value"] = x.Value }).ToArray();
    }

    private static Dictionary<string, DataParameter>? Validate(IReadOnlyList<DataParameter> parameters, IEnumerable<string> expected)
    {
        if (parameters.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != parameters.Count) return null;
        var byName = parameters.ToDictionary(x => x.Name, StringComparer.Ordinal);
        if (!byName.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected)) return null;
        foreach (var parameter in parameters)
        {
            var valid = parameter.Kind switch
            {
                DataParameterKind.Text => parameter.Value is string,
                DataParameterKind.Integer => parameter.Value is long or int,
                DataParameterKind.Boolean => parameter.Value is bool,
                DataParameterKind.Uuid => parameter.Value is Guid,
                _ => false
            };
            if (!valid) return null;
        }
        return byName;
    }

    private DataCommandResult Reject(string code, string commandId) => new(false, code, _revision, 0, $"rejected:{code}:command={commandId}:revision={_revision}");
}
