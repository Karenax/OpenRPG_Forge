using Forge.Domain.Content;

namespace Forge.Domain.Tests.Content;

public sealed class ContentResourceRegistryTests
{
    [Fact]
    public void RegistryResolvesCanonicalAddressAliasAndReferencesWithUuidBacking()
    {
        var sourceId = Guid.Parse("c0170000-0000-4000-8000-000000000001");
        var textureId = Guid.Parse("c0170000-0000-4000-8000-000000000002");
        var source = new ContentPackageSource(sourceId, 1, "test:r017", "creatures/warden",
            "data/warden.json", ContentSourceKind.Json, true, "CC0-1.0", new string('a', 64), 1024,
            ["legacy.core:text/warden"], []);
        var texture = new ContentPackageAsset(textureId, 1, "test:r017", "portraits/warden",
            "textures/warden.ktx2", true, "CC0-1.0", new string('b', 64), 4096, [],
            ["crypt.core:text/creatures/warden"]);
        var catalog = Catalog([source], [texture]);

        var result = ContentResourceRegistry.Build(catalog);
        var registry = ContentResourceRegistry.Create(result);

        Assert.True(result.Valid);
        Assert.Equal(sourceId, result.Resources.Single(item => item.Kind == ContentResourceKind.Text).Id);
        Assert.True(registry.TryResolve("legacy.core:text/warden", out var aliased));
        Assert.Equal(sourceId, aliased!.Id);
        Assert.Equal("crypt.core:text/creatures/warden", aliased.Address);
        Assert.Equal("crypt.core:text/creatures/warden",
            result.Resources.Single(item => item.Kind == ContentResourceKind.Texture).References.Single());
    }

    [Fact]
    public void RegistryRejectsAliasCollisionsInvalidAddressesAndMissingReferences()
    {
        var first = new ContentPackageSource(Guid.Parse("c0170000-0000-4000-8000-000000000011"), 1,
            "test:r017", "first", "first.json", ContentSourceKind.Json, true, "CC0-1.0", new string('a', 64),
            1024, ["shared.core:text/alias"], ["missing.core:model/mesh"]);
        var second = new ContentPackageSource(Guid.Parse("c0170000-0000-4000-8000-000000000012"), 1,
            "test:r017", "second", "second.json", ContentSourceKind.Json, true, "CC0-1.0", new string('b', 64),
            1024, ["shared.core:text/alias"], []);
        var invalid = new ContentPackageAsset(Guid.Parse("c0170000-0000-4000-8000-000000000013"), 1,
            "test:r017", "Bad Path", "mesh.glb", true, "CC0-1.0", new string('c', 64), 2048);

        var diagnostics = ContentResourceRegistry.Build(Catalog([first, second], [invalid])).Diagnostics;

        Assert.Contains(diagnostics, item => item.Code == ContentPackageCatalogValidator.ResourceAliasCollisionCode);
        Assert.Contains(diagnostics, item => item.Code == ContentPackageCatalogValidator.ResourceReferenceCode);
        Assert.Contains(diagnostics, item => item.Code == ContentPackageCatalogValidator.ResourceAddressCode);
        Assert.All(diagnostics, item => Assert.StartsWith("module/content/resources/", item.Path));
    }

    private static ContentPackageCatalog Catalog(
        IReadOnlyList<ContentPackageSource> sources,
        IReadOnlyList<ContentPackageAsset> assets) => new(
        Guid.Parse("c0170000-0000-4000-8000-000000000099"), 1, "test:r017",
        ContentConflictPolicy.HighestPriority,
        [new ContentPackageDefinition(Guid.Parse("c0170000-0000-4000-8000-000000000098"), 1,
            "test:r017", "crypt.core", "1.0.0", "CC0-1.0", new string('d', 64), 10, [], [], sources,
            assets)]);
}
