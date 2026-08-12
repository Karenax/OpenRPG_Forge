using Forge.RpgKit.Domain.Scripting;

namespace Forge.RpgKit.Domain.Tests.Scripting;

public sealed class ScriptWorldServiceTests
{
    [Fact]
    public void Inventory_transactions_query_ownership_capacity_audit_and_rollback()
    {
        var state = Inventory();
        var committed = ScriptWorldService.Transfer(state,
            new(Guid.NewGuid(), "server", "loot", "a", "b", "key", 1));
        var query = ScriptWorldService.QueryInventory(committed.State, "b", "key");
        var rolledBack = ScriptWorldService.Transfer(state,
            new(Guid.NewGuid(), "server", "loot", "a", "b", "key", 2));

        Assert.True(committed.Committed);
        Assert.Equal(1, query.Quantity);
        Assert.Equal(1, query.UsedCapacity);
        Assert.Equal(1, query.MaximumCapacity);
        Assert.Contains("owner:a->b", committed.Audit);
        Assert.False(rolledBack.Committed);
        Assert.Same(state, rolledBack.State);
    }

    [Fact]
    public void Inventory_rejects_invalid_authority_identity_and_quantity_without_mutation()
    {
        var state = Inventory();

        var result = ScriptWorldService.Transfer(state,
            new(Guid.Empty, "client", "", "a", "a", "", 0));

        Assert.False(result.Committed);
        Assert.Same(state, result.State);
        Assert.Equal("no-change", result.SemanticDiff);
    }

    [Fact]
    public void Quest_transitions_objectives_history_scope_cause_and_revision()
    {
        var quest = new QuestState("q", "party:1", "active",
            new Dictionary<string, int> { ["find"] = 0 }, [], 0);

        var progressed = ScriptWorldService.TransitionQuest(quest,
            new(Guid.NewGuid(), "server", "active", "completed", "find", 1, "found"));

        Assert.Equal("completed", progressed.Status);
        Assert.Equal(1, progressed.Objectives["find"]);
        Assert.Contains("scope=party:1", Assert.Single(progressed.History), StringComparison.Ordinal);
        Assert.Equal(1, progressed.Revision);
    }

    [Fact]
    public void Dialogue_graph_participants_variables_completion_and_safe_interruption()
    {
        var dialogue = ScriptWorldService.StartDialogue(new(Guid.NewGuid(), "server", "graph:intro",
            new Dictionary<string, string> { ["speaker"] = "npc", ["listener"] = "hero" },
            new Dictionary<string, string> { ["mood"] = "calm" }));
        var completed = ScriptWorldService.CompleteDialogue(dialogue, "accepted");
        var interrupted = ScriptWorldService.InterruptDialogue(dialogue, "combat");

        Assert.Equal("completed", completed.State);
        Assert.Equal("accepted", Assert.Single(completed.Outcomes));
        Assert.Equal("interrupted", interrupted.State);
        Assert.Equal("interrupted:combat", Assert.Single(interrupted.Outcomes));
        Assert.Equal("npc", dialogue.Participants["speaker"]);
    }

    [Fact]
    public void Faction_membership_reputation_scope_authority_bounds_query_and_revision()
    {
        var factions = new FactionState(new Dictionary<string, IReadOnlySet<string>>(),
            new Dictionary<string, int>(), 0);
        var changed = ScriptWorldService.ApplyFaction(factions,
            new(Guid.NewGuid(), "server", "hero", "wardens", 125, true, "module", "rescued-member"));
        var query = ScriptWorldService.QueryFaction(changed, "hero", "wardens", "module");

        Assert.True(query.Member);
        Assert.Equal(100, query.Relationship);
        Assert.Equal(1, query.Revision);
        Assert.Empty(factions.Memberships);
    }

    [Fact]
    public void Perception_combines_sense_los_stimulus_seed_and_spatial_trace()
    {
        var query = new PerceptionQuery("guard", "hero", "sight", 2, 10, true, .8, 42);
        var seen = ScriptWorldService.Perceive(query);
        var repeat = ScriptWorldService.Perceive(query);
        var hidden = ScriptWorldService.Perceive(query with { LineOfSight = false });

        Assert.True(seen.Perceived);
        Assert.Equal(seen.Confidence, repeat.Confidence);
        Assert.Equal(seen.Trace, repeat.Trace);
        Assert.Equal(seen.Stimuli, repeat.Stimuli);
        Assert.Contains("sense:sight", seen.Stimuli);
        Assert.Contains("seed:42", seen.Trace, StringComparison.Ordinal);
        Assert.False(hidden.Perceived);
    }

    [Fact]
    public void Invalid_world_api_requests_are_rejected_before_state_mutation()
    {
        Assert.Throws<InvalidOperationException>(() => ScriptWorldService.TransitionQuest(
            new("q", "party", "active", new Dictionary<string, int>(), [], 0),
            new(Guid.NewGuid(), "client", "active", "done", "", 0, "cause")));
        Assert.Throws<InvalidOperationException>(() => ScriptWorldService.StartDialogue(
            new(Guid.NewGuid(), "server", "graph", new Dictionary<string, string>(),
                new Dictionary<string, string>())));
        Assert.Throws<InvalidOperationException>(() => ScriptWorldService.ApplyFaction(
            new(new Dictionary<string, IReadOnlySet<string>>(), new Dictionary<string, int>(), 0),
            new(Guid.NewGuid(), "client", "hero", "wardens", 1, true, "module", "cause")));
        Assert.Throws<InvalidOperationException>(() => ScriptWorldService.Perceive(
            new("guard", "hero", "unknown", 0, 1, true, 1, 42)));
    }

    private static InventoryState Inventory() =>
        new(new Dictionary<string, IReadOnlyDictionary<string, int>>
        {
            ["a"] = new Dictionary<string, int> { ["key"] = 2 },
            ["b"] = new Dictionary<string, int>(),
        }, new Dictionary<string, int> { ["b"] = 1 }, 0);
}
