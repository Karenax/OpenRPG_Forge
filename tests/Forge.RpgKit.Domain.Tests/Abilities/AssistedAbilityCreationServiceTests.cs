using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Projects;
using Forge.RpgKit.Application.Abilities;
using Forge.RpgKit.Domain.Abilities;

namespace Forge.RpgKit.Domain.Tests.Abilities;

public sealed class AssistedAbilityCreationServiceTests
{
    [Fact]
    public void Generates_targeting_cost_effect_cues_ai_tags_tests_and_balance_deterministically()
    {
        var project = Project();
        var first = AssistedAbilityCreationService.Generate(Brief(project), project, Scripts());
        var second = AssistedAbilityCreationService.Generate(Brief(project), project, Scripts());

        Assert.True(first.CanApply);
        Assert.Empty(first.Diagnostics);
        Assert.Equal(5, first.Tests.Count);
        Assert.All(first.Tests, item => Assert.True(item.Passed));
        Assert.True(first.Balance.Passed);
        Assert.Equal("vfx.ember-lance", first.Orchestration.Ability.VisualCue.CueKey);
        Assert.Equal("audio.ember-lance", first.Orchestration.AudioCueKey);
        Assert.Contains("finisher", first.Orchestration.AiTags);
        Assert.Equal(first.RuntimeDocument.InitialValue, second.RuntimeDocument.InitialValue);
    }

    [Fact]
    public void Validation_localizes_missing_script_item_cues_tags_and_excessive_cost()
    {
        var project = Project();
        var proposal = AssistedAbilityCreationService.Generate(Brief(project), project, Scripts());
        var brokenAbility = new AbilityDefinition(proposal.Orchestration.Ability.Id, "broken", "Broken", 101, 2,
            AbilityTargeting.SingleEnemy, [new(AbilityEffectKind.Damage, 5)], new("vfx.ok"));
        var broken = proposal.Orchestration with
        {
            Ability = brokenAbility,
            ScriptPath = "scripts/missing.forge",
            RequiredItemSlug = "missing-item",
            AudioCueKey = "",
            AiTags = new HashSet<string>(),
        };
        var diagnostics = AbilityOrchestrationService.Validate(broken,
            project.Blueprints.Single(item => item.Id == broken.OwnerBlueprintId).Combat!, Scripts(),
            project.Blueprints.Where(item => item.Kind == BlueprintKind.Item).Select(item => item.Slug)
                .ToHashSet(StringComparer.Ordinal));

        Assert.Contains(diagnostics, item => item.Code == "RPG_ABILITY_COST_EXCEEDS_POOL");
        Assert.Contains(diagnostics, item => item.Code == "RPG_ABILITY_SCRIPT_MISSING" && item.Path.EndsWith("scriptPath"));
        Assert.Contains(diagnostics, item => item.Code == "RPG_ABILITY_ITEM_MISSING");
        Assert.Contains(diagnostics, item => item.Code == "RPG_ABILITY_CUE_MISSING");
        Assert.Contains(diagnostics, item => item.Code == "RPG_ABILITY_AI_TAGS_REQUIRED");
    }

    [Fact]
    public void Apply_requires_approval_permission_and_exact_dry_run_then_updates_combat_and_runtime_document()
    {
        var project = Project();
        var state = new ProjectState(project, new ProjectRevision(4));
        var handler = new ApplyAssistedAbilityHandler(new FixedClock());
        var proposal = AssistedAbilityCreationService.Generate(Brief(project), project, Scripts());
        var transaction = Guid.NewGuid();
        var plan = Guid.NewGuid();
        ApplyAssistedAbilityCommand Command(AssistedAbilityProposal value, AssistantPermissionLevel permission,
            bool dryRun) => new(CommandId.From(Guid.NewGuid()), transaction, plan, state.Revision, value, permission,
            dryRun);

        Assert.Equal(ApplyAssistedAbilityHandler.ApprovalRequiredCode,
            handler.Handle(state, Command(proposal, AssistantPermissionLevel.ApplyApprovedPlan, true)).Diagnostics.Single().Code);
        var approved = proposal.WithApproval(true, "author-approved:test");
        Assert.Equal(ApplyAssistedAbilityHandler.PermissionDeniedCode,
            handler.Handle(state, Command(approved, AssistantPermissionLevel.Propose, true)).Diagnostics.Single().Code);
        Assert.True(handler.Handle(state, Command(approved, AssistantPermissionLevel.ApplyApprovedPlan, true)).Succeeded);
        var applied = handler.Handle(state, Command(approved, AssistantPermissionLevel.ApplyApprovedPlan, false));
        Assert.True(applied.Succeeded);
        var owner = applied.Command.State.Project.Blueprints.Single(item => item.Id == approved.Orchestration.OwnerBlueprintId);
        Assert.Contains(owner.Combat!.Abilities, item => item.Id.Value == approved.Orchestration.Id);
        Assert.Contains(applied.Command.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments,
            item => item.Id == approved.RuntimeDocument.Id);
    }

    [Fact]
    public void Changed_proposal_and_stale_revision_are_rejected()
    {
        var project = Project();
        var state = new ProjectState(project, new ProjectRevision(2));
        var handler = new ApplyAssistedAbilityHandler(new FixedClock());
        var approved = AssistedAbilityCreationService.Generate(Brief(project), project, Scripts())
            .WithApproval(true, "author-approved:test");
        var command = new ApplyAssistedAbilityCommand(CommandId.From(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(),
            state.Revision, approved, Forge.Domain.Assistant.AssistantPermissionLevel.ApplyApprovedPlan, true);
        Assert.True(handler.Handle(state, command).Succeeded);
        var tampered = approved with { SemanticDiff = approved.SemanticDiff + "|tampered" };
        Assert.Equal(ApplyAssistedAbilityHandler.PlanNotPreviewedCode,
            handler.Handle(state, command with { CommandId = CommandId.From(Guid.NewGuid()), Proposal = tampered, DryRun = false }).Diagnostics.Single().Code);
        Assert.Equal(ApplyAssistedAbilityHandler.RevisionConflictCode,
            handler.Handle(new ProjectState(project, state.Revision.Next()), command).Diagnostics.Single().Code);
    }

    [Fact]
    public void Project_history_undo_and_redo_restore_combat_and_runtime_metadata_together()
    {
        var project = Project();
        var history = new ProjectCommandHistory(new ProjectState(project, new ProjectRevision(0)));
        var handler = new ApplyAssistedAbilityHandler(new FixedClock());
        var approved = AssistedAbilityCreationService.Generate(Brief(project), project, Scripts())
            .WithApproval(true, "author-approved:test");
        var transaction = Guid.NewGuid();
        var plan = Guid.NewGuid();
        ApplyAssistedAbilityCommand Command(bool dryRun) => new(CommandId.From(Guid.NewGuid()), transaction, plan,
            history.State.Revision, approved, Forge.Domain.Assistant.AssistantPermissionLevel.ApplyApprovedPlan, dryRun);
        Assert.True(handler.Handle(history.State, Command(true)).Succeeded);
        var applied = handler.Handle(history.State, Command(false));
        history.Record(applied.Command);
        Assert.Single(history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments);
        Assert.Single(history.State.Project.Blueprints.Single(item => item.Id == approved.Orchestration.OwnerBlueprintId).Combat!.Abilities);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments);
        Assert.Empty(history.State.Project.Blueprints.Single(item => item.Id == approved.Orchestration.OwnerBlueprintId).Combat!.Abilities);
        Assert.True(history.Redo().Succeeded);
        Assert.Single(history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments);
    }

    private static ProjectDefinition Project()
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("61000000-0000-4000-8000-000000000001")),
            "ability-arena", "Ability Arena", []);
        var owner = new BlueprintDefinition(BlueprintId.From(Guid.Parse("62000000-0000-4000-8000-000000000001")),
            "ember-knight", "Ember Knight", BlueprintKind.Creature,
            new CombatProfile(new CombatStats(150, 100, 14, 6, "heroes", "heroes")));
        var item = new BlueprintDefinition(BlueprintId.From(Guid.Parse("63000000-0000-4000-8000-000000000001")),
            "ember-focus", "Ember Focus", BlueprintKind.Item);
        return new ProjectDefinition(ProjectId.From(Guid.Parse("64000000-0000-4000-8000-000000000001")),
            "Ability test", [owner, item], [area]);
    }

    private static AssistedAbilityBrief Brief(ProjectDefinition project) => new(
        Guid.Parse("65000000-0000-4000-8000-000000000001"),
        project.Blueprints.Single(item => item.Kind == BlueprintKind.Creature).Id.Value,
        "ember-lance", "Ember Lance", 20, 6, AbilityTargeting.SingleEnemy,
        [new AbilityEffect(AbilityEffectKind.Damage, 18)], "vfx.ember-lance", "audio.ember-lance",
        "scripts/ember_lance.forge", "ember-focus", new HashSet<string> { "ranged", "finisher" },
        "Burst damage with visible telegraph and meaningful resource cost.", ["lore:ember-order"],
        ["style:readable", "combat:telegraphed"], "author:test", 20260806);

    private static HashSet<string> Scripts() => new(StringComparer.Ordinal) { "scripts/ember_lance.forge" };

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-08-06T00:00:00Z");
    }
}
