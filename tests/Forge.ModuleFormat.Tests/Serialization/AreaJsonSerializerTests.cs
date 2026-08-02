using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class AreaJsonSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesDimensionsKindEntitiesAndPortalLinks()
    {
        var blueprintId = BlueprintId.From(Guid.Parse("bb73e696-8a7e-4698-8459-2bb47283ac47"));
        var source = new EntityInstance(
            EntityId.From(Guid.Parse("1d13c9e5-a4bf-4422-a3c6-6049062faf9d")),
            blueprintId,
            TransformValue.Identity);
        var destination = EntityId.From(Guid.Parse("71a13aa4-e691-4255-bf89-f5bec5fcfe38"));
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("210a9e3e-230f-46cc-a915-8d2d83069f29")),
            "forest-edge",
            "Forest edge",
            [source],
            AreaKind.Exterior,
            new AreaDimensions(128, 96),
            [new PortalLink(
                PortalId.From(Guid.Parse("016df68f-1fd4-4135-a32f-f833ca2dbda2")),
                "To the ruins",
                source.Id,
                AreaId.From(Guid.Parse("f44c5ad1-5ca1-4753-b712-932e3fc3d03a")),
                destination,
                true,
                true)]);

        var json = AreaJsonSerializer.Serialize(area);
        var restored = AreaJsonSerializer.Deserialize(json);

        Assert.Equal(json, AreaJsonSerializer.Serialize(restored));
        Assert.Equal(area.Kind, restored.Kind);
        Assert.Equal(area.Dimensions, restored.Dimensions);
        Assert.Equal(area.Portals, restored.Portals);
    }
}
