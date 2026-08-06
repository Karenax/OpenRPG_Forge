using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed class ExternalWorkerSecurityPolicyCommandHistory
{
    private readonly Stack<(ExternalWorkerSecurityPolicy Before, ExternalWorkerSecurityPolicy After)> _undo = [];
    private readonly Stack<(ExternalWorkerSecurityPolicy Before, ExternalWorkerSecurityPolicy After)> _redo = [];

    public ExternalWorkerSecurityPolicyState State { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public ExternalWorkerSecurityPolicyCommandHistory(ExternalWorkerSecurityPolicy? policy = null)
    {
        State = new ExternalWorkerSecurityPolicyState(policy);
    }

    public void Record(ExternalWorkerSecurityPolicyCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Succeeded || !result.Audit.Applied || result.NewRevision is null ||
            result.Patch is null || result.Inverse is null || result.BaseRevision != State.Revision)
            throw new InvalidOperationException("Only an applied worker security command at the current revision can be recorded.");
        _undo.Push((State.Policy, result.State.Policy));
        _redo.Clear();
        State = result.State;
    }

    public bool Undo()
    {
        if (!_undo.TryPop(out var entry)) return false;
        _redo.Push(entry);
        State = new ExternalWorkerSecurityPolicyState(entry.Before, State.Revision.Next());
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var entry)) return false;
        _undo.Push(entry);
        State = new ExternalWorkerSecurityPolicyState(entry.After, State.Revision.Next());
        return true;
    }
}
