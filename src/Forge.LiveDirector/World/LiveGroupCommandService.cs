using Forge.Network.Operations;

namespace Forge.LiveDirector.World;

public sealed class LiveGroupCommandService(LiveWorldChooser chooser, AuthorizationService authorization, int selectionCap)
{
    public sealed record Selection(Guid SelectionId, IReadOnlyList<Guid> ObjectIds, string Query, string AuditEntry);
    public sealed record Movement(Guid CommandId, Guid SelectionId, Guid DestinationId, string Formation, long ExpectedRevision);
    public sealed record MovementResult(bool Applied, string Code, long Revision, IReadOnlyList<Guid> Moved, IReadOnlyDictionary<Guid, string> Failed, string PathPreview, string AuditEntry);
    public sealed record Attack(Guid CommandId, Guid SelectionId, Guid TargetId, bool FactionWarningAccepted, long ExpectedRevision);
    public sealed record AttackResult(bool Applied, string Code, long Revision, IReadOnlyList<Guid> Assigned, string ObjectivePreview, string AuditEntry);

    private readonly Dictionary<Guid, Selection> _selections = [];
    private readonly HashSet<Guid> _processed = [];
    private long _revision;

    public Selection Select(AuthorizationSubject actor, Guid selectionId, LiveWorldChooser.Query query)
    {
        if (selectionId == Guid.Empty) throw new ArgumentException("LIVE_SELECTION_ID_INVALID", nameof(selectionId));
        var ids = chooser.Search(actor, query).Take(selectionCap + 1).Select(x => x.ObjectId).ToArray();
        if (ids.Length > selectionCap) throw new InvalidOperationException("LIVE_SELECTION_CAP_EXCEEDED");
        var selection = new Selection(selectionId, ids, $"zone={query.ZoneId}:kind={query.Kind}:text={query.Text}", $"selected:count={ids.Length}:actor={actor.AccountId}");
        _selections[selectionId] = selection;
        return selection;
    }

    public MovementResult Move(AuthorizationSubject actor, Movement command, bool apply)
    {
        if (!_selections.TryGetValue(command.SelectionId, out var selection)) return Reject("LIVE_MOVEMENT_SELECTION_UNKNOWN", command);
        var destination = chooser.Find(command.DestinationId);
        if (destination is null) return Reject("LIVE_MOVEMENT_DESTINATION_UNKNOWN", command);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld, destination.ZoneId).Allowed) return Reject("LIVE_MOVEMENT_PERMISSION_DENIED", command);
        if (command.ExpectedRevision != _revision) return Reject("LIVE_MOVEMENT_REVISION_STALE", command);
        if (_processed.Contains(command.CommandId)) return Reject("LIVE_MOVEMENT_COMMAND_REPLAYED", command);
        if (command.Formation is not ("line" or "wedge" or "circle" or "free")) return Reject("LIVE_MOVEMENT_FORMATION_INVALID", command);

        var candidates = selection.ObjectIds.Select(id => chooser.Find(id)).Where(x => x is not null).Cast<LiveWorldChooser.Entry>().ToArray();
        var failed = candidates.Where(x => x.State.Contains("movement:blocked", StringComparison.Ordinal)).ToDictionary(x => x.ObjectId, _ => "LIVE_PATH_BLOCKED");
        var moved = candidates.Where(x => !failed.ContainsKey(x.ObjectId)).Select(x => x.ObjectId).ToArray();
        var path = $"path-preview:destination={destination.ObjectId}:zone={destination.ZoneId}:formation={command.Formation}:movable={moved.Length}:failed={failed.Count}";
        if (!apply) return new(false, "LIVE_MOVEMENT_PREVIEW", _revision, moved, failed, path, $"previewed:{path}:actor={actor.AccountId}");
        foreach (var entry in candidates.Where(x => moved.Contains(x.ObjectId)))
            chooser.Upsert(entry with { ZoneId = destination.ZoneId, InstanceId = destination.InstanceId, X = destination.X, Y = destination.Y, Z = destination.Z, State = "moving" });
        _processed.Add(command.CommandId);
        _revision++;
        return new(true, failed.Count == 0 ? "LIVE_MOVEMENT_APPLIED" : "LIVE_MOVEMENT_PARTIAL", _revision, moved, failed, path, $"applied:{path}:actor={actor.AccountId}:revision={_revision}");
    }

    public AttackResult AttackTarget(AuthorizationSubject actor, Attack command, bool apply)
    {
        if (!_selections.TryGetValue(command.SelectionId, out var selection)) return RejectAttack("LIVE_ATTACK_SELECTION_UNKNOWN", command);
        var target = chooser.Find(command.TargetId);
        if (target is null) return RejectAttack("LIVE_ATTACK_TARGET_UNKNOWN", command);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld, target.ZoneId).Allowed) return RejectAttack("LIVE_ATTACK_PERMISSION_DENIED", command);
        if (command.ExpectedRevision != _revision) return RejectAttack("LIVE_ATTACK_REVISION_STALE", command);
        if (_processed.Contains(command.CommandId)) return RejectAttack("LIVE_ATTACK_COMMAND_REPLAYED", command);
        if (!command.FactionWarningAccepted) return RejectAttack("LIVE_ATTACK_FACTION_WARNING_REQUIRED", command);
        var agents = selection.ObjectIds.Where(id => chooser.Find(id)?.Kind == "creature").ToArray();
        var preview = $"attack-objective:target={target.ObjectId}:agents={agents.Length}:faction-warning=accepted";
        if (!apply) return new(false, "LIVE_ATTACK_PREVIEW", _revision, agents, preview, $"previewed:{preview}:actor={actor.AccountId}");
        foreach (var id in agents)
        {
            var entry = chooser.Find(id)!;
            chooser.Upsert(entry with { State = $"attack-objective:{target.ObjectId}" });
        }
        _processed.Add(command.CommandId);
        _revision++;
        return new(true, "LIVE_ATTACK_APPLIED", _revision, agents, preview, $"applied:{preview}:actor={actor.AccountId}:revision={_revision}");
    }

    private MovementResult Reject(string code, Movement command) => new(false, code, _revision, [], new Dictionary<Guid, string>(), "no path", $"rejected:{code}:command={command.CommandId}");
    private AttackResult RejectAttack(string code, Attack command) => new(false, code, _revision, [], "no objective", $"rejected:{code}:command={command.CommandId}");
}
