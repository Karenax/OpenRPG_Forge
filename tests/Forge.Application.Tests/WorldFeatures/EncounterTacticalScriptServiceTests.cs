using Forge.Application.Projects;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Factions;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.Tests.WorldFeatures;

public sealed class EncounterTacticalScriptServiceTests
{
    [Fact]
    public void GetProjectsAuthoredFactionRouteVisibilityCleanupAndDiagnosticsWithoutMutation()
    {
        var fixture = ProjectWithEncounter(validReferences: true);
        var state = new ProjectState(fixture.Project, new ProjectRevision(23));

        var handle = new EncounterTacticalScriptService().Get(state, fixture.EncounterId);

        Assert.Equal(23, handle.Revision);
        Assert.Equal("ash-watch", handle.Reinforcement!.FactionSlug);
        Assert.Equal("Veilleurs des cendres", handle.Reinforcement.FactionDisplayName);
        Assert.Equal(2, handle.Reinforcement.RoutePointCount);
        Assert.Equal("BehindCover", handle.Visibility.Mode);
        Assert.Equal(120, handle.Cleanup.AbandonedAfterSeconds);
        Assert.Empty(handle.Diagnostics);
        Assert.Contains("runtime-adapter", handle.RuntimeBoundary, StringComparison.OrdinalIgnoreCase);
        Assert.Same(fixture.Project, state.Project);
    }

    [Fact]
    public void GetPreservesOptionalAbsenceAndReportsMissingReferencesByCodeAndPath()
    {
        var optionalFixture = ProjectWithEncounter(validReferences: true, reinforcementEnabled: false);
        var optional = new EncounterTacticalScriptService().Get(
            new ProjectState(optionalFixture.Project, new ProjectRevision(1)), optionalFixture.EncounterId);
        var invalidFixture = ProjectWithEncounter(validReferences: false);
        var invalid = new EncounterTacticalScriptService().Get(
            new ProjectState(invalidFixture.Project, new ProjectRevision(2)), invalidFixture.EncounterId);

        Assert.Null(optional.Reinforcement);
        Assert.Empty(optional.Diagnostics);
        Assert.Contains(invalid.Diagnostics, item =>
            item.StartsWith(EncounterTacticalRuntimeService.ReinforcementFactionMissingCode,
                StringComparison.Ordinal) && item.EndsWith("/factionId", StringComparison.Ordinal));
        Assert.Contains(invalid.Diagnostics, item =>
            item.StartsWith(EncounterTacticalRuntimeService.ReinforcementRouteMissingCode,
                StringComparison.Ordinal) && item.EndsWith("/routeId", StringComparison.Ordinal));
    }

    private static Fixture ProjectWithEncounter(bool validReferences, bool reinforcementEnabled = true)
    {
        var source = SampleModuleAuthoringService.Create().State.Project;
        var area = source.Areas[0];
        var routeId = Guid.Parse("a2092000-0000-4000-8000-000000000001");
        var route = new PatrolRoute(routeId, 1, "test:ux2-lw-j", PatrolTraversalPolicy.Once,
        [
            new(Guid.Parse("a2092000-0000-4000-8000-000000000002"), Vector3Value.Zero, 0, 0, 2, null, null),
            new(Guid.Parse("a2092000-0000-4000-8000-000000000003"), new(6, 0, 0), 0, 0, 2, null, null),
        ]);
        var waypoints = new WaypointAuthoringProfile(
            Guid.Parse("a2092000-0000-4000-8000-000000000004"), 1, "test:ux2-lw-j",
            area.Id, false, [], [route]);
        var authoredArea = area.WithNavigation(area.Navigation.WithWaypoints(waypoints));
        var faction = new FactionDefinition(Guid.Parse("a2092000-0000-4000-8000-000000000005"), 1,
            "test:ux2-lw-j", "ash-watch", "Veilleurs des cendres", false, [], ["guard"]);
        var encounterId = Guid.Parse("a2092000-0000-4000-8000-000000000006");
        var encounter = new EncounterWorldProfile(encounterId, 1, "test:ux2-lw-j", area.Id,
            null, null, null,
            reinforcementEnabled
                ? new(2, validReferences ? faction.Slug : "missing-faction",
                    validReferences ? route.Id : Guid.Parse("a2092000-0000-4000-8000-000000000099"),
                    4, 3, "boss.dead")
                : null,
            new(EncounterWorldProfile.VisibilityMode.BehindCover, 10, true, "vfx.spawn.ash"),
            new(30, 30, 120, true, true));
        var catalog = new WorldFeatureCatalog(source.Module.Id.Value, WorldFeatureCatalog.CurrentSchemaVersion,
            "test:ux2-lw-j", encounters: [encounter]);
        var module = source.Module.WithFactions(new FactionCatalog([faction], [], [], []))
            .WithWorldFeatures(catalog);
        var areas = source.Areas.Select(item => item.Id == authoredArea.Id ? authoredArea : item).ToArray();
        var project = new ProjectDefinition(source.Id, source.Title, module, source.Blueprints, areas,
            source.Dialogues, source.Quests);
        return new(project, encounterId);
    }

    private sealed record Fixture(ProjectDefinition Project, Guid EncounterId);
}
