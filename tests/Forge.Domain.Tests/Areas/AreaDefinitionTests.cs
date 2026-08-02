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
}
