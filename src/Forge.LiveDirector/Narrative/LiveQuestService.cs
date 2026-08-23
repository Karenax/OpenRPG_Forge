using Forge.LiveDirector.Audit;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Narrative;

public sealed class LiveQuestService(AuthorizationService authorization, LiveAuditJournal audit)
{
    public sealed record Quest(Guid QuestId, string Name, string State,
        IReadOnlyDictionary<string, IReadOnlySet<string>> Transitions);
    public sealed record Request(Guid CommandId, Guid QuestId, string TargetState,
        string PlayerScope, IReadOnlyList<Guid> PlayerAccountIds, string Reason,
        long ExpectedRevision);
    public sealed record Result(bool Applied, string Code, long Revision, string Before,
        string After, IReadOnlyList<string> Consequences, IReadOnlyList<Guid> AffectedPlayers,
        string RpgProjection, bool Restored, string AuditEntry);

    private sealed record Change(Quest Before, IReadOnlyDictionary<Guid, string?> PlayerStates);

    private readonly Dictionary<Guid, Quest> _quests = [];
    private readonly Dictionary<(Guid Quest, Guid Player), string> _playerStates = [];
    private readonly Stack<Change> _history = [];
    private readonly HashSet<Guid> _processed = [];
    private long _revision;

    public void Upsert(Quest quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (quest.QuestId == Guid.Empty || string.IsNullOrWhiteSpace(quest.Name) ||
            string.IsNullOrWhiteSpace(quest.State) || quest.Transitions is null)
            throw new ArgumentException("LIVE_QUEST_INVALID", nameof(quest));
        _quests[quest.QuestId] = quest;
    }

    public Quest? Inspect(Guid questId) => _quests.GetValueOrDefault(questId);

    public string? InspectPlayerState(Guid questId, Guid playerAccountId) =>
        _playerStates.GetValueOrDefault((questId, playerAccountId)) ??
        _quests.GetValueOrDefault(questId)?.State;

    public Result Transition(AuthorizationSubject actor, Request request, bool apply)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld).Allowed)
            return Reject("LIVE_QUEST_PERMISSION_DENIED", request);
        if (!_quests.TryGetValue(request.QuestId, out var quest))
            return Reject("LIVE_QUEST_UNKNOWN", request);
        if (request.CommandId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason))
            return Reject("LIVE_QUEST_COMMAND_INVALID", request);
        if (request.ExpectedRevision != _revision)
            return Reject("LIVE_QUEST_REVISION_STALE", request);
        if (_processed.Contains(request.CommandId))
            return Reject("LIVE_QUEST_COMMAND_REPLAYED", request);
        var selected = request.PlayerScope is "selected" or "selected-players";
        var all = request.PlayerScope is "all" or "all-players";
        if (!all && !selected || selected && request.PlayerAccountIds.Count == 0 ||
            request.PlayerAccountIds.Any(id => id == Guid.Empty))
            return Reject("LIVE_QUEST_PLAYER_SCOPE_INVALID", request);
        if (!quest.Transitions.TryGetValue(quest.State, out var allowed) ||
            !allowed.Contains(request.TargetState))
            return Reject("LIVE_QUEST_TRANSITION_INVALID", request);
        var affected = request.PlayerAccountIds.Distinct().Order().ToArray();
        var scope = selected ? "selected-players" : "all-players";
        var consequences = new[]
        {
            $"state:{quest.State}->{request.TargetState}",
            $"players:{scope}:{affected.Length}",
            "scripts:authoritative",
            "journal:updated"
        };
        var projection =
            $"rpg-kit:quest-journal:quest={quest.QuestId}:scope={scope}:players={affected.Length}:state={request.TargetState}";
        if (!apply)
            return new(false, "LIVE_QUEST_PREVIEW", _revision, quest.State,
                request.TargetState, consequences, affected, projection, false,
                $"previewed:quest={quest.QuestId}:actor={actor.AccountId}");
        var beforePlayers = affected.ToDictionary(player => player,
            player => _playerStates.GetValueOrDefault((quest.QuestId, player)));
        _history.Push(new(quest, beforePlayers));
        _quests[quest.QuestId] = quest with { State = request.TargetState };
        foreach (var player in affected)
            _playerStates[(quest.QuestId, player)] = request.TargetState;
        _processed.Add(request.CommandId);
        _revision++;
        var entry = audit.Append(actor.AccountId, "quest.transition", [quest.QuestId],
            quest.State, request.TargetState, request.Reason, request.CommandId);
        return new(true, "LIVE_QUEST_APPLIED", _revision, quest.State, request.TargetState,
            consequences, affected, projection, false, entry.Hash);
    }

    public Result Rollback(AuthorizationSubject actor, Guid commandId, string reason,
        long expectedRevision, bool apply)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld).Allowed)
            return RejectRollback("LIVE_QUEST_PERMISSION_DENIED", commandId);
        if (_history.Count == 0) return RejectRollback("LIVE_QUEST_ROLLBACK_EMPTY", commandId);
        if (commandId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
            return RejectRollback("LIVE_QUEST_COMMAND_INVALID", commandId);
        if (expectedRevision != _revision)
            return RejectRollback("LIVE_QUEST_REVISION_STALE", commandId);
        if (_processed.Contains(commandId))
            return RejectRollback("LIVE_QUEST_COMMAND_REPLAYED", commandId);
        var change = _history.Peek();
        var current = _quests[change.Before.QuestId];
        var consequences = new[] { $"state:{current.State}->{change.Before.State}",
            "journal:restored" };
        var projection =
            $"rpg-kit:quest-journal:quest={current.QuestId}:scope=rollback:state={change.Before.State}";
        if (!apply)
            return new(false, "LIVE_QUEST_ROLLBACK_PREVIEW", _revision, current.State,
                change.Before.State, consequences, change.PlayerStates.Keys.ToArray(), projection,
                false, $"previewed:quest.rollback={current.QuestId}:actor={actor.AccountId}");
        _history.Pop();
        _quests[current.QuestId] = change.Before;
        foreach (var pair in change.PlayerStates)
        {
            if (pair.Value is null) _playerStates.Remove((current.QuestId, pair.Key));
            else _playerStates[(current.QuestId, pair.Key)] = pair.Value;
        }
        _processed.Add(commandId);
        _revision++;
        var entry = audit.Append(actor.AccountId, "quest.rollback", [current.QuestId],
            current.State, change.Before.State, reason, commandId);
        return new(true, "LIVE_QUEST_ROLLED_BACK", _revision, current.State,
            change.Before.State, consequences, change.PlayerStates.Keys.ToArray(), projection,
            true, entry.Hash);
    }

    private Result Reject(string code, Request request) => new(false, code, _revision, "", "",
        [], [], "rpg-kit:quest-journal:rejected", false,
        $"rejected:{code}:command={request.CommandId}");

    private Result RejectRollback(string code, Guid commandId) => new(false, code, _revision,
        "", "", [], [], "rpg-kit:quest-journal:rejected", false,
        $"rejected:{code}:command={commandId}");
}
