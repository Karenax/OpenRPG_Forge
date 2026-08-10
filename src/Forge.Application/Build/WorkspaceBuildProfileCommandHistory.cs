using Forge.Domain.Build;

namespace Forge.Application.Build;

public sealed class WorkspaceBuildProfileCommandHistory
{
    private readonly Stack<(WorkspaceBuildProfile Before, WorkspaceBuildProfile After)> _undo = [];
    private readonly Stack<(WorkspaceBuildProfile Before, WorkspaceBuildProfile After)> _redo = [];
    public WorkspaceBuildProfileState State { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public WorkspaceBuildProfileCommandHistory(WorkspaceBuildProfile? profile = null) => State = new(profile);

    public void Record(WorkspaceBuildProfileCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Succeeded || !result.Audit.Applied || result.NewRevision is null ||
            result.Patch is null || result.Inverse is null || result.BaseRevision != State.Revision)
            throw new InvalidOperationException("Only an applied build profile command at the current revision can be recorded.");
        _undo.Push((State.Profile, result.State.Profile));
        _redo.Clear();
        State = result.State;
    }

    public bool Undo()
    {
        if (!_undo.TryPop(out var entry)) return false;
        _redo.Push(entry);
        State = new(entry.Before, State.Revision.Next());
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var entry)) return false;
        _undo.Push(entry);
        State = new(entry.After, State.Revision.Next());
        return true;
    }
}
