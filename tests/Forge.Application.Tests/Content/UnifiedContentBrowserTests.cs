using Forge.Application.Assets;
using Forge.Application.Content;
using Forge.Domain.Areas;
using Forge.Domain.Assets;
using Forge.Domain.Content;
using Forge.Domain.Modules;
using Forge.Domain.Projects;
using Forge.Domain.Localization;

namespace Forge.Application.Tests.Content;

public sealed class UnifiedContentBrowserTests
{
    [Fact]
    public void BrowserUnifiesPackagesLocalizationAndVaultWithFacetsReferencesAndActions()
    {
        var project = CreateProject();
        var moduleId = project.Module.Id.Value;
        var source = new ContentPackageSource(Guid.Parse("c0171000-0000-4000-8000-000000000001"), 1,
            "test:r017", "rules/encounter", "data/encounter.json", ContentSourceKind.Json, true,
            "CC0-1.0", new string('a', 64), 2048, ["legacy.core:text/encounter"], []);
        var package = new ContentPackageDefinition(Guid.Parse("c0171000-0000-4000-8000-000000000002"), 1,
            "test:r017", "crypt.core", "1.0.0", "CC0-1.0", new string('b', 64), 10, [], [], [source]);
        var content = new ContentWorkspaceConfiguration(project.Module.Content.Id, project.Module.Content.Batch,
            project.Module.Content.DataTables, project.Module.Content.MergePolicy, project.Module.Content.Provenance,
            packages: new ContentPackageCatalog(moduleId, 1, "test:r017", ContentConflictPolicy.HighestPriority, [package]));
        var oldModule = project.Module;
        var module = new ModuleDefinition(oldModule.Id, oldModule.Slug, oldModule.DisplayName, oldModule.Description,
            oldModule.EntryAreaId, oldModule.Metadata, oldModule.Variables, oldModule.Distribution,
            oldModule.Localization, content, oldModule.ArtisticStyle, oldModule.RuntimePolicies, oldModule.Factions,
            oldModule.WorldFeatures);
        var configured = new ProjectDefinition(project.Id, project.Title, module, project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        var asset = CreateAsset();
        var browser = new UnifiedContentBrowser(configured,
            new AssetCatalog("2026-08-08", "cc0-only", "vault", [asset], "test:r017"));

        Assert.Contains(browser.Items, item => item.Address == "crypt.core:text/rules/encounter");
        Assert.Contains(browser.Items, item => item.Kind == ContentResourceKind.Localization);
        var model = Assert.Single(browser.Search(new UnifiedContentBrowserQuery(Text: "oak", Kind: ContentResourceKind.Model)));
        Assert.Equal(asset.Id, model.AssetCatalogId);
        Assert.Contains("preview", model.Actions);
        Assert.Contains("plan-import", model.Actions);
        Assert.Contains("crypt.core", browser.PackageNamespaces);

        var script = new ContentResourceScriptService(browser);
        var handle = Assert.IsType<Forge.Scripting.ForgeScriptOpaqueHandle>(script.Find("legacy.core:text/encounter"));
        Assert.Equal("crypt.core:text/rules/encounter", script.Read(handle).Address);
    }

    private static AssetCatalogItem CreateAsset() => new(
        AssetCatalogItemId.ForExternalId("kenney:oak"), "kenney:oak", "Oak", "kenney", "oak",
        "https://example.invalid/oak", "CC0-1.0", "https://example.invalid/license", true,
        new string('c', 64), 4096, ["3d-model"], ["tree"], "installed",
        AssetAvailability.ExtractedAvailable, "normalized/kenney/oak.glb", "normalized/kenney/oak.png", null,
        "test:r017", installedPackIds: ["forest-core"]);

    private static ProjectDefinition CreateProject()
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("c0171000-0000-4000-8000-000000000090")),
            "start", "Start", []);
        var moduleId = ModuleId.From(Guid.Parse("c0171000-0000-4000-8000-000000000091"));
        var localization = new LocalizationCatalog(LocalizationCatalogId.From(moduleId.Value), "fr", ["fr"],
            [new LocalizationMessage("module.name", "Crypte")], packageNamespace: "crypt.localization",
            provenance: "test:r017");
        var module = new ModuleDefinition(moduleId, "crypt", "Crypt", "Test", area.Id,
            localization: localization);
        return new ProjectDefinition(ProjectId.From(Guid.Parse("c0171000-0000-4000-8000-000000000092")),
            "Crypt", module, [], [area]);
    }
}
