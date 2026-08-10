using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Areas;

public sealed class ModularTilePlacementResolverTests
{
    private static readonly AreaId TestAreaId = AreaId.From(Guid.Parse("a0282000-0000-4000-8000-000000000001"));
    private static readonly EntityId TestEntityId = EntityId.From(Guid.Parse("a0282000-0000-4000-8000-000000000002"));
    private static readonly BlueprintId BaseBlueprintId = BlueprintId.From(Guid.Parse("a0282000-0000-4000-8000-000000000003"));
    private static readonly BlueprintId VariantBlueprintId = BlueprintId.From(Guid.Parse("a0282000-0000-4000-8000-000000000004"));

    [Fact]
    public void Resolves_grid_rotation_mirror_and_semantic_variant_into_runtime_entity()
    {
        var entity = new EntityInstance(TestEntityId, BaseBlueprintId,
            new(new(99, 2.5f, 99), QuaternionValue.Identity, new(2, 3, 4)), ["tile"]);
        var area = Area(entity, includePlacement: true);

        var resolved = ModularTilePlacementResolver.Resolve(area, entity);

        Assert.Equal(new Vector3Value(6, 2.5f, -3), resolved.Entity.Transform.Position);
        Assert.Equal(VariantBlueprintId, resolved.Entity.BlueprintId);
        Assert.Equal(ModularTileMirrorMode.X, resolved.Mirror);
        Assert.Equal("damaged", resolved.SemanticState);
        Assert.Equal(0f, resolved.Entity.Transform.Rotation.X, 5);
        Assert.Equal(MathF.Sin(MathF.PI / 4), resolved.Entity.Transform.Rotation.Y, 5);
        Assert.Equal(MathF.Cos(MathF.PI / 4), resolved.Entity.Transform.Rotation.W, 5);
        Assert.Equal(new Vector3Value(2, 3, 4), resolved.Entity.Transform.Scale);
    }

    [Fact]
    public void Entity_without_tile_placement_is_returned_unchanged()
    {
        var entity = new EntityInstance(TestEntityId, BaseBlueprintId, TransformValue.Identity);
        var area = Area(entity, includePlacement: false);

        var resolved = ModularTilePlacementResolver.Resolve(area, entity);

        Assert.Same(entity, resolved.Entity);
        Assert.Equal(ModularTileMirrorMode.None, resolved.Mirror);
        Assert.Equal(Guid.Empty, resolved.PlacementId);
    }

    private static AreaDefinition Area(EntityInstance entity, bool includePlacement)
    {
        var defaults = AreaFoundationProfile.CreateDefault(TestAreaId);
        var definitionId = Guid.Parse("a0282000-0000-4000-8000-000000000005");
        var definition = new ModularTileDefinition(definitionId, "wall", BaseBlueprintId,
            ModularTileRotationMode.Orthogonal, true, false, [],
            [new(Guid.Parse("a0282000-0000-4000-8000-000000000006"), "damaged", "damaged", VariantBlueprintId)]);
        var placements = includePlacement
            ? new[]
            {
                new ModularTilePlacement(Guid.Parse("a0282000-0000-4000-8000-000000000007"), definitionId,
                    TestEntityId, 2, -1, 90, ModularTileMirrorMode.X, "damaged", null),
            }
            : [];
        var foundation = new AreaFoundationProfile(defaults.Id, 1, "author:r028-test", defaults.Creation,
            new(defaults.TileKit.Id, 1, "author:r028-test", 3, [definition], [], placements, [], []),
            defaults.Lifecycle, [], []);
        return new AreaDefinition(TestAreaId, "test", "Test", [entity], foundation: foundation);
    }
}
