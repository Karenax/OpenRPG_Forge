namespace Forge.RpgKit.Domain.Scripting;

public sealed record InventoryState(IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Items, IReadOnlyDictionary<string, int> Capacities, long Revision);
public sealed record InventoryTransaction(Guid Id, string Authority, string Cause, string From, string To, string ItemId, int Quantity);
public sealed record InventoryResult(bool Committed, InventoryState State, IReadOnlyList<string> Audit, string SemanticDiff);
public sealed record QuestState(string QuestId, string ScopeId, string Status, IReadOnlyDictionary<string, int> Objectives, IReadOnlyList<string> History, long Revision);
public sealed record QuestTransition(Guid Id, string Authority, string FromStatus, string ToStatus, string ObjectiveId, int Delta, string Cause);
public sealed record DialogueRequest(Guid Id, string Authority, string GraphId, IReadOnlyDictionary<string, string> Participants, IReadOnlyDictionary<string, string> Variables);
public sealed record DialogueSession(Guid Id, string GraphId, IReadOnlyDictionary<string, string> Participants, IReadOnlyDictionary<string, string> Variables, string State, IReadOnlyList<string> Outcomes);
public sealed record FactionState(IReadOnlyDictionary<string, IReadOnlySet<string>> Memberships, IReadOnlyDictionary<string, int> Relationships, long Revision);
public sealed record FactionTransaction(Guid Id, string Authority, string ActorId, string FactionId, int RelationshipDelta, bool Join, string Scope, string Cause);
public sealed record PerceptionQuery(string ObserverId, string TargetId, string Sense, double Distance, double Range, bool LineOfSight, double Signal, int Seed);
public sealed record PerceptionResult(bool Perceived, double Confidence, IReadOnlyList<string> Stimuli, string Trace);

public static class ScriptWorldService
{
    public static InventoryResult Transfer(InventoryState original, InventoryTransaction tx)
    {
        if (tx.Id == Guid.Empty || tx.Authority != "server" || tx.Quantity <= 0 || tx.From == tx.To) return new(false, original, ["inventory-rejected"], "no-change");
        var from = original.Items.GetValueOrDefault(tx.From) ?? new Dictionary<string, int>(); var available = from.GetValueOrDefault(tx.ItemId); var to = original.Items.GetValueOrDefault(tx.To) ?? new Dictionary<string, int>(); var used = to.Values.Sum();
        if (available < tx.Quantity || used + tx.Quantity > original.Capacities.GetValueOrDefault(tx.To, int.MaxValue)) return new(false, original, ["inventory-rollback"], "no-change");
        var items = original.Items.ToDictionary(pair => pair.Key, pair => pair.Value); var newFrom = from.ToDictionary(); var newTo = to.ToDictionary(); newFrom[tx.ItemId] = available - tx.Quantity; newTo[tx.ItemId] = newTo.GetValueOrDefault(tx.ItemId) + tx.Quantity; items[tx.From] = newFrom; items[tx.To] = newTo;
        return new(true, original with { Items = items, Revision = checked(original.Revision + 1) }, [$"inventory:{tx.Cause}", $"owner:{tx.From}->{tx.To}"], $"transfer:{tx.ItemId}:{tx.Quantity}|{tx.From}->{tx.To}");
    }

    public static QuestState TransitionQuest(QuestState original, QuestTransition tx)
    {
        if (tx.Id == Guid.Empty || tx.Authority != "server" || original.Status != tx.FromStatus) throw new InvalidOperationException("RPG_SCRIPT_QUEST_TRANSITION_INVALID");
        var objectives = original.Objectives.ToDictionary(); if (!string.IsNullOrWhiteSpace(tx.ObjectiveId)) objectives[tx.ObjectiveId] = Math.Max(0, objectives.GetValueOrDefault(tx.ObjectiveId) + tx.Delta);
        return original with { Status = tx.ToStatus, Objectives = objectives, History = [.. original.History, $"{tx.FromStatus}->{tx.ToStatus}:{tx.Cause}"], Revision = checked(original.Revision + 1) };
    }

    public static DialogueSession StartDialogue(DialogueRequest request)
    {
        if (request.Id == Guid.Empty || request.Authority != "server" || string.IsNullOrWhiteSpace(request.GraphId) || request.Participants.Count == 0) throw new InvalidOperationException("RPG_SCRIPT_DIALOGUE_INVALID");
        return new(request.Id, request.GraphId, request.Participants, request.Variables, "running", []);
    }
    public static DialogueSession CompleteDialogue(DialogueSession session, string outcome) => session.State == "running" ? session with { State = "completed", Outcomes = [.. session.Outcomes, outcome] } : session;
    public static DialogueSession InterruptDialogue(DialogueSession session, string cause) => session.State == "running" ? session with { State = "interrupted", Outcomes = [.. session.Outcomes, $"interrupted:{cause}"] } : session;

    public static FactionState ApplyFaction(FactionState original, FactionTransaction tx)
    {
        if (tx.Id == Guid.Empty || tx.Authority != "server" || string.IsNullOrWhiteSpace(tx.Scope) || string.IsNullOrWhiteSpace(tx.Cause)) throw new InvalidOperationException("RPG_SCRIPT_FACTION_INVALID");
        var memberships = original.Memberships.ToDictionary(pair => pair.Key, pair => pair.Value); var actor = (memberships.GetValueOrDefault(tx.ActorId) ?? new HashSet<string>()).ToHashSet(); if (tx.Join) actor.Add(tx.FactionId); else actor.Remove(tx.FactionId); memberships[tx.ActorId] = actor;
        var relationships = original.Relationships.ToDictionary(); var key = $"{tx.Scope}:{tx.ActorId}:{tx.FactionId}"; relationships[key] = Math.Clamp(relationships.GetValueOrDefault(key) + tx.RelationshipDelta, -100, 100); return original with { Memberships = memberships, Relationships = relationships, Revision = checked(original.Revision + 1) };
    }

    public static PerceptionResult Perceive(PerceptionQuery query)
    {
        if (query.Distance < 0 || query.Range < 0 || query.Signal is < 0 or > 1) throw new InvalidOperationException("RPG_SCRIPT_PERCEPTION_INVALID");
        var spatial = query.Range == 0 ? 0 : Math.Clamp(1 - query.Distance / query.Range, 0, 1); var lineFactor = query.Sense == "sight" && !query.LineOfSight ? 0 : 1; var jitter = new Random(query.Seed).NextDouble() * 0.000001; var confidence = Math.Clamp(spatial * query.Signal * lineFactor + jitter, 0, 1); var perceived = confidence >= .25;
        return new(perceived, confidence, perceived ? [$"sense:{query.Sense}", $"target:{query.TargetId}"] : [], $"observer:{query.ObserverId}|los:{query.LineOfSight}|confidence:{confidence:F6}");
    }
}
