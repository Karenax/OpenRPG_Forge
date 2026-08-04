using Forge.Application.Assets;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Assets;

namespace Forge.Application.Tests.Assets;

public sealed class AssetExternalEditCommandTests
{
    private static readonly AssetCatalogItemId AssetId = AssetCatalogItemId.From(
        Guid.Parse("c1200000-0000-4000-8000-000000000001"));
    private readonly SetAssetExternalEditHandler _handler =
        new(new FixedClock(DateTimeOffset.Parse("2026-08-04T16:00:00Z")));

    [Fact]
    public void DryRunProducesSemanticPatchWithoutMutation()
    {
        var state = new AssetExternalEditState();

        var result = _handler.Handle(state, Command(state.Revision, Configuration(), dryRun: true));

        Assert.True(result.Succeeded);
        Assert.Same(state, result.State);
        Assert.Null(result.NewRevision);
        Assert.False(result.Audit.Applied);
        Assert.Equal("add", result.Patch!.Operation);
    }

    [Fact]
    public void ApplyReplaceUndoAndRedoRestoreCanonicalConfiguration()
    {
        var history = new AssetExternalEditCommandHistory();
        history.Record(_handler.Handle(history.State, Command(history.State.Revision, Configuration())));
        var replacement = Configuration() with { WatchForChanges = false };
        history.Record(_handler.Handle(history.State, Command(history.State.Revision, replacement)));

        Assert.False(history.State.Find(AssetId)!.WatchForChanges);
        Assert.True(history.Undo());
        Assert.True(history.State.Find(AssetId)!.WatchForChanges);
        Assert.True(history.Redo());
        Assert.False(history.State.Find(AssetId)!.WatchForChanges);
    }

    [Fact]
    public void InvalidConfigurationAndStaleRevisionReturnStableDiagnostics()
    {
        var state = new AssetExternalEditState();
        var invalid = Configuration() with { Operations = [AssetExternalOperation.EditAudio] };

        var rejected = _handler.Handle(state, Command(state.Revision, invalid));
        var stale = _handler.Handle(state, Command(new AssetExternalEditRevision(4), Configuration()));

        Assert.Equal(AssetExternalEditValidator.OperationInvalidCode, Assert.Single(rejected.Diagnostics).Code);
        Assert.Equal(SetAssetExternalEditHandler.RevisionConflictCode, Assert.Single(stale.Diagnostics).Code);
    }

    private static SetAssetExternalEditCommand Command(
        AssetExternalEditRevision revision,
        AssetExternalEditConfiguration configuration,
        bool dryRun = false) =>
        new(CommandId.From(Guid.NewGuid()), revision, configuration, dryRun);

    internal static AssetExternalEditConfiguration Configuration() =>
        new(
            AssetExternalEditConfiguration.CurrentSchemaVersion,
            AssetExternalEditId.ForAsset(AssetId),
            AssetId,
            AssetPackageManifestId.ForAsset(AssetId),
            "author:test",
            "CC0-1.0",
            new string('c', 64),
            "normalized/test/tree.obj",
            "assets/imported/tree/model.glb",
            AssetExternalTool.Blender,
            [AssetExternalOperation.ValidateModel, AssetExternalOperation.NormalizeModel, AssetExternalOperation.ExportGlb],
            true,
            [AssetTargetPlatform.Windows, AssetTargetPlatform.Linux]);
}
