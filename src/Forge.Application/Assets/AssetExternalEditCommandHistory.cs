using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed class AssetExternalEditCommandHistory
{
    private readonly Stack<(IReadOnlyList<AssetExternalEditConfiguration> Before, IReadOnlyList<AssetExternalEditConfiguration> After)> _undo = [];
    private readonly Stack<(IReadOnlyList<AssetExternalEditConfiguration> Before, IReadOnlyList<AssetExternalEditConfiguration> After)> _redo = [];

    public AssetExternalEditState State { get; private set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public AssetExternalEditCommandHistory(IReadOnlyList<AssetExternalEditConfiguration>? configurations = null)
    {
        State = new AssetExternalEditState(configurations, new AssetExternalEditRevision(0));
    }

    public AssetExternalEditCommandHistory(AssetExternalEditState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public void Record(AssetExternalEditCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Succeeded || !result.Audit.Applied || result.NewRevision is null ||
            result.Patch is null || result.Inverse is null || result.BaseRevision != State.Revision)
        {
            throw new InvalidOperationException("Only an applied external edit command at the current revision can be recorded.");
        }

        _undo.Push((State.Configurations, result.State.Configurations));
        _redo.Clear();
        State = result.State;
    }

    public bool Undo()
    {
        if (!_undo.TryPop(out var entry))
        {
            return false;
        }

        _redo.Push(entry);
        State = new AssetExternalEditState(entry.Before, State.Revision.Next());
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var entry))
        {
            return false;
        }

        _undo.Push(entry);
        State = new AssetExternalEditState(entry.After, State.Revision.Next());
        return true;
    }
}
