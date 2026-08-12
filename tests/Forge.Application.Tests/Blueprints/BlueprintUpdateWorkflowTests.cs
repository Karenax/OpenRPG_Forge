using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Blueprints;

public sealed class BlueprintUpdateWorkflowTests
{
    [Fact]
    public void PreviewApplyPinsExclusionsAndHistoryPreserveCanonicalInstances()
    {
        var context = new PlaceEntityTestContext();
        var sourceId = EntityId.From(Guid.Parse("4a7753a5-f16d-4e45-9320-d657e64f3d56"));
        var exactId = EntityId.From(Guid.Parse("4a7753a5-f16d-4e45-9320-d657e64f3d57"));
        var excludedId = EntityId.From(Guid.Parse("4a7753a5-f16d-4e45-9320-d657e64f3d58"));
        var pinnedId = EntityId.From(Guid.Parse("4a7753a5-f16d-4e45-9320-d657e64f3d59"));
        var overrides = new EntityPropertyOverrides(1,
            [new EntityPropertyOverride("visual.scale", EntityPropertyValueKind.Number, "2")]);
        EntityInstance Entity(EntityId id, string version, EntityPropertyOverrides? values = null) =>
            new(id, context.BlueprintId, TransformValue.Identity, overrides: values, blueprintVersionRequirement: version);
        var area = context.State.Project.Areas[0].WithEntities([
            Entity(sourceId, "=1.0.0", overrides), Entity(exactId, "=1.0.0"),
            Entity(excludedId, "=1.0.0"), Entity(pinnedId, "=0.9.0")]);
        var project = new ProjectDefinition(context.State.Project.Id, context.State.Project.Title,
            context.State.Project.Module, context.State.Project.Blueprints, [area]);
        var state = new ProjectState(project, context.State.Revision);
        var handler = new UpdateBlueprintFromEntityHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        var command = new UpdateBlueprintFromEntityCommand(CommandId.From(Guid.NewGuid()), state.Revision,
            context.BlueprintId, sourceId, ["visual.scale"], BlueprintVersionBump.Minor, [excludedId], true,
            ["Monde/Créatures/Gardiens"]);

        var plan = BlueprintUpdatePlanner.Build(project, context.BlueprintId, sourceId, ["visual.scale"],
            BlueprintVersionBump.Minor, [excludedId]);
        var preview = handler.Handle(state, command);

        Assert.True(preview.Succeeded);
        Assert.Equal("1.1.0", plan.TargetVersion);
        Assert.Contains(plan.Instances, item => item.EntityId == excludedId && item.Action == "excluded");
        Assert.Contains(plan.Instances, item => item.EntityId == pinnedId && item.Action == "pinned-incompatible");
        Assert.Same(project, preview.State.Project);

        var applied = handler.Handle(state, command with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false });
        Assert.True(applied.Succeeded);
        var updated = applied.State.Project.Blueprints.Single();
        Assert.Equal("1.1.0", updated.Authoring.Version);
        Assert.Equal("visual.scale", Assert.Single(updated.Authoring.PromotedDefaults).Key);
        Assert.Equal(["Monde/Créatures/Gardiens"], updated.Authoring.CategoryPaths);
        Assert.Equal("=1.1.0", applied.State.Project.Areas[0].Entities.Single(item => item.Id == exactId)
            .BlueprintVersionRequirement);
        Assert.Equal("=1.0.0", applied.State.Project.Areas[0].Entities.Single(item => item.Id == excludedId)
            .BlueprintVersionRequirement);
        Assert.Empty(applied.State.Project.Areas[0].Entities.Single(item => item.Id == exactId).Overrides.Properties);

        var history = new ProjectCommandHistory(state);
        history.Record(applied);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal("1.0.0", history.State.Project.Blueprints.Single().Authoring.Version);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal("1.1.0", history.State.Project.Blueprints.Single().Authoring.Version);
    }

    [Fact]
    public void ScriptHandleResolvesPromotedDefaultsOverridesAndOptionalRequiredFallback()
    {
        var context = new PlaceEntityTestContext();
        var entityId = EntityId.From(Guid.NewGuid());
        var authoring = new Forge.Domain.Blueprints.BlueprintAuthoringMetadata(1, "1.0.0", "project.warden",
            "content/blueprints/warden.json", tags: ["warden"], categoryPaths: ["Créatures/Gardiens"],
            promotedDefaults: [new EntityPropertyOverride("visual.scale", EntityPropertyValueKind.Number, "2")]);
        var blueprint = context.State.Project.Blueprints[0].WithPaletteUpdate(authoring,
            context.State.Project.Blueprints[0].Provenance);
        var entity = new EntityInstance(entityId, blueprint.Id, TransformValue.Identity,
            overrides: new EntityPropertyOverrides(1,
                [new EntityPropertyOverride("visual.scale", EntityPropertyValueKind.Number, "3")]));
        var project = new ProjectDefinition(context.State.Project.Id, context.State.Project.Title,
            context.State.Project.Module, [blueprint], [context.State.Project.Areas[0].WithEntities([entity])]);
        var service = new BlueprintPaletteScriptService(project);

        Assert.Equal(blueprint.Id, Assert.Single(service.Query("gardiens")).BlueprintId);
        Assert.Equal("3", Assert.Single(service.Resolve(entityId, true).EffectiveProperties).Value);
        Assert.Empty(service.Resolve(EntityId.From(Guid.NewGuid()), false).Diagnostics);
        Assert.Contains(BlueprintPaletteScriptService.RequiredBlueprintMissingCode,
            Assert.Single(service.Resolve(EntityId.From(Guid.NewGuid()), true).Diagnostics));
    }
}
