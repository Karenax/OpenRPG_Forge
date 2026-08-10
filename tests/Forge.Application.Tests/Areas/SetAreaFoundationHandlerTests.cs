using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;

namespace Forge.Application.Tests.Areas;

public sealed class SetAreaFoundationHandlerTests
{
    [Fact]
    public void Preview_apply_undo_and_redo_share_the_same_canonical_foundation()
    {
        var context = new PlaceEntityTestContext();
        var original = context.State.Project.Areas.Single().Foundation;
        var updated = Profile(context.AreaId, "crypt-kit", "underground", "scripts/crypt_enter.forge");
        var command = new SetAreaFoundationCommand(CommandId.From(Guid.Parse("a0281000-0000-4000-8000-000000000001")),
            context.State.Revision, context.AreaId, updated, false);
        var handler = new SetAreaFoundationHandler(new FixedClock(PlaceEntityTestContext.Timestamp));

        var preview = handler.Handle(context.State, command with { DryRun = true });
        var applied = handler.Handle(context.State, command);
        var history = new ProjectCommandHistory(context.State);
        history.Record(applied);

        Assert.True(preview.Succeeded);
        Assert.Equal(context.State, preview.State);
        Assert.False(preview.Audit.Applied);
        Assert.Equal(updated, history.State.Project.Areas.Single().Foundation);
        Assert.True(history.Undo().Succeeded);
        Assert.Equal(original, history.State.Project.Areas.Single().Foundation);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(updated, history.State.Project.Areas.Single().Foundation);
    }

    [Fact]
    public void Script_snapshot_reads_typed_values_without_exposing_files_or_engine_nodes()
    {
        var context = new PlaceEntityTestContext();
        var area = context.State.Project.Areas.Single().WithFoundation(
            Profile(context.AreaId, "forest-kit", "temperate", "scripts/forest_enter.forge"));

        var snapshot = AreaFoundationScriptService.Read(area);

        Assert.Equal(context.AreaId, snapshot.AreaId);
        Assert.Equal("forest-kit", snapshot.KitKey);
        Assert.Equal("temperate", snapshot.Biome);
        Assert.Equal("scripts/forest_enter.forge", snapshot.EnteredScript);
        Assert.Equal(768, snapshot.RuntimeBudgetMegabytes);
    }

    [Fact]
    public void Required_unsafe_script_is_rejected_before_mutation_with_a_localized_diagnostic()
    {
        var context = new PlaceEntityTestContext();
        var invalid = Profile(context.AreaId, "crypt-kit", "underground", "../escape.forge");
        var handler = new SetAreaFoundationHandler(new FixedClock(PlaceEntityTestContext.Timestamp));

        var result = handler.Handle(context.State, new(CommandId.From(Guid.NewGuid()), context.State.Revision,
            context.AreaId, invalid, false));

        Assert.False(result.Succeeded);
        Assert.Equal(AreaFoundationValidator.LifecycleCode, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(context.State, result.State);
    }

    private static AreaFoundationProfile Profile(AreaId areaId, string kit, string biome, string enteredScript)
    {
        var defaults = AreaFoundationProfile.CreateDefault(areaId);
        return new(defaults.Id, 1, "author:r028-test",
            new("playable", kit, "flat", "interior-neutral", "walk-medium", "single-area", biome, 42, 768),
            new(defaults.TileKit.Id, 1, "author:r028-test", 2, [], [], [], [], []),
            new(new(Guid.Parse("a0281000-0000-4000-8000-000000000010"), "area.entered", enteredScript,
                "1.0.0", true, "author:r028-test"), null), [], []);
    }
}
