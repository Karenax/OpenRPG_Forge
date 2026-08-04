using Forge.Application.Assets;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Assets;

namespace Forge.Application.Tests.Assets;

public sealed class AssetProcessingProfileCommandTests
{
    private static readonly AssetCatalogItemId AssetId =
        AssetCatalogItemId.From(Guid.Parse("a5100000-0000-4000-8000-000000000001"));
    private readonly SetAssetProcessingProfileHandler _handler =
        new(new FixedClock(DateTimeOffset.Parse("2026-08-04T10:00:00Z")));

    [Fact]
    public void DryRunProducesSemanticDiffWithoutMutatingState()
    {
        var state = new AssetProcessingProfileState();

        var result = _handler.Handle(state, Command(state.Revision, ModelProfile(), dryRun: true));

        Assert.True(result.Succeeded);
        Assert.Same(state, result.State);
        Assert.Null(result.NewRevision);
        Assert.False(result.Audit.Applied);
        Assert.Equal("add", result.Patch!.Operation);
        Assert.Contains(AssetId.ToString(), result.Patch.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void AppliedProfileSupportsReplaceUndoAndRedo()
    {
        var history = new AssetProcessingProfileCommandHistory();
        var added = _handler.Handle(history.State, Command(history.State.Revision, ModelProfile()));
        history.Record(added);
        var original = ModelProfile();
        var replacement = new AssetProcessingProfile(
            original.AssetId,
            original.SchemaVersion,
            original.Kind,
            original.SourceFormat,
            original.CanonicalFormat,
            original.LicenseSpdx,
            original.ContentSha256,
            original.Provenance,
            original.MaximumOutputBytes,
            model: new ModelProcessingOptions(false, false, true, ["hand.r"], [1m, 0.4m]));
        var replaced = _handler.Handle(history.State, Command(history.State.Revision, replacement));
        history.Record(replaced);

        Assert.False(history.State.Find(AssetId, AssetProcessingKind.Model)!.Model!.ImportAnimations);
        Assert.True(history.Undo());
        Assert.True(history.State.Find(AssetId, AssetProcessingKind.Model)!.Model!.ImportAnimations);
        Assert.True(history.Redo());
        Assert.False(history.State.Find(AssetId, AssetProcessingKind.Model)!.Model!.ImportAnimations);
    }

    [Fact]
    public void TextureAndModelProfilesCanCoexistForOneCatalogAsset()
    {
        var history = new AssetProcessingProfileCommandHistory();
        history.Record(_handler.Handle(history.State, Command(history.State.Revision, ModelProfile())));
        history.Record(_handler.Handle(history.State, Command(history.State.Revision, TextureProfile())));

        Assert.Equal(2, history.State.Profiles.Count);
        Assert.NotNull(history.State.Find(AssetId, AssetProcessingKind.Model));
        Assert.NotNull(history.State.Find(AssetId, AssetProcessingKind.Texture));
    }

    [Fact]
    public void InvalidProfileAndStaleRevisionReturnStableDiagnostics()
    {
        var state = new AssetProcessingProfileState();
        var original = ModelProfile();
        var invalid = new AssetProcessingProfile(
            original.AssetId,
            original.SchemaVersion,
            original.Kind,
            original.SourceFormat,
            original.CanonicalFormat,
            original.LicenseSpdx,
            original.ContentSha256,
            original.Provenance,
            0,
            model: original.Model);
        var rejected = _handler.Handle(state, Command(state.Revision, invalid));
        var stale = _handler.Handle(
            state,
            Command(new AssetProcessingProfileRevision(4), ModelProfile()));

        Assert.False(rejected.Succeeded);
        Assert.Equal(
            AssetProcessingProfileValidator.BudgetInvalidCode,
            Assert.Single(rejected.Diagnostics).Code);
        Assert.Equal(
            SetAssetProcessingProfileHandler.RevisionConflictCode,
            Assert.Single(stale.Diagnostics).Code);
    }

    private static SetAssetProcessingProfileCommand Command(
        AssetProcessingProfileRevision revision,
        AssetProcessingProfile profile,
        bool dryRun = false) =>
        new(CommandId.From(Guid.NewGuid()), revision, profile, dryRun);

    private static AssetProcessingProfile ModelProfile() =>
        new(
            AssetId,
            AssetProcessingProfile.CurrentSchemaVersion,
            AssetProcessingKind.Model,
            "obj",
            "glb",
            "CC0-1.0",
            new string('a', 64),
            "asset-catalog:test:model",
            16 * 1024 * 1024,
            model: new(false, true, true, [], [1m, 0.5m, 0.25m]));

    private static AssetProcessingProfile TextureProfile() =>
        new(
            AssetId,
            AssetProcessingProfile.CurrentSchemaVersion,
            AssetProcessingKind.Texture,
            "png",
            "ktx2",
            "CC0-1.0",
            new string('a', 64),
            "asset-catalog:test:texture",
            8 * 1024 * 1024,
            texture: new(
                TextureColorSpace.Srgb,
                TextureCompressionProfile.Desktop,
                2048,
                10.24m,
                new Dictionary<PbrTextureChannel, string>
                {
                    [PbrTextureChannel.BaseColor] = "textures/model-base.png",
                }));
}
