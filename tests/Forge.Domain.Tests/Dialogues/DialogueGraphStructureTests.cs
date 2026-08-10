using Forge.Domain.Dialogues;

namespace Forge.Domain.Tests.Dialogues;

public sealed class DialogueGraphStructureTests
{
    [Fact]
    public void Canonical_graph_exposes_schema_roles_entries_edges_and_priority_order()
    {
        var entry = DialogueNodeId.Parse("d0050000-0000-4000-8000-000000000001");
        var first = DialogueNodeId.Parse("d0050000-0000-4000-8000-000000000002");
        var second = DialogueNodeId.Parse("d0050000-0000-4000-8000-000000000003");
        var high = new DialogueChoice(DialogueChoiceId.Parse("d0050000-0000-4000-8000-000000000011"),
            new("dialogue.high", "Plus tard"), targetNodeId: second, priority: 20);
        var low = new DialogueChoice(DialogueChoiceId.Parse("d0050000-0000-4000-8000-000000000012"),
            new("dialogue.low", "D'abord"), targetNodeId: first, priority: 5);
        var graph = new DialogueDefinition(DialogueId.Parse("d0050000-0000-4000-8000-000000000020"),
            "structure", "Structure", entry,
            [
                new(entry, null, new("dialogue.entry", "Entrée"), choices: [high, low],
                    role: DialogueNodeRole.Narration),
                new(first, null, new("dialogue.first", "Premier"), role: DialogueNodeRole.End),
                new(second, null, new("dialogue.second", "Second"), role: DialogueNodeRole.End),
            ], provenance: "author:test", metadata: new Dictionary<string, string> { ["chapter"] = "one" });

        var available = new DialogueTraversal(graph).GetAvailableChoices(new AlwaysTrue());

        Assert.Empty(DialogueValidator.Validate(graph));
        Assert.Equal(DialogueDefinition.CurrentSchemaVersion, graph.SchemaVersion);
        Assert.Equal("author:test", graph.Provenance);
        Assert.Single(graph.Participants);
        Assert.Single(graph.EntryPoints);
        Assert.Equal(2, graph.Edges.Count);
        Assert.Equal([low.Id, high.Id], available.Select(item => item.Id));
    }

    [Fact]
    public void Large_branch_inventory_has_no_arbitrary_domain_limit()
    {
        var entry = DialogueNodeId.Parse("d0050000-0000-4000-8000-999999999900");
        var leaves = Enumerable.Range(1, 512).Select(index =>
            new DialogueNode(DialogueNodeId.From(Guid.Parse($"d0050000-0000-4000-8000-{index:D12}")),
                null, new($"dialogue.leaf.{index}", $"Feuille {index}"), role: DialogueNodeRole.End)).ToArray();
        var choices = leaves.Select((node, index) => new DialogueChoice(
            DialogueChoiceId.From(Guid.Parse($"d0060000-0000-4000-8000-{index + 1:D12}")),
            new($"dialogue.choice.{index}", $"Choix {index}"), targetNodeId: node.Id, priority: index)).ToArray();
        var graph = new DialogueDefinition(DialogueId.Parse("d0050000-0000-4000-8000-999999999901"),
            "large", "Large", entry,
            [new(entry, null, new("dialogue.entry", "Entrée"), choices: choices), .. leaves]);

        Assert.Empty(DialogueValidator.Validate(graph));
        Assert.Equal(512, graph.Edges.Count);
        Assert.Equal(513, graph.Nodes.Count);
    }

    [Fact]
    public void Flow_cycle_is_rejected_but_explicit_reference_reuse_is_allowed()
    {
        var first = DialogueNodeId.Parse("d0050000-0000-4000-8000-000000000201");
        var second = DialogueNodeId.Parse("d0050000-0000-4000-8000-000000000202");
        var forward = new DialogueChoice(DialogueChoiceId.Parse("d0050000-0000-4000-8000-000000000211"),
            new("dialogue.forward", "Continuer"), targetNodeId: second);
        DialogueDefinition Build(DialogueEdgeKind returnKind) => new(
            DialogueId.Parse("d0050000-0000-4000-8000-000000000220"), "cycle", "Cycle", first,
            [
                new(first, null, new("dialogue.first", "Premier"), choices: [forward]),
                new(second, null, new("dialogue.second", "Second"), choices:
                [new(DialogueChoiceId.Parse("d0050000-0000-4000-8000-000000000212"),
                    new("dialogue.return", "Retour"), targetNodeId: first, edgeKind: returnKind),
                 new(DialogueChoiceId.Parse("d0050000-0000-4000-8000-000000000213"),
                    new("dialogue.exit", "Quitter"))]),
            ]);

        Assert.Contains(DialogueValidator.Validate(Build(DialogueEdgeKind.Flow)),
            item => item.Code == DialogueValidator.AccidentalCycleCode);
        Assert.Empty(DialogueValidator.Validate(Build(DialogueEdgeKind.Reference)));
    }

    [Fact]
    public void Multiple_incoming_edges_do_not_stop_reachability_scan()
    {
        var entry = DialogueNodeId.Parse("d0064000-0000-4000-8000-000000000001");
        var shared = DialogueNodeId.Parse("d0064000-0000-4000-8000-000000000002");
        var branch = DialogueNodeId.Parse("d0064000-0000-4000-8000-000000000003");
        var end = DialogueNodeId.Parse("d0064000-0000-4000-8000-000000000004");
        var dialogue = new DialogueDefinition(
            DialogueId.Parse("d0064000-0000-4000-8000-000000000005"), "diamond", "Diamond", entry,
            [
                new(entry, null, new("dialogue.entry", "Entry"), choices:
                [
                    new(DialogueChoiceId.Parse("d0064000-0000-4000-8000-000000000011"),
                        new("dialogue.shared", "Shared"), targetNodeId: shared),
                    new(DialogueChoiceId.Parse("d0064000-0000-4000-8000-000000000012"),
                        new("dialogue.branch", "Branch"), targetNodeId: branch),
                    new(DialogueChoiceId.Parse("d0064000-0000-4000-8000-000000000013"),
                        new("dialogue.reference", "Reference"), targetNodeId: shared,
                        edgeKind: DialogueEdgeKind.Reference),
                ]),
                new(shared, null, new("dialogue.shared.node", "Shared node"), choices:
                [new(DialogueChoiceId.Parse("d0064000-0000-4000-8000-000000000014"),
                    new("dialogue.end.shared", "End"), targetNodeId: end)]),
                new(branch, null, new("dialogue.branch.node", "Branch node"), choices:
                [new(DialogueChoiceId.Parse("d0064000-0000-4000-8000-000000000015"),
                    new("dialogue.end.branch", "End"), targetNodeId: end)]),
                new(end, null, new("dialogue.end", "End"), role: DialogueNodeRole.End),
            ]);

        Assert.Empty(DialogueValidator.Validate(dialogue));
    }

    private sealed class AlwaysTrue : IDialogueConditionEvaluator
    {
        public bool IsSatisfied(DialogueCondition condition) => true;
    }
}
