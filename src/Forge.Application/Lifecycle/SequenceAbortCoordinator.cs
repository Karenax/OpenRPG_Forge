using Forge.Domain.RuntimePolicies;

namespace Forge.Application.Lifecycle;

public static class SequenceAbortCoordinator
{
    public const string DisabledCode = "runtime.sequence.abort_disabled";
    public const string InactiveCode = "runtime.sequence.inactive";
    public const string RollbackBudgetCode = "runtime.sequence.rollback_budget_exceeded";

    public static SequenceAbortResult Abort(SequenceAbortPolicy policy, SequenceRuntimeState current,
        SequenceRuntimeState snapshot)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!policy.Enabled)
        {
            return Failure(DisabledCode, current, policy.Binding.Handler);
        }

        if (!current.SequenceActive)
        {
            return Failure(InactiveCode, current, policy.Binding.Handler);
        }

        var rollback = current.NarrativeRevision - snapshot.NarrativeRevision;
        if (policy.Restore.HasFlag(SequenceAbortRestoreFlags.NarrativeTransaction) &&
            rollback > policy.MaximumNarrativeRollbackSteps)
        {
            return Failure(RollbackBudgetCode, current, policy.Binding.Handler);
        }

        var camera = policy.Restore.HasFlag(SequenceAbortRestoreFlags.Camera) ? snapshot.CameraId : current.CameraId;
        var controls = policy.Restore.HasFlag(SequenceAbortRestoreFlags.PlayerControl)
            ? snapshot.ControlStack
            : current.ControlStack;
        var invulnerable = policy.Restore.HasFlag(SequenceAbortRestoreFlags.TemporaryInvulnerability)
            ? snapshot.TemporarilyInvulnerableActors
            : current.TemporarilyInvulnerableActors;
        var locks = policy.Restore.HasFlag(SequenceAbortRestoreFlags.Locks) ? snapshot.LockIds : current.LockIds;
        var narrativeRevision = policy.Restore.HasFlag(SequenceAbortRestoreFlags.NarrativeTransaction)
            ? snapshot.NarrativeRevision
            : current.NarrativeRevision;
        var transaction = policy.Restore.HasFlag(SequenceAbortRestoreFlags.NarrativeTransaction)
            ? snapshot.NarrativeTransactionId
            : current.NarrativeTransactionId;
        var restored = new SequenceRuntimeState(camera, controls, invulnerable, locks, narrativeRevision,
            transaction, false);
        return new SequenceAbortResult(true, null, restored,
            Math.Max(0, current.LockIds.Count - locks.Count), Math.Max(0, controls.Count - current.ControlStack.Count),
            camera != current.CameraId, narrativeRevision != current.NarrativeRevision, policy.Binding.Handler);
    }

    private static SequenceAbortResult Failure(string code, SequenceRuntimeState state, string handler) =>
        new(false, code, state, 0, 0, false, false, handler);
}
