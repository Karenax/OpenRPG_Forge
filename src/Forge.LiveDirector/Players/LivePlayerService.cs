using Forge.LiveDirector.World;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Players;

public sealed class LivePlayerService(LiveWorldChooser chooser, AuthorizationService authorization)
{
    public sealed record Player(Guid AccountId, Guid CharacterId, string Identity, string Character, Guid ZoneId, int LatencyMs, Guid? PartyId, string Permission, string VoiceStatus, IReadOnlyList<string> ModerationFlags);
    public sealed record Travel(Guid CommandId, string Mode, Guid PlayerAccountId, Guid GmObjectId, bool Confirmed, long ExpectedRevision);
    public sealed record Result(bool Applied, string Code, long Revision, IReadOnlyList<Guid> MovedObjects, string SemanticDiff, string AuditEntry);

    private readonly Dictionary<Guid, Player> _players = [];
    private readonly HashSet<Guid> _processed = [];
    private long _revision;

    public void Upsert(Player player)
    {
        if (player.AccountId == Guid.Empty || player.CharacterId == Guid.Empty || string.IsNullOrWhiteSpace(player.Identity)) throw new ArgumentException("LIVE_PLAYER_INVALID", nameof(player));
        _players[player.AccountId] = player;
    }

    public IReadOnlyList<Player> List(AuthorizationSubject actor) => authorization.Authorize(actor, NetworkCapability.ViewOperations).Allowed
        ? _players.Values.OrderBy(x => x.Identity, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.AccountId).ToArray()
        : [];

    public Result TravelToPlayer(AuthorizationSubject actor, Travel command, bool apply)
    {
        if (!_players.TryGetValue(command.PlayerAccountId, out var player)) return Reject("LIVE_PLAYER_UNKNOWN", command);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld, player.ZoneId).Allowed) return Reject("LIVE_PLAYER_TRAVEL_PERMISSION_DENIED", command);
        if (command.ExpectedRevision != _revision) return Reject("LIVE_PLAYER_TRAVEL_REVISION_STALE", command);
        if (_processed.Contains(command.CommandId)) return Reject("LIVE_PLAYER_TRAVEL_REPLAYED", command);
        if (command.Mode is not ("spectate" or "teleport-gm" or "summon-player" or "summon-party")) return Reject("LIVE_PLAYER_TRAVEL_MODE_INVALID", command);
        if (command.Mode is "summon-player" or "summon-party" && !command.Confirmed) return Reject("LIVE_PLAYER_TRAVEL_CONFIRMATION_REQUIRED", command);
        var gm = chooser.Find(command.GmObjectId);
        var character = chooser.Find(player.CharacterId);
        if (gm is null || character is null) return Reject("LIVE_PLAYER_TRAVEL_OBJECT_MISSING", command);
        var moved = command.Mode switch
        {
            "spectate" => Array.Empty<Guid>(),
            "teleport-gm" => new[] { gm.ObjectId },
            "summon-player" => new[] { character.ObjectId },
            _ => _players.Values.Where(x => x.PartyId == player.PartyId && x.PartyId is not null).Select(x => x.CharacterId).ToArray()
        };
        var diff = $"player.travel:mode={command.Mode}:player={player.AccountId}:objects={string.Join(',', moved)}";
        if (!apply) return new(false, "LIVE_PLAYER_TRAVEL_PREVIEW", _revision, moved, diff, $"previewed:{diff}:actor={actor.AccountId}");
        foreach (var id in moved)
        {
            var entry = chooser.Find(id);
            if (entry is null) continue;
            var destination = command.Mode == "teleport-gm" ? character : gm;
            chooser.Upsert(entry with { ZoneId = destination.ZoneId, InstanceId = destination.InstanceId, X = destination.X, Y = destination.Y, Z = destination.Z, State = "teleported" });
        }
        _processed.Add(command.CommandId);
        _revision++;
        return new(true, "LIVE_PLAYER_TRAVEL_APPLIED", _revision, moved, diff, $"applied:{diff}:actor={actor.AccountId}:revision={_revision}");
    }

    private Result Reject(string code, Travel command) => new(false, code, _revision, [], "no changes", $"rejected:{code}:command={command.CommandId}");
}
