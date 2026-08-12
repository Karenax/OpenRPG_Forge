using Forge.Application.Assets;
using Forge.Domain.Assets;

namespace Forge.Application.Tests.Assets;

public sealed class AssetCatalogBrowserTests
{
    [Fact]
    public void SearchSupportsAccentInsensitiveTextAndFacets()
    {
        var apple = CreateItem(
            "ambientcg:apple",
            "Pomme stylisée",
            "ambientcg",
            ["food"],
            ["pomme", "stylisé"])
            .WithInstalledPacks(["food-core"]);
        var chair = CreateItem(
            "polyhaven:chair",
            "Fauteuil gothique",
            "polyhaven",
            ["furniture"],
            ["chair", "wood"]);
        var browser = new AssetCatalogBrowser(new AssetCatalog(
            "2026-08-02", "cc0-only", "vault", [chair, apple], "fixture"));

        Assert.Equal(apple.Id, Assert.Single(browser.Search(new AssetCatalogQuery(Text: "stylise"))).Id);
        Assert.Equal(chair.Id, Assert.Single(browser.Search(new AssetCatalogQuery(
            Source: "polyhaven",
            Category: "furniture"))).Id);
        Assert.Empty(browser.Search(new AssetCatalogQuery(Source: "kenney")));
        Assert.Equal(apple.Id, Assert.Single(browser.Search(new AssetCatalogQuery(Installed: true))).Id);
        Assert.Equal(chair.Id, Assert.Single(browser.Search(new AssetCatalogQuery(Installed: false))).Id);
        Assert.Equal(["ambientcg", "polyhaven"], browser.Sources);
        Assert.Contains("food", browser.Categories);
    }

    [Fact]
    public void ImportPlannerProducesDryRunOrExplicitAvailabilityDiagnostic()
    {
        var installed = CreateItem("ambientcg:apple", "Apple", "ambientcg", ["food"], ["fruit"]);
        var missing = new AssetCatalogItem(
            AssetCatalogItemId.ForExternalId("polyhaven:missing"),
            "polyhaven:missing",
            "Missing",
            "polyhaven",
            "missing",
            "https://example.invalid/missing",
            "CC0-1.0",
            "https://example.invalid/license",
            true,
            new string('b', 64),
            0,
            ["model"],
            [],
            "catalogued",
            AssetAvailability.CatalogOnly,
            "originals/missing.glb",
            null,
            null,
            "fixture");

        var plan = AssetImportPlanner.Plan(installed);
        var blocked = AssetImportPlanner.Plan(missing);

        Assert.True(plan.Succeeded);
        Assert.True(plan.Plan!.DryRun);
        Assert.StartsWith("assets/imported/", plan.Plan.DestinationRelativeDirectory);
        Assert.False(blocked.Succeeded);
        Assert.Equal(AssetImportPlanner.SourceUnavailableCode, Assert.Single(blocked.Diagnostics).Code);
    }

    [Fact]
    public void ImportPlannerProducesDeterministicCanonicalModelTasksAndCacheKey()
    {
        var item = CreateItem("polyhaven:watchtower", "Watchtower", "polyhaven", ["building"], ["stone"]);

        var first = Assert.IsType<AssetImportPlan>(AssetImportPlanner.Plan(item).Plan);
        var second = Assert.IsType<AssetImportPlan>(AssetImportPlanner.Plan(item).Plan);

        Assert.Equal(AssetProcessingKind.Model, first.Profile.Kind);
        Assert.Equal("glb", first.Profile.CanonicalFormat);
        Assert.Equal(first.CacheKey, second.CacheKey);
        Assert.Equal(64, first.CacheKey.Length);
        Assert.Equal(
            ["model.normalize", "model.animations", "model.collisions", "model.lod.0.5", "model.lod.0.25"],
            first.Tasks.Select(task => task.Name));
        Assert.All(first.Tasks.Skip(1), task => Assert.Contains("model.normalize", task.Dependencies));
    }

    [Fact]
    public void ImportPlannerValidatesTexturePbrProfileBeforeBuild()
    {
        var item = CreateItem("ambientcg:brick", "Brick", "ambientcg", ["texture"], ["pbr"]);
        var profile = new AssetProcessingProfile(
            item.Id,
            AssetProcessingProfile.CurrentSchemaVersion,
            AssetProcessingKind.Texture,
            "png",
            "ktx2",
            item.LicenseSpdx,
            item.ContentSha256,
            item.Provenance,
            8 * 1024 * 1024,
            texture: new(
                TextureColorSpace.Srgb,
                TextureCompressionProfile.Desktop,
                4096,
                10.24m,
                new Dictionary<PbrTextureChannel, string>
                {
                    [PbrTextureChannel.BaseColor] = "textures/brick-base.png",
                    [PbrTextureChannel.Normal] = "textures/brick-normal.png",
                }));

        var result = AssetImportPlanner.Plan(item, profile);

        var plan = Assert.IsType<AssetImportPlan>(result.Plan);
        Assert.Equal("texture.process", Assert.Single(plan.Tasks).Name);
        Assert.EndsWith("texture.ktx2", plan.Tasks[0].OutputRelativePath);

        var invalid = new AssetProcessingProfile(
            item.Id,
            99,
            AssetProcessingKind.Texture,
            "dds",
            "png",
            string.Empty,
            "invalid",
            string.Empty,
            0,
            texture: new(TextureColorSpace.Srgb, TextureCompressionProfile.Desktop, 0, 0, new Dictionary<PbrTextureChannel, string>()));
        var blocked = AssetImportPlanner.Plan(item, invalid);
        Assert.False(blocked.Succeeded);
        Assert.Contains(blocked.Diagnostics, diagnostic => diagnostic.Code == AssetProcessingProfileValidator.SchemaUnsupportedCode);
        Assert.Contains(blocked.Diagnostics, diagnostic => diagnostic.Code == AssetProcessingProfileValidator.SourceFormatUnsupportedCode);
        Assert.Contains(blocked.Diagnostics, diagnostic => diagnostic.Code == AssetProcessingProfileValidator.GovernanceMissingCode);
        Assert.Contains(blocked.Diagnostics, diagnostic => diagnostic.Code == AssetProcessingProfileValidator.BudgetInvalidCode);

        var mismatched = new AssetProcessingProfile(
            AssetCatalogItemId.ForExternalId("ambientcg:other"),
            profile.SchemaVersion,
            profile.Kind,
            profile.SourceFormat,
            profile.CanonicalFormat,
            profile.LicenseSpdx,
            profile.ContentSha256,
            profile.Provenance,
            profile.MaximumOutputBytes,
            texture: profile.Texture);
        Assert.Contains(
            AssetImportPlanner.Plan(item, mismatched).Diagnostics,
            diagnostic => diagnostic.Code == AssetImportPlanner.ProfileMismatchCode);
    }

    [Fact]
    public void AdvancedSearchAndPreviewPlanUseCanonicalTechnicalMetadata()
    {
        var metadata = new AssetTechnicalMetadata(dimensions: new AssetDimensions(1, 2, 0.5),
            style: "stylized", skeleton: "humanoid", materials: ["stone"], triangleCount: 4_200,
            variants: ["ruined"], animations: ["idle"], compatibility: ["medieval"]);
        var item = CreateItem("fixture:guardian", "Gardien", "fixture", ["creature"], ["guardian"], metadata);
        var browser = new AssetCatalogBrowser(new AssetCatalog("2026-08-10", "cc0-only", "vault", [item], "fixture"));

        var result = browser.Search(new AssetCatalogQuery(Tag: "guardian", MaximumWidth: 1.5,
            MaximumHeight: 2.5, MaximumDepth: 1, Style: "stylized", Skeleton: "humanoid",
            Material: "stone", MaximumTriangleCount: 5_000, Author: "fixture",
            Compatibility: "medieval", Variant: "ruined"));
        var plan = AssetPreviewPlanBuilder.Build(Assert.Single(result));

        Assert.Equal(Enumerable.Range(0, 8).Select(index => index * 45), plan.TurntableAngles);
        Assert.Contains("ruined", plan.Variants);
        Assert.Contains("idle", plan.Animations);
        Assert.Contains(plan.Metrics, metric => metric.Label == "Triangles" && metric.Value == "4200");
        Assert.Empty(browser.Search(new AssetCatalogQuery(MaximumTriangleCount: 100)));
    }

    private static AssetCatalogItem CreateItem(
        string externalId,
        string name,
        string source,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> tags,
        AssetTechnicalMetadata? technicalMetadata = null) => new(
        AssetCatalogItemId.ForExternalId(externalId),
        externalId,
        name,
        source,
        externalId.Split(':').Last(),
        $"https://example.invalid/{externalId}",
        "CC0-1.0",
        "https://example.invalid/license",
        true,
        new string('a', 64),
        2048,
        categories,
        tags,
        "installed",
        AssetAvailability.ExtractedAvailable,
        $"normalized/{externalId.Replace(':', '/')}/model.glb",
        $"normalized/{externalId.Replace(':', '/')}/preview.png",
        null,
        "fixture",
        technicalMetadata: technicalMetadata);
}
