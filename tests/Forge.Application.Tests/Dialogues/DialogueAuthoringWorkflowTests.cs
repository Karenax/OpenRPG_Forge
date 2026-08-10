using Forge.Application.Dialogues;
using Forge.Domain.Dialogues;

namespace Forge.Application.Tests.Dialogues;

public sealed class DialogueAuthoringWorkflowTests
{
    [Fact]
    public void Authoring_codec_round_trips_variants_conditions_actions_and_pseudo_localization()
    {
        var variants = DialogueAuthoringCodec.ParseVariants("dialogue.entry",
            "fr|feminine|One|Une braise\nen|any|Other|Embers");
        var conditions = DialogueAuthoringCodec.ParseConditions(
            "IS|HasItem|ash-key|true\nNOT|QuestStateEquals|crypt|completed");
        var actions = DialogueAuthoringCodec.ParseActions(
            "SetRelation|elyra|trusted|required\nPlayAudio|elyra.accept|play|optional");
        var text = new LocalizedText("dialogue.entry", "Embers awaken", variants);

        var restoredVariants = DialogueAuthoringCodec.ParseVariants(text.Key,
            DialogueAuthoringCodec.FormatVariants(text), text.Variants);
        Assert.Equal(text.Variants.Select(item => (item.Id, item.Locale, item.Gender, item.PluralCategory, item.Text)),
            restoredVariants.Select(item => (item.Id, item.Locale, item.Gender, item.PluralCategory, item.Text)));
        Assert.Equal(conditions, DialogueAuthoringCodec.ParseConditions(
            DialogueAuthoringCodec.FormatConditions(conditions), conditions));
        Assert.Equal(actions, DialogueAuthoringCodec.ParseActions(
            DialogueAuthoringCodec.FormatActions(actions), actions));
        Assert.StartsWith("⟦", DialogueAuthoringCodec.PseudoLocalize(text), StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => DialogueAuthoringCodec.ParseConditions("OR|Unknown|key|value"));
        Assert.Throws<ArgumentException>(() => DialogueAuthoringCodec.ParseState("missing-namespace=value"));
    }

    [Fact]
    public void Simulator_evaluates_real_conditions_commits_actions_and_writes_decision_journal()
    {
        var entry = DialogueNodeId.From(Guid.Parse("d0061000-0000-4000-8000-000000000001"));
        var end = DialogueNodeId.From(Guid.Parse("d0061000-0000-4000-8000-000000000002"));
        var choiceId = DialogueChoiceId.From(Guid.Parse("d0061000-0000-4000-8000-000000000003"));
        var choice = new DialogueChoice(choiceId, new("dialogue.accept", "Accept"),
            [new(DialogueConditionKind.HasItem, "ash-key", "true"),
             new(DialogueConditionKind.QuestStateEquals, "crypt", "completed", negated: true)],
            [new(DialogueActionKind.SetRelation, "elyra", "trusted"),
             new(DialogueActionKind.StartSequence, "crypt-opening", "start")],
            end,
            conditionCombination: DialogueConditionCombination.All);
        var dialogue = new DialogueDefinition(
            DialogueId.From(Guid.Parse("d0061000-0000-4000-8000-000000000004")),
            "simulation", "Simulation", entry,
            [
                new(entry, null, new("dialogue.entry", "Entry"),
                    [new(DialogueActionKind.PlayAudio, "elyra.entry", "play")], [choice]),
                new(end, null, new("dialogue.end", "End"),
                    [new(DialogueActionKind.SetVariable, "dialogue.done", "true")], role: DialogueNodeRole.End),
            ]);
        var initial = new Dictionary<string, string>
        {
            ["item:ash-key"] = "true",
            ["quest:crypt"] = "active",
        };

        var result = DialogueSimulationService.Run(dialogue, initial, [choiceId]);

        Assert.True(result.Succeeded);
        Assert.True(result.Completed);
        Assert.Equal([entry, end], result.VisitedNodes);
        Assert.Equal("trusted", result.FinalState.Values["relation:elyra"]);
        Assert.Equal("start", result.FinalState.Values["sequence:crypt-opening"]);
        Assert.Equal("true", result.FinalState.Values["variable:dialogue.done"]);
        Assert.Contains(result.Journal, item => item.Kind == "decision" && item.Message.Contains("NOT", StringComparison.Ordinal));
        Assert.Contains(result.Journal, item => item.Kind == "choice" && item.ChoiceId == choiceId);
    }

    [Fact]
    public void Simulator_reports_unavailable_scripted_choice_without_mutating_initial_state()
    {
        var entry = DialogueNodeId.From(Guid.Parse("d0062000-0000-4000-8000-000000000001"));
        var choiceId = DialogueChoiceId.From(Guid.Parse("d0062000-0000-4000-8000-000000000002"));
        var dialogue = new DialogueDefinition(
            DialogueId.From(Guid.Parse("d0062000-0000-4000-8000-000000000003")),
            "blocked", "Blocked", entry,
            [new(entry, null, new("dialogue.entry", "Entry"), choices:
            [new(choiceId, new("dialogue.choice", "Choice"),
                [new(DialogueConditionKind.HasItem, "missing", "true")])])]);
        var initial = new Dictionary<string, string> { ["variable:untouched"] = "yes" };

        var result = DialogueSimulationService.Run(dialogue, initial, [choiceId]);

        Assert.False(result.Succeeded);
        Assert.Equal("choice-unavailable", result.StopReason);
        Assert.Equal("yes", result.FinalState.Values["variable:untouched"]);
        Assert.Equal(DialogueSimulationService.ChoiceUnavailableCode, Assert.Single(result.Diagnostics).Code);
    }
}
