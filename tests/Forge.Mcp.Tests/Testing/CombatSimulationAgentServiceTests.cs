using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Projects;
using Forge.Mcp.Assistant;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Tests.Testing;

public sealed class CombatSimulationAgentServiceTests
{
    [Fact]
    public void RunsBoundedReadOnlySimulationWithPlanTransactionAndPermission()
    {
        using var workspace = new TestWorkspace();
        var source = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var first = CombatBlueprint(source.Blueprints[0], 120, 12);
        var second = CombatBlueprint(source.Blueprints[1], 100, 15);
        var project = new ProjectDefinition(source.Id, source.Title, source.Module,
            [first, second, .. source.Blueprints.Skip(2)], source.Areas, source.Dialogues, source.Quests);
        var profile = CombatSimulationProfile.CreateDefault() with
        {
            Enabled = true,
            RunsPerMatchup = 8,
            ArchetypeBlueprintIds = [first.Id, second.Id],
        };
        var request = new CombatSimulationAgentRequest(Guid.NewGuid(), Guid.NewGuid(),
            AssistantPermissionLevel.Propose, true, profile);

        var result = CombatSimulationAgentService.Run(project, request);

        Assert.True(result.Succeeded);
        Assert.False(result.Applied);
        Assert.Equal("MCP_COMBAT_SIMULATION_PREVIEW_READY", result.Code);
        Assert.Single(result.Report!.Matchups);
        Assert.Contains("0 project mutations", result.SemanticDiff);
    }

    [Fact]
    public void RejectsMissingPlanAndPermission()
    {
        using var workspace = new TestWorkspace();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var profile = CombatSimulationProfile.CreateDefault() with { Enabled = true };
        var missing = CombatSimulationAgentService.Run(project,
            new(Guid.Empty, Guid.NewGuid(), AssistantPermissionLevel.Propose, true, profile));
        var denied = CombatSimulationAgentService.Run(project,
            new(Guid.NewGuid(), Guid.NewGuid(), AssistantPermissionLevel.ReadOnly, true, profile));

        Assert.Equal("MCP_COMBAT_SIMULATION_PLAN_REQUIRED", missing.Code);
        Assert.Equal("MCP_COMBAT_SIMULATION_PERMISSION_DENIED", denied.Code);
    }

    private static BlueprintDefinition CombatBlueprint(BlueprintDefinition source, int health, int offense)
    {
        var ability = new AbilityDefinition(AbilityId.From(Guid.NewGuid()), "strike", "Strike", 1, 0,
            AbilityTargeting.SingleEnemy, [new(AbilityEffectKind.Damage, 8)], new("combat.strike"));
        return new(source.Id, source.Slug, source.DisplayName, source.Kind,
            new CombatProfile(new(health, 20, offense, 2, source.Slug, source.Slug), [ability]),
            source.PaletteOrigin, source.IsFavorite, source.Provenance, source.CreaturePresentation,
            source.CreatureProgression, source.CreatureLoot, source.CreatureRoleBehavior,
            source.CreatureEvents, source.ItemAdvanced, source.Authoring);
    }
}
