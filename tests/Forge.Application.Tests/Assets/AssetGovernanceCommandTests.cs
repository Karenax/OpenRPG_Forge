using Forge.Application.Assets;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Assets;

namespace Forge.Application.Tests.Assets;

public sealed class AssetGovernanceCommandTests
{
    private readonly SetAssetGovernanceHandler _handler = new(new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public void DryRunApplyUndoRedoAndSafeScriptHandleShareOneCanonicalState()
    {
        var record = ValidRecord();
        var history = new AssetGovernanceCommandHistory();
        var dryRun = _handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, record, true));

        Assert.True(dryRun.Succeeded);
        Assert.Empty(history.State.Records);
        Assert.Equal("add", dryRun.Patch!.Operation);
        var applied = _handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, record, false));
        history.Record(applied);
        var script = new AssetGovernanceScriptService(history.State, new Forge.Scripting.ForgeScriptHandleRegistry());
        Assert.True(script.TryOpen(record.AssetId, out var handle));
        Assert.Equal(record, script.Resolve(handle));
        Assert.Equal(Forge.Scripting.ForgeScriptHandleKind.AssetGovernance, handle.Kind);
        Assert.True(history.Undo());
        Assert.Empty(history.State.Records);
        Assert.True(history.Redo());
        Assert.Single(history.State.Records);
    }

    [Fact]
    public void InvalidAndStaleCommandsAreRejectedWithoutMutation()
    {
        var state = new AssetGovernanceState();
        var invalid = ValidRecord() with { PermissionEvidence = string.Empty };

        var rejected = _handler.Handle(state,
            new(CommandId.From(Guid.NewGuid()), state.Revision, invalid, false));
        var stale = _handler.Handle(state,
            new(CommandId.From(Guid.NewGuid()), new AssetGovernanceRevision(9), ValidRecord(), false));

        Assert.False(rejected.Succeeded);
        Assert.Contains(rejected.Diagnostics, item => item.Code == AssetGovernanceValidator.ProvenanceIncompleteCode);
        Assert.Equal(SetAssetGovernanceHandler.RevisionConflictCode, Assert.Single(stale.Diagnostics).Code);
        Assert.Empty(state.Records);
    }

    private static AssetGovernanceRecord ValidRecord()
    {
        var assetId = AssetCatalogItemId.ForExternalId("test:application-governance");
        return new(1, AssetGovernanceId.ForAsset(assetId), assetId, AssetPackageManifestId.ForAsset(assetId),
            "CC0-1.0", new string('b', 64), "Author", "https://example.invalid/model", "permission:fixture",
            "No modifications.", false, string.Empty, string.Empty, 4096,
            new("incoming/model.glb", "model/gltf-binary", "model/gltf-binary", 12, 4096, "scanner", "clean",
                "valid", AssetQuarantineStatus.Approved, "reviewer", DateTimeOffset.UnixEpoch),
            new(AssetOriginKind.UserOwned, true, false, string.Empty));
    }
}
