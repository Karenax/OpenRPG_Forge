using Forge.LiveDirector.Audit;
using Forge.Network.Operations;

namespace Forge.LiveDirector.State;

public sealed class LiveVariableService(AuthorizationService authorization, LiveAuditJournal audit)
{
    public sealed record Definition(string Name, string Type, decimal? Minimum, decimal? Maximum, IReadOnlySet<string> AllowedValues);
    public sealed record Request(Guid CommandId, Guid TargetId, string Name, string Value, string Reason, long ExpectedRevision);
    public sealed record Result(bool Applied, string Code, long Revision, string? Before, string? After, string Diff);
    private readonly Dictionary<string, Definition> _schema = new(StringComparer.Ordinal);
    private readonly Dictionary<(Guid Target, string Name), string> _values = [];
    private readonly Stack<(Guid Target, string Name, string? Before, string After)> _history = [];
    private readonly HashSet<Guid> _processed = [];
    private long _revision;

    public void Define(Definition definition) => _schema.Add(definition.Name, definition);

    public Result Set(AuthorizationSubject actor, Request request, bool apply)
    {
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
        if (_history.Count == 0) return "LIVE_VARIABLE_HISTORY_EMPTY";
        var change = _history.Pop();
        if (change.Before is null) _values.Remove((change.Target, change.Name)); else _values[(change.Target, change.Name)] = change.Before;
        _revision++;
        audit.Append(actor.AccountId, "variable.rollback", [change.Target], change.After, change.Before ?? "<unset>", reason, correlationId);
        return "LIVE_VARIABLE_ROLLED_BACK";
    }

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
