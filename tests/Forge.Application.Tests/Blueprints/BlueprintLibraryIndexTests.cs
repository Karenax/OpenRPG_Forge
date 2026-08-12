using Forge.Application.Blueprints;
using Forge.Application.Tests.Entities;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Blueprints;

public sealed class BlueprintLibraryIndexTests
{
    [Fact]
    public void Builds_hierarchical_categories_variant_families_collections_and_impact()
    {
        var project = new PlaceEntityTestContext().State.Project;
        var source = project.Blueprints[0];
        var variant = new BlueprintDefinition(BlueprintId.From(Guid.NewGuid()), source.Slug + "--ruined", "Ruined",
            source.Kind, paletteOrigin: BlueprintPaletteOrigin.ProjectContent, isFavorite: true, provenance: source.Provenance);
        var area = project.Areas[0];
        var instance = new EntityInstance(EntityId.From(Guid.NewGuid()), variant.Id, TransformValue.Identity);
        project = new ProjectDefinition(project.Id, project.Title, project.Module, [.. project.Blueprints, variant],
            [area.WithEntities([.. area.Entities, instance])], project.Dialogues, project.Quests);

        var entry = BlueprintLibraryIndex.Build(project).Entries.Single(item => item.BlueprintId == variant.Id);

        Assert.Contains("ProjectContent/", entry.CategoryPath);
        Assert.Equal(source.Slug, entry.VariantFamily);
        Assert.Contains("Favoris", entry.Collections);
        Assert.Contains($"Variantes/{source.Slug}", entry.Collections);
        Assert.Equal([instance.Id], entry.InstanceIds);
    }

    [Fact]
    public void ExpandsPersistentAuthorCategoriesAndTagFacetsIntoVirtualFolders()
    {
        var context = new PlaceEntityTestContext();
        var source = context.State.Project.Blueprints[0];
        var authoring = new BlueprintAuthoringMetadata(1, "1.0.0", "project.forest",
            "content/blueprints/tree.json", styles: ["stylized"], compatibility: ["medieval"],
            tags: ["tree"], categoryPaths: ["Monde/Forêt/Arbres"]);
        var blueprint = source.WithPaletteUpdate(authoring, source.Provenance);
        var project = new ProjectDefinition(context.State.Project.Id, context.State.Project.Title,
            context.State.Project.Module, [blueprint], context.State.Project.Areas);

        var index = BlueprintLibraryIndex.Build(project);

        Assert.Contains("Monde", index.CategoryPaths);
        Assert.Contains("Monde/Forêt", index.CategoryPaths);
        Assert.Contains("Monde/Forêt/Arbres", index.CategoryPaths);
        Assert.Contains("Tags/tree", index.Collections);
        Assert.Contains("Styles/stylized", index.Collections);
        Assert.Contains("Compatibilité/medieval", index.Collections);
    }
}
