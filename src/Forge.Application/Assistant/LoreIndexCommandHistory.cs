using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed class LoreIndexCommandHistory
{
    private readonly Stack<(LoreIndexDefinition Before, LoreIndexDefinition After)> _undo = [];
    private readonly Stack<(LoreIndexDefinition Before, LoreIndexDefinition After)> _redo = [];

    public LoreIndexState State { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public LoreIndexCommandHistory(LoreIndexDefinition? index = null)
    {
        State = new LoreIndexState(index);
    }

    public void Record(LoreIndexCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Succeeded || !result.Audit.Applied || result.NewRevision is null ||
            result.Patch is null || result.Inverse is null || result.BaseRevision != State.Revision)
        {
            throw new InvalidOperationException("Only an applied lore index command at the current revision can be recorded.");
        }
        _undo.Push((State.Index, result.State.Index));
        _redo.Clear();
        State = result.State;
    }

    public bool Undo()
    {
        if (!_undo.TryPop(out var entry)) return false;
        _redo.Push(entry);
        State = new LoreIndexState(entry.Before, State.Revision.Next());
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var entry)) return false;
        _undo.Push(entry);
        State = new LoreIndexState(entry.After, State.Revision.Next());
        return true;
    }
}
