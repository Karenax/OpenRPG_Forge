using Forge.Domain.Creatures;
using Forge.Domain.Lifecycle;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Creatures;

public sealed class CreatureEventAiCompositionProfileTests
{
    [Fact]
    public void CompleteProfileValidatesAndNormalizesTypedControllerData()
    {
        var profile = Complete();

        Assert.Empty(CreatureEventAiCompositionValidator.Validate(profile));
        Assert.Equal(["attack", "patrol"], profile.Controller.AllowedActions);
        Assert.Equal(["alert", "state"], profile.Controller.Blackboard.Select(item => item.Key));
        Assert.Equal(15, CreatureEventAiCompositionValidator.EnumerateComponentIds(profile).Distinct().Count());
    }

    [Fact]
    public void UtilityAndStateMachineDecisionsAreDeterministicAndInspectable()
    {
        var utility = Complete();
        var utilityDecision = CreatureEventAiCompositionValidator.Decide(utility,
            new Dictionary<string, double> { ["attack"] = .9, ["patrol"] = .2 });
        var stateMachine = new CreatureEventAiCompositionProfile(utility.Id, utility.SchemaVersion,
            utility.Provenance, utility.AssetId, utility.PreviewTransform, utility.Perception,
            utility.PhysicalAttack, utility.Ability, utility.Blocked,
            utility.Controller with { Kind = CreatureEventAiControllerKind.StateMachine }, utility.Overrides);
        var stateDecision = CreatureEventAiCompositionValidator.Decide(stateMachine);

        Assert.Equal("attack", utilityDecision.Action);
        Assert.Equal(.9, utilityDecision.Score);
        Assert.Equal("patrol", stateDecision.Action);
        Assert.True(stateDecision.Blackboard["alert"].Persistent);
    }

    [Fact]
    public void InvalidBindingBlackboardAndDuplicateUuidAreDiagnosedBeforeBuild()
    {
        var source = Complete();
        var invalid = new CreatureEventAiCompositionProfile(source.Id, source.SchemaVersion,
            source.Provenance, source.AssetId, source.PreviewTransform,
            source.Perception with { Lost = source.Perception.Detected }, source.PhysicalAttack,
            source.Ability, source.Blocked,
            source.Controller with
            {
                Blackboard = [new(source.Controller.Id, "speed", CreatureEventAiBlackboardValueKind.Number,
                    "NaN", true)],
            }, source.Overrides);
        var diagnostics = CreatureEventAiCompositionValidator.Validate(invalid);

        Assert.Contains(diagnostics, item => item.Code == "creature.event_ai.component_uuid_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.event_ai.binding_invalid");
        Assert.Contains(diagnostics, item => item.Code == "creature.event_ai.controller_invalid");
    }

    [Fact]
    public void OptionalRuntimeIsIgnoredAndRequiredRuntimeProducesStableDiagnostic()
    {
        var optional = CreatureEventAiCompositionRuntimeResolver.Resolve(null, false);
        var required = CreatureEventAiCompositionRuntimeResolver.Resolve(null, true);

        Assert.True(optional.Succeeded);
        Assert.True(optional.UsedFallback);
        Assert.Equal(CreatureEventAiCompositionValidator.MissingRequiredCode,
            Assert.Single(required.Diagnostics).Code);
    }

    internal static CreatureEventAiCompositionProfile Complete() => new(
        Guid.Parse("a1800000-0000-4000-8000-000000000001"), 1, "test:r080",
        "asset.creature-event-ai.warden", TransformValue.Identity,
        new(Guid.Parse("a1800000-0000-4000-8000-000000000002"),
            Binding("a1800000-0000-4000-8000-000000000003", "creature.stimulus_detected",
                "OnPerceptionDetected"),
            Binding("a1800000-0000-4000-8000-000000000004", "creature.stimulus_lost",
                "OnPerceptionLost")),
        new(Guid.Parse("a1800000-0000-4000-8000-000000000005"),
            Binding("a1800000-0000-4000-8000-000000000006", "creature.attack_declared",
                "BeforePhysicalAttacked"),
            Binding("a1800000-0000-4000-8000-000000000007", "creature.attack_resolved",
                "OnPhysicalAttacked")),
        new(Guid.Parse("a1800000-0000-4000-8000-000000000008"),
            Binding("a1800000-0000-4000-8000-000000000009", "creature.ability_targeted",
                "BeforeAbilityTargeted"),
            Binding("a1800000-0000-4000-8000-000000000010", "creature.ability_impacted",
                "OnSpellCastAt")),
        new(Guid.Parse("a1800000-0000-4000-8000-000000000011"),
            Binding("a1800000-0000-4000-8000-000000000012", "creature.movement_blocked", "OnBlocked"),
            [CreatureEventAiRecoveryStrategy.Repath, CreatureEventAiRecoveryStrategy.Avoid,
                CreatureEventAiRecoveryStrategy.Teleport, CreatureEventAiRecoveryStrategy.Abort]),
        new(Guid.Parse("a1800000-0000-4000-8000-000000000013"), CreatureEventAiControllerKind.Utility,
            "ai.ash-warden", [
                new(Guid.Parse("a1800000-0000-4000-8000-000000000014"), "state",
                    CreatureEventAiBlackboardValueKind.Text, "patrol", true),
                new(Guid.Parse("a1800000-0000-4000-8000-000000000015"), "alert",
                    CreatureEventAiBlackboardValueKind.Boolean, "true", true)],
            ["patrol", "attack"]), new Dictionary<string, string> { ["ai.preview"] = "enabled" });

    private static EventBinding Binding(string id, string eventName, string handler) => new(
        Guid.Parse(id), 1, "test:r080", "1.0", eventName, handler, EventAuthorityScope.Server);
}
