using Forge.Domain.Content;
using Forge.Application.Content;
using Forge.Mcp.Projects;

namespace Forge.Mcp.Tests.Projects;

public sealed class ContentPackageAgentServiceTests
{
    [Fact]
    public void Supports_filtered_read_and_validated_dry_run_without_mutation()
    {
        using var workspace = new TestWorkspace();
        var current = workspace.Project.GetContentPackages();
        Assert.Empty(current.Packages);
        var package = Package("crypt.encounters", "creature.warden");
        var catalog = new ContentPackageCatalog(current.CatalogId, 1, "mcp:r016",
            ContentConflictPolicy.HighestPriority, [package]);

        var planned = workspace.Project.PlanContentPackages(new ContentPackagePlanRequest(
            1, current.Revision, catalog));

        Assert.True(planned.Succeeded);
        Assert.True(planned.DryRun);
        Assert.Equal("module/content", planned.Diff?.Path);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
        Assert.Empty(workspace.Project.GetContentPackages().Packages);

        using var configured = new TestWorkspace(project =>
        {
            var old = project.Module.Content;
            var content = new ContentWorkspaceConfiguration(old.Id, old.Batch, old.DataTables, old.MergePolicy,
                old.Provenance, old.SchemaVersion, catalog);
            var module = new Forge.Domain.Modules.ModuleDefinition(project.Module.Id, project.Module.Slug,
                project.Module.DisplayName, project.Module.Description, project.Module.EntryAreaId,
                project.Module.Metadata, project.Module.Variables, project.Module.Distribution,
                project.Module.Localization, content, project.Module.ArtisticStyle, project.Module.RuntimePolicies,
                project.Module.Factions, project.Module.WorldFeatures);
            return new Forge.Domain.Projects.ProjectDefinition(project.Id, project.Title, module, project.Blueprints,
                project.Areas, project.Dialogues, project.Quests);
        });
        Assert.Equal(package.Id.ToString("D"),
            Assert.Single(configured.Project.GetContentPackages(package.Id.ToString()).Packages).Id.ToString("D"));
        Assert.Equal("crypt.encounters",
            Assert.Single(configured.Project.GetContentPackages(search: "warden").Packages).PackageNamespace);
    }

    [Fact]
    public void Dry_run_reports_invalid_license_hash_and_stale_revision()
    {
        using var workspace = new TestWorkspace();
        var current = workspace.Project.GetContentPackages();
        var source = new ContentPackageSource(Guid.NewGuid(), 1, "mcp:r016", "bad", "data/resource.json",
            ContentSourceKind.Json, true, "CC0-1.0", new string('a', 64), 4096);
        var invalidPackage = new ContentPackageDefinition(Guid.NewGuid(), 1, "mcp:r016", "crypt.invalid",
            "1.0.0", "UNLICENSED", "bad", 10, [], [], [source]);
        var catalog = new ContentPackageCatalog(current.CatalogId, 1, "mcp:r016",
            ContentConflictPolicy.HighestPriority, [invalidPackage]);

        var invalid = workspace.Project.PlanContentPackages(new ContentPackagePlanRequest(1, current.Revision, catalog));
        var stale = workspace.Project.PlanContentPackages(new ContentPackagePlanRequest(1, 99,
            new ContentPackageCatalog(current.CatalogId, 1, "mcp:r016", ContentConflictPolicy.HighestPriority, [])));

        Assert.False(invalid.Succeeded);
        Assert.Contains(invalid.Diagnostics, item => item.Code == ContentPackageCatalogValidator.InvalidLicenseCode);
        Assert.Contains(invalid.Diagnostics, item => item.Code == ContentPackageCatalogValidator.InvalidHashCode);
        Assert.False(stale.Succeeded);
        Assert.Contains(stale.Diagnostics, item => item.Code == SetContentWorkspaceHandler.RevisionConflictCode);
        Assert.Equal(0, workspace.Project.GetCurrentRevision());
    }

    private static ContentPackageDefinition Package(string ns, string resource)
    {
        var source = new ContentPackageSource(Guid.NewGuid(), 1, "mcp:r016", resource, "data/resource.json",
            ContentSourceKind.Json, true, "CC0-1.0", new string('a', 64), 4096);
        return new ContentPackageDefinition(Guid.NewGuid(), 1, "mcp:r016", ns, "1.0.0", "CC0-1.0",
            new string('b', 64), 10, [], [], [source]);
    }
}
