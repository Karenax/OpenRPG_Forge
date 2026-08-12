namespace Forge.RpgKit.Domain.Scripting;

public sealed record InventoryState(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Items,
    IReadOnlyDictionary<string, int> Capacities,
    long Revision);

public sealed record InventoryTransaction(
    Guid Id,
    string Authority,
    string Cause,
    string From,
    string To,
    string ItemId,
    int Quantity);

public sealed record InventoryResult(
    bool Committed,
    InventoryState State,
    IReadOnlyList<string> Audit,
    string SemanticDiff);

public sealed record InventoryQueryResult(
    string OwnerId,
    string ItemId,
    int Quantity,
    int UsedCapacity,
    int MaximumCapacity,
    long Revision);

public sealed record QuestState(
    string QuestId,
    string ScopeId,
    string Status,
    IReadOnlyDictionary<string, int> Objectives,
    IReadOnlyList<string> History,
    long Revision);

public sealed record QuestTransition(
    Guid Id,
    string Authority,
    string FromStatus,
    string ToStatus,
    string ObjectiveId,
    int Delta,
    string Cause);

public sealed record DialogueRequest(
    Guid Id,
    string Authority,
    string GraphId,
    IReadOnlyDictionary<string, string> Participants,
    IReadOnlyDictionary<string, string> Variables);

public sealed record DialogueSession(
    Guid Id,
    string GraphId,
    IReadOnlyDictionary<string, string> Participants,
    IReadOnlyDictionary<string, string> Variables,
    string State,
    IReadOnlyList<string> Outcomes);

public sealed record FactionState(
    IReadOnlyDictionary<string, IReadOnlySet<string>> Memberships,
    IReadOnlyDictionary<string, int> Relationships,
    long Revision);

public sealed record FactionTransaction(
    Guid Id,
    string Authority,
    string ActorId,
    string FactionId,
    int RelationshipDelta,
    bool Join,
    string Scope,
    string Cause);

public sealed record FactionQueryResult(
    string ActorId,
    string FactionId,
    string Scope,
    bool Member,
    int Relationship,
    long Revision);

public sealed record PerceptionQuery(
    string ObserverId,
    string TargetId,
    string Sense,
    double Distance,
    double Range,
    bool LineOfSight,
    double Signal,
    int Seed);

public sealed record PerceptionResult(
    bool Perceived,
    double Confidence,
    IReadOnlyList<string> Stimuli,
    string Trace);

public static class ScriptWorldService
{
    private static readonly IReadOnlySet<string> SupportedSenses =
        new HashSet<string>(["sight", "hearing", "smell", "magic", "tremor"], StringComparer.Ordinal);

    public static InventoryResult Transfer(InventoryState original, InventoryTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(transaction);
        if (transaction.Id == Guid.Empty || transaction.Authority != "server" ||
            string.IsNullOrWhiteSpace(transaction.Cause) || string.IsNullOrWhiteSpace(transaction.From) ||
            string.IsNullOrWhiteSpace(transaction.To) || string.IsNullOrWhiteSpace(transaction.ItemId) ||
            transaction.Quantity <= 0 || transaction.From == transaction.To)
            return new(false, original, ["inventory-rejected"], "no-change");

        var from = original.Items.GetValueOrDefault(transaction.From) ?? new Dictionary<string, int>();
        var available = from.GetValueOrDefault(transaction.ItemId);
        var to = original.Items.GetValueOrDefault(transaction.To) ?? new Dictionary<string, int>();
        var used = to.Values.Sum();
        var capacity = original.Capacities.GetValueOrDefault(transaction.To, int.MaxValue);
        if (available < transaction.Quantity || capacity < 0 || used + transaction.Quantity > capacity)
            return new(false, original, ["inventory-rollback"], "no-change");

        var items = original.Items.ToDictionary(pair => pair.Key, pair => pair.Value);
        var newFrom = from.ToDictionary();
        var newTo = to.ToDictionary();
        newFrom[transaction.ItemId] = available - transaction.Quantity;
        newTo[transaction.ItemId] = newTo.GetValueOrDefault(transaction.ItemId) + transaction.Quantity;
        items[transaction.From] = newFrom;
        items[transaction.To] = newTo;
        return new(true, original with { Items = items, Revision = checked(original.Revision + 1) },
            [$"inventory:{transaction.Cause}", $"owner:{transaction.From}->{transaction.To}",
                $"item:{transaction.ItemId}:{transaction.Quantity}"],
            $"transfer:{transaction.ItemId}:{transaction.Quantity}|{transaction.From}->{transaction.To}");
    }

    public static InventoryQueryResult QueryInventory(InventoryState state, string ownerId, string itemId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        var inventory = state.Items.GetValueOrDefault(ownerId) ?? new Dictionary<string, int>();
        return new(ownerId, itemId, inventory.GetValueOrDefault(itemId), inventory.Values.Sum(),
            state.Capacities.GetValueOrDefault(ownerId, int.MaxValue), state.Revision);
    }

    public static QuestState TransitionQuest(QuestState original, QuestTransition transaction)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(transaction);
        if (transaction.Id == Guid.Empty || transaction.Authority != "server" ||
            string.IsNullOrWhiteSpace(original.QuestId) || string.IsNullOrWhiteSpace(original.ScopeId) ||
            string.IsNullOrWhiteSpace(transaction.FromStatus) || string.IsNullOrWhiteSpace(transaction.ToStatus) ||
            string.IsNullOrWhiteSpace(transaction.Cause) || original.Status != transaction.FromStatus)
            throw new InvalidOperationException("RPG_SCRIPT_QUEST_TRANSITION_INVALID");
        var objectives = original.Objectives.ToDictionary();
        if (!string.IsNullOrWhiteSpace(transaction.ObjectiveId))
            objectives[transaction.ObjectiveId] = Math.Max(0,
                objectives.GetValueOrDefault(transaction.ObjectiveId) + transaction.Delta);
        return original with
        {
            Status = transaction.ToStatus,
            Objectives = objectives,
            History = [.. original.History,
                $"{transaction.FromStatus}->{transaction.ToStatus}:{transaction.Cause}:scope={original.ScopeId}"],
            Revision = checked(original.Revision + 1),
        };
    }

    public static DialogueSession StartDialogue(DialogueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Id == Guid.Empty || request.Authority != "server" ||
            string.IsNullOrWhiteSpace(request.GraphId) || request.Participants.Count == 0 ||
            request.Participants.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value)) ||
            request.Variables.Any(item => string.IsNullOrWhiteSpace(item.Key)))
            throw new InvalidOperationException("RPG_SCRIPT_DIALOGUE_INVALID");
        return new(request.Id, request.GraphId,
            request.Participants.OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(StringComparer.Ordinal),
            request.Variables.OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(StringComparer.Ordinal), "running", []);
    }

    public static DialogueSession CompleteDialogue(DialogueSession session, string outcome)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        return session.State == "running"
            ? session with { State = "completed", Outcomes = [.. session.Outcomes, outcome] }
            : session;
    }

    public static DialogueSession InterruptDialogue(DialogueSession session, string cause)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(cause);
        return session.State == "running"
            ? session with { State = "interrupted", Outcomes = [.. session.Outcomes, $"interrupted:{cause}"] }
            : session;
    }

    public static FactionState ApplyFaction(FactionState original, FactionTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(transaction);
        if (transaction.Id == Guid.Empty || transaction.Authority != "server" ||
            string.IsNullOrWhiteSpace(transaction.ActorId) || string.IsNullOrWhiteSpace(transaction.FactionId) ||
            string.IsNullOrWhiteSpace(transaction.Scope) || string.IsNullOrWhiteSpace(transaction.Cause) ||
            transaction.RelationshipDelta is < -200 or > 200)
            throw new InvalidOperationException("RPG_SCRIPT_FACTION_INVALID");
        var memberships = original.Memberships.ToDictionary(pair => pair.Key, pair => pair.Value);
        var actor = (memberships.GetValueOrDefault(transaction.ActorId) ?? new HashSet<string>()).ToHashSet();
        if (transaction.Join) actor.Add(transaction.FactionId);
        else actor.Remove(transaction.FactionId);
        memberships[transaction.ActorId] = actor;
        var relationships = original.Relationships.ToDictionary();
        var key = RelationshipKey(transaction.Scope, transaction.ActorId, transaction.FactionId);
        relationships[key] = Math.Clamp(relationships.GetValueOrDefault(key) + transaction.RelationshipDelta,
            -100, 100);
        return original with
        {
            Memberships = memberships,
            Relationships = relationships,
            Revision = checked(original.Revision + 1),
        };
    }

    public static FactionQueryResult QueryFaction(
        FactionState state,
        string actorId,
        string factionId,
        string scope)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(factionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        var member = state.Memberships.GetValueOrDefault(actorId)?.Contains(factionId) == true;
        return new(actorId, factionId, scope, member,
            state.Relationships.GetValueOrDefault(RelationshipKey(scope, actorId, factionId)), state.Revision);
    }

    public static PerceptionResult Perceive(PerceptionQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.ObserverId) || string.IsNullOrWhiteSpace(query.TargetId) ||
            !SupportedSenses.Contains(query.Sense) || !double.IsFinite(query.Distance) ||
            !double.IsFinite(query.Range) || !double.IsFinite(query.Signal) || query.Distance < 0 ||
            query.Range < 0 || query.Signal is < 0 or > 1)
            throw new InvalidOperationException("RPG_SCRIPT_PERCEPTION_INVALID");
        var spatial = query.Range == 0 ? 0 : Math.Clamp(1 - query.Distance / query.Range, 0, 1);
        var lineFactor = query.Sense == "sight" && !query.LineOfSight ? 0 : 1;
        var jitter = new Random(query.Seed).NextDouble() * 0.000001;
        var confidence = Math.Clamp(spatial * query.Signal * lineFactor + jitter, 0, 1);
        var perceived = confidence >= .25;
        return new(perceived, confidence,
            perceived ? [$"sense:{query.Sense}", $"target:{query.TargetId}"] : [],
            $"observer:{query.ObserverId}|target:{query.TargetId}|sense:{query.Sense}|" +
            $"distance:{query.Distance:0.###}|range:{query.Range:0.###}|los:{query.LineOfSight}|" +
            $"confidence:{confidence:F6}|seed:{query.Seed}");
    }

    private static string RelationshipKey(string scope, string actorId, string factionId) =>
        $"{scope}:{actorId}:{factionId}";
}
