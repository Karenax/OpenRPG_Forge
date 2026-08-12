using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
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
        var stored = history.State.Project.Areas.Single().Authoring;
        Assert.Equal("crypt.secret.opened", Assert.Single(stored.UserEvents).NamespacedName);
        Assert.True(stored.Map.FogOfWar);
        Assert.Equal(2, stored.Map.FloorCount);
        Assert.Equal(2, stored.Layers.Count);
        Assert.True(history.Undo().Succeeded);
    }

    [Fact]
    public void R055DryRunApplyUndoAndScriptRuntimeUseTheSameCanonicalProfile()
    {
        var context = new PlaceEntityTestContext();
        var profile = Profile();
        var handler = new SetAreaAuthoringHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var dryRun = handler.Handle(context.State, new(CommandId.From(Guid.NewGuid()), context.State.Revision,
            context.AreaId, profile, true));
        var history = new ProjectCommandHistory(context.State);

        var applied = handler.Handle(history.State, new(CommandId.From(Guid.NewGuid()), history.State.Revision,
            context.AreaId, profile, false));
        history.Record(applied);
        var area = history.State.Project.Areas.Single();
        var snapshot = AreaExperienceAuthoringScriptService.Read(area);
        var dispatched = AreaExperienceAuthoringScriptService.Dispatch(area, "area.crypt.alarm",
            new Dictionary<string, object?> { ["level"] = 2 });
        var explored = AreaExperienceAuthoringScriptService.Reveal(area,
            new(AreaMapExplorationScope.Party, "party:heroes", new HashSet<string>(), new HashSet<Guid>()),
            17, 3, profile.Map.Pins[0].Id);

        Assert.True(dryRun.Succeeded);
        Assert.Equal(context.State.Revision, dryRun.State.Revision);
        Assert.Contains("visibility room", dryRun.Patch!.Summary, StringComparison.Ordinal);
        Assert.True(applied.Succeeded);
        Assert.Equal(1, snapshot.VisibilityRooms);
        Assert.Equal("schema://area/alarm/v1", dispatched.PayloadSchema);
        Assert.Contains("2:0", explored.RevealedCells);
        Assert.Contains(profile.Map.Pins[0].Id, explored.RevealedPins);
        Assert.True(history.Undo().Succeeded);
        Assert.True(history.Redo().Succeeded);
    }

    [Fact]
    public void R055PerformanceReportMeasuresVisibilityLodBudgetsAndPopping()
    {
        var context = new PlaceEntityTestContext();
        var area = context.State.Project.Areas.Single().WithAuthoring(Profile());
        var project = new Forge.Domain.Projects.ProjectDefinition(context.State.Project.Id,
            context.State.Project.Title, context.State.Project.Module, context.State.Project.Blueprints,
            [area], context.State.Project.Dialogues, context.State.Project.Quests);

        var report = AreaPerformanceAnalyzer.Analyze(project, area.Id);

        Assert.Equal(1, report.VisibilityRoomCount);
        Assert.Equal(1, report.OccluderCount);
        Assert.Equal(1, report.LodOverrideCount);
        Assert.True(float.IsFinite(report.EstimatedPopping));
    }

    [Fact]
    public void R056GroupSelectionAndPartialExportUseNestedVisibleExportableLayers()
    {
        var context = new PlaceEntityTestContext();
        var placed = context.Handler.Handle(context.State, context.CreateCommand());
        var area = placed.State.Project.Areas.Single();
        var root = Guid.Parse("a0561000-0000-4000-8000-000000000001");
        var child = Guid.Parse("a0561000-0000-4000-8000-000000000002");
        var hidden = Guid.Parse("a0561000-0000-4000-8000-000000000003");
        var source = area.Authoring;
        var profile = new AreaAuthoringProfile(source.SchemaVersion, source.UserEvents, source.Map,
        [
            new(root, "Architecture", null, true, false, "builder", [], provenance: "test:r056"),
            new(child, "Creatures", root, true, false, "writer", [context.EntityId],
                provenance: "test:r056"),
            new(hidden, "Private", root, false, false, "gm", [], provenance: "test:r056",
                exportEnabled: false),
        ], source.Experience, true);
        area = area.WithAuthoring(profile);

        var selection = AreaLayerAuthoringService.SelectGroup(area, root);
        var export = AreaLayerAuthoringService.PreviewPartialExport(area, [root]);
        var snapshot = AreaLayerAuthoringService.Read(area);

        Assert.Equal([context.EntityId], selection.EntityIds);
        Assert.Equal(3, selection.LayerIds.Count);
        Assert.Equal(2, export.Layers.Count);
        Assert.Equal(context.EntityId, Assert.Single(export.Entities).Id);
        Assert.Equal(context.BlueprintId, Assert.Single(export.BlueprintDependencies));
        Assert.Equal(3, snapshot.Layers.Count);
    }

    [Fact]
    public void R056ReparentAndAssignmentRefuseInheritedLocksAndPreserveCanonicalProfile()
    {
        var context = new PlaceEntityTestContext();
        var placed = context.Handler.Handle(context.State, context.CreateCommand());
        var area = placed.State.Project.Areas.Single();
        var locked = Guid.Parse("a0561000-0000-4000-8000-000000000010");
        var child = Guid.Parse("a0561000-0000-4000-8000-000000000011");
        var free = Guid.Parse("a0561000-0000-4000-8000-000000000012");
        var source = area.Authoring;
        var profile = new AreaAuthoringProfile(source.SchemaVersion, source.UserEvents, source.Map,
        [
            new(locked, "Locked", null, true, true, "owner", [], provenance: "test:r056"),
            new(child, "Child", locked, true, false, "owner", [], provenance: "test:r056"),
            new(free, "Free", null, true, false, "owner", [], provenance: "test:r056"),
        ], source.Experience);
        area = area.WithAuthoring(profile);

        var deniedMove = AreaLayerAuthoringService.Reparent(profile, child, free);
        var deniedAssignment = AreaLayerAuthoringService.AssignEntity(area, child, context.EntityId);
        var allowed = AreaLayerAuthoringService.AssignEntity(area, free, context.EntityId);

        Assert.False(deniedMove.Succeeded);
        Assert.Contains(deniedMove.Diagnostics, item => item.StartsWith(AreaLayerAuthoringService.LockedLayerCode));
        Assert.False(deniedAssignment.Succeeded);
        Assert.True(allowed.Succeeded);
        Assert.Equal(context.EntityId, Assert.Single(allowed.Profile.Layers.Single(item => item.Id == free).EntityIds));
    }

    [Fact]
    public void R056HandlerRejectsCyclesBeforeMutationAndSupportsDryRunApplyUndoRedo()
    {
        var context = new PlaceEntityTestContext();
        var first = Guid.Parse("a0561000-0000-4000-8000-000000000020");
        var second = Guid.Parse("a0561000-0000-4000-8000-000000000021");
        var source = context.State.Project.Areas.Single().Authoring;
        var invalid = new AreaAuthoringProfile(source.SchemaVersion, source.UserEvents, source.Map,
        [
            new(first, "First", second, true, false, "owner", [], provenance: "test:r056"),
            new(second, "Second", first, true, false, "owner", [], provenance: "test:r056"),
        ], source.Experience, true);
        var valid = new AreaAuthoringProfile(source.SchemaVersion, source.UserEvents, source.Map,
            [new(first, "First", null, true, false, "owner", [], provenance: "test:r056")],
            source.Experience, true);
        var handler = new SetAreaAuthoringHandler(new FixedClock(PlaceEntityTestContext.Timestamp));

        var rejected = handler.Handle(context.State, new(CommandId.From(Guid.NewGuid()), context.State.Revision,
            context.AreaId, invalid, false));
        var dryRun = handler.Handle(context.State, new(CommandId.From(Guid.NewGuid()), context.State.Revision,
            context.AreaId, valid, true));
        var history = new ProjectCommandHistory(context.State);
        history.Record(handler.Handle(history.State, new(CommandId.From(Guid.NewGuid()), history.State.Revision,
            context.AreaId, valid, false)));

        Assert.False(rejected.Succeeded);
        Assert.Contains(rejected.Diagnostics, item => item.Code == AreaLayerAuthoringValidator.CycleCode);
        Assert.True(dryRun.Succeeded);
        Assert.Contains("editor layer", dryRun.Patch!.Summary, StringComparison.Ordinal);
        Assert.True(history.Undo().Succeeded);
        Assert.True(history.Redo().Succeeded);
    }

    private static AreaAuthoringProfile Profile()
    {
        var room = new AreaVisibilityRoomProfile(Guid.Parse("a0552000-0000-4000-8000-000000000001"),
            "Crypt", TransformValue.Identity, false, 96, new Dictionary<string, string>());
        var visibility = new AreaVisibilityAuthoringProfile([room], [],
            [new(Guid.Parse("a0552000-0000-4000-8000-000000000002"), "Wall", TransformValue.Identity,
                false, "asset:wall", new Dictionary<string, string>())], true, true, true, 16, 128);
        var lod = new AreaLodAuthoringProfile(AreaLodSourceMode.Hybrid, [20, 50, 100], 1_500_000, 96,
            24, .15f, true, true,
            [new(Guid.Parse("a0552000-0000-4000-8000-000000000003"), "asset:column",
                AreaLodSourceMode.Generated, [1, .5f], [12000, 6000], .2f, "test:r055")]);
        var experience = new AreaExperienceAuthoringProfile(
            Guid.Parse("a0552000-0000-4000-8000-000000000004"), 1, "test:r055",
            [new(Guid.Parse("a0552000-0000-4000-8000-000000000005"), "Combat",
                new Dictionary<string, float> { ["music"] = .8f }, .75f, "area.crypt.combat", "test:r055")],
            visibility, lod, true);
        return new(1,
            [new(Guid.Parse("a0552000-0000-4000-8000-000000000006"), "area.crypt.alarm",
                "schema://area/alarm/v1", "scripts/alarm.forge", 1, ["area.crypt.alarm"], true,
                "test:r055")],
            new(true, true, "hybrid-2d-3d", 2,
                [new(Guid.Parse("a0552000-0000-4000-8000-000000000007"), "Shrine", 16, 0, 0, false)],
                Guid.Parse("a0552000-0000-4000-8000-000000000008"), 1, "test:r055", true, true,
                AreaMapExplorationScope.Party, 8, "area.map.reveal"), [], experience);
    }
}
