using Forge.RpgKit.Application.Scripting;
using Forge.RpgKit.Domain.Scripting;
using Forge.Scripting;

namespace Forge.RpgKit.Domain.Tests.Scripting;

public sealed class ForgeScriptWorldApiRuntimeAdapterTests
{
    [Fact]
    public void Optional_missing_is_safe_required_missing_is_diagnostic_and_stale_handles_fail()
    {
        var adapter = new ForgeScriptWorldApiRuntimeAdapter();
        var document = Document();

        Assert.True(adapter.Resolve(null, false).OptionalFallback);
        Assert.Equal(ForgeScriptWorldApiRuntimeAdapter.RequiredMissingCode,
            adapter.Resolve(null, true).DiagnosticCode);
        Assert.Throws<InvalidOperationException>(() => adapter.Build(new(Guid.NewGuid()), document,
            Inventory(), Quests(), Factions()));
    }

    [Fact]
    public void Inventory_quest_dialogue_and_faction_mutate_one_immutable_runtime_projection()
    {
        var (adapter, handle, runtime) = Runtime();
        var transfer = adapter.Transfer(handle, runtime,
            new(Guid.NewGuid(), "server", "loot", "chest", "hero", "key", 1));
        runtime = transfer.Runtime;
        runtime = adapter.TransitionQuest(handle, runtime, "crypt",
            new(Guid.NewGuid(), "server", "active", "completed", "find-key", 1, "found"));
        var dialogue = adapter.StartDialogue(handle, runtime,
            new(Guid.NewGuid(), "server", "graph:intro",
                new Dictionary<string, string> { ["speaker"] = "warden", ["listener"] = "hero" },
                new Dictionary<string, string> { ["quest"] = "crypt" }));
        runtime = adapter.CompleteDialogue(handle, dialogue.Runtime, dialogue.Session.Id, "accepted");
        runtime = adapter.ApplyFaction(handle, runtime,
            new(Guid.NewGuid(), "server", "hero", "wardens", 25, true, "module", "rescued"));

        Assert.True(transfer.Result.Committed);
        Assert.Equal(1, runtime.Inventory.Items["hero"]["key"]);
        Assert.Equal("completed", runtime.Quests["crypt"].Status);
        Assert.Equal("completed", runtime.Dialogues[dialogue.Session.Id].State);
        Assert.True(ScriptWorldService.QueryFaction(runtime.Factions, "hero", "wardens", "module").Member);
    }

    [Fact]
    public void Perception_is_seeded_and_invalid_authority_rolls_back_inventory()
    {
        var (adapter, handle, runtime) = Runtime();
        var query = new PerceptionQuery("warden", "hero", "sight", 2, 10, true, .8, 98);
        var first = adapter.Perceive(handle, runtime, query);
        var second = adapter.Perceive(handle, runtime, query);
        var rejected = adapter.Transfer(handle, runtime,
            new(Guid.NewGuid(), "client", "loot", "chest", "hero", "key", 1));

        Assert.True(first.Perceived);
        Assert.Equal(first.Perceived, second.Perceived);
        Assert.Equal(first.Confidence, second.Confidence);
        Assert.Equal(first.Stimuli, second.Stimuli);
        Assert.Equal(first.Trace, second.Trace);
        Assert.False(rejected.Result.Committed);
        Assert.Same(runtime, rejected.Runtime);
    }

    [Fact]
    public void Versioned_native_world_apis_compile_and_execute_in_the_same_sandbox()
    {
        var (adapter, handle, runtime) = Runtime();

        var result = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(30_000, 1000, 32, 8));

        Assert.Equal(ForgeScriptExecutionStatus.Completed, result.Status);
        Assert.Contains(result.Events, item => item.Kind == "inventory.transfer");
        Assert.Contains(result.Events, item => item.Kind == "quest.transition");
        Assert.Contains(result.Events, item => item.Kind == "dialogue.started");
        Assert.Contains(result.Events, item => item.Kind == "faction.reputation");
        Assert.Contains(result.Events, item => item.Kind == "perception.query");
    }

    private static (ForgeScriptWorldApiRuntimeAdapter Adapter, ForgeScriptWorldApiHandle Handle,
        ForgeScriptWorldApiRuntime Runtime) Runtime()
    {
        var adapter = new ForgeScriptWorldApiRuntimeAdapter();
        var document = Document();
        var handle = adapter.Resolve(document, true).Handle!;
        return (adapter, handle, adapter.Build(handle, document, Inventory(), Quests(), Factions()));
    }

    private static ForgeScriptSourceDocument Document() =>
        new(Guid.Parse("c7980000-0000-4000-8000-000000000001"), 1, "test:r098", "scripts/r098.as",
            """
            void main()
            {
                EntityRef chest = spawn("chest");
                EntityRef hero = spawn("hero");
                EntityRef warden = spawn("warden");
                InventoryResultRef loot = inventory_transfer(chest, hero, "key", 1);
                QuestResultRef quest = quest_transition("crypt", "party:1", "active", "completed", "find-key", 1, "found");
                DialogueSessionRef talk = dialogue_start("graph:intro", warden, hero);
                dialogue_complete(talk, "accepted");
                FactionResultRef faction = faction_reputation(hero, "wardens", 25, true, "module", "rescued");
                PerceptionResultRef seen = perception_query(warden, hero, "sight", 2.0f, 10.0f, true, 0.8f, 98);
            }
            """, "1.0.0", ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], []);

    private static InventoryState Inventory() =>
        new(new Dictionary<string, IReadOnlyDictionary<string, int>>
        {
            ["chest"] = new Dictionary<string, int> { ["key"] = 1 },
            ["hero"] = new Dictionary<string, int>(),
        }, new Dictionary<string, int> { ["hero"] = 8 }, 0);

    private static IReadOnlyDictionary<string, QuestState> Quests() =>
        new Dictionary<string, QuestState>
        {
            ["crypt"] = new("crypt", "party:1", "active",
                new Dictionary<string, int> { ["find-key"] = 0 }, [], 0),
        };

    private static FactionState Factions() =>
        new(new Dictionary<string, IReadOnlySet<string>>(), new Dictionary<string, int>(), 0);
}
