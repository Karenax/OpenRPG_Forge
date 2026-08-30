using System.Text.Json;
using Forge.ModuleFormat.Assets;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class CuratedEmbeddedAnimatedCharacterInstallerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"forge-embedded-character-{Guid.NewGuid():N}");

    [Fact]
    public void PlansConfinedRuntimeReadyGlbAndRequiredSemanticClips()
    {
        var repository = Path.Combine(_root, "repository");
        var vault = Path.Combine(_root, "vault");
        var workspace = Path.Combine(_root, "workspace");
        var extracted = Path.Combine(vault, "normalized", "kaykit", "adventurers");
        Directory.CreateDirectory(Path.Combine(repository, "asset_manifests", "catalogs"));
        Directory.CreateDirectory(Path.Combine(repository, "asset_manifests", "packs"));
        Directory.CreateDirectory(extracted);
        Directory.CreateDirectory(workspace);
        File.WriteAllBytes(Path.Combine(extracted, "Knight.glb"), [0x67, 0x6c, 0x54, 0x46]);
        File.WriteAllText(Path.Combine(repository, "asset_manifests", "catalogs", "kaykit.json"),
            """
            {"packs":[{"pack_id":"kaykit:adventurers","name":"KayKit Adventurers","source":"kaykit","extracted_root":"normalized/kaykit/adventurers","license":"CC0-1.0","license_url":"https://example.test/license","license_verified":true,"source_page":"https://example.test/source","files":[{"sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}]}]}
            """);
        var manifestPath = Path.Combine(repository, "asset_manifests", "packs", "knight.json");
        File.WriteAllText(manifestPath,
            """
            {"schema_version":1,"pack_id":"forest-knight","version":"1.0.0","display_name":"Forest Knight","source_catalog":"asset_manifests/catalogs/kaykit.json","source_pack_id":"kaykit:adventurers","license":"CC0-1.0","key":"knight","model_path":"Knight.glb","scale_meters":1.0,"rig":"kaykit-rig-medium-41","expected_bone_count":41,"animation_profile":"kaykit-adventurer","clips":{"idle":"Idle","run":"Running_A","jump":"Jump_Full_Short","attack":"Attack","hit":"Hit_A","death":"Death_A"},"tags":["character","fantasy"]}
            """);

        var plan = CuratedEmbeddedAnimatedCharacterInstaller.Plan(
            repository,
            vault,
            workspace,
            manifestPath);
        using var request = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            workspace,
            plan.RequestRelativePath!.Replace('/', Path.DirectorySeparatorChar))));

        Assert.False(plan.Reused);
        Assert.Equal("character", plan.Item.Intelligence.SemanticType);
        Assert.Equal(64, plan.Manifest.Intelligence!.ModelContentSha256.Length);
        Assert.Equal("kaykit-rig-medium-41", plan.Rig);
        Assert.Equal(41, plan.ExpectedBoneCount);
        Assert.Equal("kaykit-adventurer", plan.AnimationProfile);
        Assert.Equal(6, plan.RequiredClips.Count);
        Assert.Equal("Attack", plan.Clips.Attack);
        Assert.Empty(request.RootElement.GetProperty("options").GetProperty("animationSources").EnumerateArray());
        Assert.Empty(request.RootElement.GetProperty("options").GetProperty("materialTextures").EnumerateArray());
        Assert.Equal("normalized/kaykit/adventurers/Knight.glb",
            request.RootElement.GetProperty("sourceRelativePath").GetString());
    }

    [Fact]
    public void RejectsModelTraversalOutsideGovernedPack()
    {
        var repository = Path.Combine(_root, "repository-traversal");
        var vault = Path.Combine(_root, "vault-traversal");
        var workspace = Path.Combine(_root, "workspace-traversal");
        Directory.CreateDirectory(Path.Combine(repository, "asset_manifests", "catalogs"));
        Directory.CreateDirectory(Path.Combine(repository, "asset_manifests", "packs"));
        Directory.CreateDirectory(Path.Combine(vault, "normalized", "kaykit", "adventurers"));
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(repository, "asset_manifests", "catalogs", "kaykit.json"),
            """
            {"packs":[{"pack_id":"kaykit:adventurers","name":"KayKit Adventurers","source":"kaykit","extracted_root":"normalized/kaykit/adventurers","license":"CC0-1.0","license_url":"https://example.test/license","license_verified":true,"source_page":"https://example.test/source","files":[{"sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}]}]}
            """);
        var manifestPath = Path.Combine(repository, "asset_manifests", "packs", "knight.json");
        File.WriteAllText(manifestPath,
            """
            {"schema_version":1,"pack_id":"forest-knight","version":"1.0.0","display_name":"Forest Knight","source_catalog":"asset_manifests/catalogs/kaykit.json","source_pack_id":"kaykit:adventurers","license":"CC0-1.0","key":"knight","model_path":"../Knight.glb","scale_meters":1.0,"rig":"kaykit-rig-medium-41","expected_bone_count":41,"animation_profile":"kaykit-adventurer","clips":{"idle":"Idle","run":"Running_A","jump":"Jump_Full_Short","attack":"Attack","hit":"Hit_A","death":"Death_A"},"tags":["character"]}
            """);

        var error = Assert.Throws<InvalidDataException>(() =>
            CuratedEmbeddedAnimatedCharacterInstaller.Plan(repository, vault, workspace, manifestPath));

        Assert.Contains("Unsafe curated asset path", error.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
