using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Projects;

public sealed class SetProjectFoundationHandlerTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 8, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FoundationUsesDryRunApplyUndoRedoAndTypedScriptHandles()
    {
        var state = CreateState();
        var project = state.Project;
        var spawn = project.Areas[0].Entities[0];
        var profile = ProjectFoundationProfile.CreateRpgDefault(
            Guid.Parse("d0240000-0000-4000-8000-000000000021"), project.Id, project.Module.Slug,
            project.Areas[0].Id, spawn.Id, "studio.foundation") with
        {
            RenderPreset = ProjectRenderPreset.Quality,
            NetworkMode = ProjectNetworkMode.DedicatedServer,
        };
        var handler = new SetProjectFoundationHandler(new FixedClock(Timestamp));
        var command = new SetProjectFoundationCommand(
            CommandId.From(Guid.Parse("d0240000-0000-4000-8000-000000000022")),
            state.Revision, profile, false);

        var preview = handler.Handle(state, command with { DryRun = true });
        var applied = handler.Handle(state, command);
        var history = new ProjectCommandHistory(state);
        history.Record(applied);

        Assert.True(preview.Succeeded);
        Assert.Same(state, preview.State);
        Assert.False(preview.Audit.Applied);
        Assert.True(applied.Succeeded);
        Assert.Equal(ProjectNetworkMode.DedicatedServer,
            applied.State.Project.Module.Metadata.Foundation?.NetworkMode);
        Assert.Equal("studio.foundation", ProjectFoundationScriptService.Read(applied.State.Project)?.PackageNamespace);
        Assert.NotNull(ProjectFoundationScriptService.ResolvePlayerStart(applied.State.Project,
            new("player", "campaign", "adventurer", new HashSet<string>())));
        Assert.True(history.Undo().Succeeded);
        Assert.Null(history.State.Project.Module.Metadata.Foundation);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(ProjectRenderPreset.Quality, history.State.Project.Module.Metadata.Foundation?.RenderPreset);
    }

    [Fact]
    public void FoundationRejectsRevisionConflictAndImmutableIdentityChange()
    {
        var state = CreateState();
        var project = state.Project;
        var profile = ProjectFoundationProfile.CreateRpgDefault(
            Guid.Parse("d0240000-0000-4000-8000-000000000023"), project.Id, project.Module.Slug,
            project.Areas[0].Id, project.Areas[0].Entities[0].Id);
        var handler = new SetProjectFoundationHandler(new FixedClock(Timestamp));
        var command = new SetProjectFoundationCommand(
            CommandId.From(Guid.Parse("d0240000-0000-4000-8000-000000000024")), state.Revision, profile, false);

        var revision = handler.Handle(state, command with { BaseRevision = state.Revision.Next() });
        var identity = handler.Handle(state, command with
        {
            Foundation = profile with
            {
                Identity = profile.Identity with
                {
                    ProjectId = ProjectId.From(Guid.Parse("d0240000-0000-4000-8000-000000000025")),
                },
            },
        });

        Assert.Equal(SetProjectFoundationHandler.RevisionConflictCode, Assert.Single(revision.Diagnostics).Code);
        Assert.Equal(ProjectFoundationValidator.IdentityCode, Assert.Single(identity.Diagnostics).Code);
        Assert.Same(state, identity.State);
    }

    private static ProjectState CreateState()
    {
        var projectId = ProjectId.From(Guid.Parse("d0240000-0000-4000-8000-000000000030"));
        var areaId = AreaId.From(Guid.Parse("d0240000-0000-4000-8000-000000000031"));
        var blueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("d0240000-0000-4000-8000-000000000032")),
            "player-spawn", "Player", BlueprintKind.PlayerSpawn);
        var entity = new EntityInstance(
            EntityId.From(Guid.Parse("d0240000-0000-4000-8000-000000000033")),
            blueprint.Id, TransformValue.Identity, ["player"]);
        var area = new AreaDefinition(areaId, "start", "Start", [entity]);
        return new ProjectState(new ProjectDefinition(projectId, "Foundation", [blueprint], [area]),
            new ProjectRevision(4));
    }
}
