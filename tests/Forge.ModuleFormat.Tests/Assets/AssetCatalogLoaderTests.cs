using Forge.Domain.Assets;
using Forge.ModuleFormat.Assets;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class AssetCatalogLoaderTests
{
    [Fact]
    public void LoaderMergesVersionedCatalogsAndDetectsLocalAvailability()
    {
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-asset-loader", Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(root, "vault");
        var catalogs = Path.Combine(root, "asset_manifests", "catalogs");
        Directory.CreateDirectory(catalogs);
        Directory.CreateDirectory(Path.Combine(vault, "normalized", "ambientcg", "Apple"));
        Directory.CreateDirectory(Path.Combine(vault, "normalized", "kenney", "nature"));
        Directory.CreateDirectory(Path.Combine(vault, "normalized", "kaykit", "dungeon"));
        File.WriteAllText(Path.Combine(vault, "normalized", "ambientcg", "Apple", "apple.obj"), "o apple");
        File.WriteAllText(Path.Combine(vault, "normalized", "ambientcg", "Apple", "apple.png"), "preview");
        File.WriteAllText(Path.Combine(vault, "normalized", "kenney", "nature", "tree.gltf"), "{}");
        File.WriteAllText(Path.Combine(vault, "normalized", "kenney", "nature", "tree.glb"), "glTF");
        File.WriteAllText(Path.Combine(vault, "normalized", "kenney", "nature", "tree.fbx"), "fbx");
        File.WriteAllText(Path.Combine(vault, "normalized", "kaykit", "dungeon", "door.obj"), "o door");
        File.WriteAllText(Path.Combine(vault, "normalized", "kaykit", "dungeon", "door.fbx"), "fbx");
        File.WriteAllText(Path.Combine(catalogs, "cc0_3d_all.json"), UnitCatalogJson);
        File.WriteAllText(Path.Combine(catalogs, "kenney_3d_packs.json"), PackCatalogJson);
        File.WriteAllText(Path.Combine(catalogs, "kaykit_medieval_fantasy.json"), KayKitCatalogJson);
        try
        {
            var initial = AssetCatalogLoader.Load(root, vault);
            Assert.True(initial.Succeeded, string.Join(Environment.NewLine, initial.Diagnostics));
            var installed = new AssetVaultPackageStore(vault).Apply(new AssetVaultPackageStore(vault).PlanInstall(
                initial.Catalog!,
                "food-core",
                "1.0.0",
                ["ambientcg:Apple"],
                dryRun: false));
            Assert.True(installed.Succeeded, string.Join(Environment.NewLine, installed.Diagnostics));
            var result = AssetCatalogLoader.Load(root, vault);
            var repeated = AssetCatalogLoader.Load(root, vault);

            Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics));
            Assert.Equal(3, result.Catalog!.Items.Count);
            Assert.Equal(result.Catalog.Items.Select(item => item.Id), repeated.Catalog!.Items.Select(item => item.Id));
            var apple = result.Catalog.Items.Single(item => item.ExternalId == "ambientcg:Apple");
            Assert.Equal(AssetAvailability.ExtractedAvailable, apple.Availability);
            Assert.True(apple.CanPreviewLocally);
            Assert.True(apple.Installed);
            Assert.Equal(["food-core"], apple.InstalledPackIds);
            Assert.Single(result.Catalog.Credits);
            Assert.Equal(64, apple.ContentSha256.Length);
            Assert.Equal("normalized/ambientcg/Apple/apple.png", apple.RelativePreviewPath);
            var nature = result.Catalog.Items.Single(item => item.ExternalId == "kenney:nature");
            Assert.Equal("normalized/kenney/nature/tree.glb", nature.RelativeModelPath);
            var dungeon = result.Catalog.Items.Single(item => item.ExternalId == "kaykit:dungeon");
            Assert.Equal("normalized/kaykit/dungeon/door.fbx", dungeon.RelativeModelPath);
            Assert.Contains("dungeon", dungeon.Categories);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private const string UnitCatalogJson = """
        {
          "schema_version": 1,
          "catalog_date": "2026-08-02",
          "license_policy": "cc0-only",
          "assets": [
            {
              "asset_id": "ambientcg:Apple",
              "schema_version": 1,
              "name": "Apple",
              "source": "ambientcg",
              "source_asset_id": "Apple",
              "source_page": "https://example.invalid/apple",
              "license": "CC0-1.0",
              "license_url": "https://example.invalid/license",
              "license_verified": true,
              "status": "downloaded-and-extracted",
              "total_bytes": 10,
              "categories": ["3d-model", "food"],
              "tags": ["apple"],
              "extracted_root": "normalized/ambientcg/Apple",
              "model_files": ["apple.obj"],
              "extracted_files": ["apple.png"],
              "files": [
                { "path": "originals/apple.zip", "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" }
              ]
            }
          ]
        }
        """;

    private const string PackCatalogJson = """
        {
          "schema_version": 1,
          "catalog_date": "2026-08-02",
          "license_policy": "cc0-only",
          "packs": [
            {
              "pack_id": "kenney:nature",
              "schema_version": 1,
              "name": "Nature Pack",
              "source": "kenney",
              "source_page": "https://example.invalid/nature",
              "license": "CC0-1.0",
              "license_url": "https://example.invalid/license",
              "license_verified": true,
              "status": "downloaded-and-extracted",
              "total_bytes": 20,
              "extracted_root": "normalized/kenney/nature",
              "model_files": ["tree.fbx", "tree.gltf", "tree.glb"],
              "files": [
                { "path": "originals/nature.zip", "sha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" }
              ]
            }
          ]
        }
        """;

    private const string KayKitCatalogJson = """
        {
          "schema_version": 1,
          "catalog_date": "2026-08-07",
          "license_policy": "cc0-only",
          "packs": [
            {
              "pack_id": "kaykit:dungeon",
              "schema_version": 1,
              "name": "Dungeon Pack",
              "source": "kaykit",
              "source_page": "https://example.invalid/dungeon",
              "license": "CC0-1.0",
              "license_url": "https://example.invalid/license",
              "license_verified": true,
              "status": "downloaded-and-extracted",
              "total_bytes": 20,
              "categories": ["Dungeon", "Architecture"],
              "tags": ["modular"],
              "extracted_root": "normalized/kaykit/dungeon",
              "model_files": ["door.obj", "door.fbx"],
              "files": [
                { "path": "originals/dungeon.zip", "sha256": "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc" }
              ]
            }
          ]
        }
        """;
}
