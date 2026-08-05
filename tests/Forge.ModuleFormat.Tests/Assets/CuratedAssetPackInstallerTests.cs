using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class CuratedAssetPackInstallerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"forge-curated-pack-{Guid.NewGuid():N}");

    [Fact]
    public void InstallsCanonicalModelAndExternalTextureWithStableGovernance()
    {
        var repository = Path.Combine(_root, "repository");
        var vault = Path.Combine(_root, "vault");
        var workspace = Path.Combine(_root, "workspace");
        var sourceRoot = Path.Combine(vault, "normalized", "kenney", "mini-forest");
        var modelPath = Path.Combine(sourceRoot, "Models", "GLB format", "tree.glb");
        var texturePath = Path.Combine(sourceRoot, "Models", "GLB format", "Textures", "colormap.png");
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(texturePath)!);
        Directory.CreateDirectory(Path.Combine(repository, "asset_manifests", "catalogs"));
        Directory.CreateDirectory(Path.Combine(repository, "asset_manifests", "packs"));
        Directory.CreateDirectory(workspace);
        File.WriteAllBytes(modelPath, [0x67, 0x6c, 0x54, 0x46, 0x01]);
        File.WriteAllBytes(texturePath, [0x89, 0x50, 0x4e, 0x47, 0x01]);
        File.WriteAllText(Path.Combine(repository, "asset_manifests", "catalogs", "kenney_3d_packs.json"),
            """
            {"packs":[{"pack_id":"kenney:mini-forest","extracted_root":"normalized/kenney/mini-forest","license":"CC0-1.0","license_url":"https://kenney.nl/assets/mini-forest","license_verified":true,"source_page":"https://kenney.nl/assets/mini-forest"}]}
            """);
        var manifestPath = Path.Combine(repository, "asset_manifests", "packs", "test.json");
        File.WriteAllText(manifestPath,
            """
            {"schema_version":1,"pack_id":"forgotten-forest-core","version":"1.0.0","display_name":"Test Forest","source_catalog":"asset_manifests/catalogs/kenney_3d_packs.json","source_pack_id":"kenney:mini-forest","license":"CC0-1.0","models":[{"key":"tree","path":"Models/GLB format/tree.glb","tags":["tree"]}]}
            """);

        var first = CuratedAssetPackInstaller.InstallForgottenForest(repository, vault, workspace, manifestPath);
        var second = CuratedAssetPackInstaller.InstallForgottenForest(repository, vault, workspace, manifestPath);

        var entry = Assert.Single(first.Entries);
        Assert.Equal("CC0-1.0", first.LicenseSpdx);
        Assert.Equal(1, second.ReusedCount);
        Assert.True(File.Exists(Path.Combine(workspace, entry.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(
            workspace,
            Path.GetDirectoryName(entry.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar))!,
            "Textures",
            "colormap.png")));
        var package = Assert.Single(AssetPackageManifestStore.Load(workspace).Manifests);
        Assert.Equal(2, package.Chunks.Count);
        Assert.Contains(package.Chunks, chunk => chunk.RelativePath.EndsWith("Textures/colormap.png", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(workspace, "assets", "CREDITS_ASSETS.md")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
