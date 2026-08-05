using Forge.Application.Commands;
using Forge.Application.Content;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Assets;

namespace Forge.Application.Tests.Content;

public sealed class SetArtisticStyleProfileHandlerTests
{
    [Fact]
    public void DryRunApplyUndoAndRedoPreserveCanonicalStyleProfile()
    {
        var context = new EntityMutationTestContext();
        var profile = new ArtisticStyleProfile(
            ArtisticStyleProfileId.From(context.State.Project.Module.Id.Value),
            1, 1024, 15, 25, "pbr-metallic-roughness", "original-fantasy",
            ArtisticRealism.StylizedRealism, ["ember", "stone"], [], "test");
        var handler = new SetArtisticStyleProfileHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var command = new SetArtisticStyleProfileCommand(
            CommandId.From(Guid.Parse("f2000000-0000-4000-8000-000000000001")),
            context.State.Revision, profile, DryRun: false);

        var preview = handler.Handle(context.State, command with { DryRun = true });
        var history = new ProjectCommandHistory(context.State);
        history.Record(handler.Handle(history.State, command));

        Assert.True(preview.Succeeded);
        Assert.Same(context.State, preview.State);
        Assert.Equal("module/artisticStyle", preview.Patch!.Path);
        Assert.Equal(1024, history.State.Project.Module.ArtisticStyle.TargetTexelDensity);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(512, history.State.Project.Module.ArtisticStyle.TargetTexelDensity);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(1024, history.State.Project.Module.ArtisticStyle.TargetTexelDensity);
    }
}
