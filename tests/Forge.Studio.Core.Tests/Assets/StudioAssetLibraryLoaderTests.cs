using System.Text.Json;
using Forge.Studio.Core.Assets;

namespace Forge.Studio.Core.Tests.Assets;

public sealed class StudioAssetLibraryLoaderTests
{
    [Fact]
    public void GovernedLocalGltfIsInstallable()
    {
        var item = new StudioAssetLibraryItem(
            "test:pack:stall", "Stall", "test", "test:pack", "Test Pack",
            "model", "gltf", "normalized/test/stall.gltf", null,
            "https://example.invalid/stall", "CC0-1.0",
            "https://creativecommons.org/publicdomain/zero/1.0/", true,
            "Test", false, string.Empty, ["stall"], ["props"],
            [new StudioAssetVariant("gltf", "normalized/test/stall.gltf", true, 128)],
            128, true);

        Assert.True(item.CanInstall);
    }

    [Fact]
    public void Load_SplitsAndGroupsPackModelsAndPrefersLocalGlbWithMatchingPreview()
    {
        var root = Path.Combine(Path.GetTempPath(), $"forge-studio-assets-{Guid.NewGuid():N}");
        var repository = Path.Combine(root, "repository");
        var vault = Path.Combine(root, "vault");
        var catalogDirectory = Path.Combine(repository, "asset_manifests", "catalogs");
        var collectionDirectory = Path.Combine(repository, "asset_manifests", "packs");
        var packRoot = Path.Combine(vault, "normalized", "kenney", "castle-kit");
        try
        {
            Directory.CreateDirectory(catalogDirectory);
            Directory.CreateDirectory(collectionDirectory);
            Directory.CreateDirectory(Path.Combine(packRoot, "Models", "GLB"));
            Directory.CreateDirectory(Path.Combine(packRoot, "Models", "OBJ"));
            Directory.CreateDirectory(Path.Combine(packRoot, "Previews"));
            File.WriteAllBytes(Path.Combine(packRoot, "Models", "GLB", "door.glb"), [1, 2, 3]);
            File.WriteAllBytes(Path.Combine(packRoot, "Models", "GLB", "window.gltf.glb"), [1, 2, 3, 4]);
            File.WriteAllBytes(Path.Combine(packRoot, "Models", "GLB", "window.preview.png"), [7, 8, 9]);
            File.WriteAllText(Path.Combine(packRoot, "Models", "OBJ", "door.obj"), "o Door");
            File.WriteAllBytes(Path.Combine(packRoot, "Previews", "door.png"), [4, 5, 6]);
            var payload = new
            {
                schema_version = 1,
                packs = new[]
                {
                    new
                    {
                        pack_id = "kenney:castle-kit",
                        name = "Castle Kit",
                        source = "kenney",
                        source_page = "https://kenney.nl/assets/castle-kit",
                        license = "CC0-1.0",
                        license_url = "https://creativecommons.org/publicdomain/zero/1.0/",
                        license_verified = true,
                        extracted_root = "normalized/kenney/castle-kit",
                        model_files = new[]
                        {
                            "Models/OBJ/door.obj",
                            "Models/GLB/door.glb",
                            "Models/GLB/window.gltf.glb",
                        },
                        extracted_files = new[] { "Previews/door.png" },
                    },
                },
            };
            File.WriteAllText(
                Path.Combine(catalogDirectory, "test.json"),
                JsonSerializer.Serialize(payload));
            File.WriteAllText(
                Path.Combine(collectionDirectory, "medieval.json"),
                JsonSerializer.Serialize(new
                {
                    schema_version = 1,
                    collection_type = "asset-vault-collection",
                    pack_id = "medieval",
                    display_name = "Médiéval",
                    source_pack_ids = new[] { "kenney:castle-kit" },
                    tags = new[] { "medieval" },
                }));

            var index = StudioAssetLibraryLoader.Load(repository, vault);

            Assert.Equal(2, index.Items.Count);
            Assert.Equal(2, index.ThumbnailCount);
            Assert.Equal(2, index.Preview3DCount);
            Assert.Equal(0, index.MissingPreviewCount);
            var item = Assert.Single(index.Items, candidate => candidate.ExternalId == "kenney:castle-kit:door");
            Assert.Equal("kenney:castle-kit:door", item.ExternalId);
            Assert.Equal("Door", item.DisplayName);
            Assert.Equal("medieval", item.PackId);
            Assert.Equal("Médiéval", item.PackName);
            Assert.Equal("glb", item.Format);
            Assert.True(item.AvailableLocally);
            Assert.True(item.CanInstall);
            Assert.Equal("normalized/kenney/castle-kit/Previews/door.png", item.RelativePreviewPath);
            Assert.Equal(2, item.Variants.Count);
            Assert.Contains("castle", item.SearchText);
            Assert.Contains("medieval", item.SearchText);
            Assert.True(item.HasThumbnail);
            Assert.True(item.CanPreview3D);

            var nested = Assert.Single(index.Items, candidate =>
                candidate.ExternalId == "kenney:castle-kit:window");
            Assert.Equal("Window", nested.DisplayName);
            Assert.Equal(
                "normalized/kenney/castle-kit/Models/GLB/window.preview.png",
                nested.RelativePreviewPath);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
