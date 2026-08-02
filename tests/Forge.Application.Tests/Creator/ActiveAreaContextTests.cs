using Forge.Application.Creator;
using Forge.Domain.Areas;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Creator;

public sealed class ActiveAreaContextTests
{
    [Fact]
    public void Context_StartsOnEntryAreaAndRejectsUnknownArea()
    {
        var project = CreateProject();
        var context = new ActiveAreaContext();
        var changes = 0;
        context.Changed += () => changes++;

        Assert.True(context.Reset(project));
        Assert.Equal(project.Module.EntryAreaId, context.Current);
        Assert.True(context.Activate(project, project.Areas[1].Id));
        Assert.Equal(project.Areas[1].Id, context.Current);
        Assert.False(context.Activate(
            project,
            AreaId.From(Guid.Parse("2966e129-7dd6-4f51-8c2e-a4a5825a33c5"))));
        Assert.Equal(2, changes);
    }

    private static ProjectDefinition CreateProject()
    {
        var entry = new AreaDefinition(
            AreaId.From(Guid.Parse("8b72ef8b-001e-4250-8f61-736369bc09f5")),
            "entry",
            "Entry",
            []);
        var second = new AreaDefinition(
            AreaId.From(Guid.Parse("24882144-fb0e-4f40-923c-8d61250b8611")),
            "second",
            "Second",
            []);
        return new ProjectDefinition(
            ProjectId.From(Guid.Parse("67a11c8b-15a3-4e1b-a29f-6cf324c6220e")),
            "Area context",
            [],
            [entry, second]);
    }
}
