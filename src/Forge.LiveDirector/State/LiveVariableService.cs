using Forge.LiveDirector.Audit;
using Forge.Network.Operations;

namespace Forge.LiveDirector.State;

public sealed class LiveVariableService(AuthorizationService authorization, LiveAuditJournal audit)
{
    public sealed record Definition(string Name, string Type, decimal? Minimum, decimal? Maximum, IReadOnlySet<string> AllowedValues);
    public sealed record Request(Guid CommandId, Guid TargetId, string Name, string Value, string Reason, long ExpectedRevision);
    public sealed record Result(bool Applied, string Code, long Revision, string? Before, string? After, string Diff);
    public sealed record PersistedValue(Guid TargetId, string Name, string Type, string Value);
    private readonly Dictionary<string, Definition> _schema = new(StringComparer.Ordinal);
    private readonly Dictionary<(Guid Target, string Name), string> _values = [];
    private readonly Stack<(Guid Target, string Name, string? Before, string After)> _history = [];
    private readonly HashSet<Guid> _processed = [];
    private long _revision;

    public void Define(Definition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Name) ||
            definition.Type is not ("bool" or "number" or "enum" or "string"))
            throw new ArgumentException("LIVE_VARIABLE_DEFINITION_INVALID", nameof(definition));
        _schema.Add(definition.Name, definition);
    }

    public Result Set(AuthorizationSubject actor, Request request, bool apply)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld).Allowed) return Reject("LIVE_VARIABLE_PERMISSION_DENIED", request);
        if (!_schema.TryGetValue(request.Name, out var definition)) return Reject("LIVE_VARIABLE_SCHEMA_UNKNOWN", request);
        if (request.ExpectedRevision != _revision) return Reject("LIVE_VARIABLE_REVISION_STALE", request);
        if (_processed.Contains(request.CommandId)) return Reject("LIVE_VARIABLE_COMMAND_REPLAYED", request);
        if (!Valid(definition, request.Value)) return Reject("LIVE_VARIABLE_VALUE_INVALID", request);
        var before = _values.GetValueOrDefault((request.TargetId, request.Name));
        var diff = $"variable.set:target={request.TargetId}:name={request.Name}:before={before}:after={request.Value}";
        if (!apply) return new(false, "LIVE_VARIABLE_PREVIEW", _revision, before, request.Value, diff);
        _values[(request.TargetId, request.Name)] = request.Value;
        _history.Push((request.TargetId, request.Name, before, request.Value));
        _processed.Add(request.CommandId);
        _revision++;
        audit.Append(actor.AccountId, "variable.set", [request.TargetId], before ?? "<unset>", request.Value, request.Reason, request.CommandId);
        return new(true, "LIVE_VARIABLE_APPLIED", _revision, before, request.Value, diff);
    }

    public string Rollback(AuthorizationSubject actor, string reason, Guid correlationId)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld).Allowed)
            return "LIVE_VARIABLE_PERMISSION_DENIED";
        if (_history.Count == 0) return "LIVE_VARIABLE_HISTORY_EMPTY";
        var change = _history.Pop();
        if (change.Before is null) _values.Remove((change.Target, change.Name)); else _values[(change.Target, change.Name)] = change.Before;
        _revision++;
        audit.Append(actor.AccountId, "variable.rollback", [change.Target], change.After, change.Before ?? "<unset>", reason, correlationId);
        return "LIVE_VARIABLE_ROLLED_BACK";
    }

    public IReadOnlyList<PersistedValue> ExportState() => _values
        .Select(pair => new PersistedValue(pair.Key.Target, pair.Key.Name,
            _schema[pair.Key.Name].Type, pair.Value))
        .OrderBy(value => value.TargetId)
        .ThenBy(value => value.Name, StringComparer.Ordinal)
        .ToArray();

    public void RestoreState(IEnumerable<PersistedValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var restored = values.ToArray();
        foreach (var value in restored)
        {
            if (!_schema.TryGetValue(value.Name, out var definition) ||
                !string.Equals(definition.Type, value.Type, StringComparison.Ordinal) ||
                !Valid(definition, value.Value))
                throw new InvalidDataException("LIVE_VARIABLE_STATE_INVALID");
        }
        _values.Clear();
        foreach (var value in restored) _values[(value.TargetId, value.Name)] = value.Value;
        _history.Clear();
    }

    public string? Read(Guid targetId, string name) => _values.GetValueOrDefault((targetId, name));

    private static bool Valid(Definition definition, string value) => definition.Type switch
    {
        "bool" => bool.TryParse(value, out _),
        "number" => decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var number) && number >= definition.Minimum && number <= definition.Maximum,
        "enum" => definition.AllowedValues.Contains(value),
        "string" => value.Length <= 500,
        _ => false
    };
    private Result Reject(string code, Request request) => new(false, code, _revision, null, null, "no changes");
}
