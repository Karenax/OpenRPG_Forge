using Forge.Domain.Authoring;

namespace Forge.Application.Creator;

public sealed class EditorKeymapCommandHistory
{
    private readonly Stack<(EditorKeymapProfile Before, EditorKeymapProfile After)> _undo = [];
    private readonly Stack<(EditorKeymapProfile Before, EditorKeymapProfile After)> _redo = [];

    public EditorKeymapState State { get; private set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public EditorKeymapCommandHistory(EditorKeymapProfile? profile = null)
    {
        State = new EditorKeymapState(profile);
    }

    public void Record(EditorKeymapCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Succeeded || !result.Audit.Applied || result.NewRevision is null ||
            result.Patch is null || result.Inverse is null || result.BaseRevision != State.Revision)
            throw new InvalidOperationException("Only an applied editor keymap command at the current revision can be recorded.");
        _undo.Push((State.Profile, result.State.Profile));
        _redo.Clear();
        State = result.State;
    }

    public bool Undo()
    {
        if (!_undo.TryPop(out var entry)) return false;
        _redo.Push(entry);
        State = new EditorKeymapState(entry.Before, State.Revision.Next());
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var entry)) return false;
        _undo.Push(entry);
        State = new EditorKeymapState(entry.After, State.Revision.Next());
        return true;
    }
}
