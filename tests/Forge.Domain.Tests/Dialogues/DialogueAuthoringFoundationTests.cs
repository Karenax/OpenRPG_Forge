using Forge.Domain.Dialogues;

namespace Forge.Domain.Tests.Dialogues;

public sealed class DialogueAuthoringFoundationTests
{
    [Fact]
    public void Localized_text_resolves_locale_gender_plural_and_falls_back_deterministically()
    {
        var text = new LocalizedText("dialogue.count", "Fallback",
        [
            LocalizedTextVariant.Create("dialogue.count", "fr", "Un objet", "any", DialoguePluralCategory.One),
            LocalizedTextVariant.Create("dialogue.count", "fr-FR", "Une héroïne", "feminine", DialoguePluralCategory.One),
            LocalizedTextVariant.Create("dialogue.count", "fr", "Des objets", "any", DialoguePluralCategory.Other),
        ], "author:test");

        Assert.Equal("Une héroïne", text.Resolve("fr-FR", "feminine", DialoguePluralCategory.One));
        Assert.Equal("Un objet", text.Resolve("fr-CA", "masculine", DialoguePluralCategory.One));
        Assert.Equal("Des objets", text.Resolve("fr", "any", DialoguePluralCategory.Many));
        Assert.Equal("Fallback", text.Resolve("de"));
        Assert.Equal(3, text.Variants.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public void Localized_text_rejects_duplicate_dimensions()
    {
        var variants = new[]
        {
            LocalizedTextVariant.Create("dialogue.duplicate", "fr", "A"),
            LocalizedTextVariant.Create("dialogue.duplicate", "fr", "B"),
        };

        Assert.Throws<ArgumentException>(() => new LocalizedText("dialogue.duplicate", "Fallback", variants));
    }

    [Fact]
    public void Conditions_support_all_any_and_not_with_explanations()
    {
        var item = new DialogueCondition(DialogueConditionKind.HasItem, "ash-key", "true");
        var quest = new DialogueCondition(DialogueConditionKind.QuestStateEquals, "crypt", "completed", negated: true);
        var evaluator = new DictionaryEvaluator(new Dictionary<string, bool>
        {
            ["ash-key"] = true,
            ["crypt"] = false,
        });
        DialogueChoice Choice(DialogueConditionCombination combination) => new(
            DialogueChoiceId.From(Guid.NewGuid()), new("dialogue.choice", "Choice"), [item, quest],
            conditionCombination: combination);

        var all = DialogueConditionLogic.Evaluate(Choice(DialogueConditionCombination.All), evaluator);
        var any = DialogueConditionLogic.Evaluate(Choice(DialogueConditionCombination.Any), evaluator);

        Assert.True(all.Available);
        Assert.True(any.Available);
        Assert.Contains(all.Conditions, condition => condition.Negated && condition.Result);
        Assert.All(all.Conditions, condition => Assert.Contains("=>", condition.Explanation, StringComparison.Ordinal));
    }

    [Fact]
    public void Action_transaction_commits_all_or_rolls_back_required_failure()
    {
        var before = new DialogueSandboxState(new Dictionary<string, string> { ["variable:door"] = "closed" });
        var committed = DialogueActionTransaction.Apply(before,
        [
            new(DialogueActionKind.SetVariable, "door", "open"),
            new(DialogueActionKind.PlayAudio, "door.open", "play"),
        ]);
        var invalid = new DialogueAction(Guid.NewGuid(), 1, "author:test", (DialogueActionKind)999,
            "bad", "value", true);
        var rolledBack = DialogueActionTransaction.Apply(before,
        [
            new(DialogueActionKind.SetVariable, "door", "open"),
            invalid,
        ]);

        Assert.True(committed.Succeeded);
        Assert.Equal("open", committed.State.Values["variable:door"]);
        Assert.Equal("play", committed.State.Values["audio:door.open"]);
        Assert.False(rolledBack.Succeeded);
        Assert.Same(before, rolledBack.State);
        Assert.Empty(rolledBack.AppliedActions);
        Assert.Equal(DialogueActionTransaction.InvalidActionCode, Assert.Single(rolledBack.Diagnostics).Code);
    }

    [Fact]
    public void Structural_linter_reports_contradiction_and_missing_terminal_with_stable_paths()
    {
        var entry = DialogueNodeId.From(Guid.Parse("d0060000-0000-4000-8000-000000000001"));
        var loop = new DialogueChoice(DialogueChoiceId.From(Guid.Parse("d0060000-0000-4000-8000-000000000002")),
            new("dialogue.loop", "Loop"),
            [new(DialogueConditionKind.VariableEquals, "door", "open"),
             new(DialogueConditionKind.VariableEquals, "door", "closed")],
            targetNodeId: entry,
            edgeKind: DialogueEdgeKind.Reference);
        var dialogue = new DialogueDefinition(
            DialogueId.From(Guid.Parse("d0060000-0000-4000-8000-000000000003")),
            "lint", "Lint", entry,
            [new(entry, null, new("dialogue.entry", "Entry"), choices: [loop])]);

        var diagnostics = DialogueValidator.Validate(dialogue);

        Assert.Contains(diagnostics, item => item.Code == DialogueValidator.ContradictoryConditionCode &&
                                             item.Path.Contains(loop.Id.ToString(), StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == DialogueValidator.MissingTerminalNodeCode);
    }

    [Fact]
    public void Structural_linter_reports_localization_condition_and_action_types()
    {
        var entry = DialogueNodeId.From(Guid.Parse("d0065000-0000-4000-8000-000000000001"));
        var badText = new LocalizedText(Guid.NewGuid(), 99, "author:test", "dialogue.invalid", "Invalid", []);
        var badCondition = new DialogueCondition(Guid.NewGuid(), 1, "author:test",
            (DialogueConditionKind)999, "condition", "value", false, "on-choice-visible", 1);
        var badAction = new DialogueAction(Guid.NewGuid(), 1, "author:test",
            (DialogueActionKind)999, "action", "value", true);
        var dialogue = new DialogueDefinition(
            DialogueId.From(Guid.Parse("d0065000-0000-4000-8000-000000000002")),
            "invalid-types", "Invalid types", entry,
            [new(entry, null, badText, [badAction],
                [new(DialogueChoiceId.From(Guid.Parse("d0065000-0000-4000-8000-000000000003")),
                    new("dialogue.exit", "Exit"), [badCondition])])]);

        var diagnostics = DialogueValidator.Validate(dialogue);

        Assert.Contains(diagnostics, item => item.Code == DialogueValidator.LocalizationInvalidCode &&
                                             item.Path.EndsWith("/text", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == DialogueValidator.ConditionInvalidCode &&
                                             item.Path.Contains("/conditions/0", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == DialogueValidator.ActionInvalidCode &&
                                             item.Path.Contains("/onEnterActions/0", StringComparison.Ordinal));
    }

    private sealed class DictionaryEvaluator(IReadOnlyDictionary<string, bool> values) : IDialogueConditionEvaluator
    {
        public bool IsSatisfied(DialogueCondition condition) => values.GetValueOrDefault(condition.Key);
    }
}
