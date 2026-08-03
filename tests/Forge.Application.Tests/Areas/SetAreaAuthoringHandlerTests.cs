using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
namespace Forge.Application.Tests.Areas;

public sealed class SetAreaAuthoringHandlerTests
{
    [Fact]
    public void Persists_namespaced_events_map_fog_and_nested_layers_atomically()
    {
        var c = new PlaceEntityTestContext(); var root = Guid.NewGuid(); var child = Guid.NewGuid();
        var profile = new AreaAuthoringProfile(1,
            [new(Guid.NewGuid(), "crypt.secret.opened", "schema://events/secret-v1", "scripts/secret.forge")],
            new(true, true, "orthographic", 2, [new(Guid.NewGuid(), "Exit", 2, 3, 0, false)]),
            [new(root, "Architecture", null, true, false, "builder", []), new(child, "Secrets", root, false, true, "gm", [])]);
        var handler = new SetAreaAuthoringHandler(new FixedClock(PlaceEntityTestContext.Timestamp)); var history = new ProjectCommandHistory(c.State);
        history.Record(handler.Handle(history.State, new(CommandId.From(Guid.NewGuid()), history.State.Revision, c.AreaId, profile, false)));
        Assert.Equal(profile, history.State.Project.Areas.Single().Authoring); Assert.True(history.Undo().Succeeded);
    }
}
