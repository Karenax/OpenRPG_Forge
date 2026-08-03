using Forge.RpgKit.Domain.Quests;

namespace Forge.RpgKit.Domain.Tests.Quests;

public sealed class QuestQualityServiceTests
{
    [Fact]
    public void Generated_tests_cover_nominal_failure_repetition_multiplayer_and_save_resume_deterministically()
    {
        var quest = Quest(); var tests = QuestQualityService.GenerateTests(quest); Assert.Equal(Enum.GetValues<QuestTestKind>(), tests.Select(item => item.Kind)); Assert.Equal(5, tests.Select(item => item.Id).Distinct().Count()); Assert.All(tests, item => Assert.NotEmpty(item.Assertions)); Assert.Equal(tests.Select(item => item.Id), QuestQualityService.GenerateTests(quest).Select(item => item.Id));
    }

    [Fact]
    public void Generated_scenario_runner_reports_assertion_paths_and_final_state()
    {
        var scenario = QuestQualityService.GenerateTests(Quest()).Single(item => item.Kind == QuestTestKind.Nominal); var run = QuestQualityService.Run(scenario, (step, state) => step.Action == "complete-all-objectives" ? new Dictionary<string, string> { ["quest/state"] = "completed", ["quest/rewards"] = "true" } : state); Assert.True(run.Passed); Assert.Empty(run.Failures);
        var failed = QuestQualityService.Run(scenario, (_, state) => state); Assert.False(failed.Passed); Assert.Contains(failed.Failures, item => item.Contains("quest/state", StringComparison.Ordinal));
    }

    [Fact]
    public void Ai_quest_requires_citations_states_participants_dependencies_style_lint_and_human_approval()
    {
        var quest = Quest(); var context = Context(quest); var brief = new QuestAiBrief("Restore the vigil", ["hidden", "active", "completed"], ["guide"], ["zone:observatory", "creature:lyra", "transition:offer"], ["MEGACONTEXT.md#veillee"], ["style:vigil"], "agent:forge");
        var preview = QuestQualityService.PreviewAi(brief, context, _ => quest); Assert.True(preview.CanApply); Assert.False(preview.HumanApproved); Assert.False(preview.Applied); Assert.Contains("ai-preview", preview.SemanticDiff); Assert.Throws<InvalidOperationException>(() => QuestQualityService.Apply(preview));
        var approved = QuestQualityService.Approve(preview, "author:karen"); var applied = QuestQualityService.Apply(approved); Assert.True(applied.HumanApproved); Assert.True(applied.Applied); Assert.Contains("ai-apply", applied.SemanticDiff); Assert.Throws<InvalidOperationException>(() => QuestQualityService.Apply(applied));
    }

    [Fact]
    public void Ai_preview_rejects_missing_dependency_style_evidence_or_invalid_generated_quest()
    {
        var quest = Quest(); var context = Context(quest); var brief = new QuestAiBrief("Restore", ["active"], ["guide"], ["missing"], ["source"], ["unproven-style"], "agent"); var preview = QuestQualityService.PreviewAi(brief, context, _ => quest); Assert.False(preview.CanApply); Assert.Contains("missing-dependency:missing", preview.DependencyWarnings); Assert.Contains("unproven-style-rule:unproven-style", preview.StyleViolations);
        Assert.Throws<ArgumentException>(() => QuestQualityService.PreviewAi(brief with { ContextCitations = [] }, context, _ => quest));
    }

    private static QuestDefinition Quest()
    {
        var objective = new QuestObjective(Guid.NewGuid(), QuestObjectiveKind.Boolean, "Light prism", 1, new HashSet<string>(), null, TimeSpan.Zero, new HashSet<string>(), null, false); return new(Guid.Parse("12345678-0000-4000-8000-000000000001"), 1, "author:test", "vigil", "Vigil", ["zone:observatory"], [new("guide", QuestParticipantBindingKind.Blueprint, "creature:lyra", new HashSet<string>(), true)], ["placeable:prism"], [], [objective], ["transition:offer"], new(0, new Dictionary<Guid, int>(), new Dictionary<string, decimal>(), new Dictionary<string, double>(), new HashSet<string>(), []), new("design", "canon", "owner", QuestEditorialStatus.Review, [], [], ["style:vigil"], ["review"]));
    }
    private static QuestValidationContext Context(QuestDefinition quest) => new(new HashSet<string>(quest.ZoneIds), new HashSet<string>(quest.Participants.Select(item => item.BindingValue)), new HashSet<string>(quest.PropIds), new HashSet<Guid>(), new HashSet<string>(quest.TransitionIds), new HashSet<Guid>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), 10);
}
