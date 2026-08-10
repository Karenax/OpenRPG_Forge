using Forge.Domain.Content;
using Forge.Domain.Localization;
using Forge.Mcp.Projects;
using Forge.Application.Localization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Tests.Projects;

public sealed class ContentAuthoringAgentServiceTests
{
    [Fact]
    public void LocalizationReadAndPlanAreFilteredValidatedAndDryRunOnly()
    {
        using var workspace = new TestWorkspace();
        var current = workspace.Project.GetLocalization();
        var project = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var extracted = LocalizationCatalogExtractor.Extract(project).Catalog;
        var message = new LocalizationMessage("ui.inventory.count",
            "{count, plural, one {# objet} other {# objets}}", provenance: "mcp:r017");
        var catalog = new LocalizationCatalog(LocalizationCatalogId.From(current.CatalogId), current.DefaultLocale,
            [current.DefaultLocale, "en"], [.. extracted.Messages, message], packageNamespace: "crypt.localization",
            licenseSpdx: "CC0-1.0", provenance: "mcp:r017", platformByteBudget: 4096);

        var planned = workspace.Project.PlanLocalization(new LocalizationPlanRequest(1, current.Revision, catalog));
        var invalid = workspace.Project.PlanLocalization(new LocalizationPlanRequest(1, current.Revision,
            new LocalizationCatalog(catalog.Id, catalog.DefaultLocale, catalog.Locales,
                [new LocalizationMessage("bad", "{count, plural, one {x}}")],
                packageNamespace: "Bad Namespace", licenseSpdx: "UNLICENSED", provenance: "",
                platformByteBudget: 1)));

        Assert.True(planned.Succeeded, string.Join(" | ", planned.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        Assert.True(planned.DryRun);
        Assert.Equal("module/localization", planned.Diff?.Path);
        Assert.Empty(workspace.Project.GetLocalization(key: "ui.inventory.count").Messages);
        Assert.False(invalid.Succeeded);
        Assert.Contains(invalid.Diagnostics, item => item.Code == LocalizationCatalogValidator.GovernanceCode);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    [Fact]
    public void UnifiedBrowserAndAliasPlanExposeFacetsReferencesAndNoMutation()
    {
        var resourceId = Guid.Parse("c0173000-0000-4000-8000-000000000001");
        using var workspace = new TestWorkspace(project =>
        {
            var source = new ContentPackageSource(resourceId, 1, "mcp:r017", "rules/warden",
                "data/warden.json", ContentSourceKind.Json, true, "CC0-1.0", new string('a', 64), 4096);
            var package = new ContentPackageDefinition(Guid.Parse("c0173000-0000-4000-8000-000000000002"), 1,
                "mcp:r017", "crypt.core", "1.0.0", "CC0-1.0", new string('b', 64), 10, [], [], [source]);
            var old = project.Module.Content;
            var content = new ContentWorkspaceConfiguration(old.Id, old.Batch, old.DataTables, old.MergePolicy,
                old.Provenance, old.SchemaVersion, new ContentPackageCatalog(project.Module.Id.Value, 1,
                    "mcp:r017", ContentConflictPolicy.HighestPriority, [package]));
            var module = new Forge.Domain.Modules.ModuleDefinition(project.Module.Id, project.Module.Slug,
                project.Module.DisplayName, project.Module.Description, project.Module.EntryAreaId,
                project.Module.Metadata, project.Module.Variables, project.Module.Distribution,
                project.Module.Localization, content, project.Module.ArtisticStyle, project.Module.RuntimePolicies,
                project.Module.Factions, project.Module.WorldFeatures);
            return new Forge.Domain.Projects.ProjectDefinition(project.Id, project.Title, module, project.Blueprints,
                project.Areas, project.Dialogues, project.Quests);
        });

        var read = workspace.Project.GetContentResources(new UnifiedContentResourceQuery(
            PackageNamespace: "crypt.core", Kind: "text"));
        var planned = workspace.Project.PlanContentResourceAliases(new ContentResourceAliasPlanRequest(
            1, read.Revision, resourceId, ["legacy.core:text/warden"], []));
        var invalid = workspace.Project.PlanContentResourceAliases(new ContentResourceAliasPlanRequest(
            1, read.Revision, resourceId, ["bad alias"], ["missing.core:model/mesh"]));

        Assert.Equal("crypt.core:text/rules/warden", Assert.Single(read.Items).Address);
        Assert.Contains("Text", read.Kinds);
        Assert.True(planned.Succeeded);
        Assert.True(planned.DryRun);
        Assert.Equal("module/content", planned.Diff?.Path);
        Assert.False(invalid.Succeeded);
        Assert.Contains(invalid.Diagnostics, item => item.Code == ContentPackageCatalogValidator.ResourceAddressCode);
        Assert.DoesNotContain("legacy.core:text/warden",
            workspace.Project.GetContentResources(new UnifiedContentResourceQuery(PackageNamespace: "crypt.core"))
                .Items.Single().Aliases);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }
}
