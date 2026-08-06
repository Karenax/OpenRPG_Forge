using Forge.Application.Commands;
using Forge.Application.Entities;
using Forge.Application.Factions;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Factions;

public sealed class SetFactionCatalogHandlerTests
{
    private readonly SetFactionCatalogHandler _handler = new(new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public void DryRunApplyUndoAndRedoPreserveCanonicalCatalog()
    {
        var history = new ProjectCommandHistory(CreateState());
        var catalog = FactionAdvancedTestData.Complete();
        var command = new SetFactionCatalogCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            catalog, true);

        var preview = _handler.Handle(history.State, command);
        var applied = _handler.Handle(history.State, command with { DryRun = false });
        history.Record(applied);

        Assert.True(preview.Succeeded);
        Assert.Null(history.Undo().State.Project.Module.Factions.Advanced);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(catalog.Advanced!.Id, history.State.Project.Module.Factions.Advanced!.Id);
        Assert.Contains("3 faction", preview.Patch!.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void RevisionAndInvalidCatalogAreRejectedWithoutMutation()
    {
        var state = CreateState();
        var catalog = FactionAdvancedTestData.Complete();
        var conflict = _handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision.Next(),
            catalog, false));
        var invalid = _handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision,
            catalog with { Advanced = catalog.Advanced! with { ServerAuthoritative = false } }, false));

        Assert.Equal(SetFactionCatalogHandler.RevisionConflictCode, Assert.Single(conflict.Diagnostics).Code);
        Assert.Contains(invalid.Diagnostics, item => item.Code == "faction.advanced.authority_required");
        Assert.Equal(state.Revision, invalid.State.Revision);
    }

    private static ProjectState CreateState()
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("b2020000-0000-4000-8000-000000000090")),
            "faction-lab", "Faction lab", []);
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("b2020000-0000-4000-8000-000000000091")),
            "Faction project", [], [area]);
        return new(project, new ProjectRevision(0));
    }
}
