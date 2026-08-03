using Forge.RpgKit.Domain.Scripting;

namespace Forge.RpgKit.Domain.Tests.Scripting;

public sealed class ScriptWorldServiceTests
{
    [Fact]
    public void Inventory_transactions_check_ownership_capacity_and_rollback()
    {
        var state = new InventoryState(new Dictionary<string, IReadOnlyDictionary<string, int>> { ["a"] = new Dictionary<string, int> { ["key"] = 2 }, ["b"] = new Dictionary<string, int>() }, new Dictionary<string, int> { ["b"] = 1 }, 0);
        var committed = ScriptWorldService.Transfer(state, new(Guid.NewGuid(), "server", "loot", "a", "b", "key", 1)); Assert.True(committed.Committed); Assert.Equal(1, committed.State.Items["b"]["key"]); Assert.Contains("owner:a->b", committed.Audit);
        var rolledBack = ScriptWorldService.Transfer(state, new(Guid.NewGuid(), "server", "loot", "a", "b", "key", 2)); Assert.False(rolledBack.Committed); Assert.Same(state, rolledBack.State);
    }

    [Fact]
    public void Quest_dialogue_and_faction_operations_are_scoped_authoritative_and_auditable()
    {
        var quest = new QuestState("q", "party:1", "active", new Dictionary<string, int> { ["find"] = 0 }, [], 0); var progressed = ScriptWorldService.TransitionQuest(quest, new(Guid.NewGuid(), "server", "active", "completed", "find", 1, "found")); Assert.Equal("completed", progressed.Status); Assert.Single(progressed.History);
        var dialogue = ScriptWorldService.StartDialogue(new(Guid.NewGuid(), "server", "graph:intro", new Dictionary<string, string> { ["speaker"] = "npc" }, new Dictionary<string, string> { ["mood"] = "calm" })); Assert.Equal("completed", ScriptWorldService.CompleteDialogue(dialogue, "accepted").State); Assert.Equal("interrupted", ScriptWorldService.InterruptDialogue(dialogue, "combat").State);
        var factions = new FactionState(new Dictionary<string, IReadOnlySet<string>>(), new Dictionary<string, int>(), 0); var changed = ScriptWorldService.ApplyFaction(factions, new(Guid.NewGuid(), "server", "hero", "wardens", 25, true, "module", "rescued-member")); Assert.Contains("wardens", changed.Memberships["hero"]); Assert.Equal(25, changed.Relationships["module:hero:wardens"]);
    }

    [Fact]
    public void Perception_combines_sense_los_stimulus_and_seeded_spatial_confidence()
    {
        var seen = ScriptWorldService.Perceive(new("guard", "hero", "sight", 2, 10, true, .8, 42)); var repeat = ScriptWorldService.Perceive(new("guard", "hero", "sight", 2, 10, true, .8, 42)); Assert.True(seen.Perceived); Assert.Equal(seen.Confidence, repeat.Confidence); Assert.Equal(seen.Trace, repeat.Trace); Assert.Equal(seen.Stimuli, repeat.Stimuli); Assert.Contains("sense:sight", seen.Stimuli);
        Assert.False(ScriptWorldService.Perceive(new("guard", "hero", "sight", 2, 10, false, 1, 42)).Perceived);
    }
}
