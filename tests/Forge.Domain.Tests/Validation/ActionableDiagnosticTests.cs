using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;

namespace Forge.Domain.Tests.Validation;

public sealed class ActionableDiagnosticTests
{
    [Fact]
    public void Catalog_ProducesStableStructuredDiagnosticWithTransactionalQuickFix()
    {
        var project = CreateProject();

        var first = ProjectDiagnosticCatalog.Build(project)
            .Single(item => item.Code == AreaGeometryValidator.OutOfBoundsCode);
        var second = ProjectDiagnosticCatalog.Build(project)
            .Single(item => item.Code == AreaGeometryValidator.OutOfBoundsCode);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(ActionableDiagnostic.CurrentSchemaVersion, first.SchemaVersion);
        Assert.Equal(DiagnosticSeverity.Error, first.Severity);
        Assert.Equal(Guid.Parse("75000000-0000-4000-8000-000000000004"), first.SubjectUuid);
        Assert.Equal("transform", first.Property);
        Assert.Equal("AreaGeometryValidator", first.Source);
        Assert.Equal("derived-project-diagnostic", first.Provenance);
        var fix = Assert.Single(first.QuickFixes);
        Assert.Equal(DiagnosticQuickFixKind.ClampEntityToArea, fix.Kind);
        Assert.Equal(fix.Id, Assert.Single(second.QuickFixes).Id);
    }

    internal static ProjectDefinition CreateProject()
    {
        var placeable = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("75000000-0000-4000-8000-000000000001")),
            "crate",
            "Crate",
            BlueprintKind.Placeable);
        var spawnBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("75000000-0000-4000-8000-000000000002")),
            "spawn",
            "Spawn",
            BlueprintKind.PlayerSpawn);
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("75000000-0000-4000-8000-000000000003")),
            "area",
            "Area",
            [
                new EntityInstance(
                    EntityId.From(Guid.Parse("75000000-0000-4000-8000-000000000004")),
                    placeable.Id,
                    new TransformValue(
                        new Vector3Value(5, 0, 0),
                        QuaternionValue.Identity,
                        new Vector3Value(2, 1, 2)),
                    ["collision"]),
                new EntityInstance(
                    EntityId.From(Guid.Parse("75000000-0000-4000-8000-000000000005")),
                    spawnBlueprint.Id,
                    TransformValue.Identity),
            ],
            dimensions: new AreaDimensions(10, 10));
        return new ProjectDefinition(
            ProjectId.From(Guid.Parse("75000000-0000-4000-8000-000000000006")),
            "Diagnostics",
            [placeable, spawnBlueprint],
            [area]);
    }
}
