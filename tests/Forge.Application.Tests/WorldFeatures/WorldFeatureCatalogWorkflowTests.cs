using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.Tests.WorldFeatures;

public sealed class WorldFeatureCatalogWorkflowTests
{
    [Fact]
    public void Command_supports_preview_apply_history_and_typed_script_query()
    {
        var state = CreateState();
        var catalog = Catalog(state.Project.Module.Id.Value);
        var handler = new SetWorldFeatureCatalogHandler(new FixedClock(DateTimeOffset.UnixEpoch));
        var command = new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), state.Revision, catalog, true);

        var preview = handler.Handle(state, command);
        var applied = handler.Handle(state, command with { DryRun = false });
        var history = new ProjectCommandHistory(state);
        history.Record(applied);
        var script = new WorldFeatureScriptService();
        var handles = script.Query(history.State, "script");

        Assert.True(preview.Succeeded);
        Assert.Same(state, preview.State);
        Assert.Contains("flags=1", preview.Patch!.Summary);
        Assert.True(applied.Succeeded);
        Assert.Equal(state.Revision.Next(), applied.NewRevision);
        Assert.Single(handles);
        Assert.Equal("1", handles[0].Properties["flags"]);
        Assert.True(history.Undo().Succeeded);
        Assert.Empty(history.State.Project.Module.WorldFeatures.Scripts.ModuleFlags);
        Assert.True(history.Redo().Succeeded);
        Assert.Single(history.State.Project.Module.WorldFeatures.Scripts.ModuleFlags);
    }

    [Fact]
    public void Command_rejects_revision_identity_and_invalid_script_contracts()
    {
        var state = CreateState();
        var handler = new SetWorldFeatureCatalogHandler(new FixedClock(DateTimeOffset.UnixEpoch));
        var catalog = Catalog(state.Project.Module.Id.Value);
        var stale = handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision.Next(), catalog, false));
        var wrongId = handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision,
            Catalog(Guid.Parse("14000000-0000-4000-8000-000000000099")), false));
        var invalidScripts = catalog.Scripts with
        {
            ModuleFlags = [catalog.Scripts.ModuleFlags[0], catalog.Scripts.ModuleFlags[0]],
        };
        var invalid = handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision,
            new WorldFeatureCatalog(catalog.Id, 1, "test", scripts: invalidScripts), false));

        Assert.Contains(stale.Diagnostics, item => item.Code == "command.revision_conflict");
        Assert.Contains(wrongId.Diagnostics, item => item.Code == "world.catalog.id_mismatch");
        Assert.Contains(invalid.Diagnostics, item => item.Code == "world.script.flag_invalid");
    }

    private static ProjectState CreateState()
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("14000000-0000-4000-8000-000000000001")),
            "world", "World", []);
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("14000000-0000-4000-8000-000000000002")),
            "World features", [], [area]);
        return new(project, new ProjectRevision(7));
    }

    private static WorldFeatureCatalog Catalog(Guid moduleId)
    {
        var scripts = new ScriptAutomationProfile(moduleId, 1, "test:g13-aa-ab",
            new([], "restore-camera-and-control", true, true), new(false, true, []),
            [new("rest.enabled", ScriptAutomationProfile.SettingKind.Boolean, "true",
                ScriptAutomationProfile.FlagScope.Module, "Allows safe rest.", 1)],
            [new("combat.aggression", ScriptAutomationProfile.SettingKind.Choice, "balanced",
                ["passive", "balanced", "aggressive"], "Balanced combat behavior.", true)]);
        return new(moduleId, 1, "test:g13-n-ab", scripts: scripts);
    }
}
