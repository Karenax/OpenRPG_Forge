using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Areas;

public sealed class AreaRuntimeValidationWorkflowTests
{
    [Fact]
    public void Play_from_here_preview_apply_undo_and_redo_preserve_the_profile()
    {
        var context = new PlaceEntityTestContext();
        var spawnBlueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("a0291000-0000-4000-8000-000000000001")),
            "player", "Player", BlueprintKind.PlayerSpawn);
        var original = context.State.Project.Areas.Single();
        var spawn = new EntityInstance(EntityId.From(Guid.Parse("a0291000-0000-4000-8000-000000000002")),
            spawnBlueprint.Id, TransformValue.Identity);
        var area = original.WithEntities([spawn]);
        var project = new ProjectDefinition(context.State.Project.Id, context.State.Project.Title,
            context.State.Project.Module, [.. context.State.Project.Blueprints, spawnBlueprint], [area],
            context.State.Project.Dialogues, context.State.Project.Quests);
        var state = new ProjectState(project, context.State.Revision);
        var defaults = AreaPlaytestProfile.CreateDefault(area.Id);
        var profile = new AreaPlaytestProfile(defaults.Id, 1, "author:r029-test", true, false,
            PlayFromHereStartMode.PlayerSpawn, spawn.Id, TransformValue.Identity, null, spawnBlueprint.Id,
            "default", []);
        var command = new SetAreaPlaytestCommand(CommandId.From(Guid.Parse("a0291000-0000-4000-8000-000000000003")),
            state.Revision, area.Id, profile, false);
        var handler = new SetAreaPlaytestHandler(new FixedClock(PlaceEntityTestContext.Timestamp));

        var preview = handler.Handle(state, command with { DryRun = true });
        var applied = handler.Handle(state, command);
        var history = new ProjectCommandHistory(state);
        history.Record(applied);

        Assert.True(preview.Succeeded);
        Assert.Equal(state, preview.State);
        Assert.Equal(profile, history.State.Project.Areas.Single().Playtest);
        Assert.True(history.Undo().Succeeded);
        Assert.False(history.State.Project.Areas.Single().Playtest.Enabled);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(profile, history.State.Project.Areas.Single().Playtest);
    }

    [Fact]
    public async Task Transition_service_dispatches_runtime_lifecycle_and_reports_missing_streaming()
    {
        var fixture = PortalFixture(preload: false);

        var succeeded = await AreaTransitionRuntimeService.TransitionAsync(fixture.Project, fixture.Source.Id,
            fixture.Portal.Id, fixture.Source.Entities[0].Id, Vector3Value.Zero, 0);

        Assert.True(succeeded.Succeeded);
        Assert.Equal(fixture.Destination.Id, succeeded.Travel!.DestinationAreaId);
        Assert.Equal("area.entered", succeeded.Lifecycle!.Events[^1].Binding);

        var required = PortalFixture(preload: true);
        var missing = await AreaTransitionRuntimeService.TransitionAsync(required.Project, required.Source.Id,
            required.Portal.Id, null, Vector3Value.Zero, 0);
        Assert.False(missing.Succeeded);
        Assert.Contains("area.portal.streaming_required", missing.Diagnostics);
    }

    [Fact]
    public void Readiness_report_is_deterministic_and_always_covers_the_nine_source_categories()
    {
        var fixture = PortalFixture(preload: false);

        var first = AreaReadinessService.Run(fixture.Project, fixture.Source.Id);
        var second = AreaOperationsScriptService.Validate(fixture.Project, fixture.Source.Id);

        Assert.Equal(AreaReadinessService.RequiredChecks, first.Checks);
        Assert.Equal(9, first.CategoryCounts.Count);
        Assert.All(first.Diagnostics, item =>
        {
            Assert.NotEqual(Guid.Empty, item.Id);
            Assert.NotEmpty(item.Code);
            Assert.StartsWith($"areas/{fixture.Source.Id}/", item.Path, StringComparison.Ordinal);
        });
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
    }

    private static (ProjectDefinition Project, AreaDefinition Source, AreaDefinition Destination, PortalLink Portal)
        PortalFixture(bool preload)
    {
        var blueprint = new BlueprintDefinition(BlueprintId.From(Guid.Parse("a0292000-0000-4000-8000-000000000001")),
            "player", "Player", BlueprintKind.PlayerSpawn);
        var sourceId = AreaId.From(Guid.Parse("a0292000-0000-4000-8000-000000000002"));
        var destinationId = AreaId.From(Guid.Parse("a0292000-0000-4000-8000-000000000003"));
        var sourceEntity = new EntityInstance(EntityId.From(Guid.Parse("a0292000-0000-4000-8000-000000000004")),
            blueprint.Id, TransformValue.Identity);
        var destinationEntity = new EntityInstance(EntityId.From(Guid.Parse("a0292000-0000-4000-8000-000000000005")),
            blueprint.Id, new TransformValue(new Vector3Value(5, 0, 5), QuaternionValue.Identity, Vector3Value.One));
        var portal = new PortalLink(PortalId.From(Guid.Parse("a0292000-0000-4000-8000-000000000006")),
            "Crossing", sourceEntity.Id, destinationId, destinationEntity.Id, true, preload,
            provenance: "author:r029-test");
        var source = new AreaDefinition(sourceId, "source", "Source", [sourceEntity], portals: [portal]);
        var destination = new AreaDefinition(destinationId, "destination", "Destination", [destinationEntity]);
        if (preload)
        {
            var old = destination.World;
            var loading = new AreaLoadingProfile(old.Loading.Id, old.Loading.SchemaVersion,
                old.Loading.AsyncStreaming, old.Loading.PrefetchDependencies, old.Loading.CancellationEnabled,
                old.Loading.CacheBudgetMegabytes, old.Loading.MaximumConcurrentLoads, old.Loading.ChunkRadius,
                old.Loading.TargetLoadMilliseconds, ["chunks/destination.bin"], "author:r029-test");
            destination = destination.WithWorld(new AreaWorldProfile(old.Id, old.SchemaVersion, old.Terrain,
                old.Biomes, old.Features, old.VolumetricNavigation, old.FlightRoutes, loading,
                "author:r029-test"));
        }
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("a0292000-0000-4000-8000-000000000007")),
            "R029 fixture", [blueprint], [source, destination]);
        return (project, source, destination, portal);
    }
}
