namespace Forge.Network.Operations;

public sealed record AdministrationCommand(Guid CommandId, Guid ActorAccountId, string Kind, Guid? TargetAccountId, Guid? ZoneId, long ExpectedRevision, IReadOnlyDictionary<string, string> Arguments);
public sealed record AdministrationResult(bool Applied, string Code, long Revision, string SemanticDiff, string AuditEntry);

public sealed class AdministrationService(AuthorizationService authorization)
{
    private readonly HashSet<Guid> _processed = [];
    private long _revision;

    public AdministrationResult Execute(AuthorizationSubject actor, AdministrationCommand command, bool apply)
    {
        if (command.ActorAccountId != actor.AccountId) return Reject("NET_ADMIN_ACTOR_MISMATCH", command, _revision);
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
        var auth = authorization.Authorize(actor, capability.Value, command.ZoneId);
        if (!auth.Allowed) return Reject(auth.Code, command, _revision);
        var diff = $"{command.Kind}:target={command.TargetAccountId}:zone={command.ZoneId}";
        if (!apply) return new(false, "NET_ADMIN_PREVIEW", _revision, diff, $"previewed:{command.Kind}:actor={actor.AccountId}");
        _processed.Add(command.CommandId);
        _revision++;
        return new(true, "NET_ADMIN_APPLIED", _revision, diff, $"applied:{command.Kind}:actor={actor.AccountId}:revision={_revision}");
    }

    private static AdministrationResult Reject(string code, AdministrationCommand command, long revision) => new(false, code, revision, "no changes", $"rejected:{code}:command={command.CommandId}:actor={command.ActorAccountId}:revision={revision}");
}
