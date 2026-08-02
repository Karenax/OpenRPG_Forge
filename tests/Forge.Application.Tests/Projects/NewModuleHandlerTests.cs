using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
using Forge.Domain.Projects;

namespace Forge.Application.Tests.Projects;

public sealed class NewModuleHandlerTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 2, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Handle_CreatesAPlayableModuleAggregate()
    {
        var handler = new NewModuleHandler(new FixedClock(Timestamp));
        var command = CreateCommand();

        var result = handler.Handle(command);

        Assert.True(result.Succeeded);
        Assert.True(result.Audit.Applied);
        Assert.Equal(Timestamp, result.Audit.Timestamp);
        var project = Assert.IsType<ProjectState>(result.State).Project;
        Assert.Equal("les-iles-brisees", project.Module.Slug);
        Assert.Equal("archipel-oublie", project.Areas.Single().Slug);
        Assert.Equal(project.Areas.Single().Id, project.Module.EntryAreaId);
        Assert.Equal(2, project.Blueprints.Count);
        Assert.Contains(project.Blueprints, blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn);
        Assert.Contains(project.Areas.Single().Entities, entity => entity.Tags.Contains("floor", StringComparer.Ordinal));
        Assert.Contains(project.Areas.Single().Entities, entity => entity.Tags.Contains("player", StringComparer.Ordinal));
    }

    [Fact]
    public void Handle_DryRunBuildsThePreviewWithoutApplyingTheAudit()
    {
        var handler = new NewModuleHandler(new FixedClock(Timestamp));

        var result = handler.Handle(CreateCommand(dryRun: true));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.State);
        Assert.False(result.Audit.Applied);
        Assert.Equal("add", result.Patch?.Operation);
        Assert.Equal("remove", result.Inverse?.Operation);
    }

    [Fact]
    public void Handle_RejectsAnEmptyTitle()
    {
        var handler = new NewModuleHandler(new FixedClock(Timestamp));

        var result = handler.Handle(CreateCommand(title: " "));

        Assert.False(result.Succeeded);
        Assert.Equal(NewModuleHandler.TitleRequiredCode, Assert.Single(result.Diagnostics).Code);
        Assert.Null(result.State);
    }

    [Fact]
    public void Handle_RejectsANonZeroInitialRevision()
    {
        var handler = new NewModuleHandler(new FixedClock(Timestamp));

        var result = handler.Handle(CreateCommand(revision: new ProjectRevision(2)));

        Assert.False(result.Succeeded);
        Assert.Equal(NewModuleHandler.RevisionConflictCode, Assert.Single(result.Diagnostics).Code);
    }

    private static NewModuleCommand CreateCommand(
        bool dryRun = false,
        string title = "Les Îles Brisées",
        ProjectRevision? revision = null) =>
        new(
            CommandId.From(Guid.Parse("8ff97f8b-f9b4-486a-84d5-9defe849f9ef")),
            revision ?? new ProjectRevision(0),
            ProjectId.From(Guid.Parse("4d410f21-2d47-4f08-b5d0-2d2e5e43796a")),
            ModuleId.From(Guid.Parse("0a8d1557-b84b-424b-8857-e8f0a7ac2765")),
            AreaId.From(Guid.Parse("ea40d07f-3e30-46a5-a39d-f4f14a25d23a")),
            BlueprintId.From(Guid.Parse("ba1e3949-af2a-409a-af27-8c6698694111")),
            EntityId.From(Guid.Parse("50542d7e-d54f-409b-8156-06cca92b2ea6")),
            BlueprintId.From(Guid.Parse("46d891e5-a116-48df-8b2e-0fdebf16bc52")),
            EntityId.From(Guid.Parse("72dd2ac1-381c-4652-a8ff-5d2be82dcb61")),
            title,
            "Un archipel à reconstruire.",
            "Archipel oublié",
            dryRun);
}
