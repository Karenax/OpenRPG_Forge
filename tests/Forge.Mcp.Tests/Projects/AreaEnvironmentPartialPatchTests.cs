using Forge.Domain.Areas;
using Forge.Mcp.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.Mcp.Tests.Projects;

public sealed class AreaEnvironmentPartialPatchTests
{
    [Fact]
    public void LightingPatchIsDryRunPrecisePersistentRevisionSafeAndRevertible()
    {
        using var workspace = new TestWorkspace();
        var before = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas[0];
        var resource = workspace.Project.GetAreaEnvironment(before.Id.ToString());
        var lighting = new AreaLightingProfile(DayNightMode.AlwaysNight, 37, .19f, .72f, 4300, true, false);
        var request = new AreaEnvironmentPlanRequest(
            2,
            resource.Revision,
            before.Id.ToString(),
            Patch: new([AreaEnvironmentPatchField.Lighting], Lighting: lighting));

        var planned = workspace.Project.PlanAreaEnvironment(request);
        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.Equal(resource.Revision, workspace.Project.GetCurrentRevision());
        Assert.Equal($"areas/{before.Id}/environment/lighting", planned.Diff!.Path);
        Assert.Equal([$"areas/{before.Id}/environment/lighting"], planned.ChangedPaths);

        var stale = workspace.Project.PlanAreaEnvironment(request with { BaseRevision = resource.Revision + 1 });
        Assert.False(stale.Succeeded);
        Assert.Contains(stale.Diagnostics, item => item.Code == "command.revision_conflict");

        var applied = workspace.Project.ApplyAreaEnvironmentPlan(planned.PlanId!);
        Assert.True(applied.Succeeded);
        Assert.True(applied.Applied);
        var persisted = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas
            .Single(item => item.Id == before.Id);
        var expectedEnvironment = AreaEnvironmentPatch.Apply(before.Environment, request.Patch!);
        Assert.Equal(AreaJsonSerializer.Serialize(before.WithEnvironment(expectedEnvironment)),
            AreaJsonSerializer.Serialize(persisted));

        var reverted = workspace.Project.RevertAreaEnvironmentPlan(planned.PlanId!);
        Assert.True(reverted.Succeeded);
        Assert.True(reverted.Reverted);
        var restored = ProjectSession.Open(workspace.RootDirectory).Session!.Project.Areas
            .Single(item => item.Id == before.Id);
        Assert.Equal(AreaJsonSerializer.Serialize(before), AreaJsonSerializer.Serialize(restored));
    }
}
