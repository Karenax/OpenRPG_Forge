using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Assets;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class BlueprintAuthoringPersistenceTests
{
    [Fact]
    public void AggregateRoundTripPreservesAuthoringContractAndInstanceRequirement()
    {
        var source = TestProjectFactory.CreateValid();
        var original = source.Blueprints.Single();
        var baseBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("b0580000-0000-4000-8000-000000000200")),
            $"{original.Slug}-base",
            $"{original.DisplayName} Base",
            original.Kind,
            original.Combat,
            original.PaletteOrigin,
            original.IsFavorite,
            original.Provenance,
            original.CreaturePresentation,
            original.CreatureProgression,
            original.CreatureLoot,
            original.CreatureRoleBehavior,
            original.CreatureEvents,
            original.ItemAdvanced,
            original.Authoring);
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
            ["dungeon", "boss"],
            ["Monde/Crypte/Boss"],
            [new EntityPropertyOverride("visual.scale", EntityPropertyValueKind.Number, "2")],
            ["Crypt/Bosses"],
            new PrefabVariantDefinition(Guid.Parse("b0580000-0000-4000-8000-000000000201"), 1,
                "test:r058", baseBlueprint.Id,
                [new(PrefabVariantOverrideKind.Material, "body", "asset:ember")]),
            new PrefabCompositionDefinition(Guid.Parse("b0580000-0000-4000-8000-000000000202"), 1,
                "test:r058", [new(Guid.Parse("b0580000-0000-4000-8000-000000000203"), baseBlueprint.Id, null,
                    "floor", new TransformValue(new Vector3Value(2, 3, 4),
                        new QuaternionValue(0, 0.70710677f, 0, 0.70710677f), new Vector3Value(1, 2, 1)),
                    ["house-piece", "nonblocking"], new EntityPropertyOverrides(1,
                        [new EntityPropertyOverride("visual.asset", EntityPropertyValueKind.String,
                            "asset:house-wall")]))], ["socket-compatible"], ["intensity"],
                ["scripts/assemble.as"]),
            new BlueprintAssetBudgetProfile(Guid.Parse("b0580000-0000-4000-8000-000000000204"), 1,
                "test:r058", new AssetBudgetMetrics(12_000, 2, 3, 8_000_000, 1, 3, 2),
                [new AssetPlatformBudget(AssetTargetPlatform.Windows, 20_000, 4, 6, 16_000_000, 2, 4, 3)]));
        var blueprint = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, original.Kind,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance,
            original.CreaturePresentation, original.CreatureProgression, original.CreatureLoot,
            original.CreatureRoleBehavior, original.CreatureEvents, original.ItemAdvanced, authoring);
        var area = source.Areas.Single();
        var entity = area.Entities.Single();
        var bound = new EntityInstance(entity.Id, entity.BlueprintId, entity.Transform, entity.Tags, entity.Overrides,
            "^3.0.0");
        var project = new ProjectDefinition(source.Id, source.Title, source.Module, [baseBlueprint, blueprint],
            [area.WithEntities([bound])], source.Dialogues, source.Quests);

        var restored = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(project));

        var restoredAuthoring = restored.Blueprints.Single(item => item.Id == blueprint.Id).Authoring;
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
        Assert.Equal(authoring.CategoryPaths, restoredAuthoring.CategoryPaths);
        Assert.Equal(authoring.PromotedDefaults, restoredAuthoring.PromotedDefaults);
        Assert.Equal(authoring.AuthorCollections, restoredAuthoring.AuthorCollections);
        Assert.Equal(authoring.PrefabVariant!.Overrides, restoredAuthoring.PrefabVariant!.Overrides);
        Assert.Equal(authoring.PrefabComposition!.Nodes, restoredAuthoring.PrefabComposition!.Nodes);
        Assert.Equal(authoring.AssetBudget!.Metrics, restoredAuthoring.AssetBudget!.Metrics);
        Assert.Equal(authoring.AssetBudget.Platforms, restoredAuthoring.AssetBudget.Platforms);
        Assert.Equal(authoring, restoredAuthoring);
        Assert.Equal("^3.0.0", restored.Areas.Single().Entities.Single().BlueprintVersionRequirement);
    }
}
