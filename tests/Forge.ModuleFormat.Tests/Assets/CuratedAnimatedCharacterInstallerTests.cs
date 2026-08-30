using System.Text.Json;
using Forge.ModuleFormat.Assets;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class CuratedAnimatedCharacterInstallerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"forge-animated-character-{Guid.NewGuid():N}");

    [Fact]
    public void PlansConfinedHashedModelAnimationsAndSkinFromVerifiedCatalog()
    {
        var repository = Path.Combine(_root, "repository");
        var vault = Path.Combine(_root, "vault");
        var workspace = Path.Combine(_root, "workspace");
        var source = Path.Combine(vault, "normalized", "kenney", "characters");
        Directory.CreateDirectory(Path.Combine(repository, "asset_manifests", "catalogs"));
        Directory.CreateDirectory(Path.Combine(repository, "asset_manifests", "packs"));
        Directory.CreateDirectory(Path.Combine(source, "Model"));
        Directory.CreateDirectory(Path.Combine(source, "Animations"));
        Directory.CreateDirectory(Path.Combine(source, "Skins"));
        Directory.CreateDirectory(workspace);
        File.WriteAllBytes(Path.Combine(source, "Model", "character.fbx"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(source, "Animations", "idle.fbx"), [4, 5, 6]);
        File.WriteAllBytes(Path.Combine(source, "Animations", "run.fbx"), [7, 8, 9]);
        File.WriteAllBytes(Path.Combine(source, "Skins", "hero.png"), [10, 11, 12]);
        File.WriteAllText(Path.Combine(repository, "asset_manifests", "catalogs", "kenney.json"),
            """
            {"packs":[{"pack_id":"kenney:characters","extracted_root":"normalized/kenney/characters","license":"CC0-1.0","license_url":"https://kenney.nl/assets/characters","license_verified":true,"source_page":"https://kenney.nl/assets/characters","files":[{"sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}]}]}
            """);
        var manifestPath = Path.Combine(repository, "asset_manifests", "packs", "character.json");
        File.WriteAllText(manifestPath,
            """
            {"schema_version":1,"pack_id":"test-character","version":"1.0.0","display_name":"Test Adventurer","source_catalog":"asset_manifests/catalogs/kenney.json","source_pack_id":"kenney:characters","license":"CC0-1.0","key":"hero","model_path":"Model/character.fbx","skin_path":"Skins/hero.png","skin_material":"skin","scale_meters":0.5,"tags":["character"],"animations":[{"name":"idle","path":"Animations/idle.fbx"},{"name":"run","path":"Animations/run.fbx"}]}
            """);

        var plan = CuratedAnimatedCharacterInstaller.Plan(repository, vault, workspace, manifestPath);
        using var request = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            workspace,
            plan.RequestRelativePath!.Replace('/', Path.DirectorySeparatorChar))));

        Assert.False(plan.Reused);
        Assert.Equal("CC0-1.0", plan.Item.LicenseSpdx);
        Assert.Equal("character", plan.Item.Intelligence.SemanticType);
        Assert.Equal(64, plan.Manifest.Intelligence!.DependencyClosureSha256.Length);
        Assert.Equal(2, plan.Animations.Count);
        Assert.Equal(2, request.RootElement.GetProperty("options").GetProperty("animationSources").GetArrayLength());
        Assert.Equal("base_color", request.RootElement.GetProperty("options").GetProperty("materialTextures")[0]
            .GetProperty("channel").GetString());
        Assert.EndsWith("/model.glb", plan.OutputRelativePath, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
