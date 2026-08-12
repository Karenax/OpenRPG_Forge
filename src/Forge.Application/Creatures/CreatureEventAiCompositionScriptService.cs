using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Application.Creatures;

public sealed record CreatureEventAiCompositionHandle(string BlueprintId);

public sealed record CreatureEventAiBlackboardScriptView(
    string Key,
    string Kind,
    string Value,
    bool Persistent);

public sealed record CreatureEventAiCompositionScriptView(
    string BlueprintId,
    string ProfileId,
    string ControllerKind,
    string ControllerDefinitionId,
    IReadOnlyList<CreatureEventAiBlackboardScriptView> Blackboard,
    IReadOnlyList<string> AllowedActions,
    IReadOnlyList<string> RecoveryOrder,
    string OnPerceptionDetectedHandler,
    string OnPerceptionLostHandler,
    string BeforePhysicalAttackedHandler,
    string OnPhysicalAttackedHandler,
    string BeforeAbilityTargetedHandler,
    string OnSpellCastAtHandler,
    string OnBlockedHandler);

public static class CreatureEventAiCompositionScriptService
{
    public static CreatureEventAiCompositionHandle CreateHandle(BlueprintId blueprintId)
    {
        ArgumentNullException.ThrowIfNull(blueprintId);
        return new(blueprintId.ToString());
    }

    public static CreatureEventAiCompositionScriptView? Read(
        ProjectDefinition project,
        CreatureEventAiCompositionHandle handle)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(handle);
        BlueprintId blueprintId;
        try { blueprintId = BlueprintId.Parse(handle.BlueprintId); }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new KeyNotFoundException("CREATURE_EVENT_AI_HANDLE_INVALID", exception);
        }
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        var foundation = blueprint?.CreatureFoundation;
        if (foundation is null) return null;
        var resolved = CreatureEventAiCompositionRuntimeResolver.Resolve(
            foundation.EventAiComposition, foundation.EventAiCompositionRequiredAtRuntime);
        if (!resolved.Succeeded || resolved.Profile is null) return null;
        var profile = resolved.Profile;
        return new(blueprint!.Id.ToString(), profile.Id.ToString("D"), profile.Controller.Kind.ToString(),
            profile.Controller.DefinitionId, profile.Controller.Blackboard.Select(item =>
                new CreatureEventAiBlackboardScriptView(item.Key, item.Kind.ToString(), item.Value,
                    item.Persistent)).ToArray(), profile.Controller.AllowedActions,
            profile.Blocked.RecoveryOrder.Select(item => item.ToString()).ToArray(),
            profile.Perception.Detected.Handler, profile.Perception.Lost.Handler,
            profile.PhysicalAttack.Declared.Handler, profile.PhysicalAttack.Resolved.Handler,
            profile.Ability.Targeted.Handler, profile.Ability.Impacted.Handler, profile.Blocked.Blocked.Handler);
    }
}
