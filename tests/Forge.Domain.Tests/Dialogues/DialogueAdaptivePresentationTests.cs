using Forge.Domain.Dialogues;

namespace Forge.Domain.Tests.Dialogues;

public sealed class DialogueAdaptivePresentationTests
{
    [Fact]
    public void TimedChoiceHonorsAuthorityRevisionAccessibilityPauseAndTimeout()
    {
        var dialogue = Adaptive();
        var rule = Assert.Single(dialogue.Presentation!.TimedChoices);
        var client = Context() with { IsServer = false };

        var denied = DialogueAdaptiveRuntimeResolver.Resolve(dialogue, dialogue.EntryNodeId, 6_000, client);
        var stale = DialogueAdaptiveRuntimeResolver.Resolve(dialogue, dialogue.EntryNodeId, 6_000,
            client with { IsServer = true, ServerRevision = 6 });
        var paused = DialogueAdaptiveRuntimeResolver.Resolve(dialogue, dialogue.EntryNodeId, 6_000,
            client with
            {
                IsServer = true,
                ServerRevision = 7,
                PauseRequested = true,
                AccessibilityPauseRequested = true,
            });
        var timedOut = DialogueAdaptiveRuntimeResolver.Resolve(dialogue, dialogue.EntryNodeId, 6_000,
            client with { IsServer = true, ServerRevision = 7 });

        Assert.Equal(2, denied.Diagnostics.Count(item => item.Code == DialogueAdaptiveRuntimeResolver.AuthorityDeniedCode));
        Assert.Equal(DialogueAdaptiveRuntimeResolver.RevisionStaleCode, Assert.Single(stale.Diagnostics).Code);
        Assert.True(paused.TimedChoice!.Paused);
        Assert.False(paused.TimedChoice.TimedOut);
        Assert.True(timedOut.TimedChoice!.TimedOut);
        Assert.Equal(rule.DefaultChoiceId, timedOut.TimedChoice.SelectedChoiceId);
        Assert.Equal("dialogue.timeout.default", timedOut.TimedChoice.TimeoutScriptId);
    }

    [Fact]
    public void VariantSelectionIsConditionalSeededAndAvoidsRecentVariant()
    {
        var dialogue = Adaptive();
        var evaluator = new VariableEvaluator(new Dictionary<string, string> { ["mood"] = "warm" });
        var context = Context();

        var first = DialogueAdaptiveRuntimeResolver.Resolve(dialogue, dialogue.EntryNodeId, 0, context, evaluator);
        var replay = DialogueAdaptiveRuntimeResolver.Resolve(dialogue, dialogue.EntryNodeId, 0, context, evaluator);
        var alternate = DialogueAdaptiveRuntimeResolver.Resolve(dialogue, dialogue.EntryNodeId, 0,
            context with
            {
                RecentVariantIds = new Dictionary<Guid, IReadOnlyList<Guid>>
                {
                    [first.Variant!.Set.Id] = [first.Variant.Variant.Id],
                },
            }, evaluator);

        Assert.Equal(first.Variant, replay.Variant);
        Assert.Equal("fr-FR", first.Variant!.Locale);
        Assert.NotEqual(first.Variant.Variant.Id, alternate.Variant!.Variant.Id);
        Assert.StartsWith("dialogue.variant.", first.Variant.Variant.Text.Key, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidTimedAndVariantReferencesProduceLocalizedStableDiagnostics()
    {
        var dialogue = Adaptive();
        var profile = dialogue.Presentation!;
        var timed = profile.TimedChoices[0] with
        {
            DefaultChoiceId = DialogueChoiceId.From(Guid.NewGuid()),
            DurationMilliseconds = 10,
            TimeoutScriptId = "../unsafe",
        };
        var first = profile.VariantSets[0].Variants[0];
        var invalid = dialogue.WithPresentation(profile with
        {
            TimedChoices = [timed],
            VariantSets = [profile.VariantSets[0] with
            {
                Variants = [first, first with { Id = timed.Id, Weight = 0 }],
            }],
        });

        var diagnostics = DialoguePresentationValidator.Validate(invalid);

        Assert.Contains(diagnostics, item => item.Code == "dialogue.timed_choice.default_missing");
        Assert.Contains(diagnostics, item => item.Code == "dialogue.timed_choice.invalid");
        Assert.Contains(diagnostics, item => item.Code == "dialogue.presentation.uuid_duplicate");
        Assert.Contains(diagnostics, item => item.Code == "dialogue.variant.invalid");
        Assert.All(diagnostics, item => Assert.StartsWith($"dialogues/{dialogue.Id}/presentation", item.Path));
    }

    [Fact]
    public void RequiredVariantWithoutEligibleConditionReturnsDiagnostic()
    {
        var source = Adaptive();
        var set = source.Presentation!.VariantSets[0];
        var dialogue = source.WithPresentation(source.Presentation with
        {
            VariantSets = [set with
            {
                Variants = set.Variants.Select(item => item with
                {
                    Conditions = [new(DialogueConditionKind.VariableEquals, "unavailable", "yes")],
                }).ToArray(),
            }],
        });
        var result = DialogueAdaptiveRuntimeResolver.Resolve(dialogue, dialogue.EntryNodeId, 0, Context(),
            new VariableEvaluator(new Dictionary<string, string>()));

        Assert.Equal(DialogueAdaptiveRuntimeResolver.VariantRequiredCode, Assert.Single(result.Diagnostics).Code);
        Assert.Null(result.Variant);
    }

    private static DialogueDefinition Adaptive()
    {
        var dialogue = DialoguePresentationTests.Complete();
        var node = dialogue.Nodes.Single(item => item.Id == dialogue.EntryNodeId);
        var defaultChoice = Assert.Single(node.Choices).Id;
        var profile = dialogue.Presentation! with
        {
            TimedChoices =
            [
                new(Guid.Parse("d1340000-0000-4000-8000-000000000001"), node.Id, 5_000, defaultChoice,
                    DialogueTimedChoicePausePolicy.AccessibilityOnly, DialogueRuntimeAuthorityScope.Server, 7,
                    "dialogue.timeout.default", "server:test", "author:test"),
            ],
            VariantSets =
            [
                new(Guid.Parse("d1340000-0000-4000-8000-000000000002"), node.Id, "elyra-entry",
                    DialogueVariantSeedScope.Session, 23, 1, DialogueRuntimeAuthorityScope.Server, true,
                    [
                        new(Guid.Parse("d1340000-0000-4000-8000-000000000003"),
                            new("dialogue.variant.warm", "La crypte nous écoute avec patience."), 3,
                            [new(DialogueConditionKind.VariableEquals, "mood", "warm")], "author:test"),
                        new(Guid.Parse("d1340000-0000-4000-8000-000000000004"),
                            new("dialogue.variant.neutral", "La crypte nous écoute."), 1, [], "author:test"),
                    ], "author:test"),
            ],
        };
        return dialogue.WithPresentation(profile);
    }

    private static DialogueAdaptiveRuntimeContext Context() => new("fr-FR", true, 7, false, false, 42,
        Guid.Parse("d1340000-0000-4000-8000-000000000010"),
        new Dictionary<Guid, IReadOnlyList<Guid>>());

    private sealed class VariableEvaluator(IReadOnlyDictionary<string, string> variables) : IDialogueConditionEvaluator
    {
        public bool IsSatisfied(DialogueCondition condition) => condition.Kind == DialogueConditionKind.VariableEquals &&
            variables.TryGetValue(condition.Key, out var value) &&
            string.Equals(value, condition.ExpectedValue, StringComparison.Ordinal);
    }
}
