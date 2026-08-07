using System.Text.Json;
using Forge.ModuleFormat.Assets;

namespace Forge.ModuleFormat.Tests.Assets;

public sealed class CuratedCatalogModelPackInstallerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"forge-curated-catalog-model-{Guid.NewGuid():N}");

    [Fact]
    public void PlansVerifiedCatalogModelWithTypedMeshSelectionAndDecimation()
    {
        var repository = Path.Combine(_root, "repository");
        var vault = Path.Combine(_root, "vault");
        var workspace = Path.Combine(_root, "workspace");
        var catalogs = Path.Combine(repository, "asset_manifests", "catalogs");
        var packs = Path.Combine(repository, "asset_manifests", "packs");
        var source = Path.Combine(vault, "originals", "polyhaven", "fir", "gltf-1k");
        Directory.CreateDirectory(catalogs);
        Directory.CreateDirectory(packs);
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(source, "fir_1k.gltf"), "{}");
        File.WriteAllText(Path.Combine(catalogs, "cc0_3d_all.json"),
            """
            {"schema_version":1,"catalog_date":"2026-08-06","license_policy":"cc0-only","assets":[{"asset_id":"polyhaven:fir","schema_version":1,"name":"Fir","source":"polyhaven","source_asset_id":"fir","source_page":"https://polyhaven.com/a/fir","license":"CC0-1.0","license_url":"https://polyhaven.com/license","license_verified":true,"status":"downloaded-original","total_bytes":2,"categories":["3d-model"],"tags":["tree"],"files":[{"path":"originals/polyhaven/fir/gltf-1k/fir_1k.gltf","sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}]}]}
            """);
        File.WriteAllText(Path.Combine(catalogs, "kenney_3d_packs.json"),
            """
            {"schema_version":1,"catalog_date":"2026-08-06","license_policy":"cc0-only","packs":[]}
            """);
        var manifestPath = Path.Combine(packs, "forest.json");
        File.WriteAllText(manifestPath,
            """
            {"schema_version":1,"pack_id":"forest-polyhaven","version":"1.0.0","display_name":"Forest Poly Haven","license":"CC0-1.0","models":[{"external_id":"polyhaven:fir","source_mesh_names":["fir_b"],"base_decimation_ratio":0.125,"maximum_output_bytes":16777216,"tags":["forest","tree"]}]}
            """);

        var plan = CuratedCatalogModelPackInstaller.Plan(
            repository,
            vault,
            workspace,
            "polyhaven:fir",
            manifestPath);
        using var request = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            workspace,
            plan.RequestRelativePath!.Replace('/', Path.DirectorySeparatorChar))));

        Assert.False(plan.Reused);
        Assert.Equal("CC0-1.0", plan.Item.LicenseSpdx);
        Assert.Equal("curated-pack:forest-polyhaven@1.0.0;source:polyhaven:fir", plan.Item.Provenance);
        Assert.Equal("fir_b", request.RootElement.GetProperty("options").GetProperty("includedMeshNames")[0].GetString());
        Assert.Equal(0.125m,
            request.RootElement.GetProperty("options").GetProperty("baseDecimationRatio").GetDecimal());
        Assert.EndsWith("/model.glb", plan.OutputRelativePath, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
