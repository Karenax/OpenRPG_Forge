using Forge.Domain.Spatial;

namespace Forge.RpgKit.Domain.Creatures;

public enum AiControllerKind { StateMachine, BehaviorTree, Utility, Script }
public enum MovementRecoveryStrategy { Wait, Repath, Avoid, Teleport, Abort }

public sealed record AbilityTargeted(Guid EventId, Guid CreatureId, string SourceId, string AbilityId, IReadOnlySet<string> Tags, string TargetContext);
public sealed record AbilityImpacted(Guid EventId, Guid CreatureId, string SourceId, string AbilityId, IReadOnlySet<string> Tags, string Result, double Magnitude);
public sealed record MovementBlocked(Guid EventId, Guid CreatureId, string? ObstacleId, string Cause, string NavigationStatus, MovementRecoveryStrategy Recovery, Vector3Value Position);
public sealed record AiBlackboardValue(string Type, string Value, bool Persistent);
public sealed record AiControllerProfile(Guid Id, int SchemaVersion, string Provenance, AiControllerKind Kind, string DefinitionId, IReadOnlyDictionary<string, AiBlackboardValue> Blackboard, IReadOnlyList<string> AllowedActions);
public sealed record AiDecision(string Action, double Score, IReadOnlyDictionary<string, AiBlackboardValue> Blackboard);

public static class CreatureAiService
{
    public static AiControllerProfile Validate(AiControllerProfile profile)
    {
        if (profile.Id == Guid.Empty) throw new InvalidOperationException("RPG_AI_UUID_REQUIRED");
        if (profile.SchemaVersion != 1) throw new InvalidOperationException("RPG_AI_SCHEMA_UNSUPPORTED");
        if (string.IsNullOrWhiteSpace(profile.Provenance) || string.IsNullOrWhiteSpace(profile.DefinitionId) || profile.AllowedActions.Count == 0 || profile.Blackboard.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value.Type)))
            throw new InvalidOperationException("RPG_AI_PROFILE_INVALID");
        return profile;
    }

    public static AiDecision Decide(AiControllerProfile profile, IReadOnlyDictionary<string, double> utilityScores)
    {
        Validate(profile);
        var action = profile.Kind switch
        {
            AiControllerKind.Utility => profile.AllowedActions.OrderByDescending(candidate => utilityScores.GetValueOrDefault(candidate)).ThenBy(candidate => candidate, StringComparer.Ordinal).First(),
            AiControllerKind.StateMachine => profile.Blackboard.TryGetValue("state", out var state) && profile.AllowedActions.Contains(state.Value) ? state.Value : profile.AllowedActions[0],
            AiControllerKind.BehaviorTree => profile.AllowedActions.First(),
            AiControllerKind.Script => profile.DefinitionId,
            _ => profile.AllowedActions[0]
        };
        return new(action, utilityScores.GetValueOrDefault(action), profile.Blackboard);
    }

    public static AbilityImpacted Impact(AbilityTargeted targeted, string result, double magnitude, Func<Guid> eventIdSource)
    {
        if (targeted.EventId == Guid.Empty || targeted.CreatureId == Guid.Empty || string.IsNullOrWhiteSpace(targeted.SourceId) || string.IsNullOrWhiteSpace(targeted.AbilityId) || magnitude < 0)
            throw new InvalidOperationException("RPG_ABILITY_TARGET_INVALID");
        return new(eventIdSource(), targeted.CreatureId, targeted.SourceId, targeted.AbilityId, targeted.Tags, result, magnitude);
    }

    public static MovementBlocked Blocked(Guid creatureId, string? obstacleId, string cause, string navStatus, Vector3Value position, Func<Guid> eventIdSource)
    {
        var recovery = navStatus switch { "partial" => MovementRecoveryStrategy.Repath, "dynamic-obstacle" => MovementRecoveryStrategy.Avoid, "off-mesh" => MovementRecoveryStrategy.Teleport, "unreachable" => MovementRecoveryStrategy.Abort, _ => MovementRecoveryStrategy.Wait };
        return new(eventIdSource(), creatureId, obstacleId, cause, navStatus, recovery, position);
    }
}
