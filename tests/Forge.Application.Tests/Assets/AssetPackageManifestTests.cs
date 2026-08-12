using Forge.Application.Assets;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Assets;

namespace Forge.Application.Tests.Assets;

public sealed class AssetPackageManifestTests
{
    private readonly SetAssetPackageManifestHandler _handler =
        new(new FixedClock(DateTimeOffset.Parse("2026-08-04T20:00:00Z")));

    [Fact]
    public void FactoryIsDeterministicAndCarriesGovernedExtensions()
    {
        var item = CatalogItem();
        var profile = ModelProfile(item);
        var artifact = new AssetManifestArtifactInput(
            "assets/imported/castle/model.glb",
            new string('b', 64),
            4096,
            true);
        var options = AssetPackageManifestAuthoringOptions.Default with { IncludeVfx = true };

        var first = AssetPackageManifestFactory.Create(item, profile, artifact, options);
        var second = AssetPackageManifestFactory.Create(item, profile, artifact, options);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(Assert.Single(first.Chunks).Id, Assert.Single(second.Chunks).Id);
        Assert.NotNull(first.Collision);
        Assert.NotNull(first.Animation);
        Assert.NotNull(first.Vfx);
        Assert.Equal(3, first.Lod!.Levels.Count);
        Assert.Empty(AssetPackageManifestValidator.Validate(first));
    }

    [Fact]
    public void CommandSupportsDryRunApplyUndoAndRedo()
    {
        var manifest = CreateManifest();
        var history = new AssetPackageManifestCommandHistory();
        var dryRun = _handler.Handle(
            history.State,
            Command(history.State.Revision, manifest, dryRun: true));
        var applied = _handler.Handle(
            history.State,
            Command(history.State.Revision, manifest));
        history.Record(applied);

        Assert.True(dryRun.Succeeded);
        Assert.False(dryRun.Audit.Applied);
        Assert.Empty(dryRun.State.Manifests);
        Assert.Single(history.State.Manifests);
        Assert.True(history.Undo());
        Assert.Empty(history.State.Manifests);
        Assert.True(history.Redo());
        Assert.Single(history.State.Manifests);
    }

    [Fact]
    public void ResolverIgnoresOptionalExtensionButRejectsMissingRequiredChunk()
    {
        var manifest = CreateManifest();
        var path = Assert.Single(manifest.Chunks).RelativePath;
        var available = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [path] = Assert.Single(manifest.Chunks).ContentSha256,
        };

        var resolved = AssetPackageManifestResolver.Resolve(
            manifest,
            AssetTargetPlatform.Windows,
            available);
        var missing = AssetPackageManifestResolver.Resolve(
            manifest,
            AssetTargetPlatform.Windows,
            new Dictionary<string, string>(StringComparer.Ordinal));
        var corrupted = AssetPackageManifestResolver.Resolve(
            manifest,
            AssetTargetPlatform.Windows,
            new Dictionary<string, string>(StringComparer.Ordinal) { [path] = new string('c', 64) });

        Assert.True(resolved.Succeeded);
        Assert.Equal(
            AssetPackageManifestResolver.RequiredChunkMissingCode,
            Assert.Single(missing.Diagnostics).Code);
        Assert.Equal(
            AssetPackageManifestResolver.ChunkHashMismatchCode,
            Assert.Single(corrupted.Diagnostics).Code);
    }

    [Fact]
    public void InvalidManifestAndStaleRevisionReturnStableDiagnostics()
    {
        var state = new AssetPackageManifestState();
        var invalid = CreateManifest() with { SourceContentSha256 = "invalid" };
        var rejected = _handler.Handle(state, Command(state.Revision, invalid));
        var stale = _handler.Handle(
            state,
            Command(new AssetPackageManifestRevision(9), CreateManifest()));

        Assert.Equal(
            AssetPackageManifestValidator.GovernanceMissingCode,
            Assert.Single(rejected.Diagnostics).Code);
        Assert.Equal(
            SetAssetPackageManifestHandler.RevisionConflictCode,
            Assert.Single(stale.Diagnostics).Code);
    }

    [Fact]
    public void ContentExtensionsKeepFontsNavigationAndCollisionGovernedAndSeparate()
    {
        var basis = CreateManifest();
        var platforms = new[] { AssetTargetPlatform.Windows, AssetTargetPlatform.Linux };
        var collision = new AssetCollisionAuthoringInput(
            Artifact("assets/castle/collision.mesh", 'c', platforms), true,
            AssetCollisionGeneration.Manual, ["stone", "stairs"], platforms);
        var navigation = new AssetNavigationAuthoringInput(
            Artifact("assets/castle/navigation.navmesh", 'd', platforms), true,
            AssetNavigationGeneration.Manual, ["walkable", "blocked"], platforms);
        var font = new AssetFontAuthoringInput(
            "Atkinson Hyperlegible", Artifact("assets/fonts/atkinson.ttf", 'e', platforms),
            [new("Noto Sans", Artifact("assets/fonts/noto-sans.ttf", 'f', platforms))], true,
            ["Latn", "Arab"], ["fr-FR", "ar"], [96, 144, 192], 0.8m, 2m);

        var extended = AssetPackageManifestFactory.AttachContentExtensions(basis, collision, navigation, font);
        var history = new AssetPackageManifestCommandHistory();
        var applied = _handler.Handle(history.State, Command(history.State.Revision, extended));
        history.Record(applied);

        Assert.Empty(AssetPackageManifestValidator.Validate(extended));
        Assert.Equal(4, extended.Chunks.Select(chunk => chunk.Role)
            .Count(role => role is AssetChunkRole.Collision or AssetChunkRole.Navigation or AssetChunkRole.Font));
        Assert.NotEqual(extended.Collision!.ChunkId, extended.Navigation!.ChunkId);
        Assert.Equal(["Latn", "Arab"], extended.Font!.ShapingScripts);
        Assert.Equal([96, 144, 192], extended.Font.DpiBuckets);
        Assert.True(applied.Succeeded);
        Assert.True(history.Undo());
        Assert.True(history.Redo());
        Assert.Equal(extended, Assert.Single(history.State.Manifests));
    }

    [Fact]
    public void InvalidNavigationAndFontReturnLocalizedStableDiagnostics()
    {
        var manifest = CreateManifest() with
        {
            Navigation = new AssetNavigationManifest(
                true, Guid.NewGuid(), AssetNavigationGeneration.Generated, [], [AssetTargetPlatform.Windows]),
            Font = new AssetFontManifest(
                true, "", null, [], ["latin"], [], [48], 2m, 1m),
        };

        var diagnostics = AssetPackageManifestValidator.Validate(manifest);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == AssetPackageManifestValidator.NavigationInvalidCode &&
            diagnostic.Path.EndsWith("/navigation", StringComparison.Ordinal));
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == AssetPackageManifestValidator.FontInvalidCode &&
            diagnostic.Path.EndsWith("/font", StringComparison.Ordinal));
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == AssetPackageManifestValidator.ReferenceMissingCode);
    }

    private static AssetExtensionArtifactInput Artifact(
        string path,
        char hashCharacter,
        IReadOnlyList<AssetTargetPlatform> platforms) =>
        new(path, new string(hashCharacter, 64), 2048, 1024, AssetChunkCompression.Brotli, platforms);

    private static SetAssetPackageManifestCommand Command(
        AssetPackageManifestRevision revision,
        AssetPackageManifest manifest,
        bool dryRun = false) =>
        new(CommandId.From(Guid.NewGuid()), revision, manifest, dryRun);

    private static AssetPackageManifest CreateManifest()
    {
        var item = CatalogItem();
        return AssetPackageManifestFactory.Create(
            item,
            ModelProfile(item),
            new AssetManifestArtifactInput(
                "assets/imported/castle/model.glb",
                new string('b', 64),
                4096,
                true),
            AssetPackageManifestAuthoringOptions.Default with { IncludeVfx = true });
    }

    private static AssetProcessingProfile ModelProfile(AssetCatalogItem item) =>
        new(
            item.Id,
            AssetProcessingProfile.CurrentSchemaVersion,
            AssetProcessingKind.Model,
            "glb",
            "glb",
            item.LicenseSpdx,
            item.ContentSha256,
            item.Provenance,
            8 * 1024 * 1024,
            model: new(true, true, true, ["hand.r"], [1m, 0.5m, 0.25m]));

    private static AssetCatalogItem CatalogItem() =>
        new(
            AssetCatalogItemId.ForExternalId("kenney:test-castle"),
            "kenney:test-castle",
            "Test Castle",
            "kenney",
            "test-castle",
            "https://kenney.nl/assets/test-castle",
            "CC0-1.0",
            "https://creativecommons.org/publicdomain/zero/1.0/",
            true,
            new string('a', 64),
            4096,
            ["buildings"],
            ["castle"],
            "downloaded",
            AssetAvailability.ExtractedAvailable,
            "normalized/kenney/test-castle/model.glb",
            null,
            null,
            "asset-catalog:kenney:test-castle");
}
