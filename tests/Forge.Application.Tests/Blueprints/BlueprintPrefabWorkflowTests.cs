using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Blueprints;

public sealed class BlueprintPrefabWorkflowTests
{
    [Fact]
    public void ConfigurePreviewApplyHistoryRuntimeAndGroupPlacementShareCanonicalProfiles()
    {
        var context = new PlaceEntityTestContext();
        var baseBlueprint = context.State.Project.Blueprints.Single();
        var target = new BlueprintDefinition(BlueprintId.From(Guid.Parse("b0580000-0000-4000-8000-000000000101")),
            "warden-ember", "Warden Ember", baseBlueprint.Kind);
        var project = new ProjectDefinition(context.State.Project.Id, context.State.Project.Title,
            context.State.Project.Module, [baseBlueprint, target], context.State.Project.Areas);
        var state = new ProjectState(project, context.State.Revision);
        var nodeId = Guid.Parse("b0580000-0000-4000-8000-000000000102");
        var variant = new PrefabVariantDefinition(Guid.NewGuid(), 1, "test:r058", baseBlueprint.Id,
            [new(PrefabVariantOverrideKind.Component, "visual.emissive", "true"),
             new(PrefabVariantOverrideKind.Material, "body", "asset:ember")], true);
        var composition = new PrefabCompositionDefinition(Guid.NewGuid(), 1, "test:r058",
            [new(nodeId, baseBlueprint.Id, null, "floor", new TransformValue(new Vector3Value(2, 0, 3),
                QuaternionValue.Identity, Vector3Value.One))], ["socket-compatible"], ["intensity"],
            ["scripts/assemble_ember.as"], true);
        var budget = new BlueprintAssetBudgetProfile(Guid.NewGuid(), 1, "test:r058",
            new(10_000, 2, 3, 4_000_000, 1, 2, 1),
            [new(AssetTargetPlatform.Windows, 20_000, 4, 6, 8_000_000, 2, 3, 2)], true);
        var configure = new ConfigureBlueprintPrefabCommand(CommandId.From(Guid.NewGuid()), state.Revision,
            target.Id, variant, composition, budget, ["Crypt/Encounter", "Bosses"], true);
        var handler = new ConfigureBlueprintPrefabHandler(new FixedClock(PlaceEntityTestContext.Timestamp));

        var preview = handler.Handle(state, configure);
        Assert.True(preview.Succeeded);
        Assert.Same(project, preview.State.Project);
        Assert.Contains("variant=True", preview.Patch!.Summary, StringComparison.Ordinal);

        var applied = handler.Handle(state, configure with { DryRun = false, CommandId = CommandId.From(Guid.NewGuid()) });
        Assert.True(applied.Succeeded);
        var configured = applied.State.Project.Blueprints.Single(item => item.Id == target.Id);
        Assert.Equal(baseBlueprint.Id, configured.Authoring.PrefabVariant!.BaseBlueprintId);
        Assert.Single(configured.Authoring.PrefabComposition!.Nodes);
        Assert.Equal(AssetTargetPlatform.Windows, Assert.Single(configured.Authoring.AssetBudget!.Platforms).Platform);
        Assert.Equal(["Bosses", "Crypt/Encounter"], configured.Authoring.AuthorCollections);

        var history = new ProjectCommandHistory(state);
        history.Record(applied);
        Assert.True(history.Undo().Succeeded);
        Assert.Null(history.State.Project.Blueprints.Single(item => item.Id == target.Id).Authoring.PrefabVariant);
        Assert.True(history.Redo().Succeeded);

        var runtime = new BlueprintPrefabRuntimeService(applied.State.Project);
        Assert.Equal(2, runtime.ResolveVariant(target.Id, true).EffectiveOverrides.Count);
        Assert.Single(runtime.ResolveComposition(target.Id, true).Nodes);
        Assert.Equal(AssetBudgetEvaluationStatus.Passed,
            Assert.Single(runtime.EvaluateBudget(target.Id, true)).Status);
        Assert.Empty(runtime.ResolveVariant(baseBlueprint.Id, false).Diagnostics);
        Assert.Contains(BlueprintPrefabRuntimeService.RequiredVariantMissingCode,
            Assert.Single(runtime.ResolveVariant(baseBlueprint.Id, true).Diagnostics));

        var placement = new PlacePrefabCompositionCommand(CommandId.From(Guid.NewGuid()), applied.State.Revision,
            target.Id, applied.State.Project.Areas.Single().Id, new Vector3Value(10, 0, 20), true);
        var placementHandler = new PlacePrefabCompositionHandler(new FixedClock(PlaceEntityTestContext.Timestamp));
        Assert.True(placementHandler.Handle(applied.State, placement).Succeeded);
        var placed = placementHandler.Handle(applied.State,
            placement with { DryRun = false, CommandId = CommandId.From(Guid.NewGuid()) });
        Assert.True(placed.Succeeded);
        var added = Assert.Single(placed.State.Project.Areas.Single().Entities, item =>
            item.Tags.Contains($"composition:{target.Id}", StringComparer.Ordinal));
        Assert.Equal(new Vector3Value(12, 0, 23), added.Transform.Position);
    }

    [Fact]
    public void InvalidGateBudgetIsRejectedBeforeMutationWithStablePath()
    {
        var context = new PlaceEntityTestContext();
        var target = context.State.Project.Blueprints.Single();
        var invalidBudget = new BlueprintAssetBudgetProfile(Guid.NewGuid(), 1, "test:r058",
            new(50_000, 5, 5, 20_000_000, 4, 4, 4),
            [new(AssetTargetPlatform.Windows, 10_000, 2, 2, 4_000_000, 1, 2, 1)]);
        var command = new ConfigureBlueprintPrefabCommand(CommandId.From(Guid.NewGuid()), context.State.Revision,
            target.Id, null, null, invalidBudget, [], false);

        var result = new ConfigureBlueprintPrefabHandler(new FixedClock(PlaceEntityTestContext.Timestamp))
            .Handle(context.State, command);

        Assert.False(result.Succeeded);
        Assert.Equal(Forge.Domain.Validation.BlueprintPrefabValidator.AssetBudgetExceededCode,
            Assert.Single(result.Diagnostics).Code);
        Assert.Contains("Windows", Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
        Assert.Same(context.State.Project, result.State.Project);
    }
}
