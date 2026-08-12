using Forge.Domain.Assets;

namespace Forge.Domain.Tests.Assets;

public sealed class AssetCatalogTests
{
    [Fact]
    public void StableIdentityCatalogAndValidationPreserveGovernanceMetadata()
    {
        var firstId = AssetCatalogItemId.ForExternalId("polyhaven:ArmChair_01");
        var secondId = AssetCatalogItemId.ForExternalId("polyhaven:ArmChair_01");
        var item = CreateItem(firstId);
        var catalog = new AssetCatalog(
            "2026-08-02",
            "cc0-only",
            "E:/OpenRPG_AssetVault",
            [item],
            "fixture");

        Assert.Equal(firstId, secondId);
        Assert.Equal(AssetCatalogItem.CurrentSchemaVersion, item.SchemaVersion);
        Assert.Equal("CC0-1.0", item.LicenseSpdx);
        Assert.Equal(64, item.ContentSha256.Length);
        Assert.True(item.CanPlanImport);
        Assert.Empty(AssetCatalogValidator.Validate(catalog));
    }

    [Fact]
    public void ValidatorLocalizesLicenseHashAndUnsafePathFailures()
    {
        var item = new AssetCatalogItem(
            AssetCatalogItemId.ForExternalId("unsafe:item"),
            "unsafe:item",
            "Unsafe",
            "fixture",
            "item",
            "https://example.invalid/item",
            "UNKNOWN",
            "https://example.invalid/license",
            false,
            "bad-hash",
            1,
            ["model"],
            [],
            "installed",
            AssetAvailability.OriginalAvailable,
            "../outside.glb",
            null,
            null,
            "fixture");
        var catalog = new AssetCatalog("2026-08-02", "review", "vault", [item], "fixture");

        var diagnostics = AssetCatalogValidator.Validate(catalog);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == AssetCatalogValidator.LicenseUnverifiedCode);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == AssetCatalogValidator.HashInvalidCode);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == AssetCatalogValidator.PathUnsafeCode);
        Assert.All(diagnostics, diagnostic => Assert.Contains(item.Id.ToString(), diagnostic.Path));
    }

    [Fact]
    public void TechnicalMetadataValidatesDimensionsMetricsAndFacets()
    {
        var metadata = new AssetTechnicalMetadata(dimensions: new AssetDimensions(1.5, 2, 0.75),
            style: "Stylized", skeleton: "Humanoid", materials: ["Stone", "stone"],
            triangleCount: 12_500, variants: ["Ruined"], animations: ["Idle"],
            compatibility: ["medieval"]);

        Assert.Equal("stylized", metadata.Style);
        Assert.Equal(["stone"], metadata.Materials);
        Assert.Equal(12_500, metadata.TriangleCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssetTechnicalMetadata(
            dimensions: new AssetDimensions(0, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssetTechnicalMetadata(triangleCount: -1));
    }

    internal static AssetCatalogItem CreateItem(AssetCatalogItemId? id = null) => new(
        id ?? AssetCatalogItemId.ForExternalId("ambientcg:3DApple001"),
        "ambientcg:3DApple001",
        "Pomme stylisée",
        "ambientcg",
        "3DApple001",
        "https://ambientcg.com/a/3DApple001",
        "CC0-1.0",
        "https://ambientcg.com/license",
        true,
        new string('a', 64),
        2048,
        ["3d-model", "food"],
        ["pomme", "stylisé", "fruit"],
        "downloaded-and-extracted",
        AssetAvailability.ExtractedAvailable,
        "normalized/ambientcg/3DApple001/apple.obj",
        "normalized/ambientcg/3DApple001/apple.png",
        "https://example.invalid/apple.webp",
        "asset-catalog:ambientcg:3DApple001");
}
