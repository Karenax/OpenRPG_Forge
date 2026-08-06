using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.RpgKit.Application.Bosses;
using Forge.RpgKit.Domain.Bosses;

namespace Forge.RpgKit.Domain.Tests.Bosses;

public sealed class AssistedBossCreationServiceTests
{
    [Fact]
    public void Generates_canonical_creature_encounter_abilities_phases_dialogue_rewards_and_balance()
    {
        var project = Project();
        var first = AssistedBossCreationService.Generate(Brief(project), project, Scripts());
        var second = AssistedBossCreationService.Generate(Brief(project), project, Scripts());

        Assert.True(first.CanApply);
        Assert.Empty(first.Diagnostics);
        Assert.Equal(3, first.Boss.Blueprint.Combat!.Abilities.Count);
        Assert.Equal(3, first.Boss.Phases.Count);
        Assert.Equal(2, first.Boss.IntroductionDialogue.Nodes.Count);
        Assert.Equal(2, first.Boss.Rewards.Count);
        Assert.Equal(32, first.Balance.Runs);
        Assert.True(first.Balance.Passed);
        Assert.Equal(first.RuntimeDocument.InitialValue, second.RuntimeDocument.InitialValue);
        Assert.Contains("balance-runs:32", first.SemanticDiff);
    }

    [Fact]
    public void Domain_validation_localizes_missing_phase_script_ability_and_summon()
    {
        var project = Project();
        var proposal = AssistedBossCreationService.Generate(Brief(project), project, Scripts());
        var brokenPhase = proposal.Boss.Phases[1] with
        {
            AbilityIds = [Guid.NewGuid().ToString("D")],
            TransitionScript = "scripts/missing.forge",
            SummonBlueprintSlugs = ["missing-summon"],
        };
        var broken = proposal.Boss with { Phases = [proposal.Boss.Phases[0], brokenPhase, proposal.Boss.Phases[2]] };
        var diagnostics = BossOrchestrationService.Validate(broken, Scripts(),
            project.Blueprints.Select(item => item.Slug).Append(broken.Slug).ToHashSet(StringComparer.Ordinal));

        Assert.Contains(diagnostics, item => item.Code == "RPG_BOSS_PHASE_ABILITY_MISSING" && item.Path.Contains("phase:2"));
        Assert.Contains(diagnostics, item => item.Code == "RPG_BOSS_PHASE_SCRIPT_MISSING" && item.Path.Contains("transitionScript"));
        Assert.Contains(diagnostics, item => item.Code == "RPG_BOSS_SUMMON_MISSING" && item.Path.Contains("missing-summon"));
    }

    [Fact]
    public void Apply_requires_approval_permission_and_matching_dry_run_then_commits_atomic_batch()
    {
        var project = Project();
        var state = new ProjectState(project, new ProjectRevision(7));
        var handler = new ApplyAssistedBossHandler(new FixedClock());
        var proposal = AssistedBossCreationService.Generate(Brief(project), project, Scripts());
        var transaction = Guid.NewGuid();
        var plan = Guid.NewGuid();
        ApplyAssistedBossCommand Command(AssistedBossProposal value, AssistantPermissionLevel permission, bool dryRun) =>
            new(CommandId.From(Guid.NewGuid()), transaction, plan, state.Revision, value, permission, dryRun);

        Assert.Equal(ApplyAssistedBossHandler.ApprovalRequiredCode,
            handler.Handle(state, Command(proposal, AssistantPermissionLevel.ApplyApprovedPlan, true)).Diagnostics.Single().Code);
        var approved = proposal.WithApproval(true, "author-approved:test");
        Assert.Equal(ApplyAssistedBossHandler.PermissionDeniedCode,
            handler.Handle(state, Command(approved, AssistantPermissionLevel.Propose, true)).Diagnostics.Single().Code);
        var preview = handler.Handle(state, Command(approved, AssistantPermissionLevel.ApplyApprovedPlan, true));
        Assert.True(preview.Succeeded);
        Assert.False(preview.Command.Audit.Applied);
        var applied = handler.Handle(state, Command(approved, AssistantPermissionLevel.ApplyApprovedPlan, false));
        Assert.True(applied.Succeeded);
        Assert.Equal(state.Revision.Next(), applied.Command.State.Revision);
        Assert.Contains(applied.Command.State.Project.Blueprints, item => item.Id == approved.Boss.Blueprint.Id);
        Assert.Contains(applied.Command.State.Project.Dialogues, item => item.Id == approved.Boss.IntroductionDialogue.Id);
        Assert.Contains(applied.Command.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments,
            item => item.Id == approved.RuntimeDocument.Id && item.InitialValue.Contains("\"phases\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Changed_proposal_or_revision_cannot_reuse_preview_plan()
    {
        var project = Project();
        var state = new ProjectState(project, new ProjectRevision(3));
        var handler = new ApplyAssistedBossHandler(new FixedClock());
        var approved = AssistedBossCreationService.Generate(Brief(project), project, Scripts())
            .WithApproval(true, "author-approved:test");
        var transaction = Guid.NewGuid();
        var plan = Guid.NewGuid();
        var preview = new ApplyAssistedBossCommand(CommandId.From(Guid.NewGuid()), transaction, plan, state.Revision,
            approved, AssistantPermissionLevel.ApplyApprovedPlan, true);
        Assert.True(handler.Handle(state, preview).Succeeded);
        var changed = approved with { SemanticDiff = approved.SemanticDiff + "|tampered" };
        var rejected = handler.Handle(state, preview with { CommandId = CommandId.From(Guid.NewGuid()), Proposal = changed, DryRun = false });
        Assert.Equal(ApplyAssistedBossHandler.PlanNotPreviewedCode, rejected.Diagnostics.Single().Code);
        var stale = handler.Handle(new ProjectState(project, state.Revision.Next()), preview);
        Assert.Equal(ApplyAssistedBossHandler.RevisionConflictCode, stale.Diagnostics.Single().Code);
    }

    [Fact]
    public void Project_history_undo_and_redo_restore_the_whole_boss_package()
    {
        var project = Project();
        var history = new ProjectCommandHistory(new ProjectState(project, new ProjectRevision(0)));
        var handler = new ApplyAssistedBossHandler(new FixedClock());
        var approved = AssistedBossCreationService.Generate(Brief(project), project, Scripts())
            .WithApproval(true, "author-approved:test");
        var transaction = Guid.NewGuid();
        var plan = Guid.NewGuid();
        ApplyAssistedBossCommand Command(bool dryRun) => new(CommandId.From(Guid.NewGuid()), transaction, plan,
            history.State.Revision, approved, AssistantPermissionLevel.ApplyApprovedPlan, dryRun);
        Assert.True(handler.Handle(history.State, Command(true)).Succeeded);
        var applied = handler.Handle(history.State, Command(false));
        history.Record(applied.Command);
        Assert.Single(history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments);
        Assert.True(history.Redo().Succeeded);
        Assert.Single(history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments);
    }

    private static ProjectDefinition Project()
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("10000000-0000-4000-8000-000000000001")),
            "cinder-arena", "Arène des Cendres", []);
        var summonId = BlueprintId.From(Guid.Parse("20000000-0000-4000-8000-000000000001"));
        var summon = new BlueprintDefinition(summonId, "cinder-wisp", "Feu follet", BlueprintKind.Creature);
        return new ProjectDefinition(ProjectId.From(Guid.Parse("30000000-0000-4000-8000-000000000001")),
            "Boss test", [summon], [area]);
    }

    private static AssistedBossBrief Brief(ProjectDefinition project) => new(
        Guid.Parse("40000000-0000-4000-8000-000000000001"),
        Guid.Parse("50000000-0000-4000-8000-000000000001"),
        "reine-des-cendres", "Reine des Cendres", project.Areas.Single().Id.Value,
        "La reine veille sur le cœur du volcan.", 480, 24, 12, 18,
        ["Trait de braise", "Cercle de feu", "Couronne ardente"],
        ["scripts/boss_phase_one.forge", "scripts/boss_phase_two.forge", "scripts/boss_phase_three.forge"],
        ["cinder-wisp"], ["crown-ember", "royal-ash"], "scripts/boss_death.forge",
        ["world-bible.md#cinder-queen"], ["style:forge-clair", "combat:telegraphed"], "author:test", 20260806);

    private static HashSet<string> Scripts() => new(StringComparer.Ordinal)
    {
        "scripts/boss_phase_one.forge",
        "scripts/boss_phase_two.forge",
        "scripts/boss_phase_three.forge",
        "scripts/boss_death.forge",
    };

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-08-06T00:00:00Z");
    }
}
