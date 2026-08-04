using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed class AssetPackageManifestCommandHistory
{
    private readonly Stack<(IReadOnlyList<AssetPackageManifest> Before, IReadOnlyList<AssetPackageManifest> After)> _undo = [];
    private readonly Stack<(IReadOnlyList<AssetPackageManifest> Before, IReadOnlyList<AssetPackageManifest> After)> _redo = [];

    public AssetPackageManifestState State { get; private set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public AssetPackageManifestCommandHistory(IReadOnlyList<AssetPackageManifest>? manifests = null)
    {
        State = new AssetPackageManifestState(manifests, new AssetPackageManifestRevision(0));
    }

    public void Record(AssetPackageManifestCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Succeeded || !result.Audit.Applied || result.NewRevision is null ||
            result.Patch is null || result.Inverse is null || result.BaseRevision != State.Revision)
        {
            throw new InvalidOperationException(
                "Only an applied asset package manifest command at the current revision can be recorded.");
        }

        _undo.Push((State.Manifests, result.State.Manifests));
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
        State = new AssetPackageManifestState(entry.Before, State.Revision.Next());
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var entry))
        {
            return false;
        }

        _undo.Push(entry);
        State = new AssetPackageManifestState(entry.After, State.Revision.Next());
        return true;
    }
}
