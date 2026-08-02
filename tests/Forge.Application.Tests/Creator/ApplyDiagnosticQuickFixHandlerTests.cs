using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Application.Tests.Creator;

public sealed class ApplyDiagnosticQuickFixHandlerTests
{
    [Fact]
    public void ClampFix_SupportsDryRunApplyUndoRedoAndRejectsStaleDiagnostic()
    {
        var project = CreateOutOfBoundsProject();
        var state = new ProjectState(project, new ProjectRevision(8));
        var diagnostic = ProjectDiagnosticCatalog.Build(project)
            .Single(item => item.Code == AreaGeometryValidator.OutOfBoundsCode);
        var fix = Assert.Single(diagnostic.QuickFixes);
        var command = new ApplyDiagnosticQuickFixCommand(
            CommandId.From(Guid.Parse("76000000-0000-4000-8000-000000000001")),
            state.Revision,
            diagnostic.Id,
            fix.Id,
            false);
        var handler = new ApplyDiagnosticQuickFixHandler(new FixedClock(
            new DateTimeOffset(2026, 8, 2, 13, 0, 0, TimeSpan.Zero)));

        var preview = handler.Handle(state, command with { DryRun = true });
        var applied = handler.Handle(state, command);
        var history = new ProjectCommandHistory(state);
        history.Record(applied);

        Assert.True(preview.Succeeded);
        Assert.Same(state, preview.State);
        Assert.False(preview.Audit.Applied);
        Assert.Equal(4, applied.State.Project.Areas[0].Entities[0].Transform.Position.X);
        Assert.DoesNotContain(ProjectDiagnosticCatalog.Build(applied.State.Project), item => item.Id == diagnostic.Id);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(5, history.State.Project.Areas[0].Entities[0].Transform.Position.X);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(4, history.State.Project.Areas[0].Entities[0].Transform.Position.X);

        var missing = handler.Handle(
            state,
            command with { DiagnosticId = DiagnosticId.From(Guid.Parse("76000000-0000-4000-8000-000000000099")) });
        Assert.Equal(ApplyDiagnosticQuickFixHandler.MissingDiagnosticCode, Assert.Single(missing.Diagnostics).Code);
    }

    [Fact]
    public void SpawnFix_AddsDeterministicPlayerSpawn()
    {
        var spawnBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("76000000-0000-4000-8000-000000000011")),
            "spawn",
            "Spawn",
            BlueprintKind.PlayerSpawn);
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("76000000-0000-4000-8000-000000000012")),
            "empty",
            "Empty",
            []);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("76000000-0000-4000-8000-000000000013")),
            "Spawn fix",
            [spawnBlueprint],
            [area]);
        var state = new ProjectState(project, new ProjectRevision(2));
        var diagnostic = ProjectDiagnosticCatalog.Build(project)
            .Single(item => item.Code == AreaNavigationValidator.MissingSpawnCode);
        var fix = Assert.Single(diagnostic.QuickFixes);
        var handler = new ApplyDiagnosticQuickFixHandler(new FixedClock(DateTimeOffset.UnixEpoch));
        var command = new ApplyDiagnosticQuickFixCommand(
            CommandId.From(Guid.Parse("76000000-0000-4000-8000-000000000014")),
            state.Revision,
            diagnostic.Id,
            fix.Id,
            false);

        var first = handler.Handle(state, command);
        var second = handler.Handle(state, command);

        Assert.True(first.Succeeded);
        Assert.Equal(BlueprintKind.PlayerSpawn, spawnBlueprint.Kind);
        Assert.Single(first.State.Project.Areas[0].Entities);
        Assert.Equal(
            first.State.Project.Areas[0].Entities[0].Id,
            second.State.Project.Areas[0].Entities[0].Id);
        Assert.DoesNotContain(ProjectDiagnosticCatalog.Build(first.State.Project),
            item => item.Code == AreaNavigationValidator.MissingSpawnCode);
    }

    private static ProjectDefinition CreateOutOfBoundsProject()
    {
        var placeable = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("76000000-0000-4000-8000-000000000021")),
            "crate",
            "Crate",
            BlueprintKind.Placeable);
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("76000000-0000-4000-8000-000000000022")),
            "area",
            "Area",
            [
                new EntityInstance(
                    EntityId.From(Guid.Parse("76000000-0000-4000-8000-000000000023")),
                    placeable.Id,
                    new TransformValue(
                        new Vector3Value(5, 0, 0),
                        QuaternionValue.Identity,
                        new Vector3Value(2, 1, 2)),
                    ["collision"]),
            ],
            dimensions: new AreaDimensions(10, 10));
        return new ProjectDefinition(
            ProjectId.From(Guid.Parse("76000000-0000-4000-8000-000000000024")),
            "Clamp fix",
            [placeable],
            [area]);
    }
}
