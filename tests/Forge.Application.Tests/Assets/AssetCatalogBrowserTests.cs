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
            ["pomme", "stylisé"]);
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

    private static AssetCatalogItem CreateItem(
        string externalId,
        string name,
        string source,
        IReadOnlyList<string> categories,
        IReadOnlyList<string> tags) => new(
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
        "fixture");
}
