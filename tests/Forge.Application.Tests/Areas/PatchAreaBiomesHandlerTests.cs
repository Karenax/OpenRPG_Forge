using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Areas;

public sealed class PatchAreaBiomesHandlerTests
{
    [Fact]
    public void DryRunApplyUndoRedoAndStaleRevisionUseCanonicalProjectHistory()
    {
        var areaId = AreaId.From(Guid.Parse("3467a950-8faa-4b96-b664-353c1ad52d01"));
        var area = new AreaDefinition(areaId, "generic-forest", "Generic Forest", [], AreaKind.Exterior);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("885c5043-afbb-4c1e-a4f6-34b19bb45a02")), "Biome test", [], [area]);
        var initial = new ProjectState(project, new ProjectRevision(7));
        var history = new ProjectCommandHistory(initial);
        var handler = new PatchAreaBiomesHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var biome = area.World.Biomes[0];
        var patch = new BiomeScatterPatch(biome.Id,
            [BiomeScatterPatchField.Density, BiomeScatterPatchField.Seed], Density: .81f, Seed: 4102);

        var dryRun = handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, areaId, [patch], true));
        Assert.True(dryRun.Succeeded);
        Assert.Same(initial, dryRun.State);
        Assert.Equal($"areas/{areaId}/world/biomes/{biome.Id:D}", dryRun.Patch!.Path);
        Assert.Contains("density", dryRun.Patch.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("seed", dryRun.Patch.Summary, StringComparison.OrdinalIgnoreCase);

        var applied = handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), history.State.Revision, areaId, [patch], false));
        history.Record(applied);
        Assert.Equal(.81f, history.State.Project.Areas.Single().World.Biomes[0].ScatterDensity);
        Assert.Equal(4102, BiomeScatterAuthoring.Read(history.State.Project.Areas.Single().World.Biomes[0]).Seed);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(biome.ScatterDensity, history.State.Project.Areas.Single().World.Biomes[0].ScatterDensity);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(.81f, history.State.Project.Areas.Single().World.Biomes[0].ScatterDensity);

        var stale = handler.Handle(history.State,
            new(CommandId.From(Guid.NewGuid()), new ProjectRevision(7), areaId, [patch], false));
        Assert.False(stale.Succeeded);
        Assert.Contains(stale.Diagnostics, item => item.Code == "command.revision_conflict");
    }
}
