using Forge.Application.Dialogues;
using Forge.Domain.Dialogues;

namespace Forge.Application.Tests.Dialogues;

public sealed class DialogueAuthoringRuntimeServiceTests
{
    [Fact]
    public void Runtime_renders_typed_escaped_tokens_and_resolves_participant_fallback()
    {
        var dialogue = Authored(DialogueChoiceEvaluationMode.FirstValid);
        var rendered = DialogueAuthoringRuntimeService.Render(dialogue, dialogue.EntryNodeId,
            new DialogueAuthoringContext(new Dictionary<string, object> { ["hero"] = "<Elyra>" },
                new Dictionary<string, string> { ["role:narrator"] = "entity:elyra" }, 42),
            new AlwaysEvaluator());

        Assert.Equal("entity:elyra", rendered.SpeakerEntityId);
        Assert.Contains("&lt;Elyra&gt;", rendered.Text);
        Assert.DoesNotContain("<Elyra>", rendered.Text, StringComparison.Ordinal);
        Assert.Single(rendered.AvailableChoices);
        Assert.Empty(rendered.Diagnostics);
        Assert.Contains("hero", DialogueAuthoringRuntimeService.CompleteTokens(dialogue, "he"));
    }

    [Fact]
    public void Missing_or_wrong_token_reports_stable_path_without_crashing()
    {
        var dialogue = Authored(DialogueChoiceEvaluationMode.AllValid);
        var missing = DialogueAuthoringRuntimeService.Render(dialogue, dialogue.EntryNodeId,
            new DialogueAuthoringContext(new Dictionary<string, object>(),
                new Dictionary<string, string> { ["role:narrator"] = "entity:elyra" }, 1),
            new AlwaysEvaluator());
        var wrong = DialogueAuthoringRuntimeService.Render(dialogue, dialogue.EntryNodeId,
            new DialogueAuthoringContext(new Dictionary<string, object> { ["hero"] = 42 },
                new Dictionary<string, string> { ["role:narrator"] = "entity:elyra" }, 1),
            new AlwaysEvaluator());

        Assert.Contains(missing.Diagnostics, item => item.Code == DialogueAuthoringRuntimeService.TokenMissingCode &&
                                                     item.Path.Contains("/tokens/", StringComparison.Ordinal));
        Assert.Contains(wrong.Diagnostics, item => item.Code == DialogueAuthoringRuntimeService.TokenTypeCode);
        Assert.Equal(2, missing.AvailableChoices.Count);
    }

    [Fact]
    public void All_five_evaluation_modes_are_explicit_and_deterministic()
    {
        RenderedDialogueAuthoringNode Render(DialogueChoiceEvaluationMode mode, int seed = 11)
        {
            var dialogue = Authored(mode);
            return DialogueAuthoringRuntimeService.Render(dialogue, dialogue.EntryNodeId,
                new DialogueAuthoringContext(new Dictionary<string, object> { ["hero"] = "Elyra" },
                    new Dictionary<string, string> { ["role:narrator"] = "entity:elyra" }, seed),
                new AlwaysEvaluator());
        }

        Assert.Single(Render(DialogueChoiceEvaluationMode.FirstValid).AvailableChoices);
        Assert.Equal(2, Render(DialogueChoiceEvaluationMode.AllValid).AvailableChoices.Count);
        Assert.Equal(2, Render(DialogueChoiceEvaluationMode.Manual).AvailableChoices.Count);
        Assert.Single(Render(DialogueChoiceEvaluationMode.Weighted).AvailableChoices);
        Assert.Equal(Render(DialogueChoiceEvaluationMode.RandomDeterministic).AvailableChoices[0].Id,
            Render(DialogueChoiceEvaluationMode.RandomDeterministic).AvailableChoices[0].Id);
    }

    private static DialogueDefinition Authored(DialogueChoiceEvaluationMode mode)
    {
        var dialogue = CreateDialogue();
        var profile = DialogueAuthoringProfile.CreateDefault(dialogue, "author:r068");
        var token = new DialogueTextToken(Guid.NewGuid(), 1, "author:r068", "hero", DialogueTextTokenType.EntityName,
            true, null);
        profile = profile with
        {
            Participants = profile.Participants.Select(item => item.Role == "narrator"
                ? item with { Kind = DialogueParticipantBindingKind.Entity, Value = "entity:elyra" }
                : item).ToArray(),
            Nodes = profile.Nodes.Select(item => item.NodeId == dialogue.EntryNodeId
                ? item with { Tokens = [token], EvaluationMode = mode }
                : item).ToArray(),
        };
        return dialogue.WithAuthoring(profile);
    }

    private static DialogueDefinition CreateDialogue()
    {
        var entry = DialogueNodeId.Parse("d0681000-0000-4000-8000-000000000001");
        var first = DialogueNodeId.Parse("d0681000-0000-4000-8000-000000000002");
        var second = DialogueNodeId.Parse("d0681000-0000-4000-8000-000000000003");
        return new DialogueDefinition(DialogueId.Parse("d0681000-0000-4000-8000-000000000010"),
            "r068-runtime", "R068 runtime", entry,
            [
                new(entry, null, new("dialogue.r068.entry", "Bienvenue, {{hero}}."), choices:
                [
                    new(DialogueChoiceId.Parse("d0681000-0000-4000-8000-000000000020"),
                        new("dialogue.r068.first", "Premier"), targetNodeId: first, priority: 0),
                    new(DialogueChoiceId.Parse("d0681000-0000-4000-8000-000000000021"),
                        new("dialogue.r068.second", "Second"), targetNodeId: second, priority: 1),
                ], role: DialogueNodeRole.PlayerChoice),
                new(first, null, new("dialogue.r068.end.first", "Fin A"), role: DialogueNodeRole.End),
                new(second, null, new("dialogue.r068.end.second", "Fin B"), role: DialogueNodeRole.End),
            ], provenance: "author:r068");
    }

    private sealed class AlwaysEvaluator : IDialogueConditionEvaluator
    {
        public bool IsSatisfied(DialogueCondition condition) => true;
    }
}
