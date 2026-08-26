using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Mcp.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Tests.Projects;

public sealed class AreaBiomeAuthoringTests
{
    [Fact]
    public void PlanApplySaveReopenAndRevertAreTypedRevisionSafeAndWorkspaceAgnostic()
    {
        using var first = CreateExteriorWorkspace();
        using var second = CreateExteriorWorkspace();
        ExerciseWorkspace(first);
        ExerciseWorkspace(second);
    }

    private static TestWorkspace CreateExteriorWorkspace() => new(project =>
    {
        var area = project.Areas[0];
        var exterior = new AreaDefinition(area.Id, area.Slug, area.DisplayName, area.Entities, AreaKind.Exterior,
            area.Dimensions, area.Portals, area.SchemaVersion, area.Navigation, area.Environment, area.Authoring,
            AreaWorldProfile.CreateDefault(area.Id, AreaKind.Exterior, area.Dimensions), area.RuntimePolicies,
            area.Foundation, area.Playtest);
        return new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            [exterior, .. project.Areas.Skip(1)], project.Dialogues, project.Quests);
    });

    private static void ExerciseWorkspace(TestWorkspace workspace)
    {
        var canonical = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var area = canonical.Areas[0];
        var biome = area.World.Biomes[0];
        var beforeArea = area;
        var resource = workspace.Project.GetAreaBiomes(area.Id.ToString());
        Assert.Empty(resource.Diagnostics);
        Assert.Equal(2, resource.SchemaVersion);
        Assert.Contains(resource.Biomes, item => item.Layer.Id == biome.Id);

        var request = new AreaBiomePlanRequest(2, resource.Revision, area.Id.ToString(),
        [
            new BiomeScatterPatch(biome.Id,
                [BiomeScatterPatchField.Density, BiomeScatterPatchField.Resource,
                    BiomeScatterPatchField.ExclusionTags, BiomeScatterPatchField.Seed],
                Density: .77f,
                Resource: "assets/nature/generic-shrub.glb",
                ExclusionTags: ["water", "road", "focus-exclusion"],
                Seed: 761),
        ]);
        var planned = workspace.Project.PlanAreaBiomes(request);
        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.Equal(resource.Revision, workspace.Project.GetCurrentRevision());
        Assert.Equal($"areas/{area.Id}/world/biomes/{biome.Id:D}", planned.Diff!.Path);

        var stale = workspace.Project.PlanAreaBiomes(request with { BaseRevision = resource.Revision + 1 });
        Assert.False(stale.Succeeded);
        Assert.Contains(stale.Diagnostics, item => item.Code == "command.revision_conflict");

        var applied = workspace.Project.ApplyAreaBiomePlan(planned.PlanId!);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        var reopened = ProjectSession.Open(workspace.RootDirectory).Session!.Project;
        var persistedArea = reopened.Areas.Single(item => item.Id == area.Id);
        var persistedBiome = persistedArea.World.Biomes.Single(item => item.Id == biome.Id);
        Assert.Equal(.77f, persistedBiome.ScatterDensity);
        Assert.Equal(761, BiomeScatterAuthoring.Read(persistedBiome).Seed);
        var expectedWorld = new AreaWorldProfile(beforeArea.World.Id, beforeArea.World.SchemaVersion,
            beforeArea.World.Terrain, persistedArea.World.Biomes, beforeArea.World.Features,
            beforeArea.World.VolumetricNavigation, beforeArea.World.FlightRoutes, beforeArea.World.Loading,
            beforeArea.World.Provenance);
        Assert.Equal(AreaJsonSerializer.Serialize(beforeArea.WithWorld(expectedWorld)),
            AreaJsonSerializer.Serialize(persistedArea));

        var reverted = workspace.Project.RevertAreaBiomePlan(planned.PlanId!);
        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        var restored = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas
            .Single(item => item.Id == area.Id);
        Assert.Equal(AreaJsonSerializer.Serialize(beforeArea), AreaJsonSerializer.Serialize(restored));
    }
}
