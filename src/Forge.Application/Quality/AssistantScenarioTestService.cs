using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Assistant;
using Forge.Domain.Dialogues;
using Forge.Domain.Projects;
using Forge.Domain.Quests;

namespace Forge.Application.Quality;

public enum AssistantScenarioFindingSeverity { Information, Warning, Blocking }

public sealed record AssistantScenarioFinding(
    Guid Id,
    string Code,
    AssistantScenarioFindingSeverity Severity,
    string Message,
    string DocumentPath,
    Guid? ObjectId,
    IReadOnlyList<string> Citations,
    IReadOnlyList<string> ReproductionSteps,
    string? SuggestedTransaction);

public sealed record AssistantScenarioCoverage(
    int Dialogues,
    int DialogueNodesCovered,
    int DialogueNodesTotal,
    int DialogueChoicesCovered,
    int DialogueChoicesTotal,
    int Quests,
    int QuestStatesCovered,
    int QuestStatesTotal,
    int QuestTransitionsCovered,
    int QuestTransitionsTotal);

public sealed record AssistantScenarioTestReport(
    Guid ProfileId,
    int SchemaVersion,
    string Provenance,
    bool Succeeded,
    AssistantScenarioCoverage Coverage,
    IReadOnlyList<string> GameplayActions,
    IReadOnlyList<AssistantScenarioFinding> Findings,
    string Fingerprint);

public static class AssistantScenarioTestService
{
    public const string DialoguePathBlockedCode = "assistant.test.dialogue_path_blocked";
    public const string DialogueConditionContradictionCode = "assistant.test.dialogue_condition_contradiction";
    public const string DialogueToneInconsistentCode = "assistant.test.dialogue_tone_inconsistent";
    public const string QuestStateUnreachableCode = "assistant.test.quest_state_unreachable";
    public const string QuestTransitionUnreachableCode = "assistant.test.quest_transition_unreachable";
    public const string QuestCycleCode = "assistant.test.quest_cycle";
    public const string QuestRewardExploitCode = "assistant.test.quest_reward_exploit";
    public const string QuestMultiplayerDivergenceCode = "assistant.test.quest_multiplayer_divergence";
    public const string QuestSaveResumeMismatchCode = "assistant.test.quest_save_resume_mismatch";
    public const string NarrativeConflictCode = "assistant.test.narrative_conflict";

    public static AssistantScenarioTestReport Run(
        ProjectDefinition project,
        AssistantTestAutomationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(profile);
        var profileDiagnostics = AssistantTestAutomationProfileValidator.Validate(profile);
        if (profileDiagnostics.Count > 0)
            throw new ArgumentException(profileDiagnostics[0].Code, nameof(profile));

        var findings = new List<AssistantScenarioFinding>();
        var dialogueReports = new List<(DialogueDefinition Dialogue, DialogueQualityReport? Report)>();
        foreach (var dialogue in project.Dialogues)
        {
            var validation = DialogueValidator.Validate(dialogue);
            if (validation.Count > 0)
            {
                foreach (var diagnostic in validation)
                {
                    var contradiction = diagnostic.Code.Contains("contradiction", StringComparison.OrdinalIgnoreCase) ||
                                        diagnostic.Message.Contains("cannot all be true", StringComparison.OrdinalIgnoreCase);
                    Add(findings, contradiction ? DialogueConditionContradictionCode : DialoguePathBlockedCode,
                        AssistantScenarioFindingSeverity.Blocking, diagnostic.Message, diagnostic.Path,
                        dialogue.Id.Value, [diagnostic.Path],
                        ["Open the cited dialogue document.", "Run canonical dialogue validation."],
                        contradiction ? $"dialogue.update_choice_conditions:{dialogue.Id}" : null);
                }
                dialogueReports.Add((dialogue, null));
                continue;
            }
            var report = NarrativeQualityService.TestDialogue(dialogue,
                maximumDepth: profile.MaximumScenarioSteps);
            dialogueReports.Add((dialogue, report));
            InspectDialogue(dialogue, report, profile, findings);
        }
        var questReports = new List<(QuestDefinition Quest, QuestQualityReport? Report)>();
        foreach (var quest in project.Quests)
        {
            var validation = QuestValidator.Validate(quest);
            if (validation.Count > 0)
            {
                foreach (var diagnostic in validation)
                {
                    var reward = diagnostic.Code.Contains("reward", StringComparison.OrdinalIgnoreCase) ||
                                 diagnostic.Message.Contains("reward", StringComparison.OrdinalIgnoreCase);
                    Add(findings, reward ? QuestRewardExploitCode : QuestCycleCode,
                        AssistantScenarioFindingSeverity.Blocking, diagnostic.Message, diagnostic.Path,
                        quest.Id.Value, [diagnostic.Path],
                        ["Open the cited quest document.", "Run canonical quest validation."],
                        reward ? $"quest.add_idempotence:{quest.Id}" : null);
                }
                questReports.Add((quest, null));
                continue;
            }
            var report = NarrativeQualityService.TestQuest(quest,
                maximumDepth: profile.MaximumScenarioSteps);
            questReports.Add((quest, report));
            InspectQuest(quest, report, profile, findings);
        }
        InspectNarrativeConflicts(project, findings);

        var bounded = findings
            .DistinctBy(item => (item.Code, item.DocumentPath, item.Message))
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.DocumentPath, StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .Take(profile.MaximumDialoguePaths + profile.MaximumQuestPaths)
            .ToArray();
        var coverage = new AssistantScenarioCoverage(
            dialogueReports.Count,
            dialogueReports.Sum(item => item.Report?.CoveredNodes ?? 0),
            project.Dialogues.Sum(dialogue => dialogue.Nodes.Count),
            dialogueReports.Sum(item => item.Report?.CoveredChoices ?? 0),
            project.Dialogues.Sum(dialogue => dialogue.Nodes.Sum(node => node.Choices.Count)),
            questReports.Count,
            questReports.Sum(item => item.Report?.CoveredStates ?? 0),
            project.Quests.Sum(quest => quest.States.Count),
            questReports.Sum(item => item.Report?.CoveredTransitions ?? 0),
            project.Quests.Sum(quest => quest.Transitions.Count));
        var fingerprint = Fingerprint(profile, coverage, bounded);
        return new(profile.Id, profile.SchemaVersion, profile.Provenance,
            bounded.All(item => item.Severity != AssistantScenarioFindingSeverity.Blocking),
            coverage, profile.AllowedGameplayActions, bounded, fingerprint);
    }

    private static void InspectDialogue(
        DialogueDefinition dialogue,
        DialogueQualityReport report,
        AssistantTestAutomationProfile profile,
        ICollection<AssistantScenarioFinding> findings)
    {
        foreach (var path in report.Paths.Where(path => path.StopReason is not ("completed" or "no-available-choice"))
                     .Take(profile.MaximumDialoguePaths))
        {
            var last = path.Nodes.LastOrDefault();
            Add(findings, DialoguePathBlockedCode, AssistantScenarioFindingSeverity.Blocking,
                $"Dialogue path stops with '{path.StopReason}'.", $"dialogues/{dialogue.Id}/nodes/{last}",
                last?.Value, [$"dialogues/{dialogue.Id}"],
                [$"Load fixture '{path.FixtureId}'.", $"Follow nodes: {string.Join(" -> ", path.Nodes)}."], null);
        }
        foreach (var node in dialogue.Nodes)
            foreach (var choice in node.Choices.Where(choice => choice.ConditionCombination == DialogueConditionCombination.All))
                foreach (var contradiction in choice.Conditions.GroupBy(condition => (condition.Kind, condition.Key))
                             .Where(group => group.Select(condition => condition.ExpectedValue).Distinct(StringComparer.Ordinal).Count() > 1))
                {
                    Add(findings, DialogueConditionContradictionCode, AssistantScenarioFindingSeverity.Blocking,
                        $"Choice requires contradictory values for '{contradiction.Key.Key}'.",
                        $"dialogues/{dialogue.Id}/nodes/{node.Id}/choices/{choice.Id}/conditions", choice.Id.Value,
                        [$"dialogues/{dialogue.Id}/nodes/{node.Id}/choices/{choice.Id}"],
                        ["Open the dialogue simulation.", $"Inspect choice '{choice.Text.Fallback}'."],
                        $"dialogue.update_choice_conditions:{choice.Id}");
                }
        if (!profile.DetectToneInconsistencies || !dialogue.Metadata.TryGetValue("tone", out var expectedTone)) return;
        foreach (var node in dialogue.Nodes)
        {
            var key = $"tone.{node.Id.Value:D}";
            if (dialogue.Metadata.TryGetValue(key, out var nodeTone) &&
                !string.Equals(nodeTone, expectedTone, StringComparison.OrdinalIgnoreCase))
                Add(findings, DialogueToneInconsistentCode, AssistantScenarioFindingSeverity.Warning,
                    $"Node tone '{nodeTone}' conflicts with dialogue tone '{expectedTone}'.",
                    $"dialogues/{dialogue.Id}/metadata/{key}", node.Id.Value,
                    [$"dialogues/{dialogue.Id}/metadata/tone", $"dialogues/{dialogue.Id}/metadata/{key}"],
                    ["Open the dialogue tone review.", $"Compare node '{node.Text.Fallback}' with the declared tone."],
                    $"dialogue.update_tone:{node.Id}");
        }
    }

    private static void InspectQuest(
        QuestDefinition quest,
        QuestQualityReport report,
        AssistantTestAutomationProfile profile,
        ICollection<AssistantScenarioFinding> findings)
    {
        var coveredStates = report.Paths.SelectMany(path => path.States).ToHashSet();
        foreach (var state in quest.States.Where(state => !coveredStates.Contains(state.Id)))
            Add(findings, QuestStateUnreachableCode, AssistantScenarioFindingSeverity.Blocking,
                $"Quest state '{state.DisplayName}' is unreachable.", $"quests/{quest.Id}/states/{state.Id}",
                state.Id.Value, [$"quests/{quest.Id}/states/{state.Id}"],
                ["Start from the configured initial state.", "Explore all eligible transitions."], null);
        var coveredTransitions = report.Paths.SelectMany(path => path.Transitions).ToHashSet();
        foreach (var transition in quest.Transitions.Where(transition => !coveredTransitions.Contains(transition.Id)))
            Add(findings, QuestTransitionUnreachableCode, AssistantScenarioFindingSeverity.Blocking,
                "Quest transition is unreachable.", $"quests/{quest.Id}/transitions/{transition.Id}",
                transition.Id.Value, [$"quests/{quest.Id}/transitions/{transition.Id}"],
                ["Start from the configured initial state.", "Apply generated state fixtures."], null);
        foreach (var path in report.Paths.Where(path => path.StopReason == "cycle").Take(profile.MaximumQuestPaths))
        {
            var reward = profile.DetectRewardExploits && path.Actions.Any(action => action.Kind == QuestActionKind.GiveItem);
            Add(findings, reward ? QuestRewardExploitCode : QuestCycleCode,
                reward ? AssistantScenarioFindingSeverity.Blocking : AssistantScenarioFindingSeverity.Warning,
                reward ? "Repeatable quest cycle can grant a reward more than once." : "Quest path contains a cycle.",
                $"quests/{quest.Id}", quest.Id.Value, [$"quests/{quest.Id}"],
                [$"Load fixture '{path.FixtureId}'.", $"Follow states: {string.Join(" -> ", path.States)}."],
                reward ? $"quest.add_idempotence:{quest.Id}" : null);
        }
        if (profile.VerifySaveResume)
            foreach (var path in report.Paths.Where(path => !path.SaveResumeEqual))
                Add(findings, QuestSaveResumeMismatchCode, AssistantScenarioFindingSeverity.Blocking,
                    "Quest state differs after save and resume.", $"quests/{quest.Id}", quest.Id.Value,
                    [$"quests/{quest.Id}"], ["Advance one transition.", "Save, reload and compare the canonical state."], null);
        if (profile.VerifyMultiplayerDeterminism && quest.InstancePolicy.AllowedScopes.Count > 1)
        {
            var sequences = report.Paths.GroupBy(path => path.Scope)
                .ToDictionary(group => group.Key, group => group.Select(path => string.Join('/', path.Transitions))
                    .Order(StringComparer.Ordinal).ToArray());
            if (sequences.Count > 1 && sequences.Values.Skip(1).Any(value => !value.SequenceEqual(sequences.Values.First())))
                Add(findings, QuestMultiplayerDivergenceCode, AssistantScenarioFindingSeverity.Blocking,
                    "Quest transition coverage diverges across configured multiplayer scopes.",
                    $"quests/{quest.Id}/instancePolicy", quest.Id.Value, [$"quests/{quest.Id}/instancePolicy"],
                    ["Run identical fixtures for every allowed scope.", "Compare ordered transition sequences."], null);
        }
    }

    private static void InspectNarrativeConflicts(
        ProjectDefinition project,
        ICollection<AssistantScenarioFinding> findings)
    {
        var entries = project.Dialogues.SelectMany(dialogue => dialogue.Nodes.Select(node =>
                (node.Text.Key, node.Text.Fallback, Path: $"dialogues/{dialogue.Id}/nodes/{node.Id}", Id: node.Id.Value)))
            .Concat(project.Dialogues.SelectMany(dialogue => dialogue.Nodes.SelectMany(node => node.Choices.Select(choice =>
                (choice.Text.Key, choice.Text.Fallback, Path: $"dialogues/{dialogue.Id}/nodes/{node.Id}/choices/{choice.Id}", Id: choice.Id.Value)))))
            .Concat(project.Quests.SelectMany(quest => new[]
            {
                (quest.Title.Key, quest.Title.Fallback, Path: $"quests/{quest.Id}/title", Id: quest.Id.Value),
                (quest.Description.Key, quest.Description.Fallback, Path: $"quests/{quest.Id}/description", Id: quest.Id.Value),
            }));
        foreach (var conflict in entries.GroupBy(entry => entry.Key, StringComparer.Ordinal)
                     .Where(group => group.Select(entry => entry.Fallback).Distinct(StringComparer.Ordinal).Count() > 1))
        {
            var sources = conflict.OrderBy(entry => entry.Path, StringComparer.Ordinal).ToArray();
            Add(findings, NarrativeConflictCode, AssistantScenarioFindingSeverity.Warning,
                $"Narrative key '{conflict.Key}' has conflicting canonical text.", sources[0].Path, sources[0].Id,
                sources.Select(source => source.Path).ToArray(),
                ["Open every cited source.", "Compare fallback text without rewriting canon automatically."],
                $"localization.resolve_conflict:{conflict.Key}");
        }
    }

    private static void Add(
        ICollection<AssistantScenarioFinding> findings,
        string code,
        AssistantScenarioFindingSeverity severity,
        string message,
        string path,
        Guid? objectId,
        IReadOnlyList<string> citations,
        IReadOnlyList<string> reproduction,
        string? transaction) => findings.Add(new(StableId($"{code}|{path}|{message}"), code, severity, message,
            path, objectId, citations, reproduction, transaction));

    private static string Fingerprint(
        AssistantTestAutomationProfile profile,
        AssistantScenarioCoverage coverage,
        IReadOnlyList<AssistantScenarioFinding> findings)
    {
        var canonical = new StringBuilder().Append(profile.Id).Append('|').Append(profile.SchemaVersion).Append('|')
            .Append(coverage).Append('|');
        foreach (var finding in findings) canonical.Append(finding.Id).Append('|').Append(finding.Code).Append('|');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private static Guid StableId(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> id = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(id);
        id[6] = (byte)((id[6] & 0x0f) | 0x40);
        id[8] = (byte)((id[8] & 0x3f) | 0x80);
        return new Guid(id);
    }
}
