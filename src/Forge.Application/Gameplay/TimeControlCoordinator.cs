using Forge.Domain.Gameplay;

namespace Forge.Application.Gameplay;

public enum RuntimeTimeState
{
    Running,
    SlowMotion,
    Paused,
    Planning,
    TurnBased,
}

public sealed record TimeControlSession(RuntimeTimeState State, float Scale, IReadOnlySet<string> Votes,
    IReadOnlyList<PlannedGameplayAction> PlannedActions, long Revision)
{
    public static TimeControlSession Running => new(RuntimeTimeState.Running, 1,
        new HashSet<string>(StringComparer.Ordinal), [], 0);
}

public sealed record PlannedGameplayAction(Guid Id, string ActorId, string Command, string TargetId, int Order);

public sealed record TimeControlDecision(bool Accepted, string Code, TimeControlSession Session);

public static class TimeControlCoordinator
{
    public static TimeControlDecision Request(TimeControlPolicy policy, TimeControlSession session,
        RuntimeTimeState requested, string actorId, bool isMultiplayer, bool isHost,
        int connectedPlayers = 1)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        if (connectedPlayers < 1) throw new ArgumentOutOfRangeException(nameof(connectedPlayers));
        if (!IsEnabled(policy, requested)) return new(false, "time.mode_disabled", session);

        if (isMultiplayer)
        {
            if (policy.MultiplayerPolicy == MultiplayerPausePolicy.Disabled)
                return new(false, "time.multiplayer_disabled", session);
            if (policy.MultiplayerPolicy == MultiplayerPausePolicy.HostOnly && !isHost)
                return new(false, "time.host_required", session);
            if (policy.MultiplayerPolicy == MultiplayerPausePolicy.Vote)
            {
                var votes = session.Votes.Append(actorId).ToHashSet(StringComparer.Ordinal);
                var needed = (int)Math.Ceiling(connectedPlayers * policy.RequiredVoteFraction);
                if (votes.Count < needed)
                    return new(false, "time.vote_pending", session with { Votes = votes });
                session = session with { Votes = votes };
            }
        }

        var scale = requested switch
        {
            RuntimeTimeState.Running => 1,
            RuntimeTimeState.SlowMotion => policy.SlowMotionScale,
            _ => 0,
        };
        return new(true, "time.changed", session with
        {
            State = requested,
            Scale = scale,
            Votes = requested == RuntimeTimeState.Running
                ? new HashSet<string>(StringComparer.Ordinal)
                : session.Votes,
            Revision = checked(session.Revision + 1),
        });
    }

    public static TimeControlDecision Queue(TimeControlPolicy policy, TimeControlSession session,
        PlannedGameplayAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (session.State != RuntimeTimeState.Planning) return new(false, "planning.not_active", session);
        if (action.Id == Guid.Empty || string.IsNullOrWhiteSpace(action.ActorId) ||
            string.IsNullOrWhiteSpace(action.Command) || action.Order < 0)
            return new(false, "planning.action_invalid", session);
        if (session.PlannedActions.Count >= policy.MaximumPlannedActions)
            return new(false, "planning.queue_full", session);
        var queue = session.PlannedActions.Append(action).OrderBy(item => item.Order).ThenBy(item => item.Id).ToArray();
        return new(true, "planning.queued", session with
        {
            PlannedActions = queue,
            Revision = checked(session.Revision + 1),
        });
    }

    private static bool IsEnabled(TimeControlPolicy policy, RuntimeTimeState requested) => requested switch
    {
        RuntimeTimeState.Running => true,
        RuntimeTimeState.SlowMotion => policy.SlowMotionEnabled,
        RuntimeTimeState.Paused => policy.PauseEnabled,
        RuntimeTimeState.Planning => policy.PlanningEnabled,
        RuntimeTimeState.TurnBased => policy.TurnModeEnabled,
        _ => false,
    };
}
