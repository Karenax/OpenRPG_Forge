using Forge.Application.Navigation;
using Forge.Domain.Areas;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Navigation;

public sealed class AnchorScriptServiceTests
{
    [Fact]
    public void Typed_handle_supports_bounded_kind_and_tag_queries()
    {
        var areaId = AreaId.From(Guid.Parse("a9020000-0000-4000-8000-000000000001"));
        var definition = new AnchorDefinition(Guid.Parse("a9020000-0000-4000-8000-000000000002"), 1,
            "test:r009", "ember", new LocalizedText("anchor.ember", "Ember"), AnchorKind.Waypoint,
            AnchorScope.Area, ["ember", "quest"], null, null, false);
        var instance = new AnchorInstance(Guid.Parse("a9020000-0000-4000-8000-000000000003"), 1,
            "test:r009", definition.Id, TransformValue.Identity, 1,
            new EntityPropertyOverrides(1, [new("quest.marker", EntityPropertyValueKind.String, "ember")]),
            null, null);
        var area = new AreaDefinition(areaId, "area", "Area", [], navigation:
            AreaNavigationDefinition.CreateDefault(areaId).WithAnchors(new AnchorCatalog(
                Guid.Parse("a9020000-0000-4000-8000-000000000004"), 1, "test:r009", areaId, false,
                [definition], [instance])));
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("a9020000-0000-4000-8000-000000000005")),
            "Anchor script", [], [area]);
        var service = new AnchorScriptService(project, areaId);

        var values = service.Query(service.Handle, "waypoint", "ember", 1);

        var value = Assert.Single(values);
        Assert.Equal(instance.Id, value.InstanceId);
        Assert.Equal("anchor.ember", definition.Label.Key);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Query(service.Handle, limit: 501));
        Assert.Throws<InvalidOperationException>(() => service.Query(
            service.Handle with { CatalogId = Guid.NewGuid() }));
    }
}
