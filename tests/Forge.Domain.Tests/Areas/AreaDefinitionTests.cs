using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Domain.Tests.Areas;

public sealed class AreaDefinitionTests
{
    [Fact]
    public void Constructor_RejectsDuplicateEntityIds()
    {
        var entityId = EntityId.From(Guid.Parse("4a7753a5-f16d-4e45-9320-d657e64f3d56"));
        var blueprintId = BlueprintId.From(Guid.Parse("373fbd0f-dd40-4054-9d8f-e9f7bb7a36bc"));
        var entity = new EntityInstance(entityId, blueprintId, TransformValue.Identity);

        Assert.Throws<ArgumentException>(() => new AreaDefinition(
            AreaId.From(Guid.Parse("93b6f81b-49eb-4e3d-a156-08ad78529525")),
            "crypt",
            "Crypt",
            [entity, entity]));
    }

    [Fact]
    public void Constructor_PreservesIndependentAreaMetadataAndPortals()
    {
        var source = new EntityInstance(
            EntityId.From(Guid.Parse("4a7753a5-f16d-4e45-9320-d657e64f3d56")),
            BlueprintId.From(Guid.Parse("373fbd0f-dd40-4054-9d8f-e9f7bb7a36bc")),
            TransformValue.Identity);
        var portal = new PortalLink(
            PortalId.From(Guid.Parse("9d9620d8-15b4-4be1-932a-30c77ed46cff")),
            "Vers la clairière",
            source.Id,
            AreaId.From(Guid.Parse("2c8b2971-a2ba-4462-9d9a-c368f65a5a7c")),
            EntityId.From(Guid.Parse("47996244-97c1-4824-82a3-40922e577f53")),
            bidirectional: true,
            preloadDestination: true);

        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("93b6f81b-49eb-4e3d-a156-08ad78529525")),
            "forest-edge",
            "Lisière",
            [source],
            AreaKind.Exterior,
            new AreaDimensions(128, 96),
            [portal]);

        Assert.Equal(AreaDefinition.CurrentSchemaVersion, area.SchemaVersion);
        Assert.Equal(AreaKind.Exterior, area.Kind);
        Assert.Equal(new AreaDimensions(128, 96), area.Dimensions);
        Assert.Equal(portal, Assert.Single(area.Portals));
        Assert.Equal(area.Portals, area.WithEntities([]).Portals);
    }

    [Fact]
    public void Dimensions_RejectUnsupportedSizes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AreaDimensions(2, 32));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AreaDimensions(32, 8192));
    }
}
