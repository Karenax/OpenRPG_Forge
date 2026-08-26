using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Modules;

namespace Forge.Application.Tests.Projects;

public sealed class PatchModuleMetadataHandlerTests
{
    [Fact]
    public void SummaryPatchIsDryRunUndoableAndDoesNotChangePublicNameOrDescription()
    {
        var context = new PlaceEntityTestContext();
        var source = context.State.Project;
        var patch = new ModuleMetadataPatch([], Localizations:
        [
            new("fr", [ProjectLocalizedMetadataPatchField.Summary], Summary: "Résumé partiel"),
        ]);
        var handler = new PatchModuleMetadataHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var history = new ProjectCommandHistory(context.State);

        var dryRun = handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, patch, null, true));
        Assert.True(dryRun.Succeeded);
        Assert.Same(context.State, dryRun.State);
        Assert.Equal("module/metadata/localizations/fr/summary", dryRun.Patch!.Path);

        var applied = handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, patch, null, false));
        history.Record(applied);
        Assert.Equal("Résumé partiel", history.State.Project.Module.Metadata.GetDefaultLocalization().Summary);
        Assert.Equal(source.Title, history.State.Project.Title);
        Assert.Equal(source.Module.DisplayName, history.State.Project.Module.DisplayName);
        Assert.Equal(source.Module.Description, history.State.Project.Module.Description);
        Assert.True(history.Undo().Succeeded);
        Assert.True(history.Redo().Succeeded);

        var stale = handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), context.State.Revision, patch, null, false));
        Assert.False(stale.Succeeded);
        Assert.Contains(stale.Diagnostics, item => item.Code == "command.revision_conflict");
    }
}
