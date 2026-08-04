using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed class AssetProcessingProfileCommandHistory
{
    private readonly Stack<(IReadOnlyList<AssetProcessingProfile> Before, IReadOnlyList<AssetProcessingProfile> After)> _undo = [];
    private readonly Stack<(IReadOnlyList<AssetProcessingProfile> Before, IReadOnlyList<AssetProcessingProfile> After)> _redo = [];

    public AssetProcessingProfileState State { get; private set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public AssetProcessingProfileCommandHistory(IReadOnlyList<AssetProcessingProfile>? profiles = null)
    {
        State = new AssetProcessingProfileState(profiles, new AssetProcessingProfileRevision(0));
    }

    public void Record(AssetProcessingProfileCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Succeeded || !result.Audit.Applied || result.NewRevision is null ||
            result.Patch is null || result.Inverse is null || result.BaseRevision != State.Revision)
        {
            throw new InvalidOperationException(
                "Only an applied asset processing profile command at the current revision can be recorded.");
        }

        _undo.Push((State.Profiles, result.State.Profiles));
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
        State = new AssetProcessingProfileState(entry.Before, State.Revision.Next());
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var entry))
        {
            return false;
        }

        _undo.Push(entry);
        State = new AssetProcessingProfileState(entry.After, State.Revision.Next());
        return true;
    }
}
