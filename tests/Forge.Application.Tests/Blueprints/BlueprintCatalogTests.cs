using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Tests.Entities;
using Forge.Domain.Blueprints;

namespace Forge.Application.Tests.Blueprints;

public sealed class BlueprintCatalogTests
{
    private static readonly BlueprintId NewBlueprintId = BlueprintId.From(Guid.Parse("a0000000-0000-4000-8000-000000000001"));

    [Fact]
    public void UpsertDryRunProducesSemanticPatchWithoutMutation()
    {
        var context = new EntityMutationTestContext();
        var handler = new UpsertBlueprintHandler(new FixedClock(EntityMutationTestContext.Timestamp));

        var result = handler.Handle(context.State, CreateUpsert(context, dryRun: true));

        Assert.True(result.Succeeded);
        Assert.False(result.Audit.Applied);
        Assert.Null(result.NewRevision);
        Assert.Same(context.State, result.State);
        Assert.Equal("add", result.Patch!.Operation);
        Assert.Equal($"blueprints/{NewBlueprintId}", result.Patch.Path);
    }

    [Fact]
    public void UpsertCreatesEditableBlueprintAndHistoryRestoresProject()
    {
        var context = new EntityMutationTestContext();
        var handler = new UpsertBlueprintHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var history = new ProjectCommandHistory(context.State);

        var result = handler.Handle(history.State, CreateUpsert(context));
        history.Record(result);

        var created = Assert.Single(history.State.Project.Blueprints, blueprint => blueprint.Id == NewBlueprintId);
        Assert.Equal("broken-pillar", created.Slug);
        Assert.Equal(BlueprintKind.Placeable, created.Kind);
        Assert.True(history.Undo().Succeeded);
        Assert.DoesNotContain(history.State.Project.Blueprints, blueprint => blueprint.Id == NewBlueprintId);
        Assert.True(history.Redo().Succeeded);
        Assert.Contains(history.State.Project.Blueprints, blueprint => blueprint.Id == NewBlueprintId);
    }

    [Fact]
    public void UpsertRejectsDuplicateAndNonCanonicalSlugs()
    {
        var context = new EntityMutationTestContext();
        var handler = new UpsertBlueprintHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var duplicate = CreateUpsert(context) with { Slug = "ash-pillar" };
        var invalid = CreateUpsert(context) with { Slug = "Broken Pillar" };

        Assert.Equal(UpsertBlueprintHandler.DuplicateSlugCode, Assert.Single(handler.Handle(context.State, duplicate).Diagnostics).Code);
        Assert.Equal(UpsertBlueprintHandler.InvalidSlugCode, Assert.Single(handler.Handle(context.State, invalid).Diagnostics).Code);
    }

    [Fact]
    public void CreateFromEntityClonesDefinitionAndRetargetsOnlySelectedInstance()
    {
        var context = new EntityMutationTestContext();
        var handler = new CreateBlueprintFromEntityHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var command = CreateFromEntity(context);

        var result = handler.Handle(context.State, command);

        Assert.True(result.Succeeded);
        var derived = Assert.Single(result.State.Project.Blueprints, blueprint => blueprint.Id == NewBlueprintId);
        Assert.Equal(BlueprintKind.Placeable, derived.Kind);
        var entity = Assert.Single(result.State.Project.Areas.Single().Entities);
        Assert.Equal(NewBlueprintId, entity.BlueprintId);
        Assert.Equal(context.State.Project.Areas.Single().Entities.Single().Overrides, entity.Overrides);
        Assert.Equal(2, result.State.Project.Blueprints.Count);
        Assert.Equal("add", result.Patch!.Operation);
    }

    [Fact]
    public void CreateFromEntitySupportsDryRunAndRejectsUnknownEntity()
    {
        var context = new EntityMutationTestContext();
        var handler = new CreateBlueprintFromEntityHandler(new FixedClock(EntityMutationTestContext.Timestamp));
        var dryRun = CreateFromEntity(context) with { DryRun = true };
        var missing = CreateFromEntity(context) with
        {
            EntityId = Forge.Domain.Entities.EntityId.From(Guid.Parse("b0000000-0000-4000-8000-000000000001")),
        };

        var preview = handler.Handle(context.State, dryRun);
        Assert.True(preview.Succeeded);
        Assert.Same(context.State, preview.State);
        Assert.False(preview.Audit.Applied);
        Assert.Equal(CreateBlueprintFromEntityHandler.MissingEntityCode, Assert.Single(handler.Handle(context.State, missing).Diagnostics).Code);
    }

    [Fact]
    public void ReferenceIndexRebuildsEntityReferencesFromCanonicalProject()
    {
        var context = new EntityMutationTestContext();

        var references = BlueprintReferenceIndex.Build(context.State.Project).Find(context.BlueprintId);

        var reference = Assert.Single(references);
        Assert.Equal(BlueprintReferenceKind.EntityInstance, reference.Kind);
        Assert.Equal(context.EntityId.ToString(), reference.SourceId);
        Assert.Equal($"areas/{context.AreaId}/entities/{context.EntityId}/blueprintId", reference.DocumentPath);
        Assert.Empty(BlueprintReferenceIndex.Build(context.State.Project).Find(NewBlueprintId));
    }

    private static UpsertBlueprintCommand CreateUpsert(EntityMutationTestContext context, bool dryRun = false) =>
        new(
            CommandId.From(Guid.Parse("a1000000-0000-4000-8000-000000000001")),
            context.State.Revision,
            NewBlueprintId,
            "broken-pillar",
            "Pilier brisé",
            BlueprintKind.Placeable,
            dryRun);

    private static CreateBlueprintFromEntityCommand CreateFromEntity(EntityMutationTestContext context) =>
        new(
            CommandId.From(Guid.Parse("a2000000-0000-4000-8000-000000000001")),
            context.State.Revision,
            context.AreaId,
            context.EntityId,
            NewBlueprintId,
            "broken-pillar",
            "Pilier brisé",
            DryRun: false);
}
