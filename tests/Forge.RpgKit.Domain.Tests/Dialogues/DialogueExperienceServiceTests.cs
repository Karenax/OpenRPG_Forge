using Forge.RpgKit.Domain.Dialogues;

namespace Forge.RpgKit.Domain.Tests.Dialogues;

public sealed class DialogueExperienceServiceTests
{
    [Fact]
    public void Completion_and_abort_carry_reason_optional_rollback_and_resume_snapshot()
    {
        var graph = Graph("vigil", "La lumière revient."); var at = DateTimeOffset.UnixEpoch; var memory = EmptyMemory();
        var completed = DialogueExperienceService.Complete(graph, "player", [DialogueGraphService.BuildAction(DialogueActionKind.EmitEvent, "vigil", "complete")], at); Assert.Equal(graph.Id, completed.DialogueId); Assert.Single(completed.Actions);
        var aborted = DialogueExperienceService.Abort(graph, "player", graph.EntryNodeId, DialogueAbortReason.CombatStarted, true, true, memory, 9, at); Assert.True(aborted.RolledBack); Assert.NotNull(aborted.ResumeState); Assert.Equal(9, aborted.ResumeState.Seed);
    }

    [Fact]
    public void Search_filters_multiple_dialogues_and_regex_replace_remains_preview_only()
    {
        var graphs = new[] { Graph("one", "Éclat rouge"), Graph("two", "Éclat bleu") }; var query = new DialogueSearchQuery("Éclat (rouge|bleu)", true, false, new HashSet<string> { "guide" }, new HashSet<DialogueExpressionKind>(), new HashSet<DialogueActionKind>());
        var hits = DialogueExperienceService.Search(graphs, query); Assert.Equal(2, hits.Count);
        var preview = DialogueExperienceService.PreviewReplace(graphs, query, "Lueur"); Assert.Equal(2, preview.Replacements.Count); Assert.All(preview.Replacements.Values, value => Assert.Equal("Lueur", value)); Assert.Contains("preview-only:true", preview.SemanticDiff);
        Assert.Equal("Éclat rouge", graphs[0].Nodes[graphs[0].EntryNodeId].TextTemplate);
    }

    [Fact]
    public void Review_badges_and_filters_cover_condition_action_voice_localization_error_and_link()
    {
        var graph = Graph("review", "Bonjour {{hero}}"); var result = DialogueExperienceService.Review([graph], new(new HashSet<DialogueBadge> { DialogueBadge.Condition, DialogueBadge.Action, DialogueBadge.Voice, DialogueBadge.Localization, DialogueBadge.Link }, new HashSet<string>(), true));
        Assert.Single(result); Assert.Equal(DialogueEditorialStatus.Review, result[0].Status); Assert.Contains(DialogueBadge.Voice, result[0].Badges);
    }

    [Fact]
    public void Typed_memory_supports_all_scopes_durations_and_expiration()
    {
        Assert.Equal(5, Enum.GetValues<ConversationMemoryScope>().Length); Assert.Equal(5, Enum.GetValues<ConversationMemoryDuration>().Length);
        var key = new ConversationMemoryKey(ConversationMemoryScope.Party, "party:one", "trust"); var at = DateTimeOffset.UnixEpoch; var value = new ConversationMemoryValue(DialogueTokenType.Integer, 4, ConversationMemoryDuration.Campaign, at, at.AddHours(1));
        var memory = DialogueExperienceService.Write(EmptyMemory(), key, value); Assert.Equal(4, DialogueExperienceService.Read(memory, key, at.AddMinutes(1))); Assert.Null(DialogueExperienceService.Read(memory, key, at.AddHours(2)));
        Assert.Throws<ArgumentException>(() => DialogueExperienceService.Write(memory, key, value with { Value = "wrong" }));
    }

    [Fact]
    public void Skill_check_is_seeded_data_driven_and_explains_visibility_specific_result()
    {
        var success = Guid.NewGuid(); var failure = Guid.NewGuid(); var check = new SkillCheckNode(Guid.NewGuid(), "resonance", 12, "opponent:lyra", true, SkillCheckVisibility.FullExplanation, success, failure);
        var first = DialogueExperienceService.Resolve(check, 5, 2, 42); var repeated = DialogueExperienceService.Resolve(check, 5, 2, 42); Assert.Equal(first, repeated); Assert.Contains("skill 5", first.Explanation); Assert.Equal(first.Success ? success : failure, first.TargetNodeId);
        Assert.Empty(DialogueExperienceService.Resolve(check with { Visibility = SkillCheckVisibility.Hidden }, 5, 2, 42).Explanation);
    }

    private static ConversationMemory EmptyMemory() => new(new Dictionary<ConversationMemoryKey, ConversationMemoryValue>());
    private static DialogueGraph Graph(string slug, string text)
    {
        var entry = Guid.NewGuid(); var leaf = Guid.NewGuid(); var notes = new DialogueNodeNotes("advance", "calm", "brief", DialogueEditorialStatus.Review, ["record voice"], "@author"); var condition = DialogueGraphService.BuildExpression(DialogueExpressionKind.Variable, "ready");
        var node = new DialogueNode(entry, "guide", text, text.Contains("{{hero}}", StringComparison.Ordinal) ? [new("hero", DialogueTokenType.EntityName, true, null)] : [], DialogueEvaluationMode.FirstValid, [new(leaf, 0, 1, condition, [DialogueGraphService.BuildAction(DialogueActionKind.SetVariable, "heard", "true")])], notes);
        return new(Guid.NewGuid(), 1, "author:test", slug, entry, [new("guide", ParticipantBindingKind.Role, "guide", null)], new Dictionary<Guid, DialogueNode> { [entry] = node, [leaf] = new(leaf, "guide", "Fin", [], DialogueEvaluationMode.Manual, [], notes) });
    }
}
