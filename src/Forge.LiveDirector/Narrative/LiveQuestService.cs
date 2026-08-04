using Forge.LiveDirector.Audit;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Narrative;

public sealed class LiveQuestService(AuthorizationService authorization, LiveAuditJournal audit)
{
    public sealed record Quest(Guid QuestId, string Name, string State, IReadOnlyDictionary<string, IReadOnlySet<string>> Transitions);
    public sealed record Request(Guid CommandId, Guid QuestId, string TargetState, string PlayerScope, IReadOnlyList<Guid> PlayerAccountIds, string Reason, long ExpectedRevision);
    public sealed record Result(bool Applied, string Code, long Revision, string Before, string After, IReadOnlyList<string> Consequences, string AuditEntry);
    private readonly Dictionary<Guid, Quest> _quests = [];
    private readonly HashSet<Guid> _processed = [];
    private long _revision;

    public void Upsert(Quest quest) => _quests[quest.QuestId] = quest;
    public Quest? Inspect(Guid questId) => _quests.GetValueOrDefault(questId);

    public Result Transition(AuthorizationSubject actor, Request request, bool apply)
    {
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld).Allowed) return Reject("LIVE_QUEST_PERMISSION_DENIED", request);
        if (!_quests.TryGetValue(request.QuestId, out var quest)) return Reject("LIVE_QUEST_UNKNOWN", request);
        if (request.ExpectedRevision != _revision) return Reject("LIVE_QUEST_REVISION_STALE", request);
        if (_processed.Contains(request.CommandId)) return Reject("LIVE_QUEST_COMMAND_REPLAYED", request);
        if (request.PlayerScope is not ("all" or "selected") || request.PlayerScope == "selected" && request.PlayerAccountIds.Count == 0) return Reject("LIVE_QUEST_PLAYER_SCOPE_INVALID", request);
        if (!quest.Transitions.TryGetValue(quest.State, out var allowed) || !allowed.Contains(request.TargetState)) return Reject("LIVE_QUEST_TRANSITION_INVALID", request);
        var consequences = new[] { $"state:{quest.State}->{request.TargetState}", $"players:{request.PlayerScope}:{request.PlayerAccountIds.Count}", "scripts:authoritative", "journal:updated" };
        if (!apply) return new(false, "LIVE_QUEST_PREVIEW", _revision, quest.State, request.TargetState, consequences, $"previewed:quest={quest.QuestId}:actor={actor.AccountId}");
        _quests[quest.QuestId] = quest with { State = request.TargetState };
        _processed.Add(request.CommandId);
        _revision++;
        var entry = audit.Append(actor.AccountId, "quest.transition", [quest.QuestId], quest.State, request.TargetState, request.Reason, request.CommandId);
        return new(true, "LIVE_QUEST_APPLIED", _revision, quest.State, request.TargetState, consequences, entry.Hash);
    }

    private Result Reject(string code, Request request) => new(false, code, _revision, "", "", [], $"rejected:{code}:command={request.CommandId}");
}
