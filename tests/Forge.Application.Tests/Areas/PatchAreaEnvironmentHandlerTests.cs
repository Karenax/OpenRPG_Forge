using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;

namespace Forge.Application.Tests.Areas;

public sealed class PatchAreaEnvironmentHandlerTests
{
    [Fact]
    public void DryRunApplyUndoRedoAndStaleRevisionPreserveOmittedFields()
    {
        var context = new PlaceEntityTestContext();
        var source = context.State.Project.Areas.Single().Environment;
        var lighting = new AreaLightingProfile(DayNightMode.AlwaysNight, 31, .2f, .55f, 3900, true, true);
        var patch = new AreaEnvironmentPatch([AreaEnvironmentPatchField.Lighting], Lighting: lighting);
        var handler = new PatchAreaEnvironmentHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var history = new ProjectCommandHistory(context.State);

        var dryRun = handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, context.AreaId, patch, true));
        Assert.True(dryRun.Succeeded);
        Assert.Same(context.State, dryRun.State);
        Assert.Equal($"areas/{context.AreaId}/environment/lighting", dryRun.Patch!.Path);

        var applied = handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, context.AreaId, patch, false));
        history.Record(applied);
        var updated = history.State.Project.Areas.Single().Environment;
        Assert.Same(lighting, updated.Lighting);
        Assert.Same(source.Audio, updated.Audio);
        Assert.Same(source.Rendering, updated.Rendering);
        Assert.Same(source.Weather, updated.Weather);
        Assert.True(history.Undo().Succeeded);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(lighting, history.State.Project.Areas.Single().Environment.Lighting);

        var stale = handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), context.State.Revision, context.AreaId, patch, false));
        Assert.False(stale.Succeeded);
        Assert.Contains(stale.Diagnostics, item => item.Code == "command.revision_conflict");
    }
}
