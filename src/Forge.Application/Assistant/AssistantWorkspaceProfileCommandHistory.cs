using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed class AssistantWorkspaceProfileCommandHistory
{
    private readonly Stack<(AssistantWorkspaceProfile Before, AssistantWorkspaceProfile After)> _undo = [];
    private readonly Stack<(AssistantWorkspaceProfile Before, AssistantWorkspaceProfile After)> _redo = [];

    public AssistantWorkspaceProfileState State { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public AssistantWorkspaceProfileCommandHistory(AssistantWorkspaceProfile? profile = null)
    {
        State = new AssistantWorkspaceProfileState(profile);
    }

    public void Record(AssistantWorkspaceProfileCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Succeeded || !result.Audit.Applied || result.NewRevision is null ||
            result.Patch is null || result.Inverse is null || result.BaseRevision != State.Revision)
        {
            throw new InvalidOperationException("Only an applied assistant profile command at the current revision can be recorded.");
        }
        _undo.Push((State.Profile, result.State.Profile));
        _redo.Clear();
        State = result.State;
    }

    public bool Undo()
    {
        if (!_undo.TryPop(out var entry)) return false;
        _redo.Push(entry);
        State = new AssistantWorkspaceProfileState(entry.Before, State.Revision.Next());
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var entry)) return false;
        _undo.Push(entry);
        State = new AssistantWorkspaceProfileState(entry.After, State.Revision.Next());
        return true;
    }
}
