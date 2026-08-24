using Forge.Application.Projects;
using Forge.Application.WorldFeatures;
using Forge.Domain.Factions;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.Tests.WorldFeatures;

public sealed class EncounterLifecycleScriptServiceTests
{
    [Fact]
    public void GetProjectsTypedLifecycleFactionSchedulerAndDiagnosticsWithoutMutation()
    {
        var source = SampleModuleAuthoringService.Create().State.Project;
        var faction = new FactionDefinition(Guid.Parse("a2081100-0000-4000-8000-000000000001"), 1,
            "test:ux2-lw-i", "ash-watch", "Veilleurs des cendres", false, [], ["guard"]);
        var encounter = new EncounterWorldProfile(Guid.Parse("a2081100-0000-4000-8000-000000000002"), 1,
            "test:ux2-lw-i", source.Areas[0].Id, new(30, 3, "area.occupied", true, true, .5), faction.Slug,
            new(true, 5, 12, "crypt.encounter"), null,
            new(EncounterWorldProfile.VisibilityMode.InViewAllowed, 0, false, string.Empty),
            new(30, 30, 120, true, true));
        var catalog = new WorldFeatureCatalog(source.Module.Id.Value, WorldFeatureCatalog.CurrentSchemaVersion,
            "test:ux2-lw-i", encounters: [encounter]);
        var module = source.Module.WithFactions(new FactionCatalog([faction], [], [], []))
            .WithWorldFeatures(catalog);
        var project = new ProjectDefinition(source.Id, source.Title, module, source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);
        var state = new ProjectState(project, new ProjectRevision(17));

        var handle = new EncounterLifecycleScriptService().Get(state, encounter.Id);

        Assert.Equal(state.Revision.Value, handle.Revision);
        Assert.Equal(.5, handle.Respawn!.AntiFarmMultiplier);
        Assert.Equal(faction.Id, handle.SpawnFaction!.Id);
        Assert.Equal("crypt.encounter", handle.Scheduler!.CustomEventNamespace);
        Assert.Empty(handle.Diagnostics);
        Assert.Same(project, state.Project);
    }

    [Fact]
    public void GetKeepsOptionalAbsenceAndReportsUnknownFactionByCodeAndPath()
    {
        var source = SampleModuleAuthoringService.Create().State.Project;
        var encounter = new EncounterWorldProfile(Guid.Parse("a2081100-0000-4000-8000-000000000003"), 1,
            "test:ux2-lw-i", source.Areas[0].Id, null, "unknown", null, null,
            new(EncounterWorldProfile.VisibilityMode.InViewAllowed, 0, false, string.Empty),
            new(30, 30, 120, true, true));
        var catalog = new WorldFeatureCatalog(source.Module.Id.Value, WorldFeatureCatalog.CurrentSchemaVersion,
            "test:ux2-lw-i", encounters: [encounter]);
        var project = new ProjectDefinition(source.Id, source.Title,
            source.Module.WithWorldFeatures(catalog), source.Blueprints, source.Areas,
            source.Dialogues, source.Quests);

        var handle = new EncounterLifecycleScriptService().Get(
            new ProjectState(project, new ProjectRevision(17)), encounter.Id);

        Assert.Null(handle.Respawn);
        Assert.Null(handle.Scheduler);
        Assert.Null(handle.SpawnFaction);
        Assert.Contains(handle.Diagnostics, item =>
            item.StartsWith(EncounterLifecycleRuntimeService.FactionMissingCode, StringComparison.Ordinal) &&
            item.EndsWith("/spawnFactionId", StringComparison.Ordinal));
    }
}
