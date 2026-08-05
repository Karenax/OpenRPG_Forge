using Forge.Domain.Gameplay;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Validation;

namespace Forge.Domain.Tests.Gameplay;

public sealed class GameplayControlProfileTests
{
    [Fact]
    public void Defaults_are_stable_complete_and_accessible()
    {
        var moduleId = ModuleId.From(Guid.NewGuid());
        var first = ModuleGameplayControlProfile.CreateDefault(moduleId);
        var second = ModuleGameplayControlProfile.CreateDefault(moduleId);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Swimming.Id, second.Swimming.Id);
        Assert.Equal(first.RadialMenu.Actions.Select(action => action.Id),
            second.RadialMenu.Actions.Select(action => action.Id));
        Assert.Equal(first.CombatModes.Providers.Select(provider => provider.Id),
            second.CombatModes.Providers.Select(provider => provider.Id));
        Assert.True(first.Swimming.Enabled);
        Assert.True(first.Climbing.Enabled);
        Assert.True(first.Climbing.RequireAuthoredLinks);
        Assert.True(first.RadialMenu.GamepadEnabled);
        Assert.True(first.RadialMenu.KeyboardNavigation);
        Assert.True(first.RadialMenu.ListFallback);
        Assert.Contains(first.RadialMenu.Actions, action => action.ParentKey == "combat");
        Assert.True(first.TimeControl.PlanningEnabled);
        Assert.Equal(CombatModeKind.RealTimeWithPause, first.CombatModes.DefaultProvider.Kind);
        Assert.Equal(3, first.CombatModes.Providers.Count);
    }

    [Fact]
    public void Validator_rejects_gameplay_identity_and_mismatched_combat_contracts()
    {
        var project = new ProjectDefinition(ProjectId.From(Guid.NewGuid()), "Gameplay", [],
            [new Forge.Domain.Areas.AreaDefinition(Forge.Domain.Areas.AreaId.From(Guid.NewGuid()), "arena", "Arena", [])]);
        var defaults = project.Module.RuntimePolicies;
        var gameplay = defaults.Gameplay;
        var providers = gameplay.CombatModes.Providers.ToArray();
        providers[0] = new CombatModeProviderDefinition(providers[0].Id, providers[0].ProviderId,
            providers[0].DisplayName, providers[0].Kind, providers[0].CustomProvider,
            ["combat.started"], providers[0].ServerAuthoritative);
        var combat = new CombatModeProfile(gameplay.CombatModes.Id, 1, gameplay.CombatModes.DefaultProviderId,
            providers, "test");
        var invalidGameplay = new ModuleGameplayControlProfile(Guid.NewGuid(), 1, gameplay.Swimming,
            gameplay.Climbing, gameplay.RadialMenu, gameplay.TimeControl, combat, "test");
        var policies = new ModuleRuntimePolicyProfile(defaults.Id, 1, defaults.Heartbeats, defaults.Rest,
            defaults.SequenceAbort, "test", defaults.Cinematics, invalidGameplay);
        project = new ProjectDefinition(project.Id, project.Title, project.Module.WithRuntimePolicies(policies),
            project.Blueprints, project.Areas, project.Dialogues, project.Quests);

        var diagnostics = ProjectValidator.Validate(project);
        Assert.Contains(diagnostics, item => item.Code == RuntimePolicyValidator.GameplayIdentityCode);
        Assert.Contains(diagnostics, item => item.Code == RuntimePolicyValidator.CombatEventContractCode);
    }
}
