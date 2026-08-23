namespace Forge.Network.Operations;

public sealed record AdministrationCommand(Guid CommandId, Guid ActorAccountId, string Kind, Guid? TargetAccountId, Guid? ZoneId, long ExpectedRevision, IReadOnlyDictionary<string, string> Arguments);
public sealed record AdministrationResult(bool Applied, string Code, long Revision, string SemanticDiff, string AuditEntry);

public sealed class AdministrationService
{
    private readonly AuthorizationService _authorization;
    private readonly IReadOnlySet<string>? _allowedCommandIds;
    private readonly int _requestsPerMinute;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<Guid, Queue<DateTimeOffset>> _requests = [];
    private readonly HashSet<Guid> _processed = [];
    private long _revision;

    public AdministrationService(AuthorizationService authorization,
        IEnumerable<string>? allowedCommandIds = null, int requestsPerMinute = 10_000,
        TimeProvider? timeProvider = null)
    {
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        if (requestsPerMinute is < 1 or > 10_000)
            throw new ArgumentOutOfRangeException(nameof(requestsPerMinute), "NET_ADMIN_RATE_LIMIT_INVALID");
        _requestsPerMinute = requestsPerMinute;
        _timeProvider = timeProvider ?? TimeProvider.System;
        if (allowedCommandIds is null) return;
        var commands = allowedCommandIds.Select(item => item.Trim()).ToArray();
        if (commands.Length == 0 || commands.Any(string.IsNullOrWhiteSpace) ||
            commands.Distinct(StringComparer.Ordinal).Count() != commands.Length)
            throw new ArgumentException("NET_ADMIN_COMMAND_POLICY_INVALID", nameof(allowedCommandIds));
        _allowedCommandIds = commands.ToHashSet(StringComparer.Ordinal);
    }

    public AdministrationResult Execute(AuthorizationSubject actor, AdministrationCommand command, bool apply)
    {
        if (command.ActorAccountId != actor.AccountId) return Reject("NET_ADMIN_ACTOR_MISMATCH", command, _revision);
        if (_allowedCommandIds is not null && !_allowedCommandIds.Contains(command.Kind))
            return Reject("NET_ADMIN_COMMAND_NOT_ALLOWED", command, _revision);
        if (!AcceptRate(actor.AccountId)) return Reject("NET_ADMIN_RATE_LIMITED", command, _revision);
        if (command.ExpectedRevision != _revision) return Reject("NET_ADMIN_REVISION_STALE", command, _revision);
        if (_processed.Contains(command.CommandId)) return Reject("NET_ADMIN_COMMAND_REPLAYED", command, _revision);
        var capability = command.Kind switch
        {
            "player.kick" or "player.ban" => NetworkCapability.ManagePlayers,
            "world.save" or "world.rollback" => NetworkCapability.ManageWorld,
            "operations.inspect" => NetworkCapability.ViewOperations,
            _ => (NetworkCapability?)null
        };
        if (capability is null) return Reject("NET_ADMIN_COMMAND_UNKNOWN", command, _revision);
        var auth = _authorization.Authorize(actor, capability.Value, command.ZoneId);
        if (!auth.Allowed) return Reject(auth.Code, command, _revision);
        var diff = $"{command.Kind}:target={command.TargetAccountId}:zone={command.ZoneId}";
        if (!apply) return new(false, "NET_ADMIN_PREVIEW", _revision, diff, $"previewed:{command.Kind}:actor={actor.AccountId}");
        _processed.Add(command.CommandId);
        _revision++;
        return new(true, "NET_ADMIN_APPLIED", _revision, diff, $"applied:{command.Kind}:actor={actor.AccountId}:revision={_revision}");
    }

    private bool AcceptRate(Guid actor)
    {
        var now = _timeProvider.GetUtcNow();
        if (!_requests.TryGetValue(actor, out var requests))
        {
            requests = new Queue<DateTimeOffset>();
            _requests.Add(actor, requests);
        }
        while (requests.TryPeek(out var occurredAt) && occurredAt <= now - TimeSpan.FromMinutes(1))
            requests.Dequeue();
        if (requests.Count >= _requestsPerMinute) return false;
        requests.Enqueue(now);
        return true;
    }

    private static AdministrationResult Reject(string code, AdministrationCommand command, long revision) => new(false, code, revision, "no changes", $"rejected:{code}:command={command.CommandId}:actor={command.ActorAccountId}:revision={revision}");
}
