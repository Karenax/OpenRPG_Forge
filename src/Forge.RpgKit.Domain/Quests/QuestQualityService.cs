namespace Forge.RpgKit.Domain.Quests;

public enum QuestTestKind { Nominal, Failure, Repetition, Multiplayer, SaveResume }
public enum QuestTestAssertionKind { StateEquals, ObjectiveProgress, RewardGranted, HistoryContains, AuthorityAccepted, PersistenceRoundTrip }
public sealed record QuestTestStep(string Action, IReadOnlyDictionary<string, string> Parameters);
public sealed record QuestTestAssertion(QuestTestAssertionKind Kind, string Path, string Expected);
public sealed record QuestTestScenario(Guid Id, QuestTestKind Kind, string Name, int Seed, IReadOnlyList<QuestTestStep> Steps, IReadOnlyList<QuestTestAssertion> Assertions, IReadOnlySet<string> RequiredFixtures);
public sealed record QuestTestRun(Guid ScenarioId, bool Passed, IReadOnlyList<string> Failures, IReadOnlyDictionary<string, string> FinalState);
public sealed record QuestAiBrief(string Synopsis, IReadOnlyList<string> RequiredStates, IReadOnlyList<string> ParticipantRoles, IReadOnlyList<string> DependencyIds, IReadOnlyList<string> ContextCitations, IReadOnlyList<string> StyleRules, string AuthorId);
public sealed record QuestAiDraft(QuestDefinition Quest, IReadOnlyList<string> Citations, IReadOnlyList<QuestDiagnostic> Lint, IReadOnlyList<string> DependencyWarnings, IReadOnlyList<string> StyleViolations, string SemanticDiff, bool CanApply, bool HumanApproved, bool Applied);

public static class QuestQualityService
{
    public static IReadOnlyList<QuestTestScenario> GenerateTests(QuestDefinition quest)
    {
        var fixtures = quest.ZoneIds.Concat(quest.Participants.Select(item => item.BindingValue)).Concat(quest.PropIds).ToHashSet(StringComparer.Ordinal);
        QuestTestScenario Make(QuestTestKind kind, int seed, IReadOnlyList<QuestTestStep> steps, IReadOnlyList<QuestTestAssertion> assertions)
        {
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"openrpg:quest-test:{quest.Id}:{kind}"));
            return new(new Guid(hash[..16]), kind, $"{quest.Slug}:{kind}", seed, steps, assertions, fixtures);
        }
        return
        [
            Make(QuestTestKind.Nominal, 100, [new("start", new Dictionary<string, string>()), new("complete-all-objectives", new Dictionary<string, string>())], [new(QuestTestAssertionKind.StateEquals, "quest/state", "completed"), new(QuestTestAssertionKind.RewardGranted, "quest/rewards", "true")]),
            Make(QuestTestKind.Failure, 101, [new("start", new Dictionary<string, string>()), new("fail-required-objective", new Dictionary<string, string>())], [new(QuestTestAssertionKind.StateEquals, "quest/state", "failed")]),
            Make(QuestTestKind.Repetition, 102, [new("complete-transition-twice", new Dictionary<string, string>())], [new(QuestTestAssertionKind.HistoryContains, "quest/history/idempotent", "true")]),
            Make(QuestTestKind.Multiplayer, 103, [new("submit-client-intent", new Dictionary<string, string> { ["authority"] = "server" })], [new(QuestTestAssertionKind.AuthorityAccepted, "quest/authority", "server")]),
            Make(QuestTestKind.SaveResume, 104, [new("save-mid-objective", new Dictionary<string, string>()), new("reload", new Dictionary<string, string>())], [new(QuestTestAssertionKind.PersistenceRoundTrip, "quest/snapshot", "equal")])
        ];
    }

    public static QuestTestRun Run(QuestTestScenario scenario, Func<QuestTestStep, IReadOnlyDictionary<string, string>, IReadOnlyDictionary<string, string>> execute)
    {
        IReadOnlyDictionary<string, string> state = new Dictionary<string, string>(); foreach (var step in scenario.Steps) state = execute(step, state);
        var failures = scenario.Assertions.Where(assertion => state.GetValueOrDefault(assertion.Path) != assertion.Expected).Select(assertion => $"{assertion.Kind}:{assertion.Path}:expected={assertion.Expected}:actual={state.GetValueOrDefault(assertion.Path)}").ToArray();
        return new(scenario.Id, failures.Length == 0, failures, state);
    }

    public static QuestAiDraft PreviewAi(QuestAiBrief brief, QuestValidationContext context, Func<QuestAiBrief, QuestDefinition> generator)
    {
        if (string.IsNullOrWhiteSpace(brief.Synopsis) || brief.RequiredStates.Count == 0 || brief.ParticipantRoles.Count == 0 || brief.ContextCitations.Count == 0 || brief.StyleRules.Count == 0) throw new ArgumentException("Quest AI brief is incomplete.");
        var quest = generator(brief); var lint = QuestFoundationService.Validate(quest, context); var known = context.ZoneIds.Concat(context.ParticipantBindings).Concat(context.PropIds).Concat(context.TransitionIds).Concat(context.CurrencyIds).Concat(context.FactionIds).Concat(context.UnlockIds).ToHashSet(StringComparer.Ordinal); var dependencies = brief.DependencyIds.Where(id => !known.Contains(id)).Select(id => $"missing-dependency:{id}").ToArray(); var style = brief.StyleRules.Where(rule => !quest.Notes.Sources.Any(source => source.Contains(rule, StringComparison.OrdinalIgnoreCase))).Select(rule => $"unproven-style-rule:{rule}").ToArray();
        return new(quest, brief.ContextCitations, lint, dependencies, style, $"ai-preview:add-quest:{quest.Id}|objectives:{quest.Objectives.Count}|dependencies:{brief.DependencyIds.Count}|citations:{brief.ContextCitations.Count}", lint.Count == 0 && dependencies.Length == 0 && style.Length == 0, false, false);
    }

    public static QuestAiDraft Approve(QuestAiDraft preview, string reviewerId)
    {
        if (!preview.CanApply || preview.Applied || string.IsNullOrWhiteSpace(reviewerId)) throw new InvalidOperationException("Quest draft cannot be approved."); return preview with { HumanApproved = true, SemanticDiff = preview.SemanticDiff + $"|approved-by:{reviewerId}" };
    }
    public static QuestAiDraft Apply(QuestAiDraft approved)
    {
        if (!approved.CanApply || !approved.HumanApproved || approved.Applied) throw new InvalidOperationException("Quest draft requires one human-approved transaction."); return approved with { Applied = true, SemanticDiff = approved.SemanticDiff.Replace("ai-preview:", "ai-apply:", StringComparison.Ordinal) };
    }
}
