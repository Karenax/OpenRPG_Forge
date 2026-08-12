using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Lifecycle;
using Forge.Domain.Projects;
using Forge.RpgKit.Domain.Creatures;
using DomainControllerKind = Forge.Domain.Creatures.CreatureEventAiControllerKind;
using RpgControllerKind = Forge.RpgKit.Domain.Creatures.AiControllerKind;

namespace Forge.RpgKit.Application.Creatures;

public sealed record CreatureEventAiCompositionFoundationResult(
    AiControllerProfile? Controller,
    CreaturePerceptionEventProfile? Perception,
    CreaturePhysicalAttackEventProfile? PhysicalAttack,
    CreatureAbilityEventProfile? Ability,
    CreatureBlockedEventProfile? Blocked,
    bool UsedFallback,
    IReadOnlyList<CreatureFoundationDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}

public static class CreatureEventAiCompositionFoundationService
{
    public static CreatureEventAiCompositionFoundationResult Resolve(
        ProjectDefinition project,
        BlueprintId blueprintId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(blueprintId);
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        if (blueprint?.CreatureFoundation is null) return new(null, null, null, null, null, true, []);
        var foundation = blueprint.CreatureFoundation;
        var resolved = CreatureEventAiCompositionRuntimeResolver.Resolve(
            foundation.EventAiComposition, foundation.EventAiCompositionRequiredAtRuntime);
        if (!resolved.Succeeded || resolved.Profile is null)
            return new(null, null, null, null, null, resolved.UsedFallback, resolved.Diagnostics);
        var profile = resolved.Profile;
        var controller = new AiControllerProfile(profile.Controller.Id, profile.SchemaVersion, profile.Provenance,
            MapController(profile.Controller.Kind), profile.Controller.DefinitionId,
            profile.Controller.Blackboard.ToDictionary(item => item.Key,
                item => new AiBlackboardValue(item.Kind.ToString(), item.Value, item.Persistent),
                StringComparer.Ordinal), profile.Controller.AllowedActions);
        try
        {
            CreatureAiService.Validate(controller);
        }
        catch (InvalidOperationException exception)
        {
            return new(null, null, null, null, null, false,
                [new CreatureFoundationDiagnostic(exception.Message, "creatureFoundation/eventAiComposition/controller",
                    "The RPG Kit rejected the canonical creature AI controller.")]);
        }
        return new(controller, profile.Perception, profile.PhysicalAttack, profile.Ability, profile.Blocked, false, []);
    }

    private static RpgControllerKind MapController(DomainControllerKind kind) => kind switch
    {
        DomainControllerKind.StateMachine => RpgControllerKind.StateMachine,
        DomainControllerKind.BehaviorTree => RpgControllerKind.BehaviorTree,
        DomainControllerKind.Utility => RpgControllerKind.Utility,
        DomainControllerKind.Script => RpgControllerKind.Script,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static EventBinding[] EnumerateBindings(CreatureEventAiCompositionFoundationResult result) =>
        result.Succeeded && result.Perception is not null && result.PhysicalAttack is not null &&
        result.Ability is not null && result.Blocked is not null
            ? [result.Perception.Detected, result.Perception.Lost, result.PhysicalAttack.Declared,
                result.PhysicalAttack.Resolved, result.Ability.Targeted, result.Ability.Impacted,
                result.Blocked.Blocked]
            : [];
}
