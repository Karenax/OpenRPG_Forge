using Forge.Application.Assets;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;

namespace Forge.Application.Tests.Assets;

public sealed class InstallAssetBlueprintHandlerTests
{
    private readonly InstallAssetBlueprintHandler _handler =
        new(new FixedClock(DateTimeOffset.Parse("2026-08-05T10:00:00Z")));

    [Fact]
    public void Handle_PreviewsThenInstallsGovernedBlueprintWithUndoRedo()
    {
        var context = new PlaceEntityTestContext();
        var history = new ProjectCommandHistory(context.State);
        var item = CatalogItem();
        var manifest = Manifest(item, "assets/imported/castle/model.glb", canonical: true);
        var dryRun = _handler.Handle(history.State, Command(history.State.Revision, item, manifest, dryRun: true));
        var applied = _handler.Handle(history.State, Command(history.State.Revision, item, manifest));
        history.Record(applied);

        Assert.True(dryRun.Succeeded);
        Assert.False(dryRun.Audit.Applied);
        Assert.Same(context.State, dryRun.State);
        Assert.Equal(2, history.State.Project.Blueprints.Count);
        var blueprint = history.State.Project.Blueprints.Single(candidate => candidate.DisplayName == item.DisplayName);
        Assert.Equal(BlueprintKind.Placeable, blueprint.Kind);
        Assert.Equal(BlueprintPaletteOrigin.ProjectContent, blueprint.PaletteOrigin);
        Assert.True(blueprint.IsFavorite);
        Assert.Equal(item.Author, blueprint.Provenance.Author);
        Assert.Equal(item.LicenseSpdx, blueprint.Provenance.LicenseSpdx);
        Assert.Equal(Assert.Single(manifest.Chunks).ContentSha256, blueprint.Provenance.ContentHash);
        Assert.True(AssetBlueprintFactory.TryGetAssetId(blueprint, out var assetId));
        Assert.Equal(item.Id, assetId);
        Assert.True(history.Undo().Succeeded);
        Assert.Single(history.State.Project.Blueprints);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(2, history.State.Project.Blueprints.Count);
    }

    [Fact]
    public void Handle_RejectsUnverifiedGovernance()
    {
        var context = new PlaceEntityTestContext();
        var item = CatalogItem(licenseVerified: false);
        var manifest = Manifest(item, "assets/imported/castle/model.glb", canonical: true);

        var result = _handler.Handle(context.State, Command(context.State.Revision, item, manifest));

        Assert.False(result.Succeeded);
        Assert.Equal(InstallAssetBlueprintHandler.GovernanceInvalidCode, Assert.Single(result.Diagnostics).Code);
        Assert.Same(context.State, result.State);
    }

    [Fact]
    public void Handle_RejectsNonCanonicalModelChunk()
    {
        var context = new PlaceEntityTestContext();
        var item = CatalogItem();
        var manifest = Manifest(item, "assets/staging/castle/model.obj", canonical: false);

        var result = _handler.Handle(context.State, Command(context.State.Revision, item, manifest));

        Assert.False(result.Succeeded);
        Assert.Equal(InstallAssetBlueprintHandler.CanonicalModelRequiredCode, Assert.Single(result.Diagnostics).Code);
    }

    private static InstallAssetBlueprintCommand Command(
        ProjectRevision revision,
        AssetCatalogItem item,
        AssetPackageManifest manifest,
        bool dryRun = false) =>
        new(CommandId.From(Guid.NewGuid()), revision, item, manifest, dryRun);

    private static AssetPackageManifest Manifest(
        AssetCatalogItem item,
        string path,
        bool canonical)
    {
        var sourceFormat = canonical ? "glb" : "obj";
        var profile = new AssetProcessingProfile(
            item.Id,
            AssetProcessingProfile.CurrentSchemaVersion,
            AssetProcessingKind.Model,
            sourceFormat,
            "glb",
            item.LicenseSpdx,
            item.ContentSha256,
            item.Provenance,
            8 * 1024 * 1024,
            model: new(false, true, true, [], [1m, 0.5m]));
        return AssetPackageManifestFactory.Create(
            item,
            profile,
            new AssetManifestArtifactInput(path, new string('b', 64), 4096, canonical),
            AssetPackageManifestAuthoringOptions.Default);
    }

    private static AssetCatalogItem CatalogItem(bool licenseVerified = true) =>
        new(
            AssetCatalogItemId.ForExternalId("kenney:test-castle"),
            "kenney:test-castle",
            "Test Castle",
            "kenney",
            "test-castle",
            "https://kenney.nl/assets/test-castle",
            "CC0-1.0",
            "https://creativecommons.org/publicdomain/zero/1.0/",
            licenseVerified,
            new string('a', 64),
            4096,
            ["buildings"],
            ["castle"],
            "downloaded",
            AssetAvailability.ExtractedAvailable,
            "normalized/kenney/test-castle/model.glb",
            null,
            null,
            "asset-catalog:kenney:test-castle",
            author: "Kenney");
}
