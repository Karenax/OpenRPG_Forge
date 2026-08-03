using Forge.RpgKit.Domain.Dialogues;

namespace Forge.RpgKit.Domain.Tests.Dialogues;

public sealed class DialogueGraphServiceTests
{
    [Fact]
    public void Typed_tokens_render_escaped_preview_and_report_missing_required_values()
    {
        var graph = Graph(); var context = new DialogueContext(new Dictionary<string, object> { ["hero"] = "<Lyra>" }, new Dictionary<string, string> { ["guide"] = "entity:lyra" }, 7);
        var rendered = DialogueGraphService.Render(graph, graph.EntryNodeId, context);
        Assert.Equal("entity:lyra", rendered.SpeakerEntityId); Assert.Contains("&lt;Lyra&gt;", rendered.Text); Assert.DoesNotContain("<Lyra>", rendered.Text); Assert.Empty(rendered.Diagnostics);
        var missing = DialogueGraphService.Render(graph, graph.EntryNodeId, context with { Values = new Dictionary<string, object>() }); Assert.Contains(missing.Diagnostics, item => item.Code == "RPG_DIALOGUE_TOKEN_MISSING");
    }

    [Fact]
    public void Participant_role_uses_node_speaker_and_fallback_binding()
    {
        var graph = Graph(); var context = new DialogueContext(new Dictionary<string, object> { ["hero"] = "Ari" }, new Dictionary<string, string> { ["narrator"] = "entity:narrator" }, 1);
        var rendered = DialogueGraphService.Render(graph, graph.EntryNodeId, context); Assert.Equal("entity:narrator", rendered.SpeakerEntityId);
    }

    [Fact]
    public void Evaluation_modes_are_visible_ordered_and_deterministic()
    {
        var graph = Graph(); var context = new DialogueContext(new Dictionary<string, object> { ["hero"] = "Ari", ["trust"] = 5d }, new Dictionary<string, string> { ["guide"] = "entity:guide" }, 42);
        var first = DialogueGraphService.Render(graph, graph.EntryNodeId, context); Assert.Single(first.AvailableEdges); Assert.Equal(0, first.AvailableEdges[0].Priority);
        var allGraph = ReplaceMode(graph, DialogueEvaluationMode.AllValid); Assert.Equal(2, DialogueGraphService.Render(allGraph, allGraph.EntryNodeId, context).AvailableEdges.Count);
        var randomGraph = ReplaceMode(graph, DialogueEvaluationMode.RandomDeterministic); var a = DialogueGraphService.Render(randomGraph, randomGraph.EntryNodeId, context); var b = DialogueGraphService.Render(randomGraph, randomGraph.EntryNodeId, context); Assert.Equal(a.AvailableEdges[0].TargetNodeId, b.AvailableEdges[0].TargetNodeId);
        var weighted = ReplaceMode(graph, DialogueEvaluationMode.Weighted); Assert.Single(DialogueGraphService.Render(weighted, weighted.EntryNodeId, context).AvailableEdges);
        Assert.Equal(5, Enum.GetValues<DialogueEvaluationMode>().Length);
    }

    [Fact]
    public void Visual_builders_preserve_typed_source_and_validation_checks_graph_references_tokens_and_notes()
    {
        var expression = DialogueGraphService.BuildExpression(DialogueExpressionKind.GreaterThan, "trust", "3"); var action = DialogueGraphService.BuildAction(DialogueActionKind.ChangeReputation, "faction:vigil", "+2");
        Assert.StartsWith("builder:GreaterThan", expression.SourceDescription); Assert.StartsWith("builder:ChangeReputation", action.SourceDescription); Assert.Empty(DialogueGraphService.Validate(Graph()));
        var graph = Graph(); var entry = graph.Nodes[graph.EntryNodeId]; var invalid = graph with { Participants = [], Nodes = new Dictionary<Guid, DialogueNode> { [entry.Id] = entry with { TextTemplate = "No token", Notes = entry.Notes with { AuthorMention = "" }, Edges = [new(Guid.NewGuid(), -1, -1, null, [action with { SourceDescription = "" }])] } } };
        var diagnostics = DialogueGraphService.Validate(invalid); Assert.Contains(diagnostics, item => item.Code == "RPG_DIALOGUE_NODE_INVALID"); Assert.Contains(diagnostics, item => item.Code == "RPG_DIALOGUE_TOKEN_INVALID"); Assert.Contains(diagnostics, item => item.Code == "RPG_DIALOGUE_EDGE_INVALID"); Assert.Contains(diagnostics, item => item.Code == "RPG_DIALOGUE_NOTES_INVALID");
    }

    private static DialogueGraph ReplaceMode(DialogueGraph graph, DialogueEvaluationMode mode) { var entry = graph.Nodes[graph.EntryNodeId]; return graph with { Nodes = graph.Nodes.ToDictionary(item => item.Key, item => item.Key == entry.Id ? item.Value with { EvaluationMode = mode } : item.Value) }; }
    private static DialogueGraph Graph()
    {
        var entryId = Guid.NewGuid(); var yesId = Guid.NewGuid(); var noId = Guid.NewGuid(); var condition = DialogueGraphService.BuildExpression(DialogueExpressionKind.GreaterThan, "trust", "3");
        var notes = new DialogueNodeNotes("offer alliance", "hopeful", "brief:vigil", DialogueEditorialStatus.Review, ["record voice"], "@karen");
        var entry = new DialogueNode(entryId, "guide", "Bienvenue, {{hero}}.", [new("hero", DialogueTokenType.EntityName, true, null)], DialogueEvaluationMode.FirstValid, [new(yesId, 0, 3, condition, [DialogueGraphService.BuildAction(DialogueActionKind.ChangeReputation, "faction:vigil", "+1")]), new(noId, 1, 1, null, [])], notes);
        var leaf = new Func<Guid, string, DialogueNode>((id, text) => new(id, "guide", text, [], DialogueEvaluationMode.Manual, [], notes));
        return new(Guid.NewGuid(), 1, "author:test", "vigil-offer", entryId, [new("guide", ParticipantBindingKind.Role, "guide", "narrator"), new("narrator", ParticipantBindingKind.Entity, "entity:narrator", null)], new Dictionary<Guid, DialogueNode> { [entryId] = entry, [yesId] = leaf(yesId, "Ensemble."), [noId] = leaf(noId, "Une autre fois.") });
    }
}
