using Forge.RpgKit.Domain.Dialogues;

namespace Forge.RpgKit.Domain.Tests.Dialogues;

public sealed class DialogueAuthoringServiceTests
{
    [Fact]
    public void Subdialogue_contract_validates_versioned_typed_inputs_outputs_and_variable_contract()
    {
        var contract = new SubDialogueContract(Guid.NewGuid(), 2, new Dictionary<string, DialogueTokenType> { ["trust"] = DialogueTokenType.Integer }, new Dictionary<string, DialogueTokenType> { ["accepted"] = DialogueTokenType.Boolean }, new HashSet<string> { "party:vigil" });
        var valid = new SubDialogueInvocation(Guid.NewGuid(), contract, new Dictionary<string, object> { ["trust"] = 4 }, new Dictionary<string, string> { ["accepted"] = "memory:accepted" }); Assert.Empty(DialogueAuthoringService.ValidateSubDialogue(valid));
        var invalid = valid with { Inputs = new Dictionary<string, object> { ["trust"] = "wrong" }, OutputBindings = new Dictionary<string, string>() }; var diagnostics = DialogueAuthoringService.ValidateSubDialogue(invalid); Assert.Contains(diagnostics, item => item.Code == "RPG_SUBDIALOGUE_INPUT_INVALID"); Assert.Contains(diagnostics, item => item.Code == "RPG_SUBDIALOGUE_OUTPUT_INVALID");
    }

    [Fact]
    public void Typed_consequences_expose_quest_faction_item_world_impact()
    {
        Assert.Equal(4, Enum.GetValues<DialogueConsequenceKind>().Length); var node = Guid.NewGuid(); var consequences = new[] { new DialogueConsequence(DialogueConsequenceKind.Quest, "quest:vigil", "advance", "2", "The vigil advances."), new DialogueConsequence(DialogueConsequenceKind.Faction, "faction:glass", "reputation", "+3", "Glasskeepers approve.") };
        var impacts = DialogueAuthoringService.AnalyzeImpact(node, consequences, new Dictionary<string, IReadOnlyList<string>> { ["quest:vigil"] = ["journal:vigil", "ending:dawn"] }); Assert.Equal(2, impacts.Count); Assert.Contains("ending:dawn", impacts.Single(item => item.Consequence.Kind == DialogueConsequenceKind.Quest).AffectedResources);
    }

    [Fact]
    public void Coverage_generates_paths_conditions_cycles_uncovered_nodes_and_ratio()
    {
        var graph = Graph(); var coverage = DialogueAuthoringService.MeasureCoverage(graph, new HashSet<Guid> { graph.EntryNodeId }); Assert.NotEmpty(coverage.Paths); Assert.Equal(1d / graph.Nodes.Count, coverage.Ratio); Assert.Equal(graph.Nodes.Count - 1, coverage.UncoveredNodes.Count);
        var entry = graph.Nodes[graph.EntryNodeId]; var cyclic = graph with { Nodes = graph.Nodes.ToDictionary(item => item.Key, item => item.Key == entry.Id ? item.Value with { Edges = [new(entry.Id, 0, 1, null, [])] } : item.Value) }; Assert.Contains(DialogueAuthoringService.MeasureCoverage(cyclic, new HashSet<Guid>()).ImpossiblePaths, item => item.Reason == "cycle");
    }

    [Fact]
    public void Collaboration_has_stable_node_revision_lock_comments_semantic_diff_and_conflict_resolution()
    {
        var id = Guid.NewGuid(); var at = DateTimeOffset.UnixEpoch; var baseline = new DialogueNodeRevision(id, 1, "a", "{\"text\":\"base\"}", at); var ours = baseline with { Revision = 2, CanonicalJson = "{\"text\":\"ours\"}" }; var theirs = baseline with { Revision = 2, AuthorId = "b", CanonicalJson = "{\"text\":\"theirs\"}" };
        var preview = DialogueAuthoringService.Merge(baseline, ours, theirs, DialogueConflictResolution.Manual); Assert.False(preview.CanApply); Assert.Single(preview.Conflicts);
        var resolved = DialogueAuthoringService.Merge(baseline, ours, theirs, DialogueConflictResolution.Manual, "{\"text\":\"merged\"}"); Assert.True(resolved.CanApply); Assert.Contains("resolution:Manual", resolved.SemanticDiff);
        var nodeLock = new DialogueLock(id, "a", at.AddMinutes(5)); Assert.True(DialogueAuthoringService.CanEdit(nodeLock, "a", at)); Assert.False(DialogueAuthoringService.CanEdit(nodeLock, "b", at)); Assert.True(DialogueAuthoringService.CanEdit(nodeLock, "b", at.AddMinutes(6)));
        var comment = new DialogueComment(Guid.NewGuid(), id, "reviewer", "Clarify intent", false, at); Assert.False(comment.Resolved);
    }

    [Fact]
    public void Controlled_ai_requires_citations_style_preview_lint_then_explicit_transaction()
    {
        var brief = new DialogueAiBrief("A promise beneath the observatory", ["offer", "choice"], ["style:vigil"], ["PROJECT_CONTEXT.md"], "fr", "author"); var preview = DialogueAuthoringService.PreviewAi(brief, _ => Graph()); Assert.True(preview.CanApply); Assert.False(preview.Applied); Assert.Contains("ai-preview", preview.SemanticDiff); Assert.Single(preview.Citations);
        var applied = DialogueAuthoringService.ApplyAi(preview); Assert.True(applied.Applied); Assert.Contains("ai-apply", applied.SemanticDiff); Assert.Throws<InvalidOperationException>(() => DialogueAuthoringService.ApplyAi(applied));
        Assert.Throws<ArgumentException>(() => DialogueAuthoringService.PreviewAi(brief with { ContextCitations = [] }, _ => Graph()));
    }

    private static DialogueGraph Graph()
    {
        var entry = Guid.NewGuid(); var leaf = Guid.NewGuid(); var notes = new DialogueNodeNotes("style:vigil", "warm", "style:vigil", DialogueEditorialStatus.Review, [], "@author"); var condition = DialogueGraphService.BuildExpression(DialogueExpressionKind.Variable, "ready");
        return new(Guid.NewGuid(), 1, "author:test", "vigil", entry, [new("guide", ParticipantBindingKind.Role, "guide", null)], new Dictionary<Guid, DialogueNode> { [entry] = new(entry, "guide", "Speak.", [], DialogueEvaluationMode.FirstValid, [new(leaf, 0, 1, condition, [])], notes), [leaf] = new(leaf, "guide", "Done.", [], DialogueEvaluationMode.Manual, [], notes) });
    }
}
