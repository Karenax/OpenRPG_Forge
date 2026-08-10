using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class BlueprintAuthoringPersistenceTests
{
    [Fact]
    public void AggregateRoundTripPreservesAuthoringContractAndInstanceRequirement()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var authoring = new BlueprintAuthoringMetadata(
            1,
            "3.2.1",
            "project.warden",
            $"content/blueprints/{original.Id}/warden.blueprint.json",
            ["level"],
            ["pack://forge-core/1.0.0/combat"],
            $"content/blueprints/{original.Id}/warden.preview.png",
            262_144,
            "crypt-stone",
            ["gothic", "ruined"],
            ["medieval", "humanoid-rig"],
            ["dungeon", "boss"]);
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance,
            original.CreaturePresentation, original.CreatureProgression, original.CreatureLoot,
            original.CreatureRoleBehavior, original.CreatureEvents, original.ItemAdvanced, authoring);
        var area = source.Areas.Single();
        var entity = area.Entities.Single();
        var bound = new EntityInstance(entity.Id, entity.BlueprintId, entity.Transform, entity.Tags, entity.Overrides,
            "^3.0.0");
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [blueprint],
            [area.WithEntities([bound])], source.Dialogues, source.Quests);

        var restored = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(project));

        var restoredAuthoring = restored.Blueprints.Single().Authoring;
        Assert.Equal(authoring.SchemaVersion, restoredAuthoring.SchemaVersion);
        Assert.Equal(authoring.Version, restoredAuthoring.Version);
        Assert.Equal(authoring.PackageNamespace, restoredAuthoring.PackageNamespace);
        Assert.Equal(authoring.ManifestPath, restoredAuthoring.ManifestPath);
        Assert.Equal(authoring.ExposedParameters, restoredAuthoring.ExposedParameters);
        Assert.Equal(authoring.Dependencies, restoredAuthoring.Dependencies);
        Assert.Equal(authoring.PreviewPath, restoredAuthoring.PreviewPath);
        Assert.Equal(authoring.PlatformByteBudget, restoredAuthoring.PlatformByteBudget);
        Assert.Equal(authoring.Kit, restoredAuthoring.Kit);
        Assert.Equal(authoring.Styles, restoredAuthoring.Styles);
        Assert.Equal(authoring.Compatibility, restoredAuthoring.Compatibility);
        Assert.Equal(authoring.Tags, restoredAuthoring.Tags);
        Assert.Equal("^3.0.0", restored.Areas.Single().Entities.Single().BlueprintVersionRequirement);
    }
}
