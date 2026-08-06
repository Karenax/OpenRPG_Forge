using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed class AssistedAreaPlanCommandHistory
{
    private readonly Stack<(AssistedAreaPlanCatalog Before, AssistedAreaPlanCatalog After)> _undo = [];
    private readonly Stack<(AssistedAreaPlanCatalog Before, AssistedAreaPlanCatalog After)> _redo = [];

    public AssistedAreaPlanState State { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public AssistedAreaPlanCommandHistory(AssistedAreaPlanCatalog? catalog = null)
    {
        State = new AssistedAreaPlanState(catalog);
    }

    public void Record(AssistedAreaPlanCommandResult result)
    {
        if (!result.Succeeded || !result.Audit.Applied || result.NewRevision is null ||
            result.Patch is null || result.Inverse is null || result.BaseRevision != State.Revision)
            throw new InvalidOperationException("Only an applied area-plan command at the current revision can be recorded.");
        _undo.Push((State.Catalog, result.State.Catalog));
        _redo.Clear();
        State = result.State;
    }

    public bool Undo()
    {
        if (!_undo.TryPop(out var entry)) return false;
        _redo.Push(entry);
        State = new AssistedAreaPlanState(entry.Before, State.Revision.Next());
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var entry)) return false;
        _undo.Push(entry);
        State = new AssistedAreaPlanState(entry.After, State.Revision.Next());
        return true;
    }
}
